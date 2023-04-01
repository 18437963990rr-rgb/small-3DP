using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;//DLLImport导入DLL包，后面添加命名空间
using BinderJetting;
using System.Windows.Forms;
//using royal;//下面的命名空间相同

namespace royal
{
    public class RoyalPrintingMap
    {
        public bool m_bPrinting;   //正在打印
        public int m_nPrintState;  //0 待机 1 打印 2 闪喷
        public bool m_bJobStarted = false;//JOB start Trigger:20200514
        public bool m_bJobStoped = false;//JOB close Trigger:20200514
        public string m_szRipFilePath;

        public bool[] m_bEnable = new bool[10];//车头板输出：10个按钮的值
        public bool[] m_bEnable1 = new bool[6];//主板输出：6路按钮的值
        public bool[] m_bUsbOutPut = new bool[8];//主板8路墨泵输出，其中7、8路为清洗液及压墨输出

        public bool m_bYSyncCtl = false;//Y1轴和Y2轴同步标志位
        public bool m_bXmove = false;//X运动标志位
        public bool m_bYmove1 = false;//Y1运动标志位
        public bool m_bYmove2 = false;//Y2运动标志位

        //PRTIMG_LAYER _testLayer;	//打印图层对象
        public tag_ImgLayer _testLayer;    //打印图层对象(PRTIMG_LAYER)
        public class tag_ImgLayer //保存监测到的轴状态参数（C#结构体变量）：本数据被输出到状态栏
        {
            public long nLayerIndex;      //图层索引
            public int nImgStartJetIndex; //图像起始对应喷头嘴;
            public int nPrtDir;           //图像打印起始PASS方向//  0 编码负方向 1编码正方向
            public int nXEncOff;          //图层针对任务X起点偏移 单位 X编码
            public int nYJetOff;          //图层针对任务Y起点偏移 单位 喷嘴间距
            public int nColorCnts;        //颜色通道数量
                                          //long	nState;			 //图层处理状态 //0 数据处理阶段  1数据处理就绪  2 写数据到硬件  3 数据已写入硬件  4 已添加到打印队列  5打印已结束 6添加失败
                                          //long	nImgType;		 //源文件图片类型 0：BMP 1:PRT 2:CLI
            long nXDPI;          //图像XIDPI
            long nYDPI;          //图像YIDPI 计算时暂时不用，默认为喷头组DPI
            long nBytesPerLine;  //每行数据字节数
            long nHeight;        //图像高度  单位：像素
            long nWidth;             //图像宽度  单位：像素
            long nPrtFlag;        //bit0  双向打印 bit1: Y打印方向  1反方向打印 bit2: 往返差校准打印
            long[] nReserved = new long[8];
            //long[] nReserved;//不能在申明数组的同时，指定数组的大小
        }

        public tag_PrtJobItem _testJob;
        //PRTJOB_ITEM  _testJob;		//打印作业对象
        public class tag_PrtJobItem
        {
            //unsigned int nJobID;
            //unsigned int nPrtXEncPos;     //任务的X向起打位置
            //int nPrtCtl;         //bit0 跳白支持  bit1 循环喷嘴偏移  bit2 Y向偏差无重嘴 bit4 X镜像 bit5 Y镜像
            //char szJobName[64];
            int nJobID;
            char[] szJobName;//char szJobName[64];
            int nPrtXEncPos;     //任务的X向起打位置
            float fPrtYPos;         //保留 离0位位置
            long nPixelGrayBits;   //灰度数据位数
            int nPrtCtl;         //bit0 跳白支持  bit1 循环喷嘴偏移  bit2 Y向偏差无重嘴 bit4 X镜像 bit5 Y镜像
        }
        /// <summary>
        /// 20210306新增：校准Royal控制器的喷头组，提升打印精度
        /// </summary>
        /// <param name="BiDirEncPrtOff"></param>
        /// <param name="PhXRowPrtOff"></param>
        /// <param name="PhYJetOff"></param>
        /// <returns></returns>
        public bool UpdataRoyalPrintCardWithFeedbackData(int BiDirEncPrtOff,int[] PhXRowPrtOff,int[] PhYJetOff)//20210306新增：校准Royal控制器的喷头组，提升打印精度
        {
            royal.g_sys_param.nBiDirEncPrtOff = BiDirEncPrtOff;//非常关键：双向z偏差值
            royal.g_sys_param.nPhXRowPrtOff = PhXRowPrtOff;//非常关键：X向套色偏差值
            royal.g_sys_param.nPhYJetOff = PhYJetOff;//非常关键：Y向套色偏差值


            int[] PhXRowPrtOff22 = new int[64 * 32 * 2];
            int[] PhYJetOff22 = new int[16 * 32];
            PhXRowPrtOff22 = royal.g_sys_param.nPhXRowPrtOff;
            PhYJetOff22 = royal.g_sys_param.nPhYJetOff;


            bool returnST = royal.DEV_UpdateParam(ref royal.g_sys_param);
            if (returnST == false)
            {
                //MessageBox.Show("设置喷头组校准参数失败");//20210322注释
                return false;
            }
            else
            {
                //MessageBox.Show("设置喷头组校准参数成功");//20210322注释
                return true;
            }
        }

        /// <summary>
        /// 代开Royal控制器建立通讯，同时实现控制器的参数初始化
        /// </summary>
        public bool InitRoyalPrintCard()//20200305新增;新建Royal控制器初始化方法
        {
            ///（1）打开设备：
            ///（1）打开设备：
            IntPtr ParenthWnd = new IntPtr(0);//获取主窗口句柄          
            ParenthWnd = royal.FindWindow(null, "LASERADD-BinderJetter"/* "Royal3DP测试DEMO主界面"*/);//获取主窗口句柄:通过第三方dll获取
            int nRetVal = royal.DEV_OpenDevice(ParenthWnd, 
                System.Windows.Forms.Application.StartupPath+ @"\"
                /*+ @"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\Debug\"*//*"F:\\RoyalOutput\\x64\\"*/);

            bool flag1=false, flag2=false, flag3=false;
            if (nRetVal > 0)
            {
                string sztxt;
                string msg = "打开喷墨控制器设备失败！";
                Log4Net.Info(msg);
                sztxt = string.Format("打开设备失败：{0:X00000000}", nRetVal);
                MessageBox.Show(sztxt);
            }
            else
            {
                flag1 = true;
                string msg = "打开喷墨控制器设备成功！";
                Log4Net.Info(msg);
            }
            //royal.g_sys_param = new LPRYSYS_PARAM();//20210327修改：

            ///（2）更新设备参数：//20200326修订：系统参数的修改移到主界面
            royal.g_sys_param.nResMemory = 256;//20200423新增：指示软件的保留物理内存大小为256MB
            royal.g_sys_param.nIoOption = 0xC0000000;//20200803新增：系统参数控制位使能：使能31位、32位——使能生成图层信息log；使能生成PASS写数据log

            //(2-1)修改闪喷频率为500Hz,有效时间为0.5ms,周期时长为1ms
            royal.g_sys_param.fBrustCycleSec = 1f;//20220920修改：闪喷频率500-1s时间内，0.5s在工作
            royal.g_sys_param.fBrustValidSec = 0.5f;//20220920修改：闪喷频率500-1s时间内，0.5s在工作
            royal.g_sys_param.fBrustFrequecy = 500*10/*2000*//*50*/;//20220920修改：闪喷频率500-1s时间内，0.5s在工作//20230327修改：底层的配置文件的闪喷的基准频率设置不准确，需要认为设置并扩展10倍

            bool returnST = royal.DEV_UpdateParam(ref royal.g_sys_param);
            if (returnST == false)
            {
                string sztxt;
                sztxt = "更新设备参数失败";
                MessageBox.Show(sztxt);
            }
            else
            {
                flag2 = true;
            }
            ///（3）初始化设备参数：
            UInt32 nSysInitEncVal = 0x1000000 /*2000*//*0x100000*/;//20200801批注：最大计数值为83.88608M;此值来自运动系统当前X编码——非常关键：安全性至关重要 //距离Smm转换为Encode,DPI为扫描方向光栅DPI //Encode=((S/25.4)*DPI)
            UInt32 nRetVal2 = royal.DEV_InitDevice(nSysInitEncVal);
            if (nRetVal2 < 0)
            {
                string sztxt;
                sztxt = string.Format("初始化设备失败：{0:X00000000}", nRetVal);
                MessageBox.Show(sztxt);
            }
            else
            {
                flag3 = true;
            }

            ///（4）读取USB的连接状态
            LPPRINTER_INFO g_printerInfoLocal = new LPPRINTER_INFO();            
#if false
            bool returnCode = royal.DEV_GetDeviceInfo2(ref g_printerInfoLocal);//20200304修改：//bool__stdcall DEV_GetDeviceInfo(LPPRINTER_INFO pDevInfo);
#else
            ////(b)返回指针的API测试：
            IntPtr info = royal.DEV_GetDeviceInfo();//——————调用API1(修改后的API1)
            g_printerInfoLocal = (LPPRINTER_INFO)Marshal.PtrToStructure(info, typeof(LPPRINTER_INFO));//调用API1获取的指针
            //string nMainFpgaVer = string.Format("{0:X}", g_printerInfoLocal.nMainFpgaVer);//方法2：
            //MessageBox.Show("主FPGA版本号是：" + nMainFpgaVer + "，"
            //    + "USB3.0版本号是：" + Convert.ToString(g_printerInfoLocal.nUsbVersion));
#endif
            if (g_printerInfoLocal.bSuperDevice == 1)
            {
                MessageBox.Show("本机为USB3.0系统");
            }
            else if(g_printerInfoLocal.bSuperDevice == 0)
            {
                MessageBox.Show("本机为USB2.0系统");
            }
            else
            {
            }

#if false//20230331修改：更正板卡MCU程序之后，已不再需要开机手动设置喷头温度，温度自动稳定在环境温度；后续需要修改温度，手动修改温度即可
        try { 
            //（5）初始化喷头电压及温度
            for (uint d = 0; d < 1/*8*/; d++)
            {
                ////////(1)喷头电压
                //////float[] fstdVoltage = new float[4] {15f,15f,15f,15f};//内存中的对应值

                ////////(2)设置电压：20200404修改
                //////int size = Marshal.SizeOf(fstdVoltage[0]) * fstdVoltage.Length;
                //////IntPtr PfstdVoltage = Marshal.AllocHGlobal(size);
                //////Marshal.Copy(fstdVoltage, 0, PfstdVoltage, fstdVoltage.Length);//复制到非托管区内存

                //////string msg2;
                //////bool returnCode = royal.MCU_SetPhVoltage(PfstdVoltage, 0, d);
                //////if (returnCode == false)
                //////{
                //////    msg2 = $"更新{{{d + 1}}}号喷头电压失败：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                //////    Log4Net.Info(msg2);
                //////}
                //////else
                //////{
                //////    msg2 = $"更新{{{d + 1}}}号喷头电压成功：MCU_SetPhVoltage{{{fstdVoltage[0]}V,{fstdVoltage[1]}V,{ fstdVoltage[2]}V,{ fstdVoltage[3]}V}}；";
                //////    Log4Net.Info(msg2);
                //////}

                //（3）设置温度：20200404修改
                float fProTemp = 25f /*70.0f*/;//ftemp = fDTemp[p][d];  
                bool returnCode2 = royal.MCU_SetPhStdTemp(ref fProTemp, 0, d);
                if (returnCode2 == false)
                {
                    string msg2 = $"更新{{{d+1}}}号喷头温度失败：MCU_SetPhStdTemp{{{fProTemp}℃}}";
                    Log4Net.Info(msg2);
                }
                else 
                {
                    string msg2 = $"更新{{{d + 1}}}号喷头温度成功：MCU_SetPhStdTemp{{{fProTemp}℃}}";
                    Log4Net.Info(msg2);
                }
            }
        }
        catch (Exception error)
        {
            string msg = $"更新喷头温度异常：{error.ToString()}";
            Log4Net.Info(msg);
            MessageBox.Show("警告：" + error.Message + "！");//eg:listview输入有误
        }
#endif

#if true//20200618新增批注：本部分对整体的运动影响巨大，不应该出现这种情况。如果该喷墨控制卡重新开启，则会限于不动的状态//初始化运动并使能运动；//但是有本部分，自动打印状态下开启手动，又会莫名停机。
            bool RetVal4 = royal.DEM_InitAxis(0, 0x100);//分别初始化各轴的运动参数：20200305//加速度：256pluse/ms^2
            RetVal4 = royal.DEM_InitAxis(1, 0x100);//分别初始化各轴的运动参数：20200305
            RetVal4 = royal.DEM_InitAxis(2, 0x100);//分别初始化各轴的运动参数：20200305//IOS_SetConfig(0x3000,0x0);			//Reg0x14	复用掩码设置 保留输出1、2
            RetVal4 = royal.DEM_EnableAxisRun(true);//所有的轴共用1个使能，使能一次就OK!:20200305
            bool RetVal5 = royal.DEM_EnableDYOutPut(true, true);//20220506新增：//完全不必要使能双Y输出：20200305
            bool RetVal6 = royal.DEM_EnableLimitFun(1, true);//20220511新建：Y轴限位使能//参数：UInt32 nAxis, bool bEnable
#endif

            //20200421新增：3步全部正确，才能执行设置标志位，标志着初始化成功
            //20200421新增：返回初始化的状态到全局变量，这里非常关键，对于后续的干涉操作的是否执行的判断起到关键作用
            return (flag1 && flag2 && flag3);//20200421新增：返回初始化的状态到全局变量，这里非常关键，对于后续的干涉操作的是否执行的判断起到关键作用
        }

        //按照虚拟编码器位置，打开及关闭UV灯
        bool m_bEanbleAutoCtl = true;//开启/关闭UV灯的标志位    
        public void OpenUV(bool Flag, UVLightParam uVLightParam)
        {
            if (Flag==true)//开启两侧UV灯
            {
                //按照虚拟编码器位置，打开及关闭UV灯
                ///(a)初始化UV灯开启范围及限位值：
                Int32[] nMinPos = new Int32[2];
                Int32[] nMaxPos = new Int32[2];
                nMinPos[0] = (int)(uVLightParam.nMinPos[0] * 200);//字符串转数值
                nMinPos[1] = (int)(uVLightParam.nMinPos[1] * 200);//字符串转数值
                nMaxPos[0] = (int)(uVLightParam.nMaxPos[0] * 200);//字符串转数值
                nMaxPos[1] = (int)(uVLightParam.nMaxPos[1] * 200);//字符串转数值
                float m_fCycleSec = 1 / (float)uVLightParam.m_nFrequency;//20200304：50ms——(1)125Hz，对应的是8ms(2)单位时ms还是us//20200407新增确认，单位是S
                float m_fValidSec = m_fCycleSec * (float)uVLightParam.m_fPower / 100;//20200304：25ms——(1)1ms=12.5%功率，2ms=25%功率（2）m_fValidSec=m_fCycleSec*power/100;//power/100
                //（b）数组1的指针
                int size = Marshal.SizeOf(nMinPos[0]) * nMinPos.Length;
                IntPtr PnMinPos = Marshal.AllocHGlobal(size);
                //(b)数组2的指针
                int size2 = Marshal.SizeOf(nMaxPos[0]) * nMaxPos.Length;
                IntPtr PnMaxPos = Marshal.AllocHGlobal(size2);
                Marshal.Copy(nMinPos, 0, PnMinPos, nMinPos.Length);//复制到非托管区内存
                Marshal.Copy(nMaxPos, 0, PnMaxPos, nMaxPos.Length);//复制到非托管区内存
                bool nRetVal = royal.DEV_EnableUVPosCtlOut(false, false);//（0）设置UV等使能开启
                if (m_bEanbleAutoCtl)
                {
                    bool nRetVal1 = royal.DEV_SetUVLampPosRange(PnMinPos, PnMaxPos);//（1）设置UV灯使能开启
                    bool returnCode = royal.DEV_SetPwmParam(true, m_fCycleSec, m_fValidSec);//（2）设置UV灯功率输出：//20200604批注：取消UV灯参数使能按钮
                    bool nRetVal2 = royal.DEV_EnableUVPosCtlOut(true, true);//（3）打开两侧的UV灯：
                }
                Marshal.FreeHGlobal(PnMinPos);//释放内存
                Marshal.FreeHGlobal(PnMaxPos);//释放内存
            }
            else//关闭UV灯
            {
                bool nRetVal2 = royal.DEV_EnableUVPosCtlOut(false, false);//（3）关闭两侧UV灯
            }
        }

        public void ReloadRipInfo()//重载RIP信息？
        {
        }

        public int GetSrcData(string lpSrcFile)//CString lpSrcFile
        {
            return 1;
        }

        ///////图像打印处理
        public bool IDP_StopPrintJob()//关闭打印JOB
        {
            return true;
        }
        public int IDP_SartPrintJob()//开启打印JOB
        {
            return 1;
        }

        public bool DEV_ShoveInk(int nCMID, int nInkMask)//挤墨
        {
            return true;
        }
    }
}