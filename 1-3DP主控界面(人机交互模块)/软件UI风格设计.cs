using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX.Mathematics.Interop;

namespace BinderJetting
{
    public partial class 软件UI风格设计 : Form
    {
        public RawColor4[] ColorCards = new RawColor4[8];//8支画笔的色彩
        

        public 软件UI风格设计(RawColor4[] InputColorCards)
        {
            InitializeComponent();        
            ColorCards = InputColorCards;
            InitColorLabel();
        }
        private void InitColorLabel()
        {
            foreach (Control label in this.Controls)
            {
                if (label.Tag!=null && label is Label)
                {
                    label.BackColor= Color.FromArgb(
                        (byte)(ColorCards[Convert.ToInt32(label.Tag) - 1].A * 255f),
                        (byte)(ColorCards[Convert.ToInt32(label.Tag) - 1].R * 255f),
                        (byte)(ColorCards[Convert.ToInt32(label.Tag) - 1].G * 255f),
                        (byte)(ColorCards[Convert.ToInt32(label.Tag) - 1].B * 255f)
                        );
                }
            }
        }

        private void ColorLabelBtn_Click(object sender, EventArgs e)
        {
            //新建颜色对话框对象
            ColorDialog cd = new ColorDialog();
            //展示对话框
            cd.ShowDialog();
            //将选中的颜色赋值给textBox的字体
            (sender as Control).BackColor/*ForeColor*/ = cd.Color;
            ColorCards[Convert.ToInt32((sender as Control).Tag)-1]= new RawColor4((float)(cd.Color.R)/255f, (float)(cd.Color.G) / 255f,
                (float)(cd.Color.B )/ 255f, (float)(cd.Color.A)/ 255f);
        }
    }
}
