using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace jog
{
    //例程6-2 Jog运动
    public partial class Form1 : Form
    {
        short axis;
        bool[] en=new bool[8];
        uint clk;
        double vel, prfvel, encvel, prfpos, encpos;
        gts.mc.TJogPrm jog;
        public Form1()
        {
            InitializeComponent();
        }
        //加载界面
        //打开控制器：——————————————目的是：打开运动控制器，首先是和控制卡开启通讯
        //
        private void Form1_Load(object sender, EventArgs e)
        {
            gts.mc.GT_Open(0,1);//打开运动控制卡
            this.comboBox1.SelectedIndex = 0;

        }

        // 该函数检测某条GT指令的执行结果，command为指令名称，error为指令执行返回值
        static void commandhandler(string command, short error)
        {
            // 如果指令执行返回值为非0，说明指令执行错误，向屏幕输出错误结果
            if (error != 0)
            {
                Console.WriteLine("{0}={1}\n", command, error);
            }
        }

        //
        //复位控制器：————————————————目的是：复位控制模式——复位后，默认的控制模式是“脉冲+方向的脉冲控制方式”
        //
        private void button6_Click(object sender, EventArgs e)
        {
            gts.mc.GT_Reset();
        }
        //
        //初始化控制器：———————————————目的是：（1）下载配置文件（具体就包括，控制器软硬件资源的配置：设置各轴的报警、正负限位、是否有效、规划模式等等）；
        //初始化控制器：———————————————目的是：（2）清除指定轴的状态字；
        //初始化控制器：———————————————目的是：（3）设置轴的运动模式（具体体现为：点位运动、JOG运动、电子齿轮运动、插补运动、电子凸轮运动等等）。
        private void button1_Click(object sender, EventArgs e)
        {
            gts.mc.GT_LoadConfig("GTS800-加限位.cfg");//下载配置文件
            gts.mc.GT_ClrSts(1,8);//清除各轴报警和限位
            gts.mc.GT_PrfJog(axis);//设置为jog模式            
        }

        //
        //伺服使能
        //
        private void button5_Click(object sender, EventArgs e)
        {
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);
                if (!en[axis-1])
                {
                    gts.mc.GT_AxisOn(axis);//上伺服
                    this.button5.Text = "伺服关闭";
                }
                if (en[axis-1])
                {
                    gts.mc.GT_AxisOff(axis);//下伺服
                    this.button5.Text = "伺服使能";
                }
                en[axis - 1] = !en[axis - 1];          
        }
        //
        //刷新状态
        //
        private void timer1_Tick(object sender, EventArgs e)
        {
            //axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);//读取轴号
            axis = 3;

            if (!en[Convert.ToInt32(this.comboBox1.Text) - 1])
            //if (axis !=0)
            {
                this.button5.Text = "伺服使能";
            }
            //if (axis != 0)
            if (en[Convert.ToInt32(this.comboBox1.Text) - 1])
            {
                this.button5.Text = "伺服关闭";
            }
            //读取规划位置并显示
            gts.mc.GT_GetPrfPos(axis,out prfpos,1,out clk);
            this.textBox6.Text = Math.Round(prfpos,1).ToString();
            //读取规划速度并显示
            gts.mc.GT_GetPrfVel(axis,out prfvel,1,out clk);
            this.textBox7.Text = Math.Round(prfvel, 6).ToString();
            //读取实际位置并显示
            gts.mc.GT_GetEncPos(axis,out encpos,1,out clk);
            this.textBox8.Text = Math.Round(encpos, 1).ToString();
            //读取实际速度并显示
            gts.mc.GT_GetEncVel(axis,out encvel,1,out clk);
            this.textBox9.Text = Math.Round(encvel, 6).ToString();
        }
        //
        //位置清零
        //
        private void button4_Click(object sender, EventArgs e)
        {
            gts.mc.GT_ZeroPos(1,8);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            // 指令返回值
            short sRtn;
            // EXO6输出高电平，使指示灯亮
            sRtn = gts.mc.GT_SetDoBit(
                gts.mc.MC_GPO,              // 指定数字IO类型是通用输出
                7,                      // 指定第7个通用输出，即EXO6
                0);                     // 输出高电平
            commandhandler("GT_SetDoBit", sRtn);
        }

        private void button10_Click(object sender, EventArgs e)
        {
            // 指令返回值
            short sRtn;
            // EXO6输出高电平，使指示灯亮
            sRtn = gts.mc.GT_SetDoBit(
                gts.mc.MC_GPO,              // 指定数字IO类型是通用输出
                7,                      // 指定第7个通用输出，即EXO6
                1);                     // 输出高电平
            commandhandler("GT_SetDoBit", sRtn);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            // 指令返回值
            short sRtn;
            // EXO6输出高电平，使指示灯亮
            sRtn = gts.mc.GT_SetDoBit(
                gts.mc.MC_GPO,              // 指定数字IO类型是通用输出
                1,                      // 指定第7个通用输出，即EXO6
                0);                     // 输出高电平
            commandhandler("GT_SetDoBit", sRtn);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            // 指令返回值
            short sRtn;
            // EXO6输出高电平，使指示灯亮
            sRtn = gts.mc.GT_SetDoBit(
                gts.mc.MC_GPO,              // 指定数字IO类型是通用输出
                1,                      // 指定第7个通用输出，即EXO6
                1);                     // 输出低电平
            commandhandler("GT_SetDoBit", sRtn);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        //
        //按键按下启动正向jog运功
        //
        private void button3_MouseDown(object sender, MouseEventArgs e)
        {
            ///读取参数
            vel = Convert.ToDouble(this.textBox1.Text);
            jog.acc = Convert.ToDouble(this.textBox3.Text);
            jog.dec = Convert.ToDouble(this.textBox4.Text);

            gts.mc.GT_SetJogPrm(axis, ref jog);//设置jog运动参数

            gts.mc.GT_SetVel(axis, -vel);//设置目标速度

            //gts.mc.GT_Update(axis);//更新轴运动
            gts.mc.GT_Update(1 << (axis - 1));//更新轴运动
        }
        //
        //按键抬起停止正向jog运功
        //
        private void button3_MouseUp(object sender, MouseEventArgs e)
        {
            gts.mc.GT_Stop(1 << (axis - 1), 0);//停止JOG运动
        }
        //
        //按键按下启动负向jog运功
        //
        private void button2_MouseDown(object sender, MouseEventArgs e)
        {
            vel = Convert.ToDouble(this.textBox1.Text);//设置JOG参数速度
            jog.acc = Convert.ToDouble(this.textBox3.Text);//设置JOG参数加速度
            jog.dec = Convert.ToDouble(this.textBox4.Text);//设置JOG参数减速度

            gts.mc.GT_SetJogPrm(axis, ref jog);//设置jog运动参数

            gts.mc.GT_SetVel(axis, vel);//设置目标速度

            gts.mc.GT_Update(1 << (axis - 1));//更新轴运动
            //gts.mc.GT_Update(axis);//更新轴运动
        }
        //
        //按键抬起停止负向jog运功
        //
        private void button2_MouseUp(object sender, MouseEventArgs e)
        {
            gts.mc.GT_Stop(1 << (axis - 1), 0);//停止JOG运动
        }
    }
}
