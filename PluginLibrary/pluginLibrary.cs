using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using PluginInterface;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Formatters.Binary;

namespace PluginLibrary
{
    /************************************标准用例:栅格数据处理接口(实现)*******************************************/
    /************************************标准用例:栅格数据处理接口(实现)*******************************************/
    /************************************标准用例:栅格数据处理接口(实现)*******************************************/
    public class MyPlugin : IAlgorithmPlugin
    {
        public string Name => "单点栅格数据生成算法";

        public void Execute()
        {
            MessageBox.Show("MyPlugin is executing.");
        }
    }
    public class MyPlugin2 : IAlgorithmPlugin
    {
        public string Name => "灰度栅格数据生成算法";

        public void Execute()
        {
            MessageBox.Show("MyPlugin is executing.");
        }
    }
    public class MyPlugin3 : IAlgorithmPlugin
    {
        public string Name => "轮廓增强栅格数据生成算法";

        public void Execute()
        {
            MessageBox.Show("MyPlugin is executing.");
        }
    }
    public class MyPlugin4 : IAlgorithmPlugin
    {
        public string Name => "灰度栅格数据羽化搭接算法";

        public void Execute()
        {
            MessageBox.Show("MyPlugin is executing.");
        }
    }
    public class MyPlugin5 : IAlgorithmPlugin
    {
        public string Name => "校准图栅格数据生成算法";

        public void Execute()
        {
            MessageBox.Show("MyPlugin is executing.");
        }
    }
    /************************************标准用例:Mutex外部接口(实现)*******************************************/
    /************************************标准用例:Mutex外部接口(实现)*******************************************/
    /************************************标准用例:Mutex外部接口(实现)*******************************************/
    public class MutexCommandCommunicationPlugin : IMutexCommandCommunicationPlugin//20231226新建：相机控制指令，内部通过跨进程Mutex方式实现
    {
        #region 监控模块指令发送相关数据
        //public MonitorPrintParam k_MonitorPrintParam;//20231226注释:

        string[] ProcessStampNames = new string[13]{"NULL","LeaveCleanStation","1PASS-AfterCut", "2PASS-AfterCut","3PASS-AfterCut", "4PASS-AfterCut", "5PASS-AfterCut","BackCleanStation",
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

        public string Name => "Mutex外部相机接口实现";  // 插件的名称
        public async Task ConnectAsync(string brokerAddress){}
        public void SendMessageFromSharedMemory(bool StartMode, int RecordLayerNumParam, int ProcessStampParam, string PrintJobName, string MonitorRecordPath)
        {
            if (StartMode == false)//从非窗口启动状态中发送拍照指令
            {
                //（1）检查保存文件夹是否符合要求
                #region 保存文件夹路径检查及更新
                string TimeStamp = "["
                   + (DateTime.Now.Year - 2000).ToString("D2") + DateTime.Now.Month.ToString("D2") + DateTime.Now.Day.ToString("D2") + "]";//年月日
                string PrinterTaskName = "[" + /*k_MonitorPrintParam.*/PrintJobName + "]";
                string NewRecordPATH = /*k_MonitorPrintParam.*/MonitorRecordPath/*ParentPATH*/+ "\\[Record]" + TimeStamp + PrinterTaskName;
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
                string PrinterTaskName = "[" + /*textBox2.Text*/ PrintJobName + "]";
                string NewRecordPATH = /*k_MonitorPrintParam.*/MonitorRecordPath/*ParentPATH*/+ "\\[Record]" + TimeStamp + PrinterTaskName;
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
                        RecordLayerNum = /*textBox3.Text*/RecordLayerNumParam.ToString();//准备监控记录的层序号//20231226临时修改统一接口：
                        message1.MonitorRecordPath = RecordPATH;//父路径+子路径
                        message1.CurrentLayer = RecordLayerNum;//准备监控记录的层序号
                        message1.ProcessStamp = /*textBox4.Text*/ProcessStampNames[ProcessStampParam];//记录文件名要素之一//20231226临时修改统一接口：
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
        public async Task DisconnectAsync(){}
    }
    /************************************标准用例:MQTT外部接口(实现)*******************************************/
    /************************************标准用例:MQTT外部接口(实现)*******************************************/
    /************************************标准用例:MQTT外部接口(实现)*******************************************/
    public class MqttCommunicationPlugin : IMqttCommunicationPlugin
    {

        private IMqttClient mqttClient;
        private string [] topic ;
        public event EventHandler<MqttMessageReceivedEventArgs> MessageReceived;

        public string Name => "MQTT外部接口实现";
        public async Task ConnectAsync(string brokerAddress)
        {
            // Connection logic...
            try
            {
                var factory = new MqttFactory();// Create a MQTT client factory         
                /*var*/ mqttClient = factory.CreateMqttClient();// Create a MQTT client instance

                // Create MQTT client options
                // Replace with your ThingsBoard device access token
                string broker = "localhost"; //string broker = "broker.emqx.io";/*thingsboardAccessToken*/ 
                int port = 1883;
                string username = "WmlDNODbFP93gVTxgKJh";// "温湿度传感器";// "emqx";
                string password = "WmlDNODbFP93gVTxgKJh";//"public";
                string clientId = "温湿度传感器";//""WmlDNODbFP93gVTxgKJh"// Guid.NewGuid().ToString();


                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(broker, port) // MQTT broker address and port
                    .WithCredentials(username, password) // Set username and password
                    .WithClientId(clientId)
                    .WithCleanSession()
                    .Build();

                // Connect to MQTT broker
                var connectResult = await mqttClient.ConnectAsync(options);

                if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
                {
                }
                else
                {
                    Console.WriteLine($"Failed to connect to MQTT broker: {connectResult.ResultCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        public async void Subscribe(string topic)//异步流程，维族赛的
        {
            // Subscription logic...
            try
            {
                /*string*/ topic = "v1/devices/me/telemetry";//"温湿度传感器";//"csharp/mqtt";
                // Subscribe to a topic
                await mqttClient.SubscribeAsync(topic);
                // Callback function when a message is received
                mqttClient.ApplicationMessageReceivedAsync += e =>
                {
                    string xx = "Received message: " + Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.ToArray());
                    return Task.CompletedTask;
                };


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public async Task PublishAsync(string topic, string payload)
        {
            // Publishing logic...
            try
            {
                // Publish a message 10 times
                for (int i = 1; i < 1/*20*/; i++)
                {
                    // 读取本地图片
                    string imagePath = $"image-{i + 1}.jpg";
                    byte[] imageData = File.ReadAllBytes(imagePath);
                    // 将图像数据转换成 Base64 编码的字符串
                    string base64Image = Convert.ToBase64String(imageData);


                    //Create a sample key-value pair
                    var keyvaluedata = new Dictionary<string, object>
                            {
                               //{ "mqtttext", $"Hello,MQTT! Message number {i}"}
                                { "mqtttext", $"{base64Image}"}
                            };

                    // Serialize key-value pair to JSON

                    var jsonPayload = JsonConvert.SerializeObject(keyvaluedata);
                    //jsonPayload = Regex.Replace(jsonPayload, @"\""\w+\"":", match => $"{match.Value[1..^2]}\":");

                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(jsonPayload)//($"Hello,MQTT! Message number {i}")//添加的负载，此处只是一个字符串
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithRetainFlag()
                        .Build();

                    await mqttClient.PublishAsync(message);
                    await Task.Delay(0); // Wait for 1 second
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            // Disconnection logic...
            // Publishing logic...
            try
            {
                // Unsubscribe and disconnect
                await mqttClient.UnsubscribeAsync(topic[0]);
                await mqttClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

        }
    }


}
