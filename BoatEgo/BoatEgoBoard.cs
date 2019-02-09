using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatEgo
{
    public enum Piece { Empty = -3, Flag = 12, Bomb = 11, Spy = 0, _1 = 1, _2 = 2, BombSquad = 3, _4 = 4, _5 = 5, _6 = 6, _7 = 7, _8 = 8, _9 = 9, _10 = 10 };
    public enum Player { Neutral = -1, Red = 0, Blue = 1 };
    public enum BattleOutcome { Win, Loss, Tie };
    public enum GamePhase { Initializing, Inprogress, Over };

    public enum NotifyReason
    {
        InvalidCellSelected,
        AllPiecesAreInPlay,
        HomePiecesSet,
        StaleMate,
        HomeWins,
        AwayWins,
        InvalidMove,
        CorrectlyGuessedPiece,
        IncorrectlyGuessedPiece,
        PickOpponenet,
        GameOver,
        PieceMove,
        BattleLost,
        BattleWon,
        BattleTied,
        ChooseOpponent,
        PiecePlaced,
        YourTurn,
        TheirTurn
    }

    public enum State { Playable, Island, SelectableHome, Nothing, SelectableAway };

    public struct CellState
    {
        public int Row;
        public int Col;
        public Piece Piece;
        public Player Player;
        public State State;
        public bool IsHighlighted;
    }

    public struct PieceState
    {
        public int InPlay;
        public int Max;
        public int Remaining { get { return Max - InPlay; } }
    }

    public struct Coord
    {
        public int Row;
        public int Col;
    }

    public class BoatEgoBoard
    {
        public BoatEgoBoard()
        {
            Player = Player.Red;

            // init
            CurrentState = GameState.Init_Selected;
            CurrentPlayer = Player.Neutral;

            // initalize pieces
            Pieces = new PieceState[2][]; // 2 players
            for (int p = 0; p < 2; p++)
            {
                Pieces[p] = new PieceState[NumberOfPieces];
                Pieces[p][(int)Piece.Spy].Max = 1;
                Pieces[p][(int)Piece._1].Max = 2;
                Pieces[p][(int)Piece._2].Max = 5;
                Pieces[p][(int)Piece.BombSquad].Max = 5;
                Pieces[p][(int)Piece._4].Max = 2;
                Pieces[p][(int)Piece._5].Max = 2;
                Pieces[p][(int)Piece._6].Max = 2;
                Pieces[p][(int)Piece._7].Max = 1;
                Pieces[p][(int)Piece._8].Max = 1;
                Pieces[p][(int)Piece._9].Max = 1;
                Pieces[p][(int)Piece._10].Max = 1;
                Pieces[p][(int)Piece.Bomb].Max = 6;
                Pieces[p][(int)Piece.Flag].Max = 1;
            }

            // initialize the board
            States = new CellState[Rows][];
            SelectablePieces = new Coord[2][]; // number of players
            SelectablePieces[(int)Player.Red] = new Coord[NumberOfPieces];
            SelectablePieces[(int)Player.Blue] = new Coord[NumberOfPieces];
            var pieceHome = Piece.Spy;
            var pieceAway = Piece.Spy;
            for (int row = 0; row < Rows; row++)
            {
                States[row] = new CellState[Columns];
                for (int col = 0; col < Columns; col++)
                {
                    // set the cell state
                    States[row][col].State = GetCellState(row, col);
                    States[row][col].Row = row;
                    States[row][col].Col = col;
                    States[row][col].Player = Player.Neutral;
                    States[row][col].Piece = Piece.Empty;

                    if (States[row][col].State == State.SelectableHome)
                    {
                        // enable the fast lookup access for selectable items
                        SelectablePieces[(int)Player.Red][(int)pieceHome].Row = row;
                        SelectablePieces[(int)Player.Red][(int)pieceHome].Col = col;

                        // set this as a selectable piece
                        States[row][col].Piece = pieceHome;
                        States[row][col].Player = Player.Red;
                        // advance piece for next time
                        pieceHome = (Piece)((int)pieceHome + 1);
                    }
                    else if (States[row][col].State == State.SelectableAway)
                    {
                        // enable the fast lookup access for selectable items
                        SelectablePieces[(int)Player.Blue][(int)pieceAway].Row = row;
                        SelectablePieces[(int)Player.Blue][(int)pieceAway].Col = col;

                        // set this as a selectable piece
                        States[row][col].Piece = pieceAway;
                        States[row][col].Player = Player.Blue;
                        // advance piece for next time
                        pieceAway = (Piece)((int)pieceAway + 1);
                    }
                }
            }

            // init
            SelectedCell = SelectInvalidCell();
            DestinationCell = SelectInvalidCell();
            Computer = new Computer();
        }

        public Player Player { get; private set; }

        public const int Rows = 10;
        public const int Columns = 12;

        public const int NumberOfPieces = 13;

        public event Action<CellState> OnCellUpdate;
        public event Action<NotifyReason> OnNotify;
        public event Action<NotifyReason, CellState> OnNotifyInfo;
        public event Action<NotifyReason, CellState, CellState> OnNotifyChange;

        public GamePhase Phase
        {
            get
            {
                return GetGamePhase();
            }
        }

        public void Select(int row, int col)
        {
            // ensure not reentrant
            lock (this)
            {
                // pass along clicks if in the right state
                switch (CurrentState)
                {
                    // user click
                    case GameState.Init_Selected:
                    case GameState.Play_HomePlay:
                    case GameState.Play_PickOpponent:
                        MainGameLoop(row, col);
                        break;

                        // else... ignore
                }
            }
        }

        public CellState this[int row, int col]
        {
            get
            {
                if (row < 0 || row >= Rows || col < 0 || col >= Columns) throw new Exception("Invalid index for board");

                return States[row][col];
            }
        }

        public PieceState PieceInfo(Player player, Piece piece)
        {
            if (player == Player.Neutral) throw new Exception("Invalid player");
            if (piece == Piece.Empty) throw new Exception("Invalid piece");

            return Pieces[(int)player][(int)piece];
        }

        #region private
        private const int GamePlayRows = 3;
        private const int GamePlayCols = 10;
        private const int BoardRows = 8;
        private const int BoardColumns = 10;

        private static Coord HomeStarting = new Coord() { Row = 5, Col = 1 };
        private static Coord AwayStarting = new Coord() { Row = 0, Col = 1 };
        private static Coord BoardStarting = AwayStarting;

        // computer
        private IAway Computer;

        // current state
        private CellState SelectedCell;
        private CellState AttackingCell;
        private CellState DestinationCell;
        private Player CurrentPlayer;

        private CellState[][] States;
        private PieceState[][] Pieces;
        private Coord[][] SelectablePieces;

        //
        // Game State Machine
        //

        // [game setup]
        // Init_Selected-->|
        //     |  /\-------|
        //     | 
        //    \/
        //   Pre_WaitForAwaySetup
        //      |
        //     \/
        //   Pre_StartGame --------------------------------|
        //      |                                          |
        //      |          |-----------------------------| |
        //      |          |                             | |
        //      |    Play_Battle <-> Play_ChooseOpponent | |
        //      |     /\     /\                          | |
        //      |      |      |                          | |
        //     \/      |      |                          | |
        //   Play_HomePlay   Play_AwayPlay               | |
        //     /\      |      |    /\                    | |
        //      |     \/     \/     |                    | |
        //      |     Play_Move     |                    | |
        //      |      |            |                    | |
        //     \/     \/            |                    | |
        //   Play_EndTurn ----------|                    | |
        //      |    /\                                  | |
        //      |     |----------------------------------| |
        //     \/                                          |
        //   End_GameOver <--------------------------------|
        //
        enum GameState
        {
            // Initializting
            Init_Selected,
            Pre_WaitForAwaySetup,
            Pre_StartGame,

            // Inprogress
            Play_HomePlay,
            Play_AwayPlay,
            Play_Battle,
            Play_Move,
            Play_PickOpponent,
            Play_EndTurn,

            // Over
            End_GameOver
        }
        private GameState CurrentState;

        private GamePhase GetGamePhase()
        {
            switch (CurrentState)
            {
                case GameState.Init_Selected:
                case GameState.Pre_WaitForAwaySetup:
                case GameState.Pre_StartGame:
                    return GamePhase.Initializing;

                case GameState.Play_HomePlay:
                case GameState.Play_AwayPlay:
                case GameState.Play_Battle:
                case GameState.Play_PickOpponent:
                case GameState.Play_Move:
                case GameState.Play_EndTurn:
                    return GamePhase.Inprogress;

                case GameState.End_GameOver:
                    return GamePhase.Over;

                default: throw new Exception("Unknown game state");
            }
        }

        private bool ChangeState(GameState newState)
        {
            // validate that this state change TO newState FROM CurrentState is OK
            var shouldChange = false;

            switch (newState)
            {
                case GameState.Init_Selected:
                    // no valid transitions into this state
                    break;
                case GameState.Pre_WaitForAwaySetup:
                    if (CurrentState == GameState.Init_Selected) shouldChange = true;
                    break;
                case GameState.Pre_StartGame:
                    if (CurrentState == GameState.Pre_WaitForAwaySetup) shouldChange = true;
                    break;
                case GameState.Play_HomePlay:
                    if (CurrentState == GameState.Pre_StartGame ||
                        CurrentState == GameState.Play_EndTurn) shouldChange = true;
                    break;
                case GameState.Play_AwayPlay:
                    if (CurrentState == GameState.Play_EndTurn) shouldChange = true;
                    break;
                case GameState.Play_Battle:
                    if (CurrentState == GameState.Play_HomePlay ||
                        CurrentState == GameState.Play_AwayPlay ||
                        CurrentState == GameState.Play_PickOpponent) shouldChange = true;
                    break;
                case GameState.Play_PickOpponent:
                    if (CurrentState == GameState.Play_Battle) shouldChange = true;
                    break;
                case GameState.Play_Move:
                    if (CurrentState == GameState.Play_AwayPlay ||
                        CurrentState == GameState.Play_HomePlay) shouldChange = true;
                    break;
                case GameState.Play_EndTurn:
                    if (CurrentState == GameState.Play_Move ||
                        CurrentState == GameState.Play_Battle) shouldChange = true;
                    break;
                case GameState.End_GameOver:
                    if (CurrentState == GameState.Play_EndTurn ||
                        CurrentState == GameState.Pre_StartGame) shouldChange = true;
                    break;
                default: throw new Exception("Unknown state");
            }

            // make the state change if valid
            if (shouldChange)
            {
                CurrentState = newState;
                return true;
            }

            // invalid
            throw new Exception(string.Format("Inavlid state change from {0} to {1}", CurrentState, newState));
        }

        //
        // Game engine
        //

        private async void MainGameLoop(int row, int col)
        {
            var state = GetCellState(row, col);

            // clicking non clickable areas (nothing happens)
            if (state == State.Nothing || state == State.Island) return;

            var cell = States[row][col];

            var applyChange = true;
            while (applyChange)
            {
                // restet
                applyChange = false;

                // apply change
                switch (CurrentState)
                {
                    //
                    // Initialize the hom board
                    //

                    case GameState.Init_Selected:
                        // must be clicking on a selectable
                        if (state == State.SelectableHome)
                        {
                            // unselect the last piece
                            if (IsValidCell(SelectedCell))
                            {
                                Unhighlight(SelectedCell);
                            }

                            // mark this as selected
                            // check if this piece still can be placed
                            if (Pieces[(int)Player][(int)cell.Piece].InPlay < Pieces[(int)Player][(int)cell.Piece].Max)
                            {
                                // mark this as selected
                                SelectedCell = cell;
                                Highlight(cell);
                            }
                            else
                            {
                                OnNotifyInfo(NotifyReason.AllPiecesAreInPlay, 
                                    new CellState() { Player = Player, Piece = cell.Piece, Row = cell.Row, Col = cell.Col });
                            }
                        }
                        else if (state == State.Playable)
                        {
                            if (IsValidCell(SelectedCell))
                            {
                                // place the piece
                                if (IsInHomePlacementRegion(row, col))
                                {
                                    // replacing an existing placement
                                    if (cell.Piece != Piece.Empty)
                                    {
                                        // need to put this piece back into the collection
                                        RemovePiece(cell);
                                    }

                                    if (AddPiece(SelectedCell.Piece, Player, row, col))
                                    {
                                        // piece was added
                                        OnNotifyInfo(NotifyReason.PiecePlaced,
                                            new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = row, Col = col });

                                        // check if all the pieces have been placed
                                        var remaining = CountRemaining(Player);
                                        if (remaining == 0)
                                        {
                                            OnNotify(NotifyReason.HomePiecesSet);

                                            // unhighlight this piece
                                            Unhighlight(SelectedCell);

                                            // we are done
                                            ChangeState(GameState.Pre_WaitForAwaySetup);
                                            applyChange = true;
                                        }
                                        else
                                        {
                                            // unhighlight if there are no longer any pieces remaining
                                            if (Pieces[(int)Player][(int)SelectedCell.Piece].InPlay >= Pieces[(int)Player][(int)SelectedCell.Piece].Max)
                                            {
                                                // unhighlight this piece
                                                Unhighlight(SelectedCell);

                                                // remove the selectedpiece
                                                SelectedCell = SelectInvalidCell();
                                            }
                                        }
                                    } // if piece added
                                    else
                                    {
                                        throw new Exception("Not able to place the piece");
                                    }
                                } // if inHomeRegion
                                else
                                {
                                    OnNotifyInfo(NotifyReason.InvalidCellSelected,
                                        new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = row, Col = col });
                                }
                            } // if isValid
                        } // if playable
                        else
                        {
                            OnNotifyInfo(NotifyReason.InvalidCellSelected,
                                new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = row, Col = col });
                        }
                        break;

                    //
                    // Pre-game setup
                    //
                    case GameState.Pre_WaitForAwaySetup:
                        // now the home pieces are set... 

                        // check if the computer is a computer and pass the current implementation (for use of statistics)
                        if (Computer is Computer)
                        {
                            // send a full view of the field (including Home piece positions)
                            var fullView = GetBoardView(Player.Neutral);
                            (Computer as Computer).Feedback(fullView);
                        }

                        // setup the away pieces
                        var startingView = GetBoardView(Player.Blue, true /*for init*/);

                        // get the initial piece layout ascynchronsly
                        var initTask = new Task<BoatEgoBoardView>(() => { return Computer.StartingPositions(startingView); });
                        initTask.Start();
                        startingView = await initTask;

                        if (!startingView.IsValid) throw new Exception("Board created by the computer is not valid");

                        // apply the away field
                        for (int r = 0; r < GamePlayRows; r++)
                        {
                            for (int c = 0; c < BoardColumns; c++)
                            {
                                // add piece
                                AddPiece(startingView.GetPiece(r, c), Player.Blue, AwayStarting.Row + r, c + AwayStarting.Col);
                            }
                        }

                        // start game
                        ChangeState(GameState.Pre_StartGame);
                        applyChange = true;
                        break;

                    case GameState.Play_EndTurn:
                    case GameState.Pre_StartGame:
                        // check that both home and away can move

                        // check the away row if they are have no moves
                        var awayCanMove = CountPiecesThatCanMove(Player.Blue);
                        var awayFlags = CountPiece(Player.Blue, Piece.Flag);

                        // check the home row if they can move
                        var homeCanMove = CountPiecesThatCanMove(Player.Red);
                        var homeFlags = CountPiece(Player.Red, Piece.Flag);

                        if (awayCanMove == 0 && homeCanMove == 0)
                        {
                            // stale mate
                            ChangeState(GameState.End_GameOver);
                            OnNotify(NotifyReason.StaleMate);
                            applyChange = true;
                        }
                        else if (awayCanMove == 0 || awayFlags == 0)
                        {
                            // Home wins
                            ChangeState(GameState.End_GameOver);
                            OnNotify(NotifyReason.HomeWins);
                            applyChange = true;
                        }
                        else if (homeCanMove == 0 || homeFlags == 0)
                        {
                            // away wins
                            ChangeState(GameState.End_GameOver);
                            OnNotify(NotifyReason.AwayWins);
                            applyChange = true;
                        }
                        else
                        {
                            // else switch play to the other player
                            if (CurrentPlayer == Player.Red)
                            {
                                CurrentPlayer = Player.Blue;
                                ChangeState(GameState.Play_AwayPlay);
                                OnNotify(NotifyReason.TheirTurn);
                                applyChange = true;
                            }
                            else
                            {
                                // blue or neutral (eg. first time)
                                CurrentPlayer = Player.Red;
                                ChangeState(GameState.Play_HomePlay);
                                OnNotify(NotifyReason.YourTurn);
                            }
                        }

                        // set the selected cell to nothing
                        SelectedCell = SelectInvalidCell();

                        break;

                    //
                    // Player's turn
                    //

                    case GameState.Play_HomePlay:

                        // ensure there is nothing highlighted
                        UnhighlightAll();

                        // ensure the piece is playable and matches the player
                        if (state == State.Playable)
                        {
                            // select a piece to move
                            if (cell.Player == Player)
                            {
                                if (cell.Piece != Piece.Empty)
                                {
                                    // identify places that this piece can move
                                    var homeMoves = GetMoves(cell);

                                    if (homeMoves != null && homeMoves.Count > 0)
                                    {
                                        // select this piece
                                        SelectedCell = cell;

                                        // highlight
                                        HighlightAll(homeMoves);
                                    }
                                }
                            }

                            // move the piece
                            else if (SelectedCell.Player == Player)
                            {
                                if (IsValidCell(SelectedCell))
                                {
                                    // request to move this cell to this location

                                    // double check that this cell is valid for this cell
                                    var homeMoves = GetMoves(SelectedCell);

                                    if (IsValidMove(cell, homeMoves))
                                    {
                                        // check if this is a battle
                                        if (cell.Piece == Piece.Empty)
                                        {
                                            // make the move
                                            // SelectedCell is the piece to move
                                            // cell is the place to move too
                                            ChangeState(GameState.Play_Move);
                                            applyChange = true;
                                        }
                                        else
                                        {
                                            // this is a battle
                                            AttackingCell = SelectedCell;
                                            SelectedCell = SelectInvalidCell();
                                            ChangeState(GameState.Play_Battle);
                                            applyChange = true;
                                        }
                                    }
                                    else
                                    {
                                        OnNotifyChange(NotifyReason.InvalidMove,
                                            new CellState() { Player = SelectedCell.Player, Piece = SelectedCell.Piece, Row = SelectedCell.Row, Col = SelectedCell.Col },
                                            new CellState() { Player = cell.Player, Piece = cell.Piece, Row = cell.Row, Col = cell.Col });
                                    }
                                } // if selectedCell is valid
                            } // if same player
                        } // if playable
                        break;

                    case GameState.Play_Move:
                        // SelectedCell is the piece that is moving
                        // cell is the place where it is moving too

                        if (!IsValidCell(SelectedCell) ||
                            SelectedCell.Player != CurrentPlayer) throw new Exception("Choose an invalid to move");
                        if (!IsValidCell(cell) ||
                            cell.Piece != Piece.Empty) throw new Exception("Invalid locaiton to move too");

                        // request to move this cell to this location

                        // double check that this cell is valid for this cell
                        var moves = GetMoves(SelectedCell);

                        if (!IsValidMove(cell, moves)) throw new Exception("Invalid move for piece");

                        // make the move

                        // inform the computer of the move
                        if (CurrentPlayer != Player)
                        {
                            Computer.Feedback_OpponentMove(
                                new Coord() { Row = SelectedCell.Row, Col = SelectedCell.Col },
                                new Coord() { Row = cell.Row, Col = cell.Col });
                        }
                        OnNotifyChange(NotifyReason.PieceMove,
                                            new CellState() { Player = SelectedCell.Player, Piece = SelectedCell.Piece, Row = SelectedCell.Row, Col = SelectedCell.Col },
                                            new CellState() { Player = cell.Player, Piece = cell.Piece, Row = cell.Row, Col = cell.Col });

                        // remove this piece from the current location
                        var piece = SelectedCell.Piece;
                        RemovePiece(SelectedCell);

                        // add the piece at the new cell
                        AddPiece(piece, CurrentPlayer, cell.Row, cell.Col);

                        // indicate this is the end of the turn
                        ChangeState(GameState.Play_EndTurn);
                        applyChange = true;

                        break;

                    case GameState.Play_Battle:
                        // AttackingCell is the attacker and cell is the attacked
                        // SelectedCell is the guess of the piece (if attacking with a _1)
                        // cell is the destination

                        // ensure they are valid
                        if (!IsValidCell(AttackingCell)) throw new Exception("The attacker cell is not valid");
                        if (!IsValidCell(cell)) throw new Exception("The attacked cell is not valid");
                        if (AttackingCell.Piece == Piece.Empty || cell.Piece == Piece.Empty) throw new Exception("Invalid empty piece in battle");

                        // assume this is a valid move (validated elsewhere)

                        var fought = false;
                        var homeWon = true;
                        var awayWon = true;

                        if (AttackingCell.Piece == Piece._1)
                        {
                            if (!IsValidCell(SelectedCell))
                            {
                                // no battle occured
                                fought = false;

                                // need to request a piece from the user
                                if (CurrentPlayer != Player) throw new Exception("Non-human must select a guess for a 1 attack");

                                // request the user to choose a piece
                                DestinationCell = cell; // keep the current cell
                                ChangeState(GameState.Play_PickOpponent);
                                OnNotify(NotifyReason.PickOpponenet);
                            }
                            else
                            {
                                // a fight is happening
                                fought = true;

                                // check if the guess is accurate
                                if (SelectedCell.Piece == cell.Piece)
                                {
                                    OnNotifyInfo(NotifyReason.CorrectlyGuessedPiece,
                                        new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = cell.Row, Col = cell.Col });
                                    homeWon = true;
                                    awayWon = false;
                                }
                                else
                                {
                                    OnNotifyInfo(NotifyReason.IncorrectlyGuessedPiece,
                                        new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = cell.Row, Col = cell.Col });
                                    homeWon = false;
                                    awayWon = true;
                                }
                            }
                        }
                        else
                        {
                            // there was a battle
                            fought = true;

                            // TODO battle rules around BOMBs, Flag, Spy

                            // normal battle rules apply
                            if (AttackingCell.Piece == cell.Piece)
                            {
                                // they both loss
                                homeWon = awayWon = false;
                            }
                            else if (cell.Piece == Piece.Bomb)
                            {
                                awayWon = true;
                                homeWon = false;
                                if (AttackingCell.Piece == Piece.BombSquad)
                                {
                                    homeWon = true;
                                    awayWon = false;
                                }
                            }
                            else if (cell.Piece == Piece.Flag)
                            {
                                homeWon = true;
                                awayWon = false;
                            }
                            else if (AttackingCell.Piece == Piece.Spy)
                            {
                                homeWon = false;
                                awayWon = true;
                                if (cell.Piece == Piece._10)
                                {
                                    homeWon = true;
                                    awayWon = false;
                                }
                            }
                            else if (AttackingCell.Piece > cell.Piece)
                            {
                                // home wins
                                homeWon = true;
                                awayWon = false;
                            }
                            else
                            {
                                // away wins
                                homeWon = false;
                                awayWon = true;
                            }
                        }

                        // check if a battle was fought
                        if (fought)
                        {
                            // change the board according to who won
                            var aPiece = AttackingCell.Piece;
                            var aPlayer = AttackingCell.Player;

                            // inform the computer of a battle
                            Computer.Feedback_Battle(
                                new Coord() { Row = AttackingCell.Row, Col = AttackingCell.Col },
                                new Coord() { Row = cell.Row, Col = cell.Col },
                                awayWon ? BattleOutcome.Win :
                                    homeWon ? BattleOutcome.Loss : BattleOutcome.Tie);

                            // notify to the UI what happened
                            OnNotifyChange(awayWon ? NotifyReason.BattleLost :
                                    homeWon ? NotifyReason.BattleWon : NotifyReason.BattleTied,
                                    new CellState() { Player = AttackingCell.Player, Piece = AttackingCell.Piece, Row = AttackingCell.Row, Col = AttackingCell.Col },
                                    new CellState() { Player = cell.Player, Piece = Piece.Empty, Row = cell.Row, Col = cell.Col });
                            
                            // regardless the AttackingCell disappears
                            RemovePiece(AttackingCell);

                            // remove the attacked if it lost
                            if (!awayWon)
                            {
                                RemovePiece(cell);
                            }

                            // place the attacker if home wins
                            if (homeWon)
                            {
                                AddPiece(aPiece, aPlayer, cell.Row, cell.Col);
                            }

                            // clear selected
                            SelectedCell = SelectInvalidCell();

                            // indicate this is the end of the turn
                            ChangeState(GameState.Play_EndTurn);
                            applyChange = true;
                        }
                        else
                        {
                            if (CurrentPlayer != Player) throw new Exception("Computer player incorrectly requested a fight without a fight");
                        }
                        break;

                    case GameState.Play_PickOpponent:
                        // check that the appropriate cell was selected
                        if (state == State.SelectableAway)
                        {
                            // choose this cell
                            SelectedCell = cell;

                            // set the correct destination back
                            if (!IsValidCell(DestinationCell)) throw new Exception("Inavlid destination");
                            cell = DestinationCell;
                            DestinationCell = SelectInvalidCell();

                            OnNotifyInfo(NotifyReason.ChooseOpponent,
                                new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = cell.Row, Col = cell.Col });

                            // change state back to battle
                            ChangeState(GameState.Play_Battle);
                            applyChange = true;
                        }
                        break;

                    case GameState.Play_AwayPlay:
                        // Must set
                        //   AttackingCell - the cell that is attacking
                        //   cell - the cell that is being attacked (must be within a move of AttackingCell)
                        //   SelectedCell - the 'guess' if AttackingCell is a 1

                        // get a view of the board
                        var awayView = GetBoardView(Player.Blue);

                        // get the initial piece layout ascynchronsly
                        var moveTask = new Task<AwayMove>(() => { return Computer.Move(awayView); });
                        moveTask.Start();
                        var awayMove = await moveTask;

                        // get the moving piece (and validate)
                        state = GetCellState(awayMove.From.Row, awayMove.From.Col);
                        if (state != State.Playable) throw new Exception("Computer choose a non-playable piece to play");
                        SelectedCell = States[awayMove.From.Row][awayMove.From.Col];
                        if (SelectedCell.Player != CurrentPlayer ||
                            SelectedCell.Piece == Piece.Empty) throw new Exception("Computer choose an invalid piece to play");

                        // get the destination piece (and validate)
                        state = GetCellState(awayMove.To.Row, awayMove.To.Col);
                        if (state != State.Playable) throw new Exception("Computer choose a non-playable piece to play");
                        cell = States[awayMove.To.Row][awayMove.To.Col];
                        if (cell.Player == CurrentPlayer) throw new Exception("Computer choose an invalid piece to play");

                        // move & endturn or battle

                        // double check that this cell is valid for this cell
                        var awayMoves = GetMoves(SelectedCell);

                        if (IsValidMove(cell, awayMoves))
                        {
                            // check if this is a battle
                            if (cell.Piece == Piece.Empty)
                            {
                                // make the move
                                // SelectedCell is moving
                                // cell is the place to move too
                                ChangeState(GameState.Play_Move);
                                applyChange = true;
                            }
                            else
                            {
                                // this is a battle
                                AttackingCell = SelectedCell;
                                // get the guess, if there is one
                                if (awayMove.Guess != Piece.Empty)
                                {
                                    var coord = SelectablePieces[(int)CurrentPlayer][(int)awayMove.Guess];
                                    SelectedCell = States[coord.Row][coord.Col];
                                }
                                // cell is the place to attack
                                ChangeState(GameState.Play_Battle);
                                applyChange = true;
                            }
                        }
                        else
                        {
                            throw new Exception("Computer requested an invalid move");
                        }
                        break;


                    // 
                    // End of game
                    //

                    case GameState.End_GameOver:
                        OnNotify(NotifyReason.GameOver);
                        break;

                    default: throw new Exception("Unknown game state : " + CurrentState);
                }
            }
        }

        //
        // Piece control
        //

        private int CountPiece(Player player, Piece piece)
        {
            // iterate through all pieces and count all of this type
            var count = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (States[r][c].State == State.Playable &&
                        States[r][c].Player == player &&
                        States[r][c].Piece == piece)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private int CountPiecesThatCanMove(Player player)
        {
            // iterate through all pieces and count how many can move
            var count = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (States[r][c].State == State.Playable && States[r][c].Player == player)
                    {
                        var moves = GetMoves(States[r][c]);
                        if (moves != null && moves.Count > 0) count++;
                    }
                }
            }

            return count;
        }

        private List<CellState> GetMoves(CellState cell)
        {
            if (cell.Piece == Piece.Empty ||
                cell.State != State.Playable) throw new Exception("Inavlid cell cannot move");

            if (cell.Piece == Piece.Bomb ||
                cell.Piece == Piece.Flag) return null;

            var moves = new List<CellState>();

            var distance = 1;
            if (cell.Piece == Piece._2)
            {
                // check each direction until obstruction
                distance = int.MaxValue;
            }

            foreach (var detail in new Tuple<int, int>[]
            {
                new Tuple<int, int>(0,1),
                new Tuple<int, int>(0,-1),
                new Tuple<int, int>(1,0),
                new Tuple<int, int>(-1,0),
            })
            {
                var rdelta = detail.Item1;
                var cdelta = detail.Item2;
                var dist = distance;

                // move in this direction until:
                //  1) we hit an piece
                //  2) hit a non-playable cell
                //  3) exceed the bounds
                for (int r = cell.Row + rdelta, c = cell.Col + cdelta;
                    dist > 0 &&
                    r >= 0 && r < Rows &&
                    c >= 0 && c < Columns; r += rdelta, c += cdelta, dist--)
                {
                    var o = States[r][c];

                    // check if we should add this to our move list
                    if (o.State == State.Playable && o.Player != cell.Player)
                    {
                        moves.Add(o);
                    }

                    // check if we have reached the edge of a possible move
                    if (o.Piece != Piece.Empty ||
                        o.State != State.Playable) break;
                }
            }

            return moves;
        }

        private bool IsValidMove(CellState cell, List<CellState> moves)
        {
            // check that cell is present in moves - if it is, then it is valid
            foreach (var c in moves)
            {
                if (c.Row == cell.Row && c.Col == cell.Col) return true;
            }

            return false;
        }


        private int CountRemaining(Player player)
        {
            // sum all the remaining pieces to be placed
            var remaining = 0;
            for (int i = 0; i < Pieces[(int)player].Length; i++)
            {
                remaining += Pieces[(int)player][i].Max - Pieces[(int)player][i].InPlay;
            }
            return remaining;
        }

        private bool IsValidCell(CellState cell)
        {
            return cell.State != State.Nothing && cell.State != State.Island;
        }

        private CellState SelectInvalidCell()
        {
            return States[States.Length - 1][0];
        }

        private bool IsInHomePlacementRegion(int row, int col)
        {
            // the player's placement region is the last 3 rows of playable region
            return (row >= 5 && row <= 7 && col >= 1 && col <= 10);
        }

        private BoatEgoBoardView GetBoardView(Player player, bool forInit = false)
        {
            // get a view of the board from this players pov

            // get piece counts
            var counts = new Dictionary<Piece, int>();
            if (player != Player.Neutral)
            {
                for (int i = 0; i < Pieces[(int)player].Length; i++)
                {
                    counts.Add((Piece)i, Pieces[(int)player][i].Max);
                }
            }

            var view = new BoatEgoBoardView(BoardRows, BoardColumns, counts)
            {
                IsForInitialization = forInit
            };

            // populate board
            for (int r = 0; r < BoardRows; r++)
            {
                for (int c = 0; c < BoardColumns; c++)
                {
                    var cell = States[r + BoardStarting.Row][c + BoardStarting.Col];

                    // check if not playable
                    if (cell.State != State.Playable)
                    {
                        view.BlockCell(r, c);
                        continue;
                    }

                    // get moves, if the colors match
                    List<AwayMove> moves = new List<AwayMove>();
                    if (cell.Player == player && cell.Piece != Piece.Empty)
                    {
                        var pmoves = GetMoves(cell);
                        if (pmoves != null)
                        {
                            foreach (var move in pmoves)
                            {
                                // transform into a consumable form
                                moves.Add(
                                    new AwayMove()
                                    {
                                        From = new Coord() { Row = cell.Row, Col = cell.Col },
                                        To = new Coord() { Row = move.Row, Col = move.Col }
                                    }
                                    );
                            }
                        }
                    }

                    // get the piece that we should put here
                    var piece = cell.Piece;
                    if (piece != Piece.Empty && player != Player.Neutral && player != cell.Player)
                    {
                        // obfuscate the real piece
                        piece = Piece.Bomb | Piece.BombSquad | Piece.Flag | Piece.Spy |
                            Piece._1 | Piece._2 | Piece._4 | Piece._5 | Piece._6 |
                            Piece._7 | Piece._8 | Piece._9 | Piece._10;
                    }

                    // set piece
                    view.PutCell(r, c, piece, player == cell.Player, moves);
                }
            }

            return view;
        }

        private State GetCellState(int row, int col)
        {
            //
            //  p = Playable
            //  x = Nothing
            //  h = SelectableHome
            //  a = SelectableAway
            //  i = Island
            //                       1 1
            //   0 1 2 3 4 5 6 7 8 9 0 1
            // 0 x p p p p p p p p p p x
            // 1 a p p p p p p p p p p a
            // 2 a p p p p p p p p p p a
            // 3 a p p i i p p i i p p a
            // 4 a p p i i p p i i p p a
            // 5 a p p p p p p p p p p a
            // 6 a p p p p p p p p p p a
            // 7 x p p p p p p p p p p x
            // 8 x x h h h h h h h h x x
            // 9 x x x h h h h h x x x x

            // is Island
            if ((row == 3 || row == 4) &&
                (col == 3 || col == 4 || col == 7 || col == 8)) return State.Island;

            // is playable
            if ((row >= 0 && row <= 7 && col >= 1 && col <= 10)) return State.Playable;

            // is selectable (home)
            if ((row == 8 && col >= 2 && col <= 9)
                || (row == 9 && col >= 3 && col <= 7)) return State.SelectableHome;

            // is selectable (away)
            if ((col == 0 && row >= 1 && row <= 6)
                || (col == 11 && row >= 1 && row <= 6)) return State.SelectableAway;

            return State.Nothing;
        }

        //
        // Change piece states
        //

        private void HighlightAll(List<CellState> cells)
        {
            // highlight all
            foreach (var cell in cells)
            {
                Highlight(cell);
            }
        }

        private void Highlight(CellState cell)
        {
            if (cell.State != State.Playable && cell.State != State.SelectableHome) throw new Exception("Invalid cell to highlight");

            // update to mark as highlighted
            States[cell.Row][cell.Col].IsHighlighted = true;
            OnCellUpdate(States[cell.Row][cell.Col]);
        }

        private void UnhighlightAll()
        {
            // iterate through all the cells and all that are highlighted, unhighlight
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (States[r][c].IsHighlighted) Unhighlight(States[r][c]);
                }
            }
        }

        private void Unhighlight(CellState cell)
        {
            if (cell.State != State.Playable && cell.State != State.SelectableHome) throw new Exception("Invalid cell to highlight");

            // mark as not highlighted
            States[cell.Row][cell.Col].IsHighlighted = false;
            OnCellUpdate(States[cell.Row][cell.Col]);
        }

        private bool RemovePiece(CellState cell)
        {
            // validation
            if (cell.State != State.Playable) throw new Exception("May only remove from a playable location");
            if (cell.Piece == Piece.Empty) throw new Exception("May not remove an Empty piece");
            if (Pieces[(int)cell.Player][(int)cell.Piece].InPlay <= 0) throw new Exception("Not able to remove this piece as there are none in play");

            // remove this piece from the board (must use this directly to make the update)
            var piece = cell.Piece;
            States[cell.Row][cell.Col].Piece = Piece.Empty;
            States[cell.Row][cell.Col].Player = Player.Neutral;

            // update the book keeping
            Pieces[(int)cell.Player][(int)piece].InPlay--;

            // update the UI
            OnCellUpdate(States[cell.Row][cell.Col]);

            // and the selectable piece
            OnCellUpdate(States[SelectablePieces[(int)cell.Player][(int)piece].Row][SelectablePieces[(int)cell.Player][(int)piece].Col]);

            return true;
        }

        private bool AddPiece(Piece piece, Player player, int row, int col)
        {
            // validation
            if (piece == Piece.Empty) throw new Exception("Cannot place an empty piece");
            if (Pieces[(int)player][(int)piece].InPlay == Pieces[(int)player][(int)piece].Max) return false;

            // add a piece at this location
            var state = GetCellState(row, col);
            if (state != State.Playable) throw new Exception("May only place a piece on a playable location");

            var cell = States[row][col];
            if (cell.Piece != Piece.Empty) throw new Exception("Can only place a piece on an empty location");

            // add it to this location
            States[cell.Row][cell.Col].Piece = piece;
            States[cell.Row][cell.Col].Player = player;

            // update book keeping
            Pieces[(int)player][(int)piece].InPlay++;

            // update the UI
            OnCellUpdate(States[cell.Row][cell.Col]);

            // and the selectable piece
            OnCellUpdate(States[SelectablePieces[(int)player][(int)piece].Row][SelectablePieces[(int)player][(int)piece].Col]);

            return true;
        }
        #endregion
    }
}
