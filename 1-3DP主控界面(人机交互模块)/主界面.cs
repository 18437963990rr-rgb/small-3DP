//#define DataProcessDebugMode
//#define SinglePassPrintMode
#define TwoPassPrintMode
//#define TwoPassPrintPerSixTimes
#define TwoPassPrintPerThreeTimes


using Composation;
using ComposationConfirm;
using JOB管理_调度类库_JOB管理模块_JOB调度模块;
using Microsoft.VisualBasic.Devices;
using Modbus.Device;
using Motion;//导入GoogolMotionMap引用包
using Newtonsoft.Json;
using ReadFile;
using royal;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using WinFormAnimation;

using PluginLibrary;

namespace BinderJetting
{
    public partial class 主界面 : Form
    {
        const int EncoderLinePerMM = 1000;
        const int EncoderLinePerInch = EncoderLinePerMM * 254 / 10;

        private _3DP_GUI组件 _3DP_GUI = new _3DP_GUI组件();
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        Integral_interface_data dispatch = new Integral_interface_data();
        Integral_interface_data draw_data = new Integral_interface_data();
        Integral_interface_data tempDrawData = new Integral_interface_data();//20200225新增:多线程绘制图像数据

        private ConcurrentQueue<Integral_interface_data> g_qDrawQueue = new ConcurrentQueue<Integral_interface_data>();//c#高效的线程安全队列ConcurrentQueue：20200223新增
        //ConcurrentQueue
        bool dequeueSuccesful = false;//20200225新增:多线程绘制图像数据
        static ManualResetEventSlim resetEventSlim = new ManualResetEventSlim(false);//手动信号
        static ManualResetEventSlim resetEventSlim2 = new ManualResetEventSlim(false);//手动信号
        private int SR_IO_F = 0;//选中+右键+输入标志位
        private int SR_F = 0;//选中+右键标志位
        private int SP_num = 0;//选中图片个数
        private void TestFun()//20200223:判断什么时候可以绘制，判断队列中的数据
        {
            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据
            //Integral_interface_data tempDrawData = new Integral_interface_data();//20200223新增:多线程绘制图像数据
            //bool dequeueSuccesful = false;//20200223新增:多线程绘制图像数据
            double t0 = 0;
            double t1 = 0;
            while (true)
            {
                if (SR_IO_F == 1)
                {
                    SR_IO_F = 0;
                    dequeueSuccesful = g_qDrawQueue.TryDequeue(out tempDrawData);//20200223新增:多线程绘制图像数据
                    if (dequeueSuccesful)//20200223新增:多线程绘制图像数据
                    { draw_data = tempDrawData; }
                    Data_assignment();
                    resetEventSlim.Set();//开启resetEventSlim信号
                    resetEventSlim2.Reset();
                    resetEventSlim2.Wait();
                }
                else
                {
                    if (g_qDrawQueue.Count == 0)
                    {
                        continue;
                    }
                    else if (g_qDrawQueue.Count > 0 && g_qDrawQueue.Count < 5)
                    {
                        //20200223新增:多线程绘制图像数据
                        //20200223新增:多线程绘制图像数据
                        dequeueSuccesful = g_qDrawQueue.TryDequeue(out tempDrawData);//20200223新增:多线程绘制图像数据
                        if (dequeueSuccesful)//20200223新增:多线程绘制图像数据
                        { draw_data = tempDrawData; }
                        Data_assignment();
                        resetEventSlim.Set();//开启resetEventSlim信号
                        resetEventSlim2.Reset();
                        resetEventSlim2.Wait();
                    }
                    else if (g_qDrawQueue.Count >= 5 && g_qDrawQueue.Count < 10)//4作为一个调度频率；以后要以实际处分重绘的频率来调度(实际触发的时间间隔)
                    {
                        for (int Hz = 0; Hz < 4; Hz++)
                        {
                            //20200223新增:多线程绘制图像数据
                            //20200223新增:多线程绘制图像数据
                            dequeueSuccesful = g_qDrawQueue.TryDequeue(out tempDrawData);//20200223新增:多线程绘制图像数据
                            if (dequeueSuccesful)//20200223新增:多线程绘制图像数据
                            { draw_data = tempDrawData; }
                        }
                        Data_assignment();
                        resetEventSlim.Set();//开启resetEventSlim信号
                        resetEventSlim2.Reset();
                        resetEventSlim2.Wait();//t1处于等待状态
                    }
                    else if (g_qDrawQueue.Count >= 10)
                    {
                        //20200223新增:多线程绘制图像数据
                        //20200223新增:多线程绘制图像数据
                        dequeueSuccesful = g_qDrawQueue.TryDequeue(out tempDrawData);//20200223新增:多线程绘制图像数据
                        if (dequeueSuccesful)//20200223新增:多线程绘制图像数据
                        { draw_data = tempDrawData; }//20200223新增:多线程绘制图像数据
                        t0 = tempDrawData.Time; //20200223新增:多线程绘制图像数据

                        while (t1 - t0 < 400 && g_qDrawQueue.Count > 1)
                        {
                            //20200223新增:多线程绘制图像数据
                            //20200223新增:多线程绘制图像数据
                            dequeueSuccesful = g_qDrawQueue.TryDequeue(out tempDrawData);//20200223新增:多线程绘制图像数据
                            if (dequeueSuccesful)//20200223新增:多线程绘制图像数据
                            { draw_data = tempDrawData; }//20200223新增:多线程绘制图像数据
                            t1 = draw_data.Time;
                        }
                        Data_assignment();
                        resetEventSlim.Set();//开启resetEventSlim信号
                        resetEventSlim2.Reset();
                        resetEventSlim2.Wait();
                    }
                }
            }
        }
        private void TestFun2()//20200223:绘制和显示
        {
            while (true)
            {
                resetEventSlim.Wait();//t2处于等待状态
                resetEventSlim2.Reset();//t1信号关闭

                //_3DP_GUI.SetBitmap(this.Width, this.Height);//——————创建容器，并设定大小
                //Graphics g = Graphics.FromImage(_3DP_GUI.cal_bt);
                //_3DP_GUI.num_p = 0;

                //_3DP_GUI.Select_rectangle(g, this.Width, this.Height);

                //this.CreateGraphics().DrawImage(_3DP_GUI.cal_bt, new Point(0, 0));

                _3DP_GUI.SetBitmap(this.panel3.Width, this.panel3.Height);//——————创建容器，并设定大小
                Graphics g = Graphics.FromImage(_3DP_GUI.cal_bt);
                _3DP_GUI.num_p = 0;

                _3DP_GUI.Select_rectangle(g, this.panel3.Width, this.panel3.Height);

                this.panel3.CreateGraphics().DrawImage(_3DP_GUI.cal_bt, new Point(0, 0));

                resetEventSlim.Reset();//重新关闭
                resetEventSlim2.Set();//t1信号开启
            }
        }
        /*绘制赋值*/
        void Data_assignment()//20200223:给绘制线程赋值
        {
            if (draw_data != null)
            {
                //20200225新增:多线程绘制图像数据
                //20200225新增:多线程绘制图像数据
                _3DP_GUI.PX = draw_data.PX;
                _3DP_GUI.PY = draw_data.PY;
                _3DP_GUI.px = draw_data.px;
                _3DP_GUI.py = draw_data.py;
                _3DP_GUI.PH = draw_data.PH;
                _3DP_GUI.PW = draw_data.PW;
                _3DP_GUI.s_max = draw_data.s_max;
                _3DP_GUI.s_min = draw_data.s_min;
                //20200225新增:多线程绘制图像数据
                //20200225新增:多线程绘制图像数据


                ////_3DP_GUI.tempJobItems = draw_data.tempJobItems;//——————————20200224：一定不能给此值赋值，负责零件的原始信息会发生改变
                ////_3DP_GUI.CLInum = draw_data.CLInum;//——————————20200224：一定不能给此值赋值，负责零件的原始信息会发生改变
                _3DP_GUI.firstpoint = draw_data.firstpoint;
                _3DP_GUI.Firstpoint = draw_data.Firstpoint;
                _3DP_GUI.MouseDownP = draw_data.MouseDownP;
                _3DP_GUI.MouseDown_x = draw_data.MouseDown_x;
                _3DP_GUI.MouseDown_y = draw_data.MouseDown_y;
                _3DP_GUI.MouseUp_x = draw_data.MouseUp_x;
                _3DP_GUI.MouseUp_y = draw_data.MouseUp_y;
                _3DP_GUI.MouseWheel_x = draw_data.MouseWheel_x;
                _3DP_GUI.MouseWheel_y = draw_data.MouseWheel_y;
                _3DP_GUI.ResetFlag = draw_data.ResetFlag;
                _3DP_GUI.secondpoint = draw_data.secondpoint;
                _3DP_GUI.Secondpoint = draw_data.Secondpoint;
                _3DP_GUI.select_Flag = draw_data.select_Flag;
                _3DP_GUI.sr_Flag = draw_data.sr_Flag;
                //_3DP_GUI.TIFFPathCopy = draw_data.TIFFPathCopy;
                _3DP_GUI.zoom = draw_data.zoom;
                _3DP_GUI.mov_F = draw_data.mov_F;
            }
        }
        /*数据储存于队列中*/
        int f = 0;
        void data_storage()//20200223:储存计算出的绘制数据到队列中
        {
            if (f == 0)
            {
                f = 1;
                _3DP_GUI.cal_MouseDownP = _3DP_GUI.MouseDownP;
                _3DP_GUI.cal_zoom = _3DP_GUI.zoom;
                _3DP_GUI.cal_pixel2MM = _3DP_GUI.pixel2MM;

                //记录图片位置
                _3DP_GUI.cal_num_p = _3DP_GUI.num_p;
                _3DP_GUI.cal_PX = _3DP_GUI.PX;
                _3DP_GUI.cal_PY = _3DP_GUI.PY;
                _3DP_GUI.cal_px = _3DP_GUI.px;
                _3DP_GUI.cal_py = _3DP_GUI.py;
                _3DP_GUI.cal_PH = _3DP_GUI.PH;
                _3DP_GUI.cal_PW = _3DP_GUI.PW;
                _3DP_GUI.cal_LAYER = _3DP_GUI.LAYER;
                _3DP_GUI.cal_FLAG = _3DP_GUI.FLAG;

                _3DP_GUI.cal_StlFlag = _3DP_GUI.StlFlag;
                _3DP_GUI.cal_ResetFlag = _3DP_GUI.ResetFlag;

                _3DP_GUI.cal_TIFFPathCopy = _3DP_GUI.TIFFPathCopy;
                _3DP_GUI.cal_sr_Flag = _3DP_GUI.sr_Flag;
                _3DP_GUI.cal_firstpoint = _3DP_GUI.firstpoint;
                _3DP_GUI.cal_Firstpoint = _3DP_GUI.Firstpoint;
                _3DP_GUI.cal_secondpoint = _3DP_GUI.secondpoint;
                _3DP_GUI.cal_Secondpoint = _3DP_GUI.Secondpoint;

                _3DP_GUI.cal_bt = _3DP_GUI.bt;
                //_3DP_GUI.cal_img = _3DP_GUI.img;

                _3DP_GUI.cal_img_x = _3DP_GUI.img_x;
                _3DP_GUI.cal_img_y = _3DP_GUI.img_y;
                _3DP_GUI.cal_MouseDown_x = _3DP_GUI.MouseDown_x;
                _3DP_GUI.cal_MouseDown_y = _3DP_GUI.MouseDown_y;
                _3DP_GUI.cal_MouseUp_x = _3DP_GUI.MouseUp_x;
                _3DP_GUI.cal_MouseUp_y = _3DP_GUI.MouseUp_y;
                _3DP_GUI.cal_MouseWheel_x = _3DP_GUI.MouseWheel_x;
                _3DP_GUI.cal_MouseWheel_y = _3DP_GUI.MouseWheel_y;
                _3DP_GUI.cal_img_w = _3DP_GUI.img_w;
                _3DP_GUI.cal_img_h = _3DP_GUI.img_h;
                _3DP_GUI.cal_select_Flag = _3DP_GUI.select_Flag;
                _3DP_GUI.cal_mov_F = _3DP_GUI.mov_F;
            }

            //20200225新增:多线程绘制图像数据
            //20200225新增:多线程绘制图像数据
            dispatch.PX = _3DP_GUI.PX;
            dispatch.PY = _3DP_GUI.PY;
            dispatch.px = _3DP_GUI.px;
            dispatch.py = _3DP_GUI.py;
            dispatch.PH = _3DP_GUI.PH;
            dispatch.PW = _3DP_GUI.PW;
            dispatch.s_max = _3DP_GUI.s_max;
            dispatch.s_min = _3DP_GUI.s_min;

            //20200225新增:多线程绘制图像数据
            //20200225新增:多线程绘制图像数据

            dispatch.firstpoint = _3DP_GUI.cal_firstpoint;
            dispatch.Firstpoint = _3DP_GUI.cal_Firstpoint;
            dispatch.MouseDownP = _3DP_GUI.cal_MouseDownP;
            dispatch.MouseDown_x = _3DP_GUI.cal_MouseDown_x;
            dispatch.MouseDown_y = _3DP_GUI.cal_MouseDown_y;
            dispatch.MouseUp_x = _3DP_GUI.cal_MouseUp_x;
            dispatch.MouseUp_y = _3DP_GUI.cal_MouseUp_y;
            dispatch.MouseWheel_x = _3DP_GUI.cal_MouseWheel_x;
            dispatch.MouseWheel_y = _3DP_GUI.cal_MouseWheel_y;
            dispatch.ResetFlag = _3DP_GUI.cal_ResetFlag;
            dispatch.secondpoint = _3DP_GUI.cal_secondpoint;
            dispatch.Secondpoint = _3DP_GUI.cal_Secondpoint;
            dispatch.select_Flag = _3DP_GUI.cal_select_Flag;
            dispatch.sr_Flag = _3DP_GUI.cal_sr_Flag;
            dispatch.TIFFPathCopy = _3DP_GUI.cal_TIFFPathCopy;
            dispatch.zoom = _3DP_GUI.cal_zoom;
            dispatch.mov_F = _3DP_GUI.cal_mov_F;

            dispatch.Time = Convert.ToInt64(DateTime.Now.Millisecond);
            //if (g_qDrawQueue.Count > 1000)
            //    g_qDrawQueue.Dequeue();

            if (g_qDrawQueue.Count > 1000)
            {
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据
                dequeueSuccesful1 = g_qDrawQueue.TryDequeue(out tempDrawData1);//20200223新增:多线程绘制图像数据
                if (dequeueSuccesful1)//20200223新增:多线程绘制图像数据
                { draw_data = tempDrawData1; }
            }
            lock (g_qDrawQueue)
            {
                g_qDrawQueue.Enqueue(dispatch);
            }
        }
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        Integral_interface_data tempDrawData1 = new Integral_interface_data();//20200223新增:多线程绘制图像数据
        bool dequeueSuccesful1 = false;//20200223新增:多线程绘制图像数据

        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据

        List<JobItem> jobItems = new List<JobItem>();
        //20200223新增:多线程绘制图像数据~~~~~~~~
        //20200223新增:多线程绘制图像数据~~~~~~~~

        //c表示为class：020104————放到对应的数据中即可，主界面和子界面都调用到即可
        GoogolMotionMap m_cGoogolMotionMap;
        private GoogolMotionMap CreateMotionMap()
        {
            var map = new GoogolMotionMap();
            map.SetLogSink(msg => Log4Net.Info(msg), msg => Log4Net.Error(msg));
            return map;
        }
        public 主界面()
        {
            InitializeComponent();
            MyRenderer myRenderer = new MyRenderer();
            myRenderer.RoundedEdges = true;//圆角
            bool returnvalue = myRenderer.RoundedEdges;
            menuStrip3.Renderer = myRenderer/*new MyRenderer()*/;//重载主菜单的渲染方式//后面代码无效：menuStrip3.Renderer.RoundedEdges = true;
            g_SharpControl.InitiateDevice(this.renderControl1.Handle, this.renderControl1.Width, this.renderControl1.Height/*+30*/);//使用DeviceContext绘制，渲染初始化
            InitColorCards();//20200613批注：
            g_SharpControl.ModifyColorSysFlag = true;//非常关键，设置标志位

            g_SharpControl.InitRenderToWic();//20200609构建数据处理及传送：
            MouseWheel += new MouseEventHandler(Form1_MouseWheel);//添加鼠标滚轮事件//迁移到InitializeComponent中间
            this.PrinterStatusLabel.Text = "|| 准备打印中";//20200430新增：打印机状态栏
#if false//日志记录：保存校准行程、原点位置、加工日志及异常日志；在Timer2中实现
            //timer1.Enabled = true;//日志保存：保存校准行程及原点位置及加工日志及异常日志
#endif
            rd = new Random();
            //（1）Googol的映射数据结构及其初始化：
            m_cGoogolMotionMap = CreateMotionMap();
            g_cMotionMap = CreateMotionMap();
            InitGoogolIO();

            ////（2）初始化Modbus通讯：
            //CreateModbusCommunicate();
        }
        private void Form1_Load(object sender, EventArgs e)//初始化主程序时，（1）完成界面初始化（2）开启初始线程。
        {
#if false//20230406新增：临时测试无任务栏全屏显示
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.Bounds = Screen.PrimaryScreen.Bounds;
#endif 

            //this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);//解决闪烁
            //this.SetStyle(ControlStyles.Opaque, true);//解决背景重绘问题(设置不绘制窗口背景，因为重绘窗口背景会导致性能底下)
            //this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);//解决闪烁 

            //20210304修改：更新panel6的初始大小为246
            this.panel6.Width = panel6Width;// 246;20210304修改：修正
            this.panel7.Width = this.panel6.Width + this.panel8.Width;
            this.panel8.Left = 0;
            this.panel5.Width = panel1.Width - panel7.Width - 1;
            this.panel5.Left = panel7.Width + 1;
            this.panel8.Width = 24;

            g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height/*+30*/));//20210309新增：立即更新界面

            Timer = new System.Windows.Forms.Timer() { Interval = 40 };
            Timer.Tick += new EventHandler(Timer_Tick);
            base.Opacity = 0;
            Timer.Start();
            InitSystem();//（a）初始化控制器（1）打开运动控制器            
            ThreadStart initThreadEntry = new ThreadStart(RunInitThread);//线程入口方法：InitThread，在其中开启了Timer2//（b）开启初始化线程：初始化定时器2
            InitThread = new Thread(initThreadEntry) { IsBackground = true };
            InitThread.Start();
#if false//20200
            //（1）非常关键
            StartInterSpeedSpark();//20200630批注：开启待机状态间歇闪喷维护
#endif
            string JsonPath = "";
            try
            {
                //(4)20200807批注：加载打印策略参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\PrintStrategy-Configuration.json";//json配置文件：启动目录
                g_PrintStrategys = ObjectCopier.LoadJson<PrintStrategys>(JsonPath);
                g_RYSYSParam = g_PrintStrategys.LocalRYSYSParam;//20230203新增：修复打印DPI等参数无法本地保存的问题
                g_SharpControl.gc_RysysParam = g_RYSYSParam;//20230210修改：修复无法顺利加载本地保存的BPP以及灰阶设置的问题
            }
            catch (Exception)
            {
                if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
                {
                    File.Create(JsonPath);//不存在则创建文件
                    MessageBox.Show("打印策略配置文件不存在，已创建");
                }
                else
                {
                    MessageBox.Show("打印策略配置文件加载异常");
                }
            }

            try
            {
                //(5)20210304批注：加载喷头校准参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\NozzleConfigInCorrection.json";//json配置文件：启动目录
                k_PrintNozzleHeadConfigure = ObjectCopier.LoadJson<PrintNozzleHeadConfigure>(JsonPath);

                InitFormWithPrintNozzleHeadConfigureParam();//20210304新增：非常关键：完成喷头组校准参数的绑定过程
            }
            catch (Exception)
            {
                if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
                {
                    File.Create(JsonPath);//不存在则创建文件
                    MessageBox.Show("喷头校准配置文件不存在，已创建");
                }
                else
                {
                    MessageBox.Show("喷头校准配置文件加载异常");
                }
                k_PrintNozzleHeadConfigure = new PrintNozzleHeadConfigure();//20210304夜间修改：
                InitFormWithPrintNozzleHeadConfigureParam();//20201020新增：开启ParamInText自动打印参数的绑定
            }


        }
#if true//20200614批注：重写主菜单的渲染程序
        private class MyRenderer : ToolStripProfessionalRenderer
        {
            public MyRenderer() : base(new MyColors()) { }
            // This method draws a border around the GridStrip control.//20200615世彪批注：
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                base.OnRenderToolStripBorder(e);

                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    e.AffectedBounds,
                    SystemColors.ControlDarkDark,
                    SystemColors.ControlDarkDark);
            }
        }

        private class MyColors : ProfessionalColorTable
        {
            public override Color ToolStripBorder
            { get { return Color.Black; } }

            public override Color MenuItemSelected
            { get { return Color.FromArgb(120, 120, 120, 120)/*Color.FromArgb(51, 51, 52)*/; } }

            public override Color MenuBorder
            { get { return Color.Black; } }

            //fill màu item của menu khi mouse enter
            public override Color MenuItemSelectedGradientBegin
            { get { return Color.Orange/*CornflowerBlue*//*Orange*//*FromArgb(64, 64, 66)*/; } }

            public override Color MenuItemSelectedGradientEnd
            { get { return Color.Yellow/*White*//*FromArgb(64, 64, 66)*/; } }

            // chọn màu viền menu item khi mouse enter
            public override Color MenuItemBorder
            { get { return Color.FromArgb(51, 51, 52); } }

            // fill màu nút item của menu khi dc nhấn
            public override Color MenuItemPressedGradientBegin
            { get { return Color.FromArgb(120, 120, 120, 120)/*Yellow*//*FromArgb(27, 27, 28)*/; } }

            public override Color MenuItemPressedGradientEnd
            { get { return Color.FromArgb(120, 120, 120, 120)/*Green*//*FromArgb(27, 27, 28)*/; } }
        }
#endif
        //(1)关闭事件：20200527新增222
        protected override void OnClosing(CancelEventArgs e)
        {
            //(1)关闭传送线程：关闭底层正在工作的线程 20260204
            //string tempThreadName = "DataTaskTHREAD";//(1)关闭联调线程
            //DeleteThread(tempThreadName);

            //string tempThreadName1 = "PrintTaskTHREAD";
            //Thread tempThread1 = PrinterLogicThreads.Where(x => x.Name == (tempThreadName1)).FirstOrDefault();
            //string tempThreadName2 = "DataTaskTHREAD";
            //Thread tempThread2 = PrinterLogicThreads.Where(x => x.Name == (tempThreadName2)).FirstOrDefault();
            //if (tempThread1 != null || tempThread2 != null)
            //{
            //    MessageBox.Show("请新关闭打印任务！");
            //}
            //else
            //{
            //    g_SharpControl.DisposeClosing();
            //    base.OnClosing(e);
            //}

        }

        public void InitGoogolIO()/*private void InitShoveInk()*/
        {
            //(0)初始化控件列表的对应值
            for (int i = 0; i < 20; i++)//总共20个通用输入输出
            {
                m_cGoogolMotionMap.m_bIoEnable[i] = false;
            }
        }

        GoogolMotionMap g_cMotionMap;//创建GoogolMotionMap对象，供本窗口调用                                                          
        private Thread InitThread;//（1）开启初始化线程
        private System.Windows.Forms.Timer Timer = null;
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.Opacity >= 1)
            { Timer.Stop(); }
            else
            { base.Opacity += 0.35;/*延迟4帧完成显示*/}
        }

        private void 主界面_Shown(object sender, EventArgs e)//20200405：登录界面设计完成
        {
            Thread.Sleep(1000);
            //20200405新增：开启LOGIN登录界面
            g_AccountPassward = new AccountPassward();
            g_AccountPassward.m_sAccount = "123456@laseradd.com";
            g_AccountPassward.m_sPassward = "123456789";
            int returnCode = InitLoginForm();
            while (returnCode == 2)
            {
                returnCode = InitLoginForm();
            }
            this.ImportCliBtn.Focus();//20200602新建：软件启动后的鼠标焦点设置
        }

#if true //20230214新增：获取硬件信息

        //PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "", true);
        //PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes,"",true");
        //public string getCurrentCpuUsage()
        //{
        //    return $"{cpuCounter.NextValue()} %";
        //}

        //public string getAvailableRAM()
        //{
        //    return $"{ramCounter.NextValue()} MB";
        //}
        public string GetOSFriendlyName()
        {
            string result = string.Empty;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            foreach (ManagementObject os in searcher.Get())
            {
                result = os["Caption"].ToString();
                break;
            }
            return result;
        }
#endif

        public AccountPassward g_AccountPassward;
        /// <summary>
        /// 开机账户登录界面：20200405新增
        /// </summary>
        private int InitLoginForm()
        {
            登录确认 LoginForm = new 登录确认();//20200202修改

            LoginForm.m_AccountPassward = (AccountPassward)g_AccountPassward.Clone();
            DialogResult result = LoginForm.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
                if ((LoginForm.m_AccountPassward.m_sAccount == "123456@laseradd.com")
                    && (LoginForm.m_AccountPassward.m_sPassward == "123456"))
                {
#if true
                    // (1)获取CPU使用率
                    PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    cpuCounter.NextValue();
                    System.Threading.Thread.Sleep(100/*1000*/); // 等待一秒
                    float cpuUsage = cpuCounter.NextValue();//string msg1 = $"CPU使用率：{cpuUsage}%";

                    // (2)获取可用内存大小
                    ComputerInfo info = new ComputerInfo();
                    long freeMemory = (long)info.AvailablePhysicalMemory / 1024 / 1024;//MB

                    // (3)获取硬盘剩余空间
                    string msg3 = null;
                    System.IO.DriveInfo[] drives = DriveInfo.GetDrives();
                    foreach (DriveInfo drive in drives)
                    {
                        if (drive.IsReady)
                        {
                            msg3 = msg3 + $"  盘符 { drive.Name} 的剩余空间：{drive.TotalFreeSpace / 1024 / 1024 / 1024} GB\r\n";
                        }
                    }

                    // (4)获取 CPU 信息
                    string CpuInfo = null; string MemoryInfo = null; string DiskInfo = null;
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        CpuInfo = CpuInfo + string.Format("  CPU 厂家: {0} CPU 型号: {1}\r\n", queryObj["Manufacturer"], queryObj["Name"]);
                    }
                    // (5)获取内存条信息
                    searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        MemoryInfo = MemoryInfo + string.Format("  内存条厂家: {0} 内存条型号: {1}\r\n", queryObj["Manufacturer"], queryObj["PartNumber"]);
                    }
                    // (6)获取硬盘信息
                    searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        DiskInfo = DiskInfo + string.Format("  硬盘厂家: {0} 硬盘型号: {1}\r\n", queryObj["Manufacturer"], queryObj["Model"]);
                    }

                    // (7)获取当前系统的.NET Framework版本
                    string version = Environment.Version.ToString();

                    // (8)获取当前进程所在的盘符
                    string driveLetter = Process.GetCurrentProcess().MainModule.FileName.Substring(0, 1);

                    string msg = $"软件开机密码正确，登录成功1次\r\n" +
                        $"==========================================================================\r\n" +
                        $"操作系统:{{{GetOSFriendlyName()}}}\r\n" +
                        $"运行环境:{{ .Net Framework Enviroment Version {version}}}\r\n" +
                        $"盘符：{driveLetter}\r\n" +
                        $"CPU使用率：{{{cpuUsage}%}} ，可用内存大小:{{{freeMemory}MB}}，硬盘剩余空间：\r\n" +
                        $"{msg3}" +
                        $"硬件配置情况：\r\n" +
                        $"{CpuInfo}" +
                        $"{MemoryInfo}" +
                        $"{DiskInfo}" +
                        $"==========================================================================";
                    Log4Net.Info(msg);
#endif

                    this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.主界面_FormClosing);//20200405：退出前，需要确认
                    return 1;
                }
                else
                {
                    String msg = $"设备开机密码错误，登录失败1次：输入错误密码为：{{{LoginForm.m_AccountPassward.m_sPassward}}}";
                    Log4Net.Info(msg);

                    g_AccountPassward = (AccountPassward)LoginForm.m_AccountPassward.Clone();
                    g_AccountPassward.ShowReminderFlag = true;
                    return 2;
                }
            }
            else if (result == DialogResult.Cancel)//退出时，什么都不做
            {
                //(1)关闭RoyalPrintCard控制器的XYZ3周
                bool nRetVal = royal.royal.DEM_StopAxisRun(false, 0x7);//同时停止X/Y/Z的运动3轴运动：20200305
                //(2)关闭RoyalPrintCard控制器
                bool ReturnCode = royal.royal.DEV_CloseDevice();

                //(2)继续退出
                //this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.主界面_FormClosing);//20200405：放弃登录，直接退出
                Application.Exit();
                //WinAPI.AnimateWindow(this.Handle, 1000, WinAPI.HOR_NEGATIVE);//20200723新增：

                return 3;
            }
            return 1;
        }

        private void RunInitThread()//20200220：初始化定时器2
        {
            //MessageBox.Show("我进入初始化线程");//InitSystem();//具体的执行内容//20200221修改：
            //MessageBox.Show("初始化线程结束");
            this.Invoke(new MethodInvoker(delegate//20200220：开启定时器2，位于主线程中:负责刷新显示
            {
                timer2.Enabled = true;
            }));
            InitThread.Abort();//中止线程
            MessageBox.Show("测试：线程终止后，函数后面程序自动不执行");
        }

        int g_nLayerStart = 1;//20200220新增：g_nLayerStart需要小于等于g_nLayerNum
        int g_nLayerEnd = 1/*10*/;//20200220新增：20230419修改：中止层数目，需要更具打印的最大层数来自动更新，当然支持手动的修改
        int g_nLayerCurrent = 0;//20200220新增：指示当前打印层
        private int g_nSelectedLayerStart = -1;
        private int g_nSelectedLayerEnd = -1;
        private bool g_bSelectedLayerRangeReady = false;
        private int SetLayerStart//20200601:
        {
            set//终止层必须大于起始层
            {
                if (value <= g_nLayerEnd)
                { g_nLayerStart = value; }
            }
            get { return g_nLayerStart; }
        }
        private int SetLayerEnd//20200601:
        {
            set//终止层必须大于起始层
            {
                if (value >= g_nLayerStart)
                { g_nLayerEnd = value; }
            }
            get { return g_nLayerEnd; }
        }

        private bool CaptureLayerRangeFromUI(string sourceTag)
        {
            int startDisplay;
            int endDisplay;
            if (!int.TryParse(this.LayerStart.Text, out startDisplay)
                || !int.TryParse(this.LayerEnd.Text, out endDisplay))
            {
                Log4Net.Info($"层范围快照失败：source={sourceTag}，LayerStart.Text={this.LayerStart.Text}，LayerEnd.Text={this.LayerEnd.Text}");
                return false;
            }

            if (startDisplay < 1)
            {
                startDisplay = 1;
            }

            if (endDisplay < startDisplay)
            {
                endDisplay = startDisplay;
            }

            g_nSelectedLayerStart = startDisplay - 1;
            g_nSelectedLayerEnd = endDisplay - 1;
            g_bSelectedLayerRangeReady = true;

            g_nLayerStart = g_nSelectedLayerStart;
            g_nLayerEnd = g_nSelectedLayerEnd;
            g_SharpControl.g_nLayerEnd = g_nLayerEnd;

            Log4Net.Info($"层范围快照成功：source={sourceTag}，LayerStart.Text={this.LayerStart.Text}，LayerEnd.Text={this.LayerEnd.Text}，g_nLayerStart={g_nLayerStart}，g_nLayerEnd={g_nLayerEnd}");
            return true;
        }

        private void ReadLayerInfo()
        {
            //(5)（5）打印任务及运动信息     
            if (g_bSelectedLayerRangeReady)
            {
                g_nLayerStart = g_nSelectedLayerStart;
                g_nLayerEnd = g_nSelectedLayerEnd;
            }
            else
            {
                g_nLayerStart = Convert.ToInt32(this.LayerStart.Text) - 1;//201030批注：系统内部打印区间为从0开始计数第1层
                g_nLayerEnd = Convert.ToInt32(this.LayerEnd.Text) - 1;//201030批注：系统内部打印区间为从0开始计数第1层
            }

            g_SharpControl.g_nLayerEnd = g_nLayerEnd;//20210530新增：打印的总层数：打印显示的时候都会即时更新

        }
        PrinterSysParam g_cPrinterSysParam = new PrinterSysParam();//20200222新增：

        RoyalPrintingMap g_cRoyalPrint = new RoyalPrintingMap();//创建GoogolMotionMap对象，供本窗口调用    

        double g_dHeight1, g_dHeight2, g_dHeight3, g_dHeight4;
        //double[] g_dJourney = new double[6];//20200222：存储的6个电机的校准行程，也是0位的编码器值；单位：脉冲
        bool g_bResetCorrectEnabled = false;//在系统参数设置中安排
        private void InitSystem()//20200220：初始化系统
        {
            //（1）初始化固高系统（2）初始化喷墨系统（3）开启状态检测线程（4）运动机构复位 （5）打印任务信息        
            //（1）打开运动控制器：（1）初始化固高系统
            g_cMotionMap.OpenCardComunication(); //（2）复位控制器：复位控制模式——复位后，默认的控制模式是“脉冲+方向的脉冲控制方式”          
            g_cMotionMap.ResetCardControlMode();//（3）初始化控制器：（1）下载配置文件（具体就包括，控制器软硬件资源的配置：设置各轴的报警、正负限位、是否有效、规划模式等等）；          
            g_cMotionMap.InitCardConfiguration();//20200111修改：下载配置文件/固高的限位设置bug/固高的限位设置bug

            Thread.Sleep(100);
            ////（2）初始化喷墨系统————————//移动到20200305
            //g_cRoyalPrint.InitRoyalPrintCard();

            ////（4）送粉铺粉运动机构复位:3轴JOG复位，设置对应的编码值为0；//20200222:本部分运动手动调试模块执行，更为合适
            ////注意：千脉冲mm数为：1000；速度：不乘系数1000
            //（1-1）多轴行程值初始化（1-2）多轴运动值初始化
            //if (g_bResetCorrectEnabled){}//是否进行初始化复位操作
            SerializeFromBin();//20200222：导入存储的6个电机的校准行程；

            //（5）打印任务及运动信息     
            this.LayerEnd.Text = g_nLayerEnd.ToString();
            this.LayerStart.Text = g_nLayerStart.ToString();
#if true
            this.LayerEnd.Enabled = false;//20200602
            this.LayerStart.Enabled = false;//20200602
#endif
        }

        public bool LastSaveFlag = true;//断电保护现场有2个文件，其中true表示1的读写标志：
        private void SerializeToBin(PrinterSysParam p)//20200222新建:对象序列化输出
        {
            if (LastSaveFlag)
            {
                string filePath = "PrinterConfiguration0.bin";
                using (FileStream fs = new FileStream(filePath, FileMode.Create/* FileMode.Create*/))
                {
#if true
                    BinaryFormatter bf = new BinaryFormatter();
                    try
                    {
                        bf.Serialize(fs, p);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        fs.Flush(true);
                    }
#else
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(fs, p);
#endif
                }
                LastSaveFlag = !LastSaveFlag;
            }
            else
            {
                //Person p = GetPersonInfos();
                string filePath = "PrinterConfiguration1.bin";
                using (FileStream fs = new FileStream(filePath, FileMode.Create/* FileMode.Create*/))
                {
#if true
                    BinaryFormatter bf = new BinaryFormatter();
                    try
                    {
                        bf.Serialize(fs, p);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        fs.Flush(true);
                    }
#else
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(fs, p);
#endif
                }
                LastSaveFlag = !LastSaveFlag;
            }
        }
        private void SerializeFromBin()//20200222新建:对象序列化输入
        {
            string filePath1 = "PrinterConfiguration0.bin";
            string filePath2 = "PrinterConfiguration1.bin";
            //DateTime PsF = Convert.ToDateTime(g_cPrinterSysParamFF.DT);//读取文件的修改时间：时间较远的值为准确值//DateTime表示时间上的1时刻
            //DateTime Ps = Convert.ToDateTime(g_cPrinterSysParam.DT);//读取文件的修改时间：时间较远的值为准确值
#if true//使用异常处理的方式，来提高系统的容错能力:读取系统的位置值
            if (File.Exists(filePath1) && File.Exists(filePath2))//存在两个文件夹：20200515批注
            {
                try//试一试
                {
                    if (File.Exists(filePath1))//读取确定无疑的对象参数
                    {
                        using (FileStream fs = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))//20230320修改新增：
                        {
                            //(1)反序列化读取
                            BinaryFormatter bf = new BinaryFormatter();
                            g_cPrinterSysParam = bf.Deserialize(fs) as PrinterSysParam;

                            //(2)刷新到系统
                            //g_cPrinterSysParam.g_dJourney[0] = 300 * 1000; g_cPrinterSysParam.g_dJourney[1] = 300 * 1000; g_cPrinterSysParam.g_dJourney[2] = 300 * 1000;//20200222：初始化行程//测试使用,移动到InitSystem
                            //g_cPrinterSysParam.g_dJourney[3] = 500 * 1000; g_cPrinterSysParam.g_dJourney[4] = 10 * 1000; g_cPrinterSysParam.g_dJourney[5] = 350 * 1000;//20200222：初始化行程//测试使用,移动到InitSystem                      
                            for (short i = 0; i < 6; i++)//设置编码器的位置值
                            {
                                g_cMotionMap.SetEncPos((short)(i + 1), (int)(g_cPrinterSysParam.g_dPositon[i] * 1000));//在正限位值，设置0位的编码值。单位：脉冲
                            }
                        }
                    }
                }
                catch (Exception)//抓一抓
                {
                    if (File.Exists(filePath2))//读取确定无疑的对象参数
                    {
                        using (FileStream fs = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))//20230320修改：
                        {
                            //(1)反序列化读取
                            BinaryFormatter bf = new BinaryFormatter();
                            g_cPrinterSysParam = bf.Deserialize(fs) as PrinterSysParam;

                            //(2)刷新到系统
                            try
                            {
                                //g_cPrinterSysParam.g_dJourney[0] = 300 * 1000; g_cPrinterSysParam.g_dJourney[1] = 300 * 1000; g_cPrinterSysParam.g_dJourney[2] = 300 * 1000;//20200222：初始化行程//测试使用,移动到InitSystem
                                //g_cPrinterSysParam.g_dJourney[3] = 500 * 1000; g_cPrinterSysParam.g_dJourney[4] = 10 * 1000; g_cPrinterSysParam.g_dJourney[5] = 350 * 1000;//20200222：初始化行程//测试使用,移动到InitSystem                      
                                for (short i = 0; i < 6; i++)//设置编码器的位置值
                                {
                                    g_cMotionMap.SetEncPos((short)(i + 1), (int)(g_cPrinterSysParam.g_dPositon[i] * 1000));//在正限位值，设置0位的编码值。单位：脉冲
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }

#else//使用时间先后的方式，提高系统的容错能力:读取系统的位置值

                FileInfo FileInfo1 = new FileInfo(filePath1);//获取file的上次访问时间_12h 30min 30s 859ms
                FileInfo FileInfo2 = new FileInfo(filePath2);//获取file的上次访问时间_12h 30min 30s 746ms
                TimeSpan interval = FileInfo1.LastWriteTime - FileInfo2.LastWriteTime;//表示时间间隔                
                if (interval.TotalSeconds > 0)//20200226：确定准确的时间值//——谁大，谁靠前
                {
                    if (File.Exists(filePath1))//读取确定无疑的对象参数
                    {
                        using (FileStream fs = new FileStream(filePath1, FileMode.Open))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            g_cPrinterSysParam = bf.Deserialize(fs) as PrinterSysParam;
                        }
                    }
                }
                else
                {
                    if (File.Exists(filePath2))//读取确定无疑的对象参数
                    {
                        using (FileStream fs = new FileStream(filePath2, FileMode.Open))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            g_cPrinterSysParam = bf.Deserialize(fs) as PrinterSysParam;
                        }
                    }
                }
#endif
            }
            else//软件第一次运行的时候：不进行初始化工作
            {
            }
        }

        ////（1）刷新轴使能报警信号//20200718批注：新增主界面墨水接通状态
        LaserADD_BinderJetter.InkStateCtrl[] inkStateCtrl2 =
            { new LaserADD_BinderJetter.InkStateCtrl(),new LaserADD_BinderJetter.InkStateCtrl(),new LaserADD_BinderJetter.InkStateCtrl(),new LaserADD_BinderJetter.InkStateCtrl(),new LaserADD_BinderJetter.InkStateCtrl()};
        System.Drawing.Bitmap[] inkState2 = { new System.Drawing.Bitmap(500, 100), new System.Drawing.Bitmap(500, 100), new System.Drawing.Bitmap(500, 100), new System.Drawing.Bitmap(500, 100), new System.Drawing.Bitmap(500, 100) };
        Rectangle[] rectangle2 = new Rectangle[5];//20200718新建批注：修改为5项数据监控项
        double[] g_dEncpos = new double[8];
        double[] g_dEncvel = new double[8];

        double[] g_dVoltageValue = new double[4];//20200417新增：总计是8路的值，读取4路的值足够用了
        UInt32 nValveStateMask = 0b0;//20200718新增：所有的电磁阀状态信息：
        private Communication.ModbusCommunicateMap modbusCommunicateMap/* = new Communication.ModbusCommunicateMap()*/;//实例化Modbus通讯接口对象;//20220515新建：Modbus通讯映射
        private void timer2_Monitor(object sender, EventArgs e)//20200220:监控线程定时刷新定时器2//定时器本身就是1种线程处理方式
        {
            //（1）刷新6轴限位状态（2）刷新电压温度值（3）刷新墨盒信号（4）刷新打印进度（5）刷新6轴的位置///先获取电压温度值///再刷上去数据///墨盒信号///刷新打印的状态

            //(1)刷新6轴对应的限位状态————//20200220更新：
            Int32[] nIOState = new Int32[6];//20200110：6轴的状态位
            UInt32 nIOState2 = 0;//状态标志位：20200311//20200327批注：墨车的状态位
            Int32[] nIOMask = { 0/*6*/, 0, 0, 0/*127*/ }/*new int[3]*/;//20200110：6轴的限位状态，依次为P，Z，N 限位
                                                                       //UInt32 nxpos, ny1pos, ny2pos;//不需要
                                                                       //UInt32 nxAxis, nyAxis1, nyAxis2;//不需要
                                                                       //(a)实时的轴编码器位置//(b)剩余脉冲数获取//(c)实时的轴限位状态
            for (short AXIS = 1; AXIS <= 6; AXIS++)
            {
                nIOState[AXIS - 1] = g_cMotionMap.MointoringAxis2(AXIS);
            }
            for (short AXIS = 1; AXIS <= 6; AXIS++)// (1)次序为P，Z，N 限位 (2)实时的多轴报警状态
            {
                if (0 != (nIOState[AXIS - 1] & 0x20)) { nIOMask[0] |= (1 << (AXIS - 1)); }//有报警——//20200226修正：主界面报警1号轴限位不正常，无反应
                else { }//无报警
                if (0 != (nIOState[AXIS - 1] & 0x00)) { nIOMask[1] |= (1 << (AXIS - 1)); }//有报警2010110:固高控制器不接零限位信号，掩码取值20//20200226修正：主界面报警1号轴限位不正常，无反应                  
                else { }//无报警
                if (0 != (nIOState[AXIS - 1] & 0x40)) { nIOMask[2] |= (1 << (AXIS - 1)); }//有报警——//20200226修正：主界面报警1号轴限位不正常，无反应
                else { }//无报警
            }

            ////（2-1）刷新轴使能报警信号
            for (int i = 0; i < 3; i++)
            {
                inkStateCtrl2[i].SetInkCount(6, 0);//20200220新建：
                inkStateCtrl2[i].SetInkState(nIOMask[i]);//很关键//20200220新建：
                inkState2[i].SetPixel(pictureBox1.Width, pictureBox1.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
                Graphics g2 = Graphics.FromImage(inkState2[i]);
                g2.Clear(pictureBox1.BackColor);
                rectangle2[i].Width = pictureBox1.Width; rectangle2[i].Height = pictureBox1.Height;//rectanle是bitmap的大小
#if true
                inkStateCtrl2[i].OnPaint(g2, rectangle2[i]);
#else
            inkStateCtrl2[i].OnPaintCircle(g2, rectangle2[i]);
#endif
                switch (i)
                {
                    case 0:
                        pictureBox1.CreateGraphics().DrawImage(inkState2[i], new Point(0, 0));
                        pictureBox1.Image = inkState2[i];
                        break;
                    case 1:
                        pictureBox2.CreateGraphics().DrawImage(inkState2[i], new Point(0, 0));
                        pictureBox2.Image = inkState2[i];
                        break;
                    case 2:
                        pictureBox3.CreateGraphics().DrawImage(inkState2[i], new Point(0, 0));
                        pictureBox3.Image = inkState2[i];
                        break;
                }
            }

            ////（2-2）刷新剩余墨量信号//20200331新增：
            ////（0）首先把数据刷上来//20200418新增：
            //UInt32 nInkMask = royal.royal.DEV_GetInputIO() >> 16;
            UInt32 nTempInkMask = royal.royal.DEV_GetInput();//我估计不够
            UInt32 nInkMask = ((nTempInkMask >> 16) & 0b1111)
                | ((nTempInkMask >> 10) & 0b10000)
                | ((nTempInkMask >> 10) & 0b100000)
                | ((nTempInkMask >> 19) & 0b1000000);

            inkStateCtrl2[3].SetInkCount(7, 0);//20200220新建：//依次是1级墨盒，2级墨x4，1级清洗液x1，空气保护瓶x1
            inkStateCtrl2[3].SetInkState((int)nInkMask/*nIOMask[3]*/);//很关键//20200220新建：
            inkState2[3].SetPixel(pictureBox5.Width, pictureBox5.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
            Graphics g3 = Graphics.FromImage(inkState2[3]);
            g3.Clear(Color.DarkCyan/*pictureBox5.BackColor*/);
            rectangle2[3].Width = pictureBox5.Width; rectangle2[3].Height = pictureBox5.Height;//rectanle是bitmap的大小
#if false
        inkStateCtrl2[3].OnPaint(g3, rectangle2[3]);
#else
            inkStateCtrl2[3].OnPaintCircle(g3, rectangle2[3]);
#endif
            pictureBox5.CreateGraphics().DrawImage(inkState2[3], new Point(0, 0));
            pictureBox5.Image = inkState2[3];

            //(2)刷新正负压及三通电磁阀的接通状态
            UInt32 nTempValveStateMask = 0b0/*0b00000001*/;//第8位为正负压接通状态，1-7位为清洗/墨水阀的接通状态          

            ////nTempValveStateMask = royal.royal./*DEV_GetInput*//*DEV_GetUsbOutput*/DBG_GetPrtInfo(0, 7);//20200718测试：需要经过全面的测试

            nValveStateMask = ((nTempValveStateMask >> 8) & 0b00111111)
                | ((nTempValveStateMask >> 8) & 0b01000000) << 1
                | ((nTempValveStateMask >> 8) & 0b10000000) >> 1;//20200718修改批注：修复车头板保留输出7/8端口颠倒的BUG
                                                                 //    | ((nTempInkMask >> 19) & 0b1000000);

            inkStateCtrl2[4].SetInkCount(8, 0);//20200220新建：//依次是：第8位为正负压接通状态，1-7位为清洗/墨水阀的接通状态
            inkStateCtrl2[4].SetInkState((int)nValveStateMask/*nIOMask[3]*/);//很关键//20200220新建：
            inkState2[4].SetPixel(pictureBox6.Width, pictureBox6.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
            Graphics g4 = Graphics.FromImage(inkState2[4]);
            g4.Clear(Color.DarkCyan/*pictureBox6.BackColor*/);
            rectangle2[4].Width = pictureBox6.Width; rectangle2[4].Height = pictureBox6.Height;//rectanle是bitmap的大小
#if false
        inkStateCtrl2[4].OnPaint(g3, rectangle2[4]);
#else
            inkStateCtrl2[4].OnPaintValveState(g4, rectangle2[4]);
#endif
            pictureBox6.CreateGraphics().DrawImage(inkState2[4], new Point(0, 0));
            pictureBox6.Image = inkState2[4];

            ////（4）刷新打印进度
            this.WorkProgressBar.Minimum = 0;//进度条:异步刷新
            this.WorkProgressBar.Maximum = g_nLayerEnd;//进度条
            UpdateBarValueMethod2(g_nLayerCurrent);

            //（5）刷新6轴的位置+刷新墨车系统正负压的读数：20200417新建批注
            g_dEncpos = g_cMotionMap.GetEncPos();
            g_dEncvel = g_cMotionMap.GetEncVel();
            //textBox4.Clear();
            g_dVoltageValue = g_cMotionMap.GetAi();//读取固高的8路ADC电压输入
                                                   //this.PressureLabel.Clear();//20200417新增：原来的TEXT控件更换为label控件，不支持本方法

            //(5-3)计算并显示实际的正负压值
            if ((1 < g_dVoltageValue[0]) && (g_dVoltageValue[0] <= 5))//正负压在-100kPa到+100kPa之间
            {
                this.PressureLabel.ForeColor = Color.Blue;
                string PosNetPressure = ((g_dVoltageValue[0] - 3) * 200 / 4).ToString("F2");//20200427新增：显示2位数值的正负压
                this.PressureLabel.Text = PosNetPressure;
            }
            else if ((g_dVoltageValue[0] > 5.0))//超出数显表的正常的工作区间：正负压在-100kPa到+100kPa之间
            {
                //this.PressureLabel.ForeColor = Color.Red;
                this.PressureLabel.Text = "报警：气压过高！";

            }
            else if (g_dVoltageValue[0] < 1.0)
            {
                //this.PressureLabel.ForeColor = Color.Red;
                this.PressureLabel.Text = "报警：气压过低！";
            }

            ////(5-4)读取实时温度并显示：20220515新建////建立Modbus通讯，并读取实时党的温度值（工程值）：20220514新建
            //int SlaveNumber = 1; int RegisterAddress = 2000; int RegisterNumber = 1;//读取输入寄存器值并完成显示
            //ushort[] CurrentTemperature = null;
            //if (AutoPrintMotion2!= null)
            //{
            //    /******读取30001的内部计算值******/
            //    CurrentTemperature = AutoPrintMotion2.modbusCommunicateMap.ReadInputRegisters((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber);
            //}

            //if (CurrentTemperature != null)
            //{
            //    this.TemperatureLabel.Text = CurrentTemperature[0].ToString();//显示当前温度值为
            //}
            //else
            //{
            //    //this.TemperatureLabel.ForeColor = Color.Red;
            //    this.TemperatureLabel.Text = "报警：温控仪关闭!";//return;
            //}

            //(5-1)刷新墨车的位置信号//20200327新增：
            UInt32 ny1pos = royal.royal.DEV_GetPrintEncoderValue();//20200306新增：编码器位置设置
                                                                   //string ny1pos0 = ((double)(ny1pos / 5080)).ToString("F2");
                                                                   //string ny1pos0 = (((double)ny1pos) / 5080).ToString("F3");
                                                                   //string szTxt = ny1pos.ToString("X");//16进制显示——20200108
            double XPosValue = 0, YPosValue = 0, Z1PosValue = 0, Z2PosValue = 0, Z3PosValue = 0;
            string PositonText = null;
            string ny1pos0 = String.Format("{0,9:#0000.000}", ((double)ny1pos) / EncoderLinePerMM);
            XPosValue = ((double)ny1pos) / EncoderLinePerMM;
            //textBox4.AppendText(" " + "墨车当前位置：" + ny1pos0 + "mm\r\n");
            PositonText = " " + "墨车当前位置：" + ny1pos0 + " MM\r\n";
            //（5-2）刷新6轴的位置
            string PosString;
            for (short i = 0; i < 6; i++)
            {
                double PosValue = g_dEncpos[i] / 1000;
                //PosString = (PosValue/*(double)(g_dEncpos[i] / 1000)*/).ToString("F3");//20200227：显示两位小数点位置，即精确到10um
                PosString = String.Format("{0,9:#0000.000,}", PosValue);
                switch (i)
                {
                    case 0:
                        //textBox4.AppendText(" " + "主成型缸位置：" + PosString + "mm\r\n");
                        PositonText += " " + "主成型缸位置：" + PosString + " MM\r\n";
                        g_cPrinterSysParam.g_dPositon[0] = PosValue;
                        Z1PosValue = PosValue;
                        break;
                    case 1:
                        //textBox4.AppendText(" " + "后粉料缸位置：" + PosString + "mm\r\n");
                        PositonText += " " + "后粉料缸位置：" + PosString + " MM\r\n";
                        g_cPrinterSysParam.g_dPositon[1] = PosValue;
                        Z2PosValue = PosValue;
                        break;
                    case 2:
                        //textBox4.AppendText(" " + "前粉料缸位置：" + PosString + "mm\r\n");
                        PositonText += " " + "前粉料缸位置：" + PosString + " MM\r\n";
                        g_cPrinterSysParam.g_dPositon[2] = PosValue;
                        Z3PosValue = PosValue;
                        break;
                    case 3:
                        //textBox4.AppendText(" " + "铺粉小车位置：" + PosString + "mm\r\n");
                        PositonText += " " + "铺粉小车位置：" + PosString + " MM\r\n";
                        g_cPrinterSysParam.g_dPositon[3] = PosValue;
                        YPosValue = PosValue;
                        break;
                    case 4:
                        //textBox4.AppendText(" " + "卡紧电机位置：" + PosString + "mm\r\n");
                        PositonText += " " + "卡紧电机位置：" + PosString + " MM\r\n";
                        g_cPrinterSysParam.g_dPositon[4] = PosValue;
                        break;
                    case 5:
                        //textBox4.AppendText(" " + "刮墨电机位置：" + PosString + "mm");
                        PosString = String.Format("{0,9:#0000.000,}", PosValue * 125);
                        PositonText += " " + "刮墨电机位置：" + PosString + " MM";
                        g_cPrinterSysParam.g_dPositon[5] = PosValue * 125;//20200623批注：修正步进电机的参数：1000pulse/125mm
                        break;
                }
            }
            //textBox4.AppendText(PositonText);//PositonText += "\r\n";
            PositionLable.Text = PositonText;
            string PositonText2 = String.Format("|| X-{0,5:000.0,} MM; Y-{1,5:000.0,} MM; Z1-{2,5:000.0,} MM; Z2-" +
                "{3,5:000.0,} MM; Z3-{4,5:000.0,} MM",
                XPosValue, YPosValue, Z1PosValue, Z2PosValue, Z3PosValue);

            toolStripStatusLabel1.Text = PositonText2;

            //(6)刷新墨车的报警信号//20200327新增：
            //////////////////////////////////(c)实时的轴限位状态
            nIOState2 = royal.royal.DEM_GetAxisLmtZeroState(0);//底层接口已经作了12 bit移位处理，对照Reg[12]定义——————世彪批注：获取限位状态20200106
                                                               //////////////////////////////////(c)实时的多轴报警状态
                                                               /////////(2)次序为P，Z，N 限位
            if (0 != (nIOState2 & 0x2))//是否有报警：有限位报警
            { this.Y1PLLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y1PLLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState2 & 0x10))//有报警
            { this.Y1ZeroLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y1ZeroLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState2 & 0x1))//有报警
            { this.Y1NLLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y1NLLabel.BackColor = Color.LimeGreen; }

            //保存位置信息到本地
            //日志保存：保存校准行程及原点位置及加工日志及异常日志
            g_cPrinterSysParam.g_dPositon[6] = 888;
            SerializeToBin(g_cPrinterSysParam);//20200222://(1)存储6个电机的校准行程
        }
        private void CreateModbusCommunicate()
        {
            ////建立Modbus通讯，并读取实时党的温度值（工程值）：20220514新建
            /*Communication.ModbusCommunicateMap */
            modbusCommunicateMap = new Communication.ModbusCommunicateMap();//实例化Modbus通讯接口对象

            //创建串口参数
            modbusCommunicateMap.serialPort.PortName = /*"ELTIMA Virtual Serial Port(COM2->COM3)"*/ /* "COM3"*/"COM3";
            modbusCommunicateMap.serialPort.BaudRate = (int)9600;//COM1的通讯速率为9600bps
            modbusCommunicateMap.serialPort.DataBits = (int)8/*cbxDataBits.SelectedItem*/;
            modbusCommunicateMap.serialPort.Parity = System.IO.Ports.Parity.Even /*GetSelectedParity()*/;//20220514修改：为奇校验
            modbusCommunicateMap.serialPort.StopBits = System.IO.Ports.StopBits.One;

            //创建ModubusRTU主站实例
            //ModbusSerialMaster master = ModbusSerialMaster.CreateRtu(modbusCommunicateMap.serialPort);
            modbusCommunicateMap.master = ModbusSerialMaster.CreateRtu(modbusCommunicateMap.serialPort);
            try
            {
                //打开串口
                if (!modbusCommunicateMap.serialPort.IsOpen)
                {
                    modbusCommunicateMap.serialPort.Open();
                    MessageBox.Show("打开了关闭的串口！");
                }

                ////读取输入寄存器值并完成显示
                //int SlaveNumber = 1;
                //int RegisterAddress = 1/*4002*/;
                //int RegisterNumber = 19;
                //ushort[] CurrentTemperature = modbusCommunicateMap.ReadHoldingRegisters/*ReadInputRegisters*/((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber/*(byte)2, (ushort)30000, (ushort)5*/);//读取30001的内部计算值
                //                                                                                                                                                                                                       //ushort[] CurrentTemperature2 = modbusCommunicateMap.ReadInputRegisters((byte)1, (ushort)32001, (ushort)1);//读取30001的工程值

                //MessageBox.Show("当前温度的内部计算值为：" + CurrentTemperature[0].ToString() /*+ "；当前温度的工程值为：" + CurrentTemperature2[0].ToString()*/);

                ////关闭串口
                //modbusCommunicateMap.serialPort.Close();
                ////MessageBox.Show("关闭了打开的串口！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("连接失败：" + ex.Message);
                return;
            }
        }
        /************************打印机框架：喷墨打印数据传输及打印线程*************************/
        /************************打印机框架：喷墨打印数据传输及打印框架*************************/
        /************************打印机框架：喷墨打印数据传输及打印框架*************************/
        private bool StartStopDataTASK(bool OperationFlag)//20201119批注：本部分内容是阻塞操作
        {
            string msg = null;
            if (true == OperationFlag)//建立数据传输线程:DataTaskTHREAD
            {
                if (g_nCorrectionTaskThreadFlag == 0)//20210320新增:
                {
                    if (true == ImportCLIFlag)//载入CAD文件
                    {
#if true//20230419新增：确保打印前载入过波形文件
                        if (AutoPrintMotion0 == null)
                        {
                            AutoPrintMotion0 = new 手动操作(0, nValveStateMask, false);//20201030新增：读取自动打印参数
                            msg = $"创建：AutoPrintMotion0=》初次创建完成-手动操作！";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            msg = $"创建：AutoPrintMotion0=》不需重新创建-手动操作！";
                            Log4Net.Info(msg);
                        }
                        bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion0);//20230419修改：读取自动打印参数
                        if (AutoPrintMotion0.k_RYSYSParamAutoPrintParamInTest.CurrectLoadWaveName == "null")
                        {
                            msg = "启动打印线程失败，没有载入过波形文件：DataTaskTHREAD";
                            Log4Net.Info(msg);
                            MessageBox.Show("请先载入本次打印任务波形。。。");//没有载入过波形文件
                            return false;
                        }
#endif
                        if ((DataTaskFlag == 1) || (DataTaskFlag == 4)) //DataTaskTHREAD处于初始态或者结束态
                        {
                            string tempThreadName = "DataTaskTHREAD";
                            Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                            if (tempThread != null)
                            {
                                PrinterLogicThreads.Remove(tempThread);
                                ThreadStart initThreadEntry = new ThreadStart(DataTaskTHREAD);
                                tempThread = new Thread(initThreadEntry) { IsBackground = true };
                                tempThread.Name = tempThreadName;
                                tempThread.Start();
                                PrinterLogicThreads.Add(tempThread);
                            }
                            else//创建开启DataTaskTHREAD：
                            {
                                bool returnFlag = StartCloseJOB(false);//20201124新增：开启只执行1次，JOB并赋值关键打印参数；架构上本人
                                ModifyJobAeraFLag = true;//20201124新增：
                                ThreadStart initThreadEntry = new ThreadStart(DataTaskTHREAD);
                                tempThread = new Thread(initThreadEntry) { IsBackground = true };
                                tempThread.Name = tempThreadName;
                                tempThread.Start();
                                PrinterLogicThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                            }

                            msg = "启动打印线程成功： " + tempThreadName;
                            Log4Net.Info(msg);

                            return true;//创建成功
                        }
                        else//创建失败
                        {
                            msg = "启动打印线程失败，原因未知：DataTaskTHREAD";
                            Log4Net.Info(msg);

                            return false;
                        }
                    }
                    else
                    {
                        msg = "启动打印线程失败，请先载入CAD数据：DataTaskTHREAD";
                        Log4Net.Info(msg);

                        MessageBox.Show("请先载入CAD数据。。。");//没有载入过CAD文件
                        return false;//创建失败
                    }
                }
                else /*if (g_nCorrectionTaskThreadFlag == 1)*///20210320新增:建立数据传输线程:DataTaskTHREAD
                {
                    string tempThreadName = "DataTaskTHREAD2";
                    Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                    if (tempThread != null)
                    {
                        PrinterLogicThreads.Remove(tempThread);
                        ThreadStart initThreadEntry = new ThreadStart(DataTaskTHREAD2);
                        tempThread = new Thread(initThreadEntry) { IsBackground = true };
                        tempThread.Name = tempThreadName;
                        tempThread.Start();
                        msg = "启动打印线程成功： " + tempThreadName;
                        Log4Net.Info(msg);

                        PrinterLogicThreads.Add(tempThread);
                    }
                    else//创建开启DataTaskTHREAD：
                    {
                        bool returnFlag = StartCloseJOB(false);//20201124新增：开启只执行1次，JOB并赋值关键打印参数；架构上本人
                        ModifyJobAeraFLag = true;//20201124新增：
                        ThreadStart initThreadEntry = new ThreadStart(DataTaskTHREAD2);
                        tempThread = new Thread(initThreadEntry) { IsBackground = true };
                        tempThread.Name = tempThreadName;
                        tempThread.Start();
                        msg = "启动打印线程成功： " + tempThreadName;
                        Log4Net.Info(msg);

                        PrinterLogicThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                    }
                    return true;//创建成功
                }
            }
            else//关闭并删除数据传输线程:DataTaskTHREAD
            {
                if (g_nCorrectionTaskThreadFlag == 0)//20210320新增:
                {
                    //TransferModifyFlag = "PauseFlag";//通知线程进入等待状态//20230421新增：
                    while (DataTaskFlag == 2/*2*/)//等待直到进入安全态//20230421修改：
                    {
                        Thread.Sleep(100);
                    }
                    TransferModifyFlag = "StopFlag";//20230421新增：
                    string tempThreadName = "DataTaskTHREAD";//(1)关闭联调线程
                    DeleteThread(tempThreadName);
                    DataTaskFlag = 1;

                    bool returnFlag = StartCloseJOB(false);//20201124新增：开启只执行1次，JOB并赋值关键打印参数；架构上本人后续再优化一下
                    TransferModifyFlag = "StartFlag";//传送取消标志位
                    return true;//删除成功
                }
                else /*if (g_nCorrectionTaskThreadFlag == 1)*///20210320新增:建立数据传输线程:DataTaskTHREAD
                {
                    //TransferModifyFlag = "PauseFlag";//通知线程进入等待状态
                    //while (DataTaskFlag == 2)//等待直到进入安全态
                    //{
                    //    Thread.Sleep(100);
                    //}
                    string tempThreadName = "DataTaskTHREAD2";//(1)关闭联调线程
                    DeleteThread(tempThreadName);
                    //DataTaskFlag = 1;

                    bool returnFlag = StartCloseJOB(false);//20201124新增：开启只执行1次，JOB并赋值关键打印参数；架构上本人后续再优化一下
                    //TransferModifyFlag = "StartFlag";//传送取消标志位
                    return true;//删除成功
                }
            }
        }
        bool ModifyJobAeraFLag = false;//20201124新增：修改区间标志位：
        private bool StartStopPrintTASK(bool OperationFlag)//20201119修改：更新
        {
            string msg = null;
            if (true == OperationFlag)//动作1：建立打印线程：PrintTaskTHREAD
            {
                if (g_nCorrectionTaskThreadFlag == 0)//20210320新增:
                {
                    if ((PrintTaskFlag == 1) || (PrintTaskFlag == 4))//初始态、暂停态和结束态情况下才执行创建过程
                    {
                        string tempThreadName = "PrintTaskTHREAD";
                        Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                        if (tempThread != null)
                        {
                            PrinterLogicThreads.Remove(tempThread);
                        }
                        else
                        {
                            RoyalMap.m_bJobStarted = true;
                            returnPrintValue = 0;
                            StopPrintFlag = false;
                            ThreadStart initThreadEntry = new ThreadStart(PrintTaskTHREAD);
                            tempThread = new Thread(initThreadEntry) { IsBackground = true };
                            tempThread.Name = tempThreadName;
                            tempThread.Start();

                            msg = "启动打印线程成功：" + tempThreadName;
                            Log4Net.Info(msg);

                            PrinterLogicThreads.Add(tempThread);
                        }
                        return true;//创建成功
                    }
                    else if ((PrintTaskFlag == 2) || (PrintTaskFlag == 3))//处于工作态或者暂停态时则不执行
                    {
                        msg = "启动打印线程失败，处于工作态以及暂停态：PrintTaskFlag = " + PrintTaskFlag;
                        Log4Net.Info(msg);

                        return false;//创建失败
                    }
                    else
                    {
                        msg = "启动打印线程失败，未知原因！";
                        Log4Net.Info(msg);

                        return false;
                    }//创建失败
                }
                else /*if (g_nCorrectionTaskThreadFlag == 1)*///20210320新增:建立数据传输线程:PrintTaskTHREAD
                {
                    string tempThreadName = "PrintTaskTHREAD2";
                    Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                    if (tempThread != null)
                    {
                        PrinterLogicThreads.Remove(tempThread);
                    }
                    else//创建开启DataTaskTHREAD：
                    {
                        RoyalMap.m_bJobStarted = true;
                        returnPrintValue = 0;
                        StopPrintFlag = false;
                        ThreadStart initThreadEntry = new ThreadStart(PrintTaskTHREAD2);
                        tempThread = new Thread(initThreadEntry) { IsBackground = true };
                        tempThread.Name = tempThreadName;
                        tempThread.Start();
                        msg = "启动打印线程成功：" + tempThreadName;
                        Log4Net.Info(msg);

                        PrinterLogicThreads.Add(tempThread);
                        PrintTaskFlag = 1;//更新状态为2
                    }
                    return true;//创建成功
                }
            }
            else//动作2：关闭并删除数据传输线程
            {
                if (g_nCorrectionTaskThreadFlag == 0)//20210320新增:
                {
                    if (true/*PrintTaskFlag == 2*/)//处于工作态：删除对应的状态
                    {
#if false//正式打印必须释放出来
                UInt32 ny2pos = 0;//(1-1)缓冲主运动：判断你是否到达缓冲区边缘位置：确保运动到位
                bool Directory = false; uint nRevPls = 0;
                while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls))//int SleepTime = (int)((double)(AimPos - CurrentPos) / nSpeed);//Thread.Sleep(5000);//Sleep时间必须要有依据//确保运行到位，运行精度为2UM
                {
                    Thread.Sleep(20);
                }
                bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
#endif
                        PrintFlag = false;//20200716新增：关闭打印机维护的间歇闪喷使能//(1-2)关闭打印线程（联调线程）（2）关闭多轴运动：保障运动安全：20200411批注
                        string tempThreadName = "PrintTaskTHREAD";//(闭联调线程
                        DeleteThread(tempThreadName);
                        LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();//（2）关闭多轴运动：保障运动安全
                        AutomoveComponent.StopMove();
                        g_nCurrentLayer = 0;//20201021新增：当前打印的层数
                        returnPrintValue = 0;//20200508：复位打印进度值

                        PrintTaskFlag = 1;//更新状态为初始态
                    }
                    return true;
                }
                else
                {
                    if (true/*PrintTaskFlag == 2*/)//处于工作态：删除对应的状态
                    {
#if false//正式打印必须释放出来
                    UInt32 ny2pos = 0;//(1-1)缓冲主运动：判断你是否到达缓冲区边缘位置：确保运动到位
                    bool Directory = false; uint nRevPls = 0;
                    while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls))//int SleepTime = (int)((double)(AimPos - CurrentPos) / nSpeed);//Thread.Sleep(5000);//Sleep时间必须要有依据//确保运行到位，运行精度为2UM
                    {
                        Thread.Sleep(20);
                    }
                    bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108    
#endif
                        PrintFlag = false;//20200716新增：关闭打印机维护的间歇闪喷使能//(1-2)关闭打印线程（联调线程）（2）关闭多轴运动：保障运动安全：20200411批注
                        string tempThreadName = "PrintTaskTHREAD2";//(闭联调线程
                        DeleteThread(tempThreadName);
                        LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();//（2）关闭多轴运动：保障运动安全
                        AutomoveComponent.StopMove();
                        g_nCurrentLayer = 0;//20201021新增：当前打印的层数
                        returnPrintValue = 0;//20200508：复位打印进度值

                        PrintTaskFlag = 1;//更新状态为初始态
                    }
                    return true;
                }
            }
        }
        private void TaskThreadContexts(object TransferObject)//内含3大线程
        {
            TransferParam transferParam = TransferObject as TransferParam;//类型转换——输入数据 
            string OperationCode = transferParam.OperationCode;

            switch (OperationCode)
            {
                case "CreateFirst"://启动打印操作
                    if (true == StartStopDataTASK(true))//刷新：正在启动
                    {
                        UpdateDataAndTransfer(1, 0, 3);
                        if (true == StartStopPrintTASK(true))//刷新：启动成功，提示关闭打印
                        {
                            UpdateDataAndTransfer(1, 0, 4);
                        }
                        else//第二阶段失败
                        {
                            string msg = "启动打印失败：" + OperationCode;
                            Log4Net.Info(msg);

                            MessageBox.Show("打印任务启动失败:CODE 2");
                        }
                    }
                    else//刷新：启动失败，提示启动打印
                    { UpdateDataAndTransfer(1, 0, 2); }

                    g_TaskThreadSTATE[0] = 3;
                    break;

                case "CreateCorrection"://20210320新增：校准数据传输及打印控制线程
                    if (true == StartStopDataTASK(true))//刷新：正在启动
                    {
                        UpdateDataAndTransfer(1, 0, 11/*3*/);
                        if (true == StartStopPrintTASK(true))//刷新：启动成功，提示关闭打印
                        {
                            UpdateDataAndTransfer(1, 0, 12/*4*/);
                        }
                        else//第二阶段失败
                        { MessageBox.Show("校准图打印任务启动失败:CODE 2"); }
                    }
                    else//刷新：启动失败，提示启动打印
                    { UpdateDataAndTransfer(1, 0, 13/*2*/); }

                    g_TaskThreadSTATE[5] = 3;//20210320更新：
                    break;

                case "DeleteMust"://删除打印操作:删除的结果一定是成功
                    UpdateDataAndTransfer(0, 0, 10);
                    StartStopPrintTASK(false);//删除操作一定为成功//删完DataTaskTHREAD//此过程不会出现异常
                    StartStopDataTASK(false);//删除操作一定为成功//删完DataTaskTHREAD//此过程存在异常可能性
                    g_nCorrectionTaskThreadFlag = 0;//20210323新建：复位校准图打印标志位为0
                    g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;//20210323新建：复位校准图打印标志位为0

                    g_TaskThreadSTATE[1] = 3;
                    g_TaskThreadSTATE[3] = 1;//20201119新增：DataTaskThread恢复为默认状态（关机后）
                    g_TaskThreadSTATE[4] = 1;//20201119新增：DataTaskThread恢复为默认状态（关机后）
                    g_TaskThreadSTATE[5] = 1;//20210320更新：DataTaskThread恢复为默认状态（关机后）,校准图打印标志位

                    UpdateDataAndTransfer(0, 0, 2);//删除成功，提示：启动打印
                    break;

                case "CreateAgain"://修改续打操作
                    UpdateDataAndTransfer(1, 0, 9);
                    //（1）关闭所有的打印任务
                    StartStopPrintTASK(false);//删除操作一定为成功//删完DataTaskTHREAD//此过程不会出现异常
                    StartStopDataTASK(false);//删除操作一定为成功//删完DataTaskTHREAD//此过程存在异常可能性

                    g_TaskThreadSTATE[1] = 3;
                    g_TaskThreadSTATE[3] = 1;//20201119新增：DataTaskThread恢复为默认状态（关机后）
                    g_TaskThreadSTATE[4] = 1;//20201119新增：DataTaskThread恢复为默认状态（关机后）

                    //（2）更新打印区间

                    //（3）启动所有的打印任务
                    if (true == StartStopDataTASK(true))//刷新：正在启动
                    {
                        //UpdateDataAndTransfer(1, 0, 3);
                        if (true == StartStopPrintTASK(true))//刷新：启动成功，提示关闭打印
                        {
                            UpdateDataAndTransfer(1, 0, 5);

                        }
                        else//第二阶段失败
                        {
                            MessageBox.Show("打印任务启动失败:失败类型 “2”");//失败类型2：数据处理传输过程开启失败！！
                        }
                    }
                    else//刷新：启动失败，提示启动打印
                    {
                        UpdateDataAndTransfer(1, 0, 2);
                    }

                    g_TaskThreadSTATE[2] = 3;
                    break;

                default:
                    break;
            }
        }

        private void CreateDeleteTaskTHREAD(string OperationCode)//开启打印和关闭打印逻辑：20201119新增：
        {
            string tempThreadName = OperationCode + "JobTHREAD";
            Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
            bool ExecuteThreadFLAG = false;
            switch (OperationCode)
            {
                case "CreateFirst":
                    if ((g_TaskThreadSTATE[0] == 1) || (g_TaskThreadSTATE[0] == 3))
                    {
                        if (tempThread != null)
                        {
                            PrinterLogicThreads.Remove(tempThread);
                            g_TaskThreadSTATE[0] = 2;
                        }
                        ExecuteThreadFLAG = true;//执行线程
                    }
                    else if (g_TaskThreadSTATE[0] == 2)//线程正在执行过程中，不可以重复开线程
                    {
                        ExecuteThreadFLAG = false;//执行线程
                    }
                    break;

                case "CreateCorrection"://20210320新增：校准数据传输及打印控制线程
                    if (g_TaskThreadSTATE[5] == 1)
                    {
                        if (tempThread != null)
                        {
                            PrinterLogicThreads.Remove(tempThread);
                            g_TaskThreadSTATE[5] = 2;
                        }
                        ExecuteThreadFLAG = true;//执行线程
                    }
                    else if (g_TaskThreadSTATE[5] == 2)//线程正在执行过程中，不可以重复开线程
                    {
                        ExecuteThreadFLAG = false;//执行线程
                    }
                    break;


                case "DeleteMust":
                    if ((g_TaskThreadSTATE[1] == 1) || (g_TaskThreadSTATE[1] == 3))
                    {
                        if (tempThread != null)
                        {
                            PrinterLogicThreads.Remove(tempThread);
                            g_TaskThreadSTATE[1] = 2;
                        }
                        ExecuteThreadFLAG = true;//执行线程
                    }
                    else if (g_TaskThreadSTATE[1] == 2)//线程正在执行过程中，不可以重复开线程
                    {
                        ExecuteThreadFLAG = false;//执行线程
                    }

                    break;

                case "CreateAgain":
                    if ((g_TaskThreadSTATE[2] == 1) || (g_TaskThreadSTATE[2] == 3))
                    {
                        if (tempThread != null)
                        {
                            PrinterLogicThreads.Remove(tempThread);
                            g_TaskThreadSTATE[2] = 2;
                        }
                        ExecuteThreadFLAG = true;//执行线程
                    }
                    else if (g_TaskThreadSTATE[2] == 2)//线程正在执行过程中，不可以重复开线程
                    {
                        ExecuteThreadFLAG = false;//执行线程
                    }
                    break;

                default:
                    break;
            }

            if (ExecuteThreadFLAG == true)
            {
                TransferParam TranferControlInfo = new TransferParam();//20200110:放到这里主要方便下面的多线程直接调用
                TranferControlInfo.OperationCode = OperationCode;
                tempThread = new Thread(new ParameterizedThreadStart(TaskThreadContexts)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                tempThread.Name = tempThreadName;//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                tempThread.Start(TranferControlInfo);//(3)第3部曲：多线程3步曲 
                PrinterLogicThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程

            }
            else//不执行
            {
            }
        }
        private void TaskAddDeleteTHREAD(string OperationCode)//20201119新增：删除
        {
            switch (OperationCode)
            {
                case "CreateFirst"://启动打印操作
                    CreateDeleteTaskTHREAD(OperationCode);

                    break;

                case "CreateCorrection"://启动校准打印操作//20210320新建：
                    CreateDeleteTaskTHREAD(OperationCode);

                    break;

                case "DeleteMust"://删除打印操作:删除的结果一定是成功
                    CreateDeleteTaskTHREAD(OperationCode);

                    break;

                case "CreateAgain"://修改续打操作
                    CreateDeleteTaskTHREAD(OperationCode);

                    break;

                default:
                    break;
            }
        }
        //三大JOB相关线程状态+++外加另外两条线程状态，DataTaskTHREAD和PrintTaskTHREAD;默认状态为1，运行状态为2，终止状态为3；三大线程依次为 "CreateFirst"、 "DeleteMust"、"CreateAgain"
        //20210320新建：第6项为"CreateCorrection"线程
        private int[] g_TaskThreadSTATE = new int[6/*5*/] { 1, 1, 1, 1, 1, 1 };//第4-5项为: DataTaskTHREAD、PrintTaskTHREAD,；//20210320新建批注：第1-2-3项为：三大线程依次为 "CreateFirst"、 "DeleteMust"、"CreateAgain"；

        private void PrintBtn_Click(object sender, EventArgs e)//启动/关闭打印
        {
            if (Convert.ToInt32((sender as Button).Tag) == 1)//20201117新增：启动打印按钮的初始Tag为1，标志动作为：开启打印
            {
                打印区间确认 f = new 打印区间确认();//20200224修改:
                DialogResult result = f.ShowDialog();
                if (result == DialogResult.OK)//OK时，执行对应操作
                {
                    CaptureLayerRangeFromUI("PrintBtn_Click");
                    string msg = "启动打印任务：准备开启数据处理及打印线程";
                    Log4Net.Info(msg);
                    Log4Net.Info($"启动打印任务：层范围确认，LayerStart.Text={this.LayerStart.Text}，LayerEnd.Text={this.LayerEnd.Text}，g_nLayerStart={g_nLayerStart}，g_nLayerEnd={g_nLayerEnd}，g_nRePrintTimes={g_nRePrintTimes}");

                    TaskAddDeleteTHREAD("CreateFirst");
                }
                else if (result == DialogResult.Cancel)//20200224：退出时，什么都不做
                {
                    return;
                }
                else
                {
                    return;
                }
            }
            else if (Convert.ToInt32((sender as Button).Tag) == 2)//20201117新增：启动打印按钮的初始Tag不为1:，标志动作为：关闭打印
            {
                bool nRetVal3 = MeteorPrintEngine.SetFlash(false);//20230331新建：手动关闭打印任务时，首先关闭闪喷状态
                string msg = $"关闭闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal3}}}";
                Log4Net.Info(msg);

                /*string*/
                msg = "删除打印任务：准备删除数据处理及打印线程";
                Log4Net.Info(msg);

                TaskAddDeleteTHREAD("DeleteMust");
                bool nRetVal = royal.royal.DEV_EnableUVPosCtlOut(false, false);//（0）设置UV等使能开启//20210623补充：添加关闭UV灯的指令
            }
            else { }
#if false
            ////            if (JOBStartFlag == false)//没有在加工//补充代码：继续加工
            ////            {
            ////                if (!m_bPrinting)//（2）开启打印线程:20200411添加批注（本处为精华）//根据标志位，判断是否可以开始打印
            ////                {   
            ////                    string tempThreadName = "PrintTaskTHREAD";
            ////                    Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
            ////                    if (tempThread != null)
            ////                    {
            ////                        PrinterLogicThreads.Remove(tempThread);//以防万一
            ////                    }
            ////                    else
            ////                    {
            ////                        this.LayerEnd.Enabled = false;//关闭控件操作
            ////                        this.LayerStart.Enabled = false;//关闭控件操作

            ////                        RoyalMap.m_bJobStarted = true; 
            ////                        returnPrintValue = 0;//20200508：复位打印进度值//(2) 开启打印线程:20200411新增
            ////                        StopPrintFlag = false;//20200508：复位打印进度值
            ////                        ThreadStart initThreadEntry = new ThreadStart(PrintTaskTHREAD);//20200220:线程入口方法修改为联动线程
            ////                        tempThread = new Thread(initThreadEntry) { IsBackground = true };
            ////                        tempThread.Name = tempThreadName;
            ////                        tempThread.Start();
            ////                        PrinterLogicThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
            ////                    }
            ////                }       
            ////                this.PrintBtn.Text = "关闭" + "\n" + "打印";//修改按钮状态为：关闭打印
            ////                this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
            ////                this.PrintBtn.BackgroundImage = Resource.StopJob_38x38;
            ////                JOBStartFlag = true;//玩的都是标志位：20200126
            ////            }
            ////            else//正在加工
            ////            {              
            ////                UInt32 ny2pos = 0;//(1-1)缓冲主运动：判断你是否到达缓冲区边缘位置：确保运动到位
            ////                bool Directory = false;uint nRevPls=0;
            ////                while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls))//int SleepTime = (int)((double)(AimPos - CurrentPos) / nSpeed);//Thread.Sleep(5000);//Sleep时间必须要有依据//确保运行到位，运行精度为2UM
            ////                {
            ////                    Thread.Sleep(20);
            ////                }
            ////                bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108    
            ////                PrintFlag = false;//20200716新增：关闭打印机维护的间歇闪喷使能//(1-2)关闭打印线程（联调线程）（2）关闭多轴运动：保障运动安全：20200411批注
            ////                string tempThreadName = "PrintTaskTHREAD";//(闭联调线程
            ////                DeleteThread(tempThreadName);
            ////                LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();//（2）关闭多轴运动：保障运动安全
            ////                AutomoveComponent.StopMove();
            ////                g_nCurrentLayer = 0;//20201021新增：当前打印的层数

            ////#if true//20200618批注：
            ////                tempThreadName = "DataTaskTHREAD";//(1)关闭联调线程//(2) 关闭传数据线程：20200411新增
            ////                DeleteThread(tempThreadName);
            ////                this.PicComposeBtn.Text = "处理" + "\n" + "传送";//修改传送控制按钮为：处理传送（初始状态）//修改按钮状态为：暂停输出
            ////                FinalJOBThreadExistedFlag = false;//20200415批注：个人感觉可以去掉，使用后台辅助工作的话
            ////                TransferModifyFlag = "StartFlag";//传送取消标志位
            ////#else
            ////                StartDataTransControlThread(false, 0, 50);//(1)关闭联调线程
            ////#endif
            ////                //(3) Close the JOB print JOB completely：彻底关闭JOB打印任务:20200514 new created//(0-1) Start the Printing Configuration: SET the print file infomation and confirm the print file formatt
            ////                bool returnFlag = StartCloseJOB(false);
            ////                returnPrintValue = 0;//20200508：复位打印进度值
            ////                JOBStartFlag = false;

            ////                this.LayerEnd.Enabled = true;//关闭控件操作//修改按钮状态为：可操作打印的起始层及终止层数；20200411新增
            ////                this.LayerStart.Enabled = true;//关闭控件操作
            ////                this.PrintBtn.Text = "启动" + "\n" + "打印";//修改按钮状态为：启动打印
            ////                this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
            ////                this.PrintBtn.BackgroundImage = Resource.Continue_38x38;
            ////            }
#endif
        }

        //20200220：线程管理的案发现场，只要是相应的线程我就存储在这里，不管线程是死是活，祖祖辈辈就在这里，便于维护及管理
        private List<Thread> PrinterLogicThreads = new List<Thread>();//20200220:存放所有必要的打印机的工作线程
        public void DeleteThread(string ThreadName)
        {
            string tempThreadName = ThreadName;
            Thread tempThread = PrinterLogicThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
            if (tempThread != null)
            {
                tempThread.Abort();//20200221修改:当调用非托管线程时，有时会抛出异常但不一定及时停止
                while (tempThread.ThreadState != System.Threading.ThreadState.Aborted)
                { Thread.Sleep(100); }
                PrinterLogicThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG

                string msg = "关闭打印线程成功：" + tempThreadName;
                Log4Net.Info(msg);
            }
        }

        string PrintConrolFlag = "StartPrint";//20200618新增：打印标志位
        private const int PassItemTimeoutMs = 5000;//20260327新增：防止TryGetPassItem长时间阻塞导致打印线程无法收尾

        private bool TryGetPassItemWithTimeout(uint layerIndex, int passId, ref LPPassDataItem passDes, int timeoutMs, string sourceTag)
        {
            bool returnFlag = false;
            Exception workerException = null;
            LPPassDataItem localPassDes = passDes;
            Thread worker = new Thread(() =>
            {
                try
                {
                    returnFlag = MeteorPrintEngine.TryGetPassItem(layerIndex, passId, ref localPassDes);
                }
                catch (Exception ex)
                {
                    workerException = ex;
                }
            });
            worker.IsBackground = true;
            worker.Name = $"TryGetPassItemWorker_{layerIndex}_{passId}";

            Log4Net.Info($"{sourceTag}: TryGetPassItem 超时保护启动，layer={layerIndex}，pass={passId}，timeoutMs={timeoutMs}");
            var watch = Stopwatch.StartNew();
            worker.Start();
            bool finished = worker.Join(timeoutMs);
            watch.Stop();

            if (!finished)
            {
                Log4Net.Info($"{sourceTag}: TryGetPassItem 超时，layer={layerIndex}，pass={passId}，elapsedMs={watch.ElapsedMilliseconds}，workerState={worker.ThreadState}");
                return false;
            }

            if (workerException != null)
            {
                Log4Net.Info($"{sourceTag}: TryGetPassItem 线程异常，layer={layerIndex}，pass={passId}，elapsedMs={watch.ElapsedMilliseconds}，ex={workerException.GetType().FullName}，msg={workerException.Message}");
                throw new Exception($"{sourceTag}: TryGetPassItem 线程异常", workerException);
            }

            passDes = localPassDes;
            Log4Net.Info($"{sourceTag}: TryGetPassItem 超时保护结束，layer={layerIndex}，pass={passId}，returnFlag={returnFlag}，nProcState={passDes.nProcState}，elapsedMs={watch.ElapsedMilliseconds}");
            return returnFlag;
        }

        private void PauseBtn_Click(object sender, EventArgs e)//暂停自动打印过程
        {
            if ((g_TaskThreadSTATE[4] == 2) || (g_TaskThreadSTATE[4] == 2))
            {
                if (Convert.ToInt32(Convert.ToInt32((sender as Button).Tag)) == 1)//20201117新增：暂停按钮的初始Tag为1，标志动作为：暂停动作
                {
                    (sender as Button).Tag = 2;
                    if (PrintConrolFlag == "StartPrint" || PrintConrolFlag == "KeepPrint")//没有在加工
                    {
                        this.PauseBtn.Text = "继续" + "\n" + "打印"; //补充代码：继续加工 //修改按钮状态为：停止加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Stop_绿_38x38;
                        PrintConrolFlag = "PausePrint";//20200618批注：继续打印

                        string msg = "发送暂停打印指令：{PausePrint}";
                        Log4Net.Info(msg);
                    }
                    this.LayerStart.BackColor = Color.LightBlue;//可修改打印区间
                    this.LayerEnd.BackColor = Color.LightBlue;//可修改打印区间

                    this.LayerStart.Text = (g_nCurrentLayer + 1).ToString();//20201118新增：暂停之后，自动更新起始层为当前打印层
                    this.LayerEnd.Enabled = true;//可修改打印区间
                    this.LayerStart.Enabled = true;//可修改打印区间
                }
                else if (Convert.ToInt32((sender as Button).Tag) == 2)//20201117新增：暂停按钮的初始Tag为2，标志动作为：继续动作
                {
                    继续打印 f = new 继续打印();//20201112新增:继续打印
                    DialogResult result = f.ShowDialog();
                    if (result == DialogResult.OK)//常规续打
                    {
                        this.PauseBtn.Text = "暂停" + "\n" + "打印";//补充代码：暂停加工 //修改按钮状态为：继续加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Keep_38x38;
                        PrintConrolFlag = "KeepPrint";//20200618批注：暂停打印

                        string msg = "发送继续打印指令：{KeepPrint}";
                        Log4Net.Info(msg);

                        this.LayerStart.BackColor = Color.MintCream;//可修改打印区间
                        this.LayerEnd.BackColor = Color.MintCream;//可修改打印区间
                        this.LayerEnd.Enabled = false;//可修改打印区间
                        this.LayerStart.Enabled = false;//可修改打印区间

                        (sender as Button).Tag = 1;
                    }
                    else if (result == DialogResult.Retry)//修订续打//从第N层重新发送数据，然后开启打印：20201118新增：
                    {
#if false
                        TaskAddDeleteTHREAD("CreateAgain");
#endif
                    }
                    else
                    {
                        (sender as Button).Tag = 2;
                        this.PauseBtn.Text = "继续" + "\n" + "打印"; //补充代码：继续加工 //修改按钮状态为：停止加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Stop_绿_38x38;
                    }
                }
            }
            else { }
        }

        private void StartInterSpeedSpark()//间歇闪喷的入口函数：目前先实现到这一层次
        {
            string tempThreadName = "RunInterSparkThread"; //（2）开启打印线程:20200411添加批注（本处为精华）
            Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
            if (tempThread != null)
            {
                PrinterLogicThreads.Remove(tempThread);//以防万一
            }
            else//(2) 开启打印线程:20200411新增
            {
                returnPrintValue = 0;//20200508：复位打印进度值
                StopPrintFlag = false;//20200508：复位打印进度值
                ThreadStart initThreadEntry = new ThreadStart(InterSpeedSpark);//20200220:线程入口方法修改为联动线程
                tempThread = new Thread(initThreadEntry) { IsBackground = true };
                tempThread.Name = tempThreadName;
                tempThread.Start();
                PrinterLogicThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
            }
        }

        bool PrintFlag = false;//打印加工状态：20200630批注
        double g_InterSpeedSparkValidTime = 1;
        double g_InterSpeedSparkCycleTime = 20;
        /// <summary>
        /// 待机闪喷维护：20200630批注
        /// </summary>
        private void InterSpeedSpark()//初步先这么写吧
        {
            //开启闪喷线程
            while (true)//不停的循环:处于永不停歇状态
            {
                if (PrintFlag == false)//处于待机状态
                {
                    bool nRetVal = MeteorPrintEngine.SetFlash(true);//打开闪喷
                    Thread.Sleep((int)g_InterSpeedSparkValidTime * 1000);//闪喷持续周期:本人设置为1s时间
                    nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷
                    Thread.Sleep((int)(g_InterSpeedSparkCycleTime - g_InterSpeedSparkValidTime) * 1000);//闪喷间歇周期：本人设置20s时间
                }
                else//处于打印状态
                {
                    Thread.Sleep(3000);//等待3000ms,再次判断是否停止打印
                }
            }
        }
        /****************************************************喷墨打印框架开始****************************************************/
        /****************************************************喷墨打印框架开始****************************************************/
        /****************************************************喷墨打印框架开始****************************************************/
        /*****************************************主控软件的属性设置及统一刷新*********************************************/
        /*****************************************主控软件的属性设置及统一刷新*********************************************/
        /*****************************************主控软件的属性设置及统一刷新*********************************************/
        LaserADD_BinderJetter.PowderLayerParam p = new LaserADD_BinderJetter.PowderLayerParam();//铺粉参数，现在是没有实际意义的。需要之后完善。
        public PrintStrategys g_PrintStrategys = new PrintStrategys();//总策略：包含所有的策略：20200807
        //public PrintNozzleHeadConfigure g_PrintNozzleHeadConfigure = new PrintNozzleHeadConfigure();//20210304新增：喷头校准参数


        单层铺粉控制模块 layerPowder = new 单层铺粉控制模块();
        public static RYSYSParam g_RYSYSParam = new RYSYSParam();//存储所有的的JOB参数//非常关键//20200327新建:
        private void JobsParaBtn_Click(object sender, EventArgs e)//JOBS参数设置
        {
            bool PrintJobOnFlag = false;//20230320新增：
            if (((g_TaskThreadSTATE[3] == 1) && (g_TaskThreadSTATE[4] == 1)) || ((g_TaskThreadSTATE[3] == 3) && (g_TaskThreadSTATE[4] == 3)))//20230320新增：
            {
                PrintJobOnFlag = false;//(1)未开启打印任务;(2)此前的打印任务已经结束
            }
            else
            {
                PrintJobOnFlag = true;//已开启打印任务
            }

            JOB参数设置 f = new JOB参数设置(p, PrintJobOnFlag);//20200202修改
            f.nValveStateMask = nValveStateMask;//20200718新增：原因在于，需要在JOB参数设置中打开手动控制，在此过程中，需要为手动控制传递阀状态参数
            f.k_RYSYSParam.m_bFlagResetCorrect = this.g_bResetCorrectEnabled;
            f.k_RYSYSParam = (RYSYSParam)g_RYSYSParam.Clone();//20200326新增//20200401新增：避免直接赋值形成的引用，形成真正的复制
            f.PrintStrategys = ObjectCopier.Clone(g_PrintStrategys);//20200806新增：保存打印策略

            string msg = $"进入JOB参数设置：修改前初始参数：灰度数据格式{{{g_RYSYSParam.m_nPixelGrayBits}bits}}" +
                $"打印灰阶{{{g_RYSYSParam.m_dPixelGrayValue}阶}}m_XPrintDpi{{{g_RYSYSParam.m_XPrintDpi}Dpi}}" +
            $"墨车运动速度{{{g_RYSYSParam.CarMoveSpeed}MM/s}}X向起打位置{{{g_RYSYSParam.m_dPrtXEncPos}MM}}" +
            $"X向起打位置偏移{{{g_RYSYSParam.m_dXJetOff}MM}}Y向起打位置偏移{{{g_RYSYSParam.m_dYJetOff}MM}}";//20230418修正：Y方向启打位置便宜，对于多PASS打印非常关键
            Log4Net.Info(msg);

            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
                g_bResetCorrectEnabled = f.k_RYSYSParam.FlagResetCorrect;// this.m_cGoogolMotionMap = f.m_mGoogolMotionMap;//回传数据                                
                g_RYSYSParam = (RYSYSParam)f.k_RYSYSParam.Clone();//20200326新增：关键：将RYSYSParam的值赋值给全局的static的RYSYSParam

                //20200326新增:关键：将RYSYSParam的JOB参数信息，进行及时的转发，转+给royal.sysParam
                //royal.g_sys_param为static类型：
                royal.royal.g_sys_param.fBrustCycleSec = (float)g_RYSYSParam.m_dInterSpeedSparkCycleTime /*/ 10*/;//时间1s//20230512修改：适应修复的正常的基准
                royal.royal.g_sys_param.fBrustValidSec = (float)g_RYSYSParam.m_dHSpeedSparkTime /*/ 10*/;//有效时间0.5s//20230512修改：适应修复的正常的基准
                royal.royal.g_sys_param.fBrustFrequecy = g_RYSYSParam.m_nHSpeedSparkFreq /** 10*/;//频率500Hz//20230327修改：底层的配置文件的闪喷的基准频率设置不准确，需要认为设置并扩展10倍//20230512修改：适应修复的正常的基准
                royal.royal.g_sys_param.szLogPath = g_RYSYSParam.m_sLogPath;
                //royal.royal.g_sys_param.szWavePath = g_RYSYSParam.m_sWavePath;

#if fasle//临时注释：进行相应的修改需要匹配合适的运动参数
                ////royal.royal.g_prtimg_layer.nImgStartJetIndex = (int)(g_RYSYSParam.m_dYJetOff / 25.4 * 600);//20210311新增：Y向起打喷嘴位置修订
                //////royal.royal.g_prtimg_layer.nYJetOff=(int)(g_RYSYSParam.m_dYJetOff/25.4*600);//20210311新增：Y向起打位置修订
#endif
#if false//20230327新建：反差值是否生效
                royal.royal.g_sys_param.nBiDirEncPrtOff = (int)(/*0*/g_RYSYSParam.m_dXJetOff * 200);//5um的精度//20230321修订：X方向打印往返差修订//20230327修正：此处存在潜在的问题//图层的整体偏移，可正可负
#endif

                //20200327新增:
                //float m_szMovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);
                //g_RYSYSParam.CarMoveSpeed = MM_TO_DOT(m_szMovSpeed, 5080);     
                g_nCarSinglePassLength = (int)((g_RYSYSParam.m_dCarMoveBufferLength + g_RYSYSParam.m_dPrintAeraLength + g_RYSYSParam.m_dCarMoveBufferLength2) * EncoderLinePerInch);//SinglePass运动距离：20200327新增：

                g_PrintStrategys = ObjectCopier.Clone(f.PrintStrategys);//20200806新增：保存打印策略

                //20210113新增：大零件分区处理算法
                g_SharpControl.gc_RysysParam = g_RYSYSParam;//20210113新增：大零件分区处理算法
                f.PrintStrategys.LocalRYSYSParam = g_RYSYSParam;//20230203新增：修复打印DPI等参数无法本地保存的问题
                //g_RYSYSParam.m_dSubAreaWidth;
                //g_RYSYSParam.m_dWeakAreaWidth;
                //g_RYSYSParam.m_dDeviation;
                f.SaveJsonFile();

                msg = $"退出JOB参数设置：修改后参数：灰度数据格式{{{g_RYSYSParam.m_nPixelGrayBits}bits}}" +
                    $"打印灰阶{{{g_RYSYSParam.m_dPixelGrayValue}阶}}m_XPrintDpi{{{g_RYSYSParam.m_XPrintDpi[0]},{g_RYSYSParam.m_XPrintDpi[1]},{g_RYSYSParam.m_XPrintDpi[2]},{g_RYSYSParam.m_XPrintDpi[3]},{g_RYSYSParam.m_XPrintDpi[4]},{g_RYSYSParam.m_XPrintDpi[5]},{g_RYSYSParam.m_XPrintDpi[6]},{g_RYSYSParam.m_XPrintDpi[7]},{g_RYSYSParam.m_XPrintDpi[8]},{g_RYSYSParam.m_XPrintDpi[9]}," +
                    $"{g_RYSYSParam.m_XPrintDpi[10]},{g_RYSYSParam.m_XPrintDpi[11]},{g_RYSYSParam.m_XPrintDpi[12]},{g_RYSYSParam.m_XPrintDpi[13]},{g_RYSYSParam.m_XPrintDpi[14]},{g_RYSYSParam.m_XPrintDpi[15]},{g_RYSYSParam.m_XPrintDpi[16]},{g_RYSYSParam.m_XPrintDpi[17]},{g_RYSYSParam.m_XPrintDpi[18]},{g_RYSYSParam.m_XPrintDpi[19]}," +
                    $"{g_RYSYSParam.m_XPrintDpi[20]},{g_RYSYSParam.m_XPrintDpi[21]},{g_RYSYSParam.m_XPrintDpi[22]},{g_RYSYSParam.m_XPrintDpi[23]},{g_RYSYSParam.m_XPrintDpi[24]},{g_RYSYSParam.m_XPrintDpi[25]},{g_RYSYSParam.m_XPrintDpi[26]},{g_RYSYSParam.m_XPrintDpi[27]},{g_RYSYSParam.m_XPrintDpi[28]}," +
                    $"{g_RYSYSParam.m_XPrintDpi[29]},{g_RYSYSParam.m_XPrintDpi[30]}Dpi}}" +
                    $"当前打印任务选定DPI{{{g_RYSYSParam.m_XPrintDpi[g_RYSYSParam.m_nXPrintDpiIndex]}}}" +
                    $"墨车运动速度{{{g_RYSYSParam.CarMoveSpeed}MM/s}}X向起打位置{{{g_RYSYSParam.m_dPrtXEncPos}MM}}" +
                    $"X向起打位置偏移{{{g_RYSYSParam.m_dXJetOff}MM}}Y向起打位置偏移{{{g_RYSYSParam.m_dYJetOff}MM}}";
                Log4Net.Info(msg);

                bool returnST = royal.royal.DEV_UpdateParam(ref royal.royal.g_sys_param);
                if (returnST == false)
                {
                    string sztxt;
                    sztxt = "更新设备参数失败";
                    MessageBox.Show(sztxt);
                }
            }
            else if (result == DialogResult.Cancel)//退出时，什么都不做
            {
                msg = $"退出JOB参数设置：未修改直接退出！";
                Log4Net.Info(msg);
            }
        }

        private UInt32 MM_TO_DOT(float X, int DPI)
        {
            //UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) * 2.50);//20200803修改
            double dot = X * DPI / 25.4 + 0.45;
            return (UInt32) dot;
        }

        /*****************************************主控软件的属性设置及统一刷新*********************************************/
        /*****************************************主控软件的属性设置及统一刷新*********************************************/
        /*****************************************主控软件的属性设置及统一刷新*********************************************/
        /********************************************
        4个轴————6个动作
        打印预运动处理：4轴电机的逻辑控制，
        成型缸电机下降x1mm（PP-）,1号送粉缸电机上升y1mm(PP+),铺粉电机正向回原点运动1次(JOG+)，
        成型缸电机下降x1mm（PP-）, 2号送粉缸电机上升y1mm(PP+),铺粉电机逆向回原点运动1次(JOG-).
        **********************************************/
        bool InkCarHomeFlag = false; bool PowderCarHomeFlag = false;
        /// <summary>
        /// （0）启打位置检测：（a）铺粉启打位置严格控制（避免撞机）+（b）墨车启打位置严格控制（避免撞机）
        /// </summary>
        private int CheckStartPosition(double InkCarPosition, double PowderCarPosition)//20200627新建：两参数分别为墨车和粉车的启打位置
        {
            //（1）墨车和铺粉车是否回零成功？？？，没有回零成功一直等待
            while ((InkCarHomeFlag == false) || (InkCarHomeFlag == false))//
            {
                Thread.Sleep(10);//等待1Oms
                //MessageBox.Show("启动打印中：系统未回零，请重新回零！！！");//更换为信息提示框进行提示：
                PrinterRunInfo("启动打印中：系统未回零，请重新回零！！！");//更换为信息提示框进行提示：
            }
            //（2）运动到指定区间位置
            double positonX = 0; //墨车位置

            LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();
            double[] k_dJourney1 = new double[8];
            double positionY = 0;//铺粉车位置

            do
            {
                positonX = (double)royal.royal.DEV_GetPrintEncoderValue() / EncoderLinePerMM; //墨车位置
                k_dJourney1 = AutomoveComponent.GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
                positionY = k_dJourney1[3] / 1000;//铺粉车位置
                if (positonX > InkCarPosition || positionY > PowderCarPosition)
                {
                    PrinterRunInfo("启动打印中：请调节墨车和铺粉车到启打区间（21MM,6MM）！！！");//更换为信息提示框进行提示：20+1MM,5+1MM
                    Thread.Sleep(100);//等待10ms
                }
                else
                {
                    return 1;
                }
            }
            while (true/*positonX > InkCarPosition || positionY > PowderCarPosition*/);

            //return 1;//返回值指令：1为顺利启动进入，打印阶段
        }
        private int g_nCarSinglePassLength = 0;//SinglePass运动距离：20200327新增：
        uint g_nRevPls = 0;//读回的剩余脉冲值//20200328新增：
        bool g_bDir = false;//读回的运动方向//20200328新增：
        private int SelectCurrentLayer(int index, APrintStategy aPrintStategy)//20200812批注：
        {

            int tempEndLayer = 0; int tempStartLayer = 0;
            for (int i = 0; i < aPrintStategy.layerStategyContents.Count(); i++)
            {
                if (aPrintStategy.layerStategyContents[i].b_LayerEnd == -1)
                {
                    tempEndLayer = 100000;//极限层设置为10W层。
                    tempStartLayer = aPrintStategy.layerStategyContents[i].b_LayerIndex;
                }
                else
                {
                    tempEndLayer = aPrintStategy.layerStategyContents[i].b_LayerEnd;//极限层设置为10W层。
                    tempStartLayer = aPrintStategy.layerStategyContents[i].b_LayerIndex;
                }
                if ((tempStartLayer <= index) && (tempEndLayer >= index))//在区间
                {
                    return i;//返回找到的参数索引
                }
            }
            return -2;//输入层存在问题。
        }
        //(1)运动模式动态挂载切换响应：
        GoogolMotionMap motionMap = null;//创建GoogolMotionMap对象，供本窗口调用
        public int g_nCurrentLayer = 0;//20201021新增：当前打印的层数
        public int g_nCleanFrequency = 10;//20220915新增：清洗频率全局变量
        int m_nPauseMovedFlag = 0;//20230410新增：暂停打印时回手动清洗站工作位
        private int g_nCurrentPrintLayerID = 0;//20230420新增：
        private void PrintTaskTHREAD()//3DP打印主流程：
        {
            bool ReturnFlag = false;

            g_TaskThreadSTATE[4] = 2;//20201119新增：DataTaskThread恢复为运行状态（关机后）
            ReadLayerInfo();//更新指定的加工任务区间
            Log4Net.Info($"打印线程：ReadLayerInfo完成，g_bSelectedLayerRangeReady={g_bSelectedLayerRangeReady}，g_nLayerStart={g_nLayerStart}，g_nLayerEnd={g_nLayerEnd}，g_nRePrintTimes={g_nRePrintTimes}");
            motionMap = CreateMotionMap();
            g_PrintSchedule = g_nLayerStart;//20201118新增：
            while ((g_PrintSchedule == -1) || g_PrintSchedule == g_nLayerStart)//20201118新增：处于初始态或者已经传输1层数据
            {
                Thread.Sleep(100);//数据传送进度需要领先起始打印层至少2层
            }
            PrintFlag = true;//20200716新增：关闭打印机维护的间歇闪喷使能
            ConfigureJetEnvironmentControlMode();//20200602修改:初始化喷墨系统环境控制，具体包括：下发自动供墨指令、下发设置自动负压指令、下发二级墨盒的温度设置指令、设置墨水搅拌周期指令                                           
            InitCarMotor();//20200327新增：//（1）初始化被控对象及加工任务区间

            float m_szMovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增//确定准确的墨车运动速度值
            uint m_unCarMoveSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch);//20200328新增
            g_nCarSinglePassLength = (int)((g_RYSYSParam.m_dCarMoveBufferLength + g_RYSYSParam.m_dPrintAeraLength + g_RYSYSParam.m_dCarMoveBufferLength2) * EncoderLinePerInch);//SinglePass运动距离：20200327新增：

            //royal.LPPrtRunInfo RTinfo = new LPPrtRunInfo();////（2-2）20200411新增（精华）：正式的打印处理框架：//20230213注释
            LPPassDataItem pPrtPassDes = new LPPassDataItem();//20200411:此处存在比较严重的问题            
            int size2 = Marshal.SizeOf(pPrtPassDes)/* * pPrtPassDes.Length*/;//20200427新增：
            IntPtr ImgPtr = Marshal.AllocHGlobal(size2);//20200429批注：类似于C++的NEW的操作
            Marshal.StructureToPtr(pPrtPassDes, ImgPtr, false);

            int LayerEndNum = g_nLayerEnd;//20200601修改：打印的终止层数，放在此处，仅仅是方便测试而已

            int CurrentStartPrintLayer = g_nLayerStart - g_nLayerStart;//20201121新增：//20201124修改：初始值永远为0
            /*************************************************************************************************/
            /*************************************************************************************************/
#if true//20220524批注：准备操作（打印机准备开启）

            SendMessageToCamera sendMessageToCamera = null;
            if (DisableRoyalPrintRuntimeInit)
            {
                Log4Net.Info("Meteor print mode: skip PrintTaskTHREAD royal axis/ink initialization block.");
                goto PrintTaskTHREADRoyalInitSkip;
            }
            //RoyalMap.OpenUV(false, g_UVLightParam);//20200628批注：提高性能//20200619批注：每1层的UV灯参数完全可调//20201013修改：打印过程不打开UV灯//201030注释掉：UV灯使能不在此处开启。在自动打印逻辑中应用
            bool nRetVal0 = royal.royal.DEM_InitAxis(0, 0x100/*0x100*/);//分别初始化各轴的运动参数：20200305//加速度：256pluse/ms^2
            string msg = $"初始化墨轴1：DEM_InitAxis：{{0, 0x100}}";
            Log4Net.Info(msg);

            if (DisableRoyalPrintRuntimeInit)
            {
                Log4Net.Info("Meteor print mode: skip PrintTaskTHREAD royal axis/ink initialization block.");
                goto PrintTaskTHREADRoyalInitSkip;
            }
            nRetVal0 = royal.royal.DEM_EnableAxisRun(true);//所有的轴共用1个使能，使能一次就OK!:20200305     
            msg = $"使能所有墨轴：DEM_EnableAxisRun：{{true}}";
            Log4Net.Info(msg);

            bool nRetVal = royal.royal.DEV_EnableInkAutoSupply(true, 0xFF);//使能自动供墨uint ControlBit = 0xFF;//20201114修改：使能第4路墨水的自动供墨
            msg = $"开启自动供墨：DEV_EnableInkAutoSupply：ControlBit{{0xFF}}";
            Log4Net.Info(msg);

            //////(3)读取气压值：
            ////uint nIoOption = 0;
            ////nIoOption |= 0x4;//nIoOption = 0d1111;//只读取负压值
            ////LPADIB_PARAM adibCurState = new LPADIB_PARAM();
            ////bool m_bSetEnable = true;
            ////bool m_bComState = royal.royal.DEV_AdibControl(ref adibCurState, nIoOption, false, ref m_bSetEnable);//bSetParam位的作用为0：状态为读状态：20200329批注
            //////adibCurState.fcurvoltage[0].ToString("F2");
            //////(4）刷新记录：
            ////msg = $"【RecordPressure】: {{1}}号喷头打印前状态：系统负压为{{{ adibCurState.fcurAirPress[0].ToString("F2")}kPa}}；";
            ////Log4Net.Info(msg);

#endif
#region 监控发送指令//20230113新建且批注：
            sendMessageToCamera = new SendMessageToCamera(false);//20200202修改
                                                                                     //sendMessageToCamera.LoadJsonFile();
                                                                                     //sendMessageToCamera.SendMessageFromSharedMemory(tempStartMode,10,13);//20230113新建且批注：监控发送指令
                                                                                     //sendMessageToCamera.Dispose();//20230113新建且批注：监控发送指令
#endregion
        PrintTaskTHREADRoyalInitSkip:
            Log4Net.Info($"打印线程：进入主循环前，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}，CurrentStartPrintLayer={CurrentStartPrintLayer}");
            while ((RoyalMap.m_bJobStarted == true))//开启打印处理线程：20200411新建
            {
                returnPrintValue = (CurrentStartPrintLayer + 1) * g_nRePrintTimes;//20200508：复位打印进度值   
                UpdateCircularBarMethod(2);//20201121新增：开启打印进度更新

                for (int k = CurrentStartPrintLayer + 1/*(CurrentStartPrintLayer+1) * g_nRePrintTimes*//* + 1*/ ;
                    k <= (LayerEndNum - g_nLayerStart + 1) * g_nRePrintTimes/*(LayerEndNum- g_nLayerStart + 1) * g_nRePrintTimes*/; k++)//核心代码//20200411新建：k为打印层数的Index
                {
                    Log4Net.Info($"打印线程：准备推进当前打印层号，k={k}，上一层号={g_nCurrentPrintLayerID}，CurrentStartPrintLayer={CurrentStartPrintLayer}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}");
                    g_nCurrentPrintLayerID = k;//20230420新增：
                    Log4Net.Info($"打印线程：已推进当前打印层号，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，k={k}，CurrentStartPrintLayer={CurrentStartPrintLayer}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}");
                    if (PrintConrolFlag == "StartPrint" || PrintConrolFlag == "KeepPrint")//每次打印之前，都需要执行指令判断
                    {
                        if (PrintConrolFlag == "StartPrint")//20230213新增
                        {
                            msg = "接收到起始打印指令：PrintTaskTHREAD";
                            Log4Net.Info(msg);
                        }
                        else if (PrintConrolFlag == "KeepPrint")
                        {
                            msg = "接收到恢复打印指令：PrintTaskTHREAD";
                            Log4Net.Info(msg);
                        }
                        else { }

#if true//20220524批注：刷新进度控件
                        int renderIndex = ((k - 1) / g_nRePrintTimes) + g_nLayerStart /*k*/;
                        Rendering2D(renderIndex); //20200601：实现成形层的逐层预览刷新//201030修改：
                        CurrentStartPrintLayer = k;//启打层
                        Log4Net.Info($"打印线程：已更新CurrentStartPrintLayer={CurrentStartPrintLayer}，当前打印层号={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}");

                        g_SharpControl.g_CorrectionFigureFlag = 0;//临时显示是1；//20230409修复：恢复到非校准图显示状态
#endif

                        /***********************************20200508:实际打印过程：*********************************/
                        /***********************************20200508:实际打印过程：*********************************/
                        //20230402修改：（1）自动清洗运动（2）自动喷墨运动（3）自动进给送粉（4）自动固化运动

                        ///20220915新增：加入自动清洗逻辑，判断是否需要
                        ///20220915新增：读取清洗频率参数，判断是否需要
#if false
                        float m_MovSpeed2 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                        if (AutoPrintMotion1 == null)
                        {
                            /*手动操作*/
                            AutoPrintMotion1 = new 手动操作(0, nValveStateMask);//20201030新增：读取自动打印参数//20230317修正:修正潜在的闪退问题
                            msg = $"创建：AutoPrintMotion1=》初次创建完成-手动操作！";
                            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                        }
                        else
                        {
                            msg = $"创建：AutoPrintMotion1=》不需重新创建-手动操作！";
                            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                        }

                        bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion1);//20201030新增：读取自动打印参数
                        g_nCleanFrequency = AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency;//20201030新增：读取自动打印参数
                        if (AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nStartRePrintClean == 1)//每次打印都重喷
                        {
                            if ((g_nCurrentLayer % (g_nCleanFrequency * 1) == 0) && (g_nCurrentLayer != 0))
                            {
                                EquipmentMotionLogic3(0, 1, 0, m_MovSpeed2, ref sendMessageToCamera, 0, 0);//20220915新增：加入自动清洗逻辑
                            }
                        }
                        else//不需要每次都清洗，重喷一次清洗一次
                        {
                            if ((g_nCurrentLayer % (g_nCleanFrequency * g_nRePrintTimes) == 0) && (g_nCurrentLayer != 0))
                            {
                                EquipmentMotionLogic3(0, 1, 0, m_MovSpeed2, ref sendMessageToCamera, 0, 0);//20220915新增：加入自动清洗逻辑
                            }
                        }
                        ///20220915新增：结束读取清洗频率参数
#else
                        //float m_MovSpeed2 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                        //EquipmentMotionLogic3(0, 1, 0, m_MovSpeed2, ref sendMessageToCamera, 0, 0);//20220915新增：加入自动清洗逻辑
#endif
                        ///20230402批注：加入喷墨打印逻辑
                        ///20230402批注：加入喷墨打印逻辑
                        int PassItems = 0;//20220524新增：
#region 监控指令：喷墨拍摄位点1
                        if (DisableMonitoringRuntimeInit || sendMessageToCamera == null)
                        {
                            Log4Net.Info($"监控配置：完全跳过 sendMessageToCamera 相关逻辑，layer={renderIndex}, pass={PassItems}，DisableMonitoringRuntimeInit={DisableMonitoringRuntimeInit}，sendMessageToCameraNull={(sendMessageToCamera == null)}");
                        }
                        else
                        {
                            Log4Net.Info($"监控配置：准备加载 sendMessageToCamera.LoadJsonFile，layer={renderIndex}, pass={PassItems}");
                            sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                            Log4Net.Info($"监控配置：sendMessageToCamera.LoadJsonFile 完成，layer={renderIndex}, pass={PassItems}");
                            if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[PassItems])
                            {
                                Log4Net.Info($"监控配置：准备发送监控消息，layer={renderIndex}, pass={PassItems}");
                                sendMessageToCamera.SendMessageFromSharedMemory(false, renderIndex, PassItems + 1);//20230113新建且批注：监控发送指令
                                Log4Net.Info($"监控配置：发送监控消息完成，layer={renderIndex}, pass={PassItems}");
                            }
                        }
                        #endregion
                        
                        int PassNum = 6;
#if SinglePassPrintMode
                        PassNum = 6;
#endif
#if TwoPassPrintMode
#if TwoPassPrintPerSixTimes
                        PassNum = 12;
#endif
#if TwoPassPrintPerThreeTimes
                        PassNum = 6;
#endif
#endif

                        for (PassItems = 0; PassItems < PassNum/*6*//*7*/; PassItems++)//20220531修改：总共数量为6 PASS
                        {
                            /*****************（1）20220524批注：确保获取打印PASS信息*********************/
                            int nPassID = PassItems/*0*//*1*//*0*/;//20200424新增：测试结果表明1是错误的，无法顺利执行//20220524新增：修改为多PASS打印
                            /*bool*/
                            Log4Net.Info($"打印线程：准备调用 TryGetPassItem，nLayerIndex={k}，nPassID={nPassID}，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}");
                            ReturnFlag = TryGetPassItemWithTimeout((uint)k, nPassID, ref pPrtPassDes, PassItemTimeoutMs, "打印线程");
                            Log4Net.Info($"打印线程：TryGetPassItem 返回确认，nLayerIndex={k}，nPassID={nPassID}，ReturnFlag={ReturnFlag}，nProcState={pPrtPassDes.nProcState}");
                            if (ReturnFlag == false)
                            {
                                Log4Net.Info($"打印线程：TryGetPassItem 未返回有效数据，准备中止当前打印任务，nLayerIndex={k}，nPassID={nPassID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}");
                                PrintConrolFlag = "StopPrint";
                                break;
                            }
                            while (pPrtPassDes.nProcState != 3)//20200624批注：不成功就重新读
                            {
                                Log4Net.Info($"打印线程：准备重试 TryGetPassItem，nLayerIndex={k}，nPassID={nPassID}，当前ProcState={pPrtPassDes.nProcState}");
                                Thread.Sleep(100);//等待1s时间，再次GetPassItem;
                                ReturnFlag = TryGetPassItemWithTimeout((uint)k, nPassID, ref pPrtPassDes, PassItemTimeoutMs, "打印线程");
                                Log4Net.Info($"打印线程：TryGetPassItem 重试返回确认，nLayerIndex={k}，nPassID={nPassID}，ReturnFlag={ReturnFlag}，nProcState={pPrtPassDes.nProcState}");
                            }
                            /*****************（2）20220524批注：执行打印PASS运动逻辑*********************/
                            if (ReturnFlag == true/*pPrtPassDes!=null*/)//20200411:读到的数据不为空//20200430开启运动：
                            {
                                Log4Net.Info($"打印线程：准备调用 TriggerPass，nLayerIndex={k}，nPassID={nPassID}，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}");
                                var triggerWatch = System.Diagnostics.Stopwatch.StartNew();
                                bool returnCode2 = MeteorPrintEngine.TriggerPass((uint)k, nPassID /*-1*/);
                                triggerWatch.Stop();
                                Log4Net.Info($"打印线程：TriggerPass 返回，nLayerIndex={k}，nPassID={nPassID}，returnCode2={returnCode2}，elapsedMs={triggerWatch.ElapsedMilliseconds}");
                                if (returnCode2 == false)
                                {
                                    msg = $"使能Pass打印失败：IDP_DoPassPrint2：nLayerIndex{{{k}}}nPassID{{{nPassID}}}";
                                    Log4Net.Info(msg);

                                    MessageBox.Show("启动打印失败！LayerIndex=" + pPrtPassDes.nLayerIndex + ",nProcState=" + pPrtPassDes.nProcState + "；打印分频值=" + pPrtPassDes.nPrtPrecession + "有效列数" + pPrtPassDes.nValidPrtCols);//20220531修改：
                                }
                                else
                                {
                                    msg = $"使能Pass打印成功：IDP_DoPassPrint2：nLayerIndex{{{k}}}nPassID{{{nPassID}}}";
                                    Log4Net.Info(msg);

                                    ////royal.LPPRINTER_INFO pSysInfo = new royal.LPPRINTER_INFO();//20230213新增：
                                    ////bool nRetVal2 = royal.royal.DEV_GetDeviceInfo2(ref pSysInfo);//20230213新增：
                                    ////msg = $"PASS打印前关键状态：DEV_GetDeviceInfo2：nLayerIndex{{{k}}}nPassID{{{nPassID}}}" +
                                    ////    $"LPPRINTER_INFO:nXSysEncDPI{{{pSysInfo.nXSysEncDPI}}}nStatus{{{pSysInfo.nStatus}}}nPrintStatus{{{pSysInfo.nPrintStatus}}}bSuperDevice{{{pSysInfo.bSuperDevice}}}\r\n" +

                                    ////    $"LPPRINTER_INFO-LPPrtRunInfo:bJobPrtRuning{{{pSysInfo.prt_rtinfo.bJobPrtRuning}}}bLayerPrtIsOver{{{pSysInfo.prt_rtinfo.bLayerPrtIsOver}}}" +
                                    ////    $"nContReqMemErr{{{pSysInfo.prt_rtinfo.nContReqMemErr}}}nContWDErr{{{pSysInfo.prt_rtinfo.nContWDErr}}}" +
                                    ////    $"nCurPrtDir{{{pSysInfo.prt_rtinfo.nCurPrtDir}}}nDTLayerIndex{{{pSysInfo.prt_rtinfo.nDTLayerIndex}}}" +
                                    ////    $"nDTLayerPassIndex{{{pSysInfo.prt_rtinfo.nDTLayerPassIndex}}}nDTPtrCtlIndex{{{pSysInfo.prt_rtinfo.nDTPtrCtlIndex}}}" +
                                    ////    $"nLayerPassCount{{{pSysInfo.prt_rtinfo.nLayerPassCount}}}nPrintLayerIndex{{{pSysInfo.prt_rtinfo.nPrintLayerIndex}}}" +
                                    ////    $"nPrintPassIndex{{{pSysInfo.prt_rtinfo.nPrintPassIndex}}}nProcLayerIndex{{{pSysInfo.prt_rtinfo.nProcLayerIndex}}}" +
                                    ////    $"nPrtDataMemAddr{{{pSysInfo.prt_rtinfo.nPrtDataMemAddr}}}nPrtState{{{pSysInfo.prt_rtinfo.nPrtState}}}" +
                                    ////    $"nReverse{{{pSysInfo.prt_rtinfo.nReverse}}}nRevPrtCols{{{pSysInfo.prt_rtinfo.nRevPrtCols}}}\r\n" +

                                    ////    $"LPPRINTER_INFO-LPDRVINFO:nFMVersion{{{pSysInfo.sysDrvInfo[0].nFMVersion}}}nFpgaVersion{{{pSysInfo.sysDrvInfo[0].nFpgaVersion}}}" +
                                    ////    $"nPCBVersion{{{pSysInfo.sysDrvInfo[0].nPCBVersion}}}" +
                                    ////    $"nState{{{pSysInfo.sysDrvInfo[0].nState}}}nNextState{{{pSysInfo.sysDrvInfo[0].nNextState}}}" +
                                    ////    $"nPtvwarnState{{{pSysInfo.sysDrvInfo[0].nPtvwarnState}}}nCrc32{{{pSysInfo.sysDrvInfo[0].nCrc32}}}" +
                                    ////    $"nRevInfo{{{pSysInfo.sysDrvInfo[0].nRevInfo}}}nSignature{{{pSysInfo.sysDrvInfo[0].nSignature}}}";
                                    ////Log4Net.Info(msg);

                                    royal.LPPRINTER_INFO g_printerInfoLocal = new royal.LPPRINTER_INFO();//20230213新增：
                                    IntPtr info = royal.royal.DEV_GetDeviceInfo();//——————调用API1(修改后的API1)
                                    g_printerInfoLocal = (LPPRINTER_INFO)Marshal.PtrToStructure(info, typeof(LPPRINTER_INFO));//调用API1获取的指针
                                    string msg3 = $"PASS打印前关键状态：DEV_GetDeviceInfo：nLayerIndex{{{k}}}nPassID{{{nPassID}}}" +
                                         $"LPPRINTER_INFO:nXSysEncDPI{{{g_printerInfoLocal.nXSysEncDPI}}}nStatus{{{g_printerInfoLocal.nStatus}}}nPrintStatus{{{g_printerInfoLocal.nPrintStatus}}}bSuperDevice{{{g_printerInfoLocal.bSuperDevice}}}\r\n" +
                                         $"LPPRINTER_INFO-LPPrtRunInfo:bJobPrtRuning{{{g_printerInfoLocal.prt_rtinfo.bJobPrtRuning}}}bLayerPrtIsOver{{{g_printerInfoLocal.prt_rtinfo.bLayerPrtIsOver}}}" +
                                         $"nContReqMemErr{{{g_printerInfoLocal.prt_rtinfo.nContReqMemErr}}}nContWDErr{{{g_printerInfoLocal.prt_rtinfo.nContWDErr}}}" +
                                         $"nCurPrtDir{{{g_printerInfoLocal.prt_rtinfo.nCurPrtDir}}}nDTLayerIndex{{{g_printerInfoLocal.prt_rtinfo.nDTLayerIndex}}}" +
                                         $"nDTLayerPassIndex{{{g_printerInfoLocal.prt_rtinfo.nDTLayerPassIndex}}}nDTPtrCtlIndex{{{g_printerInfoLocal.prt_rtinfo.nDTPtrCtlIndex}}}" +
                                         $"nLayerPassCount{{{g_printerInfoLocal.prt_rtinfo.nLayerPassCount}}}nPrintLayerIndex{{{g_printerInfoLocal.prt_rtinfo.nPrintLayerIndex}}}" +
                                         $"nPrintPassIndex{{{g_printerInfoLocal.prt_rtinfo.nPrintPassIndex}}}nProcLayerIndex{{{g_printerInfoLocal.prt_rtinfo.nProcLayerIndex}}}" +
                                         $"nPrtDataMemAddr{{{g_printerInfoLocal.prt_rtinfo.nPrtDataMemAddr}}}nPrtState{{{g_printerInfoLocal.prt_rtinfo.nPrtState}}}" +
                                         $"nReverse{{{g_printerInfoLocal.prt_rtinfo.nReverse}}}nRevPrtCols{{{g_printerInfoLocal.prt_rtinfo.nRevPrtCols}}}\r\n" +

                                         $"LPPRINTER_INFO-LPDRVINFO:nFMVersion{{{g_printerInfoLocal.sysDrvInfo[0].nFMVersion}}}nFpgaVersion{{{g_printerInfoLocal.sysDrvInfo[0].nFpgaVersion}}}" +
                                         $"nPCBVersion{{{g_printerInfoLocal.sysDrvInfo[0].nPCBVersion}}}" +
                                         $"nState{{{g_printerInfoLocal.sysDrvInfo[0].nState}}}nNextState{{{g_printerInfoLocal.sysDrvInfo[0].nNextState}}}" +
                                         $"nPtvwarnState{{{g_printerInfoLocal.sysDrvInfo[0].nPtvwarnState}}}nCrc32{{{g_printerInfoLocal.sysDrvInfo[0].nCrc32}}}" +
                                         $"nRevInfo{{{g_printerInfoLocal.sysDrvInfo[0].nRevInfo}}}nSignature{{{g_printerInfoLocal.sysDrvInfo[0].nSignature}}}";
                                    Log4Net.Info(msg3);

#if false//20220524批注：刷新进度控件
                                    double rate = (double)g_nLayerCurrent / (double)g_nLayerEnd * 100;//20200617批注
                                    string PintRate = rate.ToString("f1");//20200411新增：本处的显示，应该转移到状态栏的第3个lable                               
                                    this.PrinterStatusLabel.Text = "|| 打印中：当前打印第"
                                                                     + (((k - 1) / g_nRePrintTimes) + g_nLayerStart + 1).ToString() + "-"
                                                                     + (((k - 1) % g_nRePrintTimes) + 1).ToString()
                                                                         + "层，剩余" + ((LayerEndNum + 1) - (((k - 1) / g_nRePrintTimes) + g_nLayerStart + 1)).ToString() + "层。";//20200430新增：打印机状态栏//201030修改：
                                    returnPrintValue = k;//20200508新建：更新进度，更新进度到手动操作//UpdateCircularBarMethod(2);//20200508新建：开启打印进度更新
#endif

                                    //20220524批注：（2）自动喷墨运动

                                    #region
                                    //（1-1）注意：一定要取消跳白功能//（1-2）计算运动参数:运行速度、运行距离，依据SinglePass和MultiPass等运动模式*/
                                    #endregion
                                    //RecordPressureAndTemperatureAndValtageInPrint();//20230322新建：记录打印之前喷头温度及电压

                                    ///20220915新增：结束读取清洗频率参数
                                    ///
                                    if (AutoPrintMotion1 == null)
                                    {
                                        /*手动操作*/
                                        AutoPrintMotion1 = new 手动操作(0, nValveStateMask, false);//20201030新增：读取自动打印参数//20230317修正:修正潜在的闪退问题
                                        msg = $"创建：AutoPrintMotion1=》初次创建完成-手动操作！";
                                        Log4Net.Info(msg);
                                    }
                                    else
                                    {
                                        msg = $"创建：AutoPrintMotion1=》不需重新创建-手动操作！";
                                        Log4Net.Info(msg);
                                    }

                                    bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion1);//20201030新增：读取自动打印参数
                                    g_nCleanFrequency = AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency;//20201030新增：读取自动打印参数
                                    //if (AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nStartRePrintClean == 1)//每次打印都重喷清洗
                                    //{
                                    //    if ((g_nCurrentLayer % (g_nCleanFrequency * 1) == 0) /*&& (g_nCurrentLayer != 0)*/)
                                    //    {
                                    //        if (nPassID == 0)
                                    //        {
                                    //            float m_MovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                    //            float m_BackCleanMovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                                    //            EquipmentMotionLogic3(0, 1, 0, m_MovSpeed3, m_BackCleanMovSpeed3, ref sendMessageToCamera, 0, 0, 0);//20220915新增：加入自动清洗逻辑
                                    //        }
                                    //    }
                                    //}
                                    /*else {}*///不需要每次都清洗，重喷一次清洗一次
                                    if (AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nAutoPrintCleanEnabled == 1)
                                    {
                                        if (g_nCleanFrequency * g_nRePrintTimes == 1)
                                        {
                                            if (nPassID == 0)
                                            {
                                                float m_MovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                                float m_BackCleanMovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                                                EquipmentMotionLogic3(0, 1, 0, m_MovSpeed3, m_BackCleanMovSpeed3, ref sendMessageToCamera, 0, 0, 0, 0, 0, 0);//20220915新增：加入自动清洗逻辑
                                            }
                                        }
                                        else
                                        {
                                            if (k % (g_nCleanFrequency * g_nRePrintTimes) == 1)
                                            {
                                                if (nPassID == 0)
                                                {
                                                    float m_MovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                                    float m_BackCleanMovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                                                    EquipmentMotionLogic3(0, 1, 0, m_MovSpeed3, m_BackCleanMovSpeed3, ref sendMessageToCamera, 0, 0, 0, 0, 0, 0);//20220915新增：加入自动清洗逻辑
                                                }
                                            }
                                        }
                                    }

                                    bool DirFlag = pPrtPassDes.bPrtDir;//102023修改：打印方向
                                    float m_MovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                    float m_BackCleanMovSpeed = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                                    m_nPauseMovedFlag = 0;
                                    if (PrintConrolFlag == "PausePrint") { m_nPauseMovedFlag = 1; }
#if SinglePassPrintMode
                                    if (g_nRePrintTimes == 1)//20230418批注：重喷次数取值范围为：1-4
                                    {
                                        Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=4，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                        EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff/2,0);//自动喷墨运动逻辑
                                    }
                                    else if (g_nRePrintTimes == 2)
                                    {
                                        if (k % g_nRePrintTimes == 1) //20230418修改:第1PASS打印
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=4，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff/2,1);//自动喷墨运动逻辑 
                                        }
                                        else if (k % g_nRePrintTimes == 0)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=5，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff/2,0);//自动喷墨运动逻辑
                                        }
       
                                    }
                                    else if (g_nRePrintTimes == 3)
                                    {
                                        if (k % g_nRePrintTimes == 1) //20230418修改:第1PASS打印
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=4，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2,1);//自动喷墨运动逻辑 
                                        }
                                        else if (k % g_nRePrintTimes == 2)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=5，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2,1);//自动喷墨运动逻辑
                                        }
                                        else if (k % g_nRePrintTimes == 0)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=4，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2,0);//自动喷墨运动逻辑
                                        }
                                    }
                                    else if (g_nRePrintTimes == 4)
                                    {
                                        if (k % g_nRePrintTimes == 1) //20230418修改:第1PASS打印
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=4，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2,1);//自动喷墨运动逻辑 
                                        }
                                        else if (k % g_nRePrintTimes == 2)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=5，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2,1);//自动喷墨运动逻辑
                                        }
                                        else if (k % g_nRePrintTimes == 3)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=4，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2,1);//自动喷墨运动逻辑
                                        }
                                        else if (k % g_nRePrintTimes == 0)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=5，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2,0);//自动喷墨运动逻辑
                                        }
                                    }
#endif
#if TwoPassPrintMode
#if TwoPassPrintPerSixTimes
                                    if (g_nRePrintTimes == 1)//20230418批注：重喷次数取值范围为：1-4
                                    {
                                        Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=6(6次)，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                        EquipmentMotionLogic3(0, 6, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0);//自动喷墨运动逻辑
                                    }
#endif
#if TwoPassPrintPerThreeTimes
                                    if (g_nRePrintTimes == 1)//20230418批注：重喷次数取值范围为：1-4
                                    {
                                        ///*int*/k = index * RePrintTimes + subindex;
                                        if (k % 3 == 1 || k == 0)//15mm偏移量
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=6(3次/offset-15)，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 6, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0, 0);//自动喷墨运动逻辑
                                        }
                                        else if (k % 3 == 2)//10mm偏移量
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=6(3次/offset-10)，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 6, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, (g_RYSYSParam.m_dYJetOff - 5) / 2, 0, 0/*2.5*/);//自动喷墨运动逻辑
                                        }
                                        else if (k % 3 == 0 && k != 0)//5mm偏移量
                                        {
                                            Log4Net.Info($"打印主循环：准备发起喷墨运动，Command=6(3次/offset-5)，k={k}，nPassID={nPassID}，g_nRePrintTimes={g_nRePrintTimes}，PauseFlag={m_nPauseMovedFlag}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                                            EquipmentMotionLogic3(0, 6, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, (g_RYSYSParam.m_dYJetOff - 10) / 2, 0, 0/*5.0*/);//自动喷墨运动逻辑
                                        }
                                        //EquipmentMotionLogic3(0, 6, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0);//自动喷墨运动逻辑
                                    }
#endif
#endif
                                    CurrentStartPrintLayer = k;
                                }
                            }
                            else
                            {//读取PASS数据失败;继续等待
                                Thread.Sleep(100);//20200411：间隔500ms to confirm that whether the pass data is get下一层的打印
                                //PassItems--;
                                break;
                            }
                        }

                        if (true) //20240105新增：添加手动回清洗站，具体执行还得看相关标志位
                        {

                            float m_MovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                            float m_BackCleanMovSpeed = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
#if TwoPassPrintPerSixTimes
                            EquipmentMotionLogic3(0, 6, 12/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0, 0);//自动喷墨运动逻辑
#endif
#if TwoPassPrintPerThreeTimes
                            EquipmentMotionLogic3(0, 6, 6/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0, 0, 0);//自动喷墨运动逻辑
#endif
                        }


#if true//20210324//20220524批注//20220915修改：（1）自动喷墨运动（2）自动进给送粉（3）自动固化运动（4）自动清洗运动//20230402修改：（1）自动清洗运动（2）自动喷墨运动（3）自动进给送粉（4）自动固化运动
                        float m_MovSpeed2 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                        float m_BackCleanMovSpeed2 = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                        g_nCurrentLayer = k;//20201023新增：    
                        if (((k % g_nRePrintTimes)) == 0 && (k != 0))
                        {
                            //Thread.Sleep(6000);//等待6s时间，再执行固化
                            //EquipmentMotionLogic3(0, 1);//自动固化逻辑
                            //EquipmentMotionLogic3(0, 3, 0, m_MovSpeed2);//自动固化逻辑//20220914修改：调换铺粉逻辑与固化逻辑顺序，否则会造成推动

                            if (k < (LayerEndNum + 1) * g_nRePrintTimes)//20220524新建：避免埋掉，最后一次不进给铺粉
                            {
                                ////#region 监控指令：铺粉拍摄位点1
                                ////                                if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[7])
                                ////                                {
                                ////                                    sendMessageToCamera.SendMessageFromSharedMemory(false, renderIndex, 8);//20230113新建且批注：监控发送指令
                                ////                                }
                                ////#endregion

                                //EquipmentMotionLogic3(0, 2);//自动进给预送粉
                                //EquipmentMotionLogic3(0, 3);//自动进给正式铺粉
                                // 简易测试平台：X/Y 接在固高轴1/2（整机为成型缸/铺粉轴），若仍执行铺粉会与喷墨共用轴1/2，造成冲突；故简易测试时强制跳过铺粉
                                if (手动操作.UseSimpleTestMotion)
                                {
                                    msg = $"简易测试模式：跳过铺粉/成型缸运动，避免与墨车X/Y（固高轴1/2）冲突";
                                    Log4Net.Info(msg);
                                }
                                else if (g_RYSYSParam.m_bApplyPowderSupplyMotion == 0)//0为采用
                                {
                                    EquipmentMotionLogic3(0, 2, 0, m_MovSpeed2, m_BackCleanMovSpeed2, ref sendMessageToCamera, renderIndex, 10, 0, 0, 0, 0);//自动铺粉逻辑//20230319调试修改此处

                                    msg = $"执行完成铺粉固化操作：EquipmentMotionLogic3：m_bApplyPowderSupplyMotion:{g_RYSYSParam.m_bApplyPowderSupplyMotion}";
                                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                                }
                                else//1为不采用
                                {
                                    msg = $"不执行跳过铺粉固化操作：m_bApplyPowderSupplyMotion:{g_RYSYSParam.m_bApplyPowderSupplyMotion}";
                                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                                }

                                ////#region 监控指令：铺粉拍摄位点5
                                ////                                if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[11])
                                ////                                {
                                ////                                    sendMessageToCamera.SendMessageFromSharedMemory(false, renderIndex, 12);//20230113新建且批注：监控发送指令
                                ////                                }
                                ////#endregion
                            }
                            else { }

                            //EquipmentMotionLogic3(0, 3, 0, m_MovSpeed2);//自动固化逻辑//20220914修改(暂时注释掉）：调换铺粉逻辑与固化逻辑顺序，否则会造成推动
                        }

                        /////////20220915新增：加入自动清洗逻辑，判断是否需要
                        /////////20220915新增：读取清洗频率参数，判断是否需要
                        //////if (AutoPrintMotion1 == null)
                        //////{
                        //////    /*手动操作*/ AutoPrintMotion1 = new 手动操作(0, nValveStateMask);//20201030新增：读取自动打印参数//20230317修正:修正潜在的闪退问题
                        //////    msg = $"创建：AutoPrintMotion1=》初次创建完成-手动操作！";
                        //////    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                        //////}
                        //////else
                        //////{
                        //////    msg = $"创建：AutoPrintMotion1=》不需重新创建-手动操作！";
                        //////    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                        //////}

                        //////bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion1);//20201030新增：读取自动打印参数
                        //////g_nCleanFrequency = AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency;//20201030新增：读取自动打印参数
                        //////if (AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nStartRePrintClean == 1)//每次打印都重喷
                        //////{
                        //////    if ((g_nCurrentLayer % (g_nCleanFrequency * 1) == 0) && (g_nCurrentLayer != 0))
                        //////    {
                        //////        EquipmentMotionLogic3(0, 1, 0, m_MovSpeed2, ref sendMessageToCamera, 0, 0);//20220915新增：加入自动清洗逻辑
                        //////    }
                        //////}
                        //////else//不需要每次都清洗，重喷一次清洗一次
                        //////{
                        //////    if ((g_nCurrentLayer % (g_nCleanFrequency * g_nRePrintTimes) == 0) && (g_nCurrentLayer != 0))
                        //////    {
                        //////        EquipmentMotionLogic3(0, 1, 0, m_MovSpeed2, ref sendMessageToCamera, 0, 0);//20220915新增：加入自动清洗逻辑
                        //////    }
                        //////}
                        /////////20220915新增：结束读取清洗频率参数
#endif
                        returnPrintValue = k;//20200508新建：更新旋转进度条
                        g_nCurrentLayer = (k - 1) / g_nRePrintTimes/*k*/;//20201121修改：打印进度值
                    }
                    else if (PrintConrolFlag == "PausePrint")
                    {
                        msg = "接收到暂停打印指令：PrintTaskTHREAD";
                        Log4Net.Info(msg);

                        if ((m_nPauseMovedFlag != 1) || (m_nPauseMovedFlag != 2))//墨车不在清洗位，补充一次运动到清洗位
                        {
                            float m_MovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                            float m_BackCleanMovSpeed = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                            m_nPauseMovedFlag = 0;
                            if (PrintConrolFlag == "PausePrint") { m_nPauseMovedFlag = 1; }
#if SinglePassPrintMode
                            EquipmentMotionLogic3(0, 4, 6/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0,0);//自动喷墨运动逻辑
#endif
#if TwoPassPrintMode //true
#if TwoPassPrintPerSixTimes
                            EquipmentMotionLogic3(0, 6, 12/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0, 0);//自动喷墨运动逻辑
#endif
#if TwoPassPrintPerThreeTimes
                            EquipmentMotionLogic3(0, 6, 6/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0, 0, 0);//自动喷墨运动逻辑
#endif
#endif
                            m_nPauseMovedFlag = 2;//已经运动过额标志位
                        }
                        else//发生过运动，墨车已在清洗位
                        { }

                        Thread.Sleep(200);//200ms周期在持续等待：继续指令或者停止指令。
                        k--;
                        g_nCurrentLayer = (k - 1) / g_nRePrintTimes/*k*/;//20201121修改：
                    }
                    else if (PrintConrolFlag == "StopPrint")
                    {
                        msg = "接收到中止打印指令：PrintTaskTHREAD";
                        Log4Net.Info(msg);

                        break;
                    }
                    else { }

                    //System.Diagnostics.Debug.WriteLine("Debug:" + "LaserADD" + "打印完成第" + k + "层");//20200801批注：添加DebugView日志记录
                    //System.Diagnostics.Trace.WriteLine("Trace:" + "LaserADD" + "打印完成第" + k + "层");//20200801批注：添加DebugView日志记录
                }
                RoyalMap.m_bJobStarted = false;
                PrinterRunInfo(":当前打印任务完成：区间为" + (g_nLayerStart + 1) + " 层到 " + (g_nLayerEnd + 1) + " 层");
            }
#region 监控发送指令//20230113新建且批注：
            if (sendMessageToCamera != null)
            {
                Log4Net.Info("打印线程：准备释放 sendMessageToCamera");
                sendMessageToCamera.Dispose(); //20230113新建且批注：监控发送指令
                Log4Net.Info("打印线程：sendMessageToCamera 已释放");
            }
            else
            {
                Log4Net.Info("打印线程：跳过释放 sendMessageToCamera，因为对象为空");
            }
#endregion

            //20230331新增：打印完成后，关闭闪喷
            bool nRetVal3 = MeteorPrintEngine.SetFlash(false);//关闭闪喷
            msg = $"关闭闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal3}}}";
            Log4Net.Info(msg);

            /*bool*/
            ReturnFlag = MeteorPrintEngine.StopJob();
            msg = $"停止打印任务，释放板卡内存：IDP_StopPrintJob()：ReturnFlag{{{ReturnFlag}}}";
            Log4Net.Info(msg);

            Log4Net.Info($"打印线程：StopJob 已完成，准备释放 ImgPtr，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}");
            Marshal.FreeHGlobal(ImgPtr);//20200429批注：释放内存,一定要及时释放内存//批注：代码位置，需要重点考虑
            Log4Net.Info("打印线程：ImgPtr 已释放");
            PrintFlag = false;//20200716新增：关闭打印机维护的间歇闪喷使能
            g_TaskThreadSTATE[4] = 3;//20201119新增：DataTaskThread恢复为终止状态（打印完）
#region
            //（3）自然执行完毕，自然结束打印区间任务
            Log4Net.Info($"打印线程：准备 DeleteThread(\"PrintTaskTHREAD\")，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={PrintConrolFlag}");
            DeleteThread("PrintTaskTHREAD");//20200220：本线程结束，需要及时清理相关线程
            Log4Net.Info("打印线程：DeleteThread(\"PrintTaskTHREAD\") 已执行");
            if (LayerEnd.InvokeRequired == true)//20200313新增批注：此位置严格来说执行不到
            {
                Log4Net.Info("打印线程：LayerEnd.InvokeRequired=true，准备 BeginInvoke 恢复控件");
                LayerEnd.BeginInvoke(new Action(() =>
                {
                    this.LayerEnd.Enabled = true;//恢复控件操作
                    this.LayerStart.Enabled = true;//恢复控件操作
                }));
                Log4Net.Info("打印线程：LayerEnd.BeginInvoke 已提交");
            }
#endregion
            Log4Net.Info("打印线程：结束收尾完成");
        }

        //手动操作 ManualControl = null;//20230317修正:修正潜在的闪退问题
        手动操作 AutoPrintMotion0 = null;//20230419修正:修正潜在重新关闭及开启任务的潜在BUG
        手动操作 AutoPrintMotion1 = null;//20230317修正:修正潜在的闪退问题//PrintTask中
        手动操作 AutoPrintMotion2 = null;//20230317修正:修正潜在的闪退问题//温控Modbus中
        手动操作 AutoPrintMotion3 = null;//20230317修正:修正潜在的闪退问题//EquipmentMotionLogic3中
        手动操作 AutoPrintMotion4 = null;//20230317修正:修正潜在的闪退问题//DataTask中

        string[] g_calirationFigurePaths = new string[7] { @"\垂直校准图.bmp", @"\往返差校准图-0.bmp", @"\往返差校准图-1.bmp", @"\喷头套色校准图-0.bmp", @"\喷头套色校准图-1.bmp", @"\STATUS.bmp", @"\往返差校准图-3.bmp" };//20210324新增：//20210325修复BUG:6张图一定要路径准确
        private void PrintTaskTHREAD2()//3DP校准打印主流程：20210321新建批注
        {
            string msg = $"进入打印图校准线程：PrintTaskTHREAD2！";
            Log4Net.Info(msg);

            g_TaskThreadSTATE[4] = 2;//20201119新增：DataTaskThread恢复为运行状态（关机后）
            ReadLayerInfo();//更新指定的加工任务区间
            g_PrintSchedule = g_nLayerStart;//20201118新增：

            //string[] g_calirationFigurePaths = new string[6] { @"\垂直校准图.bmp", @"\往返差校准图-0.bmp", @"\往返差校准图-0.bmp", @"\喷头套色校准图-0.bmp", @"\喷头套色校准图-0.bmp", @"\STATUS.bmp" };//20210324新增：
            while (g_PrintSchedule < 1/*(g_PrintSchedule == -1) || g_PrintSchedule == g_nLayerStart*/)//20201118新增：处于初始态或者已经传输1层数据
            {
                Thread.Sleep(100);//数据传送进度需要领先起始打印层至少2层
            }

            PrintFlag = true;//20200716新增：关闭打印机维护的间歇闪喷使能
            ConfigureJetEnvironmentControlMode();//20200602修改:初始化喷墨系统环境控制，具体包括：下发自动供墨指令、下发设置自动负压指令、下发二级墨盒的温度设置指令、设置墨水搅拌周期指令                                           
            InitCarMotor();//20200327新增：//（1）初始化被控对象及加工任务区间
            float m_szMovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增//确定准确的墨车运动速度值
            uint m_unCarMoveSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch);//20200328新增
            g_nCarSinglePassLength = (int)((g_RYSYSParam.m_dCarMoveBufferLength + g_RYSYSParam.m_dPrintAeraLength + g_RYSYSParam.m_dCarMoveBufferLength2) * EncoderLinePerInch);//SinglePass运动距离：20200327新增：
            //royal.LPPrtRunInfo RTinfo = new LPPrtRunInfo();////（2-2）20200411新增（精华）：正式的打印处理框架：
            LPPassDataItem pPrtPassDes = new LPPassDataItem();//20200411:此处存在比较严重的问题            
            int size2 = Marshal.SizeOf(pPrtPassDes)/* * pPrtPassDes.Length*/;//20200427新增：
            IntPtr ImgPtr = Marshal.AllocHGlobal(size2);//20200429批注：类似于C++的NEW的操作
            Marshal.StructureToPtr(pPrtPassDes, ImgPtr, false);
            int LayerEndNum = g_nLayerEnd;//20200601修改：打印的终止层数，放在此处，仅仅是方便测试而已
            int CurrentStartPrintLayer = 1;//int CurrentStartPrintLayer = g_nLayerStart - g_nLayerStart;//20201121新增：//20201124修改：初始值永远为0//20210323修改:监控反馈启打层
            /*************************************************************************************************/
            /*************************************************************************************************/
            SendMessageToCamera sendMessageToCamera = null;
            if (DisableRoyalPrintRuntimeInit)
            {
                Log4Net.Info("Meteor print mode: skip PrintTaskTHREAD2 royal axis/ink initialization block.");
                goto PrintTaskTHREAD2RoyalInitSkip;
            }
#if true//进行打印进度监控测试
            if (DisableRoyalPrintRuntimeInit)
            {
                Log4Net.Info("Meteor print mode: skip PrintTaskTHREAD2 royal axis/ink initialization block.");
                goto PrintTaskTHREAD2RoyalInitSkip;
            }

            bool nRetVal0 = royal.royal.DEM_InitAxis(0, 0x100/*0x100*/);//分别初始化各轴的运动参数：20200305//加速度：256pluse/ms^2
            /*string*/ msg = $"初始化墨轴1：DEM_InitAxis：{{0, 0x100}}";
            Log4Net.Info(msg);

            nRetVal0 = royal.royal.DEM_EnableAxisRun(true);//所有的轴共用1个使能，使能一次就OK!:20200305     
            msg = $"使能所有墨轴：DEM_EnableAxisRun：{{true}}";
            Log4Net.Info(msg);

            bool nRetVal = royal.royal.DEV_EnableInkAutoSupply(true, 0xFF);//使能自动供墨uint ControlBit = 0xFF;//20201114修改：使能第4路墨水的自动供墨

            msg = $"开启自动供墨：DEV_EnableInkAutoSupply：ControlBit{{0xFF}}";
            Log4Net.Info(msg);
#endif
        PrintTaskTHREAD2RoyalInitSkip:

            sendMessageToCamera = new SendMessageToCamera(false);//20200202修改
            //sendMessageToCamera.LoadJsonFile();

            while ((RoyalMap.m_bJobStarted == true))//开启打印处理线程：20200411新建
            {
                returnPrintValue = 1/*(CurrentStartPrintLayer + 1) * g_nRePrintTimes*/;//20200508：复位打印进度值   
                UpdateCircularBarMethod(2);//20201121新增：开启打印进度更新

                int CorrectionFigureNum = 0;//k不可以为0；原因在于，控制的第一层数据无效
                int CorrectionFigureOffset = 0;//校准图偏移量，4种校准图打印模式，偏移量依次为：（1）0；（2）1；（3）3；（4）5
                if (g_CorrectionFigureType == 1)//Type 1:垂直校准图打印模式；
                {
                    CorrectionFigureNum = 1;
                    CorrectionFigureOffset = 0;//20210325新增：
                }
                if (g_CorrectionFigureType == 2)//Type 1:往返差校准图打印模式；
                {
                    CorrectionFigureNum = 2;
                    CorrectionFigureOffset = 1;//20210325新增：
                }
                if (g_CorrectionFigureType == 5)//Type 1:往返差校准图一次性打印模式；
                {
                    CorrectionFigureNum = 1;
                    CorrectionFigureOffset = 6;//20210325新增：
                }
                if (g_CorrectionFigureType == 3)//Type 1:垂直校准图打印模式；
                {
                    CorrectionFigureNum = 2;
                    CorrectionFigureOffset = 3;//20210325新增：
                }
                if (g_CorrectionFigureType == 4)//Type 1:垂直校准图打印模式；
                {
                    CorrectionFigureNum = 1;
                    CorrectionFigureOffset = 5;//20210325新增：
                }

                for (int k = 1/*0*//*CurrentStartPrintLayer + 1*/ ; k <= CorrectionFigureNum/*2*/; k++)//核心代码//20200411新建：k为打印层数的Index
                {
                    if (PrintConrolFlag == "StartPrint" || PrintConrolFlag == "KeepPrint")//每次打印之前，都需要执行指令判断
                    {
                        //int renderIndex = ((k - 1) / g_nRePrintTimes) + g_nLayerStart /*k*/;
                        //Rendering2D(renderIndex); //20200601：实现成形层的逐层预览刷新//201030修改：
                        CurrentStartPrintLayer = k;//启打层

                        /***********************************20200508:实际打印过程：*********************************/
                        /***********************************20200508:实际打印过程：*********************************/
                        {
                            //20210324新建：刷新当前准备打印的校准图到主界面
                            //string[] calirationFigurePaths = new string[6] { @"\垂直校准图.bmp" , @"\往返差校准图-0.bmp", @"\往返差校准图-0.bmp", @"\喷头套色校准图-0.bmp", @"\喷头套色校准图-0.bmp", @"\STATUS.bmp" };
                            string CalibrationFilePath2 = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                            string BMPFilePath = CalibrationFilePath2 + g_calirationFigurePaths[k - 1 + CorrectionFigureOffset];
                            g_SharpControl.LoadingFromBMPFile(BMPFilePath);//20210328临时注释：//20230409修复：恢复显示
                            g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；//20230409修复：恢复显示

                            ////string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                            ////string BMPFilePath = CalibrationFilePath + @"\往返差校准图-1.bmp"/*@"\APCLROT-1.bmp"*/;
                            ////g_SharpControl.LoadingFromBMPFile(BMPFilePath);

                            this.renderControl1.Invalidate();

                            //string msg;
                            int PassItems = 0;
                            Log4Net.Info($"监控配置：准备创建 SendMessageToCamera，layer={k}, pass={PassItems}");
                            /*SendMessageToCamera*/ sendMessageToCamera = new SendMessageToCamera(false);//20200202修改
                            Log4Net.Info($"监控配置：SendMessageToCamera 创建完成，layer={k}, pass={PassItems}");
                            if (DisableMonitoringRuntimeInit)
                            {
                                Log4Net.Info($"监控配置：跳过 sendMessageToCamera.LoadJsonFile，layer={k}, pass={PassItems}");
                            }
                            else
                            {
                                sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                            }
                                                               //if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[PassItems])
                                                               //{
                                                               //    sendMessageToCamera.SendMessageFromSharedMemory(false, renderIndex, PassItems + 1);//20230113新建且批注：监控发送指令
                                                               //}
                            int PassNum = 6;
#if SinglePassPrintMode
                            PassNum = 6;
#endif
#if TwoPassPrintMode
#if TwoPassPrintPerSixTimes
                            PassNum = 12;
#endif
#if TwoPassPrintPerThreeTimes
                            PassNum = 6;
#endif
#endif
                            for (PassItems = 0; PassItems < PassNum/*6*//*7*/; PassItems++)//20220531修改：总共数量为6 PASS
                            {
                                /*****************（1）20220524批注：确保获取打印PASS信息*********************/
                                int nPassID = PassItems/*0*//*1*//*0*/;//20200424新增：测试结果表明1是错误的，无法顺利执行//20220524新增：修改为多PASS打印
                                /*bool*/
                                Log4Net.Info($"Meteor pass准备：开始获取PassItem，layer={k}, pass={nPassID}");
                                bool ReturnFlag = MeteorPrintEngine.TryGetPassItem((uint)k, nPassID/*0*/, /*ImgPtr*/ref pPrtPassDes);

                                while (pPrtPassDes.nProcState != 3)//20200624批注：不成功就重新读
                                {
                                    Log4Net.Info($"Meteor pass等待：nProcState={pPrtPassDes.nProcState} 未到3，继续轮询，layer={k}, pass={nPassID}");
                                    Thread.Sleep(100);//等待1s时间，再次GetPassItem;
                                    ReturnFlag = MeteorPrintEngine.TryGetPassItem((uint)k, nPassID/*0*/, /*ImgPtr*/ref pPrtPassDes);
                                }
                            /*****************（2）20220524批注：执行打印PASS运动逻辑*********************/
                            if (ReturnFlag == true/*pPrtPassDes!=null*/)//20200411:读到的数据不为空//20200430开启运动：
                            {
                                Log4Net.Info($"打印线程：准备调用 TriggerPass，nLayerIndex={k}，nPassID={nPassID}，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}");
                                var triggerWatch = Stopwatch.StartNew();
                                bool returnCode2 = MeteorPrintEngine.TriggerPass((uint)k, nPassID /*-1*/);
                                triggerWatch.Stop();
                                Log4Net.Info($"打印线程：TriggerPass 返回，nLayerIndex={k}，nPassID={nPassID}，returnCode2={returnCode2}，elapsedMs={triggerWatch.ElapsedMilliseconds}");
                                if (returnCode2 == false)
                                {
                                    msg = $"使能Pass打印失败：IDP_DoPassPrint2：nLayerIndex{{{k}}}nPassID{{{nPassID}}}";
                                    Log4Net.Info(msg);

                                        MessageBox.Show("启动打印失败！LayerIndex=" + pPrtPassDes.nLayerIndex + ",nProcState=" + pPrtPassDes.nProcState + "；打印分频值=" + pPrtPassDes.nPrtPrecession + "有效列数" + pPrtPassDes.nValidPrtCols);//20220531修改：
                                    }
                                    else
                                    {
                                        msg = $"使能Pass打印成功：IDP_DoPassPrint2：nLayerIndex{{{k}}}nPassID{{{nPassID}}}";
                                        Log4Net.Info(msg);

                                        ////royal.LPPRINTER_INFO pSysInfo = new royal.LPPRINTER_INFO();//20230213新增：
                                        ////bool nRetVal2 = royal.royal.DEV_GetDeviceInfo2(ref pSysInfo);//20230213新增：
                                        ////msg = $"PASS打印前关键状态：DEV_GetDeviceInfo2：nLayerIndex{{{k}}}nPassID{{{nPassID}}}" +
                                        ////    $"LPPRINTER_INFO:nXSysEncDPI{{{pSysInfo.nXSysEncDPI}}}nStatus{{{pSysInfo.nStatus}}}nPrintStatus{{{pSysInfo.nPrintStatus}}}bSuperDevice{{{pSysInfo.bSuperDevice}}}\r\n" +
                                        ////    $"LPPRINTER_INFO-LPPrtRunInfo:bJobPrtRuning{{{pSysInfo.prt_rtinfo.bJobPrtRuning}}}bLayerPrtIsOver{{{pSysInfo.prt_rtinfo.bLayerPrtIsOver}}}" +
                                        ////    $"nContReqMemErr{{{pSysInfo.prt_rtinfo.nContReqMemErr}}}nContWDErr{{{pSysInfo.prt_rtinfo.nContWDErr}}}" +
                                        ////    $"nCurPrtDir{{{pSysInfo.prt_rtinfo.nCurPrtDir}}}nDTLayerIndex{{{pSysInfo.prt_rtinfo.nDTLayerIndex}}}" +
                                        ////    $"nDTLayerPassIndex{{{pSysInfo.prt_rtinfo.nDTLayerPassIndex}}}nDTPtrCtlIndex{{{pSysInfo.prt_rtinfo.nDTPtrCtlIndex}}}" +
                                        ////    $"nLayerPassCount{{{pSysInfo.prt_rtinfo.nLayerPassCount}}}nPrintLayerIndex{{{pSysInfo.prt_rtinfo.nPrintLayerIndex}}}" +
                                        ////    $"nPrintPassIndex{{{pSysInfo.prt_rtinfo.nPrintPassIndex}}}nProcLayerIndex{{{pSysInfo.prt_rtinfo.nProcLayerIndex}}}" +
                                        ////    $"nPrtDataMemAddr{{{pSysInfo.prt_rtinfo.nPrtDataMemAddr}}}nPrtState{{{pSysInfo.prt_rtinfo.nPrtState}}}" +
                                        ////    $"nReverse{{{pSysInfo.prt_rtinfo.nReverse}}}nRevPrtCols{{{pSysInfo.prt_rtinfo.nRevPrtCols}}}\r\n" +

                                        ////    $"LPPRINTER_INFO-LPDRVINFO:nFMVersion{{{pSysInfo.sysDrvInfo[0].nFMVersion}}}nFpgaVersion{{{pSysInfo.sysDrvInfo[0].nFpgaVersion}}}" +
                                        ////    $"nPCBVersion{{{pSysInfo.sysDrvInfo[0].nPCBVersion}}}" +
                                        ////    $"nState{{{pSysInfo.sysDrvInfo[0].nState}}}nNextState{{{pSysInfo.sysDrvInfo[0].nNextState}}}" +
                                        ////    $"nPtvwarnState{{{pSysInfo.sysDrvInfo[0].nPtvwarnState}}}nCrc32{{{pSysInfo.sysDrvInfo[0].nCrc32}}}" +
                                        ////    $"nRevInfo{{{pSysInfo.sysDrvInfo[0].nRevInfo}}}nSignature{{{pSysInfo.sysDrvInfo[0].nSignature}}}";
                                        ////Log4Net.Info(msg);

                                        royal.LPPRINTER_INFO g_printerInfoLocal = new royal.LPPRINTER_INFO();//20230213新增：
                                        IntPtr info = royal.royal.DEV_GetDeviceInfo();//——————调用API1(修改后的API1)
                                        g_printerInfoLocal = (LPPRINTER_INFO)Marshal.PtrToStructure(info, typeof(LPPRINTER_INFO));//调用API1获取的指针
                                        string msg3 = $"PASS打印前关键状态：DEV_GetDeviceInfo：nLayerIndex{{{k}}}nPassID{{{nPassID}}}" +
                                             $"LPPRINTER_INFO:nXSysEncDPI{{{g_printerInfoLocal.nXSysEncDPI}}}nStatus{{{g_printerInfoLocal.nStatus}}}nPrintStatus{{{g_printerInfoLocal.nPrintStatus}}}bSuperDevice{{{g_printerInfoLocal.bSuperDevice}}}\r\n" +
                                             $"LPPRINTER_INFO-LPPrtRunInfo:bJobPrtRuning{{{g_printerInfoLocal.prt_rtinfo.bJobPrtRuning}}}bLayerPrtIsOver{{{g_printerInfoLocal.prt_rtinfo.bLayerPrtIsOver}}}" +
                                             $"nContReqMemErr{{{g_printerInfoLocal.prt_rtinfo.nContReqMemErr}}}nContWDErr{{{g_printerInfoLocal.prt_rtinfo.nContWDErr}}}" +
                                             $"nCurPrtDir{{{g_printerInfoLocal.prt_rtinfo.nCurPrtDir}}}nDTLayerIndex{{{g_printerInfoLocal.prt_rtinfo.nDTLayerIndex}}}" +
                                             $"nDTLayerPassIndex{{{g_printerInfoLocal.prt_rtinfo.nDTLayerPassIndex}}}nDTPtrCtlIndex{{{g_printerInfoLocal.prt_rtinfo.nDTPtrCtlIndex}}}" +
                                             $"nLayerPassCount{{{g_printerInfoLocal.prt_rtinfo.nLayerPassCount}}}nPrintLayerIndex{{{g_printerInfoLocal.prt_rtinfo.nPrintLayerIndex}}}" +
                                             $"nPrintPassIndex{{{g_printerInfoLocal.prt_rtinfo.nPrintPassIndex}}}nProcLayerIndex{{{g_printerInfoLocal.prt_rtinfo.nProcLayerIndex}}}" +
                                             $"nPrtDataMemAddr{{{g_printerInfoLocal.prt_rtinfo.nPrtDataMemAddr}}}nPrtState{{{g_printerInfoLocal.prt_rtinfo.nPrtState}}}" +
                                             $"nReverse{{{g_printerInfoLocal.prt_rtinfo.nReverse}}}nRevPrtCols{{{g_printerInfoLocal.prt_rtinfo.nRevPrtCols}}}\r\n" +

                                             $"LPPRINTER_INFO-LPDRVINFO:nFMVersion{{{g_printerInfoLocal.sysDrvInfo[0].nFMVersion}}}nFpgaVersion{{{g_printerInfoLocal.sysDrvInfo[0].nFpgaVersion}}}" +
                                             $"nPCBVersion{{{g_printerInfoLocal.sysDrvInfo[0].nPCBVersion}}}" +
                                             $"nState{{{g_printerInfoLocal.sysDrvInfo[0].nState}}}nNextState{{{g_printerInfoLocal.sysDrvInfo[0].nNextState}}}" +
                                             $"nPtvwarnState{{{g_printerInfoLocal.sysDrvInfo[0].nPtvwarnState}}}nCrc32{{{g_printerInfoLocal.sysDrvInfo[0].nCrc32}}}" +
                                             $"nRevInfo{{{g_printerInfoLocal.sysDrvInfo[0].nRevInfo}}}nSignature{{{g_printerInfoLocal.sysDrvInfo[0].nSignature}}}";
                                        Log4Net.Info(msg3);

                                        //20220524批注：（2）自动喷墨运动
                                        //20220524批注：（2）自动喷墨运动
                                        //RecordPressureAndTemperatureAndValtageInPrint();//20230322新建：记录打印之前喷头温度及电压

                                        ///20220915新增：结束读取清洗频率参数
                                        ///
                                        if (AutoPrintMotion1 == null)
                                        {
                                            /*手动操作*/
                                            AutoPrintMotion1 = new 手动操作(0, nValveStateMask, false);//20201030新增：读取自动打印参数//20230317修正:修正潜在的闪退问题
                                            msg = $"创建：AutoPrintMotion1=》初次创建完成-手动操作！";
                                            Log4Net.Info(msg);
                                        }
                                        else
                                        {
                                            msg = $"创建：AutoPrintMotion1=》不需重新创建-手动操作！";
                                            Log4Net.Info(msg);
                                        }

                                        bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion1);//20201030新增：读取自动打印参数
                                        g_nCleanFrequency = AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency;//20201030新增：读取自动打印参数

                                        if (AutoPrintMotion1.k_RYSYSParamAutoPrintParamInTest.m_nAutoPrintCleanEnabled == 1)
                                        {
                                            if (g_nCleanFrequency * g_nRePrintTimes == 1)
                                            {
                                                if (nPassID == 0)
                                                {
                                                    float m_MovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                                    float m_BackCleanMovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                                                    EquipmentMotionLogic3(0, 1, 0, m_MovSpeed3, m_BackCleanMovSpeed3, ref sendMessageToCamera, 0, 0, 0, 0, 0, 0);//20220915新增：加入自动清洗逻辑
                                                }
                                            }
                                            else
                                            {
                                                if (k % (g_nCleanFrequency * g_nRePrintTimes) == 1)
                                                {
                                                    if (nPassID == 0)
                                                    {
                                                        float m_MovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                                        float m_BackCleanMovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                                                        EquipmentMotionLogic3(0, 1, 0, m_MovSpeed3, m_BackCleanMovSpeed3, ref sendMessageToCamera, 0, 0, 0, 0, 0, 0);//20220915新增：加入自动清洗逻辑
                                                    }
                                                }
                                            }
                                        }

                                        ////if (nPassID == 0)
                                        ////{
                                        ////    float m_MovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                        ////    float m_BackCleanMovSpeed3 = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                                        ////    EquipmentMotionLogic3(0, 1, 0, m_MovSpeed3, m_BackCleanMovSpeed3, ref sendMessageToCamera, 0, 0, 0);//20220915新增：加入自动清洗逻辑
                                        ////}
                                        bool DirFlag = pPrtPassDes.bPrtDir;//102023修改：打印方向
                                        float m_MovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                        float m_BackCleanMovSpeed = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度

                                        m_nPauseMovedFlag = 0;//202304010新增：
                                        if (PrintConrolFlag == "PausePrint") { m_nPauseMovedFlag = 1; }//202304010新增：
#if SinglePassPrintMode
                                        if (g_nRePrintTimes == 1)//20230418批注：重喷次数取值范围为：1-4
                                        {
                                            EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0);//自动喷墨运动逻辑
                                        }
                                        else if (g_nRePrintTimes == 2)
                                        {
                                            if (k % g_nRePrintTimes == 1) //20230418修改:第1PASS打印
                                            {
                                                EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 1);//自动喷墨运动逻辑 
                                            }
                                            else if (k % g_nRePrintTimes == 0)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                            {
                                                EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0);//自动喷墨运动逻辑
                                            }

                                        }
                                        else if (g_nRePrintTimes == 3)
                                        {
                                            if (k % g_nRePrintTimes == 1) //20230418修改:第1PASS打印
                                            {
                                                EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 1);//自动喷墨运动逻辑 
                                            }
                                            else if (k % g_nRePrintTimes == 2)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                            {
                                                EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 1);//自动喷墨运动逻辑
                                            }
                                            else if (k % g_nRePrintTimes == 0)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                            {
                                                EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0);//自动喷墨运动逻辑
                                            }
                                        }
                                        else if (g_nRePrintTimes == 4)
                                        {
                                            if (k % g_nRePrintTimes == 1) //20230418修改:第1PASS打印
                                            {
                                                EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 1);//自动喷墨运动逻辑 
                                            }
                                            else if (k % g_nRePrintTimes == 2)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                            {
                                                EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 1);//自动喷墨运动逻辑
                                            }
                                            else if (k % g_nRePrintTimes == 3)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                            {
                                                EquipmentMotionLogic3(0, 4, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 1);//自动喷墨运动逻辑
                                            }
                                            else if (k % g_nRePrintTimes == 0)//20230418修改:第2PASS打印//Y方向的偏差值为g_RYSYSParam.m_dYJetOff
                                            {
                                                EquipmentMotionLogic3(0, 5, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0);//自动喷墨运动逻辑
                                            }
                                        }
#endif
#if TwoPassPrintMode
#if TwoPassPrintPerSixTimes
                                        EquipmentMotionLogic3(0, 6, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0);//自动喷墨运动逻辑
#endif
#if TwoPassPrintPerThreeTimes
                                        EquipmentMotionLogic3(0, 6, nPassID, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, g_RYSYSParam.m_dYJetOff / 2, 0, 0);//自动喷墨运动逻辑
#endif
#endif
                                        CurrentStartPrintLayer = k;
                                    }
                                }
                                else
                                {//读取PASS数据失败;继续等待
                                    Thread.Sleep(100);//20200411：间隔500ms to confirm that whether the pass data is get下一层的打印
                                    //PassItems--;
                                    break;
                                }
                            }

                            


#if false
                            int nPassID = 0/*1*//*0*/;//20200424新增：测试结果表明1是错误的，无法顺利执行
                            bool ReturnFlag = MeteorPrintEngine.TryGetPassItem((uint)k, nPassID/*0*/, /*ImgPtr*/ref pPrtPassDes);
                            while (pPrtPassDes.nProcState != 3)//20200624批注：不成功就重新读
                            {
                                Thread.Sleep(100);//等待1s时间，再次GetPassItem;
                                ReturnFlag = MeteorPrintEngine.TryGetPassItem((uint)k, nPassID/*0*/, /*ImgPtr*/ref pPrtPassDes);
                            }
                            if (ReturnFlag == true/*pPrtPassDes!=null*/)//20200411:读到的数据不为空//20200430开启运动：
                            {
                                bool returnCode2 = MeteorPrintEngine.TriggerPass((uint)k, -1);
                                if (returnCode2 == false) { MessageBox.Show("启动打印失败！LayerIndex=" + pPrtPassDes.nLayerIndex + ",nProcState=" + pPrtPassDes.nProcState); }
                                else
                                {
                                    double rate = (double)g_nLayerCurrent / (double)g_nLayerEnd * 100;//20200617批注
                                    string PintRate = rate.ToString("f1");//20200411新增：本处的显示，应该转移到状态栏的第3个lable                               
                                    this.PrinterStatusLabel.Text = "|| 打印中：当前打印第"
                                                                     + (((k - 1) / g_nRePrintTimes) + g_nLayerStart + 1).ToString() + "-"
                                                                     + (((k - 1) % g_nRePrintTimes) + 1).ToString()
                                                                         + "层，剩余" + /*(LayerEndNum - k)*/((LayerEndNum + 1) - (((k - 1) / g_nRePrintTimes) + g_nLayerStart + 1)).ToString() + "层。";//20200430新增：打印机状态栏//201030修改：

                                    returnPrintValue = k;//20200508新建：更新进度，更新进度到手动操作

                                    bool DirFlag = pPrtPassDes.bPrtDir;//102023修改：替换
                                    float m_MovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增

                                    bool Directory = false; uint nRevPls = 0;
                                    if (/*(PowderCarHomeFlag == false)&&*/ (InkCarHomeFlag == true))//确保：墨车系统回零成功；确保在指定区间，否则报错//临时关闭铺粉校准
                                    {
                                        double[] g_dEncpos = new double[8];
                                        g_dEncpos = motionMap.GetEncPos();
                                        double PosValue = g_dEncpos[3] / 1000;//铺粉位置
                                        if (PosValue < 50 || PosValue > 800)////正式打印逻辑
                                        {
                                            if (DirFlag == true)//在清洗端近端//执行打印流程//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                                            { BackToStation(1185/*1185*/, (float)m_MovSpeed); }
                                            else //在清洗端远端//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑200918新增：单纯的刮墨逻辑 
                                            { BackToStation(45/*45*/, (float)m_MovSpeed); }
                                            //do//20200628新增：确保主运动到打印结束区域：不停运动监控，until打印结束，才执行下次打印
                                            //{
                                            //    bool returnCode3 = royal.royal.IDP_GetPrintState(ref RTinfo); //获取打印运行状态，用来判断当前打印的状态//20200429新增：
                                            //    Thread.Sleep(20);//20200411：间隔1ms再监测是否运动结束
                                            //} while (!RTinfo.bLayerPrtIsOver);
                                            while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls))//int SleepTime = (int)((double)(AimPos - CurrentPos) / nSpeed);//Thread.Sleep(5000);//Sleep时间必须要有依据//确保运行到位，运行精度为2UM
                                            { Thread.Sleep(20); }
                                            bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                                        }
                                        else { /*MessageBox.Show("铺粉车未停靠在安全区");*/ }

                                        MessageBox.Show("关闭窗口继续下次打印！");
                                    }
                                    else {/* MessageBox.Show("墨车系统/铺粉系统未回零");*/ }
                                    CurrentStartPrintLayer = k;
                        }
                            }
                            else
                            {//读取PASS数据失败
                                Thread.Sleep(100);//20200411：间隔500ms to confirm that whether the pass data is get下一层的打印
                                break;
                            }
#endif
                        }
                        returnPrintValue = k;//20200508新建：更新旋转进度条
                        g_nCurrentLayer = (k - 1) / g_nRePrintTimes/*k*/;//20201121修改：打印进度值
                    }
                    else if (PrintConrolFlag == "PausePrint")
                    {
                        Thread.Sleep(200);//200ms周期在持续等待：继续指令或者停止指令。

                        if ((m_nPauseMovedFlag != 1) || (m_nPauseMovedFlag != 2))//墨车不在清洗位，补充一次运动到清洗位
                        {
                            float m_MovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                            float m_BackCleanMovSpeed = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度
                            m_nPauseMovedFlag = 0;
                            if (PrintConrolFlag == "PausePrint") { m_nPauseMovedFlag = 1; }
#if SinglePassPrintMode
                            EquipmentMotionLogic3(0, 4, 6/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0,1);//自动喷墨运动逻辑
#endif
#if TwoPassPrintMode
#if TwoPassPrintPerSixTimes
                            EquipmentMotionLogic3(0, 6, 12/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0, 1);//自动喷墨运动逻辑
#endif
#if TwoPassPrintPerThreeTimes
                            EquipmentMotionLogic3(0, 6, 6/*nPassID*/, m_MovSpeed, m_BackCleanMovSpeed, ref sendMessageToCamera, 0, 0, m_nPauseMovedFlag, 0, 1, 0);//自动喷墨运动逻辑
#endif
#endif
                            m_nPauseMovedFlag = 2;//已经运动过额标志位
                        }
                        else//发生过运动，墨车已在清洗位
                        { }

                        k--;
                        g_nCurrentLayer = (k - 1) / g_nRePrintTimes/*k*/;//20201121修改：
                    }
                    else if (PrintConrolFlag == "StopPrint") { break; }
                    else { }
                    System.Diagnostics.Debug.WriteLine("Debug:" + "LaserADD" + "打印完成第" + k + "层");//20200801批注：添加DebugView日志记录
                    System.Diagnostics.Trace.WriteLine("Trace:" + "LaserADD" + "打印完成第" + k + "层");//20200801批注：添加DebugView日志记录
                }
                RoyalMap.m_bJobStarted = false;
                PrinterRunInfo(":当前打印任务完成：区间为" + (g_nLayerStart + 1) + " 层到 " + (g_nLayerEnd + 1) + " 层");
            }

            //20230331新增：打印完成后，关闭闪喷
            bool nRetVal3 = MeteorPrintEngine.SetFlash(false);//关闭闪喷
            string msg2 = $"关闭闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal3}}}";
            Log4Net.Info(msg2);

            bool ReturnFlag4 = MeteorPrintEngine.StopJob();
            msg2 = $"停止打印任务，释放板卡内存：IDP_StopPrintJob()：ReturnFlag{{{ReturnFlag4}}}";
            Log4Net.Info(msg2);

            g_SharpControl.g_CorrectionFigureFlag = 0;//临时显示是1；//20230409修复：恢复到非校准图显示状态

            Marshal.FreeHGlobal(ImgPtr);//20200429批注：释放内存,一定要及时释放内存//批注：代码位置，需要重点考虑
            PrintFlag = false;//20200716新增：关闭打印机维护的间歇闪喷使能
            g_TaskThreadSTATE[4] = 3;//20201119新增：DataTaskThread恢复为终止状态（打印完）
#region
            //（3）自然执行完毕，自然结束打印区间任务
            DeleteThread("PrintTaskTHREAD2");//20200220：本线程结束，需要及时清理相关线程
            if (LayerEnd.InvokeRequired == true)//20200313新增批注：此位置严格来说执行不到
            {
                LayerEnd.BeginInvoke(new Action(() =>
                {
                    this.LayerEnd.Enabled = true;//恢复控件操作
                    this.LayerStart.Enabled = true;//恢复控件操作
                }));
            }
#endregion
        }

        private void RecordPressureAndTemperatureAndValtage()//20230322新增：
        {
            uint p = 0;//1号板卡
            uint d = 0;//1好喷头
            float[] fVoltageTemp = new float[5];//20200612修改：

            //(1)读取喷头电压
            fVoltageTemp[0] = 0.0f; fVoltageTemp[1] = 0.0f; fVoltageTemp[2] = 0.0f; fVoltageTemp[3] = 0.0f; fVoltageTemp[4] = 0.0f;//喷头温度
            int size1 = Marshal.SizeOf(fVoltageTemp[0]) * 4;
            IntPtr PfVoltage = Marshal.AllocHGlobal(size1);
            Marshal.Copy(fVoltageTemp, 0, PfVoltage, 4);//测试用：非托管区内存初始化//实际测试的时候，去掉//20200405批注
            //20200424新增//读取波形参数的电压：
            string msg = $"【RecordPressureAndTemperatureAndValtage】: 准备读取{{{d + 1}}}号喷头打印前状态：读取电压！！";
            Log4Net.Info(msg);
            bool returnCode = royal.royal.MCU_GetCurPhVoltage(PfVoltage, p, d);
            if (returnCode == true)
            {
                Marshal.Copy(PfVoltage, fVoltageTemp, 0, 4);//（1）解析读取到的电压值
                Marshal.FreeHGlobal(PfVoltage);//释放内存
            }
            Thread.Sleep(10);

            //(2)读取喷头温度
            msg = $"【RecordPressureAndTemperatureAndValtage】: 准备读取{{{d + 1}}}号喷头打印前状态：读取温度！！";
            Log4Net.Info(msg);
            int size2 = Marshal.SizeOf(fVoltageTemp[0]) * 1;//4个变量，只用到第1个
            IntPtr PfCurTemp = Marshal.AllocHGlobal(size2);
            Marshal.Copy(fVoltageTemp, 0, PfCurTemp, 1);//测试用:非托管区内存初始化//实际测试的时候，去掉//20200405批注
            //20200424新增//读取喷头的温度：
            bool returnCode2 = royal.royal.MCU_GetCurPhTemp(PfCurTemp, p, d);
            if (returnCode2 == true)//0x0802->0x0822
            {
                Marshal.Copy(PfCurTemp, fVoltageTemp, 4, 1); //（1）解析读取到的电压值
                Marshal.FreeHGlobal(PfCurTemp);//释放内存
            }

            //(3)读取气压值：
            uint nIoOption = 0;
            nIoOption |= 0x4;//nIoOption = 0d1111;//只读取负压值
            LPADIB_PARAM adibCurState = new LPADIB_PARAM();
            bool m_bSetEnable = true;
            bool m_bComState = royal.royal.DEV_AdibControl(ref adibCurState, nIoOption, false, ref m_bSetEnable);//bSetParam位的作用为0：状态为读状态：20200329批注
            //adibCurState.fcurvoltage[0].ToString("F2");
            //(4）刷新记录：
            msg = $"【RecordPressureAndTemperatureAndValtage】: {{{d + 1}}}号喷头打印前状态：系统负压为{{{ adibCurState.fcurAirPress[0].ToString("F2")}kPa}}，喷头温度为{{{fVoltageTemp[0]}℃}}，喷头电压为{{{fVoltageTemp[1]}V,{fVoltageTemp[2]}V,{fVoltageTemp[3]}V,{ fVoltageTemp[4]}V}}；";
            Log4Net.Info(msg);
        }

        private void RecordPressureAndTemperatureAndValtageInPrint()//20230322新增：
        {
            uint p = 0;//1号板卡
            uint d = 0;//1好喷头
            float[] fVoltageTemp = new float[5];//20200612修改：

            //(1)读取喷头电压
            fVoltageTemp[0] = 0.0f; fVoltageTemp[1] = 0.0f; fVoltageTemp[2] = 0.0f; fVoltageTemp[3] = 0.0f; fVoltageTemp[4] = 0.0f;//喷头温度
            int size1 = Marshal.SizeOf(fVoltageTemp[0]) * 4;
            IntPtr PfVoltage = Marshal.AllocHGlobal(size1);
            Marshal.Copy(fVoltageTemp, 0, PfVoltage, 4);//测试用：非托管区内存初始化//实际测试的时候，去掉//20200405批注
            //20200424新增//读取波形参数的电压：
            string msg = $"【RecordPressureAndTemperatureAndValtage】: 准备读取{{{d + 1}}}号喷头打印前状态：读取电压！！";
            Log4Net.Info(msg);
            bool returnCode = royal.royal.MCU_GetCurPhVoltage(PfVoltage, p, d);
            if (returnCode == true)
            {
                Marshal.Copy(PfVoltage, fVoltageTemp, 0, 4);//（1）解析读取到的电压值
                Marshal.FreeHGlobal(PfVoltage);//释放内存
            }
            Thread.Sleep(10);

            //(2)读取喷头温度
            msg = $"【RecordPressureAndTemperatureAndValtage】: 准备读取{{{d + 1}}}号喷头打印前状态：读取温度！！";
            Log4Net.Info(msg);
            int size2 = Marshal.SizeOf(fVoltageTemp[0]) * 1;//4个变量，只用到第1个
            IntPtr PfCurTemp = Marshal.AllocHGlobal(size2);
            Marshal.Copy(fVoltageTemp, 0, PfCurTemp, 1);//测试用:非托管区内存初始化//实际测试的时候，去掉//20200405批注
            //20200424新增//读取喷头的温度：
            bool returnCode2 = royal.royal.MCU_GetCurPhTemp(PfCurTemp, p, d);
            if (returnCode2 == true)//0x0802->0x0822
            {
                Marshal.Copy(PfCurTemp, fVoltageTemp, 4, 1); //（1）解析读取到的电压值
                Marshal.FreeHGlobal(PfCurTemp);//释放内存
            }

            ////(3)读取气压值：
            //uint nIoOption = 0;
            //nIoOption |= 0x4;//nIoOption = 0d1111;//只读取负压值
            //LPADIB_PARAM adibCurState = new LPADIB_PARAM();
            //bool m_bSetEnable = true;
            //bool m_bComState = royal.royal.DEV_AdibControl(ref adibCurState, nIoOption, false, ref m_bSetEnable);//bSetParam位的作用为0：状态为读状态：20200329批注
            ////adibCurState.fcurvoltage[0].ToString("F2");
            //(4）刷新记录：
            msg = $"【RecordPressureAndTemperatureAndValtage】: {{{d + 1}}}号喷头打印前状态：喷头温度为{{{fVoltageTemp[0]}℃}}，喷头电压为{{{fVoltageTemp[1]}V,{fVoltageTemp[2]}V,{fVoltageTemp[3]}V,{ fVoltageTemp[4]}V}}；";
            Log4Net.Info(msg);
        }

        private void BackToStation(double AimPos, float m_MovSpeed)//运动至初始区域：AimPos位置单位为MM：精华
        {
            //royal.LPPrtRunInfo RTinfo = new LPPrtRunInfo();////（2-2）20200411新增（精华）：正式的打印处理框架：

            UInt32 nCtlValue = /*2*/2; UInt32 CurrentPos = 0; bool DirFlag = false;
            //float m_MovSpeed = 50;//50mm/s速度进行移动；到站延时运动精度0.5mm//20201020新增：
            //bool Directory = false;uint nRevPls = 0; 
            double nSpeed = MM_TO_DOT(m_MovSpeed, EncoderLinePerInch);//20200803修改：已包含运动修正系统//double nSpeed = 68016;

            CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
            if ((double)CurrentPos / EncoderLinePerMM >= AimPos)//墨车在清洗站台右侧
            { DirFlag = false; }
            else//墨车在清洗站台左侧
            { DirFlag = true; }

            if (AimPos >= 0 && AimPos <= 1230)//是否AimPos在工作流程内:在流程内，即可打印:确认在安全工作区内
            {
                double MoveStep = (AimPos - (double)CurrentPos / EncoderLinePerMM) * 500;//
                bool nRetVal = royal.royal.DEM_Run(0, DirFlag, (UInt32)nSpeed, (int)System.Math.Abs(MoveStep), nCtlValue);//三菱驱动器的千脉冲MM数：按照之前代码，应该是500;运行100MM;单pulse-2um                                                              
                //do//20200628新增：确保主运动到打印结束区域：不停运动监控，until打印结束，才执行下次打印
                //{
                //    bool returnCode4 = royal.royal.IDP_GetPrintState(ref RTinfo); //获取打印运行状态，用来判断当前打印的状态//20200429新增：
                //    Thread.Sleep(20);//20200411：间隔1ms再监测是否运动结束
                //} while (!RTinfo.bLayerPrtIsOver);
                //这个问题终于找到了，坑死了
                //while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls))//int SleepTime = (int)((double)(AimPos - CurrentPos) / nSpeed);//Thread.Sleep(5000);//Sleep时间必须要有依据//确保运行到位，运行精度为2UM
                //{
                //    Thread.Sleep(20);
                //}
                //uint ny2pos;
                //if (DirFlag == false)
                //{
                //    do//20200628新增：确保主运动到打印结束区域：不停运动监控，until打印结束，才执行下次打印
                //    {
                //        bool returnCode3 = royal.royal.IDP_GetPrintState(ref RTinfo); //获取打印运行状态，用来判断当前打印的状态//20200429新增：
                //        Thread.Sleep(20);//20200411：间隔1ms再监测是否运动结束
                //    } while (!RTinfo.bLayerPrtIsOver);

                //    do//缓冲主运动：判断你是否到达缓冲区边缘位置：
                //    {
                //        ny2pos = royal.royal.DEV_GetPrintEncoderValue();//20200306新增：编码器位置设置
                //        Thread.Sleep(20);//20200411：间隔1ms再监测是否运动结束
                //    } while (ny2pos >= 1184 * 200);//两端距离，分别是：25 MM和1160MM；其中10000是缓冲区长度，长度为50m                }
                //}
                //else
                //{
                //    do//20200628新增：确保主运动到打印结束区域：不停运动监控，until打印结束，才执行下次打印
                //    {
                //        bool returnCode3 = royal.royal.IDP_GetPrintState(ref RTinfo); //获取打印运行状态，用来判断当前打印的状态//20200429新增：
                //        Thread.Sleep(20);//20200411：间隔1ms再监测是否运动结束
                //    } while (!RTinfo.bLayerPrtIsOver);

                //    do//缓冲主运动：判断你是否到达缓冲区边缘位置：
                //    {
                //        ny2pos = royal.royal.DEV_GetPrintEncoderValue();//20200306新增：编码器位置设置
                //        Thread.Sleep(20);//20200411：间隔1ms再监测是否运动结束
                //    } while ((ny2pos <= 46 * 200));//两端距离，分别是：25 MM和1160MM；其中10000是缓冲区长度，长度为50m
                //}
                //bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
            }
            else//不在安全工作区内:不进行运动
            {
            }
        }
        /***********************************数据传输方法：现在采用********************************/
        /***********************************数据传输方法：现在采用********************************/
        /// <summary>
        /// 传输数据测试;传输BMP格式，载入1层的BMP数据//20200409批注：内存中的bmp文件的存储方式是从上到下，从左到右；BMP文件的存储方式是从下到上，从左到右；           
        /// </summary>
        private int WriteImgLayerData(string jobPathName, int index, int PrtDirFlag, bool SpreadPowerFlagDir)//必须放在1个独立的线程中//文件的本质就是保存在HD的字节流；适配 Swath：整图分割为条带后按条发送
        {
            Log4Net.Info($"写入图层数据：开始（将进行Swath图形分割），path={jobPathName}，层索引={index}");
            System.Drawing.Bitmap processedBitmap;
            try
            {
                processedBitmap = new System.Drawing.Bitmap(jobPathName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("载入图层图像失败：" + ex.Message);
                return -1;
            }
            if (processedBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format1bppIndexed)
            {
                MessageBox.Show("目前不支持非单点图像的打印");
                processedBitmap.Dispose();
                return -1;
            }

            // 图形分割：流式分割，逐条发送并立即释放条带，降低大图内存峰值
            royal.royal.g_prtimg_layer.nXEncOff = 1;
            royal.royal.g_prtimg_layer.nXDPI = (int)g_SharpControl.XDpi;
            royal.royal.g_prtimg_layer.nYDPI = (int)g_SharpControl.RenderDpiY;
            royal.royal.g_prtimg_layer.nLayerIndex = index;
            royal.royal.g_prtimg_layer.nColorCnts = 1;
            royal.royal.g_prtimg_layer.nPrtFlag = 1;
            royal.royal.g_prtimg_layer.nPrtDir = 0;

            // 扫描模式：整层发送前发 STARTJOB，每条带前 STARTSCAN、条带后 ENDDOC，整层后 ENDJOB
            MeteorPrintEngine.SendStartJob(0, (uint)processedBitmap.Width);
            int stripIndex = 0;
            int nRet = SwathImageSplitter.SplitLayerToSwathStripsAndProcess(processedBitmap, (strip, swathTop) =>
            {
                royal.royal.g_prtimg_layer.nPrtDir = (stripIndex % 2 == 0) ? 1 : 0; // 偶数为正向，奇数为反向
                stripIndex++;
                royal.royal.g_prtimg_layer.nYJetOff = swathTop;

                System.Drawing.Rectangle rectStrip = new System.Drawing.Rectangle(0, 0, strip.Width, strip.Height);
                System.Drawing.Imaging.BitmapData bmpData = strip.LockBits(rectStrip, System.Drawing.Imaging.ImageLockMode.ReadOnly, strip.PixelFormat);
                int bytes = Math.Abs(bmpData.Stride) * strip.Height;
                int stride = bmpData.Stride;
                byte[] rgbValues = new byte[bytes];
                Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
                for (int counter = 0; counter < bytes; counter++)
                    rgbValues[counter] = (byte)~(rgbValues[counter]);
                strip.UnlockBits(bmpData);

                int size2 = Marshal.SizeOf(rgbValues[0]) * rgbValues.Length;
                IntPtr ImgPtr = Marshal.AllocHGlobal(size2);
                try
                {
                    Marshal.Copy(rgbValues, 0, ImgPtr, rgbValues.Length);
                    royal.royal.g_prtimg_layer.nBytesPerLine = stride;
                    royal.royal.g_prtimg_layer.nWidth = strip.Width;
                    royal.royal.g_prtimg_layer.nHeight = strip.Height;

                    int ret = MeteorPrintEngine.WriteImageLayer(ref royal.royal.g_prtimg_layer, ImgPtr, bytes);
 // 当前 swath 结束
                    if (ret <= 0)
                    {
                        switch (ret)
                        {
                            case -110000:
                                MessageBox.Show("作业启动失败：指定图层打印执行时的PASS总数");
                                break;
                            case -110001:
                                MessageBox.Show("作业启动失败：PC内存不足");
                                break;
                            case -110002:
                                MessageBox.Show("作业启动失败：PASS计算小于0");
                                break;
                            case -200102:
                                MessageBox.Show("作业启动失败：Meteor命令空间不足或等待超时");
                                break;
                        }
                        return ret;
                    }
                    return 2;
                }
                finally
                {
                    Marshal.FreeHGlobal(ImgPtr);
                }
            }, 0, -1);

            MeteorPrintEngine.SendEndJob(); // 扫描模式：整层条带发送完毕
            processedBitmap.Dispose();
            return nRet;
        }
        private bool LoadAutoParamsFromJson(ref 手动操作 AutoPrintMotion)
        {
            //手动操作 AutoPrintMotion = new 手动操作(0, nValveStateMask);//不必要在此处构造：20201030新增
            //（1）传递必要的参数到非显示模态类
            AutoPrintMotion.k_dJourney = g_cPrinterSysParam.g_dJourney;
            AutoPrintMotion.k_bInitRoyalSuccess = m_bInitRoyalSuccess;
            AutoPrintMotion.InkCarHomeFlag = InkCarHomeFlag;//20200627批注：
            AutoPrintMotion.PowderCarHomeFlag = PowderCarHomeFlag;//20200627批注：默认反向
            AutoPrintMotion.m_bInkSuppy = g_bAutoSupplyInkFlag;
            AutoPrintMotion.RollerDirectionFlag = g_bRollerDirectionFlag;//
            AutoPrintMotion.CorrectFlag = g_bSystemCorrectFlag;//20201014新增：系统校准标志位
            AutoPrintMotion.k_nCurrentLayer = g_nCurrentLayer;//20201021新增：同步打印进度
            //（2）加载自动供给送粉的配置文件
            bool returnCode = AutoPrintMotion.LoadJsonFile(false);//加载自动供给送粉的配置文件
            return returnCode;
        }


        /// <summary>
        /// 自动运行动作罗辑
        /// </summary>
        /// <param name="index"></param>
        /// <param name="Command"></param>
        /// <param name="PassIndex"></param>
        /// <param name="m_MovSpeed"></param>
        /// <param name="m_BackCleanMovSpeed"></param>
        /// <param name="toCamera"></param>
        /// <param name="RecordLayerIndex"></param>
        /// <param name="RecordProcessIndex"></param>
        /// <param name="PauseFlag"></param>
        /// <param name="YJetOffWidth"></param>
        /// <param name="NotGoCleanStationFlag"></param>
        /// <param name="YJetBaseOffWidth"></param>
        private void EquipmentMotionLogic3(int index, int Command, int PassIndex, float m_MovSpeed, float m_BackCleanMovSpeed, ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex, int PauseFlag, double YJetOffWidth, int NotGoCleanStationFlag, double YJetBaseOffWidth)//20220524新增：PassIndex指示当前打印PASS序号
        {
            try
            {
                string msg = $"进入：EquipmentMotionLogic3=》准备创建对象-手动操作！";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

                /*手动操作*/
                Log4Net.Info("EquipmentMotionLogic3: about to construct AutoPrintMotion3 (手动操作).");
                if (AutoPrintMotion3 == null)
                {
                    AutoPrintMotion3 = new 手动操作(0, nValveStateMask, false);//20230317修正:修正潜在的闪退问题
                    Log4Net.Info("EquipmentMotionLogic3: AutoPrintMotion3 constructed.");
                    msg = $"进入：EquipmentMotionLogic3=》初次创建完成-手动操作！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }
                else
                {
                    msg = $"进入：EquipmentMotionLogic3=》不需重新创建-手动操作！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }

                if (AutoPrintMotion2 != null)//20230317批注：AutoPrintMotion2:在初始化设备时候，建立与温度控制器之间的通讯联系
                {
                    AutoPrintMotion3.modbusCommunicateMap = AutoPrintMotion2.modbusCommunicateMap/*.Clone()*/;
                    AutoPrintMotion3.InitModbusFlag = AutoPrintMotion2.InitModbusFlag;
                    msg = $"进入：EquipmentMotionLogic3=》赋值完成自-AutoPrintMotion2！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }

                //（1）传递必要的参数到非显示模态类
                AutoPrintMotion3.k_dJourney = g_cPrinterSysParam.g_dJourney;
                AutoPrintMotion3.k_bInitRoyalSuccess = m_bInitRoyalSuccess;
                AutoPrintMotion3.InkCarHomeFlag = InkCarHomeFlag;//20200627批注：
                AutoPrintMotion3.PowderCarHomeFlag = PowderCarHomeFlag;//20200627批注：默认反向
                AutoPrintMotion3.m_bInkSuppy = g_bAutoSupplyInkFlag;
                AutoPrintMotion3.RollerDirectionFlag = g_bRollerDirectionFlag;//
                AutoPrintMotion3.CorrectFlag = g_bSystemCorrectFlag;//20201014新增：系统校准标志位
                AutoPrintMotion3.k_nCurrentLayer = g_nCurrentLayer;//20201021新增：同步打印进度
                //（2）加载自动供给送粉的配置文件
                bool returnCode = AutoPrintMotion3.LoadJsonFile(false);//加载自动供给送粉的配置文件
                msg = $"进入：EquipmentMotionLogic3=》加载配置文件-LoadJsonFile成功！";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                Log4Net.Info($"EquipmentMotionLogic3: 入口状态，Command={Command}，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}，AutoPrintMotion3={(AutoPrintMotion3 == null ? "null" : "ok")}");

                if (手动操作.UseSimpleTestMotion)
                {
                    if (Command == 4 || Command == 5 || Command == 6 || Command == 7)
                    {
                        Log4Net.Info($"EquipmentMotionLogic3: 简易测试运行时旁路生效，Command={Command}，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                        if (AutoPrintMotion3 == null)
                        {
                            Log4Net.Info($"EquipmentMotionLogic3: 简易测试运行时旁路失败，AutoPrintMotion3为空，Command={Command}，PassIndex={PassIndex}");
                            return;
                        }

                        AutoPrintMotion3.RunSimpleTestMotionRuntimeStep(PassIndex);
                        Log4Net.Info($"EquipmentMotionLogic3: 简易测试运行时旁路完成，Command={Command}，PassIndex={PassIndex}");
                        return;
                    }

                    if (Command == 1 || Command == 2 || Command == 3)
                    {
                        Log4Net.Info($"EquipmentMotionLogic3: 简易测试模式下跳过辅助运动，Command={Command}，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                string msg = $"对象发生异常，区间位点1：" + e.ToString();
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
            }

            try
            {
                if (Command == 1)//自动清洗逻辑
                {
                    //（3）执行对应的逻辑：
                    AutoPrintMotion3.AutoCleanThread2(m_BackCleanMovSpeed);//20201029:自动固化清洗//20220518修改：修改为自动清洗逻辑
                }
                else if (Command == 2)//自动送粉逻辑
                {
#if false//下送粉逻辑
                AutoPrintMotion.AutoSupplyPowderThread();//20201029:自动进给预送粉
#else//上送粉逻辑
                    //AutoPrintMotion3.NewAutoSupplyPowderThread2/*NewAutoSupplyPowderThread*/(ref toCamera, RecordLayerIndex, RecordProcessIndex);//20201029:自动上送粉//20230114修改：添加新的参数NewAutoSupplyPowderThread2

                    if (AutoPrintMotion3.k_RYSYSParamAutoPrintParamInTest.m_nRecoaterMode == 0)//20230519新增：
                    {
                        AutoPrintMotion3.NewAutoSupplyPowderThread2/*NewAutoSupplyPowderThread*/(ref toCamera, RecordLayerIndex, RecordProcessIndex);//20201029:自动上送粉//20230114修改：添加新的参数NewAutoSupplyPowderThread2
                        string msg = $"铺粉模式：铺粉固化同时进行！";
                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                    }
                    else
                    {
                        AutoPrintMotion3.NewAutoSupplyPowderThread2CureFirst/*NewAutoSupplyPowderThread*/(ref toCamera, RecordLayerIndex, RecordProcessIndex, m_BackCleanMovSpeed);//20240105:在铺粉固化中添加附加清洗逻辑//20201029:自动上送粉//20230114修改：添加新的参数NewAutoSupplyPowderThread2
                        string msg = $"铺粉模式：固化结束再开启铺粉！";
                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                    }
#endif
                }
                else if (Command == 3)//自动固化逻辑
                {
                    AutoPrintMotion3.AutoCureThread();//20201029:自动上送粉

                }
                else if (Command == 4)//20220524新增：自动喷墨逻辑,采用双PASS方式打印，第1PASS打印逻辑
                {
                    Log4Net.Info($"EquipmentMotionLogic3: 进入Command=4分支，准备调用AutoPrintThread2，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3.AutoPrintThread2(1, PassIndex, m_MovSpeed, m_BackCleanMovSpeed, ref toCamera, RecordLayerIndex, RecordProcessIndex, PauseFlag, YJetOffWidth, NotGoCleanStationFlag);//
                    Log4Net.Info($"EquipmentMotionLogic3: 完成调用AutoPrintThread2，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3?.LogInkCarAxisSnapshot($"EquipmentMotionLogic3: Command=4, PassIndex={PassIndex} 执行后轴快照");
                }
                else if (Command == 5)//20230418新增：自动喷墨逻辑,采用双PASS方式进行打印，第2PASS打印逻辑
                {
                    Log4Net.Info($"EquipmentMotionLogic3: 进入Command=5分支，准备调用AutoPrintThread3，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3.AutoPrintThread3(1, PassIndex, m_MovSpeed, m_BackCleanMovSpeed, ref toCamera, RecordLayerIndex, RecordProcessIndex, PauseFlag, YJetOffWidth, NotGoCleanStationFlag);//
                    Log4Net.Info($"EquipmentMotionLogic3: 完成调用AutoPrintThread3，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3?.LogInkCarAxisSnapshot($"EquipmentMotionLogic3: Command=5, PassIndex={PassIndex} 执行后轴快照");
                }
                else if (Command == 6)//20230418新增：自动喷墨逻辑,采用双PASS方式进行打印，第2PASS打印逻辑
                {
#if TwoPassPrintMode
#if TwoPassPrintPerSixTimes
                    Log4Net.Info($"EquipmentMotionLogic3: 进入Command=6分支(6次)，准备调用AutoPrintThread4，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3.AutoPrintThread4(1, PassIndex, m_MovSpeed, m_BackCleanMovSpeed, ref toCamera, RecordLayerIndex, RecordProcessIndex, PauseFlag, YJetOffWidth, NotGoCleanStationFlag);//
                    Log4Net.Info($"EquipmentMotionLogic3: 完成调用AutoPrintThread4(6次)，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3?.LogInkCarAxisSnapshot($"EquipmentMotionLogic3: Command=6(6次), PassIndex={PassIndex} 执行后轴快照");
#endif
#if TwoPassPrintPerThreeTimes
                    Log4Net.Info($"EquipmentMotionLogic3: 进入Command=6分支(3次)，准备调用AutoPrintThread5，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3.AutoPrintThread5(1, PassIndex, m_MovSpeed, m_BackCleanMovSpeed, ref toCamera, RecordLayerIndex, RecordProcessIndex, PauseFlag, YJetOffWidth, NotGoCleanStationFlag, YJetBaseOffWidth);//20230502新增：YJetBaseOffWidth
                    Log4Net.Info($"EquipmentMotionLogic3: 完成调用AutoPrintThread5(3次)，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3?.LogInkCarAxisSnapshot($"EquipmentMotionLogic3: Command=6(3次), PassIndex={PassIndex} 执行后轴快照");
#endif
#endif
                }
                else if (Command == 7)//20230418新增：自动喷墨逻辑,采用双PASS方式进行打印，第2PASS打印逻辑
                {
                    Log4Net.Info($"EquipmentMotionLogic3: 进入Command=7分支，准备调用AutoPrintThread4，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3.AutoPrintThread4(1, PassIndex, m_MovSpeed, m_BackCleanMovSpeed, ref toCamera, RecordLayerIndex, RecordProcessIndex, PauseFlag, YJetOffWidth, NotGoCleanStationFlag);//
                    Log4Net.Info($"EquipmentMotionLogic3: 完成调用AutoPrintThread4(命令7)，PassIndex={PassIndex}，UseSimpleTestMotion={手动操作.UseSimpleTestMotion}");
                    AutoPrintMotion3?.LogInkCarAxisSnapshot($"EquipmentMotionLogic3: Command=7, PassIndex={PassIndex} 执行后轴快照");
                }
                else
                {
                    ////（1）自动固化-清洗逻辑：
                    //AutoPrintMotion.AutoCureThread();//20201029:自动固化清洗
                    ////（2）1st自动进给送粉逻辑——自动送粉到送粉位置：
                    //AutoPrintMotion.AutoSupplyPowderThread();//20201029:自动进给送粉
                    ////（3）2st自动铺粉逻辑：
                    //AutoPrintMotion.AutoSupplyPowder2Thread();//内部逻辑需要增加供粉缸1逻辑//20220513新建批注：暂时注释
                }
            }
            catch (Exception e)
            {
                string msg = $"对象发生异常，区间位点2：" + e.ToString();
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
            }
        }

        /***********************************传统的运动逻辑12：现在采用********************************/
        /// <summary>
        /// EquipmentMotionLogic2：为本设备的运动逻辑，负责实现3dp设备1层的所有动作：包括送料配合及打印动作：20200411新增
        /// </summary>
        /// <param name="index"></param>//参数：当前运动的层数，用处不是很大：20200411新增
        /// <param name="PrtDirFlag"></param>//参数：墨车运动的方向标志
        /// <param name="SpreadPowerFlagDir"></param>//参数：铺粉运动的方向标志
        private bool EquipmentMotionLogic2(int index, bool PrtDirFlag)//20200411新增：给一写参数即完成//（1）执行打印逻辑任务：20200329批注
        {
            LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();
            if (PrtDirFlag == true)//（a）正向打印情形
            {
                //(A) 逻辑判断：（1）防止缸体之间相互碰到；（2）防止缸体与喷墨小车之间相互碰到//（1-1）PP (1,-,X1);//成型缸电机下降x1mm（PP-）//逻辑判断：也需要：判断是送粉缸粉料不足，即将触碰到限位                                          
                //(B) 逻辑判断：也需要：判断是送粉缸粉料不足，即将触碰到限位//（1-2）PP(2, +, X1);//1号送粉缸电机上升y1mm(PP+)
                //(C) 逻辑判断：也需要：判断是送粉缸粉料不足，即将触碰到限位//（3-2）PP(3, +, X1);//2号送粉缸电机上升y1mm(PP+)
                //(D) Y方向铺粉车运动  

                AutomoveComponent.TrapMoveDown(1, p.m_bMoveModeFlag[0], p.m_Svel[0], p.m_Step[0] / 1000, RollerParam);
                Thread.Sleep(200);
                AutomoveComponent.TrapMoveUp(2, p.m_bMoveModeFlag[1], p.m_Svel[1], p.m_Step[1] / 1000, RollerParam);//外缸上升um;
                Thread.Sleep(200);
                AutomoveComponent.TrapMoveDown(3, p.m_bMoveModeFlag[2], p.m_Svel[2], 1, RollerParam);//里缸下降1mm
                Thread.Sleep(200);

                if (true)//Y方向铺粉车运动：//添加逻辑：//20200220：逻辑判断，必须有，主要是是否到触碰到限位，和双X轴是否存在冲突
                {
                    double[] k_dJourney1 = new double[8];
                    double encPos1 = 0; double encPos2 = 0;
                    k_dJourney1 = AutomoveComponent.GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
                    encPos1 = k_dJourney1[3];

                    AutomoveComponent.TrapMoveUp(4, true, p.m_Svel[3], p.m_Step[3], RollerParam);//20200618替换

#if true//20200628注释
                    do//确保运动到位：1个脉冲不差
                    {
                        k_dJourney1 = AutomoveComponent.GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
                        encPos2 = k_dJourney1[3];
                        Thread.Sleep(100);//20200411：间隔1ms再监测是否运动结束
                    } while ((encPos2 - encPos1) >= p.m_Step[3] * 1000);//两端距离，分别是：50 MM和1164MM；其中10000是缓冲区长度，长度为50m
#endif
                }
                return true;
            }
            else if (PrtDirFlag == false)//（b）负向打印情形
            {
                //(A) 逻辑判断：也需要：判断是成型缸深度不足，即将触碰到限位//（3-1）PP(1, -, X1); //成型缸电机下降x1mm（PP -）
                //(B) 逻辑判断：也需要：判断是送粉缸粉料不足，即将触碰到限位//（3-2）PP(3, +, X1);//2号送粉缸电机上升y1mm(PP+)
                //(C) Y方向铺粉车运动   逻辑判断//20200220：逻辑判断，必须有，主要是是否到触碰到限位，和双X轴是否存在冲突
                //(D) Y方向铺粉车运动  

                AutomoveComponent.TrapMoveDown(1, p.m_bMoveModeFlag[0], p.m_Svel[0], p.m_Step[0] / 1000, RollerParam);
                Thread.Sleep(200);
                AutomoveComponent.TrapMoveUp(3, p.m_bMoveModeFlag[2], p.m_Svel[2], p.m_Step[2] / 1000 + 1, RollerParam);//里缸上升额外的1mm
                Thread.Sleep(200);
                if (true)//铺粉电机逆向回原点运动1次
                {
                    double[] k_dJourney1 = new double[8];
                    double encPos1 = 0; double encPos2 = 0;
                    k_dJourney1 = AutomoveComponent.GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
                    encPos1 = k_dJourney1[3];

                    AutomoveComponent.TrapMoveDown(4, true/*p.m_bMoveModeFlag[3]*/, p.m_Svel[3], p.m_Step[3], RollerParam);//20200618替换

#if true//20200628注释
                    do//确保运动到位：1个脉冲不差
                    {
                        k_dJourney1 = AutomoveComponent.GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
                        encPos2 = k_dJourney1[3];
                        Thread.Sleep(100);//20200411：间隔1ms再监测是否运动结束
                    } while ((encPos1 - encPos2) >= p.m_Step[3] * 1000);//两端距离，分别是：50 MM和1164MM；其中10000是缓冲区长度，长度为50m
#endif
                }
                return true;
            }
            return true;
        }

        /********************************传统的运动逻辑2：现在采用（结束）************************/
        /// <summary>
        /// EquipmentMotionLogic1：为本设备的运动逻辑，负责实现3dp设备1层的所有动作：包括送料配合及打印动作：20200411新增
        /// </summary>
        private void EquipmentMotionLogic1()/***********************************传统的运动逻辑1：之前采用********************************/
        {
            //（0）初始化喷墨系统环境控制，具体包括：下发自动供墨指令、下发设置自动负压指令、下发二级墨盒的温度设置指令、设置墨水的搅拌周期指令
            ConfigureJetEnvironmentControlMode();//配置并生效对应的喷射环境控制模式：20200329新增
            InitCarMotor();//20200327新增：//（1）初始化被控对象及加工任务区间
            ReadLayerInfo();//更新指定的加工任务区间

            //（2）确定准确的墨车运动速度值
            float m_szMovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增
            uint m_unCarMoveSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch);//20200328新增
            g_nCarSinglePassLength = (int)((g_RYSYSParam.m_dCarMoveBufferLength + g_RYSYSParam.m_dPrintAeraLength + g_RYSYSParam.m_dCarMoveBufferLength2) * EncoderLinePerInch);//SinglePass运动距离：20200327新增：

            //（3）执行打印逻辑任务：20200329批注
            LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();
            for (int i = g_nLayerStart/*0*/; i <= g_nLayerEnd/*m_nlayer*//*100*/; i++)
            {
                //逻辑判断：（1）防止缸体之间相互碰到；（2）防止缸体与喷墨小车之间相互碰到//（1-1）PP (1,-,X1);//成型缸电机下降x1mm（PP-）//逻辑判断：也需要：判断是送粉缸粉料不足，即将触碰到限位                                          
                AutomoveComponent.TrapMoveDown(1, p.m_bMoveModeFlag[0], p.m_Svel[0], p.m_Step[0], RollerParam);
                Thread.Sleep(100);
                //逻辑判断：也需要：判断是送粉缸粉料不足，即将触碰到限位//（1-2）PP(2, +, X1);//1号送粉缸电机上升y1mm(PP+)
                AutomoveComponent.TrapMoveUp(2, p.m_bMoveModeFlag[1], p.m_Svel[1], p.m_Step[1], RollerParam);
                Thread.Sleep(100);
                if (true)//Y方向铺粉车运动：//添加逻辑：
                {
                    //20200220：逻辑判断，必须有，主要是是否到触碰到限位，和双X轴是否存在冲突//（1-3）JOG(4, +, V1);//铺粉电机正向回原点运动1次(JOG+)——————//20200228:刮刀运动_正向回原点
                    AutomoveComponent.MoveHome(4, p.m_Svel[3], p.m_bEnds[3], RollerParam);
                    Thread.Sleep(100);
                }
                if (true)//(2)执行SinglePass打印过程：//X方向墨车运动//运行参数主要有2个：（1）运行速度（2）运行距离      
                {
                    bool nRetVal = royal.royal.DEM_Run(0, true, m_unCarMoveSpeed/*1000*/, g_nCarSinglePassLength, 0/*2*//*3*//*2*/);//再来1次，50mm的运动//3为不等停和双Y同步
                    while (royal.royal.DEM_AxisIsRuning(0, ref g_bDir, ref g_nRevPls))//批注：返回的值为寄存器的方向及剩余脉冲值；与寄存器无关，
                    {
                        //(c)实时的轴限位状态
                        uint nIOState = royal.royal.DEM_GetAxisLmtZeroState(0);//底层接口已经作了12 bit移位处理，对照Reg[12]定义——————世彪批注：获取限位状态20200106
                        if ((0 != (nIOState & 0x2)))//如果碰到正限位或者负限位，则停止轴的运动，停止轴的运动，才能轴的状态为0
                        {
                            nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                        }
                        Thread.Sleep(1);//不停的等待1ms，检测双X轴电机是否到位：20200327新增：
                    }
                }

                //逻辑判断：也需要：判断是成型缸深度不足，即将触碰到限位//（3-1）PP(1, -, X1); //成型缸电机下降x1mm（PP -）
                AutomoveComponent.TrapMoveDown(1, p.m_bMoveModeFlag[0], p.m_Svel[0], p.m_Step[0], RollerParam);
                Thread.Sleep(100);
                //逻辑判断：也需要：判断是送粉缸粉料不足，即将触碰到限位//（3-2）PP(3, +, X1);//2号送粉缸电机上升y1mm(PP+)
                AutomoveComponent.TrapMoveUp(3, p.m_bMoveModeFlag[2], p.m_Svel[2], p.m_Step[2], RollerParam);
                Thread.Sleep(100);

                //逻辑判断//20200220：逻辑判断，必须有，主要是是否到触碰到限位，和双X轴是否存在冲突
                if (true)
                {
                    //（3-3）JOG(4, -, V2);//铺粉电机逆向回原点运动1次(JOG-）
                    AutomoveComponent.MoveHome(4, p.m_Svel[3], !p.m_bEnds[3], RollerParam);//——————//20200228:刮刀运动_负向回原点
                    Thread.Sleep(100);
                }
                if (true)
                {
                    ////(4)执行SinglePass返回打印过程：
                    bool nRetVal = royal.royal.DEM_Run(0, false, m_unCarMoveSpeed/*1000*//*(UInt32)g_RYSYSParam.CarMoveSpeed*//*1000*/, g_nCarSinglePassLength, 0/*2*//*3*//*2*/);//再来1次，50mm的运动//3为不等停和双Y同步
                    while (royal.royal.DEM_AxisIsRuning(0, ref g_bDir, ref g_nRevPls))//（0）异常原因，在于运动行程超越了运动范围；（1）轴在运动，监测的是寄存器的一个脉冲输出，脉冲是否发送完；（2）如果限位之后，脉冲还是在计算输出的，只是FPGA对脉冲的输出进行了中断处理
                    {
                        //(c)实时的轴限位状态
                        uint nIOState = royal.royal.DEM_GetAxisLmtZeroState(0);//底层接口已经作了12 bit移位处理，对照Reg[12]定义——————世彪批注：获取限位状态20200106
                        if ((0 != (nIOState & 0x1)))//（a）如果碰到正限位或者负限位，则停止轴的运动，停止轴的运动，才能轴的状态为0;(B)
                        {
                            nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                        }
                        Thread.Sleep(1);//不停的等待1ms，检测双X轴电机是否到位：20200327新增：
                    }
                }
                /////20200228临时注释：测试联动
                g_nLayerCurrent = i;
            }
            //（4）自然执行完毕，自然结束打印区间任务
            //（4）自然执行完毕，针对主界面进行对应的维护
            DeleteThread("PrintTaskTHREAD");//20200220：本线程结束，需要及时清理相关线程
            if (LayerEnd.InvokeRequired == true)//此位置严格来说执行不到：20200313新增批注：//应该放置到DeleteThread之前的位置
            {
                LayerEnd.BeginInvoke(
                    new Action(() =>
                    {
                        this.LayerEnd.Enabled = true;//恢复控件操作
                        this.LayerStart.Enabled = true;//恢复控件操作
                    }
                    ));
            }
        }
        /********************************传统的运动逻辑1：之前采用（结束）************************/
        double RollerParam = 2;//20200918新增：铺粉辊子同双驱速度之比:对于铺粉运动很关键
        //public double[] m_dScrapervel = new double[2]{ 150, 150 };//20200918新增：刮墨主运动、刮墨副运动速度
        //public bool[] m_bScraperEnds = new bool[2]{ false, false};//20200918新增：刮墨主运动、刮墨副运动AB端定义
        ////public double[] m_Step = new double[2]{ 150, 450};//20200918新增：刮墨主运动、刮墨副步进距离
        ////public bool[] m_bMoveModeFlag = new bool[2]{ true, true};//20200918新增：刮墨主运动、刮墨副运动类型
        ///// <summary>
        ///// 20200918新增：刮墨动作逻辑，耦合刮墨主运动和刮墨副运动的运动逻辑
        ///// </summary>
        //private void ScraperMotionLogic()//20200918新增：
        //{
        //    LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();
        //    AutomoveComponent.MoveHome(7, m_dScrapervel[0], m_bScraperEnds[0], RollerParam);//GOHOME动作2：B端运动到A端//20200220：逻辑判断，必须有，主要是是否到触碰到限位，和双X轴是否存在冲突
        //    AutomoveComponent.MoveHome(8, m_dScrapervel[1], m_bScraperEnds[1], RollerParam);//GOHOME动作2：C端运动到D端

        //    Thread.Sleep(2000);

        //    AutomoveComponent.MoveHome(8, m_dScrapervel[0], !m_bScraperEnds[0], RollerParam);//GOHOME动作3：D端运动到C端
        //    AutomoveComponent.MoveHome(7, m_dScrapervel[1], !m_bScraperEnds[1], RollerParam);//GOHOME动作4：A端运动到B端
        //}


        //20200329新增批注：所有这个功能通过调用8个API可以完美的实现：但是还需要和小余导出的DLL的.h来再次确认////DEV、DDR、MCU、IDP、DBG的确切含义
        /// <summary>
        /// 配置喷射环境控制模式并生效：20200329新增:具体包括：下发自动供墨指令、下发设置自动负压指令、下发二级墨盒的温度设置指令、设置墨水的搅拌周期指令
        /// </summary>
        private void ConfigureJetEnvironmentControlMode()//必须结合《royal3DP测试软件DEMO》及最新的洪哥的DLL及head信息来确定//需要更新本项目的《4-royal.cs》文件，完成本部分的代码工作
        {
            if (DisableRoyalPrintRuntimeInit)
            {
                Log4Net.Info("Meteor print mode: skip ConfigureJetEnvironmentControlMode royal initialization.");
                return;
            }

            bool nRetVal;
            //（1）PPCB的ADIB参数设置（最重要的1个逻辑实现）：20200329新增
#if false//20200602批注：每次打印前，设置ADIB是非必要的;统一设置1次即可
            uint uIoOption = 0;
            bool m_bSetEnabled = false;
            //ADIB状态   nOption定义： bit[0] 版本 bit[1] 温度 bit[2] 负压 bit[3] 输入 bit[4] 电压 bit[8] 回读I2C负压 bit[16] 保持到I2C 
            nRetVal = royal.royal.DEV_AdibControl(ref royal.royal.g_sys_param.adibParam, uIoOption, true, ref m_bSetEnabled);//设置气压板的所有参数值：负压、正压、二级墨盒温度
#endif
#if false//20200627注释掉及批注：隶属于打印维护，没有必要在自动打印流程中执行，添加新的管理线程即可完成修改
            //（2）设置清洗泵及压墨输出
            //nRetVal = royal.royal.DEV_SetInkPump();//设置压墨输出 bit[0]~bit[1]  P1~P2		J28    //手动控制主板的墨量输出端口（上侧）：供墨
            bool[] m_bUsbOutPut = new bool[12];//车头板10个电磁阀的输出状态。
            int nValidMask = 0;
            if (m_bUsbOutPut[6])
                nValidMask |= 0x1;//第1位输出：P1
            if (m_bUsbOutPut[7])
                nValidMask |= 0x2;//第2位输出：P2
            nRetVal = royal.royal.DEV_SetInkPump((uint)nValidMask);
#endif
            //（3）使能自动供墨：可以封装成1个函数
            //nRetVal = royal.royal.DEV_EnableInkAutoSupply(true, 0);//使能自动供墨：根据车头板的电气墨量信号输入情况，来自动实现主板供墨接口的输出（上侧）：供墨
            bool m_bInkSuppy = false;//自动使能供墨标志位
            uint ControlBit = 0xFF/*1*//*0xFF*//*0xFF*/;//20200603批注：与SinglePass实际工作喷头配置有关，ControlBit = 1限制1号喷头工作//方便测试工作：20200417批注//20220601修改：适配新设备
            if (m_bInkSuppy)
            {
                nRetVal = royal.royal.DEV_EnableInkAutoSupply(true, ControlBit/*0xFF*/);//20200603修改：

                string msg = $"开启自动供墨：DEV_EnableInkAutoSupply：ControlBit{{0x{ControlBit:X}}}";
                Log4Net.Info(msg);
            }
            else
            {
                nRetVal = royal.royal.DEV_EnableInkAutoSupply(false, ControlBit /*0*/);//20200603修改：

                string msg = $"关闭自动供墨：DEV_EnableInkAutoSupply：ControlBit{{0x{ControlBit:X}}}";
                Log4Net.Info(msg);
            }
#if false//20220601注释掉：新设备不需要该逻辑
            //（4）设置车头吧的三通电磁阀输出信号及工作
            bool[] m_bMcbOutPut = new bool[10];//车头板10个电磁阀的输出状态：入口
            m_bMcbOutPut[1] = true;//待测试：20200603：只开启第2路喷头
            m_bMcbOutPut[2]/*[0]*/ = true;//20210305新增：双喷头配合打印时，设置2路清洗阀门//造成了大量的漏墨现象、、20210305新建批注：惨痛的教训，一位失误导致的代价
            //m_bMcbOutPut[6] = true;//待测试：20200603：只开启第2路喷头及第7路的正负压，通入负压:第7路的正负压
            
            int/*uint*/ nInkMask = 0;
            for (int i = 0; i < 10; i++)
            {
                if (m_bMcbOutPut[i])
                    nInkMask |= (1 << i);
            }
            nRetVal = royal.royal.DEV_SetMcbOutPut(0, (uint)nInkMask);//0代表是1号卡，1代表是2号卡？？？：20200329批注：还是要确认的。。。
#endif

#if false//20200627注释掉及批注：隶属于打印维护，没有必要在自动打印流程中执行，添加新的管理线程即可完成修改
            //（5）墨水搅拌功能实现：20200329添加
            nRetVal = royal.royal.DEV_SetTimer(0, (float)g_RYSYSParam.BlenderCycleSec/*m_fP11CycleSec*/, (float)g_RYSYSParam.BlenderValidSec/*m_fP11ValidSec*/);//P11口：白墨搅拌及自墨搅拌：。。。。。。？具体用哪些端口比较合适。。。。。。。？目前还缺少对应的接口：联系小余确定即可：20200329新增
#endif                                                                                                                                                   //float m_fP11CycleSec = 0, m_fP11ValidSec = 0;         
#if false//20200603:此处不会用到                                                                                                                                                          //nRetVal = royal.royal.DEV_SetTimer(1, m_fP12CycleSec, m_fP12ValidSec);//白墨搅拌及自墨搅拌：。。。。。。？具体用哪些端口比较合适。。。。。。。？目前还缺少对应的接口：联系小余确定即可：20200329新增                                                                                                                                                                //（5）检测通用输出并发出报警
            uint m_unUsbInput = royal.royal.DEV_GetInput();//获取主板的通用输入输出信号的输入情况？——需要确认才可以

            //（6）主板的通用输出大概率是用不到的
            //nRetVal = royal.royal.DEV_SetUsbOutPut();//设置保留输出 bit[0]~bit[5]  EO1~EO6	J7~J12    //设置主板的墨量通用输出端口（左侧）：备用
            //uint m_unUsbOutput = royal.royal.DEV_GetUsbOutput();//设置保留输出 bit[0]~bit[5]  EO1~EO6	J7~J12    //读取：
#endif
        }

        /****************************************************喷墨打印框架结束****************************************************/
        /****************************************************喷墨打印框架结束****************************************************/
        /****************************************************喷墨打印框架结束****************************************************/
        private int SaveFileAs()
        {//SaveFileAs将弹出一个保存文件对话框
            SaveFileDialog objSaveFile = new SaveFileDialog();
            int result = 0;
            objSaveFile.DefaultExt = "*.cli";//指定扩展名
            objSaveFile.RestoreDirectory = true;
            objSaveFile.Filter = "CLI切片文件(*.cli)|*.cli";//过滤器
            if (objSaveFile.ShowDialog() ==
                DialogResult.OK && objSaveFile.FileName != string.Empty)//判断用户是否单击了OK
            {
                result = 1;
            }
            return result;//返回保存结果
        }
        private int OpenFile()
        {//SaveFileAs将弹出一个保存文件对话框
            int result = 0;
            openFileDialog1.DefaultExt = "*.cli";//指定扩展名
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Filter = "CLI切片文件(*.cli)|*.cli";//过滤器
            if (openFileDialog1.ShowDialog() ==//判断用户是否单击了OK
                DialogResult.OK && openFileDialog1.FileName != string.Empty)
            {
                //richTextBox1.SaveFile//调用SaveFile方法保存为纯文本
                //    (objSaveFile.FileName, RichTextBoxStreamType.PlainText);
                result = 1;
            }
            return result;//返回保存结果
        }
        private UVLightParam g_UVLightParam = new UVLightParam
        { m_nFrequency = 125, m_fPower = 100f, nMinPos = new int[] { 550, 270 }, nMaxPos = new int[] { 1000, 720 } };//20200619批注：集成到自动出光设置
        private bool g_bRollerDirectionFlag = true/*false*/;//20200925新增：默认反向;20201013修改
        private bool g_bSystemCorrectFlag = false;//20201014新增：系统校准标志位
        private bool g_bAutoSupplyInkFlag = false;//20201028新增：自动供墨标志位

        private int g_nRePrintTimes = 1;//20201030新增：重新喷墨次数，默认为1；初始或使用时，从文件加载即可；
        private const bool DisableRoyalPrintRuntimeInit = true;//Meteor替代royal硬件后，跳过旧式打印初始化
        private const bool DisableMonitoringRuntimeInit = true;//监控配置加载可能阻塞打印时，允许跳过

        //public static AutoPrintParamInTest g_RYSYSParamAutoPrintParamInTest = new AutoPrintParamInTest();//存储所有的的JOB参数//非常关键//20200327新建:
        //手动操作 f2 = null;
        private void ManulBtn_Click(object sender, EventArgs e)//手动调试按钮
        {
#if false//20230318新增：调试自动打印逻辑用
            try
            {
                string msg = $"进入：EquipmentMotionLogic3=》准备创建对象-手动操作！";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

                /*手动操作*/
                if (AutoPrintMotion3 == null)
                {
                    AutoPrintMotion3 = new 手动操作(0, nValveStateMask);//20230317修正:修正潜在的闪退问题
                    msg = $"进入：EquipmentMotionLogic3=》初次创建完成-手动操作！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }
                else
                {
                    msg = $"进入：EquipmentMotionLogic3=》不需重新创建-手动操作！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }

                if (AutoPrintMotion2 != null)//20230317批注：AutoPrintMotion2:在初始化设备时候，建立与温度控制器之间的通讯联系
                {
                    AutoPrintMotion3.modbusCommunicateMap = AutoPrintMotion2.modbusCommunicateMap/*.Clone()*/;
                    AutoPrintMotion3.InitModbusFlag = AutoPrintMotion2.InitModbusFlag;
                    msg = $"进入：EquipmentMotionLogic3=》赋值完成自-AutoPrintMotion2！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }

                //（1）传递必要的参数到非显示模态类
                AutoPrintMotion3.k_dJourney = g_cPrinterSysParam.g_dJourney;
                AutoPrintMotion3.k_bInitRoyalSuccess = m_bInitRoyalSuccess;
                AutoPrintMotion3.InkCarHomeFlag = InkCarHomeFlag;//20200627批注：
                AutoPrintMotion3.PowderCarHomeFlag = PowderCarHomeFlag;//20200627批注：默认反向
                AutoPrintMotion3.m_bInkSuppy = g_bAutoSupplyInkFlag;
                AutoPrintMotion3.RollerDirectionFlag = g_bRollerDirectionFlag;//
                AutoPrintMotion3.CorrectFlag = g_bSystemCorrectFlag;//20201014新增：系统校准标志位
                AutoPrintMotion3.k_nCurrentLayer = g_nCurrentLayer;//20201021新增：同步打印进度
                                                                   //（2）加载自动供给送粉的配置文件
                bool returnCode = AutoPrintMotion3.LoadJsonFile(false);//加载自动供给送粉的配置文件

                msg = $"进入：EquipmentMotionLogic3=》加载配置文件-LoadJsonFile成功！";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
            }
            catch (Exception e2)
            {
                string msg = $"对象发生异常，位点1：" + e2.ToString();
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
            }
#else//20230318修改：调试自动打印逻辑用

            string msg = $"进入手动调试子模块！";
            Log4Net.Info(msg);

            //（1）多轴行程值初始化（2）多轴运动值初始化
            ////g_cPrinterSysParam.g_dJourney[0] = 350*1000; g_cPrinterSysParam.g_dJourney[1] = 300*1000; g_cPrinterSysParam.g_dJourney[2] = 300*1000;//20200222：初始化行程//测试使用,移动到InitSystem
            ////g_cPrinterSysParam.g_dJourney[3] = 500*1000; g_cPrinterSysParam.g_dJourney[4] = 10*1000; g_cPrinterSysParam.g_dJourney[5] = 350*1000;//20200222：初始化行程//测试使用,移动到InitSystem

            /*string*/
            msg = $"进入：ManulBtn_Click=》准备创建对象-手动操作！";
            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

            bool PrintJobOnFlag = false;//20230320新增：
            if (((g_TaskThreadSTATE[3] == 1) && (g_TaskThreadSTATE[4] == 1)) || ((g_TaskThreadSTATE[3] == 3) && (g_TaskThreadSTATE[4] == 3)))//20230320新增：
            {
                PrintJobOnFlag = false;//(1)未开启打印任务;(2)此前的打印任务已经结束
            }
            else
            {
                PrintJobOnFlag = true;//已开启打印任务
            }

            手动操作 ManualControl = new 手动操作(0, nValveStateMask, PrintJobOnFlag);//20200718修改：
            msg = $"创建：ManualControl=》创建完成-手动操作！";
            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

            if (AutoPrintMotion2 != null)
            {
                ManualControl.modbusCommunicateMap = AutoPrintMotion2.modbusCommunicateMap/*.Clone()*/;
                ManualControl.InitModbusFlag = AutoPrintMotion2.InitModbusFlag;
                msg = $"加载成功：温控Modbus接口！";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
            }

            ManualControl.k_dJourney = g_cPrinterSysParam.g_dJourney;
            ManualControl.k_bInitRoyalSuccess = m_bInitRoyalSuccess;
            //f.k_UVLightParam = g_UVLightParam;//从手动控制端传回设置的UV灯参数：20200619批注//20201029注释：
            ManualControl.InkCarHomeFlag = InkCarHomeFlag;//20200627批注：
            ManualControl.PowderCarHomeFlag = PowderCarHomeFlag;//20200627批注：默认反向
            ManualControl.m_bInkSuppy = g_bAutoSupplyInkFlag;

            //f.k_RYSYSParamAutoPrintParamInTest = g_RYSYSParamAutoPrintParamInTest;//20201020新增：
            ManualControl.RollerDirectionFlag = g_bRollerDirectionFlag;//
            ManualControl.CorrectFlag = g_bSystemCorrectFlag;//20201014新增：系统校准标志位
            ManualControl.k_nCurrentLayer = g_nCurrentLayer;//20201021新增：同步打印进度

            ManualControl.k_fBackCleanMovSpeed = Convert.ToSingle(g_RYSYSParam.CarBackCleanStationMoveSpeed);//20230404新增：回清洗站速度;

            DialogResult result = ManualControl.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
                g_cPrinterSysParam.g_dJourney = ManualControl.k_dJourney; //20200222：回传行程值及零点位置
                //g_UVLightParam= f.k_UVLightParam;//从手动控制端传回设置的UV灯参数：20200619批注//20201029注释：

                InkCarHomeFlag = ManualControl.InkCarHomeFlag;//20200627批注：
                PowderCarHomeFlag = ManualControl.PowderCarHomeFlag;//20200627批注：
                g_bRollerDirectionFlag = ManualControl.RollerDirectionFlag;//20200925新增：默认方向
                g_bSystemCorrectFlag = ManualControl.CorrectFlag;//20201014新增：系统校准标志位

                g_bAutoSupplyInkFlag = ManualControl.m_bInkSuppy;
                //g_RYSYSParamAutoPrintParamInTest = f.k_RYSYSParamAutoPrintParamInTest;//20201020新增：

                msg = "退出手动调试子模块,已执行修改\r\n" +
                $"==========================================================================";
                Log4Net.Info(msg);
            }
            else if (result == DialogResult.Cancel)//20200222：退出时，什么都不做
            {
                InkCarHomeFlag = ManualControl.InkCarHomeFlag;//20200627批注：
                PowderCarHomeFlag = ManualControl.PowderCarHomeFlag;//20200627批注：

                g_bSystemCorrectFlag = ManualControl.CorrectFlag;//20201014新增：系统校准标志位

                g_bAutoSupplyInkFlag = ManualControl.m_bInkSuppy;
                //g_RYSYSParamAutoPrintParamInTest = f.k_RYSYSParamAutoPrintParamInTest;//20201020新增：

                msg = "退出手动调试子模块，未执行修改\r\n" +
                $"==========================================================================";
                Log4Net.Info(msg);
            }

            if (ManualControl.Timer != null)//20230318修复BUG:定时器必须要手动关闭
            {
                ManualControl.Timer.Enabled = false;
                ManualControl.Timer.Dispose();
            }
            if (ManualControl.Timer3 != null)
            {
                ManualControl.Timer3.Enabled = false;
                ManualControl.Timer3.Dispose();
            }
            if (ManualControl.Timer4 != null)
            {
                ManualControl.Timer4.Enabled = false;
                ManualControl.Timer4.Dispose();
            }
            if (ManualControl.Timer5 != null)
            {
                ManualControl.Timer5.Enabled = false;
                ManualControl.Timer5.Dispose();
            }
            if (ManualControl.TimerMAxis != null)
            {
                ManualControl.TimerMAxis.Enabled = false;
                ManualControl.TimerMAxis.Dispose();
            }
            ManualControl.Dispose();//20230318新增：
#endif
        }

        private void ElecBtn_Click(object sender, EventArgs e)//设备参数
        {
            //SliceSTL(1);
#if true
            设备参数设置 f = new 设备参数设置();
            //f.Show();
            //Application.DoEvents();
            f.ShowDialog();
            //填充参数设置 g = new 填充参数设置();
            //g.ShowDialog();
#endif
        }
        //20230316新增:切片STL文件为CLI格式，并保存
        //struct Vector3D
        //{
        //    public Single X;
        //    public Single Y;
        //    public Single Z;
        //}
        public class Vertex
        {
            public Vertex(Single x, Single y, Single z)
            {
                X = x; Y = y; Z = z;
            }
            public Vertex(/*Single x, Single y, Single z*/)
            {
                //X = x; Y = y; Z = z;
            }
            public Single X { get; set; }
            public Single Y { get; set; }
            public Single Z { get; set; }
        }

        public class Triangle
        {
            public Vertex V1 { get; set; }
            public Vertex V2 { get; set; }
            public Vertex V3 { get; set; }
        }

        public static List<Triangle> ReadSTL(string filename)
        {
            List<Triangle> STLTriangle = new List<Triangle>();
            using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                // Read the 80-byte file header
                reader.ReadBytes(80);

                // Read the 4-byte number of triangles
                byte[] countBytes = reader.ReadBytes(4);
                int count = BitConverter.ToInt32(countBytes, 0);

                // Read each triangle
                for (int i = 0; i < count; i++)
                {
                    // Read the normal vector (not used)
                    reader.ReadBytes(12);//3个法向量

                    // Read the vertex positions
                    Vertex v1 = new Vertex();//3个坐标
                    v1.X = reader.ReadSingle();
                    v1.Y = reader.ReadSingle();
                    v1.Z = reader.ReadSingle();

                    Vertex v2 = new Vertex();//3个坐标
                    v2.X = reader.ReadSingle();
                    v2.Y = reader.ReadSingle();
                    v2.Z = reader.ReadSingle();

                    Vertex v3 = new Vertex();//3个坐标
                    v3.X = reader.ReadSingle();
                    v3.Y = reader.ReadSingle();
                    v3.Z = reader.ReadSingle();

                    // Add the triangle to the list
                    STLTriangle.Add(new Triangle { V1 = v1, V2 = v2, V3 = v3 });

                    ////// Read the 2-byte "attribute count" (not used)
                    //reader.ReadUInt16();
                    reader.ReadBytes(2);
                    ////int count2 = BitConverter.ToInt16(countBytes, 0);
                    ////reader.ReadBytes(count2);
                }
            }

            return STLTriangle;
        }


        static void SliceSTL(Single layerThickness)
        {
            // (1) read STL file
            //List<Vector3D> vertices = new List<Vector3D>();
            string STLFilePath = "E:\\LaserAdd_3DP_Software_220426\\1-3DP主控界面(人机交互模块)\\bin\\x64\\Debug\\打印测试件：GEAR-32T_R2.stl";
            List<Triangle> STLTriangle = ReadSTL(STLFilePath);

            // find model bounds
            // (2) Find model bounds
            Single minX = STLTriangle[0].V1.X/* Single.MaxValue*/;
            Single minY = STLTriangle[0].V1.Y/*Single.MaxValue*/;
            Single minZ = STLTriangle[0].V1.Z/*Single.MaxValue*/;
            Single maxX = STLTriangle[0].V1.X/*Single.MinValue*/;
            Single maxY = STLTriangle[0].V1.Y/*Single.MinValue*/;
            Single maxZ = STLTriangle[0].V1.Z/*Single.MinValue*/;

            foreach (Triangle triangle in STLTriangle)
            {
                if (triangle.V1.X < minX) minX = triangle.V1.X;
                if (triangle.V2.X < minX) minX = triangle.V2.X;
                if (triangle.V3.X < minX) minX = triangle.V3.X;

                if (triangle.V1.Y < minY) minY = triangle.V1.Y;
                if (triangle.V2.Y < minY) minY = triangle.V2.Y;
                if (triangle.V3.Y < minY) minY = triangle.V3.Y;

                if (triangle.V1.Z < minZ) minZ = triangle.V1.Z;
                if (triangle.V2.Z < minZ) minZ = triangle.V2.Z;
                if (triangle.V3.Z < minZ) minZ = triangle.V3.Z;

                if (triangle.V1.X > maxX) maxX = triangle.V1.X;
                if (triangle.V2.X > maxX) maxX = triangle.V2.X;
                if (triangle.V3.X > maxX) maxX = triangle.V3.X;

                if (triangle.V1.Y > maxY) maxY = triangle.V1.Y;
                if (triangle.V2.Y > maxY) maxY = triangle.V2.Y;
                if (triangle.V3.Y > maxY) maxY = triangle.V3.Y;

                if (triangle.V1.Z > maxZ) maxZ = triangle.V1.Z;
                if (triangle.V2.Z > maxZ) maxZ = triangle.V2.Z;
                if (triangle.V3.Z > maxZ) maxZ = triangle.V3.Z;
            }


            // (3) slice model
            // Determine the number of layers
            int numLayers = (int)Math.Ceiling((maxZ - minZ) / layerThickness);

            // Slice the model into closed polylines for each layer
            List<List<List<Vertex>>> slicePolylines = new List<List<List<Vertex>>>();
            List<List<Vertex>> polylines = new List<List<Vertex>>();
            for (int i = 0; i < numLayers; i++)//生成每一层的Polylines
            {
                Single layerZ = minZ + layerThickness * i;
                List<Vertex> layerVertices = new List<Vertex>();
                foreach (Triangle triangle in STLTriangle)
                {
                    // Check if the triangle intersects the current layer
                    if ((triangle.V1.Z <= layerZ && triangle.V2.Z >= layerZ) ||
                        (triangle.V2.Z <= layerZ && triangle.V1.Z >= layerZ))
                    {
                        // Calculate the intersection point with the current layer
                        Single t = (layerZ - triangle.V1.Z) / (triangle.V2.Z - triangle.V1.Z);
                        Single x = triangle.V1.X + t * (triangle.V2.X - triangle.V1.X);
                        Single y = triangle.V1.Y + t * (triangle.V2.Y - triangle.V1.Y);
                        layerVertices.Add(new Vertex(x, y, layerZ));

                        // Check if the other two vertices of the triangle also intersect the current layer
                        if ((triangle.V2.Z <= layerZ && triangle.V3.Z >= layerZ) ||
                            (triangle.V3.Z <= layerZ && triangle.V2.Z >= layerZ))
                        {
                            t = (layerZ - triangle.V2.Z) / (triangle.V3.Z - triangle.V2.Z);
                            x = triangle.V2.X + t * (triangle.V3.X - triangle.V2.X);
                            y = triangle.V2.Y + t * (triangle.V3.Y - triangle.V2.Y);
                            layerVertices.Add(new Vertex(x, y, layerZ));
                        }
                        if ((triangle.V3.Z <= layerZ && triangle.V1.Z >= layerZ) ||
                            (triangle.V1.Z <= layerZ && triangle.V3.Z >= layerZ))
                        {
                            t = (layerZ - triangle.V3.Z) / (triangle.V1.Z - triangle.V3.Z);
                            x = triangle.V3.X + t * (triangle.V1.X - triangle.V3.X);
                            y = triangle.V3.Y + t * (triangle.V1.Y - triangle.V3.Y);
                            layerVertices.Add(new Vertex(x, y, layerZ));
                        }
                    }
                }

                List<List<Vertex>> slicePolylinesList = new List<List<Vertex>>();
                while (layerVertices.Count > 0)
                {
                    List<Vertex> polyline = new List<Vertex>();
                    Vertex currentVertex = layerVertices[0];
                    layerVertices.Remove(currentVertex);
                    polyline.Add(currentVertex);
                    while (true)
                    {
                        // find adjacent vertices
                        Vertex nextVertex = FindAdjacentVertex(currentVertex, layerVertices);
                        if (nextVertex.Equals(currentVertex))
                            break; // no more adjacent vertices, polyline complete
                        polyline.Add(nextVertex);
                        layerVertices.Remove(nextVertex);
                        currentVertex = nextVertex;
                    }
                    slicePolylinesList.Add(polyline);//添加1条多段线
                }

                // Close the polylines
                //List<List<List<Vertex>>> slicePolylines = new List<List<List<Vertex>>>();
                slicePolylines.Add(slicePolylinesList);
            }

            // (4) write polylines to CLI file
            //根据生成的polylines，提取出CLI所需要的全部关键数据，先书写文件头，再书写polyline数据
            string CLIFilePath = "E:\\LaserAdd_3DP_Software_220426\\1-3DP主控界面(人机交互模块)\\bin\\x64\\Debug\\打印测试件：GEAR-32T_R2.cli";
            Single layerthickness = 1;
            CLIWriter cLIWriter = new CLIWriter(CLIFilePath, layerthickness);
            cLIWriter.Write(slicePolylines);

        }
        static Vertex FindAdjacentVertex(Vertex vertex, List<Vertex> vertices)
        {
            foreach (Vertex v in vertices)
            {
                if (v.Equals(vertex))
                    continue;
                double distance = Math.Sqrt(Math.Pow(v.X - vertex.X, 2) + Math.Pow(v.Y - vertex.Y, 2) + Math.Pow(v.Z - vertex.Z, 2));
                if (distance < 0.001)
                    return v;
            }
            return vertex;
        }
        public class CLIWriter
        {
            private const string HeaderStart = "$$HEADERSTART";
            private const string Binary = "$$BINARY";
            private const string Units = "$$UNITS/";
            private const string Version = "$$VERSION/200";
            private const string Label = "$$LABEL/{0},part{0}";
            private const string Date = "$$DATE/";
            private const string Dimension = "$$DIMENSION";
            private const string Layers = "$$LAYERS/";
            private const string HeaderEnd = "$$HEADEREND";

            private readonly string filename;
            private readonly Single layerThickness;

            public CLIWriter(string filename, Single layerThickness)
            {
                this.filename = filename;
                this.layerThickness = layerThickness;
            }

            public void Write(List<List<List<Vertex>>> slicePolylines)
            {
                using (var writer = new BinaryWriter(File.Open(filename, FileMode.Create)))
                {
                    // Write header
                    WriteHeader(writer, slicePolylines.Count);

                    // Write layers
                    for (ushort i = 0; i < slicePolylines.Count; i++)
                    {
                        WriteLayer(writer, i, slicePolylines[i]);
                    }
                }
            }

            private void WriteHeader(BinaryWriter writer, int layerCount)
            {
                // Start header
                writer.Write(Encoding.UTF8.GetBytes(HeaderStart));
                writer.Write((short)0); // Null terminator

                // Binary format
                writer.Write(Encoding.UTF8.GetBytes(Binary));
                writer.Write((short)0); // Null terminator

                // Units
                writer.Write(Encoding.UTF8.GetBytes(Units));
                writer.Write((Single)0.005); // Null terminator

                // Version
                writer.Write(Encoding.UTF8.GetBytes(Version));
                writer.Write((short)0); // Null terminator

                // Label
                string label = string.Format(Label, layerThickness);
                writer.Write(Encoding.UTF8.GetBytes(label));
                writer.Write((short)0); // Null terminator

                // Date
                writer.Write(Encoding.UTF8.GetBytes(Date));
                writer.Write((short)0); // Null terminator

                // Dimension
                writer.Write(Encoding.UTF8.GetBytes(Dimension));
                writer.Write((short)0); // Null terminator

                // Layers
                writer.Write(Encoding.UTF8.GetBytes(Layers));
                writer.Write((short)0); // Null terminator

                // Layer count
                writer.Write(layerCount);

                // End header
                writer.Write(Encoding.UTF8.GetBytes(HeaderEnd));
                writer.Write((short)0); // Null terminator
            }

            private void WriteLayer(BinaryWriter writer, ushort layerIndex, List<List<Vertex>> polylines)
            {
                // Command CI and Layer ID
                writer.Write((byte)128/*0x43*/);
                writer.Write(layerIndex/*(byte)0x49*/);//2字节

                // Command CI
                writer.Write((byte)129/*0x43*/);
                //writer.Write((ushort)0/*(byte)0x49*/);//2字节

                // Part ID
                writer.Write((ushort)0);//2字节

                // Direction
                writer.Write((ushort)0/*(byte)0x43*/);//其实，方向并无所谓
                //writer.Write((byte)0x57);

                //// Polyline count
                //writer.Write(polylines.Count);

                foreach (var polyline in polylines)
                {
                    //// Command PP
                    //writer.Write((byte)0x50);
                    //writer.Write((byte)0x50);

                    // Point count
                    writer.Write((ushort)polyline.Count);

                    // Points
                    foreach (var vertex in polyline)
                    {
                        // X coordinate
                        writer.Write(vertex.X);

                        // Y coordinate
                        writer.Write(vertex.Y);

                        //// Z coordinate
                        //writer.Write(layerIndex * layerThickness); // Calculate Z from layer index and layer thickness
                    }
                }
            }
        }


        bool RipExistFlag = false;//20200508新建：RIP进程存在Flag
        private void 主界面_FormClosing(object sender, FormClosingEventArgs e)//20200224新增：
        {
            String msg = null;

            退出确认 f = new 退出确认();//20200224修改:

            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
                //(1)关闭RoyalPrintCard控制器的XYZ3周
                bool nRetVal = royal.royal.DEM_StopAxisRun(false, 0x7);//同时停止X/Y/Z的运动3轴运动：20200305

                msg = $"软件及设备关停中：墨车所有方向运动确认停止（X,Y,Y2)： bImmeStop{{{false}}},nAxisMask{{0x7}}";
                Log4Net.Info(msg);

                //(2)关闭RoyalPrintCard控制器
                bool ReturnCode = royal.royal.DEV_CloseDevice();

                msg = $"软件及设备关停中：喷墨控制器板卡系统关闭（X,Y,Y2)：DEV_CloseDevice";
                Log4Net.Info(msg);

                if (RipExistFlag == true)
                {
                    SendControlCommand(4, 0);//写入到远程：开启远程RIP工作
                }

                string tempThreadName1 = "PrintTaskTHREAD";
                Thread tempThread1 = PrinterLogicThreads.Where(x => x.Name == (tempThreadName1)).FirstOrDefault();
                string tempThreadName2 = "DataTaskTHREAD";
                Thread tempThread2 = PrinterLogicThreads.Where(x => x.Name == (tempThreadName2)).FirstOrDefault();
                if (tempThread1 != null || tempThread2 != null)//退出失败
                {
                    msg = $"软件及设备关停失败，仍存在打印任务：{{PrintTaskTHREAD,DataTaskTHREAD}}";
                    Log4Net.Info(msg);

                    MessageBox.Show("请新关闭打印任务！");
                    e.Cancel = true;//继续正常退出
                }
                else//退出成功
                {
                    g_SharpControl.DisposeClosing();
                    base.OnClosing(e);

                    e.Cancel = false;//继续正常退出

#if false
                    msg = $"软件及设备关停成功\r\n" +
                        $"==========================================================================";
                    Log4Net.Info(msg);
#else
                    // (1)获取CPU使用率
                    PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    cpuCounter.NextValue();
                    System.Threading.Thread.Sleep(100/*1000*/); // 等待一秒
                    float cpuUsage = cpuCounter.NextValue();//string msg1 = $"CPU使用率：{cpuUsage}%";

                    // (2)获取可用内存大小
                    ComputerInfo info = new ComputerInfo();
                    long freeMemory = (long)info.AvailablePhysicalMemory / 1024 / 1024;//MB

                    // (3)获取硬盘剩余空间
                    string msg3 = null;
                    System.IO.DriveInfo[] drives = DriveInfo.GetDrives();
                    foreach (DriveInfo drive in drives)
                    {
                        if (drive.IsReady)
                        {
                            msg3 = msg3 + $"  盘符 { drive.Name} 的剩余空间：{drive.TotalFreeSpace / 1024 / 1024 / 1024} GB\r\n";
                        }
                    }

                    // (7)获取当前系统的.NET Framework版本
                    string version = Environment.Version.ToString();

                    // (8)获取当前进程所在的盘符
                    string driveLetter = Process.GetCurrentProcess().MainModule.FileName.Substring(0, 1);

                    msg = $"软件及设备关停成功\r\n" +
                        $"==========================================================================\r\n" +
                        $"操作系统:{{{GetOSFriendlyName()}}}\r\n" +
                        $"运行环境:{{ .Net Framework Enviroment Version {version}}}\r\n" +
                        $"盘符：{driveLetter}\r\n" +
                        $"CPU使用率：{{{cpuUsage}%}} ，可用内存大小:{{{freeMemory}MB}}，硬盘剩余空间：\r\n" +
                        $"{msg3}" +
                        $"==========================================================================";
                    Log4Net.Info(msg);
#endif

                }
                //#if true//普通状态退出
                //                //(2)继续退出
                //                e.Cancel = false;//继续正常退出
                //#else//20200719批注：添加退出特效：从四周缩放到中间退出
                //                Win32EffectHelper.Outer2MiddleDisappear(this);//暂时无法使用
                //#endif
            }
            else if (result == DialogResult.Cancel)//20200224：退出时，什么都不做
            {
                //string ProcessesName = "RIP组件.exe";
                //KillProcess(ProcessesName);
                //SendControlCommand(4, 0);//写入到远程：开启远程RIP工作
                msg = $"软件及设备关停中止：手动取消关停";
                Log4Net.Info(msg);
                e.Cancel = true;//取消退出
            }
        }
        /// <summary>
        /// 20200508：关闭进程名称
        /// </summary>
        /// <param name="ProcessesName"></param>
        private void KillProcess(string ProcessesName)//20200508关闭线程:最终没有用到
        {
            foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())//GetProcessesByName(strProcessesByName))
            {
                if (p.ProcessName.ToUpper().Contains(ProcessesName))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(); // possibly with a timeout
                    }
                    catch (Win32Exception e)
                    {
                        MessageBox.Show(e.Message.ToString());   // process was terminating or can't be terminated - deal with it
                    }
                    catch (InvalidOperationException e)
                    {
                        MessageBox.Show(e.Message.ToString()); // process has already exited - might be able to let this one go
                    }
                }
            }
        }

        /*************************************总开关一键启动/关闭策略设置************************************/
        //加载一键启动/关机配置文件：20200112   
        List<ulong> m_ulControlMask = new List<ulong>();//最多存储256条逻辑记录——不再使用数组修饰
        private XElement root;//读取XML的元素及属性值，同时刷新所有的值。
        private XElement OnekeyStart = new XElement("OnekeyStart");//类全局变量：一键启动配置XML文件
        private XElement OutputXML;//读取XML的元素及属性值，同时刷新所有的值。
        private void OnekeyStartLoadXML()//加载一键启动配置文件————相关的转换过程，不应该放在主界面中，主界面就是处理界面逻辑即可
        {
            //(1)读取到内存：
            root = XElement.Load("端口、一键启动配置.xml");
            //(2)查询对应的一键启动策略：
            IEnumerable<XElement> address =
                    from el in root.Descendants("OnekeyStart")
                        //where (string)el.Attribute("序号") == IT1.label.Text
                    select el;
            //(3)解析转换为对应的掩码，存入一键启动数组：
            //(3)PS:按照顺序转换为对应的掩码数组：
            //(3)掩码结构（64位）：序号(8bit,256个)|开始条件判断(8bit，256个+4bit，16个逻辑+8bit，256个)
            //|延时值（16bit,65536s=18.2h）|触发动作(8bit,256种动作)|动作有效状态（2bit,开关4种状态）
            //8+20+16+8+2=54bit————高位填充10bit的Od1111111111。

            //(1)m_ulControlMask = null;初始化之前，清空list<>
            foreach (XElement el in address)
            {
                foreach (XElement al in el.Elements())
                {
                    //string 记录1 = (string)al.Attribute("序号");
                    //string 记录2 = (string)al.Attribute("功能描述");
                    //string[] row = { 记录1, 记录2 };//20200112:关键指令，转换为主界面即可
                    //20200112:加载对应的功能码
                    ulong tempMask = (ulong)al.Attribute("功能码");//20200112:加载对应的功能码
                    m_ulControlMask.Add(tempMask);//20200112:加载对应的功能码
                }
            }
        }
        public List<ulong> m_outputPortConfigure = new List<ulong>();//20200718新增：

        private void OutputLoadXML()//20200718新增：
        {
            //(1)读取到内存：
            OutputXML = XElement.Load("端口、一键启动配置.xml");
            //(2)查询对应的一键启动策略：
            IEnumerable<XElement> address =
                    from el in OutputXML.Descendants("DI")
                    select el;

            //(1)m_ulControlMask = null;初始化之前，清空list<>
            foreach (XElement el in address)
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


                    //m_ulControlMask.Add(tempMask);//20200112:加载对应的功能码
                }
            }
        }

        //20200113:本处逻辑，可以进行大的优化
        private short TriggerLogic(ulong source, ulong logic1, ulong logic2, ulong timeout)
        {
            //bool tempstate = false;//临时，没卵用
            if (((source < 32) && (logic1 <= 1) && (logic2 == 0)) /*|| ((logic1 >= 2) && (logic2 == 1))*/) //20200309:“延时”功能
            {
                return 1;//返回值为1：进一步出发动作 
            }
            else if ((source >= 32) && (logic1 >= 2) && (logic2 == 1))//20200309:"条件执行"功能
            {
                return 2;//返回值为1：进一步立即出发动作 
            }
            else//logic1和logic2存在矛盾关系：logic1取值0和1时，logic2只能为0；logic1为2-6时，logcic2只能为1；其余匹配情况错
            {
                return 4;//返回值为4：指令存在矛盾出错
            }
        }
        //按照说明书最后的框架执行即可，对于可能可能出现的逻辑异常现象，统一抛出Throw携带不同特定信息的同一类异常；//20200311新增，关键:
        //然后，在统一的catch中完成处理，如中断；//20200311新增，关键:
        //最后，再finally中完成相关动作，如开启下1条指令的执行。//20200311新增，关键:
        private void ExcuteACommand(ulong m_ulControlMask)//PS:0为正常，正整数为出错指令编码//重点使用异常处理机制，才能合适的避免这种问题：20200312
        {
            //(1)解析生成的开机策略文件:从File到掩码指令
            //(1)代码请参考设备机电参数设置中的一体化控制策略配置文件：
            //(1)执行对应的掩码指令：从掩码指令解析成动作：使用Sleep可以实现
            ulong source = 0;//8位
            ulong logic1 = 0;//4位——多种逻辑
            ulong logic2 = 0;//8位
            ulong timeout = 0;//16位
            ulong control = 0;//8位
            ulong state = 0;//2位
            //(2)每条指令需要标配对应的提示信息（动作名称及时间信息）
            //20200112：按照技术规格编写对应的掩码值：                                                                                                                //(2)写入到掩码值：
            //ulong tempMask = 0x1FFFC00000000000;//初始MASK，高10bit置1
            //m_ulControlMask[i] = (tempMask)
            //    | ((0x3FC000000000) & (source << 38))
            //    | ((0x3C00000000) & (logic1 << 34))
            //    | ((0x3FC000000) & (logic2) << 26)
            //    | ((0x3FFFC00) & (timeout) << 10)
            //    | ((0x3FC) & (control) << 2)
            //    | ((0x3) & (state) << 0);//写入souce的掩码
            source = ((0x3FC000000000) & m_ulControlMask) >> 38;//触发的逻辑
            logic1 = ((0x3C00000000) & m_ulControlMask) >> 34;//逻辑1和逻辑2是对应的
            logic2 = ((0x3FC000000) & m_ulControlMask) >> 26;//逻辑1和逻辑2是对应的
            timeout = ((0x3FFFC00) & m_ulControlMask) >> 10;//实际数据
            control = ((0x3FC) & m_ulControlMask) >> 2;//DO对象
            state = ((0x3) & m_ulControlMask) >> 0;//输出状态

            try//核心程序语句:20200312新增
            {
                //(3)判断执行对应的功能码：
                //(3)需要提供完善的异常处理机制：
                //(3-1)读取source的状态，并进行前信号状态检测：
                short ReturnLogic = TriggerLogic(source, logic1, logic2, timeout);//检验逻辑是否合理，然后决定是否报警提示
                                                                                  //(3-2)是否执行延时动作：
                if (ReturnLogic == 1) //20200309:“延时”功能
                {
                    //source:3+32+0-1=34==》数字量读取//20200311新增：
                    int DiVoltageValue = m_cGoogolMotionMap.GetDi(16);//读取特定端口的值，需要查阅固高控制器API手册确定

                    switch (logic1)
                    {
                        case 0://停止（无效）//判断输入数据是否满足情况，满足的话，就设置对应的输出端口；否则，就抛出对应的异常，终止循环，break执行过程
                            if (0 <= source && source < 16)//（a）判断依据为DI的状态//20210312修正：此间存在较大的BUG,待完善
                            {
                                //if (/*true*/(DiVoltageValue & (1 << (short)(source))) == 0)//设置对应的DI
                                //{
                                //    Thread.Sleep((int)timeout * 1000);//延时: timeout x 1ms//(1)延时：//Application.DoEvents();//20200112：尽量不使用这种东西，表面难以处理的调试错误
                                //    m_cGoogolMotionMap.SetDo((short)(control+1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                //    m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                                //}
                                //else//抛出异常
                                //{ throw (new PlcInputException("DigitalSignal Exception")); }

                                if (m_cGoogolMotionMap.m_bIoEnable[source/* - 16*/] == false)//DO的状态
                                {
                                    Thread.Sleep((int)timeout * 1000);//延时: timeout x 1ms//(1)延时：//Application.DoEvents();//20200112：尽量不使用这种东西，表面难以处理的调试错误
                                    m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                    m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                                }
                                else//抛出异常
                                { throw (new PlcInputException("DigitalSignal Exception")); }
                            }
                            else if (16 <= source && source < 32)//（b）判断依据为DO的状态
                            {
                                if (m_cGoogolMotionMap.m_bIoEnable[source - 16] == false)//DO的状态
                                {
                                    Thread.Sleep((int)timeout * 1000);//延时: timeout x 1ms//(1)延时：//Application.DoEvents();//20200112：尽量不使用这种东西，表面难以处理的调试错误
                                    m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                    m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                                }
                                else//抛出异常
                                { throw (new PlcInputException("DigitalSignal Exception")); }
                            }
                            else { }
                            break;
                        case 1://开始（有效）
                            if (0 <= source && source < 16)//（a）判断依据为DI的状态//20210312修正：此间存在较大的BUG,待完善
                            {
                                //if (/*true*/(DiVoltageValue & (1 << (short)(source - 16))) != 0)//设置对应的输出端口
                                //{
                                //    Thread.Sleep((int)timeout * 1000);//延时: timeout x 1ms//(1)延时：//Application.DoEvents();//20200112：尽量不使用这种东西，表面难以处理的调试错误
                                //    m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                //    m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                                //}
                                //else//抛出异常
                                //{ throw (new PlcInputException("DigitalSignal Exception")); }

                                if (m_cGoogolMotionMap.m_bIoEnable[source/* - 16*/] == true)//DO的状态
                                {
                                    Thread.Sleep((int)timeout * 1000);//延时: timeout x 1ms//(1)延时：//Application.DoEvents();//20200112：尽量不使用这种东西，表面难以处理的调试错误
                                    m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                    m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                                }
                                else//抛出异常
                                { throw (new PlcInputException("DigitalSignal Exception")); }
                            }
                            else if (16 <= source && source < 32)//（b）判断依据为DO的状态
                            {
                                if (m_cGoogolMotionMap.m_bIoEnable[source - 16] == true)//DO的状态
                                {
                                    Thread.Sleep((int)timeout * 1000);//延时: timeout x 1ms//(1)延时：//Application.DoEvents();//20200112：尽量不使用这种东西，表面难以处理的调试错误
                                    m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                    m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                                }
                                else//抛出异常
                                { throw (new PlcInputException("DigitalSignal Exception")); }
                            }
                            else { }
                            break;
                    }
                }
                else if (ReturnLogic == 2)//20200309:"条件执行"功能，不满足条件，就一直保持待在循环里面,只与模拟量有关
                {
                    //source:3+32+0-1=34==》模拟量位置//20200309新增：
                    double[] AiVoltageValue = m_cGoogolMotionMap.GetAi();//读取特定端口的值，需要查阅固高控制器API手册确定

                    switch (logic1)
                    {
                        case 2://>
                            if (AiVoltageValue[source - 32] > timeout)//设置对应的输出端口
                            {
                                m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                            }
                            else//抛出异常
                            {
                                throw (new PlcInputException("AnalogSignal Exception"));
                            }
                            break;
                        case 3://<
                            if (AiVoltageValue[source - 32] < timeout)//设置对应的输出端口
                            {
                                m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                            }
                            else//抛出异常
                            {
                                throw (new PlcInputException("AnalogSignal Exception"));
                            }
                            break;
                        case 4://=
                            if (AiVoltageValue[source - 32] == timeout)//设置对应的输出端口
                            {
                                m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                            }
                            else//抛出异常
                            {
                                throw (new PlcInputException("AnalogSignal Exception"));
                            }
                            break;
                        case 5://>=
                            if (AiVoltageValue[source - 32] >= timeout)//设置对应的输出端口
                            {
                                m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                            }
                            else//抛出异常
                            {
                                throw (new PlcInputException("AnalogSignal Exception"));
                            }
                            break;
                        case 6://<=
                            if (AiVoltageValue[source - 32] <= timeout)//设置对应的输出端口
                            {
                                m_cGoogolMotionMap.SetDo((short)(control + 1), Convert.ToBoolean(state));//(1)输出：执行指定的动作//20200113:PS:到时候的被控对象，既包括运动控制器也包括墨路控制器、后续需要大量的优化工作//Output(control, state); 
                                m_cGoogolMotionMap.m_bIoEnable[control] = Convert.ToBoolean(state);
                            }
                            else//抛出异常
                            {
                                throw (new PlcInputException("AnalogSignal Exception"));
                            }
                            break;
                    }
                }
                else
                {
                    throw (new PlcInputException("LogicSetFailed Exception"));
                }
            }
            catch (PlcInputException e) //自定义几类异常：（1）数字输入异常；（2）模拟输入异常；（3）逻辑本身异常；20200312
            {
                //Console.WriteLine("PlcInputException: {0}", e.Message);
                //MessageBox.Show("PlcInputException: {0}", e.Message);

                switch (e.Message)//3种不同异常的处理机制：20200312新增
                {
                    case "DigitalSignal Exception"://DI和DO输入的条件不满足,后果不是很严重，忽略即可：
                        //记录异常即可

                        break;
                    case "AnalogSignal Exception"://AI输入的条件比较重要，必须处理：
                        //重新执行本条指令，如：某项模拟量非常关键，必须要满足模拟量才可以开关，可能需要重新输入1遍

                        break;
                    case "LogicSetFailed Exception"://逻辑判断不太重要，忽略即可
                        //记录异常即可
                        break;
                }
            }
            //finally { }//finally非必须，对于异常处理机制而言：20200312
        }
        public class PlcInputException : ApplicationException//新定义的异常类//20200312新增：
        {
            public PlcInputException(string message) : base(message) { }
        }
        //20201021新增：线程管理的案发现场，只要是相应的线程我就存储在这里，不管线程是死是活，祖祖辈辈就在这里，便于维护及管理
        private List<Thread> AutoPrintThreads = new List<Thread>();//20201021新增：线程管理的案发现场
        public void DeleteAutoPrintThread(string ThreadName)
        {
            string tempThreadName = ThreadName;
            Thread tempThread = AutoPrintThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
            if (tempThread != null)
            {
                tempThread.Abort();//20200221修改:当调用非托管线程时，有时会抛出异常但不一定及时停止
                while (tempThread.ThreadState != System.Threading.ThreadState.Aborted)
                { Thread.Sleep(100); }
                AutoPrintThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
            }
        }
        private bool CreateAndDeleteThread(string AimThreadName, List<Thread> AutoPrintThreadsPool, bool OpenCloseFlag)
        {
            if (OpenCloseFlag)//未在自动固化清洗//开启对应线程
            {
                string tempThreadName = /*"AutoCureThread"*/AimThreadName;
                Thread tempThread = /*AutoPrintThreads*/AutoPrintThreadsPool.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    /*AutoPrintThreads*/
                    AutoPrintThreadsPool.Remove(tempThread);//以防万一
                    return false;
                }
                else
                {
                    ThreadStart initThreadEntry = null;
                    if (tempThreadName == "AutoStartThread")
                    {
                        initThreadEntry = new ThreadStart(AutoStartThread);
                    }//20200220:线程入口方法修改为联动线程
                    //else if (tempThreadName == "AutoSupplyPowderThread") { initThreadEntry = new ThreadStart(AutoSupplyPowderThread); }
                    //else if (tempThreadName == "AutoCleanThread") { initThreadEntry = new ThreadStart(AutoCleanThread); }
                    else { return false; }

                    tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    tempThread.Name = tempThreadName;
                    tempThread.Start();
                    /*AutoPrintThreads*/
                    AutoPrintThreadsPool.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                    return true;
                }
            }
            else//正在自动固化清洗//关闭对应线程
            {
                string tempThreadName = AimThreadName;//(1)关闭联调线程
                DeleteAutoPrintThread(tempThreadName);
                return true;
            }
        }
        private void AutoStartThread()//线程内容：自动固化清洗
        {
            if (m_bMasterElecSwitch == true)//打开动作：一键启动系统线程 —————— 线程执行开启系列动作
            {
                string msg = "开启系统：一键启动系统开始！";
                Log4Net.Info(msg);

                OnekeyStartLoadXML();//(0)20200113:加载XML文件并解析到m_ulControlMas   
                for (int i = 0; i < m_ulControlMask.Count; i++)//(1)读取对应的功能码：
                {
                    ExcuteACommand(m_ulControlMask[i]);//(2)动作逐条执行：————20200113暂时不测，后续还需要放在新线程中执行。//20200620批注：
                }
                royal.royal.g_sys_param.nParamVer = 0x21080809;//20200326新增//（2）初始化喷墨系统
                royal.royal.g_sys_param.szLogPath = g_RYSYSParam.m_sLogPath;//20200326新增
                //m_bInitRoyalSuccess = g_cRoyalPrint.InitRoyalPrintCard();//20200618批注修改：
                m_bMasterElecSwitch = false;//开启和关闭状态标志位
                PauseStopFlag = false;//20230317新建批注：中断退出关闭标志
                AutoPrintFlag[0] = false;//线程存在标志位：标志着线程结束

                /*string*/
                msg = "开启系统：一键启动系统结束！";
                Log4Net.Info(msg);

                if (AutoPrintMotion0 == null)
                {
                    AutoPrintMotion0 = new 手动操作(0, nValveStateMask, false);//20201030新增：读取自动打印参数//20230317修正:修正潜在的闪退问题
                    msg = $"创建：AutoPrintMotion0=》初次创建完成-手动操作！";
                    Log4Net.Info(msg);
                }
                else
                {
                    msg = $"创建：AutoPrintMotion0=》不需重新创建-手动操作！";
                    Log4Net.Info(msg);
                }
                bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion0);//20230419修改：读取自动打印参数
                AutoPrintMotion0.k_RYSYSParamAutoPrintParamInTest.m_zCurrectLoadWaveName = "null";//20230419修改：复位当前波形名称为NULL
                AutoPrintMotion0.SaveJsonFile();

                UpdateAutoStartInfo();

            }
            else//关闭动作：一键关闭系统 ———— 线程执行关闭系列动作
            {
                bool CloseFlag = CloseConfirm();

                if (CloseFlag == false)//顺利完成关闭动作
                {
                    string msg2 = null;
                    InkCarHomeFlag = false;//20230419新增：标志需要重新校准
                    PowderCarHomeFlag = false;//20230419新增：标志需要重新校准
                    g_bSystemCorrectFlag = false;//20230419新增：标志需要重新校准

                    if (AutoPrintMotion0 == null)
                    {
                        AutoPrintMotion0 = new 手动操作(0, nValveStateMask, false);//20201030新增：读取自动打印参数//20230317修正:修正潜在的闪退问题
                        msg2 = $"创建：AutoPrintMotion0=》初次创建完成-手动操作！";
                        Log4Net.Info(msg2);
                    }
                    else
                    {
                        msg2 = $"创建：AutoPrintMotion0=》不需重新创建-手动操作！";
                        Log4Net.Info(msg2);
                    }
                    bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion0);//20230419修改：读取自动打印参数
                    AutoPrintMotion0.k_RYSYSParamAutoPrintParamInTest.m_zCurrectLoadWaveName = "null";//20230419修改：复位当前波形名称为NULL
                    AutoPrintMotion0.SaveJsonFile();


                    m_bMasterElecSwitch = true;
                    AutoPrintFlag[0] = false;//线程存在标志位：标志着线程结束
                    UpdateAutoStartInfo();
                }
                else//中断退出关闭动作
                {
                    m_bMasterElecSwitch = false;
                    PauseStopFlag = true;//20230317新建批注：中断退出关闭标志
                    AutoPrintFlag[0] = false;//线程存在标志位：标志着线程结束

                    UpdateAutoStartInfo();//20230317新建批注：问题出现在这里：中断退出关闭动作，会导致后续打印过程的紊乱
                }

            }
        }
        bool PauseStopFlag = false;//20230317新建批注：问题出现在这里：中断退出关闭动作，会导致后续打印过程的紊乱

        /// 定义一个代理：加载CLI过程中刷新数据
        private delegate void UpdateAutoStartInfoDelegate(/*int i, string ReadLayerNum*/);
        private bool g_IRControllerOpenCloseState = false;
        //private 手动操作 AutoPrintMotion2 /*= new 手动操作()*/;//20220523新建：与温度控制仪表建立通讯
        private void UpdateAutoStartInfo(/*int i, string ReadLayerNum*/)
        {
            if (this.MasterSwitchBtn.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
            {
                //委托执行内容
                if (m_bMasterElecSwitch == true)//20230317新建：开机流程后处理
                {
                    MasterSwitchBtn.BackgroundImage = Resource.总开关_off_38x38;
                    MasterSwitchBtn.Text = "开启\r\n系统";

                    ShowMoreBtnDialog(m_cGoogolMotionMap);//20200313新增：立即弹出电气状态
                }
                else//20230317新建：关闭流程后处理
                {
                    if (PauseStopFlag == false)
                    {
                        string msg = "初始化喷墨控制器启动！";
                        Log4Net.Info(msg);
                        LoadCorrectionJsonFile();
                        g_RYSYSParamFeedbackInCorrection = k_PrintNozzleHeadConfigure.m_RYSYSParamFeedbackInCorrection;
                        m_bInitRoyalSuccess = g_cRoyalPrint.InitRoyalPrintCard(g_RYSYSParamFeedbackInCorrection);//20200618批注修改：
                        msg = "初始化喷墨控制器成功！";
                        Log4Net.Info(msg);

                        //RecordPressureAndTemperatureAndValtage();//20230322新建：记录喷射系统的温度、电压、气压参数；


                        /*手动操作*/
                        msg = $"进入：EquipmentMotionLogic3=》准备创建对象-手动操作！";
                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

                        AutoPrintMotion2 = new 手动操作(0, nValveStateMask,false);//20220523新建：与温度控制仪表建立通讯

                        msg = $"进入：EquipmentMotionLogic3=》创建完成-手动操作！";
                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
#if false
                        AutoPrintMotion2.InitTemperatureControlCard(true, out g_IRControllerOpenCloseState);//20220523新建：与温度控制仪表建立通讯
#endif
                        AutoPrintMotion2.InitModbusFlag = true;

                        MasterSwitchBtn.BackgroundImage = Resource.总开关_on_38x38;
                        MasterSwitchBtn.Text = "关闭\r\n系统";

                        ShowMoreBtnDialog(m_cGoogolMotionMap);//20200313新增：立即弹出电气状态
                    }
                    else
                    {
                        MasterSwitchBtn.BackgroundImage = Resource.总开关_on_38x38;
                        MasterSwitchBtn.Text = "关闭\r\n系统";

                        string msg = "中止并退出关闭流程！";
                        Log4Net.Info(msg);
                    }

                }
                //ShowMoreBtnDialog(m_cGoogolMotionMap);//20200313新增：立即弹出电气状态
            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpdateAutoStartInfoDelegate DMSGD = new UpdateAutoStartInfoDelegate(UpdateAutoStartInfo);
                this.MasterSwitchBtn.Invoke(DMSGD/*, i, ReadLayerNum*/);
            }
        }

        bool[] AutoPrintFlag = new bool[3] { false, false, false };
        public bool m_bInitRoyalSuccess = false;//20200421新增
        private bool m_bMasterElecSwitch = true;//标志需要执行的动作
        private void MasterSwitchBtn_Click(object sender, EventArgs e)
        {
            string msg = "开启系统：板卡通讯，伺服供电，照明系统，UV/HR固化系统！";
            Log4Net.Info(msg);
#if true
            if (AutoPrintFlag[0] == false)//不存在线程：一键启动关闭线程
            {
                string tempThreadName = "AutoStartThread";
                Thread tempThread = AutoPrintThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    /*AutoPrintThreads*/
                    AutoPrintThreads.Remove(tempThread);//以防万一
                }
                if (m_bMasterElecSwitch == true) { this.MasterSwitchBtn.Text = "开启\r\n当中"; }
                else { this.MasterSwitchBtn.Text = "关闭\r\n当中"; }

                if (true == CreateAndDeleteThread("AutoStartThread", AutoPrintThreads, true))
                {
                    AutoPrintFlag[0] = true;
                }//开启
                else { }
            }
            else//存在线程：
            {
                //if (true == CreateAndDeleteThread("AutoStartThread", AutoPrintThreads, false)) { AutoPrintFlag[0] = false; }//关闭
                //else { }
            }

#else       //旧有的一键启动逻辑
            ////按键控制面板 f = new 按键控制面板();
            ////f.ShowDialog();
            //(a)根据ID反转并存储到对应控件列表
            m_bMasterElecSwitch = !m_bMasterElecSwitch;
            //(b)根据ID反转背景图片
            if (m_bMasterElecSwitch == true)
            {
                //(sender as Control).BackgroundImage = System.Drawing.Image.FromFile("ICON资源/总开关-on-38x38.png"); //总开关-off
                (sender as Control).BackgroundImage = Resource.总开关_on_38x38;
                (sender as Control).Text = "关闭\r\n系统";
            }
            else
            {
                //(sender as Control).BackgroundImage = System.Drawing.Image.FromFile("ICON资源/总开关-off-38x38.png");
                (sender as Control).BackgroundImage = Resource.总开关_off_38x38;
                (sender as Control).Text = "开启\r\n系统";
            }
            ////(c)计算输出
            if (m_bMasterElecSwitch == true)
            {
                //(0)20200113:加载XML文件并解析到m_ulControlMas
                OnekeyStartLoadXML();
                //(1)读取对应的功能码：
                for (int i = 0; i < m_ulControlMask.Count; i++)
                {
                    ExcuteACommand(m_ulControlMask[i]);//(2)动作逐条执行：————20200113暂时不测，后续还需要放在新线程中执行。//20200620批注：
                }
                //（2）初始化喷墨系统
                royal.royal.g_sys_param.nParamVer = 0x21080809;//20200326新增
                royal.royal.g_sys_param.szLogPath = g_RYSYSParam.m_sLogPath;//20200326新增
                //royal.royal.g_sys_param.szWavePath = g_RYSYSParam.m_sWavePath;//20200326新增
                m_bInitRoyalSuccess = g_cRoyalPrint.InitRoyalPrintCard();//20200618批注修改：
                ShowMoreBtnDialog(m_cGoogolMotionMap);//20200312新增：立即弹出电气状态
            }
            else
            {
                bool CloseFlag = CloseConfirm();

                if (CloseFlag)//当取消关闭时：CloseFlag为true时，总开关控件显示及标志位需要反转；20200312：
                {
                    //(a)根据ID反转并存储到对应控件列表
                    m_bMasterElecSwitch = !m_bMasterElecSwitch;
                    //(b)根据ID反转背景图片
                    (sender as Control).BackgroundImage = Resource.总开关_on_38x38;
                    (sender as Control).Text = "关闭";
                }
                else
                {
                    ShowMoreBtnDialog(m_cGoogolMotionMap);//20200313新增：立即弹出电气状态
                }
            };
#endif
        }

        private bool CloseConfirm()
        {
            关闭确认 f = new 关闭确认();//20200224修改:
            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
                string msg = "关闭系统：一键关闭系统开始！";
                Log4Net.Info(msg);

                //AutoPrintMotion2.InitTemperatureControlCard(false, out g_IRControllerOpenCloseState);//20220523新建：与温度控制仪表建立通讯
                //AutoPrintMotion2.InitModbusFlag = false;
                //(2)继续退出
                //e.Cancel = false;//继续正常退出
                //1键关机策略：20200312新增
                //(1)读取对应的功能码：
                for (int i = (m_ulControlMask.Count - 1); i > 0; i--)
                {
                    m_ulControlMask[i] = ((m_ulControlMask[i]) & (0xFFFFFFFFFFFFFC));//输出状态:(0xFFFFFFFFFFFFFC)为各位state为00:最后为各位设置为0：20200312新增
                    ExcuteACommand(m_ulControlMask[i]);//(2)动作逐条执行：————20200113暂时不测，后续还需要放在新线程中执行。
                }

                /*string*/
                msg = "关闭系统：一键关闭系统结束！";
                Log4Net.Info(msg);

                //加载一键启动/关机配置文件：20200112   
                m_ulControlMask.Clear();//最多存储256条逻辑记录——不再使用数组修饰//MessageBox.Show("正在关机处理中，请稍等。。。");//关机策略是类似的
                //(1)关闭RoyalPrintCard控制器
                bool nRetVal = royal.royal.DEM_StopAxisRun(false, 0x7);//同时停止X/Y/Z的运动3轴运动：20200305
                /*string*/
                msg = "喷墨控制器的所有轴输出关闭！";
                Log4Net.Info(msg);
                //(2)关闭RoyalPrintCard控制器
                bool ReturnCode = royal.royal.DEV_CloseDevice();

                msg = "喷墨控制器关闭！";
                Log4Net.Info(msg);
                return false;
            }
            else if (result == DialogResult.Cancel)//20200224：退出时，什么都不做
            {
                //e.Cancel = true;//取消退出
                return true;
            }
            else
            { return true; }
        }

        /*************************************总开关一键启动/关闭策略设置************************************/

        //public 数字输出状态 doState = new 数字输出状态();//020104：传送数据21
        private void button20_Click(object sender, EventArgs e)
        {
            ShowMoreBtnDialog(m_cGoogolMotionMap);//20200312新增：
            //按键控制面板 f = new 按键控制面板(m_cGoogolMotionMap);//下发数据
            //DialogResult result = f.ShowDialog();
            //if (result == DialogResult.OK)
            //{
            //    //问题的根本原因是，c#中类的传输，默认是一级间接（相当于使用了1级间接）：020104
            //    //this.m_cGoogolMotionMap = f.m_mGoogolMotionMap;//回传数据
            //    //Reload();
            //}
            //else if(result== DialogResult.Cancel)
            //{
            //}
        }

        private void ShowMoreBtnDialog(GoogolMotionMap e)//220200312新增：
        {
            string msg = "进入电气控制模块";
            Log4Net.Info(msg);

            OutputXML = XElement.Load("端口、一键启动配置.xml");//20200719新增：每次弹出电气面板之时，均需要刷新读取一次配置文件；本人觉得非常好
            电气面板 f = new 电气面板(m_cGoogolMotionMap, OutputXML);//下发数据://20200719修改：
            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)
            {
                //问题的根本原因是，c#中类的传输，默认是一级间接（相当于使用了1级间接）：020104
                //this.m_cGoogolMotionMap = f.m_mGoogolMotionMap;//回传数据
                //Reload();
            }
            else if (result == DialogResult.Cancel)
            {
            }

            msg = "退出电气控制模块";
            Log4Net.Info(msg);
        }

        /************************************创建、中止线程、等待中止线程、线程Sleep、线程优先级设置****************************/
        Random rd;
        private void button5_Click(object sender, EventArgs e)
        {
            //使用application .doevents只适用于winform中，且有很多的问题（见网络）；
            //使用多线程的方式更佳，在多线程中使用延时。
            //Application.DoEvents();

            //——————》》进入多线程实现。
            //=>是lambda运算符，是委托的快速实现，可以实现函数式编程的快捷实现。
            //=>是lambda运算符，是委托的符号化实现。
            //Random rd = new Random();
            Thread thread = new Thread(t =>
            {
                for (int i = 0; i < 500; i++)
                {
                    int width = rd.Next(this.panel3.Location.X - 300, this.panel3.Width);//返回一个非负随机整数
                    int height = rd.Next(this.panel3.Location.Y - 150, this.panel3.Height);//返回一个非负随机整数
                    this.panel3.CreateGraphics().DrawEllipse(new Pen(Brushes.Red, 1), new Rectangle(width, height, 10, 10));
                    //Delay
                    Thread.Sleep(100);
                }
            })
            { IsBackground = true };//指示为后台线程
            thread.Start();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            ThreadStart entry = new ThreadStart(RunThread);//线程入口方法：CalcSum
            Thread thread3 = new Thread(entry) { IsBackground = true };
            thread3.Start();
        }
        /************************************创建、中止线程、等待中止线程、线程Sleep、线程优先级设置****************************/
        /********************************************线程挂起和恢复*************************************************************/
        //Monitor.Wait() 和Monitor.Pulse()。
        //必须两个或多个线程共同调用Wait和Pulse，把资源的所有权抛来抛去，才不会死锁。
        //和AutoEvent相似是处理同步关系的，但是AutoEvent是跨进程的，而Monitor是针对线程的。
        private bool _pause = false;
        private object _threadLock = new object();
        private void RunThread()
        {
            while (true)
            {
                if (_pause)
                {
                    lock (_threadLock)
                    {
                        //0-访问这个对象的线程————————————————————————关键。
                        //1-Monitor.Wait()是 释放对象上的锁并阻止当前线程,直到它重新获取该锁。
                        //2-当在一个对象上使用Wait() 方法时，访问这个对象的线程就会一直等待直到被唤醒。                        
                        //3-然后在Wait一下释放资源，这样原线程的就可以继续执行了（代码还堵塞在wait那句话呢）。
                        //4-在lock代码里面如果调用了Monitor.Wait()，会放弃对资源的所有权，让别的线程lock进来。
                        Monitor.Wait(_threadLock);//释放锁，进程无限等待
                    }
                }
                // Do work
                {
                    //Monitor.Enter(_threadLock);
                    //Monitor.Exit(_threadLock);
                    for (int i = 0; i < 20; i++)
                    {
                        //int width = rd.Next(this.panel3.Location.X - 300, this.panel3.Width);//返回一个非负随机整数
                        //int height = rd.Next(this.panel3.Location.Y - 150, this.panel3.Height);//返回一个非负随机整数
                        int width = rd.Next(0, this.panel3.Width);//返回一个非负随机整数
                        int height = rd.Next(0, this.panel3.Height);//返回一个非负随机整
                        this.panel3.CreateGraphics().DrawEllipse(new Pen(Brushes.Blue, 2), new Rectangle(width, height, 10, 10));
                        //Delay
                        Thread.Sleep(100);//线程休眠
                    }
                }
            }
        }
        private void PauseThread()
        {
            _pause = true;
        }
        private void ResumeThread()
        {
            _pause = false;
            lock (_threadLock)
            {
                //然后别的线程代码里Pulse一下（让原线程进入到等待队列）
                Monitor.Pulse(_threadLock);
            }
        }
        /********************************************线程挂起和恢复*******************************************************/
        //private void button5_Click_1(object sender, EventArgs e)
        //{

        //    string filePath = @"face.emf";//文件路径：@"face.emf"
        //    Bitmap bmp = new Bitmap(1200, 1200);//位图：初始化220*220的位图
        //    bmp.SetResolution(600, 600);//设置分辨率

        //    //本步骤比较复杂
        //    Graphics gs = Graphics.FromImage(bmp);//绘图动作（对象）1：从位图创建新的绘图
        //    Metafile mf = new Metafile(filePath, gs.GetHdc());//创建并生成图元文件：根据文件路径和位图信息句柄，用以记录图元信息，

        //    Graphics g = Graphics.FromImage(mf);//绘图动作（对象）2：从图元创建新的绘图——面向emf的
        //    g.FillEllipse(Brushes.Gray, 0, 0, 100, 100);//绘图动作（对象）2：绘制操作，——面向emf的
        //    g.DrawEllipse(Pens.Black, 0, 0, 100, 100);//绘图动作（对象）2：绘制操作——面向emf的
        //    g.DrawArc(new Pen(Color.Red, 10), 20, 20, 60, 60, 30, 120);//绘图动作（对象）2：绘制操作——面向emf的
        //    g.Save();//保存绘图：

        //    g.Dispose();//释放绘图2：
        //    mf.Dispose();//释放绘图1：

        //    var im = Bitmap.FromFile(@"face.emf");//读取emf文件
        //    //im.Save(@"face.wmf", ImageFormat.Wmf);//另存为wmf格式
        //    im.Save(@"face.tif", ImageFormat.Tiff);
        //    //
        //}

        //private void button26_Click(object sender, EventArgs e)//关闭运动控制器
        //{
        //    g_cMotionMap.CloseCard();
        //}

        bool JOBEnableFlag = false;//20200423新建：IDP_SartPrintJob的使能标志位
        RoyalPrintingMap RoyalMap = new RoyalPrintingMap();//创建GoogolMotionMap对象，供本窗口调用
        private void StartJobBtn_Click(object sender, EventArgs e)//启动JOB
        {
#if FALSE
            if (JOBEnableFlag == false)//（壹）打开JOB指令
            {
                ///(1)JOB参数设置：
                royal.royal.g_PrtJobItem.nJobID = 1;//世彪新增0104
                royal.royal.g_PrtJobItem.nPixelGrayBits = 1;//世彪新增0104://20200429新增
                royal.royal.g_PrtJobItem.fPrtYPos = 0;//世彪新增0104//20220524批注：Y方向起始打印位置
                royal.royal.g_PrtJobItem.nPrtXEncPos = 73500/*52000*//*0*//*0x100018*/;//世彪新增0104//20220524批注且需要修改：X方向起始打印位置
                royal.royal.g_PrtJobItem.szJobName = "金属3DP打印";//世彪新增0104
                ///(2)开启JOB使能 
                int returnCode = MeteorPrintEngine.StartJob(ref royal.royal.g_PrtJobItem);
                if (returnCode < 0)
                {
                    MessageBox.Show("Can't Print");
                }
                ///(3)设置对应的标志位
                //g_nLayerCnt=0;
                //RoyalMap._testLayer.nLayerIndex = -1;
                RoyalMap.m_nPrintState = 1;
                RoyalMap.m_bJobStarted = true;

                //（4）修改按钮状态为：关闭打印
                //(sender as Control).BackgroundImage = System.Drawing.Image.FromFile("ICON资源/启动作业-off-38x38.png");//20200430新增：
                (sender as Control).BackgroundImage = Resource.启动作业_off_38x38;
                this.StartJobBtn.Text = "关闭" + "\n" + "作业";
                this.StartJobBtn.TextAlign = ContentAlignment.MiddleRight;
                JOBEnableFlag = true;//玩的都是标志位：20200126
                this.PrinterStatusLabel.Text = "|| 启动中:请等待。。。";//20200430新增：打印机状态栏
            }
            else//（贰）关闭JOB指令
            {
                ///(1)关闭JOB使能
                bool returnCode = MeteorPrintEngine.StopJob();
                if (returnCode == true)
                {
                    RoyalMap.m_nPrintState = 0;
                    RoyalMap.m_bJobStarted = false;
                }
                //（2）修改按钮状态为：启动打印
                //(sender as Control).BackgroundImage = System.Drawing.Image.FromFile("ICON资源/启动作业-on-38x38.png");//20200430新增：
                (sender as Control).BackgroundImage = Resource.启动作业_on_38x38;
                //(sender as Control).Text = "开启";

                this.StartJobBtn.Text = "开启" + "\n" + "作业";
                this.StartJobBtn.TextAlign = ContentAlignment.MiddleRight;
                JOBEnableFlag = false;//玩的都是标志位：20200126

                this.LoadDataBtn.Enabled = true;//2020430新增：恢复载入数据控件交互
            }
#endif
        }
        /// <summary>
        ///  start or close success will return true, while will return false: SET the print file infomation
        /// </summary>
        /// <param name="startCloseFlag"></param>
        /// <returns></returns>
        private bool StartCloseJOB(bool startCloseFlag)//启动JOB:20200514XINJIAN
        {
            string msg = null;
            if (startCloseFlag == true)//（壹）打开JOB指令：实质是生效打印数据
            {
                ///(1)JOB参数设置：
                royal.royal.g_PrtJobItem.nJobID = 1;//世彪新增0104
                //royal.royal.g_PrtJobItem.nPixelGrayBits = 1;//灰度位数：世彪新增0104://20200429新增//20230202新增：灰度数据位数修改
                int bpp = g_RYSYSParam.PixelGrayBits;/*2*/;//20230202新增：灰度数据位数修改      
                if (bpp == 1)
                {
                    royal.royal.g_PrtJobItem.nPixelGrayBits = 1;//灰度位数：世彪新增0104://20200429新增
                }
                else if (bpp == 2)
                {
                    royal.royal.g_PrtJobItem.nPixelGrayBits = 2;//灰度位数：世彪新增0104://20200429新增
                }
                else if (bpp == 3)
                {
                    royal.royal.g_PrtJobItem.nPixelGrayBits = 3;//灰度位数：世彪新增0104://20200429新增
                }

                royal.royal.g_PrtJobItem.fPrtYPos = 0;//世彪新增0104
                //royal.royal.g_PrtJobItem.nPrtXEncPos = 71000/*63000*//*72000*//*54800*//*52000*//*0*//*0x100018*/;//世彪修改：20200711：//20200715修改：330的位置比较合适//20200804：63000//20200923新增：315MM调整到355MM(修复双驱限位移动+重新设置零位值)
                //royal.royal.g_PrtJobItem.nPrtXEncPos = 355*200;//20200923新增：从成形参数模块中获取并设置对应的参数值
                royal.royal.g_PrtJobItem.nPrtXEncPos = (uint)(g_RYSYSParam.m_dPrtXEncPos / 0.001/*0.005*/);// 1um光栅，改为0.001，2024/04/12，Leon'//20200923新增：从成形参数模块中获取并设置对应的参数值//20220524修改：//20220531修改：1UM读数头光栅
                royal.royal.g_PrtJobItem.szJobName = "金属3DP打印";//世彪新增0104
                MeteorPrintEngine.SetPendingScanJobWidth(1);
                Log4Net.Info("写入打印参数：扫描宽度将延后到渲染阶段使用实际图宽。");
                ///(2)开启JOB使能 
                int returnCode = MeteorPrintEngine.StartJob(ref royal.royal.g_PrtJobItem);//20230209：需要确认灰度数据位数，不需要传入灰度阶数
                if (returnCode < 0)
                {
                    msg = "打印参数设置失败：IDP_SartPrintJob： " + returnCode;
                    Log4Net.Info(msg);

                    MessageBox.Show("Can't Print，错误代码：" + returnCode);
                    return false;
                }

                msg = "写入打印参数：IDP_SartPrintJob：" + $"nJobID{{{royal.royal.g_PrtJobItem.nJobID}}}" +
                    $"szJobName{{{royal.royal.g_PrtJobItem.szJobName}}}nPixelGrayBits{{{royal.royal.g_PrtJobItem.nPixelGrayBits}}} " +
                    $"nPrtCtl{{{ royal.royal.g_PrtJobItem.nPrtCtl}}}nPrtXEncPos{{{ royal.royal.g_PrtJobItem.nPrtXEncPos}}}" +
                    $"fPrtYPos{{{ royal.royal.g_PrtJobItem.fPrtYPos}}}";
                Log4Net.Info(msg);

                ///(3)设置对应的标志位
                RoyalMap.m_nPrintState = 1;
                RoyalMap.m_bJobStarted = true;
                resetEventSlimTransferPrint.Set();//must wait until the JOB START action is complished

                //（4）修改按钮状态为：关闭打印
                startCloseFlag = true;//玩的都是标志位：20200126
                this.PrinterStatusLabel.Text = "|| 启动中:请等待。。。";//20200430新增：打印机状态栏
                return true;
            }
            else//（贰）关闭JOB指令：实质是删除打印数据
            {
                ///(1)关闭JOB使能
                bool returnCode = MeteorPrintEngine.StopJob();

                msg = $"停止打印任务，释放板卡内存：IDP_StopPrintJob()：ReturnFlag{{{returnCode}}}";
                Log4Net.Info(msg);

                if (returnCode == true)
                {
                    RoyalMap.m_nPrintState = 0;
                    RoyalMap.m_bJobStarted = false;

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        static ManualResetEventSlim resetEventSlimTransferPrint = new ManualResetEventSlim(false);//手动信号:20200514 new created

        /************************************************************主界面：开始****************************************************************/
        /************************************************************主界面：开始****************************************************************/
        /************************************************************主界面：开始****************************************************************/

        /********************************************1-PreprocGUI线程（调用接口，使用到委托）******************************************/
        //Do work in the PreprocGUIThread
        private void CreatePreprocGUIThread()//for this aim, 1 thread is enough
        {
            //START THE LOAD CLI AND CREATE TIFF THREAD
            ThreadStart PreprocGUIThreadEntry = new ThreadStart(RunPreprocGUIThread);//线程入口方法
            PreprocGUIThread = new Thread(PreprocGUIThreadEntry) { IsBackground = true };
            PreprocGUIThread.Start();
        }
        /********************************************2-PreprocGUI线程定义（使用到委托）************************************************/
        //Preprocessing GUI Thread：
        private Graphics g = null; //Graphics.FromImage(_3DP_GUI.bt);
        private Thread PreprocGUIThread;//GUI预处理线程
        private void PreprocGUIContain()//具体工作：GUI预处理
        {
            //lock (_3DP_GUI)
            //{
            _3DP_GUI.SetBitmap(this.panel3.Width, this.panel3.Height);//——————创建容器，并设定大小
                                                                      ////Graphics g = Graphics.FromImage(_3DP_GUI.bt);    //20200223新减去:多线程绘制图像数据
                                                                      ////_3DP_GUI.Select_rectangle(g, this.panel3.Width, this.panel3.Height);    //20200223新减去:多线程绘制图像数据

            //主界面panel的更新（不一定用到panel实现）
            //（1）委托实现：this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
            //should use the delegrate to do this function and should combinate the invoke to update the panel
            //UpPreprocGUI();//应用到委托

            //(2)非委托实现（调用控件的线程安全方法）：
            //线程同步==equals to==线程排队==equals to==线程安全==the main problem is that different thread share the same object in the memory.
            //UpPreprocGUI();//非委托方式
            ////this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0)); //20200223新减去:多线程绘制图像数据   
            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据
            _3DP_GUI.cal_num_p = 0;
            data_storage();
            //}
        }
        /********************************************3-PreprocGUI线程（委托部分）*******************************************************/
        private delegate void UpPreprocGUIDelegate();//UpdateComposationPicDelegate/// 定义一个代理
        private void UpPreprocGUI()//register the JobItems to the 主界面线程的人机交互组件中
        {
#if (false)//委托部分
            lock (_3DP_GUI)//需要锁定使用的对象
            {
                if (this.panel3.InvokeRequired == false)//如果调用该函数的线程和控件listMain位于同一个线程内
                {
                    ////this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));//20200223新减去:多线程绘制图像数据
    //20200223新增:多线程绘制图像数据
	//20200223新增:多线程绘制图像数据      
					data_storage();
				}
                else//如果调用该函数的线程和控件lstMain不在同一个线程
                {
                    UpPreprocGUIDelegate upPreprocGUI = new UpPreprocGUIDelegate(UpPreprocGUI);
                    this.panel3.Invoke(upPreprocGUI);//invoke为同步的委托调用；beginInvoke为异步的委托掉用；endInvoke为异步的委托调用的返回值。
                }
            }
#elif true//非委托部分
            lock (_3DP_GUI)//需要锁定使用的对象
            {
                ////this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));//20200223新减去:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据 
                _3DP_GUI.cal_num_p = 0;
                data_storage();
            }
#endif
        }

        /********************************************4-PreprocGUI线程（挂起和恢复实现）*************************************************/
        //Monitor.Wait() 和Monitor.Pulse()。
        //必须两个或多个线程共同调用Wait和Pulse，把资源的所有权抛来抛去，才不会死锁。
        //和AutoEvent相似是处理同步关系的，但是AutoEvent是跨进程的，而Monitor是针对线程的。

#if false//使用lock的GUI预处理线程
        private bool _PreprocGUIpause = false;
        private object _PreprocGUIthreadLock = new object();
        //create thread
        private void RunPreprocGUIThread()//it is equals to the RunXXXThread();
        {
            while (true)
            {
                Thread.Sleep(100);//线程要休息300ms
                //WAIT until awaken
                if (_PreprocGUIpause)
                {
                    lock (_PreprocGUIthreadLock)
                    {
                        //0-访问这个对象的线程————————————————————————关键。
                        //1-Monitor.Wait()是 释放对象上的锁并阻止当前线程,直到它重新获取该锁。
                        //2-当在一个对象上使用Wait() 方法时，访问这个对象的线程就会一直等待直到被唤醒。                        
                        //3-然后在Wait一下释放资源，这样原线程的就可以继续执行了（代码还堵塞在wait那句话呢）。
                        //4-在lock代码里面如果调用了Monitor.Wait()，会放弃对资源的所有权，让别的线程lock进来。
                        Monitor.Wait(_PreprocGUIthreadLock);//释放锁，进程无限等待
                    }
                }
                // Do work when awaken
                {
                    PreprocGUIContain();//每次唤醒后的工作，在此处完成:具体的工作
                    //PausePreprocGUIThread();
                }
            }
        }
#elif true
        //create thread
        private void RunPreprocGUIThread()//it is equals to the RunXXXThread();
        {
            PreprocGUIContain();//每次唤醒后的工作，在此处完成:具体的工作
        }
#endif
#if false
        private int firstPaintFlag = 0;//首次主界面绘制标志位
#endif

        SharpControl g_SharpControl = new SharpControl();//精华

        private void 主界面_Paint(object sender, PaintEventArgs e)
        {
#if false//20200527：更换主界面
            //panel2为移动主界面显示页数操作
            //this.panel2.Location = new Point(this.panel3.Width - this.panel2.Width/*(this.panel3.Width - this.panel2.Width)/2+20*/, this.panel3.Height - this.panel2.Height);
            //初次绘制主界面//本部分废弃，其实都不要初次绘制
            if (firstPaintFlag < 1)//（a)初次绘制主界面
            {
                //初次绘制：UI线程中完成
                //不使用双缓冲方式绘制
                _3DP_GUI.SetBitmap(this.panel3.Width, this.panel3.Height);//——————创建容器，并设定大小
                Graphics g = Graphics.FromImage(_3DP_GUI.bt);
                _3DP_GUI.Select_rectangle(g, this.panel3.Width, this.panel3.Height);
                this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
                firstPaintFlag++;
            }
            else//(b)重绘主界面
            {
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_PX.Count; _3DP_GUI.cal_num_p++)//———————————5这个值是临时测试的,COUNT才是正确的——————！！！！
                {
                    _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_px[_3DP_GUI.cal_num_p] * _3DP_GUI.cal_pixel2MM + (this.Width / 2 + 20);
                    _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_py[_3DP_GUI.cal_num_p] * _3DP_GUI.cal_pixel2MM + (this.Height / 2 + 20);
                }
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据
                _3DP_GUI.cal_num_p = 0;
                data_storage();//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
            } 
            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据
            _3DP_GUI.cal_num_p = 0;
            data_storage();//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
#else
            g_SharpControl.RepaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(this.renderControl1.Top, this.renderControl1.Left, this.renderControl1.Width, this.renderControl1.Height/*+30*/));
#endif
        }

        private void panel3_SizeChanged(object sender, EventArgs e)//20200527新增
        {

        }

        private void Form1_MouseWheel(object sender, MouseEventArgs e)//滚轮滚动事件//2020052重新更新鼠标事件
        {
            //2020052重新更新鼠标事件
            if (this.renderControl1.ClientRectangle.Contains(renderControl1.PointToClient(Cursor.Position)))
            {
                PointF point = renderControl1.PointToClient(Cursor.Position);

                g_SharpControl.MouseWheelControl(e, point);

                g_SharpControl.PaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height/*+30*/));
            }
        }

        public void Zuobiaoqueding()
        {
            if (_3DP_GUI.r_d_point.X >= _3DP_GUI.s_min.X && _3DP_GUI.r_d_point.Y >= _3DP_GUI.s_min.Y &&
            _3DP_GUI.r_d_point.X <= _3DP_GUI.s_max.X && _3DP_GUI.r_d_point.Y <= _3DP_GUI.s_max.Y)//限定鼠标在选择方框内，图片才能移动
            {
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_FLAG.Count; _3DP_GUI.cal_num_p++)
                {
                    if (_3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] >= _3DP_GUI.s_min.X && _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] >= _3DP_GUI.s_min.Y &&
                    _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] <= _3DP_GUI.s_max.X && _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] <= _3DP_GUI.s_max.Y)
                    {
                        _3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] = 1;

                    }

                }
                if (SP_num == 1)//如果选中的图片张数为1
                {
                    for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_FLAG.Count; _3DP_GUI.cal_num_p++)
                    {
                        if (_3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] == 1)
                        {
                            //_3DP_GUI.cal_px[_3DP_GUI.cal_num_p] = 阵列拷贝.zbx;
                            //_3DP_GUI.cal_py[_3DP_GUI.cal_num_p] = 阵列拷贝.zby;
                            _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_px[_3DP_GUI.cal_num_p] * _3DP_GUI.pixel2MM + (this.Width / 2 + 20);
                            _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_py[_3DP_GUI.cal_num_p] * _3DP_GUI.pixel2MM + (this.Height / 2 + 20);
                        }
                    }
                }
                else//如果选中的图片张数大于1
                {
                    for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_FLAG.Count; _3DP_GUI.cal_num_p++)
                    {
                        if (_3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] == 1)
                        {
                            //_3DP_GUI.cal_px[_3DP_GUI.cal_num_p] = _3DP_GUI.cal_px[_3DP_GUI.cal_num_p] + 阵列拷贝.zbx;
                            //_3DP_GUI.cal_py[_3DP_GUI.cal_num_p] = _3DP_GUI.cal_py[_3DP_GUI.cal_num_p] + 阵列拷贝.zby;
                            _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_px[_3DP_GUI.cal_num_p] * _3DP_GUI.pixel2MM + (this.Width / 2 + 20);
                            _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_py[_3DP_GUI.cal_num_p] * _3DP_GUI.pixel2MM + (this.Height / 2 + 20);
                        }
                    }
                }
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_FLAG.Count; _3DP_GUI.cal_num_p++)
                {
                    if (_3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] == 1)
                    {
                        _3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] = 0;
                    }
                }
                _3DP_GUI.cal_sr_Flag = 1;
                data_storage();
                SR_IO_F = 1;
                SR_F = 0;
            }

            else
            {
                _3DP_GUI.FLAG[_3DP_GUI.num_p] = 0;
            }
        }

        private void panel3_MouseDown(object sender, MouseEventArgs e)
        {
            //_3DP_GUI.Write("鼠标按下");
            if (e.Button == MouseButtons.Left)
            {
                //添加这几行代码非常关键
                Point point = panel3.PointToClient(Cursor.Position);
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据 
                _3DP_GUI.cal_MouseDown_x = point.X;//————————————修改
                _3DP_GUI.cal_MouseDown_y = point.Y;
                _3DP_GUI.cal_firstpoint.X = point.X;
                _3DP_GUI.cal_firstpoint.Y = point.Y;
                int pd = 0;
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_FLAG.Count; _3DP_GUI.cal_num_p++)
                {

                    pd = pd + (int)_3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p];

                }
                if (pd == 0)
                    _3DP_GUI.cal_sr_Flag = 0;
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据 
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_FLAG.Count; _3DP_GUI.cal_num_p++)
                {
                    if ((int)_3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] == 1)
                    {
                        _3DP_GUI.cal_select_Flag = 1;
                        break;
                    }
                    else
                    {
                        _3DP_GUI.cal_select_Flag = 0;
                    }
                }
            }
            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据

            //20200225新增:多线程绘制图像数据
            else if (e.Button == MouseButtons.Right)
            {
                Point point = this.PointToClient(Cursor.Position);
                _3DP_GUI.r_d_point.X = point.X;
                _3DP_GUI.r_d_point.Y = point.Y;
                if (e.X <= _3DP_GUI.Firstpoint.X || e.Y <= _3DP_GUI.Firstpoint.Y ||
                  e.X >= _3DP_GUI.Secondpoint.X || e.Y >= _3DP_GUI.Secondpoint.Y)
                {
                    for (_3DP_GUI.num_p = 0; _3DP_GUI.num_p < _3DP_GUI.FLAG.Count; _3DP_GUI.num_p++)
                    {
                        if ((int)_3DP_GUI.FLAG[_3DP_GUI.num_p] == 1)
                            _3DP_GUI.FLAG[_3DP_GUI.num_p] = 0;

                    }
                    _3DP_GUI.sr_Flag = 1;
                    // data_storage();
                    //_3DP_GUI.Select_rectangle(g, this.Width, this.Height);
                    //this.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));
                    _3DP_GUI.firstpoint.X = 0;
                    _3DP_GUI.firstpoint.Y = 0;
                    _3DP_GUI.secondpoint.X = 0;
                    _3DP_GUI.secondpoint.Y = 0;
                }
                else
                {

                }
            }
            //20200225新增:多线程绘制图像数据

            _3DP_GUI.cal_num_p = 0;
        }

        private void panel3_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据
            if (e.Button == MouseButtons.Left)
            {
                Point point = panel3.PointToClient(Cursor.Position);//——————————添加这一项非常关键！！！！！

                _3DP_GUI.cal_MouseDown_x = point.X;
                _3DP_GUI.cal_MouseDown_y = point.Y;
                _3DP_GUI.cal_zoom = 1;

                //_3DP_GUI.cal_sr_Flag = 1;//=1，就是不用重新画方框
                _3DP_GUI.cal_sr_Flag = 1;//=1，就是不用重新画方框——————————————使用类型名称进行访问
                _3DP_GUI.cal_num_p = 0;
                data_storage();
            }
        }

        private void panel3_MouseUp(object sender, MouseEventArgs e)
        {

            //添加这几行代码非常关键
            Point point = panel3.PointToClient(Cursor.Position);

            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据
            //修改
            _3DP_GUI.cal_MouseUp_x = point.X;
            _3DP_GUI.cal_MouseUp_y = point.Y;

            //20200225右键选中标志位
            if (SR_F != 1)//20200225右键选中标志位
            {
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_FLAG.Count; _3DP_GUI.cal_num_p++)//标志位恢复没有选中的状态
                {
                    if ((int)_3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] == 1)
                        _3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] = 0;
                    _3DP_GUI.cal_mov_F = 0;
                }
            }
            //20200225
            //20200225
            if (_3DP_GUI.cal_select_Flag == 0)//——————一张也没有选中的时候，也要画
            {
                ////设置bitmap大小//设置画笔类型为bmp
                _3DP_GUI.cal_num_p = 0;
                data_storage();//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
            }
            //20200225
            //20200225
            if (_3DP_GUI.cal_select_Flag == 0)
            {
                _3DP_GUI.s_min.X = _3DP_GUI.s_min.Y = _3DP_GUI.s_max.X = _3DP_GUI.s_max.Y = 0;
                SP_num = 0;
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_PX.Count; _3DP_GUI.cal_num_p++)//判断图片是否完全位于鼠标所画方框里面————————判断是否选中
                {
                    //判断图片是否完全位于鼠标所画方框里面
                    if ((int)_3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] >= ((_3DP_GUI.cal_firstpoint.X - _3DP_GUI.cal_MouseDownP.X) / _3DP_GUI.cal_zoom + _3DP_GUI.cal_MouseDownP.X) &&
                        (int)_3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] >= ((_3DP_GUI.cal_firstpoint.Y - _3DP_GUI.cal_MouseDownP.Y) / _3DP_GUI.cal_zoom + _3DP_GUI.cal_MouseDownP.Y) &&
                        ((int)_3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] + (int)_3DP_GUI.cal_PW[_3DP_GUI.cal_num_p]) <= ((_3DP_GUI.cal_secondpoint.X - _3DP_GUI.cal_MouseDownP.X)
                        / _3DP_GUI.cal_zoom + _3DP_GUI.cal_MouseDownP.X) &&
                        ((int)_3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] + (int)_3DP_GUI.cal_PH[_3DP_GUI.cal_num_p]) <= ((_3DP_GUI.cal_secondpoint.Y - _3DP_GUI.cal_MouseDownP.Y)
                        / _3DP_GUI.cal_zoom + _3DP_GUI.cal_MouseDownP.Y))
                    {
                        _3DP_GUI.cal_select_Flag = 1;//——————————————————————————————是否有选中至少1张图片
                        _3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] = 1;//——————————————————————————————设置选中具体图片的标志位

                        //20200225
                        //20200225
                        if (_3DP_GUI.s_min.X == 0 && _3DP_GUI.s_min.Y == 0)
                        {
                            _3DP_GUI.s_min.X = _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p];
                            _3DP_GUI.s_min.Y = _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p];
                        }
                        if (_3DP_GUI.s_min.X > _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p])
                            _3DP_GUI.s_min.X = _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p];
                        if (_3DP_GUI.s_min.Y > _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p])
                            _3DP_GUI.s_min.Y = _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p];
                        if (_3DP_GUI.s_max.X < _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] + _3DP_GUI.cal_PW[_3DP_GUI.cal_num_p])
                            _3DP_GUI.s_max.X = _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] + _3DP_GUI.cal_PW[_3DP_GUI.cal_num_p];
                        if (_3DP_GUI.s_max.Y < _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] + _3DP_GUI.cal_PH[_3DP_GUI.cal_num_p])
                            _3DP_GUI.s_max.Y = _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] + _3DP_GUI.cal_PH[_3DP_GUI.cal_num_p];
                    }
                    if (_3DP_GUI.cal_FLAG[_3DP_GUI.cal_num_p] == 1)//选中就加1
                        SP_num++;
                }
            }
            _3DP_GUI.cal_num_p = 0;//——————————————————————————从序号为0开始画（画图）
            _3DP_GUI.cal_sr_Flag = 1;//画LINK的还要不要画方框——————复位后，不再需要画方框
            ////设置bitmap大小//设置画笔类型为bmp
            //_3DP_GUI.SetBitmap(this.Width, this.Height);//——————创建容器，并设定大小————！！！！！
            //Graphics g = Graphics.FromImage(_3DP_GUI.cal_bt);//————！！！！！
            //_3DP_GUI.Select_rectangle(g, this.Width, this.Height);//总控LINK和方框的绘制
            _3DP_GUI.cal_Secondpoint = _3DP_GUI.cal_secondpoint;
            _3DP_GUI.cal_Firstpoint = _3DP_GUI.cal_firstpoint;
            data_storage();//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
                           //ResumePreprocGUIThread();
                           //20200223新增:多线程绘制图像数据
                           //20200223新增:多线程绘制图像数据
        }


        private void panel3_Paint(object sender, PaintEventArgs e)//界面需要重绘时发生；鼠标移动也会触发onpaint事件
        {
            //20200223新去:多线程绘制图像数据
            //20200223新去:多线程绘制图像数据
            ////this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));//刷新
            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据
            _3DP_GUI.cal_num_p = 0;
            data_storage();//刷新
        }


        /************************************************************主界面：结束****************************************************************/
        /************************************************************主界面：结束****************************************************************/
        /************************************************************主界面：结束****************************************************************/
        bool g_IpcConnectSuccessFlag = false;
        /// <summary>
        /// create final JOBS:输出大图：负责生成1层:20200506大修
        /// </summary>
        /// <param name="_3DP_GUI"></param>
        /// <param name="DPI"></param>
        /// <param name="layerID"></param>
        /// <param name="FilePath"></param>
        private void CreateFinalJOB(_3DP_GUI组件 _3DP_GUI, int DPI, int layerIndex, string FilePath)//create final JOBS
        {
            RemoteCLIs c_RemoteCLIs = new RemoteCLIs();//20200506新建：存放1层的所有的绘制数据，即是多个零件数据
            for (int i = 0; i < _3DP_GUI.tempJobItems.Count(); i++)//20200505新增：遍历所有零件
            {
                float x = (float)_3DP_GUI.tempJobItems[i].position.X;
                float y = (float)_3DP_GUI.tempJobItems[i].position.Y;
                ///20200506新建批注：完成CLI与（X,Y）排版位置信息的匹配工作
                CLI correctCLI = CliStreams.Find(t => t.ID.Equals(_3DP_GUI.tempJobItems[i].id));//返回满足指定谓词条件的第1个返回值
#if false
                ///20200506新建批注：遍历所有的层
                for (int j = 0; j < 1/*correctCLI.LayerLine[layerIndex].LineList.Count*/; j++)//20200505新增：遍历单个零件的所有线段
                {
                    //此处省略一大段
                    //Tiff.GetOneLayerTiff(correctCLI, FilePath, layerID,x,y, g, OuterPen, BrushOuter, 
                    //InterPen, BrushInter, OneLine);

                    //（1）实际工作代码//20200505新建批注：FilePath实际没有用处，也没有用到
                    Build1BitBmp.GetALayerTiff(layerIndex, correctCLI, x, y, FilePath, g, OuterPen, BrushOuter,
                        InterPen, BrushInter, OneLine);//20200408新增修改：生成.bmp file
                    ////（2）测试多线程不起作用的原因：20200415新建，参考：https://www.dreamincode.net/forums/topic/245284-use-of-backgroundworker-hangs-gui/
                    //int cnt = 0;
                    //for (int k = 0; k < 1000000; k++)
                    //{
                    //    cnt += k;
                    //    for (int l = 0; l < 10000; l++)
                    //    {
                    //    }
                    //    //g.DrawImage(data.image, dstRect, srcRect, GraphicsUnit.Pixel);
                    //}
                }
#else
                //（二）跨进程调用：20200506新建
                // (2)跨进程调用对象关键:Invoke a method on the remote object.
                //(2-1)附加位置消息结构
                {
                    CountCLI tempCountCLI = new CountCLI();
                    tempCountCLI.x = x;
                    tempCountCLI.y = y;
                    tempCountCLI.aLayer = correctCLI.LayerLine[layerIndex];
                    //(2-2)附加远程信息结构
                    //RemoteCLIs tempRemoteCLIs = new RemoteCLIs();
                    c_RemoteCLIs.layerIndex = layerIndex;
                    c_RemoteCLIs.aLayerData.Add(tempCountCLI);
                }
#endif
            }

            //20200506新增：跨进程调用
            //（一）开启跨进程服务
            //（二）跨进程调用
            AccessServer accessServer = new AccessServer();//(0)新建AccessServer对象，完成向服务器的数据发送：20200506新建
            if (g_IpcConnectSuccessFlag == false)
            {
                int i = accessServer.IPCStart(g_IpcConnectSuccessFlag);//(0) 注册IPC信息
                if (i == 1)//设置IPC连接成功标志位
                {
                    g_IpcConnectSuccessFlag = true;
                }
            }
            accessServer.SendDataToRemote(c_RemoteCLIs, layerIndex);//(1) 发送IPC信息

            //UpdateCircularBarMethod();//20200507批注：不可以放在此处避免反复新建
        }

        //create final JOBS——输出大图   
        //This is very important to record the child file path of TIFF。
        //THE child file path of the TIFF can be very important for the GUI show action and show information in listview!!!!
        public List<string> TIFFPath = new List<string>();
        //Store the global CLI Stream information in memory.
        //the aim is to only the once CLIimport job, because it is time consuming.
        List<CLI> CliStreams = new List<CLI>();//全局存放CLI Stream的内存信息

        //string[] tempPath;//全局存放读取的CLI的文件名
        List<string> tempPath = new List<string>();//全局存放读取的CLI的文件名
        //List<string> recordPaths = new List<string>();//全局存放读取的CLI的文件名//20201110注释掉：
        List<string> recordOutputPaths = new List<string>();

        //20221125新增：保存到本地文件，文件类型.bjgroup
        //20221125新增：保存到本地文件，文件类型.bjgroup
        private void DownloadCliBtn_Click(object sender, EventArgs e)//20221125新增：保存到本地文件，文件类型.bjgroup
        {
            try//20221125新增：确保格式正确
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "(SCUT-BJ Saved File (*.bjgroup)|*.bjgroup";
                //saveFileDialog.RestoreDirectory = true;//记录上次打开的目录
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string localFilePath = saveFileDialog.FileName.ToString();//获得文件名
                    //（2）保存文件：.bjgroup类型文件
                    FileStream fs = new FileStream(localFilePath, FileMode.Create);//20221125新增：保存到.bjgroup文件
                    BinaryFormatter bf = new BinaryFormatter();
                    //List<CLI> ps = new List<CLI>();
                    bf.Serialize(fs, CliStreams);
                    fs.Close();

                    string msg = "CAD数据保存：保存.bjgroup格式CAD数据成功：" + localFilePath;
                    Log4Net.Info(msg);
                }
            }
            catch (Exception)
            {
                string msg = "保存.bjgroup格式CAD数据错误，请重新保存！";
                Log4Net.Info(msg);
                MessageBox.Show("Error:.bjgroup文件保存错误，请重新保存！");
            }
        }

        private Thread LoadCLIThread;//（1）导入CLI线程
        private void RunLoadCLIThread(object TransferObject)//载入数据线程内容//20201110修改为带参数类型：
        {
            try
            {
                //(1)Create the file System
                //(2)Build the TIFF File in the right file Path
                string msg = null;
                string tempSelectPATHS = null;

                LoadDataTransferObject OperationType = TransferObject as LoadDataTransferObject;//类型转换——输入数据//20201113修改//string OperationType = TransferObject as string;//类型转换——输入数据
                switch (OperationType.CadOperationCode)
                {
                    case "1"://ADD
                        g_SharpControl.selectPaths.Clear();//20230321新增：消除潜在BUG

                        //string ImportPathList = null;
                        foreach (string path in tempPath)//Path is the path of CLI file.
                        {
                            string extension = System.IO.Path.GetExtension(path);//20221125新增：获取文件的扩展名
                            if (extension == ".bjgroup")//为自定义的.bjgroup文件
                            {
                                try//20221125新增：确保格式正确
                                {
                                    FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);//20201125新增：待序列化文件流
                                    BinaryFormatter bf = new BinaryFormatter();//20201125新增：序列化对象
                                    List<CLI> tempCliStreams = bf.Deserialize(fs) as List<CLI>;
                                    //CliStreams = tempCliStreams;//(2)Read the CLIfile to the memory just only once    
                                    //读取.bjgroup文件，且更新JobList的相关文件
                                    for (int i = 0; i <= tempCliStreams.Count - 1; i++)//there exists some deadly error. i shouldn't use the recordPaths,but i should use the kidPath in the former part
                                    {
                                        CliStreams.Add(tempCliStreams[i]);//(2)Read the CLIfile to the memory just only once    
                                        UpdateListView(tempCliStreams[i].recordPathItem, 1, tempCliStreams[i].LayerNumber);//20201111新增：完成JobList的更新
                                    }

                                    msg = "CAD数据加载：添加.bjgroup格式CAD数据成功-" + path;
                                    Log4Net.Info(msg);
                                }
                                catch (Exception e)
                                {
                                    MessageBox.Show("Error:.bjgroup文件格式错误，请重新输入！" + e.ToString());
                                }
                            }
                            else//为CLI或者其他文件
                            {
                                if (null == CliStreams.Find(t => t.recordPathItem.Equals(path)))//重复性检查//如果不存在指定的路径
                                {
                                    CLI tempSTL = STL.ReadCLI(path);//1112修改：
                                    //tempSTL.Dimension[1].x = tempSTL.Dimension[1].x - tempSTL.Dimension[0].x;//1112新增：
                                    //tempSTL.Dimension[1].y = tempSTL.Dimension[1].y - tempSTL.Dimension[0].y;//1112新增：
                                    ThreeDimension tempDimension = new ThreeDimension();
                                    tempSTL.Dimension.Add(tempDimension);
                                    tempSTL.Dimension[2].x = 0;//1112新增：x为为偏移值δx，初始化为0
                                    tempSTL.Dimension[2].y = 0;//1112新增：x为为偏移值δy，初始化为0

                                    //20230321新增：增添对Z方向打印位置的设置
                                    if (tempSTL.Dimension[0].z == 0)
                                    {
                                    }
                                    else
                                    {
                                        int insertLayerNum = (int)(tempSTL.Dimension[0].z / tempSTL.LayerThickness);
                                        tempSTL.LayerNumber = tempSTL.LayerNumber + insertLayerNum;
                                        for (int i = 0; i < insertLayerNum; i++)
                                        {
                                            tempSTL.LayerLine.Insert(0, new Layer());
                                        }
                                    }


                                    CliStreams.Add(/*STL.ReadCLI(path)*/tempSTL);//(2)Read the CLIfile to the memory just only once                            
#if false
                                    //if (true/*CadOperationCode == "1"*/)//20230320新增：
                                    //{
                                    //    double originalY/*tempJobItem.position.Y */= tempSTL.Dimension[0/*1*/].y;//暂时先不设置，直接reset为0；之后支持在magics中进行完成的排版文件的导入————！！！！！//20200514xiugai
                                    //    tempSTL.Dimension[0/*1*/].y = -originalY + 330 / 2;//20230320修改：修复坐标系不协调的问题                      
                                    //}
                                    //tempSTL.Dimension[0].y = -tempSTL.Dimension[0].y + 175;//20221125新增：修复导入数据偏差
                                    //tempSTL.Dimension[1].y = -tempSTL.Dimension[1].y + 175;//20221125新增：修复导入数据偏差
#else
                                    double originalX = tempSTL.Dimension[0/*1*/].x - 165;//暂时先不设置，直接reset为0；之后支持在magics中进行完成的排版文件的导入————！！！！！//20200514xiugai
                                    double originalY = tempSTL.Dimension[0/*1*/].y + 165;//20230320修改：修复坐标系不协调的问题                      
                                    OperationType.xTranslate = originalX;
                                    OperationType.yTranslate = -(originalY - Math.Abs(tempSTL.Dimension[1].y - tempSTL.Dimension[0].y)) - Math.Abs(tempSTL.Dimension[1].y - tempSTL.Dimension[0].y)/*+ Math.Abs(tempSTL.Dimension[1].y- tempSTL.Dimension[0].y)*/;
                                    OperationType.CadOperationCode = "3";
                                    g_SharpControl.selectPaths.Add(tempSTL.recordPathItem);//20230320新增：
                                    bool SingleDataFlag = true;
                                    TranslateCliStreams(ref CliStreams, g_SharpControl.selectPaths, OperationType, SingleDataFlag);//MOVE//20230320修正输入文件错误BUG
                                    OperationType.CadOperationCode = "1";
                                    g_SharpControl.selectPaths.Clear();//20230320新增：
#endif
                                    UpdateListView(path, 1, tempSTL.LayerNumber);//20201111新增：完成JobList的更新

                                    msg = "CAD数据加载：添加.CLI格式CAD数据成功-" + path;
                                    Log4Net.Info(msg);
                                }
                            }
                        }
                        tempPath.Clear();//20221125新增：清理完路径

                        ////（1）读取文件：.bjgroup类型文件
                        //foreach (string path in tempPath)//20201125新增：Path is the path of CLI file.
                        //{
                        //    string extension = System.IO.Path.GetExtension(path);//20201125新增：获取文件的扩展名
                        //    if (extension == ".bjgroup")//为自定义的.bjgroup文件
                        //    {
                        //        FileStream fs = new FileStream(path, FileMode.Open);//20201125新增：待序列化文件流
                        //        BinaryFormatter bf = new BinaryFormatter();//20201125新增：序列化对象
                        //        List<CLI> tempCliStreams = bf.Deserialize(fs) as List<CLI>;
                        //        CliStreams = tempCliStreams;//(2)Read the CLIfile to the memory just only once    
                        //        //更新JobList的相关文件
                        //    }
                        //    else//为CLI或者其他文件
                        //    { }
                        //}
                        //tempPath.Clear();//20201111新增：清理完路径
                        ////（2）保存文件：.bjgroup类型文件
                        //FileStream fs = new FileStream(@"D:\Program\CSharp\NGramTest\NGramTest\serializePeople.dat", FileMode.Create);//20201125新增：保存到.bjgroup文件
                        //BinaryFormatter bf = new BinaryFormatter();
                        ////List<CLI> ps = new List<CLI>();
                        //bf.Serialize(fs, CliStreams);
                        //fs.Close();

                        break;
                    case "2"://DELETE
                        foreach (string path in g_SharpControl.selectPaths)
                        {
                            CliStreams.RemoveAll(t => t.recordPathItem.Equals(path));//CliStreams.Find(t => t.recordPathItem.Equals(path));
                            UpdateListView(path, 2, 0);//20201111新增：完成JobList的更新//参数0无意义，形式而已

                            msg = "CAD数据删除：删除.CLI格式CAD数据成功-" + path;
                            Log4Net.Info(msg);
                            //g_SharpControl.selectPaths.Clear();
                        }
                        break;
                    case "3"://MOVE

                        TranslateCliStreams(ref CliStreams, g_SharpControl.selectPaths, OperationType, true);

                        for (int i = 0; i < g_SharpControl.selectPaths.Count; i++)
                        {
                            tempSelectPATHS = tempSelectPATHS + g_SharpControl.selectPaths[i];
                        }
                        msg = $"CAD位置平移：SelectCADFile{{{ tempSelectPATHS}}}\r\n" +
                            $"平移方式{{true为目标位置平移，false为相对平移{{{OperationType.translateMode}}}}}" +
                            $"X方向平移目标值{{{OperationType.xTranslate}MM}}Y方向平移目标值{{{OperationType.yTranslate}MM}}" +
                            $"Z方向平移目标值{{{OperationType.zTranslate}MM}}X方向平移增量{{{OperationType.xDeltaTranslate}MM}}" +
                            $"Y方向平移增量{{{OperationType.yDeltaTranslate}MM}}Z方向平移增量{{{OperationType.zDeltaTranslate}MM}}";
                        Log4Net.Info(msg);

                        //g_SharpControl.selectPaths.Clear();

                        break;
                    case "4"://SCALE
                        break;
                    case "5"://MIRROR
                        break;
                    case "6"://Vitural ADD:20201112新增

                        ArrayCliStreams(ref CliStreams, g_SharpControl.selectPaths, (float)OperationType.xSpace/*60*/,
                            -(float)OperationType.ySpace/*60*/, OperationType.xnum/*5*/, OperationType.ynum/*2*/);//20201112新增，精华：Vitural ADD//20221125修改：修复Y向相反问题

                        for (int i = 0; i < g_SharpControl.selectPaths.Count; i++)
                        {
                            tempSelectPATHS = tempSelectPATHS + g_SharpControl.selectPaths[i];
                        }
                        msg = $"CAD数据阵列：SelectCADFile{{{ tempSelectPATHS}}}\r\n" +
                            $"X方向间距{{{OperationType.xSpace}MM}}Y方向间距{{{-(float)OperationType.ySpace}MM}}" +
                            $"X方向数目{{{ OperationType.xnum}}}Y方向数目{{{ OperationType.ynum}}}";
                        Log4Net.Info(msg);

                        break;
                }

                if (CliStreams.Count >= 1)//存在导入的CAD零件数
                {
                    //update the recordPath in CLI under CliStreams
                    for (int i = 0; i <= CliStreams.Count - 1; i++)//there exists some deadly error. i shouldn't use the recordPaths,but i should use the kidPath in the former part
                    {
                        //CliStreams.ElementAt(i).recordPathItem = recordOutputPaths[i];//将recordPaths中的数据刷新到CliStreams中
                        CliStreams.ElementAt(i).ID = i;//更新CliStreams中的序列号
                    }
                    // Show information work in LISTVIEW2.
                    //(0)update the four kinds information:ID+FileName+LayerNumber+CompeleteRate
                    //(0)update the four kinds infor mation:LayerNumber
                    //for (int i = 0; i < CliStreams.Count; i++)
                    //{
                    //    string ReadLayerNum = Convert.ToString(CliStreams.ElementAt(i).LayerNumber);
                    //    //listView2.SelectedItems[i].SubItems[2].Text = ReadLayerNum;//本行代码会报错
                    //    UpdateLayerNum(i, ReadLayerNum);//使用委托的方式去实现
                    //}
                    //预排版:Composation the TIFF Picture,and save the TIFF picture information to the Data Structure//20200527批注
                    Composation(ref CliStreams/*, OperationType.CadOperationCode*/);//with the help of data structure optimization, we can only transfer the ref CliStreams to achieve the ComposationCLI function 
                    //(a) 绘制CLI: 从composationCLI.jobItems——>g_SharpControl.tempJobItems 
                    g_SharpControl.tempJobItems = composationCLI.jobItems;//translate the jobItems in the Composation Space to the local jobItems                                                         
                    //(b) 整合1层的CLIs://排序确定所有零件的最大的值
                    int MaxLayer = CliStreams[0].LayerNumber;
                    for (int i = 1; i <= CliStreams.Count() - 1; i++)//排序确定零件的最大层数
                    {
                        if (MaxLayer < CliStreams[i].LayerNumber)//更新最大层数
                        { MaxLayer = CliStreams[i].LayerNumber; }
                        else//不更新
                        { }
                    }
                    g_RemoteCLIs.Clear();//20201110新增：消除重复添加无效BUG
                    for (int layerIndex = 0; layerIndex <= MaxLayer - 1; layerIndex++)
                    {
                        GetLayerCLIs(layerIndex);//GetLayerCLIs(0);//获取第1层的全部零件数据:g_SharpControl.tempJobItems——>RemoteCLIs c_RemoteCLIs
                        g_RemoteCLIs.Add(c_RemoteCLIs);
                        g_SharpControl.UILayerCount = layerIndex + 1;//层总数
                    }
                    UpdateGUILayer(MaxLayer - 1);//20200528：更新HScroll回调控制
                    g_SharpControl.ToRemoteCLIsList = g_RemoteCLIs;//20200528使用属性：g_RemoteCLIs——>g_SharpControl.gc_RemoteCLIs：UI使用
                    g_SharpControl.ToRemoteCLIsList2 = g_RemoteCLIs;//20200610使用属性：g_RemoteCLIs——>g_SharpControl.gc_RemoteCLIs2：Data使用
                    ImportCLIFlag = true;//CLI导入标志
                }
                else
                {
                    g_SharpControl.tempJobItems.Clear();
                    g_SharpControl.UILayerCount = 0;
                    g_RemoteCLIs.Clear();//20201110新增：消除重复添加无效BUG
                    g_SharpControl.ToRemoteCLIsList = g_RemoteCLIs;
                    g_SharpControl.ToRemoteCLIsList2 = g_RemoteCLIs;

                    ImportCLIFlag = false;//CLI导入标志
                }
                this.Invalidate();
                this.renderControl1.Invalidate();

            }
            catch (Exception e)
            {
                MessageBox.Show("数据加载异常：" + e.ToString());
            }
        }

#if true//20200528测试;
        RemoteCLIs c_RemoteCLIs = /*new RemoteCLIs()*/null;//第1层的全部零件数据：便于测试：全局存放所有的数据
        List<RemoteCLIs> g_RemoteCLIs = new List<RemoteCLIs>();
        int g_CurrentCLI = 0;//当前显示交互的层数
        private int SetCurrentCLI//非常关键
        {
            set
            {
                if (value <= g_SharpControl.UILayerCount)
                { g_CurrentCLI = value; }
            }
            get { return g_CurrentCLI; }
        }
#endif
        /// <summary>
        /// 检索并生成1层的所有零件的与处理后的CLI数据。
        /// </summary>
        /// <param name="_3DP_GUI"></param>
        /// <param name="layerIndex"></param>
        /// <param name="FilePath"></param>
        private bool GetLayerCLIs(int layerIndex)//20200527新建：从CreateFinalJOB处获取//20200529新增：返回值标志是否调用成功
        {
            /*RemoteCLIs*/
            c_RemoteCLIs = new RemoteCLIs();//20200506新建：存放1层的所有的绘制数据，即是多个零件数据//移动到全局区
            for (int i = 0; i < g_SharpControl.tempJobItems.Count(); i++)//20200505新增：遍历所有零件
            {
                float x = (float)g_SharpControl.tempJobItems[i].position.X;
                float y = (float)g_SharpControl.tempJobItems[i].position.Y;
                float width = (float)g_SharpControl.tempJobItems[i].Width;//20201108新增
                float height = (float)g_SharpControl.tempJobItems[i].Height;//20201108新增

                ///20200506新建批注：完成CLI与（X,Y）排版位置信息的匹配工作
                CLI correctCLI = CliStreams.Find(t => t.ID.Equals(g_SharpControl.tempJobItems[i].id));//返回满足指定谓词条件的第1个返回值
                if (layerIndex <= (correctCLI.LayerLine.Count() - 1))
                {
                    //跨进程调用：20200506新建// (2)跨进程调用对象关键:Invoke a method on the remote object.//(2-1)附加位置消息结构
                    CountCLI tempCountCLI = new CountCLI();
                    tempCountCLI.x = x;
                    tempCountCLI.y = y;
                    tempCountCLI.deltaX = (float)g_SharpControl.tempJobItems[i].deltaX;
                    tempCountCLI.deltaY = (float)g_SharpControl.tempJobItems[i].deltaY;

                    tempCountCLI.width = width;//20201108新增
                    tempCountCLI.height = height;//20201108新增
                    tempCountCLI.aLayer = correctCLI.LayerLine[layerIndex];
                    //(2-2)附加远程信息结构
                    //RemoteCLIs tempRemoteCLIs = new RemoteCLIs();
                    c_RemoteCLIs.layerIndex = layerIndex;
                    c_RemoteCLIs.aLayerData.Add(tempCountCLI);

                    //return true;
                }
                else//指定层的特别零件的零件不存在：跳过
                {
                    //return false;
                }
            }
            return true;
            //20200506新增：跨本进程及其他进程调用的关键数据结构
            //accessServer.SendDataToRemote(c_RemoteCLIs, layerIndex);//(1) 发送IPC信息
        }


        /// 定义一个代理
        private delegate void UpComposPicDelegate(List<JobItem> jobItems);//UpdateComposationPicDelegate
        private void RegisterComposPic(List<JobItem> jobItems)//register the JobItems to the 主界面线程的人机交互组件中
        {
            if (this.panel3.InvokeRequired == false)//如果调用该函数的线程和控件listMain位于同一个线程内
            {
                _3DP_GUI.RegisterComposPic(jobItems);//down the jobitems to the mainform thread: and the main work in this function are reset and register picture to 
                for (_3DP_GUI.num_p = 0; _3DP_GUI.num_p < _3DP_GUI.PX.Count; _3DP_GUI.num_p++)//———————————5这个值是临时测试的,COUNT才是正确的——————！！！！
                {
                    _3DP_GUI.PX[_3DP_GUI.num_p] = (int)_3DP_GUI.px[_3DP_GUI.num_p] * _3DP_GUI.pixel2MM + (this.panel3.Width / 2 + 20);
                    _3DP_GUI.PY[_3DP_GUI.num_p] = (int)_3DP_GUI.py[_3DP_GUI.num_p] * _3DP_GUI.pixel2MM + (this.panel3.Height / 2 + 20);
                }
                //进行即时的刷新显示
                _3DP_GUI.SetBitmap(this.panel3.Width, this.panel3.Height);//——————创建容器，并设定大小
                Graphics g = Graphics.FromImage(_3DP_GUI.bt);
                _3DP_GUI.Select_rectangle(g, this.panel3.Width, this.panel3.Height);
                this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));//最后完成显示—————需要移动到主界面———参数要确认———！！！！！ 

            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpComposPicDelegate UpdateComposationPic = new UpComposPicDelegate(RegisterComposPic);
                this.panel3.Invoke(UpdateComposationPic, jobItems);
            }
        }
        public void TranslateCliStreams(ref List<CLI> CliStreams, List<string> tempSelectPaths, LoadDataTransferObject transferObject, bool SingleDataFlag)
        {
            double maxX = 0; double maxY = 0; double minX = 0; double minY = 0;
            var tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(tempSelectPaths[0]));
            CLI MaxBorderCLI = new CLI();//20230320新建：
            double BorderCLIMinPos = 0;//20230320新建：

            if (null != tempCLIs)
            {
                if (SingleDataFlag == false)
                {
                    maxX = tempCLIs.Dimension[1].x;
                    maxY = tempCLIs.Dimension[1].y;
                    BorderCLIMinPos = tempCLIs.Dimension[0].y;//20230320新建：更新边界数据
                    minX = tempCLIs.Dimension[0].x;
                    minY = tempCLIs.Dimension[0].y;
                    //CLI MaxBorderCLI = new CLI();//20230320新建：
                    for (int m = 0; m < tempSelectPaths.Count(); m++)//20201113新增：更新最大区域
                    {
                        tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(tempSelectPaths[m]));
                        if (maxX < tempCLIs.Dimension[1].x)//更新最大尺寸
                        {
                            maxX = tempCLIs.Dimension[1].x;
                        }
                        else { }
                        if (maxY < tempCLIs.Dimension[1].y)//更新最大尺寸
                        {
                            maxY = tempCLIs.Dimension[1].y;
                            //MaxBorderCLI = tempCLIs;//20230320新建：更新边界数据
                            BorderCLIMinPos = tempCLIs.Dimension[0].y;//20230320新建：更新边界数据
                        }
                        else { }
                        if (minX > tempCLIs.Dimension[0].x)//更新最大尺寸
                        {
                            minX = tempCLIs.Dimension[0].x;
                        }
                        else { }
                        if (minY > tempCLIs.Dimension[0].y)//更新最大尺寸
                        {
                            minY = tempCLIs.Dimension[0].y;
                        }
                        else { }
                    }
                }
                else
                {
                    maxX = tempCLIs.Dimension[1].x;
                    maxY = tempCLIs.Dimension[1].y;
                    BorderCLIMinPos = tempCLIs.Dimension[0].y;//20230320新建：更新边界数据
                    minX = tempCLIs.Dimension[0].x;
                    minY = tempCLIs.Dimension[0].y;
                    //CLI MaxBorderCLI = new CLI();//20230320新建：
                    for (int m = 0; m < tempSelectPaths.Count(); m++)//20201113新增：更新最大区域
                    {
                        tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(tempSelectPaths[m]));
                        if (maxX < tempCLIs.Dimension[1].x)//更新最大尺寸
                        {
                            maxX = tempCLIs.Dimension[1].x;
                        }
                        else { }
                        if (maxY < tempCLIs.Dimension[1].y)//更新最大尺寸
                        {
                            maxY = tempCLIs.Dimension[1].y;
                            //MaxBorderCLI = tempCLIs;//20230320新建：更新边界数据
                            BorderCLIMinPos = tempCLIs.Dimension[0].y;//20230320新建：更新边界数据
                        }
                        else { }
                        if (minX > tempCLIs.Dimension[0].x)//更新最大尺寸
                        {
                            minX = tempCLIs.Dimension[0].x;
                        }
                        else { }
                        if (minY > tempCLIs.Dimension[0].y)//更新最大尺寸
                        {
                            minY = tempCLIs.Dimension[0].y;
                        }
                        else { }
                    }
                }
            }

            for (int m = 0; m < tempSelectPaths.Count(); m++)
            {
                var tempCli = CliStreams.Find(t => t.recordPathItem.Equals(tempSelectPaths[m]));
                if (null != tempCli)//重复性检查//确保存在所需必要的数据
                {
                    int tempindex = CliStreams.FindIndex(t => t.recordPathItem.Equals(tempSelectPaths[m]));
                    if (transferObject.translateMode == true)//绝对移动
                    {
                        double xtemptranslate = (transferObject.xTranslate - minX) + 165/*210*/;//20220530修改：
                        //20230320修改：
                        double ytemptranslate = (-transferObject.yTranslate + 330/*350*/ - maxY
                            - Math.Abs(maxY - BorderCLIMinPos)) - 165/*175*/;//20220530修改：
                        CliStreams[tempindex].Dimension[2].x = CliStreams[tempindex].Dimension[2].x + xtemptranslate;//tempCLIs.Dimension[2].x为为偏移值δx
                        CliStreams[tempindex].Dimension[2].y = CliStreams[tempindex].Dimension[2].y + ytemptranslate;//tempCLIs.Dimension[2].x为为偏移值δy
                        CliStreams[tempindex].Dimension[1].x = CliStreams[tempindex].Dimension[1].x + xtemptranslate;//实际位置值
                        CliStreams[tempindex].Dimension[1].y = CliStreams[tempindex].Dimension[1].y + ytemptranslate;//实际位置值
                        CliStreams[tempindex].Dimension[0].x = CliStreams[tempindex].Dimension[0].x + xtemptranslate;//实际位置值
                        CliStreams[tempindex].Dimension[0].y = CliStreams[tempindex].Dimension[0].y + ytemptranslate;//实际位置值
                    }
                    else//相对移动模式
                    {
                        CliStreams[tempindex].Dimension[2].x = CliStreams[tempindex].Dimension[2].x + transferObject.xDeltaTranslate;//tempCLIs.Dimension[2].x为为偏移值δx
                        CliStreams[tempindex].Dimension[2].y = CliStreams[tempindex].Dimension[2].y - transferObject.yDeltaTranslate;//tempCLIs.Dimension[2].x为为偏移值δy
                        CliStreams[tempindex].Dimension[1].x = CliStreams[tempindex].Dimension[1].x + transferObject.xDeltaTranslate;//实际位置值
                        CliStreams[tempindex].Dimension[1].y = CliStreams[tempindex].Dimension[1].y - transferObject.yDeltaTranslate;//实际位置值
                        CliStreams[tempindex].Dimension[0].x = CliStreams[tempindex].Dimension[0].x + transferObject.xDeltaTranslate;//实际位置值
                        CliStreams[tempindex].Dimension[0].y = CliStreams[tempindex].Dimension[0].y - transferObject.yDeltaTranslate;//实际位置值
                    }
                }
            }
        }

        public void ArrayCliStreams(ref List<CLI> CliStreams, List<string> tempSelectPaths, float xSpace, float ySpace, int xnum, int ynum)//20201112新增：输入必要的四个参数，完成相应的虚拟阵列
        {
            double maxX = 0; double maxY = 0; double minX = 0; double minY = 0;
            var tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(tempSelectPaths[0]));
            maxX = tempCLIs.Dimension[1].x;
            maxY = tempCLIs.Dimension[1].y;
            minX = tempCLIs.Dimension[0].x;
            minY = tempCLIs.Dimension[0].y;
            for (int m = 0; m < tempSelectPaths.Count(); m++)//20201113新增：更新最大区域
            {
                tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(tempSelectPaths[m]));
                if (maxX < tempCLIs.Dimension[1].x)//更新最大尺寸
                {
                    maxX = tempCLIs.Dimension[1].x;
                }
                else { }
                if (maxY < tempCLIs.Dimension[1].y)//更新最大尺寸
                {
                    maxY = tempCLIs.Dimension[1].y;
                }
                else { }
                if (minX > tempCLIs.Dimension[0].x)//更新最大尺寸
                {
                    minX = tempCLIs.Dimension[0].x;
                }
                else { }
                if (minY > tempCLIs.Dimension[0].y)//更新最大尺寸
                {
                    minY = tempCLIs.Dimension[0].y;
                }
                else { }
            }
            maxX = maxX - minX; maxY = maxY - minY;
            for (int j = 0; j < ynum; j++)//(2)Array in Y Direction
            {
                for (int i = 0; i < xnum; i++)//(1)Array in Y Direction
                {
                    //foreach (string path in tempSelectPaths)//(3)Copy all the CLI file visually:20201112新增
                    for (int m = 0; m < tempSelectPaths.Count(); m++)
                    {
                        /*var*/
                        tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(tempSelectPaths[m]/*path*/));
                        if (null != tempCLIs)//重复性检查//确保存在所需必要的数据
                        {
                            string tempPath = null;
                            if (i == 0 && j == 0)
                            {
                                //tempCLIs.Dimension[2].x = tempCLIs.Dimension[2].x;//tempCLIs.Dimension[2].x为为偏移值δx
                                //tempCLIs.Dimension[2].y = tempCLIs.Dimension[2].y;//tempCLIs.Dimension[2].x为为偏移值δy
                            }//避免重复增加阵列原始数据
                            else
                            {
                                var tempCli2 = tempCLIs.Clone();
                                int count = i;//虚拟阵列编号
                                /*string */
                                tempPath = RemoveLastChar(tempCli2.recordPathItem, ".cli");
                                tempPath = tempPath + "-copys-" + j + "-" + count + ".cli";//注意：此处是count不是i；j全是统一的，count是可能不一样的
                                while (null != CliStreams.Find(t => t.recordPathItem.Equals(tempPath)))//count
                                {
                                    count++;
                                    tempPath = RemoveLastChar(tempCli2.recordPathItem, ".cli");
                                    tempPath = tempPath + "-copys-" + j + "-" + count + ".cli";//注意：此处是count不是i；j全是统一的，count是可能不一样的
                                }
                                tempCli2.recordPathItem = tempPath;
                                tempCli2.Dimension[2].x = tempCli2.Dimension[2].x + xSpace * i + maxX * i;//tempCLIs.Dimension[2].x为为偏移值δx
                                tempCli2.Dimension[2].y = tempCli2.Dimension[2].y + ySpace * j + maxY * j;//tempCLIs.Dimension[2].x为为偏移值δy
                                tempCli2.Dimension[0].x = tempCLIs.Dimension[0].x + xSpace * i + maxX * i;//实际位置值
                                tempCli2.Dimension[0].y = tempCLIs.Dimension[0].y + ySpace * j + maxY * j;//实际位置值
                                tempCli2.Dimension[1].x = tempCLIs.Dimension[1].x + xSpace * i + maxX * i;//实际位置值
                                tempCli2.Dimension[1].y = tempCLIs.Dimension[1].y + ySpace * j + maxY * j;//实际位置值
                                //for (int k = 0; k < tempCli2.LayerNumber; k++)
                                //{
                                //    foreach (Layer tempLayer in tempCli2.LayerLine)
                                //    {
                                //        foreach (Line tempLine in tempLayer.LineList)
                                //        {
                                //            foreach (PointF[] tempLines in tempLine.Lines)
                                //            {
                                //                for (int l=0;l<tempLines.Count();l++)
                                //                {
                                //                    tempLines[l].X = tempLines[l].X+ xSpace * i;
                                //                    tempLines[l].Y = tempLines[l].Y+ ySpace * j;
                                //                }
                                //            }
                                //        }
                                //    }
                                //}
                                CliStreams.Add(tempCli2);//(2)Copy the CLIdata visually：20201112新增
                                UpdateListView(tempPath/*tempSelectPaths[m]*//*path*/, 6, tempCli2.LayerNumber);//20201111新增：完成JobList的更新
                            }
                        }
                        else { }
                    }
                }
            }
        }

        public string RemoveLastChar(string str, string value)//20201112新增：删除string末尾指定的字符串
        {
            int startIndex = str.LastIndexOf(value);
            if (startIndex != -1)
            {
                return str.Substring(0, startIndex);
            }
            return str;
        }

        private ComposationCLI composationCLI = new ComposationCLI();
        //Composation the TIFF Picture,and save the TIFF picture information to the Data Structure
        //input parameters: TIFF path, TIFF id, TIFF NUM, TIFF position
        //calculate: TIFF position for each TIFF path(also the each TIFF picture)
        //output parameters: TIFFinformatino contains{TIFF path, TIFF id, TIFF NUM, TIFF position}
        private void Composation(ref List<CLI> CliStreams/*, string CadOperationCode*/)
        {
            //TIFFPath//TIFFPath包含了TIFF的基本所有信息
            //CliStreams//CliStreams包含了CLI的基本所有的信息——本信息对于排版非常重要

            //ComposationCLI composationCLI = new ComposationCLI();
            composationCLI.Composation(ref CliStreams, 2, 2/*, CadOperationCode*/);//内部安全和外部安全距离默认采取2mm

            //_3DP_GUI.CLInum = 4;//4为测试值
        }

        /// 定义一个代理：加载CLI过程中,实时刷新实际打印CAD文件的数量
        private delegate void UpdateListViewDelegate(string tempPaths, int OperationType, int layerNumber);
        private void UpdateListView(string tempPaths, int OperationType, int layerNumber)
        {
            if (this.listView2.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
            {
                UpdateTheJobListView(tempPaths, OperationType, layerNumber);
            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpdateListViewDelegate DMSGD = new UpdateListViewDelegate(UpdateListView);
                this.listView2.Invoke(DMSGD, tempPaths, OperationType, layerNumber);
            }
        }

        /// 定义一个代理：加载CLI过程中刷新数据
        private delegate void UpdateLayerNumDelegate(int i, string ReadLayerNum);
        private void UpdateLayerNum(int i, string ReadLayerNum)
        {
            if (this.listView2.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
            {
                listView2.Items[i].SubItems[2].Text = ReadLayerNum;//本行代码才可以正确运行
            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpdateLayerNumDelegate DMSGD = new UpdateLayerNumDelegate(UpdateLayerNum);
                this.listView2.Invoke(DMSGD, i, ReadLayerNum);
            }
        }
        /// 定义一个代理：加载CLI完成后，刷新总层数；
        private delegate void UpdateGUILayerDelegate(int LayerCount);
        private void UpdateGUILayer(int LayerCount)
        {
            if (this.listView2.InvokeRequired == false)
            {
                HScrollBar.Maximum = LayerCount;
                HScrollBar.ValueChanged += new System.EventHandler(this.hScrollBar2_ValueChanged);
#if true
                g_nLayerStart = 1;//20230419修改：
                g_nLayerEnd = LayerCount;//20230419修改：
                g_bSelectedLayerRangeReady = false;
                this.LayerStart.Text = g_nLayerStart.ToString();
                this.LayerEnd.Text = g_nLayerEnd.ToString();
                this.LayerStart.Enabled = true;//20200602
                this.LayerEnd.Enabled = true;//20200602      
#endif
            }
            else
            {
                UpdateGUILayerDelegate DMSGD = new UpdateGUILayerDelegate(UpdateGUILayer);
                this.HScrollBar.Invoke(DMSGD, LayerCount);
            }
        }

        /// 定义一个代理：打印过程中，刷新并显示2D渲染图形
        private delegate void Rendering2DDelegate(int LayerCount);
        private void Rendering2D(int CurrentLayerIndex)//20200601新增
        {
            if (this.listView2.InvokeRequired == false)
            {
                //20200601：实现成形层的逐层预览刷新;类似于HScrollBar控件的事件处理
                ImportCLIFlag = false;//标志：CLI导入中或者未导入
                SetCurrentCLI = CurrentLayerIndex;
                ImportCLIFlag = true;//CLI导入标志
                this.renderControl1.Invalidate();
            }
            else
            {
                Rendering2DDelegate DMSGD = new Rendering2DDelegate(Rendering2D);
                this.HScrollBar.Invoke(DMSGD, CurrentLayerIndex);
            }
        }
        /// 定义一个代理：打印过程中，刷新并显示2D渲染图形
        private delegate void PrinterRunInfoDelegate(string RunInfoString);
        private void PrinterRunInfo(string RunInfoString)//20200601新增
        {
            if (this.listView2.InvokeRequired == false)
            {
                //20200601：实现成形层的逐层预览刷新;类似于HScrollBar控件的事件处理
                this.Text = "LASERADD-BinderJetter" + RunInfoString;
                this.Invalidate();
            }
            else
            {
                PrinterRunInfoDelegate DMSGD = new PrinterRunInfoDelegate(PrinterRunInfo);
                this.listView2.Invoke(DMSGD, RunInfoString);
            }
        }

        /// 定义一个代理：加载CLI完成后，刷新总层数；//20200610新增：测试完需要更改
        private delegate void UpdateDataAndTransferDelegate(int LayerCount, int subLayerCount, int OperationFlag);
        private void UpdateDataAndTransfer(int LayerCount, int subLayerCount, int OperationFlag)//20201119修改：
        {
            if (this.listView2.InvokeRequired == false)
            {
                switch (OperationFlag)
                {
                    case 1://PrintTaskTHREAD更新层数       
                        //SetCurrentCLI = LayerCount;//使用属性方式管理//20200601：实现成形层的逐层预览刷新;类似于HScrollBar控件的事件处理
                        ImportCLIFlag = true;//CLI导入标志
                        SetCircularProgressValueSafe(circularProgressBar1, LayerCount * g_nRePrintTimes + (subLayerCount + 1), "UpdateDataAndTransfer(case1)");
                        circularProgressBar1.Text = (((double)LayerCount + ((double)(subLayerCount + 1) / (double)g_nRePrintTimes)) / ((double)g_nLayerEnd + 1)).ToString("P1"/*"P0"*/)/*+"%"*/;//精华：20200504新增批注
                        this.Text = "打印数据：当前处理第" + (LayerCount + g_nLayerStart + 1) + "-" + (subLayerCount + 1) + "层";
                        this.Invalidate();

                        break;
                    case 2://启动打印
                        this.Text = "LASERADD-BinderJetter";
                        this.LayerEnd.Enabled = true;//可修改打印区间
                        this.LayerStart.Enabled = true;//可修改打印区间
                        this.PrintBtn.Text = "启动" + "\n" + "打印";
                        this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PrintBtn.BackgroundImage = Resource.Continue_38x38;

                        SetCircularProgressValueSafe(circularProgressBar1, 0, "UpdateDataAndTransfer(case2/cpb1)");
                        circularProgressBar1.Text = (((double)0) / ((double)g_nLayerEnd + 1)).ToString("P1");//精华：20200504新增批注
                        SetCircularProgressValueSafe(circularProgressBar2, 0, "UpdateDataAndTransfer(case2/cpb2)");//20201121新增：
                        circularProgressBar2.Text = (((double)0) / ((double)g_nLayerEnd + 1)).ToString("P1");//精华：20200504新增批注

                        PrintBtn.Tag = 1;

                        this.PauseBtn.Text = "暂停" + "\n" + "打印";//补充代码：暂停加工 //修改按钮状态为：继续加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Keep_38x38;
                        PrintConrolFlag = "KeepPrint";//20200618批注：暂停打印
                        PauseBtn.Tag = 1;

                        break;
                    case 3://正在启动
                        this.LayerEnd.Enabled = false;
                        this.LayerStart.Enabled = false;
                        this.PrintBtn.Text = "正在" + "\n" + "启动";
                        this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PrintBtn.BackgroundImage = Resource.StopJob_38x38;
                        PrintBtn.Tag = 3;

                        break;
                    case 4://关闭打印
                        this.LayerEnd.Enabled = false;//不可修改打印区间
                        this.LayerStart.Enabled = false;//不可修改打印区间
                        this.PrintBtn.Text = "关闭" + "\n" + "打印";
                        this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PrintBtn.BackgroundImage = Resource.StopJob_38x38;

                        PrintBtn.Tag = 2;

                        break;

                    case 5://更新暂停打印button提示为：暂停打印
                        this.LayerEnd.Enabled = false;//不可修改打印区间
                        this.LayerStart.Enabled = false;//不可修改打印区间
                        this.PrintBtn.Text = "关闭" + "\n" + "打印";
                        this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PrintBtn.BackgroundImage = Resource.StopJob_38x38;
                        PrintBtn.Tag = 2;

                        this.PauseBtn.Text = "暂停" + "\n" + "打印";//补充代码：暂停加工 //修改按钮状态为：继续加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Keep_38x38;
                        PrintConrolFlag = "KeepPrint";//20200618批注：暂停打印
                        PauseBtn.Tag = 1;

                        break;
                    case 6://更新暂停打印button提示为：继续打印
                        this.PauseBtn.Text = "继续" + "\n" + "打印"; //补充代码：继续加工 //修改按钮状态为：停止加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Stop_绿_38x38;
                        PrintConrolFlag = "PausePrint";//20200618批注：继续打印
                        PauseBtn.Tag = 2;

                        break;

                    case 7:
                        this.Text = "LASERADD-BinderJetter";

                        this.PauseBtn.Text = "暂停" + "\n" + "打印";//补充代码：暂停加工 //修改按钮状态为：继续加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Keep_38x38;
                        PrintConrolFlag = "KeepPrint";//20200618批注：暂停打印
                        PauseBtn.Tag = 1;
                        break;

                    case 8://加载CLI完成后，刷新总层数
                        Log4Net.Info($"UpdateDataAndTransfer(case8): enter, LayerCount={LayerCount}, subLayerCount={subLayerCount}, OperationFlag={OperationFlag}, g_PrintSchedule={g_PrintSchedule}, g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}, TransferModifyFlag={TransferModifyFlag}");
                        this.Text = "打印完成！！";

                        FinalJOBThreadExistedFlag = false;//20200415批注：个人感觉可以去掉，使用后台辅助工作的话
                        TransferModifyFlag = "StartFlag";//传送取消标志位
                        Log4Net.Info($"UpdateDataAndTransfer(case8): exit, FinalJOBThreadExistedFlag={FinalJOBThreadExistedFlag}, TransferModifyFlag={TransferModifyFlag}");
                        break;

                    case 9://正在更改续打
                        this.LayerStart.BackColor = Color.MintCream;//可修改打印区间
                        this.LayerEnd.BackColor = Color.MintCream;//可修改打印区间
                        this.LayerEnd.Enabled = false;
                        this.LayerStart.Enabled = false;
                        this.PrintBtn.Text = "正在" + "\n" + "启动";
                        this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PrintBtn.BackgroundImage = Resource.StopJob_38x38;
                        PrintBtn.Tag = 3;

                        this.PauseBtn.Text = "正在" + "\n" + "续打";//补充代码：暂停加工 //修改按钮状态为：继续加工
                        this.PauseBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PauseBtn.BackgroundImage = Resource.Keep_38x38;
                        PrintConrolFlag = "KeepPrint";//20200618批注：暂停打印
                        PauseBtn.Tag = 1;

                        break;

                    case 10://正在关闭打印
                        this.LayerEnd.Enabled = false;
                        this.LayerStart.Enabled = false;
                        this.PrintBtn.Text = "正在" + "\n" + "关闭";
                        this.PrintBtn.TextAlign = ContentAlignment.MiddleRight;
                        this.PrintBtn.BackgroundImage = Resource.StopJob_38x38;

                        break;

                    case 11://校准打印相关：打印初始化
                        break;

                    case 12://校准打印相关：正在启动，修改为停止校准打印
                        break;

                    case 13://校准打印相关：启动失败
                        break;

                    default:
                        break;
                }

            }
            else
            {
                UpdateDataAndTransferDelegate DMSGD = new UpdateDataAndTransferDelegate(UpdateDataAndTransfer);
                this.listView2.BeginInvoke(DMSGD, LayerCount, subLayerCount, OperationFlag);
            }
        }
        /// <summary>
        /// 按钮事件：导入解析CLI文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImportCliBtn_Click(object sender, EventArgs e)
        {
            string msg = "进入数据加载模块";
            Log4Net.Info(msg);

            //(1) 判断打开的路径是否存在
            if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\CLI输入文件"))
            {
                Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\CLI输入文件");//创建路径
            }
            openFileDialog1.Filter = "(*.cli)|*.cli|(*.stl)|*.stl|(*.bjgroup)|*.bjgroup";//过滤器————//openFileDialog也有其内部的事件处理函数//20221125修改：添加新文件格式：.bjgroup
            string filePath1 = System.Windows.Forms.Application.StartupPath + @"\CLI输入文件";//输入的CLI文件的存放目录。
            openFileDialog1.InitialDirectory = filePath1;//打开文件对话框的默认初始化目录为：软件的根目录"CLI文件\\
#if false//20200614批注：本部分不需要
            //(2) 判断保存的路径是否存在
            if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\TIFF输出文件"))
            {
                Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\TIFF输出文件");//创建路径
            }
            string filePath2 = System.Windows.Forms.Application.StartupPath + @"\TIFF输出文件\";//生成的文件夹目录，默认放在此文件夹中。
#endif
            //(3) 正式开始读取CLI文件
            if (openFileDialog1.ShowDialog() == DialogResult.OK /*判断用户是否单击了OK*/&& openFileDialog1.FileName != string.Empty)
            {
                //string[] tempPath=openFileDialog1.FileNames;//获取所有选中项的文件名
                //tempPath = openFileDialog1.FileNames;//获取所有选中项的文件名
                //tempPath.Clear();
                tempPath.AddRange(openFileDialog1.FileNames);//获取所有选中项的文件名——用于动态的临时的添加

#if false//20201110新建批注：重复性检查置于线程中
                for (int i = tempPath.Count - 1; i >= 0; i--)
                {
                    if (recordPaths.Contains(tempPath[i]))//如果读取到的文件中的任何1项，原来留底的tempPath已经包含
                    {
                        //不要尝试改动循环的数组或者集合————可能会报错
                        tempPath.RemoveAt(i);//去掉其，避免重复————不要尝试改动循环的数组或者集合。
                    }
                }
                recordPaths.AddRange(tempPath);//将临时本次操作用的tempPath合并到recordPaths后面
                //PATH filter action End
#endif

#if false
                //初始化listview2
                //(0)upadate the CLI FILE informatin in the listview2
                //(0)update the four kinds information:ID+FileName+LayerNumber+CompeleteRate
                //(0)support the add+delete+modified and query action
                //listView2.Items.Clear();//20201110新增：消除重复计数问题
                foreach (string path in tempPath)//Path is the path of CLI file.
                {
                    //upadate the CLI FILE informatin in the listview2
                    //update the four kinds information:ID+FileName+LayerNumber+CompeleteRate
                    //support the add+delete+modified and query action
                    string ID = Convert.ToString(listView2.Items.Count + 1);
                    string FileName = System.IO.Path.GetFileName(path);//获取带扩展名的文件名称
                    string LayerNum = Convert.ToString(0);
                    string CompeleteRate = Convert.ToString(0) + "%";
                    ListViewItem item = new ListViewItem(ID);
                    item.SubItems.Add(FileName);
                    item.SubItems.Add(LayerNum);
                    item.SubItems.Add(CompeleteRate);
                    listView2.Items.Add(item);
                }
#endif
                //START THE LOAD CLI AND CREATE .TIFF/1bit.BMP/...and so on types THREAD
                LoadDataTransferObject transferObject = new LoadDataTransferObject();//1113新增：                       
                transferObject.CadOperationCode = "1";//20201110新增：指令1为增添CAD指令；指令2为删减CAD指令；指令3为平移指令；指令4为SCALE指令；指令5为镜像指令。
                LoadCLIThread = new Thread(new ParameterizedThreadStart(RunLoadCLIThread)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                LoadCLIThread.Name = "CadOperationThread";//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                LoadCLIThread.Start(transferObject/*CadOperationCode*/);//(3)第3部曲：多线程3步曲 //开启机械CLI的线程
            }
        }

        private void UpdateTheJobListView(string tempPaths, int OperationType, int layernumber)//20201111新增：
        {
            //(0)upadate the CLI FILE informatin in the listview2
            //(0)update the four kinds information:ID+FileName+LayerNumber+CompeleteRate//(0)support the add+delete+modified and query action
            //listView2.Items.Clear();//20201110新增：消除重复计数问题

            switch (OperationType)
            {
                case 1://ADD
                    //upadate the CLI FILE informatin in the listview2
                    //update the four kinds information:ID+FileName+LayerNumber+CompeleteRate
                    //support the add+delete+modified and query action
                    string ID = Convert.ToString(listView2.Items.Count + 1);
                    string FileName = System.IO.Path.GetFileName(tempPaths);//获取带扩展名的文件名称
                    string LayerNum = Convert.ToString(layernumber/*0*/);//string CompeleteRate = Convert.ToString(0) + "%";
                    ListViewItem item = new ListViewItem(ID);
                    item.SubItems.Add(FileName);
                    item.SubItems.Add(LayerNum);//item.SubItems.Add(CompeleteRate);
                    item.SubItems.Add(tempPaths);//20201111新增：记录路径
                    item.ToolTipText = tempPaths;//20201112新增：增加ToolTip
                    listView2.Items.Add(item);
                    break;
                case 2://DELETE
                    //CliStreams.Find(t => t.recordPathItem.Equals(path)
                    //int item2 = listView2.Items.Find(tempPaths, true)[0].Index;

                    //if (item2 != null)
                    //{   
                    //    listView2.Items.RemoveAt(item2);//ListViewItem item2 = listView2.Items.Find(tempPaths, true).First();
                    //}

                    //ListViewItem item2 = listView2.Items.Cast<ListViewItem>()
                    //         .FirstOrDefault(x => x.Text == "tempPaths");//var item2 = listView2.FindItemWithText("tempPaths");

                    var item2 = this.listView2.Items.Cast<ListViewItem>()
                        .Where(x => (/*x.Text == "Some Text" ||*/
                               x.SubItems[3].Text == tempPaths/*"Some Text"*/) /*&& x.Group.Name == "group1"*/)
                        .FirstOrDefault();
                    if (item2 != null)
                    {
                        listView2.Items.Remove(item2);//ListViewItem item2 = listView2.Items.Find(tempPaths, true).First();
                    }
                    break;
                case 3://MOVE
                    break;
                case 4://SCALE
                    break;
                case 5://MIRROR
                    break;
                case 6://vISUAL ADD//20201112新增:
                    ID = Convert.ToString(listView2.Items.Count + 1);
                    FileName = System.IO.Path.GetFileName(tempPaths);//获取带扩展名的文件名称
                    LayerNum = Convert.ToString(layernumber/*0*/);//string CompeleteRate = Convert.ToString(0) + "%";
                    item = new ListViewItem(ID);
                    item.SubItems.Add(FileName);
                    item.SubItems.Add(LayerNum);//item.SubItems.Add(CompeleteRate);
                    item.SubItems.Add(tempPaths);//20201111新增：记录路径
                    item.ToolTipText = tempPaths;//20201112新增：增加ToolTip
                    listView2.Items.Add(item);
                    break;
            }

        }
        private void CADToolStripMenuItem_Click(object sender, EventArgs e)//20201110新增：动态删除CAD数据、编辑CAD数据
        {
            int myTag = Convert.ToInt32((sender as ToolStripMenuItem).Tag);
            switch (myTag)
            {
                case 1://存在选中时：删除选中CAD数据
                    if (true)
                    {
                        LoadDataTransferObject transferObject = new LoadDataTransferObject();//1113新增：                       
                        transferObject.CadOperationCode = "2";//20201110新增：指令1为增添CAD指令；指令2为删减CAD指令；指令3为平移指令；指令4为SCALE指令；指令5为镜像指令。
                        LoadCLIThread = new Thread(new ParameterizedThreadStart(RunLoadCLIThread)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                        LoadCLIThread.Name = "CadOperationThread";//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                        LoadCLIThread.Start(transferObject/*CadOperationCode*/);//(3)第3部曲：多线程3步曲 //开启机械CLI的线程
                    }
                    break;
                case 2://Translate：存在选中时，移动选中CAD数据
                    平移操作 g = new 平移操作();//20201112新增:阵列拷贝完成编辑阵列
                    double maxX = 0; double maxY = 0; double minX = 0; double minY = 0;
                    var tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(g_SharpControl.selectPaths[0]));
                    double BorderCLIMinPos = 0;//20230320新建：

                    if (null != tempCLIs)
                    {
                        maxX = tempCLIs.Dimension[1].x;
                        maxY = tempCLIs.Dimension[1].y;
                        BorderCLIMinPos = tempCLIs.Dimension[0].y;//20230320新建：更新边界数据
                        minX = tempCLIs.Dimension[0].x;
                        minY = tempCLIs.Dimension[0].y;
                        for (int m = 0; m < g_SharpControl.selectPaths.Count(); m++)//20201113新增：更新最大区域
                        {
                            tempCLIs = CliStreams.Find(t => t.recordPathItem.Equals(g_SharpControl.selectPaths[m]));
                            if (maxX < tempCLIs.Dimension[1].x)//更新最大尺寸
                            {
                                maxX = tempCLIs.Dimension[1].x;
                            }
                            else { }
                            if (maxY < tempCLIs.Dimension[1].y)//更新最大尺寸
                            {
                                maxY = tempCLIs.Dimension[1].y;
                                BorderCLIMinPos = tempCLIs.Dimension[0].y;//20230320新建：更新边界数据
                            }
                            else { }
                            if (minX > tempCLIs.Dimension[0].x)//更新最大尺寸
                            {
                                minX = tempCLIs.Dimension[0].x;
                            }
                            else { }
                            if (minY > tempCLIs.Dimension[0].y)//更新最大尺寸
                            {
                                minY = tempCLIs.Dimension[0].y;
                            }
                            else { }
                        }
                    }
                    g.k_VirtualArrayParam.m_dXTranslate = -(maxX - minX) / 2;
                    g.k_VirtualArrayParam.m_dYTranslate = -(maxY - minY) / 2 - Math.Abs(maxY - BorderCLIMinPos);
                    g.k_VirtualArrayParam.m_bTranslateMode = true;
                    DialogResult result2 = g.ShowDialog();
                    if (result2 == DialogResult.OK)
                    {
                        LoadDataTransferObject transferObject = new LoadDataTransferObject();//1113新增:
                        transferObject.translateMode = g.k_VirtualArrayParam.m_bTranslateMode;

                        transferObject.xTranslate = g.k_VirtualArrayParam.m_dXTranslate;
                        transferObject.yTranslate = g.k_VirtualArrayParam.m_dYTranslate;
                        transferObject.zTranslate = g.k_VirtualArrayParam.m_dZTranslate;
                        transferObject.xDeltaTranslate = g.k_VirtualArrayParam.m_dXDeltaTranslate;
                        transferObject.yDeltaTranslate = g.k_VirtualArrayParam.m_dYDeltaTranslate;
                        transferObject.zDeltaTranslate = g.k_VirtualArrayParam.m_dZDeltaTranslate;

                        transferObject.CadOperationCode = "3";//20201112新增：指令1为增添CAD指令；指令2为删减CAD指令；指令3为平移指令；指令4为SCALE指令；指令5为镜像指令；指令6为虚拟阵列新增指令；
                        //transferObject.xnum = f.k_VirtualArrayParam.Xnum;//1113新增：
                        //transferObject.ynum = f.k_VirtualArrayParam.Ynum;//1113新增：
                        //transferObject.xSpace = f.k_VirtualArrayParam.XSpace;//1113新增：
                        //transferObject.ySpace = f.k_VirtualArrayParam.YSpace;//1113新增：

                        LoadCLIThread = new Thread(new ParameterizedThreadStart(RunLoadCLIThread)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                        LoadCLIThread.Name = "CadOperationThread";//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                        LoadCLIThread.Start(transferObject /*CadOperationCode*/);//(3)第3部曲：多线程3步曲 //开启机械CLI的线程//1113修改：
                    }
                    else if (result2 == DialogResult.Cancel)
                    { }

                    break;
                case 3:
                    break;
                case 4:
                    阵列拷贝 f = new 阵列拷贝();//20201112新增:阵列拷贝完成编辑阵列
                    DialogResult result = f.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        //int xnum = f.k_VirtualArrayParam.Xnum;//1113新增：
                        //int ynum = f.k_VirtualArrayParam.Ynum;//1113新增：
                        //double xSpace = f.k_VirtualArrayParam.XSpace;//1113新增：
                        //double ySpace = f.k_VirtualArrayParam.YSpace;//1113新增：

                        LoadDataTransferObject transferObject = new LoadDataTransferObject();//1113新增：                       
                        transferObject.CadOperationCode = "6";//string CadOperationCode = "6";//20201112新增：指令1为增添CAD指令；指令2为删减CAD指令；指令3为平移指令；指令4为SCALE指令；指令5为镜像指令；指令6为虚拟阵列新增指令；
                        transferObject.xnum = f.k_VirtualArrayParam.Xnum;//1113新增：
                        transferObject.ynum = f.k_VirtualArrayParam.Ynum;//1113新增：
                        transferObject.xSpace = f.k_VirtualArrayParam.XSpace;//1113新增：
                        transferObject.ySpace = f.k_VirtualArrayParam.YSpace;//1113新增：

                        LoadCLIThread = new Thread(new ParameterizedThreadStart(RunLoadCLIThread)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                        LoadCLIThread.Name = "CadOperationThread";//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                        LoadCLIThread.Start(transferObject /*CadOperationCode*/);//(3)第3部曲：多线程3步曲 //开启机械CLI的线程//1113修改：
                    }
                    else if (result == DialogResult.Cancel)
                    { }
                    break;
                case 5://退出时：不执行操作
                    break;
            }
        }
        public class LoadDataTransferObject//20201113新增;
        {
            public string CadOperationCode;
            //阵列拷贝参数
            public int xnum = 1;//阵列拷贝参数//阵列个数X向
            public int ynum = 1;//阵列拷贝参数//阵列个数Y向
            public double xSpace = 5;//阵列拷贝参数//单位为MM//阵列间距X向
            public double ySpace = 5;//阵列拷贝参数//单位为MM//阵列间距Y向
            //平移参数
            public bool translateMode = true;//true为绝对移动量，false为相对移动量
            public double xTranslate = 0;
            public double yTranslate = 0;
            public double zTranslate = 0;
            public double xDeltaTranslate = 0;
            public double yDeltaTranslate = 0;
            public double zDeltaTranslate = 0;

        }

        private void 删除选中数据DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int myTag = Convert.ToInt32((sender as ToolStripMenuItem).Tag);
            switch (myTag)
            {
                case 1://JOB列表存在选中时：删除选中CAD数据
                    if (this.listView2.SelectedItems.Count != 0)
                    {
                        g_SharpControl.selectPaths.Clear();//复位选中的数据
                        foreach (ListViewItem tempitems in this.listView2.SelectedItems)
                        {
                            g_SharpControl.selectPaths.Add(tempitems.ToolTipText);
                            listView2.Items.Remove(tempitems);//删除JOB栏的数据
                        }
                        LoadDataTransferObject transferObject = new LoadDataTransferObject();//1113新增：                       
                        transferObject.CadOperationCode = "2";//20201110新增：指令1为增添CAD指令；指令2为删减CAD指令；指令3为平移指令；指令4为SCALE指令；指令5为镜像指令。
                        LoadCLIThread = new Thread(new ParameterizedThreadStart(RunLoadCLIThread)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                        LoadCLIThread.Name = "CadOperationThread";//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                        LoadCLIThread.Start(transferObject/*CadOperationCode*/);//(3)第3部曲：多线程3步曲 //开启机械CLI的线程
                    }
                    else { }
                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4://退出时：不执行操作
                    break;
            }
        }

        //int g_nPrepareDeleteFlag = 1;//201105新增：默认为1，等待开启选中
        //Point[] g_pSelectArea=new Point[2];//201105新增：//选中操作区域实际上是2点矩形//更新2点矩形坐标即可
        private void renderControl1_MouseDown(object sender, MouseEventArgs e)//201105新增：动态删除加工零件——开启绘制
        {
            if (e.Button == MouseButtons.Left)//鼠标左键按下//选中操作区域实际上是2点矩形//更新2点矩形坐标即可
            {
                g_SharpControl.k_nPrepareDeleteFlag = 2;//开启选中操作
                g_SharpControl.k_pSelectArea[0] = renderControl1.PointToClient(Cursor.Position);//复位
                g_SharpControl.k_pSelectArea[1] = renderControl1.PointToClient(Cursor.Position);//复位
            }
            else { }
        }
        private void renderControl1_MouseUp(object sender, MouseEventArgs e)//201105新增：动态删除加工零件——结束绘制
        {
            if (e.Button == MouseButtons.Left)//鼠标左键按下//选中操作区域实际上是2点矩形//更新2点矩形坐标即可
            {
                //绘制的方框实际上是2点矩形//更新2点矩形坐标即可
                g_SharpControl.k_nPrepareDeleteFlag = 3;//终止选中操作
                this.Invalidate(); this.renderControl1.Invalidate();//刷新显示
                if (g_SharpControl.selectPaths.Count() >= 1)
                {
                    UpdateListViewInGUI(g_SharpControl.selectPaths, 2);//20201112新增：复位选中更新
                    UpdateListViewInGUI(g_SharpControl.selectPaths, 1);//20201112新增：选中更新
                }
                else
                {
                    UpdateListViewInGUI(g_SharpControl.selectPaths, 2);//20201112新增：复位选中更新
                }
            }
            else if (e.Button == MouseButtons.Right)//鼠标右键按下弹起，执行选中操作，是否删除
            {
                g_SharpControl.k_nPrepareDeleteFlag = 4;//编辑选中CAD数据
                //this.Invalidate(); this.renderControl1.Invalidate();//刷新显示
            }
            else
            { }
        }

        private void UpdateListViewInGUI(List<string> selectPaths, int OperationType)//20201112新增;
        {
            switch (OperationType)
            {
                case 1://选中更新
                    foreach (string tempPaths in selectPaths)
                    {
                        var item2 = this.listView2.Items.Cast<ListViewItem>()
                           .Where(x => (x.SubItems[3].Text == tempPaths))
                           .FirstOrDefault();
                        if (item2 != null)
                        {
                            item2.ForeColor = Color.Blue;//item2.Checked = true;//更新该条item状态为选中//貌似没什么效果
                            item2.BackColor = Color.Orange/*LightGoldenrodYellow*//*LightYellow*//*OrangeRed*//*LawnGreen*/;
                        }
                    }
                    break;
                case 2://复位选中更新：未选中任何有效数据
                    foreach (ListViewItem tempitems in this.listView2.Items)
                    {
                        if (tempitems != null)
                        {

                            tempitems.ForeColor = Color.Black;//item2.Checked = true;//更新该条item状态为选中//貌似没什么效果
                            tempitems.BackColor = Color.MintCream;
                        }
                    }
                    break;
                case 3://
                    break;
                case 4://正常退出
                    break;
            }
        }

        private void renderControl1_MouseMove(object sender, MouseEventArgs e)//201105新增：动态删除加工零件——移动刷新绘制
        {
            //绘制的方框实际上是2点矩形//更新2点矩形坐标即可
            if ((e.Button == MouseButtons.Left) && (g_SharpControl.k_nPrepareDeleteFlag == 2))//鼠标移动事件中，监测到左键按下:才持续绘图;且已存在选中
            {
                g_SharpControl.k_pSelectArea[1] = renderControl1.PointToClient(Cursor.Position);//更新选中区域//刷新//g_pSelectArea[0] = g_pSelectArea[0];//不更新选中区域//复位

                this.Invalidate(); this.renderControl1.Invalidate();//刷新显示 //刷新显示 

                //g_SharpControl.MouseWheelControl(e, point); 
                //g_SharpControl.PaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height/*+30*/));

            }
            else //当检测到左键未按下：绘制选中方框或者,啥都不做
            { }
        }

        /// <summary>
        /// 按钮事件：浏览生成 的TIFF文件夹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenTiffFile_Click(object sender, EventArgs e)//浏览生成的TIFF文件夹
        {
            string a = System.Windows.Forms.Application.StartupPath + @"\TIFF输出文件\";
            //判断打开的路径是否存在
            if (!Directory.Exists(a))//如果本文件不存在
            { }
            else//如果本文件夹存在
            { System.Diagnostics.Process.Start(a); }//打开文件夹浏览
        }

        /***********************************************开始：数据处理及传送部分***********************************************/
        /***********************************************开始：数据处理及传送部分***********************************************/
        /***********************************************开始：数据处理及传送部分***********************************************/
        //图片排版确认按钮：确认/停止
        public bool ImportCLIFlag = false;//初始为true，设置为false
        bool FinalJOBThreadExistedFlag = false;//JOBTHread是否创建过标志
        string TransferModifyFlag = "StartFlag";//默认标志位为开启传送
        private void PicComposeBtn_Click(object sender, EventArgs e)//图片排版确认按钮————点击之后不可以修改，双击之后才可以修改
        {
            ////if (ImportCLIFlag == true)//已经载入CAD文件//(1)检测标志位：是否载入CAD文件
            ////{
            ////    //(1-2)检测传送线程标志位：
            ////    if (FinalJOBThreadExistedFlag == false)//不存在传送线程
            ////    {
            ////        //（1）新建数据处理及传送线程:（耗时操作）
            ////        string tempThreadName = "DataTaskTHREAD";
            ////        Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
            ////        if (tempThread != null)
            ////        {
            ////            PrinterLogicThreads.Remove(tempThread);//以防万一
            ////        }
            ////        else//开启数据处理及传送线程:（耗时操作）
            ////        {
            ////            //(1)开启数据处理及传送线程
            ////            ThreadStart initThreadEntry = new ThreadStart(DataTaskTHREAD);//20200220:线程入口方法修改为联动线程
            ////            tempThread = new Thread(initThreadEntry) { IsBackground = true };
            ////            tempThread.Name = tempThreadName;
            ////            tempThread.Start();
            ////            PrinterLogicThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程

            ////            //(2)UI进行对应的设置：
            ////            //（A）修改按钮状态为：暂停输出
            ////            this.PicComposeBtn.Text = "暂停" + "\n" + "传送";
            ////            this.PicComposeBtn.TextAlign = ContentAlignment.MiddleRight;
            ////            this.PicComposeBtn.BackgroundImage = Resource.ComposeBlue_38x38;
            ////            FinalJOBThreadExistedFlag = true;//20200415批注：个人感觉可以去掉，使用后台辅助工作的话
            ////            //（B）开启panel3监听：改变panel3的控制状态
            ////            this.panel3.Enabled = true;//停止监听事件
            ////        }
            ////    }
            ////    else//存在传送线程
            ////    {
            ////        this.PicComposeBtn.Text = "修改" + "\n" + "传送";//修改按钮状态为：暂停输出
            ////        TransferModifyFlag = "PauseFlag";//传送取消标志位

            ////        JOB传送确认 a = new JOB传送确认(ImportCLIFlag);//(1) 弹出对话框，提示：确认要输出最终的排版，开始输出：       
            ////        DialogResult result = a.ShowDialog();
            ////        if (result == DialogResult.Abort)//取消当前传送：20200611关键代码：中止本线程工作
            ////        {
            ////            string tempThreadName = "DataTaskTHREAD";//(1)关闭联调线程
            ////            DeleteThread(tempThreadName);
            ////            this.PicComposeBtn.Text = "处理" + "\n" + "传送";//修改按钮状态为：暂停输出
            ////            FinalJOBThreadExistedFlag = false;//20200415批注：个人感觉可以去掉，使用后台辅助工作的话
            ////            TransferModifyFlag = "StartFlag";//传送取消标志位
            ////        }
            ////        else if (result == DialogResult.Cancel)//忽略传送
            ////        {
            ////            TransferModifyFlag = "CancelFlag";//忽略传送标志位
            ////        }
            ////        else
            ////        {
            ////        }
            ////    }
            ////}
            ////else//没有载入过CAD文件
            ////{
            ////    MessageBox.Show("请先载入CAD数据。。。");
            ////}
        }
        static ManualResetEventSlim _FinalJobEvent = new ManualResetEventSlim(false); //手动复位事件：线程同步————important for the thread communication: 如果初始事件状态为true,那么 AutoResetEvent实例的状态为signaled
        private int g_PrintSchedule = -1;//目的为任务间同步：初始态为-1；打印完成之后//20201118新增：全局的打印进度，用于协调数据线程和打印线程的启动时机
        /// <summary>
        /// 20200609：线程执行函数：生成FinalJOB线程内容:专门匹配后台辅助工作的新工作方式:20200415新增
        /// </summary>
        private void DataTaskTHREAD()//20200609新建：不适用异步，及多进程加速使用多线程加速
        {
            string msg = null;
            g_TaskThreadSTATE[3] = 2;//20201119新增：处于运行状态
            g_PrintSchedule = -1;//20201118新增：每次开启数据处理线程时，均复位
            Log4Net.Info($"数据处理线程：启动，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={TransferModifyFlag}，g_nLayerStart={g_nLayerStart}，g_nRePrintTimes={g_nRePrintTimes}");

            //（1）设置JOB-预备传送-核心代码：20200613批注：
            // 20200609批注：传输控制线程：关键在于，等待，当检测到内存不足的情况下，随即进入等待状态。计算能力远高于传输及下游处理能力时。
#if false
            StartDataTransControlThread(true, 0, 50);//此前的开启的异步数据传送线程
#else
            //(0-1) Start the Printing Configuration: SET the print file infomation and confirm the print file formatt
            //bool returnFlag = StartCloseJOB(false);//开启只执行1次，JOB并赋值关键打印参数；架构上本人后续再优化一下
            bool returnFlag = StartCloseJOB(true);//开启只执行1次，JOB并赋值关键打印参数；架构上本人后续再优化一下
#endif
            if (returnFlag == true)//20200801批注：修改为true//20210312修改：false状态为本地调试模式
            {
                royal.royal.g_prtimg_layer.nImgStartJetIndex = 0/*(int)(g_RYSYSParam.m_dYJetOff / 25.4 * 600)*/;//20210311新增：Y向起打位置修订//20210330修改：//20220524修改：修改为0

                DataTaskFlag = 2;//工作态标志//工作态不可强制暂停
                ReadLayerInfo();//20200611新增：更新指定的加工任务区间//（2）开始传送-核心代码：20200507新增：正式向远程传输数据
                Log4Net.Info($"数据处理线程：ReadLayerInfo完成，g_bSelectedLayerRangeReady={g_bSelectedLayerRangeReady}，g_nLayerStart={g_nLayerStart}，g_nLayerEnd={g_nLayerEnd}，g_nRePrintTimes={g_nRePrintTimes}");

                msg = $"进入：EquipmentMotionLogic3=》准备创建对象-手动操作！";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

                if (AutoPrintMotion4 == null)
                {
                    /*手动操作*/
                    AutoPrintMotion4 = new 手动操作(0, nValveStateMask,false);//20201030新增：读取自动打印参数//20230317修正:修正潜在的闪退问题
                    msg = $"创建：AutoPrintMotion4=》初次创建完成-手动操作！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }
                else
                {
                    msg = $"创建：AutoPrintMotion4=》不需重新创建-手动操作！";
                    Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                }

                bool returnCode = LoadAutoParamsFromJson(ref AutoPrintMotion4);//20201030新增：读取自动打印参数
                g_nRePrintTimes = AutoPrintMotion4.k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes;//20201030新增：读取自动打印参数                                                                                      //20220915新智能：读取清洗频率参数
                UpdateCircularBarMethod(1);//2020061:1：开启数据进度更新circular panel1:其中开启了对应的定时器                                

                int tempRePrintTimes = g_nRePrintTimes;
                for (int j = g_nLayerStart - g_nLayerStart; j <= g_nLayerEnd - g_nLayerStart; j++)//202006002批注：持续的输出零件：打印任务区间，j为起始层，20为终止层
                {
                    var layerStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    int layerSubCount = 0;
                    //UpdateDataAndTransfer(j,0, 1);//20200610:在标题栏刷新当前数据处理层//Rendering2D(j); //20200601：实现成形层的逐层预览刷新
                    switch (TransferModifyFlag)//无论如何，应该等待1层执行完成，再做定夺。这比较合理
                    {
                        case "StartFlag":
                            {//以下是数据处理的核心
                                DataTaskFlag = 2;//工作态标志//工作态不可强制暂停

                                g_SharpControl.XDpi = g_RYSYSParam.m_XPrintDpi[g_RYSYSParam.XPrintDpiIndex]/*(float)(Convert.ToDouble(g_RYSYSParam.XPrintDpi))*/;
                                int i = 0;
                                for (/*int*/i = 0; i < tempRePrintTimes; i++)//20201030新增：按照重喷次数发送数据量
                                {
                                    while (((j * tempRePrintTimes + i+1) - 10/*3*/ >= g_nCurrentPrintLayerID) && (TransferModifyFlag == "StartFlag"))//发送大于打印进度前20层的数据即可//20230509新建：发送10层的数据
                                    {
                                        Log4Net.Info($"数据处理线程：层节流等待，layer={j}，sub={i}，发送进度={(j * tempRePrintTimes + i + 1)}，等待阈值={((j * tempRePrintTimes + i + 1) - 10)}，当前打印进度={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={TransferModifyFlag}");
                                        DataTaskFlag = 3;
                                        Thread.Sleep(500);
                                    }
                                    Log4Net.Info($"数据处理线程：层节流等待结束，layer={j}，sub={i}，发送进度={(j * tempRePrintTimes + i + 1)}，当前打印进度={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={TransferModifyFlag}");
                                    
                                    if (j == 0 || ModifyJobAeraFLag == true)//201030批注：第1层额外多发送1层数据
                                    {
                                        ModifyJobAeraFLag = false;//20201124新增：
                                        tempRePrintTimes = g_nRePrintTimes + 1;
                                        int ActualStartNum = g_nLayerStart;
                                        msg = $"准备处理第{j}层数据：准备调用RenderToWic，index为{j}，subindex为{i}，g_nRePrintTimes为{g_nRePrintTimes}，起始层为{ActualStartNum}";
                                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                                        var renderStopwatch = System.Diagnostics.Stopwatch.StartNew();
                                        g_SharpControl.RenderToWic(true, j/*1*/, i, g_nRePrintTimes, ActualStartNum);//第一层无效
                                        renderStopwatch.Stop();
                                        msg = $"完成处理第{j}层数据：准备调用RenderToWic，index为{j}，subindex为{i}，g_nRePrintTimes为{g_nRePrintTimes}，起始层为{ActualStartNum}";
                                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                                        Log4Net.Info($"数据处理线程：RenderToWic耗时，layer={j}，sub={i}，elapsedMs={renderStopwatch.ElapsedMilliseconds}");
                                    }
                                    else
                                    {
                                        tempRePrintTimes = g_nRePrintTimes;
                                        int ActualStartNum = g_nLayerStart;
                                        msg = $"准备处理第{j}层数据：准备调用RenderToWic，index为{j}，subindex为{i}，g_nRePrintTimes为{g_nRePrintTimes}，起始层为{ActualStartNum}";
                                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                                        var renderStopwatch = System.Diagnostics.Stopwatch.StartNew();
                                        g_SharpControl.RenderToWic(true, j/*1*/, i + 1, g_nRePrintTimes, ActualStartNum);//需要校对渲染区间是否正确//201030修改：
                                        renderStopwatch.Stop();
                                        msg = $"完成处理第{j}层数据：准备调用RenderToWic，index为{j}，subindex为{i}，g_nRePrintTimes为{g_nRePrintTimes}，起始层为{ActualStartNum}";
                                        Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                                        Log4Net.Info($"数据处理线程：RenderToWic耗时，layer={j}，sub={i}，elapsedMs={renderStopwatch.ElapsedMilliseconds}");

                                    }//201030批注：其余层不做额外补偿
                                    Log4Net.Info($"数据处理线程：准备调用 UpdateDataAndTransfer，layer={j}，sub={i}");
                                    var transferStopwatch = System.Diagnostics.Stopwatch.StartNew();
                                    UpdateDataAndTransfer(j, i, 1);//20200610:在标题栏刷新当前数据处理层//Rendering2D(j); //20200601：实现成形层的逐层预览刷新
                                    transferStopwatch.Stop();
                                    Log4Net.Info($"数据处理线程：UpdateDataAndTransfer 完成，layer={j}，sub={i}");
                                    Log4Net.Info($"数据处理线程：UpdateDataAndTransfer耗时，layer={j}，sub={i}，elapsedMs={transferStopwatch.ElapsedMilliseconds}");
                                    layerSubCount++;
                                }
                                layerStopwatch.Stop();
                                Log4Net.Info($"数据处理线程：层处理完成，layer={j}，subCount={layerSubCount}，totalElapsedMs={layerStopwatch.ElapsedMilliseconds}，g_PrintSchedule={g_PrintSchedule}，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}");
                            }
                            break;
#if false//20230410修正：数据处理线程不适用于暂停及恢复操作；暂停及恢复操作对数据处理无影响
                        case "PauseFlag":
                            {//状态转移：挂起本线程
                                DataTaskFlag = 3;//暂停态标志
                                j--;//保存当前打印层索引：j--,然后，j++，最后索引保持不变

                                msg = "接收到暂停打印指令：DataTaskTHREAD";
                                Log4Net.Info(msg);

                                Thread.Sleep(100);//挂起本线程
                            }
                            break;
                        case "CancelFlag":
                            {//状态转移：
                                DataTaskFlag = 3;//暂停态标志
                                j--;
                                TransferModifyFlag = "StartFlag";//返回到状态："StartFlag"

                                msg = "接收到恢复打印指令：DataTaskTHREAD";
                                Log4Net.Info(msg);
                            }
                            break;
#endif
                        default:
                            break;
                    }
                    g_PrintSchedule = j + 1;//20201118新增：从数据处理形成中更新全局打印进度
                    Log4Net.Info($"数据处理线程：已更新g_PrintSchedule={g_PrintSchedule}，当前层={j}，当前打印层号={g_nCurrentPrintLayerID}，TransferModifyFlag={TransferModifyFlag}");
                }
                DataTaskFlag = 4;//结束态标志
                g_TaskThreadSTATE[3] = 3;//20201119新增：终止状态
                UpdateDataAndTransfer(0, 0, 8);//return;//退出程序
            }
            else//20201120修改:开机失败，进入结束态
            {
                DataTaskFlag = 4;//结束态标志
                g_TaskThreadSTATE[3] = 3;//20201119新增：终止状态
                UpdateDataAndTransfer(0, 0, 8);//return;//退出程序
                return;//直接退出程序
            }
        }
        private int g_CorrectionFigureType = 0;//Type 1:垂直校准图打印模式；Type 2:往返差图打印模式；Type 3:喷头套色打印模式；Type 4:喷头状态图打印模式；
        private void DataTaskTHREAD2()//20200609新建：不适用异步，及多进程加速使用多线程加速
        {
            g_TaskThreadSTATE[3] = 2;//20201119新增：处于运行状态
            g_PrintSchedule = -1;//20201118新增：每次开启数据处理线程时，均复位
            Log4Net.Info($"数据处理线程2：启动，g_nCurrentPrintLayerID={g_nCurrentPrintLayerID}，g_PrintSchedule={g_PrintSchedule}，TransferModifyFlag={TransferModifyFlag}，g_nLayerStart={g_nLayerStart}，g_nRePrintTimes={g_nRePrintTimes}，g_CorrectionFigureType={g_CorrectionFigureType}");

            //（1）设置JOB-预备传送-核心代码：20200613批注：
            // 20200609批注：传输控制线程：关键在于，等待，当检测到内存不足的情况下，随即进入等待状态。计算能力远高于传输及下游处理能力时。
            //(0-1) Start the Printing Configuration: SET the print file infomation and confirm the print file formatt
            bool returnFlag = StartCloseJOB(true);//开启只执行1次，JOB并赋值关键打印参数；架构上本人后续再优化一下

            if (returnFlag == true/*false*/)//20200801批注：修改为true//20210312修改：false状态为本地调试模式
            {
#if false
                royal.royal.g_prtimg_layer.nImgStartJetIndex = (int)(g_RYSYSParam.m_dYJetOff / (25.4 / g_SharpControl.RenderDpiY) + 1);//20210311新增：Y向起打位置修订//20210330修改：
#endif
                DataTaskFlag = 2;//工作态标志//工作态不可强制暂停       
                switch (TransferModifyFlag)//无论如何，应该等待1层执行完成，再做定夺。这比较合理
                {
                    case "StartFlag"://以下是数据处理的核心
                        {
                            DataTaskFlag = 2;//工作态标志//工作态不可强制暂停

                            System.Diagnostics.Debug.WriteLine("Debug:" + "LaserADD" + "数据处理传送");//20200801批注：添加DebugView日志记录
                            System.Diagnostics.Trace.WriteLine("Trace:" + "LaserADD" + "数据处理传送");//20200801批注：添加DebugView日志记录
                            g_SharpControl.XDpi = g_RYSYSParam.m_XPrintDpi[g_RYSYSParam.XPrintDpiIndex]/*(float)(Convert.ToDouble(g_RYSYSParam.XPrintDpi))*//*g_RYSYSParam.XPrintDpi*/;//20230510修改

                            //string[] g_calirationFigurePaths = new string[6] { @"\垂直校准图.bmp", @"\往返差校准图-0.bmp", @"\往返差校准图-0.bmp", @"\喷头套色校准图-0.bmp", @"\喷头套色校准图-0.bmp", @"\STATUS.bmp" };//20210324新增：
                            if (g_CorrectionFigureType == 1)//Type 1:垂直校准图打印模式；
                            {
                                if (true)//201030批注：根据之前的测试结果，首层需要额外补偿1次
                                {
                                    g_SharpControl.RenderToWic2(true, 0/*1*/, 0, 1, g_calirationFigurePaths[0]);//第一层无效：发送第1次
                                    g_PrintSchedule = 0;//20201118新增：从数据处理形成中更新全局打印进度
                                    Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=0，子层=0");
                                }
                                g_SharpControl.RenderToWic2(true, 1/*1*/, 0, 1, g_calirationFigurePaths[0]);//需要校对渲染区间是否正确//发送第2次
                                g_PrintSchedule = 1;//20201118新增：从数据处理形成中更新全局打印进度
                                Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=1，子层=0");


                                UpdateDataAndTransfer(1, 0, 1);//20200610:在标题栏刷新当前数据处理层 //20200601：实现成形层的逐层预览刷新
                            }
                            else if (g_CorrectionFigureType == 2)//Type 2:往返差图打印模式；
                            {
                                if (true)//201030批注：根据之前的测试结果，首层需要额外补偿1次
                                {
                                    g_SharpControl.RenderToWic2(true, 0/*1*/, 0, 1, g_calirationFigurePaths[1]);//第一层无效：发送第1次
                                    g_PrintSchedule = 0;//20201118新增：从数据处理形成中更新全局打印进度
                                    Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=0，子层=0");
                                }
                                g_SharpControl.RenderToWic2(true, 1/*1*/, 0, 1, g_calirationFigurePaths[1]);//需要校对渲染区间是否正确//发送第2次
                                g_PrintSchedule = 1;//20201118新增：从数据处理形成中更新全局打印进度
                                Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=1，子层=0");
                                g_SharpControl.RenderToWic2(true, 2/*1*/, 0, 1, g_calirationFigurePaths[2]);//需要校对渲染区间是否正确//发送第3次
                                g_PrintSchedule = 2;//20201118新增：从数据处理形成中更新全局打印进度
                                Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=2，子层=0");

                                UpdateDataAndTransfer(1, 0, 1);//20200610:在标题栏刷新当前数据处理层 //20200601：实现成形层的逐层预览刷新
                            }
                            else if (g_CorrectionFigureType == 5)//Type 2:一次性的打印往返差图打印模式；
                            {
                                if (true)//201030批注：根据之前的测试结果，首层需要额外补偿1次
                                {
                                    g_SharpControl.RenderToWic2(true, 0/*1*/, 0, 1, g_calirationFigurePaths[6]);//第一层无效：发送第1次
                                    g_PrintSchedule = 0;//20201118新增：从数据处理形成中更新全局打印进度
                                    Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=0，子层=0");
                                }
                                g_SharpControl.RenderToWic2(true, 1/*1*/, 0, 1, g_calirationFigurePaths[6]);//需要校对渲染区间是否正确//发送第2次
                                g_PrintSchedule = 1;//20201118新增：从数据处理形成中更新全局打印进度
                                Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=1，子层=0");
                                //g_SharpControl.RenderToWic2(true, 2/*1*/, 0, 1, g_calirationFigurePaths[2]);//需要校对渲染区间是否正确//发送第3次
                                //g_PrintSchedule = 2;//20201118新增：从数据处理形成中更新全局打印进度

                                UpdateDataAndTransfer(1, 0, 1);//20200610:在标题栏刷新当前数据处理层 //20200601：实现成形层的逐层预览刷新
                            }
                            else if (g_CorrectionFigureType == 3)//Type 3:喷头套色打印模式；
                            {
                                if (true)//201030批注：根据之前的测试结果，首层需要额外补偿1次
                                {
                                    g_SharpControl.RenderToWic2(true, 0/*1*/, 0, 1, g_calirationFigurePaths[3]);//第一层无效：发送第1次
                                    g_PrintSchedule = 0;//20201118新增：从数据处理形成中更新全局打印进度
                                    Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=0，子层=0");
                                }
                                g_SharpControl.RenderToWic2(true, 1/*1*/, 0, 1, g_calirationFigurePaths[3]);//需要校对渲染区间是否正确//发送第2次
                                g_PrintSchedule = 1;//20201118新增：从数据处理形成中更新全局打印进度
                                Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=1，子层=0");
                                g_SharpControl.RenderToWic2(true, 2/*1*/, 0, 1, g_calirationFigurePaths[4]);//需要校对渲染区间是否正确//发送第3次
                                g_PrintSchedule = 2;//20201118新增：从数据处理形成中更新全局打印进度
                                Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=2，子层=0");

                                UpdateDataAndTransfer(1, 0, 1);//20200610:在标题栏刷新当前数据处理层 //20200601：实现成形层的逐层预览刷新
                            }
                            else if (g_CorrectionFigureType == 4)//Type 4:喷头状态图打印模式；
                            {
                                if (true)//201030批注：根据之前的测试结果，首层需要额外补偿1次
                                {
                                    g_SharpControl.RenderToWic2(true, 0/*1*/, 0, 1, g_calirationFigurePaths[5]);//第一层无效：发送第1次
                                    g_PrintSchedule = 0;//20201118新增：从数据处理形成中更新全局打印进度
                                    Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=0，子层=0");
                                }
                                g_SharpControl.RenderToWic2(true, 1/*1*/, 0, 1, g_calirationFigurePaths[5]);//需要校对渲染区间是否正确//发送第2次
                                g_PrintSchedule = 1;//20201118新增：从数据处理形成中更新全局打印进度
                                Log4Net.Info($"数据处理线程2：已更新g_PrintSchedule={g_PrintSchedule}，当前打印层号={g_nCurrentPrintLayerID}，图类型={g_CorrectionFigureType}，层=1，子层=0");

                                UpdateDataAndTransfer(1, 0, 1);//20200610:在标题栏刷新当前数据处理层 //20200601：实现成形层的逐层预览刷新
                            }
                        }
                        break;
#if false//20230410修正：数据处理线程不适用于暂停及恢复操作；暂停及恢复操作对数据处理无影响
                    case "PauseFlag"://状态转移：挂起本线程
                        {
                            DataTaskFlag = 3;//暂停态标志
                            Thread.Sleep(100);//挂起本线程
                        }
                        break;
                    case "CancelFlag"://状态转移：
                        {
                            DataTaskFlag = 3;//暂停态标志
                            TransferModifyFlag = "StartFlag";//返回到状态："StartFlag"
                        }
                        break;
#endif
                    default:
                        break;
                }

                DataTaskFlag = 4;//结束态标志
                g_TaskThreadSTATE[3] = 3;//20201119新增：终止状态
                UpdateDataAndTransfer(0, 0, 8);//return;//退出程序
            }
            else//20201120修改:开机失败，进入结束态
            {
                DataTaskFlag = 4;//结束态标志
                g_TaskThreadSTATE[3] = 3;//20201119新增：终止状态
                UpdateDataAndTransfer(0, 0, 8);//return;//退出程序
                return;//直接退出程序
            }
        }

        int DataTaskFlag = 1;//20201119新增：1为初始态，2为工作态，3位暂停态（安全态），4为结束态（安全态）
        int PrintTaskFlag = 1;//20201119新增：1为初始态，2为工作态，3位暂停态（安全态），4为结束态（安全态）

        /***********************************************结束：数据处理及传送部分***********************************************/
        /***********************************************结束：数据处理及传送部分***********************************************/
        /***********************************************结束：数据处理及传送部分***********************************************/
        private int ControlFlag = 0;//非常关键//20200507:
        private int returnFlag = 0;//非常关键//20200507:
        public void SendControlCommand(int controlFlag, int returnFlag)//20200507:
        {
            service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");
            service.SendControlCommand(controlFlag, returnFlag);
        }
        public void ReadControlCommand(ref int controlFlag, ref int returnFlag)//20200507:
        {
            service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");
            service.ReadControlCommand(ref controlFlag, ref returnFlag);
        }

        //(2) 使用多线程定时器：20200507新建
        public void TimerProc(object state)
        {
            // The state object is the Timer object.
            //System.Threading.Timer t = (System.Threading.Timer)state;
            //t.Dispose();
            Console.WriteLine("The timer callback executes.");
        }
        //public RemoteObject service;//20200504新增：
        private double GetBackValue()//从服务器回传给客户端
        {
            /*RemoteObject */
            ////service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
            ////    "ipc://localhost:9090/RemoteObject.rem");
            return service.GetBackValue();
        }

        //20200507新建定时器：
        private bool StopRipFlag = false;
        private double returnValue = 0;
        private System.Windows.Forms.Timer timer3 = new System.Windows.Forms.Timer();
        private void timer3_Monitor(object sender, EventArgs e)//20200220:监控线程定时刷新定时器2//定时器本身就是1种线程处理方式
        {
            if ((returnValue <= g_nLayerEnd + 1 /*20*/) && (StopRipFlag == false))
            {
                /*double */
                returnValue = GetBackValue();
                SetCircularProgressValueSafe(circularProgressBar1, (int)returnValue, "timer3_Monitor");
                circularProgressBar1.Text = (/*circularProgressBar.Value*/(returnValue + 1)
                    / (g_nLayerEnd + 1) /*20*//*circularProgressBar.Maximum*/).ToString("P1"/*"P0"*/)/*+"%"*/;//精华：20200504新增批注
                if (returnValue == g_nLayerEnd /*20*/)
                {
                    StopRipFlag = true;
                }
            }
            else
            {
                SendControlCommand(4, 0);//写入到远程：关闭远程RIP工作
                RipExistFlag = false;//20200508新建;
                timer3.Stop();
            }
        }
        //20200508新建定时器：
        private bool StopPrintFlag = false;
        private double returnPrintValue = 0;//非常关键
        private System.Windows.Forms.Timer timer4 = new System.Windows.Forms.Timer();
        private void timer4_Monitor(object sender, EventArgs e)//20200220:监控线程定时刷新定时器2//定时器本身就是1种线程处理方式
        {
            if ((returnPrintValue <= (g_nLayerEnd + 1) * g_nRePrintTimes /*20*/) && (StopPrintFlag == false))//201121修改：
            {
                //returnPrintValue = GetBackValue();
                SetCircularProgressValueSafe(circularProgressBar2, (int)returnPrintValue, "timer4_Monitor");
                circularProgressBar2.Text = ((returnPrintValue/*+1*/) / (double)((g_nLayerEnd + 1) * g_nRePrintTimes)).ToString("P1")/*+"%"*/;//精华：20200504新增批注//201121修改：
                if (returnPrintValue == (g_nLayerEnd + 1) * g_nRePrintTimes/*g_nLayerEnd*/)//20201121修改：
                {
                    StopPrintFlag = true;
                }
            }
            else
            {
                //SendControlCommand(4, 0);//写入到远程：开启远程RIP工作
                timer4.Stop();
            }
        }

        public RemoteObject service;//20200504新增：
        private void SetCircularProgressValueSafe(CircularProgressBar.CircularProgressBar bar, int requestedValue, string sourceTag)
        {
            try
            {
                if (bar == null || bar.IsDisposed)
                {
                    return;
                }

                int safeValue = requestedValue;
                if (safeValue < bar.Minimum)
                {
                    safeValue = bar.Minimum;
                }
                if (safeValue > bar.Maximum)
                {
                    safeValue = bar.Maximum;
                }

                if (safeValue != requestedValue)
                {
                    Log4Net.Info($"{sourceTag}: circular progress clamp, requested={requestedValue}, safe={safeValue}, min={bar.Minimum}, max={bar.Maximum}");
                }

                bar.Value = safeValue;
            }
            catch (Exception ex)
            {
                Log4Net.Error($"{sourceTag}: circular progress set failed, requested={requestedValue}\r\n{ex}");
            }
        }
        private void StartProgressMonitor(int CircularProgressIndex, int Minimum, int Maximum)//20200601新增：区间Minimum和Maximum
        {
            switch (CircularProgressIndex)
            {
                case 1://初始化circularProgressBar1：
#if false//20200611批注：在使用多进程优化时，才使用本部分
                      //(一)初始化远程对象
                    service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                        "ipc://localhost:9090/RemoteObject.rem");                  
                    
                    //(二)初始化并开启定时器
                    //(1) 使用Winform UI单线程定时器
                    this.timer3.Tick += new System.EventHandler(this.timer3_Monitor);
                    this.timer3.Interval = 25;
                    timer3.Enabled = true;
#else
                    ////(2) 使用多线程定时器：20200507新建
                    //System.Threading.Timer timer3 = new System.Threading.Timer(
                    //    new TimerCallback(TimerProc), null, 10, 600);
#endif
                    //(三)初始化控件
                    //（1）CircularProgressBar速度：
                    circularProgressBar1.Style = ProgressBarStyle.Marquee;
                    circularProgressBar1.AnimationFunction = KnownAnimationFunctions.QuinticEaseIn;
                    circularProgressBar1.AnimationSpeed = 500;
                    circularProgressBar1.MarqueeAnimationSpeed = 5500;
                    //（2）CircularProgressBar大小：
                    circularProgressBar1.Height = 94;
                    circularProgressBar1.Width = 94;
                    //（3）CircularProgressBar风格：黑底绿字比较好看：20200505新增批注
                    circularProgressBar1.BackColor = /*Color.Gray*/Color.Transparent;
                    circularProgressBar1.InnerColor = Color.White/* Color.White*/;
                    circularProgressBar1.Minimum = 0/*0*//*Minimum*/;
                    circularProgressBar1.Maximum = /*20*/Maximum;
                    circularProgressBar1.OuterColor = Color.Navy/*FromArgb(0, 0, 0)*//*黑底*//* Color.FromArgb(194, 251, 50)*//*Color.White*//*Color.FromArgb(21, 73, 94)*/;
                    circularProgressBar1.OuterMargin = -18;
                    circularProgressBar1.OuterWidth = 21;
                    circularProgressBar1.ProgressWidth = 20;
                    circularProgressBar1.StartAngle = 270;
                    circularProgressBar1.ProgressColor = Color.FromArgb(0, 248, 0)/*绿字*//* Color.FromArgb(137, 64, 169)*//*Color.Yellow*//*Color.FromArgb(194, 251, 50)*/;
                    break;
                case 2://初始化circularProgressBar2：
                    //(二)初始化并开启定时器
                    //(1) 使用Winform UI单线程定时器
                    this.timer4.Tick += new System.EventHandler(this.timer4_Monitor);
                    this.timer4.Interval = 20;
                    timer4.Enabled = true;

                    //(三)初始化控件
                    //（1）CircularProgressBar速度：
                    circularProgressBar2.Style = ProgressBarStyle.Marquee;
                    circularProgressBar2.AnimationFunction = KnownAnimationFunctions.QuinticEaseIn;
                    circularProgressBar2.AnimationSpeed = 500;
                    circularProgressBar2.MarqueeAnimationSpeed = 5500;
                    //（2）CircularProgressBar大小：
                    circularProgressBar2.Height = 94;
                    circularProgressBar2.Width = 94;
                    //（3）CircularProgressBar风格：黑底绿字比较好看：20200505新增批注
                    circularProgressBar2.BackColor = /*Color.Gray*/Color.Transparent;
                    circularProgressBar2.InnerColor = Color.White/* Color.White*/;
                    circularProgressBar2.Minimum = /*0*/Minimum;
                    circularProgressBar2.Maximum = /*20*/Maximum;
                    circularProgressBar2.OuterColor = Color.Navy/*FromArgb(0, 0, 0)*/ /*Color.Gray*/;/*FromArgb(21, 73, 94/*0, 0, 0*///*黑底*//* Color.FromArgb(194, 251, 50)*//*Color.White*//*Color.FromArgb(21, 73, 94)*/;
                    circularProgressBar2.OuterMargin = -18;
                    circularProgressBar2.OuterWidth = 21;
                    circularProgressBar2.ProgressWidth = 20;
                    circularProgressBar2.StartAngle = 270;
                    circularProgressBar2.ProgressColor = Color.FromArgb(255, 128, 0/*0, 248, 0*/)/*绿字*//* Color.FromArgb(137, 64, 169)*//*Color.Yellow*//*Color.FromArgb(194, 251, 50)*/;
                    break;
                case 3:

                    break;
                case 4:
                    break;
            }
        }

        /// 20200507批注：定义一个代理:以访问到UI线程
        private delegate void UpdateBarValue(int iValue);
        private void UpdateBarValueMethod(int iValue)
        {
            if (this.JobsProgressBar.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
            {
                if (null != JobsProgressBar && !JobsProgressBar.IsDisposed)//jobsProgressBar被释放？且不为空
                {
                    JobsProgressBar.Value = iValue;
                    double rate = (double)iValue / (double)50 * 100;
                    string tempRate = rate.ToString("f2");
                    string showRate = "RIP 进度：  " + tempRate + "%";//保留小数点后两位进程
                    this.ProcessRate.Text = showRate;//保留小数点后两位
                    this.panel1.Refresh();
                }
            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpdateBarValue UpdateBar = new UpdateBarValue(UpdateBarValueMethod);
                this.JobsProgressBar.Invoke(UpdateBar, iValue);
            }
        }
        /// 20200507新建：定义一个代理:以访问到UI线程：更新数据进度
        private delegate void UpdateCircularBar(int CircularProgressIndex);
        private void UpdateCircularBarMethod(int CircularProgressIndex)
        {
            switch (CircularProgressIndex)
            {
                case 1://20200508新建：开启数据进度监控
                    if (this.JobsProgressBar.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
                    {
                        if (null != JobsProgressBar && !JobsProgressBar.IsDisposed)//jobsProgressBar被释放？且不为空
                        {
#if false
                    //JobsProgressBar.Value = iValue;
                    //double rate = (double)iValue / (double)50 * 100;
                    //string tempRate = rate.ToString("f2");
                    //string showRate = "RIP 进度：  " + tempRate + "%";//保留小数点后两位
                    //this.ProcessRate.Text = showRate;//保留小数点后两位
                    //this.panel1.Refresh();
#else
                            StartProgressMonitor(1, 0/*g_nLayerStart * g_nRePrintTimes*/, (g_nLayerEnd + 1) * g_nRePrintTimes);//20200508新建：数据进度
                            this.panel1.Refresh();
#endif
                        }
                    }
                    else//如果调用该函数的线程和控件lstMain不在同一个线程
                    {
                        UpdateCircularBar UpdateCircularBar = new UpdateCircularBar(UpdateCircularBarMethod);
                        //UpdateCircularBar(1);//20200508新建：传递参数，存在BUG,本方法不合适
                        this.JobsProgressBar.Invoke(UpdateCircularBar, 1);
                    }

                    break;
                case 2://20200508新建：开启打印进度监控
                    if (this.JobsProgressBar.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
                    {
                        if (null != JobsProgressBar && !JobsProgressBar.IsDisposed)//jobsProgressBar被释放？且不为空
                        {
                            StartProgressMonitor(2, 0/*g_nLayerStart * g_nRePrintTimes*/, (g_nLayerEnd + 1) * g_nRePrintTimes/*0,20*/);//20200508新建：数据进度
                            this.panel1.Refresh();
                        }
                    }
                    else//如果调用该函数的线程和控件lstMain不在同一个线程
                    {
                        UpdateCircularBar UpdateCircularBar = new UpdateCircularBar(UpdateCircularBarMethod);
                        //UpdateCircularBar(2);//20200508新建：传递参数，存在BUG,本方法不合适
                        this.JobsProgressBar.Invoke(UpdateCircularBar, 2);//20200508新建：传递参数
                    }

                    break;
                case 3:

                    break;
                case 4:
                    break;
            }
        }


        /// 定义一个代理
        private delegate void UpdateBarValue2(int iValue);

        //开启和关闭闪喷功能
        bool m_bFlashFlag = true;//动作为开启
        bool m_bCleanFlag = true;//20200605修改：
        bool m_bShoveFlag = true;//20200605修改：//20220525修改：默认状态：未压墨
        private void FlashBtn_Click(object sender, EventArgs e)//闪喷控制
        {
            string msg = null;
            if (m_bFlashFlag == false)//(b)根据ID反转背景图片
            {
                // (sender as Control).BackColor = Color.DarkOrchid;
                (sender as Control).Text = "闪喷";
            }
            else
            {
                // (sender as Control).BackColor = Color.MintCream;
                (sender as Control).Text = "关闭\r\n闪喷";
            }
            //(c)计算输出
            if (m_bFlashFlag == true)//打开和关闭闪喷：
            {
                bool nRetVal = MeteorPrintEngine.SetFlash(true);//打开闪喷

                msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal}}}";
                Log4Net.Info(msg);

                m_bFlashFlag = false;
            }
            else
            {
                bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷

                msg = $"关闭闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal}}}";
                Log4Net.Info(msg);

                m_bFlashFlag = true;
            }
        }
        /// <summary>
        ///（1）开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。如此分析，一切就都顺利成章。
        /// </summary>
        /// <param name="index"></param>
        private void OpenCloseVALVE(int index, bool action)//index是对应的阀门编号：20200605新增批注
        {
            //(a)设置对应编号阀门的标志位
            RoyalMap.m_bEnable[index] = action;//20200605新增批注：
            //(b)设置对应的编码
            int nInkMask = 0; //(c)计算掩码
            for (int i = 0; i < 10; i++)
            {
                if (RoyalMap.m_bEnable[i])//变量在这里。
                    nInkMask |= (1 << i);
            }
            //(c)设置生效、特定阀门执行动作
            royal.royal.DEV_SetMcbOutPut(0, (UInt32)nInkMask);//暂时用不了
        }
        /// <summary>
        /// 清洗操作逻辑，关键是分为2步：第一步永远是先开关阀门，第2步是打开对应的泵源。类似于供煤气、供水。如此分析，一切就都顺利成章。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private PluginHost pluginHost = new PluginHost();//20231225新增:
        private void CleanBtn_Click(object sender, EventArgs e)//20200604新增：
        {
            //步骤 1：扫描并加载插件，然后将它们添加到列表控件中。
            pluginHost.LoadPlugins(@"externalplugins");//将插件类编译为 DLL，并将其放置在您选择的插件目录中。
            //步骤 2: 填充插件列表并允许用户选择
            foreach (var plugin in pluginHost.AvailablePlugins)
            {
                //listBox1.Items.Add(plugin.Name); 
            }
            //步骤 3：测试插件
            pluginHost.ExecutePlugins();



#if true//实质是清洗：20200604新增

            //if (m_bCleanFlag == false)//(b)根据ID反转背景图片
            //{
            //    //(sender as Control).BackColor = Color.DarkOrchid;
            //    (sender as Control).Text = "清洗";
            //}
            //else
            //{
            //    //(sender as Control).BackColor = Color.MintCream;
            //    (sender as Control).Text = "关闭\r\n清洗";
            //}
            ////(c)计算输出
            //if (m_bCleanFlag == true)//打开和关闭闪喷：
            //{
            //    //（1）开清洗阀门（==等效：关墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
            //    OpenCloseVALVE(2 - 1, false);//20200605批注：Tag-1

            //    //（2）开煤气、供水泵源：
            //    uint nIoVal = 0x1FF;//控制:P1-P2-P3~P7,依次是清洗泵、压墨泵、供墨泵1-7
            //    bool nRetVal = royal.royal.DEV_SetInkPump(nIoVal);//打开压墨泵
            //    m_bCleanFlag = false;
            //}
            //else
            //{
            //    //（1）关清洗阀门（==等效：开墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
            //    OpenCloseVALVE(2 - 1, true);//20200605批注：Tag-1

            //    //（2）开煤气、供水泵源：
            //    uint nIoVal = 0x0;//控制:P1-P2-P3~P7,依次是清洗泵、压墨泵、供墨泵1-7
            //    bool nRetVal = royal.royal.DEV_SetInkPump(nIoVal);//关闭压墨泵
            //    m_bCleanFlag = true;
            //}
#else
            //(a)根据ID反转并存储到对应控件列表
            InkPumpFlag = !InkPumpFlag;
            //(b2)根据ID反转颜色状态
            if ((sender as Control).BackColor == Color.LimeGreen)
            { (sender as Control).BackColor = Color.Tomato; }
            else
            { (sender as Control).BackColor = Color.LimeGreen; }

            //(c)计算掩码
            int nInkMask = 0;

            if (InkPumpFlag == true)//变量在这里。
            {
                nInkMask |= (1 << 0);
            }
            else
            {
                //nInkMask =0;
            }
            bool nRetVal = royal.royal.DEV_SetInkPump((UInt32)nInkMask);////设置压墨输出 bit[0]~bit[1]  P1~P2	J28——清洗泵和压墨泵
#endif
        }

        private void ShoveBtn_Click(object sender, EventArgs e)//挤墨控制
        {
            if (m_bShoveFlag == false)//(b)根据ID反转背景图片
            {
                (sender as Control).Text = "压墨";
            }
            else
            {
                (sender as Control).Text = "关闭\r\n压墨";
            }
            //(c)计算输出
            if (m_bShoveFlag == false)//关闭闪喷：
            {
                //（1）开压墨泵：
                motionMap.SetDo(13, false);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                m_bShoveFlag = true;
            }
            else
            {
                //（1）关压墨泵
                motionMap.SetDo(13, true);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                m_bShoveFlag = false;
            }
        }
        //铺粉系统回零：20200326添加
        private void PowerBackBtn_Click(object sender, EventArgs e)
        {
            InkCarBackBtn.Text = "铺粉\r\n系统回零中";
            InkCarBackBtn.BackColor = Color.Lime;
            手动操作 f = new 手动操作(1, nValveStateMask, false);//20200202修改
            //f.k_RYSYSParam.m_bFlagResetCorrect = this.g_bResetCorrectEnabled;
            //f.k_RYSYSParam = g_RYSYSParam;//20200326新增
            f.Width = 1000; f.Height = 650;
            //f.ControlBox = false;
            f.FormBorderStyle = FormBorderStyle.FixedSingle;
            f.Text = "铺粉系统回零";//20200327新增：
            //f.BackColor = Color.Aqua;
            ////f.Owner = this;
            ////f.Location = new Point(f.Owner.Location.X+ 100, f.Owner.Location.Y + 100);
            ////f.Location = /*new Point(this.panel3.Location);*//*this.panel3.Location+Owner.Location;*/
            ////f.Location = new Point(this.panel3.Location.X+ this.Owner.Location.X
            ////    , this.panel3.Location.Y + this.Owner.Location.Y);
            //f.Location = this.panel3.PointToScreen(this.panel3.Location);

            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
                //this.g_bResetCorrectEnabled = f.k_RYSYSParam.FlagResetCorrect;// this.m_cGoogolMotionMap = f.m_mGoogolMotionMap;//回传数据                                
                //g_RYSYSParam = f.k_RYSYSParam;//20200326新增：关键：将RYSYSParam的值赋值给全局的static的RYSYSParam

                ////20200326新增:关键：将RYSYSParam的JOB参数信息，进行及时的转发，转给royal.sysParam
                ////royal.g_sys_param为static类型：
                //royal.royal.g_sys_param.fBrustCycleSec = (float)g_RYSYSParam.m_dInterSpeedSparkCycleTime;
                //royal.royal.g_sys_param.fBrustValidSec = (float)g_RYSYSParam.m_dHSpeedSparkTime;
                //royal.royal.g_sys_param.fBrustFrequecy = g_RYSYSParam.m_nHSpeedSparkFreq;
                //royal.royal.g_sys_param.szLogPath = g_RYSYSParam.m_sLogPath;
                //royal.royal.g_sys_param.szWavePath = g_RYSYSParam.m_sWavePath;

            }
            else if (result == DialogResult.Cancel)//退出时，什么都不做
            {
            }
            InkCarBackBtn.Text = "铺粉系统\r\n重新回零";
            InkCarBackBtn.BackColor = Color.Yellow;
        }
        //墨车系统回零：20200326添加
        /*******************************墨车系统回零：20200326添加***********************************/
        bool EncoderResetFlag = false;//墨车回零校准：20200326
        private void InkCarBackBtn_Click(object sender, EventArgs e)
        {
            InitCarMotor();//20200327新增
            if (EncoderResetFlag == false)//没有在加工
            {
                //开启打印线程:
                string tempThreadName = "EncoderResetThread";
                Thread tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    EncoderResetThreads.Remove(tempThread);//以防万一
                }
                else
                {
                    ThreadStart initThreadEntry = new ThreadStart(EncoderResetThread);//20200220:线程入口方法修改为联动线程
                    tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    tempThread.Name = tempThreadName;
                    tempThread.Start();
                    EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }
                //修改按钮状态为：正在校准
                this.InkCarBackBtn.Text = "墨车\r\n系统回零中"/*"光栅校准中"*/;
                this.InkCarBackBtn.TextAlign = ContentAlignment.MiddleCenter;
                this.InkCarBackBtn.BackColor = Color.Lime;
                //this.EncoderResetBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/StopJob.png");
                EncoderResetFlag = true;//玩的都是标志位：20200126
            }
            else//正在加工
            {
                //(1)关闭联调线程（2）关闭多轴运动：保障运动安全
                string tempThreadName = "EncoderResetThread";//(1)关闭联调线程
                DeleteThread2(tempThreadName);
                //(a)检测到正限位和负限位后紧急停止运动：
                bool nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108

                //修改按钮状态为：启动打印
                this.InkCarBackBtn.Text = "墨车系统\r\n重新回零"/*"光栅校准"*/;
                this.InkCarBackBtn.TextAlign = ContentAlignment.MiddleCenter;
                this.InkCarBackBtn.BackColor = Color.Yellow;
                //this.EncoderResetBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/RunJob.png");
                EncoderResetFlag = false;//玩的都是标志位：20200126
            }
        }
        private void InitCarMotor()//20200327新增
        {
            if (DisableRoyalPrintRuntimeInit)
            {
                Log4Net.Info("Meteor print mode: skip InitCarMotor royal axis initialization.");
                return;
            }

            //初始化墨车电机参数：
            /////////////////////******初始化墨车电机******///////////
            bool nRetVal = royal.royal.DEM_InitAxis(0, 0x100);//分别初始化各轴的运动参数：20200305
            string msg = $"初始化墨轴1：DEM_InitAxis：{{0, 0x100}}";
            Log4Net.Info(msg);

            nRetVal = royal.royal.DEM_InitAxis(1, 0x1400);//分别初始化各轴的运动参数：20200305//20220513新建：轴的加速度修改为0x1400
            msg = $"初始化墨轴2：DEM_InitAxis：{{1, 0x1400}}";
            Log4Net.Info(msg);

            nRetVal = royal.royal.DEM_InitAxis(2, 0x1400);//分别初始化各轴的运动参数：20200305//20220513新建：轴的加速度修改为0x1400
            msg = $"初始化墨轴3：DEM_InitAxis：{{1, 0x1400}}";
            Log4Net.Info(msg);

            //IOS_SetConfig(0x3000,0x0);			//Reg0x14	复用掩码设置 保留输出1、2
            nRetVal = royal.royal.DEM_EnableAxisRun(true);//所有的轴共用1个使能，使能一次就OK!:20200305
            msg = $"使能所有墨轴：DEM_EnableAxisRun：{{true}}";
            Log4Net.Info(msg);
#if false//20220506新增：
            //nRetVal = royal.royal.DEM_EnableDYOutPut(true, true);//完全不必要使能双Y输出：20200305
#else
            nRetVal = royal.royal.DEM_EnableDYOutPut(true, true);//完全不必要使能双Y输出：20200305
            msg = $"使能双Y墨轴输出同步：DEM_EnableDYOutPut：{{true, true}}";
            Log4Net.Info(msg);
#endif
        }

        //20200220：线程管理的案发现场，只要是相应的线程我就存储在这里，不管线程是死是活，祖祖辈辈就在这里，便于维护及管理
        private List<Thread> EncoderResetThreads = new List<Thread>();//20200220:存放所有必要的打印机的工作线程
        public void DeleteThread2(string ThreadName)
        {
            string tempThreadName = ThreadName;
            Thread tempThread = EncoderResetThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
            if (tempThread != null)
            {
                tempThread.Abort();//20200221修改:当调用非托管线程时，有时会抛出异常但不一定及时停止
                while (tempThread.ThreadState != System.Threading.ThreadState.Aborted)
                { Thread.Sleep(100); }
                EncoderResetThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
            }
        }
        private void EncoderResetThread()//墨车回零校准线程内容：20200326
        {
            float m_szMovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增
            uint m_unCarMoveSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch);//20200328新增

            //(1)运动到负限位
            uint nIOState1;//运动之后的限位状态值 
            //速度的设置非常关键
            //20200422新增：消除运动过程中的抖动现象
            bool nRetVal = royal.royal.DEM_Run(0, false, m_unCarMoveSpeed /*(UInt32)g_RYSYSParam.CarMoveSpeed*//*1000*/, 650000, 2);//100，000/200=50mm的运动距离//最好的状态是，1次运动50mm，到达负限位
            uint EncoderPos1 = royal.royal.DEV_GetPrintEncoderValue();//20200311新增：编码器位置设置
            uint EncoderPos2 = 0;
            //位于同一线程汇中，不必要担心冲突访问的问题
            //(c)读取实时的轴限位状态
            nIOState1 = royal.royal.DEM_GetAxisLmtZeroState(0);//底层接口已经作了12 bit移位处理，对照Reg[12]定义//(c)读取实时的轴限位状态
            while ((0 == (nIOState1 & 0x2)) &&/*||*/ (0 == (nIOState1 & 0x1)))//保证运动到负限位或者正限位//20200313新增：
            {
                EncoderPos2 = royal.royal.DEV_GetPrintEncoderValue();//读取新的编码器位置值：20200311新增
                if (EncoderPos1 != EncoderPos2)//判断是否停止
                {
                    EncoderPos1 = EncoderPos2;//新的编码器值赋值给就的编码器值
                }
                else
                {
                    nRetVal = royal.royal.DEM_Run(0, false, m_unCarMoveSpeed/*(UInt32)g_RYSYSParam.CarMoveSpeed*//*1000*/, 100000, 2);//再来1次，50mm的运动
                }
                nIOState1 = royal.royal.DEM_GetAxisLmtZeroState(0);//在新线程中刷新限位状态（读取）
                Thread.Sleep(10);//10ms检查一次：20200313新增：
            }
            //(a)检测到正限位和负限位后紧急停止运动：//其实必要性不必很高，FPGA上有硬限位停止:20200313
            nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108//其实必要性不必很高，FPGA上有硬限位停止
            //(b)回到负限位后，重置光栅编码值为
            bool nRetVal2 = royal.royal.DEV_ResetPrintEncoder(2000);//相对光栅尺的话，需要设置，不能设置为0;2000为10mm位置值                                                                  

            //this.EncoderResetBtn.Text = "光栅校准好";//修改按钮状态为：正在校准//20200313存在bug
            if (InkCarBackBtn.InvokeRequired == true)
            {
                InkCarBackBtn.BeginInvoke(
                    new Action(() =>
                    {
                        this.InkCarBackBtn.Text = "重新校准光栅";//恢复控件操作
                        this.InkCarBackBtn.BackColor = Color.Tomato;
                    }
                    ));
            }
            DeleteThread("EncoderResetThread");//20200220：本线程结束，需要及时清理相关线程//20200313新增：
        }
        /// <summary>
        /// 测试1bitBMP生成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BmpTestBtn_Click(object sender, EventArgs e)
        {
#if false

            Bitmap processedBitmap = new Bitmap(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\TiffType-3.jpg");
            Bitmap clone = processedBitmap.Clone(new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height), PixelFormat.Format1bppIndexed);
            clone.Save(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\输出.bmp", ImageFormat.Bmp);
            MessageBox.Show("Success!");

#else
            string path = System.Windows.Forms.Application.StartupPath + @"\输出.bmp";
            //（1-新）保存为单色BMP文件：20200408新增
            System.Drawing.Bitmap processedBitmap = new System.Drawing.Bitmap(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\TiffType-3.jpg");

            using (MemoryStream memoryStream = new MemoryStream())//本人觉得是精华
            {
                //（1-新）保存为单色BMP文件：20200408新增                
                //(A)Output to MEMORY instead of HD
                processedBitmap.Save(memoryStream, ImageFormat.Jpeg);//save stream from origin pic as format bmp
                //g.Dispose();//————————————————————————释放画笔

                //(B)COPY & release
                System.Drawing.Bitmap MidJpgBmp = new System.Drawing.Bitmap(memoryStream);//create bmp instance from stream above
                System.Drawing.Bitmap clone = MidJpgBmp.Clone(new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height), System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                //processedBitmap.Dispose();//————————————————————————释放bmp文件
                //MidJpgBmp.Dispose();//————————————————————————释放bmp文件

                //(C)SAVE & release
                clone.Save(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\输出.bmp", ImageFormat.Bmp);//————保存到BMP文件:20200408修改
                clone.Dispose();//及时释放掉clone：20200408新增
                processedBitmap.Dispose();//————————————————————————释放bmp文件
                MidJpgBmp.Dispose();//————————————————————————释放bmp文件
            }
#endif
        }
        /// <summary>
        /// 20200419多任务测试：
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MaintainBtn_Click(object sender, EventArgs e)
        {
            //喷头搭接校准功能：
            CalibrationMoudle f = new CalibrationMoudle();//20200202修改//20210303新增：喷头打印校准功能
            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
            }
            else if (result == DialogResult.Cancel)//退出时，什么都不做
            {
            }
        }

        private void LoadDataBtn_Click(object sender, EventArgs e)
        {
#if true
            bool startCloseFlag = true;
            StartDataTransControlThread(startCloseFlag/*true*/, 0, 50);
#else
            //(sender as Control).BackgroundImage = System.Drawing.Image.FromFile("ICON资源/Shove-38x38.png");//20200430新增：
            (sender as Control).BackgroundImage = Resource.Shove_38x38;
            this.LoadDataBtn.Enabled = false;//2020430新增：恢复载入数据控件交互

            //（1）现执行代码：20200411新建
            int nRetry = 3;//DEV_GetSysEncDPI();
            int nResult = 0;//核心代码的执行结果
            bool m_bPrinting = true;//放在此处，仅仅是方便测试而已：20200411新增：
            bool m_bJobStarted = false;//放在此处，仅仅是方便测试而已：20200411新增：
            //royal.LPPRTIMG_LAYER _testLayer = new LPPRTIMG_LAYER();//放在此处，仅仅是方便测试而已：20200411新增：
            int LayerEndNum = 50;//打印的终止层数，放在此处，仅仅是方便测试而已：20200411新增：
            do///20200205:重复尝试3次写入
            {
                for (int i = 0; i < LayerEndNum/*g_nLayerWriteCount*/+ 1; i++)//g_nLayerWriteCount是压力测试，连续加载：0x100000 
                {
                    //nResult = IDP_WriteImgLayerData(pLayer, pBmpFile, pdlg->m_nPrtDataSize);    //开始传图 ，实时返回

                    string path = System.Windows.Forms.Application.StartupPath + @"\JOB输出文件\输出.bmp";//20200430新增：
                    //string path = @"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\输出.bmp";
                    nResult = WriteImgLayerData(path, i + 1, 0/*0*/, true);//参数依次是;job文件名称、层索引、打印方向、铺粉方向：20200411新增
                    if (nResult <= 0)///20200205:写入失败
                    {
                        /////(1)分析错误号：做出相应处理
                        nRetry--;
                        if (nRetry < 0)
                        {
                            MessageBox.Show("作业启动失败：PC内存不足");//重试3次后的结果
                            //底层，存在1个内存管理机制，内存不足的情况下， 会暂停write线程的工作，挂起
                            break;
                        }
                    }
                    else///20200205:写入成功
                    {
                        royal.royal.g_prtimg_layer.nLayerIndex++;//写入的层数的索引增加1
                    }
                    /*********20200423临时注销：为了便于测试，几天内总装运行的时候，需要重新打开*********/
                    //20200423临时注销：为了便于测试，几天内总装运行的时候，需要重新打开
                    //if (m_bJobStarted == false)
                    //    break;
                    //if (m_bStopMonitor == true)
                    //    break;
                    /*********20200423临时注销：为了便于测试，几天内总装运行的时候，需要重新打开*********/
                    /*********20200423临时注销：为了便于测试，几天内总装运行的时候，需要重新打开*********/
                    Thread.Sleep(10);//等待100ms
                }

            } while (nRetry < 0);///20200205:重复尝试3次写入
            this.LoadDataBtn.Enabled = true;//2020430新增：恢复载入数据控件交互

            //#if false

            //            Bitmap processedBitmap = new Bitmap(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\TiffType-3.jpg");
            //            Bitmap clone = processedBitmap.Clone(new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height), PixelFormat.Format1bppIndexed);
            //            clone.Save(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\输出.bmp", ImageFormat.Bmp);
            //            MessageBox.Show("Success!");

            //#else
            //            //（1-新）保存为单色BMP文件：20200408新增
            //            Bitmap processedBitmap = new Bitmap(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\TiffType-3.jpg");

            //            using (MemoryStream memoryStream = new MemoryStream())//本人觉得是精华
            //            {
            //                //（1-新）保存为单色BMP文件：20200408新增                
            //                //(A)Output to MEMORY instead of HD
            //                processedBitmap.Save(memoryStream, ImageFormat.Jpeg);//save stream from origin pic as format bmp
            //                //g.Dispose();//————————————————————————释放画笔

            //                //(B)COPY & release
            //                Bitmap MidJpgBmp = new Bitmap(memoryStream);//create bmp instance from stream above
            //                Bitmap clone = MidJpgBmp.Clone(new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height), PixelFormat.Format1bppIndexed);
            //                //processedBitmap.Dispose();//————————————————————————释放bmp文件
            //                //MidJpgBmp.Dispose();//————————————————————————释放bmp文件

            //                //(C)SAVE & release
            //                clone.Save(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\输出.bmp", ImageFormat.Bmp);//————保存到BMP文件:20200408修改
            //                clone.Dispose();//及时释放掉clone：20200408新增
            //                processedBitmap.Dispose();//————————————————————————释放bmp文件
            //                MidJpgBmp.Dispose();//————————————————————————释放bmp文件

            //            }
            //#endif
#endif
        }
        public class TransferParam//20200514 new create:
        {
            public int CurrentStartLayer;
            public int LayerEndNum;

            public string OperationCode;//20201119新增：
        }
        //private List<Thread> PrinterLogicThreads = new List<Thread>();//cunfangdayingluoji:20200514: has integrated into the previous code logic
        private void StartDataTransControlThread(bool StartCloseFlag, int CurrentStartLayer, int LayerEndNum)//StartCloseFlag is 
        {
            if (StartCloseFlag == true)//(1) Start the StartDataTransControlThread: 20200514 new create
            {
                TransferParam TranferControlInfo = new TransferParam();//20200110:放到这里主要方便下面的多线程直接调用
                TranferControlInfo.CurrentStartLayer = /*0*/CurrentStartLayer;
                TranferControlInfo.LayerEndNum = /*50*/LayerEndNum;//20200111修正:回零点的速度，调整为原来的10分之1

                string tempThreadName = "DataTransControlThread";
                Thread tempThread = PrinterLogicThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)//(1)预防潜在问题
                {
                    PrinterLogicThreads.Remove(tempThread);
                }
                else//(1)带参数的多线程方式实现: 20200514 new create
                {
                    tempThread = new Thread(new ParameterizedThreadStart(DataTransControlThread)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                    tempThread.Name = "DataTransControlThread";//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                    tempThread.Start(TranferControlInfo);//(3)第3部曲：多线程3步曲 
                    PrinterLogicThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }
            }
            else//(2) Close the StartDataTransControlThread: 20200514 new create
            {
                string tempThreadName = "DataTransControlThread";
                Thread tempThread = PrinterLogicThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
                if (tempThread != null)
                {
                    tempThread.Abort();//20200221修改:当调用非托管线程时，有时会抛出异常但不一定及时停止
                    while (tempThread.ThreadState != System.Threading.ThreadState.Aborted)
                    { Thread.Sleep(100); }
                    PrinterLogicThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
                }
            }
        }
        /// <summary>
        /// 20200609批注：传输控制线程：关键在于，等待，当检测到内存不足的情况下，随即进入等待状态。计算能力远高于传输及下游处理能力时。
        /// </summary>
        /// <param name="TransferObject"></param>
        private void DataTransControlThread(object TransferObject)
        {
            //(0-1) Start the Printing Configuration: SET the print file infomation and confirm the print file formatt
            bool returnFlag = StartCloseJOB(true);//开启JOB并赋值关键打印参数：

            TransferParam transferParam = TransferObject as TransferParam;//类型转换——输入数据
            int CurrentStartLayer = transferParam.CurrentStartLayer/*0*/;//Current Print Layer: 20200514 new Create
            int LayerEndNum = transferParam.LayerEndNum/*50*/;//打印的终止层数，放在此处，仅仅是方便测试而已：20200411新增：

            int nResult = 0;//核心代码的执行结果
            bool m_bStartJob = true;//
            bool m_bCloseJob = false;//放在此处，仅仅是方便测试而已：20200411新增：

            while (m_bStartJob == true && m_bCloseJob == false)
            {
                for (int i = CurrentStartLayer; i < LayerEndNum + 1; i++)//压力测试，连续加载：0x100000 
                {
                    string path = System.Windows.Forms.Application.StartupPath + @"\JOB输出文件\输出.bmp";//20200430新增：
                    nResult = WriteImgLayerData(path, i + 1, 0/*0*/, true);//参数依次是;job文件名称、层索引、打印方向、铺粉方向：20200411新增
                    if (nResult > 0)//20200205:写入成功
                    {
                        royal.royal.g_prtimg_layer.nLayerIndex++;//写入的层数的索引增加1
                        CurrentStartLayer = royal.royal.g_prtimg_layer.nLayerIndex;
                        Thread.Sleep(10);//等待10ms//
                    }
                    else if (nResult == -110001)//20200205:写入失败//(1)分析错误号：做出相应处理
                    {
                        //MessageBox.Show(i.ToString()+ "层数据传输等待：PC内存不足");//底层，存在1个内存管理机制，内存不足的情况下， 会暂停write线程的工作，挂起
                        Thread.Sleep(100);//等待100ms
                        break;
                    }
                    else if (nResult == -110002)
                    {
                        MessageBox.Show("数据传输失败：PASS计算小于0");//底层，存在1个内存管理机制，内存不足的情况下， 会暂停write线程的工作，挂起
                        Thread.Sleep(100);//等待100ms
                        break;
                    }
                }
            }
        }
        public override System.Windows.Forms.AutoValidate AutoValidate { get; set; }
        private void 主界面_SizeChanged(object sender, EventArgs e)
        {
            //20200524新增：
            MouseWheel -= new MouseEventHandler(Form1_MouseWheel);//添加鼠标滚轮事件//迁移到InitializeComponent中间//20200524新增：消除BUG至关重要
            g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height/*+30*/));
            MouseWheel += new MouseEventHandler(Form1_MouseWheel);//添加鼠标滚轮事件//迁移到InitializeComponent中间//20200524新增：消除BUG至关重要
            //InitiateDeviceResize();
            g_SharpControl.PaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height/*+30*/));
            //d3DDevice.ImmediateContext.Flush();//不必要每次都调用这个，影响GPU的性能
        }

        private void panel3_Paint_1(object sender, PaintEventArgs e)
        {
#if false//20200527：更换主界面
            //panel2为移动主界面显示页数操作
            //this.panel2.Location = new Point(this.panel3.Width - this.panel2.Width/*(this.panel3.Width - this.panel2.Width)/2+20*/, this.panel3.Height - this.panel2.Height);
            //初次绘制主界面//本部分废弃，其实都不要初次绘制
            if (firstPaintFlag < 1)//（a)初次绘制主界面
            {
                //初次绘制：UI线程中完成
                //不使用双缓冲方式绘制
                _3DP_GUI.SetBitmap(this.panel3.Width, this.panel3.Height);//——————创建容器，并设定大小
                Graphics g = Graphics.FromImage(_3DP_GUI.bt);
                _3DP_GUI.Select_rectangle(g, this.panel3.Width, this.panel3.Height);
                this.panel3.CreateGraphics().DrawImage(_3DP_GUI.bt, new Point(0, 0));//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
                firstPaintFlag++;
            }
            else//(b)重绘主界面
            {
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据
                for (_3DP_GUI.cal_num_p = 0; _3DP_GUI.cal_num_p < _3DP_GUI.cal_PX.Count; _3DP_GUI.cal_num_p++)//———————————5这个值是临时测试的,COUNT才是正确的——————！！！！
                {
                    _3DP_GUI.cal_PX[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_px[_3DP_GUI.cal_num_p] * _3DP_GUI.cal_pixel2MM + (this.Width / 2 + 20);
                    _3DP_GUI.cal_PY[_3DP_GUI.cal_num_p] = (int)_3DP_GUI.cal_py[_3DP_GUI.cal_num_p] * _3DP_GUI.cal_pixel2MM + (this.Height / 2 + 20);
                }
                //20200223新增:多线程绘制图像数据
                //20200223新增:多线程绘制图像数据
                _3DP_GUI.cal_num_p = 0;
                data_storage();//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
            } 
            //20200223新增:多线程绘制图像数据
            //20200223新增:多线程绘制图像数据
            _3DP_GUI.cal_num_p = 0;renderControl1
            data_storage();//最后完成显示—————需要移动到主界面———参数要确认———！！！！！
#else
            g_SharpControl.RepaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(this.renderControl1.Top, this.renderControl1.Left, this.renderControl1.Width, this.renderControl1.Height/*+30*/));

#endif
        }
        //20200531：实现成形层的逐层预览刷新
        private void hScrollBar2_ValueChanged(object sender, EventArgs e)
        {
            ImportCLIFlag = false;//标志：CLI导入中或者未导入
            SetCurrentCLI = HScrollBar.Value;

            //GetLayerCLIs(g_CurrentCLI);//获取第1层的全部零件数据:g_SharpControl.tempJobItems——>RemoteCLIs c_RemoteCLIs
            //g_RemoteCLIs.Add(c_RemoteCLIs);
            //g_SharpControl.ToRemoteCLIsList = g_RemoteCLIs;//20200528使用属性：g_RemoteCLIs——>g_SharpControl.gc_RemoteCLIs

            ImportCLIFlag = true;//CLI导入标志
            //this.Invalidate();
            this.renderControl1.Invalidate();//修改主菜单导致的莫名其妙的BUG：this.Invalidate无法触发自定义控件的重绘
            //g_SharpControl.RepaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(this.renderControl1.Top, this.renderControl1.Left, this.renderControl1.Width, this.renderControl1.Height/*+30*/));

        }

        private void renderControl1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            g_SharpControl.RepaintDefaultControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(this.renderControl1.Top, this.renderControl1.Left, this.renderControl1.Width, this.renderControl1.Height/*+30*/));
        }

        bool SaveBmpFlag = true;
        private void SaveBMPBtn_Click(object sender, EventArgs e)//202006010数据处理及传送：
        {
            //更换动作标志位
            if (SaveBmpFlag == false)//(b)根据ID反转背景图片
            {
                //(sender as Control).BackColor = Color.MintCream;
                (sender as Control).Text = "生成\r\n图片";
            }
            else
            {
                (sender as Control).Text = "重新\r\n生成";
                SaveBMPBtn.BackColor = System.Drawing.Color.CornflowerBlue;
            }
            //计算输出
            if (SaveBmpFlag == true)//开启挤墨
            {
#if false
                //（1）开清洗阀门（==等效：关墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                g_SharpControl.RenderToWic(true,1);
                SaveBmpFlag = false;
#endif
            }
            else//关闭挤墨
            {
#if false
                //（1）关清洗阀门（==等效：开墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                g_SharpControl.RenderToWic(false,1);
                SaveBmpFlag = true;
#endif
            }
        }

        private void PressureTestBtn_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 100; i++)//连续处理100张图片压力测试
            {
#if false
                g_SharpControl.RenderToWic(true, 1);
                this.Text = "压力测试中：当前处理第" + i + "层";
                this.Invalidate();
#endif
            }
        }

        /**************************************SHARPDX:20200509**************************************/
        /*******************************墨车系统回零：20200326添加***********************************/
        private void UpdateBarValueMethod2(int iValue)
        {
            if (this.WorkProgressBar.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
            {
                if (null != WorkProgressBar && !WorkProgressBar.IsDisposed)//jobsProgressBar被释放？且不为空
                {
                    int safeValue = iValue;
                    if (safeValue < WorkProgressBar.Minimum)
                    {
                        safeValue = WorkProgressBar.Minimum;
                    }
                    if (safeValue > WorkProgressBar.Maximum)
                    {
                        safeValue = WorkProgressBar.Maximum;
                    }
                    if (safeValue != iValue)
                    {
                        Log4Net.Info($"UpdateBarValueMethod2: clamp, requested={iValue}, safe={safeValue}, min={WorkProgressBar.Minimum}, max={WorkProgressBar.Maximum}");
                    }
                    WorkProgressBar.Value = safeValue;
                    //double rate = (double)iValue / (double)50 * 100;
                    string tempRate = safeValue.ToString();
                    string layerNum = this.LayerEnd.Text;
                    string showRate = "PRT进度： " + tempRate + "/" + layerNum + "层";//保留小数点后两位
                    this.WorkRate.Text = showRate;//保留小数点后两位
                    this.panel1.Refresh();
                }
            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpdateBarValue2 UpdateBar = new UpdateBarValue2(UpdateBarValueMethod2);
                this.WorkProgressBar.Invoke(UpdateBar, iValue);
            }
        }
        private void InitColorCards()//202000613批注
        {
            g_SharpControl.InputColorCards[0] = SharpDX.Color.White;//2D背景色1：
            g_SharpControl.InputColorCards[1] = SharpDX.Color.LightGray;//2D背景色2：
            g_SharpControl.InputColorCards[2] = SharpDX.Color.White;//基板背景色：
            g_SharpControl.InputColorCards[3] = SharpDX.Color.Orange/*Gray*//*OrangeRed*//*Yellow*//*LightGreen*//*Orange*//*CornflowerBlue*/;//20201124修改：//标尺背景色：
            g_SharpControl.InputColorCards[4] = SharpDX.Color.Black;//1级网格色：
            g_SharpControl.InputColorCards[5] = SharpDX.Color.Black;//2级网格色：
            g_SharpControl.InputColorCards[6] = SharpDX.Color.DarkSlateGray/*CornflowerBlue*//*OrangeRed*/;//定位孔色：//20220524修改：SharpDX.Color.CornflowerBlue
            g_SharpControl.InputColorCards[7] = SharpDX.Color.BlueViolet/*BlueViolet*//*MediumVioletRed*//*OrangeRed*//*Orange*//*MediumSeaGreen*//*RoyalBlue*/;//标尺前景色：修改为实体零件背景色

        }
        bool InitColorSysFlag = true;//20200612新增
        private RawColor4[] InputColorCards = new RawColor4[8];
        private void UISytleMenuItemBtn_Click(object sender, EventArgs e)
        {
            if (InitColorSysFlag == true)//执行动作标志位
            {
                /* RawColor4[] InputColorCards=new RawColor4[8];*/
#if false//测试
                InputColorCards[0] = SharpDX.Color.White;//2D背景色1：
                InputColorCards[1] = SharpDX.Color.LightGray;//2D背景色2：
                InputColorCards[2] = SharpDX.Color.White;//基板背景色：
                InputColorCards[3] = SharpDX.Color.CornflowerBlue;//标尺背景色：
                InputColorCards[4] = SharpDX.Color.Black;//1级网格色：
                InputColorCards[5] = SharpDX.Color.Black;//2级网格色：
                InputColorCards[6] = SharpDX.Color.OrangeRed;//定位孔色：
                InputColorCards[7] = SharpDX.Color.Black;//标尺前景色：修改为实体零件背景色
#else
                InputColorCards[0] = g_SharpControl.InputColorCards[0];//2D背景色1：
                InputColorCards[1] = g_SharpControl.InputColorCards[1];//2D背景色2：
                InputColorCards[2] = g_SharpControl.InputColorCards[2];//基板背景色：
                InputColorCards[3] = g_SharpControl.InputColorCards[3];//标尺背景色：
                InputColorCards[4] = g_SharpControl.InputColorCards[4];//1级网格色：
                InputColorCards[5] = g_SharpControl.InputColorCards[5];//2级网格色：
                InputColorCards[6] = g_SharpControl.InputColorCards[6];//定位孔色：
                InputColorCards[7] = g_SharpControl.InputColorCards[7];//实体零件背景色：
#endif
                InitColorSysFlag = false;
            }
            else { }

            软件UI风格设计 f = new 软件UI风格设计(InputColorCards);//下发数据
            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK || result == DialogResult.Yes)
            {
                InputColorCards = f.ColorCards;
                g_SharpControl.InputColorCards = InputColorCards;//20200612新增：
                g_SharpControl.ModifyColorSysFlag = true;//20200612新增：生效标志位，统一更新画刷系统色彩
                //this.Invalidate();//刷新
                //g_SharpControl.PaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height/*+30*/));
                this.renderControl1.Invalidate();
            }
            else if (result == DialogResult.Cancel)
            {
            }
        }

        private void MainMenuBtn_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rectangle = new Rectangle(0/*MainMenuBtn.Bounds.X-6*/, 0/*MainMenuBtn.Bounds.Y-3*/, MainMenuBtn.Bounds.Width, MainMenuBtn.Bounds.Height);
            //ControlPaint.DrawBorder(e.Graphics, rectangle, SystemColors.ControlDarkDark, ButtonBorderStyle.Solid);
            ControlPaint.DrawBorder3D(e.Graphics, rectangle, Border3DStyle.Raised/*Adjust*//*Raised*//*RaisedInner*/);
            //ControlPaint.DrawButton(e.Graphics, rectangle, ButtonState.Pushed/*, SystemColors.ControlDarkDark, ButtonBorderStyle.Solid*/);
        }
        bool PowderDirection = true;
        private void PowderBtn_Click(object sender, EventArgs e)
        {
            if (PowderDirection == true)
            {
                LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();
                AutomoveComponent.TrapMoveUp(4, true/*p.m_bMoveModeFlag[3]*/, p.m_Svel[3], p.m_Step[3], RollerParam);//20200618替换
                Thread.Sleep(500);
                PowderDirection = false;
            }
            else
            {
                LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();
                AutomoveComponent.TrapMoveDown(4, true/*p.m_bMoveModeFlag[3]*/, p.m_Svel[3], p.m_Step[3], RollerParam);//20200618替换
                Thread.Sleep(500);
                PowderDirection = true;
            }

        }

        private void 主界面_Resize(object sender, EventArgs e)
        {
            //g_SharpControl.RepaintDefaultControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(this.renderControl1.Top, this.renderControl1.Left, this.renderControl1.Width, this.renderControl1.Height/*+30*/));
        }

        private void 打印校准图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CalibrationMoudle f = new CalibrationMoudle();//20200202修改

            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
            }
            else if (result == DialogResult.Cancel)//退出时，什么都不做
            {
            }
        }

        //private bool g_bShowJob = false;//20201111新增：显示或者隐藏JOB栏
        private void SetBtnStyle(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;   //样式  
            btn.ForeColor = Color.Transparent;//前景  
            btn.BackColor = Color.Transparent;//去背景  
            btn.FlatAppearance.BorderSize = 0;//去边线 
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(100, 0, 0, 0);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(100, 0, 0, 0);
        }
        private int panel6Width = 252;//470
        private void ShowHideJobBtns_Click(object sender, EventArgs e)
        {
            if (Convert.ToInt32((sender as Control).Tag) == 1)//隐藏任务栏
            {
                this.panel4.Visible = false;
                this.panel4.Width = 0;
                this.panel3.Width = this.panel5.Width - this.panel4.Width;
                this.button5.Visible = true;
                this.panel4.Visible = true;

                g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));

                string msg = $"显示JOB任务栏";
                Log4Net.Info(msg);
            }
            else if (Convert.ToInt32((sender as Control).Tag) == 2)//显示任务栏
            {
                this.panel4.Visible = false;
                this.panel4.Width = 220;
                this.panel3.Width = this.panel5.Width - this.panel4.Width;
                this.button5.Visible = false;
                this.panel4.Visible = true;

                g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));

                string msg = $"不显示JOB任务栏";
                Log4Net.Info(msg);
            }
            else if (Convert.ToInt32((sender as Control).Tag) == 3)//隐藏监控栏
            {
                this.panel6.Visible = false;
                this.panel6.Width = 0;
                this.panel7.Width = this.panel8.Width/*this.panel7.Width-this.panel6.Width*/;
                this.panel5.Width = panel1.Width - panel7.Width;
                this.panel8.Width = 24;
                this.panel5.Left = panel7.Width;
                (sender as Control).Tag = 4;
                this.panel6.Visible = true;

                g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
                this.button6.Text = "显" + "\n\n" + "示" + "\n\n" + "监" + "\n\n" + "控";

                string msg = $"不显示监控栏";
                Log4Net.Info(msg);
            }
            else if (Convert.ToInt32((sender as Control).Tag) == 4)//显示监控栏
            {
                this.panel6.Visible = false;
                this.panel6.Width = panel6Width;// 246;20210304修改：修正
                this.panel7.Width = this.panel6.Width + this.panel8.Width;
                this.panel8.Left = 0;
                this.panel5.Width = panel1.Width - panel7.Width - 1;
                this.panel5.Left = panel7.Width + 1;
                this.panel8.Width = 24;
                (sender as Control).Tag = 3;
                this.panel6.Visible = true;

                g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
                this.button6.Text = "隐" + "\n\n" + "藏" + "\n\n" + "监" + "\n\n" + "控";

                string msg = $"显示监控栏";
                Log4Net.Info(msg);
            }
            else if (Convert.ToInt32((sender as Control).Tag) == 5)//隐藏校准栏
            {
                this.panel6.Visible = false;
                //this.panel5.Visible = false;

                panel6Width = 252;
                this.panel6.Width = panel6Width;// 246;20210304修改：修正
                this.panel7.Width = this.panel6.Width + this.panel8.Width;
                this.panel8.Left = 0;
                this.panel5.Width = panel1.Width - panel7.Width - 1;
                g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top,
                    renderControl1.Left, renderControl1.Width, renderControl1.Height));//非常关键
                this.panel5.Left = panel7.Width + 1;
                this.panel8.Width = 24;
                this.panel6.Visible = true;
                (sender as Control).Tag = 6;
                (sender as Control).Text = "模组\r\n校准";

                string msg = $"不显示模组校准栏";
                Log4Net.Info(msg);

                //this.panel6.Visible = true;
                //this.panel5.Visible = true;

                //g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
            }
            else if (Convert.ToInt32((sender as Control).Tag) == 6)//显示校准栏
            {
                this.panel6.Visible = false;
                //this.panel5.Visible = false;
                panel6Width = 470;
                this.panel6.Width = panel6Width;// 246;20210304修改：修正
                this.panel7.Width = this.panel6.Width + this.panel8.Width;
                this.panel8.Left = 0;
                this.panel5.Width = panel1.Width - panel7.Width - 1;
                //this.renderControl1.Width = panel5.Width - panel9task.Width;
                g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top,
                    renderControl1.Left, renderControl1.Width, renderControl1.Height));//非常关键

                this.panel5.Left = panel7.Width + 1;

                this.panel8.Width = 24;
                this.panel6.Visible = true;

                (sender as Control).Tag = 5;
                (sender as Control).Text = "关闭\r\n校准";

                string msg = $"显示模组校准栏";
                Log4Net.Info(msg);

                //this.panel5.Visible = true;
                //g_SharpControl.ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            //20230317修改：修复中断打印之后，重新启动设置新区间，打印过程中的实际传输实际仍然按照第1层数据发送的BUG
            g_SharpControl.RenderToWic(true, 2, 2, 3, 1);//20230317新建批注：默认起始打印层为1//
        }

        private void 主界面_ClientSizeChanged(object sender, EventArgs e)
        {
            g_SharpControl.RepaintDefaultControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(this.renderControl1.Top, this.renderControl1.Left, this.renderControl1.Width, this.renderControl1.Height/*+30*/));
        }

        private bool FirstJOBFlag = true;
        private bool OutputingJOBFlag = true;//正在输出JOB的状态

        private void ConfigureCorrectionFigure()//20210306新增：
        {
            pictureBoxFlag1.BackColor = Color.LightCoral;
            this.pictureBoxFlag1.Refresh();
            //（1）更新校准系统参数：
            this.progressBar1.Value = 0;
            this.label31.Text = "0%";
            this.label31.Refresh();
            royalCorrectSystem.g_LPCAL_PARAM.nGroups = k_PrintNozzleHeadConfigure.m_nGroups/*7*/;//7个喷头：20210325批注：本行数据不可写死，否则会造成校准图生成错误。
            royalCorrectSystem.g_LPCAL_PARAM.nDataLine = k_PrintNozzleHeadConfigure.m_nDataLine/*320*//*128*/;//320喷嘴，20210313批注：本行数据不可写死，否则会造成校准图生成错误。
            royalCorrectSystem.g_LPCAL_PARAM.nSplits = k_PrintNozzleHeadConfigure.m_nSplits/*4*/;//4列嘴，20210313批注：本行数据不可写死，否则会造成校准图生成错误。
            royalCorrectSystem.g_LPCAL_PARAM.nOverLapJets = k_PrintNozzleHeadConfigure.m_nOverLapJets/*2*/;//2行重叠嘴，20210313批注：本行数据不可写死，否则会造成校准图生成错误。

            royalCorrectSystem.g_LPCAL_PARAM.szFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart\";//世彪批注：生成的校准图位置（20200722）

            royalCorrectSystem.g_LPCAL_PARAM.nCorrctionFigureWidthBytes = k_PrintNozzleHeadConfigure.m_nCorrctionFigureWidthBytes /*1001*/;//20230408新建并修改：/*1312*///第1代设备默认是1312//1001字节
            royalCorrectSystem.PD_UpdatParam(ref royalCorrectSystem.g_LPCAL_PARAM);
            //(2)生成5张校准图：
            string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";//输入的CLI文件的存放目录。                                                                                    
            if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\CalibrationChart"))//（1-2）判断打开的路径是否存在
            {
                Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\CalibrationChart");//创建路径
            }
            this.progressBar1.Value = 5;
            this.label31.Text = "5%";
            this.label31.Refresh();
            this.progressBar1.Refresh();

            int UniversalOffset = 50;//20210319新建修改：
            int UniversalOffsetY = 50;//20210319新建修改：
            try
            {
                UniversalOffset = (int)(k_PrintNozzleHeadConfigure.m_dUniversalOffset / 25.4 * 635 / 8);//设置偏移值//mm要转换为像素
                UniversalOffsetY = (int)(k_PrintNozzleHeadConfigure.m_dUniversalOffsetY / 25.4 * 635 / 8);//设置偏移值//mm要转换为像素//20210325修改：修复残存的BUG,少了一个Y
            }
            catch (Exception)
            {
                UniversalOffset = 50;
                UniversalOffsetY = 50;
                MessageBox.Show("偏移值参数输入错误，请输入有效的偏移值！");
            }

            royalCorrectSystem.PD_GenPhStatus(UniversalOffset);//喷嘴状态
            this.progressBar1.Value = 10;
            this.label31.Refresh();
            this.label31.Text = "10%";
            this.progressBar1.Refresh();
            royalCorrectSystem.PD_GenBiDirOffset(UniversalOffset, UniversalOffsetY);//X 往返差
            royalCorrectSystem.PD_GenBiDirOffsetInAFigure(UniversalOffset, UniversalOffsetY);//适应于一次性打印的X往返差图

            this.progressBar1.Value = 40;
            this.label31.Refresh();
            this.label31.Text = "40%";
            this.progressBar1.Refresh();
            //royalCorrectSystem.PD_GenVertivalCheck(UniversalOffset);//垂直校准
            this.progressBar1.Value = 60;
            this.label31.Refresh();
            this.label31.Text = "60%";
            this.progressBar1.Refresh();
            //royalCorrectSystem.PD_GenColorOffset(0, UniversalOffset);//X套色 综合
            this.progressBar1.Value = 80;
            this.label31.Refresh();
            this.label31.Text = "80%";
            this.progressBar1.Refresh();
            //royalCorrectSystem.PD_GenColorOffset(1, UniversalOffset);//X套色 综合 
            this.progressBar1.Value = 100;
            this.label31.Refresh();
            this.label31.Text = "100%";
            this.progressBar1.Refresh();

            //string[] calirationFigurePaths = new string[6] { @"\垂直校准图.bmp" , @"\往返差校准图-0.bmp", @"\往返差校准图-0.bmp",
            //    @"\喷头套色校准图-0.bmp", @"\喷头套色校准图-0.bmp", @"\STATUS.bmp" };
            //for (int i = 0; i < 6; i++)//将6副校准图刷新到内存中
            //{
            //    string CalibrationFilePath2 = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
            //    string BMPFilePath = CalibrationFilePath2 + calirationFigurePaths[i]/*@"\APCLROT-0.bmp"*/;
            //    g_SharpControl.LoadingFromBMPFile2(BMPFilePath, i);

            //    this.progressBar1.Value = (int)((100 - 60 + 1) / 6 * (i + 1) + 60);
            //    this.label31.Refresh();
            //    this.label31.Text = ((int)((100 - 60 + 1) / 6 * (i + 1)) + 60).ToString() + "%";
            //    this.progressBar1.Refresh();
            //}

            pictureBoxFlag1.BackColor = Color.Lime;
            this.pictureBoxFlag1.Refresh();
        }


        public FeedbackInCorrection g_RYSYSParamFeedbackInCorrection = new FeedbackInCorrection();//20210304修改：
        private int[] ShowNozzleCorrectionFigure = new int[4] { 0, 0, 0, 0 };//20210325新增：0位起始值，1为显示第1张图，2位显示第2张图。
        bool CorrectionDuringCreateFlag = false;
        private void SetPrintNozzleHeadConfigBtn_Click(object sender, EventArgs e)//20210304新增:
        {
            if (CorrectionDuringCreateFlag == false)
            {
                CorrectionDuringCreateFlag = true;
                try
                {
                    int myTag = Convert.ToInt32((sender as Control).Tag);//获取列表控件的Tag中存储的ID        
                    //(sender as Control).BackColor = Color.LimeGreen;//根据ID反转颜色状态

                    //计算并执行动作：
                    switch (myTag)
                    {
                        case 1://根据给定的参数，生成新校准图
                            SaveCorrectionJsonFile();//20210325新建：保存喷头校准参数设置文件
                            ConfigureCorrectionFigure();//20210306修改：
                            break;

                        case 2://预览垂直校准图
                            if (ShowNozzleCorrectionFigure[0] == 0)
                            {
                                g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                string BMPFilePath = CalibrationFilePath + @"\垂直校准图.bmp"/*@"\APCLROT-0.bmp"*/;
                                g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                pictureBoxFlag2.BackColor = Color.Lime;
                                this.pictureBoxFlag2.Refresh();

                                ShowNozzleCorrectionFigure[0] = 1;
                            }
                            else if (ShowNozzleCorrectionFigure[0] == 1)
                            {
                                g_SharpControl.DisposeBMPFile();//20210327新增：
                                g_SharpControl.g_CorrectionFigureFlag = 0;//临时显示是1；

                                pictureBoxFlag2.BackColor = Color.LightCoral;
                                this.pictureBoxFlag2.Refresh();

                                ShowNozzleCorrectionFigure[0] = 0;
                            }
                            this.renderControl1.Invalidate();

                            break;
                        case 3://预览往返差校准图0和1，单击预览第一帧，再单击预览第2帧
                            if (k_PrintNozzleHeadConfigure.bPrintGoBackFigureAtOnce == false)//分两次打印
                            {
                                if (ShowNozzleCorrectionFigure[1] == 0)
                                {
                                    g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                    string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                    string BMPFilePath = CalibrationFilePath + @"\往返差校准图-0.bmp"/*@"\APCLROT-1.bmp"*/;
                                    g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                    pictureBoxFlag3.BackColor = Color.Lime;
                                    this.pictureBoxFlag3.Refresh();
                                    pictureBoxFlag7.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag7.Refresh();

                                    ShowNozzleCorrectionFigure[1] = 1;
                                }
                                else if (ShowNozzleCorrectionFigure[1] == 1)
                                {
                                    g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                    string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                    string BMPFilePath = CalibrationFilePath + @"\往返差校准图-1.bmp"/*@"\APCLROT-1.bmp"*/;
                                    g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                    pictureBoxFlag3.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag3.Refresh();
                                    pictureBoxFlag7.BackColor = Color.Lime;
                                    this.pictureBoxFlag7.Refresh();

                                    ShowNozzleCorrectionFigure[1] = 2;
                                }
                                else if (ShowNozzleCorrectionFigure[1] == 2)
                                {
                                    g_SharpControl.DisposeBMPFile();//20210327新增：
                                    g_SharpControl.g_CorrectionFigureFlag = 0;//临时显示是1；

                                    pictureBoxFlag3.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag3.Refresh();
                                    pictureBoxFlag7.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag7.Refresh();

                                    ShowNozzleCorrectionFigure[1] = 0;
                                }
                                this.renderControl1.Invalidate();
                            }
                            else
                            {
                                if (ShowNozzleCorrectionFigure[1] == 0)
                                {
                                    g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                    string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                    string BMPFilePath = CalibrationFilePath + @"\往返差校准图-3.bmp"/*@"\APCLROT-1.bmp"*/;
                                    g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                    pictureBoxFlag3.BackColor = Color.Lime;
                                    this.pictureBoxFlag3.Refresh();
                                    pictureBoxFlag7.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag7.Refresh();

                                    ShowNozzleCorrectionFigure[1] = 1;
                                }
                                else if (ShowNozzleCorrectionFigure[1] == 1)
                                {
                                    g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                    string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                    string BMPFilePath = CalibrationFilePath + @"\往返差校准图-3.bmp"/*@"\APCLROT-1.bmp"*/;
                                    g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                    pictureBoxFlag3.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag3.Refresh();
                                    pictureBoxFlag7.BackColor = Color.Lime;
                                    this.pictureBoxFlag7.Refresh();

                                    ShowNozzleCorrectionFigure[1] = 2;
                                }
                                else if (ShowNozzleCorrectionFigure[1] == 2)
                                {
                                    g_SharpControl.DisposeBMPFile();//20210327新增：
                                    g_SharpControl.g_CorrectionFigureFlag = 0;//临时显示是1；

                                    pictureBoxFlag3.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag3.Refresh();
                                    pictureBoxFlag7.BackColor = Color.LightCoral;
                                    this.pictureBoxFlag7.Refresh();

                                    ShowNozzleCorrectionFigure[1] = 0;
                                }
                                this.renderControl1.Invalidate();
                            }


                            break;
                        case 4://预览喷头套色校准图0和1，单击预览第一帧，再单击预览第2帧
                            if (ShowNozzleCorrectionFigure[2] == 0)
                            {
                                g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                string BMPFilePath = CalibrationFilePath + @"\喷头套色校准图-0.bmp"/* @"\APRETDIV.bmp"*/;
                                g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                pictureBoxFlag4.BackColor = Color.Lime;
                                this.pictureBoxFlag4.Refresh();
                                pictureBoxFlag8.BackColor = Color.LightCoral;
                                this.pictureBoxFlag8.Refresh();

                                ShowNozzleCorrectionFigure[2] = 1;
                            }
                            else if (ShowNozzleCorrectionFigure[2] == 1)
                            {
                                g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                string BMPFilePath = CalibrationFilePath + @"\喷头套色校准图-1.bmp"/* @"\APRETDIV.bmp"*/;
                                g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                pictureBoxFlag4.BackColor = Color.LightCoral;
                                this.pictureBoxFlag4.Refresh();
                                pictureBoxFlag8.BackColor = Color.Lime;
                                this.pictureBoxFlag8.Refresh();

                                ShowNozzleCorrectionFigure[2] = 2;
                            }
                            else if (ShowNozzleCorrectionFigure[2] == 2)
                            {
                                g_SharpControl.DisposeBMPFile();//20210327新增：
                                g_SharpControl.g_CorrectionFigureFlag = 0;//临时显示是1；

                                pictureBoxFlag4.BackColor = Color.LightCoral;
                                this.pictureBoxFlag4.Refresh();
                                pictureBoxFlag8.BackColor = Color.LightCoral;
                                this.pictureBoxFlag8.Refresh();

                                ShowNozzleCorrectionFigure[2] = 0;
                            }
                            this.renderControl1.Invalidate();

                            break;
                        case 5://预览喷头状态图
                            if (ShowNozzleCorrectionFigure[3] == 0)
                            {
                                g_SharpControl.g_CorrectionFigureFlag = 1;//临时显示是1；

                                string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";
                                string BMPFilePath = CalibrationFilePath + @"\STATUS.bmp"/*@"\Vertical.bmp"*/;
                                g_SharpControl.LoadingFromBMPFile(BMPFilePath);
                                pictureBoxFlag5.BackColor = Color.Lime;
                                this.pictureBoxFlag5.Refresh();

                                ShowNozzleCorrectionFigure[3] = 1;
                            }
                            else if (ShowNozzleCorrectionFigure[3] == 1)
                            {
                                g_SharpControl.DisposeBMPFile();//20210327新增：
                                g_SharpControl.g_CorrectionFigureFlag = 0;//临时显示是1；

                                pictureBoxFlag5.BackColor = Color.LightCoral;
                                this.pictureBoxFlag5.Refresh();

                                ShowNozzleCorrectionFigure[3] = 0;
                            }
                            this.renderControl1.Invalidate();

                            break;

                        case 6://1-校准参数保存并生效       
                               //pictureBoxFlag6.BackColor = Color.LightCoral; ///20210321暂时注释掉：
                               //this.pictureBoxFlag6.Refresh();
                               //SaveJsonFile();
                               //pictureBoxFlag6.BackColor = Color.Lime;
                               //this.pictureBoxFlag6.Refresh();
                               //int[] temperror1 = new int[64 * 32 * 2]; temperror1[0]= k_PrintNozzleHeadConfigure.SingleError;
                               //int[] temperror2 = new int[16 * 32]; temperror2[1]= k_PrintNozzleHeadConfigure.YError ;
                               //RoyalMap.UpdataRoyalPrintCardWithFeedbackData(k_PrintNozzleHeadConfigure.DoubleError, temperror1, temperror2);

                            校准参数输入 f = new 校准参数输入();//20200202修改   
                            f.k_RYSYSParamFeedbackInCorrection = g_RYSYSParamFeedbackInCorrection;
                            DialogResult result = f.ShowDialog();
                            if (result == DialogResult.OK)//OK时，执行对应操作
                            {
                                g_RYSYSParamFeedbackInCorrection = f.k_RYSYSParamFeedbackInCorrection;
                                k_PrintNozzleHeadConfigure.m_RYSYSParamFeedbackInCorrection = f.k_RYSYSParamFeedbackInCorrection;
                                SaveCorrectionJsonFile();//20210325新建：保存喷头校准参数设置文件//20230412新增：


                                int[] temperror1 = new int[64 * 32 * 2];
                                int[] temperror2 = new int[16 * 32];
                                //(1)非常关键：双向偏差值
                                int BiDirEncPrtOff/*royal.g_sys_param.nBiDirEncPrtOff*/ = g_RYSYSParamFeedbackInCorrection.m_nXBackForthFeedBack - 6;
#if false
                                //(2)更新X向的套色偏差值
                                int[] g_nXBetweenHead = new int[2 * 6]/*{5,5,5,5,5,5,5,5,5,5,5,5}*/;//X向套色偏差校准反馈值：2*6=12//20210322新增：依次为1-2-正向-喷头偏移距离、1-2-逆向-喷头偏移距离
                                g_nXBetweenHead = g_RYSYSParamFeedbackInCorrection.m_nXBetweenHead;
                                for (int i = 0; i < 12; i++)
                                {
                                    g_nXBetweenHead[i] = g_nXBetweenHead[i] - 5;
                                }
                                for (int i = 6; i > 0; i--)
                                {
                                    for (int j = 1; j < i; j++)
                                    {
                                        g_nXBetweenHead[i - 1] = g_nXBetweenHead[i - 1] + g_nXBetweenHead[j - 1];//计算正确的累计的喷头相对于1号喷头的偏移值：正向
                                        g_nXBetweenHead[i - 1 + 6] = g_nXBetweenHead[i - 1 + 6] + g_nXBetweenHead[j - 1 + 6];//计算正确的累计的喷头相对于1号喷头的偏移值：逆向
                                    }
                                }
                                for (int i = 0; i < 7; i++)
                                {
                                    for (int j = 0; j < 4; j++)
                                    {
                                        if (i == 0)
                                        {
                                            temperror1[i * 32 * 2 + j * 2]/*royal.g_sys_param.nPhXRowPrtOff*/ =
                                                g_RYSYSParamFeedbackInCorrection.m_nXNestFeedback[i * 8 + j] - 2;//非常关键：X向套色偏差值
                                            temperror1[i * 32 * 2 + j * 2 + 1]/*royal.g_sys_param.nPhXRowPrtOff*/ =
                                                g_RYSYSParamFeedbackInCorrection.m_nXNestFeedback[i * 8 + j + 4] - 2;//非常关键：X向套色偏差值
                                        }
                                        else
                                        {
                                            temperror1[i * 32 * 2 + j * 2]/*royal.g_sys_param.nPhXRowPrtOff*/ =
                                                g_RYSYSParamFeedbackInCorrection.m_nXNestFeedback[i * 8 + j] - 2//从索引换算出正确的反馈值
                                                + g_nXBetweenHead/*g_RYSYSParamFeedbackInCorrection.m_nXBetweenHead*/[i - 1];//非常关键：X向套色偏差值//从索引换算出正确的反馈值
                                            temperror1[i * 32 * 2 + j * 2 + 1]/*royal.g_sys_param.nPhXRowPrtOff*/ =
                                                g_RYSYSParamFeedbackInCorrection.m_nXNestFeedback[i * 8 + j + 4] - 2//从索引换算出正确的反馈值
                                                + g_nXBetweenHead/*g_RYSYSParamFeedbackInCorrection.m_nXBetweenHead*/[(i - 1) + 6];//非常关键：X向套色偏差值//从索引换算出正确的反馈值
                                        }
                                    }
                                }
#endif
#if false
                                //(3)更新X向的套色偏差值
                                for (int i = 0; i < 7; i++)
                                {
                                    temperror2[i/**32*/]/*royal.g_sys_param.nPhYJetOff*/ = g_RYSYSParamFeedbackInCorrection.m_nYNestFeedback[i] - 2;//非常关键：Y向套色偏差值//20210328修改：正确数据：先颜色再组数
                                }
#endif
                                //(4)更新以上3大类的套色偏差值
                                float m_MovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);//20200328新增：打印速度
                                bool returnValue = RoyalMap.UpdataRoyalPrintCardWithFeedbackData(BiDirEncPrtOff /*k_PrintNozzleHeadConfigure.DoubleError*/, temperror1, temperror2, m_MovSpeed);
                                if (returnValue == true)
                                {
                                    //pictureBoxFlag6.BackColor = Color.LightCoral;
                                    //this.pictureBoxFlag6.Refresh();
                                    //SaveJsonFile();
                                    pictureBoxFlag6.BackColor = Color.Lime;
                                    this.pictureBoxFlag6.Refresh();
                                }

                            }
                            else if (result == DialogResult.Cancel)//退出时，什么都不做
                            {
                            }

                            break;

                    }
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.ToString());
                }
                CorrectionDuringCreateFlag = false;
            }
            else { }
        }


        public void SaveCorrectionJsonFile()
        {
            string JsonPath = System.Windows.Forms.Application.StartupPath + @"\NozzleConfigInCorrection.json";//json配置文件：启动目录
            string JsonPath2 = System.Windows.Forms.Application.StartupPath;
            k_PrintNozzleHeadConfigure.FilePath = JsonPath2;

            if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
            {
                File.Create(JsonPath);//不存在则创建文件
            }
            string json = JsonConvert.SerializeObject(k_PrintNozzleHeadConfigure, Formatting.Indented);
            File.WriteAllText(JsonPath, json);

            string msg = $"更新打印校准配置文件：" + $"SaveJsonFile：配置文件路径{{{JsonPath}}}";
            Log4Net.Info(msg);
        }

        public bool LoadCorrectionJsonFile()//20201029新增：实例化类之后，不一定必要显示
        {
            string JsonPath = "";
            try//20201020新增：读取JSON配置文件
            {
                //(4)20200807批注：加载打印策略参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\NozzleConfigInCorrection.json";//json配置文件：启动目录
                k_PrintNozzleHeadConfigure = ObjectCopier.LoadJson<PrintNozzleHeadConfigure>(JsonPath);

                return true;
            }
            catch (Exception)
            {
                if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
                {
                    File.Create(JsonPath);//不存在则创建文件

                    string msg = $"加载打印校准配置文件：文件不存在，已创建：LoadJsonFile";
                    Log4Net.Info(msg);

                    MessageBox.Show("打印校准配置文件不存在，已创建");
                }
                else
                {
                    string msg = $"加载打印校准配置文件：加载异常，已创建：LoadJsonFile";
                    Log4Net.Info(msg);

                    MessageBox.Show("打印校准配置文件加载异常");
                }
                k_PrintNozzleHeadConfigure = new PrintNozzleHeadConfigure();

                return false;
            }
        }


        private void panel5_LocationChanged(object sender, EventArgs e)
        {
            g_SharpControl.RepaintControl(ImportCLIFlag, g_CurrentCLI, new RectangleF(this.renderControl1.Top, this.renderControl1.Left, this.renderControl1.Width, this.renderControl1.Height/*+30*/));
        }


        //0为初始状态，1为进入校准打印过程状态
        int g_nCorrectionTaskThreadFlag = 0;//20210320新建：非常关键，是决定打印时，具体执行哪种(1)数据传输线程内容、(2)打印线程内容的标志
        private void PrintCorrectionFigure_Click(object sender, EventArgs e)
        {
            if (Convert.ToInt32((sender as Button).Tag) == 1)//20201117新增：启动打印按钮的初始Tag为1，标志动作为：开启打印
            {
                g_CorrectionFigureType = 1;//20210324新增：

                (sender as Button).Text = "停止";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配

                g_nCorrectionTaskThreadFlag = 1;
#if false
                g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;//主界面的显示模式切换到校准图显示模式；
#endif
                PrintBtn.Enabled = false; PauseBtn.Enabled = false;//20210320新建：避免和自动打印过程直接的冲突
                TaskAddDeleteTHREAD("CreateCorrection");
                (sender as Button).Tag = 2;
                this.renderControl1.Invalidate();
            }
            else if (Convert.ToInt32((sender as Button).Tag) == 2)//20201117新增：启动打印按钮的初始Tag不为1:，标志动作为：关闭打印
            {
                TaskAddDeleteTHREAD("DeleteMust");
                PrintBtn.Enabled = true; PauseBtn.Enabled = true;//20210320新建：避免和自动打印过程直接的冲突
                //g_nCorrectionTaskThreadFlag = 0;//20210323新建批注：复位校准图打印标志位为0
                //g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;
                (sender as Button).Tag = 1;
                this.renderControl1.Invalidate();

                (sender as Button).Text = "打印";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配
            }
            else { }
        }
        private void PrintCorrectionFigure2_Click(object sender, EventArgs e)
        {
            if (Convert.ToInt32((sender as Button).Tag) == 1)//20201117新增：启动打印按钮的初始Tag为1，标志动作为：开启打印
            {
                if (k_PrintNozzleHeadConfigure.bPrintGoBackFigureAtOnce == false)//分两次打印
                {
                    g_CorrectionFigureType = 2;//20210324新增：
                }
                else
                {
                    g_CorrectionFigureType = 5;//20230408新增：一次性完成校准图的打印过程
                }

                (sender as Button).Text = "停止";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配

                g_nCorrectionTaskThreadFlag = 1;
#if false
                g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;//主界面的显示模式切换到校准图显示模式；
#endif
                PrintBtn.Enabled = false; PauseBtn.Enabled = false;//20210320新建：避免和自动打印过程直接的冲突
                TaskAddDeleteTHREAD("CreateCorrection");
                (sender as Button).Tag = 2;
                this.renderControl1.Invalidate();
            }
            else if (Convert.ToInt32((sender as Button).Tag) == 2)//20201117新增：启动打印按钮的初始Tag不为1:，标志动作为：关闭打印
            {
                TaskAddDeleteTHREAD("DeleteMust");
                PrintBtn.Enabled = true; PauseBtn.Enabled = true;//20210320新建：避免和自动打印过程直接的冲突
                //g_nCorrectionTaskThreadFlag = 0;//20210323新建批注：复位校准图打印标志位为0
                //g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;
                (sender as Button).Tag = 1;
                this.renderControl1.Invalidate();

                (sender as Button).Text = "打印";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配
            }
            else { }
        }
        private void PrintCorrectionFigure3_Click(object sender, EventArgs e)
        {
            if (Convert.ToInt32((sender as Button).Tag) == 1)//20201117新增：启动打印按钮的初始Tag为1，标志动作为：开启打印
            {
                g_CorrectionFigureType = 3;//20210324新增：

                (sender as Button).Text = "停止";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配

                g_nCorrectionTaskThreadFlag = 1;
#if false
                g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;//主界面的显示模式切换到校准图显示模式；
#endif
                PrintBtn.Enabled = false; PauseBtn.Enabled = false;//20210320新建：避免和自动打印过程直接的冲突
                TaskAddDeleteTHREAD("CreateCorrection");
                (sender as Button).Tag = 2;
                this.renderControl1.Invalidate();
            }
            else if (Convert.ToInt32((sender as Button).Tag) == 2)//20201117新增：启动打印按钮的初始Tag不为1:，标志动作为：关闭打印
            {
                TaskAddDeleteTHREAD("DeleteMust");
                PrintBtn.Enabled = true; PauseBtn.Enabled = true;//20210320新建：避免和自动打印过程直接的冲突
                //g_nCorrectionTaskThreadFlag = 0;//20210323新建批注：复位校准图打印标志位为0
                //g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;
                (sender as Button).Tag = 1;
                this.renderControl1.Invalidate();

                (sender as Button).Text = "打印";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配
            }
            else { }
        }
        private void PrintCorrectionFigure4_Click(object sender, EventArgs e)
        {
            if (Convert.ToInt32((sender as Button).Tag) == 1)//20201117新增：启动打印按钮的初始Tag为1，标志动作为：开启打印
            {
                g_CorrectionFigureType = 4;//20210324新增：

                (sender as Button).Text = "停止";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配

                g_nCorrectionTaskThreadFlag = 1;
#if false
                g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;//主界面的显示模式切换到校准图显示模式；
#endif
                PrintBtn.Enabled = false; PauseBtn.Enabled = false;//20210320新建：避免和自动打印过程直接的冲突
                TaskAddDeleteTHREAD("CreateCorrection");
                (sender as Button).Tag = 2;
                this.renderControl1.Invalidate();
            }
            else if (Convert.ToInt32((sender as Button).Tag) == 2)//20201117新增：启动打印按钮的初始Tag不为1:，标志动作为：关闭打印
            {
                TaskAddDeleteTHREAD("DeleteMust");
                PrintBtn.Enabled = true; PauseBtn.Enabled = true;//20210320新建：避免和自动打印过程直接的冲突
                //g_nCorrectionTaskThreadFlag = 0;//20210323新建批注：复位校准图打印标志位为0
                //g_SharpControl.g_CorrectionFigureFlag = g_nCorrectionTaskThreadFlag;
                (sender as Button).Tag = 1;
                this.renderControl1.Invalidate();

                (sender as Button).Text = "打印";//20210323新建：暂时先这么简单处理，没有做到完全的即时刷新，不太匹配
            }
            else { }
        }

        private void textBoxFigurepaht_TextChanged(object sender, EventArgs e)
        {
        }

        private void LayerEnd_TextChanged(object sender, EventArgs e)//20210530新增：
        {
            //g_nLayerEnd = Convert.ToInt32(this.LayerEnd.Text) - 1;//201030批注：系统内部打印区间为从0开始计数第1层
            int layerEnd;
            if (int.TryParse(this.LayerEnd.Text, out layerEnd))
            {
                if (layerEnd < 1)
                {
                    layerEnd = 1;
                }

                g_nLayerEnd = layerEnd - 1;
                g_SharpControl.g_nLayerEnd = g_nLayerEnd;//20210530新增：打印的总层数：打印显示的时候都会即时更新
                g_bSelectedLayerRangeReady = false;
            }
        }

        private void LayerStart_TextChanged(object sender, EventArgs e)
        {
            int layerStart;
            if (int.TryParse(this.LayerStart.Text, out layerStart))
            {
                if (layerStart < 1)
                {
                    layerStart = 1;
                }

                g_nLayerStart = layerStart - 1;
                g_bSelectedLayerRangeReady = false;
            }
        }

        private void SendMessageBtn_Click(object sender, EventArgs e)
        {
            bool tempStartMode = true;
            if (tempStartMode == true)//初始化框体启动
            {
                SendMessageToCamera f = new SendMessageToCamera(tempStartMode);//20200202修改
                //f.nValveStateMask = nValveStateMask;//20200718新增：原因在于，需要在JOB参数设置中打开手动控制，在此过程中，需要为手动控制传递阀状态参数
                //f.k_RYSYSParam.m_bFlagResetCorrect = this.g_bResetCorrectEnabled;
                //f.k_RYSYSParam = (RYSYSParam)g_RYSYSParam.Clone();//20200326新增//20200401新增：避免直接赋值形成的引用，形成真正的复制
                //f.PrintStrategys = ObjectCopier.Clone(g_PrintStrategys);//20200806新增：保存打印策略

                string msg = $"监控记录设置！";
                Log4Net.Info(msg);

                DialogResult result = f.ShowDialog();
                if (result == DialogResult.OK)//OK时，执行对应操作
                {
#if false
                    g_bResetCorrectEnabled = f.k_RYSYSParam.FlagResetCorrect;// this.m_cGoogolMotionMap = f.m_mGoogolMotionMap;//回传数据                                
                    g_RYSYSParam = (RYSYSParam)f.k_RYSYSParam.Clone();//20200326新增：关键：将RYSYSParam的值赋值给全局的static的RYSYSParam

                    //20200326新增:关键：将RYSYSParam的JOB参数信息，进行及时的转发，转+给royal.sysParam
                    //royal.g_sys_param为static类型：
                    royal.royal.g_sys_param.fBrustCycleSec = (float)g_RYSYSParam.m_dInterSpeedSparkCycleTime;//时间1s
                    royal.royal.g_sys_param.fBrustValidSec = (float)g_RYSYSParam.m_dHSpeedSparkTime;//有效时间0.5s
                    royal.royal.g_sys_param.fBrustFrequecy = g_RYSYSParam.m_nHSpeedSparkFreq;//频率500Hz
                    royal.royal.g_sys_param.szLogPath = g_RYSYSParam.m_sLogPath;
                    //royal.royal.g_sys_param.szWavePath = g_RYSYSParam.m_sWavePath;

                    royal.royal.g_prtimg_layer.nImgStartJetIndex = (int)(g_RYSYSParam.m_dYJetOff / (25.4 / g_SharpControl.RenderDpiY) + 1);//20210311新增：Y向起打位置修订

                    //royal.royal.g_prtimg_layer.nYJetOff=(int)(g_RYSYSParam.m_dYJetOff/25.4*600);//20210311新增：Y向起打位置修订

                    //20200327新增:
                    //float m_szMovSpeed = Convert.ToSingle(g_RYSYSParam.CarMoveSpeed);
                    //g_RYSYSParam.CarMoveSpeed = MM_TO_DOT(m_szMovSpeed, 5080);     
                    g_nCarSinglePassLength = (int)((g_RYSYSParam.m_dCarMoveBufferLength + g_RYSYSParam.m_dPrintAeraLength + g_RYSYSParam.m_dCarMoveBufferLength2) * 5080);//SinglePass运动距离：20200327新增：

                    g_PrintStrategys = ObjectCopier.Clone(f.PrintStrategys);//20200806新增：保存打印策略

                    //20210113新增：大零件分区处理算法
                    g_SharpControl.gc_RysysParam = g_RYSYSParam;//20210113新增：大零件分区处理算法

                    f.SaveJsonFile();
#endif
                }
                else if (result == DialogResult.Cancel)//退出时，什么都不做
                {

                }
            }
            else//20230114新增批注：不初始化框体启动//调试用
            {
                //SendMessageToCamera f = new SendMessageToCamera(tempStartMode);//20200202修改
                //f.SendMessageFromSharedMemory(tempStartMode,10,13);
                //f.Dispose();
            }
        }

        private void StopOutBtn_Click(object sender, EventArgs e)//停止和继续输出JOBS
        {
            if (OutputingJOBFlag == false)//停止输出JOBS
            {
                //补充代码：继续输出
                _FinalJobEvent.Set();//开启：开启大门

                //修改按钮状态为：暂停输出
                this.StopOutBtn.Text = "暂停" + "\n" + "输出";
                this.StopOutBtn.TextAlign = ContentAlignment.MiddleRight;
                //this.StopOutBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源//Stop-绿.png");
                this.StopOutBtn.BackgroundImage = Resource.Stop_绿;
                OutputingJOBFlag = true;
            }
            else//继续输出JOBS
            {
                //补充代码：暂停输出
                FirstJOBFlag = false;//FirstJOBFlag只有在主线程中调用过程，所以不存在线程冲突
                _FinalJobEvent.Reset();//关闭：关闭大门

                //修改按钮状态为：继续输出
                this.StopOutBtn.Text = "继续" + "\n" + "输出";
                this.StopOutBtn.TextAlign = ContentAlignment.MiddleRight;
                //this.StopOutBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/Keep.png");
                this.StopOutBtn.BackgroundImage = Resource.Keep;
                OutputingJOBFlag = false;
            }
        }

        //20201020新建：textbox数据绑定
        public PrintNozzleHeadConfigure k_PrintNozzleHeadConfigure = new PrintNozzleHeadConfigure();//20210304新增：喷头校准参数
        public void InitFormWithPrintNozzleHeadConfigureParam()//20201020新建：数据绑定自动固化清洗-自动进给铺粉-自动铺粉续打
        {
            //设置偏移值//mm要转换为像素//X向//20210323新增：
            //设置偏移值//mm要转换为像素//Y向//20210323新增：
            textBox3.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "UniversalOffset", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox5.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "UniversalOffsetY", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);

            // 配置和生成校准图所需参数：20210304新增：     
            textBox7.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "CorrctionFigureWidthBytes", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230408新增:
            textBox6.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "Groups", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox4.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "Splits", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//201029新增:
            textBox2.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "DataLine", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox9.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "OverLapJets", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            UpdownFlag.DataBindings.Add("SelectedIndex", k_PrintNozzleHeadConfigure, "VReverse", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            LeftrightFlag.DataBindings.Add("SelectedIndex", k_PrintNozzleHeadConfigure, "HReverse", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            PositivenegtiveFlag.DataBindings.Add("SelectedIndex", k_PrintNozzleHeadConfigure, "CarRightSide", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox1.DataBindings.Add("SelectedIndex", k_PrintNozzleHeadConfigure, "PrintGoBackFigureAtOnce", true, DataSourceUpdateMode.OnPropertyChanged);//20230408新增：一次性打印往返差图


            textBoxFigurepaht.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "FilePath", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //////生成校准图所在文件路径：public string m_szFilePath = "";  //生成校准图所在文件路径

            // 喷头校准图测量反馈值所需参数//20210325新增;非常重要，需要保存必要的校准偏差值
            //textBoxError1.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "DoubleError", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            //textBoxError2.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "SingleError", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//201029新增:
            //textBoxError3.DataBindings.Add("Text", k_PrintNozzleHeadConfigure, "YError", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
        }
    }

    /// <summary> 
    /// UV灯参数：20200619新增：
    /// </summary>
    public struct UVLightParam//20200619批注：
    {
        //UV灯的周期及有效时间：
        public int m_nFrequency/*m_fCycleSec*/;//UV灯周期
        public float m_fPower/*m_fValidSec*/;//UV灯有效时间
                                             //UV灯开启范围及限位值：
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public Int32[] nMinPos;//灯1的上限、下限
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public Int32[] nMaxPos;//灯2的上限、下限
    }
    public static class WinAPI
    {
        public const int HOR_POSITIVE = 0x1;
        public const int HOR_NEGATIVE = 0x2;
        public const int VER_POSITIVE = 0x3;
        public const int VER_NEGATIVE = 0x8;
        public const int CENTER = 0x10;
        public const int BLEND = 0x80000;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int AnimateWindow(IntPtr hwand, int dwTime, int dwFlag);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetScrollPos(int hwnd, int nBar);//20200806新增：获取鼠标点击的项------API
    }

    /// <summary>
    /// 20210304新增修改：注释掉，从CalibrationMoudle.cs 移植到主界面
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct LPCAL_PARAM//20200723批注：校准图参数结构体
    {
        public int nCorrctionFigureWidthBytes;//打印幅面：X方向
        public int nGroups;        //喷头组数
        public int nSplits;        //喷头组内喷嘴列数
        public int nDataLine;      //喷头单列的喷孔数
        public int nOverLapJets;   //重叠嘴数
        public bool bVReverse;     //上下翻转
        public bool bHReverse;     //左右翻转
        public bool bCarRightSide; //标尺正负反向
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szFilePath;  //生成校准图所在文件路径
    }
    /// <summary>
    /// 20210304新增修改：注释掉，从CalibrationMoudle.cs 移植到主界面
    /// </summary>
    public static class royalCorrectSystem//此接口类，所有的成员方法包括成员变量都是static的：20200326//c#的缺省修饰符是public；//20210327批注：本部分,C#属性 [DllImport("XXXX.dll")],是DLL框架的初始化及调用入口
    {
        public static LPCAL_PARAM g_LPCAL_PARAM;//20200723批注：校准图参数结构体

        ///////校准图生成
        [DllImport("calibration.dll")]//20210327批注：本部分,C#属性 [DllImport("XXXX.dll")],是DLL框架的初始化及调用入口
        public static extern bool PD_UpdatParam(ref LPCAL_PARAM lpParam);      //校准图参数
        [DllImport("calibration.dll")]//20210327批注：本部分,C#属性 [DllImport("XXXX.dll")],是DLL框架的初始化及调用入口
        public static extern bool PD_GenColorOffset(int nDir, int nUniversalOffset); //X套色 综合
        [DllImport("calibration.dll")]//20210327批注：本部分,C#属性 [DllImport("XXXX.dll")],是DLL框架的初始化及调用入口
        public static extern bool PD_GenBiDirOffset(int nUniversalOffset, int nUniversalOffsetY);         //X 往返差
        [DllImport("calibration.dll")]//20210327批注：本部分,C#属性 [DllImport("XXXX.dll")],是DLL框架的初始化及调用入口
        public static extern bool PD_GenBiDirOffsetInAFigure(int nUniversalOffset, int nUniversalOffsetY); 		//X 往返差//20230408新建：一次性打印的往返查图

        [DllImport("calibration.dll")]//20210327批注：本部分,C#属性 [DllImport("XXXX.dll")],是DLL框架的初始化及调用入口
        public static extern bool PD_GenPhStatus(int nUniversalOffset);                //喷嘴状态
        [DllImport("calibration.dll")]//20210327批注：本部分,C#属性 [DllImport("XXXX.dll")],是DLL框架的初始化及调用入口
        public static extern bool PD_GenVertivalCheck(int nUniversalOffset);        //垂直校准
    }

    public class PrintNozzleHeadConfigure : INotifyPropertyChanged, ICloneable//20200806新增：// 喷头配置及校准模块所需参数
    {
        /// <summary>
        /// 返回本类的浅表复本
        /// </summary>
        /// <returns></returns>
        public object Clone()//精华：
        {
            return this.MemberwiseClone();//返回本类的浅表复本//20200401新增：精华
        }
        public event PropertyChangedEventHandler PropertyChanged;//20200224新增：必须定义事件；是接口INotifyPropertyChanged的必须的事件       
                                                                 //表达式树：lambda表达式，为一段可执行代码————SQL数据库查询的时候使用
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")//20200225：【CallerMemberName】特性精华：每次调用 TraceMessage 方法时，调用方信息将替换为可选参数的参数。
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));//20200225：propertyName：是1个附带参数
        }
        /// <summary>
        /// 喷头校准图配置及校准模块所需参数
        /// </summary>
        public int m_nCorrctionFigureWidthBytes = 1004;//校准图宽度字节
        public int m_nGroups = 7;          //喷头组数
        public int m_nSplits = 4;          //喷头组内喷嘴列数
        public int m_nDataLine = 320;      //喷头单列的喷孔数
        public int m_nOverLapJets = 2;     //重叠嘴数
        public bool m_bVReverse = false;     //上下翻转:默认false
        public bool m_bHReverse = false;     //左右翻转:默认false
        public bool m_bCarRightSide = false; //标尺正负反向:默认false
        public bool bPrintGoBackFigureAtOnce = false;//20230408新增：一次性打印往返差图
        public string m_szFilePath = ""/*""*/;  //生成校准图所在文件路径

        public double m_dUniversalOffset = 200;//设置偏移值//mm要转换为像素//X向
        public double m_dUniversalOffsetY = 290;//设置偏移值//mm要转换为像素//Y向

        /// <summary>
        /// 喷头校准图测量反馈值所需参数
        /// </summary>
        /// 
        public int m_nDoubleError = 0;          //1-双向偏差值
        public int m_nSingleError = 0;          //2-单向偏差值
        public int m_nYError = 0;               //3-Y向嘴偏差值

        public FeedbackInCorrection m_RYSYSParamFeedbackInCorrection = new FeedbackInCorrection() /*=new AutoPrintParamInTest()*/;//20230412新增：


        ///// <summary>
        ///// 喷头校准图的校准状态
        ///// </summary>
        ///// 
        //public bool m_bConfigureCorrectFigure = false;          //1-配置校准图生成模块
        //public bool m_bVerticalCorrectFigure = false;          //2-垂直校准图导入模块
        //public bool m_bCorrectFigure1 = false;               //3-Y向嘴偏差值
        //public bool m_bCorrectFigure2 = false;               //3-Y向嘴偏差值
        //public bool m_bNozzleStateFigure = false;               //3-Y向嘴偏差值


        public double UniversalOffset//设置偏移值//mm要转换为像素//X向
        {
            get { return this.m_dUniversalOffset; }
            set { if (value != this.m_dUniversalOffset) { this.m_dUniversalOffset = value; NotifyPropertyChanged(); } }
        }

        public double UniversalOffsetY//设置偏移值//mm要转换为像素//Y向
        {
            get { return this.m_dUniversalOffsetY; }
            set { if (value != this.m_dUniversalOffsetY) { this.m_dUniversalOffsetY = value; NotifyPropertyChanged(); } }
        }

        public int CorrctionFigureWidthBytes//喷头组数
        {
            get { return this.m_nCorrctionFigureWidthBytes; }
            set { if (value != this.m_nCorrctionFigureWidthBytes) { this.m_nCorrctionFigureWidthBytes = value; NotifyPropertyChanged(); } }
        }

        public int Groups//喷头组数
        {
            get { return this.m_nGroups; }
            set { if (value != this.m_nGroups) { this.m_nGroups = value; NotifyPropertyChanged(); } }
        }
        public int Splits//喷头组内喷嘴列数
        {
            get { return this.m_nSplits; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nSplits) { this.m_nSplits = value; NotifyPropertyChanged(); } }
        }
        public int DataLine//喷头单列的喷孔数
        {
            get { return this.m_nDataLine; }
            set { if (value != this.m_nDataLine) { this.m_nDataLine = value; NotifyPropertyChanged(); } }
        }
        public int OverLapJets//重叠嘴数
        {
            get { return this.m_nOverLapJets; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nOverLapJets) { this.m_nOverLapJets = value; NotifyPropertyChanged(); } }
        }
        public bool VReverse//上下翻转
        {
            get { return this.m_bVReverse; }
            set { if (value != this.m_bVReverse) { this.m_bVReverse = value; NotifyPropertyChanged(); } }
        }
        public bool HReverse//左右翻转
        {
            get { return this.m_bHReverse; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bHReverse) { this.m_bHReverse = value; NotifyPropertyChanged(); } }
        }
        public bool CarRightSide//标尺正负反向
        {
            get { return this.m_bCarRightSide; }
            set { if (value != this.m_bCarRightSide) { this.m_bCarRightSide = value; NotifyPropertyChanged(); } }
        }
        public bool PrintGoBackFigureAtOnce//标尺正负反向
        {
            get { return this.bPrintGoBackFigureAtOnce; }
            set { if (value != this.bPrintGoBackFigureAtOnce) { this.bPrintGoBackFigureAtOnce = value; NotifyPropertyChanged(); } }
        }
        public string FilePath//生成校准图所在文件路径
        {
            get { return this.m_szFilePath; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_szFilePath) { this.m_szFilePath = value; NotifyPropertyChanged(); } }
        }

        public int DoubleError//1-双向偏差值
        {
            get { return this.m_nDoubleError; }
            set { if (value != this.m_nDoubleError) { this.m_nDoubleError = value; NotifyPropertyChanged(); } }
        }
        public int SingleError//2-单向偏差值
        {
            get { return this.m_nSingleError; }
            set { if (value != this.m_nSingleError) { this.m_nSingleError = value; NotifyPropertyChanged(); } }
        }
        public int YError//3-Y向嘴偏差值
        {
            get { return this.m_nYError; }
            set { if (value != this.m_nYError) { this.m_nYError = value; NotifyPropertyChanged(); } }
        }
    }
}
