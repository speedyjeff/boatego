using BoatEgo.Properties;
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
                    if (r >= (BoatEgoBoard.BoardRows - 3)) PieceGuessBoard[r][c] = UnknownNotMovingPiece;
                    else PieceGuessBoard[r][c] = Piece.Empty;
                }
            }

            var streams = engine.Common.Embedded.LoadResource<byte[]>(System.Reflection.Assembly.GetExecutingAssembly());

            // load the placement map from disk
            if (File.Exists(PlacementMatrixPath))
            {
                // load from file
                var json = File.ReadAllText(PlacementMatrixPath);
                PlacementMatrix = Newtonsoft.Json.JsonConvert.DeserializeObject<int[][]>(json);
            }

            // or from stream
            else if (streams != null && streams.TryGetValue("computer", out byte[] data))
            {
                var json = System.Text.Encoding.Default.GetString(data);
                PlacementMatrix = Newtonsoft.Json.JsonConvert.DeserializeObject<int[][]>(json);
            }

            // or create it
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

            // over time the 'Pick the most used' results in a predictable placement of pieces
            // injecting some randomness

            // take each piece and apply to board
            var comparer = new SortPiecesComparer(false /*do not use value*/);
            foreach(var kvp in board.PieceCounts.OrderByDescending(pc => pc, comparer))
            {
                var piece = kvp.Key;
                var count = kvp.Value;
                var randomizeAttempts = 5;
                var throwNextTime = false;
                do
                {
                    foreach (var index in GetPrioritizedIndexes(PlacementMatrix[(int)piece]))
                    {
                        if (count == 0) break;

                        // place this piece at this index
                        var row = index / BoatEgoBoard.GamePlayCols;
                        var col = index % BoatEgoBoard.GamePlayCols;

                        // randomly skip some of the first choices to add some randomness to placement
                        if (randomizeAttempts-- > 0 && Rand.Next() % 2 == 0) continue;

                        if (board.GetState(row, col) == ViewState.Open)
                        {
                            board.PutPiece(row, col, piece);
                            // decrement
                            count--;
                        }
                    }
                    if (throwNextTime && count != 0) throw new Exception("Failed to place piece : " + piece);
                    throwNextTime = true;
                }
                while (count > 0);
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

            // categorize the moves then pick the one that is most likely to help

            var categories = new Dictionary<MoveCategory, List<OpponentMove>>();
            for (int i = 0; i < (int)MoveCategory.END; i++) categories.Add((MoveCategory)i, new List<OpponentMove>());

            foreach (var move in view.GetAvailableMoves())
            {
                var piece = view.GetPiece(move.From.Row, move.From.Col);

                // will this move result in an attack (that we can win)
                if (IsBattle(move.To, piece /*check if this would win*/))
                {
                    categories[MoveCategory.AttackKnownOpponent].Add(move);
                }

                // are we next to a known enemy that we need to move away from?
                else if (IsAdjacentToKnownGreaterEnemy(move.From, piece) && !IsAdjacentToKnownGreaterEnemy(move.To, piece))
                {
                    categories[MoveCategory.AwayFromKnownOpponent].Add(move);
                }

                // check if this would move towards a battle that we can win (and not adjacent to a known enemy)
                else if (IsTowardsEnemy(move.From, move.To) && !IsAdjacentToKnownGreaterEnemy(move.To, piece))
                {
                    categories[MoveCategory.TowardsKnownOpponent].Add(move);
                }

                // check if we would attack (and we are not an 8+, Spy, 1 or BombSquad - which we should not try to waste)
                else if (IsBattle(move.To, Piece.Empty /* do not require win */) && 
                    piece != Piece.Spy && piece != Piece._1 && piece != Piece.BombSquad &&
                    piece < Piece._8)
                {
                    categories[MoveCategory.OtherAttack].Add(move);
                }

                // check if this would move us towards the enemy
                else if (IsMove(move.From, move.To, 1, 0))
                {
                    categories[MoveCategory.ForwardMove].Add(move);
                }

                // check if this would move us backwards
                else if (IsMove(move.From, move.To, -1, 0))
                {
                    categories[MoveCategory.BackwardMove].Add(move);
                }

                // else other
                else
                {
                    categories[MoveCategory.Other].Add(move);
                }
            }

            // iterate through the available moves (in priority order) and choose one
            for (int i = 0; i < (int)MoveCategory.END; i++)
            {
                List<OpponentMove> moves = null;
                if (categories.TryGetValue((MoveCategory)i, out moves))
                {
                    if (moves.Count > 0)
                    {
                        // todo improve (better than random)
                        var move = moves[Rand.Next() % moves.Count];
                        var piece = view.GetPiece(move.From.Row, move.From.Col);
                        var guess = Piece.Empty;

                        // should guess
                        guess = PieceGuessBoard[move.To.Row][move.To.Col];

                        if (guess == UnknownMovingPiece)
                        {
                            // guess a moving piece
                            guess = (Piece)(Rand.Next() % (int)Piece.Bomb);
                        }
                        else if (guess == UnknownNotMovingPiece)
                        {
                            // guess bomb
                            guess = Piece.Bomb;
                        }

                        System.Diagnostics.Debug.WriteLine("Cat[{0}] Piece[{1}] Guess[{2}]", (MoveCategory)i, piece, guess);

                        // make the move
                        return new OpponentMove()
                        {
                            From = move.From,
                            To = move.To,
                            Guess = guess
                        };
                    } // if moves.Count > 0
                } // if category
            } // for

            throw new Exception("A move was not successfully discovered");
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
            var guess = UnknownMovingPiece;

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

        private const Piece UnknownNotMovingPiece = Piece.Bomb | Piece.Flag;
        private const Piece UnknownMovingPiece = Piece.BombSquad | Piece.Spy |
                            Piece._1 | Piece._2 | Piece._4 | Piece._5 | Piece._6 |
                            Piece._7 | Piece._8 | Piece._9 | Piece._10;

        enum MoveCategory { AwayFromKnownOpponent = 0, AttackKnownOpponent = 1, TowardsKnownOpponent = 2, OtherAttack = 3, ForwardMove = 4, BackwardMove = 5, Other = 6, END = 7 };

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

        private bool IsMove(Coord from, Coord to, int rdir, int cdir)
        {
            // check if this move is in the desitred direction
            var rdelta = (to.Row - from.Row);
            if ((rdelta < 0 && rdir < 0) || (rdelta > 0 && rdir > 0)) return true;

            var cdelta = (to.Col - from.Col);
            if ((cdelta < 0 && cdir < 0) || (cdelta > 0 && cdir > 0)) return true;

            return false;
        }

        private bool IsTowardsEnemy(Coord from, Coord to)
        {
            // check if 1 or 2 more moves in this direction puts us closer to a known enemy
            // todo
            /*
            //          from
            // <check>   to    <check>
            // <check> <check> <check> 

            var rdelta = to.Row - from.Row;
            var cdelta = to.Col - from.Col;

            // one space further
            if (IsAdjacentToKnownGreaterEnemy(new Coord()
            {
                Row = to.Row + rdelta,
                Col = to.Col + cdelta
            }, me)) return true;

            // two spaces further
            if (IsAdjacentToKnownGreaterEnemy(new Coord()
            {
                Row = to.Row + (rdelta*2),
                Col = to.Col + (cdelta*2)
            }, me)) return true;
            */
            return false;
        }

        private bool IsBattle(Coord to, Piece piece)
        {
            // check if to is an enemy
            var dest = PieceGuessBoard[to.Row][to.Col];

            // check if this is a battle
            if (dest == Piece.Empty) return false;

            // check if we know what this piece is
            if (dest == UnknownMovingPiece || dest == UnknownNotMovingPiece) return false;

            // check if we should try to test for a win
            if (piece == Piece.Empty) return true;

            // check if we think we are going to win
            return WouldWinBattle(piece, dest);
        }

        private bool WouldWinBattle(Piece me, Piece them)
        {
            if (them == Piece.Empty) return true;
            else if (them == UnknownMovingPiece || them == UnknownNotMovingPiece) return false;
            else if (me == Piece._1) return true;
            else if (them == Piece.Flag) return true;
            else if (them == Piece.Bomb) return me == Piece.BombSquad;
            else if (them == Piece._10) return me == Piece.Spy;
            else return (them < me);
        }

        private bool IsAdjacentToKnownGreaterEnemy(Coord loc, Piece me)
        {
            Piece adj = Piece.Empty;

            // check if the loc is occupied and all its neighbors

            foreach (var delta in new Tuple<int, int>[]
            {
                new Tuple<int, int>(0, 0),
                new Tuple<int, int>(-1, 0),
                new Tuple<int, int>(1, 0),
                new Tuple<int, int>(0, -1),
                new Tuple<int, int>(0, 1)
            })
            {
                if (loc.Row + delta.Item1 >= 0 && loc.Row + delta.Item1 < BoatEgoBoard.BoardRows &&
                    loc.Col + delta.Item2 >= 0 && loc.Col + delta.Item2 < BoatEgoBoard.Columns)
                {
                    adj = PieceGuessBoard[loc.Row + delta.Item1][loc.Col + delta.Item2];
                    // if unknown or empty - keep looking
                    if (adj == Piece.Empty ||
                        adj == UnknownMovingPiece ||
                        adj == UnknownNotMovingPiece) continue;
                    // if known greather enemy - return true
                    if (!WouldWinBattle(me, adj)) return true;
                }
            }

            return false;
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
