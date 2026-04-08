#ifndef BECKHOFFPLC_H
#define BECKHOFFPLC_H

#include <QObject>
#include <QDebug>

#include "TcAdsDef.h"
#include "TcAdsAPI.h"
#include "DeviceMappingConfig.h"
#include "DeviceChannelData.h"
#include "../../common/logging/LogManager.h"
#include "../../common/global/ResourceManager.h"
#include "../plc/BeckhoffPLCApi.h"

class BeckhoffPlc:public QObject
{
	Q_OBJECT

public:
	BeckhoffPlc();
	~BeckhoffPlc();

public:
	void initBeckhoffPlc();//初始化
	void openCommunicationPort(); //打开通讯端口
	long motorHome(const std::string& plcVaribleName, bool isHome);//电机回零
	long setAllMotorHome(const std::string& plcVaribleName);//设置所有电机回零
	long motorEnable(const std::string& plcVaribleName, bool isEnable);//电机使能
	long setAllMotorEnable(const std::string& plcVaribleName);//设置所有电机使能
	long motorStop(const std::string& plcVaribleName, bool isStop);//电机停止
	long motorReset(const std::string& plcVaribleName, bool isReset);//电机复位
	long motorJog(const std::string& plcVaribleName,  bool isExecute);//电机JOG运动
	long motorAbsoluteMove(const std::string& plcVaribleName, AxisAbsoluteMoveParam sendData);//单轴电机绝对运动
	long motorRelativeMove(const std::string& plcVaribleName, AxisRelativeMoveParam sendData);//单轴电机相对运动
	long readMotorStatusData(const std::string& plcVaribleName, AxisMotionState(&receiveData)[MAX_MOTOR_NUM]);//读取电机状态数据
	long gearIn(const std::string& plcVaribleName, bool isExecute);//耦合
	long gearOut(const std::string& plcVaribleName, bool isExecute);//解耦合
	long setMotorVelocity(const std::string& plcVaribleName, AxisVelocityParam velocityParam);//设置轴的速度
	long setMotorPosition(const std::string& plcVaribleName, AxisPositionParam positionParam);//设置轴的位置

	//IO读写
	long writeIOPort(const std::string& plcVaribleName, bool outPut);//设置IO输出
	long readIOInPut(const std::string& plcVaribleName, bool& inPut);//读取IO输入
	long writeInt(const std::string& plcVaribleName, int value);//写入int类型数据

	//事件
	long readEventNotifyDriven(const std::string& plcVaribleName, PAdsNotificationFuncEx pNoteFunc);//读取事件
	long registerRouterNotification(PAmsRouterNotificationFuncEx pNoteFunc);//注册路由通知
	long detectStatusChangeForPLC(PAdsNotificationFuncEx pNoteFunc);//监控PLC状态变化
	long unRegisterRouterNotification();//取消注册路由通知
	long delDeviceNotificationReq(AmsAddr* pAddr, unsigned long hNotification);//删除设备通知

	//工艺参数
	long sendScanCommand(const std::string& plcVaribleName,bool isEnableScan);//发送开始扫描指令

	long receiveScanCommand(const std::string& plcVaribleName, bool &isEnableScan);//读取扫描完成信号
	long readADSStatus(unsigned short* adsStatus, unsigned short* deviceStatus);//读取ADS状态指令
	long sendPrintPassPosition(const std::string& plcVaribleName, int*arrayName,int arraySize);//发送图像打印位置
	long sendScanTimes(const std::string& plcVaribleName, int scanTimes);//发送扫描次数
	long sendProcessParameters(const std::string& plcVaribleName, ProcessParameters processParams);//发送全部工艺参数结构体
	long closeCommunicationPort();//关闭通讯端口

private:
	BeckhoffPLCApi* beckhoffPLCApi;
};

#endif // !BECKHOFFPLC_H