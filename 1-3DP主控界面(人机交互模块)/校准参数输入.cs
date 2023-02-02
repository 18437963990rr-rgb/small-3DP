using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BinderJetting
{
    public partial class 校准参数输入 : Form
    {
        public 校准参数输入()
        {
            InitializeComponent();

            ////计算窗体显示的坐标值，可以根据需要微调几个像素
            //int x = this.ClientRectangle.Location.X+this.panel3.Location.X;/*ScreenWidth - this.Width - 5;*/
            //int y = this.ClientRectangle.Location.Y + this.panel3.Location.Y;/*ScreenHeight - this.Height - 5;*/
            //this.Location = new Point(x, y);
        }
        private void 校准参数输入_Load(object sender, EventArgs e)
        {
            //InitKidFormWithFeedbackInCorrection();

            string JsonPath = "";
            try//20210322新增：读取JSON配置文件
            {
                //(4)20200807批注：加载打印策略参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\FeedbackParamInCorrection.json";//json配置文件：启动目录
                k_RYSYSParamFeedbackInCorrection = ObjectCopier.LoadJson<FeedbackInCorrection>(JsonPath);

                InitKidFormWithFeedbackInCorrection();//20201020新增：开启ParamInText自动打印参数的绑定
            }
            catch (Exception)
            {
                if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
                {
                    File.Create(JsonPath);//不存在则创建文
                    MessageBox.Show("校准反馈文件不存在，已创建");
                }
                else
                {
                    MessageBox.Show("校准反馈文件加载异常");
                }
                k_RYSYSParamFeedbackInCorrection = new FeedbackInCorrection();
                InitKidFormWithFeedbackInCorrection();//20201020新增：开启ParamInText自动打印参数的绑定
            }
        }

        public void SaveJsonFile()
        {
            string JsonPath = System.Windows.Forms.Application.StartupPath + @"\FeedbackParamInCorrection.json";//json配置文件：启动目录
            if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
            {
                File.Create(JsonPath);//不存在则创建文件
            }
            string json = JsonConvert.SerializeObject(k_RYSYSParamFeedbackInCorrection, Formatting.Indented);
            try
            {
                File.WriteAllText(JsonPath, json);
            }
            catch (Exception e)
            {
                MessageBox.Show("校准反馈文件加载异常");
            }

        }

        //20201020新建：textbox数据绑定
        public FeedbackInCorrection k_RYSYSParamFeedbackInCorrection/*=new AutoPrintParamInTest()*/;//20210304修改：
        public void InitKidFormWithFeedbackInCorrection()//20201020新建：数据绑定自动固化清洗-自动进给铺粉-自动铺粉续打
        {
            //X向套色偏差:20210124新增
            //1号喷头的，正向偏差及逆向偏差
            comboBox1.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback00", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox14.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback01", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox21.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback02", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox28.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback03", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox35.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback04", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox42.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback05", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox49.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback06", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox56.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback07", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //2号喷头的，正向偏差及逆向偏差 
            comboBox2.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback10", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox13.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback11", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox20.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback12", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox27.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback13", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox34.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback14", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox41.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback15", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox48.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback16", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox55.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback17", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //3号喷头的，正向偏差及逆向偏差 
            comboBox3.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback20", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox12.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback21", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox19.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback22", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox26.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback23", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox33.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback24", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox40.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback25", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox47.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback26", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox54.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback27", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //4号喷头的，正向偏差及逆向偏差 
            comboBox4.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback30", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox11.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback31", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox18.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback32", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox25.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback33", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox32.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback34", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox39.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback35", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox46.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback36", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox53.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback37", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //5号喷头的，正向偏差及逆向偏差 
            comboBox5.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback40", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox10.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback41", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox17.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback42", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox24.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback43", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox31.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback44", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox38.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback45", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox45.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback46", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox52.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback47", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //6号喷头的，正向偏差及逆向偏差 
            comboBox6.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback50", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox9.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback51", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox16.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback52", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox23.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback53", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox30.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback54", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox37.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback55", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox44.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback56", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox51.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback57", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //7号喷头的，正向偏差及逆向偏差 
            comboBox7.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback60", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox8.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback61", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox15.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback62", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox22.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback63", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox29.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback64", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox36.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback65", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox43.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback66", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox50.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XNestFeedback67", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增

            //X向套色偏差校准反馈值-正向：2*6=12//20210322新增：依次为1-2-正向-喷头偏移距离、1-2-逆向-喷头偏移距离   
            comboBox65.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead11", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox66.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead12", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox67.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead13", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox68.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead14", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox69.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead15", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox70.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead16", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            //X向套色偏差校准反馈值-逆向：2*6=12//20210322新增：依次为1-2-正向-喷头偏移距离、1-2-逆向-喷头偏移距离   
            comboBox76.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead21", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox75.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead22", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox74.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead23", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox73.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead24", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox72.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead25", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox71.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBetweenHead26", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增

            //Y向套色偏差校准反馈值：7*1=7//20210322新增：
            comboBox63.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "YNestFeedback0", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox62.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "YNestFeedback1", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox61.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "YNestFeedback2", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox60.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "YNestFeedback3", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox59.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "YNestFeedback4", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox58.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "YNestFeedback5", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增
            comboBox57.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "YNestFeedback6", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增

            //X向往返差偏差反馈值：1//20210322新增：：7*1=7//20210322新增：
            comboBox64.DataBindings.Add("SelectedIndex", k_RYSYSParamFeedbackInCorrection, "XBackForthFeedBack", true, DataSourceUpdateMode.OnPropertyChanged);//车头运动速度：20200326新增

        }

        private void button17_Click(object sender, EventArgs e)
        {
            SaveJsonFile();//20210322新增：
        }
    }

    public class FeedbackInCorrection : INotifyPropertyChanged, ICloneable//C#中，通知类的属性值已经更改，可以避免大量的通用事件的使用；其中关键是属性的理解及和lambda表达式的使用方法
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
        /// X向套色偏差校准反馈值：7*4*2=56
        /// </summary>
        /// 
        public int[] m_nXNestFeedback = new int[28 * 2] {
            2,2,2,2,2,2,2,2,
            2,2,2,2,2,2,2,2,
            2,2,2,2,2,2,2,2,
            2,2,2,2,2,2,2,2,
            2,2,2,2,2,2,2,2,
            2,2,2,2,2,2,2,2,
            2,2,2,2,2,2,2,2};//X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        public int[] m_nXBetweenHead = new int[2 * 6] {
            5,5,5,5,5,5,
            5,5,5,5,5,5};//X向套色偏差校准反馈值：2*6=12//20210322新增：依次为1-2-正向-喷头偏移距离、1-2-逆向-喷头偏移距离
        public int[] m_nYNestFeedback = new int[7] {
            2,2,2,2,2,2,2 };//Y向套色偏差校准反馈值：7*1=7//20210322新增：
        public int m_nXBackForthFeedBack = 6;//X向往返差偏差反馈值：1//20210322新增：

        //public int m_nXNestFeedback0, m_nXNestFeedback1, m_nXNestFeedback2, m_nXNestFeedback3, m_nXNestFeedback4, m_nXNestFeedback5, m_nXNestFeedback6, m_nXNestFeedback7,
        //    m_nXNestFeedback9, m_nXNestFeedback9, m_nXNestFeedback10, m_nXNestFeedback11, m_nXNestFeedback12, m_nXNestFeedback13, m_nXNestFeedback14, m_nXNestFeedback15,
        //    m_nXNestFeedback16, m_nXNestFeedback17, m_nXNestFeedback18, m_nXNestFeedback19, m_nXNestFeedback20, m_nXNestFeedback21, m_nXNestFeedback22, m_nXNestFeedback23,
        //    m_nXNestFeedback24, m_nXNestFeedback25, m_nXNestFeedback26, m_nXNestFeedback27, m_nXNestFeedback28, m_nXNestFeedback29, m_nXNestFeedback30, m_nXNestFeedback31,
        //    m_nXNestFeedback32, m_nXNestFeedback33, m_nXNestFeedback34, m_nXNestFeedback35, m_nXNestFeedback36, m_nXNestFeedback37, m_nXNestFeedback38, m_nXNestFeedback39,
        //    m_nXNestFeedback40, m_nXNestFeedback41, m_nXNestFeedback42, m_nXNestFeedback3, m_nXNestFeedback4, m_nXNestFeedback5, m_nXNestFeedback6, m_nXNestFeedback7,
        //    m_nXNestFeedback0, m_nXNestFeedback1, m_nXNestFeedback2, m_nXNestFeedback3, m_nXNestFeedback4, m_nXNestFeedback5, m_nXNestFeedback6, m_nXNestFeedback7;

        ////X向往返差偏差反馈值：1//20210322新增：：7*1=7//20210322新增：
        public int XBackForthFeedBack
        {
            get { return this.m_nXBackForthFeedBack; }
            set { if (value != this.m_nXBackForthFeedBack) { this.m_nXBackForthFeedBack = value; NotifyPropertyChanged(); } }
        }

        //Y向套色偏差校准反馈值：7*1=7//20210322新增：
        public int YNestFeedback0
        {
            get { return this.m_nYNestFeedback[0]; }
            set { if (value != this.m_nYNestFeedback[0]) { this.m_nYNestFeedback[0] = value; NotifyPropertyChanged(); } }
        }
        public int YNestFeedback1
        {
            get { return this.m_nYNestFeedback[1]; }
            set { if (value != this.m_nYNestFeedback[1]) { this.m_nYNestFeedback[1] = value; NotifyPropertyChanged(); } }
        }
        public int YNestFeedback2
        {
            get { return this.m_nYNestFeedback[2]; }
            set { if (value != this.m_nYNestFeedback[2]) { this.m_nYNestFeedback[2] = value; NotifyPropertyChanged(); } }
        }
        public int YNestFeedback3
        {
            get { return this.m_nYNestFeedback[3]; }
            set { if (value != this.m_nYNestFeedback[3]) { this.m_nYNestFeedback[3] = value; NotifyPropertyChanged(); } }
        }
        public int YNestFeedback4
        {
            get { return this.m_nYNestFeedback[4]; }
            set { if (value != this.m_nYNestFeedback[4]) { this.m_nYNestFeedback[4] = value; NotifyPropertyChanged(); } }
        }
        public int YNestFeedback5
        {
            get { return this.m_nYNestFeedback[5]; }
            set { if (value != this.m_nYNestFeedback[5]) { this.m_nYNestFeedback[5] = value; NotifyPropertyChanged(); } }
        }
        public int YNestFeedback6
        {
            get { return this.m_nYNestFeedback[6]; }
            set { if (value != this.m_nYNestFeedback[6]) { this.m_nYNestFeedback[6] = value; NotifyPropertyChanged(); } }
        }



        //X向套色偏差校准反馈值：2*6=12//20210322新增：依次为1-2-正向-喷头偏移距离、1-2-逆向-喷头偏移距离   
        public int XBetweenHead11
        {
            get { return this.m_nXBetweenHead[0]; }
            set { if (value != this.m_nXBetweenHead[0]) { this.m_nXBetweenHead[0] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead12
        {
            get { return this.m_nXBetweenHead[1]; }
            set { if (value != this.m_nXBetweenHead[1]) { this.m_nXBetweenHead[1] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead13
        {
            get { return this.m_nXBetweenHead[2]; }
            set { if (value != this.m_nXBetweenHead[2]) { this.m_nXBetweenHead[2] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead14
        {
            get { return this.m_nXBetweenHead[3]; }
            set { if (value != this.m_nXBetweenHead[3]) { this.m_nXBetweenHead[3] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead15
        {
            get { return this.m_nXBetweenHead[4]; }
            set { if (value != this.m_nXBetweenHead[4]) { this.m_nXBetweenHead[4] = value; NotifyPropertyChanged(); } }
        }

        public int XBetweenHead16
        {
            get { return this.m_nXBetweenHead[5]; }
            set { if (value != this.m_nXBetweenHead[5]) { this.m_nXBetweenHead[5] = value; NotifyPropertyChanged(); } }
        }

        public int XBetweenHead21
        {
            get { return this.m_nXBetweenHead[6]; }
            set { if (value != this.m_nXBetweenHead[6]) { this.m_nXBetweenHead[6] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead22
        {
            get { return this.m_nXBetweenHead[7]; }
            set { if (value != this.m_nXBetweenHead[7]) { this.m_nXBetweenHead[7] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead23
        {
            get { return this.m_nXBetweenHead[8]; }
            set { if (value != this.m_nXBetweenHead[8]) { this.m_nXBetweenHead[8] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead24
        {
            get { return this.m_nXBetweenHead[9]; }
            set { if (value != this.m_nXBetweenHead[9]) { this.m_nXBetweenHead[9] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead25
        {
            get { return this.m_nXBetweenHead[10]; }
            set { if (value != this.m_nXBetweenHead[10]) { this.m_nXBetweenHead[10] = value; NotifyPropertyChanged(); } }
        }
        public int XBetweenHead26
        {
            get { return this.m_nXBetweenHead[11]; }
            set { if (value != this.m_nXBetweenHead[11]) { this.m_nXBetweenHead[11] = value; NotifyPropertyChanged(); } }
        }


        //X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        //1号喷头的，正向偏差及逆向偏差
        public int XNestFeedback00//
        {
            get { return this.m_nXNestFeedback[0]; }
            set { if (value != this.m_nXNestFeedback[0]) { this.m_nXNestFeedback[0] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback01//
        {
            get { return this.m_nXNestFeedback[1]; }
            set { if (value != this.m_nXNestFeedback[1]) { this.m_nXNestFeedback[1] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback02//
        {
            get { return this.m_nXNestFeedback[2]; }
            set { if (value != this.m_nXNestFeedback[2]) { this.m_nXNestFeedback[2] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback03//
        {
            get { return this.m_nXNestFeedback[3]; }
            set { if (value != this.m_nXNestFeedback[3]) { this.m_nXNestFeedback[3] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback04//
        {
            get { return this.m_nXNestFeedback[4]; }
            set { if (value != this.m_nXNestFeedback[4]) { this.m_nXNestFeedback[4] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback05//
        {
            get { return this.m_nXNestFeedback[5]; }
            set { if (value != this.m_nXNestFeedback[5]) { this.m_nXNestFeedback[5] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback06//
        {
            get { return this.m_nXNestFeedback[6]; }
            set { if (value != this.m_nXNestFeedback[6]) { this.m_nXNestFeedback[6] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback07//
        {
            get { return this.m_nXNestFeedback[7]; }
            set { if (value != this.m_nXNestFeedback[7]) { this.m_nXNestFeedback[7] = value; NotifyPropertyChanged(); } }
        }


        //X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        //2号喷头的，正向偏差及逆向偏差
        public int XNestFeedback10//
        {
            get { return this.m_nXNestFeedback[0+8]; }
            set { if (value != this.m_nXNestFeedback[0 + 8]) { this.m_nXNestFeedback[0 + 8] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback11//
        {
            get { return this.m_nXNestFeedback[1 + 8]; }
            set { if (value != this.m_nXNestFeedback[1 + 8]) { this.m_nXNestFeedback[1 + 8] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback12//
        {
            get { return this.m_nXNestFeedback[2 + 8]; }
            set { if (value != this.m_nXNestFeedback[2 + 8]) { this.m_nXNestFeedback[2 + 8] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback13//
        {
            get { return this.m_nXNestFeedback[3 + 8]; }
            set { if (value != this.m_nXNestFeedback[3 + 8]) { this.m_nXNestFeedback[3 + 8] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback14//
        {
            get { return this.m_nXNestFeedback[4 + 8]; }
            set { if (value != this.m_nXNestFeedback[4 + 8]) { this.m_nXNestFeedback[4 + 8] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback15//
        {
            get { return this.m_nXNestFeedback[5 + 8]; }
            set { if (value != this.m_nXNestFeedback[5 + 8]) { this.m_nXNestFeedback[5 + 8] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback16//
        {
            get { return this.m_nXNestFeedback[6 + 8]; }
            set { if (value != this.m_nXNestFeedback[6 + 8]) { this.m_nXNestFeedback[6 + 8] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback17//
        {
            get { return this.m_nXNestFeedback[7 + 8]; }
            set { if (value != this.m_nXNestFeedback[7 + 8]) { this.m_nXNestFeedback[7 + 8] = value; NotifyPropertyChanged(); } }
        }

        //X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        //3号喷头的，正向偏差及逆向偏差
        public int XNestFeedback20//
        {
            get { return this.m_nXNestFeedback[0 + 16]; }
            set { if (value != this.m_nXNestFeedback[0 + 16]) { this.m_nXNestFeedback[0 + 16] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback21//
        {
            get { return this.m_nXNestFeedback[1 + 16]; }
            set { if (value != this.m_nXNestFeedback[1 + 16]) { this.m_nXNestFeedback[1 + 16] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback22//
        {
            get { return this.m_nXNestFeedback[2 + 16]; }
            set { if (value != this.m_nXNestFeedback[2 + 16]) { this.m_nXNestFeedback[2 + 16] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback23//
        {
            get { return this.m_nXNestFeedback[3 + 16]; }
            set { if (value != this.m_nXNestFeedback[3 + 16]) { this.m_nXNestFeedback[3 + 16] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback24//
        {
            get { return this.m_nXNestFeedback[4 + 16]; }
            set { if (value != this.m_nXNestFeedback[4 + 16]) { this.m_nXNestFeedback[4 + 16] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback25//
        {
            get { return this.m_nXNestFeedback[5 + 16]; }
            set { if (value != this.m_nXNestFeedback[5 + 16]) { this.m_nXNestFeedback[5 + 16] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback26//
        {
            get { return this.m_nXNestFeedback[6 + 16]; }
            set { if (value != this.m_nXNestFeedback[6 + 16]) { this.m_nXNestFeedback[6 + 16] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback27//
        {
            get { return this.m_nXNestFeedback[7 + 16]; }
            set { if (value != this.m_nXNestFeedback[7 + 16]) { this.m_nXNestFeedback[7 + 16] = value; NotifyPropertyChanged(); } }
        }

        //X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        //4号喷头的，正向偏差及逆向偏差
        public int XNestFeedback30//
        {
            get { return this.m_nXNestFeedback[0 + 24]; }
            set { if (value != this.m_nXNestFeedback[0 + 24]) { this.m_nXNestFeedback[0 + 24] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback31//
        {
            get { return this.m_nXNestFeedback[1 + 24]; }
            set { if (value != this.m_nXNestFeedback[1 + 24]) { this.m_nXNestFeedback[1 + 24] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback32//
        {
            get { return this.m_nXNestFeedback[2 + 24]; }
            set { if (value != this.m_nXNestFeedback[2 + 24]) { this.m_nXNestFeedback[2 + 24] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback33//
        {
            get { return this.m_nXNestFeedback[3 + 24]; }
            set { if (value != this.m_nXNestFeedback[3 + 24]) { this.m_nXNestFeedback[3 + 24] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback34//
        {
            get { return this.m_nXNestFeedback[4 + 24]; }
            set { if (value != this.m_nXNestFeedback[4 + 24]) { this.m_nXNestFeedback[4 + 24] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback35//
        {
            get { return this.m_nXNestFeedback[5 + 24]; }
            set { if (value != this.m_nXNestFeedback[5 + 24]) { this.m_nXNestFeedback[5 + 24] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback36//
        {
            get { return this.m_nXNestFeedback[6 + 24]; }
            set { if (value != this.m_nXNestFeedback[6 + 24]) { this.m_nXNestFeedback[6 + 24] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback37//
        {
            get { return this.m_nXNestFeedback[7 + 24]; }
            set { if (value != this.m_nXNestFeedback[7 + 24]) { this.m_nXNestFeedback[7 + 24] = value; NotifyPropertyChanged(); } }
        }

        //X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        //5号喷头的，正向偏差及逆向偏差
        public int XNestFeedback40//
        {
            get { return this.m_nXNestFeedback[0 + 32]; }
            set { if (value != this.m_nXNestFeedback[0 + 32]) { this.m_nXNestFeedback[0 + 32] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback41//
        {
            get { return this.m_nXNestFeedback[1 + 32]; }
            set { if (value != this.m_nXNestFeedback[1 + 32]) { this.m_nXNestFeedback[1 + 32] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback42//
        {
            get { return this.m_nXNestFeedback[2 + 32]; }
            set { if (value != this.m_nXNestFeedback[2 + 32]) { this.m_nXNestFeedback[2 + 32] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback43//
        {
            get { return this.m_nXNestFeedback[3 + 32]; }
            set { if (value != this.m_nXNestFeedback[3 + 32]) { this.m_nXNestFeedback[3 + 32] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback44//
        {
            get { return this.m_nXNestFeedback[4 + 32]; }
            set { if (value != this.m_nXNestFeedback[4 + 32]) { this.m_nXNestFeedback[4 + 32] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback45//
        {
            get { return this.m_nXNestFeedback[5 + 32]; }
            set { if (value != this.m_nXNestFeedback[5 + 32]) { this.m_nXNestFeedback[5 + 32] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback46//
        {
            get { return this.m_nXNestFeedback[6 + 32]; }
            set { if (value != this.m_nXNestFeedback[6 + 32]) { this.m_nXNestFeedback[6 + 32] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback47//
        {
            get { return this.m_nXNestFeedback[7 + 32]; }
            set { if (value != this.m_nXNestFeedback[7 + 32]) { this.m_nXNestFeedback[7 + 32] = value; NotifyPropertyChanged(); } }
        }

        //X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        //6号喷头的，正向偏差及逆向偏差
        public int XNestFeedback50//
        {
            get { return this.m_nXNestFeedback[0 + 40]; }
            set { if (value != this.m_nXNestFeedback[0 + 40]) { this.m_nXNestFeedback[0 + 40] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback51//
        {
            get { return this.m_nXNestFeedback[1 + 40]; }
            set { if (value != this.m_nXNestFeedback[1 + 40]) { this.m_nXNestFeedback[1 + 40] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback52//
        {
            get { return this.m_nXNestFeedback[2+ 40]; }
            set { if (value != this.m_nXNestFeedback[2+ 40]) { this.m_nXNestFeedback[2 + 40] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback53//
        {
            get { return this.m_nXNestFeedback[3 + 40]; }
            set { if (value != this.m_nXNestFeedback[3 + 40]) { this.m_nXNestFeedback[3 + 40] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback54//
        {
            get { return this.m_nXNestFeedback[4 + 40]; }
            set { if (value != this.m_nXNestFeedback[4+ 40]) { this.m_nXNestFeedback[4 + 40] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback55//
        {
            get { return this.m_nXNestFeedback[5 + 40]; }
            set { if (value != this.m_nXNestFeedback[5 + 40]) { this.m_nXNestFeedback[5 + 40] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback56//
        {
            get { return this.m_nXNestFeedback[6 + 40]; }
            set { if (value != this.m_nXNestFeedback[6 + 40]) { this.m_nXNestFeedback[6 + 40] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback57//
        {
            get { return this.m_nXNestFeedback[7 + 40]; }
            set { if (value != this.m_nXNestFeedback[7 + 40]) { this.m_nXNestFeedback[7 + 40] = value; NotifyPropertyChanged(); } }
        }

        //X向套色偏差校准反馈值：7*4*2=56//20210322新增：
        //7号喷头的，正向偏差及逆向偏差
        public int XNestFeedback60//
        {
            get { return this.m_nXNestFeedback[0 + 48]; }
            set { if (value != this.m_nXNestFeedback[0 + 48]) { this.m_nXNestFeedback[0 + 48] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback61//
        {
            get { return this.m_nXNestFeedback[1 + 48]; }
            set { if (value != this.m_nXNestFeedback[1 + 48]) { this.m_nXNestFeedback[1 + 48] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback62//
        {
            get { return this.m_nXNestFeedback[2 + 48]; }
            set { if (value != this.m_nXNestFeedback[2 + 48]) { this.m_nXNestFeedback[2 + 48] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback63//
        {
            get { return this.m_nXNestFeedback[3 + 48]; }
            set { if (value != this.m_nXNestFeedback[3 + 48]) { this.m_nXNestFeedback[3 + 48] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback64//
        {
            get { return this.m_nXNestFeedback[4 + 48]; }
            set { if (value != this.m_nXNestFeedback[4 + 48]) { this.m_nXNestFeedback[4 + 48] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback65//
        {
            get { return this.m_nXNestFeedback[5 + 48]; }
            set { if (value != this.m_nXNestFeedback[5 + 48]) { this.m_nXNestFeedback[5 + 48] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback66//
        {
            get { return this.m_nXNestFeedback[6 + 48]; }
            set { if (value != this.m_nXNestFeedback[6 + 48]) { this.m_nXNestFeedback[6 + 48] = value; NotifyPropertyChanged(); } }
        }
        public int XNestFeedback67//
        {
            get { return this.m_nXNestFeedback[7 + 48]; }
            set { if (value != this.m_nXNestFeedback[7 + 48]) { this.m_nXNestFeedback[7 + 48] = value; NotifyPropertyChanged(); } }
        }




    }
}
