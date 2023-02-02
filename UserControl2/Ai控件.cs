using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UserControl2
{
    public partial class Ai控件: UserControl
    {
        public Ai控件()
        {
            InitializeComponent();
        }

        private string _Text1 = "AI 01";
        private string _Text2 = "备用";
        private string _Text3 = "10";
        private string _Text4 = "0";
        private string _Text5 = "0";
        private string _Text6 = "100";
        private int _Text7 = 0;

        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public string Text1
        {
            set { _Text1 = value; }
            get { return _Text1; }
        }
        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public string Text2
        {
            set { _Text2 = value; }
            get { return _Text2; }
        }
        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public string Text3
        {
            set { _Text3 = value; }
            get { return _Text3; }
        }

        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public string Text4
        {
            set { _Text4 = value; }
            get { return _Text4; }
        }
        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public string Text5
        {
            set { _Text5 = value; }
            get { return _Text5; }
        }
        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public string Text6
        {
            set { _Text6 = value; }
            get { return _Text6; }
        }
        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public int Text7
        {
            set { _Text7 = value; }
            get { return _Text7; }
        }

        private void Ai控件_Load(object sender, EventArgs e)
        {
            this.label1.Text = _Text1;
            this.textBox1.Text = _Text2;
            this.textBox2.Text = _Text3;
            this.textBox3.Text = _Text4;
            this.textBox4.Text = _Text5;
            this.textBox5.Text = _Text6;
            //this.checkBox1.Checked = _Text7;
            this.ComboBox.SelectedIndex = _Text7;
        }
    }
}
