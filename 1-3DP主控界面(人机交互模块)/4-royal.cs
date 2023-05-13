using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;//DLLImport导入DLL包，后面添加命名空间



namespace royal
{
    public class DATASIZE_DEFINE
    {
        public const int MAX_LENGTH_OF_IDENTICARDID = 20;   //maximum length of identicardid
        public const int MAX_LENGTH_OF_NAME = 64;           //maximum length of name
        public const int MAX_LENGTH_OF_COUNTRY = 50;        //maximum length of country
        public const int MAX_LENGTH_OF_NATION = 50;         //maximum length of nation
        public const int MAX_LENGTH_OF_BIRTHDAY = 8;        //maximum length of birthday
        public const int MAX_LENGTH_OF_ADDRESS = 200;       //maximum length of address
    }
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct LPPRTIMG_LAYER
    {
        public int nLayerIndex;      //图层索引
        public int nImgStartJetIndex; //图像起始对应喷头嘴;//20210311修改：起始嘴修改//20230418批注：屏蔽嘴数量
        public int nPrtDir;           //图像打印起始PASS方向//  0 编码负方向 1编码正方向//20220524批注：此处应该修改为负向
        public int nXEncOff;          //图层针对任务X起点偏移 单位 X编码//整体修改的位置
        public int nYJetOff;          //图层针对任务Y起点偏移 单位 喷嘴间距//20210311修改：起始嘴修改
        public int nColorCnts;        //颜色通道数量
        //public int nState;			 //图层处理状态 //0 数据处理阶段  1数据处理就绪  2 写数据到硬件  3 数据已写入硬件  4 已添加到打印队列  5打印已结束 6添加失败
        //public int nImgType;		 //源文件图片类型 0：BMP 1:PRT 2:CLI
        public float/*int*/ nXDPI;          //图像XIDPI//20230511修改：修改为浮点数
        public int nYDPI;          //图像YIDPI 计算时暂时不用，默认为喷头组DPI
        public int nBytesPerLine;  //每行数据字节数
        public int nHeight;        //图像高度  单位：像素
        public int nWidth;             //图像宽度  单位：像素
        public int nPrtFlag;        //bit0  双向打印 bit1: Y打印方向  1反方向打印 bit2: 往返差校准打印
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public int[] nReserved;			//数组简单——————配合使用即可：[MarshalAs(UnmanagedType.ByValArray,SizeConst =64)]
    }
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct LPPassDataItem
    {
        public IntPtr pNextItem;        //IntPtr用于表示平台特定的指针和句柄类型
        public IntPtr pDataBuf;            //目标数据缓存区 //unsigned char* pDataBuf;	

        public UInt32 nLayerIndex;       //所属图层号
        public UInt32 nLayerPassCount;   //所属图层PASS总数
        public UInt32 nLayerPassIndex;   //PASS序号
        public UInt32 nValidPassJets;        //PASS有效喷嘴数	20180818 新增
        public UInt32 nProcState;            //图层处理状态 //0 数据处理阶段  1数据处理就绪  2 写数据到硬件  3 数据已写入硬件  4 已添加到打印队列  5打印已结束 
        public int nMinJet0ImgLinePos; //Y向偏差最小喷头组的0号喷嘴相对图像的行位置//20220507修改：返回的PASS的参数              
        public bool bPrtDir;           //打印方向  0 编码负方向 1正方向
        public UInt32 nValidPrtCtlCnts;   //涉及到的打印控制器数量
        public UInt32 nValidPrtCols;      //有效打印列数，单点的图像可以理解为X像素点数
        public UInt32 nStartEncPos;      //起始位置 ，编码值 enc
        public float/*UInt32*/ nPrtPrecession;        //打印分频值//20230511修改：修改为浮点数
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public UInt32[] nPrtMemHwAddr; //板卡物理内存地址 nByte
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public UInt32[] nPrtDataOffset;            //数据起始偏移
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public UInt32[] nSrcDataSize;          //数据大小
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public UInt32[] nPrtColBytes;      //列长度
        public UInt32 nHwMemAdrMatchMask;              //已分配打印内存的MASK
        public int nDataTxCompleteCnt;                 //传输完成的次数                     
        public int nSrcStartCols;                      //扫描的X起始数据列，相对扫描正方向第一列，用于X向跳白计算
        public int nSrcEndCols;                        //扫描的X结束数据列，相对扫描正向最后一列，用于X向跳白计算
    }
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct PRTJOB_ITEM
    {
        public UInt32 nJobID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szJobName;
        public UInt32 nPrtXEncPos;     //任务的X向起打位置//20220524批注：X向起打位置
        public float fPrtYPos;         //保留 离0位位置
        public int nPixelGrayBits;   //灰度数据位数：1为灰度位数
        public UInt32 nPrtCtl;		  //bit0 跳白支持  bit1 循环喷嘴偏移  bit2 Y向偏差无重嘴 bit4 X镜像 bit5 Y镜像
    }
    //读取参数：底层返回的驱动卡的硬件状态信息——————（2）
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct LPDRVINFO//——————————————————————结构体4：含普通数组————————OK
    {
        public UInt32 nState;            //连接状态 1 表示已连接
        public UInt32 nNextState;        //连级下个驱动卡连接状态
        public UInt32 nSignature;        //驱动卡序列号
        public UInt32 nPCBVersion;   //驱动卡PCB版本
        public UInt32 nFMVersion;        //驱动卡MCU版本
        public UInt32 nFpgaVersion;  //驱动卡控制程序版本
        public UInt32 nPtvwarnState;   //1喷头不存在 2,8喷头温度超过控制范围
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public UInt32[] nRevInfo;
        public UInt32 nCrc32;
    }
    //设置参数：我给你启动作业的参数：驱动波形的参数——————但是可能用不着，不是很必要
    //[StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]//多字节字符
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]//宽字节字符
    public struct LPDRV_WAVEFORM//—————————————————结构体5：含二维数组—————————基本没用到，是否来测试？？？
    {
        public UInt32 nPlsNum;               //脉冲数量			最大4个	//20181105    
        public float fPluseT2;             //脉冲保护间隔		单位 1 us	//T2 共用        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3 * 3)]//C#统一使用一维数组，来表达二维数组
        public float[] fHPluseGap;		//多脉冲间隔		单位 1 us	//20181105	3个间隔*3个喷头——————————提供了1级间接（2维数组相当于1级间接）
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4 * 3)]
        public float[] fHPluseWidth;		//高压脉冲  3个		单位 1 us	//20181105  4个脉冲*3个喷头——————————提供了1级间接
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] fHVdestVoltage;        //电压设置值  3个	单位 V		//3个喷头
        public float fBoardAlarmTemp;      //板卡报警温度		单位 ℃		// 
                                           //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
                                           //public UInt32[] nRevParam;          //保留   
        public UInt32 nClrIndex;           //波形通道
        public UInt32 nDsMask;			   //MN     
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
        public string szName;
    }
    ///2019.12.22新添加
    ///设置气压板参数结构体的子结构体
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]//宽字节字符
    public struct I2C_DATA//—————————————————结构体5：含二维数组—————————基本没用到，是否来测试？？？
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fvoltage;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fInkTankTemp;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fAirPress;
    }
    ///2019.12.22新添加
    ///设置气压板参数
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]//宽字节字符
    public struct LPADIB_PARAM//—————————————————结构体5：含二维数组—————————基本没用到，是否来测试？？？
    {
        public UInt32 nFMver;                //程序版本号
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fcurvoltage;       //当前电压//是作为I2C_DATA数据的代替：20200329
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fcurInkTankTemp;   //当前温度//是作为I2C_DATA数据的代替：20200329
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fcurAirPress;  //当前负压//是作为I2C_DATA数据的代替：20200329
        public UInt32 nLgStatus;         //保留输入
        public UInt32 nVolState;            //当前电压状态
        //(a)I2C_DATA的替代方式1——结果不可行
        //public struct I2C
        //{
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fvoltage;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fInkTankTemp;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public float[] fAirPress;
        //}
        //(b)I2C_DATA的替代方式2
        public I2C_DATA I2C;//目前已经没啥卵用
        public float fAirThreshold;     //负压阈值
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public UInt32[] nReverse;		//预留
    }
    //读取参数：底层传输上来的打印状态的参数——————（1）
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct LPPrtRunInfo   //——————————————————————结构体6：含有普通数组——————————OK
    {
        public bool bJobPrtRuning;         //作业打印运行中  true 运行中
        public bool bLayerPrtIsOver;       //当前图层打印完成标志 true 当前图层打印完成————这个是我需要的：本层是否打完
        public int nPrtState;              //板卡打印控制器状态
        public int nPrintLayerIndex;       //当前打印的图层
        public int nLayerPassCount;        //当前打印图层PASS总数
        public int nPrintPassIndex;        //当前打印的PASS
        public int nCurPrtDir;             //当前的打印方向
        public int nRevPrtCols;            //当前剩余的打印列数——————这个是我需要的
        public int nProcLayerIndex;        //当前数据处理的图层
        public int nDTLayerIndex;          //当前数据传输的图层
        public int nDTLayerPassIndex;      //当前数据传输的PASS
        public int nDTPtrCtlIndex;         //当前数据传输的控制器
        public int nContWDErr;             //连续写内存数据错误
        public int nContReqMemErr;         //连续申请内存失败计数
        public int nPrtDataMemAddr;        //打印数据地址
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] nReverse;            //预留——————————————————————————--包含了数组
    }
    //读取参数：底层传输上来的——————包含上面的那个信息———（3）————（3）个包含（1）和（2）
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct LPPRINTER_INFO //—————结构体7：含有普通数组，还有结构体数组——————————OK
    {
        public UInt32 nVersion;         //打印机软件系统版本号
        public UInt32 nCustomIerD;        //客户号
        public UInt32 nXSysEncDPI;       //系统光栅DPI
        public UInt32 nMainFpgaVer;       //主控板FPGA版本 
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public UInt32[] nCarFpgaVer;     //车头板FPGA版本 
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public UInt32[] nMcuVersion;       //车头卡MCU版本
        public UInt32 nPrtValidMask;     //车头卡连接掩码
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public UInt32[] nDrvValidMask; //驱动卡连接掩码
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public UInt32[] nDrvWaveErrMask;   //驱动卡波形加载失败掩码
        public UInt32 nUsbVersion;       //USB固件版本
        public UInt32 nStatus;            //系统状态
        public UInt32 nPrintStatus;       //0 待机  1打印 2 暂停 
        public UInt32 bSuperDevice;        //U30设备:如何是为1的时候，是30设备；如果是为0的话，是20设备
        public UInt32 nLastErrClrModule; //Y向偏差过大，导致组间分离的状态 颜色索引
        public UInt32 nLastErrPhgIndex;  //Y向偏差过大，导致组间分离的状态 组索引
                                         //RY_USAGEINFO	global_usage;		//用户使用信息
        public UInt32 nPrtHeadType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 127)]
        public UInt32[] nRevInfo;
        public LPPrtRunInfo prt_rtinfo;//底层传输上来的打印参数信息————————————————————————————？？？？
        public UInt32 nCrc32;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public LPDRVINFO[] sysDrvInfo;    //驱动卡状态信息————————————————————这个是上面提到的那个结构体信息。————？
    }
    ///2019.12.21新添加即可
    //设置参数：我给你启动作业的参数：驱动波形的参数——————但是可能用不着，不是很必要
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]//宽字节字符
    public struct LPRYSYS_PARAM//—————————————————结构体5：含二维数组—————————基本没用到，是否来测试？？？
    {
        public int nParamVer;          //参数版本：c++的long相当于C#的int
        public int nParamSize;            //参数字节数：c++的long相当于C#的int
        public int nBiDirEncPrtOff;            /* 双向偏差*///实际上称呼：往返差比较准确一点//双向编码，单向像素//单位是：编码器值，读取的是像素偏差值，需要乘以分频值
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64 * 32 * 2)]//20210313批注：7*4*2
        public int[] nPhXRowPrtOff;		//单向偏差 0 负方向 1正反向  [喷头号][喷嘴列号][方向]//三维数组：32列嘴，是为了兼容其他的喷头，只使用前4列即可
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16 * 32)]
        public int[] nPhYJetOff;        //Y向嘴偏差      
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public LPDRV_WAVEFORM[] drvWaveForm;  //驱动卡温度电压	//20181105 多脉冲增加 (19*4+32)*64 bytes
        public UInt32 nMicroJetUint;              //PASS微动时变化单位 Jet//多层之间的MicroJet编译
        public UInt32 nMicroJetCount;             //PASS微动嘴 Jet//
        public int nOverlapJetProcType;                //重叠嘴处理方式  0 交叉 1 后喷头舍弃与前喷头重叠部分
        public float fBrustCycleSec;               //闪喷的周期
        public float fBrustValidSec;               //闪喷的有效时间
        public float fBrustFrequecy;               //闪喷频率
        public int nPrtEncDir;                  //编码计数方向 0 正计数 1 反计数 由AB相位决定 需要保证X正向计数增大
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szLogPath;
        public UInt32 nResMemory;           //保留内存区的大小 单位：MB 兆字节	 限定最小值 256 MB	20180827
        public UInt32 nIoOption;            /*	bit[0]  生成波形预示图		1有效
											bit[1]  允许回流保护功能	1有效
											bit[2]  闪喷使用内存数据	1有效
											bit[3]  OLED特殊供墨方式	1有效
											bit[4]  虚拟编码使能		1有效
											bit[5]  虚拟打印方向取反	1有效
											bit[30]	生成图层信息log		1有效
											bit[31]	生成PASS写数据log	1有效
										//*/
        public LPADIB_PARAM adibParam;			//ADIB参数控制
        public UInt32 nCMSigSrc;            //供墨信号，急停防撞 0 车头卡1  1 车头卡2
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szWavePath;           //波形文件路径 *.rhdat 
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)]
        public UInt32[] nRevParam;		   //20181105 128->127->108->100
    }
    //royal 板卡的基础类库，制作成rstatic最好
    public static class royal//此接口类，所有的成员方法包括成员变量都是static的：20200326//c#的缺省修饰符是public；
    {
        public static PRTJOB_ITEM g_PrtJobItem;
        public static PRTJOB_ITEM g_PrtJobItem1;
        public static LPPassDataItem g_passItem;//第1个被传输数据
        public static LPPassDataItem g_passItem1;//第2个被传输数据
        public static LPPRINTER_INFO g_printerInfo;//测量读取的设备信息——————？？？
        public static LPDRV_WAVEFORM g_waveform;//测量波形的结构体信息——————？？？
        public static LPDRV_WAVEFORM g_tempWaveform;//测量波形的结构体信息——————？？？
        public static LPRYSYS_PARAM g_sys_param;//喷墨打印的所能修改的所有参数:20200326新增，新增这条注释非常重要
        public static LPPRTIMG_LAYER g_prtimg_layer;

        //public static PRTJOB_ITEM _PrtJobItem;
        //public static PRTJOB_ITEM _PrtJobItem1;
        //public static LPPassDataItem _passItem;//第1个被传输数据
        //public static LPPassDataItem _passItem1;//第2个被传输数据
        //public static LPPRINTER_INFO info;//测量读取的设备信息——————？？？
        //public static LPDRV_WAVEFORM waveform;//测量波形的结构体信息——————？？？
        //public static LPDRV_WAVEFORM tempWaveform;//测量波形的结构体信息——————？？？
        //public static LPRYSYS_PARAM sys_param;
        //public static LPPRTIMG_LAYER prtimg_layer;

        ///////设备控制
        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", EntryPoint = "FindWindowEx", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("RYPrtCtler.dll")]
        public static extern int DEV_OpenDevice(IntPtr notifyhand, string szDllFilePath);//C#的string是个对象，对象是1级间接；结构体是0级间接。
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_UpdateParam(ref LPRYSYS_PARAM pParam);
        [DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]//
        public static extern bool DEV_GetDeviceInfo2(ref LPPRINTER_INFO pPrinterInfo);//(a)返回指针的API测试：——理论上不存在疑似存在BUG
        //[DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]//
        //public static extern IntPtr DEV_GetDeviceInfo(ref LPPRINTER_INFO pPrinterInfo);//(a)返回指针的API测试：——理论上不存在疑似存在BUG
        //public static extern bool DEV_GetDeviceInfo2(ref LPPRINTER_INFO pPrinterInfo);// (b) 不返回指针的API测试：
        //public static extern bool DEV_GetDeviceInfo(ref LPPRINTER_INFO pDevInfo);//————测一下GETDEVICEINFO——我测，测如何读出来？？
        //[DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]
        //public static extern IntPtr DEV_GetDeviceInfo2(ref LPPRINTER_INFO pPrinterInfo);//(a)返回指针的API测试：
        ////public static extern bool DEV_GetDeviceInfo2(ref LPPRINTER_INFO pPrinterInfo);// (b) 不返回指针的API测试：
        [DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr DEV_GetDeviceInfo();//LPPRINTER_INFO __stdcall DEV_GetDeviceInfo(); 
        [DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]
        public static extern int DEV_ReloadWaveForm(IntPtr aVcomInWaveFile); //重新加载波形————接口名字要改：传给string//20230323修改：返回波形文件的基准电压
        [DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]
        public static extern bool DEV_DeviceIsConnected();
        [DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]
        public static extern bool DEV_EnableInkAutoSupply(bool bEnable, UInt32 nInkMask);
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DEV_InitDevice(UInt32 nXEncInitVal);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_CloseDevice();
        //[DllImport("RYPrtCtler.dll")]
        //public static extern UInt32 DEV_GetInputIO();
        //[DllImport("RYPrtCtler.dll")]
        //public static extern UInt32 DEV_GetOutputIO();//20200305测试显示存在异常。
        //[DllImport("RYPrtCtler.dll")]       
        //public static extern bool DEV_ShoveInk(UInt32 nCMID, UInt32 nInkMask);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetMcbOutPut(UInt32 nCMID, UInt32 nInkMask);//设置车头卡保留输出
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetVirtualPrint(UInt32 nCMID, UInt32 nXPixelCount, bool bEncDir, UInt32 nClkFreq, ref bool bAbort);	//20190605//20200106新增
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetVirtualPrtEncoder(bool bEnable, bool bEncDir, UInt32 nClkFreq);//20200106新增 
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetInkPump(UInt32 nIoVal);        //设置压墨输出 bit[0]~bit[1]  P1~P2		J28  
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetTimer(UInt32 nTimerID, float fCycleSec, float fValidSec);  //P11~P12 墨水循环//20200329本人新增，注意到缺失：//nTimerID=0=1,是11口和12口
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_EnableTimer(UInt32 nTimerID, bool bEnable);	//P11~P12 墨水循环//20230423新增：

        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetUVLampPosRange(IntPtr nMinPos/*UInt32 nMinPos[2]*/, IntPtr nMaxPos/*UInt32 nMaxPos[2]*/);  //[0] UV1 [1] UV2	//nMinPos,nMaxPos 为软件系统编码位置值                                                                                                     //RYPRTCTLER_API bool				__stdcall DEV_SetPwmParam(float fCycleSec,float fValidSec);	//设置UV灯功率
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetPwmParam(bool bPwmEnable, float fCycleSec, float fValidSec);   //设置UV灯功率
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_EnableUVPosCtlOut(bool bEnable, bool bUVSafeEn);	//打开UV灯位置控制

        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_ResetPrintEncoder(UInt32 nPos); //重新设置当前位置值
        //[DllImport("RYPrtCtler.dll")]
        //public static extern bool DEV_SetUsbOutPut(UInt32 nIoVal, UInt32 nValidBitMask);	//设置保留输出 bit[0]~bit[5]  EO1~EO6	J7~J12  
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetUsbOutPut(UInt32 nIoVal); //设置保留输出 bit[0]~bit[5]  EO1~EO6	J7~J12  
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DEV_GetInput();//获取主板保留输入//读取墨量监测
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DEV_GetUsbOutput();
        //[DllImport("RYPrtCtler.dll")]
        //public static extern UInt32 DEV_GetUsbInput();      //20200117API去掉:
        //[DllImport("RYPrtCtler.dll")]
        //public static extern bool DEV_SetInkPump(UInt32 nIoVal);      //设置压墨输出 bit[0]~bit[1]  P1~P2	J28——清洗泵和压墨泵 
        //[DllImport("RYPrtCtler.dll")]
        //public static extern bool DEV_SetPwmParam(float fCycleSec, float fValidSec);    //设置UV灯功率

        ///////图像打印处理
        [DllImport("RYPrtCtler.dll")]
        //public static extern bool IDP_GetPassItem(UInt32 nLayerIndex, int nPassID);
        public static extern bool IDP_GetPassItem2(UInt32 nLayerIndex, int nPassID, /*IntPtr pPassItem*//*out*//*ref*/ ref LPPassDataItem pPassItem);
        [DllImport("RYPrtCtler.dll")]
        public static extern int IDP_SartPrintJob(ref PRTJOB_ITEM pJobItem);//1级间接
        [DllImport("RYPrtCtler.dll")]
        public static extern bool IDP_StopPrintJob();       
        [DllImport("RYPrtCtler.dll")]
        public static extern int IDP_WriteImgLayerData(ref LPPRTIMG_LAYER lpLayerInfo, IntPtr pSrcBuf/*LPBYTE pSrcBuf[]*/, int nBytes);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool IDP_DoPassPrint(ref LPPassDataItem pPassItem);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool IDP_DoPassPrint2(UInt32 nLayerIndex, int nPassID);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool IDP_PausePassPrint(bool bPause);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool IDP_GetPrintState(ref LPPrtRunInfo pRTinfo);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool IDP_FlashPrtCtl(bool bOpen);

        //////温度电压控制
        [DllImport("RYPrtCtler.dll")]//20230323新建批注：
        public static extern bool DEV_SetWaveStdVoltage(IntPtr fstdVcom, UInt32 nCMID, UInt32 nDrvID);

        [DllImport("RYPrtCtler.dll")]//public static extern bool MCU_SetPhVoltage(ref float[] fstdVcom, UInt32 nCMID, UInt32 nDrvID);
        public static extern bool MCU_SetPhVoltage( /*[MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] ref float[] fstdVcom*/IntPtr fstdVcom, UInt32 nCMID, UInt32 nDrvID);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool MCU_SetPhStdTemp(ref float fstdTmp/*IntPtr fstdTmp*/, UInt32 nCMID, UInt32 nDrvID);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool MCU_GetCurPhVoltage(IntPtr PfVoltage, UInt32 nCMID, UInt32 nDrvID);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool MCU_GetCurPhTemp(IntPtr fCurTemp, UInt32 nCMID, UInt32 nDrvID);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool MCU_GetSate(ref Int16 nState/*IntPtr nState*/, UInt32 nCMID, UInt32 nDrvID);

        //////驱动卡控制
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_SetValidDrvMask(UInt32 nCMID, UInt32 nDrvMask);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_EnableDrvOutput(UInt32 nCMID, UInt32 nDrvID, bool bEnable);

        //////设备状态
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_RecHardwareInfo();
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DEV_GetPrintEncoderValue();//读取设备编码值
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DBG_RegRead(UInt32 nAdr);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DBG_RegWrite(UInt32 nAdr, UInt32 nDataVal);
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DBG_SendPrtCommand(UInt32 nPrtID, UInt32 nCmdData);
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DBG_GetPrtStatus(UInt32 nPrtID, UInt32 nIndex);
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DBG_GetPrtInfo(UInt32 nPrtID, UInt32 nInfoIndex);//20200718批注：输入参数为：0,7。其中，0为1号车头板，7为保留输出
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DBG_SendDrvCommand(UInt32 nPrtID, UInt32 nDrvID, UInt32 nCmdData);
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DBG_GetDrvStatus(UInt32 nPrtID, int nDrvID);
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DBG_GetDrvInfo(UInt32 nPrtID, UInt32 nDrvID, UInt32 nInfoIndex);

        //////////////运动控制
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_InitAxis(UInt32 nAxis, UInt32 nCtlFlag);
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DEM_GetAxisEncodeVal(UInt32 nAxis);//获取轴编码器位置：200107//20220506:可用于设计自动校准逻辑
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_SetAxisEncodeVal(UInt32 nAxis, UInt32 nEncPos);//20220506:可用于设计自动校准逻辑
        [DllImport("RYPrtCtler.dll")]//20200522新增：
        public static extern bool DEM_EnableZeroResetEncVal(UInt32 nAxis, bool bEnable);  //Set Encoder Value: 0x10000 //20200522新增
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_EnableDYOutPut(bool bY1Out, bool bY2Out);//20220506修改：对于Y1/Y2轴的运动非常关键，X轴不存在同步运动这一概念
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DEM_GetAxisLmtZeroState(UInt32 nAxis);//20220513批注：其中，0x10/*零位*/+0x10/*零位*/+0x10/*零位*/；//获取三个轴的限位寄存器：底层接口已做了12bit移位处理：200107
        [DllImport("RYPrtCtler.dll")]
        public static extern UInt32 DEM_GetRevPluse(UInt32 nAxis);//获取剩余脉冲数：200107
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_SetDYSyncCtlMaxOff(UInt32 nOffset);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_Run(UInt32 nAxis, bool bDir, UInt32 nPlsSpd, int nPlsCnt, UInt32 nCtrFlag);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_QueryDYSyncError(ref UInt32 pOffset);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_EnableAxisRun(bool bEnable);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_StopAxisRun(bool bImmeStop, UInt32 nAxisMask);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_AxisIsRuning(UInt32 nAxis, ref bool pDir, ref UInt32 pRPos);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEM_EnableLimitFun(UInt32 nAxis, bool bEnable);//20220511新建：Y轴限位使能 //RYPRTCTLER_API bool __stdcall  DEM_EnableLimitFun(UINT nAxis, BOOL bEnable);//20220511新建：Y轴限位使能

        //////ADIB状态   nOption定义： bit[0] 版本 bit[1] 温度 bit[2] 负压 bit[3] 输入 bit[4] 电压 bit[8] 回读I2C负压 bit[16] 保持到I2C 
        //RYPRTCTLER_API bool	__stdcall  DEV_AdibControl(LPADIB_PARAM lParam,UINT nOption,BOOL bSetParam);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DEV_AdibControl(ref LPADIB_PARAM lParam, UInt32 nOption, bool bSetParam, ref bool bAbort/*IntPtr bAbort*/ /*BOOL* bAbort*/);
        [DllImport("RYPrtCtler.dll")]
        public static extern int DDR_StartPrint(UInt32 nXPrecision, ref UInt32 nPdMemAdr/*UInt32 nPdMemAdr[]*/, UInt32 nPixelWidth, UInt32 nStartEncPos, bool bPrtDir);    //20190912
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DDR_WriteImgData(IntPtr pdata/*LPBYTE pdata*/, int nBytes, UInt32 nMemAdr);
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DDR_AbortWriteData();
        [DllImport("RYPrtCtler.dll")]
        public static extern bool DDR_WriteIsOver();


        /// <summary>
        /// 本API为专供测试C# .NET平台与C++底层功能模块之间的通讯功能
        /// </summary>
        /// <param name="waveform"></param>
        /// <returns></returns>
        [DllImport("RYPrtCtler.dll")]
        public static extern int Add(int A, int B);
        [DllImport("RYPrtCtler.dll")]
        public static extern int Sub(int A, int B);
        [DllImport("RYPrtCtler.dll", CharSet = CharSet.Unicode)]//但是个人觉得实用Unicode封送更好，符合趋势           
        //public static extern bool MyStructTest( ref LPDRV_WAVEFORM waveform);
        //public static extern bool MyStructTest(ref LPRYSYS_PARAM sys_param);
        public static extern bool MyStructTest(ref LPPRTIMG_LAYER prtimg_layer);
    }
}
