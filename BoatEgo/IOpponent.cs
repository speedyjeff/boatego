using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatEgo
{
    struct OpponentMove
    {
        public Coord From;
        public Coord To;
        public Piece Guess;
    }

    interface IOpponent
    {
        BoatEgoBoardView StartingPositions(BoatEgoBoardView board);

        OpponentMove Move(BoatEgoBoardView view);

        void Feedback_OpponentMove(Coord from, Coord to);

        void Feedback_Battle(CellState attaker, CellState attacked, BattleOutcome outcome);
    }
}
