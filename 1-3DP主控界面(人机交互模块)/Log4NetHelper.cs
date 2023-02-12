using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;

namespace BinderJetting/*LaserADD_BinderJetter*///20230212新建：打印设备日志系统
{
    public class Log4Net//Log4Net通用记录类/*Log4NetHelper*/
    {
        private static string filepath = AppDomain.CurrentDomain.BaseDirectory + @"\SysLog\";

        private static readonly log4net.ILog logComm = log4net.LogManager.GetLogger("AppLog");

        static void AppLog()
        {
            log4net.Config.XmlConfigurator.Configure(new FileInfo("log4net.config"));

            if (!Directory.Exists(filepath))
            {
                Directory.CreateDirectory(filepath);
            }
        }

        /// <summary>
        /// 输出系统日志
        /// </summary>
        /// <param name="msg">信息内容</param>
        /// <param name="source">信息来源</param>
        private static void WriteLog(string msg, /*bool isWrite,*/ Action<object> action)
        {
            if (true/*isWrite*/)
            {
                string filename = $"AppLog_{action.Method.Name}_{ DateTime.Now.ToString("yyyyMMdd_HH")}.log";
                var repository = LogManager.GetRepository();

                #region MyRegion
                var appenders = repository.GetAppenders();
                if (appenders.Length > 0)
                {
                    RollingFileAppender targetApder = null;
                    foreach (var Apder in appenders)
                    {
                        if (Apder.Name == "AppLog")
                        {
                            targetApder = Apder as RollingFileAppender;
                            break;
                        }
                    }
                    if (targetApder.Name == "AppLog")//如果是文件输出类型日志，则更改输出路径
                    {
                        if (targetApder != null)
                        {
                            if (!targetApder.File.Contains(filename))
                            {
                                targetApder.File = @"SysLog\" + filename;
                                targetApder.ActivateOptions();
                            }
                        }
                    }
                }

                #endregion
                action(msg);
                //logComm.Error(msg + "\n");
            }
        }
        public static void Error/*WriteError*/(string msg)
        {
            WriteLog(msg, logComm.Error);
        }
        public static void Info/*WriteInfo*/(string msg)
        {
            WriteLog(msg, logComm.Info);
        }
        public static void Warn/*WriteWarn*/(string msg)
        {
            WriteLog(msg, logComm.Warn);
        }
        public static void Debug/*WriteDebug*/(string msg)//20230212世彪新增：
        {
            WriteLog(msg, logComm.Debug);
        }
        public static void Fatal/*WriteFatal*/(string msg)//20230212世彪新增：
        {
            WriteLog(msg, logComm.Fatal);
        }
    }
}
