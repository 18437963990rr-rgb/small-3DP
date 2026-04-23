using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Motion;//导入GoogolMotionMap引用包
using System.Xml.Linq;//20200719新增：
//using royal; //20260203注释：移除royal引用，电气设备控制不依赖royal
using LaserADD_BinderJetter;

namespace BinderJetting
{
    public partial class 电气面板 : Form
    {
        //对于减少缓冲，效果很明显
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }
        public GoogolMotionMap m_mGoogolMotionMap;//m的含义是映射mapping:020104

        private Control GetControl(string Name)//查找指定名称控件:20200110测试通过//20200719新增：
        {
            var parent = this.FindForm();
            var findButton = parent.Controls.Find(Name, true).FirstOrDefault();
            if (findButton != null)
            { return findButton; }
            else { return null; }
        }
        //private XElement m_OutputXML;//20200719新增：
        private void OutputLoadXML(XElement OutputXML)//20200719新增：
        {
#if false//20200719批注：OutputXML作为参数传递即可：本人认为，不需要每次重新写入
            //(1)读取到内存：
            OutputXML = XElement.Load("端口、一键启动配置.xml");
#endif
            //(2)查询对应的一键启动策略：
            IEnumerable<XElement> address =
                    from el in OutputXML.Descendants("DI")
                    select el;

            //(1)m_ulControlMask = null;初始化之前，清空list<>
            foreach (XElement el in address)//并不需要双重遍历
            {
                foreach (XElement al in el.Elements())
                {
                    //string 记录1 = (string)al.Attribute("序号");
                    //string 记录2 = (string)al.Attribute("功能描述");
                    //string[] row = { 记录1, 记录2 };//20200112:关键指令，转换为主界面即可
                    //20200112:加载对应的功能码
                    int PortIndex = (int)al.Attribute("序号");//20200112:加载对应的功能码
                    string PortName = (string)al.Attribute("端口名称");//20200112:加载对应的功能码
                    int PortEnabledState = (int)al.Attribute("有效性");//20200112:加载对应的功能码

#if true//控制器输出端口配置
                    string BtnName = "DigtalOut" + (PortIndex+1);//检索对应的控件
                    Button OutputBtnSelected = (Button)GetControl(BtnName);
                    if (OutputBtnSelected != null)
                    {
                        OutputBtnSelected.Text = PortName;
                        if (PortEnabledState == 1)
                        {
                            OutputBtnSelected.Enabled = true;//使能
                            //OutputBtnSelected.BackgroundImage = Resource.state_off_红;//设置输出按钮的背景图片为灰色，表示未使能
                        }
                        else
                        {
                            OutputBtnSelected.Enabled = false;//使能
                            OutputBtnSelected.BackgroundImage = Resource.state_invalid_BlackGray;//设置输出按钮的背景图片为灰色，表示未使能
                        }
                    }
#endif
                }
            }
        }

        public 电气面板(GoogolMotionMap m_cGoogolMotionMap, XElement m_OutputXML)//020104：在构建函数将数据传出//20200719修改：
        {
            InitializeComponent();

            ///// 固高的通用输入输出信号控件集体控制初始化：————修改为static使用
            m_mGoogolMotionMap = m_cGoogolMotionMap;
            InitGoogolIO();//20200719批注：修改按钮的实时状态
            OutputLoadXML(m_OutputXML);//20200719新增：修改按钮的名称背景


            ////正常使用DO
            //button4flag = doState.Do1;
            //button1flag = doState.Do2;
            //button2flag = doState.Do3;
            //button3flag = doState.Do4;
            //button8flag = doState.Do5;
            //button7flag = doState.Do6;
            //button6flag = doState.Do7;
            //button5flag = doState.Do8;
            //button12flag = doState.Do9;
            //button11flag = doState.Do10;
            //button10flag = doState.Do11;
            //button9flag = doState.Do12;
            //button16flag = doState.Do13;
            ////备用DO
            //button15flag = doState.Do14;
            //button14flag = doState.Do15;
            //button13flag = doState.Do16;
            ////固高控制器IO初始化：更改按钮背景
            //if (button4flag == true)
            //{this.GoogolDigtalOut3.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut3.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button1flag == true)
            //{this.GoogolDigtalOut4.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut4.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button2flag == true)
            //{this.GoogolDigtalOut5.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut5.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button3flag == true)
            //{this.GoogolDigtalOut6.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut6.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button8flag == true)
            //{this.GoogolDigtalOut1.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut1.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button7flag == true)
            //{this.GoogolDigtalOut2.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{ this.GoogolDigtalOut2.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button6flag == true)
            //{this.GoogolDigtalOut7.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut7.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button5flag == true)
            //{this.GoogolDigtalOut8.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut8.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button12flag == true)
            //{this.GoogolDigtalOut9.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut9.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button11flag == true)
            //{this.GoogolDigtalOut10.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut10.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button10flag == true)
            //{this.GoogolDigtalOut11.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut11.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button9flag == true)
            //{this.GoogolDigtalOut12.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut12.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button16flag == true)
            //{ this.GoogolDigtalOut13.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png"); }
            //else
            //{this.GoogolDigtalOut13.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button15flag == true)
            //{ this.GoogolDigtalOut14.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut14.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
            //if (button14flag == true)
            //{this.GoogolDigtalOut15.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut15.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png"); }
            //if (button13flag == true)
            //{this.GoogolDigtalOut16.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");}
            //else
            //{this.GoogolDigtalOut16.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");}
        }

        private void 按键控制面板_Load(object sender, EventArgs e)
        {
            ////（一）打开运动控制器
            //motionMap.OpenCardComunication();
            ////（二）复位控制器：复位控制模式——复位后，默认的控制模式是“脉冲+方向的脉冲控制方式”
            //motionMap.ResetCardControlMode();
            ////（三）初始化控制器：（1）下载配置文件（具体就包括，控制器软硬件资源的配置：设置各轴的报警、正负限位、是否有效、规划模式等等）；
            //motionMap.InitCardConfiguration();
#if false//20200725新建批注：暂时去掉，不需要
            //固高的通用输入输出信号集控显示：定时刷新固高的通用输入输出
            Timer2 = new System.Windows.Forms.Timer() { Interval = 80 };
            Timer2.Tick += new EventHandler(Timer2_Tick);
            Timer2.Start();
#endif
        }

        /*******************************(1)固高的通用输入输出信号控件集体控制初始化***************************/
        //royal.RoyalPrintingMap RoyalMap = new royal.RoyalPrintingMap();//创建GoogolMotionMap对象，供本窗口调用
        //GoogolMotionMap m_mGoogolMotionMap = new GoogolMotionMap();//创建GoogolMotionMap对象，供本窗口调用      
        ///// 固高的通用输入输出信号控件集体控制初始化：————修改为static使用
        public void InitGoogolIO()/*private void InitShoveInk()*/
        {
            //(0)初始化控件列表的对应值
            for (int i = 0; i < 16; i++)//20200719修改：总共20个通用输入输出，本部分初始化其中的16项输出控件
            {
                //m_mGoogolMotionMap.m_bIoEnable[i] = false;//020104已经移到主界面中：
                //刷新所有控件的背景
                string name = "DigtalOut" + Convert.ToString(i+1);//20200719修改：修改了控件名称
                Control[] BtnItems=this.Controls.Find(name,false);//不需要搜索所有的子控件
                if (BtnItems.Length>0)
                {
                    Button button = (Button)BtnItems[0]; 

                    if (m_mGoogolMotionMap.m_bIoEnable[i]==true)
                    {
                        //button.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off-红.png");
                        button.BackgroundImage = Resource.state_off_红;
                    }
                    else
                    {
                        //button.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on-深蓝.png");
                        button.BackgroundImage = Resource.state_on_深蓝;
                    }
                    
                }
            }
            for (int i = 15; i < 20; i++)//20200719新增：备用的5个输出控件
            {
                string name = "DigtalOut" + Convert.ToString(i + 1);//20200719修改：修改了控件名称
                Button OutputBtnSelected = (Button)GetControl(name);
                if (OutputBtnSelected != null)
                {
                    OutputBtnSelected.Enabled = false;//使能
                    OutputBtnSelected.BackgroundImage = Resource.state_invalid_BlackGray;//设置输出按钮的背景图片为灰色，表示未使能
                }
            }
        }

        private void GoogolDigtalOut_Click(object sender, EventArgs e)//固高总共有16路EXO输出
        {
            ////(a-1)初始化：
            //bool EffectiveLevel = this.EffectiveLevelLabel.Checked;//20200110：选中为高电平有效，未选中为低电平有效
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            Control btn = sender as Control;
            //(b1)根据ID反转并存储到对应控件列表
            m_mGoogolMotionMap.m_bIoEnable[myTag - 1] = !m_mGoogolMotionMap.m_bIoEnable[myTag - 1];
            ////(b2)根据ID反转颜色状态
            //if ((sender as Control).BackColor == Color.LimeGreen)
            //{ (sender as Control).BackColor = Color.Tomato; }
            //else
            //{ (sender as Control).BackColor = Color.LimeGreen; }
            //(b3)根据ID反转背景图片
            if (m_mGoogolMotionMap.m_bIoEnable[myTag-1]== false)
            //{(sender as Control).BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on-深蓝.png");}
            { (sender as Control).BackgroundImage =Resource.state_on_深蓝; }
            else
            //{ (sender as Control).BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off-红.png");}
            { (sender as Control).BackgroundImage = Resource.state_off_红; }

            ////(c)计算掩码————此部分已经封装在了底层——googol的实现方式进行了封装
            ////(c)计算输出
            if (m_mGoogolMotionMap.m_bIoEnable[myTag - 1] == true)
            {
                m_mGoogolMotionMap.SetDo((short)myTag, true);

                string msg = "电气控制模块：手动打开端口"+myTag;
                Log4Net.Info(msg);
            }
            else
            {
                m_mGoogolMotionMap.SetDo((short)myTag, false);

                string msg = "电气控制模块：手动关闭端口" + myTag;
                Log4Net.Info(msg);
            };            
        }
        /*******************************(1)固高的通用输入输出信号控件集体控制初始化***************************/


        /*******************************(2)固高的通用输入输出信号集控显示：定时刷新固高的通用输入输出***************************/
        private System.Windows.Forms.Timer Timer2 = null;//刷新墨量显示状态定时器
        ////（1）刷新输出信号
        InkStateCtrl inkStateCtrl = new InkStateCtrl();
        Bitmap inkState = new Bitmap(1000, 100);
        Rectangle rectangle = new Rectangle();
        ////（2）刷新输入信号
        InkStateCtrl inkStateCtrl2 = new InkStateCtrl();
        Bitmap inkState2 = new Bitmap(1000, 100);
        Rectangle rectangle2 = new Rectangle();
        //////（2）刷新模拟输入信号
        //InkStateCtrl inkStateCtrl3 = new InkStateCtrl();
        //Bitmap inkState3 = new Bitmap(1000, 100);
        //Rectangle rectangle3 = new Rectangle();
        private void Timer2_Tick(object sender, EventArgs e)//刷新墨量显示状态定时器
        {
            ////（1）刷新输出信号
            inkStateCtrl.SetInkCount(20, 0);

            UInt32 nInkMask=0;//输出的掩码
            for (int i = 0; i<20; i++)
            {
                if (m_mGoogolMotionMap.m_bIoEnable[i] == true)
                {
                    nInkMask= nInkMask+(UInt32) (2 ^i);
                }
            }
            //inkStateCtrl.SetInkState(nInkMask);//很关键
            inkStateCtrl.SetInkState(0b1111111111111111);//很关键
            inkState.SetPixel(pictureBox.Width, pictureBox.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
            Graphics g = Graphics.FromImage(inkState);
            g.Clear(pictureBox.BackColor);
            rectangle.Width = pictureBox.Width; rectangle.Height = pictureBox.Height;//rectanle是bitmap的大小
            inkStateCtrl.OnPaint(g, rectangle);
            pictureBox.CreateGraphics().DrawImage(inkState, new Point(0, 0));
            pictureBox.Image = inkState;
            //g.Dispose();

            ////（2）刷新输入信号
            inkStateCtrl2.SetInkCount(16, 0);
            inkStateCtrl2.SetInkState(0b00000000000000001111);//很关键
            inkState2.SetPixel(pictureBox1.Width, pictureBox1.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
            Graphics g2 = Graphics.FromImage(inkState2);
            g2.Clear(pictureBox1.BackColor);
            rectangle2.Width = pictureBox1.Width; rectangle2.Height = pictureBox1.Height;//rectanle是bitmap的大小
            inkStateCtrl2.OnPaint(g2, rectangle2);
            pictureBox1.CreateGraphics().DrawImage(inkState2, new Point(0, 0));
            pictureBox1.Image = inkState2;
            //g.Dispose();

            //////（3）刷新模拟输入信号
            //inkStateCtrl3.SetInkCount(6, 0);
            //inkStateCtrl3.SetInkState(0b000111);//很关键
            //inkState3.SetPixel(pictureBox2.Width, pictureBox2.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
            //Graphics g3 = Graphics.FromImage(inkState3);
            //g3.Clear(pictureBox2.BackColor);
            //rectangle3.Width = pictureBox2.Width; rectangle3.Height = pictureBox2.Height;//rectanle是bitmap的大小
            //inkStateCtrl3.OnPaint(g3, rectangle3);
            //pictureBox2.CreateGraphics().DrawImage(inkState3, new Point(0, 0));
            //pictureBox2.Image = inkState3;
            ////g.Dispose();
        }

        private void 电气面板_Shown(object sender, EventArgs e)
        {
            this.button17.Focus();//20200602新建：软件启动后的鼠标焦点设置
        }

        ///*******************************(2)固高的通用输入输出信号集控显示：定时刷新固高的通用输入输出***************************/



        //GoogolMotionMap motionMap = new GoogolMotionMap();//创建GoogolMotionMap对象，供本窗口调用
        //private bool button4flag;
        //private void button4_Click(object sender, EventArgs e)
        //{
        //    if (button4flag == true)
        //    {
        //        //this.button4.BackgroundImage = System.Drawing.Image.FromFile("李老师资源-res-中文/switchoff2.bmp");
        //        this.GoogolDigtalOut3.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button4flag = false;
        //    }
        //    else
        //    {
        //        //this.button4.BackgroundImage = System.Drawing.Image.FromFile("李老师资源-res-中文/switchon2.bmp");
        //        this.GoogolDigtalOut3.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button4flag = true;
        //    }
        //}
        //private bool button1flag;
        //private void button1_Click(object sender, EventArgs e)
        //{
        //    if (button1flag == true)
        //    {
        //        this.GoogolDigtalOut4.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button1flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut4.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button1flag = true;
        //    }
        //}
        //private bool button2flag;
        //private void button2_Click(object sender, EventArgs e)
        //{
        //    if (button2flag == true)
        //    {
        //        this.GoogolDigtalOut5.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button2flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut5.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button2flag = true;
        //    }
        //}
        //private bool button3flag;
        //private void button3_Click(object sender, EventArgs e)
        //{
        //    if (button3flag == true)
        //    {
        //        this.GoogolDigtalOut6.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button3flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut6.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button3flag = true;
        //    }
        //}
        //private bool button8flag;
        //private void button8_Click(object sender, EventArgs e)
        //{
        //    if (button8flag == true)//关闭伺服
        //    {
        //        this.GoogolDigtalOut1.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button8flag = false;
        //        short state = 1;
        //        motionMap.PowerSever(ref state);
        //    }
        //    else//打开伺服
        //    {
        //        this.GoogolDigtalOut1.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button8flag = true;
        //        short state = 0;
        //        motionMap.PowerSever(ref state);
        //    }
        //}
        //// 该函数检测某条GT指令的执行结果，command为指令名称，error为指令执行返回值
        //static void commandhandler(string command, short error)
        //{
        //    // 如果指令执行返回值为非0，说明指令执行错误，向屏幕输出错误结果
        //    if (error != 0)
        //    {
        //        Console.WriteLine("{0}={1}\n", command, error);
        //    }
        //}
        //private bool button7flag;


        //private void button7_Click(object sender, EventArgs e)
        //{
        //    if (button7flag == true)//关闭照明
        //    {
        //        this.GoogolDigtalOut2.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button7flag = false;

        //        //// 指令返回值
        //        //short sRtn;
        //        //// EXO6输出高电平，使指示灯亮
        //        //sRtn = gts.mc.GT_SetDoBit(0,    //控制卡1
        //        //    gts.mc.MC_GPO,              // 指定数字IO类型是通用输出
        //        //    7,                          // 指定第7个通用输出，即EXO6
        //        //    1);                         // 输出高电平
        //        //commandhandler("GT_SetDoBit", sRtn);
        //        motionMap.SetDo(7, true);

        //    }
        //    else//打开照明
        //    {
        //        this.GoogolDigtalOut2.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button7flag = true;

        //        //// 指令返回值
        //        //short sRtn;
        //        //// EXO6输出高电平，使指示灯亮
        //        //sRtn = gts.mc.GT_SetDoBit(0,    //控制卡1
        //        //    gts.mc.MC_GPO,              // 指定数字IO类型是通用输出
        //        //    7,                          // 指定第7个通用输出，即EXO6
        //        //    0);                         // 输出高电平
        //        //commandhandler("GT_SetDoBit", sRtn);
        //        motionMap.SetDo(7, false);
        //    }
        //}
        //private bool button6flag;
        //private void button6_Click(object sender, EventArgs e)
        //{
        //    if (button6flag == true)
        //    {
        //        this.GoogolDigtalOut7.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button6flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut7.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button6flag = true;
        //    }
        //}
        //private bool button5flag;
        //private void button5_Click(object sender, EventArgs e)
        //{
        //    if (button5flag == true)
        //    {
        //        this.GoogolDigtalOut8.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button5flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut8.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button5flag = true;
        //    }
        //}
        //private bool button12flag;
        //private void button12_Click(object sender, EventArgs e)
        //{
        //    if (button12flag == true)
        //    {
        //        this.GoogolDigtalOut9.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button12flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut9.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button12flag = true;
        //    }
        //}
        //private bool button11flag;
        //private void button11_Click(object sender, EventArgs e)
        //{
        //    if (button11flag == true)
        //    {
        //        this.GoogolDigtalOut10.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button11flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut10.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button11flag = true;
        //    }
        //}
        //private bool button10flag;
        //private void button10_Click(object sender, EventArgs e)
        //{
        //    if (button10flag == true)
        //    {
        //        this.GoogolDigtalOut11.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button10flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut11.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button10flag = true;
        //    }
        //}
        //private bool button9flag;
        //private void button9_Click(object sender, EventArgs e)
        //{
        //    if (button9flag == true)
        //    {
        //        this.GoogolDigtalOut12.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button9flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut12.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button9flag = true;
        //    }
        //}
        //private bool button16flag;
        //private void button16_Click(object sender, EventArgs e)
        //{
        //    if (button16flag == true)
        //    {
        //        this.GoogolDigtalOut13.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-off.png");
        //        button16flag = false;
        //    }
        //    else
        //    {
        //        this.GoogolDigtalOut13.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/state-on.png");
        //        button16flag = true;
        //    }
        //}
        //private bool button15flag;
        //private bool button14flag;
        //private bool button13flag;
    }
}
