using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BinderJetting
{
    public partial class 阵列拷贝 : Form
    {
        public 阵列拷贝()
        {
            InitializeComponent();
        }

        private void 阵列拷贝_Load(object sender, EventArgs e)
        {
            InitKidFormWithVitualArrayParam();
        }

        //20200224新建：textbox数据绑定
        public VirtualArrayParam k_VirtualArrayParam = new VirtualArrayParam();
        public void InitKidFormWithVitualArrayParam()
        {
            //阵列参数初始化
            xnumTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "Xnum", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            ynumTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "Ynum", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            xSpaceTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "XSpace", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            ySpaceTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "YSpace", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            CountNumLabel.DataBindings.Add("Text", k_VirtualArrayParam, "CountNum", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
        }
    }

    public class VirtualArrayParam : INotifyPropertyChanged, ICloneable//20201113新增：阵列参数传递数据
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
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")//20200225：【CallerMemberName】特性精华：每次调用 TraceMessage 方法时，调用方信息将替换为可选参数的参数。
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));//20200225：propertyName：是1个附带参数
        }
        /// <summary>
        /// 阵列参数初始化
        /// </summary>
        public double m_dXSpace = 5;//X向间距，单位MM
        public double m_dYSpace = 5;//Y向间距，单位MM
        public int m_nXnum = 4;//X向阵列数
        public int m_nYnum = 1;//X向阵列数
        public int m_nCountNum = 4;//总阵列数

        public double XSpace//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dXSpace; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dXSpace) { this.m_dXSpace = value; NotifyPropertyChanged(); } }
        }
        public double YSpace//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dYSpace; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dYSpace) { this.m_dYSpace = value; NotifyPropertyChanged(); } }
        }
        public int Xnum//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_nXnum; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nXnum) { this.m_nXnum = value; NotifyPropertyChanged(); } }
        }
        public int Ynum//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_nYnum; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nYnum) { this.m_nYnum = value; NotifyPropertyChanged(); } }
        }
        public int CountNum//打印区长度：默认420mm：20200326新增
        {
            get { this.m_nCountNum= this.m_nXnum * this.m_nYnum; return this.m_nCountNum; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_nXnum * this.m_nYnum/*this.m_nCountNum*/) { this.m_nCountNum = value; NotifyPropertyChanged(); } }
        }
    }
}
