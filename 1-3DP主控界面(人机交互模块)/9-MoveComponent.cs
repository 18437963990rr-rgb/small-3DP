using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Motion;//导入GoogolMotionMap引用包
using System.Threading;
using System.ComponentModel;
using System.Drawing;
using gts;

namespace LaserADD_BinderJetter
{
    /****************************************回原点参数类****************************************/
    public class PowderLayerParam
    {
        public double[] m_Svel = { 20.0, 20.0, 20.0, 80.0 }/*new double[4]*/;//20mm/s//铺粉车速度设置为50mm/s:20200328修改，调试中，速度太慢
        public double[] m_Step = { 150, 450, 450, 835/* 0.2, 0.6, 0.6, 835*//*820*//*800*//*0.5*/ } /*new double[4]*/;//20200618批注;四周运动全部为800//20200627批注：4周运动设置为835MM
        public bool[] m_bEnds = { false, false, false, false }/*new bool[4]*/;
        public bool[] m_bMoveModeFlag = { true, true, true, true }/*new bool[4]*/;//20200618批注;四周运动全部为点动
    
        [DefaultValue(20.0), Category("轴1：成形缸/μm")]
        public double 速度1
        {
            get { return m_Svel[0]; }
            set { m_Svel[0] = value; }
        }
        [DefaultValue(0.03), Category("轴1：成形缸/μm")]
        public double 层厚1
        {
            get { return m_Step[0]; }
            set { m_Step[0] = value; }
        }
        [DefaultValue(true), Category("轴1：成形缸/μm")]
        public bool 正负端
        {
            get { return m_bEnds[0]; }
            set { m_bEnds[0] = value; }
        }
        [DefaultValue(true), Category("轴1：成形缸/μm")]
        public bool 运动模式
        {
            get { return m_bMoveModeFlag[0]; }
            set { m_bMoveModeFlag[0] = value; }
        }


        [DefaultValue(20.0), Category("轴2：送粉缸1/μm")]
        public double 速度2
        {
            get { return m_Svel[1]; }
            set { m_Svel[1] = value; }
        }
        [DefaultValue(0.03), Category("轴2：送粉缸1/μm")]
        public double 供粉量1
        {
            get { return m_Step[1]; }
            set { m_Step[1] = value; }
        }
        [DefaultValue(true), Category("轴2：送粉缸1/μm")]
        public bool 正负端2
        {
            get { return m_bEnds[1]; }
            set { m_bEnds[1] = value; }
        }
        [DefaultValue(true), Category("轴2：送粉缸1/μm")]
        public bool 运动模式2
        {
            get { return m_bMoveModeFlag[1]; }
            set { m_bMoveModeFlag[1] = value; }
        }

        [DefaultValue(20.0), Category("轴3：送粉缸2/μm")]
        public double 速度3
        {
            get { return m_Svel[2]; }
            set { m_Svel[2] = value; }
        }
        [DefaultValue(0.03), Category("轴3：送粉缸2/μm")]
        public double 供粉量2
        {
            get { return m_Step[2]; }
            set { m_Step[2] = value; }
        }
        [DefaultValue(true), Category("轴3：送粉缸2/μm")]
        public bool 正负端3
        {
            get { return m_bEnds[2]; }
            set { m_bEnds[2] = value; }
        }
        [DefaultValue(true), Category("轴3：送粉缸2/μm")]
        public bool 运动模式3
        {
            get { return m_bMoveModeFlag[2]; }
            set { m_bMoveModeFlag[2] = value; }
        }

        [DefaultValue(20.0), Category("轴4：铺粉车/mm")]
        public double 速度4
        {
            get { return m_Svel[3]; }
            set { m_Svel[3] = value; }
        }
        [DefaultValue(0.03), Category("轴4：铺粉车/mm")]
        public double 平移量
        {
            get { return m_Step[3]; }
            set { m_Step[3] = value; }
        }
        [DefaultValue(true), Category("轴4：铺粉车/mm")]
        public bool 正负端4
        {
            get { return m_bEnds[3]; }
            set { m_bEnds[3] = value; }
        }
        [DefaultValue(false), Category("轴4：铺粉车/mm")]
        public bool 运动模式4
        {
            get { return m_bMoveModeFlag[3]; }
            set { m_bMoveModeFlag[3] = value; }
        }
    }

    public class GohomeParam
    {
        public short AXIS;//对应的轴号
        public double vel;//JogGoHome的速度
        //public bool FlagGoHome;//正在GoHome标志位
        public bool Ends;//终止端
    }

    class MoveComponent
    {
        /***********************************************成型缸********************************************************/
        /***********************************************成型缸********************************************************/
        /***********************************************成型缸********************************************************/
        //(1)运动模式动态挂载切换响应：
        GoogolMotionMap motionMap;

        public MoveComponent()
        {
            motionMap = CreateMotionMap();
        }

        private GoogolMotionMap CreateMotionMap()
        {
            var map = new GoogolMotionMap();
            map.SetLogSink(msg => BinderJetting.Log4Net.Info(msg), msg => BinderJetting.Log4Net.Error(msg));
            return map;
        }
        ///// 互斥配置多轴的点动和JOG运动配置：动态挂载初始化：20200110
        //private void InitDynamicConfigureMotionMode()
        //{}

        /// <summary>
        /// 4大标志位
        /// </summary>
        //bool[] m_bMoveModeFlag = new bool[6];//6轴的运动模式切换标志位————应该移动到royalMap中：20200109
        //double[] m_dVel = new double[6];//6轴的运动速度变量（含JOG和点动）
        //double[] m_dStep = new double[6];//6轴的点动运动移动量   
        //bool[] m_bEnds = new bool[6];//202001410：6个轴的回原点位置端设置.//回原点端头标志位
        bool[] FlagGoHome = new bool[6]/*{false,false,false,false,false,false}*/;//20200110因为ref修饰//目前用不到，先暂时留着吧
        GohomeParam[] gohomeParam = new GohomeParam[6];//回零标志位

        //20200203：写入运动参数——应该是运动的加速度等等
        public void InitMovParam(PowderLayerParam m_cPowderLayerParam)//从主界面读取数据到3大标志位中
        {
            //（1）以下做法不太合适：运动的参数应该为DEC/ACC/SmoothTime/等等
            //m_bMoveModeFlag[0] = m_cPowderLayerParam.运动模式;
            //m_dVel[0] = m_cPowderLayerParam.速度1;
            //m_dStep[0] = m_cPowderLayerParam.层厚1;
            //m_bEnds[0] = m_cPowderLayerParam.正负端;

            //m_bMoveModeFlag[1] = m_cPowderLayerParam.运动模式2;
            //m_dVel[1] = m_cPowderLayerParam.速度2;
            //m_dStep[1] = m_cPowderLayerParam.供粉量1;
            //m_bEnds[1] = m_cPowderLayerParam.正负端2;

            //m_bMoveModeFlag[2] = m_cPowderLayerParam.运动模式3;
            //m_dVel[2] = m_cPowderLayerParam.速度3;
            //m_dStep[2] = m_cPowderLayerParam.供粉量2;
            //m_bEnds[2] = m_cPowderLayerParam.正负端3;

            //m_bMoveModeFlag[3] = m_cPowderLayerParam.运动模式4;
            //m_dVel[3] = m_cPowderLayerParam.速度4;
            //m_dStep[3] = m_cPowderLayerParam.平移量;
            //m_bEnds[3] = m_cPowderLayerParam.正负端4;
        }

        //(2)JOG开始事件-上升：
        //关于此上升按键，点动和JOG运动切换是正常的
        //private void JogMoveUp(object sender, MouseEventArgs e)//当鼠标按下时，先判断当前运动模式，再决定是否执行指定的JOG动作
        public void JogMoveUp(short AXIS, bool m_bMoveModeFlag, double m_dVel, double RollerParam)//
        {
            if (m_bMoveModeFlag == false)//进行了动态处理之后，此即为双保险：20200110
            {
                motionMap.StopMotion(AXIS);//停止JOG运动
                //执行JOG运动
                motionMap.jogPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                motionMap.jogPrm.dec = 1000;
                motionMap.jogPrm.smooth = 0;
                //double vel = Convert.ToDouble(this.velLabel1.Text);//千脉冲对应的mm数，这个要尽可能的统一   
                double vel = Convert.ToDouble(m_dVel);
                motionMap.JogMotion(AXIS, ref motionMap.jogPrm, vel, RollerParam);
            }
            else if (m_bMoveModeFlag == true) {/*不执行任何操作*/}//进行了动态处理之后，此即为双保险：20200110
        }
        //(2)JOG开始事件-下降：
        //关于此下降按键，点动和JOG运动切换是不正常的：JOG运动正常，点动是不正常
        //private void JogMoveDown(object sender, MouseEventArgs e)//当鼠标按下时，先判断当前运动模式，再决定是否执行指定的JOG动作
        public void JogMoveDown(short AXIS, bool m_bMoveModeFlag, double m_dVel, double RollerParam)
        {
            if (m_bMoveModeFlag == false)
            {
                motionMap.StopMotion(AXIS);//停止JOG运动
                //执行JOG运动
                //驱动器的默认坐标系是：千脉冲10mm:20200111
                motionMap.jogPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                motionMap.jogPrm.dec = 1000;
                motionMap.jogPrm.smooth = 0;//0是一个好的参数，提高了反应速度
                //double vel = Convert.ToDouble(this.velLabel1.Text);//千脉冲对应的mm数，这个要尽可能的统一                   
                double vel = Convert.ToDouble(m_dVel);
                motionMap.JogMotion(AXIS, ref motionMap.jogPrm, -vel, RollerParam);//这一行代码应该没有问题
                //motionMap.JogMotion(1, ref motionMap.jogPrm, vel);
            }
            else if (m_bMoveModeFlag == true) {/*不执行任何操作*/}
        }

        //(3)JOG结束事件-上升/下降：
        //private void JogMoveStop(object sender, MouseEventArgs e)//当鼠标弹起时，先判断当前运动模式，再决定是否执行指定的JOG动作
        public void JogMoveStop(short AXIS, bool m_bMoveModeFlag)
        {
            if (m_bMoveModeFlag == false)
            {
                motionMap.StopMotion(AXIS);//停止JOG运动
            }
            else if (m_bMoveModeFlag == true) {/*不执行任何操作*/}
        }

        //(4)回零点动作：20200120不需要线程
        public void MoveHome(short AXIS, double vel,/* ref bool FlagGoHome,*/ bool Ends, double RollerParam)//ref bool FlagGoHome:已经于20200220删除
        {
            GohomeParam gohomeParam = new GohomeParam();//20200110:放到这里主要方便下面的多线程直接调用
            gohomeParam.AXIS = AXIS;
            gohomeParam.vel = vel;//20200111修正:回零点的速度，调整为原来的10分之1
            gohomeParam.Ends = Ends;
            motionMap.GoHome(gohomeParam.AXIS, gohomeParam.vel, gohomeParam.Ends, RollerParam);//返回到原点
        }

        //20200221新增：（1）编码位置（2）编码速度（3）设置编码位置：总计读取8轴的运动数据
        public double[] GetEncPos()//20200221新建:读取8轴的编码位置
        {
            uint clk;//20200221:读取时钟————————————没卵用
            double[] encpos = new double[8];
            gts.mc.GT_GetEncPos(0, 1, out encpos[0], 8, out clk);
            return encpos;
        }
        //(4)停止运动事件：
        public void StopMove(/*short AXIS*/)//同时停止多轴的操作，供手动控制多轴停止：20200220停止
        {
            int Mask = 0b11111111;
            int Option = 0b11111111;
            motionMap.StopMultiMotion(Mask, Option);//20200220新增：
            //motionMap.StopMotion(AXIS);//20200220删减：
        }

        ////(4)回零点事件：
        ////(0）初始化回零点线程参数：
        ////GohomeParam[] gohomeParam = new GohomeParam[6];
        ////bool[] Ends = new bool[6];//202001410：6个轴的回原点位置端设置
        //private List<Thread> GoHomeThreads = new List<Thread>();
        //private void GohomeWork(object Param)
        //{
        //    GohomeParam gohomeParam = Param as GohomeParam;//类型转换
        //    //motionMap.GoHome(1, 20, true);//返回到原点
        //    motionMap.GoHome(gohomeParam.AXIS, gohomeParam.vel, gohomeParam.Ends);//返回到原点
        //}
        ////异常1：回原点有短暂的停顿现象，类似于有时候，按一下会先点动，再按一下才会回原点
        ////异常2：回零键连按2次，会JOG参数报错。
        ////private void MoveHome(object sender, EventArgs e)//不管当前的运动方式是什么，开启JOG运动模式，返回到原点
        //public void MoveHome(short AXIS, double vel, ref bool FlagGoHome, bool Ends)//JOG回原点.20200110:只负责1轴的回零点
        //{
        //    GohomeParam tempgohomeParam = new GohomeParam();//20200110:放到这里主要方便下面的多线程直接调用
        //    tempgohomeParam.AXIS = AXIS;
        //    tempgohomeParam.vel = vel;//20200111修正:回零点的速度，调整为原来的10分之1
        //    //tempgohomeParam.FlagGoHome = FlagGoHome;//没有必要放在对应的结构体中
        //    tempgohomeParam.Ends = Ends;
        //    gohomeParam[AXIS - 1] = tempgohomeParam;
        //    //(1)避免.重复回原点线程：           
        //    //if (FlagGoHome/*motionMap.FlagGoHome*/ == true)//首先关闭线程——20200110：避免重复回原点
        //    //{
        //    //    GohomeThread.Abort();
        //    //    FlagGoHome = false;
        //    //}
        //    string tempThreadName = "GoHomeThred" + AXIS.ToString();
        //    Thread tempThread = GoHomeThreads.Where(x => x.Name == (tempThreadName)).FirstOrDefault();
        //    if (/*tempThread != null*/tempThread != null/*(tempThread.IsAlive==true)*/)
        //    {
        //        GoHomeThreads.Remove(tempThread);
        //    }
        //    else
        //    {
        //        //(2a)回原点线程:20200110
        //        //motionMap.GoHome(1);
        //        //(1)不带参数的多线程方式实现：
        //        //ThreadStart entry = new ThreadStart(RunThread);/
        //        //GohomeThread = new Thread(entry) { IsBackground = true };
        //        //GohomeThread.Start();
        //        //(2b)带参数的多线程方式实现：
        //        tempThread/*Thread GohomeThread*/ = new Thread(new ParameterizedThreadStart(GohomeWork)) { IsBackground = true };//(1)第1部曲：多线程3步曲
        //        tempThread.Name = "GoHomeThred" + AXIS.ToString();//(2)第2部曲：多线程3步曲——20200110线程ID和线程名称
        //        tempThread.Start(gohomeParam[AXIS - 1]);//(3)第3部曲：多线程3步曲 
        //        FlagGoHome = true;//——————————————————————————标志位在调用出使用才可以
        //        GoHomeThreads.Add(tempThread);//没有创建过的时候，才重新添加新的线程
        //    }
        //    ////(3)相关按钮使能设置：
        //    ///
        //}
        ////(4)停止运动事件：
        ////异常1：GOHOME过程中，点击停止————再点击上升下降，就没有问题；如果直接点击上升和下降则报警
        ////异常2：如果连击两次GOHOME，——————则报警
        ////异常3：只要触发过1此正限位后，——————上升的点动运动————就不反馈
        ////异常4：初始工作过程中，点动和JOG运动切换紊乱
        ////明天需要紧急处理此三个BUG
        ////private void MoveStop(object sender, EventArgs e)//不管当前的运动方式是什么，统一停止下来
        //public void MoveStop(short AXIS, ref bool FlagGoHome)
        //{
        //    string tempThreadName = "GoHomeThred" + AXIS.ToString();
        //    Thread tempThread = GoHomeThreads.Where(x => x.Name == tempThreadName).FirstOrDefault();
        //    if (tempThread != null)
        //    {
        //        //tempThread.Abort();
        //        GoHomeThreads.Remove(tempThread);//20200111添加：解决Gohome无法重新执行的BUG
        //        FlagGoHome = false;
        //    }
        //}

        //(5)点动上升：
        /*************************(a)上升按键*****************************/
        //异常1：关于此上升按键，点动和JOG运动切换是正常的
        //异常2：点动表现出来：限位之后，按反方向点动没有反应，需要重新点击切换按键进行切换才可以————原因是：回到原点后motionMap.ReadAxisSate(1)没有及时读取到值
        //异常3：点位运动设置的步长太长之后，连续电机两次，会出现位置清零错误
        //bool OneClickflag = true;EquipMoveBtn_Click
        //private void TrapMoveUp(object sender, EventArgs e)//单击按钮时，判断是什么运动模式，再决定是否执行点动        
        public void TrapMoveUp(short AXIS, bool m_bMoveModeFlag, double m_dVel, double m_dStep,double RollerParam)
        {

            if (m_bMoveModeFlag == true)
            {
                motionMap.ClrLimitAndAbrupt(AXIS);//增添这一行非常关键。——————对应的trpmotion中间的清楚报警和限位就有点不太必要了              
                //读取指定轴的状态。
                motionMap.ReadAxisSate(AXIS);
                if (motionMap.axisSateMonitor.FlagPosLimit1 == true)//处于正限位，不进行工作
                {/*不执行任何操作*/}
                else//没有正限位，执行正向点位运动
                {

                    //执行点动运动
                    motionMap.trapPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    //（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                    motionMap.trapPrm.dec = 1000;//（1）确定：参数的acc和dec含义需要确认清楚————这个参数意义不是很大
                    motionMap.trapPrm.velStart = 5;
                    motionMap.trapPrm.smoothTime = 1;

                    int position = (int)(Convert.ToDouble(m_dStep) * 1000);//20201011修正：在固高控制器中，每mm对应1000个脉冲
                    double vel = Convert.ToDouble(m_dVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一

                    int RollerDirection = 0;//20220527新增：
                    motionMap.TrapMotion(AXIS, ref motionMap.trapPrm, position, vel, RollerParam, RollerDirection, false);
 
                }
            }
            else if (m_bMoveModeFlag == false) {/*不执行任何操作*/}
        }
        //(5)点动下降：
        /*************************(b)下降按键*****************************/
        //异常1：关于此下降按键：JOG运动是正常，点动是不正常
        //异常2：点动表现出来：限位之后，按反方向点动没有反应，需要重新点击切换按键进行切换才可以
        //private void TrapMoveDown(object sender, EventArgs e)//下降按键
        public void TrapMoveDown(short AXIS, bool m_bMoveModeFlag, double m_dVel, double m_dStep, double RollerParam)//下降按键
        {
            if (m_bMoveModeFlag == true)
            {
                motionMap.ClrLimitAndAbrupt(AXIS);//增添这一行非常关键。——————对应的trpmotion中间的清楚报警和限位就有点不太必要了
                //读取指定轴的状态。
                motionMap.ReadAxisSate(AXIS);
                //根据读取的指定轴的状态进行下面的判断。
                if (motionMap.axisSateMonitor.FlagNegLimit1 == true)//处于负限位，不进行工作
                { }
                else//不在负限位限位，执行反向点位运动
                {
                    ////点位运动前，必要的保证工作
                    ////（1）先重新暂停一下所有的运动
                    ////（2）否则，连击点动，会提示点动运动错误
                    //motionMap.StopMotion(1);
                    //执行点动运动
                    motionMap.trapPrm.acc = 1000;//————————————————————待实现，从其他的图形窗口中读取对应的值
                    motionMap.trapPrm.dec = 1000;
                    motionMap.trapPrm.velStart = 5;
                    motionMap.trapPrm.smoothTime = 1;

                    int position = (int)(Convert.ToDouble(m_dStep) * 1000);//20201011修正：在固高控制器中，每mm对应100个脉冲
                    double vel = Convert.ToDouble(m_dVel);////20201011修正：在固高控制器中，每mm对应100个脉冲：速度为原来的10分之一

                    int RollerDirection = 0;//20220527新增：
                    motionMap.TrapMotion(AXIS, ref motionMap.trapPrm, -position, vel, RollerParam, RollerDirection, false);

                }
            }
            else if (m_bMoveModeFlag == false) {/*不执行任何操作*/}
        }

        ////20200221新增：（1）编码位置（2）编码速度（3）设置编码位置：总计读取8轴的运动数据
        //private readonly short cardNumber = 0;//私有变量——默认选择为运动控制卡1
        //public double[] GetEncPos(/*out double[] encpos*/)//20200221新建:读取8轴的编码位置
        //{
        //    uint clk;//20200221:读取时钟————————————没卵用
        //    double[] encpos = new double[8];
        //    gts.mc.GT_GetEncPos(cardNumber, 1, out encpos[0], 8, out clk);
        //    return encpos;
        //}
        //public double[] GetEncVel(/*out double encpos*/)//20200221新建:读取8轴的编码器速度
        //{
        //    uint clk;//20200221:读取时钟————————————没卵用
        //    double[] encvel = new double[8];
        //    gts.mc.GT_GetEncPos(cardNumber, 1, out encvel[0], 8, out clk);
        //    return encvel;
        //}
        //public void SetEncPos(short encoder, int encpoc)//20200221新建:复位初始化，设置8轴的编码位置
        //{
        //    gts.mc.GT_SetEncPos(cardNumber, encoder, encpoc);//设置单轴的编码器位置
        //}

    }

    /****************************************墨量状态控件类****************************************/
    public class InkStateCtrl//20200220新增：3条电机运动状态显示栏
    {
        int m_nOffset;//参数1:
        int m_nInkCnts;//参数2
        int m_nInkState;//参数3
        string m_szInfo;//参数4：显示信息，stringInfo
        ///////存放所有墨盒INK的标签
        List<string> CtrlLabel = new List<string>();//初始化值为空
        //////类初始化
        public InkStateCtrl()
        {
            m_nOffset = 0;//参数1
            m_nInkCnts = 8;//参数2
            m_nInkState = 0;//参数3
            m_szInfo = "";//参数4：显示信息，stringInfo
        }
        ///////设置墨盒INK数量
        public void SetInkCount(int nInkCnts, int nOffset)//nOffset:设置非常巧妙：
                                                          //这样int可以表示那么多的状态，且不同的区域，表示不同类型的值的状态
        {
            m_nOffset = nOffset;
            m_nInkCnts = nInkCnts;
        }
        //////设置INK墨盒状态
        public void SetInkState(int nInkState)
        {
            m_nInkState = nInkState;
        }
        //////(1)绘制方形控件：20200331新增
        public void OnPaint(Graphics gra, Rectangle rectClient)
        {
            //Graphics g = this.CreateGraphics();
            Pen drawPen = new Pen(Color.Green, (float)0.5);
            SolidBrush b1 = new SolidBrush(Color.FromArgb(50, Color.Green));//画笔

            int nCtlValue;
            string CtrlText = null;
            //Rectangle rectClient;//父亲窗体，即是绘制所有的墨盒状态的父窗体空间
            Rectangle rectItem = new Rectangle(); ;//子窗体，父窗体中分割出的子窗体   

            int widthUnit = rectClient.Width / m_nInkCnts;//子窗体分割控件的宽度
            int heightUnit = rectClient.Height;//子窗体分割控件的高度

            //依次绘制出各个控件的状态（默认为8个）
            for (int i = m_nOffset; i < m_nInkCnts + m_nOffset; i++)
            {
                //从掩码中提取对应的位状态
                nCtlValue = (1 << (i - m_nOffset));

                //设置第i位的背景画布的尺寸
                //3目运算符运行的非常好，类似于1个if-else判断
                rectItem.X = (i - m_nOffset) * widthUnit;
                rectItem.Y = (i + 1 - m_nOffset < m_nInkCnts) ? ((i + 1 - m_nOffset) * widthUnit) : (rectClient.X + rectClient.Width);
                rectItem.Width = widthUnit;
                rectItem.Height = heightUnit;

                //绘制第i位的背景画布
                //dc.Draw3dRect(&rectItem, RGB(192, 192, 192), RGB(100, 100, 100));
                gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                gra.FillRectangle(b1, rectItem);//画子窗体背景填充色

                //设置第i位的前景图形
                rectItem.X += 2; rectItem.Y = 2;
                rectItem.Width -= 4; rectItem.Height -= 6;
                //绘制第i位的前景图形
                //此处的运算符巧妙：3目运算符的使用
                if ((m_nInkState & nCtlValue) == 0)//掩码校验
                {
                    drawPen = new Pen(Color.Black, (float)0.5);
                    //b1 = new SolidBrush(Color.FromArgb(100, Color.Green));//画笔
                    b1 = new SolidBrush(Color.LimeGreen/*LimeGreen*//*Lime*/);//画笔
                }
                else
                {
                    drawPen = new Pen(Color.Black, (float)0.5);
                    //b1 = new SolidBrush(Color.FromArgb(100, Color.Red));//画笔
                    b1 = new SolidBrush(Color.OrangeRed/*Red*/);//画笔
                }

                gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                gra.FillRectangle(b1, rectItem);//画子窗体背景填充色                

                //绘制第i位的前景文字
                b1 = new SolidBrush(Color.Black);//画笔
                CtrlText = (i + 1).ToString();
                gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman"/*"微软雅黑"*/, 11/*, FontStyle.Italic*//*Bold*/), b1, new Point(rectItem.X + rectItem.Width / 2 - 6, rectItem.Y + rectItem.Height / 2 - 6));

                //画笔和画刷复位
                drawPen = new Pen(Color.Red, (float)0.5);
                b1 = new SolidBrush(Color.Red);//画笔
                //this.CreateGraphics().
            }
        }
        //////(2)绘制圆形控件：20200331新增
        public void OnPaintCircle(Graphics gra, Rectangle rectClient)
        {
            //Graphics g = this.CreateGraphics();
            Pen drawPen = new Pen(Color.Green, (float)0.5);
            SolidBrush b1 = new SolidBrush(Color.FromArgb(50, Color.Green));//画笔

            int nCtlValue;
            string CtrlText = null;
            //Rectangle rectClient;//父亲窗体，即是绘制所有的墨盒状态的父窗体空间
            Rectangle rectItem = new Rectangle(); ;//子窗体，父窗体中分割出的子窗体   

            int widthUnit = rectClient.Width / m_nInkCnts;//子窗体分割控件的宽度
            int heightUnit = rectClient.Height;//子窗体分割控件的高度

            //依次绘制出各个控件的状态（默认为8个）
            for (int i = m_nOffset; i < m_nInkCnts + m_nOffset; i++)
            {
                //从掩码中提取对应的位状态
                nCtlValue = (1 << (i - m_nOffset));

                //设置第i位的背景画布的尺寸
                //3目运算符运行的非常好，类似于1个if-else判断
                rectItem.X = (i - m_nOffset) * widthUnit;
                rectItem.Y = (i + 1 - m_nOffset < m_nInkCnts) ? ((i + 1 - m_nOffset) * widthUnit) : (rectClient.X + rectClient.Width);
                rectItem.Width = widthUnit;
                rectItem.Height = heightUnit;

                //绘制第i位的背景画布
                //dc.Draw3dRect(&rectItem, RGB(192, 192, 192), RGB(100, 100, 100));
                gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                gra.FillRectangle(b1, rectItem);//画子窗体背景填充色

                //设置第i位的前景图形//20200331修改：
                rectItem.X += 3; rectItem.Y = 2;
                rectItem.Width -= 6; rectItem.Height -= 6;
                //绘制第i位的前景图形
                //此处的运算符巧妙：3目运算符的使用
                if (0 < i && i < 6/*5*/)//墨盒余量测试：0-5
                {
                    if ((m_nInkState & nCtlValue) == 0)//掩码校验
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.White/*Yellow*//*LightCyan*//*LightBlue*//*LightGoldenrodYellow*//*LightPink*//*LightGray*//*LimeGreen*/);//画笔
                    }
                    else
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.OrangeRed/*Salmon*//*LightPink*//*Red*/);//画笔
                    }

                    ////（1-1）绘制方形标志：20200331新增
                    gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                    gra.FillRectangle(b1, rectItem);//画子窗体背景填充色     
                    //（1-2）绘制椭圆形标志：20200331新增  
                    //gra.FillEllipse(b1, rectItem);//画子窗体背景填充色   
                    //gra.DrawEllipse(drawPen, rectItem);//画子窗体背景框

                    if (i == 1/*i>3*//*i < 1*/)//20200331新建：//20220525修改：
                    {
                        //绘制第i位的前景文字
                        if ((m_nInkState & nCtlValue) == 0)
                        {
                            b1 = new SolidBrush(Color.Blue/*Black*/);//画笔
                        }
                        else//触发统一更换成黑笔
                        {
                            b1 = new SolidBrush(Color.White);//画笔
                        }

                        CtrlText = "主墨";//"Ink" + "B"/* (i + 1).ToString()*/;
                        gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman", 10, FontStyle.Bold), b1, new Point(rectItem.X + rectItem.Width / 2 - 17, rectItem.Y + rectItem.Height / 2 - 6));
                        //画笔和画刷复位
                        drawPen = new Pen(Color.Red, (float)0.5);
                        b1 = new SolidBrush(Color.Red);//画笔
                    }
                    else//第3-6项
                    {
                        //绘制第i位的前景文字
                        if ((m_nInkState & nCtlValue) == 0)
                        {
                            b1 = new SolidBrush(Color.Blue/*Red*//*Black*/);//画笔
                        }
                        else//触发统一更换成黑笔
                        {
                            b1 = new SolidBrush(Color.White);//画笔
                        }


                        CtrlText = "墨 "/*"Ink"*/ + (i-1/*i+1*/).ToString();//20220525修改：
                        gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman", 10, FontStyle.Bold), b1, new Point(rectItem.X + rectItem.Width / 2 - 16, rectItem.Y + rectItem.Height / 2 - 6));
                        //画笔和画刷复位
                        drawPen = new Pen(Color.Red, (float)0.5);
                        b1 = new SolidBrush(Color.Red);//画笔
                    }

                }
                else if (i == 0/*5*/)//清洗液标志位//20200331新增：
                {
                    if ((m_nInkState & nCtlValue) == 0)//掩码校验
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.White/*LightGray*//*LimeGreen*/);//画笔
                    }
                    else
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.OrangeRed/*Salmon*//*LightPink*//*Red*/);//画笔
                    }
                    ////（1-1）绘制方形标志：20200331新增
                    gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                    gra.FillRectangle(b1, rectItem);//画子窗体背景填充色     
                    //（1-2）绘制椭圆形标志：20200331新增  
                    //gra.FillEllipse(b1, rectItem);//画子窗体背景填充色   
                    //gra.DrawEllipse(drawPen, rectItem);//画子窗体背景框

                    //绘制第i位的前景文字
                    //绘制第i位的前景文字
                    if ((m_nInkState & nCtlValue) == 0)
                    {
                        b1 = new SolidBrush(Color.Black/*Black*/);//画笔
                    }
                    else//触发统一更换成黑笔
                    {
                        b1 = new SolidBrush(Color.White);//画笔
                    }
                    
                    CtrlText = "清洗"/*"Liq"*//* +*/ /*(i + 1).ToString()*//*"C"*/;
                    gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman", 10, FontStyle.Bold), b1, new Point(rectItem.X + rectItem.Width / 2 - 16, rectItem.Y + rectItem.Height / 2 - 6));
                    //画笔和画刷复位
                    drawPen = new Pen(Color.Red, (float)0.5);
                    b1 = new SolidBrush(Color.Red);//画笔
                    //this.CreateGraphics().
                }
                else
                {
                    if ((m_nInkState & nCtlValue) == 0)//掩码校验
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.White/*LightGray*//*LimeGreen*/);//画笔
                    }
                    else
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.OrangeRed/*Salmon*//*LightPink*//*Red*/);//画笔
                    }
                    ////（1-1）绘制方形标志：20200331新增
                    gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                    gra.FillRectangle(b1, rectItem);//画子窗体背景填充色     
                    //（1-2）绘制椭圆形标志：20200331新增  
                    //gra.FillEllipse(b1, rectItem);//画子窗体背景填充色   
                    //gra.DrawEllipse(drawPen, rectItem);//画子窗体背景框

                    //绘制第i位的前景文字
                    if ((m_nInkState & nCtlValue) == 0)
                    {
                        b1 = new SolidBrush(Color.Black);//画笔
                    }
                    else//触发统一更换成黑笔
                    {
                        b1 = new SolidBrush(Color.White);//画笔
                    }

                    //b1 = new SolidBrush(Color.Black);//画笔
                    CtrlText ="保护"/* "Air" +*/ /*(i + 1).ToString()*//*"P"*/;
                    gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman", 10, FontStyle.Bold), b1, new Point(rectItem.X + rectItem.Width / 2 - 17, rectItem.Y + rectItem.Height / 2 - 6));
                    //画笔和画刷复位
                    drawPen = new Pen(Color.Red, (float)0.5);
                    b1 = new SolidBrush(Color.Red);//画笔
                    //this.CreateGraphics().
                }


            }
        }

        //////(3)绘制电池控件：20200718新增
        public void OnPaintValveState(Graphics gra, Rectangle rectClient)
        {
            //Graphics g = this.CreateGraphics();
            Pen drawPen = new Pen(Color.Green, (float)0.5);
            SolidBrush b1 = new SolidBrush(Color.FromArgb(50, Color.Green));//画笔

            int nCtlValue;
            string CtrlText = null;
            //Rectangle rectClient;//父亲窗体，即是绘制所有的墨盒状态的父窗体空间
            Rectangle rectItem = new Rectangle(); ;//子窗体，父窗体中分割出的子窗体   

            int widthUnit = rectClient.Width / m_nInkCnts;//子窗体分割控件的宽度
            int heightUnit = rectClient.Height;//子窗体分割控件的高度

            //依次绘制出各个控件的状态（默认为8个）
            for (int i = m_nOffset; i < m_nInkCnts + m_nOffset; i++)
            {
                //从掩码中提取对应的位状态
                nCtlValue = (1 << (i - m_nOffset));

                //设置第i位的背景画布的尺寸
                //3目运算符运行的非常好，类似于1个if-else判断
                rectItem.X = (i - m_nOffset) * widthUnit;
                rectItem.Y = (i + 1 - m_nOffset < m_nInkCnts) ? ((i + 1 - m_nOffset) * widthUnit) : (rectClient.X + rectClient.Width);
                rectItem.Width = widthUnit;
                rectItem.Height = heightUnit;

                //绘制第i位的背景画布
                //dc.Draw3dRect(&rectItem, RGB(192, 192, 192), RGB(100, 100, 100));
                gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                gra.FillRectangle(b1, rectItem);//画子窗体背景填充色

                //设置第i位的前景图形//20200331修改：
                rectItem.X += 2; rectItem.Y = 2;
                rectItem.Width -= 4; rectItem.Height -= 6;
                //绘制第i位的前景图形
                //此处的运算符巧妙：3目运算符的使用
                if (i>0 && i<8)//墨盒余量测试：0-6
                {
                    if ((m_nInkState & nCtlValue) == 0)//掩码校验
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.White/*Yellow*//*LightCyan*//*LightBlue*//*LightGoldenrodYellow*//*LightPink*//*LightGray*//*LimeGreen*/);//画笔
                    }
                    else
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.White/*Salmon*//*LightPink*//*Red*/);//画笔
                    }

                    ////（1-1）绘制方形标志：20200331新增
                    gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                    gra.FillRectangle(b1, rectItem);//画子窗体背景填充色

                    //（1-2）绘制椭圆形标志：20200331新增  
                    {
                        //绘制第i位的前景文字
                        if ((m_nInkState & nCtlValue) == 0)
                        {
                            b1 = new SolidBrush(Color.Red/*Black*/);//画笔
                            CtrlText = "清 " + i;
                        }
                        else//触发统一更换成黑笔
                        {
                            b1 = new SolidBrush(Color.Blue);//画笔
                            CtrlText = "墨 " + i;
                        }

                        gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman", 8, FontStyle.Bold), b1, new Point(rectItem.X + rectItem.Width / 2 - 13, rectItem.Y + rectItem.Height / 2 - 6));
                        //画笔和画刷复位
                        drawPen = new Pen(Color.Red, (float)0.5);
                        b1 = new SolidBrush(Color.Red);//画笔
                    }

                }
                else
                {
                    if ((m_nInkState & nCtlValue) == 0)//掩码校验
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.White/*LightGray*//*LimeGreen*/);//画笔
                    }
                    else
                    {
                        drawPen = new Pen(Color.Black, (float)0.5);
                        b1 = new SolidBrush(Color.OrangeRed/*Salmon*//*LightPink*//*Red*/);//画笔
                    }
                    ////（1-1）绘制方形标志：20200331新增
                    gra.DrawRectangle(drawPen, rectItem);//画子窗体背景框
                    gra.FillRectangle(b1, rectItem);//画子窗体背景填充色     
                    //（1-2）绘制椭圆形标志：20200331新增  
                    //gra.FillEllipse(b1, rectItem);//画子窗体背景填充色   
                    //gra.DrawEllipse(drawPen, rectItem);//画子窗体背景框

                    //绘制第i位的前景文字
                    if ((m_nInkState & nCtlValue) == 0)
                    {
                        b1 = new SolidBrush(Color.Black);//画笔
                        CtrlText = "负气";
                    }
                    else//触发统一更换成黑笔
                    {
                        b1 = new SolidBrush(Color.White);//画笔
                        CtrlText = "正气";
                    }

                    gra.DrawString(CtrlText, new Font(/*"宋体"*/"Times New Roman", 8, FontStyle.Bold), b1, new Point(rectItem.X + rectItem.Width / 2 - 14, rectItem.Y + rectItem.Height / 2 - 7));
                    //画笔和画刷复位
                    drawPen = new Pen(Color.Red, (float)0.5);
                    b1 = new SolidBrush(Color.Red);//画笔
                }


            }
        }

    }
}
