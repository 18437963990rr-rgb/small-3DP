using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GTSDemoCSharp
{
    //例程7-3 访问DAC
    public partial class Form1 : Form
    {
        short axis;//当前轴号
        uint clk;//时钟参数

        //电压值
        short sSetValue;
        short sGetValue;

        public Form1()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 界面加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            this.comboBox1.SelectedIndex = 0;
        }
        /// <summary>
        /// 打开运动控制卡
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            gts.mc.GT_Open(0,1);
            gts.mc.GT_Reset();
        }
        /// <summary>
        /// 设置输出电压
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);//读取轴号
            if (Convert.ToInt16(textBox1.Text) > 10 || Convert.ToInt16(textBox1.Text) < -10)
                MessageBox.Show("设定的电压值超出允许的范围，请重新设定（-10V~10V）！");
            else
            {
                sSetValue = (short)(32767 * Convert.ToInt16(textBox1.Text) / 10);//计算轴的输出电压值
                gts.mc.GT_SetDac(axis,ref sSetValue,1);//设置轴输出电压
            }
        }
        /// <summary>
        /// 获取电压值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);//读取轴号
            gts.mc.GT_GetDac(axis,out sGetValue,1,out clk);//获取轴的输出电压值
            this.textBox2.Text = Math.Round((double)sGetValue*10/32767, 2).ToString();
        }
        
        
    }
}
