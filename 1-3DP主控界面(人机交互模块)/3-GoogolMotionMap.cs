using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using gts;
//using GoogolConfig;//（1）取消掉Config类:原Config类的一个方法是适用于固高的指令错误显示;（2）取消掉Config类:原Config类的另一一个方法是打开软件时：检测Googol通讯是否正常。
//using ReadCondition;//(1) 本项不是很关键
using System.Windows.Forms;
using System.Threading;

namespace Motion
{
    //(1)本类的全部方法实现，使用对象方法和对象变量的方法实现
    //(2)不使用静态变量和静态方法
    public class GoogolMotionMap
    {
        private readonly short cardNumber = 0;//私有变量——默认选择为运动控制卡1

        public gts.mc.TTrapPrm trapPrm;//点动运动参数——对外开放访问
        public gts.mc.TJogPrm jogPrm;//JOG运动参数——对外开放访问

        //世彪20104新增
        public bool[] m_bIoEnable = new bool[20];//20个按钮的值——固高的值为16个EXO——20200109备注

        public bool m_bXmove = false;
        public bool m_bYmove1 = false;
        public bool m_bYmove2 = false;


        //public double vel, prfvel, encvel, prfpos, encpos;//保存监测到的位置参数：设置的速度、规划速度、编码器速度、规划位置、编码器位置
        //允许外部函数调用，虽然基本上不会被调用到

        public struct AxisMotionMonitor //保存监测到的运动参数（C#结构体变量）：本数据被输出到状态栏
        {
            public double prfpos;               // 规划位置
            public double prfvel;               // 规划速度
            public double prfacc;                    // 规划加速度
            //public double prfdec;                    // 规划减速度（没有减速度这么一说）
            //public double encvel;               // 编码器速度
            //public double encacc;                    //编码器位置
            public int MotionMode;                // 运动模式
            //public string MotinModeName;                 // 运动模式，字符串变量
        }
        AxisMotionMonitor axisMotionMonitor;
        public struct AxisSateMonitor //保存监测到的轴状态参数（C#结构体变量）：本数据被输出到状态栏
        {
            public bool FlagServoAlarm;            //伺服报警
            public bool FlagPosLimit1;          //正限位
            public bool FlagNegLimit1;         //负限位

            public bool FlagError;             //跟随误差越限标记
            public bool smoothStop;            //平滑停止
            public bool abruptStop;            //急停
            public bool ServoOn;               //伺服开
            public bool profileMotion;         //规划器运动
        }
        public AxisSateMonitor axisSateMonitor;

        private System.Action<string> _infoLogSink;
        private System.Action<string> _errorLogSink;
        public bool MotionDebugEnabled = true;

        public void SetLogSink(System.Action<string> infoSink, System.Action<string> errorSink = null)
        {
            _infoLogSink = infoSink;
            _errorLogSink = errorSink ?? infoSink;
        }

        private void LogInfo(string message)
        {
            if (_infoLogSink != null)
            {
                _infoLogSink(message);
                return;
            }

            System.Diagnostics.Trace.WriteLine(message);
        }

        private void LogError(string message)
        {
            if (_errorLogSink != null)
            {
                _errorLogSink(message);
                return;
            }

            System.Diagnostics.Trace.WriteLine(message);
        }

        private void LogMotionDebug(string message)
        {
            if (!MotionDebugEnabled)
            {
                return;
            }

            LogInfo(message);
        }

        //PART1:指令检测
        //（1）取消掉Config类:原Config类的一个方法是适用于固高的指令错误显示。
        //（2）取消掉Config类:原Config类的另一一个方法是打开软件时：检测Googol通讯是否正常。
        public void Commandhandler(string command, short error)
        //public static void commandhandler(string command, short error)
        {
            // 如果指令执行返回值为非0，说明指令执行错误，向屏幕输出错误结果
            if (error != 0)
            {
                LogError($"{command}:{error}");
                MessageBox.Show(command + ":" + error);
            }
        }
        //读取通用输出IO高电平//20200309新增：暂时不需要//20220523新建：红外控制器通讯时，需要检测通讯设备是否完好
        public bool/*void*/ GetDo(short DoNumber/*, out bool OutputState*/)
        {
            short sRtn;
            //通用输出IO高电平
            int OutputValues;
            sRtn = mc.GT_GetDo(cardNumber, 12, out OutputValues);//(1)12为通用输出控制//(2)底层电气是默认输出高电平，有效时为低电平——20100109
            uint result = ((uint)((0x1) << DoNumber)) & (uint)OutputValues;//DO对象
            if (result!=0)//此处存在反常逻辑，已处理:输出低电平时候，温度控制器通电状态
            {
               /* OutputState = false; */return false;//20220523新建：温控仪表关闭状态
            }
            else
            {
                /*OutputState = true; */return true;//20220523新建：温控仪表打开状态
            }
        }


        //通用输出IO高电平
        public void SetDo(short DoNumber, bool value)
        //public static void SetDo(short DoNumber, bool value)
        {
            short sRtn;
            //sRtn = gts.mc.GT_SetDoBit(cardNumber,
            //    gts.mc.MC_GPO,              // 指定数字IO类型是通用输出c
            //    1,                      // 指定第7个通用输出，即EXO6
            //    1);                     // 输出低电平

            if (value == true)//输出动作
            {
                //通用输出IO高电平
                sRtn = mc.GT_SetDoBit(cardNumber, 12, DoNumber, 0/*1*/);//底层电气是默认输出高电平，有效时为低电平——20100109
                if (sRtn != 0)
                {
                    //MessageBox.Show("set Do Level is error！");
                }
            }
            else if (value == false)//关闭动作
            {
                //通用输出IO低电平
                //short sRtn;
                sRtn = mc.GT_SetDoBit(cardNumber, 12, DoNumber, 1/*0*/);//底层电气是默认输出高电平，有效时为低电平——20100109
                if (sRtn != 0)
                {
                    //MessageBox.Show("set Do Level is error！");
                }
            }
        }
        public bool SreaderAxisHomeFlag = false;//20220526新建：撒粉轴回零标志位
        /// <summary>
        /// 寻开槽位置并找正位置：20220526新建并批注
        /// </summary>
        /// <param name="AXIS"></param>
        /// <param name="homeVel"></param>
        /// <param name="search_home"></param>
        /// <param name="SinkPostion"></param>
        public bool SetBackSpreaderAxis(short AXIS, double homeVel/*单位 圈/s*/, int search_home/*原点搜索值：2圈，一定可搜索到原点，单位圈数*/, double SinkPostion/*开槽位置：确保开槽位置的运动准确，单位度数*/)
        {
            short sRtn = gts.mc.GT_ClrSts(cardNumber, AXIS, 8); Commandhandler("GT_ClrSts", sRtn);//(0-1)清除指定轴的报警和限位 
            sRtn = gts.mc.GT_AxisOn(0, AXIS); Commandhandler("GT_AxisOn", sRtn);//(0-2)驱动器使能

            EncOff();//20200226新建：使用内部脉冲计数器                     
            sRtn = gts.mc.GT_SetCaptureMode(cardNumber, AXIS, gts.mc.CAPTURE_HOME); Commandhandler("GT_SetCaptureMode", sRtn);// (1)启动Home捕获
            sRtn = gts.mc.GT_PrfTrap(cardNumber, AXIS); Commandhandler("GT_PrfTrap", sRtn);// (2)切换到点位运动模式
            sRtn = gts.mc.GT_ZeroPos(cardNumber, AXIS, 8); Commandhandler("GT_ZeroPos", sRtn);//清零规划位置和实际位置，并进行零飘补偿。
            sRtn = gts.mc.GT_SetEncPos(cardNumber, AXIS, 0); Commandhandler("GT_SetEncPos", sRtn);//设置单轴的编码器位置为0

            trapPrm.acc = 0.5; trapPrm.dec = 0.5/*1000*/; trapPrm.velStart = 0; trapPrm.smoothTime = 0;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大//(3)执行点动运动

            sRtn = gts.mc.GT_SetTrapPrm(cardNumber, AXIS, ref trapPrm);// (3-1)设置点位模式运动参数 
            int subdivided = 1600;//20220527新增：适配细分值
            if (AXIS == 3)
            { subdivided = (int)(1600*2.1); }
            else if (AXIS == 4)
            { subdivided = 12800/*25600*/; }//刮墨轴的步进的细分参数：12800；电流：2.2 A
            else { }
            int position = (int)(search_home * /*2.1 * */subdivided/*1600*/);//20201011修正：在固高控制器中，每mm对应1000个脉冲//千脉冲 1000 Pulse/MM
            double vel = homeVel /** 2.1 *// 1 * 1 * (subdivided/*1600*/ / 1000);//电机转动速度


            sRtn = gts.mc.GT_SetVel(cardNumber, AXIS, vel/*10*/);// (3-2)设置点位模式目标速度，即回原点速度
            sRtn = gts.mc.GT_SetPos(cardNumber, AXIS, position); Commandhandler("GT_SetPos", sRtn);//运行search_home，来运动到捕获位置// (4)设置点位模式目标位置，即原点搜索距离  
            sRtn = gts.mc.GT_Update(cardNumber, 1 << (AXIS - 1)); Commandhandler("GT_Update", sRtn);// (5)启动运动

            uint pClock; int status = 0; double prfPos1, prfPos;/*规划器位置，这里没啥用*/ double encPos1, encPos;/*编码器位置*/short capture/* = new short[8]*/;/*轴捕获状态*/int pos;//读取到的位置，

            //（一）进入捕获阶段/***************************************************************************/
            double[] k_dJourney1 = new double[8]; double[] k_dJourney = new double[8];// 如果home信号已经触发，则退出循环，捕获位置已经在pos变量中保存
            do
            {
                //(6)(7)(8)
                gts.mc.GT_GetSts(cardNumber, AXIS, out status, 1, out pClock);
                gts.mc.GT_GetCaptureStatus(cardNumber, AXIS, out capture/*捕获标志位*/, out pos/*捕获编码器位置*/, 1, out pClock);//20220511新建批注：读取此刻的捕获状态和捕获位置，尤其是捕获位置
#if true
                gts.mc.GT_GetPrfPos(cardNumber, AXIS, out prfPos1/*捕获规划器位置*/, 1, out pClock);

                k_dJourney1 = GetEncPos();//20220511批注：读取所有8轴的编码器位置 //运动到正限，读取行程值。单位：脉冲//临时注释掉：
                encPos1 = k_dJourney1[AXIS-1/*2*//*1*//*3*/];//20220511批注：获取第2轴的编码器位置//20220512批注：编码器输出值反转，软件修补：取反//20220527修改：兼容刮墨轴
#endif
                //运动停止，返回出错信息
                if (0 == (status & 0x400))//返回出错信息//运动结束标志位
                {
                    return false;
                }
            }
            while (capture == 0);//20220511批注：当捕获标志位置位后，退出捕获阶段
            //MessageBox.Show("零位此刻触发！");

            //MessageBox.Show("捕获编码器位置=" + pos);//20220526新增：暂时注释
            //20220511批注：脱离捕获位置区域//找到问题在哪里了：编码器的位置接反了：原因应该再次；明天和宏鸣确认
            position = -pos + (int)(SinkPostion / 360 */* 2.1 **/ subdivided/*1600*//*1000*/);//20220511批注：下一步点动的偏移量，可正可负//20201011修正：在固高控制器中，每mm对应1000个脉冲    
            gts.mc.GT_SetPos(cardNumber, AXIS, position); Commandhandler("GT_SetPos", sRtn);//(9)// 设定目标位置为捕获位置+偏移量

            // 如果home信号已经触发，则退出循环，捕获位置已经在pos变量中保存         
            sRtn = gts.mc.GT_Update(cardNumber, 1 << (AXIS - 1)); Commandhandler("GT_Update", sRtn);//(10) 启动运动:在运动状态下更新目标位置

            //（二）进入运动终止阶段/***************************************************************************/
            do
            {
                Thread.Sleep(50);//确保停稳20200616新增：//(11)(12)（13）
                gts.mc.GT_GetSts(cardNumber, AXIS, out status, 1, out pClock);
            }
            while ((status & 0x400) != 0);//运动停止
            Thread.Sleep(200);//确保停稳20200616新增：

            //(14)
            gts.mc.GT_GetPrfPos(cardNumber, AXIS, out prfPos/*终止时刻规划期位置*/, 1, out pClock);//20220511新建：返回，终止时刻规划期位置
            k_dJourney = GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
            encPos = k_dJourney[AXIS - 1/*2*//*1*//*3*/];//20220511批注：获取第2轴的编码器位置//20220511批注：修改为第2轴的值//20220512批注：编码器输出值反转，软件修补：取反//20220527修改：兼容刮墨轴

            //校验：是否运动准确
            if (encPos != -position)//20220511批注：编码器计数方向，TMD恰好和规划器方向相反，恶心死了//进一步简化代码
            {
                SreaderAxisHomeFlag = false;//20200627批注：墨车回零成功标志位
                //MessageBox.Show("捕获编码器位置=" + pos +"继续运动后，新的编码器位置="+encPos);//20220526新增：暂时注释
                return false;//20200602：回零失败
            }
            else//重置编码器位置//20200602修改：home_value设置为0比较合适
            {
                position = (int)((0/*home_value*/ + SinkPostion) * 1000);//20201011修正：在固高控制器中，每mm对应1000个脉冲
                sRtn = gts.mc.GT_SetEncPos(cardNumber, AXIS, -position);//设置单轴的编码器位置//20220512修正：修正编码器值与规划期值相反问题
                Commandhandler("GT_SetEncPos", sRtn);
                SreaderAxisHomeFlag = true;//20200627批注：墨车回零成功标志位
            }
            return true;
        }
        public bool TrapMoveSpreaderAxis(short AXIS, double homeVel/*单位 圈/s*/, double SinkPostion/*开槽位置：确保开槽位置的运动准确，单位度数*/)
        {
            short sRtn = gts.mc.GT_ClrSts(cardNumber, AXIS, 8); Commandhandler("GT_ClrSts", sRtn);//(0-1)清除指定轴的报警和限位 
            sRtn = gts.mc.GT_AxisOn(0, AXIS); Commandhandler("GT_AxisOn", sRtn);//(0-2)驱动器使能

            EncOff();//20200226新建：使用内部脉冲计数器                     
            sRtn = gts.mc.GT_SetCaptureMode(cardNumber, AXIS, gts.mc.CAPTURE_HOME); Commandhandler("GT_SetCaptureMode", sRtn);// (1)启动Home捕获
            sRtn = gts.mc.GT_PrfTrap(cardNumber, AXIS); Commandhandler("GT_PrfTrap", sRtn);// (2)切换到点位运动模式
            sRtn = gts.mc.GT_ZeroPos(cardNumber, AXIS, 8); Commandhandler("GT_ZeroPos", sRtn);//清零规划位置和实际位置，并进行零飘补偿。
            sRtn = gts.mc.GT_SetEncPos(cardNumber, AXIS, 0); Commandhandler("GT_SetEncPos", sRtn);//设置单轴的编码器位置为0

            trapPrm.acc = 0.5; trapPrm.dec = 0.5/*1000*/; trapPrm.velStart = 0; trapPrm.smoothTime = 0;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大//(3)执行点动运动

            sRtn = gts.mc.GT_SetTrapPrm(cardNumber, AXIS, ref trapPrm);// (3-1)设置点位模式运动参数 
            int subdivided = 1600;//20220527新增：适配细分值
            subdivided = 12800/*25600*/; 
            uint pClock; int status = 0;
            double vel = homeVel /** 2.1 *// 1 * 1 * (subdivided/*1600*/ / 1000);//电机转动速度 
            int position = (int)(SinkPostion / 360 */* 2.1 **/ subdivided/*1600*//*1000*/);//20220511批注：下一步点动的偏移量，可正可负//20201011修正：在固高控制器中，每mm对应1000个脉冲    
            sRtn = gts.mc.GT_SetVel(cardNumber, AXIS, vel/*10*/);// (3-2)设置点位模式目标速度，即回原点速度 
            gts.mc.GT_SetPos(cardNumber, AXIS, position); Commandhandler("GT_SetPos", sRtn);//(9)// 设定目标位置为捕获位置+偏移量

            // 如果home信号已经触发，则退出循环，捕获位置已经在pos变量中保存         
            sRtn = gts.mc.GT_Update(cardNumber, 1 << (AXIS - 1)); Commandhandler("GT_Update", sRtn);//(10) 启动运动:在运动状态下更新目标位置

            //（二）进入运动终止阶段/***************************************************************************/
            do
            {
                Thread.Sleep(50);//确保停稳20200616新增：//(11)(12)（13）
                gts.mc.GT_GetSts(cardNumber, AXIS, out status, 1, out pClock);
            }
            while ((status & 0x400) != 0);//运动停止
            Thread.Sleep(200);//确保停稳20200616新增：

            return true;
        }


        /// <summary>
        /// search_home:为搜索距离;reset_home为复位校准距离;home_value，为原点校准之后的复位值//20220512修改：修改函数名称为SetBackHome,更容易理解
        /// </summary>
        /// <param name="AXIS"></param>
        /// <param name="search_home"></param>
        /// <param name="reset_home"></param>
        public bool SetBackHome(short AXIS, double/*int*/ home_value /*原点复位值*/, double homeVel, int search_home/*搜索距离*/, double/*int*/ home_offset/*默认复位位置：脱离距离*/, ref bool PowderCarHomeFlag)
        {
            //(0-1)清除指定轴的报警和限位
            short sRtn = gts.mc.GT_ClrSts(cardNumber, AXIS, 8);
            Commandhandler("GT_ClrSts", sRtn);

            //(0-2)驱动器使能
            sRtn = gts.mc.GT_AxisOn(0, AXIS);
            Commandhandler("GT_AxisOn", sRtn);
            EncOff();//20200226新建：使用内部脉冲计数器          

            // (1)启动Home捕获
            /*short */
            sRtn = gts.mc.GT_SetCaptureMode(cardNumber, AXIS, gts.mc.CAPTURE_HOME);
            Commandhandler("GT_SetCaptureMode", sRtn);
            // (2)切换到点位运动模式
            sRtn = gts.mc.GT_PrfTrap(cardNumber, AXIS);
            Commandhandler("GT_PrfTrap", sRtn);

            sRtn = gts.mc.GT_ZeroPos(cardNumber, AXIS, 8);//清零规划位置和实际位置，并进行零飘补偿。
            Commandhandler("GT_ZeroPos", sRtn);

            sRtn = gts.mc.GT_SetEncPos(cardNumber, AXIS, 0);//设置单轴的编码器位置为0
            Commandhandler("GT_SetEncPos", sRtn);

#if false//参数设置：
            trapPrm.acc = 0.25;
            trapPrm.dec = 0.25;
            // 设置点位模式运动参数
            sRtn = gts.mc.GT_SetTrapPrm(cardNumber, AXIS, ref trapPrm);
            // 设置点位模式目标速度，即回原点速度
            sRtn = gts.mc.GT_SetVel(cardNumber, AXIS, 10);
#else

            //(3)执行点动运动//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
            trapPrm.acc = 0.25;
            trapPrm.dec = 0.25/*1000*/;
            trapPrm.velStart = 5;
            trapPrm.smoothTime = 1;

            // (3-1)设置点位模式运动参数
            sRtn = gts.mc.GT_SetTrapPrm(cardNumber, AXIS, ref trapPrm);

            //千脉冲 1000 Pulse/MM
            int position = (int)(search_home * 1000);//20201011修正：在固高控制器中，每mm对应1000个脉冲
            double vel = Convert.ToDouble(homeVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一

            // (3-2)设置点位模式目标速度，即回原点速度
            sRtn = gts.mc.GT_SetVel(cardNumber, AXIS, vel/*10*/);
#endif
            // (4)设置点位模式目标位置，即原点搜索距离
            sRtn = gts.mc.GT_SetPos(cardNumber, AXIS, position);//运行search_home，来运动到捕获位置
            Commandhandler("GT_SetPos", sRtn);
            // (5)启动运动
            sRtn = gts.mc.GT_Update(cardNumber, 1 << (AXIS - 1));
            Commandhandler("GT_Update", sRtn);
            uint pClock;
            int status = 0;
#if false
            //double encPos /*= new double[8]*/;//编码器位置，这里没啥用
#endif
            double prfPos1, prfPos/* = new double[8]*/;//规划器位置，这里没啥用
            double encPos1, encPos;//编码器位置
            short capture/* = new short[8]*/;//轴捕获状态
            int pos;//读取到的位置，

            //（一）进入捕获阶段/***************************************************************************/
            // 如果home信号已经触发，则退出循环，捕获位置已经在pos变量中保存
            double[] k_dJourney1 = new double[8];
            double[] k_dJourney = new double[8];
            do
            {
                //(6)(7)(8)
                gts.mc.GT_GetSts(cardNumber, AXIS, out status, 1, out pClock);
                gts.mc.GT_GetCaptureStatus(cardNumber, AXIS, out capture/*捕获标志位*/, out pos/*捕获编码器位置*/, 1, out pClock);//20220511新建批注：读取此刻的捕获状态和捕获位置，尤其是捕获位置
#if true
                gts.mc.GT_GetPrfPos(cardNumber, AXIS, out prfPos1/*捕获规划器位置*/, 1, out pClock);
                //gts.mc.GT_GetEncPos(cardNumber, AXIS, out encPos1, 1, out pClock);//encPos: 读取编码器位置

                k_dJourney1 = GetEncPos();//20220511批注：读取所有8轴的编码器位置 //运动到正限，读取行程值。单位：脉冲//临时注释掉：
                encPos1 = k_dJourney1[1/*3*/];//20220511批注：获取第2轴的编码器位置//20220512批注：编码器输出值反转，软件修补：取反
#endif
                //运动停止，返回出错信息
                if (0 == (status & 0x400))//返回出错信息
                {
                    return false;
                }
            }
            while (capture == 0);//20220511批注：当捕获标志位置位后，退出捕获阶段
            //MessageBox.Show("零位此刻触发！");

            //20220511批注：脱离捕获位置区域//找到问题在哪里了：编码器的位置接反了：原因应该再次；明天和宏鸣确认
            position = -pos + (int)(home_offset * 1000);//20220511批注：下一步点动的偏移量，可正可负//20201011修正：在固高控制器中，每mm对应1000个脉冲
            //(9)
            gts.mc.GT_SetPos(cardNumber, AXIS, position);// 设定目标位置为捕获位置+偏移量
            Commandhandler("GT_SetPos", sRtn);
            // 如果home信号已经触发，则退出循环，捕获位置已经在pos变量中保存
            //(10) 启动运动:在运动状态下更新目标位置
            sRtn = gts.mc.GT_Update(cardNumber, 1 << (AXIS - 1));
            Commandhandler("GT_Update", sRtn);

            //（二）进入运动终止阶段/***************************************************************************/
            do
            {
                Thread.Sleep(50);//确保停稳20200616新增：
                //(11)(12)（13）
                gts.mc.GT_GetSts(cardNumber, AXIS, out status, 1, out pClock);
#if true
                //k_dJourney = GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
                //encPos = k_dJourney[3];
#endif
            }
            while ((status & 0x400) != 0);//运动停止
            Thread.Sleep(200);//确保停稳20200616新增：

            //(14)
            gts.mc.GT_GetPrfPos(cardNumber, AXIS, out prfPos/*终止时刻规划期位置*/, 1, out pClock);//20220511新建：返回，终止时刻规划期位置
            k_dJourney = GetEncPos();//运动到正限，读取行程值。单位：脉冲//临时注释掉：
            encPos = k_dJourney[1/*3*/];//20220511批注：获取第2轴的编码器位置//20220511批注：修改为第2轴的值//20220512批注：编码器输出值反转，软件修补：取反

            //校验：是否运动准确
            if (encPos != position)//20220511批注：编码器计数方向，TMD恰好和规划器方向相反，恶心死了//进一步简化代码
            {
#if false
                MessageBox.Show("Home出错！！" + ",捕获规划器数值：" + prfPos1 + "，捕获编码器数值：" + encPos1 + "；" +
                    "终止规划器数值：" + prfPos + "终止编码器数值：" + encPos + "。目标设置数值：" + position);
#endif
                PowderCarHomeFlag = false;//20200627批注：墨车回零成功标志位
                return false;//20200602：回零失败
            }
            else//重置编码器位置//20200602修改：home_value设置为0比较合适
            {
                position = (int)((home_value + home_offset) * 1000);//20201011修正：在固高控制器中，每mm对应1000个脉冲
                sRtn = gts.mc.GT_SetEncPos(cardNumber, AXIS, -position);//设置单轴的编码器位置//20220512修正：修正编码器值与规划期值相反问题
                Commandhandler("GT_SetEncPos", sRtn);
#if false
                MessageBox.Show("Home成功！！" + ",捕获规划器数值:" + prfPos1 + "，捕获编码器数值：" + encPos1 + "；" +
                    "终止规划器数值：" + prfPos + "终止编码器数值：" + encPos + "。目标设置数值：" + (int)(home_value * 1000));
#endif
                PowderCarHomeFlag = true;//20200627批注：墨车回零成功标志位
            }
            return true;
        }


        //使能伺服：开启伺服————给伺服设备供电
        //此使能伺服（伺服供电）和彼使能伺服不同（控制器伺服资源）
        public void PowerSever(ref short state)
        {
            // 指令返回值
            short sRtn;
            // EXO6输出高电平，使指示灯亮
            sRtn = gts.mc.GT_SetDoBit(cardNumber,
                gts.mc.MC_GPO,              // 指定数字IO类型是通用输出
                1,                      // 指定第7个通用输出，即EXO6————照明是7，伺服是1
                state);                     // 1对应输出低电平；0对应输出高电平
                                            //1);                     // 1对应输出低电平；0对应输出高电平
            Commandhandler("GT_SetDoBit", sRtn);
        }

        //（一）打开运动控制器
        //（1）打开软件的时候，完成控制器的通讯检测
        //（2）运动控制器检测
        //（3）打开运动控制器
        public void OpenCardComunication()
        //public static short openCard(short p_card)
        {
            short sRtn = mc.GT_Open(cardNumber, 0, 0);
            //Commandhandler("打开控制器", sRtn);//返回指令判断——太烦人了，每次都弹出
        }

        //（二）复位控制器
        //复位控制器：————————————————目的是：复位控制模式——复位后，默认的控制模式是“脉冲+方向的脉冲控制方式”
        public void ResetCardControlMode()//————————复位控制卡
        {
            gts.mc.GT_Reset(cardNumber);
        }

        //（三）初始化控制器
        //初始化控制器：———————————————目的是：（1）下载配置文件（具体就包括，控制器软硬件资源的配置：设置各轴的报警、正负限位、是否有效、规划模式等等）；
        //初始化控制器：———————————————目的是：（2）清除指定轴的状态字；
        //初始化控制器：———————————————目的是：（3）设置轴的运动模式（具体体现为：点位运动、JOG运动、电子齿轮运动、插补运动、电子凸轮运动等等）。
        //控制器初始化——————————————————————————————待完成
        //根据（a）轴号、(b)类型、（c）卡号没卵用——————选择初始化的轴类型
        public void InitCardConfiguration()//20200111修改：下载配置文件/固高的限位设置bug/固高的限位设置bug
        {
            gts.mc.GT_LoadConfig(cardNumber, "GTS800-加限位.cfg");//下载配置文件
#if false //20220505屏蔽此前的固高限位bug修复策略
            //ushort sValue = 0xFCFF/*0xFFFF*/;//20200111修改：固高的限位设置bug//20200426修改为FCFF:为Od1111110011111111
            ushort sValue = 0xF0FF/*0xFFFF*/;//20200111修改：固高的限位设置bug//20200426修改为FCFF:为Od1111110011111111//20200623修改：修改6轴限位触发电平
            gts.mc.GT_LmtSns(cardNumber, sValue);//20200111修改：固高的限位设置bug
#else
            ////ushort sValue = 0xFCFF/*0xFFFF*/;//20200111修改：固高的限位设置bug//20200426修改为FCFF:为Od1111110011111111
            //ushort sValue = 0xF0FF/*0xFFFF*/;//20200111修改：固高的限位设置bug//20200426修改为FCFF:为Od1111110011111111//20200623修改：修改6轴限位触发电平
            //gts.mc.GT_LmtSns(cardNumber, sValue);//20200111修改：固高的限位设置bug
#endif
            EncOff();//20200226新建：使用内部脉冲计数器

            //gts.mc.GT_EncSns(cardNumber,2);//20220512新建批注：固高的2轴外部编码器输入相反
        }

        //（四）关闭控制器
        //根据（a）轴号、(b)类型、（c）卡号没卵用——————选择初始化的轴类型
        public void CloseCard()
        {
            gts.mc.GT_Close(cardNumber);
        }

        public void ReadAxisSate(short AXIS)
        {
            //第一部分，此处实际上没有起到应有的作用——应该放到调用的地方
            int AxiStatus;//轴状态
            uint pClock;
            //out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            //ref和out调用的时候，也不能少
            short sRtn = mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
            LogMotionDebug($"ReadAxisSate: AXIS={AXIS}, sRtn={sRtn}, AxiStatus=0x{AxiStatus:X}, pClock={pClock}");
            //首先检测该轴状态字，检查是否存在报警和限位
            //Ox2:为10；0x20为100000。

            if ((AxiStatus & 0x2) != 0)//判断是否伺服驱动器报警
            {
                //设置变为标志flag
                this.axisSateMonitor.FlagServoAlarm = true;

                //bool FlagAlarm = true;
                //Commandhandler("伺服报警", sRtn);
            }
            else
            {
                //设置变为标志flag
                this.axisSateMonitor.FlagServoAlarm = false;
            }
            //else if ((AxiStatus & 0x20) != 0)//判断是否正向限位
            if ((AxiStatus & 0x20) != 0)//判断是否正向限位
            {
                //设置变为标志flag
                this.axisSateMonitor.FlagPosLimit1 = true;

                //axisSateMonitor1.FlagPosLimit1 = true;
                // Commandhandler("处于正限位", sRtn);
            }
            else
            {
                //设置变为标志flag
                this.axisSateMonitor.FlagPosLimit1 = false;
            }
            if ((AxiStatus & 0x40) != 0)//判断是否负向限位
            {
                //设置变为标志flag
                this.axisSateMonitor.FlagNegLimit1 = true;

                //bool FlagNegLimit = true;
                //Commandhandler("处于负限位", sRtn);
            }
            else
            {
                //设置变为标志flag
                this.axisSateMonitor.FlagNegLimit1 = false;
            }
            //return this.axisSateMonitor;
        }


        //点位运动：执行点位动作
        //正反方向点位运动
        public void TrapMotion(short AXIS, ref mc.TTrapPrm p_trap, int position, double vel, double RollerParam, int RollerDirection, bool WaitStopFlag)//注意：位置的类型为int，不是double；
        {
            short sRtn;//第二部分——本部分是重点（1）准备运动（a）清除各轴的报警和限位，必须的

            sRtn = mc.GT_ClrSts(cardNumber, AXIS, 8);
            LogMotionDebug($"TrapMotion: after GT_ClrSts, axis={AXIS}, sRtn={sRtn}");
            sRtn = mc.GT_AxisOn(0, AXIS);//（b）伺服使能，必须的(此使能非彼使能)，需要独立出来，硬件相关（过程中，不需要好反复的开启）
            LogMotionDebug($"TrapMotion: after GT_AxisOn, axis={AXIS}, sRtn={sRtn}");
#if false//20200515新建：此处应该关闭，不然，位置会不准确            
            sRtn = mc.GT_ZeroPos(0, AXIS, 1);//（c）实际位置清零，必须的———非必要
#endif
            sRtn = mc.GT_SetPrfPos(0, AXIS, 0); //（d）AXIS轴规划位置清零，必须的———非必要            
            LogMotionDebug($"TrapMotion: after GT_SetPrfPos, axis={AXIS}, sRtn={sRtn}");
            sRtn = mc.GT_PrfTrap(0, AXIS);//（1）设置运动模式// 将AXIS轴设为点位模式       
            LogMotionDebug($"TrapMotion: after GT_PrfTrap, axis={AXIS}, sRtn={sRtn}");
            sRtn = mc.GT_SetTrapPrm(0, AXIS, ref p_trap);//（2）设置参数并开始运动，必须的// 设置点位运动参数，必须的//trap，为引用（等同于返回值），使用之前必须初始化，否则报错。总          
            LogMotionDebug($"TrapMotion: after GT_SetTrapPrm, axis={AXIS}, sRtn={sRtn}");
            sRtn = mc.GT_SetPos(0, AXIS, position);// 设置AXIS轴的目标位置，必须的
            LogMotionDebug($"TrapMotion: after GT_SetPos, axis={AXIS}, sRtn={sRtn}, target={position}");
            sRtn = mc.GT_SetVel(0, AXIS, vel);// 设置AXIS轴的目标速度，必须的//20100111修正————已经移到调用接口中修改          
            LogMotionDebug($"TrapMotion: after GT_SetVel, axis={AXIS}, sRtn={sRtn}, vel={vel}");
            sRtn = gts.mc.GT_Update(0, 1 << (AXIS - 1));// 启动AXIS轴的运动，必须的//更新轴运动
            LogMotionDebug($"TrapMotion: after GT_Update, axis={AXIS}, sRtn={sRtn}");

            uint pClock; double pos; int AxiStatus;//轴状态//确保到位检测：//可能此处要添加读取规划位置进行检测——可以解决问题。
            sRtn = mc.GT_GetPrfPos(0, AXIS, out pos, 1, out pClock);
            mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
            LogMotionDebug($"TrapMotion: after start state readback, axis={AXIS}, sRtn={sRtn}, pos={pos}, AxiStatus=0x{AxiStatus:X}");
            if (WaitStopFlag == false)
            {
                DateTime waitHeartbeat = DateTime.MinValue;
                while ((Math.Abs(pos) < Math.Abs(position)) && ((AxiStatus & 0x20) == 0) && ((AxiStatus & 0x40) == 0))//20200220:调试时暂时去掉下方的循环检测（1）没到位置了（2）没到正限位了（3）没到负限，所有的均没发生，继续读取//或者是没有限位的时候
                {
                    sRtn = mc.GT_GetPrfPos(0, AXIS, out pos, 1, out pClock);
                    mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
                    DateTime now = DateTime.UtcNow;
                    if (waitHeartbeat == DateTime.MinValue || (now - waitHeartbeat) >= TimeSpan.FromSeconds(3))
                    {
                        LogMotionDebug($"TrapMotion: waiting, axis={AXIS}, sRtn={sRtn}, pos={pos}, AxiStatus=0x{AxiStatus:X}, target={position}");
                        waitHeartbeat = now;
                    }
                    if (sRtn != 0)
                    {
                        Commandhandler("读取规划位置", sRtn);//返回指令判断
                    }
                    Thread.Sleep(1);
                }
            }
            else
            { }
        }

        static double Pi = 3.14159265359;//精确到小数点后11位

#if false//20220512修改：
        private double[] Perimeter = new double[3] { 40 * Pi * 0.95, 20 * 5/*40 * Pi*/, 2 };//20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        private double[] SubDivideCoe = new double[3] { 800, 800, 800 };//20200916新增：3项步进电机细分设置参数
#else
        double[] Perimeter = new double[3] { 1, 5, 1 };//20220509新增：7轴的螺距修改为5mm //20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        double[] SubDivideCoe = new double[3] { 25600, 25000, 1600 };//20200916新增：3项步进电机细分设置参数
        double[] Perimeter2 = new double[3] { 1, 1, 1 };//20210505新增3个步进轴：特地用于3-4-5此3轴-1圈的周长//20200916新增：3项步进电机细分设置参数//20200916修正：铺粉辊电机功率偏小，运行不准确，40圈转38圈，修正系数为0.95
        double[] SubDivideCoe2 = new double[3] { 1600/*1600*/, 25600, 1600 };//20210505新增3个步进轴：特地用于3-4-5此3轴-具体的细分值//20200916新增//第3步进轴加有2：1的减速比
#endif


        //JOG运动：执行JOG动作
        //正反方向JOG运动
        public void JogMotion(short AXIS, ref mc.TJogPrm p_jog, double vel, double RollerParam)
        {
            short sRtn;//指令返回代码//回零之前，必要的保证工作://(1)先重新暂停一下所有的运动//（2）清楚所有的报警状态

            //（2）开启当前轴的JOG运动
            gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 1 << (AXIS - 1));//执行完，需要关闭、停止JOG运动
            sRtn = mc.GT_ClrSts(cardNumber, AXIS, 8);
            sRtn = mc.GT_AxisOn(cardNumber, AXIS);// 伺服使能           
            LogMotionDebug($"JogMotion: after GT_AxisOn, axis={AXIS}, sRtn={sRtn}");
            sRtn = mc.GT_PrfJog(cardNumber, AXIS);// 将AXIS轴设为Jog模式
            LogMotionDebug($"JogMotion: after GT_PrfJog, axis={AXIS}, sRtn={sRtn}");
            //sRtn = mc.GT_GetJogPrm(cardNumber, AXIS, out jog);// 读取Jog运动参数//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            //jog.acc = p_jog.acc;//单位，每毫秒1个脉冲的加速度
            //jog.dec = p_jog.dec;//单位，每毫秒1个脉冲的加速度
            //jog.smooth = 0;//单位，平滑系数，[0,1),越大，加减速过程越平滑————添加了           
            sRtn = mc.GT_SetJogPrm(cardNumber, AXIS, ref p_jog);//20220506批注：此值乘以1000为1s发出的实际脉冲数设置Jog运动参数//trap，为引用（等同于返回值），使用之前必须初始化，否则报错。总                      
            sRtn = mc.GT_SetVel(cardNumber, AXIS, vel);//20220506批注：此值乘以1000为1s发出的实际脉冲数设置Jog运动参数// 设置AXIS轴的目标速度//vel单位为pulse/ms//20200111：速度调整为原来的10分之一，电机是10mm / 1000脉冲；固高是1mm / 1000脉冲            
            LogMotionDebug($"JogMotion: after GT_SetVel, axis={AXIS}, sRtn={sRtn}, vel={vel}");
            sRtn = mc.GT_Update(cardNumber, 1 << (AXIS - 1));// 启动AXIS轴的运动
            LogMotionDebug($"JogMotion: after GT_Update, axis={AXIS}, sRtn={sRtn}");
        }

        public void StopMotion(short AXIS, bool bImmeStop = false)//平滑停止运动
        {
            //gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 0);//停止JOG运动——————使用的是平滑停止的方式——————此处需要修改
            //gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 1);//停止JOG运动——————使用的是紧急停止的方式——————此处需要修改
            gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 1 << (AXIS - 1));//停止JOG运动——————使用的是紧急停止的方式——————此处需要修改
        }

        public void StopMultiMotion(int Mask, int Option)//同时停止多轴运动：20200220添加——用于手动急停按钮
        {
            //gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 0);//停止JOG运动——————使用的是平滑停止的方式——————此处需要修改
            //gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 1);//停止JOG运动——————使用的是紧急停止的方式——————此处需要修改
            gts.mc.GT_Stop(cardNumber, Mask/*1 << (AXIS - 1)*/, Option /*1 << (AXIS - 1)*/);//停止JOG运动——————使用的是紧急停止的方式——————此处需要修改
        }

        //清除急停标志位和限位
        //急停操作：执行指定轴的急停动作
        //每个轴都可以设置平滑停止和急停，
        //但是，之后，必须调用GT_ClrSts指令清除标志停止位（bit7和bit8）
        //注意：：：：标志位：：bit7是平滑停止标志位（外部硬件输入信号）
        //————————————————bit8是急停标志位（外部硬件输入信号）
        //注意：：：：Googol的急停操作是指：googol的外部硬件接线信号！！！！！！
        //（1）清除限位操作：执行指定轴的急停动作——————待实现
        //（2）离开限位后——————再清除该轴的限位触发状态————才可以重新回到正常的运动状态（左右均可以自由运动的状态）
        public void ClrLimitAndAbrupt(short AXIS)
        //public static void ClrLimit(short cardNumber, short AXIS)
        {
            short sRtn;
            // （0）清除各轴的报警和限位——————这一行代码是最关键的
            // （1）清除各轴的报警和限位——————这一行代码是最关键的：JOG运动的时候，首先要判断是否是处于报警限位的状态
            // （2）还有考虑限位是否正常工作
            sRtn = mc.GT_ClrSts(cardNumber, AXIS, 8);

            //20200220:调试时暂时去掉下方的循环检测
            //Commandhandler("GT_ClrSts", sRtn);//返回指令判断
        }


        //线程标志位，放在调用程序中，比较合适
        public bool FlagGoHome = false;//false为：Gohome线程默认不工作//GoHome为独立工作线程
        //异常1：GoHome操作的时候，其他案件不能上升、下降和停止按钮不能操作，否则提示错误：GT_SetJogPrm错误指令为“1”！
        //异常2：GoHome操作的时候，正在GoHome过程中，电机上升或者下降，会提示错误：GT_SetJogPrm错误指令为“1”！
        //GOHOME操作：执行指定轴的回原点动作——————————————然后需要去好好实现————采取什么方式，判断标志位的方式
        public void GoHome(short AXIS, double vel, bool Ends, double RollerParam)//其中，Ends为true，回正端，Ends为false时，回负端。
        //public static void GoHome(short cardNumber, short AXIS)
        {
            int AxiStatus; uint pClock; short sRtn;//轴状态//第一部分，此处实际上没有起到应有的作用——应该放到调用的地方
            gts.mc.TJogPrm jog;
            jog.acc = 1000;//单位，每毫秒1个脉冲的加速度
            jog.dec = 1000;//单位，每毫秒1个脉冲的加速度
            jog.smooth = 0;//单位，平滑系数，[0,1),越大，加减速过程越平滑

            // 固高轴号（20260416）：1 墨车X 2 墨车Y 3 落粉 4 刮墨 5 粉辊2 6 粉辊1 7 铺粉车 8 成型缸
            // 成型缸(8)为伺服，老程序中对应轴1的 GoHome 方式：默认 acc/dec、速度不做 Perimeter 换算（与轴1、2 同分支）。
            // 落粉(3)、刮墨(4)、粉辊2(5)、粉辊1(6)：步进类，沿用原「轴6」周长/细分 Perimeter[0]/SubDivideCoe[0]。
            if (AXIS == 3 || AXIS == 4 || AXIS == 5 || AXIS == 6)
            {
                jog.acc = 0.5;
                jog.dec = 0.5;
                jog.smooth = 0;
                vel = (Convert.ToDouble(vel) / Perimeter[0]) * 1 * (SubDivideCoe[0] / 1000);
            }
            else if (AXIS == 7)//铺粉车：伺服
            {
                jog.acc = 1000;
                jog.dec = 1000;
                jog.smooth = 0;
                vel = (Convert.ToDouble(vel) / Perimeter[1]) * 1 * (SubDivideCoe[1] / 1000);
            }
            else//轴1、2（墨车 X/Y）、轴8（成型缸，伺服，老代码对应轴1）：保持方法开头默认 jog 参数，vel 不做 Perimeter 换算
            {
            }

            gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 1 << (AXIS - 1));//执行完，还是要关闭一下的：停止JOG运动//回零之前，必要的保证工作//(1)先重新暂停一下所有的运动（2）清楚所有的报警状态
            sRtn = mc.GT_ClrSts(cardNumber, AXIS, 8);
            mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);////首先检测该轴状态字，检查是否存在报警和限位                    
            sRtn = mc.GT_AxisOn(cardNumber, AXIS);// 伺服使能（1）清除各轴的报警和限位——————这一行代码是最关键的：JOG运动的时候，首先要判断是否是处于报警限位的状态；（2）还有考虑限位是否正常工作
            LogMotionDebug($"JogMotion: after GT_AxisOn, axis={AXIS}, sRtn={sRtn}");
            sRtn = mc.GT_PrfJog(cardNumber, AXIS);// 将AXIS轴设为Jog模式            
            LogMotionDebug($"JogMotion: after GT_PrfJog, axis={AXIS}, sRtn={sRtn}");
            sRtn = mc.GT_SetJogPrm(cardNumber, AXIS, ref jog);//设置Jog运动参数//trap，为引用（等同于返回值），使用之前必须初始化，否则报错。//只有在JOG运动停止的情况下才能再次调用GT_SetJogPrm                                                                                                             

            if (Ends == true)//返回正端//设置AXIS轴的目标速度
            {
                sRtn = mc.GT_SetVel(cardNumber, AXIS, vel);//20100111修正               
            LogMotionDebug($"JogMotion: after GT_SetVel, axis={AXIS}, sRtn={sRtn}, vel={vel}");
                sRtn = mc.GT_Update(cardNumber, 1 << (AXIS - 1));// 启动AXIS轴的运动
            LogMotionDebug($"JogMotion: after GT_Update, axis={AXIS}, sRtn={sRtn}");

                while ((AxiStatus & 0x20) == 0)//20200220:调试时暂时去掉下方的循环检测//没有碰到正限位时，一直JOG运动到原点，选择默认速度为35
                {
                    sRtn = mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);//20200110:先注释掉//一直保持GT_UPDATE是正常的状态
                    Commandhandler("GT_GetSts", sRtn);//返回指令判断20200110:先注释掉                  
                    //Thread.Sleep(1);//20200110:延时1ms测试//添加延时1ms————定时1ms检查一次正限位状态，//释放主任的占有权限
                }
            }
            else//返回负端
            {
                sRtn = mc.GT_SetVel(cardNumber, AXIS, -vel);//————————————-正常是没有问题的//20100111修正               
                sRtn = mc.GT_Update(cardNumber, 1 << (AXIS - 1));// 启动AXIS轴的运动
            LogMotionDebug($"JogMotion: after GT_Update, axis={AXIS}, sRtn={sRtn}");

                while ((AxiStatus & 0x40) == 0)//没有碰到负限位限位时，一直JOG运动到原点，选择默认速度为35//20200220:调试时暂时去掉下方的循环检测
                {
                    sRtn = mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);//一直保持GT_UPDATE是正常的状态
                    Commandhandler("GT_GetSts", sRtn);//返回指令判断                   
                    //Thread.Sleep(1);//20200110:延时1ms测试//添加延时1ms————定时1ms检查一次正限位状态，//释放主任的占有权限
                }
            }

            gts.mc.GT_Stop(cardNumber, 1 << (AXIS - 1), 1 << (AXIS - 1));//执行完，还是要关闭一下的：停止JOG运动
            //Thread.CurrentThread.Abort(); //20200111关闭当前线程：避免gohome线程只执行一次BUG
        }
        /*************************************************轴状态监控API***************************************************/
        //20200221新增：（1）编码位置（2）编码速度（3）设置编码位置：总计读取8轴的运动数据
        public double[] GetEncPos(/*out double[] encpos*/)//20200221新建:读取8轴的编码位置
        {
            uint clk;//20200221:读取时钟————————————没卵用
            double[] encpos = new double[8];
            gts.mc.GT_GetEncPos(cardNumber, 1, out encpos[0], 8, out clk);
            encpos[1] = -encpos[1];//20220512修改：修复第2轴的编码器方向与轴运动方向方向问题
            return encpos;
        }
        public void GetAxisStatus(short AXIS,out int AxiStatus)//20220512新建：获取各轴的状态：正负限位是否发生信息等
        {
            uint pClock = 0;
            short sRtn = gts.mc.GT_GetSts(0, AXIS, out AxiStatus, 1, out pClock);
            if (sRtn != 0) { Commandhandler("读取规划位置", sRtn);/*返回指令判断*/}
        }
        public void GetPrfPos(short AXIS, out double prfPos)//20220512新建：获取指定轴的规划器位置：正负限位是否发生信息等
        {
            uint pClock = 0;
            short sRtn = gts.mc.GT_GetPrfPos(0, AXIS, out prfPos, 1, out pClock);

            if (sRtn != 0) { Commandhandler("读取规划位置", sRtn);/*返回指令判断*/}
        }

        public double[] GetEncVel(/*out double encpos*/)//20200221新建:读取8轴的编码器速度
        {
            uint clk;//20200221:读取时钟————————————没卵用
            double[] encvel = new double[8];
            gts.mc.GT_GetEncPos(cardNumber, 1, out encvel[0], 8, out clk);
            return encvel;
        }    
        public void SetEncPos(short encoder, int encpoc)//20200221新建:复位初始化，设置8轴的编码位置
        {
            gts.mc.GT_SetEncPos(cardNumber, encoder, encpoc);//设置单轴的编码器位置
        }
        public void EncOff()//设置为脉冲计数器形式//20200226新建：
        {
            for (short i=1;i<=8;i++)//20200226新建：
            {
                gts.mc.GT_EncOff(cardNumber, i);//encoder计数值为正整数
            }
        }
        public void EncOn()//设置为脉冲计数器形式//20200226新建：
        {
            for (short i = 1; i <= 8; i++)//20200226新建：
            {
                gts.mc.GT_EncOn(cardNumber, i);//encoder计数值为正整数
            }
        }
        /*************************************************轴状态监控API***************************************************/
        //20200309新增：读取通用输入EXI状态,可以一次性读取所有的EXI位(范围为4路)
        public int GetDi(short DoNumber)
        {
            int DiValue = 0;
            short sRtn = mc.GT_GetDi(cardNumber, 4, out DiValue);//(1)读取EXI的输入值，端口默认信号为低电平，有效电平为高电平//(2)4为通用输入EXI
            if (sRtn != 0) { }
            return DiValue;
        }
        //20200309新增：读取通用模拟输入AI状态，可以一次性读取所有的AI位(范围为-10V~10V)
        public double[] GetAi()//20200417新建批注：可以用于读取气压表的实时的值，不过需要执行一定的换算过程
        {
            double[] AiVoltageValue = new double[4]; //3+32+0-1=34==》模拟量位置//20200309新增：
            uint pclock = 0;
            //I think that using GetADC is much better than the using of GetADCValue
            //4路足够了
            short sRtn = mc.GT_GetAdc(cardNumber, 1, out AiVoltageValue[0], /*8*/4, out pclock);//读取的模拟量，输出范围为-10V~10V的double型数据（双浮点数）
            if (sRtn != 0) { }
            //short[] AiDigitalValue = new short[4];//20200309:使用的频率不高，及时读回数字值，也要转换为等效的电压值           
            //20200309:使用的频率不高，及时读回数字值，也要转换为等效的电压值
            //sRtn = mc.GT_GetAdcValue(cardNumber, 1, out AiDigitalValue[0], 4, out pclock);//读取的数字量，输出范围为-26214~26214的short型数据（16位）
            //if (sRtn != 0) { }
            //return AiDigitalValue;
            return AiVoltageValue;
        }
        /*************************************************轴状态监控API***************************************************/


        //刷新轴运动参数：
        //刷新状态：刷新指定轴的运动状态——————位置、速度、加速度
        //调用位置：本方法在timer中调用，刷新间隔500ms
        public void MonitoringMotion(short AXIS)
        //public static void MotionProf(short cardNumber, short AXIS)
        {
            short sRtn; // 指令返回值变量
            uint clk;//读取时钟————————————没卵用
            AxisMotionMonitor axisParameter;// 读取运动数据————存放到结构体
            
            //gts.mc.GT_GetPrfPos(cardNumber, AXIS, out prfpos, 1, out clk);
            //this.textBox6.Text = Math.Round(prfpos, 1).ToString();//显示到状态栏框里面
            //gts.mc.GT_GetPrfVel(cardNumber, AXIS, out prfvel, 1, out clk);
            //this.textBox7.Text = Math.Round(prfvel, 1).ToString();//显示到状态栏框里面
            //gts.mc.GT_GetEncPos(cardNumber, AXIS, out encpos, 1, out clk);
            //this.textBox8.Text = Math.Round(encpos, 1).ToString();//显示到状态栏框里面
            //gts.mc.GT_GetEncVel(cardNumber, AXIS, out encvel, 1, out clk);
            //this.textBox9.Text = Math.Round(encvel, 1).ToString();//显示到状态栏框里面
            gts.mc.GT_GetPrfPos(cardNumber, AXIS, out axisParameter.prfpos, 1, out clk);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            gts.mc.GT_GetPrfVel(cardNumber, AXIS, out axisParameter.prfvel, 1, out clk);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            gts.mc.GT_GetPrfAcc(cardNumber, AXIS, out axisParameter.prfacc, 1, out clk);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。          

            //gts.mc.GT_GetEncPos(cardNumber, AXIS, out axisParameter.encpos, 1, out clk);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            //gts.mc.GT_GetEncVel(cardNumber, AXIS, out axisParameter.encvel, 1, out clk);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            // 读取运动模式：
            // 本行代码需要修改：
            sRtn = mc.GT_GetPrfMode(cardNumber, AXIS, out axisParameter.MotionMode, 1, out clk);//clk没有多大卵用//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            Commandhandler("GT_GetPrfMode", sRtn);//返回指令判断

            //交互显示部分：必要在本文件中实现，放在控件类背后实现
            //axis = Convert.ToInt16(this.comboBox1.SelectedIndex + 1);
            //if (!en[Convert.ToInt32(this.comboBox1.Text) - 1])
            ////if (axis !=0)
            //{
            //    this.button5.Text = "伺服使能";
            //}
            ////if (axis != 0)
            //if (en[Convert.ToInt32(this.comboBox1.Text) - 1])
            //{
            //    this.button5.Text = "伺服关闭";
            //}
            //this.textBox6.Text = Math.Round(prfpos, 1).ToString();//显示到状态栏框里面
            //this.textBox7.Text = Math.Round(prfvel, 1).ToString();//显示到状态栏框里面
            //this.textBox8.Text = Math.Round(encpos, 1).ToString();//显示到状态栏框里面
            //this.textBox9.Text = Math.Round(encvel, 1).ToString();//显示到状态栏框里面
        }

        //刷新轴状态字
        //刷新轴状态字：刷新指定轴的报警状态——————报警、使能、急停等状态
        //调用位置：本方法在timer中调用，刷新间隔500ms
        //处理流程：（1）根据GT_GetSts获得指定轴的轴状态字
        //————————（2）提取出对应的状态信息
        //————————————————（3）发送给状态显示栏
        public int MointoringAxis2(short AXIS)
        {
            int lAxisStatus;//轴状态变量:32bit代码.20200110
            uint pClock;//读取时钟.20200110
            short State= mc.GT_GetSts(cardNumber, AXIS, out lAxisStatus, 1, out pClock);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            return lAxisStatus;
        }
        public void MointoringAxis(short AXIS)
        //public static void MointoringAxis(short cardNumber, short AXIS)
        {
            short sRtn; // 指令返回值变量
            //int sts;                        //轴状态变量————32bit代码
            int lAxisStatus;                //轴状态变量————32bit代码
            uint pClock;                    //读取时钟
            AxisSateMonitor axiCondition;        //将下面的8个标志位封装到本结构体。

            /****************作为临时变量——判断指令执行是否正确*****************/
            short bFlagAlarm = 0;           // 伺服报警标志
            short bFlagMError = 0;          // 跟随误差越限标志
            short bFlagPosLimit = 0;        // 正限位触发标志
            short bFlagNegLimit = 0;        // 负限位触发标志
            short bFlagSmoothStop = 0;      // 平滑停止标志
            short bFlagAbruptStop = 0;      // 急停标志
            short bFlagServoOn = 0;         // 伺服使能标志
            short bFlagMotion = 0;          // 规划器运动标志                  
            /****************作为临时变量——判断指令执行是否正确*****************/

            // 读取轴状态
            sRtn = mc.GT_GetSts(cardNumber, AXIS, out lAxisStatus, 1, out pClock);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            Commandhandler("GT_GetSts", sRtn);//返回指令判断
            // 伺服报警标志
            if ((lAxisStatus & 0x2) == 2)//
            {
                axiCondition.FlagServoAlarm = true;
                bFlagAlarm = 1;
                Commandhandler("伺服报警\n", bFlagAlarm);//返回指令判断
            }
            else
            {
                bFlagAlarm = 0;
                axiCondition.FlagServoAlarm = false;
                Commandhandler("伺服正常\n", bFlagAlarm);//返回指令判断
            }
            // 跟随误差越限标志
            if ((lAxisStatus & 0x10) == 0x10)
            {
                bFlagMError = 1;
                axiCondition.FlagError = true;
                Commandhandler("运动出错\n", bFlagMError);//返回指令判断
            }
            else
            {
                axiCondition.FlagError = false;
                bFlagMError = 0;
                Commandhandler("运动正常\n", bFlagMError);//返回指令判断
            }
            // 正向限位
            if ((lAxisStatus & 0x20) == 0x20)
            {
                axiCondition.FlagPosLimit1 = true;
                bFlagPosLimit = 1;
                Commandhandler("正限位触发\n", bFlagPosLimit);//返回指令判断
            }
            else
            {
                axiCondition.FlagPosLimit1 = false;
                bFlagPosLimit = 0;
                Commandhandler("正限位未触发\n", bFlagPosLimit);//返回指令判断
            }
            // 负向限位
            if ((lAxisStatus & 0x40) == 0x40)
            {
                axiCondition.FlagNegLimit1 = true;
                bFlagNegLimit = 1;
                Commandhandler("负限位触发\n", 1);//返回指令判断
            }
            else
            {
                axiCondition.FlagNegLimit1 = false;
                bFlagNegLimit = 0;
                Commandhandler("负限位未触发\n", 1);//返回指令判断
            }
            // 平滑停止
            if ((lAxisStatus & 0x80) == 0x80)
            {
                axiCondition.smoothStop = true;
                bFlagSmoothStop = 1;
                Commandhandler("平滑停止触发\n", 1);//返回指令判断
            }
            else
            {
                axiCondition.smoothStop = false;
                bFlagSmoothStop = 0;
                Commandhandler("平滑停止未触发\n", 0);//返回指令判断
            }
            // 急停标志
            if ((lAxisStatus & 0x100) == 0x100)
            {
                axiCondition.abruptStop = true;
                bFlagAbruptStop = 1;
                Commandhandler("急停触发\n", 1);//返回指令判断
            }
            else
            {
                axiCondition.abruptStop = false;
                bFlagAbruptStop = 0;
                Commandhandler("急停未触发\n", 0);//返回指令判断
            }
            // 伺服使能标志
            if ((lAxisStatus & 0x200) == 0x200)
            {
                axiCondition.ServoOn = true;
                bFlagServoOn = 1;
                Commandhandler("伺服使能\n", 1);//返回指令判断
            }
            else
            {
                axiCondition.ServoOn = false;
                bFlagServoOn = 0;
                Commandhandler("伺服关闭\n", 0);//返回指令判断
            }
            // 规划器正在运动标志
            if ((lAxisStatus & 0x400) == 0x400)
            {
                axiCondition.profileMotion = true;
                bFlagMotion = 1;
                Commandhandler("规划器正在运动\n", 1);//返回指令判断
            }
            else
            {
                axiCondition.profileMotion = false;
                bFlagMotion = 0;
                Commandhandler("规划器已停止\n", 0);//返回指令判断
            }
        }

        public void ReadDAC(short sartAdc, short nCount)//暂时不需要
        //public static void readDAC(short sartAdc, short card, short nCount)
        {
            short sRtn;
            // 电压值
            double dGetVoltageValue;
            // 数字转换值
            short sGetDigitalValue;
            // 读取4个通道的输入电压
            uint pClock;
            sRtn = mc.GT_GetAdc(cardNumber, sartAdc, out dGetVoltageValue, nCount, out pClock);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
            // 读取4个通道输入电压的数字转换值
            sRtn = mc.GT_GetAdcValue(cardNumber, sartAdc, out sGetDigitalValue, nCount, out pClock);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
        }

        ////Gohome：执行回原点动作
        //public static void GoHome(short axi, short card)
        //{
        //    mc.TJogPrm jopPra = new mc.TJogPrm();
        //    jopPra.acc = 0.25;
        //    jopPra.dec = 0.25;
        //    double value = 20;
        //    AxiCondition.AxiSituation axiSituation = new AxiCondition.AxiSituation();
        //    JogMotin(0, 1, jopPra, -value);
        //    do
        //    { axiSituation = AxiCondition.JustReadCondition(axi, card); }
        //    while (axiSituation.negativeLimit == false);

        //    mc.TTrapPrm p_trap = new mc.TTrapPrm();
        //    p_trap.acc = 0.25;
        //    p_trap.dec = 0.25;
        //    p_trap.smoothTime = 25;
        //    p_trap.velStart = 1;

        //    TrapParameter setTrapParameter = new TrapParameter();
        //    setTrapParameter.position = 10000;
        //    setTrapParameter.sleep = 20;
        //    Trap(true, p_trap, setTrapParameter, axi);
        //}

        ////GoSafetyPosition：执行回安全位置动作
        //public static void GoSafetyPosition(short axi, short card)
        //{
        //    AxiCondition.AxiSituation axiSituation = new AxiCondition.AxiSituation();
        //    axiSituation = AxiCondition.readCondition(axi, card);
        //    if (axiSituation.negativeLimit == true)
        //    {
        //        mc.TTrapPrm p_trap = new mc.TTrapPrm();
        //        p_trap.acc = 0.25;
        //        p_trap.dec = 0.25;
        //        p_trap.smoothTime = 25;
        //        TrapParameter setTrapParameter = new TrapParameter();
        //        setTrapParameter.position = 5000;
        //        setTrapParameter.sleep = 20;
        //        Trap(true, p_trap, setTrapParameter, axi);
        //    }
        //    else if (axiSituation.positveLimit == true)
        //    {
        //        mc.TTrapPrm p_trap = new mc.TTrapPrm();
        //        p_trap.acc = 0.25;
        //        p_trap.dec = 0.25;
        //        p_trap.smoothTime = 25;
        //        TrapParameter setTrapParameter = new TrapParameter();
        //        setTrapParameter.position = -5000;
        //        setTrapParameter.sleep = 20;
        //        Trap(true, p_trap, setTrapParameter, axi);
        //    }
        //}

        //public void ReadAxiDAC(short cardNumber, short axi)//暂时不需要
        ////public static void readAxiDAC(short axi, short card)
        //{
        //    short sRtn;
        //    //电压值
        //    short sSetValue;
        //    short sGetValue;
        //    uint pClock;
        //    //控制器复位，所有轴都为脉冲
        //    //计算轴4电压输出值
        //    sSetValue = (short)32767 * 5 / 10;
        //    //设置轴4的输出电压
        //    sRtn = mc.GT_SetDac(cardNumber, axi, ref sSetValue, 1);//trap，为引用（等同于返回值），使用之前必须初始化，否则报错。总
        //    //读取轴的输出电压
        //    sRtn = mc.GT_GetDac(cardNumber, axi, out sGetValue, 1, out pClock);//out为引用（等于1个返回值），使用之前必须不能初始化（不允许初始化）。
        //}
    }
}
