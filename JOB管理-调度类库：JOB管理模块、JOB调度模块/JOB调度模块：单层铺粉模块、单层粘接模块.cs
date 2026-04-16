using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using _3DP_KeyLibrary;
using Motion;//导入GoogolMotionMap引用包
//using _3DP_
//using static System.Net.Mime.MediaTypeNames;
//using static System.Net.Mime.MediaTypeNames;//增添本应用，看是否解决问题
//using System.Windows.Forms;

namespace JOB管理_调度类库_JOB管理模块_JOB调度模块
{
    //1-实现Layer接口：（核心内容）
    //调度模块实现的功能是逐层Layer:按照层号，从JOB中读取各层的信息：按照循环的流程控制相应接口输出完毕。
    //(I)读取JOB的id层：根据JOB文件树,（a）读取单层的1副TIFF和对应的参数++++++++（b）读取成型层厚和成型速度等信息。
    //(II)输出流程：(1)PPoint成型缸下降PARAM1————》(2)PPoint送粉缸上升PARAM2————》(3)JOG铺粉车撞限位停止PARAM3————》
    //(III)输出流程：(1)Transfer一幅TIFF到喷头————》(2)Start控制喷墨控制卡{PARAM4/PARAM5/PARAM6......}，双伺服电机和7喷头协同动作。
    //(IV)层数：id++
    //2实现Pause接口：（核心内容）
    //3实现Stop接口：（核心内容）
    //4实现Status接口：（全部使用隐式接口的方式实现）
    //JOB调度模块，通过调用单层铺粉控制模块和单层喷墨成型模块实现管理调度的功能。
    class JOB调度模块 : IAuto
    {
        public static void Delay(int milliSecond)
        {
            int start = Environment.TickCount;
            while (Math.Abs(Environment.TickCount - start) < milliSecond)
            {
                ////使用application .doevents只适用于winform中，且有很多的问题（见网络）；
                ////使用多线程的方式更佳，在多线程中使用延时。
                ////Application.DoEvents();

                ////——————》》进入多线程实现。
                ////=>是lambda运算符，是委托的快速实现，可以实现函数式编程的快捷实现。
                ////=>是lambda运算符，是委托的符号化实现。
                //Random rd = new Random();
                //Thread thread = new Thread((t) =>
                // {
                //     for (int i = 0; i < 100; i++)
                //     {
                //         int width = rd.Next(0, this.width);//返回一个非负随机整数
                //         int height = rd.Next(50, this.Height);//返回一个非负随机整数
                //         this.CreateGraphics().DrawEllipse(new Pen(Brushes.Red, 1), new rectangle(width,height,10,10));
                //         //Delay
                //         Thread.Sleep(100);
                //     }
                // })
                //{ IsBackground =true};//指示为后台线程
                //thread.Start();

            }
        }

        public void BLayer(List<Layer> 单层任务数据对象的列表)
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public List<string> Status()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
    
    
    //实现单层铺粉控制模块的功能
    //实现单层铺粉控制模块的功能
    //实现单层铺粉控制模块的功能
    class 单层铺粉控制模块 : ILayerPowder
    {
        public List<string> GetAxises()
        {
            throw new NotImplementedException();
        }

        public Parameters GetPara()
        {
            throw new NotImplementedException();
        }

        public void Jog(long 轴号, bool 方向, double 数值)
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }


        GoogolMotionMap motionMap = new GoogolMotionMap();//创建GoogolMotionMap对象，供本窗口调用

        private void RunThread()
        {
            //返回到原点（铺粉车固高轴7）
            motionMap.GoHome(7, 40, true, 0);
        }
        private Thread GohomeThread;

        //自动执行固高的动作
        //每个动作之间，延时1s
        public void Run()
        {
            //点动下降动作：执行成型缸下降动作（自动）
            motionMap.trapPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                                         //（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
            motionMap.trapPrm.dec = 1000;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
            motionMap.trapPrm.velStart = 5;
            motionMap.trapPrm.smoothTime = 1;
            int position = Convert.ToInt32(Convert.ToDouble(this.stepTextBox1.Text) * 1000);//千脉冲对应的mm数，这个要尽可能的统一                   
                                                                                            //35是一个比较合适的参数值
                                                                                            //Dimetal-100（109）设备的驱动器比例是1000脉冲/MM
                                                                                            //double vel = 35;//————————————————————待实现，从其他的图形窗口中读取对应的值
            double vel = Convert.ToDouble(this.velTextBox1.Text);//千脉冲对应的mm数，这个要尽可能的统一
            motionMap.TrapMotion(8, ref motionMap.trapPrm, position, vel, 0, 0, false);//成型缸固高轴8


            //点动上升动作：执行粉料缸上升动作（自动）
            motionMap.trapPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                                         //（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
            motionMap.trapPrm.dec = 1000;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
            motionMap.trapPrm.velStart = 5;
            motionMap.trapPrm.smoothTime = 1;
            position = Convert.ToInt32(Convert.ToDouble(this.stepTextBox1.Text) * 1000);//千脉冲对应的mm数，这个要尽可能的统一//Dimetal-100（109）设备的驱动器比例是1000脉冲/MM
            vel = Convert.ToDouble(this.velTextBox1.Text);//千脉冲对应的mm数，这个要尽可能的统一
            motionMap.TrapMotion(3, ref motionMap.trapPrm, position, vel, 0, 0, false);//落粉/送粉固高轴3


            //JOG水平动作：执行铺粉动作（自动）
            if (motionMap.FlagGoHome == true)//首先关闭线程
            {
                GohomeThread.Abort();
                //motionMap.FlagGoHome = false;
                motionMap.FlagGoHome = false;
            }
            //多线程方式实现：
            ThreadStart entry = new ThreadStart(RunThread);//线程入口方法：CalcSum
            GohomeThread = new Thread(entry) { IsBackground = true };
            motionMap.FlagGoHome = true;//——————————————————————————标志位在调用出使用才可以
            GohomeThread.Start();
            /*****************************************************************************************
            //motionMap.StopMotion(1);
            ////执行JOG运动
            //motionMap.jogPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
            //motionMap.jogPrm.dec = 1000;
            //motionMap.jogPrm.smooth = 0;
            //double vel = Convert.ToDouble(this.velTextBox1.Text);//千脉冲对应的mm数，这个要尽可能的统一                   
            //motionMap.JogMotion(1, ref motionMap.jogPrm, vel);
            *****************************************************************************************/


            //成型缸下降-然后延时0.5s
            Thread.Sleep(500);

            //粉料缸上升-然后延时0.5s
            Thread.Sleep(500);

            //铺粉臂单向铺粉-运动到另一端，触碰限位停止-然后延时0.5s
            Thread.Sleep(500);

            //throw new NotImplementedException();
        }

        public void SetPara(Parameters 工艺参数)
        {
            throw new NotImplementedException();
        }

        public List<string> Status()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
    //单层喷墨成型模块
    //单层喷墨成型模块
    //单层喷墨成型模块
    //要等单层铺粉控制模块实现之后，再实现本部分
    //OK
    class 单层喷墨成型模块 : ILayerBinders
    {
        public List<string> GetAxises()
        {
            throw new NotImplementedException();
        }

        public Parameters GetPara()
        {
            throw new NotImplementedException();
        }

        public void Jog(long 轴号, bool 方向, double 数值)
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public void Run(List<Command> 指令数据)
        {
            throw new NotImplementedException();
        }

        public void SetPara(Parameters 工艺参数)
        {
            throw new NotImplementedException();
        }

        public List<string> Status()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void 喷墨()
        {
            throw new NotImplementedException();
        }
    }
}
