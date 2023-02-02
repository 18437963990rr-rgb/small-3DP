using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _3_附属控件_UserControl3
{
    public partial class UserControl3: UserControl
    {
        public UserControl3()
        {
            InitializeComponent();
        }

        private string _Text1 = "AI 01";
        private string _Text2 = "备用";
        private string _Text3 = "10";
        private string _Text4 = "0";
        private string _Text5 = "0";
        private string _Text6 = "100";
        private string _Text7 = "抱闸";
        private string _Text8 = "无效";

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
        public string Text7
        {
            set { _Text7 = value; }
            get { return _Text7; }
        }
        [CategoryAttribute("自定义属性"),
        DescriptionAttribute("自定义对话框中label属性"),
        DefaultValue("")]
        public string Text8
        {
            set { _Text8 = value; }
            get { return _Text8; }
        }

        private void Ai控件_Load(object sender, EventArgs e)
        {
            this.label1.Text = _Text1;
            this.comboBox1.Text = Text2;
            this.comboBox2.Text = _Text3;
            this.comboBox3.Text = _Text4;
            this.textBox1.Text = _Text5;
            this.label2.Text = _Text6;
            this.comboBox4.Text = _Text7;
            this.comboBox5.Text = _Text8;

        }
    }
}
