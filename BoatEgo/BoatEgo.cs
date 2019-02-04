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

            Board = new BoatEgoBoard(500, 600)
            {
                Width = 600,
                Height = 500,
                PlayOpenFace = true
            };
            this.Controls.Add(Board);
        }

        #region private
        private BoatEgoBoard Board;        
        #endregion
    }
}
