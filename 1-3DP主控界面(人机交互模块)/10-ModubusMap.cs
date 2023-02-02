using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using Modbus;//202205074新增：建立仪表通讯
//using System.Threading.Tasks;//202205074新增：建立仪表通讯
using System.Net.Sockets;//202205074新增：建立仪表通讯
using System.Net;
using Modbus.Device;
using System.IO.Ports;
using System.Windows.Forms;

namespace Communication//20220514新建：Modbus通讯关系
{
    public class ModbusCommunicateMap
    {
        /// <summary>
        /// 私有串口实例
        /// </summary>
        public SerialPort serialPort = new SerialPort();

        /// <summary>
        /// 私有ModbusRTU主站字段
        /// </summary>
        public /*static*/ IModbusMaster master;
        
        private bool communicateExistedFlag = false;//20220521新建：通讯创建标志位
        public void SetCommunicateExiste(bool existed)
        {
            if (existed == true) { communicateExistedFlag = true; }
            else {
                communicateExistedFlag = false;
            }
        }
        public void SetIRLightPower(double PowerPercentage)//手动出光，且设置IR光源输出功率
        {
            //读取输入寄存器值并完成显示
            int SlaveNumber =1; int RegisterAddress = 0; int RegisterValue = 0;

            //20220520新建：设置出光模式为手动出光方式:默认出光方式为自动出光控制
            RegisterAddress = 132; RegisterValue = 1;//20220520新建批注：寄存器地址为40133;手动打印模式寄存器值为1
            WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//20220520批注：寄存器为40133;

            //ushort[] CurrentTemperature = modbusCommunicateMap.ReadHoldingRegisters((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterNumber);//读取30001的内部计算值
            //设置功率为50%
            RegisterAddress = 121/*2121*/; RegisterValue = Convert.ToInt32(PowerPercentage * 100)/*5000*/;//给内部计算值设置值，可以和之的更加精确
            WriteSingleRegister((byte)SlaveNumber, (ushort)RegisterAddress, (ushort)RegisterValue);//协议地址：40122----MV

        }
        public bool CreateQuitModbusCommunication(bool CreateQuitFlag)//20220521新建:建立或删除Modbus通讯
        {
            if (CreateQuitFlag == true)//通过COM口建立稳定通讯
            {
                try//20220520新建：连接异常逻辑
                {
                    if (communicateExistedFlag == false)
                    {
                        //(B)创建串口参数
                        serialPort.PortName = /*"ELTIMA Virtual Serial Port(COM2->COM3)"*/ /* "COM3"*//*"COM3"*/"COM" + "1"/*COMNumber*//*textBox18.Text*/;
                        serialPort.BaudRate = (int)9600;//COM1的通讯速率为9600bps
                        serialPort.DataBits = (int)8/*cbxDataBits.SelectedItem*/;
                        serialPort.Parity = System.IO.Ports.Parity.Odd/*Even*/ /*GetSelectedParity()*/;//20220514修改：为奇校验
                        serialPort.StopBits = System.IO.Ports.StopBits.One;

                        //serialPort.ReadTimeout = 5;//写延时5ms
                        //serialPort.WriteTimeout = 5;//读延时5ms

                        //(C)创建ModubusRTU主站实例        
                        master = ModbusSerialMaster.CreateRtu(serialPort);//ModbusSerialMaster master = ModbusSerialMaster.CreateRtu(modbusCommunicateMap.serialPort);
                        //(D)打开串口
                        ////string[] portNames=SerialPort.GetPortNames();
                        ////string PortName = "The following serial ports were found:";
                        ////foreach (string port in portNames)
                        ////{
                        ////    PortName = PortName +" "+ port;
                        ////}
                        ////MessageBox.Show(PortName);

                        //Console.WriteLine("The following serial ports were found:");
                        //// Display each port name to the console.
                        //foreach (string port in portNames)
                        //{
                        //    Console.WriteLine(port);
                        //}
                        //Console.ReadLine();
                        if (serialPort.IsOpen == false)
                        {
                            try
                            {
                                serialPort.Open();//MessageBox.Show("打开了关闭的串口！");//Open线程会阻塞主线程
                            }
                            catch (Exception ex)
                            {
                                return false;
                            }
                        }
                        communicateExistedFlag = true;

                        SetIRLightPower(0);//初始输出功率为10%
                    }
                    else {}//不予重新创建
                    return true;//创建成功
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("连接失败：" + ex.Message);
                    return false;//创建失败
                }
            }
            else//关闭现存的稳定通讯
            {
                SetIRLightPower(0);//关闭：关闭时输出功率为0%
                if (/*!*/serialPort.IsOpen==true)//存在即关闭串口
                {
                    serialPort.Close();
                    serialPort.Dispose();
                }
                //master = null;//20220521新建：析构通讯连接，防止重新建立通讯
                master.Dispose();
                communicateExistedFlag = false;//析构通讯
                //MessageBox.Show("关闭了打开的串口！");
                return true;//关闭成功
            }
        }

        /// <summary>
        /// (1) 功能码1：写入单个线圈
        /// </summary>
        public void WriteSingleCoil(byte slaveAddress, ushort coilAddress, bool value)
        {
            //bool result = false;
            //if (rbxRWMsg.Text.Equals("true", StringComparison.OrdinalIgnoreCase) || rbxRWMsg.Text.Equals("1", StringComparison.OrdinalIgnoreCase))
            //{ result = true; }
            master.WriteSingleCoil( slaveAddress, coilAddress, value/*(byte)nudSlaveID.Value, (ushort)nudStartAdr.Value, result*/);
        }

        /// <summary>
        ///  (2) 功能码2：批量写入线圈
        /// </summary>
        public void WriteArrayCoil(byte slaveAddress, ushort startAddress, bool[] data)
        {
            //List<string> strList = rbxRWMsg.Text.Split(',').ToList();
            //List<bool> result = new List<bool>();
            //strList.ForEach(m => result.Add(m.Equals("true", StringComparison.OrdinalIgnoreCase) || m.Equals("1", StringComparison.OrdinalIgnoreCase)));
            master.WriteMultipleCoils( slaveAddress, startAddress, data/*(byte)nudSlaveID.Value, (ushort)nudStartAdr.Value, result.ToArray()*/);
        }

        /// <summary>
        /// (3) 功能码3： 写入单个寄存器
        /// </summary>
        public void WriteSingleRegister(byte slaveAddress, ushort registerAddress, ushort value)
        {
            try
            {
                //ushort result = Convert.ToUInt16(rbxRWMsg.Text);
                master.WriteSingleRegister( slaveAddress, registerAddress, value/*(byte)nudSlaveID.Value, (ushort)nudStartAdr.Value, result*/);
            }
            catch (Exception ex)
            {
                //MessageBox.Show("功率设置异常");
                return;
            }
        }

        /// <summary>
        ///  (4) 功能码4：批量写入寄存器
        /// </summary>
        public void WriteArrayRegister(byte slaveAddress, ushort startAddress, ushort[] data)
        {
            //List<string> strList = rbxRWMsg.Text.Split(',').ToList();
            //List<ushort> result = new List<ushort>();
            //strList.ForEach(m => result.Add(Convert.ToUInt16(m)));
            master.WriteMultipleRegisters( slaveAddress,  startAddress, data/*(byte)nudSlaveID.Value, (ushort)nudStartAdr.Value, result.ToArray()*/);
        }

        /// <summary>
        /// (5) 功能码5： 读取输出线圈
        /// </summary>
        /// <returns></returns>
        public bool[] ReadCoils(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
        {
            return master.ReadCoils( slaveAddress, startAddress, numberOfPoints/*(byte)nudSlaveID.Value, (ushort)nudStartAdr.Value, (ushort)nudLength.Value*/);
        }

        /// <summary>
        ///  (6) 功能码6：读取输入线圈
        /// </summary>
        /// <returns></returns>
        public bool[] ReadInputs(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
        {
            return master.ReadInputs( slaveAddress, startAddress, numberOfPoints/*(byte)nudSlaveID.Value, (ushort)nudStartAdr.Value, (ushort)nudLength.Value*/);
        }

        /// <summary>
        ///  (7) 功能码7：读取保持型寄存器 //20220514新建批注: Modubus寄存器的大小为16bit//4区为保持寄存器
        /// </summary>
        /// <returns></returns>
        public ushort[] ReadHoldingRegisters(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
        {
            return master.ReadHoldingRegisters( slaveAddress, startAddress, numberOfPoints/*(byte)nudSlaveID.Value, (ushort)nudStartAdr.Value, (ushort)nudLength.Value*/);
        }

        /// <summary>
        ///  (8) 功能码8：读取输入寄存器//3区为输入寄存器
        /// </summary>
        /// <returns></returns>
        public ushort[] ReadInputRegisters(byte slaveAddress, ushort startAddress, ushort numberOfPoints/*,out ushort[] temperatures*/)//20220514新建批注: Modubus寄存器的大小为16bit
        {
            try
            {
                return master.ReadInputRegisters( slaveAddress, startAddress, numberOfPoints);
                //return true;
            }
            catch (Exception ex)
            {
                //temperatures = null;//不返回任何有效值
                return null;//读取失败
            }
        }


    }
}
