using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//————————————————
//版权声明：本文为CSDN博主「blueday406」的原创文章，遵循CC 4.0 BY-SA版权协议，转载请附上原文出处链接及本声明。
//原文链接：https://blog.csdn.net/blueday406/article/details/105009359

namespace BinderJetting
{
    public class LogHelper
    {
        //public static readonly log4net.ILog logInfo = log4net.LogManager.GetLogger("InfoLog");
        //public static readonly log4net.ILog logError = log4net.LogManager.GetLogger("Error");
        public static readonly log4net.ILog log = log4net.LogManager.GetLogger("InfoLog");


        ///// <summary>
        ///// 普通日志
        ///// </summary>
        ///// <param name="message">日志内容</param>
        //public static void Info(string message)
        //{
        //    if (logInfo.IsInfoEnabled)
        //    {
        //        logInfo.Info(message);
        //    }
        //}
        ///// <summary>
        ///// 错误日志
        ///// </summary>
        ///// <param name="message">错误日志</param>
        //public static void Error(string message)
        //{
        //    if (logError.IsErrorEnabled)
        //    {
        //        logError.Error(message);
        //    }
        //}


        public static void log4net_demo()//20210327新增：log4net世彪新增
        {
            FileInfo fi = new FileInfo("log4net.xml");
            log4net.Config.XmlConfigurator.Configure(fi);
            log4net.GlobalContext.Properties["host"] = Environment.MachineName;
        }

        /// <summary>
        /// 普通日志
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void Log(string message)
        {
            if (log.IsInfoEnabled)
            {
                log.Info(message);
            }
        }
    }
}
