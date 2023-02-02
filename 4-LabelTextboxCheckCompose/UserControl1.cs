using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LabelTextboxCheckCompose
{
    public partial class UserControl1: UserControl
    {
        public UserControl1()
        {
            InitializeComponent();

        }
        private string _Text1 = "I 01";
        private string _Text2 = "备用";
        private int _Text3 = 0;
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
        public int Text3//值为0-1，0为无效，1为有效效
        {
            set { _Text3 = value; }
            get { return _Text3; }
        }

        private void UserControl1_Load(object sender, EventArgs e)
        {
            this.label.Text = _Text1;
            this.textBox.Text = _Text2;
            //this.ComboBox.Checked = _Text3;
            this.ComboBox.SelectedIndex = _Text3;
        }
    }
}
