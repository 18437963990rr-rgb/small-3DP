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
    public partial class 登录确认 : Form
    {
        public 登录确认()
        {

            InitializeComponent();
            m_AccountPassward = new AccountPassward();

        }
        //20200224新建：textbox数据绑定
        public AccountPassward m_AccountPassward;
        public void InitLoginFormWithDefaultAccount()
        {
            // 账号和密码：
            AccountBox.DataBindings.Add("Text", m_AccountPassward, "Account", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);
            PasswordBox.DataBindings.Add("Text", m_AccountPassward, "Passward", true/*false*/, DataSourceUpdateMode.OnPropertyChanged);
        }

        private void 登录确认_Load(object sender, EventArgs e)
        {
            InitLoginFormWithDefaultAccount();
            if (m_AccountPassward.ShowReminderFlag==true)
            {
                this.ReminderLabel.Visible = true;
                this.ReminderLabel.Refresh();
            }
        }
    }

    public class AccountPassward : INotifyPropertyChanged, ICloneable//C#中，通知类的属性值已经更改，可以避免大量的通用事件的使用；其中关键是属性的理解及和lambda表达式的使用方法
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
        /// 账号和密码：
        /// </summary>
        public string m_sAccount = "";//高速闪喷频率
        public string m_sPassward ="";//高速闪喷时间

        public bool ShowReminderFlag = false;//是否显示密码提示框

        public event PropertyChangedEventHandler PropertyChanged;//20200224新增：必须定义事件；是接口INotifyPropertyChanged的必须的事件       
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")//20200225：【CallerMemberName】特性精华：每次调用 TraceMessage 方法时，调用方信息将替换为可选参数的参数。
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));//20200225：propertyName：是1个附带参数
        }

        /// 喷头保护设置参数：(清洗和闪喷两种作用)
        public string Account//待机闪喷频率
        {
            get { return this.m_sAccount; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_sAccount) { this.m_sAccount = value; NotifyPropertyChanged(); } }
        }
        public string Passward//高速闪喷频率
        {
            get { return this.m_sPassward; }/*//20200225：value 关键字用于定义由 set 取值函数分配的值。*/
            set { if (value != this.m_sPassward) { this.m_sPassward = value; NotifyPropertyChanged(); } }
        }
    }
}
