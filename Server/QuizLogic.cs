using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    internal class QuizLogic
    {
        // Cau hoi khong dau
        private static readonly (string q, string a)[] Bank = new[]
        {
            ("Thu do cua Viet Nam la gi?", "ha noi"),
            ("2 + 2 = ?", "4"),
            ("Ngon ngu C# do hang nao phat trien?", "microsoft"),
            ("HTTP viet tat cua giao thuc gi (3 chu cai)?", "http"),
            ("So nguyen to nho nhat?", "2"),
        };

        public static void StartQuiz(QuizRoom room)
        {
            room.Score1 = room.Score2 = 0;
            room.CurrentIndex = -1;
            NextQuestion(room);
        }

        private static void NextQuestion(QuizRoom room)
        {
            room.AnsweredThisRound.Clear();
            room.RoundWon = false;
            room.CurrentIndex++;

            if (room.CurrentIndex >= Bank.Length)
            {
                string final = $"KET THUC! Ti so {room.Score1} - {room.Score2}. " +
                               (room.Score1 > room.Score2 ? "Nguoi choi 1 THANG!"
                               : room.Score2 > room.Score1 ? "Nguoi choi 2 THANG!"
                               : "HOA!");
                Broadcast(room, final);
                Broadcast(room, "Game da ket thuc.");
                return;
            }

            var (q, a) = Bank[room.CurrentIndex];
            room.CurrentQuestion = q;
            room.CurrentAnswer = Normalize(a);

            Broadcast(room, $"Cau {room.CurrentIndex + 1}/{Bank.Length}: {q}\n" +
                            $"Nhap dap an va nhan Enter:");
            ShowScore(room);
        }

        public static void HandleLogic(Socket client, string inputRaw)
        {
            QuizRoom room = QuizRoomList.Rooms.FirstOrDefault(r => r.Player1 == client || r.Player2 == client);
            if (room == null) return;
            if (room.CurrentIndex >= Bank.Length) return;

            string input = Normalize(inputRaw);
            if (string.IsNullOrWhiteSpace(input)) return;
            if (room.RoundWon) return;

            room.AnsweredThisRound.Add(client);

            if (input == room.CurrentAnswer)
            {
                room.RoundWon = true;
                if (client == room.Player1) room.Score1++;
                else room.Score2++;

                string who = (client == room.Player1) ? "Nguoi choi 1" : "Nguoi choi 2";
                Broadcast(room, $"{who} tra loi DUNG!");
                ShowScore(room);
                NextQuestion(room);
                return;
            }

            Send(client, "Sai roi!");
            if (room.AnsweredThisRound.Count >= 2 && !room.RoundWon)
            {
                Broadcast(room, $"Het luot! Dap an dung: {room.CurrentAnswer}");
                NextQuestion(room);
            }
        }

        private static string Normalize(string s)
            => (s ?? "").Trim().ToLowerInvariant();

        private static void Send(Socket client, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                client.Send(data);
            }
            catch { }
        }

        private static void Broadcast(QuizRoom room, string message)
        {
            Send(room.Player1, message);
            Send(room.Player2, message);
        }

        private static void ShowScore(QuizRoom room)
        {
            Broadcast(room, $"Ti so: {room.Score1} - {room.Score2}");
        }
    }
}
