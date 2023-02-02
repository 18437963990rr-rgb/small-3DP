using BuildBMP;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;

namespace RIP组件
{
    public partial class 数据处理进度 : Form
    {
        public 数据处理进度()
        {
            InitializeComponent();
            StartCommunication();//开启通讯

        }
        /// <summary>
        /// 进程通讯状态机：20200507新建：
        /// </summary>
        /// <param name="ControlFlag"></param>
        private int StartStateMachineCommunicate(int ControlFlag)
        {
            //(a)获取列表控件的Tag中存储的ID
            //int myTag = Convert.ToInt32((sender as Control).Tag);
            //(c)计算并执行动作：
            int ReturnFlag = 0;
            switch (ControlFlag)//回零点：想起来了：20200110C#多线程爽的一比
            {
                case 0://不执行直接跳过：20200507新建

                    break;
                case 1://指令1：打开IPC开始通讯:1001为执行成功；1000为执行异常

                    ReturnFlag = IPCStart();//开启IPC服务API
                    //ReturnFlag =1001;
                    break;

                case 2://指令2：开启远程工作

                    //ReturnFlag = StartPicAPI();//开启远程生成API
                    StartPicEvent(this, new EventArgs());
                    ReturnFlag = 2001;
                    break;

                case 3://指令4：关闭IPC服务API：暂时不实现也可以
                    ReturnFlag = 3001;
                    break;

                case 4://指令3：关闭本身进程

                    Application.Exit();//20200507：直接关闭进程退出
                    ReturnFlag = 4001;//本处是无法返回的，因为程序已经结束
                    break;

            }
            return ReturnFlag;//20200507新建
        }
        public event EventHandler StartPicEvent;//定义委托类型的事件
        private void StartCommunication()//10s侦听1次，进行状态变换
        {
            StartPicEvent += new System.EventHandler(this.NetDataBtn_Click);

            int ReturnFlag = IPCStart();
            if (ReturnFlag == 1001)
            {
                SendControlCommand(ControlFlag, ReturnFlag);//写入到远程：返回returnFlag

                this.timer3.Tick += new System.EventHandler(this.timer3_Monitor);
                this.timer3.Interval = 10;
                timer3.Enabled = true;
            }
            else { MessageBox.Show("Start Communication Failed"); }

        }
        private int ControlFlag = 0;
        private int returnFlag = 0;
        private System.Windows.Forms.Timer timer3 = new System.Windows.Forms.Timer();
        private void timer3_Monitor(object sender, EventArgs e)//20200220:监控线程定时刷新定时器2//定时器本身就是1种线程处理方式
        {
            //(1)读取从远程对象中
            ReadControlCommand(ref ControlFlag, ref returnFlag);//目的在于读取ControlFlag

            if (ControlFlag != 0)//ControlFlag=0,就不执行:ControlFlag==0,没有接受到任何远程指令
            {
                //(2)进行状态机判断：执行不同的任务
                returnFlag = StartStateMachineCommunicate(ControlFlag);
                ControlFlag = 0;//关键：ControlFlag置位
                //(3)写入到远程对象中
                SendControlCommand(ControlFlag, returnFlag);//写入到远程：返回returnFlag
                ControlFlag = 0;//关键：ControlFlag置位
                returnFlag = 0;//关键：ControlFlag置位
            }            
        }

        /***********************************(1)远程的RIP工作：开始*********************************/
        /***********************************(1)远程的RIP工作：开始*********************************/
        /***********************************(1)远程的RIP工作：开始*********************************/
        //图片排版确认按钮：确认/停止
        public bool ComposeFlag = false;//初始为true，设置为false
        bool FinalJOBThreadExistedFlag = false;//JOBTHread是否创建过标志

        //202000507新增：开启及关闭排版
        private int StartPicAPI()//图片排版确认按钮————点击之后不可以修改，双击之后才可以修改
        {
            int returnCode = 0;
            try
            {
                //关闭排版任务
                if (FinalJOBThreadExistedFlag == false/*FinalJOBThread.IsAlive==true*/)//是否存在排版任务
                {
                    //(1)配置后台辅助工作的事件系统状态
                    SaveRIPFileBWorks.WorkerReportsProgress = true;//注册backgroundWorker1的监控报告事件:20200415新增
                    SaveRIPFileBWorks.WorkerSupportsCancellation = true;//注册backgroundWorker1的完成、BUG、取消事件:20200415新增

                    //(2)开启后台辅助工作（耗时操作）
                    if (SaveRIPFileBWorks.IsBusy != true)
                    {
                        ////(1)UI进行对应的设置：//20200602修改：统一移到后面的SaveRIPFileBWorks异步线程中去。
                        //this.JobsProgressBar.Minimum = 0;//进度条:异步刷新
                        //this.JobsProgressBar.Maximum = pointCoordinates.g_LayerDatas.Count()/*20*/;//进度条//20200602修改：修改为当前打印任务区间的终止层

                        // Start the asynchronous operation.
                        SaveRIPFileBWorks.RunWorkerAsync();

                        returnCode = 2001;//开启任务成功
                    }
                }
                else//开启排版任务
                {
                    //(3)关闭后台辅助工作（耗时操作）
                    if (SaveRIPFileBWorks.WorkerSupportsCancellation == true)
                    {
                        // Cancel the asynchronous operation.
                        SaveRIPFileBWorks.CancelAsync();
                        returnCode = 2002;//关闭任务成功
                    }
                }
            }
            catch (Exception)
            {
                returnCode = 2000;
            }
            return returnCode;
        }

        //20200415新增：使用BWorks代替Thread解决UI线程卡死问题尝试
        //20200425新增：将此后台线程的方法用于形式异步执行代码;————期待可以解决这个问题
        private async void SaveRIPFileBWorks_DoWork(object sender, DoWorkEventArgs e)
        {
            //（4）方式4;
            int result = await RunFinalJOBThreadNew();
        }

        private void SaveRIPFileBWorks_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }

        private void SaveRIPFileBWorks_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                MessageBox.Show("Canceled!");
            }
            else if (e.Error != null)
            {
                MessageBox.Show("Error: " + e.Error.Message);
            }
            else
            {
                //resultLabel.Text = "Done!";
                //MessageBox.Show("JOB文件生成完毕!");
            }
        }
        private bool FirstJOBFlag = true;
        ////static ManualResetEventSlim _FinalJobEvent = new ManualResetEventSlim(false); //手动复位事件：线程同步————important for the thread communication: 如果初始事件状态为true,那么 AutoResetEvent实例的状态为signaled
        //private Thread FinalJOBThread;//（1）生成FinalJOB线程——本线程支持手动开启和手动停止
        ///// <summary>
        ///// 生成FinalJOB线程内容:20200415新增批注
        ///// </summary>
        //private void RunFinalJOBThread()//（2）生成FinalJOB线程内容
        //{
        //    //判断保存的路径是否存在
        //    if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\JOB输出文件"))
        //    {
        //        Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\JOB输出文件");//创建路径
        //    }
        //    string FilePath = System.Windows.Forms.Application.StartupPath + @"\JOB输出文件\";//生成的文件夹目录，默认放在此文件夹中。

        //    for (int i = 0; i < 10; i++)//20200506：持续的输出零件：输入数据为20层，测试其中10层
        //    {
        //        if (FirstJOBFlag == true)//
        //        {
        //            CreateFinalJOB(600, i, FilePath);//在指定的文件夹生成大TIFF
        //            UpdateBarValueMethod(i + 1);//刷新进度条和label
        //        }
        //        else
        //        {
        //            CreateFinalJOB(600, i, FilePath);//在指定的文件夹生成大TIFF
        //            UpdateBarValueMethod(i + 1);//刷新进度条和label
        //            //_FinalJobEvent.Wait();//等待——非常关键，必须添加
        //        }
        //    }

        //    MessageBox.Show("JOB文件生成完毕");
        //}

        //20200507批注：（1）异步RIP线程中更新RIP的UI线程的控件;（2）异步RIP线程中更新远程对象
        private int LocalRemoteProgress = 0;//20200507批注：需要替换到远程对象
        private delegate void UpdateBarValue(int iValue);
        private void UpdateBarValueMethod(int iValue)
        {
            if (this.JobsProgressBar.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
            {
                if (null != JobsProgressBar && !JobsProgressBar.IsDisposed)//jobsProgressBar被释放？且不为空
                {
                    //（1）异步RIP线程中更新RIP的UI线程的控件;
                    JobsProgressBar.Value = iValue;
                    double rate = (double)iValue / (double)/*20*/  pointCoordinates.g_LayerDatas.Count() * 100;//20200602修改：修改为当前打印任务区间的终止层
                    string tempRate = rate.ToString("f2");
                    string showRate = "RIP 进度：  " + tempRate + "%";//保留小数点后两位
                    this.Text = showRate;//保留小数点后两位
                    this.Refresh();

                    //（2）异步RIP线程中更新远程对象:
                    dynamicValue = iValue-1;//20200602批注：消除总是少1层的BUG
                    bool returnCode = SetBackValue();
                    this.XLabel.Text = dynamicValue.ToString();
                }
            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpdateBarValue UpdateBar = new UpdateBarValue(UpdateBarValueMethod);
                this.JobsProgressBar.Invoke(UpdateBar, iValue);
            }
        }
        private delegate void UpdateBarMaximumValue(int iValue);
        private void UpdateBarMaxiumMethod(int iValue)
        {
            if (this.JobsProgressBar.InvokeRequired == false)//如果调用该函数的线程和控件lstMain位于同一个线程内
            {
                if (null != JobsProgressBar && !JobsProgressBar.IsDisposed)//jobsProgressBar被释放？且不为空
                {
                    //(1)UI进行对应的设置：//20200602修改：统一移到后面的SaveRIPFileBWorks异步线程中去。
                    this.JobsProgressBar.Minimum = 0;//进度条:异步刷新
                    this.JobsProgressBar.Maximum = iValue/*20*/;//进度条//20200602修改：修改为当前打印任务区间的终止层
                }
            }
            else//如果调用该函数的线程和控件lstMain不在同一个线程
            {
                UpdateBarMaximumValue UpdateBar = new UpdateBarMaximumValue(UpdateBarMaxiumMethod);
                this.JobsProgressBar.Invoke(UpdateBar, iValue);
            }
        }


        /// <summary>
        /// 生成FinalJOB线程内容:专门匹配后台辅助工作的新工作方式:20200415新增
        /// </summary>
        private async Task<int> RunFinalJOBThreadNew()//（2）生成FinalJOB线程内容，专门匹配后台辅助工作的新工作方式:20200415新增
        {
            return await Task.Run(() =>
            {
                //判断保存的路径是否存在
                if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\JOB输出文件"))
                {
                    Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\JOB输出文件");//创建路径
                }
                string FilePath = System.Windows.Forms.Application.StartupPath + @"\JOB输出文件\";//生成的文件夹目录，默认放在此文件夹中。

                //20200506新建：获取远程JOB对象
                clientService = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                    "ipc://localhost:9090/RemoteObject.rem");
                pointCoordinates = clientService.GetPositionData();

                //(1)UI进行对应的设置：//20200602修改：统一移到后面的SaveRIPFileBWorks异步线程中去。
                //this.JobsProgressBar.Minimum = 0;//进度条:异步刷新
                //this.JobsProgressBar.Maximum = pointCoordinates.g_LayerDatas.Count()/*20*/;//进度条//20200602修改：修改为当前打印任务区间的终止层
                UpdateBarMaxiumMethod(pointCoordinates.g_LayerDatas.Count()/*20*/);

                //myDelegateUpdateBarValue = new UpdateBarValue(UpdateBarValueMethod);//开启委托
                //this.JobsProgressBar.Minimum = 0;//进度条:异步刷新
                //this.JobsProgressBar.Maximum = 50;//进度条
                //bool FirstFlag = true;
                for (int i = 0; i < /*20*//*50*/ pointCoordinates.g_LayerDatas.Count(); i++)//20200602修改：修改为当前打印任务区间的终止层//20200506：持续的输出零件：输入数据为20层，测试其中10层
                {
                    if (FirstJOBFlag == true)//生成1层的数据：20200415新增：记得没错的话，应该是第一次手动排版的时候进入此环节
                    {
                        CreateFinalJOB(600, i, FilePath);//在指定的文件夹生成大TIFF
                        //System.Threading.Thread.Sleep(500);//20200415新增：完全没有必要
                        UpdateBarValueMethod(i + 1);//刷新进度条和label//20200415取消掉：理论上是可以用的，但是现在没必要了，专门匹配后台辅助工作的新工作方式后
                    }
                    else//生成1层的数据：20200415新增：
                    {
                        CreateFinalJOB(600, i, FilePath);//在指定的文件夹生成大TIFF
                        UpdateBarValueMethod(i + 1);//刷新进度条和label//20200506新增：
                        //_FinalJobEvent.Wait();//等待——非常关键，必须添加
                    }
                    ////20200419测试取消：避免在task中调用线程的东西
                    //SaveRIPFileBWorks.ReportProgress(i+1/*i * 10*/);//传递出对应的事件
                }

                //MessageBox.Show("JOB文件生成完毕");//没有必要出现这个
                return 1;//20200419新增：异步编程时的返回情况
            });
        }
        RemoteObject clientService;
        PointCoordinates pointCoordinates;
        //the aim is to only the once CLIimport job, because it is time consuming.
        //List<CLI> CliStreams = new List<CLI>();//全局存放CLI Stream的内存信息
        /// <summary>
        /// create final JOBS:输出大图：负责生成1层
        /// </summary>
        /// <param name="DPI"></param>
        /// <param name="layerID"></param>
        /// <param name="FilePath"></param>
        private void CreateFinalJOB(int DPI, int layerIndex, string FilePath)//create final JOBS
        {
            Bitmap outputBMP = new Bitmap((int)(420 * 600 / 25.4 + 1), (int)(350 * 600 / 25.4 + 1));//图片大小为
            outputBMP.SetResolution(600, 600);//设置分辨率
            Graphics g = Graphics.FromImage(outputBMP);//GDI+，对象
            g.SmoothingMode = SmoothingMode.AntiAlias;  //使绘图质量最高，即消除锯齿
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            //画笔，绘制轮廓线
            Pen OuterPen = new Pen(Color.Blue, 5);
            Pen InterPen = new Pen(Color.Green, 5);
            Pen OneLine = new Pen(Color.Black, 3);
            //画刷，内部填充刷
            Brush backcolorBrush = new SolidBrush(Color.White);//2020.04.08新增：
            Brush BrushOuter = new SolidBrush(Color.Green);
            Brush BrushInter = new SolidBrush(Color.White);
            PointF[] BackgroundPoly = { new PointF(0, 0), new PointF(outputBMP.Width,0), new PointF(outputBMP.Width, outputBMP.Height),
                new PointF(0,outputBMP.Height) };//2020.04.08新增：
            g.FillPolygon(backcolorBrush, BackgroundPoly);//输出轮廓内部填充//2020.04.08新增：          
            
            ////20200506新建：获取远程JOB对象
            //clientService = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
            //    "ipc://localhost:9090/RemoteObject.rem");
            //pointCoordinates = clientService.GetPositionData();
            ////pointCoordinates.g_LayerDatas.Count();//层数
            ////pointCoordinates.g_LayerDatas[0].aLayerData.Count();//零件数
            ////pointCoordinates.g_LayerDatas[0].aLayerData[0].aLayer.LineList[0].Lines[0].Count();//零件数

            RemoteCLIs c_RemoteCLIs = new RemoteCLIs();//20200506新建：存放1层的所有的绘制数据

            for (int i = 0; i < pointCoordinates.g_LayerDatas[layerIndex].aLayerData.Count(); i++)//20200505新增：遍历所有零件
            {
                float x = pointCoordinates.g_LayerDatas[layerIndex].aLayerData[i].x;
                float y = pointCoordinates.g_LayerDatas[layerIndex].aLayerData[i].y;

                Layer correctLAYER /*CLI correctCLI*/ = pointCoordinates.g_LayerDatas[layerIndex].aLayerData[i].aLayer;

                {
                    //（1）生成1层的排版零件图：20200506新建批注
                    Build1BitBmp.SetABigTiff(correctLAYER, x, y, FilePath, 
                        g, OuterPen, BrushOuter, InterPen, BrushInter, OneLine);//输出1层的排版零件图：20200506新建批注
                }
            }

            //（1-新）保存为单色BMP文件：20200408新增
            using (MemoryStream memoryStream = new MemoryStream())//本人觉得是精华//20200408新增：
            {
                Bitmap clone = /*MidJpgBmp*/outputBMP.Clone(new Rectangle(0, 0, outputBMP.Width, outputBMP.Height), PixelFormat.Format1bppIndexed);
                outputBMP.Dispose();//————————————————————————释放bmp文件
                //(C)SAVE & release
                clone.Save(FilePath + Convert.ToString(layerIndex) + ".bmp", ImageFormat.Bmp);//————保存到BMP文件:20200408修改
                clone.Dispose();//及时释放掉clone：20200408新增
            }
        }
        /***********************************(1)远程的RIP工作：开始*********************************/
        /***********************************(1)远程的RIP工作：开始*********************************/
        /***********************************(1)远程的RIP工作：开始*********************************/


        /***********************************(2)服务器端测试DEMO：开始*********************************/
        /***********************************(2)服务器端测试DEMO：开始*********************************/
        /***********************************(2)服务器端测试DEMO：开始*********************************/
        public double dynamicValue = 0;//动态值，初始化为0：20200504新增
        private bool SetBackValue()//从服务器回传给客户端
        {
            /*RemoteObject */
            service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");
            service.SetBackValue(dynamicValue);
            return true;
        }
        public void SendControlCommand(int controlFlag, int returnFlag)//20200507:
        {
            service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");
            service.SendControlCommand(controlFlag, returnFlag);

        }
        public void ReadControlCommand(ref int controlFlag, ref int returnFlag)//20200507:
        {
            service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");
            service.ReadControlCommand(ref controlFlag, ref returnFlag);
        }


        private int IPCStart( )
        {
            int returnCode = 0;
            try
            {
                //（1） Create the server channel.
                IpcChannel ipcServerChannel = new IpcChannel("localhost:9090"/*"testChannel"*/);

                //（2）Register the server channel.
                ChannelServices.RegisterChannel(ipcServerChannel, false);

                //（3）Show the name of the channel.
                string tempString = ("The name of the channel is " + ipcServerChannel.ChannelName + ".\r\n");
                InformationBox.AppendText(tempString);

                //（4）Show the priority of the channel.
                tempString = ("The priority of the channel is " + ipcServerChannel.ChannelPriority + ".\r\n");
                InformationBox.AppendText(tempString);

                //（5）Show the URIs associated with the channel.
                ChannelDataStore channelData =
                    (ChannelDataStore)ipcServerChannel.ChannelData;
                foreach (string uri in channelData.ChannelUris)
                {
                    InformationBox.AppendText("The channel URI is  " + uri + ".\r\n");
                }

                //（6） Expose an object for remote calls.//RemoteSample:是远程对象
                System.Runtime.Remoting.RemotingConfiguration.
                    RegisterWellKnownServiceType(
                        typeof(RemoteObject), "RemoteObject.rem"/*"RemoteObject.rem"*/,
                        System.Runtime.Remoting.WellKnownObjectMode.Singleton);

                //（7）Parse the channel's URI.
                string[] urls = ipcServerChannel.GetUrlsForUri("RemoteObject.rem");
                if (urls.Length > 0)
                {
                    string objectUrl = urls[0];
                    string objectUri;
                    string channelUri = ipcServerChannel.Parse(objectUrl, out objectUri);

                    InformationBox.AppendText("The object URI is   " + objectUri + ".\r\n");
                    InformationBox.AppendText("The channel URI is  " + channelUri + ".\r\n");
                    InformationBox.AppendText("The object URL is  " + objectUrl + ".\r\n");
                }

                //（8）Wait for the user prompt.
                InformationBox.AppendText("Press ENTER to exit the server.\r\n");
                Console.ReadLine();//这一行需要特别省视
                InformationBox.AppendText("The server is exiting.\r\n");

                returnCode = 1001;//正确的指令
            }
            catch (Exception)
            {
                returnCode = 1000;//错误的指令
            }
            //finally{}
            return returnCode;//返回执行状态：20200507新建
        }
        private void IPCStartBtn_Click(object sender, EventArgs e)
        {
            //（1） Create the server channel.
            IpcChannel ipcServerChannel = new IpcChannel("localhost:9090"/*"testChannel"*/);

            //（2）Register the server channel.
            ChannelServices.RegisterChannel(ipcServerChannel, false);

            //（3）Show the name of the channel.
            string tempString = ("The name of the channel is " + ipcServerChannel.ChannelName + ".\r\n");
            InformationBox.AppendText(tempString);

            //（4）Show the priority of the channel.
            tempString = ("The priority of the channel is " + ipcServerChannel.ChannelPriority + ".\r\n");
            InformationBox.AppendText(tempString);

            //（5）Show the URIs associated with the channel.
            ChannelDataStore channelData =
                (ChannelDataStore)ipcServerChannel.ChannelData;
            foreach (string uri in channelData.ChannelUris)
            {
                InformationBox.AppendText("The channel URI is  " + uri + ".\r\n");
            }

            //（6） Expose an object for remote calls.//RemoteSample:是远程对象
            System.Runtime.Remoting.RemotingConfiguration.
                RegisterWellKnownServiceType(
                    typeof(RemoteObject), "RemoteObject.rem"/*"RemoteObject.rem"*/,
                    System.Runtime.Remoting.WellKnownObjectMode.Singleton);

            //（7）Parse the channel's URI.
            string[] urls = ipcServerChannel.GetUrlsForUri("RemoteObject.rem");
            if (urls.Length > 0)
            {
                string objectUrl = urls[0];
                string objectUri;
                string channelUri = ipcServerChannel.Parse(objectUrl, out objectUri);

                InformationBox.AppendText("The object URI is   " + objectUri + ".\r\n");
                InformationBox.AppendText("The channel URI is  " + channelUri + ".\r\n");
                InformationBox.AppendText("The object URL is  " + objectUrl + ".\r\n");
            }

            //（8）Wait for the user prompt.
            InformationBox.AppendText("Press ENTER to exit the server.\r\n");
            Console.ReadLine();//这一行需要特别省视
            InformationBox.AppendText("The server is exiting.\r\n");
        }

        private void ParseBtn_Click(object sender, EventArgs e)//20200503批注：从通道的两端，都可以通过以下的方式读取远程对象的数据，但是要注意冲突的问题。
        {
            RemoteObject service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");
            // (2)跨进程调用对象关键:Invoke a method on the remote object.
            PointCoordinates returnCoordinates = service.GetPosition();
        }

        private System.Windows.Forms.Timer timer2 = new System.Windows.Forms.Timer();
        private void NetDataBtn_Click(object sender, EventArgs e)
        {
            StartPicAPI();//开启远程生成
#if false
            this.timer2.Tick += new System.EventHandler(this.timer2_Monitor);
            this.timer2.Interval = 25;
            timer2.Enabled = true;
#endif
        }

        public RemoteObject service;//20200504新增：

        private void timer2_Monitor(object sender, EventArgs e)//20200220:监控线程定时刷新定时器2//定时器本身就是1种线程处理方式
        {
            dynamicValue++;
            bool returnCode = SetBackValue();
            this.XLabel.Text = dynamicValue.ToString();
        }
        /***********************************服务器端测试DEMO：结束*********************************/
        /***********************************服务器端测试DEMO：结束*********************************/
        /***********************************服务器端测试DEMO：结束*********************************/
    }
}
