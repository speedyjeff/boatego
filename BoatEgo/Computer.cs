using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BoatEgo
{
    class Computer : IOpponent
    {
        public Computer(Player player)
        {
            Rand = new Random();
            Me = player;

            // create the guess board
            PieceGuessBoard = new Piece[BoatEgoBoard.BoardRows][];
            for(int r = 0; r<PieceGuessBoard.Length; r++)
            {
                PieceGuessBoard[r] = new Piece[BoatEgoBoard.Columns];
                for(int c = 0; c < PieceGuessBoard[r].Length; c++)
                {
                    // assume everthing is a bomb or flag
                    if (r >= (BoatEgoBoard.BoardRows - 3)) PieceGuessBoard[r][c] = Piece.Bomb | Piece.Flag;
                    else PieceGuessBoard[r][c] = Piece.Empty;
                }
            }

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
            // validate that our guess board and the view are in-sync
            // check all the guess spaces coorespond with an enemy in the view
            for(int r = 0; r < view.Rows; r++)
            {
                for(int c = 0; c < view.Columns; c++)
                {
                    var guess = PieceGuessBoard[r][c];
                    var isEnemy = view.GetState(r, c) == ViewState.Enemy;
                    if ((guess == Piece.Empty && isEnemy) ||
                        (guess != Piece.Empty && !isEnemy)) throw new Exception("Invalid guess board");
                }
            }

            // todo
            var moves = view.GetAvailableMoves().ToList();
            return moves[Rand.Next() % moves.Count];
        }

        public void Feedback_OpponentMove(Coord from, Coord to)
        {
            if (from.Row < 0 || from.Row >= PieceGuessBoard.Length ||
                from.Col < 0 || from.Col >= PieceGuessBoard[from.Row].Length ||
                to.Row < 0 || to.Row >= PieceGuessBoard.Length ||
                to.Col < 0 || to.Col >= PieceGuessBoard[to.Row].Length) throw new Exception("Out of the bounds of our guess board");

            // if the move traversed more than 2 spaces, it is a _2
            // else it is not a Bomb or Flag
            var rdelta = Math.Abs(from.Row - to.Row);
            var cdelta = Math.Abs(from.Col - to.Col);
            var guess = Piece.BombSquad | Piece.Spy |
                            Piece._1 | Piece._2 | Piece._4 | Piece._5 | Piece._6 |
                            Piece._7 | Piece._8 | Piece._9 | Piece._10;

            // this is a _2
            if (rdelta > 1 || cdelta > 1) guess = Piece._2;
            // we already know what it is
            if (PieceGuessBoard[from.Row][from.Col] != (Piece.Bomb | Piece.Flag)) guess = PieceGuessBoard[from.Row][from.Col];

            // make the update
            PieceGuessBoard[from.Row][from.Col] = Piece.Empty;
            PieceGuessBoard[to.Row][to.Col] = guess;
        }

        public void Feedback_Battle(CellState attacker, CellState attacked, BattleOutcome outcome)
        {
            if (attacker.Row < 0 || attacker.Row >= PieceGuessBoard.Length ||
                attacker.Col < 0 || attacker.Col >= PieceGuessBoard[attacker.Row].Length ||
                attacked.Row < 0 || attacked.Row >= PieceGuessBoard.Length ||
                attacked.Col < 0 || attacked.Col >= PieceGuessBoard[attacked.Row].Length) throw new Exception("Out of the bounds of our guess board");

            // use this information to update our guess board
            var piece = Piece.Empty;

            // check if we are being attacked
            if (attacker.Player != Me)
            {
                // we are being attacked

                if (PieceGuessBoard[attacker.Row][attacker.Col] == Piece.Empty) throw new Exception("Had an invalid piece for the attacked");

                // remove this piece as it at least has moved
                PieceGuessBoard[attacker.Row][attacker.Col] = Piece.Empty;

                // the piece that is moving into the place would be the attacker
                piece = attacker.Piece;
            }
            else
            {
                // we are attacking

                if (PieceGuessBoard[attacked.Row][attacked.Col] == Piece.Empty) throw new Exception("Had an invalid piece for the attacked");

                // remove the piece from our guess board (we will add it back - if we lost)
                PieceGuessBoard[attacked.Row][attacked.Col] = Piece.Empty;

                piece = attacked.Piece;
            }
 
            if (outcome == BattleOutcome.Loss)
            {
                // we now know what the opponents piece is
                PieceGuessBoard[attacked.Row][attacked.Col] = piece;
            }
        }

        #region private
        private Player Me;
        private Random Rand;
        private string PlacementMatrixPath = @"computer.json";
        private int[][] PlacementMatrix;
        private Piece[][] PieceGuessBoard;

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
