using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;

namespace Server
{
    internal class QuizRoom
    {
        public Socket Player1 { get; set; }
        public Socket Player2 { get; set; }

        public Dictionary<Socket, int> Scores { get; } = new Dictionary<Socket, int>();
        public Dictionary<Socket, char?> Answers { get; } = new Dictionary<Socket, char?>();
        public Dictionary<Socket, System.TimeSpan> AnswerTimes { get; } = new Dictionary<Socket, System.TimeSpan>();
        public Dictionary<Socket, string> Names { get; } = new Dictionary<Socket, string>();

        public int CurrentIndex { get; set; } = -1;
        public Stopwatch RoundWatch { get; } = new Stopwatch();

        public List<Question> Questions { get; set; } = new List<Question>();
        public object LockObj { get; } = new object();

        public void Init()
        {
            Scores[Player1] = 0;
            Scores[Player2] = 0;

            Answers[Player1] = null;
            Answers[Player2] = null;

            AnswerTimes[Player1] = System.TimeSpan.MaxValue;
            AnswerTimes[Player2] = System.TimeSpan.MaxValue;
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
