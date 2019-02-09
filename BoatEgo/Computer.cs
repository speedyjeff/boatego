using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatEgo
{
    class Computer : IOpponent
    {
        public Computer()
        {
            Rand = new Random();
        }

        public BoatEgoBoardView StartingPositions(BoatEgoBoardView board)
        {
            // fill the positions with the available pieces

            // todo make this real

            // fill in based on what the player did
            for(int r = 0; r < board.Rows; r++)
            {
                for(int c = 0; c <board.Columns; c++)
                {
                    if (board.GetState(r, c) == ViewState.Open)
                    {
                        board.PutPiece(r, c, FullView.GetPiece(board.Rows - r - 1, c));
                    }
                }
            }

            return board;
        }

        public OpponentMove Move(BoatEgoBoardView view)
        {
            // todo
            var moves = view.GetAvailableMoves().ToList();
            return moves[Rand.Next() % moves.Count];
        }

        public void Feedback_OpponentMove(Coord from, Coord to)
        {
        }

        public void Feedback_Battle(Coord attaker, Coord attack, BattleOutcome outcome)
        {
        }

        #region private
        private Random Rand;
        private BoatEgoBoardView FullView;

        internal void Feedback(BoatEgoBoardView view)
        {
            FullView = view;
        }
        #endregion
    }
}
