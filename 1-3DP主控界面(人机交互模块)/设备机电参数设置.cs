using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using SystemParameterSeting;

namespace BinderJetting
{
    public partial class 设备参数设置 : Form
    {
        private Timer Timer = null;

        public 设备参数设置()
        {
            InitializeComponent();

            /////系统电气参数初始化设置
            this.listView1.View = View.Details;
            this.listView1.FullRowSelect = true;
            listView1.Columns.Add("序号", 50);
            listView1.Columns.Add("功能描述", 600);
            if (ListViewId > 0)
            {
                deletebutton.Enabled = true;//当插入新行时，唤醒删除按钮。
                updatebutton.Enabled = true;//当插入新行时，唤醒修改按钮。
            }
            else
            {
                deletebutton.Enabled = false;//当插入新行时，唤醒删除按钮。
                updatebutton.Enabled = false;//当插入新行时，唤醒修改按钮。
            }
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

        private void 设备参数设置_Load(object sender, EventArgs e)//40ms的间隔比较合适，人眼的分辨率是41.6ms
        {
            Timer = new Timer() { Interval = 20 };
            Timer.Tick += new EventHandler(Timer_Tick);
            base.Opacity = 0;
            Timer.Start();

            /////（1）系统电气参数初始化设置：
            readXML();//读取XML的元素及属性值，同时刷新所有的值。
            UpdateComboBox();
            //更新UserControl3中combox的items

            /////（2）系统电机参数初始化设置：
#if fasle
            #region 选项卡设置
            //选项卡1
            tabControl3.TabPages[0].Text = "粉料电机1";
            tabControl3.TabPages[1].Text = "成型缸电机";
            tabControl3.TabPages[2].Text = "粉料电机2";
            fen1 = new BindingList<MotorParameter>(粉料电机1);
            //绑定数据集合
            Motor1Parameter.DataBindings.Add("DataSource", this, "fen1", false, DataSourceUpdateMode.OnPropertyChanged);

            SetMotorParater(成型缸电机, 成型DGV, 5);
            SetMotorParater(粉料电机2, 粉2DGV, 5);

            //选项卡2
            tabControl2.TabPages[0].Text = "锁紧电机";
            tabControl2.TabPages[1].Text = "铺粉电机";
            tabControl2.TabPages[2].Text = "刮液电机";
            SetMotorParater(锁紧电机, 锁紧DGV, 5);
            SetMotorParater(铺粉电机, 铺粉DGV, 5);
            SetMotorParater(刮液电机, 刮液DGV, 5);

            //选项卡3
            tabControl4.TabPages[0].Text = "导轨电机1";
            tabControl4.TabPages[1].Text = "导轨电机2";
            tabControl4.TabPages[2].Text = "喷墨系统参数";
            SetMotorParater(导轨电机01, 导轨1DGV, 5);
            SetMotorParater(导轨电机02, 导轨2DGV, 5);
            SetMotorParater(BJ, 喷墨DGV, 3);
            #endregion
#else

            InitDataGrid1(1);//初始化datagridview//初始化棚舍固化过程的手动控制//20200327新增：
#endif
        }

        ////////定时刷新状态：通过自定义控件实现
        //private System.Windows.Forms.Timer Timer2 = null;//刷新墨量显示状态定时器
        //////（1）刷新液位报警信号
        //InkStateCtrl inkStateCtrl = new InkStateCtrl();
        //Bitmap inkState = new Bitmap(500, 100);
        //Rectangle rectangle = new Rectangle();
        //////（2）刷新轴使能报警信号
        //InkStateCtrl inkStateCtrl2 = new InkStateCtrl();
        //Bitmap inkState2 = new Bitmap(500, 100);
        //Rectangle rectangle2 = new Rectangle();
        //private void Timer2_Tick(object sender, EventArgs e)//刷新墨量显示状态定时器
        //{
        //    ////（1）刷新液位报警信号
        //    inkStateCtrl.SetInkCount(6, 0);
        //    inkStateCtrl.SetInkState(0b010101);//很关键
        //    inkState.SetPixel(pictureBox.Width, pictureBox.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
        //    Graphics g = Graphics.FromImage(inkState);
        //    g.Clear(pictureBox.BackColor);
        //    rectangle.Width = pictureBox.Width; rectangle.Height = pictureBox.Height;//rectanle是bitmap的大小
        //    inkStateCtrl.OnPaint(g, rectangle);
        //    pictureBox.CreateGraphics().DrawImage(inkState, new Point(0, 0));
        //    pictureBox.Image = inkState;
        //    //g.Dispose();

        //    ////（2）刷新轴使能报警信号
        //    inkStateCtrl2.SetInkCount(8, 0);
        //    inkStateCtrl2.SetInkState(0b00001111);//很关键
        //    inkState2.SetPixel(pictureBox1.Width, pictureBox1.Height, Color.FromArgb(0, 0, 0));//inkState是bitmap
        //    Graphics g2 = Graphics.FromImage(inkState2);
        //    g2.Clear(pictureBox1.BackColor);
        //    rectangle2.Width = pictureBox1.Width; rectangle2.Height = pictureBox1.Height;//rectanle是bitmap的大小
        //    inkStateCtrl2.OnPaint(g2, rectangle2);
        //    pictureBox1.CreateGraphics().DrawImage(inkState2, new Point(0, 0)
        //    pictureBox1.Image = inkState2;
        //    //g.Dispose();
        //}

        /*******************************多轴系统信号集控显示：20200626批注***************************/
        //private System.Windows.Forms.Timer Timer3 = null;//刷新墨量显示状态定时器
        bool m_FlagUpdateAdibInfo = false;//默认不刷新温度、电压、负压：20200403新增

        DataTable dataTable = new DataTable();//绑定datagridview1的dataTable
        BindingSource bingdingSource1 = new BindingSource();
        bool FlagFirstBindTable1 = true;//第一次绑定dataTable到datagridview1：20200403新增：
        private void InitDataGrid1(int BindType)//20200626批注//20200626新增移植
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
                dataGridView1.Columns[0].HeaderText = "千脉冲行程/mm:";
                dataGridView1.Columns[1].HeaderText = "转脉冲数/pulse:";
                dataGridView1.Columns[2].HeaderText = "加速度/(mm/s^2):";
                dataGridView1.Columns[3].HeaderText = "减速度/(mm/s^2):";
                dataGridView1.Columns[4].HeaderText = "速度/(mm/s):";
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
                    dataTable.Columns.Add("千脉冲行程/mm:", typeof(String));
                    dataTable.Columns.Add("转脉冲数/pulse", typeof(String));
                    dataTable.Columns.Add("加速度/(mm/s^2):", typeof(String));
                    dataTable.Columns.Add("减速度/(mm/s^2):", typeof(String));
                    dataTable.Columns.Add("速度/(mm/s):", typeof(String));
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);
                    dataTable.Rows.Add("1.00", "1500.00", "1000.00", "1000.00", "80.00"/*, "103"*/);//9号轴

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
            dataGridView1.TopLeftHeaderCell.Value = "电机配置";
            foreach (DataGridViewColumn column in dataGridView1.Columns)//20200331新增：取消列排列模式
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            // dataGridView1.Rows[1].HeaderCell.Value = String.Format("{0}", 1 + 1);/*"HHHH"*/ ;/*.Value = j.ToString();*/
            dataGridView1.RowHeadersWidth = 61;//行头宽度：85
            dataGridView1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;//所有的单元文字中心
            dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;//所有的列头中心
            for (int i = 0; i < dataGridView1.Columns.Count; i++)
            {
                int j = i + 1;
                dataGridView1.Columns[i].Width = 72;
            }
            //(4)dataGridView1控件颜色自定义：不同列设置不同的颜色（颜色设置）：20200331新增
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGreen;//20200331新增:
            dataGridView1.RowHeadersDefaultCellStyle.BackColor = Color.LightYellow;//20200331新增:
            dataGridView1.Columns[0].DefaultCellStyle.BackColor = Color.Orange;//20200331新增:不同列设置不同的颜色
            dataGridView1.Columns[1].DefaultCellStyle.BackColor = Color.LightBlue;//20200331新增:不同列设置不同的颜色
            dataGridView1.Columns[2].DefaultCellStyle.BackColor = Color.LightSalmon/*Color.PaleVioletRed*/;//20200331新增，不同列设置不同的颜色
            dataGridView1.Columns[3].DefaultCellStyle.BackColor = Color.LightPink/*Color.LightSeaGreen*//*Color.LightYellow*//*Color.LightBlue*//*Color.PaleVioletRed*/;//20200331新增，不同列设置不同的颜色
        }

        private void dataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)//自定义绘制dataGridView控件的行头
        {
            var grid = sender as DataGridView;
            var rowIdx = /*"喷头"+*/(e.RowIndex + 1).ToString() + "号轴";//20200331新增：

            var centerFormat = new StringFormat()
            {
                // right alignment might actually make more sense for numbers
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            e.Graphics.DrawString(rowIdx, this.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }

        public static void Delay(int milliSecond)
        {
            int start = Environment.TickCount;
            while (Math.Abs(Environment.TickCount - start) < milliSecond)//毫秒
            {
                Application.DoEvents();//可执行某无聊的操作
            }
        }
        private void 设备参数设置_Shown(object sender, EventArgs e)
        {
            Application.DoEvents();
        }

        /********************************************系统电气参数设置********************************************/
        private void UpdateComboBox()
        {
            //(1)新增DO端口//顺序是优化过的：20200312
            IEnumerable<XElement> address2 =
            from el in root.Descendants("DO")
                //where (string)el.Attribute("序号") == IT1.label.Text
            select el;
            foreach (XElement el in address2)
            {
                foreach (XElement al in el.Elements())
                {
                    string 记录1 = (string)al.Attribute("序号");
                    string 记录2 = (string)al.Attribute("端口名称");
                    //string[] row = { 记录1, 记录2 };
                    userControl32.comboBox1.Items.Add(记录1 + "：" + 记录2);
                    //userControl32.comboBox4.Items.Add(记录1 + "：" + 记录2);//20200309去除：非常有用，可以简化软件，并且提高软件的稳定性
                }
            }

            //(2)新增DI端口//顺序是优化过的：20200312
            IEnumerable<XElement> address =
            from el in root.Descendants("DI")
                //where (string)el.Attribute("序号") == IT1.label.Text
            select el;
            userControl32.comboBox1.Text = "输入端";//20200309新增：消除存在的bug
            userControl32.comboBox4.Text = "输出端";//20200309新增：消除存在的bug
            userControl32.comboBox4.Items.Clear();//20200309新增：消除存在的bug
            foreach (XElement el in address)
            {
                foreach (XElement al in el.Elements())
                {
                    string 记录1 = (string)al.Attribute("序号");
                    string 记录2 = (string)al.Attribute("端口名称");
                    //string[] row = { 记录1, 记录2 };
                    userControl32.comboBox1.Items.Add(记录1 + "：" + 记录2);
                    userControl32.comboBox4.Items.Add(记录1 + "：" + 记录2);
                }
            }

            //(3)新增AI端口//顺序是优化过的：20200312
            IEnumerable<XElement> address3 =
            from el in root.Descendants("AI")
                //where (string)el.Attribute("序号") == IT1.label.Text
            select el;
            foreach (XElement el in address3)
            {
                foreach (XElement al in el.Elements())
                {
                    string 记录1 = (string)al.Attribute("序号");
                    string 记录2 = (string)al.Attribute("端口名称");
                    //string[] row = { 记录1, 记录2 };
                    userControl32.comboBox1.Items.Add(记录1 + "：" + 记录2);
                    //userControl32.comboBox4.Items.Add(记录1 + "：" + 记录2);//20200309去除：非常有用，可以简化软件，并且提高软件的稳定性
                }
            }
        }
        //读取XML的元素及属性值，同时刷新所有的值。
        private XElement root;
        private void readXML()
        {
            root = XElement.Load("端口、一键启动配置.xml");
            //XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), CreateXElement0());
            //xdoc.Save("LINQ2XML测试-X子元素.xml");
            //XElement Di = new XElement("DI");           
            //XElement.Load("C:/Users/SummerGhost/Documents/Visual Studio 2017/Projects/LaserAdd_3DP_Software/2-SetParamDlg/bin/Release/LINQ2XML测试-X子元素.xml");
            //XDocument root = XDocument.Load("LINQ2XML测试-X子元素.xml");
            //(a)端口配置-数字输出端口：20200112
            foreach (Control IT in this.groupBox1.Controls)
            {
                if (IT is LabelTextboxCheckCompose.UserControl1)
                {
                    LabelTextboxCheckCompose.UserControl1 IT1 = (LabelTextboxCheckCompose.UserControl1)IT;

                    IEnumerable<XElement> address =
                        from el in root.Descendants(IT1.label.Text)
                            //where (string)el.Attribute("序号") == IT1.label.Text
                        select el;

                    foreach (XElement el in address)
                    {
                        IT1.textBox.Text = (string)el.Attribute("端口名称");

                        IT1.ComboBox.SelectedIndex = (int)el.Attribute("有效性");
                    }
                }
            }
            //(b)端口配置-数字输入端口：20200112
            foreach (Control IT in this.groupBox8.Controls)
            {
                if (IT is LabelTextboxCheckCompose.UserControl1)
                {
                    LabelTextboxCheckCompose.UserControl1 IT1 = (LabelTextboxCheckCompose.UserControl1)IT;
                    IEnumerable<XElement> address =
                        from el in root.Descendants(IT1.label.Text)
                            //where (string)el.Attribute("序号") == IT1.label.Text
                        select el;

                    foreach (XElement el in address)
                    {
                        IT1.textBox.Text = (string)el.Attribute("端口名称");
                        //IT1.ComboBox.Checked = (bool)el.Attribute("有效性"); 
                        IT1.ComboBox.SelectedIndex = (int)el.Attribute("有效性");
                        //Console.WriteLine(el);//没啥卵用
                    }
                }
            }
            //(c)端口配置 - 模拟输入端口：20200112
            foreach (Control IT in this.groupBox3.Controls)
            {
                if (IT is UserControl2.Ai控件)
                {
                    UserControl2.Ai控件 IT1 = (UserControl2.Ai控件)IT;
                    IEnumerable<XElement> address =
                        from el in root.Descendants(IT1.label1.Text)
                            //where (string)el.Attribute("序号") == IT1.label.Text
                        select el;

                    foreach (XElement el in address)
                    {
                        IT1.textBox1.Text = (string)el.Attribute("端口名称");
                        IT1.textBox2.Text = (string)el.Attribute("最小电压");
                        IT1.textBox3.Text = (string)el.Attribute("最大电压");
                        IT1.textBox4.Text = (string)el.Attribute("最小数值");
                        IT1.textBox5.Text = (string)el.Attribute("最大数值");
                        IT1.ComboBox.SelectedIndex = (int)el.Attribute("有效性");
                    }
                }
            }
            //(d)加载一键启动/关机配置文件：20200112
            OnekeyStartLoadXML();
        }

        private void SaveConfigBtn_Click(object sender, EventArgs e)
        {
            XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), CreateXElement0());//保存到xdoc中
            xdoc.Save("端口、一键启动配置.xml");
        }

        //CreateXElement()——————本行代码是创建配置文件的关键，将来的linq2xml在此处实现。
        private XElement CreateXElement0()//增加XML树，
        {
            //（1）Di端口配置
            XElement Di = new XElement("DI");
            foreach (Control IT in this.groupBox1.Controls)
            {
                if (IT is LabelTextboxCheckCompose.UserControl1)
                {
                    LabelTextboxCheckCompose.UserControl1 IT1 = (LabelTextboxCheckCompose.UserControl1)IT;

                    XElement ChildDI = new XElement(IT1.label.Text);
                    //XElement ChildDI = new XElement("我靠");
                    ChildDI.Add(new XAttribute("序号", IT1.Tag /*IT1.label.Text*/));//20200719修改：序号需要进行必要的修改
                    ChildDI.Add(new XAttribute("端口名称", IT1.textBox.Text));
                    //ChildDI.Add(new XAttribute("有效性", IT1.ComboBox.Checked));
                    ChildDI.Add(new XAttribute("有效性", IT1.ComboBox.SelectedIndex));
                    //ChildDI.Add(new XElement("子元素1","1"));
                    Di.Add(ChildDI);
                }
            }

            //查询元素并排序  
            var query = Di.Elements()
                                 //.Where(e => Convert.ToInt32(e.Attribute("BookID").Value.Substring(e.Attribute("BookID").Value.Length - 1, 1)) > 1)
                                 //.OrderByDescending(e => (string)e.Element("BookName"))//按照降序排列
                                 .OrderBy(e => (string)e.Attribute("序号"))//按照升序排列
                                 .ToList();
            Di.RemoveAll();
            Di.Add(query);

            //（2）Do端口配置
            XElement Do = new XElement("DO");
            foreach (Control IT in this.groupBox8.Controls)
            {
                if (IT is LabelTextboxCheckCompose.UserControl1)
                {
                    LabelTextboxCheckCompose.UserControl1 IT1 = (LabelTextboxCheckCompose.UserControl1)IT;

                    XElement ChildDO = new XElement(IT1.label.Text);
                    //XElement ChildDO = new XElement("我靠1");
                    ChildDO.Add(new XAttribute("序号", IT1.Tag/*IT1.label.Text*/));//20200719修改：序号需要进行必要的修改
                    ChildDO.Add(new XAttribute("端口名称", IT1.textBox.Text));
                    //ChildDO.Add(new XAttribute("有效性", IT1.ComboBox.Checked));
                    ChildDO.Add(new XAttribute("有效性", IT1.ComboBox.SelectedIndex));

                    Do.Add(ChildDO);
                }
            }

            //查询元素并排序  
            query = Do.Elements()
                                 //.Where(e => Convert.ToInt32(e.Attribute("BookID").Value.Substring(e.Attribute("BookID").Value.Length - 1, 1)) > 1)
                                 //.OrderByDescending(e => (string)e.Element("BookName"))//按照降序排列
                                 .OrderBy(e => (string)e.Attribute("序号"))//按照升序排列
                                 .ToList();
            Do.RemoveAll();
            Do.Add(query);

            //（3）Ai端口配置
            XElement Ai = new XElement("AI");
            foreach (Control IT in this.groupBox3.Controls)
            {
                if (IT is UserControl2.Ai控件)
                {
                    UserControl2.Ai控件 IT1 = (UserControl2.Ai控件)IT;
                    XElement ChildAI = new XElement(IT1.label1.Text);
                    //XElement ChildAI = new XElement("我靠4");
                    ChildAI.Add(new XAttribute("序号", IT1.Tag/*IT1.label1.Text*/));//20200719修改：序号需要进行必要的修改
                    ChildAI.Add(new XAttribute("端口名称", IT1.textBox1.Text));
                    ChildAI.Add(new XAttribute("最小电压", IT1.textBox2.Text));
                    ChildAI.Add(new XAttribute("最大电压", IT1.textBox3.Text));
                    ChildAI.Add(new XAttribute("最小数值", IT1.textBox4.Text));
                    ChildAI.Add(new XAttribute("最大数值", IT1.textBox5.Text));
                    //ChildAI.Add(new XAttribute("有效性", IT1.checkBox1.Checked));
                    ChildAI.Add(new XAttribute("有效性", IT1.ComboBox.SelectedIndex));
                    //ChildDI.Add(new XElement("子元素1","1"));
                    Ai.Add(ChildAI);
                }
            }

            //查询元素并排序  
            query = Ai.Elements()
                                 //.Where(e => Convert.ToInt32(e.Attribute("BookID").Value.Substring(e.Attribute("BookID").Value.Length - 1, 1)) > 1)
                                 //.OrderByDescending(e => (string)e.Element("BookName"))//按照降序排列
                                 .OrderBy(e => (string)e.Attribute("序号"))//按照升序排列
                                 .ToList();
            Ai.RemoveAll();
            Ai.Add(query);

            //（4）OnekeyStart端口配置
            OnekeyStart.RemoveAll();
            this.OnekeyStartSaveXML();//20200112：精华具体保存一键启动和一键终止策略

            XElement rootAutomation = new XElement("rootAutomation", Di, Do, Ai, OnekeyStart);

            return rootAutomation;//所有的文件，都保存在这里
        }

        private XElement OnekeyStart = new XElement("OnekeyStart");//类全局变量：一键启动配置XML文件
        //加载一键启动配置文件//(d)加载一键启动/关机配置文件：20200112
        private void OnekeyStartLoadXML()//加载一键启动配置文件
        {
            //this.listView1.BeginUpdate();   //数据更新，UI暂时挂起，直到EndUpdate绘制控件，可以有效避免闪烁并大大提高加载速度
            IEnumerable<XElement> address =
                    from el in root.Descendants("OnekeyStart")
                        //where (string)el.Attribute("序号") == IT1.label.Text
                    select el;
            foreach (XElement el in address)
            {
                foreach (XElement al in el.Elements())
                {
                    string 记录1 = (string)al.Attribute("序号");
                    string 记录2 = (string)al.Attribute("功能描述");
                    string[] row = { 记录1, 记录2 };
                    ListViewItem item = new ListViewItem(row);
                    this.listView1.Items.Add(item);

                    //20200112:加载对应的功能码
                    ulong tempMask = (ulong)al.Attribute("功能码");//20200112:加载对应的功能码
                    ControlMask.Add(tempMask);//20200112:加载对应的功能码

                    ListViewId++;
                }
            }
            //this.listView1.EndUpdate();  //结束数据处理，UI界面一次性绘制。

            if (ListViewId > 0)
            {
                deletebutton.Enabled = true;//当插入新行时，唤醒删除按钮。
                updatebutton.Enabled = true;//当插入新行时，唤醒修改按钮。
            }
            else
            {
                deletebutton.Enabled = false;//当插入新行时，唤醒删除按钮。
                updatebutton.Enabled = false;//当插入新行时，唤醒修改按钮。
            }
        }

        private void OnekeyStartSaveXML()
        {
            for (int i = 0; i < this.listView1.Items.Count; i++)
            {
                XElement ChildOnekeyStart = new XElement("OnekeyStart" + listView1.Items[i].SubItems[0].Text);
                ChildOnekeyStart.Add(new XAttribute("序号", listView1.Items[i].SubItems[0].Text));
                ChildOnekeyStart.Add(new XAttribute("功能描述", listView1.Items[i].SubItems[1].Text));
                ChildOnekeyStart.Add(new XAttribute("功能码", ControlMask[i]));//20200112新增
                OnekeyStart.Add(ChildOnekeyStart);
            }
        }
        /***********************************listView维护**************************************/
        private int ListViewId = 0;//正确的来讲，用于记录行数
        List<ulong> ControlMask = new List<ulong>();//最多存储256条逻辑记录——不再使用数组修饰
        //生成指定格式的掩码：
        private void WriteControlMask(int i/*第i条逻辑记录*/)//根据6个控件的ID来生成MASK
        {
            try { 
                //(3)PS:按照顺序转换为对应的掩码数组：
                //(3)掩码结构（64位）：
                //序号(8bit,256个)|开始条件判断(8bit，256个+4bit，16个逻辑+8bit，256个)
                //|延时值（16bit,65536s=18.2h）|触发动作(8bit,256种动作)|动作有效状态（2bit,开关4种状态）
                //8+20+16+8+2=54bit————高位填充10bit的Od1111111111

                //20200112：按照技术规格编写对应的掩码值：
                //(1)读取对应的掩码区域值：
                //ulong source  = (ulong)this.userControl32.comboBox1.Items.IndexOf(this.userControl32.comboBox1.Text);//8位
                //ulong source = (ulong)this.userControl32.comboBox1.Tag;//8位//20210312新增修改：修改GPO异常错误
                string sourceText = this.userControl32.comboBox1.Text;
                string[] sub1 = Regex.Split(sourceText, "：", RegexOptions.IgnoreCase);
                ulong source = (ulong)Convert.ToInt64(sub1[0]);

                ulong logic1 = (ulong)this.userControl32.comboBox2.Items.IndexOf(this.userControl32.comboBox2.Text);//4位——多种逻辑
                ulong logic2 = (ulong)this.userControl32.comboBox3.Items.IndexOf(this.userControl32.comboBox3.Text);//8位
                ulong timeout=(ulong)Convert.ToInt64(this.userControl32.textBox1.Text);//16位

                //ulong control = (ulong)this.userControl32.comboBox4.Items.IndexOf(this.userControl32.comboBox4.Text);//8位
                //ulong control = (ulong)this.userControl32.comboBox4.Tag;//8位//20210312新增修改：修改GPO异常错误
                string controlText = this.userControl32.comboBox4.Text;
                string[] sub2 = Regex.Split(controlText, "：", RegexOptions.IgnoreCase);
                ulong control = (ulong)Convert.ToInt64(sub2[0]);

                ulong state = (ulong)this.userControl32.comboBox5.Items.IndexOf(this.userControl32.comboBox5.Text);//2位
                //(2)写入到掩码值：
                ulong tempMask = 0x1FFFC00000000000;//初始MASK，高10bit置1
                ControlMask[i] = (tempMask)
                    |((0x3FC000000000) &(source<<38))
                    |((0x3C00000000) & (logic1<<34)) 
                    |((0x3FC000000) & (logic2)<<26)
                    |((0x3FFFC00) & (timeout)<<10) 
                    |((0x3FC) & (control)<<2)
                    |((0x3) & (state)<<0);//写入souce的掩码
            }
            catch (Exception e)//提示潜在的输入错误
            {
                MessageBox.Show(e.ToString());
            }
        }

        //（4）一键启动配置：添加——在最后一行添加记录
        private void AddBtn_Click(object sender, EventArgs e)//（4）一键启动配置：添加
        {
            ListViewId++;
            string 记录 = "当"
                + this.userControl32.comboBox1.Text
                + this.userControl32.comboBox2.Text + "时，"
                + this.userControl32.comboBox3.Text
                + this.userControl32.textBox1.Text + "，"
                + "触发动作："
                + this.userControl32.comboBox4.Text
                + this.userControl32.comboBox5.Text;
            string[] row = { ListViewId.ToString(), 记录 };
            ListViewItem item = new ListViewItem(row);
            this.listView1.Items.Add(item);
            //this.listView1.EndUpdate();  //结束数据处理，UI界面一次性绘制。

            //最后1行新增Mask
            //20200112:生成对应的掩码
            ControlMask.Insert(ListViewId - 1, 0);//20200112:insert对应的掩码数据
            WriteControlMask(ListViewId-1);//20200112:生成对应的掩码

            if (ListViewId > 0)
            {
                deletebutton.Enabled = true;//当插入新行时，唤醒删除按钮。
                updatebutton.Enabled = true;//当插入新行时，唤醒修改按钮。
            }
            else
            {
                deletebutton.Enabled = false;//当插入新行时，唤醒删除按钮。
                updatebutton.Enabled = false;//当插入新行时，唤醒修改按钮。
            }
        }

        //（4）一键启动配置：修改
        private void UpdateBtn_Click(object sender, EventArgs e)//（4）一键启动配置：修改
        {
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                listView1.SelectedItems[0].SubItems[1].Text = "当"
                  + this.userControl32.comboBox1.Text
                  + this.userControl32.comboBox2.Text + "时，"
                  + this.userControl32.comboBox3.Text
                  + this.userControl32.textBox1.Text + "，"
                  + "触发动作："
                  + this.userControl32.comboBox4.Text
                  + this.userControl32.comboBox5.Text;

                int number = listView1.SelectedItems[0].Index;     //用于记录选中行号，加一是因为本来是从0开始计数的。
                
                //指定行修改后的Mask
                //ControlMask.RemoveAt(number);//20200112:insert对应的掩码数据
                WriteControlMask(number);//20200112:生成对应的掩码
            }
        }

        //（4）一键启动配置：插入，在指定行下一行新建记录
        private void InsertBtn_Click(object sender, EventArgs e)
        {
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                int number = listView1.SelectedItems[0].Index;

                string 记录 = "当"
                    + this.userControl32.comboBox1.Text
                    + this.userControl32.comboBox2.Text + "时，"
                    + this.userControl32.comboBox3.Text
                    + this.userControl32.textBox1.Text + "，"
                    + "触发动作："
                    + this.userControl32.comboBox4.Text
                    + this.userControl32.comboBox5.Text;
                //string[] row = { ListViewId.ToString(), 记录 };
                string[] row = { "", 记录 };
                ListViewItem item = new ListViewItem(row);

                //item.SubItems.Add("");              //  给新增的行第4列添加数据
                this.listView1.Items.Insert(number+1, item);     //     将新增的对象item插入到指定行

                //指定行修新增Mask
                //20200112:生成对应的掩码
                ControlMask.Insert(number+1, 0);//20200112:insert对应的掩码数据
                WriteControlMask(number+1);//20200112:生成对应的掩码

                this.listView1.BeginUpdate();   //数据更新，UI暂时挂起，直到EndUpdate绘制控件，可以有效避免闪烁并大大提高加载速度
                ListViewId++;//记录总行数    
                for (int i = 0; i < ListViewId; i++)             // 重新绘制行号，不影响第二，第三列的内容
                {
                    this.listView1.Items[i].SubItems[0].Text = (i + 1).ToString();     //添加行号
                }
                this.listView1.EndUpdate();  //结束数据处理，UI界面一次性绘制。                               
                                             //listView1.SelectedItems.Clear();          //清空表格行的选择状态
                if (ListViewId > 0)
                {
                    deletebutton.Enabled = true;//当插入新行时，唤醒删除按钮。
                    updatebutton.Enabled = true;//当插入新行时，唤醒修改按钮。
                }
                else
                {
                    deletebutton.Enabled = false;//当插入新行时，唤醒删除按钮。
                    updatebutton.Enabled = false;//当插入新行时，唤醒修改按钮。
                }
            }
            else
            {
                //MessageBox.Show(this, "前选择要在哪一行后进行插入", "信息提示",MessageBoxButtons.OK, MessageBoxIcon.Information);
            }


        }
        //（4）一键启动配置：删除
        private void DeleteBtn_Click(object sender, EventArgs e)//（4）一键启动配置：删除
        {
            //此处存在问题。如果双击直接操作，就奔溃了。是因为，此时没有选中正确的index。
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                int number = listView1.SelectedItems[0].Index;     //用于记录选中行号，加一是因为本来是从0开始计数的。
                if (MessageBox.Show("确定要删除本条记录：“序号：" + this.listView1.Items[number].SubItems[0].Text + "功能描述："
                    + this.listView1.Items[number].SubItems[1].Text
                    + "”?", "DELETE", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    listView1.Items.RemoveAt(number);
                    //listView1.Items.RemoveAt(listView1.SelectedIndices[0]);

                    ListViewId--;//记录总行数

                    //指定行删除Mask
                    //20200112:生成对应的掩码
                    ControlMask.RemoveAt(number);//20200112:insert对应的掩码数据
                    //WriteControlMask(number);//20200112:生成对应的掩码

                
                    this.listView1.BeginUpdate();   //数据更新，UI暂时挂起，直到EndUpdate绘制控件，可以有效避免闪烁并大大提高加载速度
                    for (int i = 0; i < ListViewId; i++)             // 重新绘制行号，不影响第二，第三列的内容
                    {
                        this.listView1.Items[i].SubItems[0].Text = (i + 1).ToString();     //添加行号
                    }
                    this.listView1.EndUpdate();  //结束数据处理，UI界面一次性绘制。                                                                        
                }

                if (ListViewId > 0)
                {
                    deletebutton.Enabled = true;//当插入新行时，唤醒删除按钮。
                    updatebutton.Enabled = true;//当插入新行时，唤醒修改按钮。
                }
                else
                {
                    deletebutton.Enabled = false;//当插入新行时，唤醒删除按钮。
                    updatebutton.Enabled = false;//当插入新行时，唤醒修改按钮。
                }
            }
        }

        /******************************电机参数设定**************************************/
        public MotorParameter 默认电机参数 = new MotorParameter()
        { 千脉冲行程 = 0, 周脉冲数 = 0, 加速度 = 0, 减加速度 = 0, 速度 = 0 };

        public BJParameter 默认喷墨参数 = new BJParameter() { UV功率 = 0, 正压力值 = 0, 负压力值 = 0 };

#region 初始化链表参数
        public List<MotorParameter> 粉料电机1 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=2000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<MotorParameter> 粉料电机2 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=1000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<MotorParameter> 成型缸电机 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=1000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<MotorParameter> 锁紧电机 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=1000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<MotorParameter> 刮液电机 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=1000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<MotorParameter> 导轨电机01 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=1000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<MotorParameter> 导轨电机02 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=1000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<MotorParameter> 铺粉电机 = new List<MotorParameter>()
            { new MotorParameter(){ 千脉冲行程=1,周脉冲数=1000 ,加速度=0.5,减加速度=0.5,速度=10},
             };

        List<BJParameter> BJ = new List<BJParameter>() { new BJParameter() { 正压力值 = 10, 负压力值 = -10, UV功率 = 30 } };
#endregion
        void SetMotorParater(List<MotorParameter> ListMP, DataGridView DGV, int n)
        {
            //新内容
            BindingList<MotorParameter> MP;
            MP = new BindingList<MotorParameter>(ListMP);
            DGV.GridColor = Color.Blue;
            DGV.DataSource = MP;
            for (int i = 0; i < n; i++)
            {//有多少变量就需要设置多少个列宽
                DGV.Columns[i].Width = 85;//设置列宽
            }
        }
        void SetMotorParater(List<BJParameter> ListMP, DataGridView DGV, int n)
        {
            BindingList<BJParameter> MP;
            MP = new BindingList<BJParameter>(ListMP);
            DGV.GridColor = Color.Blue;
            DGV.DataSource = MP;
            for (int i = 0; i < n; i++)
            {//有多少变量就需要设置多少个列宽
                DGV.Columns[i].Width = 85;//设置列宽
            }
        }
        public void ResetVaule(DataGridView DGV, BindingList<MotorParameter> BList)
        {
            //获取行对象后
            List<MotorParameter> modiObj = DGV.CurrentRow.DataBoundItem as List<MotorParameter>;
            int pos = DGV.CurrentRow.Index; //记位置
            modiObj[pos].千脉冲行程 = 100; //修改值
            BList.RemoveAt(pos); //删除行
            BList.Insert(pos, modiObj[pos]);//添加修改后的行到指定位置， 不指定位置默认添加到最后
            DGV.DataBindings.Add("DataSource", this, "blogNewsRegardUI", false, DataSourceUpdateMode.OnPropertyChanged);
        }
        void SetMotorParater(BindingList<MotorParameter> LMP, DataGridView DGV, int n)
        {
            //新内容
            DGV.GridColor = Color.Blue;
            DGV.DataSource = LMP;
            for (int i = 0; i < n; i++)
            {   //有多少变量就需要设置多少个列宽
                DGV.Columns[i].Width = 85;//设置列宽
            }
        }
        public BindingList<MotorParameter> fen1 { get; set; }//get和set是必须的

        //private void Form1_Load(object sender, EventArgs e)
        //{
        //    #region 选项卡设置
        //    //选项卡1
        //    tabControl3.TabPages[0].Text = "粉料电机1";
        //    tabControl3.TabPages[1].Text = "成型缸电机";
        //    tabControl3.TabPages[2].Text = "粉料电机2";
        //    fen1 = new BindingList<MotorParameter>(粉料电机1);
        //    //绑定数据集合
        //    Motor1Parameter.DataBindings.Add("DataSource", this, "fen1", false, DataSourceUpdateMode.OnPropertyChanged);

        //    SetMotorParater(成型缸电机, 成型DGV, 5);
        //    SetMotorParater(粉料电机2, 粉2DGV, 5);

        //    //选项卡2
        //    tabControl2.TabPages[0].Text = "锁紧电机";
        //    tabControl2.TabPages[1].Text = "铺粉电机";
        //    tabControl2.TabPages[2].Text = "刮液电机";
        //    SetMotorParater(锁紧电机, 锁紧DGV, 5);
        //    SetMotorParater(铺粉电机, 铺粉DGV, 5);
        //    SetMotorParater(刮液电机, 刮液DGV, 5);

        //    //选项卡3
        //    tabControl4.TabPages[0].Text = "导轨电机1";
        //    tabControl4.TabPages[1].Text = "导轨电机2";
        //    tabControl4.TabPages[2].Text = "喷墨系统参数";
        //    SetMotorParater(导轨电机01, 导轨1DGV, 5);
        //    SetMotorParater(导轨电机02, 导轨2DGV, 5);
        //    SetMotorParater(BJ, 喷墨DGV, 3);
        //    #endregion
        //}

        private void SystemParameter_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {



        }

        public void DelectRow(DataGridView DGV, BindingList<MotorParameter> BList)
        {
            DataGridViewSelectedRowCollection rows = DGV.SelectedRows;

            foreach (DataGridViewRow row in rows)
            {
                if (row.Index < BList.Count)
                { BList.RemoveAt(row.Index); }
                else { MessageBox.Show("请选择有效的行"); }
            }

        }

        public void DelectRow(DataGridView DGV, BindingList<BJParameter> BList)
        {
            DataGridViewSelectedRowCollection rows = DGV.SelectedRows;

            foreach (DataGridViewRow row in rows)
            {
                if (row.Index < BList.Count)
                { BList.RemoveAt(row.Index); }
                else { MessageBox.Show("请选择有效的行"); }
            }

        }

        private void button7_Click(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {

        }


        private void groupBox6_Enter(object sender, EventArgs e)
        {

        }

        private void ElecConfigPage_Click(object sender, EventArgs e)
        {

        }
    }
}
