using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;//DLLImport导入DLL包，后面添加命名空间
using System.IO;

namespace BinderJetting
{
    public partial class CalibrationMoudle : Form
    {
        public CalibrationMoudle()
        {
            InitializeComponent();
            //（1-0）更新校准系统参数
            royalCorrectSystem.g_LPCAL_PARAM.nGroups = 7;
            royalCorrectSystem.g_LPCAL_PARAM.nDataLine = 128;
            royalCorrectSystem.g_LPCAL_PARAM.nSplits = 4;
            royalCorrectSystem.g_LPCAL_PARAM.nOverLapJets = 2;
            royalCorrectSystem.g_LPCAL_PARAM.szFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart\"/*"D:\\1111\\TstFiles"*/;//世彪批注：生成的校准图位置（20200722）
            royalCorrectSystem.PD_UpdatParam(ref royalCorrectSystem.g_LPCAL_PARAM);

            //（1-1）设置默认打开路径及文件过滤格式
            this.Refresh();
            string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";//输入的CLI文件的存放目录。      
            //（1-2）判断打开的路径是否存在
            if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\CalibrationChart"))
            {
                Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\CalibrationChart");//创建路径
            }

            int UniversalOffset = 0;//20210319新建修改：
            int UniversalOffsetY = 0;//20210319新建修改：
            royalCorrectSystem.PD_GenPhStatus(UniversalOffset);//喷嘴状态
            royalCorrectSystem.PD_GenBiDirOffset(UniversalOffset, UniversalOffsetY);//X 往返差
            royalCorrectSystem.PD_GenVertivalCheck(UniversalOffset);//垂直校准
            royalCorrectSystem.PD_GenColorOffset(0, UniversalOffset);//X套色 综合
            royalCorrectSystem.PD_GenColorOffset(1, UniversalOffset);//X套色 综合

            FileStream fs = new System.IO.FileStream(CalibrationFilePath + @"\APCLROT-0.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect1.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\APCLROT-1.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect2.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\APRETDIV.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect3.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\STATUS.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect4.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\Vertical.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect5.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            this.Refresh();
        }

        /// <summary>
        /// 20200723批注：更新校准图参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCorrectParam_Click(object sender, EventArgs e)
        {
#if false//20200723批注：通过PCS读取校准图生成参数
            int nRet = ::DEV_OpenDevice(m_hWnd, (unsigned char *)W2A(m_szFireWarePath.GetBuffer(0)));	//加载PCS文件，要在这个指定路径下
            ::DEV_InitDevice(0x100000); //处理设备信息，赋值
            LPPRINTER_INFO lpDevInfo = ::DEV_GetDeviceInfo();   //获取当前信息
            lpDevInfo->nColors;
            royalCorrectSystem.g_LPCAL_PARAM.nDataLine = lpDevInfo->nDatalines;
            royalCorrectSystem.g_LPCAL_PARAM.nGroups = lpDevInfo->nGroups;
            royalCorrectSystem.g_LPCAL_PARAM.nSplits = lpDevInfo->nSplits;
            royalCorrectSystem.g_LPCAL_PARAM.nOverLapJets = lpDevInfo->nOverLapJets;
#else   //20200723批注：通过赋值生成校准图参数
            royalCorrectSystem.g_LPCAL_PARAM.nGroups = 7;
            royalCorrectSystem.g_LPCAL_PARAM.nDataLine = 128;
            royalCorrectSystem.g_LPCAL_PARAM.nSplits = 4;
            royalCorrectSystem.g_LPCAL_PARAM.nOverLapJets = 2;
            royalCorrectSystem.g_LPCAL_PARAM.szFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart\"/*"D:\\1111\\TstFiles"*/;//世彪批注：生成的校准图位置（20200722）
#endif
            royalCorrectSystem.PD_UpdatParam(ref royalCorrectSystem.g_LPCAL_PARAM);
            //::PD_UpdatParam(&_koceladj);	//更新校准图参数
            //初始值 D 盘根目录
        }
        /// <summary>
        /// 20200723批注：生成校准图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCreateCorrect_Click(object sender, EventArgs e)
        {
            //（1-1）设置默认打开路径及文件过滤格式
            this.Refresh();
            string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart";//输入的CLI文件的存放目录。      
            //（1-2）判断打开的路径是否存在
            if (!Directory.Exists(System.Windows.Forms.Application.StartupPath + @"\CalibrationChart"))
            {
                Directory.CreateDirectory(System.Windows.Forms.Application.StartupPath + @"\CalibrationChart");//创建路径
            }

            int UniversalOffset = 0;//20210319新建修改：
            int UniversalOffsetY = 0;//20210319新建修改：
            royalCorrectSystem.PD_GenPhStatus(UniversalOffset);//喷嘴状态
            royalCorrectSystem.PD_GenBiDirOffset(UniversalOffset, UniversalOffsetY);//X 往返差
            royalCorrectSystem.PD_GenVertivalCheck(UniversalOffset);//垂直校准
            royalCorrectSystem.PD_GenColorOffset(0, UniversalOffset);//X套色 综合
            royalCorrectSystem.PD_GenColorOffset(1, UniversalOffset);//X套色 综合 
            FileStream fs = new System.IO.FileStream(CalibrationFilePath + @"\APCLROT-0.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect1.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\APCLROT-1.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect2.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\APRETDIV.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect3.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\STATUS.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect4.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs = new System.IO.FileStream(CalibrationFilePath + @"\Vertical.bmp", FileMode.Open, FileAccess.Read);
            PicBoxCorrect5.Image = System.Drawing.Image.FromStream(fs);
            fs.Close();

            this.Refresh();
        }

        private void PrintCorrectionFigure(object sender, EventArgs e)
        {
            int CorrectionStep = (int)(sender as Control).Tag;
            switch (CorrectionStep)
            {
                case 1://20200508新建：启动并打印垂直校准图

                    break;
                case 2://20200508新建：启动并打印往返差校准图1

                    break;
                case 3://20200508新建：启动并打印往返差校准图1

                    break;
            }
        }

        ///// <summary>
        ///// 20210304新增修改：注释掉，移植到主界面
        ///// </summary>
        //[StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        //public struct LPCAL_PARAM//20200723批注：校准图参数结构体
        //{
        //    public int nGroups;        //喷头组数
        //    public int nSplits;        //喷头组内喷嘴列数
        //    public int nDataLine;      //喷头单列的喷孔数
        //    public int nOverLapJets;   //重叠嘴数
        //    public bool bVReverse;     //上下翻转
        //    public bool bHReverse;     //左右翻转
        //    public bool bCarRightSide; //标尺正负反向
        //    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        //    public string szFilePath;  //生成校准图所在文件路径
        //}

        //public static class royalCorrectSystem//此接口类，所有的成员方法包括成员变量都是static的：20200326//c#的缺省修饰符是public；
        //{
        //    public static LPCAL_PARAM g_LPCAL_PARAM;//20200723批注：校准图参数结构体

        //    ///////校准图生成
        //    [DllImport("calibration.dll")]
        //    public static extern bool PD_UpdatParam(ref LPCAL_PARAM lpParam);      //校准图参数
        //    [DllImport("calibration.dll")]
        //    public static extern bool PD_GenColorOffset(int nDir); //X套色 综合
        //    [DllImport("calibration.dll")]
        //    public static extern bool PD_GenBiDirOffset();         //X 往返差
        //    [DllImport("calibration.dll")]
        //    public static extern bool PD_GenPhStatus();                //喷嘴状态
        //    [DllImport("calibration.dll")]
        //    public static extern bool PD_GenVertivalCheck();        //垂直校准
        //}
    }

}
