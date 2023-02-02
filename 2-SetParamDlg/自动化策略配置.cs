using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Xml.Linq;

namespace SetParamDlg_模仿李老师_
{
    public partial class 自动化策略配置 : Form
    {
        public 自动化策略配置()
        {
            this.SetStyle(
                ControlStyles.UserPaint 
                | ControlStyles.AllPaintingInWmPaint 
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.DoubleBuffer 
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable
                | ControlStyles.SupportsTransparentBackColor,
                true);
            //this.SetStyle( ControlStyles.DoubleBuffer, true);
            UpdateStyles();
            InitializeComponent();

            this.listView1.View = View.Details;
            this.listView1.FullRowSelect = true;
            listView1.Columns.Add("序号", 50);
            listView1.Columns.Add("功能描述", 600);
            //deletebutton.Enabled = false;//当插入新行时，唤醒删除按钮。
            //updatebutton.Enabled = false;//当插入新行时，唤醒修改按钮。
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
            //readXML();//读取XML的元素及属性值，同时刷新所有的值。
            //Invalidate();
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
        //load是初始化时执行；shown是显示时执行。
        private void 自动化策略配置_Load(object sender, EventArgs e)
        {
            Timer = new System.Windows.Forms.Timer() { Interval = 40 };
            Timer.Tick += new EventHandler(Timer_Tick);
            base.Opacity = 0;
            Timer.Start();

            readXML();//读取XML的元素及属性值，同时刷新所有的值。
            UpdateComboBox();
            //更新UserControl3中combox的items

        }

        private void UpdateComboBox()
        {
            //(1)新增DI端口
            IEnumerable<XElement> address =
            from el in root.Descendants("DI")
                        //where (string)el.Attribute("序号") == IT1.label.Text
                    select el;
            foreach (XElement el in address)
            {
                foreach (XElement al in el.Elements())
                {
                    string 记录1 = (string)al.Attribute("序号");
                    string 记录2 = (string)al.Attribute("端口名称");
                    //string[] row = { 记录1, 记录2 };
                    userControl32.comboBox1.Items.Add(记录1+"："+记录2);
                    userControl32.comboBox4.Items.Add(记录1+"："+记录2);

                    //ListViewItem item = new ListViewItem(row);
                    //this.listView1.Items.Add(item);
                    //ListViewId++;
                }
            }

            //(2)新增DO端口
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
                    userControl32.comboBox4.Items.Add(记录1 + "：" + 记录2);
                }
            }

            //(3)新增AI端口
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
                    userControl32.comboBox4.Items.Add(记录1 + "：" + 记录2);
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
                        //IT1.ComboBox.Checked = (bool)el.Attribute("有效性");//
                        IT1.ComboBox.SelectedIndex = (int)el.Attribute("有效性");//
                        //Console.WriteLine(el);//没啥卵用
                    }
                    //IT1.textBox.Text ="我靠";
                    //IT1.checkBox.Checked = true;
                    //IEnumerable<XElement> address =
                    //    from el in root.Elements("rootAutomation")//输入数据源
                    //    where
                    //        //(string)el.Element("序号") == IT1.label.Text
                    //        (from add in el.Elements("DI")
                    //         where
                    //         (string)add.Attribute("序号") == IT1.label.Text
                    //         select add)
                    //         .Any()
                    //    select el;

                    //IEnumerable<XElement> address =
                        //from el in root.Elements("DI")
                        //where
                        //    (from add in el.Elements("配置")
                        //     where
                        //         (string)add.Attribute("序号") == IT1.label.Text
                        //     //(string)add.Attribute("序号") == "Shipping" &&
                        //     //(string)add.Element("State") == "NY"
                        //     select add)
                        //    .Any()
                        //select el;
                        //Console.WriteLine(root);//没啥卵用

                    //var address =
                    //    from el in root.Elements("DI")
                    //    where (string)el.Attribute("序号") == IT1.label.Text
                    //    select el;
                }
            }

            //XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), CreateXElement0());
            //xdoc.Save("LINQ2XML测试-X子元素.xml");
            //XElement Do = new XElement("DO");
            foreach (Control IT in this.groupBox2.Controls)
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

            //XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), CreateXElement0());
            //xdoc.Save("LINQ2XML测试-X子元素.xml");
            //XElement Ai = new XElement("AI");
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
                        //IT1.checkBox1.Checked = (bool)el.Attribute("有效性");
                        IT1.ComboBox.SelectedIndex = (int)el.Attribute("有效性");                       
                        //Console.WriteLine(el);//没啥卵用
                    }
                }
            }
            
            //加载一键启动配置文件
            OnekeyStartLoadXML();
        }

        ////存储地址
        //public static readonly string ArchiveDir = Application.UserAppDataPath + "/ReadingData";//修改

        private void button19_Click(object sender, EventArgs e)
        {
            XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), CreateXElement0());
            xdoc.Save("端口、一键启动配置.xml");

            //LINQ to XML,是本技术的实现方式，实现针对XML文件的增删改查，在项目中的作用非常突出。
            //LINQ to XML,是本软件的实现方式。
            /************************************XML Document读写方法******************************************
            if (!File.Exists(ArchiveDir))
            {

            }
            //(1)创建XML
            XmlDocument xml = new XmlDocument();
            //(2)创建root根节点。
            XmlElement root = xml.CreateElement("InputaOutputConfigure");

            //第一步：生成DigitalInput的配置：
            //(3)创建item子根节点

            XmlElement item;
            item = xml.CreateElement("DigitalInputConfigure");
            ////(4)创建X个子节点
            //XmlElement itemS = xml.CreateElement("DigitalInputConfigure");
            ////写入X个子节点的element值
            //foreach()
            foreach (Control IT in groupBox1.Controls)
            {
                if (IT is LabelTextboxCheckCompose.UserControl1)
                {
                    LabelTextboxCheckCompose.UserControl1 IT1 = (LabelTextboxCheckCompose.UserControl1)IT;
                    DInputInfo itemInfoS = new DInputInfo();
                    itemInfoS.ID = IT1.label.Text;
                    itemInfoS.Name = IT1.textBox.Text;
                    itemInfoS.ActiveState = IT1.checkBox.Checked;

                    XmlElement element1 = xml.CreateElement(itemInfoS.ID);//写入name
                    element1.SetAttribute("ID", itemInfoS.ID);
                    element1.SetAttribute("端口名称", itemInfoS.Name);
                    element1.SetAttribute("有效性", itemInfoS.ActiveState.ToString());
                    item.AppendChild(element1);//(5)写完element

                    ////XmlAttribute attr = xml.CreateAttribute( "nil");//不换行
                    ////attr.Value = "true";//不换行
                    ////element1.SetAttributeNode(attr);//不换行
                    //element1.InnerText = itemInfoS.Name;//写入value
                    //itemS.AppendChild(element1);//(5)写完element
                    
                    //XmlElement element2 = xml.CreateElement(itemInfoS.ID);//写入name
                    ////XmlAttribute attr2 = xml.CreateAttribute("nil");//不换行
                    ////attr2.Value = "true";//不换行
                    ////element2.SetAttributeNode(attr2);//不换行
                    //element2.InnerText = itemInfoS.ActiveState.ToString();//写入value
                    //itemS.AppendChild(element2);//(5)写完element
                }
            }
            //item.AppendChild(itemS);//（6）写完ELement
            root.AppendChild(item);//（7）写完item

            //第二步：生成DigitalOutput的配置：
            //(3)创建item子根节点
            XmlElement item2;
            item2 = xml.CreateElement("DigitalOutputConfigure");
            //xml.InsertAfter(item2,item);

            ////(4)创建X个子节点
            //XmlElement itemS = xml.CreateElement("DigitalOutputConfigure");
            ////写入X个子节点的element值
            //foreach()
            foreach (Control IT in groupBox2.Controls)
            {
                if (IT is LabelTextboxCheckCompose.UserControl1)
                {
                    LabelTextboxCheckCompose.UserControl1 IT1 = (LabelTextboxCheckCompose.UserControl1)IT;
                    DInputInfo itemInfoS = new DInputInfo();
                    itemInfoS.ID = IT1.label.Text;
                    itemInfoS.Name = IT1.textBox.Text;
                    itemInfoS.ActiveState = IT1.checkBox.Checked;

                    XmlElement element1 = xml.CreateElement(itemInfoS.ID);//写入name
                    element1.SetAttribute("ID", itemInfoS.ID);
                    element1.SetAttribute("端口名称", itemInfoS.Name);
                    element1.SetAttribute("有效性", itemInfoS.ActiveState.ToString());
                    item.AppendChild(element1);//(5)写完element

                    ////XmlAttribute attr = xml.CreateAttribute( "nil");//不换行
                    ////attr.Value = "true";//不换行
                    ////element1.SetAttributeNode(attr);//不换行
                    //element1.InnerText = itemInfoS.Name;//写入value
                    //itemS.AppendChild(element1);//(5)写完element

                    //XmlElement element2 = xml.CreateElement(itemInfoS.ID);//写入name
                    ////XmlAttribute attr2 = xml.CreateAttribute("nil");//不换行
                    ////attr2.Value = "true";//不换行
                    ////element2.SetAttributeNode(attr2);//不换行
                    //element2.InnerText = itemInfoS.ActiveState.ToString();//写入value
                    //itemS.AppendChild(element2);//(5)写完element
                }
            }
            //item.AppendChild(itemS);//（6）写完ELement
            root.AppendChild(item2);//（7）写完item

            xml.AppendChild(root);//（8）写入完root
            xml.Save(ArchiveDir + ".xml");//（9）保存到xml文件
            **************************************XML Document读写方法***************************************/
        }

        ///// <summary>
        ///// 生成XML文件
        ///// </summary>
        ///// <param name="XmlFile">XML保存的路径</param>
        //private static void CreateXmlFile(string XmlFile)
        //{
        //    XDocument xdoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), CreateXElement());
        //    xdoc.Save(XmlFile);
        //}

        ////CreateXElement()——————本行代码是创建配置文件的关键，将来的linq2xml在此处实现。
        //private XElement CreateXElement1()//增加XML树
        //{
        //    XElement root = new XElement("Root", 
        //        new XElement("User1", new XElement("UserID", "1"), new XElement("UserName", "踏浪帅")),
        //        new XElement("User2", new XElement("UserID", "2"), new XElement("UserName", "wujunyang")),
        //        new XElement("User3", new XElement("UserID", "3"), new XElement("UserName", "cnblogs")));
        //    return root;
        //}
        ////CreateXElement()——————本行代码是创建配置文件的关键，将来的linq2xml在此处实现。
        //private XElement CreateXElement2()//增加XML树，
        //{
        //    XElement root = new XElement("Root", 
        //        new XElement("User1", new XAttribute("UserName", "wujy"), new XAttribute("PassWord", "76543"), new XAttribute("Age", "30")),
        //        new XElement("User2", new XAttribute("UserName", "cnblogs"), new XAttribute("PassWord", "23456"), new XAttribute("Age", "26")),
        //        new XElement("User3", new XAttribute("UserName", "踏浪帅"), new XAttribute("PassWord", "4567"), new XAttribute("Age", "34")));
        //    return root;
        //}

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
                    //DInputInfo itemInfoS = new DInputInfo();
                    //itemInfoS.ID = IT1.label.Text;
                    //itemInfoS.Name = IT1.textBox.Text;
                    //itemInfoS.ActiveState = IT1.checkBox.Checked;
                    //XElement ChildDI = new XElement(IT1.label.Text, new XAttribute("A","1"));
                    XElement ChildDI = new XElement(IT1.label.Text);
                    //XElement ChildDI = new XElement("我靠");
                    ChildDI.Add(new XAttribute("序号", IT1.label.Text));
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
            //var query = from p in Do.Elements("配置")
            //            //where 1 == 1
            //            select p;
            //query.OrderBy(e => e.Element("序号"));


            //（2）Do端口配置
            XElement Do = new XElement("DO");
            foreach (Control IT in this.groupBox2.Controls)
            {
                if (IT is LabelTextboxCheckCompose.UserControl1)
                {
                    LabelTextboxCheckCompose.UserControl1 IT1 = (LabelTextboxCheckCompose.UserControl1)IT;
                    //DInputInfo itemInfoS = new DInputInfo();
                    //itemInfoS.ID = IT1.label.Text;
                    //itemInfoS.Name = IT1.textBox.Text;
                    //itemInfoS.ActiveState = IT1.checkBox.Checked;
                    //XElement ChildDI = new XElement(IT1.label.Text, new XAttribute("A","1"));
                    XElement ChildDO = new XElement(IT1.label.Text);
                    //XElement ChildDO = new XElement("我靠1");
                    ChildDO.Add(new XAttribute("序号", IT1.label.Text));
                    ChildDO.Add(new XAttribute("端口名称", IT1.textBox.Text));
                    //ChildDO.Add(new XAttribute("有效性", IT1.ComboBox.Checked));
                    ChildDO.Add(new XAttribute("有效性", IT1.ComboBox.SelectedIndex));
                    //IT1.ComboBox.SelectedIndex;
                    //ChildDI.Add(new XElement("子元素1","1"));
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
                    //DInputInfo itemInfoS = new DInputInfo();
                    //itemInfoS.ID = IT1.label.Text;
                    //itemInfoS.Name = IT1.textBox.Text;
                    //itemInfoS.ActiveState = IT1.checkBox.Checked;
                    //XElement ChildDI = new XElement(IT1.label.Text, new XAttribute("A","1"));
                    XElement ChildAI = new XElement(IT1.label1.Text);
                    //XElement ChildAI = new XElement("我靠4");
                    ChildAI.Add(new XAttribute("序号", IT1.label1.Text));
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
            this.OnekeyStartSaveXML();

            XElement rootAutomation = new XElement("rootAutomation",Di,Do,Ai,OnekeyStart);
            //rootAutomation.Add(DI);
            //rootAutomation.Add(DO);
            //rootAutomation.Add(AI);
            //rootAutomation.Add(OnekeyStart);
            return rootAutomation;
        }

        private XElement OnekeyStart = new XElement("OnekeyStart");//类全局变量：一键启动配置XML文件
        
        //加载一键启动配置文件
        private void OnekeyStartLoadXML()//加载一键启动配置文件
        {
            //foreach (Control IT in this.groupBox3.Controls)
            //{
            //    if (IT is UserControl2.Ai控件)
            //    {
            //        UserControl2.Ai控件 IT1 = (UserControl2.Ai控件)IT;
            //        IEnumerable<XElement> address =
            //            from el in root.Descendants(IT1.label1.Text)
            //                //where (string)el.Attribute("序号") == IT1.label.Text
            //            select el;

            //        foreach (XElement el in address)
            //        {
            //            IT1.textBox1.Text = (string)el.Attribute("端口名称");
            //            IT1.textBox2.Text = (string)el.Attribute("最小电压");
            //            IT1.textBox3.Text = (string)el.Attribute("最大电压");
            //            IT1.textBox4.Text = (string)el.Attribute("最小数值");
            //            IT1.textBox5.Text = (string)el.Attribute("最大数值");
            //            IT1.checkBox1.Checked = (bool)el.Attribute("有效性");
            //            //Console.WriteLine(el);//没啥卵用
            //        }
            //    }
            //}


            //this.listView1.BeginUpdate();   //数据更新，UI暂时挂起，直到EndUpdate绘制控件，可以有效避免闪烁并大大提高加载速度
            IEnumerable<XElement> address =
                    from el in root.Descendants("OnekeyStart")
                        //where (string)el.Attribute("序号") == IT1.label.Text
                            select el;
            foreach (XElement el in address)
            {
                foreach (XElement al in el.Elements())
                {
                    string 记录1= (string)al.Attribute("序号");
                    string 记录2= (string)al.Attribute("功能描述");
                    string[] row = { 记录1, 记录2 };
                    ListViewItem item = new ListViewItem(row);
                    this.listView1.Items.Add(item);

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
            for (int i= 0; i < this.listView1.Items.Count; i++)
            {
                XElement ChildOnekeyStart = new XElement("OnekeyStart"+ listView1.Items[i].SubItems[0].Text);
                ChildOnekeyStart.Add(new XAttribute("序号", listView1.Items[i].SubItems[0].Text));
                ChildOnekeyStart.Add(new XAttribute("功能描述", listView1.Items[i].SubItems[1].Text));
                OnekeyStart.Add(ChildOnekeyStart);
            }

            ////ChildOnekeyStart.Add(new XAttribute("序号", 1));
            ////ChildOnekeyStart.Add(new XAttribute("功能描述", "当"
            ////    + this.userControl32.comboBox1.Text
            ////    + this.userControl32.comboBox2.Text + "时，"
            ////    + this.userControl32.comboBox3.Text
            ////    + this.userControl32.textBox1.Text + "，"
            ////    + "触发动作："
            ////    + this.userControl32.comboBox4.Text
            ////    + this.userControl32.comboBox5.Text));
            ////OnekeyStart.Add(ChildOnekeyStart);


            ////XElement OnekeyStart = new XElement("OnekeyStart");
            //foreach (Control IT in this.groupBox3.Controls)
            //{
            //    if (IT is UserControl2.Ai控件)
            //    {
            //        UserControl2.Ai控件 IT1 = (UserControl2.Ai控件)IT;
            //        //DInputInfo itemInfoS = new DInputInfo();
            //        //itemInfoS.ID = IT1.label.Text;
            //        //itemInfoS.Name = IT1.textBox.Text;
            //        //itemInfoS.ActiveState = IT1.checkBox.Checked;
            //        //XElement ChildDI = new XElement(IT1.label.Text, new XAttribute("A","1"));
            //        XElement ChildOnekeyStart = new XElement(IT1.label1.Text);
            //        //XElement ChildAI = new XElement("我靠4");
            //        ChildOnekeyStart.Add(new XAttribute("序号", IT1.label1.Text));
            //        ChildOnekeyStart.Add(new XAttribute("端口名称", IT1.textBox1.Text));
            //        ChildOnekeyStart.Add(new XAttribute("最小电压", IT1.textBox2.Text));
            //        ChildOnekeyStart.Add(new XAttribute("最大电压", IT1.textBox3.Text));
            //        ChildOnekeyStart.Add(new XAttribute("最小数值", IT1.textBox4.Text));
            //        ChildOnekeyStart.Add(new XAttribute("最大数值", IT1.textBox5.Text));
            //        ChildOnekeyStart.Add(new XAttribute("有效性", IT1.checkBox1.Checked));
            //        //ChildOnekeyStart.Add(new XElement("子元素1","1"));
            //        OnekeyStart.Add(ChildOnekeyStart);
            //    }
            //}
        }

        private int ListViewId = 0;//正确的来讲，用于记录行数
        //（4）一键启动配置：添加——在最后一行添加记录
        private void button2_Click(object sender, EventArgs e)//（4）一键启动配置：添加
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
        private void updatebutton_Click(object sender, EventArgs e)//（4）一键启动配置：修改
        {
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                listView1.SelectedItems[0].SubItems[1].Text =  "当"
                  + this.userControl32.comboBox1.Text
                  + this.userControl32.comboBox2.Text + "时，"
                  + this.userControl32.comboBox3.Text
                  + this.userControl32.textBox1.Text + "，"
                  + "触发动作："
                  + this.userControl32.comboBox4.Text
                  + this.userControl32.comboBox5.Text;
            }
        }       
        
        //（4）一键启动配置：插入，在指定行下一行新建记录
        private void insertbutton3_Click(object sender, EventArgs e)
        {
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                int number = listView1.SelectedItems[0].Index + 1;     //用于记录选中行号，加一是因为本来是从0开始计数的。
                                                                       //string Row_Number = (number + 1).ToString();     //然后将选中的行数加一

                //ListViewItem item = new ListViewItem();       //   创建一个listview行的对象  
                //item.SubItems.Add("");             //  给新增的行第2列添加数据   插入空数据，因为没有数据的话，修改该行会报错！
                //item.SubItems.Add("当"
                //  + this.userControl32.comboBox1.Text
                //  + this.userControl32.comboBox2.Text + "时，"
                //  + this.userControl32.comboBox3.Text
                //  + this.userControl32.textBox1.Text + "，"
                //  + "触发动作："
                //  + this.userControl32.comboBox4.Text
                //  + this.userControl32.comboBox5.Text);//  给新增的行第3列添加数据
                string 记录 = "当"
                    + this.userControl32.comboBox1.Text
                    + this.userControl32.comboBox2.Text + "时，"
                    + this.userControl32.comboBox3.Text
                    + this.userControl32.textBox1.Text + "，"
                    + "触发动作："
                    + this.userControl32.comboBox4.Text
                    + this.userControl32.comboBox5.Text;
                //string[] row = { ListViewId.ToString(), 记录 };
                string[] row = {"", 记录 };
                ListViewItem item = new ListViewItem(row);

                //item.SubItems.Add("");              //  给新增的行第4列添加数据
                this.listView1.Items.Insert(number, item);     //     将新增的对象item插入到指定行

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
        private void deletebutton_Click(object sender, EventArgs e)//（4）一键启动配置：删除
        {
            //此处存在问题。如果双击直接操作，就奔溃了。是因为，此时没有选中正确的index。
            if (this.listView1.SelectedItems.Count != 0)    //如果选中的行等于0,就不执行。默认设置了不能多选
            {
                int number = listView1.SelectedItems[0].Index;     //用于记录选中行号，加一是因为本来是从0开始计数的。
                if (MessageBox.Show("确定要删除本条记录：“序号："+ this.listView1.Items[number].SubItems[0].Text + "功能描述："
                    +this.listView1.Items[number].SubItems[1].Text 
                    + "”?", "DELETE", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {                   
                    listView1.Items.RemoveAt(number);
                    //listView1.Items.RemoveAt(listView1.SelectedIndices[0]);

                    ListViewId--;//记录总行数
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

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void userControl11_Load(object sender, EventArgs e)
        {

        }

        private void userControl117_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)//完成xml配置文件的输出
        {

        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void comboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

    }

    ////<读写XML文件类库>

    ///// <summary>
    ///// 书籍集合信息
    ///// </summary>
    //public class DInputInfo
    //{
    //    //书籍信息
    //    private string _Id;
    //    public string ID
    //    {
    //        get
    //        {return _Id;}
    //        set
    //        {_Id = value;}
    //    }
    //    private string _Name;
    //    public string Name
    //    {
    //        get
    //        {return _Name;}
    //        set
    //        {_Name = value;}
    //    }
    //    private bool _ActiveState = false;
    //    public bool ActiveState
    //    {
    //        get
    //        {return _ActiveState;}
    //        set
    //        { _ActiveState = value;}
    //    }
    //}

    ///// <summary>
    ///// 保存数据
    ///// </summary>
    //public class ReadingSave
    //{
    //    //存储地址
    //    public static readonly string ArchiveDir = Application.UserAppDataPath + "/ReadingData";//修改

    //    /// <summary>
    //    /// 加载数据
    //    /// </summary>
    //    /// <returns></returns>
    //    public DInputInfo PlayLoad()
    //    {
    //        if (File.Exists(ArchiveDir + ".xml"))
    //        {

    //            //创建xml
    //            XmlDocument xml = new XmlDocument();

    //            //加载
    //            xml.Load(ArchiveDir + ".xml");

    //            DInputInfo info = new DInputInfo();
    //            try
    //            {
    //                XmlNodeList nodeList = xml.GetElementsByTagName("ReadingData");
    //                nodeList = xml.GetElementsByTagName("BookItem");
    //                if (nodeList.Count != 0)
    //                    for (int i = 0; i < nodeList.Count; i++)
    //                    {
    //                        int k = 0;
    //                        DInputInfo item = new DInputInfo();
    //                        //item.Id = int.Parse(nodeList[i].ChildNodes[k++].InnerText);
    //                        //item.Schedule = int.Parse(nodeList[i].ChildNodes[k++].InnerText);
    //                        //item.ActiveState = int.Parse(nodeList[i].ChildNodes[k++].InnerText);
    //                        //info.DIInfoList.Add(item.Id, item);
    //                    }
    //            }
    //            catch (Exception e)
    //            {
    //                //Debug.Log(e.Message);
    //            }
    //            return info;
    //        }
    //        else
    //        {
    //            return null;
    //        }
    //    }
    //}
    ////</读写XML文件类库>
}
