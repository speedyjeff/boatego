using engine.Common;
using engine.Winforms;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BoatEgo
{
    public partial class BoatEgoBoardUI : UserControl
    {
        public BoatEgoBoardUI()
        {
            InitializeComponent();
            Initialize(Width, Height);
        }

        public BoatEgoBoardUI(int width, int height)
        {
            InitializeComponent();
            Initialize(width, height);
        }

        public bool PlayOpenFace { get; set; }

        #region private
        private Random Rand;
        private UIHookup UI;
        private Board Board;
        private BoatEgoBoard Game;

        // UI elements
        private IImage[] NotshowingPieceImages;
        private IImage[] WavesImages;
        private IImage[] BaseImages;
        private IImage[] PieceImages;
        private IImage BorderImage;
        private IImage IslandImage;
        private IImage EdgeImage;
        private IImage[] ScrollImages;
        private IImage WinImage;
        private IImage LostImage;
        private IImage TitleImage;

        // overlay tracking
        private string OPreviousText = "";
        private bool OHighlightAwayPieces = false;
        private CellState OOpponentMoveFrom = default(CellState);
        private CellState OPlayerMoveFrom = default(CellState);
        private List<Tuple<bool, CellState>> OBattles = new List<Tuple<bool,CellState>>();

        // additional tracking for visible pieces during battles
        private List<CellState> ForceVisible = new List<CellState>();

        // tutorial tracking
        private bool IsTuturial = true;

        //
        // Init
        //
        private void Initialize(int width, int height)
        {
            // init control
            DoubleBuffered = true;
            Height = height;
            Width = width;

            // init
            Rand = new Random();

            // setup board
            Board = new Board(
                new BoardConfiguration()
                {
                    Height = Height,
                    Width = Width,
                    Columns = BoatEgoBoard.Columns,
                    Rows = BoatEgoBoard.Rows,
                    Background = new RGBA() { R = 51, G = 158, B = 247, A = 255 }
                }
            );
            Board.OnCellClicked += Board_OnCellClicked;

            // init UI
            UI = new UIHookup(this, Board);

            // load images
            var images = Resources.LoadImages(System.Reflection.Assembly.GetExecutingAssembly(), UI.Surface);
            // mark them all as transparent
            foreach (var kvp in images)
            {
                kvp.Value.MakeTransparent(RGBA.White);
            }

            // store some for quick access

            // artifacts
            BorderImage = images["border"];
            IslandImage = images["island"];
            EdgeImage = images["edge"];
            ScrollImages = new IImage[]
                {
                    images["scroll"],
                    images["scrolllong"]
                };
            WinImage = images["win"];
            LostImage = images["lost"];
            TitleImage = images["title"];

            // waves
            WavesImages = new IImage[]
                {
                images["waves1"],
                images["waves2"],
                images["waves3"],
                };

            // bases
            BaseImages = new IImage[2];
            BaseImages[(int)Player.Red] = images["redbase"];
            BaseImages[(int)Player.Blue] = images["bluebase"];

            // pieces (not visible)
            NotshowingPieceImages = new IImage[2];
            NotshowingPieceImages[(int)Player.Red] = images["redpiece"];
            NotshowingPieceImages[(int)Player.Blue] = images["bluepiece"];

            // pieces (visible)
            PieceImages = new IImage[BoatEgoBoard.NumberOfPieces];
            PieceImages[(int)Piece.Spy] = images["diver"];
            PieceImages[(int)Piece._1] = images["floaty"];
            PieceImages[(int)Piece._2] = images["duck"];
            PieceImages[(int)Piece.BombSquad] = images["rov"];
            PieceImages[(int)Piece._4] = images["turtle"];
            PieceImages[(int)Piece._5] = images["rowboat"];
            PieceImages[(int)Piece._6] = images["sailboat"];
            PieceImages[(int)Piece._7] = images["squid"];
            PieceImages[(int)Piece._8] = images["shark"];
            PieceImages[(int)Piece._9] = images["submarine"];
            PieceImages[(int)Piece._10] = images["ironclad"];
            PieceImages[(int)Piece.Bomb] = images["bomb"];
            PieceImages[(int)Piece.Flag] = images["chest"];

            // init the game engine
            Game = new BoatEgoBoard();
            Game.OnCellUpdate += Game_OnCellUpdate;
            Game.OnNotify += Game_OnNotify;

            // paint the full board
            for (int r = 0; r < BoatEgoBoard.Rows; r++)
            {
                for (int c = 0; c < BoatEgoBoard.Columns; c++)
                {
                    UpdateCell(Game[r, c]);
                }
            }

            // add an overlay to the user to start placing pieces
            IsTuturial = true;
            AddOverlay("Start placing your pieces");
        }

        //
        // Callbacks
        //

        private void Board_OnCellClicked(int row, int col, float x, float y)
        {
            // turn off the tuturial if it is on
            if (IsTuturial)
            {
                IsTuturial = false;
                AddOverlay(OPreviousText);
            }

            if (Game != null) Game.Select(row, col);
        }

        private void Game_OnCellUpdate(CellState cell)
        {
            UpdateCell(cell);
        }

        private void Game_OnNotify(NotifyReason reason, CellState[] cells)
        {
            switch (reason)
            {
                // game setup
                case NotifyReason.PiecePlaced:
                    if (cells == null || cells.Length != 1) throw new Exception("Invalid cells sent to notify");
                    AddOverlay(string.Format("Placed a {0}", cells[0].Piece.ToString().Replace("_", "")));
                    break;
                case NotifyReason.InvalidCellSelected:
                    AddOverlay("Cannot place a piece on this cell, try again");
                    break;

                // choose an opponent's piece to win the battle
                case NotifyReason.PickOpponent:
                    OHighlightAwayPieces = true;
                    AddOverlay("Choose an opponent's piece. If you guess correctly you win the battle.");
                    break;

                // battle
                case NotifyReason.BattleLost:
                case NotifyReason.BattleWon:
                case NotifyReason.BattleTied:
                    if (cells == null || cells.Length != 2) throw new Exception("Invalid cells sent to notify");

                    // track human or opponenet move
                    if (cells[0].Player == Player.Red) OPlayerMoveFrom = cells[0];
                    else OOpponentMoveFrom = cells[0];

                    // clear the battles if human
                    if (cells[0].Player == Player.Red) OBattles.Clear();

                    // indicate that a cell should be marked for a battle
                    OBattles.Add(new Tuple<bool,CellState>(reason == NotifyReason.BattleWon, cells[1] ));

                    // turn off highlighting if needed to guess
                    OHighlightAwayPieces = false;

                    // update the UI
                    ForceVisible.Add(cells[1]);
                    GetCellAndUpdate(cells[1].Row, cells[1].Col);

                    // update, but do not change the current text
                    AddOverlay(OPreviousText);
                    break;

                // piece moved
                case NotifyReason.PieceMove:
                    if (cells == null || cells.Length != 2) throw new Exception("Invalid cells sent to notify");
                    // track human or opponenet move
                    if (cells[0].Player == Player.Red) OPlayerMoveFrom = cells[0];
                    else OOpponentMoveFrom = cells[0];

                    // clear the battles if human
                    if (cells[0].Player == Player.Red)
                    {
                        // clear
                        OBattles.Clear();

                        // update the force visible cells
                        var update = ForceVisible.ToArray();
                        ForceVisible.Clear();
                        foreach (var cell in update) GetCellAndUpdate(cell.Row, cell.Col);
                    }
                    else
                    {
                        // check if one of the force visible cells has moved
                        CellState update = default(CellState);
                        foreach(var cell in ForceVisible)
                        {
                            if (cell.Row == cells[0].Row && cell.Col == cells[0].Col) update = cell;
                        }
                        if (!update.Equals(default(CellState)))
                        {
                            // found it, so delete and move it
                            ForceVisible.Remove(update);
                            ForceVisible.Add(cells[1]);
                        }
                    }

                    // update, but do not change the current text
                    AddOverlay(OPreviousText);
                    break;

                // indicate that it is time to pick a piece to play
                case NotifyReason.YourTurn:
                    // todo
                    AddOverlay("It is your turn");
                    break;

                case NotifyReason.InvalidMove:
                    AddOverlay("That move is invalid. Choose another piece.");
                    break;

                // check for the win
                case NotifyReason.StaleMate:
                    AddOverlay("Stale mate. No winner.");
                    break;

                case NotifyReason.PlayerWins:
                    AddOverlay("You win!");
                    break;

                case NotifyReason.OpponentWins:
                    AddOverlay("Opponent wins.");
                    break;

                case NotifyReason.GameOver:
                    AddOverlay(OPreviousText);
                    break;

                // nothing
                case NotifyReason.AllPiecesAreInPlay:
                case NotifyReason.CorrectlyGuessedPiece:
                case NotifyReason.IncorrectlyGuessedPiece:
                case NotifyReason.ChooseOpponent:
                case NotifyReason.PlayerPiecesSet:
                case NotifyReason.TheirTurn:
                    break;

                default:
                    throw new Exception("Inavlid Notify reason : " + reason);
            }
        }

        //
        // UI updates
        //

        private void AddOverlay(string text)
        {
            // determine where the best place is to put the message
            var x = 0;
            var y = 0;
            var xdelta = 0;
            var ydelta = 0;
            var width = 0;
            var height = 0;
            IImage background = null;
            var lines = new List<string>();

            // determine where to put messages
            if (Game.Phase == GamePhase.Initializing)
            {
                background = ScrollImages[0];
                x = Board.CellWidth * 2;
                y = Board.CellHeight;
                width = Board.CellWidth * 4;
                height = Board.CellHeight * 4;
                xdelta = 30;
                ydelta = 55;

                // break text into lines
                var linesize = 17;
                var start = 0;
                do
                {
                    var length = start + linesize > text.Length ? text.Length - start: linesize;
                    if (start + length < text.Length)
                    {
                        // walk back to the last whitespace
                        while (!char.IsWhiteSpace(text[start + length]))
                        {
                            length--;
                            if (length == 0)
                            {
                                // give up
                                length = linesize;
                                break;
                            }
                        }
                    }
                    lines.Add(text.Substring(start, length));
                    start += length;
                }
                while (start < text.Length);
            }
            else
            {
                background = ScrollImages[1];
                x = Board.CellWidth;
                y = Board.CellHeight * (Board.Rows-2); // exclude borders
                height = Board.CellHeight;
                width = Board.CellWidth * (Board.Columns-2); // exclude border
                xdelta = 35;
                ydelta = 20;

                // include all text
                lines.Add(text);
            }

            // update the overlay
            Board.UpdateOverlay((img) =>
            {
                // clear
                img.Graphics.Clear(RGBA.White);
                img.MakeTransparent(RGBA.White);

                // put scroll
                img.Graphics.Image(background, x, y, width, height);

                // put messages
                for (int i=0; i<lines.Count; i++)
                {
                    img.Graphics.Text(RGBA.Black, x + xdelta, y + ydelta + (i*18), lines[i], 12);
                }

                // highlight the previous move places
                foreach (var cell in new CellState[] { OPlayerMoveFrom, OOpponentMoveFrom })
                {
                    if (!cell.Equals(default(CellState)))
                    {
                        img.Graphics.Rectangle(new RGBA()
                        {
                            R = (byte)(cell.Player == Player.Red ? 255 : 150),
                            G = 150,
                            B = (byte)(cell.Player == Player.Blue ? 255 : 150),
                            A = 200
                        },
                            Board.CellWidth * cell.Col,
                            Board.CellHeight * cell.Row,
                            Board.CellWidth,
                            Board.CellHeight,
                            true, // fill
                            false // no border
                            );
                    }
                }

                // highlight the opponenets selectable pieces
                if (OHighlightAwayPieces)
                {
                    img.Graphics.Rectangle(new RGBA() { R = 255, G = 255, A = 100 }, Board.CellWidth, Board.CellHeight * 2, Board.CellWidth, Board.CellHeight * 6, true, true);
                    img.Graphics.Rectangle(new RGBA() { R = 255, G = 255, A = 100 }, Board.Width - (Board.CellWidth*2), Board.CellHeight * 2, Board.CellWidth, Board.CellHeight * 6, true, true);
                }

                // mark the battles
                foreach (var b in OBattles)
                {
                    // mark these cells as a win or loss
                    if (b.Item1)
                    {
                        img.Graphics.Image(WinImage, b.Item2.Col * Board.CellWidth, b.Item2.Row * Board.CellHeight, Board.CellWidth / 5, Board.CellHeight / 5);
                    }
                    else
                    {
                        img.Graphics.Image(LostImage, ((b.Item2.Col + 1) * Board.CellWidth) - (Board.CellWidth / 5), b.Item2.Row * Board.CellHeight, Board.CellWidth / 5, Board.CellHeight / 5);
                    }
                }

                // display the tutorial
                if (IsTuturial)
                {
                    // put title image
                    img.Graphics.Rectangle(new RGBA() { R = 255, G = 255, B = 255, A = 230 }, Board.CellWidth, Board.CellHeight, Board.Width - (Board.CellWidth * 2), Board.CellHeight * 8, true, true);
                    img.Graphics.Image(TitleImage, Board.CellWidth, Board.CellHeight, Board.Width - (Board.CellWidth * 2), Board.CellHeight * 4);

                    // insert instructions
                    var font = 12;
                    var ty = Board.CellHeight * 5;
                    img.Graphics.Text(RGBA.Black, Board.CellWidth * 2, ty, "Welcome to BoatEgo, the game of strategy. You control the Red pieces.", font);
                    ty += 30;
                    img.Graphics.Text(RGBA.Black, Board.CellWidth * 2, ty, "Your mission is to capture Blue's Flag.  Start by placing your 30 soliders", font);
                    ty += 30;
                    img.Graphics.Text(RGBA.Black, Board.CellWidth * 2, ty, "in the lowest 3 rows. Once your last piece is placed the battle begins.", font);
                    ty += 30;
                    img.Graphics.Text(RGBA.Black, Board.CellWidth * 2, ty, "Battles are won and lost based on the value of the piece (the value is ", font);
                    ty += 30;
                    img.Graphics.Text(RGBA.Black, Board.CellWidth * 2, ty, "displayed in the small white box).  A few special rules exist:", font);
                    ty += 30;
                    img.Graphics.Text(RGBA.Black, Board.CellWidth * 2, ty, "(S) capture 10s, (3) capture Bombs, and (1) capture any guessed piece", font);

                    ty += 30;
                    img.Graphics.Text(RGBA.Black, Board.CellWidth * 2, ty, "<click to start>", 8);
                }
            });



            // retain the text from this overlap
            OPreviousText = text;
        }

        private void GetCellAndUpdate(int row, int col)
        {
            UpdateCell(Game[row,col]);
        }

        private void UpdateCell(CellState cell)
         {
            // update the cell
            Board.UpdateCell(cell.Row, cell.Col, (img) =>
            {
                switch (cell.State)
                {
                    case State.Nothing:
                        img.Graphics.Image(EdgeImage, 0, 0, img.Width, img.Height);
                        break;

                    case State.Island:
                        img.Graphics.Image(IslandImage, 0, 0, img.Width, img.Height);
                        break;

                    case State.Playable:
                        // display the piece

                        // the piece is visible if...
                        //  this is the home player
                        //  playopenface
                        //  or the piece was just part of a battle
                        var visible = (cell.Player == Player.Red) || PlayOpenFace;
                        if (!visible)
                        {
                            // check if this cell has been requested to be visible (eg. a battle)
                            foreach(var fcell in ForceVisible)
                            {
                                if (fcell.Row == cell.Row && fcell.Col == cell.Col) visible = true;
                            }
                        }

                        DrawPiece(cell.Player, 
                            cell.Piece, 
                            true, // border
                            visible, // visible
                            false, // show remaining pieces
                            img);
                        break;

                    case State.SelectablePlayer:
                    case State.SelectableOpponent:
                        // display piece
                        DrawPiece(cell.Player, 
                            cell.Piece, 
                            false, // no border
                            true, // visible
                            true, // show remaining pieces
                            img);

                        break;
                    default: throw new Exception("Unknown state : " + cell.State);
                }

                // add highlight
                if (cell.IsHighlighted)
                { 
                    img.Graphics.Rectangle(new RGBA() { A = 100, R = 255, G = 255, B = 0 }, 0, 0, img.Width, img.Height, true, false);
                }
            });
        }

        private void DrawPiece(Player player, Piece piece, bool border, bool visible, bool showRemaining, IImage img)
        {
            // replace with background
            if (!string.IsNullOrWhiteSpace(Board.BackgroundImage))
            {
                img.Graphics.Image(Board.BackgroundImage, 0, 0, img.Width, img.Height);
            }
            else
            {
                img.Graphics.Clear(Board.BackgroundColor);
            }

            if (piece == Piece.Empty)
            {
                // add waves
                var wave = Rand.Next() % WavesImages.Length;
                img.Graphics.Image(WavesImages[wave], 0, 0, img.Width, img.Height);
                return;
            }

            // add border
            if (border) img.Graphics.Image(BorderImage, 0, 0, img.Width, img.Height);
            else
            {
                // make the background a shade of the player
                if (player == Player.Red)
                {
                    img.Graphics.Clear(new RGBA() { R = 150, A = 255 });
                }
                else if (player == Player.Blue)
                {
                    img.Graphics.Clear(new RGBA() { B = 150, A = 255 });
                }
            }

            if (visible)
            {
                // add base
                if (player == Player.Red) img.Graphics.Image(BaseImages[(int)Player.Red], 0, 0, img.Width, img.Height);
                else if (player == Player.Blue) img.Graphics.Image(BaseImages[(int)Player.Blue], 0, 0, img.Width, img.Height);

                // add piece
                img.Graphics.Image(PieceImages[(int)piece], 0, 0, img.Width, img.Height);

                // add piece number
                img.Graphics.Rectangle(RGBA.White, img.Width - 20, img.Height - 20, 12, 12, true /*fill*/, false /*border*/);
                var name = ((int)piece).ToString();
                if (piece == Piece.Spy) name = "S";
                else if (piece == Piece.Bomb) name = "B";
                else if (piece == Piece.Flag) name = "F";
                if (name.Length > 1) img.Graphics.Text(RGBA.Black, img.Width - 22, img.Height - 20, name, 8);
                else img.Graphics.Text(RGBA.Black, img.Width - 20, img.Height - 20, name, 8);

                // if asked to show the remaining pieces
                if (showRemaining)
                {
                    img.Graphics.Rectangle(RGBA.Black, 0, 0, 12, 12, true /*fill*/, false /*border*/);
                    img.Graphics.Text(RGBA.White, 0, 0, (Game.PieceInfo(player, piece).Remaining).ToString(), 8);
                }
            }
            else
            {
                // add platform
                if (player == Player.Red) img.Graphics.Image(NotshowingPieceImages[(int)Player.Red], 0, 0, img.Width, img.Height);
                else if (player == Player.Blue) img.Graphics.Image(NotshowingPieceImages[(int)Player.Blue], 0, 0, img.Width, img.Height);
            }
        }
        #endregion  
    }
}
