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
    //例程7-4 访问ADC
    public partial class Form1 : Form
    {
        uint clk;//时钟参数
        //电压值
        double[] dGetVoltageValue=new double[4];
        //数字转换值
        short[] sGetDigitalValue = new short[4];

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
            
        }
        
        /// <summary>
        /// 运动状态刷新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            //读取4个通道的输入电压
            gts.mc.GT_GetAdc(1, out dGetVoltageValue[0], 4,out clk);//20200416新增：直接读取电压，采样频率8kHz,8路12位/16位精度的外部模拟电压输入通道
            this.textBox1.Text = Math.Round(dGetVoltageValue[0], 1).ToString();
            this.textBox2.Text = Math.Round(dGetVoltageValue[1], 1).ToString();
            this.textBox3.Text = Math.Round(dGetVoltageValue[2], 1).ToString();
            this.textBox4.Text = Math.Round(dGetVoltageValue[3], 1).ToString();
            //读取4个通道的输入电压的数字转换值
            gts.mc.GT_GetAdcValue(1, out sGetDigitalValue[0], 4, out clk);//20200416新增：直接读取电压
            this.textBox5.Text = sGetDigitalValue[0].ToString();
            this.textBox6.Text = sGetDigitalValue[1].ToString();
            this.textBox7.Text = sGetDigitalValue[2].ToString();
            this.textBox8.Text = sGetDigitalValue[3].ToString();
        }
        /// <summary>
        /// 打开控制卡
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            gts.mc.GT_Open(0,1);
        }
        /// <summary>
        /// 读取编码器位置值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Start();//启动定时器，循环检测各个通道输入电压值
        }
        
    }
}
