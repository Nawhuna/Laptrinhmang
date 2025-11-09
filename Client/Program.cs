using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var serverIp = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2121);
            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                client.Connect(serverIp);
                Console.WriteLine("Da ket noi toi server Quiz Duel!");
                Console.WriteLine("Meo: Khi co cau hoi, chi nhap A/B/C/D roi Enter. (/quit de thoat)");

                bool awaitingName = true;      // <-- LẦN ĐẦU là NHẬP TÊN
                bool closed = false;

                // Thread nhan tin
                var receiveThread = new Thread(() =>
                {
                    try
                    {
                        var buffer = new byte[4096];
                        while (true)
                        {
                            int bytes = client.Receive(buffer);
                            if (bytes <= 0) break;

                            string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                            Console.WriteLine();
                            Console.WriteLine(msg);
                            Console.Write("> ");
                        }
                    }
                    catch { }
                    finally
                    {
                        closed = true;
                        Console.WriteLine("\nMat ket noi voi server.");
                    }
                });
                receiveThread.IsBackground = true;
                receiveThread.Start();

                // Vong lap nhap
                while (!closed)
                {
                    Console.Write("> ");
                    string input = Console.ReadLine();
                    if (input == null) continue;
                    input = input.Trim();
                    if (input.Length == 0) continue;

                    if (awaitingName)
                    {
                        // GỬI TÊN TỰ DO (không ép A/B/C/D)
                        byte[] data = Encoding.UTF8.GetBytes(input);
                        client.Send(data);
                        awaitingName = false;   // từ sau mới kiểm tra A/B/C/D
                        continue;
                    }

                    if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                    {
                        try { client.Shutdown(SocketShutdown.Both); } catch { }
                        try { client.Close(); } catch { }
                        break;
                    }

                    // Từ đây trở đi chỉ chấp nhận A/B/C/D
                    char first = char.ToUpperInvariant(input[0]);
                    if (first == 'A' || first == 'B' || first == 'C' || first == 'D')
                    {
                        byte[] data = Encoding.UTF8.GetBytes(first.ToString());
                        client.Send(data);
                    }
                    else
                    {
                        Console.WriteLine("Chi chap nhan A/B/C/D (hoac /quit).");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi ket noi: " + ex.Message);
            }
        }
    }
}
