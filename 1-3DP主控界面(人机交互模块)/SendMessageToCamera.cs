using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BinderJetting
{
    public partial class SendMessageToCamera : Form
    {
        public SendMessageToCamera(bool StartMode)
        {
            if (StartMode)//StartMode为true时，初始化视图资源
            {
                InitializeComponent();
                LoadJsonFile();
                InitKidFormWithMonitorPrintParam();
            }
            else//不初始化视图资源
            {
                LoadJsonFile();
            }
            try//尝试打开线存的互斥锁
            {
                bool ReadedFlag = Mutex.TryOpenExisting("testmapmutex", out mutex);//系统中唯一的：相当于系统中的一个唯一的洗手间 //new Mutex(true, "testmapmutex", out mutexCreated);
                if (ReadedFlag == true)//读取成功
                { }
                else//读取失败或者存在已破损
                {
                    mutex = new Mutex(true, "testmapmutex", out mutexCreated);
                    mutex.ReleaseMutex();
                    ////MessageBox.Show("提示：不存在指定mutex:" + "testmapmutex,已经重新创建");//已经过充分检验，不需要提醒本信息！！！
                }
            }
            catch (Exception e) {}
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveJsonFile();
            mmf.Dispose();//与using不需要同时使用，mmf在C#中使用更加方便
            mutex.Dispose();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SendMessageFromSharedMemory(true, 0, 0);//参数0:初始无效参数
        }

        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择文件路径";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                k_MonitorPrintParam.MonitorRecordPath/*ParentPATH*/ = dialog.SelectedPath;
                textBox1.Text = dialog.SelectedPath;
            }
        }

        #region 监控模块指令发送相关数据
        public MonitorPrintParam k_MonitorPrintParam;
        string[] ProcessStampNames = new string [13]{"NULL","LeaveCleanStation","1PASS-AfterCut", "2PASS-AfterCut","3PASS-AfterCut", "4PASS-AfterCut", "5PASS-AfterCut","BackCleanStation",
            "LeavePowderStation", "Recoat-InAdvanceCut","Recoat-RightArrived","Recoat-InRecedeCut","BackPowderStation"};//总共13项目

        const int MMF_MAX_SIZE = 1024;  // allocated memory for this memory mapped file (bytes)
        const int MMF_VIEW_SIZE = 1024; // how many bytes of the allocated memory can this process access
        //string ParentPATH = null;//监控记录的父节点
        string RecordPATH = null;//默认为："[Record][230107][打印任务名称]"//20230107新建批注：（i）输入名称;（ii）有我新建的3DP的文件格式！
        string RecordLayerNum = null;//监控记录的层序号
        MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen("testmap", MMF_MAX_SIZE, MemoryMappedFileAccess.ReadWrite);//20230106批注：共享内存只要有进程在用，就不会在系统中删除；因此，直接采用CreateOrOpen即可
        Mutex mutex = null;
        bool mutexCreated = false;
        #endregion

        public void SendMessageFromSharedMemory(bool StartMode, int RecordLayerNumParam, int ProcessStampParam)
        {
            if (StartMode==false)//从非窗口启动状态中发送拍照指令
            {
                //（1）检查保存文件夹是否符合要求
                #region 保存文件夹路径检查及更新
                string TimeStamp = "["
                   + (DateTime.Now.Year - 2000).ToString("D2") + DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") + "]";//年月日
                string PrinterTaskName = "[" + k_MonitorPrintParam.PrintJobName + "]";
                string NewRecordPATH = k_MonitorPrintParam.MonitorRecordPath/*ParentPATH*/+ "\\[Record]" + TimeStamp + PrinterTaskName;
                //(2) 检查是否是有效的路径名称
                //(2) 判断该路径是否创建过，未创建则重新创建
                if (NewRecordPATH != "")
                {
                    Regex regex = new Regex(@"^([a-zA-Z]:)?[^/:*?""<>|,]*$");
                    Match m = regex.Match(NewRecordPATH);
                    if (!m.Success)
                    {
                        MessageBox.Show("非法的文件保存路径，请重新选择或输入！");
                        return;
                    }
                    regex = new Regex(@"^[^*?""<>|,]+$");//(@"^[^/:*?""<>|,]+$");
                    m = regex.Match(NewRecordPATH);
                    if (!m.Success)
                    {
                        MessageBox.Show("请勿在文件名中包含 / : * ？ \" < > | 等字符，请重新输入有效文件名！");
                        return;
                    }
                    //判断是否为新路径名称
                    if (RecordPATH != NewRecordPATH)
                    {
                        RecordPATH = NewRecordPATH;//更新路径名称
                    }
                }
                #endregion 保存文件夹路径检查及更新

                //（2）编码数据并发送指令// this is what we want to write to the memory mapped file
                MonitoringMessage.Message message1 = new MonitoringMessage.Message();
                {
                    mutex.WaitOne();
                    // creates a stream for this process, which allows it to write data from offset 0 to 1024 (whole memory)
                    using (MemoryMappedViewStream stream = mmf.CreateViewStream(0, MMF_VIEW_SIZE))
                    {
                        //（1）数据编码 // this is what we want to write to the memory mapped file
                        if (RecordLayerNumParam == 0)
                        {
                            RecordLayerNum = "0-手动调试";
                        }
                        else
                        {
                            RecordLayerNum = RecordLayerNumParam.ToString()/*textBox3.Text*/;//准备监控记录的层序号
                        }
                        message1.MonitorRecordPath = RecordPATH;//父路径+子路径
                        message1.CurrentLayer = RecordLayerNum;//准备监控记录的层序号
                        message1.ProcessStamp = ProcessStampNames[ProcessStampParam]/*textBox4.Text*/;//记录文件名要素之一
                        message1.TriggerFlag = 1;//执行触发标志

                        //（2）数据发送// serialize the variable 'message1' and write it to the memory mapped file
                        BinaryFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(stream, message1);
                        stream.Seek(0, SeekOrigin.Begin); // sets the current position back to the beginning of the stream
                    }
                    mutex.ReleaseMutex();
                }
            }
            else//从窗口启动状态中发送拍照指令
            {
                //（1）检查保存文件夹是否符合要求
                #region 保存文件夹路径检查及更新
                string TimeStamp = "["
                   + (DateTime.Now.Year - 2000).ToString("D2") + DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2")
                   + "]";//年月日
                string PrinterTaskName = "[" + textBox2.Text + "]";
                string NewRecordPATH = k_MonitorPrintParam.MonitorRecordPath/*ParentPATH*/+ "\\[Record]" + TimeStamp + PrinterTaskName;
                //(2) 检查是否是有效的路径名称
                //(2) 判断该路径是否创建过，未创建则重新创建
                if (NewRecordPATH != "")
                {
                    Regex regex = new Regex(@"^([a-zA-Z]:)?[^/:*?""<>|,]*$");
                    Match m = regex.Match(NewRecordPATH);
                    if (!m.Success)
                    {
                        MessageBox.Show("非法的文件保存路径，请重新选择或输入！");
                        return;
                    }
                    regex = new Regex(@"^[^*?""<>|,]+$");//(@"^[^/:*?""<>|,]+$");
                    m = regex.Match(NewRecordPATH);
                    if (!m.Success)
                    {
                        MessageBox.Show("请勿在文件名中包含 / : * ？ \" < > | 等字符，请重新输入有效文件名！");
                        return;
                    }

                    //判断是否为新路径名称
                    if (RecordPATH != NewRecordPATH)
                    {
                        RecordPATH = NewRecordPATH;//更新路径名称
                    }
                }
                #endregion 保存文件夹路径检查及更新

                //（2）编码数据并发送指令
                // this is what we want to write to the memory mapped file
                MonitoringMessage.Message message1 = new MonitoringMessage.Message();
                // creates the memory mapped file which allows 'Reading' and 'Writing'
                //using (MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen("mmf1", MMF_MAX_SIZE, MemoryMappedFileAccess.ReadWrite))
                {
                    mutex.WaitOne();
                    // creates a stream for this process, which allows it to write data from offset 0 to 1024 (whole memory)
                    using (MemoryMappedViewStream stream = mmf.CreateViewStream(0, MMF_VIEW_SIZE))
                    {
                        //（1）数据编码
                        // this is what we want to write to the memory mapped file
                        //MonitoringMessage.Message message1 = new MonitoringMessage.Message();
                        RecordLayerNum = textBox3.Text;//准备监控记录的层序号
                        message1.MonitorRecordPath = RecordPATH;//父路径+子路径
                        message1.CurrentLayer = RecordLayerNum;//准备监控记录的层序号
                        message1.ProcessStamp = textBox4.Text;//记录文件名要素之一
                        message1.TriggerFlag = 1;//执行触发标志

                        //（2）数据发送
                        // serialize the variable 'message1' and write it to the memory mapped file
                        BinaryFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(stream, message1);
                        stream.Seek(0, SeekOrigin.Begin); // sets the current position back to the beginning of the stream
                    }
                    mutex.ReleaseMutex();
                }
            }
        }

        public void InitKidFormWithMonitorPrintParam()
        {
            //监控模块接口参数
            textBox1.DataBindings.Add("Text", k_MonitorPrintParam, "MonitorRecordPath", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);// 监控视频保存路径
            textBox2.DataBindings.Add("Text", k_MonitorPrintParam, "PrintJobName", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//打印任务名称：

            checkBox1.DataBindings.Add("Checked", k_MonitorPrintParam, "JettingBinderBedMonitorFlags1", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox2.DataBindings.Add("Checked", k_MonitorPrintParam, "JettingBinderBedMonitorFlags2", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox3.DataBindings.Add("Checked", k_MonitorPrintParam, "JettingBinderBedMonitorFlags3", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox4.DataBindings.Add("Checked", k_MonitorPrintParam, "JettingBinderBedMonitorFlags4", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox5.DataBindings.Add("Checked", k_MonitorPrintParam, "JettingBinderBedMonitorFlags5", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox6.DataBindings.Add("Checked", k_MonitorPrintParam, "JettingBinderBedMonitorFlags6", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox7.DataBindings.Add("Checked", k_MonitorPrintParam, "JettingBinderBedMonitorFlags7", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
  
            checkBox8.DataBindings.Add("Checked", k_MonitorPrintParam, "RecoatPowderBedMonitorFlags1", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox9.DataBindings.Add("Checked", k_MonitorPrintParam, "RecoatPowderBedMonitorFlags2", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox10.DataBindings.Add("Checked", k_MonitorPrintParam, "RecoatPowderBedMonitorFlags3", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox11.DataBindings.Add("Checked", k_MonitorPrintParam, "RecoatPowderBedMonitorFlags4", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点
            checkBox12.DataBindings.Add("Checked", k_MonitorPrintParam, "RecoatPowderBedMonitorFlags5", true /*false*/, DataSourceUpdateMode.OnPropertyChanged);//20230113新建：喷墨监控拍摄位点

        }

        public bool LoadJsonFile()//20201029新增：实例化类之后，不一定必要显示
        {
            string JsonPath = "";
            try//20201020新增：读取JSON配置文件
            {
                //(4)20200807批注：加载打印策略参数
                JsonPath = System.Windows.Forms.Application.StartupPath + @"\MonitorPrint-Configuration.json";//json配置文件：启动目录
                k_MonitorPrintParam = ObjectCopier.LoadJson<MonitorPrintParam>(JsonPath);

                return true;
            }
            catch (Exception)
            {
                if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
                {
                    File.Create(JsonPath);//不存在则创建文件
                    MessageBox.Show("监控配置文件不存在，已创建");
                }
                else
                {
                    MessageBox.Show("监控配置文件加载异常");
                }
                k_MonitorPrintParam = new MonitorPrintParam();

                return false;
            }
        }

        public void SaveJsonFile()
        {
            string JsonPath = System.Windows.Forms.Application.StartupPath + @"\MonitorPrint-Configuration.json";//json配置文件：启动目录
            if (!File.Exists(JsonPath))// 返回bool类型，存在返回true，不存在返回false
            {
                File.Create(JsonPath);//不存在则创建文件
            }
            string json = JsonConvert.SerializeObject(k_MonitorPrintParam, Formatting.Indented);
            File.WriteAllText(JsonPath, json);
        }

    }

    public class MonitorPrintParam : INotifyPropertyChanged, ICloneable//C#中，通知类的属性值已经更改，可以避免大量的通用事件的使用；其中关键是属性的理解及和lambda表达式的使用方法
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
                                                                 //表达式树：lambda表达式，为一段可执行代码————SQL数据库查询的时候使用
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")//20200225：【CallerMemberName】特性精华：每次调用 TraceMessage 方法时，调用方信息将替换为可选参数的参数。
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));//20200225：propertyName：是1个附带参数
        }

        /// <summary>
        ///  //监控模块接口参数
        /// </summary>
        /// 
        public string m_sMonitorRecordPath = null;// 监控视频保存路径
        public string m_sPrintJobName = null;//固化策略类型：默认0为UV固化方式，1为IR固化方式

        public bool [] m_anJettingBinderBedMonitorFlags = new bool[12]{false, false, false, false, false, false, false, false, false, false, false, false};//20230113新建：喷墨监控拍摄微店
        ////public bool m_anJettingBinderBedMonitorFlags1 = false;//20230113新建：喷墨监控拍摄微店
        ////public bool m_anJettingBinderBedMonitorFlags2 = false;//20230113新建：喷墨监控拍摄微店
        ////public bool m_anJettingBinderBedMonitorFlags3 = false;//20230113新建：喷墨监控拍摄微店
        ////public bool m_anJettingBinderBedMonitorFlags4 = false;//20230113新建：喷墨监控拍摄微店
        ////public bool m_anJettingBinderBedMonitorFlags5 = false;//20230113新建：喷墨监控拍摄微店
        ////public bool m_anJettingBinderBedMonitorFlags6 = false;//20230113新建：喷墨监控拍摄微店
        ////public bool m_anJettingBinderBedMonitorFlags7 = false;//20230113新建：喷墨监控拍摄微店

        ////public bool m_anRecoatPowderBedMonitorFlags1 = false;//20230113新建：铺粉监控拍摄微店
        ////public bool m_anRecoatPowderBedMonitorFlags2 = false;//20230113新建：铺粉监控拍摄微店
        ////public bool m_anRecoatPowderBedMonitorFlags3 = false;//20230113新建：铺粉监控拍摄微店
        ////public bool m_anRecoatPowderBedMonitorFlags4 = false;//20230113新建：铺粉监控拍摄微店
        ////public bool m_anRecoatPowderBedMonitorFlags5 = false;//20230113新建：铺粉监控拍摄微店

        ////20230110新增：监控视频保存路径
        public string MonitorRecordPath//
        {
            get { return this.m_sMonitorRecordPath; }
            set { if (value != this.m_sMonitorRecordPath) { this.m_sMonitorRecordPath = value; NotifyPropertyChanged(); } }
        }
        ////20230110新增：打印任务名称：
        public string PrintJobName
        {
            get { return this.m_sPrintJobName; }
            set { if (value != this.m_sPrintJobName) { this.m_sPrintJobName = value; NotifyPropertyChanged(); } }
        }

        //20230113新建：喷墨监控拍摄位点
        public bool JettingBinderBedMonitorFlags1
        {
            get { return this.m_anJettingBinderBedMonitorFlags[0]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[0]) { this.m_anJettingBinderBedMonitorFlags[0] = value; NotifyPropertyChanged(); } }
        }
        public bool JettingBinderBedMonitorFlags2
        {
            get { return this.m_anJettingBinderBedMonitorFlags[1]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[1]) { this.m_anJettingBinderBedMonitorFlags[1] = value; NotifyPropertyChanged(); } }
        }
        public bool JettingBinderBedMonitorFlags3
        {
            get { return this.m_anJettingBinderBedMonitorFlags[2]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[2]) { this.m_anJettingBinderBedMonitorFlags[2] = value; NotifyPropertyChanged(); } }
        }
        public bool JettingBinderBedMonitorFlags4
        {
            get { return this.m_anJettingBinderBedMonitorFlags[3]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[3]) { this.m_anJettingBinderBedMonitorFlags[3] = value; NotifyPropertyChanged(); } }
        }
        public bool JettingBinderBedMonitorFlags5
        {
            get { return this.m_anJettingBinderBedMonitorFlags[4]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[4]) { this.m_anJettingBinderBedMonitorFlags[4] = value; NotifyPropertyChanged(); } }
        }
        public bool JettingBinderBedMonitorFlags6
        {
            get { return this.m_anJettingBinderBedMonitorFlags[5]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[5]) { this.m_anJettingBinderBedMonitorFlags[5] = value; NotifyPropertyChanged(); } }
        }
        public bool JettingBinderBedMonitorFlags7
        {
            get { return this.m_anJettingBinderBedMonitorFlags[6]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[6]) { this.m_anJettingBinderBedMonitorFlags[6] = value; NotifyPropertyChanged(); } }
        }

        //20230113新建：铺粉监控拍摄微店
        public bool RecoatPowderBedMonitorFlags1
        {
            get { return this.m_anJettingBinderBedMonitorFlags[7]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[7]) { this.m_anJettingBinderBedMonitorFlags[7] = value; NotifyPropertyChanged(); } }
        }
        public bool RecoatPowderBedMonitorFlags2
        {
            get { return this.m_anJettingBinderBedMonitorFlags[8]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[8]) { this.m_anJettingBinderBedMonitorFlags[8] = value; NotifyPropertyChanged(); } }
        }
        public bool RecoatPowderBedMonitorFlags3
        {
            get { return this.m_anJettingBinderBedMonitorFlags[9]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[9]) { this.m_anJettingBinderBedMonitorFlags[9] = value; NotifyPropertyChanged(); } }
        }
        public bool RecoatPowderBedMonitorFlags4
        {
            get { return this.m_anJettingBinderBedMonitorFlags[10]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[10]) { this.m_anJettingBinderBedMonitorFlags[10] = value; NotifyPropertyChanged(); } }
        }
        public bool RecoatPowderBedMonitorFlags5
        {
            get { return this.m_anJettingBinderBedMonitorFlags[11]; }
            set { if (value != this.m_anJettingBinderBedMonitorFlags[11]) { this.m_anJettingBinderBedMonitorFlags[11] = value; NotifyPropertyChanged(); } }
        }

    }

    /////// <summary>
    /////// Reference Article http://www.codeproject.com/KB/tips/SerializedObjectCloner.aspx
    /////// Provides a method for performing a deep copy of an object.
    /////// Binary Serialization is used to perform the copy.
    /////// </summary>
    ////public static class ObjectCopier
    ////{
    ////    /// <summary>
    ////    /// Perform a deep Copy of the object.
    ////    /// </summary>
    ////    /// <typeparam name="T">The type of object being copied.</typeparam>
    ////    /// <param name="source">The object instance to copy.</param>
    ////    /// <returns>The copied object.</returns>
    ////    public static T Clone<T>(this T source)
    ////    {
    ////        var serialized = JsonConvert.SerializeObject(source);
    ////        return JsonConvert.DeserializeObject<T>(serialized);
    ////    }
    ////    /// <summary>
    ////    /// 泛型方式，实现JSON文件加载
    ////    /// </summary>
    ////    /// <typeparam name="T"></typeparam>
    ////    /// <param name="jsonPath"></param>
    ////    /// <returns></returns>
    ////    public static T LoadJson<T>(this string jsonPath)
    ////    {
    ////        using (StreamReader r = new StreamReader(jsonPath))
    ////        {
    ////            string json = r.ReadToEnd();
    ////            return JsonConvert.DeserializeObject<T>(json);
    ////        }
    ////    }
    ////}
}
