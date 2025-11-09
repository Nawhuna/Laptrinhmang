using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    internal static class QuizLogic
    {
        // Tạo bộ câu hỏi demo (bạn thay bằng dữ liệu thực nếu muốn)
        private static List<Question> BuildQuestions()
        {
            return new List<Question>
            {
                new Question { Text="Thủ đô Việt Nam?",
                    A="Hà Nội", B="Đà Nẵng", C="Hải Phòng", D="TP.HCM", Correct='A' },
                new Question { Text="2 + 2 = ?",
                    A="3", B="4", C="5", D="22", Correct='B' },
                new Question { Text="Giao thức dùng kết nối tin cậy?",
                    A="UDP", B="TCP", C="ICMP", D="ARP", Correct='B' },
            };
        }

        public static void StartQuiz(QuizRoom room)
        {
            room.Init();
            room.Questions = BuildQuestions();

            Broadcast(room, "\n=== BẮT ĐẦU TRÒ CHƠI ===\nLuật: Mỗi câu có 10s. Điểm bắt đầu 1000, cứ mỗi 0.5s trừ 50. Trả lời A/B/C/D.\n");

            for (int i = 0; i < room.Questions.Count; i++)
            {
                lock (room.LockObj)
                {
                    room.CurrentIndex = i;
                    room.Answers[room.Player1] = null;
                    room.Answers[room.Player2] = null;
                }

                var q = room.Questions[i];
                SendQuestion(room, q, i + 1, room.Questions.Count);

                // Bắt thời gian cho câu này
                room.RoundWatch.Restart();

                // Đợi tối đa 10s, hoặc hết sớm nếu cả 2 đã trả lời
                WaitUntilAnsweredOrTimeout(room, TimeSpan.FromSeconds(10));

                // Tính điểm + công bố BXH
                ScoreAndAnnounce(room, q);

                room.RoundWatch.Reset();
            }

            // Kết thúc game: công bố người thắng
            AnnounceWinner(room);
        }

        /// <summary>
        /// Nhận dữ liệu từ client (Program.HandleClient gọi vào).
        /// Hợp lệ nếu là chuỗi A/B/C/D (không phân biệt hoa thường).
        /// </summary>
        public static void HandleLogic(Socket client, string raw)
        {
            char ans = Char.ToUpperInvariant(raw.Trim().FirstOrDefault());
            if (ans != 'A' && ans != 'B' && ans != 'C' && ans != 'D') return;

            var room = FindRoom(client);
            if (room == null) return;

            lock (room.LockObj)
            {
                // Nếu đã hết giờ thì bỏ qua
                if (!room.RoundWatch.IsRunning) return;

                // Chỉ nhận lần đầu của mỗi người chơi
                if (room.Answers.ContainsKey(client) && room.Answers[client] == null)
                {
                    room.Answers[client] = ans;
                    // Phản hồi đã nhận
                    SafeSend(client, $"Đã nhận đáp án: {ans}\n");
                }
            }
        }

        // ====== Helpers ======

        private static void SendQuestion(QuizRoom room, Question q, int index, int total)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\n--- Câu {index}/{total} ---");
            sb.AppendLine(q.Text);
            sb.AppendLine($"A) {q.A}");
            sb.AppendLine($"B) {q.B}");
            sb.AppendLine($"C) {q.C}");
            sb.AppendLine($"D) {q.D}");
            sb.AppendLine("Bạn có 10 giây. Gõ A/B/C/D rồi Enter.");
            Broadcast(room, sb.ToString());
        }

        private static void WaitUntilAnsweredOrTimeout(QuizRoom room, TimeSpan timeout)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start) < timeout)
            {
                bool allAnswered;
                lock (room.LockObj)
                {
                    allAnswered = room.Answers.Values.All(v => v != null);
                }
                if (allAnswered) break;
                Thread.Sleep(50);
            }
            // Hết vòng
            room.RoundWatch.Stop();
        }

        private static int ComputeScore(TimeSpan elapsed, bool isCorrect)
        {
            if (!isCorrect) return 0;
            if (elapsed.TotalSeconds > 10) return 0;

            // Mỗi 0.5s trừ 50 điểm kể từ 1000
            int steps = (int)Math.Floor(elapsed.TotalMilliseconds / 500.0);
            int score = 1000 - 50 * steps;
            return Math.Max(0, score);
        }

        private static void ScoreAndAnnounce(QuizRoom room, Question q)
        {
            TimeSpan elapsed = room.RoundWatch.Elapsed;

            // Lấy đáp án 2 người
            char? a1, a2;
            lock (room.LockObj)
            {
                a1 = room.Answers[room.Player1];
                a2 = room.Answers[room.Player2];
            }

            // Tính điểm
            int s1 = ComputeScore(elapsed, a1.HasValue && a1.Value == q.Correct);
            int s2 = ComputeScore(elapsed, a2.HasValue && a2.Value == q.Correct);

            lock (room.LockObj)
            {
                room.Scores[room.Player1] += s1;
                room.Scores[room.Player2] += s2;
            }

            // Công bố kết quả câu
            var msg =
                $"Kết quả câu: đáp án đúng = {q.Correct}\n" +
                $"Player1 (+{s1}) | Player2 (+{s2})\n";

            Broadcast(room, msg);

            // Bảng xếp hạng tạm thời
            AnnounceLeaderboard(room);
        }

        private static void AnnounceLeaderboard(QuizRoom room)
        {
            var pairs = new[]
            {
                (Name:"Player1", Score:room.Scores[room.Player1], Sock:room.Player1),
                (Name:"Player2", Score:room.Scores[room.Player2], Sock:room.Player2),
            }
            .OrderByDescending(p => p.Score)
            .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("=== Bảng xếp hạng hiện tại ===");
            for (int i = 0; i < pairs.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {pairs[i].Name}: {pairs[i].Score} điểm");
            }
            Broadcast(room, sb.ToString());
        }

        private static void AnnounceWinner(QuizRoom room)
        {
            int s1 = room.Scores[room.Player1];
            int s2 = room.Scores[room.Player2];

            string result;
            if (s1 > s2) result = $" Player1 thắng với {s1} điểm! Player2: {s2} điểm";
            else if (s2 > s1) result = $" Player2 thắng với {s2} điểm! Player1: {s1} điểm";
            else result = $" Hòa! Cùng {s1} điểm";

            Broadcast(room, "\n=== TRÒ CHƠI KẾT THÚC ===\n" + result + "\n");
        }

        private static QuizRoom FindRoom(Socket anyPlayer)
        {
            return QuizRoomList.Rooms.FirstOrDefault(r => r.Player1 == anyPlayer || r.Player2 == anyPlayer);
        }

        private static void Broadcast(QuizRoom room, string message)
        {
            SafeSend(room.Player1, message);
            SafeSend(room.Player2, message);
        }

        private static void SafeSend(Socket sock, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                sock.Send(data);
            }
            catch { /* bỏ qua lỗi send khi client thoát */ }
        }
    }
}
