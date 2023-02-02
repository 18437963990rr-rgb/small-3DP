using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Channels.Tcp;
using System.Windows.Forms;

namespace BinderJetting
{
    class AccessServer
    {
        IpcChannel channel = new IpcChannel();//移来全局变量区
        WellKnownClientTypeEntry remoteType =
            new WellKnownClientTypeEntry(
                typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");
        System.Runtime.Remoting.Messaging.IMessageSink messageSink;
        //private void IPCStartBtn_Click(object sender, EventArgs e)
        public int IPCStart(bool firstFlag )//20200506新建：
        {
            if (firstFlag == true)
            {
                try//（1）添加异常检测：防止出现信道已住注册
                {
                    ////(方式1)TCP-IP通讯：20200502新增
                    //TcpServerChannel channel = new TcpServerChannel(6666);
                    //ChannelServices.RegisterChannel(channel);
                    //RemotingConfiguration.RegisterWellKnownServiceType(typeof(RemoteObject),
                    // "RemoteObject", WellKnownObjectMode.SingleCall);
                    //System.Console.WriteLine("Press Any Key");
                    //System.Console.ReadLine();

                    //(方式2)IPC通讯：20200502新增    
                    //(一)注册IPC及对象：
                    //（1）Create the channel.
                    //IpcChannel channel = new IpcChannel();
                    //（2） Register the channel.
                    ChannelServices.RegisterChannel(channel, false);

                    //（3） Register as client for remote object.
                    //WellKnownClientTypeEntry remoteType =
                    //    new WellKnownClientTypeEntry(
                    //        typeof(RemoteObject),
                    //        "ipc://localhost:9090/RemoteObject.rem");
                    RemotingConfiguration.RegisterWellKnownClientType(remoteType);

                    return 1;
                    //（4）第4条的创建消息槽是非必要的：20200502新增
                    //（4）Create a message sink.//（3）提供的新的插件点：20200504新插入点
#if false
                string objectUri;
                /*System.Runtime.Remoting.Messaging.IMessageSink */messageSink =
                    channel.CreateMessageSink(
                        "ipc://localhost:9090/RemoteObject.rem", null,
                        out objectUri);
                InformationBox.AppendText("【!!!】The URI of the message sink is " 
                    + objectUri + ".\r\n");
                if (messageSink != null)
                {
                    InformationBox.AppendText("【!!!】The type of the message sink is "
                        + messageSink.GetType().ToString() + ".\r\n");
                }
#endif
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                finally
                {
                    //（二）跨进程调用
                    // (1)跨进程调用对象关键:Create an instance of the remote object.
                    //RemoteObject service = new RemoteObject();//移向全局变量区
                    RemoteObject clientService = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                        "ipc://localhost:9090/RemoteObject.rem");

                    // (2)跨进程调用对象关键:Invoke a method on the remote object.
                    //InformationBox.AppendText("The client is invoking the remote object.\r\n");
                    //InformationBox.AppendText("The remote object has been called " + clientService.GetCount() + "times.\r\n");
                }
            }
            else { return 2; }
            return 3;
        }

        //private RemoteObject clientService=new RemoteObject();//移来全局变量区
//        private void ReConnectBtn_Click(object sender, EventArgs e)
//        {
//            //（二）跨进程调用
//            // (1)跨进程调用对象关键:Create an instance of the remote object.
//            RemoteObject clientService = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
//                "ipc://localhost:9090/RemoteObject.rem");

//            // (2)跨进程调用对象关键:Invoke a method on the remote object.
//            //InformationBox.AppendText("The client is invoking the remote object.\r\n");
//            //InformationBox.AppendText("The remote object has been called " + clientService.GetCount() + "times.\r\n");

//            //（3）提供的新的插件点：20200504新插入点
//#if false
//            if (messageSink != null)
//            {
//                InformationBox.AppendText("【!!!】The type of the message sink is "
//                    + messageSink.GetType().ToString() + ".\r\n");

//                InformationBox.AppendText("【!!!】The contain in the message sink is "
//                    + messageSink.ToString() + ".\r\n");
//            }
//#endif
//        }
        //private List<PointCoordinate>  LocalPointCoordinates=new List<PointCoordinate>();
        private PointCoordinates LocalPointCoordinates = new PointCoordinates();
        //private void AddPositionBtn_Click(object sender, EventArgs e)
        //{
        //    PointCoordinate pointCoordinate = new PointCoordinate();
        //    //pointCoordinate.x = Convert.ToDouble(XLabel.Text);
        //    //pointCoordinate.y = Convert.ToDouble(YLabel.Text);
        //    LocalPointCoordinates.g_PointCoordinates.Add(pointCoordinate);
        //}

        //private void LoadDataBtn_Click(object sender, EventArgs e)
        /// <summary>
        /// 传输1层的所有数据：20200506新增
        /// </summary>
        /// <param name="LayerIndex"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="layer"></param>
        public void SendDataToRemote(RemoteCLIs remoteCLIs, int LayerIndex)
        {
            //(1)跨进程调用对象关键:Create an instance of the remote object.
            RemoteObject clientService = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
                "ipc://localhost:9090/RemoteObject.rem");

            // (2)跨进程调用对象关键:Invoke a method on the remote object.
            ////(2-1)附加位置消息结构
            //CountCLI tempCountCLI = new CountCLI();
            //tempCountCLI.x = x; tempCountCLI.y = y;
            //tempCountCLI.aLayer = layer;
            ////(2-2)附加远程信息结构
            //RemoteCLIs tempRemoteCLIs = new RemoteCLIs();
            //tempRemoteCLIs.layerIndex = LayerIndex;
            //tempRemoteCLIs.aLayerData.Add(tempCountCLI);
            //(2-3)远程发送
            bool returnCode = clientService.SendPosition(remoteCLIs, LayerIndex);
        }

        //private void MasterSwitchBtn_Click(object sender, EventArgs e)
        //{
        //    //（二）跨进程调用
        //    // (1)跨进程调用对象关键:Create an instance of the remote object.
        //    RemoteObject clientService = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
        //        "ipc://localhost:9090/RemoteObject.rem");

        //    // (2)跨进程调用对象关键:Invoke a method on the remote object.
        //    PointCoordinates returnCoordinates = clientService.GetPosition();
        //}
        public RemoteObject service;//20200504新增：
        private double GetBackValue()//从服务器回传给客户端
        {
            /*RemoteObject */
            ////service = (RemoteObject)Activator.GetObject(typeof(RemoteObject),
            ////    "ipc://localhost:9090/RemoteObject.rem");
            return service.GetBackValue();
        }
    }
}
