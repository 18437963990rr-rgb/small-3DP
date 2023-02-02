using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomPanel
{
    public partial class DoublePanel:Panel
    {
        //public DoublePanel()
        //{
        //    InitializeComponent();
        //}

        public DoublePanel()
        {
            InitializeComponent();
            SetStyle(ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer
            /*ControlStyles.ResizeRedraw| ControlStyles.SupportsTransparentBackColor*/, true);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
        }

    }
}
