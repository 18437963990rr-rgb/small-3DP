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
    //例程8-2 Home回原点
    public partial class Form1 : Form
    {
        short axis;//当前轴号
        int search_home;//搜索距离
        int home_offset;//偏移量
        bool[] en = new bool[8];//各轴的伺服使能状态
        uint clk;//时钟参数
        short capture;
        gts.mc.TTrapPrm trapPrm;
        int status;
        int pos;
        double prfpos, encpos;//运动状态参数

        short stage;//阶段标识，1表示处于Home搜索阶段，2表示处于回零阶段

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
            gts.mc.GT_Open(0, 1);//打开运动控制卡
            //初始化控件及变量
            this.comboBox1.SelectedIndex = 0;
            stage = 0;

            //启动定时器刷新状态
            timer1.Start();
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            gts.mc.GT_LoadConfig("GT800_test.cfg");//下载配置文件
            gts.mc.GT_ClrSts(1, 8);//清除各轴报警和限位
        }
        /// <summary>
        /// 位置清零
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            gts.mc.GT_ZeroPos(1, 8);
        }
        /// <summary>
        /// 伺服使能
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);
            if (!en[axis - 1])
            {
                gts.mc.GT_AxisOn(axis);//上伺服
                this.button3.Text = "伺服关闭";
            }
            if (en[axis - 1])
            {
                gts.mc.GT_AxisOff(axis);//下伺服
                this.button3.Text = "伺服使能";
            }
            en[axis - 1] = !en[axis - 1];
        }
        /// <summary>
        /// 复位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            gts.mc.GT_Reset();
        }
        /// <summary>
        /// 运动状态刷新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);//读取轴号

            if (!en[Convert.ToInt32(this.comboBox1.Text) - 1])
            {
                this.button3.Text = "伺服使能";
            }
            if (en[Convert.ToInt32(this.comboBox1.Text) - 1])
            {
                this.button3.Text = "伺服关闭";
            }
            // 读取轴状态
            gts.mc.GT_GetSts(axis, out status, 1, out clk);
            // 读取捕获状态
            gts.mc.GT_GetCaptureStatus(axis, out capture, out pos, 1, out clk);
            // 读取规划位置
            gts.mc.GT_GetPrfPos(axis, out prfpos, 1, out clk);
            // 读取编码器位置
            gts.mc.GT_GetEncPos(axis, out encpos, 1, out clk);

            //Home搜索阶段的状态
            if (stage == 1)
            {
                
                //捕获到Home信号进入第二阶段
                if (capture == 1)
                {
                    stage = 0;
                    MessageBox.Show("已找到Home，可以执行Home回零！");
                    
                }
                // 如果运动停止，返回出错信息
                if (0 == (status & 0x400) && capture!=1)
                {
                    stage = 0;
                    MessageBox.Show("没有找到Home！");
                    
                }
            }
            //Home回零阶段的状态
            if (stage == 2)
            {
                
                // 运动停止
                if (0 == (status & 0x400))
                {
                    if (prfpos != pos + home_offset)
                    {
                        stage = 0;
                        MessageBox.Show("Home回零过程出错！");
                        
                    }
                    else
                    {
                        stage = 0;
                        MessageBox.Show("Home回零完成！");
                        
                    }
                }
            }
            //显示规划位置
            this.textBox1.Text = Math.Round(prfpos,1).ToString();
            //显示实际位置
            this.textBox2.Text = Math.Round(prfpos, 1).ToString();
            //显示捕获状态
            this.textBox3.Text = capture.ToString();
            //显示捕获位置
            this.textBox4.Text = pos.ToString();
        }
        /// <summary>
        /// 停止运动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            gts.mc.GT_Stop(1 << (axis - 1), 0);
        }
        /// <summary>
        /// Home搜索
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button6_Click(object sender, EventArgs e)
        {
            //搜索距离
            search_home = Convert.ToInt32(textBox5.Text);

            short sRtn;
            // 启动Home捕获
            sRtn = gts.mc.GT_SetCaptureMode(axis, gts.mc.CAPTURE_HOME);
            // 切换到点位运动模式
            sRtn = gts.mc.GT_PrfTrap(axis);
            // 读取点位模式运动参数
            sRtn = gts.mc.GT_GetTrapPrm(axis, out trapPrm);
            trapPrm.acc = 0.25;
            trapPrm.dec = 0.25;
            // 设置点位模式运动参数
            sRtn = gts.mc.GT_SetTrapPrm(axis, ref trapPrm);
            // 设置点位模式目标速度，即回原点速度
            sRtn = gts.mc.GT_SetVel(axis, 10);
            // 设置点位模式目标位置，即原点搜索距离
            sRtn = gts.mc.GT_SetPos(axis, search_home);
            // 启动运动
            sRtn = gts.mc.GT_Update(1 << (axis - 1));

            //阶段标识
            stage = 1;
        }
        /// <summary>
        /// 回零
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button7_Click(object sender, EventArgs e)
        {
            home_offset = Convert.ToInt32(textBox6.Text);

            short sRtn;
            
            //已经捕获到Home才可以回零
            if (capture == 1)
            {
                // 运动到"捕获位置+偏移量"
                sRtn = gts.mc.GT_SetPos(axis, pos + home_offset);
                // 在运动状态下更新目标位置
                sRtn = gts.mc.GT_Update(1 << (axis - 1));

                //阶段标识
                stage = 2;
            }

        }

        
    }
}
