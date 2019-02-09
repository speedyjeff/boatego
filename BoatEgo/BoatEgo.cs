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
            var width = BoatEgoBoard.Columns * 50;
            var height = BoatEgoBoard.Rows * 50;

            Board = new BoatEgoBoardUI(width, height)
            {
                Width = width,
                Height = height,
                PlayOpenFace = true
            };
            this.Controls.Add(Board);
        }

        #region private
        private BoatEgoBoardUI Board;        
        #endregion
    }
}
