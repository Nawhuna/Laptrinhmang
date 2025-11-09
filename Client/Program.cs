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

                // Thread nhan tin tu server
                var receiveThread = new Thread(() =>
                {
                    try
                    {
                        var buffer = new byte[2048];
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
                    catch
                    {
                        // server dong ket noi
                    }
                    finally
                    {
                        Console.WriteLine("\nMat ket noi voi server.");
                    }
                });
                receiveThread.IsBackground = true;
                receiveThread.Start();

                // Vong lap nhap lenh/tra loi
                while (true)
                {
                    Console.Write("> ");
                    string input = Console.ReadLine();
                    if (input == null) continue;

                    input = input.Trim();
                    if (input.Length == 0) continue;

                    // lenh thoat
                    if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                    {
                        try { client.Shutdown(SocketShutdown.Both); } catch { }
                        client.Close();
                        break;
                    }

                    // Chi gui A/B/C/D: lay ky tu dau, viet hoa
                    char first = char.ToUpperInvariant(input[0]);
                    if (first == 'A' || first == 'B' || first == 'C' || first == 'D')
                    {
                        byte[] data = Encoding.UTF8.GetBytes(first.ToString());
                        client.Send(data);
                    }
                    else
                    {
                        // Khong phai A/B/C/D -> bo qua de tranh gay nhieu
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
