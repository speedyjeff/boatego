using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatEgo
{
    public enum ViewState { Open, Blocked, Enemy, Owned };

    class BoatEgoBoardView
    {
        public BoatEgoBoardView(int rows, int cols, Dictionary<Piece, int> pieces)
        {
            Rows = rows;
            Columns = cols;
            PieceCounts = pieces;

            View = new Details[Rows][];
            for(int r = 0; r<Rows; r++)
            {
                View[r] = new Details[Columns];
                for(int c=0; c<Columns; c++)
                {
                    View[r][c].Piece = Piece.Empty;
                    // set the starting positions (for away)
                    View[r][c].StartingCell = (r >= 0 && r < 3);
                }
            }

            // todo
            IsValid = true;
        }

        public int Rows { get; private set; }
        public int Columns { get; private set; }

        public bool IsForInitialization { get; internal set; }
        public bool IsValid { get; internal set; }

        public Dictionary<Piece, int> PieceCounts;

        public IEnumerable<OpponentMove> GetAvailableMoves()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (View[r][c].Moves != null && View[r][c].Moves.Count > 0)
                    {
                        foreach(var move in View[r][c].Moves)
                        {
                            yield return move;
                        }
                    }
                }
            }
        }

        public ViewState GetState(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns) throw new Exception("Invalid index for view");

            if (View[row][col].IsBlocked) return ViewState.Blocked;
            if (View[row][col].Piece == Piece.Empty &&
                (!IsForInitialization || (IsForInitialization && View[row][col].StartingCell))) return ViewState.Open;
            if (View[row][col].Owned) return ViewState.Owned;

            return ViewState.Enemy;
        }

        public Piece GetPiece(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns) throw new Exception("Invalid index for view");

            return View[row][col].Piece;
        }

        public bool PutPiece(int row, int col, Piece piece)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns) throw new Exception("Invalid index for view");
            if (!IsForInitialization) return false;

            // check that we are in the right zone for initialization
            if (!View[row][col].StartingCell) return false;
            // check that we have not exceeded our piece count
            if (View[row][col].Piece != Piece.Empty)
            {
                PieceCounts[View[row][col].Piece]++;
            }
            if (PieceCounts[piece] <= 0) return false;
            PieceCounts[piece]--;

            // put the piece
            View[row][col].Piece = piece;
            View[row][col].Owned = true;

            return true;
        }

        #region private
        private Details[][] View;
        struct Details
        {
            public bool IsBlocked;
            public Piece Piece;
            public bool Owned;
            public bool StartingCell;
            public List<OpponentMove> Moves;
        }

        internal void PutCell(int row, int col, Piece piece, bool mine, List<OpponentMove> moves)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns) throw new Exception("Invalid index for view");

            View[row][col].Piece = piece;
            View[row][col].Owned = mine;
            View[row][col].Moves = moves;
        }

        internal void BlockCell(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns) throw new Exception("Invalid index for view");

            View[row][col].IsBlocked = true;
        }
        #endregion
    }
}
