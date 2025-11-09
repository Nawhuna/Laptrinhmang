using System.Net.Sockets;
using System.Collections.Generic;

namespace Server
{
    public class QuizRoom
    {
        public Socket Player1 { get; set; }
        public Socket Player2 { get; set; }

        public int Score1 { get; set; }
        public int Score2 { get; set; }

        public int CurrentIndex { get; set; } = -1;
        public string CurrentQuestion { get; set; }
        public string CurrentAnswer { get; set; }

        public HashSet<Socket> AnsweredThisRound { get; } = new HashSet<Socket>();
        public bool RoundWon { get; set; } = false;
    }
}
