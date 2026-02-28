using System;
using System.Threading.Tasks;

namespace PluginInterface
{
    public interface IAlgorithmPlugin 
    {
        string Name { get; }
        void Execute();
    }

    /************************************标准用例:Mutex外部接口*******************************************/
    /************************************标准用例:Mutex外部接口*******************************************/
    /************************************标准用例:Mutex外部接口*******************************************/
    public interface IMutexCommandCommunicationPlugin //20231226新建接口：
    {
        string Name { get; }  // 插件的名称
        //Task PublishAsync(string topic, string payload);
        //void Subscribe(string topic);
        //event EventHandler<MqttMessageReceivedEventArgs> MessageReceived;
        Task ConnectAsync(string brokerAddress);
        void SendMessageFromSharedMemory(bool StartMode, int RecordLayerNumParam, int ProcessStampParam, 
            string PrintJobName, string MonitorRecordPath);//20231226修改：相机控制接口

        Task DisconnectAsync();
    }

    /************************************标准用例:MQTT外部接口*******************************************/
    /************************************标准用例:MQTT外部接口*******************************************/
    /************************************标准用例:MQTT外部接口*******************************************/
    //MqttMessageReceivedEventArgs 是一个自定义事件参数类，用于传递接收到的消息：
    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
    }
    // 定义 MQTT 通讯插件接口
    public interface IMqttCommunicationPlugin//20231225新建接口：
    {
        string Name { get; }  // 插件的名称
        Task ConnectAsync(string brokerAddress);
        Task PublishAsync(string topic, string payload);
        void Subscribe(string topic);
        event EventHandler<MqttMessageReceivedEventArgs> MessageReceived;
        Task DisconnectAsync();
    }
    /************************************标准用例:MQTT*******************************************/
    /************************************标准用例:MQTT*******************************************/
    //public partial class MainForm : Form
    //{
    //    private IMqttCommunicationPlugin _mqttPlugin;

    //    public MainForm()
    //    {
    //        InitializeComponent();
    //        _mqttPlugin = new MqttCommunicationPlugin();
    //        _mqttPlugin.MessageReceived += OnMqttMessageReceived;
    //        _mqttPlugin.ConnectAsync("broker.hivemq.com");
    //        _mqttPlugin.Subscribe("some/topic");
    //    }

    //    private void OnMqttMessageReceived(object sender, MqttMessageReceivedEventArgs e)
    //    {
    //        // 处理接收到的消息
    //        // 注意：如果需要更新 UI，确保在 UI 线程上执行
    //        Invoke(new Action(() =>
    //        {
    //            textBoxReceivedMessages.Text += $"{e.Topic}: {e.Payload}\n";
    //        }));
    //    }
    //}
    /************************************标准用例:MQTT外部接口*******************************************/
    /************************************标准用例:MQTT外部接口*******************************************/
    /************************************标准用例:MQTT外部接口*******************************************/

    //var tcpPlugin = new TcpCommunicationPlugin();
    //tcpPlugin.Connect("127.0.0.1:8000");
    //tcpPlugin.Send("Hello, world!");
    //string response = tcpPlugin.Receive();
    //tcpPlugin.Disconnect();
}
