using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BinderJetting
{
    public partial class JOB参数设置 : Form
    {
        public LaserADD_BinderJetter.PowderLayerParam m_mPowderLayerParam;//m的含义是映射mapping:020104

        public APrintStategy a_layerStategy = new APrintStategy();//单策略;1项打印策略
        private LayerStategyContent m_mlayerStategyContent;//单层策略：层参数设计:20200806新增

        //public List<APrintStategy> layerStategyContentss = new List<APrintStategy>();//打印策略内容
        public JOB参数设置(LaserADD_BinderJetter.PowderLayerParam m_cPowderLayerParam, bool PrintJobExistedFlag)
        {
            InitializeComponent();
            //InitKidFormWithSysparam();//20200225：
            k_RYSYSParam = new RYSYSParam();//20200225：
            //20200202新增:
            m_mPowderLayerParam = m_cPowderLayerParam;
            this.MoveParamPropertyGrid.SelectedObject = m_mPowderLayerParam;

            m_mlayerStategyContent = new LayerStategyContent();

            this.listView1.View = View.Details;
            this.listView1.FullRowSelect = true;
            this.listView1.BackColor = Color.LightYellow;
            listView1.Columns.Add("参数号", 45);
            listView1.Columns.Add("层(起始)", 58);
            listView1.Columns.Add("层(结束)", 58);
            listView1.Columns.Add("自动续打", 60);
            listView1.Columns.Add("需开铺粉", 60);
            listView1.Columns.Add("需开UV", 70);
            listView1.Columns.Add("层厚", 50);
            listView1.Columns.Add("UV功率", 50);
            listView1.Columns.Add("波形号", 50);
            listView1.LabelEdit = false;//允许ListView处于可编辑状态
            ListViewBinding(a_layerStategy.layerStategyContents);

            listView2.Columns.Add("策略", 25);
            listView2.Columns.Add("名称", 150);
            listView2.LabelEdit = false;//允许ListView处于可编辑状态
            //ListView2Binding(PrintStrategys);

            if (PrintJobExistedFlag == false) //不存在打印任务
            { }
            else
            {
                GrayScaleBox.Enabled = false;
                GrayValueBox.Enabled = false;
                XDpiBox.Enabled = false;
                textBox9.Enabled = false;
                textBox1.Enabled = false;
                textBox10.Enabled = false;
                textBox8.Enabled = false;
                textBox37.Enabled = false;
                textBox27.Enabled = false;//20230321新增：
                checkBox10.Enabled = false;
                comboBox2.Enabled = false;//20230320新增：
            }
        }


        public PrintStrategys PrintStrategys = new PrintStrategys();//总策略：包含所有的策略

        /// <summary>
        /// 20200807新增：更新ListView1控件值：
        /// </summary>
        public void ListView2Binding(PrintStrategys PrintStrategys)
        {
            listView2.Items.Clear();
            this.SelectedItemComboBox.Items.Clear();//修改打印参数选中Combox
            for (int i = 0; i < PrintStrategys.m_PrintStrategys.Count; i++)
            {
                SelectedItemComboBox.Items.Add(PrintStrategys.m_PrintStrategys[i].Name);

                ListViewItem list_item = new ListViewItem();
                APrintStategy model = PrintStrategys.m_PrintStrategys[i];//实例一个实体对象=list中的实体对象
                list_item.Text = (i + 1) + "号";

                string tempStringItem = model.Name;
                list_item.SubItems.Add(tempStringItem/*(model.b_AutoPrintNextlayer).ToString()*/);//xx为相应属性

                listView2.Items.Add(list_item);//将设置好的listiem添加到items中
            }

            this.Refresh();

            try
            {
                //20200807新建：显示默认的打印策略
                string SelectedName = PrintStrategys.m_PrintStrategys[PrintStrategys.SelectedAPrintStategyItem].Name;
                SelectedItemComboBox.SelectedIndex = SelectedItemComboBox.FindStringExact(SelectedName);
            }
            catch (Exception) { }

        }

        /// <summary>
        /// 20200806新增：更新ListView1控件值：
        /// </summary>
        public void ListViewBinding(
            List<LayerStategyContent> layerStategyContents)
        {
            listView1.Items.Clear();
            for (int i = 0; i < layerStategyContents.Count; i++)
            {
                ListViewItem list_item = new ListViewItem();
                LayerStategyContent model = layerStategyContents[i];//实例一个实体对象=list中的实体对象
                list_item.Text = "参数" + (i + 1);
                list_item.SubItems.Add((model.b_LayerIndex).ToString() + "层");
                list_item.SubItems.Add((model.b_LayerEnd).ToString() + "层");

                string tempStringItem = null;

                if (model.b_AutoPrintNextlayer == 0) { tempStringItem = "自动续打"; }
                else { tempStringItem = "不自动"; }
                list_item.SubItems.Add(tempStringItem/*(model.b_LayerInherit).ToString()*/);

                if (model.b_NeedPowder == 0) { tempStringItem = "开铺粉"; }
                else { tempStringItem = "不开铺粉"; }
                list_item.SubItems.Add(tempStringItem/*(model.b_NeedPowder).ToString()*/);

                if (model.b_NeedUV == 0) { tempStringItem = "开UV固化"; }
                else { tempStringItem = "不开固化"; }
                list_item.SubItems.Add(tempStringItem/*(model.b_NeedUV).ToString()*/);

                list_item.SubItems.Add((model.m_dPowderThick).ToString() + "um");
                list_item.SubItems.Add((model.m_dUVPower).ToString() + "%");
                list_item.SubItems.Add((model.m_nWaveType).ToString() + "号");
                listView1.Items.Add(list_item);//将设置好的listiem添加到items中
            }
            this.Refresh();
        }


        private void listView2_MouseClick(object sender, MouseEventArgs e)
        {
            //此处存在问题。如果双击直接操作，就奔溃了。是因为，此时没有选中正确的index。
            if (listView2.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                //(1)排序容器：从大到小排序生成所需删除次序
                int[] SortListContain = new int[listView2.SelectedItems.Count];//记录待删除项的索引号
                for (int j = 0; j < this.listView2.SelectedItems.Count; j++)
                {
                    SortListContain[j] = listView2.SelectedItems[j].Index;
                }

                //(2)进行排序工作：从大到小排序生成所需删除次序
                for (int i = 0; i < SortListContain.Length - 1; i++) //总共要比较的趟数
                {
                    for (int j = 0; j < SortListContain.Length - 1 - i; j++) //每趟中要比较的次数
                    {
                        if (SortListContain[j] < SortListContain[j + 1]) //判断两个数值的大小，若前一项比后一项大，则交换位置
                        {
                            int temp = SortListContain[j];//定义一个中间量temp
                            SortListContain[j] = SortListContain[j + 1];
                            SortListContain[j + 1] = temp;
                        }
                    }
                }
                a_layerStategy = ObjectCopier.Clone(PrintStrategys.m_PrintStrategys[SortListContain[SortListContain.Length - 1]]);

                //(4)刷新绑定的ListView的显示
                ListViewBinding(a_layerStategy.layerStategyContents);
                label67.Text = "打印策略:" + a_layerStategy.Name;
            }
        }


        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            //此处存在问题。如果双击直接操作，就奔溃了。是因为，此时没有选中正确的index。
            if (listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                //(1)排序容器：从大到小排序生成所需删除次序
                int[] SortListContain = new int[listView1.SelectedItems.Count];//记录待删除项的索引号
                for (int j = 0; j < this.listView1.SelectedItems.Count; j++)
                {
                    SortListContain[j] = listView1.SelectedItems[j].Index;
                }

                //(2)进行排序工作：从大到小排序生成所需删除次序
                for (int i = 0; i < SortListContain.Length - 1; i++) //总共要比较的趟数
                {
                    for (int j = 0; j < SortListContain.Length - 1 - i; j++) //每趟中要比较的次数
                    {
                        if (SortListContain[j] < SortListContain[j + 1]) //判断两个数值的大小，若前一项比后一项大，则交换位置
                        {
                            int temp = SortListContain[j];//定义一个中间量temp
                            SortListContain[j] = SortListContain[j + 1];
                            SortListContain[j + 1] = temp;
                        }
                    }
                }
                LayerStategyContent templayerStategyContent = ObjectCopier.Clone(a_layerStategy.layerStategyContents[SortListContain[SortListContain.Length - 1]]);
                //m_mlayerStategyContent = (LayerStategyContent)a_layerStategy.layerStategyContents[SortListContain[SortListContain.Length - 1]].Clone();
                {//此处不可以使用深复制或者浅复制，应该直接使用属性赋值
                    m_mlayerStategyContent.LayerIndexStart = templayerStategyContent.b_LayerIndex;

                    m_mlayerStategyContent.NeedPowder = templayerStategyContent.b_NeedPowder;
                    m_mlayerStategyContent.NeedUV = templayerStategyContent.b_NeedUV;
                    m_mlayerStategyContent.LayerIndexEnd = templayerStategyContent.b_LayerEnd;
                    m_mlayerStategyContent.AutoPrintNextlayer = templayerStategyContent.b_AutoPrintNextlayer;

                    m_mlayerStategyContent.PowderThick = templayerStategyContent.m_dPowderThick;
                    m_mlayerStategyContent.UVPower = templayerStategyContent.m_dUVPower;
                    m_mlayerStategyContent.WaveType = templayerStategyContent.m_nWaveType;
                }

                int k = SortListContain[SortListContain.Length - 1];
                label66.Text = "层参数设计:" + "参数" + (k + 1);
            }
        }

        private void AddLayerBtn_Click(object sender, EventArgs e)
        {
            LayerStategyContent tempLayerParam = ObjectCopier.Clone(m_mlayerStategyContent);

            a_layerStategy.layerStategyContents.Add(tempLayerParam);
            ListViewBinding(a_layerStategy.layerStategyContents);
        }

        private void Insert_Click(object sender, EventArgs e)
        {
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                //(1)在选中行的下一行进行插入
                int number = this.listView1.SelectedItems[0].Index + 1;//

                a_layerStategy.layerStategyContents.Insert(number, ObjectCopier.Clone(m_mlayerStategyContent));

                //a_layerStategy.layerStategyContents[number] = ObjectCopier.Clone(m_mlayerStategyContent);
                //(2)刷新ListView
                ListViewBinding(a_layerStategy.layerStategyContents);
            }

        }

        private void ModifyBtn_Click(object sender, EventArgs e)
        {
            //（4）参数策略配置：修改
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                //(1)更新数据：从扫描策略的层参数——》层参数设计
                int number = this.listView1.SelectedItems[0].Index;

                a_layerStategy.layerStategyContents[number] = ObjectCopier.Clone(m_mlayerStategyContent);
                //(2)刷新ListView
                ListViewBinding(a_layerStategy.layerStategyContents);
            }
        }

        private void AddStrategyBtn_Click(object sender, EventArgs e)
        {
            //修改策略列表绑定的策略数据
            //APrintStategy tempLayerParam = (APrintStategy)a_layerStategy.Clone();
            a_layerStategy.Name = "新建策略" + PrintStrategys.m_PrintStrategys.Count();
            APrintStategy tempLayerParam = ObjectCopier.Clone(a_layerStategy);
            //传送到临时策略
            PrintStrategys.m_PrintStrategys.Add(ObjectCopier.Clone(tempLayerParam));
            //刷新显示
            ListView2Binding(PrintStrategys);
        }

        private void DeleteBtn_Click(object sender, EventArgs e)
        {
            DeleteListView();
        }

        private int ListViewId = 0;//正确的来讲，用于记录行数
        public void DeleteListView()
        {
            //此处存在问题。如果双击直接操作，就奔溃了。是因为，此时没有选中正确的index。
            if (listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                //(1)排序容器：从大到小排序生成所需删除次序
                int[] SortListContain = new int[listView1.SelectedItems.Count];//记录待删除项的索引号
                for (int j = 0; j < this.listView1.SelectedItems.Count; j++)
                {
                    SortListContain[j] = listView1.SelectedItems[j].Index;
                }

                //(2)进行排序工作：从大到小排序生成所需删除次序
                for (int i = 0; i < SortListContain.Length - 1; i++) //总共要比较的趟数
                {
                    for (int j = 0; j < SortListContain.Length - 1 - i; j++) //每趟中要比较的次数
                    {
                        if (SortListContain[j] < SortListContain[j + 1]) //判断两个数值的大小，若前一项比后一项大，则交换位置
                        {
                            int temp = SortListContain[j];//定义一个中间量temp
                            SortListContain[j] = SortListContain[j + 1];
                            SortListContain[j + 1] = temp;
                        }
                    }
                }
                //(3)依次完成删除：删除原始的数据
                for (int i = 0; i < SortListContain.Length; i++) //将排序后的数值按序输出
                {
                    a_layerStategy.layerStategyContents.RemoveAt(SortListContain[i]);
                }
                //(4)刷新绑定的ListView的显示
                ListViewBinding(a_layerStategy.layerStategyContents);
            }
        }

        private void DeleteBtn2_Click(object sender, EventArgs e)
        {
            //此处存在问题。如果双击直接操作，就奔溃了。是因为，此时没有选中正确的index。
            if (listView2.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                //(1)排序容器：从大到小排序生成所需删除次序
                int[] SortListContain = new int[listView2.SelectedItems.Count];//记录待删除项的索引号
                for (int j = 0; j < this.listView2.SelectedItems.Count; j++)
                {
                    SortListContain[j] = listView2.SelectedItems[j].Index;
                }

                //(2)进行排序工作：从大到小排序生成所需删除次序
                for (int i = 0; i < SortListContain.Length - 1; i++) //总共要比较的趟数
                {
                    for (int j = 0; j < SortListContain.Length - 1 - i; j++) //每趟中要比较的次数
                    {
                        if (SortListContain[j] < SortListContain[j + 1]) //判断两个数值的大小，若前一项比后一项大，则交换位置
                        {
                            int temp = SortListContain[j];//定义一个中间量temp
                            SortListContain[j] = SortListContain[j + 1];
                            SortListContain[j + 1] = temp;
                        }
                    }
                }
                //(3)依次完成删除：删除原始的数据
                for (int i = 0; i < SortListContain.Length; i++) //将排序后的数值按序输出
                {
                    PrintStrategys.m_PrintStrategys.RemoveAt(SortListContain[i]);
                }
                //(4)刷新显示
                ListView2Binding(PrintStrategys);
            }
        }
        private void Modify2Btn_Click(object sender, EventArgs e)
        {
            //(1)更新数据：
            //a_layerStategy.layerStategyContents[number]= (LayerStategyContent)m_mlayerStategyContent.Clone();
            try
            {
                int FindIndex = PrintStrategys.m_PrintStrategys.FindIndex(t => t.Name == a_layerStategy.Name);
                PrintStrategys.m_PrintStrategys[FindIndex] = ObjectCopier.Clone(a_layerStategy);
                //(2)刷新ListView
                ListViewBinding(a_layerStategy.layerStategyContents);
            }
            catch (Exception)
            {
                MessageBox.Show("Note:待更新项已删除");
            }

        }

        private void RenameBtn_Click(object sender, EventArgs e)
        {
            int i = PrintStrategys.m_PrintStrategys.FindIndex(t => t.Name == a_layerStategy.Name);
            a_layerStategy.Name = RenameText.Text;
            PrintStrategys.m_PrintStrategys[i] = ObjectCopier.Clone(a_layerStategy);

            //刷新显示
            ListView2Binding(PrintStrategys);
            ListViewBinding(a_layerStategy.layerStategyContents);
            label67.Text = "打印策略:" + a_layerStategy.Name;
        }

        private void SelectedItemComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //20200807新建：显示默认的打印策略
            PrintStrategys.SelectedAPrintStategyItem = SelectedItemComboBox.SelectedIndex;
        }
        /// <summary>
        /// 保存为本地的.JSON配置文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveStrategyBtn_Click(object sender, EventArgs e)
        {
            //string json = JsonConvert.SerializeObject(PrintStrategys, Formatting.Indented);
            //string JsonPath = System.Windows.Forms.Application.StartupPath + @"\PrintStrategy-Configuration.json";//json配置文件：启动目录
            //File.WriteAllText(JsonPath, json);

            SaveJsonFile();
        }

        public void SaveJsonFile()
        {
            string json = JsonConvert.SerializeObject(PrintStrategys, Formatting.Indented);
            string JsonPath = System.Windows.Forms.Application.StartupPath + @"\PrintStrategy-Configuration.json";//json配置文件：启动目录
            File.WriteAllText(JsonPath, json);
        }
        private void JOB参数设置_Shown(object sender, EventArgs e)
        {
            this.button17.Focus();//20200602新建：软件启动后的鼠标焦点设置

            ListView2Binding(PrintStrategys);
            //PrintStrategys.SelectedAPrintStategyItem = 1;//在ListView中，默认显示第1项的数据
            if (PrintStrategys.m_PrintStrategys.Count() > 0)
            {
                a_layerStategy = ObjectCopier.Clone(PrintStrategys.m_PrintStrategys[PrintStrategys.SelectedAPrintStategyItem]);
                ListViewBinding(a_layerStategy.layerStategyContents);//20200806新增：刷新显示打印策略
                label67.Text = "打印策略:" + a_layerStategy.Name;
            }
        }

        private void 墨车参数设置_Load(object sender, EventArgs e)
        {
            //this.ResetCorrectCheckBox.Checked = m_FlagResetCorrect;//20200220:参数设置
            InitKidFormWithSysparam();
            int index = SpeedBox.FindString((k_RYSYSParam.m_dCarMoveSpeed).ToString());
            SpeedBox.SelectedIndex = index;

            /*int*/
            index = comboBox6.FindString((k_RYSYSParam.m_dCarBackCleanStationMoveSpeed).ToString());
            comboBox6.SelectedIndex = index;

            index = XDpiBox.FindString((k_RYSYSParam.m_XPrintDpi).ToString());
            XDpiBox.SelectedIndex = index;

            index = GrayScaleBox.FindString((k_RYSYSParam.m_nPixelGrayBits).ToString());//20220202修改：GrayScale修改控件
            GrayScaleBox.SelectedIndex = index;

            #region  //20230202修改：更新灰度打印值列表
            string[] GrayValues1 = new string[] { "1" };//{ "100" };
            string[] GrayValues2 = new string[] { "1", "2", "3" };//{ "33.3", "66.6", "100" };//3阶灰度
            string[] GrayValues3 = new string[] { "1", "2", "3", "4", "5", "6", "7" };//{ "14.28", "28.57", "42.86", "57.14", "71.43", "85.71", "100" };//7阶灰度
            if (k_RYSYSParam.m_nPixelGrayBits == 1)
            {
                GrayValueBox.Items.Clear();//20230210新建
                GrayValueBox.Items.AddRange(GrayValues1);

            }
            else if (k_RYSYSParam.m_nPixelGrayBits == 2)
            {
                GrayValueBox.Items.Clear();//20230210新建
                GrayValueBox.Items.AddRange(GrayValues2);
            }
            else if (k_RYSYSParam.m_nPixelGrayBits == 3)
            {
                GrayValueBox.Items.Clear();//20230210新建
                GrayValueBox.Items.AddRange(GrayValues3);
            }
            else { }

            index = GrayValueBox.FindString((k_RYSYSParam.m_dPixelGrayValue).ToString());//20220202修改：GrayScale修改控件
            if (index == -1)//未检索到
            {
                k_RYSYSParam.m_dPixelGrayValue = 1;
                index = GrayValueBox.FindString((k_RYSYSParam.m_dPixelGrayValue).ToString());
                GrayValueBox.SelectedIndex = index;
            }
            else//检索到
            {
                GrayValueBox.SelectedIndex = index;
            }
            #endregion

            //消除加载时黑框显示的临时定时器：20200527批注：本部分代码非常关键
            Timer = new System.Windows.Forms.Timer() { Interval = 100 };
            Timer.Tick += new EventHandler(Timer_Tick);
            base.Opacity = 0;
            Timer.Start();

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
            }
            else
            {
                base.Opacity += 0.5;//延迟4帧完成显示
            }
        }

        //20200224新建：textbox数据绑定
        public RYSYSParam k_RYSYSParam;
        public void InitKidFormWithSysparam()
        {
            ///必须将RYSYSParam和UI的初始化分开
            ////RYSYSParam k_RYSYSParam = new RYSYSParam();
            //k_RYSYSParam = new RYSYSParam();

            // 喷头保护设置参数：(清洗和闪喷两种作用)
            textBox11.DataBindings.Add("Text", k_RYSYSParam, "StandbySpeedSparkFreq", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);

            //20220920新建批注：时间：1s;有效时间：0.5s;频率：500Hz
            textBox14.DataBindings.Add("Text", k_RYSYSParam, "InterSpeedSparkCycleTime", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox12.DataBindings.Add("Text", k_RYSYSParam, "HSpeedSparkTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox13.DataBindings.Add("Text", k_RYSYSParam, "HSpeedSparkFreq", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);


            textBox16.DataBindings.Add("Text", k_RYSYSParam, "InterSpeedSparkValidTime", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);

            textBox15.DataBindings.Add("Text", k_RYSYSParam, "PrintCleaningCycleTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox17.DataBindings.Add("Text", k_RYSYSParam, "StandbyPreMoiWaitTime", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox18.DataBindings.Add("Text", k_RYSYSParam, "PreMoiHeadHeight", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox21.DataBindings.Add("Text", k_RYSYSParam, "CleaningHeadHeight", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);

            textBox20.DataBindings.Add("Text", k_RYSYSParam, "CleaningInkSupplyTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox19.DataBindings.Add("Text", k_RYSYSParam, "CleaningInkSupplyWaitTime", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);

            textBox24.DataBindings.Add("Text", k_RYSYSParam, "CleaningInkJourneyLength", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox23.DataBindings.Add("Text", k_RYSYSParam, "CleaningCount", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox22.DataBindings.Add("Text", k_RYSYSParam, "CleaningBladeHeight", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);

            // UV灯设置参数
            textBox2.DataBindings.Add("Text", k_RYSYSParam, "UVLightPower", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox4.DataBindings.Add("Text", k_RYSYSParam, "UVAdavanceLength", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox7.DataBindings.Add("Text", k_RYSYSParam, "UVStrengthLength", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);

            // 运动供墨参数
            textBox1.DataBindings.Add("Text", k_RYSYSParam, "CarMoveBufferLength", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox6.DataBindings.Add("Text", k_RYSYSParam, "InkSupplyCycleValidTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            textBox5.DataBindings.Add("Text", k_RYSYSParam, "InkSupplyCycleTime", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            SpeedBox.DataBindings.Add("SelectedItem", k_RYSYSParam, "CarMoveSpeed", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox6.DataBindings.Add("SelectedItem", k_RYSYSParam, "CarBackCleanStationMoveSpeed", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//车头回清洗站运动速度：20200326新增


            textBox9.DataBindings.Add("Text", k_RYSYSParam, "PrintAeraLength", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印区长度：默认420mm：20200326新增
            textBox10.DataBindings.Add("Text", k_RYSYSParam, "CarMoveBufferLength2", true/*true*//*false*/, DataSourceUpdateMode.OnPropertyChanged);//车头运动缓冲长度2：默认10mm：20200326新增

            textBox25.DataBindings.Add("Text", k_RYSYSParam, "BlenderValidSec", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//墨水搅拌有效时间：20200329新增
            textBox26.DataBindings.Add("Text", k_RYSYSParam, "BlenderCycleSec", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//X向喷射密度：20201017新增


            XDpiBox.DataBindings.Add("SelectedItem", k_RYSYSParam, "XPrintDpi", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//墨水搅拌周期：20200329新增


            // 喷头保护设置参数：(清洗和闪喷两种作用)
            checkBox1.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkDrawGenal", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//普通清洗，压墨
            checkBox2.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkShoveGenal", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//普通清洗，压墨
            checkBox3.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkBladeGenal", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//保湿车头高度

            checkBox6.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkDrawPrinting", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印清洗，压墨
            checkBox5.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkShovePrinting", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印清洗，压墨
            checkBox4.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkBladePrinting", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印清洗，刮墨

            checkBox7.DataBindings.Add("Checked", k_RYSYSParam, "FlagSparkBeforePrint", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印前高速闪喷
            checkBox8.DataBindings.Add("Checked", k_RYSYSParam, "FlagSparkDuringPrintWait", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印等待时闪喷
            checkBox9.DataBindings.Add("Checked", k_RYSYSParam, "FlagCleaningBeforePrint", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印前清洗

            // 功能选项:
            checkBox10.DataBindings.Add("Checked", k_RYSYSParam, "FlagJumpWhite", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//普通清洗，压墨
            checkBox11.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkShoveStarting", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//普通清洗，压墨
            checkBox12.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkLevelAlarm", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//保湿车头高度            checkBox1.DataBindings.Add("Checked", k_RYSYSParam, "FlagInkDrawGenal", false, DataSourceUpdateMode.OnPropertyChanged);//普通清洗，压墨
            checkBox13.DataBindings.Add("Checked", k_RYSYSParam, "FlagSparkWhenPreMoi", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//普通清洗，压墨
            checkBox14.DataBindings.Add("Checked", k_RYSYSParam, "FlagComeXOriginEnding", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//保湿车头高度

            //textBox27.DataBindings.Add("Text", k_RYSYSParam, "PixelGrayBits", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增//20230202修改：灰度数据格式:
            GrayScaleBox.DataBindings.Add("SelectedItem", k_RYSYSParam, "PixelGrayBits", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230202修改：灰度数据格式:
            GrayValueBox.DataBindings.Add("SelectedItem", k_RYSYSParam, "PixelGrayValue", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230202修改：灰度数据格式:

            textBox28.DataBindings.Add("Text", k_RYSYSParam, "PrtCtl", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//JOB控制字：
            textBox8.DataBindings.Add("Text", k_RYSYSParam, "PrtXEncPos", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//任务的X向起打位置:20200411新增

            textBox27.DataBindings.Add("Text", k_RYSYSParam, "XJetOff", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//任务的X向起打位置修正:20230321修订
            textBox37.DataBindings.Add("Text", k_RYSYSParam, "YJetOff", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//任务的X向起打位置:20200411新增

            // 送粉系统校准
            ResetCorrectCheckBox.DataBindings.Add("Checked", k_RYSYSParam, "FlagResetCorrect", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//保湿车头高度
            textBox3.DataBindings.Add("Text", k_RYSYSParam, "LogPath", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//日志路径：20200326新增
                                                                                                                               //textBox8.DataBindings.Add("Text", k_RYSYSParam, "WavePath", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//波形路径：20200326新增
                                                                                                                               //打印策略系统参数绑定：
            textBox30.DataBindings.Add("Text", m_mlayerStategyContent, "LayerIndexEnd", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox3.DataBindings.Add("SelectedIndex", m_mlayerStategyContent, "AutoPrintNextlayer", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox4.DataBindings.Add("SelectedIndex", m_mlayerStategyContent, "NeedPowder", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox5.DataBindings.Add("SelectedIndex", m_mlayerStategyContent, "NeedUV", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增

            textBox34.DataBindings.Add("Text", m_mlayerStategyContent, "PowderThick", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增
            textBox35.DataBindings.Add("Text", m_mlayerStategyContent, "UVPower", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增
            textBox36.DataBindings.Add("Text", m_mlayerStategyContent, "WaveType", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增

            textBox29.DataBindings.Add("Text", m_mlayerStategyContent, "LayerIndexStart", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增


            // 20210113新增：大零件子区域处理算法设置：双层循环
            textBox33.DataBindings.Add("Text", k_RYSYSParam, "SubAreaWidth", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增
            textBox32.DataBindings.Add("Text", k_RYSYSParam, "WeakAreaWidth", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增
            textBox31.DataBindings.Add("Text", k_RYSYSParam, "Deviation", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增
            // 202105303新增：大零件子区域处理算法设置的无效深度：距上表面深度，目前设置为层数，方便使用
            textBox38.DataBindings.Add("Text", k_RYSYSParam, "UnactDepth", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);//灰度数据格式:20200411新增
            ////20210605新增：是否应用子区域处理算法
            comboBox1.DataBindings.Add("SelectedIndex", k_RYSYSParam, "ApplaySubAreaAlthogrim", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增

            ////20230320新增：是否处于调试状态
            comboBox2.DataBindings.Add("SelectedIndex", k_RYSYSParam, "ApplyPowderSupplyMotion", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
        }

        private void WaveformSelectBtn_Click(object sender, EventArgs e)
        {
            WaveformSelectBtn.Text = "波形选\r\n择中 ";
            //WaveformSelectBtn.BackColor = Color.Lime;

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
            }

            WaveformSelectBtn.Text = "选择波\r\n形参数";
            //WaveformSelectBtn.BackColor = Color.Yellow;
        }
        private UVLightParam g_UVLightParam = new UVLightParam
        { m_nFrequency = 125, m_fPower = 50f, nMinPos = new int[] { 510, 230 }, nMaxPos = new int[] { 960, 680 } };//20200619批注：集成到自动出光设置//20200627新增：只是消除BUG,但是还没与主界面建立通讯+
        public UInt32 nValveStateMask = 0;//20200718新增
        private void ADIBSetApplyBtn_Click(object sender, EventArgs e)
        {
            ADIBSetApplyBtn.Text = "参数设\r\n置中 ";
            手动操作 f = new 手动操作(2, nValveStateMask);//20200202修改//20200718修改：新增第2项参数
            f.Width = 1000; f.Height = 650;
            //f.ControlBox = false;
            f.FormBorderStyle = FormBorderStyle.FixedSingle;
            f.Text = "环境参数设置中";//20200327新增：
            f.k_UVLightParam = g_UVLightParam;//从手动控制端传回设置的UV灯参数：20200619批注//20200627新增

            DialogResult result = f.ShowDialog();
            if (result == DialogResult.OK)//OK时，执行对应操作
            {
                g_UVLightParam = f.k_UVLightParam;//从手动控制端传回设置的UV灯参数：20200619批注//20200627新增
            }
            else if (result == DialogResult.Cancel)//退出时，什么都不做
            {
            }
            ADIBSetApplyBtn.Text = "设置环\r\n境参数";
            //ADIBSetApplyBtn.BackColor = Color.Yellow;
        }

        private void GrayScaleBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            #region  //20230202修改：更新灰度打印值列表
            string[] GrayValues1 = new string[] { "1" };//{ "100" };
            string[] GrayValues2 = new string[] { "1", "2", "3" };//{ "33.3", "66.6", "100" };//3阶灰度
            string[] GrayValues3 = new string[] { "1", "2", "3", "4", "5", "6", "7" };//{ "14.28", "28.57", "42.86", "57.14", "71.43", "85.71", "100" };//7阶灰度
            if ((sender as ComboBox).SelectedIndex/*k_RYSYSParam.m_nPixelGrayBits */== 0)
            {
                GrayValueBox.Items.Clear();
                GrayValueBox.Items.AddRange(GrayValues1);
                GrayValueBox.SelectedIndex = 0;//最大墨量
            }
            else if ((sender as ComboBox).SelectedIndex == 1)
            {
                GrayValueBox.Items.Clear();
                GrayValueBox.Items.AddRange(GrayValues2);
                GrayValueBox.SelectedIndex = 2;//最大墨量
            }
            else if ((sender as ComboBox).SelectedIndex == 2)
            {
                GrayValueBox.Items.Clear();
                GrayValueBox.Items.AddRange(GrayValues3);
                GrayValueBox.SelectedIndex = 6;//最大墨量
            }
            else { }
            //index = GrayValueBox.FindString((k_RYSYSParam.m_dPixelGrayValue).ToString());//20220202修改：GrayScale修改控件
            //if (index == -1)//未检索到
            //{
            //    k_RYSYSParam.m_dPixelGrayValue = 1;
            //    index = GrayValueBox.FindString((k_RYSYSParam.m_dPixelGrayValue).ToString());
            //    GrayValueBox.SelectedIndex = index;
            //}
            //else//检索到
            //{
            //    GrayValueBox.SelectedIndex = index;
            //}
            #endregion
        }

        private void button17_Click(object sender, EventArgs e)
        {

        }
    }

    public class RYSYSParam : INotifyPropertyChanged, ICloneable//C#中，通知类的属性值已经更改，可以避免大量的通用事件的使用；其中关键是属性的理解及和lambda表达式的使用方法
    {
        /// <summary>
        /// 返回本类的浅表复本
        /// </summary>
        /// <returns></returns>
        public object Clone()//精华
        {
            return this.MemberwiseClone();//返回本类的浅表复本//20200401新增：精华
        }
        /// <summary>
        /// 喷头保护设置参数：(清洗和闪喷两种作用)
        /// </summary>
        //总计有2种方式的闪喷：高速闪喷和待机闪喷；闪喷策略，分别保存到对应的结构体
        public double m_dInterSpeedSparkCycleTime = 1.5/*20*/;//间歇闪喷周期//20220920修改：周期为1s
        public double m_dHSpeedSparkTime = 0.5/*1*/;//高速闪喷时间//20220920修改：有效时间0.5s
        public int m_nHSpeedSparkFreq = 500;//高速闪喷频率//20220920修改：频率500Hz

        public int m_nStandbySpeedSparkFreq = 500/*string.Empty*/;//待机闪喷频率——————n表示int；d表示double//待机就是间歇20200326：
        public double m_dInterSpeedSparkValidTime = 1;//间歇闪喷有效时间


        public double m_dPrintCleaningCycleTime = 0;//打印自动清洗周期
        public double m_dStandbyPreMoiWaitTime = 100000;//待机定时保湿时间
        public double m_dPreMoiHeadHeight = 33.7;//保湿车头高度
        public double m_dCleaningHeadHeight = 3;//保湿车头高度

        public double m_dCleaningInkSupplyTime = 1;//清洗时压墨持续时间
        public double m_dCleaningInkSupplyWaitTime = 5;//清洗吸墨/等待时间

        public double m_dCleaningInkJourneyLength = 90/*string.Empty*/;//清洗刮片/抽吸于东长度
        public int m_nCleaningCount = 90;//清洗刮片/抽吸次数
        public double m_dCleaningBladeHeight = 14.5;//清洗喷头时刮片高度

        /// <summary>
        /// 喷头保护设置参数：(清洗和闪喷两种作用)
        /// </summary>
        public bool m_bFlagInkDrawGenal = true;//普通清洗，压墨
        public bool m_bFlagInkShoveGenal = true;//普通清洗，压墨
        public bool m_bFlagInkBladeGenal = true;//普通清洗，刮墨

        public bool m_bFlagInkDrawPrinting = true;//打印清洗，压墨
        public bool m_bFlagInkShovePrinting = false;//打印清洗，压墨
        public bool m_bFlagInkBladePrinting = true;//打印清洗，刮墨

        public bool m_bFlagSparkBeforePrint = false;//打印前高速闪喷//20200225：临时修改为public
        public bool m_bFlagSparkDuringPrintWait = false;//打印等待时闪喷
        public bool m_bFlagCleaningBeforePrint = false;//打印前清洗

        /// <summary>
        /// UV灯设置参数
        /// </summary>
        public double m_dUVLightPower = 5000;//UV灯功率
        public double m_dUVAdavanceLength = 150.0;//快门提前打开距离
        public double m_dUVStrengthLength = 10;//打印完固化加强长度

        /// <summary>
        /// 运动供墨参数
        /// </summary>
        public double m_dCarMoveBufferLength = 360;//车头运动缓冲长度
        public double m_dInkSupplyCycleValidTime = 60;//供墨循环有效时间
        public double m_dInkSupplyCycleTime = 3600;//供墨循环周期
        public double m_dCarMoveSpeed = 40/*80*//*10*/;//车头运动速度：20200326新增//20200422修改为50mm/s,10mm/s速度太慢//20210201新建批注：40mm/s的打印速度是优选的，对于喷墨质量的稳定非常关键
        public double m_dCarBackCleanStationMoveSpeed = 150;//车头回清洗站运动速度：20200326新增

        public double m_dPrintAeraLength = 420;//打印区长度：默认420mm：20200326新增
        public double m_dCarMoveBufferLength2 = 10;//车头运动缓冲长度2：默认10mm：20200326新增

        public double m_dBlenderValidSec = 10;//墨水搅拌有效时间：20200329新增
        public double m_dBlenderCycleSec = 300;//墨水搅拌周期：20200329新增

        public int m_XPrintDpi = 635 * 2;//X向喷射密度：20201017新增

        /// <summary>
        /// 功能选项
        /// </summary>
        public bool m_bFlagJumpWhite = false;//自动跳白标志
        public bool m_bFlagInkShoveStarting = false;//开机时挤墨
        public bool m_bFlagInkLevelAlarm = false;//允许液位报警
        public bool m_bFlagSparkWhenPreMoi = true;//保湿时闪喷
        public bool m_bFlagComeXOriginEnding = true;//打印结束X回原点

        public int m_nPixelGrayBits = 1/*4000*/;//灰度数据格式：20200411新增
        public int m_dPixelGrayValue = 2;//像素打印灰度值：20220202新增：
        public int m_nPrtCtl = 8;//JOB控制字：bit0:跳白支持，bit1：循环喷嘴偏移，bit2 Y向偏差无重嘴， bit3 X镜像， bit4 Y镜像:20200411新增
        public double m_dPrtXEncPos = 50/*30*//*379.5*//*362*/;////任务的X向起打位置:20200923修改：设置X向启打位置值为362//20210312修正：依据实际测量的成型缸体截面尺寸，进行为修改//20220601新建：修改X向启打位置修订
        //20220524修改：起始打印值为幅面的左端起始点，修改为30MM
        public double m_dXJetOff = 0;//20230321新建：修改X向启打位置修订
        public double m_dYJetOff = 0/*20*/;//Y向起打位置修订:20210311新增//20210312修正：依据实际测量的成型缸体截面尺寸，进行为修改//20230319新建：此值修改为默认值0，消除此前的相关BUGS       


        /// <summary>
        /// 送粉系统校准
        /// </summary>
        public bool m_bFlagResetCorrect = false;//断电后校准
        public string m_sLogPath = System.Windows.Forms.Application.StartupPath + @"\日志文件\";//日志路径：20200326新增
        //public string m_sWavePath = System.Windows.Forms.Application.StartupPath + @"\波形文件\ricoh_phcfg16.rhdat";//波形路径：20200326新增



        public event PropertyChangedEventHandler PropertyChanged;//20200224新增：必须定义事件；是接口INotifyPropertyChanged的必须的事件       
        //表达式树：lambda表达式，为一段可执行代码————SQL数据库查询的时候使用
        //private void NotifyPropertyChanged<T>(/*Func<string>p*/ Expression<Func<T>> property)//20200225：触发事件
        //{
        //    if (PropertyChanged == null) return;
        //    var memberExpression = property.Body as MemberExpression;
        //    if (memberExpression == null) return;
        //    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        //}
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")//20200225：【CallerMemberName】特性精华：每次调用 TraceMessage 方法时，调用方信息将替换为可选参数的参数。
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));//20200225：propertyName：是1个附带参数
        }

        //public string TheValue//20200225：属性——测试的示例
        //{
        //    get { return this._theValue; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
        //    set {if (value != this._theValue){this._theValue = value; NotifyPropertyChanged();}}
        //}

        /// 喷头保护设置参数：(清洗和闪喷两种作用)
        public int StandbySpeedSparkFreq//待机闪喷频率
        {
            get { return this.m_nStandbySpeedSparkFreq; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nStandbySpeedSparkFreq) { this.m_nStandbySpeedSparkFreq = value; NotifyPropertyChanged(); } }
        }
        public int HSpeedSparkFreq//高速闪喷频率
        {
            get { return this.m_nHSpeedSparkFreq; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nHSpeedSparkFreq) { this.m_nHSpeedSparkFreq = value; NotifyPropertyChanged(); } }
        }
        public double HSpeedSparkTime//高速闪喷时间
        {
            get { return this.m_dHSpeedSparkTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dHSpeedSparkTime) { this.m_dHSpeedSparkTime = value; NotifyPropertyChanged(); } }
        }
        public double InterSpeedSparkCycleTime//间歇闪喷周期
        {
            get { return this.m_dInterSpeedSparkCycleTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dInterSpeedSparkCycleTime) { this.m_dInterSpeedSparkCycleTime = value; NotifyPropertyChanged(); } }
        }
        public double InterSpeedSparkValidTime//间歇闪喷有效时间
        {
            get { return this.m_dInterSpeedSparkValidTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dInterSpeedSparkValidTime) { this.m_dInterSpeedSparkValidTime = value; NotifyPropertyChanged(); } }
        }

        public double PrintCleaningCycleTime//打印自动清洗周期
        {
            get { return this.m_dPrintCleaningCycleTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dPrintCleaningCycleTime) { this.m_dPrintCleaningCycleTime = value; NotifyPropertyChanged(); } }
        }
        public double StandbyPreMoiWaitTime//待机定时保湿时间
        {
            get { return this.m_dStandbyPreMoiWaitTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dStandbyPreMoiWaitTime) { this.m_dStandbyPreMoiWaitTime = value; NotifyPropertyChanged(); } }
        }
        public double PreMoiHeadHeight//保湿车头高度
        {
            get { return this.m_dPreMoiHeadHeight; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dPreMoiHeadHeight) { this.m_dPreMoiHeadHeight = value; NotifyPropertyChanged(); } }
        }
        public double CleaningHeadHeight//保湿车头高度
        {
            get { return this.m_dCleaningHeadHeight; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCleaningHeadHeight) { this.m_dCleaningHeadHeight = value; NotifyPropertyChanged(); } }
        }

        public double CleaningInkSupplyTime//清洗时压墨持续时间
        {
            get { return this.m_dCleaningInkSupplyTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCleaningInkSupplyTime) { this.m_dCleaningInkSupplyTime = value; NotifyPropertyChanged(); } }
        }
        public double CleaningInkSupplyWaitTime//清洗吸墨/等待时间
        {
            get { return this.m_dCleaningInkSupplyWaitTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCleaningInkSupplyWaitTime) { this.m_dCleaningInkSupplyWaitTime = value; NotifyPropertyChanged(); } }
        }

        public double CleaningInkJourneyLength//待机定时保湿时间
        {
            get { return this.m_dCleaningInkJourneyLength; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCleaningInkJourneyLength) { this.m_dCleaningInkJourneyLength = value; NotifyPropertyChanged(); } }
        }
        public int CleaningCount//保湿车头高度
        {
            get { return this.m_nCleaningCount; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nCleaningCount) { this.m_nCleaningCount = value; NotifyPropertyChanged(); } }
        }
        public double CleaningBladeHeight//保湿车头高度
        {
            get { return this.m_dCleaningBladeHeight; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCleaningBladeHeight) { this.m_dCleaningBladeHeight = value; NotifyPropertyChanged(); } }
        }

        /// <summary>
        /// UV灯设置参数
        /// </summary>
        public double UVLightPower//待机定时保湿时间
        {
            get { return this.m_dUVLightPower; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dUVLightPower) { this.m_dUVLightPower = value; NotifyPropertyChanged(); } }
        }
        public double UVAdavanceLength//保湿车头高度
        {
            get { return this.m_dUVAdavanceLength; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dUVAdavanceLength) { this.m_dUVAdavanceLength = value; NotifyPropertyChanged(); } }
        }
        public double UVStrengthLength//保湿车头高度
        {
            get { return this.m_dUVStrengthLength; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dUVStrengthLength) { this.m_dUVStrengthLength = value; NotifyPropertyChanged(); } }
        }
        public double CarMoveSpeed//车头运动速度：20200326新增
        {
            get { return this.m_dCarMoveSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set 
            {
                if ((value != this.m_dCarMoveSpeed) && (40 <= value && value <= 500))
                {
                    this.m_dCarMoveSpeed = value; NotifyPropertyChanged();
                }
                else if (value > 500)
                {
                    this.m_dCarMoveSpeed = 500; NotifyPropertyChanged();
                }
                else if (value < 40)
                {
                    this.m_dCarMoveSpeed = 40; NotifyPropertyChanged();
                }
            }
        }
        public double CarBackCleanStationMoveSpeed//车头回清洗站运动速度：20200326新增
        {
            get { return this.m_dCarBackCleanStationMoveSpeed; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCarBackCleanStationMoveSpeed) { this.m_dCarBackCleanStationMoveSpeed = value; NotifyPropertyChanged(); } }
        }
        public double PrintAeraLength//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dPrintAeraLength; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dPrintAeraLength) { this.m_dPrintAeraLength = value; NotifyPropertyChanged(); } }
        }
        public double CarMoveBufferLength2//车头运动缓冲长度2：默认10mm：20200326新增
        {
            get { return this.m_dCarMoveBufferLength2; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCarMoveBufferLength2) { this.m_dCarMoveBufferLength2 = value; NotifyPropertyChanged(); } }
        }

        public double BlenderValidSec//墨水搅拌有效时间：20200329新增
        {
            get { return this.m_dBlenderValidSec; }
            set { if (value != this.m_dBlenderValidSec) { this.m_dBlenderValidSec = value; NotifyPropertyChanged(); } }
        }
        public double BlenderCycleSec//墨水搅拌周期：20200329新增
        {
            get { return this.m_dBlenderCycleSec; }
            set { if (value != this.m_dBlenderCycleSec) { this.m_dBlenderCycleSec = value; NotifyPropertyChanged(); } }
        }

        public int XPrintDpi//X向喷射密度：20201017新增
        {
            get { return this.m_XPrintDpi; }
            set { if (value != this.m_XPrintDpi) { this.m_XPrintDpi = value; NotifyPropertyChanged(); } }
        }
        /// <summary>
        /// 喷头保护设置
        /// </summary>
        public double CarMoveBufferLength//待机定时保湿时间
        {
            get { return this.m_dCarMoveBufferLength; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dCarMoveBufferLength) { this.m_dCarMoveBufferLength = value; NotifyPropertyChanged(); } }
        }
        public double InkSupplyCycleValidTime//保湿车头高度
        {
            get { return this.m_dInkSupplyCycleValidTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dInkSupplyCycleValidTime) { this.m_dInkSupplyCycleValidTime = value; NotifyPropertyChanged(); } }
        }
        public double InkSupplyCycleTime//保湿车头高度
        {
            get { return this.m_dInkSupplyCycleTime; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dInkSupplyCycleTime) { this.m_dInkSupplyCycleTime = value; NotifyPropertyChanged(); } }
        }


        /// <summary>
        /// 喷头保护设置参数：(清洗和闪喷两种作用)
        /// </summary>
        public bool FlagInkDrawGenal//普通清洗，压墨
        {
            get { return this.m_bFlagInkDrawGenal; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagInkDrawGenal) { this.m_bFlagInkDrawGenal = value; NotifyPropertyChanged(); } }
        }
        public bool FlagInkShoveGenal//普通清洗，压墨
        {
            get { return this.m_bFlagInkShoveGenal; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != m_bFlagInkShoveGenal) { this.m_bFlagInkShoveGenal = value; NotifyPropertyChanged(); } }
        }
        public bool FlagInkBladeGenal//保湿车头高度
        {
            get { return this.m_bFlagInkBladeGenal; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagInkBladeGenal) { this.m_bFlagInkBladeGenal = value; NotifyPropertyChanged(); } }
        }

        public bool FlagInkDrawPrinting//打印清洗，压墨
        {
            get { return this.m_bFlagInkDrawPrinting; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagInkDrawPrinting) { this.m_bFlagInkDrawPrinting = value; NotifyPropertyChanged(); } }
        }
        public bool FlagInkShovePrinting//打印清洗，压墨
        {
            get { return this.m_bFlagInkShovePrinting; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != m_bFlagInkShovePrinting) { this.m_bFlagInkShovePrinting = value; NotifyPropertyChanged(); } }
        }
        public bool FlagInkBladePrinting//打印清洗，刮墨
        {
            get { return this.m_bFlagInkBladePrinting; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagInkBladePrinting) { this.m_bFlagInkBladePrinting = value; NotifyPropertyChanged(); } }
        }

        public bool FlagSparkBeforePrint//打印前高速闪喷//20200225：临时修改为public
        {
            get { return this.m_bFlagSparkBeforePrint; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagSparkBeforePrint) { this.m_bFlagSparkBeforePrint = value; NotifyPropertyChanged(); } }
        }
        public bool FlagSparkDuringPrintWait//打印等待时闪喷
        {
            get { return this.m_bFlagSparkDuringPrintWait; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != m_bFlagSparkDuringPrintWait) { this.m_bFlagSparkDuringPrintWait = value; NotifyPropertyChanged(); } }
        }
        public bool FlagCleaningBeforePrint//打印前清洗
        {
            get { return this.m_bFlagCleaningBeforePrint; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagCleaningBeforePrint) { this.m_bFlagCleaningBeforePrint = value; NotifyPropertyChanged(); } }
        }

        /// <summary>
        /// 运动供墨参数
        /// </summary>
        public bool FlagJumpWhite//自动跳白标志
        {
            get { return this.m_bFlagJumpWhite; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagJumpWhite) { this.m_bFlagJumpWhite = value; NotifyPropertyChanged(); } }
        }

        public bool FlagInkShoveStarting//开机时挤墨
        {
            get { return this.m_bFlagInkShoveStarting; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagInkShoveStarting) { this.m_bFlagInkShoveStarting = value; NotifyPropertyChanged(); } }
        }
        public bool FlagInkLevelAlarm//允许液位报警
        {
            get { return this.m_bFlagInkLevelAlarm; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != m_bFlagInkLevelAlarm) { this.m_bFlagInkLevelAlarm = value; NotifyPropertyChanged(); } }
        }
        public bool FlagSparkWhenPreMoi//保湿时闪喷
        {
            get { return this.m_bFlagSparkWhenPreMoi; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagSparkWhenPreMoi) { this.m_bFlagSparkWhenPreMoi = value; NotifyPropertyChanged(); } }
        }
        public bool FlagComeXOriginEnding//打印结束X回原点
        {
            get { return this.m_bFlagComeXOriginEnding; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_bFlagComeXOriginEnding) { this.m_bFlagComeXOriginEnding = value; NotifyPropertyChanged(); } }
        }
        //public double[] PixelGrayValues = new double[7];
        public int PixelGrayBits//灰度数据格式:20200411新增
        {
            get { return this.m_nPixelGrayBits; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nPixelGrayBits) { this.m_nPixelGrayBits = value; NotifyPropertyChanged(); } }
        }
        public int PixelGrayValue//灰度数据格式:20200411新增
        {
            get { return this.m_dPixelGrayValue; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dPixelGrayValue) { this.m_dPixelGrayValue = value; NotifyPropertyChanged(); } }
        }

        public int PrtCtl//JOB控制字：bit0:跳白支持，bit1：循环喷嘴偏移，bit2 Y向偏差无重嘴， bit3 X镜像， bit4 Y镜像:20200411新增
        {
            get { return this.m_nPrtCtl; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nPrtCtl) { this.m_nPrtCtl = value; NotifyPropertyChanged(); } }
        }
        public double PrtXEncPos//任务的X向起打位置:20200411新增
        {
            get { return this.m_dPrtXEncPos; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dPrtXEncPos) { this.m_dPrtXEncPos = value; NotifyPropertyChanged(); } }
        }
        public double XJetOff//X向起打位置修订:20210311新增
        {
            get { return this.m_dXJetOff; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dXJetOff) { this.m_dXJetOff = value; NotifyPropertyChanged(); } }
        }

        public double YJetOff//Y向起打位置修订:20210311新增//20230418修改：适用于多PASS打印过程处理
        {
            get { return this.m_dYJetOff; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set 
            {
                if ((value != this.m_dYJetOff) && (0 <= value && value <= 15))
                {
                    this.m_dYJetOff = value; NotifyPropertyChanged();
                }
                else if (value > 15)
                {
                    this.m_dYJetOff = 15; NotifyPropertyChanged();
                }
                else if (value < 0)
                {
                    this.m_dYJetOff = 0; NotifyPropertyChanged();
                }
            }


        }

        /// <summary>
        /// 送粉系统校准
        /// </summary>
        public bool FlagResetCorrect//断电后校准
        {
            get { return this.m_bFlagResetCorrect; }
            set { if (value != this.m_bFlagResetCorrect) { this.m_bFlagResetCorrect = value; NotifyPropertyChanged(); } }
        }

        public string LogPath//日志路径：20200326新增
        {
            get { return this.m_sLogPath; }
            set { if (value != this.m_sLogPath) { this.m_sLogPath = value; NotifyPropertyChanged(); } }
        }

        /// <summary>
        /// 20210113新增：大零件子区域处理算法设置：双层循环
        /// </summary>
        public double m_dSubAreaWidth = 6;//子区域间距：默认8mm//20210201新建批注：优选参数应该为6mm
        public double m_dWeakAreaWidth = 100.0;//弱连接宽度：默认50um//20210201新建批注：优选参数应该为100um
        public double m_dDeviation = 3;//层间偏移：默认4mm//20210201新建批注：优选参数应该为3mm
        public double m_dUnactDepth = 5;//202105303新增：大零件子区域处理算法设置的无效深度：距上表面深度，目前设置为层数，方便使用；优选参数应该为5层
        public int m_bApplaySubAreaAlthogrim = 1;//20210605新增：是否应用子区域处理算法————特别的，0是采用，1是不采用//20220530修改:修改为默认不采用
        public int m_bApplyPowderSupplyMotion = 0;//20230320新增：是否处于调试状态————特别的，0是采用，1是不采用

        public double SubAreaWidth//任务的X向起打位置:20200411新增
        {
            get { return this.m_dSubAreaWidth; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dSubAreaWidth) { this.m_dSubAreaWidth = value; NotifyPropertyChanged(); } }
        }
        public double WeakAreaWidth//任务的X向起打位置:20200411新增
        {
            get { return this.m_dWeakAreaWidth; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dWeakAreaWidth) { this.m_dWeakAreaWidth = value; NotifyPropertyChanged(); } }
        }
        public double Deviation//任务的X向起打位置:20200411新增
        {
            get { return this.m_dDeviation; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dDeviation) { this.m_dDeviation = value; NotifyPropertyChanged(); } }
        }

        public double UnactDepth// 202105303新增：大零件子区域处理算法设置的无效深度：距上表面深度，目前设置为层数，方便使用
        {
            get { return this.m_dUnactDepth; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dUnactDepth) { this.m_dUnactDepth = value; NotifyPropertyChanged(); } }
        }

        public int ApplaySubAreaAlthogrim//20210605新增：是否应用子区域处理算法
        {
            get { return this.m_bApplaySubAreaAlthogrim; }
            set { if (value != this.m_bApplaySubAreaAlthogrim) { this.m_bApplaySubAreaAlthogrim = value; NotifyPropertyChanged(); } }
        }
        public int ApplyPowderSupplyMotion//20230320新增：是否处于调试状态
        {
            get { return this.m_bApplyPowderSupplyMotion; }
            set { if (value != this.m_bApplyPowderSupplyMotion) { this.m_bApplyPowderSupplyMotion = value; NotifyPropertyChanged(); } }
        }
    }
    /// <summary>
    /// 层参数设计：20200806批注
    /// </summary>
    public class LayerStategyContent : INotifyPropertyChanged, ICloneable//20200806新增
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

        public int b_LayerIndex = 0;
        public int b_LayerEnd = 0;
        public int b_NeedUV = 0;
        public int b_NeedPowder = 0;
        public int b_AutoPrintNextlayer = 0;
        public double m_dUVPower = 50;
        public double m_dPowderThick = 100;
        public int m_nWaveType = 1;

        //层间继承控制：是否开启层间继承
        public int LayerIndexStart//
        {
            get { return this.b_LayerIndex; }
            set { if (value != this.b_LayerIndex) { this.b_LayerIndex = value; NotifyPropertyChanged(); } }
        }
        public int LayerIndexEnd/*LayerInherit*///
        {
            get { return this.b_LayerEnd; }
            set { if (value != this.b_LayerEnd) { this.b_LayerEnd = value; NotifyPropertyChanged(); } }
        }

        //逐层打印逻辑控制：是否使能对应功能
        public int NeedUV//
        {
            get { return this.b_NeedUV; }
            set { if (value != this.b_NeedUV) { this.b_NeedUV = value; NotifyPropertyChanged(); } }
        }
        public int NeedPowder//
        {
            get { return this.b_NeedPowder; }
            set { if (value != this.b_NeedPowder) { this.b_NeedPowder = value; NotifyPropertyChanged(); } }
        }

        public int AutoPrintNextlayer//
        {
            get { return this.b_AutoPrintNextlayer; }
            set { if (value != this.b_AutoPrintNextlayer) { this.b_AutoPrintNextlayer = value; NotifyPropertyChanged(); } }
        }

        public double UVPower//UV功率，百分比
        {
            get { return this.m_dUVPower; }
            set { if (value != this.m_dUVPower) { this.m_dUVPower = value; NotifyPropertyChanged(); } }
        }
        public double PowderThick//层厚，um
        {
            get { return this.m_dPowderThick; }
            set { if (value != this.m_dPowderThick) { this.m_dPowderThick = value; NotifyPropertyChanged(); } }
        }
        public int WaveType//波形，1号波，2号波，3号波，4号波。。。。。。
        {
            get { return this.m_nWaveType; }
            set { if (value != this.m_nWaveType) { this.m_nWaveType = value; NotifyPropertyChanged(); } }
        }
    }

    public class APrintStategy : ICloneable//20200806新增
    {
        /// <summary>
        /// 返回本类的浅表复本
        /// </summary>
        /// <returns></returns>
        public object Clone()//精华PrintStrategys
        {
            return this.MemberwiseClone();//返回本类的浅表复本//20200401新增：精华
        }
        public string Name = null;//策略名称
        public List<LayerStategyContent> layerStategyContents = new List<LayerStategyContent>();//包含所有的层参数
    }

    public class PrintStrategys : ICloneable//20200806新增
    {
        /// <summary>
        /// 返回本类的浅表复本
        /// </summary>
        /// <returns></returns>
        public object Clone()//精华
        {
            return this.MemberwiseClone();//返回本类的浅表复本//20200401新增：精华
        }
        public int SelectedAPrintStategyItem;//当前选中的打印策略项
        public List<APrintStategy> m_PrintStrategys = new List<APrintStategy>();//包含所有加载的策略

        public RYSYSParam LocalRYSYSParam = new RYSYSParam();//20230203新增：修复打印DPI等参数无法本地保存的问题
    }

    /// <summary>
    /// Reference Article http://www.codeproject.com/KB/tips/SerializedObjectCloner.aspx
    /// Provides a method for performing a deep copy of an object.
    /// Binary Serialization is used to perform the copy.
    /// </summary>
    public static class ObjectCopier
    {
        /// <summary>
        /// Perform a deep Copy of the object.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T Clone<T>(this T source)
        {
            var serialized = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<T>(serialized);
        }
        /// <summary>
        /// 泛型方式，实现JSON文件加载
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonPath"></param>
        /// <returns></returns>
        public static T LoadJson<T>(this string jsonPath)
        {
            using (StreamReader r = new StreamReader(jsonPath))
            {
                string json = r.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(json);
            }
        }

        //public static void SaveJsonFile(string JsonPath,string json)
        //{
        //    string json = JsonConvert.SerializeObject(PrintStrategys, Formatting.Indented);
        //    //string JsonPath = System.Windows.Forms.Application.StartupPath + @"\PrintStrategy-Configuration.json";//json配置文件：启动目录
        //    File.WriteAllText(JsonPath, json);
        //}
    }
}
