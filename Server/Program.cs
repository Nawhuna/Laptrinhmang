using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Server
{
    internal class Program
    {
        private static readonly List<Socket> _clientList = new List<Socket>();
        private static Queue<Socket> _waitingClient = new Queue<Socket>();

        private static readonly Dictionary<Socket, string> _names = new Dictionary<Socket, string>();

        private static string _ipAdress = "127.0.0.1";
        private static int _port = 2121;

        static void Main(string[] args)
        {
            var serverIp = new IPEndPoint(IPAddress.Parse(_ipAdress), _port);
            var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server.Bind(serverIp);
            server.Listen(10);
            Console.WriteLine("Quiz Duel Server dang chay tren port 2121...");

            while (true)
            {
                Socket client = server.Accept();
                lock (_clientList) _clientList.Add(client);

                SendMessage(client, "Chao mung! Vui long nhap ten khong dau roi nhan Enter:");
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

                    string data = Encoding.UTF8.GetString(buffer, 0, bytes).Trim();
                    if (string.IsNullOrEmpty(data)) continue;

                    // Neu chua co ten: tin nhan dau tien duoc coi la ten
                    if (!_names.ContainsKey(client))
                    {
                        string name = ToAsciiNoDiacritics(data).Trim();
                        if (name.Length == 0) name = "Khach";
                        if (name.Length > 20) name = name.Substring(0, 20);
                        _names[client] = name;

                        SendMessage(client, "Xin chao " + name + "!");
                        FindPlayer(client);
                        continue;
                    }

                    // Da co ten -> xu ly dap an
                    QuizLogic.HandleLogic(client, data);
                }
            }
            catch
            {
                try { Console.WriteLine($"{client.RemoteEndPoint} roi phong"); } catch { }
            }
            finally
            {
                lock (_clientList) _clientList.Remove(client);

                // Loai khoi hang doi neu dang cho ghep
                lock (_waitingClient)
                {
                    var list = new List<Socket>(_waitingClient);
                    if (list.Contains(client))
                    {
                        list.Remove(client);
                        _waitingClient = new Queue<Socket>(list);
                    }
                }

                try { client.Shutdown(SocketShutdown.Both); } catch { }
                try { client.Close(); } catch { }
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

                    var newRoom = new QuizRoom
                    {
                        Player1 = opponent,
                        Player2 = client
                    };

                    newRoom.Names[opponent] = _names.ContainsKey(opponent) ? _names[opponent] : "Nguoi choi 1";
                    newRoom.Names[client] = _names.ContainsKey(client) ? _names[client] : "Nguoi choi 2";

                    QuizRoomList.Rooms.Add(newRoom);

                    SendMessage(opponent, "Ghep cap thanh cong!");
                    SendMessage(client, "Ghep cap thanh cong!");
                    SendMessage(opponent, "Luat: Moi cau co 10s de tra loi . Nhap A/B/C/D.");
                    SendMessage(client, "Luat: Moi cau co 10s. Diem bat dau 1000, moi 0.5s tru 50. Nhap A/B/C/D.");

                    QuizLogic.StartQuiz(newRoom);
                }
            }
        }

        private static void SendMessage(Socket sender, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                sender.Send(data);
            }
            catch { }
        }

        // Bo dau TV -> ASCII (khong dau)
        private static string ToAsciiNoDiacritics(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string stFormD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in stFormD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
