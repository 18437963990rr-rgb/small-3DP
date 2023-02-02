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

namespace BinderJetting
{
    public partial class 手动操作 : Form
    {
        int m_PowerBackBtnFlag = 0;//默认状态为0；20200411批注：
        public 手动操作(int PowerBackBtnFlag, UInt32 nValveStateMask)//20200718修改：
        {
            m_PowerBackBtnFlag = PowerBackBtnFlag;//20200327新增:
            InitializeComponent();
            InitShoveInk(nValveStateMask);//初始化挤墨控件集体控制————应该移到主界面中去：

            //刷新（1）墨量余量（2）温度、电压、气压状态定时器
            Timer3 = new System.Windows.Forms.Timer() { Interval = 300 };
            Timer3.Tick += new EventHandler(Timer3_Tick);
            Timer3.Start();

            StartUpadateMAixsMoveStatus();//开启6轴轴MOVE限位信号：20200110
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

        private System.Windows.Forms.Timer Timer = null;
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
            StartCloseMoveBtn(true);//是否开启双Y轴运动监控:20200514 new created

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

            if (RollerDirectionFlag == true)
            {
                this.RollerFlag.SelectedIndex = 0;//正转
            }
            else
            {
                this.RollerFlag.SelectedIndex = 1;//反转
            }

            if (CorrectFlag/*SystemCorrectFlag*/== true) { EncoderResetBtn.Text = "已校准"; }
            else { EncoderResetBtn.Text = "未校准"; }
            if (PowderCarHomeFlag/*SystemCorrectFlag*/== true)
            { PowderHomeBtn.BackColor = Color.Lime; InkCarHomeBtn.BackColor = Color.Lime;/*PowderHomeBtn.Text = "粉车已校准";*/ }
            else { PowderHomeBtn.BackColor = Color.Tomato; InkCarHomeBtn.BackColor = Color.Tomato; /*PowderHomeBtn.Text = "铺粉未校准";*/ }

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

        public bool LoadJsonFile()//20201029新增：实例化类之后，不一定必要显示
        {
            string JsonPath = "";
            try//20201020新增：读取JSON配置文件
            {
                //(4)20200807批注：加载打印策略参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\AutoPrint-Configuration.json";//json配置文件：启动目录
                k_RYSYSParamAutoPrintParamInTest = ObjectCopier.LoadJson<AutoPrintParamInTest>(JsonPath);

                InitKidFormWithAutoPrintParamInTest();//20201020新增：开启ParamInText自动打印参数的绑定
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
                InitKidFormWithAutoPrintParamInTest();//20201020新增：开启ParamInText自动打印参数的绑定
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
                    dataTable2.Columns.Add("通道4", typeof(String));
                    dataTable2.Columns.Add("通道5", typeof(String));
                    dataTable2.Columns.Add("通道6", typeof(String));
                    //dataTable2.Columns.Add("7", typeof(String));
                    //dataTable2.Columns.Add("8", typeof(String));
                    dataTable2.Rows.Add("12.00", "12.00", "12.00", "12.00", "12.00", "12.00"/*, "0.00", "0.00"*/);//本项目实际只用到4组：20200331批注：控制器的操作电压24V，取折中输出电压为12.00V
                    dataTable2.Rows.Add("45.00", "45.00", "45.00", "45.00", "45.00", "45.00"/*, "0.00", "0.00"*/);//本项目实际只用到4组：20200331批注
                    dataTable2.Rows.Add("-3.80", "-3.80", "-3.80", "-3.80", "-3.80", "-3.80"/*, "0.00", "0.00"*/);//本项目实际只用到4组：20200331批注

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

        private System.Windows.Forms.Timer Timer2 = null;//刷新墨量显示状态定时器
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
        GoogolMotionMap motionMap = new GoogolMotionMap();//创建GoogolMotionMap对象，供本窗口调用
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
                    { DynamicConfigureMotionMode(1, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(1, false); }//(2)设置为JOG模式，执行JOGhandler挂载                 
                    break;
                case 2:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    { DynamicConfigureMotionMode(2, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(2, false); }
                    break;
                case 3:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    { DynamicConfigureMotionMode(3, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(3, false); }
                    break;
                case 4:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    { DynamicConfigureMotionMode(4, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(4, false); }
                    break;
                case 5:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    { DynamicConfigureMotionMode(5, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(5, false); }
                    break;
                case 6:
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    { DynamicConfigureMotionMode(6, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(6, false); }
                    break;

                case 7://20200903批注:轴数由6轴提升到8轴
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    { DynamicConfigureMotionMode(7, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(7, false); }
                    break;
                case 8://20200903批注:轴数由6轴提升到8轴
                    if ((sender as CheckBox).Checked == true)//(1)设置为点动模式，执行点动handler挂载
                    { DynamicConfigureMotionMode(8, true); }
                    else//(1)设置为JOG模式，执行JOGhandler挂载
                    { DynamicConfigureMotionMode(8, false); }
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
        double[] Perimeter = new double[3] { 1, 5, 1 };//20220509新增：7轴的螺距修改为5mm //20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        double[] SubDivideCoe = new double[3] { 25600, 25000, 1600 };//20200916新增：3项步进电机细分设置参数
        double[] Perimeter2 = new double[3] { 1, 1, 1 };//20210505新增3个步进轴：特地用于3-4-5此3轴-1圈的周长//20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        double[] SubDivideCoe2 = new double[3] { 1600/*1600*/, 12800/*25600*/, 1600 };//20220527修改：本轴细分12800，电流2.2A//20210505新增3个步进轴：特地用于3-4-5此3轴-具体的细分值//20200916新增//第3步进轴加有2：1的减速比
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

                if (AXIS == 3)//为步进电机：20220505新增：接粉轴电机-半圈限位
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    vel = (Convert.ToDouble(m_sVel) *2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0] / 1000);//单位：rev//20220509新建：考虑到2：1的机械减速比
                }
                else if (AXIS == 4)//为步进电机：20220505新增：刮墨轴
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[1]) * 1 * (SubDivideCoe2[1] / 1000);//单位：rev//1000pulse/1mm,当前细分
                }
                else if (AXIS == 5)//为步进电机：20220505新增：空置
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[2]) * 1 * (SubDivideCoe2[2] / 1000);//单位：rev//1000pulse/1mm,当前细分
                }


                else if (AXIS == 6 /*|| AXIS == 7 || AXIS == 8*/)//为步进电机：20200622新增:铺粉辊电机//20220511修复bug:此处应该为else if
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);
                }
                else if (AXIS == 7)//为步进电机：20200622新增：刮墨主运动
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[1]) * 1 * (SubDivideCoe[1] / 1000);//20220509批注：5mm的导程//1000pulse/1mm,当前细分
                }
                else if (AXIS == 8)//为步进电机：20200622新增：刮墨辅运动
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[2]) * 1 * (SubDivideCoe[2] / 1000);//1000pulse/1mm,当前细分
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
                if (AXIS == 3)//为步进电机：20220505新增：接粉轴电机-半圈限位
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    vel = (Convert.ToDouble(m_sVel)*2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0] / 1000);//20220509新建：单位：rev//20220509新建：考虑到2：1的机械减速比
                }
                else if (AXIS == 4)//为步进电机：20220505新增：刮墨轴
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[1]) * 1 * (SubDivideCoe2[1] / 1000);//1000pulse/1mm,当前细分
                }
                else if (AXIS == 5)//为步进电机：20220505新增：空置
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    vel = (Convert.ToDouble(m_sVel) / Perimeter2[2]) * 1 * (SubDivideCoe2[2] / 1000);//1000pulse/1mm,当前细分
                }


                else if (AXIS == 6 /*|| AXIS == 7 || AXIS == 8*/)//为步进电机：20200622新增:铺粉辊电机//20220511修复bug:此处应该为else if
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = Convert.ToDouble(m_sVel) / 125;//1000pulse/1mm,当前细分
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);
                }
                else if (AXIS == 7)//为步进电机：20200622新增：刮墨主运动//20220505修改：喷头轴Z
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    //vel = (Convert.ToDouble(m_sVel) / Perimeter[1]) * 1 * (SubDivideCoe[1] / 1000);//1000pulse/1mm,当前细分
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[1]) * 1 * (SubDivideCoe[1] / 1000);//1000pulse/1mm,当前细分
                }
                else if (AXIS == 8)//为步进电机：20200622新增：刮墨辅运动//20220505修改：闭环步进6-落粉轴
                {
                    motionMap.StopMotion(AXIS);//停止JOG运动
                    //执行JOG运动
                    motionMap.jogPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.jogPrm.dec = 0.5/*0.1*/;
                    motionMap.jogPrm.smooth = 0;
                    vel = (Convert.ToDouble(m_sVel) / Perimeter[2]) * 1 * (SubDivideCoe[2] / 1000);//1000pulse/1mm,当前细分
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
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)
            {
                case 1:
                    if (m_bMoveModeFlag[0] == false)//为JOG运动标志位
                    { JogMoveUp(1, m_bMoveModeFlag[0], m_sVel[0]); }
                    else {/*不执行任何操作*/}
                    break;
                case 2:
                    if (m_bMoveModeFlag[0] == false)
                    { JogMoveDown(1, m_bMoveModeFlag[0], m_sVel[0]); }
                    else {/*不执行任何操作*/}
                    break;
                case 3:
                    if (m_bMoveModeFlag[1] == false)
                    { JogMoveUp(2, m_bMoveModeFlag[1], m_sVel[1]); }
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
            if (AXIS == 4)//20200917新增：判断当前停止轴，是否为铺粉双驱轴：是铺粉双驱轴
            {
                AXIS = 6;
                motionMap.StopMotion(AXIS);//停止铺粉辊JOG转动
                AXIS = 4;
            }
            else//不是铺粉双驱轴
            { }

            if (m_bMoveModeFlag == false)
            {
                motionMap.StopMotion(AXIS);//停止JOG运动
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
                    { JogMoveStop(1, m_bMoveModeFlag[0]); }
                    else {/*不执行任何操作*/}
                    break;
                case 2:
                    if (m_bMoveModeFlag[0] == false)
                    { JogMoveStop(1, m_bMoveModeFlag[0]); }
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
                case 7://20200917批注：铺粉双驱运动
                    if (m_bMoveModeFlag[3] == false)
                    { JogMoveStop(4, m_bMoveModeFlag[3]); }
                    else {/*不执行任何操作*/}
                    break;
                case 8://20200917批注：铺粉双驱运动
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
                case 11://20200917批注：铺粉辊子运动
                    if (m_bMoveModeFlag[5] == false)
                    { JogMoveStop(6, m_bMoveModeFlag[5]); }
                    else {/*不执行任何操作*/}
                    break;
                case 12://20200917批注：铺粉辊子运动
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

                case 4://20200917批注：铺粉双驱轴
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

            if (AXIS == 4)
            {              
                motionMap.StopMotion(AXIS);//停止运动
                motionMap.StopMotion(6);//停止运动
            }
            else
            {                
                motionMap.StopMotion(AXIS);//停止运动
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
        private void TrapMoveUp(short AXIS, bool m_bMoveModeFlag, string m_sVel, string m_sStep,bool OtherThreadUse, bool WaitStopFlag)//20220512新建:点动运动是否等停
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
                    if (AXIS == 3)//为步进电机：20220505新增：接粉轴电机-半圈限位
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) *2.1/ Perimeter2[0]) * 1 * (SubDivideCoe2[0] / 1000);//20220509新建：考虑到第3轴步进的减速比2：1//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep)*2.1 / Perimeter2[0]) * 1 * (SubDivideCoe2[0]));//20220509新建：考虑到第3轴步进的减速比2：1//当前细分的脉冲输出数
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
                    else if (AXIS == 5)//为步进电机：20220505新增：空置
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
                    else if (AXIS == 7)//为步进电机：20200622新增：刮墨主运动
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter[1]) * 1 * (SubDivideCoe[1] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter[1]) * 1 * (SubDivideCoe[1]));//当前细分的脉冲输出数
                    }
                    else if (AXIS == 8)//为步进电机：20200622新增：刮墨辅运动
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter[2]) * 1 * (SubDivideCoe[2] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter[2]) * 1 * (SubDivideCoe[2]));//当前细分的脉冲输出数
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
        public void TrapMoveDown(short AXIS, bool m_bMoveModeFlag, string m_sVel, string m_sStep,bool OtherThreadUse, bool WaitStopFlag)//下降按键
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
                    {}
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
                    if (AXIS == 3)//为步进电机：20220505新增：接粉轴电机-半圈限位
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
                    else if (AXIS == 5)//为步进电机：20220505新增：空置
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
                    else if (AXIS == 7)//为步进电机：20200622新增：刮墨主运动
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter[1]) * 1 * (SubDivideCoe[1] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter[1]) * 1 * (SubDivideCoe[1]));//当前细分的脉冲输出数
                    }
                    else if (AXIS == 8)//为步进电机：20200622新增：刮墨辅运动
                    {
                        motionMap.trapPrm.acc = 0.5/*0.1*/;//————————————————————待实现，从其他的图形窗口中读取对应的值
                        motionMap.trapPrm.dec = 0.5/*0.1*/;
                        motionMap.trapPrm.velStart = 0;
                        motionMap.trapPrm.smoothTime = 0;

                        vel = (Convert.ToDouble(m_sVel) / Perimeter[2]) * 1 * (SubDivideCoe[2] / 1000);//当前细分对应的脉冲输出速度
                        position = (int)((Convert.ToDouble(m_sStep) / Perimeter[2]) * 1 * (SubDivideCoe[2]));//当前细分的脉冲输出数
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
            //(b2)根据ID反转颜色状态
            //(c)计算并执行动作：
            switch (myTag)
            {
                case 1:
                    if (m_bMoveModeFlag[0] == true)
                    { TrapMoveUp(1, m_bMoveModeFlag[0], m_sVel[0], m_sStep[0],false,false); }
                    else {/*不执行任何操作*/}
                    break;
                case 2:
                    if (m_bMoveModeFlag[0] == true)
                    {
                        TrapMoveDown(1, m_bMoveModeFlag[0], m_sVel[0], m_sStep[0],false, false);
                    }
                    else {/*不执行任何操作*/}
                    break;
                case 3:
                    if (m_bMoveModeFlag[1] == true)
                    { TrapMoveUp(2, m_bMoveModeFlag[1], m_sVel[1], m_sStep[1],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 4:
                    if (m_bMoveModeFlag[1] == true)
                    { TrapMoveDown(2, m_bMoveModeFlag[1], m_sVel[1], m_sStep[1],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 5:
                    if (m_bMoveModeFlag[2] == true)
                    { TrapMoveUp(3, m_bMoveModeFlag[2], m_sVel[2], m_sStep[2],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 6:
                    if (m_bMoveModeFlag[2] == true)
                    { TrapMoveDown(3, m_bMoveModeFlag[2], m_sVel[2], m_sStep[2],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 7:
                    if (m_bMoveModeFlag[3] == true)
                    { TrapMoveUp(4, m_bMoveModeFlag[3], m_sVel[3], m_sStep[3],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 8:
                    if (m_bMoveModeFlag[3] == true)
                    { TrapMoveDown(4, m_bMoveModeFlag[3], m_sVel[3], m_sStep[3],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 9:
                    if (m_bMoveModeFlag[4] == true)
                    { TrapMoveUp(5, m_bMoveModeFlag[4], m_sVel[4], m_sStep[4],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 10:
                    if (m_bMoveModeFlag[4] == true)
                    { TrapMoveDown(5, m_bMoveModeFlag[4], m_sVel[4], m_sStep[4],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 11:
                    if (m_bMoveModeFlag[5] == true)
                    { TrapMoveUp(6, m_bMoveModeFlag[5], m_sVel[5], m_sStep[5],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 12:
                    if (m_bMoveModeFlag[5] == true)
                    { TrapMoveDown(6, m_bMoveModeFlag[5], m_sVel[5], m_sStep[5],false, false); }
                    else {/*不执行任何操作*/}
                    break;

                case 13://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == true)
                    { TrapMoveUp(7, m_bMoveModeFlag[6], m_sVel[6], m_sStep[6],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 14://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[6] == true)
                    { TrapMoveDown(7, m_bMoveModeFlag[6], m_sVel[6], m_sStep[6],false, false); }
                    else {/*不执行任何操作*/}
                    break;

                case 15://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == true)
                    { TrapMoveUp(8, m_bMoveModeFlag[7], m_sVel[7], m_sStep[7],false, false); }
                    else {/*不执行任何操作*/}
                    break;
                case 16://20200903批注:轴数由6轴提升到8轴
                    if (m_bMoveModeFlag[7] == true)
                    { TrapMoveDown(8, m_bMoveModeFlag[7], m_sVel[7], m_sStep[7],false, false); }
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
        private System.Windows.Forms.Timer TimerMAxis = null;//刷新虚拟打印编码器状态显示定时器
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
            string PositonText = "粉车: " + PosValue.ToString("F1") + " MM";
            PowerPosLable.Text = PositonText;//202001021新增位置监测：
            UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
            PosValue = CurrentPos * 0.005;
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
            int nRetVal = royal.royal.DEV_OpenDevice(ParenthWnd,
                System.Windows.Forms.Application.StartupPath + @"\"
                /*@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\Debug\"*/);
            if (nRetVal > 0)
            {
                string sztxt;
                sztxt = string.Format("失败：{0:X00000000}", nRetVal);
                MessageBox.Show(sztxt);
            }
        }

        private void DEV_UpdateParam_Click(object sender, EventArgs e)
        {
            royal.royal.g_sys_param.nParamVer = 0x21080809;
            ////这个地方比较关键：————Marshal.sizeof，处理的必须是非托管区的数据：(1)是否必要；（2）这一部分，需从底层更新上来——没啥用
            //royal.royal.sys_param.nParamSize = Marshal.sizeof(LPRYSYS_PARAM);
            royal.royal.g_sys_param.szWavePath =
                System.Windows.Forms.Application.StartupPath +
                @"\波形文件\ricoh_phcfg16.rhdat"/*"金属3DP打印ccccccd"*/;//仅为测试
            bool returnST = royal.royal.DEV_UpdateParam(ref royal.royal.g_sys_param);
            if (returnST == false)
            {
                string sztxt;
                sztxt = "更新设备参数失败";
                MessageBox.Show(sztxt);
            }
        }

        private void DEV_InitDevice_Click(object sender, EventArgs e)
        {
            UInt32 nSysInitEncVal = 0x100000;//此值来自运动系统当前X编码——非常关键：安全性至关重要
            //距离Smm转换为Encode,DPI为扫描方向光栅DPI
            //Encode=((S/25.4)*DPI)

            UInt32 nRetVal = royal.royal.DEV_InitDevice(nSysInitEncVal);
            if (nRetVal < 0)
            {
                string sztxt;
                sztxt = String.Format("失败：{0:X00000000}", nRetVal);
                MessageBox.Show(sztxt);
            }
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
            //该函数用来加载参数结构RYSYS_PARAM中的drvWaveForm[MAX_DRV_CNT]的返回值
            //0 夹杂及成功
            //1 车头卡1 负载驱动卡存在波形加载失败
            //2 车头卡1 负载驱动卡存在波形加载失败
            int nRetVal = royal.royal.DEV_ReloadWaveForm();//当前X轴信号状态
            if (nRetVal > 0) { MessageBox.Show("波形加载失败！"); }
            royal.LPPRINTER_INFO pSysInfo = new royal.LPPRINTER_INFO();
            bool nRetVal2 = royal.royal.DEV_GetDeviceInfo2(ref pSysInfo);
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
            royal.royal.g_PrtJobItem.nJobID = 0;
            royal.royal.g_PrtJobItem.nPixelGrayBits = 1;
            royal.royal.g_PrtJobItem.nPrtXEncPos = 0x100018;
            royal.royal.g_PrtJobItem.szJobName = "金属3DP打印";
            if (royal.royal.IDP_SartPrintJob(ref royal.royal.g_PrtJobItem) < 0)
            {
                MessageBox.Show("Cant Print");
            }
            else
            {
                //AfxBeginThread(A);//上位机开启图像处理线程
                //AfxBeginThread(b);//上位机开启打印管理线程
            }
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
            int nRet = -1;//默认的数据为-1；
            do
            {
                nRet = royal.royal.IDP_WriteImgLayerData(ref royal.royal.g_prtimg_layer, ptr, bytes);
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
            bool nRetVal = royal.royal.IDP_GetPrintState(ref RTinfo);
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
                bool nRetVal = royal.royal.IDP_DoPassPrint2(nLayerIndex, i);
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
            UInt32 nPrtcess = nPrtPassDes.nPrtPrecession;//光栅打印分频值
            UInt32 nXEndPos = 0;//X打印结束编码值
            if (nPrtPassDes.bPrtDir == true)
            {
                nXEndPos = nXStartPos + nPrtcess * nXValidCol;//计算PASS打印的中止编码值
            }
            else
            {
                nXEndPos = nXStartPos + nPrtcess * nXValidCol;//计算PASS打印的中止编码值
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
            bool nRetVal = royal.royal.IDP_StopPrintJob();
        }

        private void FlashPrtCtl_Click(object sender, EventArgs e)
        {
            bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);
        }

        /*******************************挤墨控件集体控制初始化***************************/
        royal.RoyalPrintingMap RoyalMap = new royal.RoyalPrintingMap();//创建GoogolMotionMap对象，供本窗口调用
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
        private System.Windows.Forms.Timer Timer3 = null;//刷新墨量显示状态定时器
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
        float nSpeed = 5;//20200305新增；
        private void XMoveBtn_MouseDown(object sender, MouseEventArgs e)//（1）运动按钮按下时响应动作
        {
            RoyalMap.m_bYSyncCtl = this.checkBox6.Checked;//checkBox1选中，则为true;不选中则为false

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

            //nSpeed = MM_TO_DOT(m_szMovSpeed, 5080,0);//20220506调整：必须调整成正确的运转速度//YINC_PERDOT扩展到48——20200108

            //(c)计算并执行动作：
            switch (myTag)
            {
                case 3://20220506批注：X轴主运动-前进
#if false
                    if (RoyalMap.m_bYSyncCtl)
                    {
                        nCtlValue = 2;//_MC_CTL_SYNC_MASK扩展到2
                    }
#else
                    nCtlValue = 2;//20220511批注：第1位是否不等停；第2位是否双Y同步//_MC_CTL_SYNC_MASK扩展到2
#endif
                    nSpeed = MM_TO_DOT(m_szMovSpeed, 5080, 0);//20220506调整：必须调整成正确的运转速度
                    nRetVal = royal.royal.DEM_Run(0, true, (uint)nSpeed, 750000, nCtlValue);//20220506批注：第1轴运动
                    if (RoyalMap.m_bYSyncCtl == true)
                    { this.YStateLabel.Text = "X1轴正向运动"; }
                    else
                    { this.YStateLabel.Text = "X轴同步正向运动"; }
                    //SetDlgItemText(IDC_STA_YMOV, ((wparam & 0x1) ? (m_bYSyncCtl ? _T("Y轴同步负向运动") : _T("Y1轴负向运动")) : (m_bYSyncCtl ? _T("Y轴同步正向运动") : _T("Y1轴正向运动"))));
                    RoyalMap.m_bYmove1 = true;//Y1轴正在运动标志位
                    break;
                case 4://20220506批注：X轴主运动-后退
#if false
                    if (RoyalMap.m_bYSyncCtl)
                    {
                        nCtlValue = 2;//_MC_CTL_SYNC_MASK扩展到2
                    }
#else
                    nCtlValue = 2; //20220511批注：第1位是否不等停；第2位是否双Y同步//_MC_CTL_SYNC_MASK扩展到2
#endif
                    nSpeed = MM_TO_DOT(m_szMovSpeed, 5080, 0);//20220506调整：必须调整成正确的运转速度
                    nRetVal = royal.royal.DEM_Run(0, false, (uint)nSpeed, 750000, nCtlValue);//20220506批注：第1轴运动
                    if (RoyalMap.m_bYSyncCtl == true)
                    { this.YStateLabel.Text = "X1轴负向运动"; }
                    else
                    { this.YStateLabel.Text = "X轴同步负向运动"; }
                    RoyalMap.m_bYmove1 = true;//Y1轴正在运动标志位
                    break;

                case 5://20220506批注：Y轴主运动-前进
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
                        nSpeed = MM_TO_DOT(m_szMovSpeed, 5080, 1);//20220506调整：必须调整成正确的运转速度
                        nRetVal = royal.royal.DEM_Run(1, true, (uint)nSpeed, 750000, nCtlValue);  //添加强转(UINT)nSpeed
                        this.YStateLabel.Text = "Y1轴正向运动";
                        //SetDlgItemText(IDC_STA_YMOV, ((wparam & 0x1) ? _T("Y2轴负向运动") : _T("Y2轴正向运动")));
                        RoyalMap.m_bYmove2 = true;//Y2轴正在运动标志位
                    }
                    break;
                case 6://20220506批注：Y轴主运动-后退
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
                        nSpeed = MM_TO_DOT(m_szMovSpeed, 5080, 1);//20220506调整：必须调整成正确的运转速度
                        nRetVal = royal.royal.DEM_Run(1, false, (uint)nSpeed, 750000, nCtlValue);        //添加强转(UINT)nSpeed
                        this.YStateLabel.Text = "Y1轴负向运动";
                        RoyalMap.m_bYmove2 = true;//Y2轴正在运动标志位
                    }
                    break;
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

        private UInt32 MM_TO_DOT(float X, int DPI,int AxisIndex)//20220506修改：新增AxisIndex为墨车轴的编号，3轴依次为0,1,2
        {
            if (AxisIndex == 0)//第1轴
            {
                //UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) * 2.50);//20210506修改：速度校准系数：1/20为计算值//20200803修改：2.50为墨车速度校准系数
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) *5);//20220511批注：1/25.4mm*5080=200dot//应该乘以5才对//20210506修改：速度校准系数//20200803修改：2.50为墨车速度校准系数
                dot = (UInt32)(float)(X *1000);//20220511修改：这样更加精确
                return dot;
            }
            else if (AxisIndex == 1)//第2轴
            {
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) *5);//20220511批注：1/25.4mm*5080=200dot//应该乘以5才对//20210506修改：速度校准系数：1/200为计算值//20200803修改：2.50为墨车速度校准系数
                dot = (UInt32)(float)(X * 1000);//20220511修改：这样更加精确
                return dot;
            }
            else if (AxisIndex == 2)//第3轴
            {
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) /5);//20210506修改：速度校准系数:1/2000仅仅是富余//20200803修改：2.50为墨车速度校准系数
                return dot;
            }
            else//第3轴
            {
                UInt32 dot = (UInt32)((((float)(X * DPI)) / 25.4 + 0.45f) /2000);//20210506修改：速度校准系数:1/2000仅仅是富余//20200803修改：2.50为墨车速度校准系数
                return dot;
            }
        }

        //轴运动的关闭这个非常关键；
        //操作的参数是掩码；
        //（总共3个轴，1/2是X轴，3是Y轴);
        private void XMoveBtn_MouseUp(object sender, MouseEventArgs e)//（2）运动按钮弹起时响应动作
        {
            bool nRetVal = false;
            (sender as Control).BackColor = SystemColors.Control/*Color.LimeGreen*/;


            if (RoyalMap.m_bYmove1)
            {
                if (RoyalMap.m_bYSyncCtl == true) { this.YStateLabel.Text = "X轴运动停止"; }
                else { this.YStateLabel.Text = "X1轴运动停止"; }
                nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                RoyalMap.m_bYmove1 = false;
            }
            if (RoyalMap.m_bYmove2)
            {
                this.YStateLabel.Text = "Y轴运动停止";
                nRetVal = royal.royal.DEM_StopAxisRun(false, 0x2);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                RoyalMap.m_bYmove2 = false;
            }
            if (RoyalMap.m_bXmove)//单独停止3轴
            {
                this.XStateLabel.Text = "Y2轴运动停止";
                nRetVal = royal.royal.DEM_StopAxisRun(false, 0x4);//停止轴运动20200107//_MC_X_MASKBIT扩展到0x1——20200108
                RoyalMap.m_bXmove = false;
            }
        }
        /*****************************************打开DY运动监测****************************************/
        bool m_bMoveRunTest = false;//运行测试的标志位
        private void OpenMoveBtn_Click(object sender, EventArgs e)//开启双Y轴运动监控
        {

            nSpeed = MM_TO_DOT(50, 5080,0);//20220506修改：第1轴//20200328新增：
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
                nSpeed = MM_TO_DOT(50, 5080, 0);//20220506修改：第1轴//20200328新增：
                int index = SpeedBox.FindString("100"/*"50"*/);
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
        private void EncoderResetBtn_Click(object sender, EventArgs e)//开启关闭对应的校准线程
        {
            if (EncoderResetFlag == false)//20220521批注：没有存在校准任务
            {
                //开启墨车打印线程1:
                string tempThreadName = "InkCarEncoderResetThread";
                Thread tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                if (tempThread != null)
                {
                    EncoderResetThreads.Remove(tempThread);//以防万一
                }
                else
                {
                    ThreadStart initThreadEntry = new ThreadStart(InkCarEncoderResetThread);//20200220:线程入口方法修改为联动线程
                    tempThread = new Thread(initThreadEntry) { IsBackground = true };
                    tempThread.Name = tempThreadName;
                    tempThread.Start();
                    EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
                }

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
                //(1-1)关闭联调线程；（1-2）关闭多轴运动：保障运动安全
                string tempThreadName = "InkCarEncoderResetThread";//(1)关闭联调线程
                DeleteThread(tempThreadName);
                //(a)检测到正限位和负限位后紧急停止运动：
                bool nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108

                //(2-1)关闭粉车校准线程(2-2)关闭多轴运动：保证运动安全
                tempThreadName = "PowderCarHomeResetThread";//(1)关闭联调线程
                DeleteThread(tempThreadName);
                //(a)检测到正限位和负限位后紧急停止运动：
                short AXIS = 2; motionMap.StopMotion(AXIS);//停止铺粉车轴运动

                //修改按钮状态为：启动打印
                this.EncoderResetBtn.Text = "校准运动系统";
                this.EncoderResetBtn.TextAlign = ContentAlignment.MiddleCenter;
                //this.EncoderResetBtn.BackColor = Color.Yellow; //this.EncoderResetBtn.BackgroundImage = System.Drawing.Image.FromFile("ICON资源/RunJob.png");
                EncoderResetFlag = false;//玩的都是标志位：20200126
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

        private void PowderCarHomeResetThread()//20220521新建：送粉车教校准
        {
            int nIOState = motionMap.MointoringAxis2(2/*4*/);//铺粉轴的限位状态//motionMap.ClrLimitAndAbrupt(AXIS);//增添这一行非常关键//20220521新建：清楚该轴的限位                 
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                //MessageBox.Show("粉车不在正常停靠区间");
                PowderCarHomeFlag = false;//20220521新建：粉车校准校准异常
            }
            else//粉车位于正常停靠区
            {
                bool ReturnCode = motionMap.SetBackHome(2/*轴*/, k_RYSYSParamAutoPrintParamInTest.m_dPowderCarHomeposition /*0*//*105*//*原点复位值，单位MM*/, 20/*校准速度，单位MM/S*/, 1000/*搜索距离,单位MM*/, 5/*脱离距离，单位MM*/, ref PowderCarHomeFlag/*校准完成标志*/);//20220511修改：铺粉轴为轴2，回零速度为30mm/s,搜索距离为105CM，脱离距离为5CM//20200602：需要测试，以40mm/s的速度回零，搜索距离为1M,脱离距离为5CM//20200627批注：修改为10CM//HOME的复位值设置为105MM
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
                        if (PowderCarHomeFlag == true)//20220521新建：粉车部分校准完成汇报
                        {
                            /*PowderHomeBtn.Text = "铺粉"; */PowderHomeBtn.BackColor = Color.Lime;//已校标志
                            MessageBox.Show("粉车-Home成功！");
                        }
                        else
                        {
                            /*PowderHomeBtn.Text = "铺粉"; */PowderHomeBtn.BackColor = Color.Tomato;//未校标志
                            MessageBox.Show("粉车-Home失败！");    
                        }

                        if (InkCarHomeFlag == true && PowderCarHomeFlag == true)//20220521新建：粉车、墨车全部校准汇报
                        {
                            EncoderResetBtn.Text = "已校准";
                            CorrectFlag = true;//20200919新建：校准完成标志位
                        }
                        else { EncoderResetBtn.Text = "未校准"; }
                    }
                    ));
            }
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
        private void InkCarEncoderResetThread()//20220521新建：墨车校准
        {
            bool ntempRetVal = royal.royal.DEV_EnableUVPosCtlOut(false, false);//20210623批注：（0）关闭UV使能 //20220512新建：通过低速撞零点方式校准//适用于第2代的设备逻辑
#if (true)
            //（1）墨车X轴校准
            //（1）墨车X轴校准
            bool nRetVal = royal.royal.DEV_ResetPrintEncoder(0x1000000);//20200801批注：关键BUG,最大计数值为83.88608M;
            uint nIOState1 = royal.royal.DEM_GetAxisLmtZeroState(0);//(b)读取实时的轴限位状态//底层接口已经作了12 bit移位处理，对照Reg[12]定义//(c)读取实时的轴限位状态
            uint EncoderPos1 = royal.royal.DEV_GetPrintEncoderValue(); uint EncoderPos2 = 0;//20200311新增：编码器位置设置

            nSpeed = MM_TO_DOT(20f/*50f*/, 5080, 0);//20220506修改：第1轴;校准速度应该控制在20mm/s//YINC_PERDOT扩展到48——20200108
            while (/*(0 == (nIOState1 & 0x10)) &&*/ (0 == (nIOState1 & 0x2/*负限位*/)) && (0 == (nIOState1 & 0x1/*正限位*/)))//20200526批注：0x10/*零位*/；保证运动到零位,核心在于保证触发零位，正限位，负限位不重要
            {
                EncoderPos2 = royal.royal.DEV_GetPrintEncoderValue();//读取新的编码器位置值：20200311新增
                if (EncoderPos1 != EncoderPos2)//判断是否停止
                { EncoderPos1 = EncoderPos2;/*新的编码器值赋值给就的编码器值*/}
                else
                {
                    nRetVal = royal.royal.DEM_Run(0, true/*false*/, (UInt32)nSpeed/*1000*/, 800000/*100000*/, 2);//开启2000mm的运动量，消除运动过程中的抖动现象
                    Thread.Sleep(1/*10*/);//20220513新建批注：程序睡眠时间缩减为1ms//10ms检查一次：20200313新增：//20200623批注：借此消除潜在的偶发的喘振
                }
                nIOState1 = royal.royal.DEM_GetAxisLmtZeroState(0);//在新线程中刷新限位状态（读取）
                Thread.Sleep(1);//10ms检查一次：20200313新增：//20200623批注：借此消除潜在的偶发的喘振
            }
            nRetVal = royal.royal.DEM_StopAxisRun(false /*false*/, 0x1);//20200623批注：修改成带减速停止//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108//其实必要性不必很高，FPGA上有硬限位停止
            nRetVal = royal.royal.DEM_Run(0, false/*true*/, (UInt32)nSpeed, 50000 /*10000*//*40000*/, 3/*2*/);//20220513新建：运动到限位，反向运动5CM，脱离限位区域;50000pulse对应5CM距离//10000，对应5cm，确保退的时候不撞到右侧的铺粉车。脱离零位：执行正向运动，运动指定零位右侧某个位置：5cm      
            bool Directory = false; uint nRevPls = 0;
            while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls)) { Thread.Sleep(1); } //uint CorrectPos = royal.royal.DEV_GetPrintEncoderValue();
            bool nRetVal3 = royal.royal.DEV_ResetPrintEncoder((790-50)*1000/5 /*CorrectPos - 0x10000 + 10000*//*4000*//*80000*//*80000*/);//20220513批注：墨车X轴的正负限位总长为790.165MM
                                                                                                                                          //暂时不生效 //BackToStation(40/*22.715*/, 50);//InkCarHomeFlag = true;//20200627新增：墨车校准成功标志位
            //（2）墨车Y轴校准
            //（2）墨车Y轴校准
            nRetVal3 = royal.royal.DEM_SetAxisEncodeVal(1/*第2轴*/, 0x1000000);//20220513修改：更换API//20200801批注：关键BUG,最大计数值为83.88608M;
            nIOState1 = royal.royal.DEM_GetAxisLmtZeroState(0/*第2轴也为0*/);//20220513批注：修改为1轴 //(b)读取实时的轴限位状态//底层接口已经作了12 bit移位处理，对照Reg[12]定义//(c)读取实时的轴限位状态
            EncoderPos1 = royal.royal.DEM_GetAxisEncodeVal(1/*第2轴*/); EncoderPos2 = 0;//20200311新增：编码器位置设置

            nSpeed = MM_TO_DOT(20f/*50f*/, 5080, 0);//20220506修改：第1轴;校准速度应该控制在20mm/s//YINC_PERDOT扩展到48——20200108
            while ((0 == (nIOState & 0x4 /*nIOState1 & 0x2*//*负限位*/)) && (0 == (nIOState & 0x8 /*nIOState1 & 0x1*//*正限位*/)))//20200526批注：0x10/*零位*/；保证运动到零位,核心在于保证触发零位，正限位，负限位不重要
            {
                EncoderPos2 = royal.royal.DEM_GetAxisEncodeVal(1/*第2轴*/);//读取新的编码器位置值：20200311新增
                if (EncoderPos1 != EncoderPos2)//判断是否停止
                { EncoderPos1 = EncoderPos2;/*新的编码器值赋值给就的编码器值*/}
                else
                {
                    nRetVal = royal.royal.DEM_Run(1/*第2轴*/, false, (UInt32)nSpeed, 800000, 0/*2*/);//开启2000mm的运动量，消除运动过程中的抖动现象//uint nCtlValue = 0; //20220511批注：第1位是否不等停；第2位是否双Y同步//_MC_CTL_SYNC_MASK扩展到2
                    Thread.Sleep(1/*10*/);//20220513新建批注：程序睡眠时间缩减为1ms//10ms检查一次：20200313新增：//20200623批注：借此消除潜在的偶发的喘振
                }
                nIOState1 = royal.royal.DEM_GetAxisLmtZeroState(0/*第2轴也为0*/);//20220513批注：修改为1轴 //在新线程中刷新限位状态（读取）
                Thread.Sleep(1);//10ms检查一次：20200313新增：//20200623批注：借此消除潜在的偶发的喘振
            }
            nRetVal = royal.royal.DEM_StopAxisRun(false, 0x2/*第2轴*/);//20200623批注：修改成带减速停止//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108//其实必要性不必很高，FPGA上有硬限位停止
            nRetVal = royal.royal.DEM_Run(1/*第2轴*/, true, (UInt32)nSpeed, 50000 , 0/*3*//*2*/);//20220513新建：运动到限位，反向运动5CM，脱离限位区域;50000pulse对应5CM距离//10000，对应5cm，确保退的时候不撞到右侧的铺粉车。脱离零位：执行正向运动，运动指定零位右侧某个位置：5cm      
            Directory = false; nRevPls = 0;
            while (royal.royal.DEM_AxisIsRuning(1/*第2轴*/, ref Directory, ref nRevPls)) { Thread.Sleep(1); } //uint CorrectPos = royal.royal.DEV_GetPrintEncoderValue();
            nRetVal3 = royal.royal.DEM_SetAxisEncodeVal(1/*第2轴*/,50/* (298 - 50)*//*50*/ * 1000 / 5);//20220513批注：墨车X轴的正负限位总长为297.935MM//20220524修改：复位值为50MM:安全值：10MM以内距离不允许过去
            //暂时不生效 //BackToStation(40/*22.715*/, 50);
            InkCarHomeFlag = true;//20200627新增：墨车校准成功标志位
#endif
            if (EncoderResetBtn.InvokeRequired == true)
            {
                EncoderResetBtn.BeginInvoke(
                    new Action(() =>
                    {
                        if (InkCarHomeFlag == true)//20220521新建：墨车部分校准完成汇报
                        {
                            //CorrectFlag = true;//20200919新建：校准完成标志位 
                            InkCarHomeBtn.BackColor = Color.Lime; //已校标志//this.EncoderResetBtn.Text = "运动系统已校";//恢复控件操作 //this.EncoderResetBtn.BackColor = Color.Tomato;                   
                            MessageBox.Show("墨车-Home成功！");
                            //20200604新增：回复回零速度为界面选中速度
                            nSpeed = MM_TO_DOT(m_szMovSpeed/*50f*/, 5080, 0);//20220506修改：第1轴 //YINC_PERDOT扩展到48——20200108
                        }
                        else { InkCarHomeBtn.BackColor = Color.Tomato; }

                        if (InkCarHomeFlag == true && PowderCarHomeFlag == true)//20220521新建：粉车、墨车全部校准汇报
                        {
                            EncoderResetBtn.Text = "已校准";
                            CorrectFlag = true;//20200919新建：校准完成标志位
                        }
                        else { EncoderResetBtn.Text = "未校准"; }
                    }
                    ));
            }
            DeleteThread("InkCarEncoderResetThread");//20200220：本线程结束，需要及时清理相关线程//20200313新增：
        }

        public bool InkCarHomeFlag = false; public bool PowderCarHomeFlag = false;

        //刷新虚拟打印编码器状态显示定时器
        private System.Windows.Forms.Timer Timer4 = null;//刷新虚拟打印编码器状态显示定时器
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
        private void Timer4_Tick(object sender, EventArgs e)//刷新墨量显示状态定时器
        {
            string szTxt;
            //UInt32 nIOState;//墨车控制器的状态标志位：20200311
            double/*UInt32*/ nxpos, ny1pos, ny2pos;//20200602批注：
            UInt32 nxAxis, nyAxis1, nyAxis2;

            //////////////////////////////////(a)实时的轴编码器位置
            //ny1pos = royal.royal.DEM_GetAxisEncodeVal(0);//_MC_Y1_INDEX扩展到1
            //UInt32 readEncoderPosValue = royal.royal.DEV_GetPrintEncoderValue();//20200306新增：
            //MessageBox.Show("设备的编码器值是：" + Convert.ToString(readEncoderPosValue));//20200306新增：           
            ny1pos = /*(uint)*/(royal.royal.DEV_GetPrintEncoderValue() * 0.005);//20200306新增：编码器位置设置

            ny2pos = /*(uint)*/(royal.royal.DEM_GetAxisEncodeVal(1) * 0.005);//_MC_Y2_INDEX扩展到2//其实这个API用处不是很大：20200327批注
            nxpos = /*(uint)*/(royal.royal.DEM_GetAxisEncodeVal(2) * 0.005);//_MC_X_INDEX扩展到0//其实这个API用处不是很大：20200327批注
            szTxt = ny1pos.ToString(/*"G"*//*"X"*/"F3") + " MM";//16进制显示——20200108
            this.Y1PosLabel.Text = szTxt;
            //this.CurEncoderText.Text = szTxt;//UV灯栏显示的光栅编码器值//20200306新增(又去掉)：虚拟编码可以测试UV灯数据

            szTxt = ny2pos.ToString("F3"/*"X"*/) + " MM";//16进制显示——20200108
            this.Y2PosLabel.Text = szTxt;

            szTxt = nxpos.ToString("F3"/*"X"*/) + " MM";//将十进制数值以16进制的形式显示——20200108
            this.XPosLabel.Text = szTxt;//20200306修改错误：

            //////////////////////////////////(b)剩余脉冲数获取//20200327：这个用处不是很大：I think是这样。
            nyAxis1 = royal.royal.DEM_GetRevPluse(0);
            nyAxis2 = royal.royal.DEM_GetRevPluse(1);
            nxAxis = royal.royal.DEM_GetRevPluse(2);//查询剩余脉冲20200107

            szTxt = nyAxis1.ToString("G"/*"X"*/);//16进制显示——20200108
            this.Y1RevPosLabel.Text = szTxt;
            szTxt = nyAxis2.ToString("G"/*"X"*/);//16进制显示——20200108
            this.Y2RevPosLabel.Text = szTxt;

            szTxt = nxAxis.ToString("G"/*"X"*/);//16进制显示——20200108
            this.XRevPosLabel.Text = szTxt;

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
            nSpeed = MM_TO_DOT(m_szMovSpeed, 5080,0);//YINC_PERDOT扩展到48——20200108

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
        }

        private void DefaultUVRangeBtn_Click(object sender, EventArgs e)//恢复默认区间
        {
            k_RYSYSParamAutoPrintParamInTest.LeftCureOn = 550;
            k_RYSYSParamAutoPrintParamInTest.LeftCureOff = 1000;
            k_RYSYSParamAutoPrintParamInTest.RightCureOn = 270;
            k_RYSYSParamAutoPrintParamInTest.RightCureOff = 720;
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
        private void OpenUVLamp(bool EnableLight, double UV1LimitUp, double UV1LimitDown , double UV2LimitUp, double UV2LimitDown)//20220520新建：默认输入（0,1000,01,1000）
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

            }
            else//关闭灯
            {
                OpenUVLamp(false, UV1Pos1, UV1Pos2, UV2Pos1, UV2Pos2);
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
        private System.Windows.Forms.Timer Timer5 = null;//刷新虚拟打印编码器状态显示定时器
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
                }
                catch(Exception exception)
                {
                    MessageBox.Show("发生异常：异常代码"+exception);//20210201新建：增加软件操作的稳健性
                }
            }
            else//修改datagridview状态为选中：选中状态，可编辑datagridview的cell
            {
                try
                {
                    //(0)dataGridView2控件重新初始化：
                    InitDataGrid2(1);
                    ADIBSetControlInterferenceMaintain(true);//进入设置状态时：
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

            while (FlagPrinting == false)//20200612批注：随便找的1个标志位作为测试
            {
                float[,] fVoltageTempData = new float[8, 5];//20200612修改：

                for (uint d = 0; d < 8; d++)
                {
                    if (true)
                    {
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
                        fVoltageTemp[0] = 18.0f; fVoltageTemp[1] = 18.0f; fVoltageTemp[2] = 18.0f; fVoltageTemp[3] = 18.0f;
                        fVoltageTemp[4] = 25.0f;//喷头温度
                        int size1 = Marshal.SizeOf(fVoltageTemp[0]) * 4/*fVoltageTemp.Length*/;
                        IntPtr PfVoltage = Marshal.AllocHGlobal(size1);

                        Marshal.Copy(fVoltageTemp, 0, PfVoltage, 4);//测试用：非托管区内存初始化//实际测试的时候，去掉//20200405批注

                        //20200424新增//读取波形参数的电压：
                        bool returnCode = royal.royal.MCU_GetCurPhVoltage(PfVoltage, p, d);
                        if (returnCode == true)
                        {
                            //（1）解析读取到的电压值
                            Marshal.Copy(PfVoltage, fVoltageTemp, 0, 4);

                            //（2）释放内存空间
                            Marshal.FreeHGlobal(PfVoltage);//释放内存
                        }
                        if (FlagPrinting /*&& (*bAbort) || m_bStopMonitor || m_bCloseAutoChk*/)
                            return;
                        Thread.Sleep(10);

                        //喷头温度
                        int size2 = Marshal.SizeOf(fVoltageTemp[0]) * 1 /* *fCurTemp.Length*/;//4个变量，只用到第1个
                        IntPtr PfCurTemp = Marshal.AllocHGlobal(size2);

                        Marshal.Copy(fVoltageTemp, 0, PfCurTemp, 1/*fVoltageTemp.Length*/);//测试用:非托管区内存初始化//实际测试的时候，去掉//20200405批注

                        //20200424新增//读取喷头的温度：
                        bool returnCode2 = royal.royal.MCU_GetCurPhTemp(PfCurTemp, p, d);

                        if (returnCode2 == true)//0x0802->0x0822
                        {
                            //（1）解析读取到的电压值
                            Marshal.Copy(PfCurTemp, fVoltageTemp, 4, 1/* 0, fCurTemp.Length*/);

                            //（2）释放内存空间
                            Marshal.FreeHGlobal(PfCurTemp);//释放内存
                        }

                        for (int j = 0; j < 5; j++)
                        {
                            fVoltageTempData[d, j] = fVoltageTemp[j];
                        }
                    }
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
                for (int d = 0; d < 8; d++)//依次刷新8个喷头的温度电压
                {
                    //（1）修改1个喷头的电压参数：
                    dataGridView1.Rows[(int)d].Cells[0].Value = fVoltageTempData[d, 0].ToString("F2");//浮点数格式，2位小数点
                    dataGridView1.Rows[(int)d].Cells[1].Value = fVoltageTempData[d, 1].ToString("F2");//浮点数格式，2位小数点
                    dataGridView1.Rows[(int)d].Cells[2].Value = fVoltageTempData[d, 2].ToString("F2");//浮点数格式，2位小数点
                    dataGridView1.Rows[(int)d].Cells[3].Value = fVoltageTempData[d, 3].ToString("F2");//浮点数格式，2位小数点                   
                    //（2）修改1个喷头的温度控件：
                    dataGridView1.Rows[(int)d].Cells[4].Value = fVoltageTempData[d, 4].ToString("F2");//浮点数格式，2位小数点   
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
                m_bSetEnable = false;
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
                m_bSetEnable = true;
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
                        //try
                        //{
                        //    //20200421新增：新增异常捕获
                        //    fcurval[j] = Convert.ToSingle(dataTable2.Rows[i][j].ToString());//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                        //}
                        //catch(Exception error)
                        //{
                        //    MessageBox.Show("输入有误：{0}",error.Message);
                        //}
                        //finally
                        //{}
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

                //(3-2）此过程耗时2-3s，时间比较长，因此，本部分，暂时取消
                bool nRetVal = royal.royal.DEV_AdibControl(ref royal.royal.g_sys_param.adibParam, nIoOption, true, ref m_bSetEnabled);//设置气压板的所有参数值：负压、正压、二级墨盒温度

            }
            catch (Exception error)
            {
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
        private void JetSetApplyBtn_Click(object sender, EventArgs e)//写指令到喷头驱动板
        {
            //（0）设置的总计8路32个电压值，8路8个温度值
            //float [,,] fVolt= new float[1,8,4];//1个车头板，8个驱动板，每个驱动板4路电压//20200404：本人的方式，不采用数组的形式存储这些电压值
            //float [,,] fDTemp= new float[1,8,1];//1个车头板，8个驱动板，每个驱动板4路电压//20200404：本人的方式，不采用数组的形式存储这些电压值
            int nPrtValidMask = 0;//2020404新增：车头卡连接掩码
            int nDrvValidMask = 0;//2020404新增：驱动卡连接掩码

            //（1）将DataTable的数据存储到多维数组中：fVolt和fDTemp
            for (int i = 0; i < 8; i++) //电压暂时没用 0 -> 1
            {
                for (int j = 0; j < 5; j++)//（1）解析第i行数据
                {
                    if (j < 4)//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                    { fVolt[i, j] = Convert.ToSingle(dataTable.Rows[i][j].ToString()); }
                    else//20200401新增：非常关键的1步，完成了从dataTable到数组数据的转换
                    { fDTemp[i] = Convert.ToSingle(dataTable.Rows[i][j].ToString()); }
                }
            }

            //（2）设置并生效所有的电压和温度值
            if (true/*(nPrtValidMask & (1 << 0))!=0*/)//判断车头卡连接掩码：//20200404新增：
            {
                for (uint d = 0; d < 8; d++)
                {
                    if (true/*(nDrvValidMask & (1 << (int)d)) != 0*/)//判断驱动卡连接掩码：//20200404新增：
                    {
                        //(1)复制数据：20200404修改
                        float[] fstdVoltage = new float[4];//内存中的对应值
                        for (int i = 0; i < 4; i++)
                        {
                            fstdVoltage[i] = fVolt[d, i];//for (int i = 0; i < 3; i++) fstdVoltage[i] = 85.0f;
                        }

                        //(2)设置电压：20200404修改
                        int size = Marshal.SizeOf(fstdVoltage[0]) * fstdVoltage.Length;
                        IntPtr PfstdVoltage = Marshal.AllocHGlobal(size);
                        Marshal.Copy(fstdVoltage, 0, PfstdVoltage, fstdVoltage.Length);//复制到非托管区内存

                        bool returnCode = royal.royal.MCU_SetPhVoltage(PfstdVoltage, 0, d);
                        if (returnCode == false/*!royal.royal.MCU_SetPhVoltage(PfstdVoltage, 0, d)*/)//if (!royal.royal.MCU_SetPhVoltage(fVolt[p][d], p, d))
                        {
                            /*//TRACE("电压SYS_McWrite-ERROR\r\n");//20200404：设置对应的日志记录*/
                        }
                        //Thread.Sleep(300);//睡眠300ms进行下次设置

                        //（3）设置温度：20200404修改
                        float fProTemp = fDTemp[d] /*70.0f*/;//ftemp = fDTemp[p][d];  
                        bool returnCode2 = royal.royal.MCU_SetPhStdTemp(ref fProTemp, 0, d);
                        if (returnCode2 == false)    //0x1800+(4*60) - > 0x1800+4*4//royal.royal.MCU_SetPhStdTemp(&ftemp, p, d)
                        {
                            /*//TRACE("温度SYS_McWrite-ERROR\r\n");//20200404：设置对应的日志记录*/
                        }
                        //Thread.Sleep(300);//睡眠300ms进行下次设置
                    }
                }
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
            if (true/*m_hWnd*/ && (!bLastSet))      //编辑时不查询//标志位：参数设置标志位
            {
                bLastSet = m_bSetEnable;//标志位：参数设置标志位

                //20200401批注：结构体赋值的问题，有点头大！！！！！！！！！！！！！！！！！

                royal.LPADIB_PARAM lpAdibparam = adibCurState;//完成对应的引用设置：20200329批注
                //    SafeArrayRankMismatchException AF A


                if (bLastSet)//问题在这里，终于找到了！！！！
                {

                    //lpAdibparam = royal.royal.g_sys_param.adibParam;//标志位：bLastSet标志位。切换：是使用当前的&adibCurState还是&g_sysParam.adibParam：20200329批注。
                }
                //m_SigLogicInput.SetInkState(~(lpAdibparam->nLgStatus&0xFF));

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
                    dataGridView2.Rows[0].Cells[i].Value = lpAdibparam.fcurvoltage[i].ToString("F2");//浮点数格式，2位小数点
                    dataGridView2.Rows[1].Cells[i].Value = lpAdibparam.fcurInkTankTemp[i].ToString("F2");//浮点数格式，2位小数点
                    dataGridView2.Rows[2].Cells[i].Value = lpAdibparam.fcurAirPress[i].ToString("F2");//浮点数格式，2位小数点
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
            uint nIoOption = 0xF;//nIoOption = 0d1111;
            while (!m_bStopMonitor)
            {

                if (m_bSetEnable)//标志位：设置标志位，只有在设置无效的时候，才每隔100ms进行1次查询工作：20200329新增
                {
                    //nIoOption=0x1FF000F;
                    m_bComState = royal.royal.DEV_AdibControl(ref adibCurState, nIoOption, false, ref m_bSetEnable);//bSetParam位的作用为0：状态为读状态：20200329批注

                    if (m_bComState)//标志位：与PPCB卡建立通讯
                        nIoOption = 0x6;//0d0110——————不再重新读取PPCB及FPGA的版本号：额外话，PPCB及FPGA各只有2个版本：20200329批注
                    else//标志位：未与PPCB卡建立通讯
                        nIoOption = 0xF;//0d1111——————继续读取PPCB及FPGA的版本号：20200329批注
                }
#if false//20200604新增：同时监测温度电压的数据并完成更新
                if (m_bVTSetEnable==true)
                {
                    bool nReturn = GetCurVoltageTemp(false);
                }
#endif
                Thread.Sleep(100);
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
                royal.royal.g_sys_param.szWavePath = openWaveFormFileDialog.FileNames[0];//获取所有选中项的文件名
                royal.royal.DEV_UpdateParam(ref royal.royal.g_sys_param);//20200801新增：先更新波形，再加载完波形
                royal.royal.DEV_ReloadWaveForm();//20200801新增：先更新波形，再加载完波形
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
            if (m_bCleanFlag == false)//(b)根据ID反转背景图片
            {
                //(sender as Control).BackColor = Color.DarkOrchid;
                (sender as Control).Text = "清洗";
            }
            else
            {
                //(sender as Control).BackColor = Color.MintCream;
                (sender as Control).Text = "关闭\r\n清洗";
            }
            //(c)计算输出
            if (m_bCleanFlag == true)//打开和关闭闪喷：
            {
                //（1）开清洗阀门（==等效：关墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                OpenCloseVALVE(2 - 1, false);//20200605批注：Tag-1

                //（2）开煤气、供水泵源：
                uint nIoVal = 0x1FF;//控制:P1-P2-P3~P7,依次是清洗泵、压墨泵、供墨泵1-7
                bool nRetVal = royal.royal.DEV_SetInkPump(nIoVal);//打开压墨泵
                m_bCleanFlag = false;
            }
            else
            {
                //（1）关清洗阀门（==等效：开墨水阀门）。开煤气阀门： 打开对应的泵源。类似于供煤气、供水阀门。
                OpenCloseVALVE(2 - 1, true);//20200605批注：Tag-1

                //（2）开煤气、供水泵源：
                uint nIoVal = 0x0;//控制:P1-P2-P3~P7,依次是清洗泵、压墨泵、供墨泵1-7
                bool nRetVal = royal.royal.DEV_SetInkPump(nIoVal);//关闭压墨泵
                m_bCleanFlag = true;
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
        public double[] m_dScrapervel = new double[2] { 150, 150 };//20200918新增：刮墨主运动、刮墨副运动速度
        public bool[] m_bScraperEnds = new bool[2] { false, false };//20200918新增：刮墨主运动、刮墨副运动AB端定义
        public bool CorrectFlag = false;//20200919新增：系统校准标志位
        //public double[] m_Step = new double[2]{ 150, 450};//20200918新增：刮墨主运动、刮墨副步进距离
        //public bool[] m_bMoveModeFlag = new bool[2]{ true, true};//20200918新增：刮墨主运动、刮墨副运动类型
        /// <summary>
        /// 20200918新增：刮墨动作逻辑，耦合刮墨主运动和刮墨副运动的运动逻辑n  
        /// </summary>
        private void ScraperMotionLogic()//20200918新增：精华
        {           
            if (CorrectFlag)//墨车双驱系统是否进行过校准
            {
                BackToStation(155.6, 50, false,true);//移动到155.6MM行程处//ExchangeToSNexttation(155.6);//运动至压墨回收站//Thread.Sleep(100);//BackToStation(22.715);//移动到22.715MM行程处
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
                AutomoveComponent.MoveHome(7, m_dScrapervel[0], m_bScraperEnds[0], RollerParam);//GOHOME动作2：B端运动到A端//20200220：逻辑判断，必须有，主要是是否到触碰到限位，和双X轴是否存在冲突
                AutomoveComponent.MoveHome(8, m_dScrapervel[1], !m_bScraperEnds[1], RollerParam);//GOHOME动作2：C端运动到D端                

                ExchangeToSNexttation(/*155.6*/280);//运动至压墨回收站//Thread.Sleep(100);
                AutomoveComponent.MoveHome(8, m_dScrapervel[0], m_bScraperEnds[0], RollerParam);//GOHOME动作3：D端运动到C端
                AutomoveComponent.MoveHome(7, m_dScrapervel[1]*2, !m_bScraperEnds[1], RollerParam);//GOHOME动作4：A端运动到B端

                ExchangeToSNexttation(45/*22.715*/);//运动至清洗站//Thread.Sleep(100);//回位//20201021修改：运动至45mm处即可

                //添加新逻辑：20210121新增
                //添加开启闪喷逻辑：预防刮墨清洗之后导致的喷头局部出墨异常
                //输入参数为：闪喷时间

                int CleanSpartTime = (int)k_RYSYSParamAutoPrintParamInTest.m_dCleanSparkTime * 1000;//实时生效清洗闪喷时长设置：20200122新增

                bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷
                Thread.Sleep(CleanSpartTime/*3000*/);//开启闪喷3秒钟
                nRetVal = royal.royal.IDP_FlashPrtCtl(false);//打开闪喷
            }
            else
            {
                MessageBox.Show("系统未校准，请先校准！");
            }
        }

        private void ExchangeToSNexttation(double AimPos)//运动切换到新工作站区域：精华
        {
            UInt32 nCtlValue = 2; UInt32 ny1pos = 0; UInt32 ny2pos = 0; bool DirFlag = false; float m_MovSpeed = 50;//50mm/s速度进行移动；到站延时运动精度0.5mm
            double nSpeed = MM_TO_DOT(m_MovSpeed, 5080,0);//20220506修改：第1轴 //20200803修改：已包含运动修正系统//double nSpeed = 68016;
            bool Directory = false; uint nRevPls = 0;

            ny1pos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
            if (ny1pos * 0.005 >= AimPos)//墨车在清洗站台右侧
            { DirFlag = false; }
            else//墨车在清洗站台左侧
            { DirFlag = true; }

            double MoveStep = (AimPos - ny1pos * 0.005) * 500;
            bool nRetVal = royal.royal.DEM_Run(0, DirFlag, (UInt32)nSpeed, (int)System.Math.Abs(MoveStep)/*50000*/, nCtlValue);//三菱驱动器的千脉冲MM数：按照之前代码，应该是500;运行100MM;单pulse-2um
            while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls))//int SleepTime = (int)((double)(AimPos - CurrentPos) / nSpeed);//Thread.Sleep(5000);//Sleep时间必须要有依据//确保运行到位，运行精度为2UM
            {
                Thread.Sleep(1);
            }
            nRetVal = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
        }
        private void BackToStation2(double AimPos, double m_MovSpeed,bool WaitStopFLag)//20220520新建：铺粉轴运动至指定区域：AimPos位置单位为MM
        {      
            double CurrentPos = GetCurrentPos(2);//20220520新建：读取指定轴的当前编码器位置
            double TrapSpace = AimPos - CurrentPos;
            TrapMoveUp(2, true, Convert.ToString(m_MovSpeed), Convert.ToString(TrapSpace), true, !WaitStopFLag/*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
        }
        private void BackToStation3(double AimPos, double m_MovSpeed, bool WaitStopFLag)//20220528新建：刮墨轴运动到指定为位置：AimPos位置单位为MM
        {
            //double CurrentPos = GetCurrentPos(4);//20220520新建：读取指定轴的当前编码器位置
            //double TrapSpace = AimPos - CurrentPos;
            //TrapSpace = TrapSpace / 12800;//转数
            //m_MovSpeed = m_MovSpeed / 12800;//转每秒
            TrapMoveUp(4, true, Convert.ToString(m_MovSpeed), Convert.ToString(AimPos), true, !WaitStopFLag/*true*/);//20200528批注：TrapSpace量为转数，m_MovSpeed为转/秒
        }

        private double GetCurrentPos(int Axis)//20220520新建：读取指定轴的当前编码器位置
        {
            double[] g_dEncpos = new double[8]; g_dEncpos = motionMap.GetEncPos(); double CurrentPos = g_dEncpos[Axis-1/*1*/] / 1000;//粉车位置：
            return CurrentPos;
        }
        private void WaitStop(int Axis)//20220520新建：实现墨车相关的等停逻辑
        {
            bool Directory = false; uint nRevPls = 0;
            while (royal.royal.DEM_AxisIsRuning((uint)(Axis-1)/*0*/, ref Directory, ref nRevPls)) { Thread.Sleep(1);/*确保运行到位，运行精度为2UM*/ }//0为第1轴，1为第2轴
            bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, (uint)Axis/*0x1*//*0x2*/);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
        }
        private void BackToStation(double AimPos, float m_MovSpeed, bool MoveDirectionFlag,bool WaitStopFLag)//运动至初始区域：AimPos位置单位为MM：精华
        {
            switch (MoveDirectionFlag)
            {
                case false://20220513新建：为X方向的运动指令
                    UInt32 nCtlValue = 2; UInt32 CurrentPos = 0; bool DirFlag = false;
                    bool Directory = false; uint nRevPls = 0; double nSpeed = MM_TO_DOT(m_MovSpeed, 5080, 0);//20220506修改：第1轴//20200803修改：已包含运动修正系统//double nSpeed = 68016;

                    CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
                    if (CurrentPos * 0.005 >= AimPos) { DirFlag = false; }/*墨车在目标位置右侧*/else { DirFlag = true; }//墨车在目标位置左侧

                    if (AimPos >= 10 && AimPos <= 780)//是否AimPos在工作流程内:在流程内，即可打印:确认在安全工作区内；行程为（0MM，790MM）
                    {
                        double MoveStep = (AimPos - CurrentPos * 0.005) * 1000;//20220513修改：
                        bool nRetVal = royal.royal.DEM_Run(0, DirFlag, (UInt32)nSpeed, (int)System.Math.Abs(MoveStep), nCtlValue);//三菱驱动器的千脉冲MM数：按照之前代码，应该是500;运行100MM;单pulse-2um                                                              
                        if (WaitStopFLag == true)
                        {
                            while (royal.royal.DEM_AxisIsRuning(0, ref Directory, ref nRevPls)) { Thread.Sleep(1);/*确保运行到位，运行精度为2UM*/ }
                            bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x1);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                        } 
                    } else{}//不在安全工作区内:不进行运动

                    break;

                case true://20220513新建：为Y方向的运动指令
                    nCtlValue = 0; CurrentPos = 0; DirFlag = false;
                    Directory = false; nRevPls = 0; nSpeed = MM_TO_DOT(m_MovSpeed, 5080, 0);//20220506修改：第1轴//20200803修改：已包含运动修正系统//double nSpeed = 68016;

                    CurrentPos = royal.royal.DEM_GetAxisEncodeVal(1/*获取第2轴编码器*/);//初始编码器位置：
                    if (CurrentPos * 0.005 >= AimPos) { DirFlag = false; }/*墨车在目标位置前侧*/else { DirFlag = true; }//墨车在目标位置后侧

                    if (AimPos >= 10 && AimPos <= 295)//是否AimPos在工作流程内:在流程内，即可打印:确认在安全工作区内
                    {
                        double MoveStep = (AimPos - CurrentPos * 0.005) * 1000;//20220513修改：
                        bool nRetVal = royal.royal.DEM_Run(1/*第2轴*/, DirFlag, (UInt32)nSpeed, (int)System.Math.Abs(MoveStep), nCtlValue);//三菱驱动器的千脉冲MM数：按照之前代码，应该是500;运行100MM;单pulse-2um                                                              
                        if (WaitStopFLag == true)
                        {
                            while (royal.royal.DEM_AxisIsRuning(1/*第2轴*/, ref Directory, ref nRevPls)) { Thread.Sleep(1); }//int SleepTime = (int)((double)(AimPos - CurrentPos) / nSpeed);//Thread.Sleep(5000);//Sleep时间必须要有依据//确保运行到位，运行精度为2UM
                            bool nRetVal2 = royal.royal.DEM_StopAxisRun(false, 0x2/*第2轴*/);//停止轴运动20200107//_MC_Y_MASKBIT扩展到0x2——20200108
                        }            
                    }
                    else { }//不在安全工作区内:不进行运动

                    break;
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
                bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷
                m_bFlashFlag = false;
            }
            else
            {
                bool nRetVal = royal.royal.IDP_FlashPrtCtl(false);//关闭闪喷
                m_bFlashFlag = true;
            }
        }
        private void StartCloseCureLight(bool EnableFlag,int CurePowerDensity, double CureBackSpeed, out double ReturnVelocity1, out double ReturnVelocity2,out bool DoubleCureEnabled)//按照虚拟编码器位置，打开及关闭UV灯
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
            /*double*/ ReturnVelocity1 = 50;//固化灯速度：默认为100mm/s
            /*double*/ ReturnVelocity2 = 150;
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
                nMinPos[0] = (int)((Convert.ToDouble(m_szUV1Pos1)+1500) * 200);//字符串转数值//20210623:添加1.5m偏移，使左灯无效
                nMaxPos[0] = (int)((Convert.ToDouble(m_szUV1Pos2)+1500) * 200);//字符串转数值//20210623:添加1.5m偏移，使左灯无效
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
            float m_fValidSec = (float)(m_fCycleSec * m_szValidSec/ 100);//20200304：25ms——(1)1ms=12.5%功率，2ms=25%功率（2）m_fValidSec=m_fCycleSec*power/100;//power/100
         

            bool nRetVal = royal.royal.DEV_EnableUVPosCtlOut(false, false);//（0）设置UV等使能开启
            if (EnableFlag)
            {
                bool nRetVal1 = royal.royal.DEV_SetUVLampPosRange(PnMinPos, PnMaxPos);//（1）设置UV灯使能开启
                bool returnCode = royal.royal.DEV_SetPwmParam(true/*Param1Check.Checked*//*true*/, m_fCycleSec, m_fValidSec);//（2）设置UV灯功率输出：//20200604批注：取消UV灯参数使能按钮
                bool nRetVal2 = royal.royal.DEV_EnableUVPosCtlOut(/*LightEnabled1*/true,true /*LightEnabled2*//*true, true*/);//（3）打开两侧的UV灯：//20210621修改:
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
        public void AutoCleanThread()//20220520修改及注释：线程内容：自动清洗动作
        {
#if true//20220518新建：新设备使用的自动清洗逻辑
            //（1）撒粉轴找回零位：20220527新建：
            double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;//刮墨轴的HOME位置

            //motionMap.SetDo(7, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵//20220915注释：无效掉

            bool ReturnCode = motionMap.SetBackSpreaderAxis(4, 1/*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s//下//20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
            if (ReturnCode == true){ /*MessageBox.Show("回零成功");*/}//校准成功
            else { MessageBox.Show("回零失败"); }

            //(2) 刮片竖直位置：-------------------------------------------------------------------------->          
            int subdivided = 12800; int SinkPostion = 0;

            //（3）联动清洗逻辑
            double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/ double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
            double CleanNozzleSpeed = m_szCleanSpeed;
            int CleanTimes = m_nCleanTime/*3*/;//默认清洗次数为3次
            for (int i = 0; i < CleanTimes/*3*/; i++)//i为清洗总次数；PASS宽度为喷头宽度//默认清洗次数为3次
            {
                if (i == 0)
                {
                    BackToStation(96 + 25/*25*/, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅 面^^^^^^^^^^^^^^^^^//96MM
                    BackToStation(710/*425*/, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅<---------------//780MM

                    SinkPostion = 135;//逆135
                    motionMap.TrapMoveSpreaderAxis(4, 2/*0.5*/, -SinkPostion);

                }

                SinkPostion = 45;//逆45
                motionMap.TrapMoveSpreaderAxis(4, 2/*0.5*/, -SinkPostion);
                //20220915修改：修改打印方式
                motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                Thread.Sleep(3000);//20220915新建：暂停2.0 s
                motionMap.SetDo(13/*7*/, false);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵

                //20220920新增：压墨之后，需要开启闪喷功能
                bool nRetVal = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷//20220920批注：闪喷关闭需要在手动部分关闭
                m_bFlashFlag = false;//20220920批注：指示手动控制闪喷功能是否开启之时的正确闪喷动作应为关闭

                BackToStation(780/*25*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true);//停靠在右侧，向左侧运动打印幅面--------------->//710MM

                SinkPostion = -45;//顺45
                //motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                motionMap.TrapMoveSpreaderAxis(4, 2/*0.5*/, -SinkPostion);

                BackToStation(710/*425*/, (float)CleanNozzleSpeed/*ReturnVelocity1*/, false, true);//停靠在右侧，向左侧运动打印幅面<---------------//780MM
            }

            SinkPostion = 180+45;//逆225
            //motionMap.SetDo(13/*7*/, true);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
            motionMap.TrapMoveSpreaderAxis(4, 0.5, -SinkPostion);

#elif true//20220518批注：该功能注释掉，就设备好用的固化清洗逻辑
            //(1)执行清洗及固化动作，判断是否需要执行清洗动作；
            int DirFlag = 0;
            UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
            
            if ((CurrentPos * 0.005 <= 50) && (0 <= CurrentPos * 0.005)) { DirFlag = 1; }//墨车在清洗站台右侧;
            else if ((1180 <= CurrentPos * 0.005) && (CurrentPos * 0.005 <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
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
        public void AutoCureThread()//20220520新建批注：线程内容：新设备使用的自动固化动作及其逻辑
        {
            if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 0)//判断使用UV还是IR作为固化光源
            { UVIRLightFlag = true; }
            else if (k_RYSYSParamAutoPrintParamInTest.m_nCureLightStrategy == 1)
            { UVIRLightFlag = false; }
            else { }

            //(0000)//20220602修改：Z向进给量:下降一个固定高度
            double vel = 1;//Z向运动速度为1mm/s
            double TrapSpace = -/*k_RYSYSParamAutoPrintParamInTest.m_nLayerThick*/200 / 1000;//20220525新建批注：层厚：调试用150μm//为负方向
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
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
                /*double*/ PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置
                if (PosValue >= (255- LightSourceOffset/*70*/) && m_startFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                {
                    if (UVIRLightFlag == true) { OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/ }
                    else { OpenIRLamp(true);/*打开红外灯*/ }
                    m_startFlag = true;
                }
                else { }
            }
            while (PosValue <= (620- LightSourceOffset/*70*/));//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM

            if (UVIRLightFlag == true) { OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/ }
            else { OpenIRLamp(false);/*关闭红外灯*/  }
            m_startFlag = false;

            int AxiStatus = 0; double prfPos = 0;
            while ((Math.Abs(PosValue) < Math.Abs(695)) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉轴，第2轴的状态为停止
            {
                PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(2, out prfPos);
                motionMap.GetAxisStatus(2, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
            }

            //B: 洒粉车回到落粉站位置（回站）：20220512批注
            PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置
            RollerParam = 0/*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed*/;//201029批注：更新辊子速度                                                                              /*double*/
            AimPos = 1; MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed;//更新值到本地变量
            BackToStation2(AimPos, MovSpeed, false/*true*/);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停//20220521设置为不等停
            RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

            //B-2:开启回程固化
            do//检查X轴是否位于指定区域
            {
                PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置
                if (PosValue <= (620 - LightSourceOffset/*70*/) && m_startFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                {
                    if (UVIRLightFlag == true) { OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/ }
                    else { OpenIRLamp(true);/*打开红外灯*/  }                    
                    m_startFlag = true;
                }
                else { }
            }
            while (PosValue >= (225 - LightSourceOffset/*70*/));//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
            if (UVIRLightFlag == true) { OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/ }
            else { OpenIRLamp(false);/*关闭红外灯*/  }  

            //B-3:确保回到落粉站
            AxiStatus = 0; prfPos = 0;
            while ((Math.Abs(PosValue) > Math.Abs(AimPos)) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉轴，第2轴的状态为停止
            {
                PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(2, out prfPos);
                motionMap.GetAxisStatus(2, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
            }

            //(9999)//20220602修改：Z向进给量:上升一个固定高度
            vel = 1;//Z向运动速度为1mm/s
            TrapSpace = /*-*//*k_RYSYSParamAutoPrintParamInTest.m_nLayerThick*/200 / 1000;//20220525新建批注：层厚：调试用150μm//为负方向
            TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
            Thread.Sleep(800);//等待800 ms
        }

        //20220512新建：新的上送粉铺粉逻辑
        //20220512新建：新的上送粉铺粉逻辑
        //20220512新建：新的上送粉铺粉逻辑
        public void NewAutoSupplyPowderThread()//20220512新建：新的上送粉铺粉逻辑
        {
            int nIOState = motionMap.MointoringAxis2(2);//铺粉轴的限位状态//第2轴         
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {
#if false
                //(1)判断是否可以执行自动进给铺粉动作
                int DirFlag = 0;
                UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
                if ((CurrentPos * 0.005 <= 50) && (0 <= CurrentPos * 0.005)) { DirFlag = 1; }//墨车在清洗站台右侧;
                else if ((1180 <= CurrentPos * 0.005) && (CurrentPos * 0.005 <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
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
                        double vel = 1;//Z向运动速度为1mm/s
                        double TrapSpace = -k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / 1000;//20220525新建批注：层厚：调试用150μm//为负方向
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                        Thread.Sleep(800);//等待800 ms
#endif
                        //(1)落 粉站漏斗阀门转3圈-再停止（接粉）：20220512批注
                        double rotateNuM = k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyRotateNum;
                        TrapMoveUp(8, true, "0.5", rotateNuM.ToString()/* "2"*/, true, false);//0.5rev/s速度转2圈
                        Thread.Sleep(300);

                        //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        double AimPos = 695; double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                        BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                        //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                        bool m_startFlag = false;
                        bool m_startLightFlag = false;//开灯标志，确保开灯1次
                        bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次
                        double PosValue = 0;
                        do//检查X轴是否位于指定区域
                        {
                            /*double*/
                            PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置

                            //20220920新建：打开UV或者IR灯
                            if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            {
                                if (UVIRLightFlag == true) { OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/ }
                                else { OpenIRLamp(true);/*打开红外灯*/ }
                                m_startLightFlag = true;
                            }
                            else { }
                            //20220920新建:关闭UV或者IR灯
                            if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            {
                                if (UVIRLightFlag == true) { OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/ }
                                else { OpenIRLamp(false);/*关闭红外灯*/  }
                                m_startLightFlag2 = true;
                            }

                            //开启撒粉动作
                            if (PosValue >= 255 && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）
                            {
                                double DispenseRollerSpeed = 0.5 / (255 / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed);//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;
                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/
                                m_startFlag = true;
                            }
                            else { }
                        }
                        while (PosValue <= 620);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
                        TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位

                        int AxiStatus = 0; double prfPos = 0;
                        while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉轴，第2轴的状态为停止
                        {
                            /*double*/
                            PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(2, out prfPos);
                            motionMap.GetAxisStatus(2, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
                        }

                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = 1;//Z向运动速度为1mm/s
                        TrapSpace = -1500 / 1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                        Thread.Sleep(1000);//等待800 ms

                        #region 监控指令：铺粉拍摄位点3
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 10);//20230113新建且批注：监控发送指令
                        }
                        #endregion


                        //(2)洒粉车回到落粉站位置（回站）：20220512批注
                        PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        double PowderStationCorrection = k_RYSYSParamAutoPrintParamInTest.m_dPowderStationCorrection;
                        AimPos = 1 - PowderStationCorrection;
                        MovSpeed = 125/*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/;//更新值到本地变量//回程速度125mm/s
                        BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

                        //(3)撒粉轴找回零位：20220527新建：
                        double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition/*Convert.ToDouble(textBox22.Text)*/;
                        bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition);//旋转速度：0.5 圈/s
                        if (ReturnCode == true)//校准成功
                        {
                            //MessageBox.Show("回零成功");//成功执行不需要额外的反馈
                        }
                        else { MessageBox.Show("回零失败"); }

                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = 1;//Z向运动速度为1mm/s
                        TrapSpace = 1500 / 1000;//20220525新建批注：层厚：调试用150μm//为负方向
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                        Thread.Sleep(1000);//等待800 ms

                        #region 监控指令：铺粉拍摄位点5
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[11])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 12);//20230113新建且批注：监控发送指令
                        }
                        #endregion
                    }
                    else {}

                }
                else
                { MessageBox.Show("墨车不在正常停靠区间"); }

                #region 监控发送指令//20230113新建且批注：
                sendMessageToCamera.Dispose(); //20230113新建且批注：监控发送指令
                #endregion
            }
        }
        public void NewAutoSupplyPowderThread2(ref SendMessageToCamera toCamera, int RecordLayerIndex, int RecordProcessIndex)//20220512新建：新的上送粉铺粉逻辑
        {
            int nIOState = motionMap.MointoringAxis2(2);//铺粉轴的限位状态//第2轴         
            if ((0 != (nIOState & 0x20)) || (0 != (nIOState & 0x40)))//粉车位于负限位报警区
            {
                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {
#if false
                //(1)判断是否可以执行自动进给铺粉动作
                int DirFlag = 0;
                UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
                if ((CurrentPos * 0.005 <= 50) && (0 <= CurrentPos * 0.005)) { DirFlag = 1; }//墨车在清洗站台右侧;
                else if ((1180 <= CurrentPos * 0.005) && (CurrentPos * 0.005 <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                else { DirFlag = 3; }
#endif

                if (true/*(DirFlag == 1) || (DirFlag == 2)*/)//墨车在非安全区域++粉车在正常工作区间内==粉末在正负限位区间内
                {
                    if (true/*0 == k_RYSYSParamAutoPrintParamInTest.m_nRecoaterStrategy*/)//20220512新建批注：新设备只需要使用直接铺粉逻辑即可//(2)直接铺粉方式：20210125新增
                    {
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
                        double vel = 1;//Z向运动速度为1mm/s
                        double TrapSpace = -k_RYSYSParamAutoPrintParamInTest.m_nLayerThick / 1000;//20220525新建批注：层厚：调试用150μm//为负方向
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                        Thread.Sleep(800);//等待800 ms
#endif
                        //(1)落 粉站漏斗阀门转3圈-再停止（接粉）：20220512批注
                        double rotateNuM = k_RYSYSParamAutoPrintParamInTest.m_dPowderSupplyRotateNum;
                        TrapMoveUp(8, true, "0.5", rotateNuM.ToString()/* "2"*/, true, false);//0.5rev/s速度转2圈
                        Thread.Sleep(300);

                        //(2)洒粉车覆盖打印区域-过程中依次开铺粉辊及洒粉轴（移动-辊粉-撒粉）：20220512批注
                        //(A-B: 移动-辊粉)//20220919测量：辊子直径 25MM,原来的40MM(记忆中)
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        double AimPos = 695; double MovSpeed = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed; //更新值到本地变量
                        BackToStation2(AimPos, MovSpeed, false);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为不等停

                        //C: 撒粉-指定区域内开启撒粉;具体策略1：先用迅速方式落粉 策略2：用插补模式落粉；暂时使用策略1
                        bool m_startFlag = false;
                        bool m_startLightFlag = false;//开灯标志，确保开灯1次
                        bool m_startLightFlag2 = false;//20220920新增：关灯标志，确保关灯1次
                        double PosValue = 0;
                        do//检查X轴是否位于指定区域
                        {
                            /*double*/
                            PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置

                            //20220920新建：打开UV或者IR灯
                            if (PosValue >= (255 - LightSourceOffset/*70*/) && m_startLightFlag == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            {
                                if (UVIRLightFlag == true) { OpenUVLamp(true, 0, 1000, 0, 1000);/*开启UV灯*/ }
                                else { OpenIRLamp(true);/*打开红外灯*/ }
                                m_startLightFlag = true;
                            }
                            else { }
                            //20220920新建:关闭UV或者IR灯
                            if (PosValue > (620 - LightSourceOffset/*-200*//*70*/) && m_startLightFlag2 == false)//开启UV灯及IR灯：UV灯距离落粉中心位置70MM,IR灯距离落粉中心位置为110MM
                            {
                                if (UVIRLightFlag == true) { OpenUVLamp(false, 0, 1000, 0, 1000);/*关闭UV灯*/ }
                                else { OpenIRLamp(false);/*关闭红外灯*/  }
                                m_startLightFlag2 = true;
                            }

                            //开启撒粉动作
                            if (PosValue >= 255 && m_startFlag == false)//开启扫粉轴：先匀速转半圈（策略1）
                            {
                                double DispenseRollerSpeed = 0.5 / (255 / k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed);//20220512新建批注：有效区域宽度为460MM;起始打印位置：255MM;
                                TrapMoveUp(3, true, Convert.ToString(DispenseRollerSpeed)/*"0.25"*/, "0.5"/*Convert.ToString(TrapSpace)*/, true, true);//20220512批注：此处不同于默认，为不等停/*(2)铺粉车移动到手动填粉位置;//30mm位置处*/
                                m_startFlag = true;
                            }
                            else { }
                        }
                        while (PosValue <= 620);//20220512新建批注：有效区域宽度为360MM;起始打印位置：255MM;终止洒粉位置620MM
                        TrapMoveUp(3, true, "2", "0.5"/*Convert.ToString(TrapSpace)*/, true, false);//20220512新建：转完剩余的圈数，回到其轴的零位

                        int AxiStatus = 0; double prfPos = 0;
                        while ((Math.Abs(PosValue) < Math.Abs(695)/*Math.Abs(prfPos) < Math.Abs(695*1000)*/) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20220512新建：等待铺粉轴，第2轴的状态为停止
                        {
                            /*double*/
                            PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置 //motionMap.GetPrfPos(2, out prfPos);
                            motionMap.GetAxisStatus(2, out AxiStatus); //封装：mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
                        }

                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = 1;//Z向运动速度为1mm/s
                        TrapSpace = -1500 / 1000;//20220525新建批注：层厚：调试用150μm//为负方向//下降1500μm
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                        Thread.Sleep(1000);//等待800 ms

                        #region 监控指令：铺粉拍摄位点3
                        if (toCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[9])
                        {
                            toCamera.SendMessageFromSharedMemory(false, RecordLayerIndex, RecordProcessIndex);//20230113新建且批注：监控发送指令
                        }
                        #endregion

                        //(2)洒粉车回到落粉站位置（回站）：20220512批注
                        PosValue = GetCurrentPos(2);//20220520新建：查询实时铺粉车位置
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackRollerSpeed;//201029批注：更新辊子速度
                        double PowderStationCorrection = k_RYSYSParamAutoPrintParamInTest.m_dPowderStationCorrection;
                        AimPos = 1 - PowderStationCorrection;
                        MovSpeed = 125/*k_RYSYSParamAutoPrintParamInTest.m_dPowderCarBackSpeed*/;//更新值到本地变量//回程速度125mm/s
                        BackToStation2(AimPos, MovSpeed, true);//20220520新建：单位为MM//此处：true为的等停，false为不等停//此处为等停
                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速 //进入下一打印环节；等待继续铺

                        //(3)撒粉轴找回零位：20220527新建：
                        double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition/*Convert.ToDouble(textBox22.Text)*/;
                        bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition);//旋转速度：0.5 圈/s
                        if (ReturnCode == true)//校准成功
                        {
                            //MessageBox.Show("回零成功");//成功执行不需要额外的反馈
                        }
                        else { MessageBox.Show("回零失败"); }

                        //20220915新增：铺粉完成 下降一段距离，避免回程压碎
                        vel = 1;//Z向运动速度为1mm/s
                        TrapSpace = 1500 / 1000;//20220525新建批注：层厚：调试用150μm//为负方向
                        TrapMoveUp(1, true, Convert.ToString(vel), Convert.ToString(TrapSpace), true, true/*!WaitStopFLag*//*true*/);//20200520批注：铺粉车移动到指定位置;//不同于默认，为不等停
                        Thread.Sleep(1000);//等待800 ms
                    }
                    else { }
                }
                else
                { MessageBox.Show("墨车不在正常停靠区间"); }
            }
        }



        public void AutoSupplyPowderThread()//自动进给铺粉动作
        {
            int nIOState = motionMap.MointoringAxis2(4);//铺粉轴的限位状态            
            if ((0 != (nIOState & 0x20))||(0!=(nIOState & 0x40)))//粉车位于负限位报警区
            {
                MessageBox.Show("粉车不在正常停靠区间");
            }
            else//粉车位于正常停靠区
            {
                //(1)判断是否可以执行自动进给铺粉动作
                int DirFlag = 0;
                UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
                if ((CurrentPos * 0.005 <= 50) && (0 <= CurrentPos * 0.005)) { DirFlag = 1; }//墨车在清洗站台右侧;
                else if ((1180 <= CurrentPos * 0.005) && (CurrentPos * 0.005 <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
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

                        RollerParam = k_RYSYSParamAutoPrintParamInTest.m_dRollerSpeed;//201029批注：复位辊子速度为回铺辊速
                                                                                      //进入下一打印环节；等待继续铺
                    }
                    else {}
#endif
                }
                else
                { MessageBox.Show("墨车不在正常停靠区间"); }
            }
        }
  
        public void AutoPrintThread()//20220513新建:自动喷墨运动动作
        {
            #region 监控发送指令//20230113新建且批注：
            SendMessageToCamera sendMessageToCamera = new SendMessageToCamera(false);//20200202修改
            //sendMessageToCamera.LoadJsonFile();
            //sendMessageToCamera.SendMessageFromSharedMemory(tempStartMode,10,13);//20230113新建且批注：监控发送指令
            //sendMessageToCamera.Dispose();//20230113新建且批注：监控发送指令
            #endregion

            #region 监控指令：喷墨拍摄位点1
            sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
            if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[0])
            {
                sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 1);//20230113新建且批注：监控发送指令
            }
            #endregion

            double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/ double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
            for (int i = 0; i < 6; i++)//i为PASS序号；PASS宽度为喷头宽度
            {
                switch (i)
                {
                    case 0:
                        BackToStation(10, (float)ReturnVelocity1, true, false/*true*/);//停靠在里侧，向外侧步进喷头幅面
                        BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------
                        BackToStation(10 + PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面

                        #region 监控指令：喷墨拍摄位点2
                        sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[1])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 2);//20230113新建且批注：监控发送指令
                        }
                        #endregion

                        break;
                    case 1:
                        BackToStation(425, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面--------------->
                        BackToStation(10 + 2 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面

                        #region 监控指令：喷墨拍摄位点3
                        sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[2])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 3);//20230113新建且批注：监控发送指令
                        }
                        #endregion

                        break;
                    case 2:
                        BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------
                        BackToStation(10 + 3 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面

                        #region 监控指令：喷墨拍摄位点4
                        sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[3])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 4);//20230113新建且批注：监控发送指令
                        }
                        #endregion

                        break;
                    case 3:
                        BackToStation(425, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面--------------->
                        BackToStation(10 + 4 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面

                        #region 监控指令：喷墨拍摄位点5
                        sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[4])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 5);//20230113新建且批注：监控发送指令
                        }
                        #endregion

                        break;
                    //case 4:
                    //    BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------                        
                    //                                                           //BackToStation(25 + 5 *PrintHeadWidth, (float)ReturnVelocity1,true);//停靠在里侧，向外侧步进喷头幅面

                    case 4:
                        BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------
                        BackToStation(15 + 5 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面

                        #region 监控指令：喷墨拍摄位点6
                        sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
                        if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[5])
                        {
                            sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 6);//20230113新建且批注：监控发送指令
                        }
                        #endregion

                        break;
                    case 5://第6Pass
                        BackToStation(425, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面--------------->
                        //BackToStation(25 + 5 *PrintHeadWidth, (float)ReturnVelocity1,true);//停靠在里侧，向外侧步进喷头幅面

                        break;
                }
            }
            BackToStation(710/*735*/, (float)ReturnVelocity1, false, false);//回到原点：X=50MM处<---------------//20220520新建：位置修改为780MM处，清洗工作位
            BackToStation(96 + 25/*25*/, (float)ReturnVelocity1, true, false);//回到原点：Y=50MM处//20220520新建：位置修改为96MM处，清洗工作位//20220525修改：补偿25
            WaitStop(1);//20220520新建：等停墨车第1轴：X方向//外部等停
            WaitStop(2);//20220520新建：//等停墨车第2轴：Y方向//外部等停

            #region 监控指令：喷墨拍摄位点7
            sendMessageToCamera.LoadJsonFile();//20230113新建且批注：更新监控情况
            if (sendMessageToCamera.k_MonitorPrintParam.m_anJettingBinderBedMonitorFlags[6])
            {
                sendMessageToCamera.SendMessageFromSharedMemory(false, 0, 7);//20230113新建且批注：监控发送指令
            }
            #endregion

            #region 监控发送指令//20230113新建且批注：
            sendMessageToCamera.Dispose(); //20230113新建且批注：监控发送指令
            #endregion
        }
        public void AutoPrintThread2(int Command, int PassIndex,float m_MovSpeed)//20220513新建:自动喷墨运动动作
        {
            if (Command == 0) {}
            else if (Command == 1)//第2代设备的打印PASS总数为6
            {
                double PrintWidth = 350;/*宽度值设为350MM*/ double PrintHeadWidth = 54;/*宽度值设为5MM*/ double ReturnVelocity1 = m_szMovSpeed/*20*/;//喷墨移动速度
                ReturnVelocity1 = m_MovSpeed;
                { 
                    switch (PassIndex)
                    {
                        case 0:
                            //20220920新增：在第1PASS打印运动之前，需要关闭闪喷：否则会导致打印乱码
                            bool nRetVal = royal.royal.IDP_FlashPrtCtl(false);//关闭闪喷

                            BackToStation(15, (float)ReturnVelocity1, true, false/*true*/);//停靠在里侧，向外侧步进喷头幅面
                            BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面

                            break;
                        case 1:
                            BackToStation(425, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 2 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面
                            break;
                        case 2:
                            BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 3 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面
                            break;
                        case 3:
                            BackToStation(425, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面--------------->
                            BackToStation(15 + 4 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面
                            break;
                        case 4:
                            BackToStation(25, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------
                            BackToStation(15 + 5 * PrintHeadWidth, (float)ReturnVelocity1, true, true);//停靠在里侧，向外侧步进喷头幅面
                            break;
                        case 5://第6Pass
                            BackToStation(425, (float)ReturnVelocity1, false, true);//停靠在右侧，向左侧运动打印幅面<---------------
                            //BackToStation(25 + 5 *PrintHeadWidth, (float)ReturnVelocity1,true);//停靠在里侧，向外侧步进喷头幅面

                            BackToStation(710/*735*/, (float)ReturnVelocity1, false, false);//回到原点：X=50MM处<---------------//20220520新建：位置修改为780MM处，清洗工作位
                            BackToStation(96+25/*25*/, (float)ReturnVelocity1, true, false);//回到原点：Y=50MM处//20220520新建：位置修改为96MM处，清洗工作位//20220525修改：补偿25
                            WaitStop(1);//20220520新建：等停墨车第1轴：X方向//外部等停
                            WaitStop(2);//20220520新建：//等停墨车第2轴：Y方向//外部等停

                            //20220920新增：在第5PASS打印运动之后，需要开启闪喷：否则会因为胶水的流动性问题，导致打印不连续
                            bool nRetVal2 = royal.royal.IDP_FlashPrtCtl(true);//打开闪喷

                            break;
                    }
                }
            }
            else { }
        }
        private bool CreateAndDeleteThread(string AimThreadName, List<Thread> AutoPrintThreadsPool,bool OpenCloseFlag)
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
                    ThreadStart initThreadEntry=null;//20220520修改：从你先弄按照新设备UI形式，进行修改
                    //if (tempThreadName == "AutoCleanThread") { initThreadEntry = new ThreadStart(AutoCleanThread); }//20200220:线程入口方法修改为联动线程
                    /*else */if (tempThreadName == "AutoSupplyPowderThread") { initThreadEntry = new ThreadStart(AutoSupplyPowderThread); }//20200220:线程入口方法修改为联动线程
                    else if (tempThreadName == "NewAutoSupplyPowderThread") { initThreadEntry = new ThreadStart(NewAutoSupplyPowderThread); }
                    else if (tempThreadName == "AutoPrintThread") { initThreadEntry = new ThreadStart(AutoPrintThread); }
                    else if (tempThreadName == "AutoCleanThread") { initThreadEntry = new ThreadStart(AutoCleanThread); }
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
        bool[] AutoPrintFlag = new bool[4/*3*/] { false,false,false,false};//20220521新建：添加自动固化逻辑执行标志位

        private void AutoCureBtn_Click(object sender, EventArgs e)//20220521实现：自动固化逻辑
        {
            //20220521新建：新设备自动固化逻辑
            if (AutoPrintFlag[3] == false)//自动固化运动标志位
            {
                if (InkCarHomeFlag == true && PowderCarHomeFlag == true)//确保：墨车回零标志位；确保在指定区间，否则报错
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
                    MessageBox.Show("墨车系统未回零");
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
            }
        }
        private void AutoCleanBtn_Click(object sender, EventArgs e)//20201020新增：自动喷胶固化，便捷调试功能//20220518新建：修改为自动清洗喷头运动
        {
            //20220520新建：新设备清洗运动逻辑
            if (AutoPrintFlag[0] == false)//自动清洗运动标志位//20220520新建：是否已有自动清洗逻辑在运行————》开启自动打印逻辑
            {
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
                {  MessageBox.Show("墨车系统未回零"); } 
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
            }
        }

        private void AutoSupplyPowderBtn_Click(object sender, EventArgs e)//20201020新增：自动进给铺粉，便捷调试功能
        {
#if true//20220512批注：新设备铺粉逻辑
            if (AutoPrintFlag[1] == false)
            {
                if (true/*InkCarHomeFlag == true*/)//确保：墨车系统回零成功；确保在指定区间，否则报错
                {
                    //int DirFlag = 0;
                    //UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
                    //double PosValue = CurrentPos * 0.005;

                    //if ((CurrentPos * 0.005 <= 50) && (0 <= CurrentPos * 0.005)) { DirFlag = 1; }//墨车在清洗站台右侧;
                    //else if ((1180 <= CurrentPos * 0.005) && (CurrentPos * 0.005 <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
                    //else { DirFlag = 3; }

                    //string PositonText = "墨车: " + PosValue.ToString("F2") + " MM";
                    //InkCarPosLable.Text = PositonText;

                    if (true/*DirFlag == 1 || DirFlag == 2*/)
                    {
                        if (true == CreateAndDeleteThread("NewAutoSupplyPowderThread"/*"AutoSupplyPowderThread"*/, AutoPrintThreads, true)) { AutoPrintFlag[1] = true; this.AutoSupplyPowderBtn.Text = "停止进给铺粉"; }//开启
                        else { AutoPrintFlag[1] = false; this.AutoSupplyPowderBtn.Text = "自动进给铺粉"; }       
                    }
                    else
                    { MessageBox.Show("墨车未停靠在安全区"); }
                }
                else
                { MessageBox.Show("墨车系统未回零"); }
            }
            else
            {
                if (true == CreateAndDeleteThread("AutoSupplyPowderThread", AutoPrintThreads, false))
                {
                    AutoPrintFlag[1] = false;
                    motionMap.StopMotion(2);//停止铺粉轴运动         
                    motionMap.StopMotion(8);//停止铺粉轴运动
                    motionMap.StopMotion(3);//停止铺粉轴运动
                    motionMap.StopMotion(6);//停止铺粉轴运动
                    this.AutoSupplyPowderBtn.Text = "自动进给铺粉";
                }//关闭
                else { AutoPrintFlag[1] = true; this.AutoSupplyPowderBtn.Text = "停止进给铺粉"; }
            }
#else//20220512批注：旧有逻辑
            if (AutoPrintFlag[1] == false)
            {
                if (InkCarHomeFlag == true)//确保：墨车系统回零成功；确保在指定区间，否则报错
                {
                    int DirFlag = 0;
                    UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
                    double PosValue = CurrentPos * 0.005;

                    if ((CurrentPos * 0.005 <= 50) && (0 <= CurrentPos * 0.005)) { DirFlag = 1; }//墨车在清洗站台右侧;
                    else if ((1180 <= CurrentPos * 0.005) && (CurrentPos * 0.005 <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
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
                if (InkCarHomeFlag == true)//确保：墨车系统回零成功；确保在指定区间，否则报错
                {
#if true//20220513注释：新设备时使用此简化逻辑
                    if (true == CreateAndDeleteThread("AutoPrintThread", AutoPrintThreads, true)) { AutoPrintFlag[2] = true; this.AutoPrintBtn.Text = "停止喷墨运动"; }//开启
                    else { AutoPrintFlag[2] = false; this.AutoPrintBtn.Text = "自动喷墨运动"; }
#else//20220513注释：第一代设备时使用此逻辑
                    int DirFlag = 0;
                    UInt32 CurrentPos = royal.royal.DEV_GetPrintEncoderValue();//初始编码器位置：
                    double PosValue = CurrentPos * 0.005;
                    if ((CurrentPos * 0.005 <= 50) && (0 <= CurrentPos * 0.005)) { DirFlag = 1; }//墨车在清洗站台右侧;
                    else if ((1180 <= CurrentPos * 0.005) && (CurrentPos * 0.005 <= 1230)) { DirFlag = 2; }//墨车在清洗站台左侧
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
                {   MessageBox.Show("墨车系统未回零"); }
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
            }
        }

        //20201020新建：textbox数据绑定
        public AutoPrintParamInTest k_RYSYSParamAutoPrintParamInTest/*=new AutoPrintParamInTest()*/;//20210304修改：
        public void InitKidFormWithAutoPrintParamInTest()//20201020新建：数据绑定自动固化清洗-自动进给铺粉-自动铺粉续打
        {
            //铺粉策略类型:20210124新增
            comboBox3.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "RecoaterStrategy", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //固化策略类型：20220523新增
            comboBox1.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "CureLightStrategy", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //辊子方向校准：20220527新增：便于修正辊子方向
            comboBox2.DataBindings.Add("SelectedIndex", k_RYSYSParamAutoPrintParamInTest, "RollerRotateDirection", true, DataSourceUpdateMode.OnPropertyChanged);//辊子方向校准：20220527新增：便于修正辊子方向


            // 喷头保护设置参数：(清洗和闪喷两种作用)
            textBox5.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "LayerThick", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox13.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "LayerThick2", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//201029新增:
            textBox24.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderStationCorrection", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220528新增：单位MM
            textBox25.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSupplyRotateNum", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220528新增：单位圈


            textBox6.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CureBackSpeed", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);

            textBox11.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "CleanFrequency", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox10.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "RePrintTimes", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
                     
            textBox8.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarStartposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox9.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarStayposition", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox2.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarHomeposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220512新建：墨车HOME值设置，此值需考虑实际的光电HOME传感器的物理位置

            textBox22.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSpreaderHomeposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220526新建：粉末Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置
            textBox23.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "InkSpreaderHomeposition", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220526新建：墨车Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置



            //IR固化功率：20220523新增：
            textBox19.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "IRPowerPercentage", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20220512新建：墨车HOME值设置，此值需考虑实际的光电HOME传感器的物理位置


            velLabel3.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox4.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox7.DataBindings.Add("Text", k_RYSYSParamAutoPrintParamInTest, "PowderCarBackSpeed", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);

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
                        if (modbusCommunicateMap.serialPort.IsOpen==true)//存在即关闭串口
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
                    }
                    else//关闭Modbus线程
                    {
                        OpenCloseState = false;//打开状态标记
                        modbusCommunicateMap.master.Dispose();
                        modbusCommunicateMap.serialPort.Dispose();
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
            if (OutputState == true)//MessageBox.Show("红外仪表打开");
            {
                //MessageBox.Show("红外仪表打开");
                if (InitModbusFlag == false)
                {
                    ////开启Modbus通讯线程:
                    //string tempThreadName = "ModbusThread";
                    //Thread tempThread = EncoderResetThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
                    //if (tempThread != null)
                    //{
                    //    EncoderResetThreads.Remove(tempThread);//以防万一
                    //}
                    //else
                    bool returnFlag = modbusCommunicateMap.CreateQuitModbusCommunication(true);//UI线程内建立通讯
                                                                                                  //ThreadStart initThreadEntry = new ThreadStart(ModbusThread);//20200220:线程入口方法修改为联动线程
                                                                                                  //tempThread = new Thread(initThreadEntry) { IsBackground = true };
                                                                                                  //tempThread.Name = tempThreadName;
                                                                                                  //tempThread.Start();
                                                                                                  //EncoderResetThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程

                    //returnFlag = WaitModbusResponse(20);//等待2 ms,等待创建成功的结果//避免阻塞//精华
                    if (returnFlag == true)//一打开Modbus线程
                    {
                        InitModbusFlag = true; this.ModbusBtn.Text = "关闭通讯";
                    }
                    else//关闭Modbus线程
                    {
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

                    modbusCommunicateMap.CreateQuitModbusCommunication(false);//建立通讯
                    InitModbusFlag = false;
                    this.ModbusBtn.Text = "建立通讯";
                }
            }
            else
            {
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

                    //关闭输出
                    RegisterAddress = 121/*2121*//*121*/; RegisterValue = Convert.ToInt32(0 * 100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40122----MV
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
                MessageBox.Show("红外仪表关闭状态");
            }
        }
        private void button26_Click(object sender, EventArgs e)
        {
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

                    //ushort[] CurrentTemperature = modbusCommunicateMap.ReadHoldingRegisters((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber);//读取30001的内部计算值
                    //设置功率为50%
                    RegisterAddress = 121/*2121*//*121*/; RegisterValue = Convert.ToInt32(Convert.ToDouble(textBox19.Text)*100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40122----MV
#if false
                    //设置温度为75℃
                    RegisterAddress = 2002; RegisterValue = Convert.ToInt32(textBox16.Text)/*1875*/; //RegisterAddress = 2; RegisterValue = 1875;
                    modbusCommunicateMap.WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40003----SV
#endif
                }
            }
            else
            {
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
            if (OutputState==true)
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
                (sender as Control).Text = "关闭\r\n压墨";
            }
            //(c)计算输出
            if (m_bPushInkFlag == false)//打开和关闭闪喷：
            {
                //（1）开压墨泵：
                motionMap.SetDo(13, false);//20220525新建：压墨//压墨输出端口为第13口//打开压墨泵
                m_bPushInkFlag = true;
            }
            else
            {
                //（1）关压墨泵
                motionMap.SetDo(13, true);//20220525新建：压墨//压墨输出端口为第13口//关闭压墨泵
                m_bPushInkFlag = false;
            }
        }

        private void SpreaderHomeBtn_Click(object sender, EventArgs e)
        {
            double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dPowderSpreaderHomeposition;//撒粉轴的HOME位置
            bool ReturnCode = motionMap.SetBackSpreaderAxis(3, 0.5, 2, -SinkPosition);//旋转速度：0.5 圈/s
            if (ReturnCode == true)//校准成功
            {
                MessageBox.Show("回零成功");      
            }
            else { MessageBox.Show("回零失败"); }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            double SinkPosition = k_RYSYSParamAutoPrintParamInTest.m_dInkSpreaderHomeposition;//刮墨轴的HOME位置
            bool ReturnCode = motionMap.SetBackSpreaderAxis(4, 1/*0.5*/, 2, -SinkPosition);//旋转速度：0.5 圈/s////20220919修正：长时间运行，低速导致刮墨轴容易卡死：修正为1圈/s
            if (ReturnCode == true)//校准成功
            {
                MessageBox.Show("回零成功");
            }
            else { MessageBox.Show("回零失败"); }
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
        public float m_fAirHold = 0.16f/*20.0f*//*"0.2"*/;//负压阈值
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
        public int m_nCureLightStrategy = 0;//固化策略类型：默认0为UV固化方式，1为IR固化方式
        public int m_nRollerRotateDirection = 0;//辊子方向校准：20220527新增：便于修正辊子方向

        public int m_nLayerThick = 150;//层厚进给（um）//20220525新建：默认铺粉层厚为150μm
        public int m_nLayerThick2 = 300;//层厚进给2（um）

        public double m_dPowderStationCorrection = 2.5;//20220528新增：单位MM
        public double m_dPowderSupplyRotateNum = 1;//20220528新增：单位圈


        public double m_dCureBackSpeed = 150;//固化回程速度（mm/s）//20210621修改：
        public int m_nCleanFrequency = 25;//清洗频率（层）//20210201新建批注：优化工艺参数为25层清洗1次，较为合适
        public double m_dCleanSparkTime = 5;//清洗闪喷时长（s）//20210201新建批注：5s的清洗闪喷后处理较为合适，较为合适
        public int m_nRePrintTimes = 1;//重喷次数（次）////20210201新建批注：1次的重喷参数匹配合适的打印速度及数据精度，打印的效果，较为合适

        public /*int*/double /*m_nCureFrequency*/m_dCureFrequency = 125;//工作频率（Hz）//2021.615修改为double
        public double m_dDutyRatio = 100;//占空比（%）//20210621修改:由m_dCurePower修改为m_dDutyRatio
        public int m_nCureEnergyDensity = 1600;//20210621新增:UV灯Cure能量密度：取值为0mJ-25mJ-50mJ-100mJ-200mJ-400mJ-800mJ-1600mJ-3200mJ


        public double m_dLeftCureOn = 550;//左灯开启（mm）
        public double m_dLeftCureOff = 1000;//左灯开启（mm）
        public double m_dRightCureOn = 270;//左灯开启（mm）
        public double m_dRightCureOff = 720;//左灯开启（mm）

        public double m_dPowderSpeed = 40;//铺粉速度（mm/s）
        public double m_dRollerSpeed = 1;//滚动速度（mm/s）
        public double m_dPowderCarStartposition = 100;//铺粉起铺位置
        public double m_dPowderCarStayposition = 5;//铺粉停靠位置
        public double m_dPowderCarBackSpeed = 40;//铺粉车复位速度（mm/s）
        public double m_dPowderCarBackRollerSpeed = 1;//铺粉车复位滚动速度（REV/s）
        public double m_dPowderCarHomeposition = 112/*5*/;//20220512新建：光电HOME传感器物理位置//20220521修改：铺粉回零位修改为112MM
        public double m_dPowderSpreaderHomeposition = 40/*5*/;//20220526新建：粉末Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置，单位度（°）
        public double m_dInkSpreaderHomeposition = 40/*5*/;//20220526新建：墨车Spreader HOME值设置，此值需考虑实际的光电HOME传感器的物理位置，单位度（°）

        public double m_dIRPowerPercentage = 20;//20220523新建：默认IR功率为20%


        public double m_dPowderSupplyCoefficient = 1;//供粉系数，供粉缸进给量相比成形缸的进给量的倍数：201030新增
        
        //自动铺粉策略类型:20210124新增
        public int RecoaterStrategy//
        {
            get { return this.m_nRecoaterStrategy; }
            set { if (value != this.m_nRecoaterStrategy) { this.m_nRecoaterStrategy = value; NotifyPropertyChanged(); } }
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
            set { if (value != this.m_dPowderSupplyRotateNum) { this.m_dPowderSupplyRotateNum = value; NotifyPropertyChanged(); } }
        }

        public double CureBackSpeed//固化回程速度（mm/s）//20210621修改：
        {
            get { return this.m_dCureBackSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set
            {
                if (value != this.m_dCureBackSpeed && (0 <= value && value <= 200))
                {
                    this.m_dCureBackSpeed = value; NotifyPropertyChanged();
                }
                else if (value > 200)
                {
                    this.m_dCureBackSpeed = 200; NotifyPropertyChanged();
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

        public int RePrintTimes//固化速度（mm/s）
        {
            get { return this.m_nRePrintTimes; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nRePrintTimes) { this.m_nRePrintTimes = value; NotifyPropertyChanged(); } }
        }
        public double DutyRatio//占空比（%）//20210621修改:由CurePower修改为DutyRatio
        {
            get/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            {
                return this.m_dDutyRatio;
            }
            set
            {
                if (value != this.m_dDutyRatio && (0<=value && value<=100))
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
}

