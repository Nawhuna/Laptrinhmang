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
            IPEndPoint serverIp = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2121);
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                client.Connect(serverIp);
                Console.WriteLine("Da ket noi toi server Quiz Duel!");

                Thread receiveThread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            byte[] buffer = new byte[1024];
                            int bytes = client.Receive(buffer);
                            if (bytes == 0) break;

                            string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                            Console.WriteLine($"\n{msg}");
                            Console.Write("> ");
                        }
                        catch
                        {
                            Console.WriteLine("\nMat ket noi voi server.");
                            break;
                        }
                    }
                });
                receiveThread.Start();

                while (true)
                {
                    Console.Write("> ");
                    string input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    byte[] data = Encoding.UTF8.GetBytes(input);
                    client.Send(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi ket noi: " + ex.Message);
            }
        }
    }
}
