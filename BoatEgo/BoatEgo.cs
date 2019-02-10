using engine.Common;
using engine.Winforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BoatEgo
{
    public partial class BoatEgo : Form
    {
        public BoatEgo()
        {

            InitializeComponent();
            Width = BoatEgoBoard.Columns * 50;
            Height = BoatEgoBoard.Rows * 50;

            Board = new BoatEgoBoardUI(Width, Height)
            {
                Width = Width,
                Height = Height,
                PlayOpenFace = false
            };
            this.Controls.Add(Board);
        }

        #region private
        private BoatEgoBoardUI Board;        
        #endregion
    }
}
