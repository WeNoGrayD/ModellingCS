using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZedGraph;

namespace CS_lab3
{
    public partial class fDiagramView : Form
    {
        frmDCS DCS;

        public fDiagramView(frmDCS dcs, ZedGraphControl zgc)
        {
            InitializeComponent();

            DCS = dcs;

            this.Controls.Add(zgc);

            zgc.BringToFront();

            zgc.Size = new Size(this.Size.Width - 50, this.Size.Height - 50);

            zgc.AxisChange();
            zgc.Invalidate();
        }
    }
}
