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
    public partial class 平移操作 : Form
    {
        public 平移操作()
        {
            InitializeComponent();
        }

        private void 平移操作_Load(object sender, EventArgs e)
        {
            InitKidFormWithTranslateParam();
        }

        //20200224新建：textbox数据绑定
        public VirtualTranslateParam k_VirtualArrayParam = new VirtualTranslateParam();
        public void InitKidFormWithTranslateParam()
        {
            //阵列参数初始化
            xTranslateTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "XTranslate", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            yTranslateTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "YTranslate", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            zTranslateTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "ZTranslate", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
            xDeltaTranslateTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "XDeltaTranslate", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            yDeltaTranslateTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "YDeltaTranslate", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            zDeltaTranslateTextBox.DataBindings.Add("Text", k_VirtualArrayParam, "ZDeltaTranslate", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);

            if (k_VirtualArrayParam.m_bTranslateMode == false)
            {
                SwitchTranslateModeBtn.Tag = "0";
                SwitchTranslateModeBtn.Text = "切到绝对移动";

                xTranslateTextBox.Enabled = false;
                yTranslateTextBox.Enabled = false;
                zTranslateTextBox.Enabled = false;
                xDeltaTranslateTextBox.Enabled = true;
                yDeltaTranslateTextBox.Enabled = true;
                zDeltaTranslateTextBox.Enabled = false;
            }
            else
            {
                SwitchTranslateModeBtn.Tag = "1";
                SwitchTranslateModeBtn.Text = "切到相对移动";

                xTranslateTextBox.Enabled = true;
                yTranslateTextBox.Enabled = true;
                zTranslateTextBox.Enabled = false;
                xDeltaTranslateTextBox.Enabled = false;
                yDeltaTranslateTextBox.Enabled = false;
                zDeltaTranslateTextBox.Enabled = false;
            }

        }

        private void SwitchTranslateModeBtn_Click(object sender, EventArgs e)
        {
            if ((string)(sender as Button).Tag == "1")
            {
                k_VirtualArrayParam.m_bTranslateMode = false;//处于相对移动模式
                (sender as Button).Tag = "0";
                (sender as Button).Text = "切到绝对移动";

                xTranslateTextBox.Enabled = false;
                yTranslateTextBox.Enabled = false;
                zTranslateTextBox.Enabled = false;
                xDeltaTranslateTextBox.Enabled = true;
                yDeltaTranslateTextBox.Enabled = true;
                zDeltaTranslateTextBox.Enabled = true;
            }
            else if((string)(sender as Button).Tag == "0")
            {
                k_VirtualArrayParam.m_bTranslateMode = true;//处于绝对移动模式
                (sender as Button).Tag = "1";
                (sender as Button).Text = "切到相对移动";

                xTranslateTextBox.Enabled = true;
                yTranslateTextBox.Enabled = true;
                zTranslateTextBox.Enabled = true;
                xDeltaTranslateTextBox.Enabled = false;
                yDeltaTranslateTextBox.Enabled = false;
                zDeltaTranslateTextBox.Enabled = false;
            }
            else { }
        }
    }

    public class VirtualTranslateParam : INotifyPropertyChanged, ICloneable//20201113新增：阵列参数传递数据
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

        //平移参数
        public bool m_bTranslateMode = false;//false为相对移动量，true为绝对移动量
        public double m_dXTranslate = 0;
        public double m_dYTranslate = 0;
        public double m_dZTranslate = 0;
        public double m_dXDeltaTranslate = 10;
        public double m_dYDeltaTranslate = 0;
        public double m_dZDeltaTranslate = 0;

        //public bool TranslateMode//打印区长度：默认420mm：20200326新增
        //{
        //    get { return this.m_bTranslateMode; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
        //    set { if (value != this.m_bTranslateMode) { this.m_bTranslateMode = value; NotifyPropertyChanged(); } }
        //}
        public double XTranslate//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dXTranslate; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dXTranslate) { this.m_dXTranslate = value; NotifyPropertyChanged(); } }
        }
        public double YTranslate//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dYTranslate; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dYTranslate) { this.m_dYTranslate = value; NotifyPropertyChanged(); } }
        }
        public double ZTranslate//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dZTranslate; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dZTranslate) { this.m_dZTranslate = value; NotifyPropertyChanged(); } }
        }
        public double XDeltaTranslate//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dXDeltaTranslate; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dXDeltaTranslate) { this.m_dXDeltaTranslate = value; NotifyPropertyChanged(); } }
        }
        public double YDeltaTranslate//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dYDeltaTranslate; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dYDeltaTranslate) { this.m_dYDeltaTranslate = value; NotifyPropertyChanged(); } }
        }
        public double ZDeltaTranslate//打印区长度：默认420mm：20200326新增
        {
            get { return this.m_dZDeltaTranslate; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_dZDeltaTranslate) { this.m_dZDeltaTranslate = value; NotifyPropertyChanged(); } }
        }
    }
}
