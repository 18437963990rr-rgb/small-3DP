using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Test
{
    //例程6-1 点位运动
    public partial class Form1 : Form
    {
        short axis;
        bool[] en=new bool[8];
        int pos;
        uint clk;
        double vel, prfvel, encvel, prfpos, encpos;
        gts.mc.TTrapPrm trap;
        public Form1()
        {
            InitializeComponent();
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

        private void Form1_Load(object sender, EventArgs e)
        {
            gts.mc.GT_Open(0,1);//打开运动控制卡
            this.comboBox1.SelectedIndex = 0;
        }
        //复位——复位控制器
        private void button6_Click(object sender, EventArgs e)
        {
            gts.mc.GT_Reset();
        }
        //初始化——清除全部8轴的报警限位——初始化控制卡和各轴状态——各轴的报警、限位
        private void button1_Click(object sender, EventArgs e)
        {
            gts.mc.GT_LoadConfig("GTS800-加限位.cfg");//下载配置文件
            gts.mc.GT_ClrSts(1,8);//清除各轴报警和限位
            pos = 0;//初始化的时候的位置为位置
        }
        //伺服使能——驱动器使能——打开驱动器
        private void button5_Click(object sender, EventArgs e)
        {
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);
                if (!en[axis-1])
                {
                    gts.mc.GT_AxisOn(axis);//上伺服——驱动器使能
                    this.button5.Text = "伺服关闭";
                }
                if (en[axis-1])
                {
                    gts.mc.GT_AxisOff(axis);//下伺服——驱动器使能
                    this.button5.Text = "伺服使能";
                }
                en[axis - 1] = !en[axis - 1];
          
        }

        //通用EXO给轴系统供电——伺服系统供电
        private void button7_Click(object sender, EventArgs e)//开启伺服EXO0
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

        private void button8_Click(object sender, EventArgs e)//关闭伺服EXO0
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

        private void button9_Click(object sender, EventArgs e)//打开照明EXO6
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

        private void button10_Click(object sender, EventArgs e)//关闭照明EXO6
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

        //启动点位运动
        private void button2_Click(object sender, EventArgs e)
        {
            //读取数据
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);
            vel = Convert.ToDouble(this.textBox1.Text);
            pos += Convert.ToInt32(this.textBox2.Text);//pos是点动运动坐标系的值，textBox2.Text是步长值
            trap.acc = Convert.ToDouble(this.textBox3.Text);
            trap.dec = Convert.ToDouble(this.textBox4.Text);
            trap.smoothTime = Convert.ToInt16(this.textBox5.Text);
            trap.velStart = 0;

            gts.mc.GT_SetTrapPrm(axis,ref trap);//设置点位运动参数
            gts.mc.GT_SetVel(axis,vel);//设置目标速度
            gts.mc.GT_SetPos(axis,pos);//设置目标位置
            gts.mc.GT_Update(1<<(axis-1));//更新轴运动
        }

        //平滑停止——立即停止轴运动
        private void button3_Click(object sender, EventArgs e)
        {
            gts.mc.GT_Stop(1<<(axis-1),0);
            pos = 0;//电动中，位置就变为了0；这个位置的基准在上面。//很关键
        }
        
        //位置清零——立即指令——清零规划位置和实际位置——零飘补偿
        private void button4_Click(object sender, EventArgs e)
        {
            gts.mc.GT_ZeroPos(1,8);
            pos = 0;//电动中，位置就变为了0；这个位置的基准在上面。//很关键
        }

        //刷新状态
        private void timer1_Tick(object sender, EventArgs e)
        {
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);
            if (!en[Convert.ToInt32(this.comboBox1.Text)-1])
            //if (axis !=0)
            {
                this.button5.Text = "伺服使能";
            }
            //if (axis != 0)
            if (en[Convert.ToInt32(this.comboBox1.Text)-1])
            {
                this.button5.Text = "伺服关闭";
            }
            gts.mc.GT_GetPrfPos(axis,out prfpos,1,out clk);
            this.textBox6.Text = Math.Round(prfpos,1).ToString();
            gts.mc.GT_GetPrfVel(axis,out prfvel,1,out clk);
            this.textBox7.Text = Math.Round(prfvel,1).ToString();
            gts.mc.GT_GetEncPos(axis,out encpos,1,out clk);
            this.textBox8.Text = Math.Round(encpos, 1).ToString();
            gts.mc.GT_GetEncVel(axis,out encvel,1,out clk);
            this.textBox9.Text = Math.Round(encvel, 1).ToString();
        }
        
    }
}
