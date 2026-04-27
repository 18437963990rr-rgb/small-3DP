#define TwoPassPrintMode
//#define SinglePassPrintMode
//#define DataProcessDebugMode
//#define UseP5PortForCleaning
#define UseP11PortForCleaning
//#define UseDirectPushInkMode
#define OpenMagnetWhenUse//20230508新增：

using Newtonsoft.Json;
using Motion;//导入GoogolMotionMap引用包
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LaserADD_BinderJetter;
using System.Windows.Forms;
using System.ComponentModel;
using System.Runtime.CompilerServices;
//using Modbus;//202205074新增：建立仪表通讯
//using System.Threading.Tasks;//202205074新增：建立仪表通讯
using System.Net.Sockets;//202205074新增：建立仪表通讯
using System.Net;
using Modbus.Device;
using System.IO.Ports;
using System.Windows.Interop;

namespace BinderJetting
{
    public partial class 手动操作 : Form
    {
        // 简易测试运动旁路开关（由主界面读取）。默认关闭，不改变正常打印流程。
        public static bool UseSimpleTestMotion = false;

        // 手动界面的 Meteor 调试按钮会直接用到渲染接口，这里提供独立实例以避免引用主界面私有成员。
        private readonly SharpControl g_SharpControl = new SharpControl();

        // 定义常量光栅尺分辨率为每毫米1000线，每英寸25400线（Royal/旧光栅用）
        const int EncoderLinePerMM = 1000;
        const int EncoderLinePerInch =  EncoderLinePerMM * 254 / 10;
        const int DriverPulsePerMM = 1000;              // 伺服驱动器每毫米发送脉冲数（它轴沿用）
        const int XMaxDistanceMM = 860;                 // X轴正负行程开关之间距离
        const int YMaxDistanceMM = 470;                 // Y轴正负行程开关之间距离

        private const bool DisableRoyalStartupPolling = true;

        // 墨车的一些常用位置
        const double INKCAR_SOA_MIN_X = 10.0;           // 墨车安全区右上角坐标，（X，Y）
        const double INKCAR_SOA_MIN_Y = 40.0;

        const double INKCAR_SOA_MAX_X = 660.0;          // 墨车安全区左下角坐标，（X，Y)
        const double INKCAR_SOA_MAX_Y = 335.0;

        const double INKCAR_DEFAULT_X = 680.0;          // 墨车开始打印前默认位置
        const double INKCAR_DEFAULT_Y = 116.0;

        /*
         * 刮墨位置坐标: (850, 10)
         * 压墨位置坐标: (750,10)
         */
        const double INKCAR_CLEAN_STATION_X = 750.0;    // 墨车清洗站位置坐标，压墨位置
        const double INKCAR_CLEAN_STATION_Y =  10.0;
        const double INKCAR_CLEAN_SCRAPE_POS_REL = 105.0;    // 墨车清洗站刮墨位置, mm

        /// <summary>
        /// Y 轴开始运动时触发（在 BackToStation 的 Y 分支内、运动已启动后发出）。
        /// 可用于 Meteor 扫描模式下在每条 swath 结束后发 PCMD_ENDDOC 等逻辑。
        /// </summary>
        public static event Action YAxisMoveStarted;

        // 铺粉车的一些常数
        const double POWDERCAR_TRAVEL_DIST = /*918.0*/900;     // 铺粉车行程距离，mm //20251206修改，硬件更换
        const double POWDERCAR_DROP_BEGIN  = /*400.0*/325;     // 铺粉车开始落粉位置，mm //20251206修改，硬件更换
        const double POWDERCAR_DROP_END    = 825.0;     // 铺粉车停止落粉位置，mm

        int m_PowerBackBtnFlag = 0;//默认状态为0；20200411批注：
        public 手动操作(int PowerBackBtnFlag, UInt32 nValveStateMask, bool PrintJobExistedFlag)//20200718修改：
        {
            Log4Net.Info("AutoPrintMotion constructor: start.");
            m_PowerBackBtnFlag = PowerBackBtnFlag;//20200327新增:
            InitializeComponent();
            Log4Net.Info("AutoPrintMotion constructor: InitializeComponent done.");
            if (PrintJobExistedFlag == false) //不存在打印任务
            { }
            else
            {
                textBox10.Enabled = false;//20230419修改：打印时不得修改打印次数
                PowderCarHomeBtn.Enabled = false;//20230419修改：
                PrintCarHomeBtn.Enabled = false;//20230419修改：
            }
            InitShoveInk(nValveStateMask);//初始化挤墨控件集体控制————应该移到主界面中去：

#if false //20230317新建：开启定时器进行刷新，此处存在潜在BUG，需要进行修正
            //刷新（1）墨量余量（2）温度、电压、气压状态定时器
            Timer3 = new System.Windows.Forms.Timer() { Interval = 300 };
            Timer3.Tick += new EventHandler(Timer3_Tick);
            if (!DisableRoyalStartupPolling)
            {
                Timer3.Start();
            }

            StartUpadateMAixsMoveStatus();//开启6轴轴MOVE限位信号：20200110
#endif
            InitDynamicConfigureMotionMode();// 互斥配置多轴的点动和JOG运动配置：动态挂载初始化：20200110

            k_EnvironmentParam = new EnvironmentParam();//20200402新增：
        }

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

        public/*private*/ System.Windows.Forms.Timer Timer = null;
        // 主界面在简易测试模式下会调用此入口旁路自动运动流程。
        public void RunSimpleTestMotionRuntimeStep(int passIndex)
        {
            Log4Net.Info($"RunSimpleTestMotionRuntimeStep invoked, passIndex={passIndex}");
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.Opacity >= 1)
            {
                Timer.Stop();
                //Timer.Dispose();
            }
            else
            {
                base.Opacity += 0.5;//延迟4帧完成显示
            }
        }
        //Bitmap inkState = new Bitmap(500,500);
        private void 手动操作_Load(object sender, EventArgs e)//窗口加载完成前，完成本部分的工作
        {
#if true //20230317新建：开启定时器进行刷新，此处存在潜在BUG，需要进行修正//20230317重新实现在此处
            //刷新（1）墨量余量（2）温度、电压、气压状态定时器
            Timer3 = new System.Windows.Forms.Timer() { Interval = 300 };
            Timer3.Tick += new EventHandler(Timer3_Tick);
            if (!DisableRoyalStartupPolling)
            {
                Timer3.Start();
            }

            if (!DisableRoyalStartupPolling)
            {
                StartUpadateMAixsMoveStatus();
            }
            else
            {
                Log4Net.Info("Startup royal polling disabled: skip move-status refresh.");
            }
#endif

            if (!DisableRoyalStartupPolling)
            {
                StartCloseMoveBtn(true);//是否开启双Y轴运动监控:20200514 new created
            }

            if (m_PowerBackBtnFlag == 1)//执行手动铺粉系统回零操作，进入对应的模块
            {
                ManulDebugTAB.TabPages.Remove(tabPage2);//20200327新增：铺粉系统回零时删除以禁用tabPage2
                ManulDebugTAB.Width = 965; ManulDebugTAB.Height = 569;//20200327新增：
            }
            else if (m_PowerBackBtnFlag == 2)//执行环境参数修改工作，进入对应的模块
            {
                ManulDebugTAB.TabPages.Remove(tabPage1);//20200327新增：铺粉系统回零时删除以禁用tabPage2
                ManulDebugTAB.Width = 965; ManulDebugTAB.Height = 569;//20200327新增：

                InitDataGrid1(1);//初始化datagridview//初始化棚舍固化过程的手动控制//20200327新增：
                //ADIB状态   nOption定义： bit[0] 版本 bit[1] 温度 bit[2] 负压 bit[3] 输入 bit[4] 电压 bit[8] 回读I2C负压 bit[16] 保持到I2C 
                InitDataGrid2(0);//初始化喷墨系统环境控制：PPCB的ADIB参数设置（最重要的1个逻辑实现）：20200329新增

                ADIBSetControlInterferenceMaintain(false);//监控状态时:20200403新增
                InitKidFormWithSysparam();//(1)20200401新增:完成数据绑定
                //OpenVTMonitorThread();//(2)20200401新增:开启温度电压气压监控线程

                //adibCurState = new royal.LPADIB_PARAM();//20200401新增
                adibCurState.fcurAirPress = new float[8];
                adibCurState.fcurInkTankTemp = new float[8];
                adibCurState.fcurvoltage = new float[8];
#if false//注释掉
                //20200627批注：UV灯参数设置
                this.CycleBox.Text = (k_UVLightParam.m_nFrequency).ToString();
                this.ValidBox.Text = (k_UVLightParam.m_fPower).ToString();
                this.UV1LimitUp.Text = (k_UVLightParam.nMinPos[0]).ToString();
                this.UV2LimitUp.Text = (k_UVLightParam.nMinPos[1]).ToString();
                this.UV1LimitDown.Text = (k_UVLightParam.nMaxPos[0]).ToString();
                this.UV2LimitDown.Text = (k_UVLightParam.nMaxPos[1]).ToString();
#endif
            }
            else//默认为0的状态
            {
                InitDataGrid1(1);//初始化datagridview//初始化棚舍固化过程的手动控制//20200327新增：
                //ADIB状态   nOption定义： bit[0] 版本 bit[1] 温度 bit[2] 负压 bit[3] 输入 bit[4] 电压 bit[8] 回读I2C负压 bit[16] 保持到I2C 
                InitDataGrid2(0);//初始化喷墨系统环境控制：PPCB的ADIB参数设置（最重要的1个逻辑实现）：20200329新增

                ADIBSetControlInterferenceMaintain(false);//监控状态时:20200403新增
                InitKidFormWithSysparam();//(1)20200401新增:完成数据绑定
                //OpenVTMonitorThread();//(2)20200401新增:开启温度电压气压监控线程

                //adibCurState = new royal.LPADIB_PARAM();//20200401新增
                adibCurState.fcurAirPress = new float[8];
                adibCurState.fcurInkTankTemp = new float[8];
                adibCurState.fcurvoltage = new float[8];

#if false//注释掉
                this.CycleBox.Text = (k_UVLightParam.m_nFrequency).ToString();
                this.ValidBox.Text = (k_UVLightParam.m_fPower).ToString();
                this.UV1LimitUp.Text = (k_UVLightParam.nMinPos[0]).ToString();
                this.UV2LimitUp.Text = (k_UVLightParam.nMinPos[1]).ToString();
                this.UV1LimitDown.Text = (k_UVLightParam.nMaxPos[0]).ToString();
                this.UV2LimitDown.Text = (k_UVLightParam.nMaxPos[1]).ToString();
#endif
            }

            this.JourneyBox1.Text = (k_dJourney[0] / 1000).ToString("F2");//20200220:参数设置
            this.JourneyBox2.Text = (k_dJourney[1] / 1000).ToString("F2");//20200220:参数设置
            this.JourneyBox3.Text = (k_dJourney[2] / 1000).ToString("F2");//20200220:参数设置
            this.JourneyBox4.Text = (k_dJourney[3] / 1000).ToString("F2");//20200220:参数设置
            this.JourneyBox5.Text = (k_dJourney[4] / 1000).ToString("F2");//20200220:参数设置
            this.JourneyBox6.Text = (k_dJourney[5] / 1000).ToString("F2");//20200220:参数设置

            //消除加载时黑框显示的临时定时器：20200527批注：本部分代码非常关键
            Timer = new System.Windows.Forms.Timer() { Interval = 100 };
            Timer.Tick += new EventHandler(Timer_Tick);
            base.Opacity = 0;
            Timer.Start();

            _timerIrProbeTemp = new System.Windows.Forms.Timer { Interval = 300 };
            _timerIrProbeTemp.Tick += TimerIrProbeTemp_Tick;
            _timerIrProbeTemp.Start();

            if (RollerDirectionFlag == true)
            {
                this.RollerFlag.SelectedIndex = 0;//正转
            }
            else
            {
                this.RollerFlag.SelectedIndex = 1;//反转
            }

            if (CorrectFlag/*SystemCorrectFlag*/== true)
            {
                EncoderResetBtn.Text = "已校准";
            }
            else
            {
                EncoderResetBtn.Text = "未校准";
            }
            if (PowderCarHomeFlag/*SystemCorrectFlag*/== true)
            {
                PowderHomeBtn.BackColor = Color.Lime;
                //InkCarHomeBtn.BackColor = Color.Lime;
                /*PowderHomeBtn.Text = "铺粉车已校准";*/
                PowderCarHomeBtn.Text = "已校准铺车";//20230331新增：
            }
            else
            {
                PowderHomeBtn.BackColor = Color.Tomato;
                //InkCarHomeBtn.BackColor = Color.Tomato;
                /*PowderHomeBtn.Text = "铺粉未校准";*/
                PowderCarHomeBtn.Text = "校准铺车";//20230331新增：
            }
            if (InkCarHomeFlag/*SystemCorrectFlag*/== true)//20230331新增：
            {
                //PowderHomeBtn.BackColor = Color.Lime;
                InkCarHomeBtn.BackColor = Color.Lime;
                /*PowderHomeBtn.Text = "铺粉车已校准";*/
                PrintCarHomeBtn.Text = "已校准墨车";//20230331新增：
            }
            else
            {
                //PowderHomeBtn.BackColor = Color.Tomato;
                InkCarHomeBtn.BackColor = Color.Tomato;
                /*PowderHomeBtn.Text = "铺粉未校准";*/
                PrintCarHomeBtn.Text = "校准墨车";//20230331新增：
            }

            if (m_bInkSuppy == false)
            {
                DEV_EnableInkAutoSupply.BackColor = Color.White;
                DEV_EnableInkAutoSupply.ForeColor = Color.Black/*White*/;
            }
            else
            {
                DEV_EnableInkAutoSupply.BackColor = Color.LimeGreen;
                DEV_EnableInkAutoSupply.ForeColor = Color.Black/*White*/;
            }

            string JsonPath = "";
            try//20201020新增：读取JSON配置文件
            {
                //(4)20200807批注：加载打印策略参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\AutoPrint-Configuration.json";//json配置文件：启动目录
                k_RYSYSParamAutoPrintParamInTest = ObjectCopier.LoadJson<AutoPrintParamInTest>(JsonPath);

                InitKidFormWithAutoPrintParamInTest();//20201020新增：开启ParamInText自动打印参数的绑定

                int index = UVCureEnergycomboBox.FindString((k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity).ToString());//20210621新增:
                UVCureEnergycomboBox.SelectedIndex = index;//20210621新增:

            }
            catch (Exception)
            {
                if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
                {
                    File.Create(JsonPath);//不存在则创建文
                    MessageBox.Show("自动打印配置文件不存在，已创建");
                }
                else
                {
                    MessageBox.Show("自动打印参数配置文件加载异常");
                }
                k_RYSYSParamAutoPrintParamInTest = new AutoPrintParamInTest();
                InitKidFormWithAutoPrintParamInTest();//20201020新增：开启ParamInText自动打印参数的绑定

                int index = UVCureEnergycomboBox.FindString((k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity).ToString());//20210621新增:
                UVCureEnergycomboBox.SelectedIndex = index;//20210621新增:
            }

            this.label64.Text = "当前打印： " + k_nCurrentLayer + "层";//20201022新增：显示打印状态

        }

        public bool LoadJsonFile(bool FromFormFlag)//20201029新增：实例化类之后，不一定必要显示
        {
            Log4Net.Info($"AutoPrintMotion.LoadJsonFile start, FromFormFlag={FromFormFlag}");
            string JsonPath = "";
            try//20201020新增：读取JSON配置文件
            {
                //(4)20200807批注：加载打印策略参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\AutoPrint-Configuration.json";//json配置文件：启动目录
                k_RYSYSParamAutoPrintParamInTest = ObjectCopier.LoadJson<AutoPrintParamInTest>(JsonPath);

                if (FromFormFlag == true)
                {
                    InitKidFormWithAutoPrintParamInTest();//20201020新增：开启ParamInText自动打印参数的绑定   
                }
                Log4Net.Info("AutoPrintMotion.LoadJsonFile success.");
                return true;
            }
            catch (Exception)
            {
                if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
                {
                    File.Create(JsonPath);//不存在则创建文件
                    MessageBox.Show("自动打印配置文件不存在，已创建");

                }
                else
                {
                    MessageBox.Show("自动打印参数配置文件加载异常");
                }
                k_RYSYSParamAutoPrintParamInTest = new AutoPrintParamInTest();
                if (FromFormFlag == true)
                {
                    InitKidFormWithAutoPrintParamInTest();//20201020新增：开启ParamInText自动打印参数的绑定
                }
                return false;
            }
        }

        public void SaveJsonFile()
        {
            string JsonPath = System.Windows.Forms.Application.StartupPath + @"\AutoPrint-Configuration.json";//json配置文件：启动目录
            if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
            {
                File.Create(JsonPath);//不存在则创建文件
            }
            string json = JsonConvert.SerializeObject(k_RYSYSParamAutoPrintParamInTest, Formatting.Indented);
            File.WriteAllText(JsonPath, json);
        }

        //protected override void OnKeyUp(KeyEventArgs e)
        //{
        //    if (e.KeyCode == Keys.Up)
        //    {
        //        MessageBox.Show("Escape was pressed");
        //        e.Handled = true;
        //    }
        //    base.OnKeyUp(e);
        //}
        //protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        //{
        //    // look for the expected key
        //    if (keyData == Keys.A)
        //    {
        //        // take some action
        //        MessageBox.Show("The A key was pressed");
        //        // eat the message to prevent it from being passed on
        //        return true /*true*/;
        //        // (alternatively, return FALSE to allow the key event to be passed on)
        //    }
        //    // call the base class to handle other key events
        //    return base.ProcessCmdKey(ref msg, keyData);
        //}

        //private void MainForm_KeyUp(object sender, KeyEventArgs e)
        //{
        //    //Log("MainForm_KeyUp");
        //    if (e.KeyCode == Keys.A)
        //    {
        //        //RefreshStuff();
        //        MessageBox.Show("The A key was pressed");
        //    }
        //}
        DataTable dataTable = new DataTable();//绑定datagridview1的dataTable
        DataTable dataTable2 = new DataTable();//绑定datagridview2的dataTable//20200331新增：
        BindingSource bingdingSource1 = new BindingSource();
        BindingSource bingdingSource2 = new BindingSource();
        bool FlagFirstBindTable1 = true;//第一次绑定dataTable到datagridview1：20200403新增：
        private void InitDataGrid1(int BindType)
        {
            if (BindType == 0)//不绑定dataTABLE初始化
            {
                //Timer3.Start();
                m_FlagUpdateAdibInfo = true;
                dataGridView1.EndEdit(); bingdingSource1.EndEdit();//非常关键：20200403新增：

                //(3)dataGridView2控件尺寸自定义：
                dataGridView1.DataSource = null;
                dataGridView1.Refresh();
                //bingdingSource2.DataSource = null;
                dataGridView1.ColumnCount = 5; dataGridView1.RowCount = 8;
                //for (int i = 0; i < 6; i++)
                //{
                //    dataGridView1.Columns[i].HeaderText = "通道" + (i + 1);
                //}
                dataGridView1.Columns[0].HeaderText = "通道电压1/V:";
                dataGridView1.Columns[1].HeaderText = "通道电压2/V:";
                dataGridView1.Columns[2].HeaderText = "通道电压3/V:";
                dataGridView1.Columns[3].HeaderText = "通道电压4/V:";
                dataGridView1.Columns[4].HeaderText = "通道温度1:";
            }
            else if (BindType == 1)//绑定dataTABLE初始化
            {
                if (FlagFirstBindTable1 == true)//首次绑定初始化
                {
                    //////Timer3.Stop();
                    ////m_FlagUpdateAdibInfo = false;//定时器标志位
                    //(0）dataGridView1重新绑定数据dataTable：
                    dataGridView1.EndEdit(); bingdingSource1.EndEdit();//非常关键：20200403新增：
                    dataGridView1.ColumnCount = 0;//这行代码非常关键：20200403新增

                    //(1)dataTable数据写入：
                    dataTable.Columns.Add("通道电压1/V:", typeof(String));
                    dataTable.Columns.Add("通道电压2/V:", typeof(String));
                    dataTable.Columns.Add("通道电压3/V:", typeof(String));
                    dataTable.Columns.Add("通道电压4/V:", typeof(String));
                    dataTable.Columns.Add("通道温度1:", typeof(String));
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);//减少喷头挥发性，温度设置为25摄氏度，依据：广州此地温度范围为30摄氏度
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);
                    dataTable.Rows.Add("18.00", "18.00", "18.00", "18.00", "25.00"/*, "103"*/);

                    //(2）dataGridView1绑定数据dataTable：
                    //dataGridView1.DataSource = /*bs*/dataTable;
                    bingdingSource1.DataSource = dataTable;
                    dataGridView1.DataSource = bingdingSource1;
                    FlagFirstBindTable1 = false;//标志位变换：不是第一次绑定
                }
                else//后续的绑定初始化
                {
                    ////Timer3.Stop();
                    //m_FlagUpdateAdibInfo = false;//定时器标志位
                    dataGridView1.EndEdit(); bingdingSource1.EndEdit();//非常关键：20200403新增：

                    //(2）dataGridView2重新绑定数据dataTable：
                    dataGridView1.ColumnCount = 0;//这行代码非常关键：20200403新增
                    dataGridView1.AutoGenerateColumns = true;//这行代码非常关键：20200403新增
                    dataGridView1.DataSource = bingdingSource1;
                    dataGridView1.Refresh();//dataGridView2.Update();
                }
            }

            //(3)dataGridView1控件尺寸自定义：
            dataGridView1.TopLeftHeaderCell.Value = "喷头序号";
            foreach (DataGridViewColumn column in dataGridView1.Columns)//20200331新增：取消列排列模式
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            // dataGridView1.Rows[1].HeaderCell.Value = String.Format("{0}", 1 + 1);/*"HHHH"*/ ;/*.Value = j.ToString();*/
            dataGridView1.RowHeadersWidth = 60;//行头宽度：85
            dataGridView1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;//所有的单元文字中心
            dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;//所有的列头中心
            for (int i = 0; i < dataGridView1.Columns.Count; i++)
            {
                int j = i + 1;
                dataGridView1.Columns[i].Width = 55;
            }
            //(4)dataGridView1控件颜色自定义：不同列设置不同的颜色（颜色设置）：20200331新增
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGreen;//20200331新增:
            dataGridView1.RowHeadersDefaultCellStyle.BackColor = Color.LightYellow;//20200331新增:
            dataGridView1.Columns[0].DefaultCellStyle.BackColor = Color.Orange;//20200331新增:不同列设置不同的颜色
            dataGridView1.Columns[1].DefaultCellStyle.BackColor = Color.LightBlue;//20200331新增:不同列设置不同的颜色
            dataGridView1.Columns[2].DefaultCellStyle.BackColor = Color.LightSalmon/*Color.PaleVioletRed*/;//20200331新增，不同列设置不同的颜色
            dataGridView1.Columns[3].DefaultCellStyle.BackColor = Color.LightPink/*Color.LightSeaGreen*//*Color.LightYellow*//*Color.LightBlue*//*Color.PaleVioletRed*/;//20200331新增，不同列设置不同的颜色

        }
        bool FlagFirstBindTable2 = true;//第一次绑定dataTable到datagridview2：20200403新增：
        private void InitDataGrid2(int BindType)//20200331新增：
        {
            if (BindType == 0)//不绑定dataTABLE初始化
            {
                //Timer3.Start();
                m_FlagUpdateAdibInfo = true;

                dataGridView2.EndEdit(); bingdingSource2.EndEdit();//非常关键：20200403新增：

                //(3)dataGridView2控件尺寸自定义：
                dataGridView2.DataSource = null;
                dataGridView2.Refresh();
                //bingdingSource2.DataSource = null;
                dataGridView2.ColumnCount = 6; dataGridView2.RowCount = 3;
                for (int i = 0; i < 6; i++)
                {
                    dataGridView2.Columns[i].HeaderText = "通道" + (i + 1);
                }
            }
            else if (BindType == 1)//绑定dataTABLE初始化
            {
                if (FlagFirstBindTable2 == true)//首次绑定初始化
                {
                    //Timer3.Stop();
                    m_FlagUpdateAdibInfo = false;//定时器标志位

                    //(0）dataGridView2重新绑定数据dataTable：
                    dataGridView2.EndEdit(); bingdingSource2.EndEdit();//非常关键：20200403新增：
                    dataGridView2.ColumnCount = 0;//这行代码非常关键：20200403新增

                    //(1)dataTable数据写入：
                    dataTable2.Columns.Add("通道1", typeof(String));
                    dataTable2.Columns.Add("通道2", typeof(String));
                    dataTable2.Columns.Add("通道3", typeof(String));
                    dataTable2.Columns.Add("通道5", typeof(String));
                    dataTable2.Columns.Add("通道6", typeof(String));
                    //dataTable2.Columns.Add("7", typeof(String));
                    //dataTable2.Columns.Add("8", typeof(String));
                    dataTable2.Rows.Add("12.00", "12.00", "12.00", "12.00", "12.00", "12.00"/*, "0.00", "0.00"*/);//本项目实际只用到4组：20200331批注：控制器的操作电压24V，取折中输出电压为12.00V
                    dataTable2.Rows.Add("25.00", "25.00", "25.00", "25.00", "25.00", "25.00"/*, "0.00", "0.00"*/);//本项目实际只用到4组：20200331批注
                    dataTable2.Rows.Add("-2.40", "-2.40", "-2.40", "-2.40", "-2.40", "-240"/*, "0.00", "0.00"*/);//本项目实际只用到4组：20200331批注

                    dataTable2.Columns.Add("通道4", typeof(String));
                    //(2）dataGridView2绑定数据dataTable：
                    bingdingSource2.DataSource = dataTable2;
                    dataGridView2.DataSource = bingdingSource2;

                    FlagFirstBindTable2 = false;//标志位变换：不是第一次绑定
                }
                else//后续的绑定初始化
                {
                    //Timer3.Stop();
                    m_FlagUpdateAdibInfo = false;//定时器标志位

                    dataGridView2.EndEdit(); bingdingSource2.EndEdit();//非常关键：20200403新增：

                    //(2）dataGridView2重新绑定数据dataTable：
                    dataGridView2.ColumnCount = 0;//这行代码非常关键：20200403新增
                    dataGridView2.AutoGenerateColumns = true;//这行代码非常关键：20200403新增
                    dataGridView2.DataSource = bingdingSource2;
                    dataGridView2.Refresh();//dataGridView2.Update();
                }
            }

            //(3)dataGridView2控件尺寸自定义：
            dataGridView2.TopLeftHeaderCell.Value = "参数列表";
            foreach (DataGridViewColumn column in dataGridView2.Columns)//20200331新增：取消列排列模式
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;

            }
            //dataGridView2.Rows[1].HeaderCell.Value = String.Format("{0}", 1 + 1);
            dataGridView2.RowHeadersWidth = 85;//行头宽度：85
            dataGridView2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;//所有的单元文字中心
            dataGridView2.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;//所有的列头中心
            for (int i = 0; i < dataGridView2.Columns.Count; i++)
            {
                int j = i + 1;
                dataGridView2.Columns[i].Width = 55;
            }
            //(4)dataGridView2控件颜色自定义：不同列设置不同的颜色（颜色设置）：20200331新增
            dataGridView2.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGreen;//20200331新增:
            dataGridView2.RowHeadersDefaultCellStyle.BackColor = Color.LightYellow;//20200331新增:
            dataGridView2.Columns[0].DefaultCellStyle.BackColor = Color.Orange;//20200331新增:不同列设置不同的颜色
            dataGridView2.Columns[1].DefaultCellStyle.BackColor = Color.LightBlue;//20200331新增:不同列设置不同的颜色
            dataGridView2.Columns[2].DefaultCellStyle.BackColor = Color.LightSalmon/*Color.PaleVioletRed*/;//20200331新增，不同列设置不同的颜色
            dataGridView2.Columns[3].DefaultCellStyle.BackColor = Color.LightPink/*Color.LightSeaGreen*//*Color.LightYellow*//*Color.LightBlue*//*Color.PaleVioletRed*/;//20200331新增，不同列设置不同的颜色

            ////（0）dataGridView2停止交互：不选中，不交互
            //dataGridView2.Enabled = false;//20200401：停止对用户交互做出反应
            //dataGridView2.ReadOnly = true;
            //dataGridView2.ClearSelection();//20200401：清楚所有的编辑状态：清除之前的选中状态

        }
        ///// <summary> 
        ///// 初始化喷射系统的两张表
        ///// </summary>
        //private void InitListViewCtrl()
        //{
        //    //更新表1
        //    this.listView1.View = View.Details;
        //    this.listView1.GridLines = true;
        //    this.listView1.FullRowSelect = true;
        //    this.listView1.MultiSelect = true;

        //    listView1.Columns.Add(" PH", 75, HorizontalAlignment.Center);        //第1列标题添加 
        //    listView1.Columns.Add(" MCU", 75, HorizontalAlignment.Center);        //第1列标题添加
        //    listView1.Columns.Add(" T1(C)", 75, HorizontalAlignment.Center);        //第1列标题添加

        //    ////更新表2
        //    //this.listView2.View = View.Details;
        //    //this.listView2.GridLines = true;
        //    //this.listView2.FullRowSelect = true;
        //    //this.listView2.MultiSelect = true;
        //    //listView2.Columns.Add(" PH", 75, HorizontalAlignment.Center);        //第1列标题添加 
        //    //listView2.Columns.Add(" MCU", 75, HorizontalAlignment.Center);        //第1列标题添加
        //    //listView2.Columns.Add(" T1(C)", 75, HorizontalAlignment.Center);        //第1列标题添加
        //    //listView2.Columns.Add(" T2(C)", 75, HorizontalAlignment.Center);        //第1列标题添加
        //    //listView2.Columns.Add(" T3(C)", 75, HorizontalAlignment.Center);        //第1列标题添加
        //    //listView2.Columns.Add(" T4(C)", 75, HorizontalAlignment.Center);        //第1列标题添加
        //}

        //private System.Windows.Forms.Timer Timer2 = null;//刷新墨量显示状态定时器
        //////（1）刷新液位报警信号
        //InkStateCtrl inkStateCtrl = new InkStateCtrl();
        //Bitmap inkState = new Bitmap(500, 100);
        //Rectangle rectangle=new Rectangle();

        //////（2）刷新轴使能报警信号//20200327注释掉
        //InkStateCtrl inkStateCtrl2 = new InkStateCtrl();
        //Bitmap inkState2 = new Bitmap(500, 100);
        //Rectangle rectangle2 = new Rectangle();
        //private void Timer2_Tick(object sender, EventArgs e)//刷新墨量显示状态定时器
        //{
        //    ////////（2）刷新轴使能报警信号
        //    ////inkStateCtrl2.SetInkCount(8, 0);
        //    ////inkStateCtrl2.SetInkState(0b00001111);//很关键
        //    ////inkState2.SetPixel(pictureBox1.Width, pictureBox1.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
        //    ////Graphics g2 = Graphics.FromImage(inkState2);
        //    ////g2.Clear(pictureBox1.BackColor);
        //    ////rectangle2.Width = pictureBox1.Width; rectangle2.Height = pictureBox1.Height;//rectanle是bitmap的大小
        //    ////inkStateCtrl2.OnPaint(g2, rectangle2);
        //    ////pictureBox1.CreateGraphics().DrawImage(inkState2, new Point(0, 0));
        //    ////pictureBox1.Image = inkState2;
        //}

        /***********************************************成型缸********************************************************/
        /***********************************************成型缸********************************************************/
        /***********************************************成型缸********************************************************/
        //(1)运动模式动态挂载切换响应：
        private GoogolMotionMap _motionMap;
        private GoogolMotionMap motionMap => _motionMap ?? (_motionMap = CreateMotionMap());//创建GoogolMotionMap对象，供本窗口调用

        private GoogolMotionMap CreateMotionMap()
        {
            var map = new GoogolMotionMap();
            map.SetLogSink(msg => Log4Net.Info(msg), msg => Log4Net.Error(msg));
            return map;
        }

        /// <summary>红外探头接入固高 AI 的通道下标（<see cref="GoogolMotionMap.GetAi"/> 返回数组索引，0 表示第 1 路）。</summary>
        private const int IrProbeAdcChannelIndex = 0;
        /// <summary>探头约定：0~10V 线性对应 0~100℃。</summary>
        private const double IrProbeVoltageFullScale = 10.0;
        private const double IrProbeTempFullScale = 100.0;

        private System.Windows.Forms.Timer _timerIrProbeTemp;

        /// 互斥配置多轴的点动和JOG运动配置：动态挂载初始化：20200110
        private void InitDynamicConfigureMotionMode()
        {
            //(a-1)初始化6轴运动的所有参数
            InitMovParam();//从主界面读取数据到3大标志位中   
            //(b)初始化6周运动的挂载模式
            for (short AXIS = 1; AXIS <= /*6*/8; AXIS++)//20200903批注:轴数由6轴提升到8轴
            {
                bool MoveModeFlag = m_bMoveModeFlag[AXIS - 1];
                DynamicConfigureMotionMode(AXIS, MoveModeFlag);
            }
        }
        /// <summary>
        /// 互斥配置多轴的点动和JOG运动配置：
        /// </summary>
        //根据多轴的点动和JOG运动选择情况配置
        private void DynamicConfigureMotionMode(short AXIS, bool MoveModeFlag)//根据多轴的点动和JOG运动选择情况配置
        {
            //(1)检索到相关指定的控件：
            string MoveUpBtn = "MoveUpBtn" + AXIS.ToString();
            string MoveDownBtn = "MoveDownBtn" + AXIS.ToString();
            Button MoveUpBtnSelected = (Button)GetControl(MoveUpBtn);
            Button MoveDownBtnSelected = (Button)GetControl(MoveDownBtn);
            //(2)动态挂载对应的运动模式：
            if (MoveModeFlag/*this.MoveModeLabel0.Checked*/ == true)//（1）点动挂载
            {
                //(2)动态添加方式实现：点动模式
                //(2-1)撤载JOG模式
                if (MoveUpBtnSelected != null)
                {
                    //(2-1)撤载JOG模式
                    MoveUpBtnSelected.MouseDown -= new System.Windows.Forms.MouseEventHandler(Move_MouseDownEvent);//20200110：动态添加事件
                    MoveUpBtnSelected.MouseUp -= new System.Windows.Forms.MouseEventHandler(Move_MouseUpEvent);//20200110：动态添加事件
                }
                if (MoveDownBtnSelected != null)
                {
                    //(2-1)撤载JOG模式
                    MoveDownBtnSelected.MouseDown -= new System.Windows.Forms.MouseEventHandler(Move_MouseDownEvent);//20200110：动态添加事件
                    MoveDownBtnSelected.MouseUp -= new System.Windows.Forms.MouseEventHandler(Move_MouseUpEvent);//20200110：动态添加事件
                }
                if (MoveUpBtnSelected != null)
                {
                    //(2-2)挂载点动模式
                    MoveUpBtnSelected.Click += new EventHandler(MoveBtn_Click);//20200110：动态添加事件
                }
                if (MoveDownBtnSelected != null)
                {
                    //(2-2)挂载点动模式
                    MoveDownBtnSelected.Click += new EventHandler(MoveBtn_Click);//20200110：动态添加事件
                }
            }
            else if (MoveModeFlag/*this.MoveModeLabel0.Checked*/ == false)//（2）JOG挂载
            {
                ////(2)动态添加方式实现：JOG模式
                //(2-1)撤载点动模式
                if (MoveUpBtnSelected != null)
                {
                    //(2-1)撤载点动模式
                    MoveUpBtnSelected.Click -= new EventHandler(MoveBtn_Click);//20200110：动态添加事件
                }
                if (MoveDownBtnSelected != null)
                {
                    //(2-1)撤载点动模式
                    MoveDownBtnSelected.Click -= new EventHandler(MoveBtn_Click);//20200110：动态添加事件
                }
                if (MoveUpBtnSelected != null)
                {
                    //(2-2)挂载JOG模式
                    MoveUpBtnSelected.MouseDown += new System.Windows.Forms.MouseEventHandler(Move_MouseDownEvent);//20200110：动态添加事件
                    MoveUpBtnSelected.MouseUp += new System.Windows.Forms.MouseEventHandler(Move_MouseUpEvent);//20200110：动态添加事件
                }
                if (MoveDownBtnSelected != null)
                {
                    //(2-2)挂载JOG模式
                    MoveDownBtnSelected.MouseDown += new System.Windows.Forms.MouseEventHandler(Move_MouseDownEvent);//20200110：动态添加事件
                    MoveDownBtnSelected.MouseUp += new System.Windows.Forms.MouseEventHandler(Move_MouseUpEvent);//20200110：动态添加事件
                }

            }
        }
        private void MoveModeLabel0_CheckedChanged(object sender, EventArgs e)
        {
            InitMovParam();//从主界面读取数据到3大标志位中           
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)//回零点：想起来了：20200110C#多线程爽的一比
            {
                case 1:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(1, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第1轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(1, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第1轴}}";
                        Log4Net.Info(msg);
                    }//(2)设置为JOG模式，执行JOGhandler挂载                 
                    break;
                case 2:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(2, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第2轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(2, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第2轴}}";
                        Log4Net.Info(msg);
                    }
                    break;
                case 3:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(3, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第3轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(3, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第3轴}}";
                        Log4Net.Info(msg);
                    }
                    break;
                case 4:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(4, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第4轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(4, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第4轴}}";
                        Log4Net.Info(msg);
                    }
                    break;
                case 5:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(5, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第5轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(5, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第5轴}}";
                        Log4Net.Info(msg);
                    }
                    break;
                case 6:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(6, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第6轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(6, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第6轴}}";
                        Log4Net.Info(msg);
                    }
                    break;

                case 7://20200903批注:轴数由6轴提升到8轴
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(7, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第7轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(7, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第7轴}}";
                        Log4Net.Info(msg);
                    }
                    break;
                case 8://20200903批注:轴数由6轴提升到8轴
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    {
                        DynamicConfigureMotionMode(8, true);
                        string msg = $"切换为点动模式====》：输出AXIS{{第8轴}}";
                        Log4Net.Info(msg);
                    }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    {
                        DynamicConfigureMotionMode(8, false);
                        string msg = $"切换为JOG模式====》：输出AXIS{{第8轴}}";
                        Log4Net.Info(msg);
                    }
                    break;
            }

        }
        /// <summary>
        /// 4大标志位
        /// </summary>
        bool[] m_bMoveModeFlag = new bool[8];//6轴的运动模式切换标志位————应该移动到royalMap中：20200109//20200903增加至8轴  
        string[] m_sVel = new string[8];//6轴的运动速度变量（含JOG和点动）//20200903增加至8轴  
        string[] m_sStep = new string[8];//6轴的点动运动移动量//20200903增加至8轴   
        bool[] FlagGoHome = new bool[8]/* { false,false,false,false,false,false}*/;//20200110因为ref修饰//目前用不到，先暂时留着吧//20200903增加至8轴
        bool[] m_bEnds = new bool[8];//202001410：6个轴的回原点位置端设置.//回原点端头标志位//20200903增加至8轴
        GohomeParam[] gohomeParam = new GohomeParam[8];//回零标志位//20200903增加至8轴  

        void InitMovParam()//从主界面读取数据到3大标志位中
        {
            m_bMoveModeFlag[0] = this.MoveModeLabel0.Checked;
            m_sVel[0] = this.velLabel0.Text;
            m_sStep[0] = this.stepLabel0.Text;
            m_bEnds[0] = this.HomeEndsLabel0.Checked;
            m_bMoveModeFlag[1] = this.MoveModeLabel1.Checked;
            m_sVel[1] = this.velLabel1.Text;
            m_sStep[1] = this.stepLabel1.Text;
            m_bEnds[1] = this.HomeEndsLabel1.Checked;
            m_bMoveModeFlag[2] = this.MoveModeLabel2.Checked;
            m_sVel[2] = this.velLabel2.Text;
            m_sStep[2] = this.stepLabel2.Text;
            m_bEnds[2] = this.HomeEndsLabel2.Checked;
            m_bMoveModeFlag[3] = this.MoveModeLabel3.Checked;
            m_sVel[3] = this.velLabel3.Text;
            m_sStep[3] = this.stepLabel3.Text;
            m_bEnds[3] = this.HomeEndsLabel3.Checked;
            m_bMoveModeFlag[4] = this.MoveModeLabel4.Checked;
            m_sVel[4] = this.velLabel4.Text;
            m_sStep[4] = this.stepLabel4.Text;
            m_bEnds[4] = this.HomeEndsLabel4.Checked;

            m_bMoveModeFlag[5] = this.MoveModeLabel5.Checked;
            m_sVel[5] = this.velLabel5.Text;
            m_sStep[5] = this.stepLabel5.Text;
            m_bEnds[5] = this.HomeEndsLabel5.Checked;

            m_bMoveModeFlag[6] = this.MoveModeLabel6.Checked;
            m_sVel[6] = this.velLabel6.Text;//20200915新增
            m_sStep[6] = this.stepLabel6.Text;
            m_bEnds[6] = this.HomeEndsLabel6.Checked;

            m_bMoveModeFlag[7] = this.MoveModeLabel7.Checked;
            m_sVel[7] = this.velLabel7.Text;
            m_sStep[7] = this.stepLabel7.Text;
            m_bEnds[7] = this.HomeEndsLabel7.Checked;

            RollerParam = Convert.ToDouble(this.RollerParamLabel.Text);//滚动系数
        }
        static double Pi = 3.14159265359;//精确到小数点后11位
#if false//就有的步进电机参数，Perimeter2、SubDivideCoe2为20220505新增
        double[] Perimeter = new double[3] { 40 * Pi * 0.95, 20 * 5, 2 };//20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        double[] SubDivideCoe = new double[3] { 800, 800, 800 };//20200916新增：3项步进电机细分设置参数
        double[] Perimeter2 = new double[3] { 40 * Pi * 0.95, 20 * 5, 2 };//20210505新增3个步进轴：特地用于3-4-5此3轴-1圈的周长//20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        double[] SubDivideCoe2 = new double[3] { 800, 800, 800 };//20210505新增3个步进轴：特地用于3-4-5此3轴-具体的细分值//20200916新增：3项步进电机细分设置参数
#else
        // 步进 JOG：vel=(m_sVel/Perimeter*)*(每转脉冲/1000) → motionMap 内 GT_SetVel 为 pulse/ms（约 ×1000=脉冲/秒）
        // 轴4(刮墨)、轴5(粉辊2)、轴6(粉辊1)：均按硬件 6400 脉冲/转；下式「每转脉冲」与固高+驱动当量需一致
        double[] Perimeter = new double[3] { 1, 5, 1 };//20220509新增：7轴的螺距修改为5mm //20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        // 索引0 给轴6：旧 25600/转，与现场驱动说明 6400/转 差 4 倍，已改为 6400
        double[] SubDivideCoe = new double[3] { 6400, 25000, 1600 };//20200916新增：3项步进电机细分设置参数
        double[] Perimeter2 = new double[3] { 1, 1, 1 };//20210505新增3个步进轴：特地用于3-4-5此3轴-1圈的周长//20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        // 索引1 轴4、索引2 轴5；轴4 旧 12800/转；轴5 旧 1600/转，均与 6400/转 现场说明对齐后见下行
        double[] SubDivideCoe2 = new double[3] { 1600, 6400, 6400 };// 轴3(索引0)落粉/2:1 减速仍 1600；轴4/轴5/轴6(另见 SubDivideCoe[0])=6400/转
#endif

        public bool RollerDirectionFlag = true;//20200925新增：默认辊子方向为与运动方向反向。false为counter，true为NoCounter;
        public bool SystemCorrectFlag = false;//20201014新增：系统校准标志位

        double RollerParam = 50;//20200917新增：铺粉辊子同双驱速度之比:对于铺粉运动很关键//20200924修改:辊子运动速度由运动系数修改为辊子运动的线速度
        //(2)JOG开始事件-上升：
        //关于此上升按键，点动和JOG运动切换是正常的
        //private void JogMoveUp(object sender, MouseEventArgs e)//当鼠标按下时，先判断当前运动模式，再决定是否执行指定的JOG动作
        private void JogMoveUp(short AXIS, bool m_bMoveModeFlag, string m_sVel)//
        {
            if (m_bMoveModeFlag == false)//进行了动态处理之后，此即为双保险：20200110
            {
                double vel = 0;

                if (AXIS == 3)//为步进电机：20220505新增：落粉轴电机-半圈限位
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    vel = (Convert.ToDouble(m_sVel) * 2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0] / 1000);//单位：rev//20220509新建：考虑到2：1的机械减速比
                }
                else if (AXIS == 4)//为步进电机：20220505新增：刮墨轴
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    // 旧换算（每转 12800 脉冲，与驱动 6400/转 不一时圈速会偏 2 倍）：vel=(m_sVel/Perimeter2[1])*(12800/1000)
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[1]) * 1 * (SubDivideCoe2[1] / 1000);// 与 SubDivideCoe2[1] 一致，现 6400/转
                }
                else if (AXIS == 5)//为步进电机：20260416修改：粉辊2
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    // 旧换算：每转 1600 脉冲、与 6400/转 不一时与目标圈速差约 4 倍：vel=(m_sVel/Perimeter2[2])*(1600/1000)
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[2]) * 1 * (SubDivideCoe2[2] / 1000);// 与 SubDivideCoe2[2] 一致，现 6400/转
                }


                else if (AXIS == 6 /*|| AXIS == 7 || AXIS == 8*/)//为步进电机：20200622新增:铺粉辊电机//20220511修复bug:此处应该为else if
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    // 旧换算（每转 25600 脉冲，与驱动 6400/转 不一时圈速会偏 4 倍）：vel=(m_sVel/Perimeter[0])*(25600/1000)
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);// 与 SubDivideCoe[0] 一致，现 6400/转
                }
                else if (AXIS == 7 || AXIS == 8)//20260416修改：轴7铺粉车、轴8成型缸，按伺服轴处理
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    motionMap.jogPrm.acc = 1000;
                    motionMap.jogPrm.dec = 1000;
                    motionMap.jogPrm.smooth = 0;
                    vel = Convert.ToDouble(m_sVel);
                }
                else//为伺服电机
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                                               //执行JOG运动
                    motionMap.jogPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 1000;
                    motionMap.jogPrm.smooth = 0;

                    vel = Convert.ToDouble(m_sVel);//1000pulse/1mm
                }
                //double vel = Convert.ToDouble(this.velLabel1.Text);//千脉冲对应的mm数，这个要尽可能的统一   
                //double vel = Convert.ToDouble(m_sVel);
                if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                {
                    RollerParam = -System.Math.Abs(RollerParam);
                }
                else//辊子运动方向为正向：20200925新增：
                {
                    RollerParam = System.Math.Abs(RollerParam);
                }
                motionMap.JogMotion(AXIS, ref motionMap.jogPrm, vel, RollerParam);

                string msg = $"手动正向JOG指令（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}},运动速度{{{m_sVel}MM/s}}";
                Log4Net.Info(msg);

            }
            else if (m_bMoveModeFlag == true) {/*不执行任何操作*/}//进行了动态处理之后，此即为双保险：20200110
        }
        //(2)JOG开始事件-下降：
        //关于此下降按键，点动和JOG运动切换是不正常的：JOG运动正常，点动是不正常
        //private void JogMoveDown(object sender, MouseEventArgs e)//当鼠标按下时，先判断当前运动模式，再决定是否执行指定的JOG动作
        private void JogMoveDown(short AXIS, bool m_bMoveModeFlag, string m_sVel)

        {
            double vel = 0;
            if (m_bMoveModeFlag == false)
            {
                if (AXIS == 3)//为步进电机：20220505新增：落粉轴电机-半圈限位
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    vel = (Convert.ToDouble(m_sVel) * 2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0] / 1000);//20220509新建：单位：rev//20220509新建：考虑到2：1的机械减速比
                }
                else if (AXIS == 4)//为步进电机：20220505新增：刮墨轴
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    // 旧换算：每转 12800 脉冲时 vel=(m_sVel/Perimeter2[1])*(12800/1000)
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[1]) * 1 * (SubDivideCoe2[1] / 1000);// 与 JogMoveUp 轴4 一致
                }
                else if (AXIS == 5)//为步进电机：20260416修改：粉辊2
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    // 旧换算：每转 1600 时 vel=(m_sVel/Perimeter2[2])*(1600/1000)
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[2]) * 1 * (SubDivideCoe2[2] / 1000);// 与 JogMoveUp 轴5 一致
                }

                else if (AXIS == 6 /*|| AXIS == 7 || AXIS == 8*/)//为步进电机：20200622新增:铺粉辊电机//20220511修复bug:此处应该为else if
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    // 旧换算：每转 25600 脉冲时 vel=(m_sVel/Perimeter[0])*(25600/1000)
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);// 与 JogMoveUp 轴6 一致
                }
                else if (AXIS == 7 || AXIS == 8)//20260416修改：轴7铺粉车、轴8成型缸，按伺服轴处理
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    motionMap.jogPrm.acc = 1000;
                    motionMap.jogPrm.dec = 1000;
                    motionMap.jogPrm.smooth = 0;
                    vel = Convert.ToDouble(m_sVel);
                }
                else//为伺服电机
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    //驱动器的默认坐标系是：千脉冲10mm:20200111
                    motionMap.jogPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 1000;
                    motionMap.jogPrm.smooth = 0;
                    vel = Convert.ToDouble(m_sVel);//1000pulse/1mm
                }
                //double vel = Convert.ToDouble(this.velLabel1.Text);//千脉冲对应的mm数，这个要尽可能的统一                   
                //double vel = Convert.ToDouble(m_sVel);

                if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                {
                    RollerParam = System.Math.Abs(RollerParam);
                }
                else//辊子运动方向为正向：20200925新增：
                {
                    RollerParam = -System.Math.Abs(RollerParam);
                }
                motionMap.JogMotion(AXIS, ref motionMap.jogPrm, -vel, RollerParam);//这一行代码应该没有问题
                //motionMap.JogMotion(1, ref motionMap.jogPrm, vel);

                string msg = $"手动负向JOG指令（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}},运动速度{{{m_sVel}MM/s}}";
                Log4Net.Info(msg);
            }
            else if (m_bMoveModeFlag == true) {/*不执行任何操作*/}
        }
        /// <summary>
        /// JOG运动开启处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Move_MouseDownEvent(object sender, MouseEventArgs e)//当鼠标按下时，先判断当前运动模式，再决定是否执行指定的JOG动作
        {
            //(a-1)初始化6轴运动的所有参数
            //bool[] m_bMoveModeFlag = new bool[6];//6轴的运动模式切换标志位
            //string[] m_sVel = new string[6];//6轴的运动速度变量（含JOG和点动）
            //string[] m_sStep = new string[6];//6轴的点动运动移动量
            InitMovParam();//从主界面读取数据到3大标志位中           
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            if (IsRetrofitProtectedTag(myTag) && !EnsureRetrofitReady($"手动JOG轴组Tag={myTag}"))
            {
                return;
            }
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)
            {
                case 1:
                    if (m_bMoveModeFlag[0] == false)//为JOG运动标志位
                    {
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);//20230508新建：
//#endif
                        JogMoveUp(1, m_bMoveModeFlag[0], m_sVel[0]);
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);//20230508新建：
//#endif
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 2:
                    if (m_bMoveModeFlag[0] == false)
                    {
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);//20230508新建：
//#endif
                        JogMoveDown(1, m_bMoveModeFlag[0], m_sVel[0]);
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);//20230508新建：
//#endif
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 3:
                    if (m_bMoveModeFlag[1] == false)
                    {
                        JogMoveUp(2, m_bMoveModeFlag[1], m_sVel[1]); 
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 4:
                    if (m_bMoveModeFlag[1] == false)
                    { JogMoveDown(2, m_bMoveModeFlag[1], m_sVel[1]); }
                    else {/*不执行任何操作*/}
                    break;
                case 5:
                    if (m_bMoveModeFlag[2] == false)
                    { JogMoveUp(3, m_bMoveModeFlag[2], m_sVel[2]); }
                    else {/*不执行任何操作*/}
                    break;
                case 6:
                    if (m_bMoveModeFlag[2] == false)
                    { JogMoveDown(3, m_bMoveModeFlag[2], m_sVel[2]); }
                    else {/*不执行任何操作*/}
                    break;
                case 7:
                    if (m_bMoveModeFlag[3] == false)
                    { JogMoveUp(4, m_bMoveModeFlag[3], m_sVel[3]); }
                    else {/*不执行任何操作*/}
                    break;
                case 8:
                    if (m_bMoveModeFlag[3] == false)
                    { JogMoveDown(4, m_bMoveModeFlag[3], m_sVel[3]); }
                    else {/*不执行任何操作*/}
                    break;
                case 9:
                    if (m_bMoveModeFlag[4] == false)
                    { JogMoveUp(5, m_bMoveModeFlag[4], m_sVel[4]); }
                    else {/*不执行任何操作*/}
                    break;
                case 10:
                    if (m_bMoveModeFlag[4] == false)
                    { JogMoveDown(5, m_bMoveModeFlag[4], m_sVel[4]); }
                    else {/*不执行任何操作*/}
                    break;
                case 11:
                    if (m_bMoveModeFlag[5] == false)
                    { JogMoveUp(6, m_bMoveModeFlag[5], m_sVel[5]); }
                    else {/*不执行任何操作*/}
                    break;
                case 12:
                    if (m_bMoveModeFlag[5] == false)
                    { JogMoveDown(6, m_bMoveModeFlag[5], m_sVel[5]); }
                    else {/*不执行任何操作*/}
                    break;

                case 13://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == false)
                    { JogMoveUp(7, m_bMoveModeFlag[6], m_sVel[6]); }
                    else {/*不执行任何操作*/}
                    break;
                case 14://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == false)
                    { JogMoveDown(7, m_bMoveModeFlag[6], m_sVel[6]); }
                    else {/*不执行任何操作*/}
                    break;

                case 15://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == false)
                    { JogMoveUp(8, m_bMoveModeFlag[7], m_sVel[7]); }
                    else {/*不执行任何操作*/}
                    break;
                case 16://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == false)
                    { JogMoveDown(8, m_bMoveModeFlag[7], m_sVel[7]); }
                    else {/*不执行任何操作*/}
                    break;
            }
        }
        //(3)JOG结束事件-上升/下降：
        //private void JogMoveStop(object sender, MouseEventArgs e)//当鼠标弹起时，先判断当前运动模式，再决定是否执行指定的JOG动作
        private void JogMoveStop(short AXIS, bool m_bMoveModeFlag)
        {
            if (AXIS == 7)//20260416修改：铺粉车运动轴改为轴7，停止时同步停粉辊1轴
            {
                AXIS = 6;
                motionMap.StopMotion(AXIS);//停止铺粉辊JOG转动
                string msg = $"自动停止铺粉棍轴JOG输出（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}}";
                Log4Net.Info(msg);
                AXIS = 7;
            }
            else//不是当前铺粉车联动停轴
            { }

            if (m_bMoveModeFlag == false)
            {
                motionMap.StopMotion(AXIS);//停止JOG运动

                string msg = $"手动停止JOG输出（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}}";
                Log4Net.Info(msg);
            }
            else if (m_bMoveModeFlag == true) {/*不执行任何操作*/}
        }
        /// <summary>
        /// JOG运动关闭处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Move_MouseUpEvent(object sender, MouseEventArgs e)//当鼠标弹起时，先判断当前运动模式，再决定是否执行指定的JOG动作
        {
            //(a-1)初始化6轴运动的所有参数
            InitMovParam();//从主界面读取数据到3大标志位中           
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)
            {
                case 1:
                    if (m_bMoveModeFlag[0] == false)//为JOG运动标志位
                    { 
                        JogMoveStop(1, m_bMoveModeFlag[0]);
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);//20230508新建：
//#endif
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 2:
                    if (m_bMoveModeFlag[0] == false)
                    { 
                        JogMoveStop(1, m_bMoveModeFlag[0]);
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);//20230508新建：
//#endif
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 3:
                    if (m_bMoveModeFlag[1] == false)
                    { JogMoveStop(2, m_bMoveModeFlag[1]); }
                    else {/*不执行任何操作*/}
                    break;
                case 4:
                    if (m_bMoveModeFlag[1] == false)
                    { JogMoveStop(2, m_bMoveModeFlag[1]); }
                    else {/*不执行任何操作*/}
                    break;
                case 5:
                    if (m_bMoveModeFlag[2] == false)
                    { JogMoveStop(3, m_bMoveModeFlag[2]); }
                    else {/*不执行任何操作*/}
                    break;
                case 6:
                    if (m_bMoveModeFlag[2] == false)
                    { JogMoveStop(3, m_bMoveModeFlag[2]); }
                    else {/*不执行任何操作*/}
                    break;
                case 7://20260416修改：刮墨轴
                    if (m_bMoveModeFlag[3] == false)
                    { JogMoveStop(4, m_bMoveModeFlag[3]); }
                    else {/*不执行任何操作*/}
                    break;
                case 8://20260416修改：刮墨轴
                    if (m_bMoveModeFlag[3] == false)
                    { JogMoveStop(4, m_bMoveModeFlag[3]); }
                    else {/*不执行任何操作*/}
                    break;
                case 9:
                    if (m_bMoveModeFlag[4] == false)
                    { JogMoveStop(5, m_bMoveModeFlag[4]); }
                    else {/*不执行任何操作*/}
                    break;
                case 10:
                    if (m_bMoveModeFlag[4] == false)
                    { JogMoveStop(5, m_bMoveModeFlag[4]); }
                    else {/*不执行任何操作*/}
                    break;
                case 11://20260416修改：粉辊1运动
                    if (m_bMoveModeFlag[5] == false)
                    { JogMoveStop(6, m_bMoveModeFlag[5]); }
                    else {/*不执行任何操作*/}
                    break;
                case 12://20260416修改：粉辊1运动
                    if (m_bMoveModeFlag[5] == false)
                    { JogMoveStop(6, m_bMoveModeFlag[5]); }
                    else {/*不执行任何操作*/}
                    break;

                case 13://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == false)
                    { JogMoveStop(7, m_bMoveModeFlag[6]); }
                    else {/*不执行任何操作*/}
                    break;
                case 14://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == false)
                    { JogMoveStop(7, m_bMoveModeFlag[6]); }
                    else {/*不执行任何操作*/}
                    break;

                case 15://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == false)
                    { JogMoveStop(8, m_bMoveModeFlag[7]); }
                    else {/*不执行任何操作*/}
                    break;
                case 16://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == false)
                    { JogMoveStop(8, m_bMoveModeFlag[7]); }
                    else {/*不执行任何操作*/}
                    break;
            }
        }
        //(4)回零点事件：
        //(0）初始化回零点线程参数：
        //GohomeParam[] gohomeParam = new GohomeParam[6];
        //bool[] Ends = new bool[6];//202001410：6个轴的回原点位置端设置
        private List<Thread> GoHomeThreads = new List<Thread>();
        //private Thread GohomeThread;
        //private void RunThread(bool FlagGoHome,bool Ends)
        //{motionMap.GoHome(1, 20, true);/*返回到原点*/}

        //20200222：double m_dHeight1, m_dHeight2, m_dHeight3, m_dHeight4;//20200222：存储的6个电机的校准行程，也是0位的编码器值；单位：脉冲
        public double[] k_dJourney = new double[6];//20200222：存储的6个电机的校准行程，也是0位的编码器值；单位：脉冲
        int[] k_bCorrectFlag = new int[6];
        private void GohomeWork(object Param)
        {
            GohomeParam gohomeParam = Param as GohomeParam;//类型转换——输入数据
            ////motionMap.GoHome(1, 20, true);//返回到原点
            //motionMap.GoHome(gohomeParam.AXIS, gohomeParam.vel, gohomeParam.Ends);//返回到原点————临时注释掉

            //20200222：零位校准设置1轴其编码器值为0，向下运动到限位
            //20200222：校准零位和行程
            if (k_bCorrectBtn == false)//没有使能重新校准//20200227:
            {
                //motionMap.GoHome(1, 20, true);//返回到原点
                if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                {
                    RollerParam = -System.Math.Abs(RollerParam);
                }
                else//辊子运动方向为正向：20200925新增：
                {
                    RollerParam = System.Math.Abs(RollerParam);
                }
                motionMap.GoHome(gohomeParam.AXIS, gohomeParam.vel, gohomeParam.Ends, RollerParam);//返回到原点————临时注释掉

                motionMap.StopMotion(6);//停止运动

                string msg = $"自动JOG回零（8轴运控板卡系统） ====》：" +
                    $"输出AXIS{{第{gohomeParam.AXIS}轴}}速度{{{gohomeParam.vel}MM/S}}返回端{{{gohomeParam.Ends}端}}";
                Log4Net.Info(msg);

            }
            else//是否使能重新校准？//20200227:
            {
                if (k_bCorrectFlag[gohomeParam.AXIS - 1] == 0)//（1）第一步，校准零位————//20200227：校准的时候是：首先向下运动，然后再向上运动
                {
                    //20200227添加：修复校准bug
                    //20200925新增：
                    if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                    {
                        RollerParam = -System.Math.Abs(RollerParam);
                    }
                    else//辊子运动方向为正向：20200925新增：
                    {
                        RollerParam = System.Math.Abs(RollerParam);
                    }
                    motionMap.GoHome(gohomeParam.AXIS, gohomeParam.vel, gohomeParam.Ends, RollerParam);//返回到原点————临时注释掉

                    motionMap.SetEncPos(gohomeParam.AXIS, 0);//运动到负限，校准0位。单位：脉冲
                    k_bCorrectFlag[gohomeParam.AXIS - 1]++;//校准完零位，该标志位设为1：对应的行程框设置为红色
                    //m_bEnds[gohomeParam.AXIS - 1] = !m_bEnds[gohomeParam.AXIS - 1];//20200227去除

                    //对应的行程框设置为红色
                    string JourneyBoxName = "JourneyBox" + gohomeParam.AXIS.ToString();
                    TextBox JourneyBox = (TextBox)GetControl(JourneyBoxName); //选中对应的形成框：20200222新增
                    if ((JourneyBox != null) && (JourneyBox.InvokeRequired == true))//20200220：监控线程中，使用委托定时刷新状态
                    {
                        JourneyBox.BeginInvoke(
                            new Action(() =>
                            { JourneyBox.BackColor = Color.Salmon; }
                            ));
                    }
                }
                else if (k_bCorrectFlag[gohomeParam.AXIS - 1] == 1)//(2)第二步，校准行程————//20200227：校准的时候是：其次向上运动
                {
                    //20200227添加：修复校准bug
                    if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                    {
                        RollerParam = -System.Math.Abs(RollerParam);
                    }
                    else//辊子运动方向为正向：20200925新增：
                    {
                        RollerParam = System.Math.Abs(RollerParam);
                    }
                    motionMap.GoHome(gohomeParam.AXIS, gohomeParam.vel, !(gohomeParam.Ends), RollerParam);//返回到原点————临时注释掉

                    k_dJourney = motionMap.GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
                    ////k_dJourney[0] = 1000; k_dJourney[1] = 2000; k_dJourney[2] = 3000;//测试使用
                    ////k_dJourney[3] = 4000; k_dJourney[4] = 5000; k_dJourney[5] = 6000;//测试使用
                    motionMap.SetEncPos(gohomeParam.AXIS, (int)(k_dJourney[gohomeParam.AXIS - 1]));//在正限位值，设置0位的编码值。单位：脉冲
                    k_bCorrectFlag[gohomeParam.AXIS - 1]++;//校准完零位，该标志位设为1：对应的行程框设置为绿色
                    //m_bEnds[gohomeParam.AXIS - 1] = !m_bEnds[gohomeParam.AXIS - 1];//20200227去除

                    //对应的行程框设置为绿色
                    string JourneyBoxName = "JourneyBox" + gohomeParam.AXIS.ToString();
                    TextBox JourneyBox = (TextBox)GetControl(JourneyBoxName); //选中对应的形成框：20200222新增
                    if ((JourneyBox != null) && (JourneyBox.InvokeRequired == true))//20200220：监控线程中，使用委托定时刷新状态
                    {
                        JourneyBox.BeginInvoke(
                            new Action(() =>
                            {
                                JourneyBox.BackColor = Color.GreenYellow;
                                JourneyBox.Text = (k_dJourney[gohomeParam.AXIS - 1] / 1000).ToString();
                            }
                            ));
                    }
                }
            }
        }
        //异常1：回原点有短暂的停顿现象，类似于有时候，按一下会先点动，再按一下才会回原点
        //异常2：回零键连按2次，会JOG参数报错。
        //private void MoveHome(object sender, EventArgs e)//不管当前的运动方式是什么，开启JOG运动模式，返回到原点
        private void MoveHome(short AXIS, double vel, ref bool FlagGoHome, bool Ends)//JOG回原点.20200110:只负责1轴的回零点
        {
            GohomeParam tempgohomeParam = new GohomeParam();//20200110:放到这里主要方便下面的多线程直接调用
            tempgohomeParam.AXIS = AXIS;
            tempgohomeParam.vel = vel;//20200111修正:回零点的速度，调整为原来的10分之1
            //tempgohomeParam.FlagGoHome = FlagGoHome;//没有必要放在对应的结构体中
            tempgohomeParam.Ends = Ends;
            gohomeParam[AXIS - 1] = tempgohomeParam;
            //(1)避免.重复回原点线程：           
            //if (FlagGoHome/*motionMap.FlagGoHome*/ == true)//首先关闭线程——20200110：避免重复回原点
            //{
            //    GohomeThread.Abort();
            //    FlagGoHome = false;
            //}
            string tempThreadName = "GoHomeThred" + AXIS.ToString();
            Thread tempThread = GoHomeThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
            if (/*tempThread != null*/tempThread != null/*(tempThread.IsAlive==true)*/)
            {
                //if (tempThread.IsAlive == false)//线程不活
                //{
                //    tempThread.Start(gohomeParam[AXIS - 1]);//(3)第3部曲：多线程3步曲 
                //    FlagGoHome = true;//——————————————————————————标志位在调用出使用才可以
                //    GoHomeThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                //}
                //else if (tempThread.IsAlive == true)
                //{
                //    /*不执行任何操作*/
                //    tempThread.Abort();//20200111:三保险
                //    FlagGoHome = false;//20200111:三保险
                //}
                GoHomeThreads.Remove(tempThread);
            }
            else
            {
                //(2a)回原点线程:20200110
                //motionMap.GoHome(1);
                //(1)不带参数的多线程方式实现：
                //ThreadStart entry = new ThreadStart(RunThread);/
                //GohomeThread = new Thread(entry) { IsBackground = true };
                //GohomeThread.Start();
                //(2b)带参数的多线程方式实现：
                tempThread/*Thread GohomeThread*/ = new Thread(new ParameterizedThreadStart(GohomeWork)) { IsBackground = true };//(1)第1部曲：多线程3步曲
                tempThread.Name = "GoHomeThred" + AXIS.ToString();//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
                tempThread.Start(gohomeParam[AXIS - 1]);//(3)第3部曲：多线程3步曲 
                FlagGoHome = true;//——————————————————————————标志位在调用出使用才可以
                GoHomeThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程

            }
            //(3)相关按钮使能设置：
            //GohomeThread.Abort();
            string MoveUpBtn = "MoveUpBtn" + AXIS.ToString();
            string MoveDownBtn = "MoveDownBtn" + AXIS.ToString();
            string homeButton = "homeButton" + AXIS.ToString();
            Button MoveUpBtnSelected = (Button)GetControl(MoveUpBtn);
            Button MoveDownBtnSelected = (Button)GetControl(MoveDownBtn);
            Button homeButtonSelected = (Button)GetControl(homeButton);
            if (MoveUpBtnSelected != null)
            { MoveUpBtnSelected.Enabled = false;/*//上升按键不可按*/}
            if (MoveDownBtnSelected != null)
            { MoveDownBtnSelected.Enabled = false;/*//下降按键不可按*/}
            if (homeButtonSelected != null)
            { homeButtonSelected.Enabled = false;/*//回零键不可按*/}
            //this.MoveUpBtn1.Enabled = false;//上升按键不可按
            //this.MoveDownBtn1.Enabled = false;//下降按键不可按
            //this.homeButton1.Enabled = false;//回零键不可按
        }
        private Control GetControl(string Name)//查找指定名称控件:20200110测试通过
        {
            var parent = this.FindForm();
            var findButton = parent.Controls.Find(Name, true).FirstOrDefault();
            if (findButton != null)
            { return findButton; }
            else { return null; }
        }

        private void HomeBtn_Click(object sender, EventArgs e)//不管当前的运动方式是什么，统一停止下来
        {
            InitMovParam();//从主界面读取数据到3大标志位中
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            if (IsRetrofitProtectedHomeAxis(myTag) && !EnsureRetrofitReady($"手动回零轴{myTag}"))
            {
                return;
            }
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            double vel = Convert.ToDouble(m_sVel[myTag - 1]/* 20*/);
            switch (myTag)//回零点：想起来了：20200110C#多线程爽的一比
            {
                case 1:
                    MoveHome(1, vel, ref FlagGoHome[0], m_bEnds[0]);//新建线程，执行会零点，并标记回零点标志位：20200110
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(1, (int)(0 - 1000 * m_dHeight1));}
                    //20200222：对应的位置初始化放到线程中实现。
                    break;
                case 2:
                    MoveHome(2, vel, ref FlagGoHome[1], m_bEnds[1]);
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(2, (int)(0 - 1000 * m_dHeight2)); }
                    break;
                case 3:
                    MoveHome(3, vel, ref FlagGoHome[2], m_bEnds[2]);
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(3, (int)(0 - 1000 * m_dHeight3)); }
                    break;

                case 4://20260416修改：刮墨轴
                    MoveHome(4, vel, ref FlagGoHome[3], m_bEnds[3]);
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(4, (int)(0 - 1000 * m_dHeight4)); }
                    break;
                case 5:
                    MoveHome(5, vel, ref FlagGoHome[4], m_bEnds[4]);
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(5, (int)(0 - 1000 * m_dHeight5)); }
                    break;
                case 6://20200917批注：铺粉辊子轴//vel = (Convert.ToDouble(m_sVel[5]) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);                    
                    MoveHome(6, vel/*vel/125*/, ref FlagGoHome[5], m_bEnds[5]);//20200623修改：1000pulse/125mm
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(6, (int)(0 - 1000 * m_dHeight6)); }
                    break;
                case 7://20200903批注:轴数由6轴提升到8轴              
                    MoveHome(7, vel, ref FlagGoHome[6], m_bEnds[6]);//20200623修改：1000pulse/125mm
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(6, (int)(0 - 1000 * m_dHeight6)); }
                    break;
                case 8://20200903批注:轴数由6轴提升到8轴：刮墨副运动
                    MoveHome(8, vel, ref FlagGoHome[7], m_bEnds[7]);//20200623修改：1000pulse/125mm
                    //if (this.CorrectCheck.Checked)//20200222：零位校准设置1轴其编码器值为0，向下运动到限位
                    //{ motionMap.SetEncPos(6, (int)(0 - 1000 * m_dHeight6)); }
                    break;
            }
        }
        //(4)停止运动事件：
        //异常1：GOHOME过程中，点击停止————再点击上升下降，就没有问题；如果直接点击上升和下降则报警
        //异常2：如果连击两次GOHOME，——————则报警
        //异常3：只要触发过1此正限位后，——————上升的点动运动————就不反馈
        //异常4：初始工作过程中，点动和JOG运动切换紊乱
        //明天需要紧急处理此三个BUG
        //private void MoveStop(object sender, EventArgs e)//不管当前的运动方式是什么，统一停止下来
        private void MoveStop(short AXIS, ref bool FlagGoHome)
        {
            //if (motionMap.FlagGoHome == true)
            //{
            //    GohomeThread.Abort();
            //    motionMap.FlagGoHome = false;
            //}
            string tempThreadName = "GoHomeThred" + AXIS.ToString();
            Thread tempThread = GoHomeThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
            if (tempThread != null)
            {
                //tempThread.Abort();
                GoHomeThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
                FlagGoHome = false;
            }

            if (AXIS == 7)
            {
                motionMap.StopMotion(6);//停止运动

                string msg = $"自动停止铺粉棍轴JOG输出（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}}";
                Log4Net.Info(msg);

                motionMap.StopMotion(AXIS);//停止运动

                msg = $"手动停止JOG输出（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}}";
                Log4Net.Info(msg);
            }
            else
            {
                motionMap.StopMotion(AXIS);//停止运动

                string msg = $"手动停止JOG输出（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}}";
                Log4Net.Info(msg);
            }

            string MoveUpBtn = "MoveUpBtn" + AXIS.ToString();
            string MoveDownBtn = "MoveDownBtn" + AXIS.ToString();
            string homeButton = "homeButton" + AXIS.ToString();
            Button MoveUpBtnSelected = (Button)GetControl(MoveUpBtn);
            Button MoveDownBtnSelected = (Button)GetControl(MoveDownBtn);
            Button homeButtonSelected = (Button)GetControl(homeButton);
            if (MoveUpBtnSelected != null)
            { MoveUpBtnSelected.Enabled = true;/*//上升按键不可按*/}
            if (MoveDownBtnSelected != null)
            { MoveDownBtnSelected.Enabled = true;/*//下降按键不可按*/}
            if (homeButtonSelected != null)
            { homeButtonSelected.Enabled = true;/*//回零键不可按*/}
            //this.MoveUpBtn1.Enabled = true;
            //this.MoveDownBtn1.Enabled = true;
            //this.homeButton1.Enabled = true;//回零键不可按
        }
        //bool[] FlagGoHome = new bool[6];
        /// <summary>
        /// 关闭运动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopBtn_Click(object sender, EventArgs e)//不管当前的运动方式是什么，统一停止下来
        {
            //(a-1)初始化6轴运动的所有参数
            //bool[] FlagGoHome = new bool[6];
            InitMovParam();//从主界面读取数据到3大标志位中           
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)
            {
                case 1:
                    MoveStop(1, ref FlagGoHome[0]);
                    break;
                case 2:
                    MoveStop(2, ref FlagGoHome[1]);
                    break;
                case 3:
                    MoveStop(3, ref FlagGoHome[2]);
                    break;
                case 4:
                    MoveStop(4, ref FlagGoHome[3]);
                    break;
                case 5:
                    MoveStop(5, ref FlagGoHome[4]);
                    break;
                case 6:
                    MoveStop(6, ref FlagGoHome[5]);
                    break;

                case 7://20200903批注:轴数由6轴提升到8轴
                    MoveStop(7, ref FlagGoHome[6]);
                    break;
                case 8://20200903批注:轴数由6轴提升到8轴
                    MoveStop(8, ref FlagGoHome[7]);
                    break;
            }
        }

        //(5)点动上升：
        /*************************(a)上升按键*****************************/
        //异常1：关于此上升按键，点动和JOG运动切换是正常的
        //异常2：点动表现出来：限位之后，按反方向点动没有反应，需要重新点击切换按键进行切换才可以————原因是：回到原点后motionMap.ReadAxisSate(1)没有及时读取到值
        //异常3：点位运动设置的步长太长之后，连续电机两次，会出现位置清零错误
        //bool OneClickflag = true;EquipMoveBtn_Click
        //private void TrapMoveUp(object sender, EventArgs e)//单击按钮时，判断是什么运动模式，再决定是否执行点动        
        private void TrapMoveUp(short AXIS, bool m_bMoveModeFlag, string m_sVel, string m_sStep, bool OtherThreadUse, bool WaitStopFlag)//20220512新建:点动运动是否等停
        {
            if (m_bMoveModeFlag == true)
            {
                motionMap.ClrLimitAndAbrupt(AXIS);//增添这一行非常关键。——————对应的trpmotion中间的清楚报警和限位就有点不太必要了              
                //读取指定轴的状态。
                motionMap.ReadAxisSate(AXIS);
                if (motionMap.axisSateMonitor.FlagPosLimit1 == true)//处于正限位，不进行工作
                {/*不执行任何操作*/}
                else//没有正限位，执行正向点位运动
                {
                    Button MoveUpBtnSelected = null, MoveDownBtnSelected = null;
                    if (OtherThreadUse == true)//调用自其他线程
                    { }
                    else//调用自界面线程
                    {
                        string MoveUpBtn = "MoveUpBtn" + AXIS.ToString();
                        string MoveDownBtn = "MoveDownBtn" + AXIS.ToString();
                        MoveUpBtnSelected = (Button)GetControl(MoveUpBtn);
                        MoveDownBtnSelected = (Button)GetControl(MoveDownBtn);
                        if (MoveUpBtnSelected != null)
                        { MoveUpBtnSelected.Enabled = false;/*//上升按键不可按*/}
                        if (MoveDownBtnSelected != null)
                        { MoveDownBtnSelected.Enabled = false;/*//下降按键不可按*/}
                        //this.MoveUpBtn1.Enabled = false;//上升按键不可按
                        //this.MoveDownBtn1.Enabled = false;//下降按键不可按
                    }

                    ////点位运动前，必要的保证工作
                    ////（1）先重新暂停一下所有的运动
                    ////（2）否则，连击点动，会提示点动运动错误
                    //motionMap.StopMotion(1);
                    double vel = 0;
                    int position = 0;

                    //20220509新增：实现10轴电机的精度控制
                    //20220509新增：实现10轴电机的精度控制
                    if (AXIS == 3)//为步进电机：20220505新增：落粉轴电机-半圈限位
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) * 2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0] / 1000);//20220509新建：考虑到第3轴步进的减速比2：1//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) * 2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0]));//20220509新建：考虑到第3轴步进的减速比2：1//当前细分的脉冲输出数
                    }
                    else if (AXIS == 4)//为步进电机：20220505新增：刮墨轴
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter2[1]) * 1 * (SubDivideCoe2[1] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter2[1]) * 1 * (SubDivideCoe2[1]));//当前细分的脉冲输出数
                    }
                    else if (AXIS == 5)//为步进电机：20260416修改：粉辊2
                    {

                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter2[2]) * 1 * (SubDivideCoe2[2] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter2[2]) * 1 * (SubDivideCoe2[2]));//当前细分的脉冲输出数
                    }


                    else if (AXIS == 6 /*|| AXIS == 7 || AXIS == 8*/)//20220511修复bug:此处应该为else if//为步进电机：20200622新增
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;
                        //vel = Convert.ToDouble(m_sVel)/125;//此前的脉冲速度
                        //position = (int)(Convert.ToDouble(m_sStep) * 1000)/125;//当前细分的脉冲输出数

                        vel = (Convert.ToDouble(m_sVel) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter[0]) * 1 * (SubDivideCoe[0]));//当前细分的脉冲输出数
                    }
                    else if (AXIS == 7 || AXIS == 8)//20260416修改：轴7铺粉车、轴8成型缸，按伺服轴处理
                    {
                        motionMap.trapPrm.acc = 1000;
                        motionMap.trapPrm.dec = 1000;
                        motionMap.trapPrm.velStart = 5;
                        motionMap.trapPrm.smoothTime = 1;

                        vel = Convert.ToDouble(m_sVel);
                        position = (int)(Convert.ToDouble(m_sStep) * 1000);
                    }
                    else//为伺服电机
                    {
                        //执行点动运动
                        motionMap.trapPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值                                                    
                        motionMap.trapPrm.dec = 1000;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                        motionMap.trapPrm.velStart = 5;
                        motionMap.trapPrm.smoothTime = 1;
                        vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一
#if false//20220509新建：
                        if (AXIS == 1 /*|| AXIS == 2*/ || AXIS == 3)
                        {
                            position = (int)(Convert.ToDouble(m_sStep));//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else if (AXIS == 2)//20210716拟修复：偶发的送粉缸1报热过载的问题：从型号方面考虑
                        {
                            motionMap.trapPrm.acc = 10;//————————————————————待实现，从其他的图形窗口中读取对应的值                                                    
                            motionMap.trapPrm.dec = 10;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                            motionMap.trapPrm.velStart = 0;
                            motionMap.trapPrm.smoothTime = 25;
                            vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一
                            position = (int)(Convert.ToDouble(m_sStep));//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else
                        {
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
#else
                        if (AXIS == 1)//20220509修改：1000pulse/mm//20210716拟修复：偶发的送粉缸1报热过载的问题：从型号方面考虑
                        {
                            motionMap.trapPrm.acc = 10;//————————————————————待实现，从其他的图形窗口中读取对应的值                                                    
                            motionMap.trapPrm.dec = 10;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                            motionMap.trapPrm.velStart = 0;
                            motionMap.trapPrm.smoothTime = 25;
                            vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else if (AXIS == 2)//20220509修改：1000pulse/mm
                        {
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20220509修改：1000pulse/mm//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else//20220509修改：1000pulse/mm
                        {
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        double[] jogEncPos2 = motionMap.GetEncPos();
                        double jogCountPerMm2 = GetInkCarCountPerMM(AXIS);
                        double jogEncRaw2 = jogEncPos2[AXIS - 1];
                        double jogEncMm2 = jogEncRaw2 / jogCountPerMm2;
                        Log4Net.Info($"点动换算：AXIS={AXIS}, stepMm={Convert.ToDouble(m_sStep):F3}, pulse={position}, encoderRaw={jogEncRaw2:F0}, encoderMm={jogEncMm2:F3}, countPerMm={jogCountPerMm2:F4}");
                        double[] jogEncPos = motionMap.GetEncPos();
                        double jogCountPerMm = GetInkCarCountPerMM(AXIS);
                        double jogEncRaw = jogEncPos[AXIS - 1];
                        double jogEncMm = jogEncRaw / jogCountPerMm;
                        Log4Net.Info($"点动换算：AXIS={AXIS}, stepMm={Convert.ToDouble(m_sStep):F3}, pulse={position}, encoderRaw={jogEncRaw:F0}, encoderMm={jogEncMm:F3}, countPerMm={jogCountPerMm:F4}");
#endif
                    }

                    //int position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应1000个脉冲
                    //double vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一
                    if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                    {
                        RollerParam = -System.Math.Abs(RollerParam);
                    }
                    else//辊子运动方向为正向：20200925新增：
                    {
                        RollerParam = System.Math.Abs(RollerParam);
                    }
                    int RollerDirection = k_RYSYSParamAutoPrintParamInTest.m_nRollerRotateDirection;//20220527新增：
                    motionMap.TrapMotion(AXIS, ref motionMap.trapPrm, position, vel, RollerParam, RollerDirection, WaitStopFlag);//20220512修改：默认点动运动等停为false，特殊情况下不等停

                    string msg = $"手动正向点动指令（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}},点动量{{{m_sStep}MM}},运动速度{{{m_sVel}MM/s}}";
                    Log4Net.Info(msg);

                    if (OtherThreadUse == true)//调用自其他线程
                    { }
                    else//调用自界面线程
                    {
                        if (MoveUpBtnSelected != null)
                        { MoveUpBtnSelected.Enabled = true;/*//上升按键不可按*/}
                        if (MoveDownBtnSelected != null)
                        { MoveDownBtnSelected.Enabled = true;/*//下降按键不可按*/}
                    }
                }
            }
            else if (m_bMoveModeFlag == false) {/*不执行任何操作*/}
        }
        //(5)点动下降：
        /*************************(b)下降按键*****************************/
        //异常1：关于此下降按键：JOG运动是正常，点动是不正常
        //异常2：点动表现出来：限位之后，按反方向点动没有反应，需要重新点击切换按键进行切换才可以
        //private void TrapMoveDown(object sender, EventArgs e)//下降按键
        public void TrapMoveDown(short AXIS, bool m_bMoveModeFlag, string m_sVel, string m_sStep, bool OtherThreadUse, bool WaitStopFlag)//下降按键
        {
            if (m_bMoveModeFlag == true)
            {
                motionMap.ClrLimitAndAbrupt(AXIS);//增添这一行非常关键。——————对应的trpmotion中间的清楚报警和限位就有点不太必要了
                //读取指定轴的状态。
                motionMap.ReadAxisSate(AXIS);
                //根据读取的指定轴的状态进行下面的判断。
                if (motionMap.axisSateMonitor.FlagNegLimit1 == true)//处于负限位，不进行工作
                { }
                else//不在负限位限位，执行反向点位运动
                {
                    Button MoveUpBtnSelected = null, MoveDownBtnSelected = null;
                    if (OtherThreadUse == true)//调用自其他线程
                    { }
                    else//调用自界面线程
                    {
                        string MoveUpBtn = "MoveUpBtn" + AXIS.ToString();
                        string MoveDownBtn = "MoveDownBtn" + AXIS.ToString();
                        MoveUpBtnSelected = (Button)GetControl(MoveUpBtn);
                        MoveDownBtnSelected = (Button)GetControl(MoveDownBtn);
                        if (MoveUpBtnSelected != null)
                        {
                            MoveUpBtnSelected.Enabled = false;/*//上升按键不可按*/
                            MoveUpBtnSelected.BackColor = Color.Plum;
                        }
                        if (MoveDownBtnSelected != null)
                        {
                            MoveDownBtnSelected.Enabled = false;/*//下降按键不可按*/
                            MoveUpBtnSelected.BackColor = Color.Plum;
                        }
                    }

                    //this.MoveUpBtn1.Enabled = false;//上升按键不可按
                    //this.MoveDownBtn1.Enabled = false;//下降按键不可按
                    //this.MoveUpBtn1.BackColor =Color.Plum;
                    //this.MoveDownBtn1.BackColor = Color.Plum;
                    //OneClickflag = false;
                    ////点位运动前，必要的保证工作
                    ////（1）先重新暂停一下所有的运动
                    ////（2）否则，连击点动，会提示点动运动错误
                    //motionMap.StopMotion(1);
                    double vel = 0;
                    int position = 0;


                    //20220509新增：实现10轴电机的精度控制
                    //20220509新增：实现10轴电机的精度控制
                    if (AXIS == 3)//为步进电机：20220505新增：落粉轴电机-半圈限位
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) * 2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0] / 1000);//20220509新建：考虑到第3轴步进的减速比2：1//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) * 2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0]));//20220509新建：考虑到第3轴步进的减速比2：1//当前细分的脉冲输出数
                    }
                    else if (AXIS == 4)//为步进电机：20220505新增：刮墨轴
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter2[1]) * 1 * (SubDivideCoe2[1] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter2[1]) * 1 * (SubDivideCoe2[1]));//当前细分的脉冲输出数
                    }
                    else if (AXIS == 5)//为步进电机：20260416修改：粉辊2
                    {

                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter2[2]) * 1 * (SubDivideCoe2[2] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter2[2]) * 1 * (SubDivideCoe2[2]));//当前细分的脉冲输出数
                    }


                    else if (AXIS == 6 /*|| AXIS == 7 || AXIS == 8*/)//20220511修复bug:此处应该为else if//为步进电机：20200622新增
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;
                        //vel = Convert.ToDouble(m_sVel) / 125;
                        //position = (int)(Convert.ToDouble(m_sStep) * 1000)/125;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter[0]) * 1 * (SubDivideCoe[0]));//当前细分的脉冲输出数
                    }
                    else if (AXIS == 7 || AXIS == 8)//20260416修改：轴7铺粉车、轴8成型缸，按伺服轴处理
                    {
                        motionMap.trapPrm.acc = 1000;
                        motionMap.trapPrm.dec = 1000;
                        motionMap.trapPrm.velStart = 5;
                        motionMap.trapPrm.smoothTime = 1;

                        vel = Convert.ToDouble(m_sVel);
                        position = (int)(Convert.ToDouble(m_sStep) * 1000);
                    }
                    else//为伺服电机
                    {
                        //执行点动运动
                        motionMap.trapPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值                                                    
                        motionMap.trapPrm.dec = 1000;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                        motionMap.trapPrm.velStart = 5;
                        motionMap.trapPrm.smoothTime = 1;
                        vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一

#if false//20220509新建：
                        if (AXIS == 1/* || AXIS == 2 */|| AXIS == 3)
                        {
                            position = (int)(Convert.ToDouble(m_sStep));//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else if (AXIS == 2)//20210716拟修复：偶发的送粉缸1报热过载的问题：从型号方面考虑
                        {
                            motionMap.trapPrm.acc = 10;//————————————————————待实现，从其他的图形窗口中读取对应的值                                                    
                            motionMap.trapPrm.dec = 10;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                            motionMap.trapPrm.velStart = 0;
                            motionMap.trapPrm.smoothTime = 25;
                            vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一
                            position = (int)(Convert.ToDouble(m_sStep));//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else
                        {
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
#else
                        if (AXIS == 1)//20220509修改：1000pulse/mm//20210716拟修复：偶发的送粉缸1报热过载的问题：从型号方面考虑
                        {
                            motionMap.trapPrm.acc = 10;//————————————————————待实现，从其他的图形窗口中读取对应的值                                                    
                            motionMap.trapPrm.dec = 10;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                            motionMap.trapPrm.velStart = 0;
                            motionMap.trapPrm.smoothTime = 25;
                            vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else if (AXIS == 2)//20220509修改：1000pulse/mm
                        {
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20220509修改：1000pulse/mm//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
                        else//20220509修改：1000pulse/mm
                        {
                            position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                        }
#endif
                    }
                    //int position = (int)(Convert.ToDouble(m_sStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                    //double vel = Convert.ToDouble(m_sVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一

                    if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                    {
                        RollerParam = System.Math.Abs(RollerParam);
                    }
                    else//辊子运动方向为正向：20200925新增：
                    {
                        RollerParam = -System.Math.Abs(RollerParam);
                    }
                    int RollerDirection = k_RYSYSParamAutoPrintParamInTest.m_nRollerRotateDirection;//20220527新增：
                    motionMap.TrapMotion(AXIS, ref motionMap.trapPrm, -position, vel, RollerParam, RollerDirection, WaitStopFlag);

                    string msg = $"手动负向点动指令（8轴运控板卡系统） ====》：输出AXIS{{第{AXIS}轴}},点动量{{{m_sStep}MM}},运动速度{{{m_sVel}MM/s}}";
                    Log4Net.Info(msg);

                    if (OtherThreadUse == true)//调用自其他线程
                    {
                    }
                    else//调用自界面线程
                    {
                        if (MoveUpBtnSelected != null)
                        {
                            MoveUpBtnSelected.Enabled = true;/*//上升按键不可按*/
                            MoveUpBtnSelected.BackColor = Color.Transparent;
                        }
                        if (MoveDownBtnSelected != null)
                        {
                            MoveDownBtnSelected.Enabled = true;/*//下降按键不可按*/
                            MoveUpBtnSelected.BackColor = Color.Transparent;
                        }
                    }
                    //this.MoveUpBtn1.Enabled = true;//上升按键不可按
                    //this.MoveDownBtn1.Enabled = true;//
                    //this.MoveUpBtn1.BackColor = Color.Transparent;
                    //this.MoveDownBtn1.BackColor = Color.Transparent;
                }
            }
            else if (m_bMoveModeFlag == false) {/*不执行任何操作*/}
        }
        /// <summary>
        /// 点动运动开启处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MoveBtn_Click(object sender, EventArgs e)//单击按钮时，判断是什么运动模式，再决定是否执行点动
        {
            //(a-1)初始化6轴运动的所有参数
            InitMovParam();//从主界面读取数据到3大标志位中
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            if (IsRetrofitProtectedTag(myTag) && !EnsureRetrofitReady($"手动点动轴组Tag={myTag}"))
            {
                return;
            }
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)
            {
                case 1:
                    if (m_bMoveModeFlag[0] == true)
                    {
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);//20230508新建：
//#endif
                        TrapMoveUp(1, m_bMoveModeFlag[0], m_sVel[0], m_sStep[0], false, false);
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);//20230508新建：
//#endif
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 2:
                    if (m_bMoveModeFlag[0] == true)
                    {
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);//20230508新建：
//#endif
                        TrapMoveDown(1, m_bMoveModeFlag[0], m_sVel[0], m_sStep[0], false, false);
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);//20230508新建：
//#endif
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 3:
                    if (m_bMoveModeFlag[1] == true)
                    { TrapMoveUp(2, m_bMoveModeFlag[1], m_sVel[1], m_sStep[1], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 4:
                    if (m_bMoveModeFlag[1] == true)
                    { TrapMoveDown(2, m_bMoveModeFlag[1], m_sVel[1], m_sStep[1], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 5:
                    if (m_bMoveModeFlag[2] == true)
                    { TrapMoveUp(3, m_bMoveModeFlag[2], m_sVel[2], m_sStep[2], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 6:
                    if (m_bMoveModeFlag[2] == true)
                    { TrapMoveDown(3, m_bMoveModeFlag[2], m_sVel[2], m_sStep[2], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 7:
                    if (m_bMoveModeFlag[3] == true)
                    { TrapMoveUp(4, m_bMoveModeFlag[3], m_sVel[3], m_sStep[3], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 8:
                    if (m_bMoveModeFlag[3] == true)
                    { TrapMoveDown(4, m_bMoveModeFlag[3], m_sVel[3], m_sStep[3], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 9:
                    if (m_bMoveModeFlag[4] == true)
                    { TrapMoveUp(5, m_bMoveModeFlag[4], m_sVel[4], m_sStep[4], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 10:
                    if (m_bMoveModeFlag[4] == true)
                    { TrapMoveDown(5, m_bMoveModeFlag[4], m_sVel[4], m_sStep[4], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 11:
                    if (m_bMoveModeFlag[5] == true)
                    { TrapMoveUp(6, m_bMoveModeFlag[5], m_sVel[5], m_sStep[5], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 12:
                    if (m_bMoveModeFlag[5] == true)
                    { TrapMoveDown(6, m_bMoveModeFlag[5], m_sVel[5], m_sStep[5], false, false); }
                    else {/*不执行任何操作*/}
                    break;

                case 13://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == true)
                    { TrapMoveUp(7, m_bMoveModeFlag[6], m_sVel[6], m_sStep[6], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 14://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == true)
                    { TrapMoveDown(7, m_bMoveModeFlag[6], m_sVel[6], m_sStep[6], false, false); }
                    else {/*不执行任何操作*/}
                    break;

                case 15://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == true)
                    { TrapMoveUp(8, m_bMoveModeFlag[7], m_sVel[7], m_sStep[7], false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 16://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == true)
                    { TrapMoveDown(8, m_bMoveModeFlag[7], m_sVel[7], m_sStep[7], false, false); }
                    else {/*不执行任何操作*/}
                    break;
            }
        }
        /*****************************************打开MAxis运动监测****************************************/
        private void OpenMAxisStateMonitor()//开启6轴轴运动监控
        {
            //(b1)反转对应的标志位
            //(b2)根据ID反转颜色状态
            //(c)打开DY轴运动监控
            StartUpadateMAixsMoveStatus();/*开启DY运动监控刷新*/

        }
        //刷新虚拟打印编码器状态显示定时器
        public/*private*/ System.Windows.Forms.Timer TimerMAxis = null;//刷新虚拟打印编码器状态显示定时器
        private void StartUpadateMAixsMoveStatus()//开启6轴轴MOVE限位信号
        {
            //(1)刷新MAxis运动监测显示定时器
            TimerMAxis = new System.Windows.Forms.Timer() { Interval = 300 };//间隔100ms刷新MAxis的状态值
            TimerMAxis.Tick += new EventHandler(TimerMAxis_Tick);
            TimerMAxis.Start();
        }
        private void TimerMAxis_Tick(object sender, EventArgs e)//刷新6轴对应的限位状态
        {
            Int32[] nIOState = new Int32[8];//20200110：6轴的状态位//20200903批注：更新到8轴
            //UInt32 nxpos, ny1pos, ny2pos;//不需要
            //UInt32 nxAxis, nyAxis1, nyAxis2;//不需要
            //////////////////////////////////(a)实时的轴编码器位置
            //////////////////////////////////(b)剩余脉冲数获取
            //////////////////////////////////(c)实时的轴限位状态
            for (short AXIS = 1; AXIS <= 8; AXIS++)//20200903批注：更新到8轴
            {
                nIOState[AXIS - 1] = motionMap.MointoringAxis2(AXIS);
            }
            //////////////////////////////////(c)实时的多轴报警状态
            /////////(1)次序为P，Z，N 限位
            if (0 != (nIOState[0] & 0x20))//是否有报警：有限位报警
            { this.PLLabel1.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel1.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[0] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel1.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel1.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[0] & 0x40))//有报警
            { this.NLLabel1.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel1.BackColor = Color.LimeGreen; }

            /////////(2)次序为P，Z，N 限位
            if (0 != (nIOState[1] & 0x20))//是否有报警：有限位报警
            { this.PLLabel2.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel2.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[1] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel2.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel2.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[1] & 0x40))//有报警
            { this.NLLabel2.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel2.BackColor = Color.LimeGreen; }

            /////////(3)次序为P，Z，N 限位
            if (0 != (nIOState[2] & 0x20))//是否有报警：有限位报警
            { this.PLLabel3.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel3.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[2] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel3.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel3.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[2] & 0x40))//有报警
            { this.NLLabel3.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel3.BackColor = Color.LimeGreen; }

            /////////(4)次序为P，Z，N 限位
            if (0 != (nIOState[3] & 0x20))//是否有报警：有限位报警
            { this.PLLabel4.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel4.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[3] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel4.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel4.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[3] & 0x40))//有报警
            { this.NLLabel4.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel4.BackColor = Color.LimeGreen; }

            /////////(5)次序为P，Z，N 限位
            if (0 != (nIOState[4] & 0x20))//是否有报警：有限位报警
            { this.PLLabel5.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel5.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[4] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel5.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel5.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[4] & 0x40))//有报警
            { this.NLLabel5.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel5.BackColor = Color.LimeGreen; }

            /////////(6)次序为P，Z，N 限位
            if (0 != (nIOState[5] & 0x20))//是否有报警：有限位报警
            { this.PLLabel6.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel6.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[5] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel6.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel6.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[5] & 0x40))//有报警
            { this.NLLabel6.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel6.BackColor = Color.LimeGreen; }

            /////////(7)次序为P，Z，N 限位//20200903批注：更新到8轴
            if (0 != (nIOState[6] & 0x20))//是否有报警：有限位报警
            { this.PLLabel7.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel7.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[6] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel7.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel7.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[6] & 0x40))//有报警
            { this.NLLabel7.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel7.BackColor = Color.LimeGreen; }

            /////////(6)次序为P，Z，N 限位//20200903批注：更新到8轴
            if (0 != (nIOState[7] & 0x20))//是否有报警：有限位报警
            { this.PLLabel8.BackColor = Color.Red; }
            else//无报警
            { this.PLLabel8.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[7] & 0x00))//有报警————2010110:固高控制器不接零限位信号，掩码取值20
            { this.ZeroLabel8.BackColor = Color.Red; }
            else//无报警
            { this.ZeroLabel8.BackColor = Color.LimeGreen; }
            if (0 != (nIOState[7] & 0x40))//有报警
            { this.NLLabel8.BackColor = Color.Red; }
            else//无报警
            { this.NLLabel8.BackColor = Color.LimeGreen; }

            double[] g_dEncpos = new double[8];
            g_dEncpos = motionMap.GetEncPos();
            double PosValue = g_dEncpos[3] / 1000;//铺粉位置
            string PositonText = "铺粉车: " + PosValue.ToString("F1") + " MM";
            PowerPosLable.Text = PositonText;//202001021新增位置监测：
            double CurrentPos = GetCurrentPos(1);//初始编码器位置：
            PosValue = CurrentPos;
            PositonText = "墨车: " + PosValue.ToString("F1") + " MM";
            InkCarPosLable.Text = PositonText;//202001021新增位置监测：

        }
        /*****************************************************喷墨手动控制部分——20200109*****************************************************/
        /*****************************************************喷墨手动控制部分——20200109*****************************************************/
        /*****************************************************喷墨手动控制部分——20200109*****************************************************/
        /// <summary>
        /// 喷墨手动控制部分——20200109
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenDEV_Click(object sender, EventArgs e)
        {
            IntPtr ParenthWnd = new IntPtr(0);//获取主窗口句柄
            //获取主窗口句柄:通过第三方dll获取
            ParenthWnd = royal.royal.FindWindow(null, "LASERADD-BinderJetter");
            //int nRetVal = royal.royal.DEV_OpenDevice(ParenthWnd, "F:\\RoyalOutput\\x64\\");//文件路径方式1：
            //int nRetVal = royal.royal.DEV_OpenDevice(ParenthWnd, @"F:\RoyalOutput\x64\");//文件路径方式2：
            //int nRetVal = royal.royal.DEV_OpenDevice(ParenthWnd, "C:\\Users\\SummerGhost\\Documents\\Visual Studio 2017\\Projects\\LaserAdd_3DP_Software\\1-3DP主控界面(人机交互模块)\\bin\\Debug\\");
            // 2026-02-02修改：墨车轴切换到固高控制（4轴卡测试），注释Royal设备初始化--2360~2368行
            // int nRetVal = royal.royal.DEV_OpenDevice(ParenthWnd,
            //     System.Windows.Forms.Application.StartupPath + @"\"
            //     /*@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\Debug\"*/);
            // if (nRetVal > 0)
            // {
            //     string sztxt;
            //     sztxt = string.Format("失败：{0:X00000000}", nRetVal);
            //     MessageBox.Show(sztxt);
            // }
        }

        private void DEV_UpdateParam_Click(object sender, EventArgs e)
        {
            royal.royal.g_sys_param.nParamVer = 0x21080809;
            ////这个地方比较关键：————Marshal.sizeof，处理的必须是非托管区的数据：(1)是否必要；（2）这一部分，需从底层更新上来——没啥用
            //royal.royal.sys_param.nParamSize = Marshal.sizeof(LPRYSYS_PARAM);
            royal.royal.g_sys_param.szWavePath =
                System.Windows.Forms.Application.StartupPath +
                @"\波形文件\ricoh_phcfg16.rhdat"/*"金属3DP打印ccccccd"*/;//仅为测试
            // 2026-02-02修改：墨车轴切换到固高控制（4轴卡测试），注释Royal参数更新
            // bool returnST = royal.royal.DEV_UpdateParam(ref royal.royal.g_sys_param);
            // if (returnST == false)
            // {
            //     string sztxt;
            //     sztxt = "更新设备参数失败";
            //     MessageBox.Show(sztxt);
            // }
        }

        private void DEV_InitDevice_Click(object sender, EventArgs e)
        {
            UInt32 nSysInitEncVal = 0x100000;//此值来自运动系统当前X编码——非常关键：安全性至关重要
            //距离Smm转换为Encode,DPI为扫描方向光栅DPI
            //Encode=((S/25.4)*DPI)

// 2026-02-02修改：墨车轴切换到固高控制（4轴卡测试），注释Royal设备初始化
            // UInt32 nRetVal = royal.royal.DEV_InitDevice(nSysInitEncVal);
            // if (nRetVal < 0)
            // {
            //     string sztxt;
            //     sztxt = String.Format("失败：{0:X00000000}", nRetVal);
            //     MessageBox.Show(sztxt);
            // }
        }

        private void DEV_GetDeviceInfo_Click(object sender, EventArgs e)
        {
            //（c）不返回指针的API测试
            royal.LPPRINTER_INFO pSysInfo = new royal.LPPRINTER_INFO();//命名空间注意
            bool nRetVal = royal.royal.DEV_GetDeviceInfo2(ref pSysInfo);
        }

        private void DEV_AdibControl_Click(object sender, EventArgs e)
        {
            royal.LPADIB_PARAM lParam = new royal.LPADIB_PARAM();
            UInt32 nOption = 110;
            bool bSetParam = false;
            bool bAbort = false;
            bool nRetVal = royal.royal.DEV_AdibControl(ref lParam, nOption, bSetParam, ref bAbort);
        }

        private void SetPwmParam_Click(object sender, EventArgs e)
        {
            //UInt32 SetEncoderPosValue = 1;
            bool nRetVal = royal.royal.DEV_SetPwmParam(true, 0.001f, 0.001f);//设置UV灯功率
        }
        bool InkPumpFlag = false;
        private void SetInkPump_Click(object sender, EventArgs e)
        {
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

        }

        private void SetUsbOutput_Click(object sender, EventArgs e)
        {
            //UInt32 nInkMask = /*0xFF*/0b001011;//6路输出
            //UInt32 nValidBitMask = 0b10000;
            UInt32 nInkMask = /*0xFF*/1;//6路输出
            UInt32 nValidBitMask = 0b10001;
            bool nRetVal = royal.royal.DEV_SetUsbOutPut(nInkMask/*, nValidBitMask*/);//设置保留输出 bit[0]~bit[5]  EO1~EO6	J7~J12————9录得保留输出
        }

        private void ResetPrintEncoder_Click(object sender, EventArgs e)
        {
            UInt32 SetEncoderPosValue = 20191228;
            bool nRetVal = royal.royal.DEV_ResetPrintEncoder(SetEncoderPosValue);
        }

        public bool m_bInkSuppy = false;//自动使能供墨标志位
        private void DEV_EnableInkAutoSupply_Click(object sender, EventArgs e)//20200715修改：使能所有供墨系统
        {
            //UInt32 nInkMask = 0xFF;//二进制：11111111——八路输出
            //bool nRetVal = royal.royal.DEV_EnableInkAutoSupply(false, nInkMask);

            //（2）使能自动供墨：可以封装成1个函数
            //bool m_bInkSuppy = false;//自动使能供墨标志位
            if (m_bInkSuppy == false)
            {
                uint ControlBit = 0xFF/*8*//*0xFF*//*0xFF*/;//方便测试工作：20200417批注//20200714修改：只使能第4路墨水的自动供墨
                bool nRetVal = royal.royal.DEV_EnableInkAutoSupply(true, ControlBit);

                string msg = $"开启自动供墨：DEV_EnableInkAutoSupply：ReturnCode{{{nRetVal}}},Action{{true}},ControlBit{{{ControlBit}}}";
                Log4Net.Info(msg);

                if (nRetVal == true)
                {
                    m_bInkSuppy = true;

                    DEV_EnableInkAutoSupply.BackColor = Color.LimeGreen;
                    DEV_EnableInkAutoSupply.ForeColor = Color.Black/*White*/;
                }
            }
            else
            {
                uint ControlBit = 0xFF/*8*/;//方便测试工作：20200417批注
                //::DEV_EnableInkAutoSupply(TRUE,0);
                bool nRetVal = royal.royal.DEV_EnableInkAutoSupply(false, ControlBit);//20200714修改：只使能第4路墨水的自动供墨

                string msg = $"停止自动供墨：DEV_EnableInkAutoSupply：ReturnCode{{{nRetVal}}},Action{{false}},ControlBit{{{ControlBit}}}";
                Log4Net.Info(msg);

                if (nRetVal == true)
                {
                    m_bInkSuppy = false;
                    DEV_EnableInkAutoSupply.BackColor = Color.White;
                    DEV_EnableInkAutoSupply.ForeColor = Color.Black/*White*/;
                }
            }
        }

        private void button29_Click(object sender, EventArgs e)
        {

        }

        private void GetOutputIO_Click(object sender, EventArgs e)
        {
            ////UInt32 OutPutIoState = royal.royal.DEV_GetOutputIO();//20200305测试显示存在异常。————作用不大， 临时注释
        }

        private void GetUsbOutput_Click(object sender, EventArgs e)
        {
            UInt32 GetUsbOutput = 0;
            GetUsbOutput = royal.royal.DEV_GetUsbOutput();
            //string sztxt= string.Format("失败：{0:X00000000}", GetUsbOutput);
            string sztxt = System.Convert.ToString(GetUsbOutput, 2);//2进制显示
            MessageBox.Show("车头板的保留输出状态为：" + sztxt);
        }

        private void GetPrintEncoder_Click(object sender, EventArgs e)
        {
            UInt32 readEncoderPosValue = royal.royal.DEV_GetPrintEncoderValue();
            MessageBox.Show("设备的编码器值是：" + Convert.ToString(readEncoderPosValue));
        }

        private void GetUsbInput_Click(object sender, EventArgs e)
        {
            //UInt32 GetUsbInput = 0;
            //GetUsbInput = royal.royal.DEV_GetUsbInput();
        }

        private void button28_Click(object sender, EventArgs e)
        {

        }

        private void button53_Click(object sender, EventArgs e)
        {
            bool OutPutIoState = royal.royal.DEV_CloseDevice();//待测试
        }
        private void ReloadWaveForm_Click(object sender, EventArgs e)
        {
#if false//20230323临时注释
            //该函数用来加载参数结构RYSYS_PARAM中的drvWaveForm[MAX_DRV_CNT]的返回值
            //0 夹杂及成功
            //1 车头卡1 负载驱动卡存在波形加载失败
            //2 车头卡1 负载驱动卡存在波形加载失败
            int nRetVal = royal.royal.DEV_ReloadWaveForm();//当前X轴信号状态
            if (nRetVal > 0) { MessageBox.Show("波形加载失败！"); }
            royal.LPPRINTER_INFO pSysInfo = new royal.LPPRINTER_INFO();
            bool nRetVal2 = royal.royal.DEV_GetDeviceInfo2(ref pSysInfo);
#endif
        }
        private void DEV_CloseDevice_Click(object sender, EventArgs e)
        {
            bool OutPutIoState = royal.royal.DEV_CloseDevice();//待测试
        }
        private void DEV_SetVitualPrint_Click(object sender, EventArgs e)
        {

        }

        private void SetValidDrvMask_Click(object sender, EventArgs e)
        {
            //(b)方式2：bool__stdcall DEV_GetDeviceInfo(LPPRINTER_INFO pDevInfo);
            royal.LPPRINTER_INFO pPrinterInfo = new royal.LPPRINTER_INFO();
            bool nRetVal0 = royal.royal.DEV_GetDeviceInfo2(ref pPrinterInfo);//20200304修改：

            string nMainFpgaVer = string.Format("{0:X}", pPrinterInfo.nMainFpgaVer);//方法2：

            UInt32 nDrvMask = pPrinterInfo.nDrvValidMask[0];//车头卡1为例
            bool nRetVal = royal.royal.DEV_SetValidDrvMask(0, nDrvMask);//正常打印时设置的值
            bool nRetVa2 = royal.royal.DEV_SetValidDrvMask(0, (UInt32)(nDrvMask & (~0x2)));//正常打印时设置的值——1为0x1
        }

        private void EnableDrvOutput_Click(object sender, EventArgs e)
        {
            bool nRetVal1 = royal.royal.DEV_EnableDrvOutput(0, 0, true);
            bool nRetVal2 = royal.royal.DEV_EnableDrvOutput(0, 0, false);
        }

        private void SartPrintJob_Click(object sender, EventArgs e)
        {
#if false
            royal.royal.g_PrtJobItem.nJobID = 0;
            royal.royal.g_PrtJobItem.nPixelGrayBits = 1;
            royal.royal.g_PrtJobItem.nPrtXEncPos = 0x100018;
            royal.royal.g_PrtJobItem.szJobName = "金属3DP打印";
            if (MeteorPrintEngine.StartJob(ref royal.royal.g_PrtJobItem) < 0)
            {
                MessageBox.Show("Cant Print");
            }
            else
            {
                //AfxBeginThread(A);//上位机开启图像处理线程
                //AfxBeginThread(b);//上位机开启打印管理线程
            }
#endif
        }
        /// <summary>
        /// 传输数据测试;传输BMP格式，载入1层的BMP数据//20200409批注：内存中的bmp文件的存储方式是从上到下，从左到右；BMP文件的存储方式是从下到上，从左到右；           
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WriteImgLayerData_Click(object sender, EventArgs e)//必须放在1个独立的线程中//文件的本质就是保存在HD的字节流
        {
            /*****************************************新的有效方法*****************************************/
            /*****************************************新的有效方法*****************************************/
            //(壹)  使用Bitmap内置的文件流解析单点BMP数据//(A):第一步，完成图片文件的解析成字节流：
            //(壹)  使用Bitmap内置的文件流解析单点BMP数据
            Bitmap processedBitmap = new Bitmap(@"C:\Users\SummerGhost\Documents\" +
                @"Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\" +
                @"bin\x64\Debug\JOB输出文件\0.bmp");
            //(贰)校验传输的数据是否准确：20200409新增
            //(贰)校验传输的数据是否准确：20200409新增
            if (processedBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format1bppIndexed)
            {
                MessageBox.Show("目前不支持非单点图像的打印");
                return;//退出程序
            }

            //(叁)图像取反处理：20200409新增
            //(贰)图像取反处理：20200409新增
            // （1）processedBit//执行必要的位操作
            // （1）processedBit// Lock the bitmap's bits.  map.LockBits();//锁定到内存
            Rectangle rect = new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height);
            System.Drawing.Imaging.BitmapData bmpData = processedBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, processedBitmap.PixelFormat);
            // （2）Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;
            // （3）Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bmpData.Stride) * processedBitmap.Height;//the size of BitmapData
            byte[] rgbValues = new byte[bytes];
            // （4）Copy the RGB values into the array.
            Marshal.Copy(ptr, rgbValues, 0, bytes);/*System.Runtime.InteropServices.*/
            // （5）Set every third value to the opposite value
            for (int counter = 0; counter < bytes; counter++)
                rgbValues[counter] = (byte)~(rgbValues[counter]);
            // （6）Copy the RGB values back to the bitmap
            Marshal.Copy(rgbValues, 0, ptr, bytes);/*System.Runtime.InteropServices.*/


            //（肆） 保存附带的所有必要的BMP数据
            //（肆） 处理图层信息
            royal.royal.g_prtimg_layer.nXDPI = 635;//图像的XDPI，本质必须与光栅的DPI保持协调
            royal.royal.g_prtimg_layer.nYDPI = 600;//图像的XDPI，本值必须与喷头的DPI保持一致
            royal.royal.g_prtimg_layer.nBytesPerLine = bmpData.Stride;//bmpData每行的数据字节数
            royal.royal.g_prtimg_layer.nWidth = processedBitmap.Width;//bmpData的像素宽度
            royal.royal.g_prtimg_layer.nHeight = processedBitmap.Height;//bmpData的像素高度
            int i = 1;//第1层的数据：20200409新增：具体实现的时候，会移植到为爱面
            royal.royal.g_prtimg_layer.nLayerIndex = i;//发送图层的序号
            royal.royal.g_prtimg_layer.nColorCnts = 1;//颜色个数，打印图形颜色为单色
            royal.royal.g_prtimg_layer.nPrtDir = 1;//起始打印方向为增序：光栅计数增大的方向开始计数
            royal.royal.g_prtimg_layer.nPrtFlag = 1;//双向打印 bit[0] 控制单双向打印

            //（伍） 完成数据的传输
            //（伍） 完成数据的传输
            if (!MeteorPrintEngine.SendStartJob(0, (uint)processedBitmap.Width))
            {
                processedBitmap.UnlockBits(bmpData);
                processedBitmap.Dispose();
                return;
            }
            int nRet = -1;//默认的数据为-1；
            do
            {
                nRet = MeteorPrintEngine.WriteImageLayer(ref royal.royal.g_prtimg_layer, ptr, bytes);
                if (nRet > 0)
                {
                    break;
                }
                else//20200409批注：分析错误号，做出相应处理：提示或者其他处理均可以
                {
                    switch (nRet)
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
                    }
                }
            } while (nRet <= 0);

            //（陆） 释放对应的数据
            //（陆） 释放对应的数据
            // （7）Unlock the bits.
            processedBitmap.UnlockBits(bmpData);
            MeteorPrintEngine.SendEndJob();

            //反色测试：20200409新增
            processedBitmap.Save(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\输出-反色-2.bmp", System.Drawing.Imaging.ImageFormat.Bmp);//————保存到BMP文件:20200408修改
            processedBitmap.Dispose();//及时释放掉clone：20200408新增

            //（柒） 用不着
            //（柒） 用不着
            // （8）Another edit way: Draw the modified image.//modified the BMP file with graphics in VC
            //e.Graphics.DrawImage(bmp, 0, 150);

            /*****************************************旧的有效方法*****************************************/
            /*****************************************旧的有效方法*****************************************/
            //////(1-旧)单纯的文件流解析数据
            ////string path = "";//测试图片的位置
            ////FileStream files = File.OpenRead(path); //OpenRead          
            ////int filelength = 0;//图片文件的字节长度（8位byte）
            ////filelength = (int)files.Length; //获得文件长度 
            ////Byte[] imageData = new Byte[filelength]; //建立一个字节数组————关键在这里————读取到了image这一byte[]数组内存中
            ////files.Read(imageData, 0, filelength); //按字节流读取 
            ////System.Drawing.Image result = System.Drawing.Image.FromStream(files);
            ////files.Close();
            //////（B）从Byte[]中解析出pSrcBuff[]和nBytes/biWidth/biHeight/nColors/nPrtDir/nPrtFlag
            //////1 解析文件头方法//2 解析图像数据方法//3 解析文件数据方法
            ////IntPtr LPBYTE = Marshal.AllocHGlobal(filelength + 1024);
            ////Marshal.Copy(imageData, 0, LPBYTE, 1000);
            //////将此部分移动到指针区域     

            //////（A）数组初始化pSrcBuf：unsigned char[]赋值方式3：(其他方式传值相同)
            ////// Initialize unmanaged memory to hold the array.
            ////string tempstr = "我是吴世彪，i am wushibiao";
            ////byte[] tempSrcBuf = System.Text.Encoding.Default.GetBytes(tempstr);
            ////int size = Marshal.SizeOf(tempSrcBuf[0]) * tempSrcBuf.Length;
            ////IntPtr pSrcBuf = Marshal.AllocHGlobal(size);

            //////（B）初始化Buf1：测试读取的的prtimg_layer的指针区
            ////IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf((royal.royal.g_prtimg_layer)));
            ////try//我当初为什么要加异常处理？：避免内存不足的危险
            ////{
            ////    //初始化结构体2：
            ////    royal.royal.g_prtimg_layer.nLayerIndex = 1;
            ////    royal.royal.g_prtimg_layer.nPrtFlag = 2;
            ////    royal.royal.g_prtimg_layer.nReserved = new int[8];
            ////    royal.royal.g_prtimg_layer.nReserved[0] = 1111;
            ////    royal.royal.g_prtimg_layer.nReserved[1] = 2222;
            ////    royal.royal.g_prtimg_layer.nReserved[7] = 8888;
            ////    ////（A）初始化结构体1的BUFF1：
            ////    //Marshal.StructureToPtr(royal.royal.prtimg_layer, buffer, false);
            ////    //（B）初始化结构体1的BUFF2：Copy the array to unmanaged memory.
            ////    Marshal.Copy(tempSrcBuf, 0, pSrcBuf, tempSrcBuf.Length);

            ////    //(a)测试C++的DLL:
            ////    if (royal.royal.IDP_WriteImgLayerData(ref royal.royal.g_prtimg_layer, pSrcBuf, size) == 0)//
            ////    {
            ////        MessageBox.Show("作业启动失败");
            ////    }
            ////    //测试C++操作指针修改的区域
            ////    //royal.royal._passItem1 = (LPPassDataItem)Marshal.PtrToStructure(
            ////    //            buffer, typeof(LPPassDataItem));
            ////    ////测试非托管区结构体指针内容复制到托管区结构体
            ////    //LPPassDataItem personRes = (LPPassDataItem)Marshal.PtrToStructure(
            ////    //    royal.royal._passItem.pNextItem, typeof(LPPassDataItem));
            ////}
            ////catch (Exception) { }
            ////finally
            ////{
            ////    Marshal.FreeHGlobal(pSrcBuf);
            ////    Marshal.FreeHGlobal(buffer);
            ////}
            /*****************************************旧的有效方法*****************************************/
            /*****************************************旧的有效方法*****************************************/
        }

        private void GetPrintState_Click(object sender, EventArgs e)
        {
            royal.LPPrtRunInfo RTinfo = new royal.LPPrtRunInfo();
            bool nRetVal = MeteorPrintEngine.GetPrintState(ref RTinfo);
            if (1 == RTinfo.nPrtState)
            {
                MessageBox.Show("当前PASS打印进行中");
            }
            else if (3 == RTinfo.nPrtState)
            {
                MessageBox.Show("当前系统正在闪喷");
            }
            else if (0 == RTinfo.nPrtState)
            {
                MessageBox.Show("系统空闲");
            }
        }
        /// <summary>
        /// 获取PASS信息测试
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GetPassItem_Click(object sender, EventArgs e)//必须放在1个独立线程中:和运动主线程是相同的
        {
            royal.LPPassDataItem pPrtPassDes = new royal.LPPassDataItem();

            UInt32 nLayerIndex = 0;//总共4pass,序号是0Pass
            int nPassCount = 4;
            for (int i = 0; i < nPassCount; i++)
            {
                // bool nRetVal = royal.royal.IDP_GetPassItem(nLayerIndex, i, ref/*ref*/ pPrtPassDes);//获取Pass
                //if ( pPrtPassDes!=null)
                //{
                //    royal.royal.IDP_DoPassPrint(ref pPrtPassDes);
                //}
                bool nRetVal2 = royal.royal.IDP_DoPassPrint(ref pPrtPassDes);//打印指定的第i Pass
            }
        }
        /// <summary>
        /// 执行第2种打印方式打印
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoPassPrint2_Click(object sender, EventArgs e)//必须放在1个独立线程中：和运动主线程是相同的
        {
            royal.LPPassDataItem nPrtPassDes = new royal.LPPassDataItem();
            UInt32 nLayerIndex = 0;//上位机已经添加过的涂层索引号
            int nPassCount = 4;//IDP_WriteImgLayerData的返回值
            for (int i = 0; i < nPassCount; i++)
            {
                bool nRetVal = MeteorPrintEngine.TriggerPass(nLayerIndex, i);
                if (nRetVal != false)
                {
                    //查看打印状态、作相应处理
                }
                else
                {
                    //bool nRetVal2 = royal.royal.IDP_GetPassItem(nLayerIndex, i,ref /*ref*/ nPrtPassDes);
                    //if (nRetVal2 != false)
                    //{
                    //    //计算当前PASS运动系统需要的参数
                    //}
                }
            }//计算当前PASS运动系统需要的参数
        }


        private void DoPassPrint_Click(object sender, EventArgs e)
        {
            royal.LPPassDataItem nPrtPassDes = new royal.LPPassDataItem();
            UInt32 nLayerIndex = 0;//上位机已经添加过的涂层索引号
            int nPassCount = 4;//
            for (int i = 0; i < nPassCount; i++)
            {
                //bool nRetVal = royal.royal.IDP_GetPassItem(nLayerIndex, i, ref/*ref*/ nPrtPassDes);//获取Pass
                //if (nRetVal != false)
                //{
                //    bool nRetVal2 = royal.royal.IDP_DoPassPrint(ref nPrtPassDes);//打印指定的第i Pass
                //}
            }//计算当前PASS运动系统需要的参数

            int nYLastJetOff = nPrtPassDes.nMinJet0ImgLinePos;//上Pass的行偏移数
            int nYCurJetOff = nPrtPassDes.nMinJet0ImgLinePos;//当前Pass的行偏移数
            UInt32 nXStartPos = nPrtPassDes.nStartEncPos;//当前PASS打印起始编码值
            UInt32 nXValidCol = nPrtPassDes.nValidPrtCols;//当前PASS打印的有效列数
            float/*UInt32*/ nPrtcess = nPrtPassDes.nPrtPrecession;//光栅打印分频值//20230511修改：修改为浮点数
            UInt32 nXEndPos = 0;//X打印结束编码值
            if (nPrtPassDes.bPrtDir == true)
            {
                nXEndPos = (UInt32)(nXStartPos + nPrtcess * nXValidCol);//计算PASS打印的中止编码值
            }
            else
            {
                nXEndPos = (UInt32)(nXStartPos + nPrtcess * nXValidCol);//计算PASS打印的中止编码值
            }//X车头的运动范围，图像区域的起点是nXStartPos,到终点nXEndPos，两边需要加上缓冲区
            int nYDPI = 300;//图层Y向DPI,当前系统定义的打印方式，必须等于喷头DPI
            float fYmmStepSize = ((nYCurJetOff - nYLastJetOff) / nYDPI) * 25.4f;
            //Y轴运动需要运动的距离 mm
            //float fYmmStepSize = ((nYCurJetOff - nYLastJetOff) / nYDPI) * 25.4 mm;表示差值
        }


        /// InitShoveInk控件初始化：
        bool PausePassBtnFlag = false;
        private void InitPauseBtn()//初始化为关闭状态
        {
            ////(0)初始化控件列表的对应值
            //for (int i = 0; i < 10; i++)
            //{ RoyalMap.m_bEnable[i] = false; }
        }
        private void PausePassPrint_Click(object sender, EventArgs e)
        {
            if (PausePassBtnFlag == false)
            {
                bool nRetVal1 = royal.royal.IDP_PausePassPrint(true);//设备运动时，停止 
            }
            else
            {
                bool nRetVal2 = royal.royal.IDP_PausePassPrint(false);//设备就绪时执行
            }
        }

        private void StopPrintJob_Click(object sender, EventArgs e)
        {
#if false
            bool nRetVal = MeteorPrintEngine.StopJob();
#endif
        }

        private void FlashPrtCtl_Click(object sender, EventArgs e)
        {
            bool nRetVal = MeteorPrintEngine.SetFlash(true);
        }

        /*******************************挤墨控件集体控制初始化***************************/
        private royal.RoyalPrintingMap _RoyalMap;
        private royal.RoyalPrintingMap RoyalMap => _RoyalMap ?? (_RoyalMap = new royal.RoyalPrintingMap());//创建GoogolMotionMap对象，供本窗口调用
        // 墨车 X/Y（固高轴1/2）与八轴点动共用同一套 motionMap（见 InitCardConfiguration 下发的 cfg），勿再使用第二套 GoogolMotionMap 实例发脉冲。
        ///// InitShoveInk控件初始化：————修改为static使用
        public void InitShoveInk(UInt32 nValveStateMask)/*private void InitShoveInk()*///20200718新建：初始化挤墨控件
        {
            ////初始化控件列表的对应值
            //for (int i=0;i<10;i++)
            //{
            //    RoyalMap.m_bEnable[i] = false;
            //}            
            //(0)初始化控件列表的对应值
            for (int i = 0; i < 8; i++)//20200718新增：
            {
                //从掩码中提取对应的位状态
                int nCtlValue = (1 << (i));
                string BtnName = "ValveBtn" + i;

                if ((nValveStateMask & nCtlValue) == 0)//掩码校验
                {
                    RoyalMap.m_bEnable[i] = false;//20200718新建批注：执行关闭动作
                    Button ValveBtnSelected = (Button)GetControl(BtnName);
                    if (ValveBtnSelected != null)
                    {
                        ValveBtnSelected.BackColor = Color.WhiteSmoke;
                    }
                }
                else
                {
                    RoyalMap.m_bEnable[i] = true; //20200718新建批注：/执行打开动作
                    Button ValveBtnSelected = (Button)GetControl(BtnName);
                    if (ValveBtnSelected != null)
                    {
                        ValveBtnSelected.BackColor = Color.LimeGreen;
                    }
                }
            }
        }
        private void ShoveInk_Click(object sender, EventArgs e)
        {
            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);
            //(b1)根据ID反转并存储到对应控件列表
            RoyalMap.m_bEnable[myTag - 1] = !RoyalMap.m_bEnable[myTag - 1];
            //(b2)根据ID反转颜色状态
            if ((sender as Control).BackColor == Color.LimeGreen)
            { (sender as Control).BackColor = Color.WhiteSmoke/*Tomato*/; }//20200715批注修改：
            else
            { (sender as Control).BackColor = Color.LimeGreen; }

            //(c)计算掩码
            int nInkMask = 0;
            for (int i = 0; i < 8; i++)
            {
                if (RoyalMap.m_bEnable[i])//变量在这里。
                    nInkMask |= (1 << i);
            }
            //RoyalMap.DEV_ShoveInk(0, nInkMask);//DLL中没有对应的接口
            //royal.royal.DEV_ShoveInk(0, (UInt32)nInkMask);//暂时用不了
            bool returncoded = royal.royal.DEV_SetMcbOutPut(0, (UInt32)nInkMask);//暂时用不了
        }
        /*******************************墨量剩余信号集控显示***************************/
        public/*private*/ System.Windows.Forms.Timer Timer3 = null;//刷新墨量显示状态定时器
        bool m_FlagUpdateAdibInfo = false;//默认不刷新温度、电压、负压：20200403新增

        ////（1）刷新液位报警信号
        //InkStateCtrl inkStateCtrl = new InkStateCtrl();//20200401删除：
        LaserADD_BinderJetter.InkStateCtrl inkStateCtrl = new LaserADD_BinderJetter.InkStateCtrl();//20200401新增：
        Bitmap inkState = new Bitmap(500, 100);
        Rectangle rectangle = new Rectangle();
        private void Timer3_Tick(object sender, EventArgs e)//刷新墨量显示状态定时器
        {
            ////（0）首先把数据刷上来
            //UInt32 nInkMask = royal.royal.DEV_GetInputIO() >> 16;
            UInt32 nTempInkMask = royal.royal.DEV_GetInput();//我估计不够

            UInt32 nInkMask = ((nTempInkMask >> 16) & 0b1111)
                | ((nTempInkMask >> 10) & 0b10000)
                | ((nTempInkMask >> 10) & 0b100000)
                | ((nTempInkMask >> 19) & 0b1000000);

            ////（1）刷新液位报警信号
            //inkStateCtrl.SetInkCount(10, 0);
            //inkStateCtrl.SetInkState((int)nInkMask);//很关键
            //inkState.SetPixel(pictureBox2.Width, pictureBox2.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
            //Graphics g = Graphics.FromImage(inkState);
            //g.Clear(pictureBox2.BackColor);
            //rectangle.Width = pictureBox2.Width; rectangle.Height = pictureBox2.Height;//rectanle是bitmap的大小
            //inkStateCtrl.OnPaint(g, rectangle);
            //pictureBox2.CreateGraphics().DrawImage(inkState, new Point(0, 0));
            //pictureBox2.Image = inkState;
            ////g.Dispose();

            ////（1）刷新剩余墨量信号//20200331新增：
            inkStateCtrl.SetInkCount(7, 0);//20200220新建：//依次是1级墨盒，2级墨x4，1级清洗液x1，空气保护瓶x1
            inkStateCtrl.SetInkState((int)nInkMask);//很关键//20200220新建：
            inkState.SetPixel(pictureBox2.Width, pictureBox2.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
            Graphics g3 = Graphics.FromImage(inkState);
            g3.Clear(Color.DarkCyan/*pictureBox5.BackColor*/);
            rectangle.Width = pictureBox2.Width; rectangle.Height = pictureBox2.Height;//rectanle是bitmap的大小
#if false
            inkStateCtrl2[3].OnPaint(g3, rectangle2[3]);
#else
            inkStateCtrl.OnPaintCircle(g3, rectangle);
#endif
            pictureBox2.CreateGraphics().DrawImage(inkState, new Point(0, 0));
            pictureBox2.Image = inkState;

            if (m_FlagUpdateAdibInfo)//判断是否需要刷新温度、电压、负压
            {
                //（2）刷线温度、电压、负压//20200402新增：
                UpdateAdibInfo(true/*m_bComState*/);
            }

#if false//20200612批注：统一在定时器中完成刷新，在工控机存在卡顿问题：需要在独立线程实现刷新
            if (m_bVTSetEnable == true)//设置标志位，在统一的定时器中完成刷新显示     
            {
                bool nReturn = GetCurVoltageTemp(false);
            }
#endif
        }

        private void TimerIrProbeTemp_Tick(object sender, EventArgs e)
        {
            try
            {
                double[] ai = motionMap.GetAi();
                if (ai == null || IrProbeAdcChannelIndex < 0 || IrProbeAdcChannelIndex >= ai.Length)
                {
                    textBoxIrProbeTemp.Text = "--";
                    return;
                }
                double v = ai[IrProbeAdcChannelIndex];
                double temp = v * (IrProbeTempFullScale / IrProbeVoltageFullScale);
                if (temp < 0.0) temp = 0.0;
                if (temp > IrProbeTempFullScale) temp = IrProbeTempFullScale;
                textBoxIrProbeTemp.Text = temp.ToString("F1");
            }
            catch
            {
                textBoxIrProbeTemp.Text = "--";
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_timerIrProbeTemp != null)
            {
                _timerIrProbeTemp.Stop();
                _timerIrProbeTemp.Dispose();
                _timerIrProbeTemp = null;
            }
            base.OnFormClosed(e);
        }


        //重新校准使能：20200222新增
        bool k_bCorrectBtn = false;
        private void CorrectCheck_CheckedChanged(object sender, EventArgs e)
        {
            k_bCorrectBtn = this.CorrectCheck.Checked;//修改对应的标志位：20200222
            for (int i = 0; i < 6; i++)//校准值标志位设为0：20200222
            {
                k_bCorrectFlag[i] = 0;
            }
        }

        private void dataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)//自定义绘制dataGridView控件的行头
        {
            var grid = sender as DataGridView;
            var rowIdx = /*"喷头"+*/(e.RowIndex + 1).ToString() + "号";//20200331新增：

            var centerFormat = new StringFormat()
            {
                // right alignment might actually make more sense for numbers
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            e.Graphics.DrawString(rowIdx, this.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }

        private void dataGridView2_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            //var rowIdx = (e.RowIndex + 1).ToString();
            var rowIdx = e.RowIndex;//20200331新增：
            string[] rowHeadText = { "电压(V)", "温度(℃)", "负压(kPa)" };//20200331新增：

            var centerFormat = new StringFormat()
            {
                // right alignment might actually make more sense for numbers
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            e.Graphics.DrawString(rowHeadText[rowIdx], this.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }
        ////double m_dHeight1, m_dHeight2, m_dHeight3, m_dHeight4;//20200222：存储的6个电机的校准行程
        //double[] k_dJourney = new double[6];//20200222：存储的6个电机的校准行程
        //LaserADD_BinderJetter.PowderLayerParam m_cPowderLayerParam = new LaserADD_BinderJetter.PowderLayerParam();//铺粉参数，现在是没有实际意义的。需要之后完善。
        //private void CorrectBtn1_Click(object sender, EventArgs e)//20200221:零位校准：先复位，再执行校准
        //{
        //    //（4）送粉铺粉运动机构复位:3轴JOG复位，设置对应的编码值为0；   
        //    //注意：千脉冲mm数为：1000；速度：不乘系数1000
        //    if (true/*g_bResetCorrectEnabled*/)//是否进行初始化复位操作
        //    {
        //        LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();//（2）关闭多轴运动：保障运动安全
        //        AutomoveComponent.MoveHome(3, m_cPowderLayerParam.m_Svel[3], m_cPowderLayerParam.m_bEnds[3]);//（1-3）JOG(4, +, V1);//铺粉电机正向回原点运动1次(JOG+)，向下运动到限位
        //        AutomoveComponent.MoveHome(3, m_cPowderLayerParam.m_Svel[3], m_cPowderLayerParam.m_bEnds[3]);//（1-3）JOG(4, +, V1);//铺粉电机正向回原点运动1次(JOG+)，向下运动到限位
        //        AutomoveComponent.MoveHome(3, m_cPowderLayerParam.m_Svel[3], m_cPowderLayerParam.m_bEnds[3]);//（1-3）JOG(4, +, V1);//铺粉电机正向回原点运动1次(JOG+)，向下运动到限位
        //        AutomoveComponent.MoveHome(3, m_cPowderLayerParam.m_Svel[3], m_cPowderLayerParam.m_bEnds[3]);//（1-3）JOG(4, +, V1);//铺粉电机正向回原点运动1次(JOG+)，向下运动到限位
        //        motionMap.SetEncPos(1, (int)(0 - 1000 * m_dHeight1));//20200222：设置1轴其编码器值为0，向下运动到限位
        //        motionMap.SetEncPos(2, (int)(0 - 1000 * m_dHeight2));//20200222：设置2轴其编码器值为0，向下运动到限位
        //        motionMap.SetEncPos(3, (int)(0 - 1000 * m_dHeight3));//20200222：设置3轴其编码器值为0，向下运动到限位
        //        motionMap.SetEncPos(4, (int)(0 - 1000 * m_dHeight4));//20200222：设置4轴其编码器值为0，向下运动到限位
        //    }
        //}

        /************************************************（1）UV灯控制模块*****************************************************/

        /***************************************************（2）X轴及双Y轴控制**************************************************/
        /// <summary>
        /// 双Y轴运动模块：//20200228新增:
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// 2024/04/10, 改为单Y轴运动控制
        ///     其中，索引 0 对应的是X轴，
        ///          索引 1 对应的是Y轴
        float nSpeed = 5;//20200305新增；
        private void XMoveBtn_MouseDown(object sender, MouseEventArgs e)//（1）运动按钮按下时响应动作
        {
            // 改为单Y轴运动控制
            //RoyalMap.m_bYSyncCtl = this.checkBox6.Checked;//checkBox1选中，则为true;不选中则为false
            RoyalMap.m_bYSyncCtl = false;

            //bool m_bYSyncCtl = false;//————移动到royalPrintingMaping中
            //bool m_bXmove = false;//X运动标志位
            //bool m_bYmove1 = false;//Y1运动标志位
            //bool m_bYmove2 = false;//Y2运动标志位
            bool nRetVal = false;

            //初始化局部变量：
            int nspd = 0;
            int nmovlen = 0;
            bool bDir = false;
            bool bRet = false;
            UInt32 nAxis = 0;
            UInt32 nCtlValue = 0;

            //(a)获取列表控件的Tag中存储的ID
            int myTag = Convert.ToInt32((sender as Control).Tag);

            //(b2)根据ID反转颜色状态
            (sender as Control).BackColor = Color.LimeGreen;

            //(c)更新速度：20200803批注
            //m_szMovSpeed = Convert.ToSingle(SpeedBox.SelectedItem);//20210618修正：
            //m_szMovSpeed = Convert.ToSingle(textBoxSpeed.Text);//20210618修正：无极速度//修改：移动到确认速度进行修改

            //nSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch,0);//20220506调整：必须调整成正确的运转速度//YINC_PERDOT扩展到48——20200108

            //(c)计算并执行动作：
            switch (myTag)
            {
                case 3://20220506批注：X轴主运动-前进, 即向右运动
                    {
#if false
                    if (RoyalMap.m_bYSyncCtl)
                    {
                        nCtlValue = 2;//_MC_CTL_SYNC_MASK扩展到2
                    }
#else
                    // X轴采用单电机控制，nCtlValue 应该赋值0，Leon, 2024/04/10
                    //nCtlValue = 2;//20220511批注：第1位是否不等停；第2位是否双Y同步//_MC_CTL_SYNC_MASK扩展到2
                    nCtlValue = 0;
#endif
                    nSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch, 0);//20220506调整：必须调整成正确的运转速度
                    {
                        gts.mc.TTrapPrm xTrapPrm = new gts.mc.TTrapPrm();
                        xTrapPrm.acc = 0.5;
                        xTrapPrm.dec = 0.5;
                        xTrapPrm.velStart = 5;
                        xTrapPrm.smoothTime = 1;
                        int xPosition = 750000 / 1000;
                        double xVel = nSpeed;
                        // 与 MoveUpBtn1/TrapMoveUp 使用同一套 motionMap，避免第二套 GoogolMotionMap 与卡/配置未协同导致无动作
                        motionMap.TrapMotion(1, ref xTrapPrm, xPosition, xVel, 0, 0, true);
                    }
                    motionMap.m_bXmove = true; // X轴正在运动标志位

                    string msg = $"手动控制墨车运动开启，沿X轴右侧方向运动 ====》：nAxis{{0}},Dir{{true}},nPlsSpeed{{{nSpeed}}},nPlsCount{{750000}},cControlFlag{{{nCtlValue}}}";
                    Log4Net.Info(msg);

                    // X轴采用单电机控制，没有同步问题
                    //if (RoyalMap.m_bYSyncCtl == true)
                    //{ this.YStateLabel.Text = "X1轴正向运动"; }
                    //else
                    //{ this.YStateLabel.Text = "X轴同步正向运动"; }
                    ////SetDlgItemText(IDC_STA_YMOV, ((wparam & 0x1) ? (m_bYSyncCtl ? _T("Y轴同步负向运动") : _T("Y1轴负向运动")) : (m_bYSyncCtl ? _T("Y轴同步正向运动") : _T("Y1轴正向运动"))));
                    //RoyalMap.m_bYmove1 = true;//Y1轴正在运动标志位

                    this.XStateLabel.Text = "X轴正向运动";
                    RoyalMap.m_bXmove = true; // X轴正在运动标志位

                    break;
                    }
                case 4://20220506批注：X轴主运动-后退，即向左运动
                    {
#if false
                    if (RoyalMap.m_bYSyncCtl)
                    {
                        nCtlValue = 2;//_MC_CTL_SYNC_MASK扩展到2
                    }
#else
                    // X轴采用单电机控制，nCtlValue 应该赋值0，Leon, 20240410
                    //nCtlValue = 2; //20220511批注：第1位是否不等停；第2位是否双Y同步//_MC_CTL_SYNC_MASK扩展到2
                    nCtlValue= 0;
#endif
                    nSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch, 0);//20220506调整：必须调整成正确的运转速度
                    {
                        gts.mc.TTrapPrm xTrapPrm = new gts.mc.TTrapPrm();
                        xTrapPrm.acc = 0.5;
                        xTrapPrm.dec = 0.5;
                        xTrapPrm.velStart = 5;
                        xTrapPrm.smoothTime = 1;
                        int xPosition = -750000 / 1000;
                        double xVel = nSpeed;
                        motionMap.TrapMotion(1, ref xTrapPrm, xPosition, xVel, 0, 0, true);
                    }
                    motionMap.m_bXmove = true; // X轴正在运动标志位

                    string msg = "手动控制墨车运动开启：沿X轴左侧方向运动 《====：nAxis{{0}},Dir{{false}},nPlsSpeed{{{nSpeed}}},nPlsCount{{750000}},cControlFlag{{{nCtlValue}}}";
                    Log4Net.Info(msg);

                    // X轴采用单电机控制，没有同步问题, Leon, 2024/04/10
                    //if (RoyalMap.m_bYSyncCtl == true)
                    //{ this.YStateLabel.Text = "X1轴负向运动"; }
                    //else
                    //{ this.YStateLabel.Text = "X轴同步负向运动"; }
                    //RoyalMap.m_bYmove1 = true;//Y1轴正在运动标志位

                    this.XStateLabel.Text = "X轴负向运动";
                    RoyalMap.m_bXmove = true; // X轴正在运动标志位
                    break;
                    }

                case 5://20220506批注：Y轴主运动-前进
                    {
                    if (true/*!RoyalMap.m_bYSyncCtl*/)//同步运动标志位//20200421修改：100000对应是500mm;我修改为200000
                    {
                        //if (RoyalMap.m_bYSyncCtl)//20220506新增
                        //{
                        //    nCtlValue = 2;//_MC_CTL_SYNC_MASK扩展到2
                        //}
#if false
                        nCtlValue = 0;//_MC_CTL_SYNC_MASK扩展到）：Y1和Y2
#else
                        nCtlValue = 0; //20220511批注：第1位是否不等停；第2位是否双Y同步//_MC_CTL_SYNC_MASK扩展到2
#endif
                        nSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch, 1);//20220506调整：必须调整成正确的运转速度
                        {
                            gts.mc.TTrapPrm y1TrapPrm = new gts.mc.TTrapPrm();
                            y1TrapPrm.acc = 0.5;
                            y1TrapPrm.dec = 0.5;
                            y1TrapPrm.velStart = 5;
                            y1TrapPrm.smoothTime = 1;
                            int y1Position = 750000 / 1000;
                            double y1Vel = nSpeed;
                            motionMap.TrapMotion(2, ref y1TrapPrm, y1Position, y1Vel, 0, 0, true);
                        }
                        motionMap.m_bYmove1 = true; // Y1轴正在运动标志位

                        string msg = "手动控制墨车运动开启：沿Y轴后侧方向运动 ↓↓↓：nAxis{{1}},Dir{{true}},nPlsSpeed{{{nSpeed}}},nPlsCount{{750000}},cControlFlag{{{nCtlValue}}}";
                        Log4Net.Info(msg);

                        this.YStateLabel.Text = "Y1轴正向运动";
                        //SetDlgItemText(IDC_STA_YMOV, ((wparam & 0x1) ? _T("Y2轴负向运动") : _T("Y2轴正向运动")));
                        //RoyalMap.m_bYmove2 = true;//Y2轴正在运动标志位
                        RoyalMap.m_bYmove1 = true;//Y1轴正在运动标志位
                    }
                    break;
                    }
                case 6://20220506批注：Y轴主运动-后退
                    {
                    if (true/*!RoyalMap.m_bYSyncCtl*/)//同步运动标志位
                    {
                        //if (RoyalMap.m_bYSyncCtl)//20220506新增
                        //{
                        //    nCtlValue = 2;//_MC_CTL_SYNC_MASK扩展到2
                        //}
#if false
                        nCtlValue = 0;//_MC_CTL_SYNC_MASK扩展到）：Y1和Y2
#else
                        nCtlValue = 0; //20220511批注：第1位是否不等停；第2位是否双Y同步//_MC_CTL_SYNC_MASK扩展到2
#endif
                        nSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch, 1);//20220506调整：必须调整成正确的运转速度
                        {
                            gts.mc.TTrapPrm y1TrapPrm = new gts.mc.TTrapPrm();
                            y1TrapPrm.acc = 0.5;
                            y1TrapPrm.dec = 0.5;
                            y1TrapPrm.velStart = 5;
                            y1TrapPrm.smoothTime = 1;
                            int y1Position = -750000 / 1000;
                            double y1Vel = nSpeed;
                            motionMap.TrapMotion(2, ref y1TrapPrm, y1Position, y1Vel, 0, 0, true);
                        }
                        motionMap.m_bYmove1 = true; // Y1轴正在运动标志位

                        string msg = "手动控制墨车运动开启：沿Y轴前侧方向运动 ↑↑↑：nAxis{{1}},Dir{{false}},nPlsSpeed{{{nSpeed}}},nPlsCount{{750000}},cControlFlag{{{nCtlValue}}}";
                        Log4Net.Info(msg);

                        this.YStateLabel.Text = "Y1轴负向运动";
                        //RoyalMap.m_bYmove2 = true;//Y2轴正在运动标志位
                        RoyalMap.m_bYmove1 = true;//Y1轴正在运动标志位
                    }
                    break;
                    }
                case 1://暂时不用
#if false//20220506批注：
                    nRetVal = royal.royal.DEM_Run(2, true, (uint)nSpeed, 750000, nCtlValue);
#endif
                    this.XStateLabel.Text = "Y轴负向运动";
                    RoyalMap.m_bXmove = true;//X轴正在运动标志位
                    break;
                case 2://暂时不用
#if false//20220506批注：
                    nRetVal = royal.royal.DEM_Run(2, false, (uint)nSpeed, 750000, nCtlValue);
#endif
                    this.XStateLabel.Text = "Y轴正向运动";
                    RoyalMap.m_bXmove = true;//X轴正在运动标志位
                    break;
            }
        }


        /// <summary>
        /// 将毫米数转换为点数
        /// </summary>
        /// <param name="X">毫米</param>
        /// <param name="DPI">分辨率，单位：点/英寸</param>
        /// <param name="AxisIndex">轴号索引，0 - X轴，1 - Y轴</param>
        /// <returns>需要运动的点数，即光栅尺的线数</returns>
        private UInt32 MM_TO_DOT(float X, int DPI, int AxisIndex)//20220506修改：新增AxisIndex为墨车轴的编号，3轴依次为0,1,2
        {
            if (AxisIndex == 0)//墨车X（AxisIndex=0；固高轴1）
            {
                //UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) * 2.50);//20210506修改：速度校准系数：1/20为计算值//20200803修改：2.50为墨车速度校准系数
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) * 5);//20220511批注：1/25.4mm*5080=200dot//应该乘以5才对//20210506修改：速度校准系数//20200803修改：2.50为墨车速度校准系数
                dot = (UInt32)(float)(X * 1000);//20220511修改：这样更加精确，实际是乘以驱动器每毫米发送脉冲数，2024/04/11，Leon
                return dot;
            }
            else if (AxisIndex == 1)//墨车Y（AxisIndex=1；固高轴2，非铺粉车轴7）
            {
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) * 5);//20220511批注：1/25.4mm*5080=200dot//应该乘以5才对//20210506修改：速度校准系数：1/200为计算值//20200803修改：2.50为墨车速度校准系数
                dot = (UInt32)(float)(X * 1000);//20220511修改：这样更加精确
                return dot;
            }
            else if (AxisIndex == 2)//第3轴
            {
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) / 5);//20210506修改：速度校准系数:1/2000仅仅是富余//20200803修改：2.50为墨车速度校准系数
                return dot;
            }
            else//第3轴
            {
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) / 2000);//20210506修改：速度校准系数:1/2000仅仅是富余//20200803修改：2.50为墨车速度校准系数
                return dot;
            }
        }

        //轴运动的关闭这个非常关键；
        //操作的参数是掩码；
        //（总共3个轴，1/2是X轴，3是Y轴);
        // 2024/04/10, 改为单Y轴运动控制，1是X轴，2是Y1轴, 4是Y2轴，Y2轴暂时不用
        private void XMoveBtn_MouseUp(object sender, MouseEventArgs e)//（2）运动按钮弹起时响应动作
        {
            bool nRetVal = false;
            (sender as Control).BackColor = SystemColors.Control/*Color.LimeGreen*/;

            //if (RoyalMap.m_bYmove1)
            //{
            //    if (RoyalMap.m_bYSyncCtl == true) { this.YStateLabel.Text = "X轴运动停止"; }
            //    else { this.YStateLabel.Text = "X1轴运动停止"; }
            //    nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
            //    RoyalMap.m_bYmove1 = false;

            //    string msg = $"手动控制墨车运动带减速停止：沿X轴方向运动 ====》《=== ====: bImmeStop{{false}},nAxisMask{{0x1}}";
            //    Log4Net.Info(msg);
            //}
            //if (RoyalMap.m_bYmove2)
            //{
            //    this.YStateLabel.Text = "Y轴运动停止";
            //    nRetVal = royal.royal.DEM_StopAxisRun(false, 0x2);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
            //    RoyalMap.m_bYmove2 = false;

            //    string msg = "手动控制墨车运动带减速停止：沿Y轴方向运动 ↑↑↑↓↓↓ ====: bImmeStop{{false}},nAxisMask{{0x2}}";
            //    Log4Net.Info(msg);
            //}
            //if (RoyalMap.m_bXmove)//单独停止3轴
            //{
            //    this.XStateLabel.Text = "Y2轴运动停止";
            //    nRetVal = royal.royal.DEM_StopAxisRun(false, 0x4);//停止轴运动20200107//_MC_X_MASKBIT扩展到0x1——20200108
            //    RoyalMap.m_bXmove = false;

            //    string msg = "手动控制墨车运动带减速停止：沿Y2轴方向运动 ↑↑↑↓↓↓ ====: bImmeStop{{false}},nAxisMask{{0x4}}";
            //    Log4Net.Info(msg);
            //}
            // 2024/04/10修改，Leon；与 MoveUpBtn 共用 motionMap 停止墨车 X/Y（轴1/2）
            motionMap.StopMotion(1, true);
            motionMap.StopMotion(2, true);
            motionMap.m_bXmove = false;
            motionMap.m_bYmove1 = false;
            motionMap.m_bYmove2 = false;

            if (RoyalMap.m_bXmove)
            {
                RoyalMap.m_bXmove = false;
                this.XStateLabel.Text = "X轴运动停止";
                string msg = "手动控制墨车运动带减速停止：沿X轴方向运动 ====》《=== ====: bImmeStop{{false}},nAxisMask{{0x1}}";
                Log4Net.Info(msg);
            }
            if (RoyalMap.m_bYmove1)
            {
                RoyalMap.m_bYmove1 = false;
                this.YStateLabel.Text = "Y轴运动停止";
                string msg = "手动控制墨车运动带减速停止：沿Y轴方向运动 ↑↑↑↓↓↓ ====: bImmeStop{{false}},nAxisMask{{0x2}}";
                Log4Net.Info(msg);
            }
            if (RoyalMap.m_bYmove2)
            {
                //nRetVal = royal.royal.DEM_StopAxisRun(false, 0x4);//停止轴运动
                RoyalMap.m_bYmove2 = false;
                this.YStateLabel.Text = "Y2轴运动停止";
                string msg = "手动控制墨车运动带减速停止：沿Y轴方向运动 ↑↑↑↓↓↓ ====: bImmeStop{{false}},nAxisMask{{0x2}}";
                Log4Net.Info(msg);
            }
        }

        /*****************************************打开DY运动监测****************************************/
        bool m_bMoveRunTest = false;//运行测试的标志位
        private void OpenMoveBtn_Click(object sender, EventArgs e)//开启双Y轴运动监控
        {

            nSpeed = MM_TO_DOT(50, EncoderLinePerInch, 0);//20220506修改：第1轴//20200328新增：
            int index = SpeedBox.FindString("100");
            SpeedBox.SelectedIndex = index;


            //(b1)反转对应的标志位
            //bool m_bMoveRunTest = true;//运行测试的标志位
            if (m_bMoveRunTest == false)
            { m_bMoveRunTest = true; }
            else
            { m_bMoveRunTest = false; }
            //(b2)根据ID反转颜色状态
            if ((sender as Control).BackColor == Color.Lime)
            { (sender as Control).BackColor = Color.Yellow; }
            else { (sender as Control).BackColor = Color.Lime; }

            //(c)打开DY轴运动监控
            if (m_bMoveRunTest)
            {
                StartUpadateDYMoveStatus();/*开启DY运动监控刷新*/
            }
            else
            {
                if (Timer4 != null)
                { Timer4.Stop();/*关闭DY运动监控刷新*/}
            }
        }
        private void StartCloseMoveBtn(bool StartCloseFlag)//开启双Y轴运动监控:20200514 new created
        {
            if (StartCloseFlag == true)
            {
                nSpeed = MM_TO_DOT(50, EncoderLinePerInch, 0);//20220506修改：第1轴//20200328新增：
                int index = SpeedBox.FindString("50"/*"50"*/);
                SpeedBox.SelectedIndex = index;

                StartUpadateDYMoveStatus();/*开启DY运动监控刷新*/
                m_bMoveRunTest = true;
            }
            else
            {
                if (Timer4 != null)
                {
                    Timer4.Stop();/*关闭DY运动监控刷新*/
                }
                m_bMoveRunTest = false;
            }

        }

        bool EncoderResetFlag = false;//墨车回零校准：20200326
        bool PowderCarEncoderResetFlag = false;//粉车回零校准：20230330
        bool PrintCarEncoderResetFlag = false;//墨车回零校准：20230330
        // true：不拦截点动/JOG/回零等（与 EnsureRetrofitReady 配合）；轴序或 cfg 未对齐时误动作风险高，确认现场后再改回 false。
        private const bool RetrofitAxisMappingEnabled = true;// 原为 false 用于改造期锁定；现放开便于 MoveUpBtn1 等与墨车/固高调试

        private bool IsRetrofitProtectedTag(int tag)
        {
            switch (tag)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                case 13:
                case 14:
                case 15:
                case 16:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsRetrofitProtectedHomeAxis(int axis)
        {
            return axis == 1 || axis == 2 || axis == 7 || axis == 8;
        }

        private bool EnsureRetrofitReady(string featureName)
        {
            if (RetrofitAxisMappingEnabled)
            {
                return true;
            }

            string msg = $"设备轴改造尚未完成，已拦截功能：{featureName}";
            Log4Net.Info(msg);
            MessageBox.Show($"设备轴改造尚未完成，当前已禁止执行：{featureName}");
            return false;
        }


        /// <summary>
        /// 墨车和铺粉车回零校准
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EncoderResetBtn_Click(object sender, EventArgs e)//开启关闭对应的校准线程
        {
            if (!EnsureRetrofitReady("运动系统回零校准"))
            {
                return;
            }
            if (EncoderResetFlag == false)//20220521批注：没有存在校准任务
            {
                string msg = $"开启墨车和铺粉车回零校准：EncoderResetBtn_Click";
                Log4Net.Info(msg);
#if true
                //开启墨车打印线程1:
                string tempThreadName = "InkCarGoogolHomeThread";
                Thread tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    EncoderResetThreads.Remove(tempThread);//以防万一
                }
                else
                {
                    ThreadStart initThreadEntry = new ThreadStart(InkCarGoogolHomeThread);//20200220:线程入口方法修改为联动线程
                    tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    tempThread.Name = tempThreadName;
                    tempThread.Start();

                    msg = $"开启墨车回零校准线程：InkCarGoogolHomeThread";
                    Log4Net.Info(msg);

                    EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }
#endif
                //开启粉车打印线程2:
                tempThreadName = "PowderCarHomeResetThread";
                tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    EncoderResetThreads.Remove(tempThread);//以防万一
                }
                else
                {
                    ThreadStart initThreadEntry = new ThreadStart(PowderCarHomeResetThread);//20200220:线程入口方法修改为联动线程
                    tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    tempThread.Name = tempThreadName;
                    tempThread.Start();


                    msg = $"开启铺粉车回零校准线程：PowderCarHomeResetThread";
                    Log4Net.Info(msg);

                    EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }

                //修改按钮状态为：正在校准
                this.EncoderResetBtn.Text = "校准运动系统中...";
                this.EncoderResetBtn.TextAlign = ContentAlignment.MiddleCenter;
                //this.EncoderResetBtn.BackColor = Color.Lime; //this.EncoderResetBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/StopJob.png");
                EncoderResetFlag = true;//玩的都是标志位：20200126
            }
            else//20220521批注：存在校准任务
            {
#if true
                //(1-1)关闭联调线程；（1-2）关闭多轴运动：保障运动安全
                string tempThreadName = "InkCarGoogolHomeThread";//(1)关闭联调线程
                DeleteThread(tempThreadName);
                //(a)检测到正限位和负限位后紧急停止运动（与点动/墨车 Jog 同用 motionMap 轴1/2）
                motionMap.StopMotion(1, true);
                motionMap.StopMotion(2, true);
#endif

                //(2-1)关闭铺粉车校准线程(2-2)关闭多轴运动：保证运动安全
                tempThreadName = "PowderCarHomeResetThread";//(1)关闭联调线程
                DeleteThread(tempThreadName);
                //(a)检测到正限位和负限位后紧急停止运动：
                short AXIS = 7; motionMap.StopMotion(AXIS);//20260416修改：停止铺粉车轴运动

                //修改按钮状态为：启动打印
                this.EncoderResetBtn.Text = "校准运动系统";
                this.EncoderResetBtn.TextAlign = ContentAlignment.MiddleCenter;
                //this.EncoderResetBtn.BackColor = Color.Yellow; //this.EncoderResetBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/RunJob.png");
                EncoderResetFlag = false;//玩的都是标志位：20200126
            }
        }
        private void PowderCarHomeBtn_Click(object sender, EventArgs e)//20230330新增：
        {
            if (!EnsureRetrofitReady("铺粉车回零校准"))
            {
                return;
            }
            if (PowderCarEncoderResetFlag == false)//20220521批注：没有存在校准任务
            {
                string msg = $"开启铺粉车回零校准：PowderCarHomeBtn_Click";
                Log4Net.Info(msg);

                //开启粉车打印线程2:
                string tempThreadName = "PowderCarHomeResetThread";
                Thread tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    EncoderResetThreads.Remove(tempThread);//以防万一
                }
                else
                {
                    ThreadStart initThreadEntry = new ThreadStart(PowderCarHomeResetThread);//20200220:线程入口方法修改为联动线程
                    tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    tempThread.Name = tempThreadName;
                    tempThread.Start();


                    msg = $"开启铺粉车回零校准线程：PowderCarHomeResetThread";
                    Log4Net.Info(msg);

                    EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }

                //修改按钮状态为：正在校准
                this.PowderCarHomeBtn.Text = "校准铺粉车中...";
                this.PowderCarHomeBtn.TextAlign = ContentAlignment.MiddleCenter;
                //this.PowderCarHomeBtn.BackColor = Color.Lime; //this.PowderCarHomeBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/StopJob.png");
                PowderCarEncoderResetFlag = true;//玩的都是标志位：20200126
            }
            else//20220521批注：存在校准任务
            {
                //(2-1)关闭铺粉车校准线程(2-2)关闭多轴运动：保证运动安全
                string tempThreadName = "PowderCarHomeResetThread";//(1)关闭联调线程
                DeleteThread(tempThreadName);
                //(a)检测到正限位和负限位后紧急停止运动：
                short AXIS = 7; motionMap.StopMotion(AXIS);//20260416修改：停止铺粉车轴运动

                //修改按钮状态为：启动打印
                this.PowderCarHomeBtn.Text = "校准铺粉车";
                this.PowderCarHomeBtn.TextAlign = ContentAlignment.MiddleCenter;
                //this.PowderCarHomeBtn.BackColor = Color.Yellow; //this.PowderCarHomeBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/RunJob.png");
                PowderCarEncoderResetFlag = false;//玩的都是标志位：20200126
            }
        }


        /// <summary>
        /// 墨车校准
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PrintCarHomeBtn_Click(object sender, EventArgs e)//20230330新增：
        {
            if (PrintCarEncoderResetFlag == false)//20220521批注：没有存在校准任务
            {
                墨车校准确认 f = new 墨车校准确认();//20200224修改:
                DialogResult result = f.ShowDialog();
                if (result == DialogResult.OK)//OK时，执行对应操作
                {
                    string msg = $"开启墨车回零校准：PrintCarHomeBtn_Click";
                    Log4Net.Info(msg);
#if true
                    //开启墨车打印线程1:
                    string tempThreadName = "InkCarGoogolHomeThread";
                    Thread tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                    if (tempThread != null)
                    {
                        EncoderResetThreads.Remove(tempThread);//以防万一
                    }
                    else
                    {
                        ThreadStart initThreadEntry = new ThreadStart(InkCarGoogolHomeThread);//20200220:线程入口方法修改为联动线程
                        tempThread = new Thread(initThreadEntry) { IsBackground = true };
                        tempThread.Name = tempThreadName;
                        tempThread.Start();

                        msg = $"开启墨车回零校准线程：InkCarGoogolHomeThread";
                        Log4Net.Info(msg);

                        EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                    }
#endif

                    //修改按钮状态为：正在校准
                    this.PrintCarHomeBtn.Text = "校准墨车中...";
                    this.PrintCarHomeBtn.TextAlign = ContentAlignment.MiddleCenter;
                    //this.PrintCarHomeBtn.BackColor = Color.Lime; //this.PrintCarHomeBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/StopJob.png");
                    PrintCarEncoderResetFlag = true;//玩的都是标志位：20200126
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
            else//20220521批注：存在校准任务
            {
#if true
                //(1-1)关闭联调线程；（1-2）关闭多轴运动：保障运动安全
                string tempThreadName = "InkCarGoogolHomeThread";//(1)关闭联调线程
                DeleteThread(tempThreadName);
                //(a)检测到正限位和负限位后紧急停止运动（与点动/墨车 Jog 同用 motionMap 轴1/2）
                motionMap.StopMotion(1, true);
                motionMap.StopMotion(2, true);
#endif

                //修改按钮状态为：启动打印
                this.PrintCarHomeBtn.Text = "校准墨车";
                this.PrintCarHomeBtn.TextAlign = ContentAlignment.MiddleCenter;
                //this.PrintCarHomeBtn.BackColor = Color.Yellow; //this.PrintCarHomeBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/RunJob.png");
                PrintCarEncoderResetFlag = false;//玩的都是标志位：20200126
            }
        }

        //20200220：线程管理的案发现场，只要是相应的线程我就存储在这里，不管线程是死是活，祖祖辈辈就在这里，便于维护及管理
        private List<Thread> EncoderResetThreads = new List<Thread>();//20200220:存放所有必要的打印机的工作线程
        public void DeleteThread(string ThreadName)
        {
            string tempThreadName = ThreadName;
            Thread tempThread = EncoderResetThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
            if (tempThread != null)
            {
                tempThread.Abort();//20200221修改:当调用非托管线程时，有时会抛出异常但不一定及时停止
                while (tempThread.ThreadState != ThreadState.Aborted)
                { Thread.Sleep(2); }
                EncoderResetThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
            }
        }

        private void PowderCarHomeResetThread()//20220521新建：铺粉车校准，20260416修改为轴7
        {
            existCorrectProcessFlag2 = true;//20230419新建：存在校准任务
            int nIOState = motionMap.MointoringAxis2(7);//20260416修改：当前铺粉车运动轴改为轴7
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//铺粉车位于负限位报警区
            {
                //MessageBox.Show("铺粉车不在正常停靠区间");
                PowderCarHomeFlag = false;//20220521新建：铺粉车校准异常
            }
            else//铺粉车位于正常停靠区
            {
                bool ReturnCode = motionMap.SetBackHome(7/*轴*/, k_RYSYSParamAutoPrintParamInTest.m_dPowderCarHomeposition /*0*//*105*//*原点复位值，单位MM*/, 20/*校准速度，单位MM/S*/, 1000/*搜索距离,单位MM*/, 5/*脱离距离，单位MM*/, ref PowderCarHomeFlag/*校准完成标志*/);//20260416修改：铺粉轴改为轴7
                if (ReturnCode == true)//校准成功
                {
                    RollerParam = 0/*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed*/;//201029批注：更新辊子速度
                    double AimPos = 1; double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                    BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停        
                }
            }

            if (EncoderResetBtn.InvokeRequired == true)
            {
                EncoderResetBtn.BeginInvoke(
                    new Action(() =>
                    {
                        if (PowderCarHomeFlag == true)//20220521新建：铺粉车部分校准完成汇报
                        {
                            string msg = $"铺粉车回零校准线程成功：PowderCarHomeResetThread";
                            Log4Net.Info(msg);

                            /*PowderHomeBtn.Text = "铺粉"; */
                            PowderCarHomeBtn.Text = "已校准铺粉";//20230330新增：
                            PowderHomeBtn.BackColor = Color.Lime;//已校标志
                            MessageBox.Show("铺粉车-Home成功！");
                        }
                        else
                        {
                            string msg = $"铺粉车回零校准线程失败：PowderCarHomeResetThread";
                            Log4Net.Info(msg);

                            /*PowderHomeBtn.Text = "铺粉"; */
                            PowderCarHomeBtn.Text = "校准铺粉失败";//20230330新增：
                            PowderHomeBtn.BackColor = Color.Tomato;//未校标志
                            MessageBox.Show("铺粉车-Home失败！");
                        }

                        RefreshSystemCorrectState();
                    }
                    ));
            }
            existCorrectProcessFlag2 = false;//20230419新建：校准完了，不存在校准任务了
            FinalizeCombinedCalibrationIfIdle();
            DeleteThread("PowderCarHomeResetThread");//20200220：本线程结束，需要及时清理相关线程//20200313新增：

        }
        private void ModbusThread()//20220521新建：Modbus线程
        {
            bool returnFlag = modbusCommunicateMap.CreateQuitModbusCommunication(true);//建立通讯
            CreateSerialPortSuccessFlag = returnFlag;//创建成功标志位
#if false
            if (EncoderResetBtn.InvokeRequired == true)
            {
                EncoderResetBtn.BeginInvoke(
                    new Action(() =>
                    {

                        //CreateSerialPortSuccessFlag = returnFlag;//创建成功标志位
                    }
                    ));
            }
#endif
            DeleteThread("ModbusThread");//20200220：本线程结束，需要及时清理相关线程//20200313新增：
        }
        bool existCorrectProcessFlag = false;//20230419:
        bool existCorrectProcessFlag2 = false;//20230419:


        /// <summary>
        /// 墨车校准线程
        /// </summary>
        /// <note>
        /// 备注：喷墨系统坐标原点在右下角，而校准0点在左上角
        ///  
        ///                             000.000mm             860.000mm
        ///                                |<------ 860mm ------>|        
        ///                          DIR = false <----- . -----> DIR = true
        ///                               XNegLim               XPosLim
        ///                             O--+---------------------+--->
        ///                             |                             
        ///  000.000mm --     YNegLim   + [O]                              
        ///            /\   DIR = false |                     [H]           
        ///                     /\      |   +-----------+                          
        ///          450mm              |   |           |                         
        ///                     \/      |   |           |                   
        public bool InkCarHomeFlag = false; public bool PowderCarHomeFlag = false;
        public bool InkCarXHomeFlag = false; public bool InkCarYHomeFlag = false;

        //刷新虚拟打印编码器状态显示定时器
        public/*private*/ System.Windows.Forms.Timer Timer4 = null;//刷新虚拟打印编码器状态显示定时器
        private void StartUpadateDYMoveStatus()//开启双Y轴MOVE限位信号
        {
            bool nRetVal = false;
            /////////////////////******初始化墨车电机******///////////
#if false//20200618新增批注：本部分对整体的运动影响巨大，不应该出现这种情况。如果该喷墨控制卡重新开启，则会限于不动的状态//初始化运动并使能运动；//但是有本部分，自动打印状态下开启手动，又会莫名停机。
            nRetVal = royal.royal.DEM_InitAxis(0, 0x100);//分别初始化各轴的运动参数：20200305//加速度：256pluse/ms^2
            nRetVal = royal.royal.DEM_InitAxis(1, 0x1400);//分别初始化各轴的运动参数：20200305
            nRetVal = royal.royal.DEM_InitAxis(2, 0x1400);//分别初始化各轴的运动参数：20200305//IOS_SetConfig(0x3000,0x0);			//Reg0x14	复用掩码设置 保留输出1、2
            nRetVal = royal.royal.DEM_EnableAxisRun(true);//所有的轴共用1个使能，使能一次就OK!:20200305
#endif
#if true//20200618批注：
            //刷新X轴和Y轴的编码器位置、显示状态定时器
            Timer4 = new System.Windows.Forms.Timer() { Interval = 100 };//间隔100ms刷新编码器位置
            Timer4.Tick += new EventHandler(Timer4_Tick);
            Timer4.Start();
#endif
        }
        UInt32 nIOState = 0;//状态标志位：20200311
        /// <summary>
        /// 刷新墨量显示状态定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer4_Tick(object sender, EventArgs e)//刷新墨量显示状态定时器
        {
            // 备注：Y1 为 X轴，即 0 号轴
            // 备注：Y2 为 Y轴，即 1 号轴
            // 备注：X  为 Z轴，即 2 号轴，单Y轴时停用
            // 备注：光栅尺型号：WTB1-0600，WTB1-0900，1um分辨率
            // 问题：如何显示负数？
            string szTxt;
            //UInt32 nIOState;//墨车控制器的状态标志位：20200311
            double/*UInt32*/ nxpos, ny1pos, ny2pos;//20200602批注：
            UInt32 nxAxis, nyAxis1, nyAxis2;

            //////////////////////////////////(a)实时的轴编码器位置
            //ny1pos = royal.royal.DEM_GetAxisEncodeVal(0);//_MC_Y1_INDEX扩展到1
            //UInt32 readEncoderPosValue = royal.royal.DEV_GetPrintEncoderValue();//20200306新增：
            //MessageBox.Show("设备的编码器值是：" + Convert.ToString(readEncoderPosValue));//20200306新增：           

            //ny1pos = /*(uint)*/(royal.royal.DEV_GetPrintEncoderValue() / EncoderLinePerMM);//20200306新增：编码器位置设置
            ny1pos = /*(uint)*/((double)royal.royal.DEM_GetAxisEncodeVal(0) / EncoderLinePerMM);// 2024/04/11修改

            //ny2pos = /*(uint)*/(royal.royal.DEM_GetAxisEncodeVal(1) / EncoderLinePerMM);//_MC_Y2_INDEX扩展到2//其实这个API用处不是很大：20200327批注
            ny2pos = /*(uint)*/((double)royal.royal.DEM_GetAxisEncodeVal(1) / EncoderLinePerMM);// 光栅尺分辨率为0.001mm，2024/04/11修改
            nxpos = /*(uint)*/((double)royal.royal.DEM_GetAxisEncodeVal(2) / EncoderLinePerMM);//_MC_X_INDEX扩展到0//其实这个API用处不是很大：20200327批注

            szTxt = ny1pos.ToString(/*"G"*//*"X"*/"F3") + " MM";//16进制显示——20200108
            this.Y1PosLabel.Text = szTxt;
            //this.CurEncoderText.Text = szTxt;//UV灯栏显示的光栅编码器值//20200306新增(又去掉)：虚拟编码可以测试UV灯数据

            szTxt = ny2pos.ToString("F3"/*"X"*/) + " MM";//16进制显示——20200108
            this.Y2PosLabel.Text = szTxt;

            szTxt = nxpos.ToString("F3"/*"X"*/) + " MM";//将十进制数值以16进制的形式显示——20200108
            //this.XPosLabel.Text = szTxt;//20200306修改错误：
            this.XPosLabel.Text = "";

            //////////////////////////////////(b)剩余脉冲数获取//20200327：这个用处不是很大：I think是这样。
            nyAxis1 = royal.royal.DEM_GetRevPluse(0);
            nyAxis2 = royal.royal.DEM_GetRevPluse(1);
            //nxAxis = royal.royal.DEM_GetRevPluse(2);//查询剩余脉冲20200107

            szTxt = nyAxis1.ToString("G"/*"X"*/);//16进制显示——20200108
            this.Y1RevPosLabel.Text = szTxt;

            szTxt = nyAxis2.ToString("G"/*"X"*/);//16进制显示——20200108
            this.Y2RevPosLabel.Text = szTxt;

            //szTxt = nxAxis.ToString("G"/*"X"*/);//16进制显示——20200108
            //this.XRevPosLabel.Text = szTxt;
            this.XRevPosLabel.Text = "";

            //////////////////////////////////(c)实时的轴限位状态
            nIOState = royal.royal.DEM_GetAxisLmtZeroState(0);//底层接口已经作了12 bit移位处理，对照Reg[12]定义——————世彪批注：获取限位状态20200106
            //////////////////////////////////(c)实时的多轴报警状态
            /////////(2)次序为P，Z，N 限位
            if (0 != (nIOState & 0x2))//是否有报警：有限位报警
            { this.Y1PLLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y1PLLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState & 0x10))//有报警
            { this.Y1ZeroLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y1ZeroLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState & 0x1))//有报警
            { this.Y1NLLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y1NLLabel.BackColor = Color.LimeGreen; }

            /////////(3)次序为P，Z，N 限位
            if (0 != (nIOState & 0x4))//是否有报警：有限位报警、、
            { this.Y2PLLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y2PLLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState & 0x20))//有报警
            { this.Y2ZeroLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y2ZeroLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState & 0x8))//有报警
            { this.Y2NLLabel.BackColor = Color.Red; }
            else//无报警
            { this.Y2NLLabel.BackColor = Color.LimeGreen; }

            /////////(1)次序为P，Z，N 限位
            if (0 != (nIOState & 0x4))//是否有报警：有限位报警
            { this.XPLLabel.BackColor = Color.Red; }
            else//无报警
            { this.XPLLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState & 0x40))//有报警
            { this.XZeroLabel.BackColor = Color.Red; }
            else//无报警
            { this.XZeroLabel.BackColor = Color.LimeGreen; }
            if (0 != (nIOState & 0x8))//有报警
            { this.XNLLabel.BackColor = Color.Red; }
            else//无报警
            { this.XNLLabel.BackColor = Color.LimeGreen; }
        }


        float m_szMovSpeed = 50/*0*/;//20210618修改：修改默认值到50  mm/s
        float m_szCleanSpeed = 50;//20220520新建：默认刮喷头速度为50 mm/s
        int m_nCleanTime = 1;//20220520新建：默认刮喷头次数为3次//20220915修改：修改为2次清洗
        private void SpeedBox_SelectedIndexChanged(object sender, EventArgs e)//更新速度
        {
            //20210619注释：
            float MovSpeed = Convert.ToSingle(SpeedBox.SelectedItem);//20210619批注：暂时无效SpeedBox功能

            ////20210619新建修改：
            //textBoxSpeed.Text = "20";//20210619批注：暂时无效SpeedBox功能,默认100
            ///*float*/ MovSpeed = Convert.ToSingle(textBoxSpeed.Text);//20210618修正：无极速度
            if ((0 < MovSpeed) && (MovSpeed <= 300))//验证输入速度是否位于安全区间？//20220506修改：降低安全移动速度
            {
                m_szMovSpeed = MovSpeed;//20210618修正：无极速度
            }
            else
            {
                MessageBox.Show("输入速度错误，请输入正确的速度：0mm/s-300mm/s");
            }
            nSpeed = MM_TO_DOT(m_szMovSpeed, EncoderLinePerInch, 0);//YINC_PERDOT扩展到48——20200108

        }

        private void ConfirmSpeedBtn_Click(object sender, EventArgs e)//20210618新建
        {
            float MovSpeed = Convert.ToSingle(textBoxSpeed.Text);//20210618修正：无极速度
            if ((0 < MovSpeed) && (MovSpeed <= 150))//验证输入速度是否位于安全区间？
            {
                m_szMovSpeed = MovSpeed;//20210618修正：无极速度
            }
            else
            {
                this.textBoxSpeed.Text = m_szMovSpeed.ToString();//仍保持原来的参数值
                MessageBox.Show("输入速度错误，请输入正确的速度：0mm/s-300mm/s");
            }
#if false
            float CleanSpeed = Convert.ToSingle(textBox20.Text);//20210618修正：无极速度
            if ((0 < CleanSpeed) && (CleanSpeed <= 150))//验证输入速度是否位于安全区间？
            {
                m_szCleanSpeed = CleanSpeed;//20210618修正：无极速度
            }
            else
            {
                this.textBox20.Text = m_szCleanSpeed.ToString();//仍保持原来的参数值
                MessageBox.Show("输入速度错误，请输入正确的速度：0mm/s-300mm/s");
            }
            int CleanTime = Convert.ToInt16(textBox21.Text);//20220520新建：默认刮喷头次数为3次
            if (0 < CleanTime && CleanTime < 10) { m_nCleanTime = CleanTime; }
            else
            {
                m_nCleanTime = 3;
                this.textBox21.Text = m_nCleanTime.ToString();//仍保持原来的参数值
            }
            string msg = $"确认墨车打印、清洗速度以及清洗次数【手动测试环境】：ConfirmSpeedBtn_Click：打印速度{{{m_szMovSpeed}MM/s}}清洗速度{{{CleanSpeed}MM/s}}" +
                $"清洗次数{{{m_nCleanTime}次}}";
            Log4Net.Info(msg);
#endif

        }

        private void DefaultUVRangeBtn_Click(object sender, EventArgs e)//恢复默认区间
        {
            k_RYSYSParamAutoPrintParamInTest.LeftCureOn = 550;
            k_RYSYSParamAutoPrintParamInTest.LeftCureOff = 1000;
            k_RYSYSParamAutoPrintParamInTest.RightCureOn = 270;
            k_RYSYSParamAutoPrintParamInTest.RightCureOff = 720;

            string msg = $"回复默认光区：" +
                $"左灯开启{{{k_RYSYSParamAutoPrintParamInTest.LeftCureOn}MM}}左灯关闭{{{k_RYSYSParamAutoPrintParamInTest.LeftCureOff}MM}}" +
                $"右灯开启{{{k_RYSYSParamAutoPrintParamInTest.RightCureOn}MM}}右灯关闭{{{k_RYSYSParamAutoPrintParamInTest.RightCureOff}MM}}";
            Log4Net.Info(msg);
        }

        private void CalibrateUVRangeBtn_Click(object sender, EventArgs e)
        {
            k_RYSYSParamAutoPrintParamInTest.LeftCureOn = 1000;
            k_RYSYSParamAutoPrintParamInTest.LeftCureOff = 1120;
            k_RYSYSParamAutoPrintParamInTest.RightCureOn = 720;
            k_RYSYSParamAutoPrintParamInTest.RightCureOff = 840;
        }

        /***************************************************（2）X轴及双Y轴控制************************************************/

        /***************************************************（1）UV灯控制模块**************************************************/
        //打开虚拟打印，岁定时器运行虚拟编码器
        bool m_bRunTest = false;//运行测试的标志位
        private void OpenVitualEncodeBtn_Click(object sender, EventArgs e)
        {
            //(b1)反转对应的标志位
            //bool m_bRunTest = true;//运行测试的标志位
            if (m_bRunTest == false)
            { m_bRunTest = true; }
            else
            { m_bRunTest = false; }
            //(b2)根据ID反转颜色状态
            if ((sender as Control).BackColor == Color.Yellow)
            { (sender as Control).BackColor = Color.Lime; }
            else { (sender as Control).BackColor = Color.Yellow; }

            //打开虚拟打印，运行虚拟编码器
            UInt32 nPos = 1000000/*0x100000*/;//初始的编码器值
            if (m_bRunTest)
            {
                bool nRetVal = royal.royal.DEV_ResetPrintEncoder(nPos); //重新设置当前位置值
                bool nRetVal2 = royal.royal.DEV_SetVirtualPrtEncoder(true, true, 100 /*1000*/);//开启编码计数：TRUE 计数增大方向 1000 代表频率，可根据情况调整
                StartUpadatePrintEncoder();//开启刷新
            }
            else
            {
                bool nRetVal = royal.royal.DEV_SetVirtualPrtEncoder(false, true, 100 /*1000*/);//停止编码计数//20200306:100是速度。
                bool nRetVal2 = royal.royal.DEV_ResetPrintEncoder(nPos);
                if (Timer5 != null)
                {
                    Timer5.Stop();
                }
            }
        }
        //public struct UVLightParam//20200619批注：
        //{
        //    //UV灯的周期及有效时间：
        //    public float m_fCycleSec;//UV灯周期
        //    public float m_fValidSec;//UV灯有效时间
        //    //UV灯开启范围及限位值：
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        //    public Int32[] nMinPos;//灯1的上限、下限
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        //    public Int32[] nMaxPos;//灯2的上限、下限
        //}
        public UVLightParam k_UVLightParam = new UVLightParam { };

        //按照虚拟编码器位置，打开及关闭UV灯
        bool m_bEanbleAutoCtl = false;//开启/关闭UV灯的标志位    
        private void OpenIRLamp(bool EnableLight)
        {
            if (EnableLight == true)//打开IR灯//设置功率为0
            {
                //if (InitModbusFlag == true)
                //{
                //读取输入寄存器值并完成显示
                int SlaveNumber = Convert.ToInt32(textBox17.Text); int RegisterAddress = Convert.ToInt32(textBox16.Text); int RegisterNumber = 1; int RegisterValue = Convert.ToInt32(textBox19.Text);
                //20220520新建：设置出光模式为手动出光方式:默认出光方式为自动出光控制
                RegisterAddress = 132; RegisterValue = 1;//20220520新建批注：寄存器地址为40133;手动打印模式寄存器值为1
                modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//20220520批注：寄存器为40133;
                                                                                                                            //设置功率为50%
#if true//20220524临时设置为25%功率，打印功能测试时以30设置
                RegisterAddress = 121; RegisterValue = Convert.ToInt32(k_RYSYSParamAutoPrintParamInTest.m_dIRPowerPercentage/*30*//*Convert.ToDouble(textBox19.Text)*/ * 100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
#else
                RegisterAddress = 121; RegisterValue = Convert.ToInt32(Convert.ToDouble(textBox19.Text) * 100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
#endif
                modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40122----MV
                //}
            }
            else//关闭IR灯//设置功率为0
            {
                //读取输入寄存器值并完成显示
                int SlaveNumber = Convert.ToInt32(textBox17.Text); int RegisterAddress = Convert.ToInt32(textBox16.Text); int RegisterNumber = 1; int RegisterValue = Convert.ToInt32(textBox19.Text);
                //20220520新建：设置出光模式为手动出光方式:默认出光方式为自动出光控制
                RegisterAddress = 132; RegisterValue = 1;//20220520新建批注：寄存器地址为40133;手动打印模式寄存器值为1
                modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//20220520批注：寄存器为40133;
                //设置功率为50%
                RegisterAddress = 121; RegisterValue = Convert.ToInt32(0 * 100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
                modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40122----MV

            }
        }
        private void OpenUVLamp(bool EnableLight, double UV1LimitUp, double UV1LimitDown, double UV2LimitUp, double UV2LimitDown)//20220520新建：默认输入（0,1000,01,1000）
        {
            //按照虚拟编码器位置，打开及关闭UV灯
            ///(a)初始化UV灯开启范围及限位值：
            Int32[] nMinPos = new Int32[2];
            Int32[] nMaxPos = new Int32[2];
            //UInt32[] nMinPos = new UInt32[2];//跨平台不能处理uint
            //UInt32[] nMaxPos = new UInt32[2];
            string m_szUV1Pos1 = UV1LimitUp.ToString()/*this.UV1LimitUp.Text*/;//灯1的上限
            string m_szUV1Pos2 = UV1LimitDown.ToString()/*this.UV1LimitDown.Text*/;//灯1的下限
            string m_szUV2Pos1 = UV2LimitUp.ToString()/*this.UV2LimitUp.Text*/;//灯2的上限
            string m_szUV2Pos2 = UV2LimitDown.ToString()/*this.UV2LimitDown.Text*/;//灯2的下限

            nMinPos[1] = (int)(Convert.ToDouble(m_szUV2Pos1) * 200);//字符串转数值
            nMaxPos[1] = (int)(Convert.ToDouble(m_szUV2Pos2) * 200);//字符串转数值
            nMinPos[0] = (int)(Convert.ToDouble(m_szUV1Pos1) * 200);//字符串转数值
            nMaxPos[0] = (int)(Convert.ToDouble(m_szUV1Pos2) * 200);//字符串转数值

            //string m_szCycleSec = this.CycleBox.Text;//20200306新增//20200531：周期应该固定为0.008//20210619修改：取消掉
            //string m_szValidSec = this.ValidBox.Text;//20200306新增//20200531：有效应该设置为：X%；m_fCycleSec*X%//20210619修改：取消掉

            string m_szCycleSec = (k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency/*m_nCureFrequency*/).ToString();//灯1的上限//20210619新增：
            string m_szValidSec = (k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio).ToString();//灯1的上限//20210619新增：
            if (0 <= k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio
                && k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio <= 100)//20210619新建，判断
            {
                m_szValidSec = (k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio).ToString();//灯1的上限//20201029修改
                //this.ValidBox.Text = m_szValidSec;//20210619新增：
            }

            float m_fCycleSec = 1 / Convert.ToSingle(m_szCycleSec)/*50*/;//20200304：50ms——(1)125Hz，对应的是8ms(2)单位时ms还是us//20200407新增确认，单位是S//20210615修改：直接设置为时间
            //float m_fCycleSec =Convert.ToSingle(m_szCycleSec)/*50*/;//20200304：50ms——(1)125Hz，对应的是8ms(2)单位时ms还是us//20200407新增确认，单位是S
            float m_fValidSec = m_fCycleSec * Convert.ToSingle(m_szValidSec) / 100/*25*/;//20200304：25ms——(1)1ms=12.5%功率，2ms=25%功率（2）m_fValidSec=m_fCycleSec*power/100;//power/100

            //（b）数组1的指针
            int size = Marshal.SizeOf(nMinPos[0]) * nMinPos.Length;
            IntPtr PnMinPos = Marshal.AllocHGlobal(size);
            //Marshal.AllocHGlobal(PnMinPos);//20200404新增：
            //(b)数组2的指针
            int size2 = Marshal.SizeOf(nMaxPos[0]) * nMaxPos.Length;
            IntPtr PnMaxPos = Marshal.AllocHGlobal(size2);
            //Marshal.AllocHGlobal(PnMaxPos);//20200404新增：

            Marshal.Copy(nMinPos, 0, PnMinPos, nMinPos.Length);//复制到非托管区内存
            Marshal.Copy(nMaxPos, 0, PnMaxPos, nMaxPos.Length);//复制到非托管区内存

            bool nRetVal = royal.royal.DEV_EnableUVPosCtlOut(false, false);//（0）设置UV等使能开启
            if (EnableLight/*m_bEanbleAutoCtl*/)//如果为True则出光
            {
                bool nRetVal1 = royal.royal.DEV_SetUVLampPosRange(PnMinPos, PnMaxPos);//（1）设置UV灯使能开启
                bool returnCode = royal.royal.DEV_SetPwmParam(true/*Param1Check.Checked*//*true*/, m_fCycleSec, m_fValidSec);//（2）设置UV灯功率输出：//20200604批注：取消UV灯参数使能按钮
                //20200604批注：本部分提示可以取消，已经完成充分测试
                ///MessageBox.Show("PWM设置结果为："+returnCode+"。其中，参数1状态为"+ /*Param1Check.Checked */"true"+ "，UV灯的PWM周期为："+m_fCycleSec.ToString()+" s,有效时间为："+m_fValidSec.ToString()+" s");
                bool nRetVal2 = royal.royal.DEV_EnableUVPosCtlOut(true, true);//（3）打开两侧的UV灯：
            }
            Marshal.FreeHGlobal(PnMinPos);//释放内存
            Marshal.FreeHGlobal(PnMaxPos);//释放内存
        }
        private void OpenUVBtn_Click(object sender, EventArgs e)
        {
            //(b1)反转对应的标志位
            //bool m_bEanbleAutoCtl = true;//开启/关闭UV灯的标志位
            if (m_bEanbleAutoCtl == false) { m_bEanbleAutoCtl = true; }
            else { m_bEanbleAutoCtl = false; }
            //(b2)根据ID反转颜色状态
            if ((sender as Control).BackColor == Color.Yellow)
            { (sender as Control).BackColor = Color.Lime; }
            else { (sender as Control).BackColor = Color.Yellow; }

#if true//20220520批注：重构逻辑
            double UV1Pos1 = Convert.ToDouble(this.UV1LimitUp.Text);//灯1的上限
            double UV1Pos2 = Convert.ToDouble(this.UV1LimitDown.Text);//灯1的下限
            double UV2Pos1 = Convert.ToDouble(this.UV2LimitUp.Text);//灯2的上限
            double UV2Pos2 = Convert.ToDouble(this.UV2LimitDown.Text);//灯2的下限

            if (m_bEanbleAutoCtl)//打开灯
            {
                if (0 <= k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio && k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio <= 100)//20210619新建，判断
                {
                    string m_szValidSec = (k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio).ToString();//灯1的上限//20201029修改
                    this.ValidBox.Text = m_szValidSec;//20210619新增：
                }
                OpenUVLamp(true, UV1Pos1, UV1Pos2, UV2Pos1, UV2Pos2);

                string msg = $"开启UV灯：OpenUVLamp" +
                    $"出光频率{{{k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency}Hz}}占空比{{{k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio}%}}" +
                    $"左灯开启{{{UV1Pos1}MM}}左灯关闭{{{UV1Pos2}MM}}" +
                    $"右灯开启{{{UV2Pos1}MM}}右灯关闭{{{UV2Pos2}MM}}";
                Log4Net.Info(msg);

            }
            else//关闭灯
            {
                OpenUVLamp(false, UV1Pos1, UV1Pos2, UV2Pos1, UV2Pos2);

                string msg = $"关闭UV灯：OpenUVLamp" +
                    $"出光频率{{{k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency}Hz}}占空比{{{k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio}%}}" +
                    $"左灯开启{{{UV1Pos1}MM}}左灯关闭{{{UV1Pos2}MM}}" +
                    $"右灯开启{{{UV2Pos1}MM}}右灯关闭{{{UV2Pos2}MM}}";
                Log4Net.Info(msg);
            }
#else//20220520批注：旧有逻辑
            //按照虚拟编码器位置，打开及关闭UV灯
            ///(a)初始化UV灯开启范围及限位值：
            Int32[] nMinPos = new Int32[2];
            Int32[] nMaxPos = new Int32[2];
            //UInt32[] nMinPos = new UInt32[2];//跨平台不能处理uint
            //UInt32[] nMaxPos = new UInt32[2];
            string m_szUV1Pos1 = this.UV1LimitUp.Text;//灯1的上限
            string m_szUV1Pos2 = this.UV1LimitDown.Text;//灯1的下限
            string m_szUV2Pos1 = this.UV2LimitUp.Text;//灯2的上限
            string m_szUV2Pos2 = this.UV2LimitDown.Text;//灯2的下限

            nMinPos[1] = (int)(Convert.ToDouble(m_szUV2Pos1) * 200);//字符串转数值
            nMaxPos[1] = (int)(Convert.ToDouble(m_szUV2Pos2) * 200);//字符串转数值
            nMinPos[0] = (int)(Convert.ToDouble(m_szUV1Pos1) * 200);//字符串转数值
            nMaxPos[0] = (int)(Convert.ToDouble(m_szUV1Pos2) * 200);//字符串转数值

            //string m_szCycleSec = this.CycleBox.Text;//20200306新增//20200531：周期应该固定为0.008//20210619修改：取消掉
            //string m_szValidSec = this.ValidBox.Text;//20200306新增//20200531：有效应该设置为：X%；m_fCycleSec*X%//20210619修改：取消掉

            string m_szCycleSec = (k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency/*m_nCureFrequency*/).ToString();//灯1的上限//20210619新增：
            string m_szValidSec = (k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio).ToString();//灯1的上限//20210619新增：
            if (0 <= k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio
                && k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio <= 100)//20210619新建，判断
            {
                m_szValidSec = (k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio).ToString();//灯1的上限//20201029修改
                this.ValidBox.Text = m_szValidSec;//20210619新增：
            }

            float m_fCycleSec = 1 / Convert.ToSingle(m_szCycleSec)/*50*/;//20200304：50ms——(1)125Hz，对应的是8ms(2)单位时ms还是us//20200407新增确认，单位是S//20210615修改：直接设置为时间
            //float m_fCycleSec =Convert.ToSingle(m_szCycleSec)/*50*/;//20200304：50ms——(1)125Hz，对应的是8ms(2)单位时ms还是us//20200407新增确认，单位是S
            float m_fValidSec = m_fCycleSec * Convert.ToSingle(m_szValidSec) / 100/*25*/;//20200304：25ms——(1)1ms=12.5%功率，2ms=25%功率（2）m_fValidSec=m_fCycleSec*power/100;//power/100

            //（b）数组1的指针
            int size = Marshal.SizeOf(nMinPos[0]) * nMinPos.Length;
            IntPtr PnMinPos = Marshal.AllocHGlobal(size);
            //Marshal.AllocHGlobal(PnMinPos);//20200404新增：
            //(b)数组2的指针
            int size2 = Marshal.SizeOf(nMaxPos[0]) * nMaxPos.Length;
            IntPtr PnMaxPos = Marshal.AllocHGlobal(size2);
            //Marshal.AllocHGlobal(PnMaxPos);//20200404新增：

            Marshal.Copy(nMinPos, 0, PnMinPos, nMinPos.Length);//复制到非托管区内存
            Marshal.Copy(nMaxPos, 0, PnMaxPos, nMaxPos.Length);//复制到非托管区内存

            bool nRetVal = royal.royal.DEV_EnableUVPosCtlOut(false, false);//（0）设置UV等使能开启
            if (m_bEanbleAutoCtl)
            {
                bool nRetVal1 = royal.royal.DEV_SetUVLampPosRange(PnMinPos, PnMaxPos);//（1）设置UV灯使能开启
                bool returnCode = royal.royal.DEV_SetPwmParam(true/*Param1Check.Checked*//*true*/, m_fCycleSec, m_fValidSec);//（2）设置UV灯功率输出：//20200604批注：取消UV灯参数使能按钮
                //20200604批注：本部分提示可以取消，已经完成充分测试
                ///MessageBox.Show("PWM设置结果为："+returnCode+"。其中，参数1状态为"+ /*Param1Check.Checked */"true"+ "，UV灯的PWM周期为："+m_fCycleSec.ToString()+" s,有效时间为："+m_fValidSec.ToString()+" s");
                bool nRetVal2 = royal.royal.DEV_EnableUVPosCtlOut(true, true);//（3）打开两侧的UV灯：
            }
            Marshal.FreeHGlobal(PnMinPos);//释放内存
            Marshal.FreeHGlobal(PnMaxPos);//释放内存
#endif
        }

        /// <summary>
        /// 20251209添加
        /// 通过固高EXO输出控制超声装置开关
        /// </summary>
        /// <param name="enable">true:开启超声, false:关闭超声</param>
        private void ControlUltrasonicViaGoogolIO(bool enable)
        {
            try
            {
                // 超声装置连接到固高控制器的第5路EXO输出
                // 固高的EXO通道编号通常为0-15
                int ultrasonicChannel = 5;

                if (enable)
                {
                    // 开启超声：设置该路EXO输出为高电平（true）
                    motionMap.SetDo((short)ultrasonicChannel, true);
                    Log4Net.Info($"超声装置: 已开启（固高EXO通道{ultrasonicChannel}输出高电平）");
                }
                else
                {
                    // 关闭超声：设置该路EXO输出为低电平（false）
                    motionMap.SetDo((short)ultrasonicChannel, false);
                    Log4Net.Info($"超声装置: 已关闭（固高EXO通道{ultrasonicChannel}输出低电平）");
                }
            }
            catch (Exception ex)
            {
                Log4Net.Error($"超声装置控制失败: {ex.Message}");
            }
        }

        //20200404新感悟。
        //非常牛逼的思维：原始目的，是为了解决全局变量分配中，存在的问题。
        //新的一种重构思维：将不确定的东西，在确定的地方，统一安排好，即可避免类似的问题！！！！！！！！！！！！！！20200404新增：
        //IntPtr PnMinPos = new IntPtr(); IntPtr PnMaxPos = new IntPtr();

        //private void CreatHGValueForAPI()//解决经常卡死的问题，异常报错：内存空间不足，你的程序无法运行//20200404新增：
        //{
        //    /*IntPtr */PnMinPos = Marshal.AllocHGlobal(size);
        //    /*IntPtr */PnMaxPos = Marshal.AllocHGlobal(size2);
        //}


        //刷新虚拟打印编码器状态显示定时器
        public/*private*/ System.Windows.Forms.Timer Timer5 = null;//刷新虚拟打印编码器状态显示定时器
        private void StartUpadatePrintEncoder()
        {
            //刷新墨量显示状态定时器
            Timer5 = new System.Windows.Forms.Timer() { Interval = 100 };//间隔100ms刷新编码器位置
            Timer5.Tick += new EventHandler(Timer5_Tick);
            Timer5.Start();
        }
        private void Timer5_Tick(object sender, EventArgs e)//刷新墨量显示状态定时器
        {
            ////（1）刷新数据显示到textBox中
            CurEncoderText.Text = (royal.royal.DEV_GetPrintEncoderValue()).ToString();
        }
        /************************************************结束：（1）UV灯控制模块*****************************************************/
        /************************************************结束：（1）UV灯控制模块*****************************************************/

        /********************************************温度电压气压设置：开始*************************************************/
        /// <summary>
        /// 设定对应的datagridview的状态为可编辑和不可编辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        bool k_bADIBCheck = false;
        private void ADIBSetCheck_CheckedChanged(object sender, EventArgs e)//设定对应的datagridview的状态为可编辑和不可编辑
        {
            k_bADIBCheck = this.ADIBSetCheck.Checked;//修改对应的标志位：20200401，本函数只修改这一部分
            if (k_bADIBCheck == false)//不编辑状态（不选中）：不可编辑datagridview的cell
            {
                try
                {
                    dataGridView2.EndEdit(); bingdingSource2.EndEdit();//非常关键：20200403新增：
                    //(0)dataGridView2控件重新初始化：
                    InitDataGrid2(0);
                    ADIBSetControlInterferenceMaintain(false);//进入监控状态时:

                    string msg = $"进入气压控制卡参数设置状态时";
                    Log4Net.Info(msg);
                }
                catch (Exception exception)
                {
                    MessageBox.Show("发生异常：异常代码" + exception);//20210201新建：增加软件操作的稳健性
                }
            }
            else//修改datagridview状态为选中：选中状态，可编辑datagridview的cell
            {
                try
                {
                    //(0)dataGridView2控件重新初始化：
                    InitDataGrid2(1);
                    ADIBSetControlInterferenceMaintain(true);//进入设置状态时：

                    string msg = $"进入气压控制卡参数监控状态时";
                    Log4Net.Info(msg);
                }
                catch (Exception exception)
                {
                    MessageBox.Show("发生异常：异常代码" + exception);//20210201新建：增加软件操作的稳健性
                }
            }
        }

        string TransferModifyFlag = "Start";//VT监控线程转换指令
        /// <summary>
        /// 喷头状态VT监控线程
        /// </summary>
        private void StartVTMonitorThread(string ControlCommand)
        {
            //(1)开启VT监控线程
            if (ControlCommand == "Start")//不存在VT监控线程
            {
                //（1）新建数据处理及传送线程:（耗时操作）
                string tempThreadName = "VTMonitorThread";
                Thread tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    EncoderResetThreads.Remove(tempThread);//以防万一
                }
                else//开启数据处理及传送线程:（耗时操作）
                {
                    //(1)开启数据处理及传送线程
                    ThreadStart initThreadEntry = new ThreadStart(RunVTMonitorThread);//20200220:线程入口方法修改为联动线程
                    tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    tempThread.Name = tempThreadName;
                    tempThread.Start();
                    EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }
            }
            else if ((ControlCommand == "Abort"))//关闭VT监控线程
            {
                string tempThreadName = "VTMonitorThread";//(1)关闭联调线程
                DeleteThread(tempThreadName);
            }
            else { }
        }
        bool FlagPrinting = false;//处于没有打印的状态
        private void RunVTMonitorThread()///20200612批注：多线程内容
        {
            //GetCurVoltageTemp(false);//多线程优化
            if (FlagPrinting) return;
            float[] ftemp = new float[4];
            float[] fVolt = new float[4];
            short nState = 0;
            uint p = 0;
            string msg;

            while (FlagPrinting == false)//20200612批注：随便找的1个标志位作为测试
            {
                float[,] fVoltageTempData = new float[8, 5];//20200612修改：

                for (uint d = 0; d < 1/*8*/; d++)
                {
#if true
                    if (FlagPrinting/* && (*bAbort) || m_bStopMonitor || m_bCloseAutoChk*/)
                        return;

                    //运行状态
                    bool returnCode0 = royal.royal.MCU_GetSate(ref nState, p, d);
                    if (returnCode0 == true)   //20180702 新增
                    { }
                    if (FlagPrinting /*&& bAbort) || m_bStopMonitor || m_bCloseAutoChk*/)
                        return;
                    Thread.Sleep(10);

                    //喷头电压
                    float[] fVoltageTemp = new float[5];//20200612修改：
                    //fVoltageTemp[0] = 18.0f; fVoltageTemp[1] = 18.0f; fVoltageTemp[2] = 18.0f; fVoltageTemp[3] = 18.0f; fVoltageTemp[4] = 25.0f;//喷头温度
                    fVoltageTemp[0] = 0.0f; fVoltageTemp[1] = 0.0f; fVoltageTemp[2] = 0.0f; fVoltageTemp[3] = 0.0f; fVoltageTemp[4] = 0.0f;//喷头温度

                    int size1 = Marshal.SizeOf(fVoltageTemp[0]) * 4/*fVoltageTemp.Length*/;
                    IntPtr PfVoltage = Marshal.AllocHGlobal(size1);

                    Marshal.Copy(fVoltageTemp, 0, PfVoltage, 4);//测试用：非托管区内存初始化//实际测试的时候，去掉//20200405批注

                    //20200424新增//读取波形参数的电压：
                    if (d == 0)//执行操作
                    {
                        ////msg = $"准备读取{{{d + 1}}}号喷头打印前状态：读取电压！！";
                        ////Log4Net.Info(msg);
                        bool returnCode = royal.royal.MCU_GetCurPhVoltage(PfVoltage, p, d);
                        if (returnCode == true)
                        {
                            //（1）解析读取到的电压值
                            Marshal.Copy(PfVoltage, fVoltageTemp, 0, 4);

                            //（2）释放内存空间
                            Marshal.FreeHGlobal(PfVoltage);//释放内存
                        }
                    }
                    else { }//不执行读取操作

                    if (FlagPrinting /*&& (*bAbort) || m_bStopMonitor || m_bCloseAutoChk*/)
                        return;
                    Thread.Sleep(10);

                    //喷头温度
                    int size2 = Marshal.SizeOf(fVoltageTemp[0]) * 1 /* *fCurTemp.Length*/;//4个变量，只用到第1个
                    IntPtr PfCurTemp = Marshal.AllocHGlobal(size2);

                    Marshal.Copy(fVoltageTemp, 0, PfCurTemp, 1/*fVoltageTemp.Length*/);//测试用:非托管区内存初始化//实际测试的时候，去掉//20200405批注

                    //20200424新增//读取喷头的温度：
                    if (d == 0)//执行操作
                    {
                        ////msg = $"准备读取{{{d + 1}}}号喷头打印前状态：读取温度！！";
                        ////Log4Net.Info(msg);
                        bool returnCode2 = royal.royal.MCU_GetCurPhTemp(PfCurTemp, p, d);
                        if (returnCode2 == true)//0x0802->0x0822
                        {
                            //（1）解析读取到的电压值
                            Marshal.Copy(PfCurTemp, fVoltageTemp, 4, 1/* 0, fCurTemp.Length*/);

                            //（2）释放内存空间
                            Marshal.FreeHGlobal(PfCurTemp);//释放内存
                        }
                    }
                    else { }//不执行读取操作
#endif
                    for (int j = 0; j < 5; j++)
                    {
                        fVoltageTempData[d, j] = fVoltageTemp[j];
                    }

                    ////msg = $"{{{d + 1}}}号喷头打印前状态：喷头温度为{{{fVoltageTemp[0]}℃}}，喷头电压为{{{fVoltageTemp[1]}V,{fVoltageTemp[2]}V,{fVoltageTemp[3]}V,{ fVoltageTemp[4]}V}}；";
                    ////Log4Net.Info(msg);
                }


                UpdateVTMonitor(fVoltageTempData);//刷新到datagridview1//读出1个喷头的数据刷新1次
                //Thread.Sleep(100);
            }
            return;
        }
        /// 定义一个代理：加载CLI完成后，刷新总层数
        private delegate void UpdateVTMonitorDelegate(float[,] fVoltageTempData);//20200612新增：
        private void UpdateVTMonitor(float[,] fVoltageTempData)
        {
            if (this.dataGridView1.InvokeRequired == false)
            {
                for (int d = 0; d < 8/*8*/; d++)//依次刷新8个喷头的温度电压//20230322新增修改：
                {
                    if (d == 0)
                    {
                        //（1）修改1个喷头的电压参数：
                        dataGridView1.Rows[(int)d].Cells[0].Value = fVoltageTempData[d, 0].ToString("F2");//浮点数格式，2位小数点
                        dataGridView1.Rows[(int)d].Cells[1].Value = fVoltageTempData[d, 1].ToString("F2");//浮点数格式，2位小数点
                        dataGridView1.Rows[(int)d].Cells[2].Value = fVoltageTempData[d, 2].ToString("F2");//浮点数格式，2位小数点
                        dataGridView1.Rows[(int)d].Cells[3].Value = fVoltageTempData[d, 3].ToString("F2");//浮点数格式，2位小数点                   
                        //（2）修改1个喷头的温度控件：
                        dataGridView1.Rows[(int)d].Cells[4].Value = fVoltageTempData[d, 4].ToString("F2");//浮点数格式，2位小数点
                    }
                    else
                    {
                        //（1）修改1个喷头的电压参数：
                        dataGridView1.Rows[(int)d].Cells[0].Value = "--";//浮点数格式，2位小数点
                        dataGridView1.Rows[(int)d].Cells[1].Value = "--";//浮点数格式，2位小数点
                        dataGridView1.Rows[(int)d].Cells[2].Value = "--";//浮点数格式，2位小数点
                        dataGridView1.Rows[(int)d].Cells[3].Value = "--";//浮点数格式，2位小数点                   
                        //（2）修改1个喷头的温度控件：
                        dataGridView1.Rows[(int)d].Cells[4].Value = "--";//浮点数格式，2位小数点
                    }
                }
                dataGridView1.Refresh();
                //TransferModifyFlag = "StartFlag";//传送取消标志位
            }
            else
            {
                UpdateVTMonitorDelegate DMSGD = new UpdateVTMonitorDelegate(UpdateVTMonitor);
                this.dataGridView1.Invoke(DMSGD, fVoltageTempData);
            }
        }


        /// <summary>
        /// 设定对应的datagridview1的状态为可编辑和不可编辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        bool k_bJetSetCheck = false;
        private void JetSetCheck_CheckedChanged(object sender, EventArgs e)//喷头温度电压设置
        {
            k_bJetSetCheck = this.JetSetCheck.Checked;//修改对应的标志位：20200401，本函数只修改这一部分
            if (k_bJetSetCheck == false)//不编辑状态（不选中）：不可编辑datagridview的cell
            {
                //k_bJetSetCheck = this.JetSetCheck.Checked;//修改对应的标志位：20200401，本函数只修改这一部分
                dataGridView1.EndEdit(); bingdingSource1.EndEdit();//非常关键：20200403新增：

                //(0)dataGridView2控件重新初始化：
                InitDataGrid1(0);
                JetSetControlInterferenceMaintain(false);//进入监控状态时:

                StartVTMonitorThread("Start");//20200612新建批注：
                Thread.Sleep(850);//暂停500ms
            }
            else//修改datagridview状态为选中：选中状态，可编辑datagridview的cell
            {
                //k_bJetSetCheck = this.JetSetCheck.Checked;//修改对应的标志位：20200401，本函数只修改这一部分
                //(0)dataGridView2控件重新初始化：
                InitDataGrid1(1);
                JetSetControlInterferenceMaintain(true);//进入设置状态时：

                StartVTMonitorThread("Abort");//20200612新建批注：
                //Thread.Sleep(100);//暂停500ms
            }
        }

        /// <summary>
        /// ADIB设置控件干涉维护：20200403新增
        /// </summary>
        /// <param name="FlagSet"></param>
        private void ADIBSetControlInterferenceMaintain(bool FlagSet)
        {
            if (FlagSet)//设置状态时：
            {
                //（1）关闭电压温度气压监控：
                m_bSetEnable = false;
                OpenVTMonitorThread(false);//(2)20200401新增:关闭温度电压气压监控线程

                //（2）开启控件交互：
                dataGridView2.Enabled = true;//20200401：停止对用户交互做出反应
                dataGridView2.ReadOnly = false;
                dataGridView2.ClearSelection();

                //(3)
                ADIBSetApplyBtn.Enabled = true; //ADIBCancleApplyBtn.Enabled = true; textBox1.Enabled = true;//临时注销
            }
            else//监控状态时:
            {
                //（1）关闭控件交互：
                dataGridView2.Enabled = false;//20200401：停止对用户交互做出反应
                dataGridView2.ReadOnly = true;
                dataGridView2.ClearSelection();//20200401：清楚所有的编辑状态：清除之前的选中状态

                //（2）开启电压温度气压监控：
                m_bSetEnable = true;
                OpenVTMonitorThread(true);//(2)20200401新增:开启温度电压气压监控线程

                //(3)
                ADIBSetApplyBtn.Enabled = false; //ADIBCancleApplyBtn.Enabled = false; textBox1.Enabled = false;
            }
        }

        /// <summary>
        /// ADIB设置控件干涉维护：20200403新增
        /// </summary>
        /// <param name="FlagSet"></param>
        private void JetSetControlInterferenceMaintain(bool FlagSet)
        {
            if (FlagSet)//设置状态时：
            {
                //（1）关闭电压温度气压监控：
                //m_bSetEnable = false;//20230322临时注释
#if false//20200604批注：修改为过程的实时监测
                ////OpenVTMonitorThread(false);//(2)20200401新增:开启温度电压气压监控线程
#else
                m_bVTSetEnable = false;//设置标志位，在统一的定时器中完成刷新显示     
                //OpenVTMonitorThread(false);//(2)20200401新增:开启温度电压气压监控线程

#endif
                //（2）开启控件交互：
                dataGridView1.Enabled = true;//20200401：停止对用户交互做出反应
                dataGridView1.ReadOnly = false;
                dataGridView1.ClearSelection();

                //(3)
                JetSetApplyBtn.Enabled = true; //JetCancleApplyBtn.Enabled = true;
            }
            else//监控状态时:
            {
                //（1）关闭控件交互：
                dataGridView1.Enabled = false;//20200401：停止对用户交互做出反应
                dataGridView1.ReadOnly = true;
                dataGridView1.ClearSelection();//20200401：清楚所有的编辑状态：清除之前的选中状态

                //（2）开启电压温度气压监控：
                //m_bSetEnable = true;//20230322临时注释
                ////OpenVTMonitorThread(true);//(2)20200401新增:开启温度电压气压监控线程
                ///
#if false//20200604批注：修改为过程的实时监测

                bool nReturn = GetCurVoltageTemp(false);
                m_bVTSetEnable = true; //设置标志位，在统一的定时器中完成刷新显示             
                //OpenVTMonitorThread(true);//(2)20200401新增:开启温度电压气压监控线程
#endif

                //(3)
                JetSetApplyBtn.Enabled = false; //JetCancleApplyBtn.Enabled = false;
            }
        }

        /********************************************
        获取实时的电压温度状态
        **********************************************/
        bool m_bVTSetEnable = false;//20200604新增:标志位：是否持续监测驱动卡电压及喷头温度标志位
        private bool GetCurVoltageTemp(bool FlagPrinting)
        {
            if (FlagPrinting /*&& (*bAbort)*/)
                return false;
            uint nFMVer = 0;
            float[] ftemp = new float[4];
            float[] fVolt = new float[4];
            short nState = 0;
            int nItem = 0;
            uint p = 0;
            int nPrtValidMask = 0; int nDrvValidMask = 0;
            if (true)
            {
                for (uint d = 0; d < 8; d++)
                {
                    if (true)
                    {
                        if (FlagPrinting/* && (*bAbort) || m_bStopMonitor || m_bCloseAutoChk*/)
                            return false;

                        //运行状态
                        bool returnCode0 = royal.royal.MCU_GetSate(ref nState, p, d);
                        if (returnCode0 == true)   //20180702 新增
                        {
                            // m_nState[p][d] = nState;
                        }
                        if (FlagPrinting /*&& bAbort) || m_bStopMonitor || m_bCloseAutoChk*/)
                            return false;
                        Thread.Sleep(10);

                        //喷头电压
                        float[] fVoltage = new float[4]; fVoltage[0] = 18.0f; fVoltage[1] = 18.5f; fVoltage[2] = 18.0f; fVoltage[3] = 19.0f;
                        int size1 = Marshal.SizeOf(fVoltage[0]) * fVoltage.Length;
                        IntPtr PfVoltage = Marshal.AllocHGlobal(size1);

                        Marshal.Copy(fVoltage, 0, PfVoltage, fVoltage.Length);//测试用：非托管区内存初始化//实际测试的时候，去掉//20200405批注

                        //20200424新增//读取波形参数的电压：
                        bool returnCode = royal.royal.MCU_GetCurPhVoltage(PfVoltage, p, d);
                        if (returnCode == true)
                        {
                            //（1）解析读取到的电压值
                            Marshal.Copy(PfVoltage, fVoltage, 0, fVoltage.Length);

                            //（2）修改控件：
                            dataGridView1.Rows[(int)d].Cells[0].Value = fVoltage[0].ToString("F2");//浮点数格式，2位小数点
                            dataGridView1.Rows[(int)d].Cells[1].Value = fVoltage[1].ToString("F2");//浮点数格式，2位小数点
                            dataGridView1.Rows[(int)d].Cells[2].Value = fVoltage[2].ToString("F2");//浮点数格式，2位小数点
                            dataGridView1.Rows[(int)d].Cells[3].Value = fVoltage[3].ToString("F2");//浮点数格式，2位小数点

                            //（3）释放内存空间
                            Marshal.FreeHGlobal(PfVoltage);//释放内存
                        }
                        if (FlagPrinting /*&& (*bAbort) || m_bStopMonitor || m_bCloseAutoChk*/)
                            return false;
                        Thread.Sleep(10);


                        //喷头温度
                        float[] fCurTemp = new float[4]; fCurTemp[0] = 26.0f;
                        int size2 = Marshal.SizeOf(fCurTemp[0]) * fCurTemp.Length;//4个变量，只用到第1个
                        IntPtr PfCurTemp = Marshal.AllocHGlobal(size2);

                        Marshal.Copy(fCurTemp, 0, PfCurTemp, fCurTemp.Length);//测试用:非托管区内存初始化//实际测试的时候，去掉//20200405批注

                        //20200424新增//读取喷头的温度：
                        bool returnCode2 = royal.royal.MCU_GetCurPhTemp(PfCurTemp, p, d);

                        if (returnCode2 == true)//0x0802->0x0822
                        {
                            //（1）解析读取到的电压值
                            Marshal.Copy(PfCurTemp, fCurTemp, 0, fCurTemp.Length);

                            //修改控件：
                            dataGridView1.Rows[(int)d].Cells[4].Value = fCurTemp[0].ToString("F2");//浮点数格式，2位小数点                

                            //（3）释放内存空间
                            Marshal.FreeHGlobal(PfCurTemp);//释放内存
                        }
                        nItem++;
                        Thread.Sleep(10/*100*/);
                    }
                }
                dataGridView1.Refresh();
            }
            return true;
        }

        /// <summary>
        /// 应用修改后的datagridview的参数，完成设置过程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ADIBSetApplyBtn_Click(object sender, EventArgs e)//写指令到PPCB的MCU中
        {
            try
            {
                //(0)预备设置
                float[] fcurval = new float[8];//暂存1组8个数据：20200401新增
                uint nIoOption = 0;//：DEV_AdibControl的控制位：20200401新增
                bool m_bSetEnabled = false;///20200401新增：是否设置应用：世彪新增

                //(1)负压、温度、电压设置
                for (int i = 0; i < 3; i++) //电压暂时没用 0 -> 1
                {
                    //（1）解析第i行数据
                    for (int j = 0; j < 6; j++)
                    {
                        //20200421新增：新增异常捕获
                        fcurval[j] = Convert.ToSingle(dataTable2.Rows[i][j].ToString());//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                    }

                    if (1 == i)//（1）二级墨盒温度设置: 0x2是指版本号————20200421新建批注
                    {
                        nIoOption |= 0x2;//对应的标志位完成记录，最后生效的时候，依次生效所有的负压阈值//20200329新增：
                        royal.royal.g_sys_param.adibParam.fcurInkTankTemp = fcurval.ToArray();//非常好的数组赋值：20200401修改
                    }
                    else if (2 == i)//（2）负压设置：0x4是指负压值————20200421新建批注
                    {
                        nIoOption |= 0x4;//对应的标志位完成记录，最后生效的时候，依次生效所有的负压阈值//20200329新增：
                        royal.royal.g_sys_param.adibParam.fcurAirPress = fcurval.ToArray(); ;
                    }
                    else//（3）二级墨盒加热电压：设置0x10是指加热电压————20200421新建批注
                    {
                        nIoOption |= 0x10;//对应的标志位完成记录，最后生效的时候，依次生效所有的负压阈值//20200329新增：
                        royal.royal.g_sys_param.adibParam.fcurvoltage = fcurval.ToArray(); ;
                    }
                }
                //（2）负压阈值设置
                if (k_EnvironmentParam.m_fAirHold > 0.0f)//（4）负压阈值设置：
                {
                    nIoOption |= 0x20;//对应的标志位完成记录，最后生效的时候，依次生效所有的负压阈值//20200329新增：
                    royal.royal.g_sys_param.adibParam.fAirThreshold = k_EnvironmentParam.m_fAirHold;
                }
                //(3-1)负压、温度、电压设置生效
                nIoOption |= 0x80000000;//对应的标志位完成记录，最后生效的时候，依次生效所有的负压阈值//20200329新增：
                //pApp->SetCtrlMask((nCtrlMask|0x2));//bit[0] 0写 1读 ，bit[1] 保存到MCU
#if true//20230322新增：ADIB设置的设置值
                nIoOption = 0;
                nIoOption |= 0x4;
                nIoOption |= 0x20;
#endif
                //(3-2）此过程耗时2-3s，时间比较长，因此，本部分，暂时取消
                bool nRetVal = royal.royal.DEV_AdibControl(ref royal.royal.g_sys_param.adibParam, nIoOption, true, ref m_bSetEnabled);//设置气压板的所有参数值：负压、正压、二级墨盒温度

                string msg = $"更新气压控制卡参数：" +
                     $"DEV_AdibControl:二级墨盒加热电压：{{{ royal.royal.g_sys_param.adibParam.fcurvoltage[0]}V}}；" +
                     $"MCU_SetPhVoltage:二级墨盒加热温度：{{{royal.royal.g_sys_param.adibParam.fcurInkTankTemp[0]}℃}}" +
                     $"MCU_SetPhVoltage:负压目标值：{{{royal.royal.g_sys_param.adibParam.fcurAirPress}kPa}}" +
                     $"MCU_SetPhVoltage:负压阈值：{{{royal.royal.g_sys_param.adibParam.fAirThreshold}kPa}}";
                Log4Net.Info(msg);
            }
            catch (Exception error)
            {
                string msg = $"更新气压控制卡参数失败：{error.ToString()}";
                Log4Net.Info(msg);

                //string reminderText = "输入有误：{" + error.Message+"}";
                MessageBox.Show("警告：" + error.Message + "！");//listview输入有误
            }
            finally
            {
            }
        }

        float[,] fVolt = new float[8, 4];//1个车头板，8个驱动板，每个驱动板4路电压//20200404：本人的方式，不采用数组的形式存储这些电压值
        float[] fDTemp = new float[8/*, 1*/];//1个车头板，8个驱动板，每个驱动板4路电压//20200404：本人的方式，不采用数组的形式存储这些电压值

        /// <summary>
        /// 应用修改后的datagridview1的参数，完成设置过程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JetSetApplyBtn_Click(object sender, EventArgs e)//写指令到喷头驱动板//基准电压
        {
            try
            {
                //（0）设置的总计8路32个电压值，8路8个温度值
                //float [,,] fVolt= new float[1,8,4];//1个车头板，8个驱动板，每个驱动板4路电压//20200404：本人的方式，不采用数组的形式存储这些电压值
                //float [,,] fDTemp= new float[1,8,1];//1个车头板，8个驱动板，每个驱动板4路电压//20200404：本人的方式，不采用数组的形式存储这些电压值

                //（1）将DataTable的数据存储到多维数组中：fVolt和fDTemp
                for (int i = 0; i < 8; i++) //电压暂时没用 0 -> 1
                {
                    for (int j = 0; j < 5; j++)//（1）解析第i行数据
                    {
#if true
                        if (j < 4)//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                        { fVolt[i, j] = Convert.ToSingle(dataTable.Rows[i][j].ToString()); }
                        //else//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                        //{ fDTemp[i] = Convert.ToSingle(dataTable.Rows[i][j].ToString()); }
#else
#endif
                    }
                }

                //（2）设置并生效所有的电压和温度值
                for (uint d = 0; d < 1/*8*/; d++)//20230322修改：只设置第1路喷头生效
                {
                    //(1)复制数据：20200404修改
                    float[] fstdVoltage = new float[4];//内存中的对应值
                    for (int i = 0; i < 4; i++)
                    {
                        fstdVoltage[i] = fVolt[d, i];
                    }

                    //(2)微调实际基准电压到准确的目标值：20200404修改
                    int size = Marshal.SizeOf(fstdVoltage[0]) * fstdVoltage.Length;
                    IntPtr PfstdVoltage = Marshal.AllocHGlobal(size);
                    Marshal.Copy(fstdVoltage, 0, PfstdVoltage, fstdVoltage.Length);//复制到非托管区内存
#if false//20230323新增：此处重新更新基准，只为能够精确实现在目标大范围的基准电压的准确调节
                    //(2-1)使用新的基准值
                    bool returnCode = royal.royal.DEV_SetWaveStdVoltage(PfstdVoltage, 0, 0);//20230323批注：基准电压设置值<1时，基准电压的设定值以波形文件中为准
                    if (returnCode == false)
                    {
                        string msg2 = $"{{{d + 1}}}号喷头基准电压更新失败：DEV_SetWaveStdVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }
                    else
                    {
                        string msg2 = $"{{{d + 1}}}号喷头基准电压更新成功：DEV_SetWaveStdVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }
#endif
                    //(2-2)微调实际基准电压到精确的目标值：20200404修改
                    bool returnCode = royal.royal.MCU_SetPhVoltage(PfstdVoltage, 0, d);//微调参数值，精确设定
                    if (returnCode == false)
                    {
                        string msg2 = $"{{{d + 1}}}号喷头电压调压失败：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }
                    else
                    {
                        string msg2 = $"{{{d + 1}}}号喷头电压调压成功：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }

#if false
                    //（3）设置温度：20200404修改
                    float fProTemp = fDTemp[d] /*70.0f*/;//ftemp = fDTemp[p][d];  
                    bool returnCode2 = royal.royal.MCU_SetPhStdTemp(ref fProTemp, 0, d);
                    if (returnCode2 == false)
                    {
                        msg2 = $"{{{d+1}}}号喷头温度更新失败：MCU_SetPhVoltage{{{fProTemp}℃}}";
                        Log4Net.Info(msg2);
                    }
                    else 
                    {
                        msg2 = $"{{{d + 1}}}号喷头温度更新失败：MCU_SetPhVoltage{{{fProTemp}℃}}";
                        Log4Net.Info(msg2);
                    }
#endif
                }
            }
            catch (Exception error)
            {
                string msg = $"喷头电压更新异常：{error.ToString()}";
                Log4Net.Info(msg);
                MessageBox.Show("警告：" + error.Message + "！");//eg:listview输入有误
            }
        }
        private void JetSetApplyBtn2_Click(object sender, EventArgs e)//20230322修改：修改温度，修改温度和修改电压应该分开
        {
            try
            {
                //（1）将DataTable的数据存储到多维数组中：fVolt和fDTemp
                for (int i = 0; i < 8; i++) //电压暂时没用 0 -> 1
                {
                    for (int j = 0; j < 5; j++)//（1）解析第i行数据
                    {
                        if (j < 4)//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                        {
                            //fVolt[i, j] = Convert.ToSingle(dataTable.Rows[i][j].ToString());
                        }
                        else//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                        { fDTemp[i] = Convert.ToSingle(dataTable.Rows[i][j].ToString()); }
                    }
                }

                //（2）设置并生效所有的电压和温度值
                for (uint d = 0; d < 1/*8*/; d++)//20230322修改：只设置第1路喷头生效
                {
#if false
                    //(1)复制数据：20200404修改
                    float[] fstdVoltage = new float[4];//内存中的对应值
                    for (int i = 0; i < 4; i++)
                    {
                        fstdVoltage[i] = fVolt[d, i];
                    }

                    //(2)设置电压：20200404修改
                    int size = Marshal.SizeOf(fstdVoltage[0]) * fstdVoltage.Length;
                    IntPtr PfstdVoltage = Marshal.AllocHGlobal(size);
                    Marshal.Copy(fstdVoltage, 0, PfstdVoltage, fstdVoltage.Length);//复制到非托管区内存

                    string msg2;
                    bool returnCode = royal.royal.MCU_SetPhVoltage(PfstdVoltage, 0, d);
                    if (returnCode == false)
                    {
                        msg2 = $"{{{d + 1}}}号喷头电压更新失败：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }
                    else
                    {
                        msg2 = $"{{{d + 1}}}号喷头电压更新成功：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }
#endif
                    //（3）设置温度：20200404修改
                    float fProTemp = fDTemp[d] /*70.0f*/;//ftemp = fDTemp[p][d];  
                    bool returnCode2 = royal.royal.MCU_SetPhStdTemp(ref fProTemp, 0, d);
                    if (returnCode2 == false)
                    {
                        string msg2 = $"{{{d + 1}}}号喷头温度更新失败：MCU_SetPhStdTemp{{{fProTemp}℃}}";
                        Log4Net.Info(msg2);
                    }
                    else
                    {
                        string msg2 = $"{{{d + 1}}}号喷头温度更新成功：MCU_SetPhStdTemp{{{fProTemp}℃}}";
                        Log4Net.Info(msg2);
                    }
                }
            }
            catch (Exception error)
            {
                string msg = $"喷头温度更新异常：{error.ToString()}";
                Log4Net.Info(msg);
                MessageBox.Show("警告：" + error.Message + "！");//eg:listview输入有误
            }
        }


        //20200402新增：数据绑定
        public EnvironmentParam k_EnvironmentParam;
        public void InitKidFormWithSysparam()//完成控件绑定设置
        {
            // 20200402新增：正压阈值设置
            textBox1.DataBindings.Add("Text", k_EnvironmentParam, "AirHold", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//这里非常关键，必须保证：启用格式设置
        }

        //listview控件输入绑定：20200421批注
        private void dataGridView2_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)//编辑datagridview控件时事件：
        {
            e.Control.KeyPress -= new KeyPressEventHandler(Column1_KeyPress);
            TextBox tb = e.Control as TextBox;
            if (tb != null)
            {
                tb.KeyPress += new KeyPressEventHandler(Column1_KeyPress);
            }
        }
        private void Column1_KeyPress(object sender, KeyPressEventArgs e)//listview控件输入绑定：20200421批注
        {
            if (!char.IsControl(e.KeyChar) && (!char.IsDigit(e.KeyChar) && (e.KeyChar) != 45 && (e.KeyChar) != 46))//46为小数点:不是控制字符、既不是数字、也不是小数点；45为负号“-”
            {
                e.Handled = true;
            }
        }
        /********************************************温度电压气压设置：结束*************************************************/

        /********************************************温度电压气压监控线程：开始*********************************************/

        royal.LPADIB_PARAM adibCurState = new royal.LPADIB_PARAM();//20200401新增
        bool m_bSetEnable = false;//20200401新增
        /// <summary>
        /// 定时刷新datagridview2控件的值
        /// </summary>
        /// <param name="bState"></param>
        private void UpdateAdibInfo(bool bState)
        {
            //（1-1）刷新PPCB的连接状态
            string szInfo;
            bool bLastSet = false;//标志位：上次设置标志位           
            if (bState)//更新连接状态
            {
                //格式字符串的（1）第1个是Index,（2）第2个是字符串长度及对其方式（其中，-为左对齐，+为右对齐），（3）第3个是输出字符串的格式
                szInfo = string.Format("PPCB连接状态：[MCU:{0,5:X},FPGA:{0,5:X}]", adibCurState.nFMver, adibCurState.nLgStatus);//比较关键：20200402//nLgStatus，是个什么鬼？？？？？//"PPCB  [MCU: %X,FPGA:%X]"
            }
            else
                szInfo = "PPCB连接状态：>> disconnected ";

            //（1-2）显示PPCB的连接状态
            label38.Text = szInfo;

            //（2）更新气压、电压、温度的状态
            if (true/*m_hWnd*/ && (!bLastSet))//编辑时不查询//标志位：参数设置标志位//
            {
                //ADIB状态   nOption定义： bit[0] 版本e bit[1] 温度 bit[2] 负压 bit[3] 输入 bit[4] 电压 bit[8] 回读I2C负压 bit[16] 保持到I2C //20200329:bit[0]实际上是PPCB板卡的版本号+++bit[4]实际上是FPGA的版本号

#if true//20230322修改：临时注释
                uint nIoOption = 0;
                //if (m_bComState)//标志位：与PPCB卡建立通讯
                //    nIoOption = 0x6;//0d0110——————不再重新读取PPCB及FPGA的版本号：额外话，PPCB及FPGA各只有2个版本：20200329批注
                //else//标志位：未与PPCB卡建立通讯
                //    nIoOption = 0xF;//0d1111——————继续读取PPCB及FPGA的版本号：20200329批注
                royal.LPADIB_PARAM lpAdibparam = adibCurState;//完成对应的引用设置：20200329批注//20230322注释掉

                bLastSet = m_bSetEnable;//标志位：参数设置标志位
#else
                uint nIoOption = 0;
                /*uint*/
                nIoOption |= 0x4/*0xF*/;//nIoOption = 0d1111;

                bLastSet = m_bSetEnable;//标志位：参数设置标志位
                //20230322新增：
                m_bComState = royal.royal.DEV_AdibControl(ref adibCurState, nIoOption, false, ref m_bSetEnable);//bSetParam位的作用为0：状态为读状态：20200329批注
#endif
                for (int i = 0; i < 6; i++)
                {
                    //（1）第1种处理方式：
                    //异常处理的好处是能忽略异常，但是带来的坏处就是，速度及性能的大幅度下降
                    //try
                    //{
                    //    dataTable2.Rows[0][i] = lpAdibparam.fcurvoltage[i].ToString("F2");//浮点数格式，2位小数点
                    //    dataTable2.Rows[1][i] = lpAdibparam.fcurInkTankTemp[i].ToString("F2");
                    //    dataTable2.Rows[2][i] = lpAdibparam.fcurAirPress[i].ToString("F2");
                    //}
                    //catch { }

                    //（2）第2种处理方式：
                    //dataTable2.Rows[0][i] = lpAdibparam.fcurvoltage[i].ToString("F2");//浮点数格式，2位小数点
                    //dataTable2.Rows[1][i] = lpAdibparam.fcurInkTankTemp[i].ToString("F2");
                    //dataTable2.Rows[2][i] = lpAdibparam.fcurAirPress[i].ToString("F2");

                    //（3）第3种处理方式：
                    dataGridView2.Rows[0].Cells[i].Value = lpAdibparam/*adibCurState*//*lpAdibparam*/.fcurvoltage[i].ToString("F2");//浮点数格式，2位小数点
                    dataGridView2.Rows[1].Cells[i].Value = lpAdibparam/*adibCurState*//*lpAdibparam*/.fcurInkTankTemp[i].ToString("F2");//浮点数格式，2位小数点
                    dataGridView2.Rows[2].Cells[i].Value = lpAdibparam/*adibCurState*//*lpAdibparam*/.fcurAirPress[i].ToString("F2");//浮点数格式，2位小数点
                }
                dataGridView2.Refresh();
            }
            else if (!m_bSetEnable)
                bLastSet = false;
        }
        public bool k_bInitRoyalSuccess = false;
        //线程入口
        private void OpenVTMonitorThread(bool action)//打开温度电压监控线程：入口
        {
#if true//关闭当前的监控线程//20230322新增：
            if (action == true)//没有在加工
            {
                //开启打印线程:
                string tempThreadName = "VTMonitorThread";
                Thread tempThread = VTMonitorThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    VTMonitorThreads.Remove(tempThread);//以防万一
                }
                else
                {
                    if (k_bInitRoyalSuccess == true)//20200421新增：
                    {
                        ThreadStart initThreadEntry = new ThreadStart(VTMonitorThread);//20200220:线程入口方法修改为联动线程
                        tempThread = new Thread(initThreadEntry) { IsBackground = true };
                        tempThread.Name = tempThreadName;
                        tempThread.Start();
                        VTMonitorThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程

                    }
                    //ThreadStart initThreadEntry = new ThreadStart(VTMonitorThread);//20200220:线程入口方法修改为联动线程
                    //tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    //tempThread.Name = tempThreadName;
                    //tempThread.Start();
                    //VTMonitorThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }
            }
            else
            {
                //(1)关闭联调线程（2）关闭多轴运动：保障运动安全
                string tempThreadName = "VTMonitorThread";//(1)关闭联调线程
                DeleteThread2(tempThreadName);
            }
#endif
        }

        //20200220：线程管理的案发现场，只要是相应的线程我就存储在这里，不管线程是死是活，祖祖辈辈就在这里，便于维护及管理
        private List<Thread> VTMonitorThreads = new List<Thread>();//20200220:存放所有必要的打印机的工作线程
        public void DeleteThread2(string ThreadName)
        {
            string tempThreadName = ThreadName;
            Thread tempThread = VTMonitorThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
            if (tempThread != null)
            {
                tempThread.Abort();//20200221修改:当调用非托管线程时，有时会抛出异常但不一定及时停止
                while (tempThread.ThreadState != ThreadState.Aborted)
                { Thread.Sleep(2); }
                VTMonitorThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
            }
        }
        ////温度电压监控线程：20200401新增
        bool m_bStopMonitor = false;//VTMonitorThread线程停止标志位，默认线程为开启状态
        bool m_bComState = false;//初始状态为，PPCB不连接
        private void VTMonitorThread()/////（1）开启对应的监控线程：20200401
        {
            //（1）开启监控线程：每隔100ms间隔1次
            //ADIB状态   nOption定义： bit[0] 版本e bit[1] 温度 bit[2] 负压 bit[3] 输入 bit[4] 电压 bit[8] 回读I2C负压 bit[16] 保持到I2C //20200329:bit[0]实际上是PPCB板卡的版本号+++bit[4]实际上是FPGA的版本号
#if false//20230322新增：临时修改
            uint nIoOption = 0xF;//nIoOption = 0d1111;
#else
            uint nIoOption = 0;
            nIoOption |= 0x4;//nIoOption = 0d1111;//只读取负压值
#endif
            while (!m_bStopMonitor)
            {
                if (m_bSetEnable)//标志位：设置标志位，只有在设置无效的时候，才每隔100ms进行1次查询工作：20200329新增
                {
                    //nIoOption=0x1FF000F;
                    m_bComState = royal.royal.DEV_AdibControl(ref adibCurState, nIoOption, false, ref m_bSetEnable);//bSetParam位的作用为0：状态为读状态：20200329批注
#if false//20230322新增：临时注释
                    if (m_bComState)//标志位：与PPCB卡建立通讯
                        nIoOption = 0x6;//0d0110——————不再重新读取PPCB及FPGA的版本号：额外话，PPCB及FPGA各只有2个版本：20200329批注
                    else//标志位：未与PPCB卡建立通讯
                        nIoOption = 0xF;//0d1111——————继续读取PPCB及FPGA的版本号：20200329批注
#endif
                }
#if false//20200604新增：同时监测温度电压的数据并完成更新
                if (m_bVTSetEnable==true)
                {
                    bool nReturn = GetCurVoltageTemp(false);
                }
#endif
                //Thread.Sleep(10);//20230322注释掉：不需要等待
            }

            DeleteThread("VTMonitorThread");//20200220：本线程结束，需要及时清理相关线程//20200313新增：
        }

        private void WaveformSelectBtn_Click(object sender, EventArgs e)
        {
            //（1-2）建立文件选择对象并检查波形文件夹是否存在
            OpenFileDialog openWaveFormFileDialog = new OpenFileDialog();
            //（1-2）判断打开的路径是否存在
            if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\波形文件"))
            {
                Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\波形文件");//创建路径
            }

            //（2）设置默认打开路径及文件过滤格式
            string filePath1 = System.Windows.Forms.Application.StartupPath + @"\波形文件";//输入的CLI文件的存放目录。
            openWaveFormFileDialog.InitialDirectory = filePath1;//打开文件对话框的默认初始化目录为：软件的根目录"CLI文件
            openWaveFormFileDialog.Filter = "(*.rhdat)|*.rhdat";//过滤器————//openFileDialog也有其内部的事件处理函数
            openWaveFormFileDialog.Multiselect = false;//不可以选中多项文件
            openWaveFormFileDialog.Title = "请选择压电喷头波形文件！！！";

            //（3）读取波形文件：保存.rhdat文件路径
            if ((openWaveFormFileDialog.ShowDialog() == DialogResult.OK)
                && (openWaveFormFileDialog.FileName != string.Empty)
                /*&& (openWaveFormFileDialog.FileNames.Length==1)*/)//有且只能选中1项
            {
                //(0)更新波形文件路径
                royal.royal.g_sys_param.szWavePath = openWaveFormFileDialog.FileNames[0];//获取所有选中项的文件名
                royal.royal.DEV_UpdateParam(ref royal.royal.g_sys_param);//20200801新增：先更新波形，再加载完波形
                string msg = $"更新波形路径：DEV_UpdateParam：{{{royal.royal.g_sys_param.szWavePath}}}";
                Log4Net.Info(msg);
                string CurrentWaveName = System.IO.Path.GetFileName(royal.royal.g_sys_param.szWavePath);
                k_RYSYSParamAutoPrintParamInTest.CurrectLoadWaveName = CurrentWaveName;//20230419新增:更新当前的波形路径


                //(1)重新更新基准电压值//20230323新增：若 RYPrtCtler.dll 无 DEV_SetWaveStdVoltage 则跳过，避免崩溃
                float[] fstdVoltage = new float[4];//内存中的对应值
                for (int i = 0; i < 4; i++)
                {
                    fstdVoltage[i] = fVolt[0, i]/*0f*//*fVolt[0, i]*/;//避免潜在的BUG,设置为0时，基准电压的设定值以波形文件中为准
                }
                int size = Marshal.SizeOf(fstdVoltage[0]) * fstdVoltage.Length;
                IntPtr PfstdVoltage = Marshal.AllocHGlobal(size);
                Marshal.Copy(fstdVoltage, 0, PfstdVoltage, fstdVoltage.Length);//复制到非托管区内存
                bool returnCode;
                try
                {
                    returnCode = royal.royal.DEV_SetWaveStdVoltage(PfstdVoltage, 0, 0);//20230323批注：基准电压设置值<1时，基准电压的设定值以波形文件中为准
                    if (returnCode == false)
                    {
                        string msg2 = $"{{{0 + 1}}}号喷头基准电压更新失败：DEV_SetWaveStdVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }
                    else
                    {
                        string msg2 = $"{{{0 + 1}}}号喷头基准电压更新成功：DEV_SetWaveStdVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                        Log4Net.Info(msg2);
                    }
                }
                catch (System.EntryPointNotFoundException ex)
                {
                    Log4Net.Info($"波形加载：RYPrtCtler.dll 中未找到 DEV_SetWaveStdVoltage，跳过基准电压设置并继续加载波形。{ex.Message}");
                }
                catch (DllNotFoundException ex)
                {
                    Log4Net.Info($"波形加载：RYPrtCtler.dll 加载异常，跳过基准电压设置。{ex.Message}");
                }
                finally
                {
                    Marshal.FreeHGlobal(PfstdVoltage);
                }

                //(2)生效波形及其基准电压值，特别的，重新更新基准电压值
                float[] fVcomInWaveFile = new float[1]; fVcomInWaveFile[0] = 0.0f;
                int size2 = Marshal.SizeOf(fVcomInWaveFile[0]) * 1;
                IntPtr PfVcomInWaveFile = Marshal.AllocHGlobal(size2);
                Marshal.Copy(fVcomInWaveFile, 0, PfVcomInWaveFile, 1);//测试用:非托管区内存初始化//实际测试的时候，去掉//20200405批注

                royal.royal.DEV_ReloadWaveForm(PfVcomInWaveFile);//20200801新增：先更新波形，再加载完波形                                                               
                Marshal.Copy(PfVcomInWaveFile, fVcomInWaveFile, 0, 1);//（1）解析读取到的电压值
                msg = $"重新加载完波形：DEV_ReloadWaveForm! 当前波形文件的基准电压参考值为：{{{fVcomInWaveFile[0]}V}}";
                Log4Net.Info(msg);

                //(3)MCU调压，跳到喷头内部的电压值：20230323新增：消除潜在的BUG
                float[] fstdVoltage2 = new float[4];//内存中的对应值
                for (int i = 0; i < 4; i++)
                {
                    if (fVolt[0, i] > 1.0f)//与手动修改的电压保持一致
                    {
                        fstdVoltage2[i] = fVolt[0, i]/*19.0f*//*fVcomInWaveFile[0]*//*fVolt[d, i]*/;//20230323新增：非常关键    
                    }
                    else//与喷头电压保持一致
                    {
                        fstdVoltage2[i] = fVcomInWaveFile[0]/*fVcomInWaveFile[0]*//*fVolt[d, i]*/;//20230323新增：非常关键//与波形文件中的基准电压值保持一致    
                    }
                }
                Marshal.FreeHGlobal(PfVcomInWaveFile);
                size = Marshal.SizeOf(fstdVoltage2[0]) * fstdVoltage2.Length;
                IntPtr PfstdVoltage2 = Marshal.AllocHGlobal(size);
                Marshal.Copy(fstdVoltage2, 0, PfstdVoltage2, fstdVoltage2.Length);//复制到非托管区内存
                returnCode = royal.royal.MCU_SetPhVoltage(PfstdVoltage2, 0, 0);//微调参数值，精确设定
                if (returnCode == false)
                {
                    msg = $"{{{0 + 1}}}号喷头电压调压失败：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                    Log4Net.Info(msg);
                }
                else
                {
                    msg = $"{{{0 + 1}}}号喷头电压调压成功：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                    Log4Net.Info(msg);
                }
                Marshal.FreeHGlobal(PfstdVoltage2);
            }
        }

        private void GeneralIOTestBtn_Click(object sender, EventArgs e)
        {
            ////（0）首先把数据刷上来
            //UInt32 nInkMask = royal.royal.DEV_GetInputIO() >> 16;
            //UInt32 nInkMask = royal.royal.DEV_GetInput()/* >> 16*/;//我估计不够

            UInt32 nTempInkMask = royal.royal.DEV_GetInput();//20200417测试新增：增加墨量检测信号

            UInt32 nInkMask = ((nTempInkMask >> 16) & 0b1111)
                | ((nTempInkMask >> 10) & 0b10000)
                | ((nTempInkMask >> 10) & 0b100000)
                | ((nTempInkMask >> 19) & 0b1000000);

            ////(1) 其次把数据显示在对应控件
            //this.GeneralLabel.Text = Convert.ToString(nInkMask,X);/*nInkMask.ToString("X");*/
            //格式字符串的（1）第1个是Index,（2）第2个是字符串长度及对其方式（其中，-为左对齐，+为右对齐），（3）第3个是输出字符串的格式
            this.GeneralLabel.Text = string.Format("连接状态：\n[IO:{0,8:X}]", nInkMask);//比较关键：20200402//nLgStatus，是个什么鬼？？？？？//"PPCB  [MCU: %X,FPGA:%X]"

        }

        private void 手动操作_Shown(object sender, EventArgs e)
        {
            this.button20.Focus();//20200602新建：软件启动后的鼠标焦点设置
        }
        private void KidFormApplyBtn(object sender, FormClosingEventArgs e)//此种方式，相对于直接在应用及退出按键上的单个处理，不知道快捷多少
        {
            if ((existCorrectProcessFlag == true) || (existCorrectProcessFlag2 == true))//20230419修改：是否有校准任务存在？如果没有则可以正常退出
            {
                e.Cancel = true; // 阻止对话框关闭
                DialogResult = DialogResult.None; // 将 DialogResult 属性设置为 None
            }

            StartVTMonitorThread("Abort");//20200612修改：解决监控缓慢的问题
            if (k_bJetSetCheck == false)//20200604:如果datagridview1处于监控时
            {
                //消除存在的BUG：20200604新增
                if (this.dataGridView1.DataSource == null)//如果没有绑定，则绑定，
                {
                    //(0)dataGridView2控件重新初始化：
                    InitDataGrid1(1);
                    JetSetControlInterferenceMaintain(true);//设置状态时
                }
                else
                {

                }
            }
            else//datagridview1处于设置时
            {
                //dataGridView1.EndEdit(); bingdingSource1.EndEdit();//非常关键：20200403新增：
                ////(0)dataGridView2控件重新初始化：
                //InitDataGrid1(0);
                //JetSetControlInterferenceMaintain(false);//监控状态时:
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
        bool m_bCleanFlag = true;//20200605修改：动作指示
        private void CleanBtn_Click(object sender, EventArgs e)
        {
            bool nRetVal = false;
            if (m_bCleanFlag == false)//(b)根据ID反转背景图片
            {
                //(sender as Control).BackColor = Color.DarkOrchid;
                (sender as Control).Text = "挤墨";
            }
            else
            {
                //(sender as Control).BackColor = Color.MintCream;
                (sender as Control).Text = "关闭挤墨";
            }
            //(c)计算输出
            if (m_bCleanFlag == true)//打开和关闭闪喷：
            {
                ////                //（1）开清洗阀门（==等效：关墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                ////                OpenCloseVALVE(2 - 1, false);//20200605批注：Tag-1
                ////#if UseP5PortForCleaning
                ////                //（2-1）开泵源：
                ////                uint nIoVal = 0x1FF;//控制:P1-P2-P3~P7,依次是清洗泵、压墨泵、供墨泵1-7
                ////                nRetVal = royal.royal.DEV_SetInkPump(nIoVal);//打开压墨泵
                ////                string msg = $"开启手动冲洗喷头：";
                ////                Log4Net.Info(msg);
                ////#endif
                ////                float fPushingCleanInkCycleTime = (float)k_RYSYSParamAutoPrintParamInTest.PushingCleanInkCycleTime;//20230423：清洗控制周期
                ////                float fPushingCleanDutyCycleTime = (float)k_RYSYSParamAutoPrintParamInTest.PushingCleanDutyCycleTime;//20230423：清洗控制占空比
                ////#if UseP11PortForCleaning
                ////                //（2-2）开泵源：
                ////                nRetVal = royal.royal.DEV_SetTimer(0, fPushingCleanInkCycleTime, fPushingCleanDutyCycleTime/*1f,0.5f*/);//设置清洗泵的开启频率
                ////                nRetVal = royal.royal.DEV_EnableTimer(0,true);//设置清洗泵是否开启
                ////                string msg1 = $"开启手动冲洗喷头，端口{{{11+0}}}，清洗控制周期：{{{fPushingCleanInkCycleTime}}}S,{{{fPushingCleanDutyCycleTime}}}S";
                ////                Log4Net.Info(msg1);
                ////#endif
                EnableInkPush(true);
                m_bCleanFlag = false;


            }
            else
            {
                ////                //（1）关清洗阀门（==等效：开墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                ////                OpenCloseVALVE(2 - 1, true);//20200605批注：Tag-1

                ////#if UseP5PortForCleaning
                ////                //（2）关闭泵源：
                ////                uint nIoVal = 0x0;//控制:P1-P2-P3~P7,依次是清洗泵、压墨泵、供墨泵1-7
                ////                nRetVal = royal.royal.DEV_SetInkPump(nIoVal);//关闭压墨泵
                ////                string msg = $"关闭手动冲洗喷头：";
                ////                Log4Net.Info(msg);
                ////#endif

                ////#if UseP11PortForCleaning
                ////                //（2-2）关闭泵源：            
                ////                nRetVal = royal.royal.DEV_EnableTimer(0, false);//设置清洗泵是否开启
                ////                string msg = $"关闭手动冲洗喷头：";
                ////                Log4Net.Info(msg);
                ////#endif
                EnableInkPush(false);
                m_bCleanFlag = true;

            }
        }

        private void EnableInkPush(bool EnableFlag)//20230506新增：
        {
            bool nRetVal = false;
            if (EnableFlag == true)//开启
            {
                //（1）开清洗阀门（==等效：关墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                OpenCloseVALVE(2 - 1, true);//20200605批注：Tag-1
                float fPushingCleanInkCycleTime = (float)k_RYSYSParamAutoPrintParamInTest.PushingCleanInkCycleTime;//20230423：清洗控制周期
                float fPushingCleanDutyCycleTime = (float)k_RYSYSParamAutoPrintParamInTest.PushingCleanDutyCycleTime;//20230423：清洗控制占空比
#if UseP11PortForCleaning
                //（2-2）开泵源：
                nRetVal = royal.royal.DEV_SetTimer(0, fPushingCleanInkCycleTime, fPushingCleanDutyCycleTime/*1f,0.5f*/);//设置清洗泵的开启频率
                nRetVal = royal.royal.DEV_EnableTimer(0, true);//设置清洗泵是否开启
                string msg1 = $"开启手动清洗挤墨喷头，DEV_EnableTimer: 端口{{{11 + 0}}}，清洗控制周期：{{{fPushingCleanInkCycleTime}}}S,{{{fPushingCleanDutyCycleTime}}}S";
                Log4Net.Info(msg1);
#endif
            }
            else 
            {
#if false //20230713新建批注：关闭阀门
                //（1）关清洗阀门（==等效：开墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                OpenCloseVALVE(2 - 1, true);//20200605批注：Tag-1
#endif

#if UseP11PortForCleaning
                //（2-2）关闭泵源：            
                nRetVal = royal.royal.DEV_EnableTimer(0, false);//设置清洗泵是否开启
                string msg = $"关闭手动清洗挤墨喷头：DEV_EnableTimer";
                Log4Net.Info(msg);
#endif

                Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime * 1000));//压墨等待一段时间
                msg = $"压墨等待结束,等待：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                Log4Net.Info(msg);

#if true //20230713新建批注：切换阀门
                //（1）关清洗阀门（==等效：开墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                OpenCloseVALVE(2 - 1, false);//20200605批注：Tag-1
                Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime2 * 1000));//压墨等待一段时间
                msg = $"切换完成，继续等待：{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime2}} S}}";
#endif
            }
        }

        private void button20_Click(object sender, EventArgs e)//20200619批注
        {
            SaveJsonFile();//20201020新增：
#if false//
            k_UVLightParam.m_nFrequency = Convert.ToInt32(this.CycleBox.Text);
            k_UVLightParam.m_fPower = Convert.ToSingle(this.ValidBox.Text);
            k_UVLightParam.nMinPos[0] = Convert.ToInt32(this.UV1LimitUp.Text);
            k_UVLightParam.nMinPos[1] = Convert.ToInt32(this.UV2LimitUp.Text);
            k_UVLightParam.nMaxPos[0] = Convert.ToInt32(this.UV1LimitDown.Text);
            k_UVLightParam.nMaxPos[1] = Convert.ToInt32(this.UV2LimitDown.Text);
#endif
        }

        /***********************************喷头擦拭逻辑*********************************/
        //double RollerParam = 1;//20200918新增：铺粉辊子同双驱速度之比:对于铺粉运动很关键
        public double[] m_dScrapervel = new double[2] { 150, 150 };//历史刮墨双运动速度参数；20260416起不再驱动轴7/8
        public bool[] m_bScraperEnds = new bool[2] { false, false };//历史刮墨双运动AB端定义；20260416起不再驱动轴7/8
        public bool CorrectFlag = false;//20200919新增：系统校准标志位
        //public double[] m_Step = new double[2]{ 150, 450};//20200918新增：刮墨主运动、刮墨副步进距离
        //public bool[] m_bMoveModeFlag = new bool[2]{ true, true};//20200918新增：刮墨主运动、刮墨副运动类型
        /// <summary>
        /// 20200918新增：刮墨动作逻辑，原先耦合轴7/8。
        /// 20260416起轴7/8已重映射为铺粉车/成型缸，此处停用旧耦合动作，避免误驱动新机构。
        /// </summary>
        private void ScraperMotionLogic()//20200918新增：精华
        {
            if (CorrectFlag)//墨车双驱系统是否进行过校准
            {
                BackToStation(155.6, 50, false, true, 1);//移动到155.6MM行程处//ExchangeToSNexttation(155.6);//运动至压墨回收站//Thread.Sleep(100);//BackToStation(22.715);//移动到22.715MM行程处
                ShoveCommand(true, 3);//20200922新增：压墨3秒钟

                ExchangeToSNexttation(22.715);//运动至清洗站//Thread.Sleep(100);//回位
                LaserADD_BinderJetter.MoveComponent AutomoveComponent = new LaserADD_BinderJetter.MoveComponent();

                if (RollerDirectionFlag == false)//辊子运动方向为反向：20200925新增：
                {
                    RollerParam = -System.Math.Abs(RollerParam);
                }
                else//辊子运动方向为正向：20200925新增：
                {
                    RollerParam = System.Math.Abs(RollerParam);
                }
                Log4Net.Info("ScraperMotionLogic：轴7/8旧刮墨耦合动作已停用，避免与当前铺粉车轴7、成型缸轴8定义冲突。");

                ExchangeToSNexttation(/*155.6*/280);//运动至压墨回收站//Thread.Sleep(100);

                ExchangeToSNexttation(45/*22.715*/);//运动至清洗站//Thread.Sleep(100);//回位//20201021修改：运动至45mm处即可

                //添加新逻辑：20210121新增
                //添加开启闪喷逻辑：预防刮墨清洗之后导致的喷头局部出墨异常
                //输入参数为：闪喷时间

                int CleanSpartTime = (int)k_RYSYSParamAutoPrintParamInTest.m_dCleanSparkTime * 1000;//实时生效清洗闪喷时长设置：20200122新增

                bool nRetVal = MeteorPrintEngine.SetFlash(true);//打开闪喷
                Thread.Sleep(CleanSpartTime/*3000*/);//开启闪喷3秒钟
                nRetVal = MeteorPrintEngine.SetFlash(false);//打开闪喷
            }
            else
            {
                MessageBox.Show("系统未校准，请先校准！");
            }
        }

        private void ExchangeToSNexttation(double AimPos)//运动切换到新工作站区域：固高 TrapMotion
        {
            float m_MovSpeed = 50f;
            double[] enc = motionMap.GetEncPos();
            double currentMm = enc[0] / EncoderLinePerMM;
            int moveCounts = (int)((AimPos - currentMm) * EncoderLinePerMM);
            double velCountPerMs = m_MovSpeed * DriverPulsePerMM / 1000.0;

            gts.mc.TTrapPrm xTrapPrm = new gts.mc.TTrapPrm();
            xTrapPrm.acc = 0.5; xTrapPrm.dec = 0.5; xTrapPrm.velStart = 5; xTrapPrm.smoothTime = 1;
            motionMap.TrapMotion(1, ref xTrapPrm, moveCounts, velCountPerMs, 0, 0, true);
            motionMap.StopMotion(1, true);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="AimPos"></param>
        /// <param name="m_MovSpeed"></param>
        /// <param name="WaitStopFLag"></param>
        private void BackToStation2(double AimPos, double m_MovSpeed, bool WaitStopFLag)//20220520新建：铺粉轴运动至指定区域：AimPos位置单位为MM
        {
            double CurrentPos = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
            double TrapSpace = AimPos - CurrentPos;
            TrapMoveUp(7, true, Convert.ToString(m_MovSpeed), Convert.ToString(TrapSpace), true, !WaitStopFLag/*true*/);//20260416修改：当前铺粉车运动轴改为轴7
        }
        private void BackToStation3(double AimPos, double m_MovSpeed, bool WaitStopFLag)//20220528新建：刮墨轴运动到指定为位置：AimPos位置单位为MM
        {
            //double CurrentPos = GetCurrentPos(4);//20220520新建：读取指定轴的当前编码器位置
            //double TrapSpace = AimPos - CurrentPos;
            //TrapSpace = TrapSpace / 12800;//转数
            //m_MovSpeed = m_MovSpeed / 12800;//转每秒
            TrapMoveUp(4, true, Convert.ToString(m_MovSpeed), Convert.ToString(AimPos), true, !WaitStopFLag/*true*/);//20200528批注：TrapSpace量为转数，m_MovSpeed为转/秒
        }

        /// <summary>璇诲彇鎸囧畾杞村綋鍓嶇紪鐮佸櫒浣嶇疆锛坢m锛夈€?X/Y 杞寸敤鍚勮嚜鐨勭數瀛愰娇杞︽瘮杩涜鎹㈢畻銆?/summary>
        private double GetCurrentPos(int Axis)//20220520鏂板缓锛氳鍙栨寚瀹氳酱鐨勫綋鍓嶇紪鐮佸櫒浣嶇疆
        {
            try
            {
                Log4Net.Info($"GetCurrentPos: enter, Axis={Axis}");
                double[] g_dEncpos = motionMap.GetEncPos();
                if (g_dEncpos == null)
                {
                    Log4Net.Info($"GetCurrentPos: enc array is null, Axis={Axis}");
                    return 0;
                }

                if (g_dEncpos.Length < Axis)
                {
                    Log4Net.Info($"GetCurrentPos: enc array length insufficient, Axis={Axis}, length={g_dEncpos.Length}");
                    return 0;
                }

                double countPerMM = GetInkCarCountPerMM(Axis);
                double rawCount = g_dEncpos[Axis - 1];
                double currentMm2 = rawCount / countPerMM;
                Log4Net.Info($"GetCurrentPos: exit, Axis={Axis}, rawCount={rawCount}, countPerMM={countPerMM}, currentMm={currentMm2:F3}");
                return currentMm2;
            }
            catch (Exception ex)
            {
                Log4Net.Error($"GetCurrentPos: exception, Axis={Axis}, ex={ex}");
                throw;
            }
        }

        /// <summary>墨车 X/Y（轴1/2）与光栅口径一致：1000 count/mm；其它轴默认 1000。</summary>
        private double GetInkCarCountPerMM(int Axis)
        {
            if (Axis == 1 || Axis == 2)
                return EncoderLinePerMM;
            return 1000.0;
        }

        public void LogInkCarAxisSnapshot(string context)
        {
            try
            {
                LogInkCarSingleAxisSnapshot(1, $"{context}（X）");
                LogInkCarSingleAxisSnapshot(2, $"{context}（Y）");
                LogInkCarSingleAxisSnapshot(6, $"{context}（Y物理轴6）");
            }
            catch (Exception ex)
            {
                Log4Net.Error($"{context}: 墨车轴状态快照异常：{ex}");
            }
        }

        private void LogInkCarSingleAxisSnapshot(short axis, string context)
        {
            try
            {
                if (motionMap == null)
                {
                    Log4Net.Info($"{context}: motionMap为空");
                    return;
                }

                int axisStatus = 0;
                motionMap.GetAxisStatus(axis, out axisStatus);
                motionMap.ReadAxisSate(axis);

                double[] encPos = motionMap.GetEncPos();
                double rawCount = double.NaN;
                double currentMm = double.NaN;
                if (encPos != null && encPos.Length >= axis)
                {
                    rawCount = encPos[axis - 1];
                    double countPerMm = GetInkCarCountPerMM(axis);
                    if (countPerMm != 0)
                    {
                        currentMm = rawCount / countPerMm;
                    }
                }

                bool posLimit = motionMap.axisSateMonitor.FlagPosLimit1;
                bool negLimit = motionMap.axisSateMonitor.FlagNegLimit1;
                string rawCountText = double.IsNaN(rawCount) ? "NaN" : rawCount.ToString("F0");
                string currentMmText = double.IsNaN(currentMm) ? "NaN" : currentMm.ToString("F3");
                Log4Net.Info($"{context}: Axis={axis}, rawCount={rawCountText}, currentMm={currentMmText}, axisStatus=0x{axisStatus:X}, PosLimit={posLimit}, NegLimit={negLimit}");
            }
            catch (Exception ex)
            {
                Log4Net.Error($"{context}: Axis={axis} 轴状态快照异常：{ex}");
            }
        }

        private void WaitStop(int Axis)//20220520鏂板缓锛氬疄鐜板ⅷ杞︾浉鍏崇殑绛夊仠閫昏緫
        {
            Log4Net.Info($"WaitStop: enter, Axis={Axis}");
            if (Axis == 1 || Axis == 2)
            {
                // 墨车 X/Y 已由固高 TrapMotion 内部等停，不再依赖 Royal，避免卡顿
                Log4Net.Info($"WaitStop: Axis={Axis} is inkcar (Googol) direct return");
                return;
            }
            // ========== Royal控制逻辑（非墨车轴等停，墨车轴1/2 已在上方 return，保留供参考）==========
            bool Directory = false; uint nRevPls = 0;
            DateTime waitStart = DateTime.Now;
            DateTime lastHeartbeat = waitStart;
            while (royal.royal.DEM_AxisIsRuning((uint)(Axis - 1), ref Directory, ref nRevPls))
            {
                if ((DateTime.Now - lastHeartbeat) >= TimeSpan.FromSeconds(3))
                {
                    Log4Net.Info($"WaitStop: waiting, Axis={Axis}, elapsedMs={(DateTime.Now - waitStart).TotalMilliseconds:F0}, directory={Directory}, nRevPls={nRevPls}");
                    lastHeartbeat = DateTime.Now;
                }
                Thread.Sleep(1);
            }
            bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, (uint)Axis);
            Log4Net.Info($"轴{Axis} 到位等停(非墨车)：DEM_StopAxisRun={nRetVal2}");
            Log4Net.Info($"WaitStop: exit, Axis={Axis}, elapsedMs={(DateTime.Now - waitStart).TotalMilliseconds:F0}");
        }


        /// <summary>
        /// 墨车返回指定工作位置
        /// </summary>
        /// <param name="AimPos">工作位置的墨车坐标系的绝对坐标值</param>
        /// <param name="m_MovSpeed">墨车移动速度</param>
        /// <param name="MoveDirectionFlag">移动轴选择，false - X轴，true - Y轴</param>
        /// <param name="WaitStopFLag">等待标志</param>
        /// <param name="CorrectionRatio">修正系数</param>
        /// <note> 修改X轴与Y轴的行程范围，2024/04/12，Leon</note>
        private void BackToStation(double AimPos, float m_MovSpeed, bool MoveDirectionFlag, bool WaitStopFLag, double CorrectionRatio)//运动至初始区域：AimPos位置单位为MM：精华
        {
            try 
            {   
                Log4Net.Info($"BackToStation: enter, AimPos={AimPos:F3}, m_MovSpeed={m_MovSpeed:F3}, MoveDirectionFlag={(MoveDirectionFlag ? "Y" : "X")}, WaitStopFlag={WaitStopFLag}, CorrectionRatio={CorrectionRatio:F3}");
                switch (MoveDirectionFlag)
                    {
                        case false://X 轴：固高编码器定位
                            if (AimPos >= 5 && AimPos <= (XMaxDistanceMM - 5))
                            {
                                Log4Net.Info($"BackToStation X: before GetEncPos, AimPos={AimPos:F3}");
                                double[] encX = motionMap.GetEncPos();
                                double currentMmX = encX[0] / EncoderLinePerMM;
                                Log4Net.Info($"BackToStation X: encoder raw={encX[0]}, currentMm={currentMmX:F3}");
                                int moveCountsX = (int)((AimPos - currentMmX) * EncoderLinePerMM);
                                double velCountPerMsX = m_MovSpeed * DriverPulsePerMM / 1000.0;

                                string msg = $"X 固高定位：当前{{{currentMmX:F3}}}mm 目标{{{AimPos}}}mm 相对{{{moveCountsX}}}count 速度{{{velCountPerMsX:F1}}}count/ms";
                                Log4Net.Info(msg);

                                gts.mc.TTrapPrm xTrapPrm = new gts.mc.TTrapPrm();
                                xTrapPrm.acc = 0.5; xTrapPrm.dec = 0.5; xTrapPrm.velStart = 5; xTrapPrm.smoothTime = 1;
                                Log4Net.Info($"BackToStation X: before TrapMotion, moveCountsX={moveCountsX}, velCountPerMsX={velCountPerMsX:F3}, waitStop={WaitStopFLag}");
                                motionMap.TrapMotion(1, ref xTrapPrm, moveCountsX, velCountPerMsX, 0, 0, WaitStopFLag);
                                Log4Net.Info("BackToStation X: after TrapMotion");

                                if (WaitStopFLag)
                                {
                                    Log4Net.Info("BackToStation X: before StopMotion");
                                    motionMap.StopMotion(1, true);
                                    Log4Net.Info("BackToStation X: after StopMotion");
                                }
                                Log4Net.Info("BackToStation X: before encoder readback");
                                double afterMm = motionMap.GetEncPos()[0] / EncoderLinePerMM;
                                Log4Net.Info($"X 到位：编码器位置{{{afterMm:F3}}}mm");
                                LogInkCarAxisSnapshot($"BackToStation X 结束后轴快照，AimPos={AimPos:F3}");
                            }
                            break;

                        case true://Y 轴：固高编码器定位
                            if (AimPos >= 1 && AimPos <= (YMaxDistanceMM - 10))
                            {
                                Log4Net.Info($"BackToStation Y: before GetEncPos, AimPos={AimPos:F3}");
                                double[] encY = motionMap.GetEncPos();
                                double currentMmY = encY[1] / EncoderLinePerMM;
                                Log4Net.Info($"BackToStation Y: encoder raw={encY[1]}, currentMm={currentMmY:F3}");
                                int moveCountsY = (int)((AimPos - currentMmY) * EncoderLinePerMM);
                                double velCountPerMsY = m_MovSpeed * DriverPulsePerMM / 1000.0;

                                string msg = $"Y 固高定位：当前{{{currentMmY:F3}}}mm 目标{{{AimPos}}}mm 相对{{{moveCountsY}}}count 速度{{{velCountPerMsY:F1}}}count/ms";
                                Log4Net.Info(msg);

                                gts.mc.TTrapPrm y1TrapPrm = new gts.mc.TTrapPrm();
                                y1TrapPrm.acc = 0.5; y1TrapPrm.dec = 0.5; y1TrapPrm.velStart = 5; y1TrapPrm.smoothTime = 1;
                                Log4Net.Info($"BackToStation Y: before TrapMotion, moveCountsY={moveCountsY}, velCountPerMsY={velCountPerMsY:F3}, waitStop={WaitStopFLag}");
                                motionMap.TrapMotion(2, ref y1TrapPrm, moveCountsY, velCountPerMsY, 0, 0, WaitStopFLag);
                                Log4Net.Info("BackToStation Y: after TrapMotion");

                                try { YAxisMoveStarted?.Invoke(); } catch (Exception ex) { Log4Net.Info($"YAxisMoveStarted 回调异常：{ex.Message}"); }

                                if (WaitStopFLag)
                                {
                                    Log4Net.Info("BackToStation Y: before StopMotion");
                                    motionMap.StopMotion(2, true);
                                    Log4Net.Info("BackToStation Y: after StopMotion");
                                }
                                Log4Net.Info("BackToStation Y: before encoder readback");
                                double afterMmY = motionMap.GetEncPos()[1] / EncoderLinePerMM;
                                Log4Net.Info($"Y 到位：编码器位置{{{afterMmY:F3}}}mm");
                                LogInkCarAxisSnapshot($"BackToStation Y 结束后轴快照，AimPos={AimPos:F3}");
                            }
                            break;
                    }
            }
            catch (Exception e)
            { 
                string msg = e.ToString();
                Log4Net.Info(msg);//20230315新建：解决20230314打印94层中途停止的潜在问题
                MessageBox.Show(msg);
            }
        }
        private void ShoveCommand(bool OpenFlag, double HoldTime)//20200922新增：连续喷墨HoldTime,单位为秒
        {
            int nInkMask = 0; int myTag = 1;/*来自正负压事件位置*/

            RoyalMap.m_bEnable[myTag - 1] = OpenFlag;//(1)开始挤墨控制位               
            for (int i = 0; i < 8; i++) { if (RoyalMap.m_bEnable[i]) nInkMask |= (1 << i); }
            royal.royal.DEV_SetMcbOutPut(0, (UInt32)nInkMask);

            Thread.Sleep((int)(HoldTime * 1000));//（2）保持time秒

            nInkMask = 0;
            RoyalMap.m_bEnable[myTag - 1] = !OpenFlag;//(1)开始挤墨控制位            
            for (int i = 0; i < 8; i++) { if (RoyalMap.m_bEnable[i]) nInkMask |= (1 << i); }
            royal.royal.DEV_SetMcbOutPut(0, (UInt32)nInkMask);
        }

        private void ScraperNozzleBtn_Click(object sender, EventArgs e)//：精华
        {
            //ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑//20210121修改：添加了3秒的闪喷逻辑
        }

        private void RollerFlag_SelectedIndexChanged(object sender, EventArgs e)
        {
            int SelectedFlag /*RollerDirectionFlag*/ = Convert.ToInt32(this.RollerFlag.SelectedIndex);//20200925新增
            if (SelectedFlag == 0)
            {
                RollerDirectionFlag = true;
            }
            else if ((SelectedFlag == 1))
            {
                RollerDirectionFlag = false;
            }
            else
            { }
        }

        //开启和关闭闪喷功能
        bool m_bFlashFlag = true;//动作为开启
        private void FlashBtn_Click(object sender, EventArgs e)
        {
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

                m_bFlashFlag = false;

                string msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal}}}";
                Log4Net.Info(msg);
            }
            else
            {
                bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷
                m_bFlashFlag = true;

                string msg = $"关闭闪喷： IDP_FlashPrtCtl(false)：ReturnCode{{{nRetVal}}}";
                Log4Net.Info(msg);
            }
        }
        private void StartCloseCureLight(bool EnableFlag, int CurePowerDensity, double CureBackSpeed, out double ReturnVelocity1, out double ReturnVelocity2, out bool DoubleCureEnabled)//按照虚拟编码器位置，打开及关闭UV灯
        {
            ////(1)初始化UV灯开启范围及限位值：
            //Int32[] nMinPos = new Int32[2];
            //Int32[] nMaxPos = new Int32[2];
            //string m_szUV1Pos1 = (k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOn).ToString();//灯1的上限//20201029修改
            //string m_szUV1Pos2 = (k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOff).ToString();//灯1的下限//20201029修改
            //string m_szUV2Pos1 = (k_RYSYSParamAutoPrintParamInTest.m_dRightCureOn).ToString();//灯2的上限//20201029修改
            //string m_szUV2Pos2 = (k_RYSYSParamAutoPrintParamInTest.m_dRightCureOff).ToString();//灯2的下限//20201029修改
            //nMinPos[1] = (int)(Convert.ToDouble(m_szUV2Pos1) * 200);//字符串转数值
            //nMaxPos[1] = (int)(Convert.ToDouble(m_szUV2Pos2) * 200);//字符串转数值
            //nMinPos[0] = (int)(Convert.ToDouble(m_szUV1Pos1) * 200);//字符串转数值
            //nMaxPos[0] = (int)(Convert.ToDouble(m_szUV1Pos2) * 200);//字符串转数值
            //int size = Marshal.SizeOf(nMinPos[0]) * nMinPos.Length;//数组1的指针
            //IntPtr PnMinPos = Marshal.AllocHGlobal(size);
            //int size2 = Marshal.SizeOf(nMaxPos[0]) * nMaxPos.Length;//数组2的指针
            //IntPtr PnMaxPos = Marshal.AllocHGlobal(size2);
            //Marshal.Copy(nMinPos, 0, PnMinPos, nMinPos.Length);//复制到非托管区内存
            //Marshal.Copy(nMaxPos, 0, PnMaxPos, nMaxPos.Length);//复制到非托管区内存

            //(1)调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
            /*double*/
            ReturnVelocity1 = 50;//固化灯速度：默认为100mm/s
            /*double*/
            ReturnVelocity2 = 150;
            bool LightEnabled1 = false;
            bool LightEnabled2 = false;
            DoubleCureEnabled = false;

            switch (CurePowerDensity)
            {
                //低功率：
                case 0://0mJ/cm2
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 0;//信号
                    ReturnVelocity1 = /*150*/CureBackSpeed;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = false;//无灯
                    LightEnabled2 = false;//无灯
                    DoubleCureEnabled = false;//单次

                    break;
                case 25://25mJ/cm2
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 0;//信号
                    ReturnVelocity1 = 224;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = false;
                    LightEnabled2 = true;//单灯
                    DoubleCureEnabled = false;//单次

                    break;
                case 50://50mJ/cm2
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 0;//信号
                    ReturnVelocity1 = 112;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = false;
                    LightEnabled2 = true;//单灯
                    DoubleCureEnabled = false;//单次

                    break;
                case 100:
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 0;//信号
                    ReturnVelocity1 = 55.3;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = false;
                    LightEnabled2 = true;//单灯
                    DoubleCureEnabled = false;//单次

                    break;

                //中高功率：
                case 200:
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 57.5;//信号
                    ReturnVelocity1 = 200;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = true;//双灯
                    LightEnabled2 = true;
                    DoubleCureEnabled = false;//单次

                    break;
                case 400:
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 57.5;//信号
                    ReturnVelocity1 = 100;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = true;//双灯
                    LightEnabled2 = true;
                    DoubleCureEnabled = false;//单次

                    break;
                case 800:
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 57.5;//信号
                    ReturnVelocity1 = 49.5;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = true;//双灯
                    LightEnabled2 = true;
                    DoubleCureEnabled = false;//单次

                    break;
                case 1600:
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 57.5;//信号
                    ReturnVelocity1 = 24.27;
                    ReturnVelocity2 = /*150*/CureBackSpeed;
                    LightEnabled1 = true;//双灯
                    LightEnabled2 = true;
                    DoubleCureEnabled = false;//单次

                    break;

                //高功率：
                case 3200:
                    k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency = 125;//信号
                    k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio = 57.5;//信号
                    ReturnVelocity1 = 24.27;
                    ReturnVelocity2 = 24.27;
                    LightEnabled1 = true;//双灯
                    LightEnabled2 = true;
                    DoubleCureEnabled = true;//双次

                    break;
            }

            //(2)初始化UV灯开启范围及限位值：
            Int32[] nMinPos = new Int32[2];
            Int32[] nMaxPos = new Int32[2];
            string m_szUV1Pos1 = (k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOn).ToString();//灯1的上限//20201029修改
            string m_szUV1Pos2 = (k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOff).ToString();//灯1的下限//20201029修改
            string m_szUV2Pos1 = (k_RYSYSParamAutoPrintParamInTest.m_dRightCureOn).ToString();//灯2的上限//20201029修改
            string m_szUV2Pos2 = (k_RYSYSParamAutoPrintParamInTest.m_dRightCureOff).ToString();//灯2的下限//20201029修改
            if (LightEnabled1 == false)//开启右灯的时候，不开启左灯
            {
                nMinPos[1] = (int)(Convert.ToDouble(m_szUV2Pos1) * 200);//字符串转数值
                nMaxPos[1] = (int)(Convert.ToDouble(m_szUV2Pos2) * 200);//字符串转数值
                nMinPos[0] = (int)((Convert.ToDouble(m_szUV1Pos1) + 1500) * 200);//字符串转数值//20210623:添加1.5m偏移，使左灯无效
                nMaxPos[0] = (int)((Convert.ToDouble(m_szUV1Pos2) + 1500) * 200);//字符串转数值//20210623:添加1.5m偏移，使左灯无效
            }
            else//开启右灯的时候，开启左灯
            {
                nMinPos[1] = (int)(Convert.ToDouble(m_szUV2Pos1) * 200);//字符串转数值
                nMaxPos[1] = (int)(Convert.ToDouble(m_szUV2Pos2) * 200);//字符串转数值
                nMinPos[0] = (int)(Convert.ToDouble(m_szUV1Pos1) * 200);//字符串转数值
                nMaxPos[0] = (int)(Convert.ToDouble(m_szUV1Pos2) * 200);//字符串转数值
            }

            int size = Marshal.SizeOf(nMinPos[0]) * nMinPos.Length;//数组1的指针
            IntPtr PnMinPos = Marshal.AllocHGlobal(size);
            int size2 = Marshal.SizeOf(nMaxPos[0]) * nMaxPos.Length;//数组2的指针
            IntPtr PnMaxPos = Marshal.AllocHGlobal(size2);
            Marshal.Copy(nMinPos, 0, PnMinPos, nMinPos.Length);//复制到非托管区内存
            Marshal.Copy(nMaxPos, 0, PnMaxPos, nMaxPos.Length);//复制到非托管区内存

            //(3)执行UV灯：设置UV灯开启范围及其信号周期、有效时间、是否开2灯：
            double m_szCycleSec = k_RYSYSParamAutoPrintParamInTest.m_dCureFrequency/*m_nCureFrequency*/;//灯1的上限//20201029修改
            double m_szValidSec = k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio;//灯1的上限//20201029修改
            if (0 <= k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio
                && k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio <= 100)//20210619新建，判断
            {
                m_szValidSec = k_RYSYSParamAutoPrintParamInTest.m_dDutyRatio;//灯1的上限//20201029修改
            }
            float m_fCycleSec = (float)(1 / m_szCycleSec);//20200304：50ms——(1)125Hz，对应的是8ms(2)单位时ms还是us//20200407新增确认，单位是S
            float m_fValidSec = (float)(m_fCycleSec * m_szValidSec / 100);//20200304：25ms——(1)1ms=12.5%功率，2ms=25%功率（2）m_fValidSec=m_fCycleSec*power/100;//power/100


            bool nRetVal = royal.royal.DEV_EnableUVPosCtlOut(false, false);//（0）设置UV等使能开启
            if (EnableFlag)
            {
                bool nRetVal1 = royal.royal.DEV_SetUVLampPosRange(PnMinPos, PnMaxPos);//（1）设置UV灯使能开启
                bool returnCode = royal.royal.DEV_SetPwmParam(true/*Param1Check.Checked*//*true*/, m_fCycleSec, m_fValidSec);//（2）设置UV灯功率输出：//20200604批注：取消UV灯参数使能按钮
                bool nRetVal2 = royal.royal.DEV_EnableUVPosCtlOut(/*LightEnabled1*/true, true /*LightEnabled2*//*true, true*/);//（3）打开两侧的UV灯：//20210621修改:
            }
            Marshal.FreeHGlobal(PnMinPos);//释放内存
            Marshal.FreeHGlobal(PnMaxPos);//释放内存
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
                while (tempThread.ThreadState != ThreadState.Aborted)
                { Thread.Sleep(100); }
                AutoPrintThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
            }
        }
        public int k_nCurrentLayer = 0;//当前的打印进度
        public float k_fBackCleanMovSpeed = 100;

        /// <summary>与自动打印 Command2 一致：监控关闭时用占位对象，避免 NewAutoSupplyPowderThread2* 对 toCamera 空引用。</summary>
        private SendMessageToCamera _powderMonitorStub = new SendMessageToCamera(false);

        /// <summary>
        /// 自动清洗线程
        /// </summary>
        /// 
        /// !!!********** 本线程由"自动清洗"按钮触发，只执行一次！ **********!!!
        /// 
        ///     1. 修改清洗站位置坐标：(850.0,10.0) ✓ 2024/04/12
        ///     2. 修改回清洗站路径，先移动X轴，当X轴坐标到达安全区域后，同时移动Y轴 ✓ 2024/04/12
        [Obsolete("与自动打印不一致：UI与主流程已统一为 AutoCleanThread2（EquipmentMotionLogic3 Command1 / UiThreadEntry_AutoCleanThread2）。工艺调试请只改 AutoCleanThread2。")]
        public void AutoCleanThread(/*float m_BackCleanMovSpeed*/)//20220520修改及注释：线程内容：自动清洗动作
        {
            string msg = $"进入自动清洗过程：AutoCleanThread";
            Log4Net.Info(msg);

#if true//20220518新建：新设备使用的自动清洗逻辑//20230401之后逻辑
            //（1）撒粉轴找回零位：20220527新建：
            double SinkHomePosition = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;//刮墨轴的HOME位置
            double AimScraperPosition = k_RYSYSParamAutoPrintParamInTest.m_dSraperAngleOffHome +15;//刮墨片限位片位置有15度的偏差，进行补偿
            //(AimScraperPosition+ SinkHomePosition)

            //20230331修改为0.5圈/s,避免飞溅
            bool ReturnCode = motionMap.SetBackSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*//*1*//*0.5*/, 2, -SinkHomePosition);//旋转速度：0.5 圈/s//下//20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
            if (ReturnCode == true)
            {
                msg = $"刮墨轴回零成功：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                Log4Net.Info(msg);
                /*MessageBox.Show("回零成功");*/
            }//校准成功
            else
            {
                msg = $"刮墨轴回零失败：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                Log4Net.Info(msg);

                MessageBox.Show("回零失败");
            }

            //(2) 刮片竖直位置：-------------------------------------------------------------------------->          
            int subdivided = 12800; double/*int*/ SinkPostion = 0;

            //（3）联动清洗逻辑
            double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/
            double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
            float ReturnVelocity2 = k_fBackCleanMovSpeed;

            double CleanNozzleSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCleanCarSpeed/*m_szCleanSpeed*/;//20230331修改
            int CleanTimes = k_RYSYSParamAutoPrintParamInTest.m_nCleanTimes/*m_nCleanTime*//*3*/;//默认清洗次数为3次//20230331修改
            for (int i = 0; i < CleanTimes/*3*/; i++)//i为清洗总次数；PASS宽度为喷头宽度//默认清洗次数为3次
            {
                if (i == 0)
                {
                    /*
                     * 墨车回清洗站压墨区，先启动Y轴，新的硬件会产生碰撞！！！！
                     * 2024/04/15 已经修正自动清洗功能，Leon
                     *      1. 需要改先启动X轴！  
                     *      2. 加入判断，X轴在安全区域时同时移动Y轴
                     *      3. 修改坐标值
                     */
                    double pos_x;

                    // 先移动X轴
                    BackToStation(INKCAR_CLEAN_STATION_X, (float)ReturnVelocity2, false, false, 1.0);

                    // 判断X坐标是否已经超出SOA的最大X坐标位
                    do
                    {
                        Thread.Sleep(100);

                        pos_x = GetCurrentPos(1);
                    } while (pos_x < INKCAR_SOA_MAX_X);

                    // X轴已经安全，移动Y轴
                    //BackToStation(116/*96 + 25*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//停靠在里侧，向外侧步进喷头幅 面^^^^^^^^^^^^^^^^^//96MM
                    BackToStation(INKCAR_CLEAN_STATION_Y, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);    // Leon, 2024/04/15
                }
                else//第2次刮墨也需要回零
                {
                   
                    SinkPostion = (AimScraperPosition + SinkHomePosition)/*180*/;//20230411修改：避免回零时碰撞喷头压墨收集盒                                                             
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*/, -SinkPostion);

                    //20230331修改为0.5圈/s,避免飞溅
                    ReturnCode = motionMap.SetBackSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*//*1*//*0.5*/, 2, -SinkHomePosition);//旋转速度：0.5 圈/s//下//20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
                    if (ReturnCode == true)
                    {
                        msg = $"刮墨轴回零成功：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                        Log4Net.Info(msg);
                        /*MessageBox.Show("回零成功");*/
                    }//校准成功
                    else
                    {
                        msg = $"刮墨轴回零失败：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                        Log4Net.Info(msg);

                        MessageBox.Show("回零失败");
                    }

                    /*
                     * 墨车重回清洁站压墨区
                     */
                    BackToStation(INKCAR_CLEAN_STATION_X, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);   // Leon, 2024/04/15
                }
                /*
                 * X轴回清洁站
                 */
                //BackToStation(780/*710*//*425*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);//停靠在右侧，向左侧运动打印幅<---------------//780MM

                bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷//20230327批注：关闭闪喷
                msg = $"关闭闪喷： IDP_FlashPrtCtl(false)：ReturnCode{{{nRetVal}}}";
                Log4Net.Info(msg);
                if (i == 0)//第1次
                {
                    //(a)沾一下墨水
                    SinkPostion = (75 - SinkHomePosition)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可//20230417批注：75为刮片限位片与理想距离的误差
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                    //(b)到刮墨位
                    SinkPostion = -(AimScraperPosition + 75)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                    //20230416修改：压墨之前，需要确保开启闪喷功能
                    if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)    // 如果闪喷
                    {
                        bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                        msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal2}}}";
                        Log4Net.Info(msg);

                        m_bFlashFlag = false;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                    }

                    //20220915修改：修改打印方式
//#if UseDirectPushInkMode
//                    EnableInkPush(true);
//#else
//                    motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
//                    msg = $"开启压墨： motionMap.SetDo(13, true)";
//                    Log4Net.Info(msg);
//#endif
                    if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                    {
                        EnableInkPush(true);
                        Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
                    }
                    else//间接压墨
                    {
                        motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                        msg = $"开启压墨： motionMap.SetDo(13, true)";
                        Log4Net.Info(msg);
                        Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime2 * 1000)/*3000*/);//20220915新建：暂停2.0 s
                    }

                    //Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
//#if UseDirectPushInkMode
//                    EnableInkPush(false);
//#else
//                    motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
//                    msg = $"关闭压墨： motionMap.SetDo(13, false)";
//                    Log4Net.Info(msg);
//#endif
                    if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                    {
                        EnableInkPush(false); //内含压墨等待一段时间，切换阀门
                    }
                    else//间接压墨
                    {
                        motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                        msg = $"关闭压墨： motionMap.SetDo(13, false)";
                        Log4Net.Info(msg);

                        msg = $"压墨等待开启：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                        Log4Net.Info(msg);
                        Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime * 1000));//压墨等待一段时间
                        msg = $"压墨等待完成：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                        Log4Net.Info(msg);
                    }

                    //20230416修改：压墨之后，需要关闭闪喷功能
                    if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
                    {
                        bool nRetVal2 = MeteorPrintEngine.SetFlash(false);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                        msg = $"开启闪喷： IDP_FlashPrtCtl(false)：ReturnCode{{{nRetVal2}}}";
                        Log4Net.Info(msg);

                        m_bFlashFlag = true;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                    }

                }
                else
                {
                    //(a)沾一下墨水
                    SinkPostion = (75 - SinkHomePosition)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                    //(b)到刮墨位
                    SinkPostion = -(AimScraperPosition + 75)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                    if (k_RYSYSParamAutoPrintParamInTest.m_nPressAgainRePrintClean == 1)
                    {
                        //20230416修改：压墨之前，需要确保开启闪喷功能
                        if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
                        {
                            bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                            msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal2}}}";
                            Log4Net.Info(msg);

                            m_bFlashFlag = false;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                        }

                        //20220915修改：修改打印方式
//#if UseDirectPushInkMode
//                        EnableInkPush(true);
//#else
//                        motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
//                        msg = $"开启压墨： motionMap.SetDo(13, true)";
//                        Log4Net.Info(msg);
//#endif
                        if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                        {
                            EnableInkPush(true);
                            Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
                        }
                        else//间接压墨
                        {
                            motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                            msg = $"开启压墨： motionMap.SetDo(13, true)";
                            Log4Net.Info(msg);
                            Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime2 * 1000)/*3000*/);//20220915新建：暂停2.0 s
                        }

                        //Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
//#if UseDirectPushInkMode
//                        EnableInkPush(false);
//#else
//                        motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
//                        msg = $"关闭压墨： motionMap.SetDo(13, false)";
//                        Log4Net.Info(msg);
//#endif
                        if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                        {
                            EnableInkPush(false);
                        }
                        else//间接压墨
                        {
                            motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                            msg = $"关闭压墨： motionMap.SetDo(13, false)";
                            Log4Net.Info(msg);

                            msg = $"压墨等待开启：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                            Log4Net.Info(msg);
                            Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime * 1000));//压墨等待一段时间
                            msg = $"压墨等待完成：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                            Log4Net.Info(msg);
                        }

                        //20230416修改：压墨之后，需要关闭闪喷功能
                        if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
                        {
                            bool nRetVal2 = MeteorPrintEngine.SetFlash(false);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                            msg = $"开启闪喷： IDP_FlashPrtCtl(false)：ReturnCode{{{nRetVal2}}}";
                            Log4Net.Info(msg);

                            m_bFlashFlag = true;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                        }
                    }

                }

                /*
                 * 本次清洗结束，左移100mm，移至刮墨区
                 */
                //BackToStation(680/*700*//*425*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------//780MM
                BackToStation(INKCAR_CLEAN_STATION_X + INKCAR_CLEAN_SCRAPE_POS_REL, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true, 1);   // 墨车向左移动100mm
            }

            /*
             * 清洗完毕，进行最后一次刮墨
             */

            SinkPostion = (AimScraperPosition + SinkHomePosition)/*180*/;//顺时针135：20230331修改之后：避免飞溅//20230401修改:直接顺时针转180度即可//20230405修改：直接逆时针转180度即可
            motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*/, -SinkPostion);

            /*
             * 返回观察站 
             */
            if (k_RYSYSParamAutoPrintParamInTest.m_nBackToRevisionStationAfterClean == 1)//是否回观察站
            {
                BackToStation(758/*734.275*//*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1.0);//到达刮墨位置
                BackToStation(317/*321.455*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1.0);//到达刮墨位置
            }
            else    // 增加墨车移动回到默认位置, Leon, 2024/04/15
            {
                BackToStation(INKCAR_DEFAULT_Y, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false, 1.0);             // 先移动Y轴, 不等待到位
                Thread.Sleep(100);
                BackToStation(INKCAR_DEFAULT_X, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1.0);   // 再移动X轴
            }

            msg = $"结束自动清洗过程：AutoCleanThread";
            Log4Net.Info(msg);

#elif false//20220518新建：新设备使用的自动清洗逻辑//20230401之前逻辑                                             
            //（1）撒粉轴找回零位：20220527新建：
            double SinkHomePosition = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;//刮墨轴的HOME位置

            //motionMap.SetDo(7, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵//20220915注释：无效掉
            
            //20230331修改为0.5圈/s,避免飞溅
            bool ReturnCode = motionMap.SetBackSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*//*1*//*0.5*/, 2, -SinkHomePosition);//旋转速度：0.5 圈/s//下//20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
            if (ReturnCode == true)
            {
                msg = $"刮墨轴回零成功：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                Log4Net.Info(msg);
                /*MessageBox.Show("回零成功");*/
            }//校准成功
            else 
            {
                msg = $"刮墨轴回零失败：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                Log4Net.Info(msg);

                MessageBox.Show("回零失败"); 
            }

        //(2) 刮片竖直位置：-------------------------------------------------------------------------->          
        int subdivided = 12800; int SinkPostion = 0;

            //（3）联动清洗逻辑
            double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/ double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
            double CleanNozzleSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCleanCarSpeed/*m_szCleanSpeed*/;//20230331修改
            int CleanTimes = k_RYSYSParamAutoPrintParamInTest.m_nCleanTimes/*m_nCleanTime*//*3*/;//默认清洗次数为3次//20230331修改
            for (int i = 0; i < CleanTimes/*3*/; i++)//i为清洗总次数；PASS宽度为喷头宽度//默认清洗次数为3次
            {
                if (i == 0)
                {
                    BackToStation(96 + 25/*25*/, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅 面^^^^^^^^^^^^^^^^^//96MM
                    BackToStation(710/*425*/, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅<---------------//780MM

                    //SinkPostion = 135;//逆135
                    //motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                }
                //SinkPostion = 45;//逆45
                SinkPostion = 180;//逆45//合并，直接逆转180度即可
                motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                
                //20220915修改：修改打印方式
#if UseDirectPushInkMode
                EnableInkPush(true);
#else
                motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                msg = $"开启压墨： motionMap.SetDo(13, true)";
                Log4Net.Info(msg);
#endif
                Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
#if UseDirectPushInkMode
                EnableInkPush(false);
#else
                motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                msg = $"关闭压墨： motionMap.SetDo(13, true)";
                Log4Net.Info(msg);
#endif

                //20220920新增：压墨之后，需要开启闪喷功能
                if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1) 
                {
                    bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                    msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal2}}}";
                    Log4Net.Info(msg);
                } 

                m_bFlashFlag = false;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭

                BackToStation(780/*25*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true);//停靠在右侧，向左侧运动打印幅面--------------->//710MM

                SinkPostion = -45;//顺45
                motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                BackToStation(710/*425*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true);//停靠在右侧，向左侧运动打印幅面<---------------//780MM
            }

            //SinkPostion = 180+45;//逆225：20230331之前方法
            SinkPostion = -135/*180 + 45*/;//顺时针135：20230331修改之后：避免飞溅
            //SinkPostion = -180/*-135*//*180 + 45*/;//顺时针135：20230331修改之后：避免飞溅：进一步修改

            //motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
            motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*/, -SinkPostion);
            if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
            {
                bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷//20230327批注：关闭闪喷
                msg = $"关闭闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal}}}";
            }

            msg = $"结束自动清洗过程：AutoCleanThread";
            Log4Net.Info(msg);

#elif false//20220518批注：该功能注释掉，就设备好用的固化清洗逻辑
            //(1)执行清洗及固化动作，判断是否需要执行清洗动作；
            int DirFlag = 0;
            double CurrentPos = GetCurrentPos(1);//初始编码器位置：
            
            if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
            else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
            else { DirFlag = 3; }
            if (DirFlag == 1)//在清洗端近端
            {
                if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes)==0)
                    &&(k_nCurrentLayer!=0))//判断是否清洗
                {
                    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑//20210623临时注释

                    ////添加新逻辑：20210121新增
                    ////添加开启闪喷逻辑：预防刮墨清洗之后导致的喷头局部出墨异常
                    ////输入参数为：闪喷时间
                    //bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷
                    //Thread.Sleep(3000);//开启闪喷3秒钟
                    //nRetVal = royal.royal.IDP_FlashPrtCtl(false);//打开闪喷
                }

                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1,out ReturnVelocity2,out DoubleCureEnabled);//第1次固化：
                BackToStation(/*1185*/k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOff+1,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1,false);//如果此刻停靠在右侧，则移动到左侧+1MM缓冲//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);

                StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                BackToStation(45,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed)*/(float)ReturnVelocity2,false);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 2)//在清洗端远端
            {
                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第1次固化：
                BackToStation(/*45*/k_RYSYSParamAutoPrintParamInTest.m_dRightCureOn-1,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1,false);//如果此刻停靠在右侧，则移动到左侧-1MM缓冲//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);

                if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes) == 0)
                    &&(k_nCurrentLayer!=0))//判断是否清洗
                {
                    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑 //20210623临时注释
                    ////添加新逻辑：20210121新增
                    ////添加开启闪喷逻辑：预防刮墨清洗之后导致的喷头局部出墨异常
                    ////输入参数为：闪喷时间
                    //bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷
                    //Thread.Sleep(3000);//开启闪喷3秒钟
                    //nRetVal = royal.royal.IDP_FlashPrtCtl(false);//打开闪喷
                }

                StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                BackToStation(1185,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity2,false);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 3)
            { MessageBox.Show("墨车不在正常停靠区间"); }
            else
            { }
#else//测试时逻辑
            int DirFlag = 1;

            if (DirFlag == 1)//在清洗端近端
            {
                //if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes)==0)
                //    &&(k_nCurrentLayer!=0))//判断是否清洗
                //{
                //    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑
                //}

                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2,out DoubleCureEnabled);//第1次固化：
                ////BackToStation(/*1185*/k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOff+1,
                //    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1);//如果此刻停靠在右侧，则移动到左侧+1MM缓冲//执行固化逻辑
                //StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                //    out ReturnVelocity1,out ReturnVelocity2, out DoubleCureEnabled);

                //StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                //    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                ////BackToStation(45,
                //    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed)*/(float)ReturnVelocity2);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                //StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                //    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 2)//在清洗端远端
            {
                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第1次固化：
                //BackToStation(/*45*/k_RYSYSParamAutoPrintParamInTest.m_dRightCureOn-1,
                    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1);//如果此刻停靠在右侧，则移动到左侧-1MM缓冲//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1,out ReturnVelocity2, out DoubleCureEnabled);

                //if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes) == 0)
                //    &&(k_nCurrentLayer!=0))//判断是否清洗
                //{
                //    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑 
                //}

                StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                //BackToStation(1185,
                    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity2);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 3)
            { MessageBox.Show("墨车不在正常停靠区间"); }
            else
            { }
#endif
        }


        /// <summary>
        /// 自动清洗函数，由自动打印函数调用，在打印过程中按序调用自动清洗功能
        /// !!!=========== 这个是一个函数 ===========!!! 
        /// </summary>
        /// <param name="m_BackCleanMovSpeed"></param>
        /// DONE::修改墨车回清洗站移动方式：先X轴，当X轴在安全区域后，同时移动Y轴
        /// DONE::修改清洗站坐标

        public void AutoCleanThread2(float m_BackCleanMovSpeed)//20220520修改及注释：线程内容：自动清洗动作
        {
            //开发需求：（1）清洗过程中，可以指定刮板来回挂的次数；（2）也可以指定压墨的时间，不能限制5S-10S;(3)压墨的时间要延长，清洗的
            string msg = $"进入自动清洗过程：AutoCleanThread";
            Log4Net.Info(msg);

#if true//20220518新建：新设备使用的自动清洗逻辑//20230401之后逻辑
            //（1）撒粉轴找回零位：20220527新建：
            double SinkHomePosition = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;//刮墨轴的HOME位置
            double AimScraperPosition = k_RYSYSParamAutoPrintParamInTest.m_dSraperAngleOffHome+15;//刮墨片限位片位置有15度的偏差，进行补偿

            //motionMap.SetDo(7, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵//20220915注释：无效掉

            //20230331修改为0.5圈/s,避免飞溅
            bool ReturnCode = motionMap.SetBackSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*//*1*//*0.5*/, 2, -SinkHomePosition);//旋转速度：0.5 圈/s//下//20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
            if (ReturnCode == true)
            {
                msg = $"刮墨轴回零成功：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                Log4Net.Info(msg);
                /*MessageBox.Show("回零成功");*/
            }//校准成功
            else
            {
                msg = $"刮墨轴回零失败：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                Log4Net.Info(msg);

                MessageBox.Show("回零失败");
            }

            //(2) 刮片竖直位置：-------------------------------------------------------------------------->          
            int subdivided = 12800; double/*int*/ SinkPostion = 0;

            //（3）联动清洗逻辑
            double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/
            double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
            float ReturnVelocity2 = m_BackCleanMovSpeed;

            double CleanNozzleSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCleanCarSpeed/*m_szCleanSpeed*/;//20230331修改
            int CleanTimes = k_RYSYSParamAutoPrintParamInTest.m_nCleanTimes/*m_nCleanTime*//*3*/;//默认清洗次数为3次//20230331修改
            for (int i = 0; i < CleanTimes/*3*/; i++)//i为清洗总次数；PASS宽度为喷头宽度//默认清洗次数为3次
            {
                if (i == 0)
                {
                    /*
                     * 移动墨车至压墨区
                     * 
                     * DONE::先移动X轴，不等待到位
                     */
                    //BackToStation(116/*96 + 25*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//停靠在里侧，向外侧步进喷头幅 面^^^^^^^^^^^^^^^^^//96MM
                    //BackToStation(780/*710*//*425*/, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅<---------------//780MM

                    //SinkPostion = 135;//逆135
                    //motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                    /*
                     * 2024/04/15 新增改动，Leon
                     */
                    double pos_x;

                    // 先移动X轴，不等待移动到位
                    BackToStation(INKCAR_CLEAN_STATION_X, (float)ReturnVelocity2, false, false, 1.0);

                    // 判断X坐标是否已经超出SOA的最大X坐标位
                    do
                    {
                        Thread.Sleep(100);

                        pos_x = GetCurrentPos(1);
                    } while (pos_x < INKCAR_SOA_MAX_X);

                    // X轴已经安全，移动Y轴
                    //BackToStation(116/*96 + 25*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//停靠在里侧，向外侧步进喷头幅 面^^^^^^^^^^^^^^^^^//96MM
                    BackToStation(INKCAR_CLEAN_STATION_Y, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);    // Leon, 2024/04/15
                }
                else//第2次刮墨也需要回零
                {
                    SinkPostion = (AimScraperPosition + SinkHomePosition)/*180*//*-180*//*-135*//*180 + 45*/;//顺时针135：20230331修改之后：避免飞溅//20230401修改:直接顺时针转180度即可//20230405修改：直接逆时针转180度即可//20230411修改：避免回零时碰撞喷头压墨收集盒                                                             
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*/, -SinkPostion);

                    //20230331修改为0.5圈/s,避免飞溅
                    ReturnCode = motionMap.SetBackSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*//*1*//*0.5*/, 2, -SinkHomePosition);//旋转速度：0.5 圈/s//下//20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
                    if (ReturnCode == true)
                    {
                        msg = $"刮墨轴回零成功：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                        Log4Net.Info(msg);
                        /*MessageBox.Show("回零成功");*/
                    }//校准成功
                    else
                    {
                        msg = $"刮墨轴回零失败：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkHomePosition)";
                        Log4Net.Info(msg);

                        MessageBox.Show("回零失败");
                    }

                    /*
                     * 墨车重回清洁站的压墨区
                     */
                    BackToStation(INKCAR_CLEAN_STATION_X, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);   // Leon, 2024/04/15
                }

               
                //BackToStation(780/*710*//*425*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);//停靠在右侧，向左侧运动打印幅<---------------//780MM

                ////SinkPostion = 45;//逆45
                //SinkPostion = -180;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                //motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷//20230327批注：关闭闪喷
                msg = $"关闭闪喷： IDP_FlashPrtCtl(false)：ReturnCode{{{nRetVal}}}";

                if (i == 0)//第1次
                {
                    //(a)沾一下墨水
                    SinkPostion = (75 - SinkHomePosition)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                    //(b)到刮墨位
                    SinkPostion = -(AimScraperPosition + 75)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                    //20230416修改：压墨之前，需要确保开启闪喷功能
                    if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
                    {
                        bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                        msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal2}}}";
                        Log4Net.Info(msg);

                        m_bFlashFlag = false;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                    }

                    //20220915修改：修改打印方式
//#if UseDirectPushInkMode
//                    EnableInkPush(true);
//#else
//                    motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
//                    msg = $"开启压墨： motionMap.SetDo(13, true)";
//                    Log4Net.Info(msg);
//#endif
                    if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                    {
                        EnableInkPush(true);
                        Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
                    }
                    else//间接压墨
                    {
                        motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                        msg = $"开启压墨： motionMap.SetDo(13, true)";
                        Log4Net.Info(msg);
                        Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime2 * 1000)/*3000*/);//20220915新建：暂停2.0 s
                    }

                    //Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
//#if UseDirectPushInkMode
//                    EnableInkPush(false);
//#else
//                    motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
//                    msg = $"关闭压墨： motionMap.SetDo(13, false)";
//                    Log4Net.Info(msg);
//#endif
                    if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                    {
                        EnableInkPush(false);
                    }
                    else//间接压墨
                    {
                        motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                        msg = $"关闭压墨： motionMap.SetDo(13, false)";
                        Log4Net.Info(msg);

                        msg = $"压墨等待开启：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                        Log4Net.Info(msg);
                        Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime * 1000));//压墨等待一段时间
                        msg = $"压墨等待完成：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                        Log4Net.Info(msg);

                    }

                    //20230416修改：压墨之后，需要关闭闪喷功能
                    if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
                    {
                        bool nRetVal2 = MeteorPrintEngine.SetFlash(false);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                        msg = $"开启闪喷： IDP_FlashPrtCtl(false)：ReturnCode{{{nRetVal2}}}";
                        Log4Net.Info(msg);

                        m_bFlashFlag = true;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                    }

                }
                else
                {
                    //(a)沾一下墨水
                    SinkPostion = (75 - SinkHomePosition)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                    //(b)到刮墨位
                    SinkPostion = -(AimScraperPosition + 75)/*180*/;//逆45//合并，直接逆转180度即可//20230401修改:直接顺时针转180度即可
                    motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                    if (k_RYSYSParamAutoPrintParamInTest.m_nPressAgainRePrintClean == 1)
                    {
                        //20230416修改：压墨之前，需要确保开启闪喷功能
                        if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
                        {
                            bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                            msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal2}}}";
                            Log4Net.Info(msg);

                            m_bFlashFlag = false;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                        }

                        //20220915修改：修改打印方式
//#if UseDirectPushInkMode
//                        EnableInkPush(true);
//#else
//                        motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
//                        msg = $"开启压墨： motionMap.SetDo(13, true)";
//                        Log4Net.Info(msg);
//#endif
                        if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                        {
                            EnableInkPush(true);
                            Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
                        }
                        else//间接压墨
                        {
                            motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                            msg = $"开启压墨： motionMap.SetDo(13, true)";
                            Log4Net.Info(msg);
                            Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime2 * 1000)/*3000*/);//20220915新建：暂停2.0 s
                        }
                        //Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
//#if UseDirectPushInkMode
//                        EnableInkPush(false);
//#else
//                        motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
//                        msg = $"关闭压墨： motionMap.SetDo(13, false)";
//                        Log4Net.Info(msg);
//#endif
                        if (k_RYSYSParamAutoPrintParamInTest.m_nUseDirectPushInkModeEnabled == 1)//直接压墨
                        {
                            EnableInkPush(false);
                        }
                        else//间接压墨
                        {
                            motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                            msg = $"关闭压墨： motionMap.SetDo(13, false)";
                            Log4Net.Info(msg);

                            msg = $"压墨等待开启：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                            Log4Net.Info(msg);
                            Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime * 1000));//压墨等待一段时间
                            msg = $"压墨等待完成：{{{k_RYSYSParamAutoPrintParamInTest.m_dPressInkWaitTime}S}}";
                            Log4Net.Info(msg);
                        }

                        //20230416修改：压墨之后，需要关闭闪喷功能
                        if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
                        {
                            bool nRetVal2 = MeteorPrintEngine.SetFlash(false);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                            msg = $"开启闪喷： IDP_FlashPrtCtl(false)：ReturnCode{{{nRetVal2}}}";
                            Log4Net.Info(msg);

                            m_bFlashFlag = true;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭
                        }

                    }

                }


                //////BackToStation(780/*25*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true);//停靠在右侧，向左侧运动打印幅面--------------->//710MM
                ////SinkPostion = -45;//顺45
                ////motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                /*
                 * 本次清洗结束，墨车向左移动100mm，移至刮墨区
                 */
                //BackToStation(680/*700*//*425*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------//780MM
                BackToStation(INKCAR_CLEAN_STATION_X + INKCAR_CLEAN_SCRAPE_POS_REL, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true, 1);   // 墨车向左移动100mm
            }

            /*
             * 结束清洗前进行最后一次刮墨
             */
            //SinkPostion = 180+45;//逆225：20230331之前方法
            SinkPostion = (AimScraperPosition + SinkHomePosition)/*180*//*-180*//*-135*//*180 + 45*/;//顺时针135：20230331修改之后：避免飞溅//20230401修改:直接顺时针转180度即可//20230405修改：直接逆时针转180度即可
            //SinkPostion = -180/*-135*//*180 + 45*/;//顺时针135：20230331修改之后：避免飞溅：进一步修改

            //motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
            motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*/, -SinkPostion);
            ////if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
            ////{
            ////    bool nRetVal = royal.royal.IDP_FlashPrtCtl(false);//关闭闪喷//20230327批注：关闭闪喷
            ////    msg = $"关闭闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal}}}";
            ////}
                ///

            /*
             * 自动清洗结束，增加墨车移动回到默认位置, Leon, 2024/04/15
             */
            BackToStation(INKCAR_DEFAULT_Y, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false, 1.0);             // 先移动Y轴, 不等待到位
            Thread.Sleep(100);
            BackToStation(INKCAR_DEFAULT_X, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1.0);   // 再移动X轴

            /*
             * 日志记录
             */
            msg = $"结束自动清洗过程：AutoCleanThread";
            Log4Net.Info(msg);

#elif false//20220518新建：新设备使用的自动清洗逻辑//20230401之前逻辑
            //（1）撒粉轴找回零位：20220527新建：
            double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;//刮墨轴的HOME位置

            //motionMap.SetDo(7, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵//20220915注释：无效掉
            
            //20230331修改为0.5圈/s,避免飞溅
            bool ReturnCode = motionMap.SetBackSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*//*1*//*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s//下//20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
            if (ReturnCode == true)
            {
                msg = $"刮墨轴回零成功：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkPosition)";
                Log4Net.Info(msg);
                /*MessageBox.Show("回零成功");*/
            }//校准成功
            else 
            {
                msg = $"刮墨轴回零失败：motionMap.SetBackSpreaderAxis(4, 1, 2, -SinkPosition)";
                Log4Net.Info(msg);

                MessageBox.Show("回零失败"); 
            }

        //(2) 刮片竖直位置：-------------------------------------------------------------------------->          
        int subdivided = 12800; int SinkPostion = 0;

            //（3）联动清洗逻辑
            double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/ double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
            double CleanNozzleSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCleanCarSpeed/*m_szCleanSpeed*/;//20230331修改
            int CleanTimes = k_RYSYSParamAutoPrintParamInTest.m_nCleanTimes/*m_nCleanTime*//*3*/;//默认清洗次数为3次//20230331修改
            for (int i = 0; i < CleanTimes/*3*/; i++)//i为清洗总次数；PASS宽度为喷头宽度//默认清洗次数为3次
            {
                if (i == 0)
                {
                    BackToStation(96 + 25/*25*/, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅 面^^^^^^^^^^^^^^^^^//96MM
                    BackToStation(710/*425*/, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅<---------------//780MM

                    //SinkPostion = 135;//逆135
                    //motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                }
                //SinkPostion = 45;//逆45
                SinkPostion = 180;//逆45//合并，直接逆转180度即可
                motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);
                
                //20220915修改：修改打印方式
#if UseDirectPushInkMode
                EnableInkPush(true);
#else
                motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                msg = $"开启压墨： motionMap.SetDo(13, true)";
                Log4Net.Info(msg);
#endif
                Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime * 1000)/*3000*/);//20220915新建：暂停2.0 s
#if UseDirectPushInkMode
                EnableInkPush(false);
#else
                motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                msg = $"关闭压墨： motionMap.SetDo(13, true)";
                Log4Net.Info(msg);
#endif

                //20220920新增：压墨之后，需要开启闪喷功能
                if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1) 
                {
                    bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                    msg = $"开启闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal2}}}";
                    Log4Net.Info(msg);
                } 

                m_bFlashFlag = false;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭

                BackToStation(780/*25*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true);//停靠在右侧，向左侧运动打印幅面--------------->//710MM

                SinkPostion = -45;//顺45
                motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*2*//*0.5*/, -SinkPostion);

                BackToStation(710/*425*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true);//停靠在右侧，向左侧运动打印幅面<---------------//780MM
            }

            //SinkPostion = 180+45;//逆225：20230331之前方法
            SinkPostion = -135/*180 + 45*/;//顺时针135：20230331修改之后：避免飞溅
            //SinkPostion = -180/*-135*//*180 + 45*/;//顺时针135：20230331修改之后：避免飞溅：进一步修改

            //motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
            motionMap.TrapMoveSpreaderAxis(4, k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed/*0.5*/, -SinkPostion);
            if (k_RYSYSParamAutoPrintParamInTest.m_nStartSpark == 1)
            {
                bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷//20230327批注：关闭闪喷
                msg = $"关闭闪喷： IDP_FlashPrtCtl(true)：ReturnCode{{{nRetVal}}}";
            }

            msg = $"结束自动清洗过程：AutoCleanThread";
            Log4Net.Info(msg);

#elif false//20220518批注：该功能注释掉，就设备好用的固化清洗逻辑
            //(1)执行清洗及固化动作，判断是否需要执行清洗动作；
            int DirFlag = 0;
            double CurrentPos = GetCurrentPos(1);//初始编码器位置：
            
            if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
            else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
            else { DirFlag = 3; }
            if (DirFlag == 1)//在清洗端近端
            {
                if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes)==0)
                    &&(k_nCurrentLayer!=0))//判断是否清洗
                {
                    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑//20210623临时注释

                    ////添加新逻辑：20210121新增
                    ////添加开启闪喷逻辑：预防刮墨清洗之后导致的喷头局部出墨异常
                    ////输入参数为：闪喷时间
                    //bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷
                    //Thread.Sleep(3000);//开启闪喷3秒钟
                    //nRetVal = royal.royal.IDP_FlashPrtCtl(false);//打开闪喷
                }

                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1,out ReturnVelocity2,out DoubleCureEnabled);//第1次固化：
                BackToStation(/*1185*/k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOff+1,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1,false);//如果此刻停靠在右侧，则移动到左侧+1MM缓冲//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);

                StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                BackToStation(45,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed)*/(float)ReturnVelocity2,false);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 2)//在清洗端远端
            {
                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第1次固化：
                BackToStation(/*45*/k_RYSYSParamAutoPrintParamInTest.m_dRightCureOn-1,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1,false);//如果此刻停靠在右侧，则移动到左侧-1MM缓冲//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);

                if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes) == 0)
                    &&(k_nCurrentLayer!=0))//判断是否清洗
                {
                    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑 //20210623临时注释
                    ////添加新逻辑：20210121新增
                    ////添加开启闪喷逻辑：预防刮墨清洗之后导致的喷头局部出墨异常
                    ////输入参数为：闪喷时间
                    //bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷
                    //Thread.Sleep(3000);//开启闪喷3秒钟
                    //nRetVal = royal.royal.IDP_FlashPrtCtl(false);//打开闪喷
                }

                StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                BackToStation(1185,
                    /*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity2,false);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 3)
            { MessageBox.Show("墨车不在正常停靠区间"); }
            else
            { }
#else//测试时逻辑
            int DirFlag = 1;

            if (DirFlag == 1)//在清洗端近端
            {
                //if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes)==0)
                //    &&(k_nCurrentLayer!=0))//判断是否清洗
                //{
                //    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑
                //}

                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2,out DoubleCureEnabled);//第1次固化：
                ////BackToStation(/*1185*/k_RYSYSParamAutoPrintParamInTest.m_dLeftCureOff+1,
                //    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1);//如果此刻停靠在右侧，则移动到左侧+1MM缓冲//执行固化逻辑
                //StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                //    out ReturnVelocity1,out ReturnVelocity2, out DoubleCureEnabled);

                //StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                //    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                ////BackToStation(45,
                //    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed)*/(float)ReturnVelocity2);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                //StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                //    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 2)//在清洗端远端
            {
                //20210621新增：调节信号周期、有效时间、移动速度1，移动速度2，是否开2灯
                double ReturnVelocity1 = 50; double ReturnVelocity2 = 150;/*固化灯速度：默认为100mm/s*/ bool DoubleCureEnabled = false;

                StartCloseCureLight(true, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity,k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第1次固化：
                //BackToStation(/*45*/k_RYSYSParamAutoPrintParamInTest.m_dRightCureOn-1,
                    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity1);//如果此刻停靠在右侧，则移动到左侧-1MM缓冲//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1,out ReturnVelocity2, out DoubleCureEnabled);

                //if (((k_nCurrentLayer) % (k_RYSYSParamAutoPrintParamInTest.m_nCleanFrequency * k_RYSYSParamAutoPrintParamInTest.m_nRePrintTimes) == 0)
                //    &&(k_nCurrentLayer!=0))//判断是否清洗
                //{
                //    ScraperMotionLogic();//20200918新增：单纯的刮墨逻辑 
                //}

                StartCloseCureLight(DoubleCureEnabled/*true*/, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);//第2次固化：
                //BackToStation(1185,
                    ///*25*//*(float)k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed*/(float)ReturnVelocity2);//如果此刻停靠在右侧，则移动到左侧//执行固化逻辑
                StartCloseCureLight(false, k_RYSYSParamAutoPrintParamInTest.m_nCureEnergyDensity, k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed,
                    out ReturnVelocity1, out ReturnVelocity2, out DoubleCureEnabled);
            }
            else if (DirFlag == 3)
            { MessageBox.Show("墨车不在正常停靠区间"); }
            else
            { }
#endif
        }



        private bool UVIRLightFlag = true;//20220523批注：默认true,对应UV灯工作


        /// <summary>
        /// 自动固化线程
        /// </summary>
        public void AutoCureThread()//20220520新建批注：线程内容：新设备使用的自动固化动作及其逻辑
        {
            if (k_RYSYSParamAutoPrintParamInTest.m_nRecoaterMode == 1)//20240103新增：手动测试铺粉固化逻辑，先铺粉再喷墨模式
            {
                NewAutoSupplyPowderThread2CureFirst(ref _powderMonitorStub, 0, 10, k_fBackCleanMovSpeed);//20240103新增：与自动打印 Command2 同源；阶段C直调，避免命中 Obsolete 包装
            }
            else//20240103批注：手动测试铺粉固化逻辑，同步铺粉固化模式
            {
                string msg = $"手动开启固化运动逻辑：AutoCureThread";
                Log4Net.Info(msg);

                if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 0)//判断使用UV还是IR作为固化光源
                { UVIRLightFlag = true; }
                else if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 1)
                { UVIRLightFlag = false; }
                else { }

                //(0000)//20220602修改：Z向进给量:下降一个固定高度
                double vel = 1;//Z向运动速度为1mm/s
                double TrapSpace = -/*k_RYSYSParamAutoPrintParamInTest.m_nLayerThick*/(double)200 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20260416修改：成型缸固高轴8；轴1为墨车X勿用
                Thread.Sleep(800);//等待800 ms


                //洒粉车覆盖打印区域-过程中根据需要开UV灯及红外灯（移动-开UV灯/红外灯-光UV灯/红外灯）//20220520批注
                //(A: 移动)
                RollerParam = 0/*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed*/;//201029批注：更新辊子速度
                double AimPos = 695; double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                //A-2: 开启去程固化
                bool m_startFlag = false;
                double PosValue = 0;
                double LightSourceOffset = 70;//默认为UV灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                if (UVIRLightFlag == true) { LightSourceOffset = 70; }
                else { LightSourceOffset = 110; }

                do//检查X轴是否位于指定区域
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置
                    if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/
                        }
                        else
                        {
                            OpenIRLamp(true);/*打开红外灯*/
                        }
                        m_startFlag = true;
                    }
                    else { }
                }
                while (PosValue <= (620 - LightSourceOffset/*70*/));//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM

                if (UVIRLightFlag == true) { OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/ }
                else { OpenIRLamp(false);/*关闭红外灯*/  }
                m_startFlag = false;

                int AxiStatus = 0; double prfPos = 0;
                while ((Math.Abs(PosValue) < Math.Abs(695)) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                {
                    PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7 //motionMap.GetPrfPos(7, out prfPos);
                    motionMap.GetAxisStatus(7, out AxiStatus); //20260416修改：当前铺粉车运动轴改为轴7
                }

                //B: 洒粉车回到落粉站位置（回站）：20220512批注
                PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置
                RollerParam = 0/*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed*/;//201029批注：更新辊子速度                                                                              /*double*/
                AimPos = 1; MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed;//更新值到本地变量
                BackToStation2(AimPos, MovSpeed, false/*true*/);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停//20220521设置为不等停
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

                //B-2:开启回程固化
                do//检查X轴是否位于指定区域
                {
                    PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置
                    if (PosValue <= (620 - LightSourceOffset/*70*/) && m_startFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/
                        }
                        else
                        {
                            OpenIRLamp(true);/*打开红外灯*/
                        }
                        m_startFlag = true;
                    }
                    else { }
                }
                while (PosValue >= (225 - LightSourceOffset/*70*/));//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
                if (UVIRLightFlag == true) { OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/ }
                else { OpenIRLamp(false);/*关闭红外灯*/  }

                //B-3:确保回到落粉站
                AxiStatus = 0; prfPos = 0;
                while ((Math.Abs(PosValue) > Math.Abs(AimPos)) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                {
                    PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(7, out prfPos);
                    motionMap.GetAxisStatus(7, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
                }

                //(9999)//20220602修改：Z向进给量:上升一个固定高度
                vel = 1;//Z向运动速度为1mm/s
                TrapSpace = /*-*//*k_RYSYSParamAutoPrintParamInTest.m_nLayerThick*/(double)200 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20260416修改：成型缸固高轴8；轴1为墨车X勿用
                Thread.Sleep(800);//等待800 ms

                msg = $"固化运动逻辑正常结束：AutoCureThread";
                Log4Net.Info(msg);
            }
        }

        //20220512新建：新的上送粉铺粉逻辑
        //20220512新建：新的上送粉铺粉逻辑
        //20220512新建：新的上送粉铺粉逻辑
        /// <summary>
        /// 自动铺粉线程，没有引用
        /// </summary>
        public void NewAutoSupplyPowderThread3()//20220512新建：新的上送粉铺粉逻辑//20230509新增：测试电磁体即时开启条件下，铺粉测试分析
        {
            //(1)Z向进给：20210125新增//20220525修改：Z向进给量
            double vel = /*1*/2;//Z向运动速度为1mm/s
            double TrapSpace = -(double)k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
            GoogolDigtalOut(14, true);
            GoogolDigtalOut(15, true);
#endif
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                                                                                                                                  //Thread.Sleep(800/*800*/);//等待800 ms
                                                                                                                                  //#if OpenMagnetWhenUse
                                                                                                                                  //                GoogolDigtalOut(15,false);
                                                                                                                                  //#endif

            string msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);

            //20230313新增：单独下降层厚，精度不够：继续下降1500um
            //20230313新增：单独下降层厚，精度不够：继续下降1500um
            //20230313新增：单独下降层厚，精度不够：继续下降1500um
            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = /*1*/2;//Z向运动速度为1mm/s
            TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
                                                     //#if OpenMagnetWhenUse
                                                     //                GoogolDigtalOut(15,true);
                                                     //#endif
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//不同于默认，为不等停
                                                                                                                                  //#if OpenMagnetWhenUse
                                                                                                                                  //                GoogolDigtalOut(15,false);
                                                                                                                                  //#endif

            msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);
            //Thread.Sleep(1000);//等待800 ms

            //20230313新增：单独下降层厚，精度不够：回程1500um
            //20230313新增：单独下降层厚，精度不够：回程1500um
            //20230313新增：单独下降层厚，精度不够：回程1500um
            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = /*1*/2;//Z向运动速度为1mm/s
            TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
                                                    //#if OpenMagnetWhenUse
                                                    //                GoogolDigtalOut(15,true);
                                                    //#endif
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
#if OpenMagnetWhenUse
            GoogolDigtalOut(15, false);
            GoogolDigtalOut(14, false);
#endif
            msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);

            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = /*1*/2;//Z向运动速度为1mm/s
            TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
#if OpenMagnetWhenUse
            GoogolDigtalOut(14, true);
            GoogolDigtalOut(15, true);
#endif
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
#if OpenMagnetWhenUse
            GoogolDigtalOut(15, false);
            GoogolDigtalOut(14, false);
#endif
            msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);

            //Thread.Sleep(1000);//等待800 ms

            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = /*1*/2;//Z向运动速度为1mm/s
            TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
            GoogolDigtalOut(14, true);
            GoogolDigtalOut(15, true);
#endif
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
#if OpenMagnetWhenUse
            GoogolDigtalOut(15, false);
            GoogolDigtalOut(14, false);
#endif
            msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);

        }



        /// <summary>
        /// 供粉线程，由手动调节对话框中的[手动铺粉]按钮触发工作
        /// </summary>
        /// DONE::修改铺粉行程，落粉开始位置，落粉结束位置等常数
        [Obsolete("与自动打印不一致：UI 与主流程已统一为 NewAutoSupplyPowderThread2*（EquipmentMotionLogic3 Command2 / UiThreadEntry_NewAutoSupplyPowderThread2AlignedWithAutoPrint）。工艺调试请只改 NewAutoSupplyPowderThread2 系列。")]
        public void NewAutoSupplyPowderThread()//20220512新建：新的上送粉铺粉逻辑
        {
#if false //20230308调试Z轴运动精度，临时使用
            //(1)
            string msg = $"20230308修改+++++：开启手动铺粉逻辑：NewAutoSupplyPowderThread";
            Log4Net.Info(msg);
            //(1)Z向进给：20210125新增//20220525修改：Z向进给量
            double vel = 1;//Z向运动速度为1mm/s
            double TrapSpace = -(double)k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
            //Thread.Sleep(1000);//等待800 ms
            msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);

            //20230313新增：单独下降层厚，精度不够：继续下降1500um
            //20230313新增：单独下降层厚，精度不够：继续下降1500um
            //20230313新增：单独下降层厚，精度不够：继续下降1500um
            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = 1;//Z向运动速度为1mm/s
            TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//不同于默认，为不等停

            msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);

            //Thread.Sleep(2000);//等待800 ms

            //20230313新增：单独下降层厚，精度不够：回程1500um
            //20230313新增：单独下降层厚，精度不够：回程1500um
            //20230313新增：单独下降层厚，精度不够：回程1500um
            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = 1;//Z向运动速度为1mm/s
            TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停

            msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);
            //Thread.Sleep(800);//等待800 ms



            //(2)
            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = 1;//Z向运动速度为1mm/s
            TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
            msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);
            //Thread.Sleep(1000);//等待800 ms

            //(3)
            //20220915新增：铺粉完成 下降一段距离，避免回程压碎
            vel = 1;//Z向运动速度为1mm/s
            TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停

            msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
            Log4Net.Info(msg);

#endif

            string msg = $"开启手动铺粉逻辑：NewAutoSupplyPowderThread";
            Log4Net.Info(msg);

            int nIOState = motionMap.MointoringAxis2(7);//铺粉车固高轴7限位状态         
            if (/*false*/(0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                msg = $"中止手动铺粉逻辑，异常停靠区间退出：NewAutoSupplyPowderThread2：ReturnCode{{{nIOState}}}";
                Log4Net.Info(msg);

                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {
#if false
                            //(1)判断是否可以执行自动进给铺粉动作
                            int DirFlag = 0;
                            double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                            if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                            else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                            else { DirFlag = 3; }
#endif
#region 监控发送指令//20230113新建且批注：
                SendMessageToCamera sendMessageToCamera = new SendMessageToCamera(false);//20200202修改
                                                                                         //sendMessageToCamera.LoadJsonFile();
                                                                                         //sendMessageToCamera.SendMessageFromSharedMemory(tempStartMode,10,13);//20230113新建且批注：监控发送指令
                                                                                         //sendMessageToCamera.Dispose();//20230113新建且批注：监控发送指令
#endregion

                if (true/*(DirFlag == 1) || (DirFlag == 2)*/)//墨车在非安全区域++粉车在正常工作区间内==粉末在正负限位区间内
                {
                    if (true/*0 == k_RYSYSParamAutoPrintParamInTest.m_nRecoaterStrategy*/)//20220512新建批注：新设备只需要使用直接铺粉逻辑即可//(2)直接铺粉方式：20210125新增
                    {
#region 监控指令：铺粉拍摄位点1
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[7])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 8);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);

                        }
#endregion

                        //20220920新建：判断是UV固化还是红外固化
                        if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 0)//判断使用UV还是IR作为固化光源
                        { UVIRLightFlag = true; }
                        else if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 1)
                        { UVIRLightFlag = false; }
                        else { }

                        //A-2: 开启去程固化
                        double LightSourceOffset = 70;//默认为UV灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                        if (UVIRLightFlag == true) { LightSourceOffset = 70; }
                        else { LightSourceOffset = 110; }



#if true//铺粉逻辑，暂时注释掉//20220524新建：成型缸逻辑，一次下降1个层厚
                        //(1)Z向进给：20210125新增//20220525修改：Z向进给量
                        double vel = /*1*/2;//Z向运动速度为1mm/s
                        double TrapSpace = -(double)k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                        //Thread.Sleep(800/*800*/);//等待800 ms
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);
//#endif

                        msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                        Log4Net.Info(msg);

                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = /*1*/2;//Z向运动速度为1mm/s
                        TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);
//#endif
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//不同于默认，为不等停
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);
//#endif

                        msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                        Log4Net.Info(msg);
                        //Thread.Sleep(1000);//等待800 ms

                        //20230313新增：单独下降层厚，精度不够：回程1500um
                        //20230313新增：单独下降层厚，精度不够：回程1500um
                        //20230313新增：单独下降层厚，精度不够：回程1500um
                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = /*1*/2;//Z向运动速度为1mm/s
                        TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
                                                                //#if OpenMagnetWhenUse
                                                                //                GoogolDigtalOut(15,true);
                                                                //#endif
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                        msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                        Log4Net.Info(msg);
                        //Thread.Sleep(1000);//等待800 ms
#endif
                        /*
                         * 初始落粉，Leon，2024/04/17
                         */
                        //(1)落粉站漏斗阀门转指定圈数后停止（初始落粉）：20220512批注
                        double rotateNuM = k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyRotateNum;
                        TrapMoveUp(3, true, "2.5"/*"0.5"*/, rotateNuM.ToString()/* "2"*/, true, false);//20260416修改：初始落粉由轴8切换为轴3

                        msg = $"初始落粉改由轴3执行，转{rotateNuM}圈落粉：TrapMoveUp(3, true, 2.5, rotateNuM.ToString(), true, false)";
                        Log4Net.Info(msg);

                        Thread.Sleep(1000);//20230411新增：等待1s保证接上粉


                        /*
                         * 铺粉车开始向右移动，Leon，2024/04/17
                         */
                        //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                        /*
                         *   先移动到落粉位置
                         *   DONE::已替换常数255
                         */
                        //double AimPos = 255- k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*695*/;
                        double AimPos = POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*695*/;
                        double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量
                        msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                        Log4Net.Info(msg);
                        BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                        double PosValue = 0;
                        PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
                        msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                        Log4Net.Info(msg);


                        /*
                         *   其次移动到铺粉最大行程位置
                         */
                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        /*double*/
                        //AimPos = 695;       // 铺粉车最大行程位置，需要替换常数695
                        // DONE::已替换常数695
                        AimPos = POWDERCAR_TRAVEL_DIST;       // 铺粉车最大行程位置，
                        /*double*/
                        MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                        BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                        msg = $"开启铺粉车不等停运动至站2，速度0.5rev/s：BackToStation2";
                        Log4Net.Info(msg);


                        //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                        bool m_startFlag = false;
                        bool m_startFlag3 = false;//20230411新建:落粉轴预先运动标志位
                        bool m_startLightFlag = false;//开灯标志，确保开灯1次
                        bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次
                        //double PosValue = 0;
                        do//检查X轴是否位于指定区域
                        {
                            /*double*/
                            PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置

                            //20220920新建：打开UV或者IR灯
                            // DONE::需要替换常数255
                            //if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            if (PosValue >= (POWDERCAR_DROP_BEGIN - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            {
                                if (UVIRLightFlag == true)
                                {
                                    OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

                                    msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
                                    Log4Net.Info(msg);

                                }
                                else
                                {
                                    OpenIRLamp(true);/*打开红外灯*/

                                    msg = $"打开IR灯：OpenIRLamp(true)";
                                    Log4Net.Info(msg);
                                }
                                m_startLightFlag = true;
                            }
                            else { }
                            //20220920新建:关闭UV或者IR灯
                            // DONE::需要替换常数620
                            //if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            if (PosValue > (POWDERCAR_DROP_END - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            {
                                if (UVIRLightFlag == true)
                                {
                                    OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

                                    msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
                                    Log4Net.Info(msg);
                                }
                                else
                                {
                                    OpenIRLamp(false);/*关闭红外灯*/

                                    msg = $"关闭IR灯：OpenIRLamp(false)";
                                    Log4Net.Info(msg);
                                }
                                m_startLightFlag2 = true;
                            }

                            
                            /*
                             * 落粉操作
                             */
                            //(1)预先转30度的撒粉动作
                            // DONE::需要替换常数255
                            //if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag3 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度

                            //20251210注释：无需撒粉动作
                            if (PosValue >= (POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag3 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                            {
                                double PreAngleForPowderSupply = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply / 360;//20230411备注：单位为圈数
                                double DispenseRollerSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleRotateSpeedForPowderSupply;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数//20230411:1r/s速度
                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, Convert.ToString(PreAngleForPowderSupply), true, false/*true*/);//等停运动//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                                msg = $"在{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/}mm}}处，落粉轴运动{{{k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply}度}}，落粉轴转速{{{DispenseRollerSpeed}rev/s}}：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                                Log4Net.Info(msg);

                                m_startFlag3 = true;
                            }
                           

                            /*
                             * 开始落粉
                             */
                            //(2)开启撒粉动作
                            // DONE::需要替换常数255
                            //if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*- 90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                            if (PosValue >= (POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*- 90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                            {
                                // DONE::需要替换常数620，常数255
                                //double DispenseRollerSpeed = 0.5 / ((620 - 255)/*255*/ / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed) * 1.1;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//20230406修正：1.2未补偿系数
                                double DispenseRollerSpeed = 0.5 / ((POWDERCAR_DROP_END - POWDERCAR_DROP_BEGIN)/*255*/ / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed) * 1.1;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//20230406修正：1.2未补偿系数
                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/
                                ControlUltrasonicViaGoogolIO(true);//20251210批注：行程开始时开启超声装置
                                msg = $"开启均匀落粉及辊子铺平运动：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true);开启超声装置： ControlUltrasonicViaGoogolIO(true)";
                                Log4Net.Info(msg);

                                m_startFlag = true;
                            }
                            else { }

                            Thread.Sleep(10);       // 延时10ms

                        } while (PosValue <= POWDERCAR_DROP_END);// DONE::需要替换常数620
                        //while (PosValue <= 620) ;//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM 



                        TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位
                        ControlUltrasonicViaGoogolIO(false);//20251210批注：行程关闭时关闭超声装置
                        msg = $"落粉轴继续转动以倒掉余粉：TrapMoveUp(3, true, 2, 0.5, true, false)；关闭超声装置：ControlUltrasonicViaGoogolIO(false)";
                        Log4Net.Info(msg);


                        /*
                         * 等待铺粉车到达行程终点
                         */
                        int AxiStatus = 0; double prfPos = 0;
                        // DONE::需要替换常数695
                        //while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                        while ((Math.Abs(PosValue) < Math.Abs(POWDERCAR_TRAVEL_DIST)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                        {
                            /*double*/
                            PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(7, out prfPos);
                            motionMap.GetAxisStatus(7, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
                            Thread.Sleep(10);
                        }

                        /*
                         * 铺粉车已经到达行程终点
                         */
                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = /*1*/2;//Z向运动速度为1mm/s
                        TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                        msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                        Log4Net.Info(msg);

                        //Thread.Sleep(1000);//等待800 ms

#region 监控指令：铺粉拍摄位点3
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 10);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion

                        /*
                         * 铺粉车回程
                         */
                        //(2)洒粉车回到落粉站位置（回站）：20220512批注
                        PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        double PowderStationCorrection = k_RYSYSParamAutoPrintParamInTest.m_dPowderStationCorrection;
                        AimPos = 1 - PowderStationCorrection;
                        MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed /*125*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/;//更新值到本地变量
                                                                                                                                                       //回程速度125mm/s//20230228修改：回程固化速度可以修改
                        BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停

                        msg = $"铺粉车返回至站1：BackToStation2(AimPos, MovSpeed, true)：{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                        Log4Net.Info(msg);

                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

                        //20251206批注：更换铺粉解释，无需落粉轴
                        //(3)撒粉轴找回零位：20220527新建：
                        //double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition/*Convert.ToDouble(textBox22.Text)*/;
                        //bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 2.5/*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s
                        //if (ReturnCode == true)//校准成功
                        //{
                        //    msg = $"落粉轴回零成功：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                        //    Log4Net.Info(msg);

                        //    //MessageBox.Show("回零成功");//成功执行不需要额外的反馈
                        //}
                        //else
                        //{
                        //    msg = $"落粉轴回零失败：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                        //    Log4Net.Info(msg);

                        //    MessageBox.Show("回零失败");
                        //}

                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = /*1*/2;//Z向运动速度为1mm/s
                        TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                        msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                        Log4Net.Info(msg);

                        //Thread.Sleep(1000);//等待800 ms

                        msg = $"手动铺粉正常结束：NewAutoSupplyPowderThread";
                        Log4Net.Info(msg);

#region 监控指令：铺粉拍摄位点5
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[11])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 12);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion
                    }
                    else { }

                }
                else
                { MessageBox.Show("墨车不在正常停靠区间"); }

#region 监控发送指令//20230113新建且批注：
                sendMessageToCamera.Dispose(); //20230113新建且批注：监控发送指令
#endregion
            }
        }



        /// <summary>
        /// 自动送粉线程，固化优先（与自动打印 Command2、RecoaterMode!=0 同源实现）。
        /// </summary>
        /// <remarks>阶段C：无参入口已删除重复体，统一转调 <see cref="NewAutoSupplyPowderThread2CureFirst"/>。</remarks>
        [Obsolete("请改调 NewAutoSupplyPowderThread2CureFirst(ref SendMessageToCamera, layer, process, backCleanSpeed)；本方法仅保留兼容。")]
        public void NewAutoSupplyPowderThreadCureFirst()
        {
            NewAutoSupplyPowderThread2CureFirst(ref _powderMonitorStub, 0, 10, k_fBackCleanMovSpeed);
        }
        //        public void NewAutoSupplyPowderThreadCureFirst20240102beifen()//20220512新建：新的上送粉铺粉逻辑//20240102修改之时备份
        //        {
        //            string msg = $"开启手动铺粉逻辑：NewAutoSupplyPowderThread";
        //            Log4Net.Info(msg);

        //            int nIOState = motionMap.MointoringAxis2(7);//铺粉车固高轴7限位状态         
        //            if (/*false*/(0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
        //            {
        //                msg = $"中止手动铺粉逻辑，异常停靠区间退出：NewAutoSupplyPowderThread2：ReturnCode{{{nIOState}}}";
        //                Log4Net.Info(msg);

        //                MessageBox.Show("粉车不在正常停靠区间");
        //            }
        //            else//粉车位于正常停靠区
        //            {
        //                #region 监控发送指令//20230113新建且批注：
        //                SendMessageToCamera sendMessageToCamera = new SendMessageToCamera(false);//20200202修改
        //                #endregion

        //                if (true/*(DirFlag == 1) || (DirFlag == 2)*/)//墨车在非安全区域++粉车在正常工作区间内==粉末在正负限位区间内
        //                {
        //                    if (true/*0 == k_RYSYSParamAutoPrintParamInTest.m_nRecoaterStrategy*/)//20220512新建批注：新设备只需要使用直接铺粉逻辑即可//(2)直接铺粉方式：20210125新增
        //                    {
        //                        #region 监控指令：铺粉拍摄位点1
        //                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[7])
        //                        {
        //                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 8);//20230113新建且批注：监控发送指令

        //                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
        //                            Log4Net.Info(msg);

        //                        }
        //                        #endregion

        //                        //20220920新建：判断是UV固化还是红外固化
        //                        if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 0)//判断使用UV还是IR作为固化光源
        //                        { UVIRLightFlag = true; }
        //                        else if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 1)
        //                        { UVIRLightFlag = false; }
        //                        else { }

        //                        //A-2: 开启去程固化
        //                        double LightSourceOffset = 70;//默认为UV灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
        //                        if (UVIRLightFlag == true) { LightSourceOffset = 70; }
        //                        else { LightSourceOffset = 110; }

        //                        /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
        //                        /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
        //#if true//铺粉逻辑，暂时注释掉//20220524新建：成型缸逻辑，一次下降1个层厚
        //                        //(1)Z向进给：20210125新增//20220525修改：Z向进给量
        //                        double vel = /*1*/2;//Z向运动速度为1mm/s
        //                        double TrapSpace = -(double)k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(14, true);
        //                        GoogolDigtalOut(15, true);
        //#endif
        //                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停

        //                        //Thread.Sleep(800/*800*/);//等待800 ms
        //                        //#if OpenMagnetWhenUse
        //                        //                GoogolDigtalOut(15,false);
        //                        //#endif

        //                        msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        Log4Net.Info(msg);

        //                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
        //                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
        //                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
        //                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
        //                        vel = /*1*/2;//Z向运动速度为1mm/s
        //                        TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
        //                                                                 //#if OpenMagnetWhenUse
        //                                                                 //                GoogolDigtalOut(15,true);
        //                                                                 //#endif
        //                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//不同于默认，为不等停
        //                                                                                                                                              //#if OpenMagnetWhenUse
        //                                                                                                                                              //                GoogolDigtalOut(15,false);
        //                                                                                                                                              //#endif

        //                        msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        Log4Net.Info(msg);
        //                        //Thread.Sleep(1000);//等待800 ms

        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(15, false);
        //                        GoogolDigtalOut(14, false);
        //#endif

        //                        //#if OpenMagnetWhenUse
        //                        //                        GoogolDigtalOut(14, true);
        //                        //                        GoogolDigtalOut(15, true);
        //                        //#endif
        //                        //                        //Thread.Sleep(800/*800*/);//等待800 ms
        //                        //                        //#if OpenMagnetWhenUse
        //                        //                        //                GoogolDigtalOut(15,false);
        //                        //                        //#endif

        //                        //                        msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        //                        Log4Net.Info(msg);

        //                        //                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
        //                        //                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
        //                        //                        //20230313新增：单独下降层厚，精度不够：继续下降1500um
        //                        //                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
        //                        //                        vel = /*1*/2;//Z向运动速度为1mm/s
        //                        //                        TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
        //                        //                                                                 //#if OpenMagnetWhenUse
        //                        //                                                                 //                GoogolDigtalOut(15,true);
        //                        //                                                                 //#endif
        //                        //                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//不同于默认，为不等停
        //                        //                                                                                                                                              //#if OpenMagnetWhenUse
        //                        //                                                                                                                                              //                GoogolDigtalOut(15,false);
        //                        //                                                                                                                                              //#endif

        //                        //                        msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        //                        Log4Net.Info(msg);
        //                        //                        //Thread.Sleep(1000);//等待800 ms

        //                        //                        //20230313新增：单独下降层厚，精度不够：回程1500um
        //                        //                        //20230313新增：单独下降层厚，精度不够：回程1500um
        //                        //                        //20230313新增：单独下降层厚，精度不够：回程1500um
        //                        //                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
        //                        //                        vel = /*1*/2;//Z向运动速度为1mm/s
        //                        //                        TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
        //                        //                                                                //#if OpenMagnetWhenUse
        //                        //                                                                //                GoogolDigtalOut(15,true);
        //                        //                                                                //#endif
        //                        //                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
        //                        //#if OpenMagnetWhenUse
        //                        //                        GoogolDigtalOut(15, false);
        //                        //                        GoogolDigtalOut(14, false);
        //                        //#endif
        //                        //                        msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        //                        Log4Net.Info(msg);
        //#endif
        //                        //(1)落 粉站漏斗阀门转3圈-再停止（接粉）：20220512批注
        //                        double rotateNuM = k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyRotateNum;
        //                        TrapMoveUp(8, true, "2.5"/*"0.5"*/, rotateNuM.ToString()/* "2"*/, true, false);//0.5rev/s速度转2圈

        //                        msg = $"Hopper落粉轴转2圈落粉，速度0.5rev/s：TrapMoveUp(8, true, 0.5, rotateNuM.ToString(), true, false)";
        //                        Log4Net.Info(msg);

        //                        Thread.Sleep(1000);//20230411新增：等待1s保证接上粉

        //                        /*****************************************=========>>>***************************************/
        //                        /*****************************************=========>>>***************************************/
        //                        //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
        //                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
        //                        double AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*695*/; double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量
        //                        msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
        //                        Log4Net.Info(msg);
        //                        BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

        //                        double PosValue = 0;
        //                        PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
        //                        msg = $"当前铺粉车位置：打印位置{PosValue}mm";
        //                        Log4Net.Info(msg);
        //                        /*****************************************=========>>>***************************************/
        //                        /*****************************************=========>>>***************************************/
        //                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
        //                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
        //                        /*double*/
        //                        AimPos = 695; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarCureSpeed/*m_dPowderCarBackSpeed*/; //更新值到本地变量
        //                        BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

        //                        msg = $"开启铺粉车不等停运动至站2，速度0.5rev/s：BackToStation2";
        //                        Log4Net.Info(msg);


        //                        //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
        //                        bool m_startFlag = false;
        //                        bool m_startFlag3 = false;//20230411新建:落粉轴预先运动标志位
        //                        bool m_startLightFlag = false;//开灯标志，确保开灯1次
        //                        bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次
        //                        //double PosValue = 0;
        //                        do//检查X轴是否位于指定区域
        //                        {
        //                            /*double*/
        //                            PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置

        //                            //20220920新建：打开UV或者IR灯
        //                            if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
        //                            {
        //                                if (UVIRLightFlag == true)
        //                                {
        //                                    OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

        //                                    msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
        //                                    Log4Net.Info(msg);

        //                                }
        //                                else
        //                                {
        //                                    OpenIRLamp(true);/*打开红外灯*/

        //                                    msg = $"打开IR灯：OpenIRLamp(true)";
        //                                    Log4Net.Info(msg);
        //                                }
        //                                m_startLightFlag = true;
        //                            }
        //                            else { }
        //                            //20220920新建:关闭UV或者IR灯
        //                            if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
        //                            {
        //                                if (UVIRLightFlag == true)
        //                                {
        //                                    OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

        //                                    msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
        //                                    Log4Net.Info(msg);
        //                                }
        //                                else
        //                                {
        //                                    OpenIRLamp(false);/*关闭红外灯*/

        //                                    msg = $"关闭IR灯：OpenIRLamp(false)";
        //                                    Log4Net.Info(msg);
        //                                }
        //                                m_startLightFlag2 = true;
        //                            }
        //#if false
        //                            //(1)预先转30度的撒粉动作
        //                            if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag3 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
        //                            {
        //                                double PreAngleForPowderSupply = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply / 360;//20230411备注：单位为圈数
        //                                double DispenseRollerSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleRotateSpeedForPowderSupply;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数//20230411:1r/s速度
        //                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, Convert.ToString(PreAngleForPowderSupply), true, false/*true*/);//等停运动//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

        //                                msg = $"在{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/}mm}}处，落粉轴运动{{{k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply}度}}，落粉轴转速{{{DispenseRollerSpeed}rev/s}}：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
        //                                Log4Net.Info(msg);

        //                                m_startFlag3 = true;
        //                            }

        //                            //(2)开启撒粉动作
        //                            if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*- 90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
        //                            {
        //                                double DispenseRollerSpeed = 0.5 / ((620 - 255)/*255*/ / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed) * 1.1;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//20230406修正：1.2未补偿系数
        //                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

        //                                msg = $"开启均匀落粉及辊子铺平运动：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
        //                                Log4Net.Info(msg);

        //                                m_startFlag = true;
        //                            }
        //                            else { }
        //#endif
        //                        }
        //                        while (PosValue <= 620);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM

        //                        ////TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位
        //                        ////msg = $"落粉轴继续转动以倒掉余粉：TrapMoveUp(3, true, 2, 0.5, true, false)";
        //                        ////Log4Net.Info(msg);

        //                        int AxiStatus = 0; double prfPos = 0;
        //                        while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
        //                        {
        //                            /*double*/
        //                            PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(7, out prfPos);
        //                            motionMap.GetAxisStatus(7, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
        //                        }

        //                        #region 监控指令：铺粉拍摄位点3
        //                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
        //                        {
        //                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 10);//20230113新建且批注：监控发送指令

        //                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
        //                            Log4Net.Info(msg);
        //                        }
        //                        #endregion

        //                        /*****************************************<<<=========***************************************/
        //                        /*****************************************<<<=========***************************************/
        //                        //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
        //                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
        //                        /*double*/
        //                        AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*695*/; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量
        //                        msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
        //                        Log4Net.Info(msg);
        //                        BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

        //                        /*double*/
        //                        PosValue = 0;
        //                        PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
        //                        msg = $"当前铺粉车位置：打印位置{PosValue}mm";
        //                        Log4Net.Info(msg);

        //                        /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
        //                        /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(14, true);
        //                        GoogolDigtalOut(15, true);
        //#endif

        //                        //20230313新增：单独下降层厚，精度不够：回程1500um
        //                        //20230313新增：单独下降层厚，精度不够：回程1500um
        //                        //20230313新增：单独下降层厚，精度不够：回程1500um
        //                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
        //                        vel = /*1*/2;//Z向运动速度为1mm/s
        //                        TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
        //                                                                //#if OpenMagnetWhenUse
        //                                                                //                GoogolDigtalOut(15,true);
        //                                                                //#endif
        //                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(15, false);
        //                        GoogolDigtalOut(14, false);
        //#endif
        //                        msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        Log4Net.Info(msg);
        //                        /*****************************************=========>>>***************************************/
        //                        /*****************************************=========>>>***************************************/
        //                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
        //                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
        //                        /*double*/
        //                        AimPos = 695; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
        //                        BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

        //                        msg = $"开启铺粉车不等停运动至站2，速度0.5rev/s：BackToStation2";
        //                        Log4Net.Info(msg);


        //                        //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
        //#if false
        //                        bool m_startFlag = false;
        //                        bool m_startFlag3 = false;//20230411新建:落粉轴预先运动标志位
        //                        bool m_startLightFlag = false;//开灯标志，确保开灯1次
        //                        bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次
        //#endif
        //                        //double PosValue = 0;
        //                        do//检查X轴是否位于指定区域
        //                        {
        //                            /*double*/
        //                            PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置
        //#if false
        //                            //20220920新建：打开UV或者IR灯
        //                            if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
        //                            {
        //                                if (UVIRLightFlag == true)
        //                                {
        //                                    OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

        //                                    msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
        //                                    Log4Net.Info(msg);

        //                                }
        //                                else
        //                                {
        //                                    OpenIRLamp(true);/*打开红外灯*/

        //                                    msg = $"打开IR灯：OpenIRLamp(true)";
        //                                    Log4Net.Info(msg);
        //                                }
        //                                m_startLightFlag = true;
        //                            }
        //                            else { }

        //                            //20220920新建:关闭UV或者IR灯
        //                            if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
        //                            {
        //                                if (UVIRLightFlag == true)
        //                                {
        //                                    OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

        //                                    msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
        //                                    Log4Net.Info(msg);
        //                                }
        //                                else
        //                                {
        //                                    OpenIRLamp(false);/*关闭红外灯*/

        //                                    msg = $"关闭IR灯：OpenIRLamp(false)";
        //                                    Log4Net.Info(msg);
        //                                }
        //                                m_startLightFlag2 = true;
        //                            }
        //#endif
        //#if true
        //                            //(1)预先转30度的撒粉动作
        //                            if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag3 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
        //                            {
        //                                double PreAngleForPowderSupply = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply / 360;//20230411备注：单位为圈数
        //                                double DispenseRollerSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleRotateSpeedForPowderSupply;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数//20230411:1r/s速度
        //                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, Convert.ToString(PreAngleForPowderSupply), true, false/*true*/);//等停运动//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

        //                                msg = $"在{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/}mm}}处，落粉轴运动{{{k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply}度}}，落粉轴转速{{{DispenseRollerSpeed}rev/s}}：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
        //                                Log4Net.Info(msg);

        //                                m_startFlag3 = true;
        //                            }

        //                            //(2)开启撒粉动作
        //                            if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*- 90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
        //                            {
        //                                double DispenseRollerSpeed = 0.5 / ((620 - 255)/*255*/ / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed) * 1.1;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//20230406修正：1.2未补偿系数
        //                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

        //                                msg = $"开启均匀落粉及辊子铺平运动：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
        //                                Log4Net.Info(msg);

        //                                m_startFlag = true;
        //                            }
        //                            else { }
        //#endif
        //                        }
        //                        while (PosValue <= 620);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
        //#if true
        //                        TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位
        //                        msg = $"落粉轴继续转动以倒掉余粉：TrapMoveUp(3, true, 2, 0.5, true, false)";
        //                        Log4Net.Info(msg);
        //#endif

        //                        /*int*/
        //                        AxiStatus = 0; /*double*/ prfPos = 0;
        //                        while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
        //                        {
        //                            /*double*/
        //                            PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(7, out prfPos);
        //                            motionMap.GetAxisStatus(7, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
        //                        }
        //                        /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
        //                        /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
        //                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
        //                        vel = /*1*/2;//Z向运动速度为1mm/s
        //                        TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(14, true);
        //                        GoogolDigtalOut(15, true);
        //#endif
        //                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(15, false);
        //                        GoogolDigtalOut(14, false);
        //#endif
        //                        msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        Log4Net.Info(msg);

        //                        //Thread.Sleep(1000);//等待800 ms

        //                        //#region 监控指令：铺粉拍摄位点3
        //                        //                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
        //                        //                        {
        //                        //                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 10);//20230113新建且批注：监控发送指令

        //                        //                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
        //                        //                            Log4Net.Info(msg);
        //                        //                        }
        //                        //#endregion

        //                        /*****************************************<<<=========***********************************/
        //                        /*****************************************<<<=========***********************************/
        //                        //(2)洒粉车回到落粉站位置（回站）：20220512批注
        //                        PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置
        //                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
        //                        double PowderStationCorrection = k_RYSYSParamAutoPrintParamInTest.m_dPowderStationCorrection;
        //                        AimPos = 1 - PowderStationCorrection;
        //                        MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed /*125*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/;//更新值到本地变量
        //                                                                                                                                                       //回程速度125mm/s//20230228修改：回程固化速度可以修改
        //                        BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停
        //                        msg = $"铺粉车返回至站1：BackToStation2(AimPos, MovSpeed, true)：{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
        //                        Log4Net.Info(msg);

        //                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

        //                        //(3)撒粉轴找回零位：20220527新建：
        //                        double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition/*Convert.ToDouble(textBox22.Text)*/;
        //                        bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 2.5/*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s
        //                        if (ReturnCode == true)//校准成功
        //                        {
        //                            msg = $"落粉轴回零成功：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
        //                            Log4Net.Info(msg);

        //                            //MessageBox.Show("回零成功");//成功执行不需要额外的反馈
        //                        }
        //                        else
        //                        {
        //                            msg = $"落粉轴回零失败：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
        //                            Log4Net.Info(msg);

        //                            MessageBox.Show("回零失败");
        //                        }

        //                        /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
        //                        /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
        //                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
        //                        vel = /*1*/2;//Z向运动速度为1mm/s
        //                        TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(14, true);
        //                        GoogolDigtalOut(15, true);
        //#endif
        //                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
        //#if OpenMagnetWhenUse
        //                        GoogolDigtalOut(15, false);
        //                        GoogolDigtalOut(14, false);
        //#endif
        //                        msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
        //                        Log4Net.Info(msg);

        //                        //Thread.Sleep(1000);//等待800 ms

        //                        msg = $"手动铺粉正常结束：NewAutoSupplyPowderThread";
        //                        Log4Net.Info(msg);

        //                        #region 监控指令：铺粉拍摄位点5
        //                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[11])
        //                        {
        //                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 12);//20230113新建且批注：监控发送指令

        //                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
        //                            Log4Net.Info(msg);
        //                        }
        //                        #endregion
        //                    }
        //                    else { }

        //                }
        //                else
        //                { MessageBox.Show("墨车不在正常停靠区间"); }

        //                #region 监控发送指令//20230113新建且批注：
        //                sendMessageToCamera.Dispose(); //20230113新建且批注：监控发送指令
        //                #endregion
        //            }
        //        }


        /// <summary>
        /// 自动铺粉函数，由主界面调用，在自动打印是实现，
        /// 铺粉车先走一遍固化，再走一遍铺粉
        /// </summary>
        /// <param name="toCamera"></param>
        /// <param name="RecordLayerIndex"></param>
        /// <param name="RecordProcessIndex"></param>
        /// <param name="BackCleanMovSpeed"></param>
        ///
        /// DONE::1. 修改铺粉行程
        /// DONE::2. 修改落粉位置
        public void NewAutoSupplyPowderThread2CureFirst(ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex, float BackCleanMovSpeed)//20220512新建：新的上送粉铺粉逻辑
        {
            string msg = $"进入自动铺粉逻辑：NewAutoSupplyPowderThread2";
            Log4Net.Info(msg);

            int nIOState = motionMap.MointoringAxis2(7);//铺粉车固高轴7限位状态         
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                msg = $"中止自动铺粉逻辑，异常停靠区间退出：NewAutoSupplyPowderThread2：ReturnCode{{{nIOState}}}";
                Log4Net.Info(msg);

                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {

#if false
                //(1)判断是否可以执行自动进给铺粉动作
                int DirFlag = 0;
                double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                else { DirFlag = 3; }
#endif
                #region 监控指令：铺粉拍摄位点1
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[7])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 8/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令//202303013修改：修改为8


                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);

                }
                #endregion

                //20220920新建：判断是UV固化还是红外固化
                if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 0)//判断使用UV还是IR作为固化光源
                { UVIRLightFlag = true; }
                else if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 1)
                { UVIRLightFlag = false; }
                else { }

                //A-2: 开启去程固化
                double LightSourceOffset = 70;//默认为UV灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                if (UVIRLightFlag == true) { LightSourceOffset = 70; }
                else { LightSourceOffset = 110; }

                /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
                /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
#if true//铺粉逻辑，暂时注释掉//20220524新建：成型缸逻辑，一次下降1个层厚
                //(1)Z向进给：20210125新增//20220525修改：Z向进给量
                double vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                double TrapSpace = -(double)k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
                                                                                                                                      //#endif
                msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(800);//等待800 ms

                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
                                                         //#if OpenMagnetWhenUse
                                                         //                GoogolDigtalOut(15,true);
                                                         //#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
                                                                                                                                      //#if OpenMagnetWhenUse
                                                                                                                                      //                GoogolDigtalOut(15,false);
                                                                                                                                      //#endif
                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);

#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif

                //#if OpenMagnetWhenUse
                //                GoogolDigtalOut(14, true);
                //                GoogolDigtalOut(15, true);
                //#endif
                //                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                //                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                //                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
                //                                                         //#if OpenMagnetWhenUse
                //                                                         //                GoogolDigtalOut(15,true);
                //                                                         //#endif
                //                TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//不同于默认，为不等停
                //                                                                                                                                      //#if OpenMagnetWhenUse
                //                                                                                                                                      //                GoogolDigtalOut(15,false);
                //                                                                                                                                      //#endif
                //                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                //                Log4Net.Info(msg);

                //                //Thread.Sleep(1000);//等待800 ms

                //                //20230313新增：单独下降层厚，精度不够：回程1500um
                //                //20230313新增：单独下降层厚，精度不够：回程1500um
                //                //20230313新增：单独下降层厚，精度不够：回程1500um
                //                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                //                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                //                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
                //                                                        //#if OpenMagnetWhenUse
                //                                                        //                GoogolDigtalOut(15,true);
                //                                                        //#endif
                //                TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                //#if OpenMagnetWhenUse
                //                GoogolDigtalOut(15, false);
                //                GoogolDigtalOut(14, false);
                //#endif
                //                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                //                Log4Net.Info(msg);
                //                //Thread.Sleep(1000);//等待800 ms

                //#endif

                //(1)落粉站漏斗阀门转指定圈数后停止（接粉）
                double rotateNuM = k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyRotateNum;
                msg = $"当前版本停用轴3初始落粉动作，保留原工艺参数参考：轴3原计划转{rotateNuM}圈";
                Log4Net.Info(msg);

                Thread.Sleep(1000);//20230411新增：等待1s保证接上粉

                /*****************************************=========>>>***************************************/
                /*****************************************=========>>>***************************************/
                //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                double AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*40*//*695*/;
                double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量//20230425修改：修订铺粉位置


                msg = $"开启铺粉车不等停运动，准备运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                double PosValue = 0;
                PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
                msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                Log4Net.Info(msg);


                /*****************************************=========>>>***************************************/
                /*****************************************=========>>>***************************************/
                //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                bool m_startFlag = false;
                bool m_startFlag3 = false;//20230411新建:落粉轴预先运动标志位

                int n_AdapativeDryTimes = 1;//20240102新建：设置固化循环次数为指定值
                int n_AdapativeStartDryPosition = 0;
                int n_AdapativeCloseDryPosition = 365;
                int n_AdapativeDryMoveStopPosition = 620 + 5/*75*/;//20240102新建：固化过程中，墨车的终止运动位置
                if (k_RYSYSParamAutoPrintParamInTest.m_nAdapativeDryMode == 1)
                {
                    n_AdapativeDryTimes = k_RYSYSParamAutoPrintParamInTest.m_nAdapativeDryTimes;//20240102新建：设置固化循环次数为指定值
                    n_AdapativeStartDryPosition = k_RYSYSParamAutoPrintParamInTest.m_nAdapativeStartDryPosition;
                    n_AdapativeCloseDryPosition = k_RYSYSParamAutoPrintParamInTest.m_nAdapativeCloseDryPosition;
                    n_AdapativeDryMoveStopPosition = 255 + k_RYSYSParamAutoPrintParamInTest.m_nAdapativeCloseDryPosition + 5/*75*/;//20240102新建：默认就是一次性走到695位置
                }
                else
                {
                    n_AdapativeDryTimes = 1;//20240102新建：不进行固化循环
                    n_AdapativeDryMoveStopPosition = 620 + 5/*75*/;//20240102新建：默认就是一次性走到695位置
                }

                for (int i = 0; i < n_AdapativeDryTimes; i++)
                {
                    if (k_RYSYSParamAutoPrintParamInTest.m_nAdapativeDryMode == 1)
                    {
                        n_AdapativeDryTimes = k_RYSYSParamAutoPrintParamInTest.m_nAdapativeDryTimes;//20240102新建：设置固化循环次数为指定值
                        n_AdapativeStartDryPosition = k_RYSYSParamAutoPrintParamInTest.m_nAdapativeStartDryPosition;
                        n_AdapativeCloseDryPosition = k_RYSYSParamAutoPrintParamInTest.m_nAdapativeCloseDryPosition;
                        n_AdapativeDryMoveStopPosition = 255 + k_RYSYSParamAutoPrintParamInTest.m_nAdapativeCloseDryPosition + 5/*75*/;//20240102新建：默认就是一次性走到695位置
                    }
                    else
                    {
                        n_AdapativeDryTimes = 1;//20240102新建：不进行固化循环
                        n_AdapativeDryMoveStopPosition = 620 + 5/*75*/;//20240102新建：默认就是一次性走到695位置
                    }

                    //public int m_nAdapativeDryMode = 0;//自适应出光策略：默认0为非自适应固化方式，1为自适应出光区间和出光次数设置
                    //public int m_nAdapativeDryTimes = 1;//固化循环次数（次）//20240102新增：默认1次
                    //public double m_dAdapativeDryWaitTimes = 0;//固化等待时间//20240102新增：默认等待时间为0秒
                    //public int m_nAdapativeStartDryPosition = 255;//自适应出光位置（mm）//20240102新增：默认自适应出光位置 255mm
                    //public int m_nAdapativeCloseDryPosition = 620;//自适应闭光位置（mm）//20240102新增：默认自适应闭光位置 620mm


                    /*****************************************=========>>>***************************************/
                    /*****************************************=========>>>***************************************/
                    //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                    RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                    /*double*/
                    AimPos = n_AdapativeDryMoveStopPosition/*620+75*//*695*/;
                    /*double*/
                    MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarCureSpeed/*m_dPowderCarBackSpeed*/; //更新值到本地变量
                    BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                    msg = $"开启铺粉车不等停运动至站2，速度0.5rev/s：BackToStation2";
                    Log4Net.Info(msg);

                    bool m_startLightFlag = false;//开灯标志，确保开灯1次
                    bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次
                                                   //double PosValue = 0;
                    do//检查X轴是否位于指定区域
                    {
                        /*double*/
                        PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置

                        //20220920新建：打开UV或者IR灯

                        if (PosValue >= (n_AdapativeStartDryPosition + 255 - LightSourceOffset/*70*/)
                            && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                        {
                            if (UVIRLightFlag == true)
                            {
                                OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

                                msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
                                Log4Net.Info(msg);

                            }
                            else
                            {
                                OpenIRLamp(true);/*打开红外灯*/

                                msg = $"打开IR灯：OpenIRLamp(true)";
                                Log4Net.Info(msg);
                            }
                            m_startLightFlag = true;
                        }
                        else { }
                        //20220920新建:关闭UV或者IR灯
                        if (PosValue > (n_AdapativeCloseDryPosition + 255/*+ 620*/ - LightSourceOffset/*-200*//*70*/)
                            && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                        {
                            if (UVIRLightFlag == true)
                            {
                                OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

                                msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
                                Log4Net.Info(msg);
                            }
                            else
                            {
                                OpenIRLamp(false);/*关闭红外灯*/

                                msg = $"关闭IR灯：OpenIRLamp(false)";
                                Log4Net.Info(msg);
                            }
                            m_startLightFlag2 = true;
                        }
#if false
                                //(1)预先转30度的撒粉动作
                                if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag3 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                                {
                                    double PreAngleForPowderSupply = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply / 360;//20230411备注：单位为圈数
                                    double DispenseRollerSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleRotateSpeedForPowderSupply;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数//20230411:1r/s速度
                                    TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, Convert.ToString(PreAngleForPowderSupply), true, false/*true*/);//等停运动//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                                    msg = $"在{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/}mm}}处，落粉轴运动{{{k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply}度}}，落粉轴转速{{{DispenseRollerSpeed}rev/s}}：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                                    Log4Net.Info(msg);

                                    m_startFlag3 = true;
                                }

                                //(2)开启撒粉动作
                                if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*- 90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                                {
                                    double DispenseRollerSpeed = 0.5 / ((620 - 255)/*255*/ / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed) * 1.1;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//20230406修正：1.2未补偿系数
                                    TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                                    msg = $"开启均匀落粉及辊子铺平运动：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                                    Log4Net.Info(msg);

                                    m_startFlag = true;
                                }
                                else { }
#endif
                    }
                    while (PosValue <= n_AdapativeDryMoveStopPosition - 5/*75*//*620*/);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM

                    ////TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位
                    ////msg = $"落粉轴继续转动以倒掉余粉：TrapMoveUp(3, true, 2, 0.5, true, false)";
                    ////Log4Net.Info(msg);

                    int AxiStatus2 = 0; /*double prfPos = 0;*/
                    while ((Math.Abs(PosValue) < Math.Abs(n_AdapativeDryMoveStopPosition/*620 + 75*//*695*/)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus2 & 0x20) == 0) && ((AxiStatus2 & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                    {
                        /*double*/
                        PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(7, out prfPos);
                        motionMap.GetAxisStatus(7, out AxiStatus2); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
                    }

                    #region 监控指令：铺粉拍摄位点3

                    if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
                    {
                        toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 10/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令

                        msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                        Log4Net.Info(msg);
                    }
                    #endregion

                    /*****************************************<<<=========***************************************/
                    /*****************************************<<<=========***************************************/
                    //20240103新增：固化灯快速移动到成型区域左侧的出光区域，准备下次出光操作
                    AimPos = k_RYSYSParamAutoPrintParamInTest.m_nAdapativeStartDryPosition + 255 - LightSourceOffset;//AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply; 
                    MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量
                    msg = $"开启铺粉车不等停运动至再次出光前位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                    Log4Net.Info(msg);
                    BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                    PosValue = 0;
                    PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
                    msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                    Log4Net.Info(msg);

                    /*****************************************^^^^^^^^^^^^***************************************/
                    /*****************************************^^^^^^^^^^^^***************************************/
                    if (i < n_AdapativeDryTimes - 1)
                    {
                        //20240102新建：执行固化等待时间
                        Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dAdapativeDryWaitTimes * 1000));//等待指定时间，最长60s
                    }

                    /*****************************************@@@@@@@@@@@@***************************************/
                    /*****************************************@@@@@@@@@@@@***************************************/
                    //20240105新增：隔一定次数进行清洗，避免打印过程中喷头堵塞
                    if ((i % k_RYSYSParamAutoPrintParamInTest.m_nAdapativeCleanPerDryTimes == 0) && (i != 0)) //只在指定层执行自动清洗
                    {
                        AutoCleanThread2(BackCleanMovSpeed);
                    }
                }

                /*****************************************<<<=========***************************************/
                /*****************************************<<<=========***************************************/
                //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                /*double*/
                // DONE::需要替换常数255
                //AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*695*/;
                AimPos = POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*695*/; 
                MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量

                msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                /*double*/
                PosValue = 0;
                PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
                msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                Log4Net.Info(msg);

                /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
                /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif

                //Thread.Sleep(1000);//等待800 ms

                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
                                                        //#if OpenMagnetWhenUse
                                                        //                GoogolDigtalOut(15,true);
                                                        //#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(1000);//等待800 ms

#endif

                /*****************************************=========>>>***************************************/
                /*****************************************=========>>>***************************************/
                //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                /*double*/
                // DONE::需要替换常数255
                //AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*40*//*695*/; /*double*/
                AimPos = POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*40*//*695*/; /*double*/
                MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量//20230425修改：修订铺粉位置
                msg = $"开启铺粉车不等停运动，准备运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                /*double*/
                PosValue = 0;
                PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
                msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                Log4Net.Info(msg);

                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                /*double*/
                // DONE::需要替换常数695
                //AimPos = 695; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                AimPos = POWDERCAR_TRAVEL_DIST; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停
                msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);

                //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                ////bool m_startFlag = false;
                bool m_startFlag2 = false;//20230411新建:落粉轴预先运动标志位
                ////bool m_startLightFlag = false;//开灯标志，确保开灯1次
                ////bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次

                do//检查X轴是否位于指定区域
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
#if false
                    //20220920新建：打开UV或者IR灯
                    if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

                            msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(true);/*打开红外灯*/

                            msg = $"打开IR灯：OpenIRLamp(true)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag = true;
                    }
                    else { }
                    //20220920新建:关闭UV或者IR灯
                    if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

                            msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(false);/*关闭红外灯*/

                            msg = $"关闭IR灯：OpenIRLamp(false)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag2 = true;
                    }
#endif
                    //(1)预先转30度的撒粉动作
                    // DONE::需要替换常数255
                    //if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag2 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                    if (PosValue >= (POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag2 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                    {
                        msg = $"到达轴3原预落粉触发位置{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply}mm}}，当前版本停用轴3预落粉动作；超声暂不启动，等待进入正式工作区";
                        Log4Net.Info(msg);

                        m_startFlag2 = true;
                    }

                    //(2)开启正式的撒粉动作
                    // DONE::需要替换常数255
                    //if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply /*90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                    if (PosValue >= (POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply /*90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                    {
                        ControlUltrasonicViaGoogolIO(true);//20260417调整：超声仅在正式工作区开启
                        msg = $"到达正式铺粉区，当前版本停用轴3均匀落粉/铺平动作；开启超声装置：ControlUltrasonicViaGoogolIO(true)";
                        Log4Net.Info(msg);

                        m_startFlag = true;
                    }
                    else { }
                } while (PosValue <= POWDERCAR_DROP_END);// DONE::需要替换常数620
                //while (PosValue <= 620);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM

                ControlUltrasonicViaGoogolIO(false);//20251210批注：行程关闭时关闭超声装置

                msg = $"粉辊去程结束，联动关闭超声装置：ControlUltrasonicViaGoogolIO(false)；当前版本停用轴3尾部倒余粉动作";
                Log4Net.Info(msg);

                int
                AxiStatus = 0; double prfPos = 0;

                // DONE::需要替换常数695
                //while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                while ((Math.Abs(PosValue) < Math.Abs(POWDERCAR_TRAVEL_DIST)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7 //motionMap.GetPrfPos(7, out prfPos);
                    motionMap.GetAxisStatus(7, out AxiStatus); //20260416修改：当前铺粉车运动轴改为轴7
                }

                /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
                /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);

                //Thread.Sleep(1000);//等待800 ms

                /*****************************************<<<=========***********************************/
                /*****************************************<<<=========***********************************/
                //(2)洒粉车回到落粉站位置（回站）：20220512批注
                PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                double PowderStationCorrection = k_RYSYSParamAutoPrintParamInTest.m_dPowderStationCorrection;
                AimPos = 1 - PowderStationCorrection;
                MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*125*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/;//更新值到本地变量//回程速度125mm/s
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停
                msg = $"铺粉车返回至站1：BackToStation2(AimPos, MovSpeed, true)：{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);

                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

                //20251206批注：更换铺粉解释，无需落粉轴
                ////(3)撒粉轴找回零位：20220527新建：
                //double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition/*Convert.ToDouble(textBox22.Text)*/;
                //bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 2.5/*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s//20230403修改：
                //if (ReturnCode == true)//校准成功
                //{
                //    msg = $"落粉轴回零成功：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                //    Log4Net.Info(msg);

                //    //MessageBox.Show("回零成功");//成功执行不需要额外的反馈
                //}
                //else
                //{
                //    msg = $"落粉轴回零失败：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                //    Log4Net.Info(msg);

                //    MessageBox.Show("回零失败");
                //}

                /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
                /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(1000);//等待800 ms

                msg = $"自动铺粉正常结束：NewAutoSupplyPowderThread2";
                Log4Net.Info(msg);

                #region 监控指令：铺粉拍摄位点5
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[11])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 12/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令

                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);
                }
                #endregion
            }
        }


        /// <summary>
        /// 自动铺粉函数，没有引用，备份版本
        /// </summary>
        /// <param name="toCamera"></param>
        /// <param name="RecordLayerIndex"></param>
        /// <param name="RecordProcessIndex"></param>
        public void NewAutoSupplyPowderThread2CureFirst20240104beifen(ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex)//20220512新建：新的上送粉铺粉逻辑
        {
            string msg = $"进入自动铺粉逻辑：NewAutoSupplyPowderThread2";
            Log4Net.Info(msg);

            int nIOState = motionMap.MointoringAxis2(7);//铺粉车固高轴7限位状态         
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                msg = $"中止自动铺粉逻辑，异常停靠区间退出：NewAutoSupplyPowderThread2：ReturnCode{{{nIOState}}}";
                Log4Net.Info(msg);

                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {

#if false
                //(1)判断是否可以执行自动进给铺粉动作
                int DirFlag = 0;
                double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                else { DirFlag = 3; }
#endif
#region 监控指令：铺粉拍摄位点1
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[7])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 8/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令//202303013修改：修改为8


                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);

                }
#endregion

                //20220920新建：判断是UV固化还是红外固化
                if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 0)//判断使用UV还是IR作为固化光源
                { UVIRLightFlag = true; }
                else if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 1)
                { UVIRLightFlag = false; }
                else { }

                //A-2: 开启去程固化
                double LightSourceOffset = 70;//默认为UV灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                if (UVIRLightFlag == true) { LightSourceOffset = 70; }
                else { LightSourceOffset = 110; }

/*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
/*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
#if true//铺粉逻辑，暂时注释掉//20220524新建：成型缸逻辑，一次下降1个层厚
                //(1)Z向进给：20210125新增//20220525修改：Z向进给量
                double vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                double TrapSpace = -(double)k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
                                                                                                                         //#endif
                msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(800);//等待800 ms

                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
                                                         //#if OpenMagnetWhenUse
                                                         //                GoogolDigtalOut(15,true);
                                                         //#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
                                                                                                                                      //#if OpenMagnetWhenUse
                                                                                                                                      //                GoogolDigtalOut(15,false);
                                                                                                                                      //#endif
                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);

#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif

//#if OpenMagnetWhenUse
//                GoogolDigtalOut(14, true);
//                GoogolDigtalOut(15, true);
//#endif
//                //20230313新增：单独下降层厚，精度不够：继续下降1500um
//                //20230313新增：单独下降层厚，精度不够：继续下降1500um
//                //20230313新增：单独下降层厚，精度不够：继续下降1500um
//                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
//                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
//                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
//                                                         //#if OpenMagnetWhenUse
//                                                         //                GoogolDigtalOut(15,true);
//                                                         //#endif
//                TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//不同于默认，为不等停
//                                                                                                                                      //#if OpenMagnetWhenUse
//                                                                                                                                      //                GoogolDigtalOut(15,false);
//                                                                                                                                      //#endif
//                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
//                Log4Net.Info(msg);

//                //Thread.Sleep(1000);//等待800 ms

//                //20230313新增：单独下降层厚，精度不够：回程1500um
//                //20230313新增：单独下降层厚，精度不够：回程1500um
//                //20230313新增：单独下降层厚，精度不够：回程1500um
//                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
//                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
//                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
//                                                        //#if OpenMagnetWhenUse
//                                                        //                GoogolDigtalOut(15,true);
//                                                        //#endif
//                TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15, false);
//                GoogolDigtalOut(14, false);
//#endif
//                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
//                Log4Net.Info(msg);
//                //Thread.Sleep(1000);//等待800 ms

//#endif

                //(1)落粉站漏斗阀门转指定圈数后停止（接粉）
                double rotateNuM = k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyRotateNum;
                msg = $"当前版本停用轴3初始落粉动作，保留原工艺参数参考：轴3原计划转{rotateNuM}圈";
                Log4Net.Info(msg);

                Thread.Sleep(1000);//20230411新增：等待1s保证接上粉

/*****************************************=========>>>***************************************/
/*****************************************=========>>>***************************************/
                //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                double AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*40*//*695*/; double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量//20230425修改：修订铺粉位置
                msg = $"开启铺粉车不等停运动，准备运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                double PosValue = 0;
                PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
                msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                Log4Net.Info(msg);

/*****************************************=========>>>***************************************/
/*****************************************=========>>>***************************************/
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                /*double*/
                AimPos = 695; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarCureSpeed/*m_dPowderCarBackSpeed*/; //更新值到本地变量
                BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停
                msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);

                //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                bool m_startFlag = false;
                bool m_startFlag2 = false;//20230411新建:落粉轴预先运动标志位
                bool m_startLightFlag = false;//开灯标志，确保开灯1次
                bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次

                do//检查X轴是否位于指定区域
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置

                    //20220920新建：打开UV或者IR灯
                    if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

                            msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(true);/*打开红外灯*/

                            msg = $"打开IR灯：OpenIRLamp(true)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag = true;
                    }
                    else { }
                    //20220920新建:关闭UV或者IR灯
                    if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

                            msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(false);/*关闭红外灯*/

                            msg = $"关闭IR灯：OpenIRLamp(false)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag2 = true;
                    }
#if false
                    //////(1)预先转30度的撒粉动作
                    ////if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag2 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                    ////{
                    ////    double PreAngleForPowderSupply = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply / 360;//20230411备注：单位为圈数
                    ////    double DispenseRollerSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleRotateSpeedForPowderSupply;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数//20230411:1r/s速度
                    ////    TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, Convert.ToString(PreAngleForPowderSupply), true, false/*true*/);//等停运动//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                    ////    msg = $"在{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/}mm}}处，落粉轴运动{{{k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply}度}}，落粉轴转速{{{DispenseRollerSpeed}rev/s}}：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                    ////    Log4Net.Info(msg);

                    ////    m_startFlag2 = true;
                    ////}

                    //////(2)开启正式的撒粉动作
                    ////if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply /*90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                    ////{
                    ////    double DispenseRollerSpeed = 0.5 / ((620 - 255)/*255*/ / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed) * 1.1;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数
                    ////    TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                    ////    msg = $"开启均匀落粉及辊子铺平运动：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                    ////    Log4Net.Info(msg);

                    ////    m_startFlag = true;
                    ////}
                    ////else { }
#endif
                }
                while (PosValue <= 620);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
#if false
                TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位
                msg = $"落粉轴继续转动以倒掉余粉：TrapMoveUp(3, true, 2, 0.5, true, false)";
                Log4Net.Info(msg);
#endif

                int AxiStatus = 0; double prfPos = 0;
                while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(7, out prfPos);
                    motionMap.GetAxisStatus(7, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
                }

#region 监控指令：铺粉拍摄位点3
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 10/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令

                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);
                }
#endregion

/*****************************************<<<=========***************************************/
/*****************************************<<<=========***************************************/
                //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                /*double*/
                AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*695*/; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量
                msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                /*double*/
                PosValue = 0;
                PosValue = GetCurrentPos(7);//20230425新建：实时铺粉车位置
                msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                Log4Net.Info(msg);

/*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
/*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif

                //Thread.Sleep(1000);//等待800 ms

                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
                                                        //#if OpenMagnetWhenUse
                                                        //                GoogolDigtalOut(15,true);
                                                        //#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(1000);//等待800 ms

#endif

/*****************************************=========>>>***************************************/
/*****************************************=========>>>***************************************/
                //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                /*double*/
                AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*40*//*695*/; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量//20230425修改：修订铺粉位置
                msg = $"开启铺粉车不等停运动，准备运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                /*double*/ PosValue = 0;
                PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
                msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                Log4Net.Info(msg);

                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                /*double*/
                AimPos = 695; /*double*/ MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停
                msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);

                //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                ////bool m_startFlag = false;
                ////bool m_startFlag2 = false;//20230411新建:落粉轴预先运动标志位
                ////bool m_startLightFlag = false;//开灯标志，确保开灯1次
                ////bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次

                do//检查X轴是否位于指定区域
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
#if false
                    //20220920新建：打开UV或者IR灯
                    if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

                            msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(true);/*打开红外灯*/

                            msg = $"打开IR灯：OpenIRLamp(true)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag = true;
                    }
                    else { }
                    //20220920新建:关闭UV或者IR灯
                    if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

                            msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(false);/*关闭红外灯*/

                            msg = $"关闭IR灯：OpenIRLamp(false)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag2 = true;
                    }
#endif

                    //(1)预先转30度的撒粉动作
                    if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag2 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                    {
                        double PreAngleForPowderSupply = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply / 360;//20230411备注：单位为圈数
                        double DispenseRollerSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleRotateSpeedForPowderSupply;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数//20230411:1r/s速度
                        TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, Convert.ToString(PreAngleForPowderSupply), true, false/*true*/);//等停运动//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                        msg = $"在{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/}mm}}处，落粉轴运动{{{k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply}度}}，落粉轴转速{{{DispenseRollerSpeed}rev/s}}：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                        Log4Net.Info(msg);

                        m_startFlag2 = true;
                    }

                    //(2)开启正式的撒粉动作
                    if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply /*90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                    {
                        double DispenseRollerSpeed = 0.5 / ((620 - 255)/*255*/ / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed) * 1.1;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数
                        TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                        msg = $"开启均匀落粉及辊子铺平运动：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                        Log4Net.Info(msg);

                        m_startFlag = true;
                    }
                    else { }
                }
                while (PosValue <= 620);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
                TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位

                msg = $"落粉轴继续转动以倒掉余粉：TrapMoveUp(3, true, 2, 0.5, true, false)";
                Log4Net.Info(msg);

                /*int*/ AxiStatus = 0; /*double*/ prfPos = 0;
                while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7 //motionMap.GetPrfPos(7, out prfPos);
                    motionMap.GetAxisStatus(7, out AxiStatus); //20260416修改：当前铺粉车运动轴改为轴7
                }

                /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
                /*****************************************↓↓↓↓↓↓↓↓↓↓***********************************/
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);

                //Thread.Sleep(1000);//等待800 ms

/*****************************************<<<=========***********************************/
/*****************************************<<<=========***********************************/
                //(2)洒粉车回到落粉站位置（回站）：20220512批注
                PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                double PowderStationCorrection = k_RYSYSParamAutoPrintParamInTest.m_dPowderStationCorrection;
                AimPos = 1 - PowderStationCorrection;
                MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*125*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/;//更新值到本地变量//回程速度125mm/s
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停
                msg = $"铺粉车返回至站1：BackToStation2(AimPos, MovSpeed, true)：{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);

                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

                //20251206批注：更换铺粉解释，无需落粉轴
                ////(3)撒粉轴找回零位：20220527新建：
                //double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition/*Convert.ToDouble(textBox22.Text)*/;
                //bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 2.5/*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s//20230403修改：
                //if (ReturnCode == true)//校准成功
                //{
                //    msg = $"落粉轴回零成功：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                //    Log4Net.Info(msg);

                //    //MessageBox.Show("回零成功");//成功执行不需要额外的反馈
                //}
                //else
                //{
                //    msg = $"落粉轴回零失败：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                //    Log4Net.Info(msg);

                //    MessageBox.Show("回零失败");
                //}

                /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
                /*****************************************↑↑↑↑↑↑↑↑↑↑***********************************/
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(1000);//等待800 ms

                msg = $"自动铺粉正常结束：NewAutoSupplyPowderThread2";
                Log4Net.Info(msg);

#region 监控指令：铺粉拍摄位点5
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[11])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 12/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令

                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);
                }
#endregion
            }
        }
        private void GoogolDigtalOut(int myTag, bool OpenEnabled)//成型缸电磁铁动作
        {
            //(b1)根据ID反转并存储到对应控件列表
            //motionMap.m_bIoEnable[myTag - 1] = !motionMap.m_bIoEnable[myTag - 1];

            if (OpenEnabled == true)
            {
                motionMap.SetDo((short)myTag, true);
                string msg = "电气控制模块：手动打开端口" + myTag;
                Log4Net.Info(msg);

                //motionMap.m_bIoEnable[myTag - 1] = true;
            }
            else
            {
                motionMap.SetDo((short)myTag, false);
                string msg = "电气控制模块：手动关闭端口" + myTag;
                Log4Net.Info(msg);

               //motionMap.m_bIoEnable[myTag - 1] = false;
            }
        }


        /// <summary>
        /// 自动送粉铺粉函数
        /// </summary>
        /// <param name="toCamera"></param>
        /// <param name="RecordLayerIndex"></param>
        /// <param name="RecordProcessIndex"></param>
        /*
         * 二代设备铺粉车运行参数：
         *      最大行程: 695mm
         *      落粉位置: 255mm
         *      结束落粉: 620mm
         *                      去程         回程
         *              O -------->           <-------- O
         *              x-----------x-----------x-------x
         *              ^           ^           ^       ^
         *              |           |           |       |
         *             接粉        落粉       结束落粉   回程 
         *              |<-- 255 -->|           |       |
         *              |<-------- 620 -------->|       |
         *              |<------------ 695 ------------>|
         *              
         * 三代设备铺粉车运行常数：
         *      最大行程: 920mm，设定918mm
         *      落粉位置: 400mm
         *      结束落粉: 825mm
         * 
         */
        public void NewAutoSupplyPowderThread2(ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex)//20220512新建：新的上送粉铺粉逻辑
        {
            string msg = $"进入自动铺粉逻辑：NewAutoSupplyPowderThread2";
            Log4Net.Info(msg);

            int nIOState = motionMap.MointoringAxis2(7);//铺粉车固高轴7限位状态         
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                msg = $"中止自动铺粉逻辑，异常停靠区间退出：NewAutoSupplyPowderThread2：ReturnCode{{{nIOState}}}";
                Log4Net.Info(msg);

                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {

#if false
                //(1)判断是否可以执行自动进给铺粉动作
                int DirFlag = 0;
                double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                else { DirFlag = 3; }
#endif
#region 监控指令：铺粉拍摄位点1
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[7])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 8/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令//202303013修改：修改为8


                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);

                }
#endregion

                //20220920新建：判断是UV固化还是红外固化
                if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 0)//判断使用UV还是IR作为固化光源
                { UVIRLightFlag = true; }
                else if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 1)
                { UVIRLightFlag = false; }
                else { }

                //A-2: 开启去程固化
                double LightSourceOffset = 70;//默认为UV灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                if (UVIRLightFlag == true) { LightSourceOffset = 70; }
                else { LightSourceOffset = 110; }

#if true//铺粉逻辑，暂时注释掉//20220524新建：成型缸逻辑，一次下降1个层厚
                //(1)Z向进给：20210125新增//20220525修改：Z向进给量
                double vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                double TrapSpace = -(double)k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);
//#endif
                msg = $"成形面高度下降层厚 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(800);//等待800 ms

                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20230313新增：单独下降层厚，精度不够：继续下降1500um
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                /*
                 * 成型杠下降，Leon，2024/04/16
                 */
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);
//#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,false);
//#endif
                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);

                //Thread.Sleep(1000);//等待800 ms

                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20230313新增：单独下降层厚，精度不够：回程1500um
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                /*
                 * 成型缸上升，Leon，2024/04/16
                 */
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
//#if OpenMagnetWhenUse
//                GoogolDigtalOut(15,true);
//#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(1000);//等待800 ms

#endif

                /*
                 * 初始落粉，Leon，2024/04/16
                 */
                //(1)落粉站漏斗阀门转指定圈数后停止（初始落粉）：20220512批注
                double rotateNuM = k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyRotateNum;
                msg = $"当前版本停用轴3初始落粉动作，保留原工艺参数参考：轴3原计划转{rotateNuM}圈";
                Log4Net.Info(msg);

                Thread.Sleep(1000);//20230411新增：等待1s保证接上粉

                /*
                 * 铺粉车去程开始，Leon，2024/04/16
                 */
                //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)//20230403修改：快速移动到成型区域
                // DONE::需要替换常数255, 2024/04/17, Leon
                //double AimPos = 255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*40*//*695*/;
                double AimPos = POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*40*//*695*/;
                double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*250*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/; //更新值到本地变量//20230425修改：修订铺粉位置
                msg = $"开启铺粉车不等停运动，准备运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                double PosValue = 0;
                PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
                msg = $"当前铺粉车位置：打印位置{PosValue}mm";
                Log4Net.Info(msg);

                //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                /*double*/
                // DONE::需要替换常数695
                //AimPos = 695;       // 铺粉车去程最大行程，695mm
                AimPos = POWDERCAR_TRAVEL_DIST;       // 铺粉车去程最大行程
                /*double*/
                MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量

                /*
                 * 铺粉车去程
                 */
                BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停
                msg = $"开启铺粉车不等停运动至准备打印位置{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);

                //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                bool m_startFlag = false;
                bool m_startFlag2 = false;//20230411新建:落粉轴预先运动标志位
                bool m_startLightFlag = false;//开灯标志，确保开灯1次
                bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次

                do//检查X轴是否位于指定区域
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7

                    /*
                     * UV或IR固化控制
                     */
                    //20220920新建：打开UV或者IR灯
                    // DONE::需要替换常数255
                    //if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    if (PosValue >= (POWDERCAR_DROP_BEGIN - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/

                            msg = $"打开UV灯：OpenUVLamp(true, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(true);/*打开红外灯*/

                            msg = $"打开IR灯：OpenIRLamp(true)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag = true;
                    }
                    else { }
                    //20220920新建:关闭UV或者IR灯
                    // DONE::需要替换常数620
                    //if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    if (PosValue > (POWDERCAR_DROP_END - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                    {
                        if (UVIRLightFlag == true)
                        {
                            OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/

                            msg = $"关闭UV灯：OpenUVLamp(false, 0, 1000, 0, 1000)";
                            Log4Net.Info(msg);
                        }
                        else
                        {
                            OpenIRLamp(false);/*关闭红外灯*/

                            msg = $"关闭IR灯：OpenIRLamp(false)";
                            Log4Net.Info(msg);
                        }
                        m_startLightFlag2 = true;
                    }
                    //////(1)预先转30度的撒粉动作
                    ////if (PosValue >= k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/ && m_startFlag2 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                    ////{
                    ////    double PreAngleForPowderSupply = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply/360;//20230411备注：单位为圈数
                    ////    double DispenseRollerSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPreAngleRotateSpeedForPowderSupply;//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;//此处存在问题//20230406修正：1.2未补偿系数//20230411:1r/s速度
                    ////    TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, Convert.ToString(PreAngleForPowderSupply)/*"0.097"*/ /*"0.5"*//*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/

                    ////    msg = $"在{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply/*220*/}mm}}处，落粉轴运动{{{k_RYSYSParamAutoPrintParamInTest.m_dPreAngleForPowderSupply}度}}，落粉轴转速{{{DispenseRollerSpeed}rev/s}}：TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed), 0.5, true, true)";
                    ////    Log4Net.Info(msg);

                    ////    m_startFlag2 = true;
                    ////}

                    /*
                     * 落粉控制
                     * 20260416: 轴3落粉功能已由超声功能替代，保留区间判定但停用轴3动作
                     */
                    //(1)预先转30度的撒粉动作
                    // DONE::需要替换常数255
                    //if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag2 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                    if (PosValue >= (POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply)/*220*/ && m_startFlag2 == false)//20230411新建开启扫粉轴：先匀速转半圈（策略1）：到达220的时候先转30度
                    {
                        msg = $"到达轴3原预落粉触发位置{{{k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply}mm}}，当前版本停用轴3预落粉动作；超声暂不启动，等待进入正式工作区";
                        Log4Net.Info(msg);

                        m_startFlag2 = true;
                    }


                    //(2)开启正式的撒粉动作
                    /*
                     * DONE::修改撒粉位置
                     * DONE::需要替换常数255，常数620
                     *      
                     */
                    //if (PosValue >= (255 - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply /*90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                    if (PosValue >= (POWDERCAR_DROP_BEGIN - k_RYSYSParamAutoPrintParamInTest.PreAngleRotatePositionForPowderSupply /*90 - 40*/) && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）//20230411:200mm/s补偿9CM
                    {
                        ControlUltrasonicViaGoogolIO(true);//20260417调整：超声仅在正式工作区开启
                        msg = $"到达正式铺粉区，当前版本停用轴3均匀落粉/铺平动作；开启超声装置：ControlUltrasonicViaGoogolIO(true)";
                        Log4Net.Info(msg);

                        m_startFlag = true;
                    }
                    else { }

                    Thread.Sleep(10);

                } while (PosValue <= POWDERCAR_DROP_END);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
                //while (PosValue <= 620) ;//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM


                ControlUltrasonicViaGoogolIO(false);//20251210批注：行程关闭时关闭超声装置

                msg = $"粉辊去程结束，联动关闭超声装置：ControlUltrasonicViaGoogolIO(false)；当前版本停用轴3尾部倒余粉动作";
                Log4Net.Info(msg);

                int AxiStatus = 0; double prfPos = 0;
                // DONE::需要替换常数695
                //while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                while ((Math.Abs(PosValue) < Math.Abs(POWDERCAR_TRAVEL_DIST)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉车（固高轴7）停止；墨车Y为轴2勿混
                {
                    /*double*/
                    PosValue = GetCurrentPos(7);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(7, out prfPos);
                    motionMap.GetAxisStatus(7, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);

                    Thread.Sleep(10);
                }

                /*
                 * 准备回程，Leon，2024/04/16
                 */
                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = -(double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度下降指定厚度 TrapSpace{{{-TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);

                //Thread.Sleep(1000);//等待800 ms

#region 监控指令：铺粉拍摄位点3
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 10/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令

                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);
                }
#endregion

                //(2)洒粉车回到落粉站位置（回站）：20220512批注
                /*
                 * 铺粉车回程
                 */
                PosValue = GetCurrentPos(7);//20260416修改：当前铺粉车运动轴改为轴7
                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                double PowderStationCorrection = k_RYSYSParamAutoPrintParamInTest.m_dPowderStationCorrection;
                AimPos = 1 - PowderStationCorrection;
                MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCureBackSpeed/*125*//*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/;//更新值到本地变量//回程速度125mm/s
                BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停
                msg = $"铺粉车返回至站1：BackToStation2(AimPos, MovSpeed, true)：{AimPos}mm，速度{MovSpeed}mm/s：BackToStation2";
                Log4Net.Info(msg);

                RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

                //20251206批注：更换铺粉解释，无需落粉轴
                ////(3)撒粉轴找回零位：20220527新建：
                //double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition/*Convert.ToDouble(textBox22.Text)*/;
                //bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 2.5/*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s//20230403修改：
                //if (ReturnCode == true)//校准成功
                //{
                //    msg = $"落粉轴回零成功：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                //    Log4Net.Info(msg);

                //    //MessageBox.Show("回零成功");//成功执行不需要额外的反馈
                //}
                //else
                //{
                //    msg = $"落粉轴回零失败：motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition)：ReturnCode{{{ReturnCode}}}";
                //    Log4Net.Info(msg);

                //    MessageBox.Show("回零失败");
                //}

                //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                vel = 2/*1*/;//Z向运动速度为1mm/s//20230403修改：
                TrapSpace = (double)1500 / (double)1000;//20220525新建批注：层厚：调试用150μm//为负方向
#if OpenMagnetWhenUse
                GoogolDigtalOut(14, true);
                GoogolDigtalOut(15, true);
#endif
                TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, false/*true*//*!WaitStopFLag*//*true*/);//20260416修改：当前成型缸轴改为轴8
#if OpenMagnetWhenUse
                GoogolDigtalOut(15, false);
                GoogolDigtalOut(14, false);
#endif
                msg = $"成形面高度上升层厚 TrapSpace{{{TrapSpace}mm}}：TrapMoveUp(8, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true）";
                Log4Net.Info(msg);
                //Thread.Sleep(1000);//等待800 ms

                msg = $"自动铺粉正常结束：NewAutoSupplyPowderThread2";
                Log4Net.Info(msg);

#region 监控指令：铺粉拍摄位点5
                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[11])
                {
                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 12/*RecordProcessIndex*/);//20230113新建且批注：监控发送指令

                    msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                    Log4Net.Info(msg);
                }
#endregion
            }
        }



        /// <summary>
        /// 
        /// </summary>
        [Obsolete("旧版进给铺粉入口：CreateAndDeleteThread 中线程名 AutoSupplyPowderThread 仅保留兼容；新逻辑请用 NewAutoSupplyPowderThread（内部已走 NewAutoSupplyPowderThread2*）。")]
        public void AutoSupplyPowderThread()//自动进给铺粉动作
        {
            int nIOState = motionMap.MointoringAxis2(7);//铺粉车固高轴7限位状态（原误用轴4）
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {
                //(1)判断是否可以执行自动进给铺粉动作
                int DirFlag = 0;
                double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                else { DirFlag = 3; }

                if ((DirFlag == 1) || (DirFlag == 2))//墨车在非安全区域++粉车在正常工作区间内==粉末在正负限位区间内
                {
#if false//上送粉到位逻辑
                    TrapMoveDown(1, true/* m_bMoveModeFlag[0]*/, "5"/*Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_nLayerThick)*//*m_sVel[0]*/,
                        Convert.ToString(3000 + k_RYSYSParamAutoPrintParamInTest.m_nLayerThick)/*m_sStep[0]*/, true);//(1)成型缸点动下降3000um;
                    Thread.Sleep(100);

                    RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;
                    double[] g_dEncpos = new double[8]; g_dEncpos = motionMap.GetEncPos(); double PosValue = g_dEncpos[3] / 1000;//当前粉车位置：
                    double TrapSpace = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarStartposition - PosValue;//240MM处为铺粉速度临界点
                    TrapMoveUp(4, true/*m_bMoveModeFlag[0]*/, Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed)/*m_sVel[0]*/,
                        Convert.ToString(TrapSpace)/*m_sStep[0]*/, true);//(2)铺粉车移动到手动填粉位置；//30mm位置处
                    Thread.Sleep(100);

                    TrapMoveUp(1, true/*m_bMoveModeFlag[0]*/, "5"/*m_sVel[0]*/, "3000"/*m_sStep[0]*/, true);//(3)成型缸上升3000-移动量um;
                    //进入下一打印环节；等待继续铺
#else//下送粉1st铺粉逻辑：20201029新增
                    if (1 == k_RYSYSParamAutoPrintParamInTest.m_nRecoaterStrategy)//(1)间接铺粉方式：20210125新增
                    {
                        //(1)Z向进给：20210125新增
                        TrapMoveUp(2, true, "1",
                        Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_nLayerThick2 * k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyCoefficient/*2*/), true, false);//(1)靠人侧送粉缸点动上升进给量粉末;m_nLayerThick2进给2
                        Thread.Sleep(300);
                        TrapMoveDown(1, true, "5",
                            Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_nLayerThick2), true, false);//(2)成型缸点动下降层厚;m_nLayerThick2进给2
                        Thread.Sleep(300);


                        //(2)开始铺粉：20210125新增
                        //RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：更新辊子速度
                        RollerParam = /*120*//*0*/k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        double[] g_dEncpos = new double[8]; g_dEncpos = motionMap.GetEncPos(); double PosValue = g_dEncpos[3] / 1000;//当前粉车位置：
                        double TrapSpace = /*240*/230 - PosValue;//240MM处为铺粉速度临界点
                        TrapMoveUp(4, true, Convert.ToString(/*40*//*60*/k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed),
                            Convert.ToString(TrapSpace), true, false);//(2)铺粉车移动到手动填粉位置;//30mm位置处

                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        g_dEncpos = motionMap.GetEncPos(); PosValue = g_dEncpos[3] / 1000;//当前粉车位置：
                        TrapSpace = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarStartposition - PosValue;
                        TrapMoveUp(4, true, Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed),
                            Convert.ToString(TrapSpace), true, false);//(4)铺粉车移动到正限位位置；//0mm位置处

                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速
                                                                                      //进入下一打印环节；等待继续铺
                    }
                    else if (0 == k_RYSYSParamAutoPrintParamInTest.m_nRecoaterStrategy)//(2)直接铺粉方式：20210125新增
                    {
                        //(1)Z向进给：20210125新增
                        TrapMoveUp(2, true, "1",
                            Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_nLayerThick * k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyCoefficient/*2*/), true, false);//(1)靠人侧送粉缸点动上升进给量粉末;m_nLayerThick2进给2
                        Thread.Sleep(300);
                        TrapMoveDown(1, true, "5",
                            Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_nLayerThick), true, false);//(2)成型缸点动下降层厚;m_nLayerThick2进给2
                        Thread.Sleep(300);

                        //(2)开始铺粉：20210125新增
                        //RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：更新辊子速度
                        RollerParam = /*120*//*0*/k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        double[] g_dEncpos = new double[8]; g_dEncpos = motionMap.GetEncPos(); double PosValue = g_dEncpos[3] / 1000;//当前粉车位置：
                        double TrapSpace = /*240*/230 - PosValue;//240MM处为铺粉速度临界点
                        TrapMoveUp(4, true, Convert.ToString(/*40*//*60*/k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed),
                            Convert.ToString(TrapSpace), true, false);//(2)铺粉车移动到手动填粉位置;//30mm位置处

                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        g_dEncpos = motionMap.GetEncPos(); PosValue = g_dEncpos[3] / 1000;//当前粉车位置：
                        TrapSpace = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarStartposition - PosValue;
                        TrapMoveUp(4, true, Convert.ToString(k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed),
                            Convert.ToString(TrapSpace), true, false);//(4)铺粉车移动到正限位位置；//0mm位置处

                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速//进入下一打印环节；等待继续铺
                    }
                    else { }
#endif
                }
                else
                { MessageBox.Show("墨车不在正常停靠区间"); }
            }
        }



        /// <summary>
        /// 自动打印线程，由手动控制对话框中的“手动打印”按钮触发
        /// </summary>
        /// TODO::修改打印辐面，打印位置等
        public void AutoPrintThread()//20220513新建:自动喷墨运动动作
        {
            string msg = null;
#region 监控发送指令//20230113新建且批注：
            SendMessageToCamera sendMessageToCamera = new SendMessageToCamera(false);//20200202修改
#endregion

            double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/ double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
            for (int i = 0; i < 6; i++)//i为PASS序号；PASS宽度为喷头宽度
            {
                switch (i)
                {
                    case 0:
#region 监控指令：喷墨拍摄位点1
                        sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[0])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 1);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion

                        BackToStation(10, (float)ReturnVelocity1, true, false/*true*/, 1);//停靠在里侧，向外侧步进喷头幅面
                        BackToStation(25, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                        BackToStation(10 + PrintHeadWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点2
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[1])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 2);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion

                        break;
                    case 1:
                        BackToStation(425, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面--------------->
                        BackToStation(10 + 2 * PrintHeadWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点3
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[2])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 3);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion
                        break;
                    case 2:
                        BackToStation(25, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                        BackToStation(10 + 3 * PrintHeadWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点4
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[3])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 4);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion
                        break;
                    case 3:
                        BackToStation(425, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面--------------->
                        BackToStation(10 + 4 * PrintHeadWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点5
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[4])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 5);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion

                        break;
                    //case 4:
                    //    BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------                        
                    //                                                           //BackToStation(25 + 5 *PrintHeadWidth, (float)ReturnVelocity1,true);//停靠在里侧，向外侧步进喷头幅面

                    case 4:
                        BackToStation(25, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                        BackToStation(15 + 5 * PrintHeadWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 6);//20230113新建且批注：监控发送指令

                            msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                            Log4Net.Info(msg);
                        }
#endregion

                        break;
                    case 5://第6Pass
                        BackToStation(425, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面--------------->
                        //BackToStation(25 + 5 *PrintHeadWidth, (float)ReturnVelocity1,true);//停靠在里侧，向外侧步进喷头幅面

                        break;
                }
            }
            BackToStation(710/*735*/, (float)ReturnVelocity1, false, false, 1);//回到原点：X=50MM处<---------------//20220520新建：位置修改为780MM处，清洗工作位
            BackToStation(96 + 25/*25*/, (float)ReturnVelocity1, true, false, 1);//回到原点：Y=50MM处//20220520新建：位置修改为96MM处，清洗工作位//20220525修改：补偿25
            WaitStop(1);//20220520新建：等停墨车第1轴：X方向//外部等停
            WaitStop(2);//20220520新建：//等停墨车第2轴：Y方向//外部等停

#region 监控指令：喷墨拍摄位点7
            if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[6])
            {
                sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 7);//20230113新建且批注：监控发送指令

                msg = $"发送监控指令，拍照记录1条：SendMessageFromSharedMemory";
                Log4Net.Info(msg);
            }
#endregion

#region 监控发送指令//20230113新建且批注：
            sendMessageToCamera.Dispose(); //20230113新建且批注：监控发送指令
#endregion
        }

        /// <summary>
        /// 自动打印函数，由自动打印过程调用
        /// </summary>
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
        /// TODO::修改打印辐面等常数 
        public void AutoPrintThread2(int Command, int PassIndex, float m_MovSpeed, float m_BackCleanMovSpeed, ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex, int PauseFlag, double YJetOffWidth, int NotGoCleanStationFlag)//20220513新建:自动喷墨运动动作
        {
            string msg = $"进入：AutoPrintThread2！";
            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

            if (Command == 0) { }
            else if (Command == 1)//第2代设备的打印PASS总数为6
            {
                double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/
                double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
                ReturnVelocity1 = m_MovSpeed;
                double ReturnVelocity2 = m_BackCleanMovSpeed;//20230404新增：
                {
                    switch (PassIndex)
                    {
                        case 0:
#region 监控指令：喷墨拍摄位点1
                            //toCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[0])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 1);//20230113新建且批注：监控发送指令
                            }
#endregion
                            //20220920新增：在第1PASS打印运动之前，需要关闭闪喷：否则会导致打印乱码
                            bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷
                            /*string*/
                            msg = $"关闭闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal}}}";
                            Log4Net.Info(msg);

                            BackToStation(15 - YJetOffWidth, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false/*true*/, 1);//停靠在里侧，向外侧步进喷头幅面
                            BackToStation(425, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点2
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[1])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 2);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 1:
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 2 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点3
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[2])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 3);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 2:
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 3 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面


#region 监控指令：喷墨拍摄位点4
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[3])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 4);//20230113新建且批注：监控发送指令
                            }
#endregion
                            break;
                        case 3:
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 4 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点5
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[4])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 5);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 4:
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 5 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 5://第6Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                            //BackToStation(25 + 5 *PrintHeadWidth, (float)ReturnVelocity1,true);//停靠在里侧，向外侧步进喷头幅面

                            if (NotGoCleanStationFlag == 1) //不回清洗站
                            { }
                            else//回清洗站
                            {
                                if (PauseFlag != 1)
                                {
                                    BackToStation(780/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, false, 1);//回到原点：X=50MM处<---------------//20220520新建：位置修改为780MM处，清洗工作位
                                    BackToStation(96 + 25/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false, 1);//回到原点：Y=50MM处//20220520新建：位置修改为96MM处，清洗工作位//20220525修改：补偿25
                                    WaitStop(1);//20220520新建：等停墨车第1轴：X方向//外部等停
                                    WaitStop(2);//20220520新建：//等停墨车第2轴：Y方向//外部等停
                                }
                                else if (PauseFlag == 1)//20230410新增：
                                {
                                    BackToStation(734.275/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                    BackToStation(321.455/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                                }
#region 监控指令：喷墨拍摄位点7
                                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[6])
                                {
                                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 7);//20230113新建且批注：监控发送指令
                                }
#endregion

                                //20220920新增：在第5PASS打印运动之后，需要开启闪喷：否则会因为胶水的流动性问题，导致打印不连续
                                bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷
                                msg = $"开启闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal2}}}";
                                Log4Net.Info(msg);
                            }
                            

                            break;
                        case 6://20230410修改：附加一次运动，确保墨车运动到清洗站工作位，以提供手动操作
                            if (PauseFlag == 1)//20230410新增：
                            {
                                BackToStation(758/*734.275*//*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                BackToStation(317/*321.455*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                            }

                            break;
                    }
                }
            }
            else { }
        }
        //20230418新增：自动喷墨逻辑,采用双PASS方式进行打印
        public void AutoPrintThread3(int Command, int PassIndex, float m_MovSpeed, float m_BackCleanMovSpeed, ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex, int PauseFlag,double YJetOffWidth, int NotGoCleanStationFlag)//20220513新建:自动喷墨运动动作
        {
            string msg = $"进入：AutoPrintThread3！";
            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

            if (Command == 0) { }
            else if (Command == 1)//第2代设备的打印PASS总数为6
            {
                double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/
                double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
                ReturnVelocity1 = m_MovSpeed;
                double ReturnVelocity2 = m_BackCleanMovSpeed;//20230404新增：
                {
                    switch (PassIndex)
                    {
                        case 0://第1PASS
#region 监控指令：喷墨拍摄位点1
                            //toCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[0])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 1);//20230113新建且批注：监控发送指令
                            }
#endregion
                            //20220920新增：在第1PASS打印运动之前，需要关闭闪喷：否则会导致打印乱码
                            bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷
                            /*string*/
                            msg = $"关闭闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal}}}";
                            Log4Net.Info(msg);
#if false
                            BackToStation(15, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false/*true*/, 1);//准备Y向进给到位：停靠在里侧，向外侧步进喷头幅面
                            BackToStation(425, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);//准备移动打印到位：停靠在右侧，向左侧运动打印幅面<---------------
#else
                            BackToStation(15 + 5 * PrintHeadWidth + YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面
#endif
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 4 * PrintHeadWidth + YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点2
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[1])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 2);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 1://第2PASS
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 3 * PrintHeadWidth + YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点3
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[2])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 3);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 2://第3PASS
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 2 * PrintHeadWidth + YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面


#region 监控指令：喷墨拍摄位点4
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[3])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 4);//20230113新建且批注：监控发送指令
                            }
#endregion
                            break;
                        case 3://第4PASS
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 1 * PrintHeadWidth + YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点5
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[4])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 5);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 4://第5Pass
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 0 * PrintHeadWidth + YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 5://第6Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------

                            if (NotGoCleanStationFlag == 1) //不回清洗站
                            { }
                            else//回清洗站
                            {
                                if (PauseFlag != 1)//回原点
                                {
                                    BackToStation(780/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, false, 1);//回到原点：X=50MM处<---------------//20220520新建：位置修改为780MM处，清洗工作位
                                    BackToStation(96 + 25/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false, 1);//回到原点：Y=50MM处//20220520新建：位置修改为96MM处，清洗工作位//20220525修改：补偿25
                                    WaitStop(1);//20220520新建：等停墨车第1轴：X方向//外部等停
                                    WaitStop(2);//20220520新建：//等停墨车第2轴：Y方向//外部等停
                                }
                                else if (PauseFlag == 1)//20230410新增：
                                {
                                    BackToStation(734.275/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                    BackToStation(321.455/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                                }
#region 监控指令：喷墨拍摄位点7
                                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[6])
                                {
                                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 7);//20230113新建且批注：监控发送指令
                                }
#endregion

                                //20220920新增：在第5PASS打印运动之后，需要开启闪喷：否则会因为胶水的流动性问题，导致打印不连续
                                bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷
                                msg = $"开启闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal2}}}";
                                Log4Net.Info(msg);
                            }

                            break;
                        case 6://20230410修改：附加一次运动，确保墨车运动到清洗站工作位，以提供手动操作
                            if (PauseFlag == 1)//20230410新增：
                            {
                                BackToStation(758/*734.275*//*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                BackToStation(317/*321.455*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                            }

                            break;
                    }
                }
            }
            else { }
        }
        //20230420新增：自动喷墨逻辑,采用双PASS方式进行打印
        public void AutoPrintThread4(int Command, int PassIndex, float m_MovSpeed, float m_BackCleanMovSpeed, ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex, int PauseFlag, double YJetOffWidth, int NotGoCleanStationFlag)//20220513新建:自动喷墨运动动作
        {
            string msg = $"进入：AutoPrintThread3！";
            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

            if (Command == 0) { }
            else if (Command == 1)//第2代设备的打印PASS总数为6
            {
                double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/
                double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
                ReturnVelocity1 = m_MovSpeed;
                double ReturnVelocity2 = m_BackCleanMovSpeed;//20230404新增：
                {
                    switch (PassIndex)
                    {
                        case 0://第1PASS
#region 监控指令：喷墨拍摄位点1
                            //toCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[0])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 1);//20230113新建且批注：监控发送指令
                            }
#endregion
                            //20220920新增：在第1PASS打印运动之前，需要关闭闪喷：否则会导致打印乱码
                            bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷
                            /*string*/
                            msg = $"关闭闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal}}}";
                            Log4Net.Info(msg);

                            BackToStation(15 - YJetOffWidth, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false/*true*/, 1);//准备Y向进给到位：停靠在里侧，向外侧步进喷头幅面
                            BackToStation(425, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);//准备移动打印到位：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + YJetOffWidth + (25.4 / 1200), (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点2
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[1])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 2);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 1://第2PASS
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 1 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点3
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[2])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 3);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 2://第3PASS
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 1 * PrintHeadWidth + YJetOffWidth + (25.4 / 1200), (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面


#region 监控指令：喷墨拍摄位点4
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[3])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 4);//20230113新建且批注：监控发送指令
                            }
#endregion
                            break;
                        case 3://第4PASS
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 2 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点5
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[4])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 5);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 4://第5Pass
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 2 * PrintHeadWidth + YJetOffWidth + (25.4 / 1200), (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 5://第6Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 3 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 6://第7Pass
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 3 * PrintHeadWidth + YJetOffWidth + (25.4 / 1200), (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 7://第8Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 4 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 8://第9Pass
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 4 * PrintHeadWidth + YJetOffWidth + (25.4 / 1200), (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 9://第10Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 5 * PrintHeadWidth - YJetOffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 10://第11Pass
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 5 * PrintHeadWidth + YJetOffWidth + (25.4 / 1200), (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;

                        case 11://第12Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------

                            if (NotGoCleanStationFlag == 1) //不回清洗站
                            { }
                            else//回清洗站
                            {
                                if (PauseFlag != 1)//回原点
                                {
                                    BackToStation(780/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, false, 1);//回到原点：X=50MM处<---------------//20220520新建：位置修改为780MM处，清洗工作位
                                    BackToStation(96 + 25/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false, 1);//回到原点：Y=50MM处//20220520新建：位置修改为96MM处，清洗工作位//20220525修改：补偿25
                                    WaitStop(1);//20220520新建：等停墨车第1轴：X方向//外部等停
                                    WaitStop(2);//20220520新建：//等停墨车第2轴：Y方向//外部等停
                                }
                                else if (PauseFlag == 1)//20230410新增：
                                {
                                    BackToStation(734.275/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                    BackToStation(321.455/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                                }
#region 监控指令：喷墨拍摄位点7
                                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[6])
                                {
                                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 7);//20230113新建且批注：监控发送指令
                                }
#endregion

                                //20220920新增：在第5PASS打印运动之后，需要开启闪喷：否则会因为胶水的流动性问题，导致打印不连续
                                bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷
                                msg = $"开启闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal2}}}";
                                Log4Net.Info(msg);
                            }

                            break;
                        case 12://20230410修改：附加一次运动，确保墨车运动到清洗站工作位，以提供手动操作
                            if (PauseFlag == 1)//20230410新增：
                            {
                                BackToStation(758/*734.275*//*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                BackToStation(317/*321.455*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                            }

                            break;
                    }
                }
            }
            else { }
        }
        public void AutoPrintThread5(int Command, int PassIndex, float m_MovSpeed, float m_BackCleanMovSpeed, ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex, int PauseFlag, double YJetOffWidth, int NotGoCleanStationFlag,double YJetBaseOffWidth)//20220513新建:自动喷墨运动动作
        {
            string msg = $"进入：AutoPrintThread3！";
            Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题

            if (Command == 0) { }
            else if (Command == 1)//第2代设备的打印PASS总数为6
            {
                double PrintWidth = 147;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/
                double OffWidth = (310-147)/ 2/* + YJetBaseOffWidth*/;//20230424新增：小幅面打印时，中心偏移距离
                double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
                ReturnVelocity1 = m_MovSpeed;
                double ReturnVelocity2 = m_BackCleanMovSpeed;//20230404新增：
                {
                    switch (PassIndex)
                    {
                        case 0://第1PASS
#region 监控指令：喷墨拍摄位点1
                            //toCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[0])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 1);//20230113新建且批注：监控发送指令
                            }
#endregion
                            //20220920新增：在第1PASS打印运动之前，需要关闭闪喷：否则会导致打印乱码
                            bool nRetVal = MeteorPrintEngine.SetFlash(false);//关闭闪喷
                            /*string*/
                            msg = $"关闭闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal}}}";
                            Log4Net.Info(msg);

                            BackToStation(15 - YJetOffWidth + OffWidth, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false/*true*/, 1);//准备Y向进给到位：停靠在里侧，向外侧步进喷头幅面
                            BackToStation(425, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true, 1);//准备移动打印到位：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + YJetOffWidth + (25.4 / 1200 + OffWidth), (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点2
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[1])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 2);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 1://第2PASS
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 1 * PrintHeadWidth - YJetOffWidth + OffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点3
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[2])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 3);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 2://第3PASS
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 1 * PrintHeadWidth + YJetOffWidth + (25.4 / 1200) + OffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面


#region 监控指令：喷墨拍摄位点4
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[3])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 4);//20230113新建且批注：监控发送指令
                            }
#endregion
                            break;
                        case 3://第4PASS
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 2 * PrintHeadWidth - YJetOffWidth + OffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点5
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[4])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 5);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;
                        case 4://第5Pass
                            BackToStation(25, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 2 * PrintHeadWidth + YJetOffWidth + (25.4 / 1200) + OffWidth, (float)ReturnVelocity1, true, true, 1);//进给一次：停靠在里侧，向外侧步进喷头幅面

#region 监控指令：喷墨拍摄位点6
                            if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                            {
                                toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 6);//20230113新建且批注：监控发送指令
                            }
#endregion

                            break;

                        case 5://第12Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true, 1);//打印一次：停靠在右侧，向左侧运动打印幅面<---------------

                            if (NotGoCleanStationFlag == 1) //不回清洗站
                            { }
                            else//回清洗站
                            {
                                if (PauseFlag != 1)//回原点
                                {
                                    BackToStation(780/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, false, 1);//回到原点：X=50MM处<---------------//20220520新建：位置修改为780MM处，清洗工作位
                                    BackToStation(96 + 25/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, false, 1);//回到原点：Y=50MM处//20220520新建：位置修改为96MM处，清洗工作位//20220525修改：补偿25
                                    WaitStop(1);//20220520新建：等停墨车第1轴：X方向//外部等停
                                    WaitStop(2);//20220520新建：//等停墨车第2轴：Y方向//外部等停
                                }
                                else if (PauseFlag == 1)//20230410新增：
                                {
                                    BackToStation(734.275/*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                    BackToStation(321.455/*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                                }
#region 监控指令：喷墨拍摄位点7
                                if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[6])
                                {
                                    toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, 7);//20230113新建且批注：监控发送指令
                                }
#endregion

                                //20220920新增：在第5PASS打印运动之后，需要开启闪喷：否则会因为胶水的流动性问题，导致打印不连续
                                bool nRetVal2 = MeteorPrintEngine.SetFlash(true);//打开闪喷
                                msg = $"开启闪喷操作：IDP_FlashPrtCtl：返回值{{{nRetVal2}}}";
                                Log4Net.Info(msg);
                            }

                            break;
                        case 6://20230410修改：附加一次运动，确保墨车运动到观察站（手动清洗站）工作位，以提供手动操作
                            if (PauseFlag == 1)//20230410新增：暂停指定下，要回观察站
                            {
                                BackToStation(758/*734.275*//*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                BackToStation(317/*321.455*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                            }
                            else //20240105新增：在非暂停时在执行的时候，也执行会观察站操作
                            {
                                if (k_RYSYSParamAutoPrintParamInTest.m_nBackToRevisionStationAfterClean == 1)//是否回观察站
                                {
                                    BackToStation(758/*734.275*//*710*//*710*//*735*/, (float)ReturnVelocity2/*ReturnVelocity1*/, false, true /*false*/, 1);//到达刮墨位置
                                    BackToStation(317/*321.455*//*25*/, (float)ReturnVelocity2/*ReturnVelocity1*/, true, true, 1);//到达刮墨位置
                                }
                            }

                            break;
                    }
                }
            }
            else { }
        }

        /// <summary>阶段A收敛：UI「自动清洗」与主界面 EquipmentMotionLogic3 Command1 相同，走 AutoCleanThread2。</summary>
        private void UiThreadEntry_AutoCleanThread2()
        {
            AutoCleanThread2(k_fBackCleanMovSpeed);
        }

        /// <summary>阶段A收敛：UI「自动进给铺粉」与主界面 EquipmentMotionLogic3 Command2 相同，走 NewAutoSupplyPowderThread2*；层/工序号用 0,10 与自动打印占位一致。</summary>
        /// <remarks>阶段B路由表（唯一主路径）：Command1→AutoCleanThread2；Command2→NewAutoSupplyPowderThread2*；Command3→AutoCureThread；Command4–7→AutoPrintThread2/3/4/5。已标记 [Obsolete] 的旧方法请勿再作工艺修改。</remarks>
        private void UiThreadEntry_NewAutoSupplyPowderThread2AlignedWithAutoPrint()
        {
            if (k_RYSYSParamAutoPrintParamInTest.m_nRecoaterMode == 0)
                NewAutoSupplyPowderThread2(ref _powderMonitorStub, 0, 10);
            else
                NewAutoSupplyPowderThread2CureFirst(ref _powderMonitorStub, 0, 10, k_fBackCleanMovSpeed);
        }

        private bool CreateAndDeleteThread(string AimThreadName, List<Thread> AutoPrintThreadsPool, bool OpenCloseFlag)
        {
            if (OpenCloseFlag)//未在自动固化清洗//开启对应线程
            {
                string tempThreadName = AimThreadName;
                Thread tempThread = AutoPrintThreadsPool.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    AutoPrintThreadsPool.Remove(tempThread);//以防万一
                    return false;
                }
                else
                {
                    ThreadStart initThreadEntry = null;//20220520修改：从你先弄按照新设备UI形式，进行修改
                                                       //if (tempThreadName == "AutoCleanThread") { initThreadEntry = new ThreadStart(AutoCleanThread); }//20200220:线程入口方法修改为联动线程
                    /*else */
#pragma warning disable CS0618 // AutoSupplyPowderThread：Obsolete 旧入口，仅 #else 等分支可能仍用线程名 AutoSupplyPowderThread
                    if (tempThreadName == "AutoSupplyPowderThread") { initThreadEntry = new ThreadStart(AutoSupplyPowderThread); }//20200220:线程入口方法修改为联动线程（旧逻辑保留）
#pragma warning restore CS0618
                    else if (tempThreadName == "NewAutoSupplyPowderThread") { initThreadEntry = new ThreadStart(UiThreadEntry_NewAutoSupplyPowderThread2AlignedWithAutoPrint); }//20260416：与自动打印 Command2 同路径
                    else if (tempThreadName == "AutoPrintThread") { initThreadEntry = new ThreadStart(AutoPrintThread); }
                    else if (tempThreadName == "AutoCleanThread") { initThreadEntry = new ThreadStart(UiThreadEntry_AutoCleanThread2); }//20260416：与自动打印 Command1 同路径（AutoCleanThread2）
                    else if (tempThreadName == "AutoCureThread") { initThreadEntry = new ThreadStart(AutoCureThread); }//20220520修改：新建的AutoCureThread线程内容
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
        bool[] AutoPrintFlag = new bool[4/*3*/] { false, false, false, false };//20220521新建：添加自动固化逻辑执行标志位

        private void AutoCureBtn_Click(object sender, EventArgs e)//20220521实现：自动固化逻辑
        {
            //20220521新建：新设备自动固化逻辑
            if (AutoPrintFlag[3] == false)//自动固化运动标志位
            {
                string msg = $"手动开启自动固化过程：AutoCureBtn_Click";
                Log4Net.Info(msg);

                if (/*InkCarHomeFlag == true && */PowderCarHomeFlag == true)//确保：墨车回零标志位；确保在指定区间，否则报错//20230330修改：临时注释InkCarHomeFlag == true
                {
#if false//20220520注释：读取铺粉车位置
                    double[] g_dEncpos = new double[8]; g_dEncpos = motionMap.GetEncPos(); double PosValue = g_dEncpos[3] / 1000;//铺粉位置
                    string PositonText = "粉车: " + PosValue.ToString("F2") + " MM"; PowerPosLable.Text = PositonText;
#endif
                    if (true == CreateAndDeleteThread("AutoCureThread", AutoPrintThreads, true)) { AutoPrintFlag[3] = true; this.AutoCureBtn.Text = "停止固化运动"; }//开启
                    else { AutoPrintFlag[3] = false; this.AutoCureBtn.Text = "自动固化运动"; }
                }
                else
                {
                    msg = $"中止自动固化过程，墨车系统未回零：AutoCureBtn_Click";
                    Log4Net.Info(msg);

                    MessageBox.Show("粉车系统未回零");
                }
            }
            else//20220520新建：是否已有自动固化逻辑在运行
            {
                if (true == CreateAndDeleteThread("AutoCureThread", AutoPrintThreads, false))
                {
                    AutoPrintFlag[3] = false;
                    bool nRetVal = royal.royal.DEM_StopAxisRun(false/*false*/, 0x1);//20200623批注：修改成带减速停止//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108//其实必要性不必很高，FPGA上有硬限位停止
                    this.AutoCureBtn.Text = "自动固化运动";
                }
                else { AutoPrintFlag[3] = true; this.AutoCureBtn.Text = "停止自动固化"; }


                string msg = $"手动停止自动固化过程：AutoCureBtn_Click";
                Log4Net.Info(msg);
            }
        }
        private void AutoCleanBtn_Click(object sender, EventArgs e)//20201020新增：自动喷胶固化，便捷调试功能//20220518新建：修改为自动清洗喷头运动
        {
            //20220520新建：新设备清洗运动逻辑
            if (AutoPrintFlag[0] == false)//自动清洗运动标志位//20220520新建：是否已有自动清洗逻辑在运行————》开启自动打印逻辑
            {
                string msg = $"手动开启自动清洗过程：AutoCleanBtn_Click";
                Log4Net.Info(msg);

                if (InkCarHomeFlag == true)//确保：墨车回零标志位；确保在指定区间，否则报错
                {
#if false//20220520注释：读取铺粉车位置
                    double[] g_dEncpos = new double[8]; g_dEncpos = motionMap.GetEncPos(); double PosValue = g_dEncpos[3] / 1000;//铺粉位置
                    string PositonText = "粉车: " + PosValue.ToString("F2") + " MM"; PowerPosLable.Text = PositonText;
#endif
                    if (true == CreateAndDeleteThread("AutoCleanThread", AutoPrintThreads, true)) { AutoPrintFlag[0] = true; this.AutoCleanBtn.Text = "停止自动清洗"; }//开启
                    else { AutoPrintFlag[0] = false; this.AutoCleanBtn.Text = "自动清洗运动"; }
                }
                else
                {
                    msg = $"中止自动清洗过程，墨车系统未回零：AutoCleanBtn_Click";
                    Log4Net.Info(msg);

                    MessageBox.Show("墨车系统未回零");
                }
            }
            else//20220520新建：是否已有自动清洗逻辑在运行————》关闭自动打印逻辑
            {
                if (true == CreateAndDeleteThread("AutoCleanThread", AutoPrintThreads, false))
                {
                    AutoPrintFlag[0] = false;
                    bool nRetVal = royal.royal.DEM_StopAxisRun(false/*false*/, 0x1);//20200623批注：修改成带减速停止//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108//其实必要性不必很高，FPGA上有硬限位停止
                    this.AutoCleanBtn.Text = "自动清洗运动";
                }//关闭
                else { AutoPrintFlag[0] = true; this.AutoCleanBtn.Text = "停止自动清洗"; }

                string msg = $"手动停止自动清洗过程：AutoCleanBtn_Click";
                Log4Net.Info(msg);

            }
        }

        private void AutoSupplyPowderBtn_Click(object sender, EventArgs e)//20201020新增：自动进给铺粉，便捷调试功能
        {
#if true//20220512批注：新设备铺粉逻辑
            if (!AutoPrintFlag[1] && !EnsureRetrofitReady("自动进给铺粉"))
            {
                return;
            }
            if (AutoPrintFlag[1] == false)
            {
                string msg = $"手动开启自动进给铺粉过程：AutoSupplyPowderBtn_Click";
                Log4Net.Info(msg);

                if (PowderCarHomeFlag == true/*true*//*InkCarHomeFlag == true*/)//确保：墨车系统回零成功；确保在指定区间，否则报错//20230330修改：
                {
                    //int DirFlag = 0;
                    //double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                    //double PosValue = CurrentPos;

                    //if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                    //else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                    //else { DirFlag = 3; }

                    //string PositonText = "墨车: " + PosValue.ToString("F2") + " MM";
                    //InkCarPosLable.Text = PositonText;

                    if (true/*DirFlag == 1 || DirFlag == 2*/)
                    {
                        if (true == CreateAndDeleteThread("NewAutoSupplyPowderThread"/*"AutoSupplyPowderThread"*/, AutoPrintThreads, true)) { AutoPrintFlag[1] = true; this.AutoSupplyPowderBtn.Text = "停止进给铺粉"; }//开启
                        else { AutoPrintFlag[1] = false; this.AutoSupplyPowderBtn.Text = "自动进给铺粉"; }
                    }
                    else
                    {
                        MessageBox.Show("墨车未停靠在安全区");
                    }
                }
                else
                { MessageBox.Show("粉车系统未回零"); }
            }
            else
            {
                if (true == CreateAndDeleteThread("NewAutoSupplyPowderThread", AutoPrintThreads, false))
                {
                    AutoPrintFlag[1] = false;
                    motionMap.StopMotion(7);//20260416修改：停止铺粉车轴运动         
                    motionMap.StopMotion(8);//停止成型缸轴运动
                    motionMap.StopMotion(3);//停止落粉轴运动
                    motionMap.StopMotion(6);//停止粉辊等辅轴
                    this.AutoSupplyPowderBtn.Text = "自动进给铺粉";
                }//关闭
                else { AutoPrintFlag[1] = true; this.AutoSupplyPowderBtn.Text = "停止进给铺粉"; }

                string msg = $"手动停止自动进给铺粉过程：AutoSupplyPowderBtn_Click";
                Log4Net.Info(msg);

            }
#else//20220512批注：旧有逻辑
            if (AutoPrintFlag[1] == false)
            {
                if (InkCarHomeFlag == true)//确保：墨车系统回零成功；确保在指定区间，否则报错
                {
                    int DirFlag = 0;
                    double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                    double PosValue = CurrentPos;

                    if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                    else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                    else { DirFlag = 3; }

                    string PositonText = "墨车: " + PosValue.ToString("F2") + " MM";
                    InkCarPosLable.Text = PositonText;

                    if (DirFlag == 1 || DirFlag == 2)
                    {
                        if (true == CreateAndDeleteThread("AutoSupplyPowderThread", AutoPrintThreads, true)) { AutoPrintFlag[1] = true; this.AutoSupplyPowderBtn.Text = "停止进给铺粉"; }//开启
                        else { AutoPrintFlag[1] = false; this.AutoSupplyPowderBtn.Text = "自动进给铺粉"; }
                    }
                    else
                    {  MessageBox.Show("墨车未停靠在安全区"); }
                }
                else
                {  MessageBox.Show("墨车系统未回零"); }
            }
            else
            {
                if (true == CreateAndDeleteThread("AutoSupplyPowderThread", AutoPrintThreads, false))
                {
                    AutoPrintFlag[1] = false;
                    motionMap.StopMotion(4);//停止铺粉轴运动         
                    motionMap.StopMotion(6);//停止铺粉轴运动
                    this.AutoSupplyPowderBtn.Text = "自动进给铺粉";
                }//关闭
                else { AutoPrintFlag[1] = true; this.AutoSupplyPowderBtn.Text = "停止进给铺粉"; }
            }
#endif
        }
        private void AutoPrintBtn_Click(object sender, EventArgs e)//20201020新增：自动清洗续打，便捷调试功能
        {
            if (AutoPrintFlag[2] == false)
            {
                string msg = $"手动开启自动喷墨过程：AutoPrintBtn_Click";
                Log4Net.Info(msg);

                if (InkCarHomeFlag == true)//确保：墨车系统回零成功；确保在指定区间，否则报错
                {
#if true//20220513注释：新设备时使用此简化逻辑
                    if (true == CreateAndDeleteThread("AutoPrintThread", AutoPrintThreads, true)) { AutoPrintFlag[2] = true; this.AutoPrintBtn.Text = "停止喷墨运动"; }//开启
                    else { AutoPrintFlag[2] = false; this.AutoPrintBtn.Text = "自动喷墨运动"; }
#else//20220513注释：第一代设备时使用此逻辑
                    int DirFlag = 0;
                    double CurrentPos = GetCurrentPos(1);//初始编码器位置：
                    double PosValue = CurrentPos;
                    if ((CurrentPos <= 50) && (0 <= CurrentPos)) { DirFlag = 1; }//墨车在清洗站台右侧;
                    else if ((1180 <= CurrentPos) && (CurrentPos <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                    else { DirFlag = 3; }

                    string PositonText = "墨车: " + PosValue.ToString("F2") + " MM";
                    InkCarPosLable.Text = PositonText;

                    if (DirFlag == 1 || DirFlag == 2)
                    {
                        if (true == CreateAndDeleteThread("AutoPrintThread", AutoPrintThreads, true)) { AutoPrintFlag[2] = true; this.AutoPrintBtn.Text = "停止铺粉续打"; }//开启
                        else { AutoPrintFlag[2] = false; this.AutoPrintBtn.Text = "自动铺粉续打"; }
                    }
                    else
                    { MessageBox.Show("墨车未停靠在安全区"); }
#endif
                }
                else
                {
                    msg = $"中止自动喷墨过程，墨车系统未回零：AutoPrintBtn_Click";
                    Log4Net.Info(msg);

                    MessageBox.Show("墨车系统未回零");
                }
            }
            else
            {
                if (true == CreateAndDeleteThread("AutoPrintThread", AutoPrintThreads, false))
                {
                    AutoPrintFlag[2] = false;
#if true//20220513新建：第1代设备逻辑
                    bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                    nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x2/*第2轴*/);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
#else//第2代设备逻辑
                    //motionMap.StopMotion(4);//停止铺粉轴运动         
                    //motionMap.StopMotion(6);//停止铺粉轴运动
#endif
                    this.AutoPrintBtn.Text = "自动喷墨运动";
                }//关闭
                else { AutoPrintFlag[2] = true; this.AutoPrintBtn.Text = "停止喷墨运动"; }


                string msg = $"手动停止自动喷墨过程：AutoPrintBtn_Click";
                Log4Net.Info(msg);
            }
        }

        //20201020新建：textbox数据绑定
        public AutoPrintParamInTest k_RYSYSParamAutoPrintParamInTest/*=new AutoPrintParamInTest()*/;//20210304修改：
        public void InitKidFormWithAutoPrintParamInTest()//20201020新建：数据绑定自动固化清洗-自动进给铺粉-自动铺粉续打
        {
            //铺粉策略类型:20210124新增
            comboBox3.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "RecoaterStrategy", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox10.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "RecoaterMode", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增

            //固化策略类型：20220523新增
            comboBox1.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "CureLightStrategy", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //辊子方向校准：20220527新增：便于修正辊子方向
            comboBox2.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "RollerRotateDirection", true, DataSourceUpdateMode.OnPropertyChanged);//辊子方向校准：20220527新增：便于修正辊子方向


            // 喷头保护设置参数：(清洗和闪喷两种作用)
            textBox5.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "LayerThick", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox13.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "LayerThick2", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//201029新增:
            textBox24.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderStationCorrection", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220528新增：单位MM
            textBox25.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSupplyRotateNum", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220528新增：单位圈

            textBox28.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PreAngleForPowderSupply", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox29.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PreAngleRotateSpeedForPowderSupply", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox30.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PreAngleRotatePositionForPowderSupply", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);


            textBox6.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CureBackSpeed", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);

            textBox11.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CleanFrequency", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox10.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "RePrintTimes", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);

            textBox8.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarStartposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox9.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarStayposition", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox2.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarHomeposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220512新建：墨车HOME值设置，此值需考虑实际的光电HOME传感器的物理位置

            textBox22.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSpreaderHomeposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220526新建：粉末Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置
            textBox23.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "InkSpreaderHomeposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220526新建：墨车Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置
            textBox34.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "SraperAngleOffHome", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220526新建：墨车Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置
            textBox43.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "InkScraperDebugAngle", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20260420新增：刮墨轴独立调角目标角度

            //清洗参数：20230331新增：
            textBox20.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CleanCarSpeed", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时墨车运动速度（清洗时用）
            textBox27.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CleanAxisSpeed", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时墨车运动速度（清洗时用）
            textBox26.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PressInkTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：压墨时长（清洗时用）//20230714修改为：直接压墨
            textBox39.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PressInkTime2", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230714新增：压墨时长（清洗时用）//20230714新增：直接压墨

            textBox31.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PressInkWaitTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230412新增：压墨等待时长（清洗时用）
            textBox38.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PressInkWaitTime2", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230412新增：压墨等待时长（清洗时用）

            comboBox4.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "StartSpark", true, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时是否闪喷（清洗时用）
            comboBox5.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "StartRePrintClean", true, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时是否闪喷（清洗时用）
            comboBox7.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "AutoPrintCleanEnabled", true, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时是否闪喷（清洗时用）
            comboBox9.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "UseDirectPushInkModeEnabled", true, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时是否闪喷（清洗时用）
            comboBox6.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "PressAgainRePrintClean", true, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时是否重刮压墨（清洗时用）
            comboBox8.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "BackToRevisionStationAfterClean", true, DataSourceUpdateMode.OnPropertyChanged);//20230416新增：手动清洗时，是否回观察站
            textBox21.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CleanTimes", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗次数（清洗时用）


            textBox35.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PushingCleanInkCycleTime", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230423：清洗控制周期
            textBox36.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PushingCleanDutyCycleTime", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230423：清洗控制占空比


            CurrentLoadWave.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CurrectLoadWaveName", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230331新增：清洗时墨车运动速度（清洗时用）

            //IR固化功率：20220523新增：
            textBox19.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "IRPowerPercentage", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220512新建：墨车HOME值设置，此值需考虑实际的光电HOME传感器的物理位置


            //自适应固化增强策略：20240102新增：
            comboBox11.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "AdapativeDryMode", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            textBox41.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "AdapativeDryTimes", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox42.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "AdapativeDryWaitTimes", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox47.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "AdapativeStartDryPosition", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox48.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "AdapativeCloseDryPosition", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            //自适应固化增强策略中的清洗频率：20240102新增：固化防堵清洗频率（固化次数）
            textBox40.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "AdapativeCleanPerDryTimes", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);



            velLabel3.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox4.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox7.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarBackSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox37.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarCureSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);


            RollerParamLabel.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "RollerSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox3.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "RollerSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox12.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarBackRollerSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//201029新增:

            textBox14.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSupplyCoefficient", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//201029新增:

            ValidBox.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "DutyRatio", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            CycleBox.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CureFrequency", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            UV1LimitUp.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "LeftCureOn", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            UV1LimitDown.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "LeftCureOff", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            UV2LimitUp.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "RightCureOn", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            UV2LimitDown.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "RightCureOff", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);

            UVCureEnergycomboBox.DataBindings.Add("SelectedItem", k_RYSYSParamAutoPrintParamInTest, "CureEnergyDensity", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20210621新增：自动打印时的UV能量密度



            //闪喷时长：20210121新增
            textBox15.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CleanSparkTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
        }

        private void button41_Click(object sender, EventArgs e)
        {
            SaveJsonFile();//20201020新增：
        }
        public bool InitModbusFlag = false;//建立Modbus通讯标志位
        public Communication.ModbusCommunicateMap modbusCommunicateMap = new Communication.ModbusCommunicateMap();//实例化Modbus通讯接口对象
        bool CreateSerialPortSuccessFlag = false;
        private bool WaitModbusResponse(int times)//20220520新建：实现墨车相关的等停逻辑//单位 ms
        {
            int i = 0;//计时系数
            while ((CreateSerialPortSuccessFlag == false) && (i < times))//等待1s时间，判断是否创建成功过
            {
                Thread.Sleep(1); i++;
            }
            return CreateSerialPortSuccessFlag;/*串口创建成功*//*串口创建失败*/
        }

        public void InitTemperatureControlCard(bool OpenCloseFlag, out bool OpenCloseState)//20220523新增：仪表通讯控制
        {
            bool OutputState = motionMap.GetDo(3/*, out OutputState*/);//20220523批注：端口位置，暂时写死在此处
            if (OutputState == true)//MessageBox.Show("红外仪表打开");
            {
                if (OpenCloseFlag == true)//打开仪表通讯
                {
#if true//20220523新增：避免打开电气逻辑中，关闭IR电源后，再关闭电气逻辑后，重新启动电气逻辑中
                    if (modbusCommunicateMap.serialPort != null)
                    {
                        modbusCommunicateMap.serialPort.PortName = /*"ELTIMA Virtual Serial Port(COM2->COM3)"*/ /* "COM3"*//*"COM3"*/"COM" + "1"/*COMNumber*//*textBox18.Text*/;
                        modbusCommunicateMap.serialPort.BaudRate = (int)9600;//COM1的通讯速率为9600bps
                        modbusCommunicateMap.serialPort.DataBits = (int)8/*cbxDataBits.SelectedItem*/;
                        modbusCommunicateMap.serialPort.Parity = System.IO.Ports.Parity.Odd/*Even*/ /*GetSelectedParity()*/;//20220514修改：为奇校验
                        modbusCommunicateMap.serialPort.StopBits = System.IO.Ports.StopBits.One;

                        modbusCommunicateMap.serialPort.Close();
                        modbusCommunicateMap.serialPort.Dispose();
                        if (modbusCommunicateMap.serialPort.IsOpen == true)//存在即关闭串口
                        {
                            modbusCommunicateMap.serialPort.Close();
                            modbusCommunicateMap.serialPort.Dispose();
                        }
                    }

                    //master = null;//20220521新建：析构通讯连接，防止重新建立通讯
                    if (modbusCommunicateMap.master != null)
                    {
                        modbusCommunicateMap.master.Dispose();
                    }
                    modbusCommunicateMap.SetCommunicateExiste(false);
#endif

                    bool returnFlag = modbusCommunicateMap.CreateQuitModbusCommunication(true);//UI线程内建立通讯
                    if (returnFlag == true)//一打开Modbus线程
                    {
                        OpenCloseState = true; //打开状态标记
                        string msg = "红外灯控制器485通讯建立！";
                        Log4Net.Info(msg);
                    }
                    else//关闭Modbus线程
                    {
                        OpenCloseState = false;//打开状态标记
                        modbusCommunicateMap.master.Dispose();
                        modbusCommunicateMap.serialPort.Dispose();

                        string msg = "红外灯控制器485通讯建立失败：串口打开失败：请检查子站电源及物理连接！";
                        Log4Net.Info(msg);

                        MessageBox.Show("串口打开失败：请检查子站电源及物理连接");
                    }
                }
                else
                {
                    modbusCommunicateMap.CreateQuitModbusCommunication(false);//建立通讯
                    modbusCommunicateMap.master.Dispose();
                    modbusCommunicateMap.serialPort.Dispose();
                    OpenCloseState = false;//打开状态标记
                }
            }
            else
            {
                MessageBox.Show("红外仪表关闭状态");
#if true
                //modbusCommunicateMap.CreateQuitModbusCommunication(false);//建立通讯
                modbusCommunicateMap.serialPort.Close();
                modbusCommunicateMap.master.Dispose();
                modbusCommunicateMap.serialPort.Dispose();
#endif
                OpenCloseState = false;//打开状态标记
            }

        }
        private void ModbusBtn_Click(object sender, EventArgs e)//20220507新增：仪表通讯控制
        {
            bool OutputState = motionMap.GetDo(3/*, out OutputState*/);//20220523批注：端口位置，暂时写死在此处
            if (OutputState == true)
            {
                if (InitModbusFlag == false)
                {
                    bool returnFlag = modbusCommunicateMap.CreateQuitModbusCommunication(true);//UI线程内建立通讯
                    //ThreadStart initThreadEntry = new ThreadStart(ModbusThread);//20200220:线程入口方法修改为联动线程
                    //tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    //tempThread.Name = tempThreadName;
                    //tempThread.Start();
                    //EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程

                    //returnFlag = WaitModbusResponse(20);//等待2 ms,等待创建成功的结果//避免阻塞//精华
                    if (returnFlag == true)//一打开Modbus线程
                    {
                        string msg = $"ModubusRTU通讯成功：CreateQuitModbusCommunication";
                        Log4Net.Info(msg);

                        InitModbusFlag = true;
                        this.ModbusBtn.Text = "关闭通讯";
                    }
                    else//关闭Modbus线程
                    {
                        string msg = $"ModubusRTU通讯失败：CreateQuitModbusCommunication：{{串口打开失败：请检查子站电源及物理连接}}";
                        Log4Net.Info(msg);

                        InitModbusFlag = false;
                        //tempThreadName = "ModbusThread";//(1)关闭联调线程
                        //DeleteThread(tempThreadName);

                        modbusCommunicateMap.master.Dispose();
                        modbusCommunicateMap.serialPort.Dispose();
                        MessageBox.Show("串口打开失败：请检查子站电源及物理连接");
                    }
                }
                else
                {
                    ////关闭Modbus线程
                    //string tempThreadName = "ModbusThread";//(1)关闭联调线程
                    //DeleteThread(tempThreadName);

                    modbusCommunicateMap.CreateQuitModbusCommunication(false);//关闭通讯

                    string msg = $"关闭ModubusRTU通讯：CreateQuitModbusCommunication";
                    Log4Net.Info(msg);

                    InitModbusFlag = false;
                    this.ModbusBtn.Text = "建立通讯";
                }
            }
            else
            {
                string msg = $"ModubusRTU通讯中止：红外仪表输出：处于关闭状态！";
                Log4Net.Info(msg);

                MessageBox.Show("红外仪表关闭状态");
            }

            //#if true
            //        try//20220520新建：连接异常逻辑
            //        {
            //            if (InitModbusFlag == false)
            //            {
            //                //(A)建立Modbus通讯，并读取实时党的温度值（工程值）：20220514新建
            //                //Communication.ModbusCommunicateMap modbusCommunicateMap = new Communication.ModbusCommunicateMap();//实例化Modbus通讯接口对象

            //                //(B)创建串口参数
            //                modbusCommunicateMap.serialPort.PortName = /*"ELTIMA Virtual Serial Port(COM2->COM3)"*/ /* "COM3"*//*"COM3"*/"COM" + textBox18.Text;
            //                modbusCommunicateMap.serialPort.BaudRate = (int)9600;//COM1的通讯速率为9600bps
            //                modbusCommunicateMap.serialPort.DataBits = (int)8/*cbxDataBits.SelectedItem*/;
            //                modbusCommunicateMap.serialPort.Parity = System.IO.Ports.Parity.Odd/*Even*/ /*GetSelectedParity()*/;//20220514修改：为奇校验
            //                modbusCommunicateMap.serialPort.StopBits = System.IO.Ports.StopBits.One;

            //                //(C)创建ModubusRTU主站实例        
            //                modbusCommunicateMap.master = ModbusSerialMaster.CreateRtu(modbusCommunicateMap.serialPort);//ModbusSerialMaster master = ModbusSerialMaster.CreateRtu(modbusCommunicateMap.serialPort);

            //                //(D)打开串口
            //                if (!modbusCommunicateMap.serialPort.IsOpen)
            //                {
            //                    modbusCommunicateMap.serialPort.Open();
            //                    MessageBox.Show("打开了关闭的串口！");
            //                }
            //                InitModbusFlag = true;
            //            }
            //                ////关闭串口
            //                //modbusCommunicateMap.serialPort.Close();
            //                ////MessageBox.Show("关闭了打开的串口！");

            //#if false//20220520新建：暂时注释
            //                //读取输入寄存器值并完成显示
            //                int SlaveNumber = Convert.ToInt32(textBox17.Text)/*1*/;
            //            int RegisterAddress = Convert.ToInt32(textBox16.Text) /*0*//*30001*/;//1/*4002*/;
            //            int RegisterNumber = 1;
            //            ushort[] CurrentTemperature = modbusCommunicateMap.ReadInputRegisters((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber);//读取30001的内部计算值
            //            //ushort[] CurrentTemperature = modbusCommunicateMap.ReadHoldingRegisters/*ReadInputRegisters*/((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber/*(byte)2, (ushort)30000, (ushort)5*/);//读取30001的内部计算值
            //            //ushort[] CurrentTemperature2 = modbusCommunicateMap.ReadInputRegisters((byte)1, (ushort)32001, (ushort)1);//读取30001的工程值

            //            MessageBox.Show("当前温度的内部计算值为：" + CurrentTemperature[0].ToString());

            //            ////关闭串口
            //            //modbusCommunicateMap.serialPort.Close();
            //            ////MessageBox.Show("关闭了打开的串口！");
            //#endif
            //        }
            //        catch (Exception ex)
            //        {
            //            MessageBox.Show("连接失败：" + ex.Message);
            //            return;
            //        }
            //#elif false//第一种方法：通过底层的TCP/IP建立物理层连接；进而建立Modubs的应用层的连接协议；
            //            //20220507新疆：开启读取从机的一项输入
            //            IPAddress address = new IPAddress(new byte[]{127,0,0,1 });//IP地址
            //            TcpClient client = new TcpClient(address.ToString(), 502);//TCP/IP客户端

            //            client.SendTimeout = 1;//TCP/IP读取延时，1ms
            //            ModbusIpMaster master = ModbusIpMaster.CreateIp(client);//建立Modbus主机端
            //            ushort startAddress = 0;//起始位置：第1个
            //            ushort numInputs = 10;//读取数量：10个
            //            ushort[] inputs = master.ReadInputRegisters(2, startAddress, numInputs);//读取输入信号

            //            for (int i = 0; i < numInputs; i++)
            //            {
            //                Console.WriteLine($"Register {(startAddress + i)}={(inputs[i])}");
            //            }
            //#else//第二种方法:通过底层的TCP/IP建立物理层连接；进而建立Modubs的应用层的连接协议；
            //                try
            //            {
            //                ////(1)TCP/IP底层通讯方式:20220514新建：不采用TCP/IP协议
            //                //IPAddress address2 = new IPAddress(new byte[] { 127, 0, 0, 1 });//IP地址
            //                //TcpClient tcpClient = new TcpClient(address2.ToString(), 502);//TCP/IP客户端
            //                //tcpClient.SendTimeout = 1;//TCP/IP读取延时，1ms
            //                //tcpClient.Connect(IPAddress.Parse(this.txt_IP.Text.Trim()), int.Parse(this.txt_Port.Text.Trim()));
            //                //ModbusIpMaster ipMaster = ModbusIpMaster.CreateIp(tcpClient);//建立Modbus主机端

            //                //(2)RS485底层通讯方式
            //                SerialPort rtuClient = new SerialPort();//物理层及底层的RS485通讯端口
            //                ModbusSerialMaster modbusMaster = ModbusSerialMaster.CreateRtu(rtuClient);//建立应用层的RTU Modbus主机端

            //            }
            //            catch (Exception ex)
            //            {
            //                MessageBox.Show("连接失败：" + ex.Message);
            //                return;
            //            }
            //            MessageBox.Show("连接成功");
            //#endif
        }

        private void ManulDebugTAB_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
        bool OpenInfratelightFlag = false;
        //打开或者关闭红外灯
        private void button2_Click(object sender, EventArgs e)//打开关闭红外灯
        {
            bool OutputState = motionMap.GetDo(3/*, out OutputState*/);//20220523批注：端口位置，暂时写死在此处
            if (OutputState == true)//MessageBox.Show("红外仪表打开");
            {
#if true
                if (InitModbusFlag == true)
                {
                    //读取输入寄存器值并完成显示
                    int SlaveNumber = Convert.ToInt32(textBox17.Text)/*1*/;
                    int RegisterAddress = Convert.ToInt32(textBox16.Text) /*0*//*30001*/;//1/*4002*/;
                    int RegisterNumber = 1;
                    int RegisterValue = Convert.ToInt32(textBox19.Text);

                    //20220520新建：设置出光模式为手动出光方式:默认出光方式为自动出光控制
                    RegisterAddress = 132; RegisterValue = 1;//20220520新建批注：寄存器地址为40133;手动打印模式寄存器值为1
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//20220520批注：寄存器为40133;

                    string msg = $"IR固化手动出光模式：WriteSingleRegister：SlaveNumber{{{SlaveNumber}}}" +
                        $"RegisterAddress{{{RegisterAddress}}}RegisterValue{{{RegisterValue}}}";
                    Log4Net.Info(msg);

                    //关闭输出
                    RegisterAddress = 121/*2121*//*121*/; RegisterValue = Convert.ToInt32(0 * 100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40122----MV

                    msg = $"IR固化停止出光成功：WriteSingleRegister：SlaveNumber{{{SlaveNumber}}}" +
                        $"RegisterAddress{{{RegisterAddress}}}目标温度{{0 ℃}}";
                    Log4Net.Info(msg);
                }
#else
            if (InitModbusFlag == true)
            {
                if (OpenInfratelightFlag == false)//寄存器地址：40004//打开设置为0；打开
                {
                    //读取输入寄存器值并完成显示
                    int SlaveNumber = Convert.ToInt32(textBox17.Text)/*1*/;
                    //int RegisterAddress = 3/* Convert.ToInt32(textBox16.Text)*/ /*0*//*30001*/;//1/*4002*/;
                    int RegisterAddress = 133;//20220520批注：40134,也是开启关闭；需要验证是否为手动模式下的远程开启及关闭功能
                    int RegisterValue = 0;
                    //ushort[] CurrentTemperature = modbusCommunicateMap.ReadHoldingRegisters((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber);//读取30001的内部计算值
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);
                    OpenInfratelightFlag = true;
                    (sender as Control).Text = "关闭红外灯";
                }
                else//寄存器地址：40004
                {
                    //读取输入寄存器值并完成显示
                    int SlaveNumber = Convert.ToInt32(textBox17.Text)/*1*/;
                    //int RegisterAddress = 3/*Convert.ToInt32(textBox16.Text)*/ /*0*//*30001*/;//1/*4002*/;
                    int RegisterAddress = 133;//20220520批注：40134,也是开启关闭；需要验证是否为手动模式下的远程开启及关闭功能
                    int RegisterValue = 1;
                    //ushort[] CurrentTemperature = modbusCommunicateMap.ReadHoldingRegisters((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber);//读取30001的内部计算值
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);
                    OpenInfratelightFlag = false;
                    (sender as Control).Text = "打开红外灯";
                }
            }
#endif
            }
            else
            {
                string msg = $"开启IR固化灯中止：红外仪表输出：处于关闭状态！";
                Log4Net.Info(msg);

                MessageBox.Show("红外仪表关闭状态");
            }
        }

        //设置功率
        private void button7_Click(object sender, EventArgs e)
        {
            bool OutputState = motionMap.GetDo(3/*, out OutputState*/);//20220523批注：端口位置，暂时写死在此处
            if (OutputState == true)//MessageBox.Show("红外仪表打开");
            {
                if (InitModbusFlag == true)
                {
                    //读取输入寄存器值并完成显示
                    int SlaveNumber = Convert.ToInt32(textBox17.Text)/*1*/;
                    int RegisterAddress = Convert.ToInt32(textBox16.Text) /*0*//*30001*/;//1/*4002*/;
                    int RegisterNumber = 1;
                    int RegisterValue = Convert.ToInt32(textBox19.Text);

                    //20220520新建：设置出光模式为手动出光方式:默认出光方式为自动出光控制
                    RegisterAddress = 132; RegisterValue = 1;//20220520新建批注：寄存器地址为40133;手动打印模式寄存器值为1
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//20220520批注：寄存器为40133;

                    string msg = $"IR固化手动出光模式：WriteSingleRegister：SlaveNumber{{{SlaveNumber}}}" +
                        $"RegisterAddress{{{RegisterAddress}}}RegisterValue{{{RegisterValue}}}";
                    Log4Net.Info(msg);

                    //ushort[] CurrentTemperature = modbusCommunicateMap.ReadHoldingRegisters((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber);//读取30001的内部计算值
                    //设置功率为50%
                    RegisterAddress = 121/*2121*//*121*/; RegisterValue = Convert.ToInt32(Convert.ToDouble(textBox19.Text) * 100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40122----MV

                    msg = $"IR固化更新功率为设定值：WriteSingleRegister：SlaveNumber{{{SlaveNumber}}}" +
                              $"RegisterAddress{{{RegisterAddress}}}目标温度{{{textBox19.Text}℃}}";
                    Log4Net.Info(msg);

#if false
                    //设置温度为75℃
                    RegisterAddress = 2002; RegisterValue = Convert.ToInt32(textBox16.Text)/*1875*/; //RegisterAddress = 2; RegisterValue = 1875;
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40003----SV
#endif
                }
            }
            else
            {
                string msg = $"开启IR固化灯中止：红外仪表输出：处于关闭状态！";
                Log4Net.Info(msg);

                MessageBox.Show("红外仪表关闭状态");
            }
        }

        private void groupBox6_Enter(object sender, EventArgs e)
        {
        }

        private void groupBox26_Enter(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            bool OutputState = motionMap.GetDo(3/*, out OutputState*/);//20220523批注：端口位置，暂时写死在此处
            if (OutputState == true)
            { MessageBox.Show("红外仪表打开"); }
            else
            { MessageBox.Show("红外仪表关闭"); }
        }
        bool m_bPushInkFlag = true;//20220525新建：压墨动作
        private void PushInkBtn_Click(object sender, EventArgs e)//20220525新建：压墨功能
        {
            if (m_bPushInkFlag == false)//(b)根据ID反转背景图片
            {
                (sender as Control).Text = "压墨";
            }
            else
            {
                (sender as Control).Text = "关闭压墨";
            }
            //(c)计算输出
            if (m_bPushInkFlag == false)//打开和关闭闪喷：
            {
                //（1）开压墨泵：
                motionMap.SetDo(13, false);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                m_bPushInkFlag = true;

                string msg = $"停止手动压墨上墨：PushInkBtn_Click";
                Log4Net.Info(msg);
            }
            else
            {
                //（1）关压墨泵
                motionMap.SetDo(13, true);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                m_bPushInkFlag = false;

                string msg = $"开启手动压墨上墨：PushInkBtn_Click";
                Log4Net.Info(msg);
            }
        }

        private void SpreaderHomeBtn_Click(object sender, EventArgs e)//撒粉轴回零
        {
            string msg = $"开启撒粉轴回零校准：SpreaderHomeBtn_Click";
            Log4Net.Info(msg);

            double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition;//撒粉轴的HOME位置
            bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition);//旋转速度：0.5 圈/s
            if (ReturnCode == true)//校准成功
            {
                msg = $"撒粉轴回零成功：SetBackSpreaderAxis：{{AXIS{{3}}, Vel{{0.5圈/s}},开槽位置{{{-SinkPosition}°}}}}";
                Log4Net.Info(msg);

                MessageBox.Show("回零成功");
            }
            else
            {
                msg = $"撒粉轴回零失败：SetBackSpreaderAxis：{{AXIS{{3}}, Vel{{0.5圈/s}},开槽位置{{{-SinkPosition}°}}}}";
                Log4Net.Info(msg);

                MessageBox.Show("回零失败");
            }
        }

        private bool HomeInkScraperAxis(double homeSpeed, string operationName, bool showMessage)
        {
            string msg = $"开启刮墨轴回零校准：{operationName}";
            Log4Net.Info(msg);

            double sinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;//刮墨轴的HOME位置
            bool returnCode = motionMap.SetBackSpreaderAxis(4, homeSpeed, 2, -sinkPosition);//旋转速度：0.5 圈/s////20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
            if (returnCode == true)//校准成功
            {
                msg = $"刮墨轴回零成功：{operationName}：{{AXIS{{4}},Vel{{{homeSpeed}圈/s}},开槽位置{{{-sinkPosition}°}}}}";
                Log4Net.Info(msg);

                if (showMessage) { MessageBox.Show("回零成功"); }
            }
            else
            {
                msg = $"刮墨轴回零失败：{operationName}：{{AXIS{{4}}, Vel{{{homeSpeed}圈/s}},开槽位置{{{-sinkPosition}°}}}}";
                Log4Net.Info(msg);

                if (showMessage) { MessageBox.Show("回零失败"); }
            }

            return returnCode;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            HomeInkScraperAxis(1, "SpreaderHomeBtn_Click", true);
        }

        private void button32_Click(object sender, EventArgs e)
        {
            string msg = "开启刮墨轴调试角度动作：InkScraperDebugAngleBtn_Click";
            Log4Net.Info(msg);

            if (!HomeInkScraperAxis(1, "InkScraperDebugAngleBtn_Home", false))
            {
                MessageBox.Show("刮墨轴回零失败，无法转到调试角度");
                return;
            }

            // 与 textBox23（回零标定角）统一坐标：第二步相对机台量 = 调试角 − 回零标定角；Trap 在回零后内部清零，再发「相对转」
            double homeAngle = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;
            double debugAngle = k_RYSYSParamAutoPrintParamInTest.m_dInkScraperDebugAngle;
            double relativeDeg = debugAngle - homeAngle;
            double axisSpeed = k_RYSYSParamAutoPrintParamInTest.m_dCleanAxisSpeed;
            if (axisSpeed <= 0) { axisSpeed = 1; }

            // 若第二步转向与机台/习惯相反，将「-relativeDeg」改为「+relativeDeg」再试
            bool returnCode = motionMap.TrapMoveSpreaderAxis(4, axisSpeed, -relativeDeg);
            if (returnCode)
            {
                msg = $"刮墨轴调试角度到位：Vel{{{axisSpeed}圈/s}}，回零标定{{{homeAngle}°}}，目标角{{{debugAngle}°}}，相对转动{{{relativeDeg}°}}";
                Log4Net.Info(msg);
                MessageBox.Show($"已转到调试角度：{debugAngle}°（相对回零{homeAngle}° 的增量 {relativeDeg:F1}°）");
            }
            else
            {
                msg = $"刮墨轴调试角度失败：Vel{{{axisSpeed}圈/s}}，回零标定{{{homeAngle}°}}，目标角{{{debugAngle}°}}，相对{{{relativeDeg}°}}";
                Log4Net.Info(msg);
                MessageBox.Show("转到调试角度失败");
            }
        }

        private void SetInkCarHomeButtonState(bool enabled)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetInkCarHomeButtonState(enabled)));
                return;
            }

            InkCarHomeBtn.Enabled = enabled;
            PowderHomeBtn.Enabled = enabled;
        }

        private void RefreshInkCarHomeFlag()
        {
            InkCarHomeFlag = InkCarXHomeFlag && InkCarYHomeFlag;
        }

        private void UpdateInkCarHomeButtonText()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(UpdateInkCarHomeButtonText));
                return;
            }

            InkCarHomeBtn.Text = InkCarXHomeFlag ? "X已回零" : "X回零";
            PowderHomeBtn.Text = InkCarYHomeFlag ? "Y已回零" : "Y回零";
        }

        private void RefreshSystemCorrectState()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(RefreshSystemCorrectState));
                return;
            }

            CorrectFlag = InkCarHomeFlag && PowderCarHomeFlag;
            EncoderResetBtn.Text = CorrectFlag ? "已校准" : "未校准";
        }

        private void FinalizeInkCarGoogolHome(bool success)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<bool>(FinalizeInkCarGoogolHome), success);
                return;
            }

            PrintCarHomeBtn.Text = success ? "已校准墨车" : "校准墨车失败";
            InkCarHomeBtn.BackColor = success ? Color.Lime : Color.Tomato;
            UpdateInkCarHomeButtonText();
            RefreshSystemCorrectState();
            PrintCarEncoderResetFlag = false;
        }

        private void FinalizeCombinedCalibrationIfIdle()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(FinalizeCombinedCalibrationIfIdle));
                return;
            }

            if (!existCorrectProcessFlag && !existCorrectProcessFlag2)
            {
                EncoderResetFlag = false;
            }
        }

        private void InkCarGoogolHomeThread()
        {
            existCorrectProcessFlag = true;
            try
            {
                InkCarXHomeFlag = false;
                InkCarYHomeFlag = false;
                RefreshInkCarHomeFlag();
                UpdateInkCarHomeButtonText();

                RunInkCarAxisHome(1, true, XMaxDistanceMM - 50.0, "X");
                RunInkCarAxisHome(2, false, 0.0, "Y");

                FinalizeInkCarGoogolHome(InkCarHomeFlag);
            }
            catch (Exception ex)
            {
                Log4Net.Error($"InkCarGoogolHomeThread exception: {ex}");
                InkCarXHomeFlag = false;
                InkCarYHomeFlag = false;
                RefreshInkCarHomeFlag();
                FinalizeInkCarGoogolHome(false);
            }
            finally
            {
                existCorrectProcessFlag = false;
                FinalizeCombinedCalibrationIfIdle();
                DeleteThread("InkCarGoogolHomeThread");
            }
        }

        private bool WaitForInkCarLimitHit(short Axis, bool SearchPositiveDirection, TimeSpan timeout)
        {
            Log4Net.Info($"墨车轴{Axis}限位回零：开始轮询限位，方向={(SearchPositiveDirection ? "正" : "负")}，timeoutMs={timeout.TotalMilliseconds:F0}");
            DateTime startTime = DateTime.Now;
            DateTime lastHeartbeat = startTime;
            while ((DateTime.Now - startTime) < timeout)
            {
                int axisStatus = 0;
                motionMap.GetAxisStatus(Axis, out axisStatus);
                motionMap.ReadAxisSate(Axis);
                bool hitLimit = SearchPositiveDirection
                    ? motionMap.axisSateMonitor.FlagPosLimit1
                    : motionMap.axisSateMonitor.FlagNegLimit1;
                if (hitLimit)
                {
                    Log4Net.Info($"墨车轴{Axis}限位回零：检测到限位触发，状态=0x{axisStatus:X}");
                    return true;
                }

                    if ((DateTime.Now - lastHeartbeat) >= TimeSpan.FromSeconds(3))
                    {
                        Log4Net.Info($"墨车轴{Axis}限位回零：等待中，elapsedMs={(DateTime.Now - startTime).TotalMilliseconds:F0}, 状态=0x{axisStatus:X}, PosLimit={motionMap.axisSateMonitor.FlagPosLimit1}, NegLimit={motionMap.axisSateMonitor.FlagNegLimit1}");
                        lastHeartbeat = DateTime.Now;
                    }

                Thread.Sleep(3000);
            }

            Log4Net.Info($"墨车轴{Axis}限位回零：等待超时，方向={(SearchPositiveDirection ? "正" : "负")}，timeoutMs={timeout.TotalMilliseconds:F0}");
            return false;
        }

        private bool WaitForInkCarPosition(short Axis, double targetMm, double toleranceMm, TimeSpan timeout)
        {
            Log4Net.Info($"墨车轴{Axis}位置等待：开始轮询，targetMm={targetMm:F3}, toleranceMm={toleranceMm:F3}, timeoutMs={timeout.TotalMilliseconds:F0}");
            DateTime startTime = DateTime.Now;
            DateTime lastHeartbeat = startTime;
            double currentMm = double.NaN;
            while ((DateTime.Now - startTime) < timeout)
            {
                currentMm = GetCurrentPos(Axis);
                if (Math.Abs(currentMm - targetMm) <= toleranceMm)
                {
                    Log4Net.Info($"墨车轴{Axis}位置等待：达到目标，currentMm={currentMm:F3}, targetMm={targetMm:F3}, toleranceMm={toleranceMm:F3}");
                    return true;
                }

                    if ((DateTime.Now - lastHeartbeat) >= TimeSpan.FromSeconds(3))
                    {
                        Log4Net.Info($"墨车轴{Axis}位置等待：等待中，elapsedMs={(DateTime.Now - startTime).TotalMilliseconds:F0}, currentMm={currentMm:F3}, targetMm={targetMm:F3}, toleranceMm={toleranceMm:F3}");
                        lastHeartbeat = DateTime.Now;
                    }

                Thread.Sleep(3000);
            }

            Log4Net.Info($"墨车轴{Axis}位置等待：等待超时，currentMm={currentMm:F3}, targetMm={targetMm:F3}, toleranceMm={toleranceMm:F3}");
            return false;
        }

        private bool WaitForInkCarLimitRelease(short Axis, bool WasPositiveLimit, TimeSpan timeout)
        {
            Log4Net.Info($"墨车轴{Axis}限位释放等待：开始轮询，方向={(WasPositiveLimit ? "正限位" : "负限位")}，timeoutMs={timeout.TotalMilliseconds:F0}");
            DateTime startTime = DateTime.Now;
            DateTime lastHeartbeat = startTime;
            while ((DateTime.Now - startTime) < timeout)
            {
                int axisStatus = 0;
                motionMap.GetAxisStatus(Axis, out axisStatus);
                motionMap.ReadAxisSate(Axis);
                bool limitStillActive = WasPositiveLimit
                    ? motionMap.axisSateMonitor.FlagPosLimit1
                    : motionMap.axisSateMonitor.FlagNegLimit1;
                if (!limitStillActive)
                {
                    Log4Net.Info($"墨车轴{Axis}限位释放等待：限位已释放，状态=0x{axisStatus:X}");
                    return true;
                }

                if ((DateTime.Now - lastHeartbeat) >= TimeSpan.FromSeconds(3))
                {
                    Log4Net.Info($"墨车轴{Axis}限位释放等待：等待中，elapsedMs={(DateTime.Now - startTime).TotalMilliseconds:F0}, 状态=0x{axisStatus:X}, PosLimit={motionMap.axisSateMonitor.FlagPosLimit1}, NegLimit={motionMap.axisSateMonitor.FlagNegLimit1}");
                    lastHeartbeat = DateTime.Now;
                }

                Thread.Sleep(100);
            }

            Log4Net.Info($"墨车轴{Axis}限位释放等待：等待超时，方向={(WasPositiveLimit ? "正限位" : "负限位")}");
            return false;
        }

        private bool RunInkCarAxisLimitHome(short Axis, bool SearchPositiveDirection, double HomeMm)
        {
            try
            {
                Log4Net.Info($"墨车轴{Axis}限位回零：进入，方向={(SearchPositiveDirection ? "正" : "负")}, HomeMm={HomeMm:F3}");
                double countPerMm = GetInkCarCountPerMM(Axis);
                // 搜索行程按编码器计数/mm；TrapMotion 速度按命令脉冲/mm（与 BackToStation 固高分支一致）。
                int searchCounts = (int)(2000.0 * countPerMm);
                double homeCounts = HomeMm * countPerMm;
                double velocity = 20.0 * DriverPulsePerMM / 1000.0;

                gts.mc.TTrapPrm trapPrm = new gts.mc.TTrapPrm();
                trapPrm.acc = 0.5;
                trapPrm.dec = 0.5;
                trapPrm.velStart = 5;
                trapPrm.smoothTime = 1;

                motionMap.ClrLimitAndAbrupt(Axis);
                motionMap.StopMotion(Axis, true);

                int searchPosition = SearchPositiveDirection ? searchCounts : -searchCounts;
                Log4Net.Info($"墨车轴{Axis}限位回零：开始搜索限位，方向={(SearchPositiveDirection ? "正" : "负")}，目标脉冲={searchPosition}");
                motionMap.TrapMotion(Axis, ref trapPrm, searchPosition, velocity, 0, 0, true);
                Log4Net.Info($"墨车轴{Axis}限位回零：搜索阶段完成");

                Log4Net.Info($"墨车轴{Axis}限位回零：进入限位轮询");
                if (!WaitForInkCarLimitHit(Axis, SearchPositiveDirection, TimeSpan.FromSeconds(30)))
                {
                    Log4Net.Info($"墨车轴{Axis}限位回零：未检测到预期限位，终止");
                    motionMap.ClrLimitAndAbrupt(Axis);
                    motionMap.StopMotion(Axis, true);
                    return false;
                }

                motionMap.StopMotion(Axis, true);
                Thread.Sleep(250);
                motionMap.ClrLimitAndAbrupt(Axis);

                int homeEncPos;
                if (Axis == 1 && SearchPositiveDirection)
                {
                    const double rollbackMm = 50.0;
                    double rollbackCounts = rollbackMm * countPerMm;
                    double rollbackTargetMm = XMaxDistanceMM - rollbackMm;
                    gts.mc.TTrapPrm retreatTrapPrm = new gts.mc.TTrapPrm
                    {
                        acc = trapPrm.acc,
                        dec = trapPrm.dec,
                        velStart = trapPrm.velStart,
                        smoothTime = trapPrm.smoothTime
                    };

                    Log4Net.Info($"墨车轴{Axis}限位回零：开始执行脱离限位回退，rollbackMm={rollbackMm:F3}, targetMm={rollbackTargetMm:F3}");
                    motionMap.TrapMotion(Axis, ref retreatTrapPrm, -(int)Math.Round(rollbackCounts), velocity, 0, 0, false);
                    Thread.Sleep(500);
                    motionMap.ClrLimitAndAbrupt(Axis);
                    WaitForInkCarLimitRelease(Axis, true, TimeSpan.FromSeconds(5));

                    homeEncPos = (int)Math.Round(rollbackTargetMm * countPerMm);
                    Log4Net.Info($"墨车轴{Axis}限位回零：回退完成后设定坐标，targetMm={rollbackTargetMm:F3}, targetEnc={homeEncPos}");
                }
                else
                {
                    homeEncPos = Axis == 2 ? -(int)Math.Round(homeCounts) : (int)Math.Round(homeCounts);
                }

                double finalHomeMm = Axis == 2 ? -homeEncPos / countPerMm : homeEncPos / countPerMm;
                Log4Net.Info($"墨车轴{Axis}限位回零：触发限位后设定坐标，目标编码器={homeEncPos}，显示位置={finalHomeMm:F2}mm");
                motionMap.SetEncPos(Axis, homeEncPos);
                motionMap.ReadAxisSate(Axis);
                double homeReadBackMm = GetCurrentPos(Axis);
                bool homeMatch = Math.Abs(homeReadBackMm - HomeMm) <= 0.5;
                Log4Net.Info($"墨车轴{Axis}限位回零：初始位设置完成，当前显示位置={homeReadBackMm:F3}mm");
                LogInkCarAxisSnapshot($"墨车轴{Axis}限位回零：零位设定后轴快照");
                Log4Net.Info($"墨车轴{Axis}限位回零：坐标设定检查，目标位置={HomeMm:F3}mm，实际位置={homeReadBackMm:F3}mm，结果={(homeMatch ? "OK" : "NG")}");
                Log4Net.Info($"墨车轴{Axis}限位回零：退出，结果=OK");
                return true;
            }
            catch (Exception ex)
            {
                Log4Net.Error($"墨车轴{Axis}限位回零异常：{ex}");
                try
                {
                    motionMap.ClrLimitAndAbrupt(Axis);
                    motionMap.StopMotion(Axis, true);
                }
                catch { }
                return false;
            }
        }

        private void RunInkCarAxisHome(short Axis, bool SearchPositiveDirection, double HomeMm, string AxisName)
        {
            string msg = $"开启墨车{AxisName}轴回零";
            Log4Net.Info(msg);
            Log4Net.Info($"RunInkCarAxisHome: enter, Axis={Axis}, AxisName={AxisName}, SearchPositiveDirection={SearchPositiveDirection}, HomeMm={HomeMm:F3}");

            SetInkCarHomeButtonState(false);

            bool returnCode = RunInkCarAxisLimitHome(Axis, SearchPositiveDirection, HomeMm);
            if (Axis == 1)
            {
                InkCarXHomeFlag = returnCode;
            }
            else if (Axis == 2)
            {
                InkCarYHomeFlag = returnCode;
            }
            RefreshInkCarHomeFlag();

            UpdateInkCarHomeButtonText();
            SetInkCarHomeButtonState(true);

            double currentX = GetCurrentPos(1);
            double currentY = GetCurrentPos(2);
            Log4Net.Info($"墨车{AxisName}轴回零后坐标读回：X={currentX:F3}mm，Y={currentY:F3}mm");
            Log4Net.Info($"墨车{AxisName}轴回零后参考设定：X=810.000mm，Y=50.000mm");

            double axisReadBackMm = Axis == 1 ? currentX : currentY;
            bool axisMatch = Math.Abs(axisReadBackMm - HomeMm) <= 0.5;
            Log4Net.Info($"墨车{AxisName}轴回零完成判定：目标位置={HomeMm:F3}mm，实际位置={axisReadBackMm:F3}mm，结果={(axisMatch ? "OK" : "NG")}");
            LogInkCarAxisSnapshot($"墨车{AxisName}轴回零后轴快照");
            if (InkCarHomeFlag)
            {
                Log4Net.Info($"墨车XY轴回零完成判定：X={currentX:F3}mm，Y={currentY:F3}mm，结果=OK");
            }

            if (returnCode == true)
            {
                msg = $"墨车{AxisName}轴回零成功";
                Log4Net.Info(msg);
            }
            else
            {
                msg = $"墨车{AxisName}轴回零失败";
                Log4Net.Info(msg);
            }
            Log4Net.Info($"RunInkCarAxisHome: exit, Axis={Axis}, AxisName={AxisName}, returnCode={returnCode}");
        }

        private void button18_Click(object sender, EventArgs e)
        {
            Thread xHomeThread = new Thread(() => RunInkCarAxisHome(1, true, 860.0, "X"))
            {
                IsBackground = true
            };
            xHomeThread.Start();
        }

        private void button18_Click_1(object sender, EventArgs e)
        {
            button18_Click(sender, e);
        }

        private void button25_Click(object sender, EventArgs e)
        {
            Thread yHomeThread = new Thread(() => RunInkCarAxisHome(2, false, 0.0, "Y"))
            {
                IsBackground = true
            };
            yHomeThread.Start();
        }

        private void button25_Click_1(object sender, EventArgs e)
        {
            button25_Click(sender, e);
        }

        private void ReadRegister_Click(object sender, EventArgs e)
        {
            bool returnCode2 = royal.royal.DEV_RecHardwareInfo();
            if (returnCode2 == false)
            {
                string msg2 = $"记录硬件寄存器状态失败";
                Log4Net.Info(msg2);
            }
            else
            {
                string msg2 = $"记录硬件寄存器状态成功";
                Log4Net.Info(msg2);
            }
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void InkCarTapMoveForCorrection_Click(object sender, EventArgs e)
        {
            int myTag = Convert.ToInt32((sender as Control).Tag);
            double XStepWidth = 20;
            double YStepWidth = 20;/*距离设为50MM*/
            double MoveSpeed = m_szMovSpeed/*20*/;//喷墨移动速度
            double CurrentPos, AimPos, CurrentPos2;
            string msg = null;

            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)
            {
                case 1://墨车X方向
                    //（1）墨车Y方向点动：按照千脉冲/MM算
                    XStepWidth = Convert.ToInt32(textBox32.Text);/*距离设为20MM*/
                    if (XStepWidth >= 55)
                    {
                        XStepWidth = 55;
                    }
                    else if (XStepWidth <= -55)
                    {
                        XStepWidth = -55;
                    }

                    MoveSpeed = m_szMovSpeed/*20*/;//喷墨移动速度
                    //（1）墨车X方向点动：按照千脉冲/MM算
                    CurrentPos = GetCurrentPos(1);//初始编码器位置：
                    AimPos = CurrentPos + XStepWidth;

                    msg = $"编码器当前位置：DEM_GetAxisEncodeVal：CurrentPos{{{CurrentPos:F3}}}AimPos{{{AimPos:F3}}}";
                    Log4Net.Info(msg);
                    BackToStation(AimPos, (float)MoveSpeed, false, true, 1);//停靠在右侧，向左侧运动打印幅面<---------------
                    Thread.Sleep(2000);//等待停稳

                    //（2）计算校准系数：根据实际的光栅值进行校准系数计算
                    CurrentPos2 = GetCurrentPos(1);//初始编码器位置：
                    msg = $"编码器当前位置：DEM_GetAxisEncodeVal：CurrentPos{{{CurrentPos2:F3}}}";

                    double XcorrectionRatio = (CurrentPos2 - CurrentPos) / XStepWidth;
                    label106.Text = XcorrectionRatio.ToString("F3");//刷新显示：小数点后3位

                    break;

                case 2://墨车Y方向
                    //（1）墨车Y方向点动：按照千脉冲/MM算
                    YStepWidth = Convert.ToInt32(textBox33.Text);/*距离设为20MM*/
                    if (YStepWidth >= 55)
                    {
                        YStepWidth = 55;
                    }
                    else if (YStepWidth <= -55)
                    {
                        YStepWidth = -55;
                    }

                    MoveSpeed = m_szMovSpeed/*20*/;//喷墨移动速度
                    CurrentPos = GetCurrentPos(2);//初始编码器位置：
                    AimPos = CurrentPos + YStepWidth;
                    msg = $"编码器当前位置：DEM_GetAxisEncodeVal：CurrentPos{{{CurrentPos:F3}}}AimPos{{{AimPos:F3}}}";
                    Log4Net.Info(msg);

                    BackToStation(AimPos, (float)MoveSpeed, true, true, 1);//停靠在里侧，向外侧步进喷头幅面
                    Thread.Sleep(2000);//等待停稳

                    //（2）计算校准系数：根据实际的光栅值进行校准系数计算
                    CurrentPos2 = GetCurrentPos(2);//初始编码器位置：
                    msg = $"编码器当前位置：DEM_GetAxisEncodeVal：CurrentPos{{{CurrentPos2:F3}}}";

                    double YcorrectionRatio = (CurrentPos2 - CurrentPos) / YStepWidth;
                    label107.Text = YcorrectionRatio.ToString("F3");//刷新显示：小数点后3位

                    break;
            }
            /********************************************温度电压气压监控线程：结束*********************************************/
        }
        /****************************************墨量状态控件类****************************************/
        public class InkStateCtrl
        {
            int m_nOffset;//参数1:
            int m_nInkCnts;//参数2
            int m_nInkState;//参数3
            string m_szInfo;//参数4：显示信息，stringInfo
                            ///////存放所有墨盒INK的标签
            List<string> CtrlLabel = new List<string>();//初始化值为空
                                                        //////类初始化
            public InkStateCtrl()
            {
                m_nOffset = 0;//参数1
                m_nInkCnts = 8;//参数2
                m_nInkState = 0;//参数3
                m_szInfo = "";//参数4：显示信息，stringInfo
            }
            ///////设置墨盒INK数量
            public void SetInkCount(int nInkCnts, int nOffset)//nOffset:设置非常巧妙：
                                                              //这样int可以表示那么多的状态，且不同的区域，表示不同类型的值的状态
            {
                m_nOffset = nOffset;
                m_nInkCnts = nInkCnts;
            }
            //////设置INK墨盒状态
            public void SetInkState(int nInkState)
            {
                m_nInkState = nInkState;
            }
            //////绘制控件
            public void OnPaint(Graphics gra, Rectangle rectClient)
            {
                //Graphics g = this.CreateGraphics();
                Pen drawPen = new Pen(Color.Green, (float)0.5);
                SolidBrush b1 = new SolidBrush(Color.FromArgb(50, Color.Green));//画笔

                int nCtlValue;
                string CtrlText = null;
                //Rectangle rectClient;//父亲窗体，即是绘制所有的墨盒状态的父窗体空间
                Rectangle rectItem = new Rectangle(); ;//子窗体，父窗体中分割出的子窗体   

                int widthUnit = rectClient.Width / m_nInkCnts;//子窗体分割控件的宽度
                int heightUnit = rectClient.Height;//子窗体分割控件的高度

                //依次绘制出各个控件的状态（默认为8个）
                for (int i = m_nOffset; i < m_nInkCnts + m_nOffset; i++)
                {
                    //从掩码中提取对应的位状态
                    nCtlValue = (1 << (i - m_nOffset));

                    //设置第i位的背景画布的尺寸
                    //3目运算符运行的非常好，类似于1个if-else判断
                    rectItem.X = (i - m_nOffset) * widthUnit;
                    rectItem.Y = (i + 1 - m_nOffset < m_nInkCnts) ? ((i + 1 - m_nOffset) * widthUnit) : (rectClient.X + rectClient.Width);
                    rectItem.Width = widthUnit;
                    rectItem.Height = heightUnit;

                    //绘制第i位的背景画布
                    //dc.Draw3dRect(&rectItem, RGB(192, 192, 192), RGB(100, 100, 100));
                    gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                    gra.FillRectangle(b1, rectItem);//画子窗体背景填充色

                    //设置第i位的前景图形
                    rectItem.X += 2; rectItem.Y = 2;
                    rectItem.Width -= 4; rectItem.Height -= 6;
                    //绘制第i位的前景图形
                    //此处的运算符巧妙：3目运算符的使用
                    if ((m_nInkState & nCtlValue) == 0)//掩码校验
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        //b1 = new SolidBrush(Color.FromArgb(100, Color.Green));//画笔
                        b1 = new SolidBrush(Color.Red);//画笔
                    }
                    else
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        //b1 = new SolidBrush(Color.FromArgb(100, Color.Red));//画笔
                        b1 = new SolidBrush(Color.LimeGreen);//画笔
                    }

                    gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                    gra.FillRectangle(b1, rectItem);//画子窗体背景填充色                

                    //绘制第i位的前景文字
                    b1 = new SolidBrush(Color.Black);//画笔
                    CtrlText = (i + 1).ToString();
                    gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman", 11/*,FontStyle.Bold*/), b1, new Point(rectItem.X + rectItem.Width / 2 - 6, rectItem.Y + rectItem.Height / 2 - 6));

                    //画笔和画刷复位
                    drawPen = new Pen(Color.LimeGreen, (float)0.5);
                    b1 = new SolidBrush(Color.LimeGreen);//画笔
                                                         //this.CreateGraphics().
                }
            }
        }
        /****************************************墨量状态控件类****************************************/
        public class GohomeParam
        {
            public short AXIS;//对应的轴号
            public double vel;//JogGoHome的速度
                              //public bool FlagGoHome;//正在GoHome标志位
            public bool Ends;//终止端
        }

        public class EnvironmentParam : INotifyPropertyChanged//C#中，通知类的属性值已经更改，可以避免大量的通用事件的使用；其中关键是属性的理解及和lambda表达式的使用方法
        {
            //private string _theValue = string.Empty;////20200225：字段————测试的示例

            /// <summary>
            /// 负压阈值
            /// </summary>
            public float m_fAirHold = 0.1f/*20.0f*//*"0.2"*/;//负压阈值
            public event PropertyChangedEventHandler PropertyChanged;//20200224新增：必须定义事件；是接口INotifyPropertyChanged的必须的事件       
            private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")//20200225：【CallerMemberName】特性精华：每次调用 TraceMessage 方法时，调用方信息将替换为可选参数的参数。
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));//20200225：propertyName：是1个附带参数
            }

            /// 负压阈值
            public float AirHold//负压阈值
            {
                //（1）测试通过：20200401新增
                get { return this.m_fAirHold; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_fAirHold) { this.m_fAirHold = value; NotifyPropertyChanged(); } }

                //（2）调试专用：20200401新增，新减
                //get {
                //    return (this.m_fAirHold).ToString();
                //}
                //set {
                //    if (Convert.ToSingle(value) != this.m_fAirHold/*value != (this.m_fAirHold).ToString()*/)//（1）输入框的字符串和变量的字符串不一致，就刷新从值下去；（2）存在异常情况：输入小数点，输入小数点后数字为0,输入的字符串不是小数点和数字
                //    {
                //        this.m_fAirHold = Convert.ToSingle(value);
                //        NotifyPropertyChanged();
                //    }
                //    else
                //    {
                //    }
                //}
            }
        }

        public class AutoPrintParamInTest : INotifyPropertyChanged, ICloneable//C#中，通知类的属性值已经更改，可以避免大量的通用事件的使用；其中关键是属性的理解及和lambda表达式的使用方法
        {
            /// <summary>
            /// 返回本类的浅表复本
            /// </summary>
            /// <returns></returns>
            public object Clone()//精华
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
            /// 自动固化-自动进给铺粉-自动铺粉续打所需参数
            /// </summary>
            /// 
            public int m_nRecoaterStrategy = 0;//自动铺粉策略：默认0为直接铺粉方式
            public int m_nRecoaterMode = 0;//20230519新增：铺粉策略：默认0为同步铺粉及固化方式，1为先固化，再铺粉方式

            public int m_nCureLightStrategy = 0;//固化策略类型：默认0为UV固化方式，1为IR固化方式
            public int m_nRollerRotateDirection = 0;//辊子方向校准：20220527新增：便于修正辊子方向

            public int m_nLayerThick = 150;//层厚进给（um）//20220525新建：默认铺粉层厚为150μm
            public int m_nLayerThick2 = 300;//层厚进给2（um）

            public double m_dPowderStationCorrection = 2.5;//20220528新增：单位MM
            public double m_dPowderSupplyRotateNum = 2.5;//20220528新增：单位圈

            public double m_dPreAngleForPowderSupply = 45;//20230411新增：45°
            public double m_dPreAngleRotateSpeedForPowderSupply = 1;//20230411新增：1rev/s
            public double m_dPreAngleRotatePositionForPowderSupply = 40;//铺粉位置提前量：落粉提前量


            public double m_dCureBackSpeed = 150;//固化回程速度（mm/s）//20210621修改：
            public int m_nCleanFrequency = 25;//清洗频率（层）//20210201新建批注：优化工艺参数为25层清洗1次，较为合适
            public double m_dCleanSparkTime = 5;//清洗闪喷时长（s）//20210201新建批注：5s的清洗闪喷后处理较为合适，较为合适
            public int m_nRePrintTimes = 1;//重喷次数（次）////20210201新建批注：1次的重喷参数匹配合适的打印速度及数据精度，打印的效果，较为合适

            public /*int*/double /*m_nCureFrequency*/m_dCureFrequency = 125;//工作频率（Hz）//2021.615修改为double
            public double m_dDutyRatio = 100;//占空比（%）//20210621修改:由m_dCurePower修改为m_dDutyRatio
            public int m_nCureEnergyDensity = 1600;//20210621新增:UV灯Cure能量密度：取值为0mJ-25mJ-50mJ-100mJ-200mJ-400mJ-800mJ-1600mJ-3200mJ

            /// <summary>
            /// 自适应固化增强策略：20240102新增：增强原位固化参数的实时性，避免过高的drying功率带来的设备损坏，大幅度扩展设备的工艺探索能力
            /// </summary>
            /// 
            public int m_nAdapativeDryMode = 0;//自适应出光策略：默认0为非自适应固化方式，1为自适应出光区间和出光次数设置
            public int m_nAdapativeDryTimes = 1;//固化循环次数（次）//20240102新增：默认1次
            public double m_dAdapativeDryWaitTimes = 0;//固化等待时间//20240102新增：默认等待时间为0秒
            public int m_nAdapativeStartDryPosition = 0;//自适应出光位置（mm）//20240102新增：默认自适应出光位置 0mm
            public int m_nAdapativeCloseDryPosition = 365;//自适应闭光位置（mm）//20240102新增：默认自适应闭光位置 365mm
            public int m_nAdapativeCleanPerDryTimes = 5;//自适应固化增强策略中的清洗频率//20240102新增：默认次数为5；固化防堵清洗频率（固化次数）

            public double m_dLeftCureOn = 550;//左灯开启（mm）
            public double m_dLeftCureOff = 1000;//左灯开启（mm）
            public double m_dRightCureOn = 270;//左灯开启（mm）
            public double m_dRightCureOff = 720;//左灯开启（mm）

            public double m_dPowderSpeed = 40;//铺粉速度（mm/s）
            public double m_dRollerSpeed = 1;//滚动速度（mm/s）
            public double m_dPowderCarStartposition = 100;//铺粉起铺位置
            public double m_dPowderCarStayposition = 5;//铺粉停靠位置
            public double m_dPowderCarBackSpeed = 40;//铺粉车复位速度（mm/s）
            public double m_dPowderCarCureSpeed = 40;//铺粉车固化时速度（mm/s）//20230519新增：

    
            public double m_dPowderCarBackRollerSpeed = 1;//铺粉车复位滚动速度（REV/s）
            public double m_dPowderCarHomeposition = 112/*5*/;//20220512新建：光电HOME传感器物理位置//20220521修改：铺粉回零位修改为112MM
            public double m_dPowderSpreaderHomeposition = 40/*5*/;//20220526新建：粉末Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置，单位度（°）
            public double m_dInkSpreaderHomeposition = 90/*40*//*5*/;//20220526新建：墨车Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置，单位度（°）//20230407修改：修正后的复位值为106°
            public double m_dSraperAngleOffHome = 75;//20230417新建：墨车Spreader距离HOME的角度位置，此值需考虑实际的光电HOME传感器的物理位置，单位度（°）//20230407修改
            public double m_dInkScraperDebugAngle = 150;//20260420新增：刮墨轴独立调角的目标角度，单位度（°）

            public string m_zCurrectLoadWaveName = "null";//20230419新增：当前加载的波形名称

            public double m_dIRPowerPercentage = 20;//20220523新建：默认IR功率为20%


            public double m_dPowderSupplyCoefficient = 1;//供粉系数，供粉缸进给量相比成形缸的进给量的倍数：201030新增

            //自动铺粉策略类型:20210124新增
            public int RecoaterStrategy//
            {
                get { return this.m_nRecoaterStrategy; }
                set { if (value != this.m_nRecoaterStrategy) { this.m_nRecoaterStrategy = value; NotifyPropertyChanged(); } }
            }
            //20230519新增：铺粉策略：默认0为同步铺粉及固化方式，1为先固化，再铺粉方式
            public int RecoaterMode//
            {
                get { return this.m_nRecoaterMode; }
                set { if (value != this.m_nRecoaterMode) { this.m_nRecoaterMode = value; NotifyPropertyChanged(); } }
            }


            //固化策略类型：20220523新增
            public int CureLightStrategy
            {
                get { return this.m_nCureLightStrategy; }
                set { if (value != this.m_nCureLightStrategy) { this.m_nCureLightStrategy = value; NotifyPropertyChanged(); } }
            }
            ////辊子方向校准：20220527新增：便于修正辊子方向
            public int RollerRotateDirection
            {
                get { return this.m_nRollerRotateDirection; }
                set { if (value != this.m_nRollerRotateDirection) { this.m_nRollerRotateDirection = value; NotifyPropertyChanged(); } }
            }

            ///自动固化-自动进给铺粉-自动铺粉续打所需参数
            public int LayerThick//层厚进给（um）
            {
                get { return this.m_nLayerThick; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_nLayerThick) { this.m_nLayerThick = value; NotifyPropertyChanged(); } }
            }
            public int LayerThick2//层厚进给（um）
            {
                get { return this.m_nLayerThick2; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_nLayerThick2) { this.m_nLayerThick2 = value; NotifyPropertyChanged(); } }
            }

            /////20220528新增：单位MM
            public double PowderStationCorrection
            {
                get { return this.m_dPowderStationCorrection; }
                set { if (value != this.m_dPowderStationCorrection) { this.m_dPowderStationCorrection = value; NotifyPropertyChanged(); } }
            }
            ///20220528新增：单位圈
            public double PowderSupplyRotateNum
            {
                get { return this.m_dPowderSupplyRotateNum; }
                set
                {
                    if (value != this.m_dPowderSupplyRotateNum)//20230425新建：落粉圈数控制：默认2.5圈
                    {
                        if (value >= 3)
                        {
                            this.m_dPowderSupplyRotateNum = 5; NotifyPropertyChanged();
                        }
                        else if (value <= 0.5)
                        {
                            this.m_dPowderSupplyRotateNum = 1; NotifyPropertyChanged();
                        }
                        else
                        {
                            this.m_dPowderSupplyRotateNum = value; NotifyPropertyChanged();
                        }
                    }
                }
            }

            public double PreAngleForPowderSupply
            {
                get { return this.m_dPreAngleForPowderSupply; }
                set
                {
                    if (value != this.m_dPreAngleForPowderSupply)
                    {
                        if (value >= 180)
                        {
                            this.m_dPreAngleForPowderSupply = 180; NotifyPropertyChanged();
                        }
                        else if (value <= 0)
                        {
                            this.m_dPreAngleForPowderSupply = 0; NotifyPropertyChanged();
                        }
                        else
                        {
                            this.m_dPreAngleForPowderSupply = value; NotifyPropertyChanged();
                        }
                    }
                }
            }
            public double PreAngleRotateSpeedForPowderSupply
            {
                get { return this.m_dPreAngleRotateSpeedForPowderSupply; }
                set
                {
                    if (value != this.m_dPreAngleRotateSpeedForPowderSupply)
                    {
                        if (value >= 5)
                        {
                            this.m_dPreAngleRotateSpeedForPowderSupply = 5; NotifyPropertyChanged();
                        }
                        else if (value <= 0)
                        {
                            this.m_dPreAngleRotateSpeedForPowderSupply = 1; NotifyPropertyChanged();
                        }
                        else
                        {
                            this.m_dPreAngleRotateSpeedForPowderSupply = value; NotifyPropertyChanged();
                        }
                    }
                }
            }

            public double PreAngleRotatePositionForPowderSupply//默认铺粉提前量40mm,不得小于0mm且超过150mm
            {
                get { return this.m_dPreAngleRotatePositionForPowderSupply; }
                set
                {
                    if (value != this.m_dPreAngleRotatePositionForPowderSupply)
                    {
                        if (value >= 150)
                        {
                            this.m_dPreAngleRotatePositionForPowderSupply = 150; NotifyPropertyChanged();
                        }
                        else if (value <= 0)
                        {
                            this.m_dPreAngleRotatePositionForPowderSupply = 0; NotifyPropertyChanged();
                        }
                        else
                        {
                            this.m_dPreAngleRotatePositionForPowderSupply = value; NotifyPropertyChanged();
                        }
                    }
                }
            }

            //自适应固化增强策略：20240102新增：
            public int AdapativeDryMode//自适应出光策略：默认0为非自适应固化方式，1为自适应出光区间和出光次数设置
            {
                get { return this.m_nAdapativeDryMode; }
                set { if (value != this.m_nAdapativeDryMode) { this.m_nAdapativeDryMode = value; NotifyPropertyChanged(); } }
            }
            public int AdapativeDryTimes//固化循环次数（次）//20240102新增：默认1次
            {
                get { return this.m_nAdapativeDryTimes; }
                set
                {
                    if (value != this.m_nAdapativeDryTimes)
                    {
                        if (value >= 100) //循环次数不超过100/*20*/
                        {
                            this.m_nAdapativeDryTimes = 100/*20*/; NotifyPropertyChanged();
                        }
                        else if (value > 0)
                        {
                            this.m_nAdapativeDryTimes = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }
            public int AdapativeCleanPerDryTimes//自适应固化增强策略中的清洗频率//20240102新增：默认每固化次数为5
            {
                get { return this.m_nAdapativeCleanPerDryTimes; }
                set
                {
                    if (value != this.m_nAdapativeCleanPerDryTimes)
                    {
                        if (value >= 20) //自适应固化增强策略中的清洗频率不超过20
                        {
                            this.m_nAdapativeCleanPerDryTimes = 20; NotifyPropertyChanged();
                        }
                        else if (value > 0)
                        {
                            this.m_nAdapativeCleanPerDryTimes = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }

            public double AdapativeDryWaitTimes//固化等待时间//20240102新增：默认等待时间为0秒
            {
                get { return this.m_dAdapativeDryWaitTimes; }
                set
                {
                    if (value != this.m_dAdapativeDryWaitTimes)
                    {
                        if (value >= 60) //每次最长等待时间为60S
                        {
                            this.m_dAdapativeDryWaitTimes = 60; NotifyPropertyChanged();
                        }
                        else if (value >= 0)
                        {
                            this.m_dAdapativeDryWaitTimes = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }
            public int AdapativeStartDryPosition//自适应出光位置（mm）//20240102新增：默认自适应出光位置 365mm
            {
                get { return this.m_nAdapativeStartDryPosition; }
                set
                {
                    if (value != this.m_nAdapativeStartDryPosition)
                    {
                        if (value > 365 || value >= (m_nAdapativeCloseDryPosition-5)) //自适应出光位置不得超过365 mm
                        {
                            this.m_nAdapativeStartDryPosition = m_nAdapativeCloseDryPosition - 5; NotifyPropertyChanged();//5 mm为缓冲
                        }
                        else if (value >= 0 && value < m_nAdapativeCloseDryPosition)
                        {
                            this.m_nAdapativeStartDryPosition = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }
            public int AdapativeCloseDryPosition//自适应闭光位置（mm）//20240102新增：默认自适应闭光位置 365mm
            {
                get { return this.m_nAdapativeCloseDryPosition; }
                set
                {
                    if (value != this.m_nAdapativeCloseDryPosition)
                    {
                        if (value > 365) //自适应出光位置不超过365 mm
                        {
                            this.m_nAdapativeCloseDryPosition = 365; NotifyPropertyChanged();
                        }
                        else if (value >= 0 && value> (m_nAdapativeStartDryPosition + 5))//需要大于起始出光位置
                        {
                            this.m_nAdapativeCloseDryPosition = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }


            public double CureBackSpeed//固化回程速度（mm/s）//20210621修改：
            {
                get { return this.m_dCureBackSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set
                {
                    if (value != this.m_dCureBackSpeed && (0 <= value && value <= 250))
                    {
                        this.m_dCureBackSpeed = value; NotifyPropertyChanged();
                    }
                    else if (value > 250)
                    {
                        this.m_dCureBackSpeed = 250; NotifyPropertyChanged();
                    }
                    else if (value < 0)
                    {
                        this.m_dCureBackSpeed = 0; NotifyPropertyChanged();
                    }
                }
            }
            public int CleanFrequency//固化速度（mm/s）
            {
                get { return this.m_nCleanFrequency; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_nCleanFrequency) { this.m_nCleanFrequency = value; NotifyPropertyChanged(); } }
            }
            public double CleanSparkTime//清洗闪喷时长（s）
            {
                get { return this.m_dCleanSparkTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dCleanSparkTime) { this.m_dCleanSparkTime = value; NotifyPropertyChanged(); } }
            }

            public int RePrintTimes//重喷次数（mm/s）
            {
                get { return this.m_nRePrintTimes; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set 
                {
#if SinglePassPrintMode
                    if ((value != this.m_nRePrintTimes)&& (1 <= value && value <= 4))
                    {
                        this.m_nRePrintTimes = value; NotifyPropertyChanged();
                    }
                    else if (value > 4)
                    {
                        this.m_nRePrintTimes = 4; NotifyPropertyChanged();
                    }
                    else if (value < 1)
                    {
                        this.m_nRePrintTimes = 1; NotifyPropertyChanged();
                    }
#endif
#if TwoPassPrintMode
                    if ((value != this.m_nRePrintTimes)&& (1 <= value && value < 2))
                    {
                        this.m_nRePrintTimes = value; NotifyPropertyChanged();
                    }
                    else if (value >= 2)
                    {
                        this.m_nRePrintTimes = 1; NotifyPropertyChanged();
                    }
                    else if (value < 1)
                    {
                        this.m_nRePrintTimes = 1; NotifyPropertyChanged();
                    }
#endif
                }
            }
            public double DutyRatio//占空比（%）//20210621修改:由CurePower修改为DutyRatio
            {
                get/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                {
                    return this.m_dDutyRatio;
                }
                set
                {
                    if (value != this.m_dDutyRatio && (0 <= value && value <= 100))
                    {
                        this.m_dDutyRatio = value; NotifyPropertyChanged();
                    }
                    else if (value > 100)
                    {
                        this.m_dDutyRatio = 100; NotifyPropertyChanged();
                    }
                    else if (value < 0)
                    {
                        this.m_dDutyRatio = 0; NotifyPropertyChanged();
                    }
                }
            }
            public int CureEnergyDensity//20210621新增:UV灯Cure能量密度：取值为0mJ-25mJ-50mJ-100mJ-200mJ-400mJ-800mJ-1600mJ-3200mJ
            {
                get { return this.m_nCureEnergyDensity; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_nCureEnergyDensity) { this.m_nCureEnergyDensity = value; NotifyPropertyChanged(); } }
            }

            public /*int*/double CureFrequency//工作频率（Hz）//2021.615修改为double
            {
                get { return this.m_dCureFrequency; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dCureFrequency) { this.m_dCureFrequency = value; NotifyPropertyChanged(); } }
            }
            public double LeftCureOn//工作频率（Hz）
            {
                get { return this.m_dLeftCureOn; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dLeftCureOn) { this.m_dLeftCureOn = value; NotifyPropertyChanged(); } }
            }
            public double LeftCureOff//工作频率（Hz）
            {
                get { return this.m_dLeftCureOff; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dLeftCureOff) { this.m_dLeftCureOff = value; NotifyPropertyChanged(); } }
            }
            public double RightCureOn//工作频率（Hz）
            {
                get { return this.m_dRightCureOn; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dRightCureOn) { this.m_dRightCureOn = value; NotifyPropertyChanged(); } }
            }
            public double RightCureOff//工作频率（Hz）
            {
                get { return this.m_dRightCureOff; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dRightCureOff) { this.m_dRightCureOff = value; NotifyPropertyChanged(); } }
            }


            public double PowderSpeed//铺粉速度（mm/s）
            {
                get { return this.m_dPowderSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderSpeed) { this.m_dPowderSpeed = value; NotifyPropertyChanged(); } }
            }
            public double RollerSpeed//滚动速度（mm/s）
            {
                get { return this.m_dRollerSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dRollerSpeed) { this.m_dRollerSpeed = value; NotifyPropertyChanged(); } }
            }
            public double PowderCarStartposition//铺粉起铺位置
            {
                get { return this.m_dPowderCarStartposition; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderCarStartposition) { this.m_dPowderCarStartposition = value; NotifyPropertyChanged(); } }
            }
            public double PowderCarHomeposition//20220512新建：光电HOME传感器物理位置
            {
                get { return this.m_dPowderCarHomeposition; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderCarHomeposition) { this.m_dPowderCarHomeposition = value; NotifyPropertyChanged(); } }
            }
            public double PowderSpreaderHomeposition//20220526新建：粉末Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置
            {
                get { return this.m_dPowderSpreaderHomeposition; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderSpreaderHomeposition) { this.m_dPowderSpreaderHomeposition = value; NotifyPropertyChanged(); } }
            }
            public double InkSpreaderHomeposition//20220526新建：墨车Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置
            {
                get { return this.m_dInkSpreaderHomeposition; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dInkSpreaderHomeposition) { this.m_dInkSpreaderHomeposition = value; NotifyPropertyChanged(); } }
            }
            public double SraperAngleOffHome//20230417新建：墨车Spreader距离HOME的角度位置，此值需考虑实际的光电HOME传感器的物理位置，单位度（°）

            {
                get { return this.m_dSraperAngleOffHome; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set 
                {
                    if (value != this.m_dSraperAngleOffHome && (0 <= value && value <= 90))
                    {
                        this.m_dSraperAngleOffHome = value; NotifyPropertyChanged();
                    }
                    else if (value > 90)
                    {
                        this.m_dSraperAngleOffHome = 90; NotifyPropertyChanged();
                    }
                    else if (value < 0)
                    {
                        this.m_dSraperAngleOffHome = 0; NotifyPropertyChanged();
                    }
                }
            }
            public double InkScraperDebugAngle//20260420新增：刮墨轴独立调角功能，允许现场调试更大角度范围
            {
                get { return this.m_dInkScraperDebugAngle; }
                set
                {
                    if (value != this.m_dInkScraperDebugAngle && (0 <= value && value <= 180))
                    {
                        this.m_dInkScraperDebugAngle = value; NotifyPropertyChanged();
                    }
                    else if (value > 180)
                    {
                        this.m_dInkScraperDebugAngle = 180; NotifyPropertyChanged();
                    }
                    else if (value < 0)
                    {
                        this.m_dInkScraperDebugAngle = 0; NotifyPropertyChanged();
                    }
                }
            }

            //清洗参数：20230331新增：
            public double CleanCarSpeed//20230331新增：清洗时墨车运动速度（清洗时用）
            {
                get { return this.m_dCleanCarSpeed; }
                set
                {
                    if (value != this.m_dCleanCarSpeed)
                    {
                        if (value >= 400)//最大速度不得超过400 mm/s
                        {
                            this.m_dCleanCarSpeed = 400; NotifyPropertyChanged();
                        }
                        else if (value >= 0)
                        {
                            this.m_dCleanCarSpeed = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }
            public string CurrectLoadWaveName 
            {
                get { return this.m_zCurrectLoadWaveName; }
                set { if (value != this.m_zCurrectLoadWaveName) { this.m_zCurrectLoadWaveName = value; NotifyPropertyChanged(); } }
            }

            public double CleanAxisSpeed//20230331新增：清洗时墨车轴转动速度（清洗时用）
            {
                get { return this.m_dCleanAxisSpeed; }
                set
                {
                    if (value != this.m_dCleanAxisSpeed)
                    {
                        if (value >= 2)//最大速度不得超过2 圈/s
                        {
                            this.m_dCleanAxisSpeed = 2; NotifyPropertyChanged();
                        }
                        else if (value >= 0)
                        {
                            this.m_dCleanAxisSpeed = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }
            public double PressInkTime//20230331新增：直接压墨时长（清洗时用）
            {
                get { return this.m_dPressInkTime; }
                set
                {
                    if (value != this.m_dPressInkTime)
                    {
                        if (value >= 2) //最长压墨时间不超过2S
                        {
                            this.m_dPressInkTime = 2; NotifyPropertyChanged();
                        }
                        else if (value >= 0)
                        {
                            this.m_dPressInkTime = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }
            public double PressInkTime2//20230331新增：间接压墨时长（清洗时用）
            {
                get { return this.m_dPressInkTime2; }
                set
                {
                    if (value != this.m_dPressInkTime2)
                    {
                        if (value >= 7) //最长压墨时间不超过2S
                        {
                            this.m_dPressInkTime2 = 7; NotifyPropertyChanged();
                        }
                        else if (value >= 0)
                        {
                            this.m_dPressInkTime2 = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }

            public double PressInkWaitTime//20230412新增：压墨等待时长（清洗时用）
            {
                get { return this.m_dPressInkWaitTime; }
                set
                {
                    if (value != this.m_dPressInkWaitTime)
                    {
                        if (value >= 5) //最长压墨时间不超过3S
                        {
                            this.m_dPressInkWaitTime = 5; NotifyPropertyChanged();
                        }
                        else if (value >= 0)
                        {
                            this.m_dPressInkWaitTime = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }
            public double PressInkWaitTime2//20230412新增：压墨等待时长2（清洗时用）：补充用
            {
                get { return this.m_dPressInkWaitTime2; }
                set
                {
                    if (value != this.m_dPressInkWaitTime2)
                    {
                        if (value >= 4) //最长压墨时间不超过3S
                        {
                            this.m_dPressInkWaitTime2 = 4; NotifyPropertyChanged();
                        }
                        else if (value >= 0)
                        {
                            this.m_dPressInkWaitTime2 = value; NotifyPropertyChanged();
                        }
                        else { }
                    }
                }
            }

            public int StartSpark//20230331新增：清洗时是否闪喷（清洗时用）
            {
                get { return this.m_nStartSpark; }
                set { if (value != this.m_nStartSpark) { this.m_nStartSpark = value; NotifyPropertyChanged(); } }
            }
            public int StartRePrintClean//20230401新增：重喷时是否开启清洗（清洗时用）
            {
                get { return this.m_nStartRePrintClean; }
                set { if (value != this.m_nStartRePrintClean) { this.m_nStartRePrintClean = value; NotifyPropertyChanged(); } }
            }
            public int AutoPrintCleanEnabled//20230401新增：打印过程自动清洗使能（清洗时用）
            {
                get { return this.m_nAutoPrintCleanEnabled; }
                set { if (value != this.m_nAutoPrintCleanEnabled) { this.m_nAutoPrintCleanEnabled = value; NotifyPropertyChanged(); } }
            }
            public int UseDirectPushInkModeEnabled//20230401新增：打印过程自动清洗使能（清洗时用）
            {
                get { return this.m_nUseDirectPushInkModeEnabled; }
                set { if (value != this.m_nUseDirectPushInkModeEnabled) { this.m_nUseDirectPushInkModeEnabled = value; NotifyPropertyChanged(); } }
            }

            public int PressAgainRePrintClean//20230401新增：重喷时是否开启重刮压墨（清洗时用）
            {
                get { return this.m_nPressAgainRePrintClean; }
                set { if (value != this.m_nPressAgainRePrintClean) { this.m_nPressAgainRePrintClean = value; NotifyPropertyChanged(); } }
            }
            public int BackToRevisionStationAfterClean//20230331新增：清洗时是否重刮压墨（清洗时用）
            {
                get { return this.m_nBackToRevisionStationAfterClean; }
                set { if (value != this.m_nBackToRevisionStationAfterClean) { this.m_nBackToRevisionStationAfterClean = value; NotifyPropertyChanged(); } }
            }

            public int CleanTimes//20230331新增：清洗次数（清洗时用）
            {
                get { return this.m_nCleanTimes; }
                set
                {
                    if (value != this.m_nCleanTimes)
                    {
                        if (value >= 3) //清洗次数不超过3次
                        {
                            this.m_nCleanTimes = 3; NotifyPropertyChanged();
                        }
                        else if (value > 0)
                        {
                            this.m_nCleanTimes = value; NotifyPropertyChanged();
                        }
                        else
                        { }
                    }
                }
            }
            public double m_dCleanCarSpeed = 100;//20230331新增：清洗时墨车运动速度（清洗时用）//不超过400 MM/s
            public double m_dCleanAxisSpeed = 0.5;//20230331新增：清洗时墨车轴转动速度（清洗时用）//不超过2 圈/s
            public double m_dPressInkTime = 1.5;//20230331新增：压墨时长（清洗时用）//不超过2S//中间存在反应时间c//20230714修改：修改为直接
            public double m_dPressInkTime2 = 1.5;//20230714新增：压墨时长（清洗时用）//不超过2S//中间存在反应时间c//20230714修改：修改为间接

            public double m_dPressInkWaitTime = 1;//20230412新增：压墨等待时长（清洗时用）
            public double m_dPressInkWaitTime2 = 0.5;//20230714新增：压墨等待时长（清洗时用）
            public int m_nStartSpark = 0;//20230331新增：清洗时是否闪喷（清洗时用）//0为否，1未是
            public int m_nStartRePrintClean = 0;//20230331新增：重喷时是否开启清洗（清洗时用）//0为否，1未是
            public int m_nAutoPrintCleanEnabled = 1;//20230331新增：重喷时是否开启清洗（清洗时用）//0为否，1未是
            public int m_nUseDirectPushInkModeEnabled = 0;//20230331新增：重喷时是否开启清洗（清洗时用）//0为否，1未是

            public int m_nPressAgainRePrintClean = 0;//20230331新增：重喷时是否开启重刮压墨（清洗时用）//0为否，1未是
            public int m_nBackToRevisionStationAfterClean = 0;//20230331新增：清洗时是否重刮压墨（清洗时用）//0为否，1未是
            public int m_nCleanTimes = 1;//20230331新增：清洗次数（清洗时用）//不超过3次
            public double PushingCleanInkCycleTime//20230423：清洗控制周期
            {
                get { return this.m_dPushingCleanInkCycleTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set
                {
                    if ((value != this.m_dPushingCleanInkCycleTime) && (0.5 <= value && value <= 2))
                    {
                        this.m_dPushingCleanInkCycleTime = value; NotifyPropertyChanged();
                    }
                    else if (value > 2)
                    {
                        this.m_dPushingCleanInkCycleTime = 2; NotifyPropertyChanged();
                    }
                    else if (value < 0.5)
                    {
                        this.m_dPushingCleanInkCycleTime = 0.5; NotifyPropertyChanged();
                    }

                }
            }
            public double PushingCleanDutyCycleTime//20230423：清洗控制占空比
            {
                get { return this.m_dPushingCleanDutyCycleTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set
                {
                    if ((value != this.m_dPushingCleanDutyCycleTime) && (0 <= value && value <= 0.5))
                    {
                        this.m_dPushingCleanDutyCycleTime = value; NotifyPropertyChanged();
                    }
                    else if (value > 0.5)
                    {
                        this.m_dPushingCleanDutyCycleTime = 0.5; NotifyPropertyChanged();
                    }
                    else if (value < 0)
                    {
                        this.m_dPushingCleanDutyCycleTime = 0; NotifyPropertyChanged();
                    }
                }
            }
            public double m_dPushingCleanInkCycleTime = 1/*20*/;//20230423：清洗控制周期
            public double m_dPushingCleanDutyCycleTime = 0.1/*20*/;//20230423：清洗控制占空比

            public double IRPowerPercentage//20220523新建：默认IR功率为20%
            {
                get { return this.m_dIRPowerPercentage; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dIRPowerPercentage) { this.m_dIRPowerPercentage = value; NotifyPropertyChanged(); } }
            }

            public double PowderCarStayposition//铺粉停靠位置
            {
                get { return this.m_dPowderCarStayposition; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderCarStayposition) { this.m_dPowderCarStayposition = value; NotifyPropertyChanged(); } }
            }

            public double PowderCarBackSpeed//铺粉停靠位置
            {
                get { return this.m_dPowderCarBackSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderCarBackSpeed) { this.m_dPowderCarBackSpeed = value; NotifyPropertyChanged(); } }
            }

            public double PowderCarCureSpeed//铺粉车固化时速度
            {
                get { return this.m_dPowderCarCureSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderCarCureSpeed) { this.m_dPowderCarCureSpeed = value; NotifyPropertyChanged(); } }
            }
            public double PowderCarBackRollerSpeed//滚动速度（mm/s）
            {
                get { return this.m_dPowderCarBackRollerSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderCarBackRollerSpeed) { this.m_dPowderCarBackRollerSpeed = value; NotifyPropertyChanged(); } }
            }

            public double PowderSupplyCoefficient//滚动速度（mm/s）
            {
                get { return this.m_dPowderSupplyCoefficient; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
                set { if (value != this.m_dPowderSupplyCoefficient) { this.m_dPowderSupplyCoefficient = value; NotifyPropertyChanged(); } }
            }
        }




        bool openValueFlag = true;
        private void button9_Click(object sender, EventArgs e)
        {
            if (openValueFlag == true)//开启
            {
                //（1）开清洗阀门（==等效：关墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                OpenCloseVALVE(2 - 1, true);//20200605批注：Tag-1
                openValueFlag = false;
                (sender as Control).Text = "关阀";
            }
            else
            {

                //（1）关清洗阀门（==等效：开墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                OpenCloseVALVE(2 - 1, false);//20200605批注：Tag-1
                Thread.Sleep((int)(0.5 * 1000));//压墨等待一段时间
                string msg = $"切换完成，继续等待：0.5 S}}";
                openValueFlag = true;
                (sender as Control).Text = "开阀";
            }
        }

        private void dataGridView2_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void button12_Click(object sender, EventArgs e)
        {
            //（1）开压墨泵：
            motionMap.SetDo(13, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵

            string msg = $"开启手动压墨上墨：PushInkBtn_Click";
            Log4Net.Info(msg);

            //（2）压墨持续时间：
            Log4Net.Info(msg);
            Thread.Sleep((int)(k_RYSYSParamAutoPrintParamInTest.m_dPressInkTime2 * 1000)/*3000*/);//20220915新建：暂停2.0 s


            //（3）关闭压墨泵
            motionMap.SetDo(13, false);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵

            msg = $"关闭手动压墨上墨：PushInkBtn_Click";
            Log4Net.Info(msg);

        }

        private void button13_Click(object sender, EventArgs e)
        {
            SaveJsonFile();//20201020新增：
        }

        private void button15_Click(object sender, EventArgs e)
        {
            SaveJsonFile();//20201020新增：
        }

        private void MoveUpBtn1_Click(object sender, EventArgs e)
        {

        }

        private void HomeEndsLabel0_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void button16_Click(object sender, EventArgs e)
        {
            Log4Net.Info("button16：简易测试流程已移除，打印始终走 AutoPrintThread2 正式 Pass 调度。");
        }

        private void button16_MouseDown(object sender, MouseEventArgs e)
        {
            Log4Net.Info($"button16 MouseDown：Button={e.Button}, Location=({e.X},{e.Y})");
        }

        private void button16_MouseUp(object sender, MouseEventArgs e)
        {
            Log4Net.Info($"button16 MouseUp：Button={e.Button}, Location=({e.X},{e.Y})");
        }

        private void button17_Click(object sender, EventArgs e)
        {
            MeteorPrintEngine.HeadPowerOn = true;

            if (MeteorPrintEngine.HeadPowerOn)
            {
                Log4Net.Info("手动操作界面 button17：喷头上电成功");
            }
            else
            {
                Log4Net.Info("手动操作界面 button17：喷头上电失败");
            }
        }
        private void button30_Click(object sender, EventArgs e)
        {
            string jobBitmapPath = System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, @"JOB输出文件\0.bmp");
            Log4Net.Info($"button30_Click: 开始执行 Meteor 单向单PASS测试，bitmap={jobBitmapPath}");

            if (!System.IO.File.Exists(jobBitmapPath))
            {
                string msg = $"Meteor 单向测试失败：未找到测试图 {jobBitmapPath}";
                Log4Net.Info(msg);
                MessageBox.Show(msg);
                return;
            }

            System.Drawing.Bitmap processedBitmap = null;
            System.Drawing.Imaging.BitmapData bmpData = null;
            IntPtr imgPtr = IntPtr.Zero;

            try
            {
                processedBitmap = new System.Drawing.Bitmap(jobBitmapPath);
                if (processedBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format1bppIndexed)
                {
                    string msg = $"Meteor 单向测试失败：当前仅支持 1bpp 图像，实际像素格式={processedBitmap.PixelFormat}";
                    Log4Net.Info(msg);
                    MessageBox.Show(msg);
                    return;
                }

                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height);
                bmpData = processedBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, processedBitmap.PixelFormat);

                int bytes = Math.Abs(bmpData.Stride) * processedBitmap.Height;
                byte[] rgbValues = new byte[bytes];
                Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);

                for (int counter = 0; counter < bytes; counter++)
                    rgbValues[counter] = (byte)~rgbValues[counter];

                imgPtr = Marshal.AllocHGlobal(bytes);
                Marshal.Copy(rgbValues, 0, imgPtr, bytes);

                royal.royal.g_prtimg_layer = new royal.LPPRTIMG_LAYER();
                royal.royal.g_prtimg_layer.nReserved = new int[8];
                royal.royal.g_prtimg_layer.nLayerIndex = 1;
                royal.royal.g_prtimg_layer.nImgStartJetIndex = 0;
                royal.royal.g_prtimg_layer.nPrtDir = 1;
                royal.royal.g_prtimg_layer.nXEncOff = 1;
                royal.royal.g_prtimg_layer.nYJetOff = 0;
                royal.royal.g_prtimg_layer.nColorCnts = 1;
                royal.royal.g_prtimg_layer.nXDPI = 400;
                royal.royal.g_prtimg_layer.nYDPI = 400;
                royal.royal.g_prtimg_layer.nBytesPerLine = bmpData.Stride;
                royal.royal.g_prtimg_layer.nWidth = processedBitmap.Width;
                royal.royal.g_prtimg_layer.nHeight = processedBitmap.Height;
                royal.royal.g_prtimg_layer.nPrtFlag = 0;

                Log4Net.Info(
                    $"button30_Click: 测试参数 width={processedBitmap.Width}, height={processedBitmap.Height}, " +
                    $"stride={bmpData.Stride}, XDPI={royal.royal.g_prtimg_layer.nXDPI}, " +
                    $"YDPI={royal.royal.g_prtimg_layer.nYDPI}, nPrtDir={royal.royal.g_prtimg_layer.nPrtDir}, " +
                    $"nPrtFlag={royal.royal.g_prtimg_layer.nPrtFlag}, nXEncOff={royal.royal.g_prtimg_layer.nXEncOff}, " +
                    $"nYJetOff={royal.royal.g_prtimg_layer.nYJetOff}, nImgStartJetIndex={royal.royal.g_prtimg_layer.nImgStartJetIndex}");

                if (!MeteorPrintEngine.SendStartJob(0, (uint)processedBitmap.Width))
                {
                    Log4Net.Info("button30_Click: SendStartJob 失败。");
                    MessageBox.Show("Meteor 单向测试失败：SendStartJob 失败。");
                    return;
                }

                int nRet = MeteorPrintEngine.WriteImageLayer(ref royal.royal.g_prtimg_layer, imgPtr, bytes);
                Log4Net.Info($"button30_Click: WriteImageLayer 返回 nRet={nRet}");
                if (nRet <= 0)
                {
                    MessageBox.Show($"Meteor 单向测试失败：WriteImageLayer 返回 {nRet}");
                    return;
                }

                Log4Net.Info("button30_Click: Meteor 单向单PASS测试发送完成。");
                MessageBox.Show("Meteor 单向单PASS测试发送完成。");
            }
            catch (Exception ex)
            {
                Log4Net.Info($"button30_Click exception: {ex}");
                MessageBox.Show("Meteor 单向测试异常：" + ex.Message);
            }
            finally
            {
                try
                {
                    MeteorPrintEngine.SendEndJob();
                }
                catch (Exception ex)
                {
                    Log4Net.Info($"button30_Click: SendEndJob exception: {ex.Message}");
                }

                if (bmpData != null && processedBitmap != null)
                    processedBitmap.UnlockBits(bmpData);
                if (imgPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(imgPtr);
                if (processedBitmap != null)
                    processedBitmap.Dispose();
            }
        }

        private void button31_Click(object sender, EventArgs e)
        {
            Log4Net.Info("button31_Click: 开始执行仅导出切片图测试，不启动Meteor发送。");
            try
            {
                g_SharpControl.ExportSlicesOnlyMode = true;
                g_SharpControl.RenderToWic(true, 0, 0, 1, 0);
                string exportRoot = System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, "TEMP_METEOR_BITMAP_EXPORT");
                Log4Net.Info($"button31_Click: 切片图导出完成，目录={exportRoot}");
                MessageBox.Show("切片图导出完成。");
            }
            catch (Exception ex)
            {
                Log4Net.Info($"button31_Click exception: {ex}");
                MessageBox.Show("导出切片图异常：" + ex.Message);
            }
            finally
            {
                g_SharpControl.ExportSlicesOnlyMode = false;
            }
        }
    }
}
