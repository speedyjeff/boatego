using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BoatEgo
{
    class Computer : IOpponent
    {
        public Computer()
        {
            Rand = new Random();

            // load the placement map from disk
            if (File.Exists(PlacementMatrixPath))
            {
                // load from file
                var json = File.ReadAllText(PlacementMatrixPath);
                PlacementMatrix = Newtonsoft.Json.JsonConvert.DeserializeObject<int[][]>(json);
            }
            else
            {
                // create the matrix (pieces x cells)
                PlacementMatrix = new int[BoatEgoBoard.NumberOfPieces][];
                for(int r=0; r<PlacementMatrix.Length; r++)
                {
                    PlacementMatrix[r] = new int[BoatEgoBoard.GamePlayRows * BoatEgoBoard.GamePlayCols];
                }
            }
        }

        public BoatEgoBoardView StartingPositions(BoatEgoBoardView board)
        {
            // fill the positions with the available pieces

            // take each piece and apply to board
            var comparer = new SortPiecesComparer(false /*do not use value*/);
            foreach(var kvp in board.PieceCounts.OrderByDescending(pc => pc, comparer))
            {
                var piece = kvp.Key;
                var count = kvp.Value;
                foreach(var index in GetPrioritizedIndexes(PlacementMatrix[(int)piece]))
                {
                    if (count == 0) break;

                    // place this piece at this index
                    var row = index / BoatEgoBoard.GamePlayCols;
                    var col = index % BoatEgoBoard.GamePlayCols;

                    if (board.GetState(row, col) == ViewState.Open)
                    {
                        board.PutPiece(row, col, piece);
                        // decrement
                        count--;
                    }
                }
                if (count != 0) throw new Exception("Failed to place piece : " + piece);
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

        public void Feedback_Battle(CellState attaker, CellState attacked, BattleOutcome outcome)
        {
        }

        #region private
        private Random Rand;
        private string PlacementMatrixPath = @"computer.json";
        private int[][] PlacementMatrix;

        internal void Feedback(BoatEgoBoardView view)
        {
            // add these to our current tracking file to help train the computer
            // only consider the rows where the Player pieces are placed
            for(int r = 0; r < BoatEgoBoard.GamePlayRows; r++)
            {
                for(int c = 0; c < view.Columns; c++)
                {
                    // consider the view upside down
                    if (view.GetState((view.Rows - r - 1), c) != ViewState.Enemy) throw new Exception("Not considering the right row");

                    var piece = view.GetPiece((view.Rows - r - 1), c);
                    var index = (r * view.Columns) + c;
                    // increment this piece as seen at this row,column 
                    PlacementMatrix[(int)piece][index]++;
                }
            }

            // serialize to disk
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(PlacementMatrix);
            File.WriteAllText(PlacementMatrixPath, json);
        }

        private IEnumerable<int> GetPrioritizedIndexes(int[] counts)
        {
            var comparer = new SortPiecesComparer(true /*use value*/);

            // counts represents counts of all the pieces (the index is the index)
            // return a descending sorted list of pieces
            var pieces = new Dictionary<int, int>();
            for (int i = 0; i < counts.Length; i++) pieces.Add(i, counts[i]);
            foreach (var kvp in pieces.OrderByDescending(p => p.Value))
            {
                yield return kvp.Key;
            }
        }
        #endregion
    }

    class SortPiecesComparer : IComparer<KeyValuePair<Piece,int>>
    {
        public SortPiecesComparer(bool useValue)
        {
            UseValueToSort = useValue;
        }

        public int Compare(KeyValuePair<Piece, int> x, KeyValuePair<Piece, int> y)
        {
            if (UseValueToSort)
            {
                // 1  if x > y
                // -1 if x < y
                // 0  if x == y
                if (x.Value > y.Value) return 1;
                else if (x.Value < y.Value) return -1;
                else
                {
                    // they are equal
                    if (x.Key > y.Key) return -1;
                    else if (x.Key < y.Key) return 1;
                }
            }
            else
            {
                // check for equal
                if (x.Key == y.Key) return 0;

                // sort Flag, Spy, _1 first (as they are rare)
                if (x.Key == Piece.Flag) return 1;
                else if (y.Key == Piece.Flag) return -1;
                else if (x.Key == Piece.Spy) return 1;
                else if (y.Key == Piece.Spy) return -1;
                else if (x.Key == Piece._1) return 1;
                else if (y.Key == Piece._1) return -1;

                // then sort by key
                if (x.Key > y.Key) return 1;
                else if (x.Key < y.Key) return -1;
            }

            // they are equal
            return 0;
        }

        #region private
        private bool UseValueToSort;
        #endregion
    }
}
