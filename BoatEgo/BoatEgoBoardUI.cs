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

        //
        // Init
        //
        private void Initialize(int width, int height)
        {
            // init control
            DoubleBuffered = true;
            Height = width;
            Width = height;

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
                    Background = new RGBA() { R = 0, G = 100, B = 200, A = 255 }
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
            Game.OnNotifyInfo += Game_OnNotifyInfo;
            Game.OnNotifyChange += Game_OnNotifyChange;

            // paint the full board
            for (int r = 0; r < BoatEgoBoard.Rows; r++)
            {
                for (int c = 0; c < BoatEgoBoard.Columns; c++)
                {
                    UpdateCell(Game[r, c]);
                }
            }

            // add an overlay to the user to start placing pieces
            AddOverlay("Start placing your pieces");
        }

        //
        // Callbacks
        //

        private void Board_OnCellClicked(int row, int col, float x, float y)
        {
            if (Game != null) Game.Select(row, col);
        }

        private void Game_OnCellUpdate(CellState cell)
        {
            UpdateCell(cell);
        }

        private void Game_OnNotify(NotifyReason reason)
        {
            AddOverlay(reason.ToString());
        }

        private void Game_OnNotifyChange(NotifyReason reason, CellState from, CellState to)
        {
            AddOverlay(string.Format("{0} [{1},{2},{3}x{4}] [{5},{6},{7}x{8}]",
                reason.ToString(),
                from.Player,
                from.Piece,
                from.Row,
                from.Col,
                to.Player,
                to.Piece,
                to.Row,
                to.Col));
        }

        private void Game_OnNotifyInfo(NotifyReason reason, CellState cell)
        {
            AddOverlay(string.Format("{0} [{1},{2},{3}x{4}]",
                reason.ToString(),
                cell.Player,
                cell.Piece,
                cell.Row,
                cell.Col));
        }

        //
        // UI updates
        //

        private void AddOverlay(string text)
        {
            Board.UpdateOverlay((img) =>
            {
                img.Graphics.Clear(RGBA.White);
                img.MakeTransparent(RGBA.White);
                img.Graphics.Text(RGBA.Black, img.Width / 5, img.Height / 2, text, 12);
            });
        }

        private void UpdateCell(CellState cell)
         {
            // update the cell
            Board.UpdateCell(cell.Row, cell.Col, (img) =>
            {
                switch (cell.State)
                {
                    case State.Nothing:
                        break;

                    case State.Island:
                        img.Graphics.Image(IslandImage, 0, 0, img.Width, img.Height);
                        break;

                    case State.Playable:
                        // display the piece
                        DrawPiece(cell.Player, 
                            cell.Piece, 
                            true, // border
                            (cell.Player == Player.Red) || PlayOpenFace, // visible
                            false, // show remaining pieces
                            img);
                        break;

                    case State.SelectableHome:
                    case State.SelectableAway:
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
            }

            // add border
            if (border) img.Graphics.Image(BorderImage, 0, 0, img.Width, img.Height);

            if (piece == Piece.Empty) return;

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
