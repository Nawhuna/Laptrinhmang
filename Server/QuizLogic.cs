using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
    internal static class QuizLogic
    {
        private static readonly Random Rng = new Random();

        // Tao cau hoi tinh nham cong/tru, so hang < 50; phep cong khong gioi han ket qua
        private static List<Question> BuildQuestionsArithmetic(int count)
        {
            var list = new List<Question>();
            for (int i = 0; i < count; i++)
            {
                bool isAdd = Rng.Next(2) == 0;

                int a, b, ans;
                if (isAdd)
                {
                    a = Rng.Next(1, 50);      // 1..49
                    b = Rng.Next(1, 50);      // 1..49
                    ans = a + b;              // co the > 50
                }
                else
                {
                    a = Rng.Next(2, 50);      // 2..49
                    b = Rng.Next(1, a);       // 1..a-1 => ket qua duong
                    ans = a - b;
                }

                // Tao 3 dap an sai lech nho quanh ans
                var opts = new HashSet<int> { ans };
                while (opts.Count < 4)
                {
                    int delta = Rng.Next(1, 8); // lech 1..7
                    int candidate = ans + (Rng.Next(2) == 0 ? -delta : delta);
                    if (candidate != ans && candidate > -100 && candidate < 200) // gioi han hop ly
                        opts.Add(candidate);
                }

                var arr = opts.ToList();
                // Tron
                for (int k = 0; k < arr.Count; k++)
                {
                    int j = Rng.Next(k, arr.Count);
                    int tmp = arr[k]; arr[k] = arr[j]; arr[j] = tmp;
                }

                char correctLetter = 'A';
                if (arr[0] == ans) correctLetter = 'A';
                else if (arr[1] == ans) correctLetter = 'B';
                else if (arr[2] == ans) correctLetter = 'C';
                else correctLetter = 'D';

                string text = isAdd
                    ? $"Tinh nhanh: {a} + {b} = ?"
                    : $"Tinh nhanh: {a} - {b} = ?";

                list.Add(new Question
                {
                    Text = text,
                    A = arr[0].ToString(),
                    B = arr[1].ToString(),
                    C = arr[2].ToString(),
                    D = arr[3].ToString(),
                    Correct = correctLetter
                });
            }
            return list;
        }

        public static void StartQuiz(QuizRoom room)
        {
            room.Init();
            room.Questions = BuildQuestionsArithmetic(5); // so cau tuy chinh

            Broadcast(room, "\n=== BAT DAU TRO CHOI ===\nLuat: Moi cau co 10s. Diem bat dau 1000, moi 0.5s tru 50. Nhap A/B/C/D.\n");

            for (int i = 0; i < room.Questions.Count; i++)
            {
                lock (room.LockObj)
                {
                    room.CurrentIndex = i;
                    room.Answers[room.Player1] = null;
                    room.Answers[room.Player2] = null;
                    room.AnswerTimes[room.Player1] = TimeSpan.MaxValue;
                    room.AnswerTimes[room.Player2] = TimeSpan.MaxValue;
                }

                var q = room.Questions[i];
                SendQuestion(room, q, i + 1, room.Questions.Count);

                room.RoundWatch.Restart();
                WaitUntilAnsweredOrTimeout(room, TimeSpan.FromSeconds(10));
                ScoreAndAnnounce(room, q);
                room.RoundWatch.Reset();
            }

            AnnounceWinner(room);
        }

        // Nhan A/B/C/D
        public static void HandleLogic(Socket client, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            char ans = Char.ToUpperInvariant(raw.Trim()[0]);
            if (ans != 'A' && ans != 'B' && ans != 'C' && ans != 'D') return;

            var room = FindRoom(client);
            if (room == null) return;

            lock (room.LockObj)
            {
                if (!room.RoundWatch.IsRunning) return;

                if (room.Answers.ContainsKey(client) && room.Answers[client] == null)
                {
                    room.Answers[client] = ans;
                    room.AnswerTimes[client] = room.RoundWatch.Elapsed; // thoi diem ca nhan
                    SafeSend(client, "Da nhan dap an: " + ans + "\n");
                }
            }
        }

        // ===== Helpers =====

        private static void SendQuestion(QuizRoom room, Question q, int index, int total)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n--- Cau " + index + "/" + total + " ---");
            sb.AppendLine(q.Text);
            sb.AppendLine("A) " + q.A);
            sb.AppendLine("B) " + q.B);
            sb.AppendLine("C) " + q.C);
            sb.AppendLine("D) " + q.D);
            sb.AppendLine("Ban co 10 giay. Go A/B/C/D roi Enter.");
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
            room.RoundWatch.Stop();
        }

        private static int ComputeScore(TimeSpan elapsed, bool isCorrect)
        {
            if (!isCorrect) return 0;
            if (elapsed.TotalSeconds > 10) return 0;

            int steps = (int)Math.Floor(elapsed.TotalMilliseconds / 500.0);
            int score = 1000 - 50 * steps;
            if (score < 0) score = 0;
            return score;
        }

        private static void ScoreAndAnnounce(QuizRoom room, Question q)
        {
            char? a1, a2;
            TimeSpan t1, t2;

            lock (room.LockObj)
            {
                a1 = room.Answers[room.Player1];
                a2 = room.Answers[room.Player2];
                t1 = room.AnswerTimes[room.Player1];
                t2 = room.AnswerTimes[room.Player2];
            }

            int s1 = ComputeScore(t1, a1.HasValue && a1.Value == q.Correct);
            int s2 = ComputeScore(t2, a2.HasValue && a2.Value == q.Correct);

            lock (room.LockObj)
            {
                room.Scores[room.Player1] += s1;
                room.Scores[room.Player2] += s2;
            }

            string name1 = room.Names.ContainsKey(room.Player1) ? room.Names[room.Player1] : "Nguoi choi 1";
            string name2 = room.Names.ContainsKey(room.Player2) ? room.Names[room.Player2] : "Nguoi choi 2";

            var msg = new StringBuilder();
            msg.AppendLine("Ket qua cau: dap an dung = " + q.Correct);
            msg.AppendLine(name1 + " (+" + s1 + ") | " + name2 + " (+" + s2 + ")");
            Broadcast(room, msg.ToString());

            AnnounceLeaderboard(room);
        }

        private static void AnnounceLeaderboard(QuizRoom room)
        {
            string name1 = room.Names.ContainsKey(room.Player1) ? room.Names[room.Player1] : "Nguoi choi 1";
            string name2 = room.Names.ContainsKey(room.Player2) ? room.Names[room.Player2] : "Nguoi choi 2";

            var pairs = new[]
            {
                new { Name = name1, Score = room.Scores[room.Player1] },
                new { Name = name2, Score = room.Scores[room.Player2] }
            }
            .OrderByDescending(p => p.Score)
            .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("=== Bang xep hang hien tai ===");
            for (int i = 0; i < pairs.Count; i++)
                sb.AppendLine((i + 1) + ". " + pairs[i].Name + ": " + pairs[i].Score + " diem");

            Broadcast(room, sb.ToString());
        }

        private static void AnnounceWinner(QuizRoom room)
        {
            string name1 = room.Names.ContainsKey(room.Player1) ? room.Names[room.Player1] : "Nguoi choi 1";
            string name2 = room.Names.ContainsKey(room.Player2) ? room.Names[room.Player2] : "Nguoi choi 2";
            int s1 = room.Scores[room.Player1];
            int s2 = room.Scores[room.Player2];

            string result;
            if (s1 > s2) result = "🏆 " + name1 + " thang voi " + s1 + " diem! " + name2 + ": " + s2 + " diem";
            else if (s2 > s1) result = "🏆 " + name2 + " thang voi " + s2 + " diem! " + name1 + ": " + s1 + " diem";
            else result = "🤝 Hoa! Moi nguoi " + s1 + " diem";

            Broadcast(room, "\n=== TRO CHOI KET THUC ===\n" + result + "\n");
        }

        private static QuizRoom FindRoom(Socket anyPlayer)
        {
            foreach (var r in QuizRoomList.Rooms)
                if (r.Player1 == anyPlayer || r.Player2 == anyPlayer)
                    return r;
            return null; // cho phep null trong C# 7.3
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
            catch { }
        }
    }
}
