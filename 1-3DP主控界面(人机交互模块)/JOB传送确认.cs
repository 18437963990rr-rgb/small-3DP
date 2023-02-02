using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BinderJetting;
using System.Threading;
//using BinderJetting;//人机交互组件包

namespace ComposationConfirm 
{
    public partial class JOB传送确认 : Form
    {
        public JOB传送确认(bool ComposeFlag)
        {
            InitializeComponent();
            if (ComposeFlag==false)
            {
                label1.Text = "排版前，请输入CLI文件。";
                label1.BackColor = Color.Transparent;
            }
            else if(ComposeFlag == true)
            {
                label1.Text = "排版完成，确认输出JOBS。";
                label1.BackColor = Color.Transparent/*GreenYellow*/;
                //_FinalJobs.CreateFinalJOB(主界面.);
                //输出到指定的文件夹——D盘
                //开启1个新的线程——存在D盘
                //START THE FinalJOB线程
                ThreadStart FinalJOBThreadEntry = new ThreadStart(RunFinalJOBThread);//线程入口方法
                FinalJOBThread = new Thread(FinalJOBThreadEntry) { IsBackground = true };
                FinalJOBThread.Start();//开启CreateFinalJOB线程的线程
            }
        }
        FinalJOBS _FinalJobs = new FinalJOBS();
        private Thread FinalJOBThread;//（1）导入CLI线程
        private void RunFinalJOBThread(/*ref _3DP_GUI组件 _3DP_GUI*/)//载入数据线程内容
        {
            //_FinalJobs.CreateFinalJOB(/*ref _3DP_GUI*/);//在指定的文件夹生成大TIFF
        }
    }
}
