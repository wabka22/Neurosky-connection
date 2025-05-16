using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Globalization;

class MindWaveReader
{
    static SerialPort serialPort;
    static StreamWriter txtWriter;
    static string txtFilePath;

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("MindWave Reader");

        string exePath = AppDomain.CurrentDomain.BaseDirectory;
        string projectRoot = Path.GetFullPath(Path.Combine(exePath, @"..\..\..\..\"));
        string dataFolder = Path.Combine(projectRoot, "data");

        txtFilePath = Path.Combine(dataFolder, $"mindwave_timestamps_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        try
        {
            txtWriter = new StreamWriter(txtFilePath, false, Encoding.UTF8);
            Console.WriteLine($"Timestamps will be saved to: {txtFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating file: {ex.Message}");
            return;
        }

        Console.WriteLine("Available COM ports:");
        string[] ports = SerialPort.GetPortNames();
        foreach (string port in ports)
        {
            Console.WriteLine("  " + port);
        }

        Console.Write("Enter MindWave COM port (e.g., COM3): ");
        string portName = Console.ReadLine();

        serialPort = new SerialPort(portName, 57600);
        serialPort.DataReceived += SerialPort_DataReceived;

        try
        {
            serialPort.Open();
            Console.WriteLine("Connected to MindWave");
            RequestData();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Connection error: " + ex.Message);
            txtWriter?.Close();
            return;
        }

        Console.WriteLine("Press Enter to stop recording...");
        Console.ReadLine();

        serialPort.Close();
        txtWriter.Close();
        Console.WriteLine("Connection closed. Timestamps saved to: " + txtFilePath);
    }

    static void RequestData()
    {
        Console.WriteLine("Attempting to send data request");
        byte[] request = new byte[] { 0xAA };
        serialPort.Write(request, 0, request.Length);
        Console.WriteLine("Data request sent.");
    }

    static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = (SerialPort)sender;
        int bytesToRead = sp.BytesToRead;
        byte[] buffer = new byte[bytesToRead];
        sp.Read(buffer, 0, bytesToRead);

        ParseThinkGearStream(buffer);
    }

    static void ParseThinkGearStream(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            byte code = data[i];

            switch (code)
            {
                case 0x83: // ASIC_EEG_POWER (8x3 bytes)
                    if (i + 24 < data.Length)
                    {
                        Console.WriteLine("[EEG] ASIC_EEG_POWER data received");
                        // Записываем только метку времени
                        WriteTimestampToTxt();
                        i += 24;
                    }
                    break;

                default:
                    Console.WriteLine($"[IGNORED] Received data with code 0x{code:X2}");
                    i += SkipBytesForCode(code, data, i);
                    break;
            }
        }
    }

    static int SkipBytesForCode(byte code, byte[] data, int currentIndex)
    {
        switch (code)
        {
            case 0x02:
            case 0x03:
            case 0x04:
            case 0x05:
            case 0x06:
                return 1;
            case 0x80:
            case 0x86:
                return 2;
            case 0x81:
                return 32;
            case 0x83:
                return 24;
            default:
                return 1;
        }
    }

    static void WriteTimestampToTxt()
    {
        DateTime now = DateTime.Now;
        string timestamp = $"{now.Minute}:{now.Second}.{now.Millisecond}";
        txtWriter.WriteLine(timestamp);
        txtWriter.Flush();
    }
}