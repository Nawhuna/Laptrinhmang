using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Diagnostics;

namespace Server
{
    internal class QuizRoom
    {
        public Socket Player1 { get; set; }
        public Socket Player2 { get; set; }

        // Tổng điểm tích lũy
        public Dictionary<Socket, int> Scores { get; } = new Dictionary<Socket, int>();

        // Vòng hiện tại
        public int CurrentIndex { get; set; } = -1;

        // Thời điểm bắt đầu câu hỏi hiện tại (để tính điểm theo thời gian)
        public Stopwatch RoundWatch { get; } = new Stopwatch();

        // Lưu đáp án người chơi ở câu hiện tại
        public Dictionary<Socket, char?> Answers { get; } = new Dictionary<Socket, char?>();

        // Danh sách câu hỏi
        public List<Question> Questions { get; set; } = new List<Question>();

        public object LockObj { get; } = new object();

        public void Init()
        {
            Scores[Player1] = 0;
            Scores[Player2] = 0;
            Answers[Player1] = null;
            Answers[Player2] = null;
        }
    }

    internal class Question
    {
        public string Text { get; set; } = "";
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public string C { get; set; } = "";
        public string D { get; set; } = "";
        public char Correct { get; set; }  // 'A','B','C','D'
    }
}
