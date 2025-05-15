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

        txtFilePath = Path.Combine(dataFolder, $"mindwave_data_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        try
        {
            txtWriter = new StreamWriter(txtFilePath, false, Encoding.UTF8);
            Console.WriteLine($"Data will be saved to: {txtFilePath}");
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
        Console.WriteLine("Connection closed. Data saved to: " + txtFilePath);
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
            string valueStr = "";

            switch (code)
            {
                case 0x02: // POOR_SIGNAL quality (0-255)
                    if (i + 1 < data.Length)
                    {
                        valueStr = data[i + 1].ToString();
                        Console.WriteLine($"[SIGNAL] 0x{code:X2}: {valueStr}");
                        WriteToTxt(code, valueStr);
                        i++;
                    }
                    break;

                case 0x03: // HEART_RATE (0-255)
                    if (i + 1 < data.Length)
                    {
                        valueStr = data[i + 1].ToString();
                        Console.WriteLine($"[HR] 0x{code:X2}: {valueStr}");
                        WriteToTxt(code, valueStr);
                        i++;
                    }
                    break;

                case 0x04: // ATTENTION eSense (0-100)
                    if (i + 1 < data.Length)
                    {
                        valueStr = data[i + 1].ToString();
                        Console.WriteLine($"[eSense] 0x{code:X2}: {valueStr}");
                        WriteToTxt(code, valueStr);
                        i++;
                    }
                    break;

                case 0x05: // MEDITATION eSense (0-100)
                    if (i + 1 < data.Length)
                    {
                        valueStr = data[i + 1].ToString();
                        Console.WriteLine($"[eSense] 0x{code:X2}: {valueStr}");
                        WriteToTxt(code, valueStr);
                        i++;
                    }
                    break;

                case 0x06: // 8BIT_RAW wave value (0-255)
                    if (i + 1 < data.Length)
                    {
                        valueStr = data[i + 1].ToString();
                        Console.WriteLine($"[EEG] 0x{code:X2}: {valueStr}");
                        WriteToTxt(code, valueStr);
                        i++;
                    }
                    break;

                case 0x07: // RAW_MARKER section start
                    valueStr = "1";
                    Console.WriteLine($"[MARKER] 0x{code:X2}");
                    WriteToTxt(code, valueStr);
                    break;

                case 0x80: // RAW wave value (2 bytes)
                    if (i + 2 < data.Length)
                    {
                        short rawValue = (short)((data[i + 1] << 8) | data[i + 2]);
                        valueStr = rawValue.ToString();
                        Console.WriteLine($"[EEG] 0x{code:X2}: {valueStr}");
                        WriteToTxt(code, valueStr);
                        i += 2;
                    }
                    break;

                case 0x81: // EEG_POWER (8x4 bytes)
                    if (i + 32 < data.Length)
                    {
                        Console.WriteLine("[EEG] EEG_POWER:");
                        for (int j = 0; j < 8; j++)
                        {
                            byte[] floatBytes = new byte[4]
                            {
                                data[i + 1 + j * 4],
                                data[i + 2 + j * 4],
                                data[i + 3 + j * 4],
                                data[i + 4 + j * 4]
                            };

                            if (BitConverter.IsLittleEndian)
                            {
                                Array.Reverse(floatBytes);
                            }

                            float value = BitConverter.ToSingle(floatBytes, 0);
                            valueStr = value.ToString(CultureInfo.InvariantCulture);
                            Console.WriteLine($"  0x{code:X2}: {valueStr}");
                            WriteToTxt(code, valueStr);
                        }
                        i += 32;
                    }
                    break;

                case 0x83: // ASIC_EEG_POWER (8x3 bytes)
                    if (i + 24 < data.Length)
                    {
                        Console.WriteLine("[EEG] ASIC_EEG_POWER:");
                        for (int j = 0; j < 8; j++)
                        {
                            int offset = i + 1 + j * 3;
                            uint value = (uint)((data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2]);
                            valueStr = value.ToString();
                            Console.WriteLine($"  0x{code:X2}: {valueStr}");
                            WriteToTxt(code, valueStr);
                        }
                        i += 24;
                    }
                    break;

                case 0x86: // RRINTERVAL (2 bytes)
                    if (i + 2 < data.Length)
                    {
                        ushort rrInterval = (ushort)((data[i + 1] << 8) | data[i + 2]);
                        valueStr = rrInterval.ToString();
                        Console.WriteLine($"[HR] 0x{code:X2}: {valueStr} ms");
                        WriteToTxt(code, valueStr);
                        i += 2;
                    }
                    break;

                case 0x55: // EXCODE (reserved)
                    valueStr = "1";
                    Console.WriteLine($"[SYSTEM] 0x{code:X2}");
                    WriteToTxt(code, valueStr);
                    break;

                case 0xAA: // SYNC (reserved)
                    valueStr = "1";
                    Console.WriteLine($"[SYSTEM] 0x{code:X2}");
                    WriteToTxt(code, valueStr);
                    break;

                default:
                    valueStr = "1";
                    Console.WriteLine($"[UNKNOWN] 0x{code:X2}");
                    WriteToTxt(code, valueStr);
                    break;
            }
        }
    }

    static void WriteToTxt(byte code, string value)
    {
        string line = $"0x{code:X2},{value}";
        txtWriter.WriteLine(line);
        txtWriter.Flush();
    }
}
