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
        PlayerPiecesSet,
        StaleMate,
        PlayerWins,
        OpponentWins,
        InvalidMove,
        CorrectlyGuessedPiece,
        IncorrectlyGuessedPiece,
        PickOpponent,
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

    public enum State { Playable, Island, SelectablePlayer, Nothing, SelectableOpponent };

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
            var piecePlayer = Piece.Spy;
            var pieceOpponent = Piece.Spy;
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

                    if (States[row][col].State == State.SelectablePlayer)
                    {
                        // enable the fast lookup access for selectable items
                        SelectablePieces[(int)Player.Red][(int)piecePlayer].Row = row;
                        SelectablePieces[(int)Player.Red][(int)piecePlayer].Col = col;

                        // set this as a selectable piece
                        States[row][col].Piece = piecePlayer;
                        States[row][col].Player = Player.Red;
                        // advance piece for next time
                        piecePlayer = (Piece)((int)piecePlayer + 1);
                    }
                    else if (States[row][col].State == State.SelectableOpponent)
                    {
                        // enable the fast lookup access for selectable items
                        SelectablePieces[(int)Player.Blue][(int)pieceOpponent].Row = row;
                        SelectablePieces[(int)Player.Blue][(int)pieceOpponent].Col = col;

                        // set this as a selectable piece
                        States[row][col].Piece = pieceOpponent;
                        States[row][col].Player = Player.Blue;
                        // advance piece for next time
                        pieceOpponent = (Piece)((int)pieceOpponent + 1);
                    }
                }
            }

            // init
            SelectedCell = SelectInvalidCell();
            DestinationCell = SelectInvalidCell();
            Computer = new Computer();
        }

        public Player Player { get; private set; }

        public const int Rows = 12;
        public const int Columns = 14;

        public const int NumberOfPieces = 13;

        public event Action<CellState> OnCellUpdate;
        public event Action<NotifyReason, CellState[]> OnNotify;

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
                    case GameState.Play_PlayerPlay:
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

        private static Coord PlayerStarting = new Coord() { Row = 6, Col = 2 };
        private static Coord OpponentStarting = new Coord() { Row = 1, Col = 2 };
        private static Coord BoardStarting = OpponentStarting;

        // computer
        private IOpponent Computer;

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
        //   Pre_WaitForOpponentSetup
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
        // Play_PlayerPlay   Play_OpponentPlay           | |
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
            Pre_WaitForOpponentSetup,
            Pre_StartGame,

            // Inprogress
            Play_PlayerPlay,
            Play_OpponentPlay,
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
                case GameState.Pre_WaitForOpponentSetup:
                case GameState.Pre_StartGame:
                    return GamePhase.Initializing;

                case GameState.Play_PlayerPlay:
                case GameState.Play_OpponentPlay:
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
                case GameState.Pre_WaitForOpponentSetup:
                    if (CurrentState == GameState.Init_Selected) shouldChange = true;
                    break;
                case GameState.Pre_StartGame:
                    if (CurrentState == GameState.Pre_WaitForOpponentSetup) shouldChange = true;
                    break;
                case GameState.Play_PlayerPlay:
                    if (CurrentState == GameState.Pre_StartGame ||
                        CurrentState == GameState.Play_EndTurn) shouldChange = true;
                    break;
                case GameState.Play_OpponentPlay:
                    if (CurrentState == GameState.Play_EndTurn) shouldChange = true;
                    break;
                case GameState.Play_Battle:
                    if (CurrentState == GameState.Play_PlayerPlay ||
                        CurrentState == GameState.Play_OpponentPlay ||
                        CurrentState == GameState.Play_PickOpponent) shouldChange = true;
                    break;
                case GameState.Play_PickOpponent:
                    if (CurrentState == GameState.Play_Battle) shouldChange = true;
                    break;
                case GameState.Play_Move:
                    if (CurrentState == GameState.Play_OpponentPlay ||
                        CurrentState == GameState.Play_PlayerPlay) shouldChange = true;
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
                        if (state == State.SelectablePlayer)
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
                                OnNotify(NotifyReason.AllPiecesAreInPlay, 
                                    new CellState[] { new CellState() { Player = Player, Piece = cell.Piece, Row = cell.Row, Col = cell.Col } });
                            }
                        }
                        else if (state == State.Playable)
                        {
                            if (IsValidCell(SelectedCell))
                            {
                                // place the piece
                                if (row >=  PlayerStarting.Row && row < (PlayerStarting.Row + GamePlayRows) &&
                                    col >= PlayerStarting.Col && col < (PlayerStarting.Col + GamePlayCols))
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
                                        OnNotify(NotifyReason.PiecePlaced,
                                            new CellState[] { new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = row, Col = col } });

                                        // check if all the pieces have been placed
                                        var remaining = CountRemaining(Player);
                                        if (remaining == 0)
                                        {
                                            OnNotify(NotifyReason.PlayerPiecesSet, null);

                                            // unhighlight this piece
                                            Unhighlight(SelectedCell);

                                            // we are done
                                            ChangeState(GameState.Pre_WaitForOpponentSetup);
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
                                } // if inPlayerRegion
                                else
                                {
                                    OnNotify(NotifyReason.InvalidCellSelected,
                                        new CellState[] { new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = row, Col = col } });
                                }
                            } // if isValid
                        } // if playable
                        else
                        {
                            OnNotify(NotifyReason.InvalidCellSelected,
                                new CellState[] { new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = row, Col = col } });
                        }
                        break;

                    //
                    // Pre-game setup
                    //
                    case GameState.Pre_WaitForOpponentSetup:
                        // now the player pieces are set... 

                        // check if the computer is a computer and pass the current implementation (for use of statistics)
                        if (Computer is Computer)
                        {
                            // send a full view of the field (including player piece positions)
                            var fullView = GetBoardView(Player.Neutral);
                            (Computer as Computer).Feedback(fullView);
                        }

                        // setup the opponent pieces
                        var startingView = GetBoardView(Player.Blue, true /*for init*/);

                        // get the initial piece layout ascynchronsly
                        var initTask = new Task<BoatEgoBoardView>(() => { return Computer.StartingPositions(startingView); });
                        initTask.Start();
                        startingView = await initTask;

                        if (!startingView.IsValid) throw new Exception("Board created by the computer is not valid");

                        // apply the opponent field
                        for (int r = 0; r < GamePlayRows; r++)
                        {
                            for (int c = 0; c < BoardColumns; c++)
                            {
                                // add piece
                                AddPiece(startingView.GetPiece(r, c), Player.Blue, OpponentStarting.Row + r, c + OpponentStarting.Col);
                            }
                        }

                        // start game
                        ChangeState(GameState.Pre_StartGame);
                        applyChange = true;
                        break;

                    case GameState.Play_EndTurn:
                    case GameState.Pre_StartGame:
                        // check that both player and opponent can move

                        // check the opponent row if they are have no moves
                        var opponentCanMove = CountPiecesThatCanMove(Player.Blue);
                        var opponentFlags = CountPiece(Player.Blue, Piece.Flag);

                        // check the palyer row if they can move
                        var playerCanMove = CountPiecesThatCanMove(Player.Red);
                        var playerFlags = CountPiece(Player.Red, Piece.Flag);

                        if (opponentCanMove == 0 && playerCanMove == 0)
                        {
                            // stale mate
                            ChangeState(GameState.End_GameOver);
                            OnNotify(NotifyReason.StaleMate, null);
                            applyChange = true;
                        }
                        else if (opponentCanMove == 0 || opponentFlags == 0)
                        {
                            // player wins
                            ChangeState(GameState.End_GameOver);
                            OnNotify(NotifyReason.PlayerWins, null);
                            applyChange = true;
                        }
                        else if (playerCanMove == 0 || playerFlags == 0)
                        {
                            // opponent wins
                            ChangeState(GameState.End_GameOver);
                            OnNotify(NotifyReason.OpponentWins, null);
                            applyChange = true;
                        }
                        else
                        {
                            // else switch play to the other player
                            if (CurrentPlayer == Player.Red)
                            {
                                CurrentPlayer = Player.Blue;
                                ChangeState(GameState.Play_OpponentPlay);
                                OnNotify(NotifyReason.TheirTurn, null);
                                applyChange = true;
                            }
                            else
                            {
                                // blue or neutral (eg. first time)
                                CurrentPlayer = Player.Red;
                                ChangeState(GameState.Play_PlayerPlay);
                                OnNotify(NotifyReason.YourTurn, null);
                            }
                        }

                        // set the selected cell to nothing
                        SelectedCell = SelectInvalidCell();

                        break;

                    //
                    // Player's turn
                    //

                    case GameState.Play_PlayerPlay:

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
                                    var playerMoves = GetMoves(cell);

                                    if (playerMoves != null && playerMoves.Count > 0)
                                    {
                                        // select this piece
                                        SelectedCell = cell;

                                        // highlight
                                        HighlightAll(playerMoves);
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
                                    var playerMoves = GetMoves(SelectedCell);

                                    if (IsValidMove(cell, playerMoves))
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
                                        OnNotify(NotifyReason.InvalidMove,
                                            new CellState[]
                                            {
                                            new CellState() { Player = SelectedCell.Player, Piece = SelectedCell.Piece, Row = SelectedCell.Row, Col = SelectedCell.Col },
                                            new CellState() { Player = cell.Player, Piece = cell.Piece, Row = cell.Row, Col = cell.Col }
                                            });
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
                        OnNotify(NotifyReason.PieceMove,
                            new CellState[]
                            {
                                            new CellState() { Player = SelectedCell.Player, Piece = SelectedCell.Piece, Row = SelectedCell.Row, Col = SelectedCell.Col },
                                            new CellState() { Player = cell.Player, Piece = cell.Piece, Row = cell.Row, Col = cell.Col }
                            });

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
                        var attackerWon = true;
                        var attackedWon = true;

                        if (AttackingCell.Piece == Piece._1)
                        {
                            if (cell.Piece == Piece.Flag)
                            {
                                // player wins the battle (without having to guess)
                                fought = true;
                                attackerWon = true;
                                attackedWon = false;
                            }
                            else if (!IsValidCell(SelectedCell))
                            {
                                // no battle occured
                                fought = false;

                                // need to request a piece from the user
                                if (CurrentPlayer != Player) throw new Exception("Non-human must select a guess for a 1 attack");

                                // request the user to choose a piece
                                DestinationCell = cell; // keep the current cell
                                ChangeState(GameState.Play_PickOpponent);
                                OnNotify(NotifyReason.PickOpponent, null);
                            }
                            else
                            {
                                // a fight is happening
                                fought = true;

                                // check if the guess is accurate
                                if (SelectedCell.Piece == cell.Piece)
                                {
                                    OnNotify(NotifyReason.CorrectlyGuessedPiece,
                                        new CellState[] { new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = cell.Row, Col = cell.Col } });
                                    attackerWon = true;
                                    attackedWon = false;
                                }
                                else
                                {
                                    OnNotify(NotifyReason.IncorrectlyGuessedPiece,
                                        new CellState[] { new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = cell.Row, Col = cell.Col } });
                                    attackerWon = false;
                                    attackedWon = true;
                                }
                            }
                        }
                        else
                        {
                            // there was a battle
                            fought = true;

                            // normal battle rules apply
                            if (AttackingCell.Piece == cell.Piece)
                            {
                                // they both loss
                                attackerWon = attackedWon = false;
                            }
                            else if (cell.Piece == Piece.Bomb)
                            {
                                attackedWon = true;
                                attackerWon = false;
                                if (AttackingCell.Piece == Piece.BombSquad)
                                {
                                    attackerWon = true;
                                    attackedWon = false;
                                }
                            }
                            else if (cell.Piece == Piece.Flag)
                            {
                                attackerWon = true;
                                attackedWon = false;
                            }
                            else if (AttackingCell.Piece == Piece.Spy)
                            {
                                attackerWon = false;
                                attackedWon = true;
                                if (cell.Piece == Piece._10)
                                {
                                    attackerWon = true;
                                    attackedWon = false;
                                }
                            }
                            else if (AttackingCell.Piece > cell.Piece)
                            {
                                // attacker wins
                                attackerWon = true;
                                attackedWon = false;
                            }
                            else
                            {
                                // attacked wins
                                attackerWon = false;
                                attackedWon = true;
                            }
                        }

                        // check if a battle was fought
                        if (fought)
                        {
                            // change the board according to who won
                            var aPiece = AttackingCell.Piece;
                            var aPlayer = AttackingCell.Player;

                            // human outcome
                            var humanOutcome = NotifyReason.BattleTied;
                            if (!attackedWon && !attackerWon) humanOutcome = NotifyReason.BattleTied;
                            else if (aPlayer == Player)
                            {
                                // this is a human
                                if (attackerWon) humanOutcome = NotifyReason.BattleWon;
                                else humanOutcome = NotifyReason.BattleLost;
                            }
                            else
                            {
                                // this is the computer
                                if (attackerWon) humanOutcome = NotifyReason.BattleLost;
                                else humanOutcome = NotifyReason.BattleWon;
                            }

                            // inform the computer of a battle
                            Computer.Feedback_Battle(
                                new Coord() { Row = AttackingCell.Row, Col = AttackingCell.Col },
                                new Coord() { Row = cell.Row, Col = cell.Col },
                                humanOutcome == NotifyReason.BattleTied ? BattleOutcome.Tie :
                                (humanOutcome == NotifyReason.BattleWon ? BattleOutcome.Loss : BattleOutcome.Win)
                                );

                            // notify to the UI what happened
                            OnNotify(humanOutcome,
                                new CellState[]
                                {
                                    new CellState() { Player = AttackingCell.Player, Piece = AttackingCell.Piece, Row = AttackingCell.Row, Col = AttackingCell.Col },
                                    new CellState() { Player = cell.Player, Piece = Piece.Empty, Row = cell.Row, Col = cell.Col }
                                });
                            
                            // regardless the AttackingCell disappears
                            RemovePiece(AttackingCell);

                            // remove the attacked if it lost
                            if (!attackedWon)
                            {
                                RemovePiece(cell);
                            }

                            // place the attacker if wins
                            if (attackerWon)
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
                        if (state == State.SelectableOpponent)
                        {
                            // choose this cell
                            SelectedCell = cell;

                            // set the correct destination back
                            if (!IsValidCell(DestinationCell)) throw new Exception("Inavlid destination");
                            cell = DestinationCell;
                            DestinationCell = SelectInvalidCell();

                            OnNotify(NotifyReason.ChooseOpponent,
                                new CellState[] { new CellState() { Player = Player, Piece = SelectedCell.Piece, Row = cell.Row, Col = cell.Col } });

                            // change state back to battle
                            ChangeState(GameState.Play_Battle);
                            applyChange = true;
                        }
                        break;

                    case GameState.Play_OpponentPlay:
                        // Must set
                        //   AttackingCell - the cell that is attacking
                        //   cell - the cell that is being attacked (must be within a move of AttackingCell)
                        //   SelectedCell - the 'guess' if AttackingCell is a 1

                        // get a view of the board
                        var opponentView = GetBoardView(Player.Blue);

                        // get the initial piece layout ascynchronsly
                        var moveTask = new Task<OpponentMove>(() => { return Computer.Move(opponentView); });
                        moveTask.Start();
                        var opponentMove = await moveTask;

                        // get the moving piece (and validate)
                        state = GetCellState(opponentMove.From.Row, opponentMove.From.Col);
                        if (state != State.Playable) throw new Exception("Computer choose a non-playable piece to play");
                        SelectedCell = States[opponentMove.From.Row][opponentMove.From.Col];
                        if (SelectedCell.Player != CurrentPlayer ||
                            SelectedCell.Piece == Piece.Empty) throw new Exception("Computer choose an invalid piece to play");

                        // get the destination piece (and validate)
                        state = GetCellState(opponentMove.To.Row, opponentMove.To.Col);
                        if (state != State.Playable) throw new Exception("Computer choose a non-playable piece to play");
                        cell = States[opponentMove.To.Row][opponentMove.To.Col];
                        if (cell.Player == CurrentPlayer) throw new Exception("Computer choose an invalid piece to play");

                        // move & endturn or battle

                        // double check that this cell is valid for this cell
                        var opponentMoves = GetMoves(SelectedCell);

                        if (IsValidMove(cell, opponentMoves))
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
                                if (opponentMove.Guess != Piece.Empty)
                                {
                                    var coord = SelectablePieces[(int)CurrentPlayer][(int)opponentMove.Guess];
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
                        OnNotify(NotifyReason.GameOver, null);
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
                    List<OpponentMove> moves = new List<OpponentMove>();
                    if (cell.Player == player && cell.Piece != Piece.Empty)
                    {
                        var pmoves = GetMoves(cell);
                        if (pmoves != null)
                        {
                            foreach (var move in pmoves)
                            {
                                // transform into a consumable form
                                moves.Add(
                                    new OpponentMove()
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
            //  h = SelectablePlayer
            //  a = SelectableOpponent
            //  i = Island
            //                        1 1 1 1
            //    0 1 2 3 4 5 6 7 8 9 0 1 2 3
            //  0 x x x x x x x x x x x x x x
            //  1 x x p p p p p p p p p p x x
            //  2 x a p p p p p p p p p p a x
            //  3 x a p p p p p p p p p p a x
            //  4 x a p p i i p p i i p p a x
            //  5 x a p p i i p p i i p p a x
            //  6 x a p p p p p p p p p p a x
            //  7 x a p p p p p p p p p p a x
            //  8 x x p p p p p p p p p p x x
            //  9 x h h h h h h h h h h h h x
            // 10 x x x x x x h x x x x x x x
            // 11 x x x x x x x x x x x x x x

            // is Island
            if ((row == (BoardStarting.Row + GamePlayRows) || 
                row == (BoardStarting.Row + GamePlayRows + 1)) 
                &&
                (col == (BoardStarting.Col + 2) || 
                col == (BoardStarting.Col + 3) || 
                col == (BoardStarting.Col + GamePlayCols - 4) || 
                col == (BoardStarting.Col + GamePlayCols - 3))) return State.Island;

            // is playable
            if ((row >= BoardStarting.Row && 
                row < (BoardStarting.Row+BoardRows) && 
                col >= BoardStarting.Col && 
                col < (BoardStarting.Col + BoardColumns))) return State.Playable;

            // is selectable (player)
            if ((row == (BoardStarting.Row + BoardRows) &&
            col >= (BoardStarting.Col - 1) &&
            col <= (BoardStarting.Col + BoardColumns))
            ||
            (row == (BoardStarting.Row + BoardRows + 1) &&
            col == BoardStarting.Col + (BoardColumns / 2))) return State.SelectablePlayer;


            // is selectable (opponent)
            if ((col == (BoardStarting.Col-1) && 
                row >= (BoardStarting.Row+1) && 
                row <= (BoardStarting.Row + BoardRows - 2))
                || 
                (col == (BoardStarting.Col + BoardColumns) && 
                row >= (BoardStarting.Row + 1) && 
                row <= (BoardStarting.Row + BoardRows - 2))) return State.SelectableOpponent;

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
            if (cell.State != State.Playable && cell.State != State.SelectablePlayer) throw new Exception("Invalid cell to highlight");

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
            if (cell.State != State.Playable && cell.State != State.SelectablePlayer) throw new Exception("Invalid cell to highlight");

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
