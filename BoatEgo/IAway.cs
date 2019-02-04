using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatEgo
{
    struct AwayMove
    {
        public Coord From;
        public Coord To;
        public Piece Guess;
    }

    interface IAway
    {
        BoatEgoBoardView StartingPositions(BoatEgoBoardView board);

        AwayMove Move(BoatEgoBoardView view);

        void Feedback_OpponentMove(Coord from, Coord to);

        void Feedback_Battle(Coord attaker, Coord attack, BattleOutcome outcome);
    }
}
