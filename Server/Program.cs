using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    internal class Program
    {
        private static List<Socket> _clientList = new List<Socket>();
        private static Queue<Socket> _waitingClient = new Queue<Socket>();

        private static string _ipAdress = "127.0.0.1";
        private static int _port = 2121;

        static void Main(string[] args)
        {
            IPEndPoint serverIp = new IPEndPoint(IPAddress.Parse(_ipAdress), _port);
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server.Bind(serverIp);
            server.Listen(10);
            Console.WriteLine("Quiz Duel Server dang chay tren port 2121...");

            while (true)
            {
                Socket client = server.Accept();
                _clientList.Add(client);
                FindPlayer(client);
                Task.Run(() => HandleClient(client));
            }
        }

        private static void HandleClient(Socket client)
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[1024];
                    int bytes = client.Receive(buffer);
                    if (bytes == 0) break;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytes);
                    Console.WriteLine($"[{client.RemoteEndPoint}] -> {data}");
                    QuizLogic.HandleLogic(client, data);
                }
            }
            catch
            {
                Console.WriteLine($"{client.RemoteEndPoint} da roi khoi phong");
            }
            finally
            {
                lock (_clientList) _clientList.Remove(client);
                client.Close();
            }
        }

        private static void FindPlayer(Socket client)
        {
            lock (_waitingClient)
            {
                if (_waitingClient.Count == 0)
                {
                    _waitingClient.Enqueue(client);
                    SendMessage(client, "Dang cho nguoi choi khac...");
                }
                else
                {
                    Socket opponent = _waitingClient.Dequeue();
                    QuizRoom newRoom = new QuizRoom
                    {
                        Player1 = opponent,
                        Player2 = client
                    };
                    QuizRoomList.Rooms.Add(newRoom);

                    SendMessage(opponent, "Ghep cap thanh cong! Ban la Nguoi choi 1.");
                    SendMessage(client, "Ghep cap thanh cong! Ban la Nguoi choi 2.");
                    SendMessage(opponent, "Luat: Ai tra loi dung truoc duoc +1 diem.");
                    SendMessage(client, "Luat: Ai tra loi dung truoc duoc +1 diem.");

                    QuizLogic.StartQuiz(newRoom);
                }
            }
        }

        private static void SendMessage(Socket sender, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            sender.Send(data);
        }
    }
}
