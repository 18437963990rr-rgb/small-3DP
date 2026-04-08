#ifndef BECKHOFFPLCAPI_H
#define BECKHOFFPLCAPI_H

#include <QObject>
#include <QString>
#include "TcAdsDef.h"
#include "TcAdsAPI.h"
#include "DeviceMappingConfig.h"
#include "DeviceChannelData.h"
#include "../../common/logging/LogManager.h"

class BeckhoffPLCApi  : public QObject
{
	Q_OBJECT

public:
	BeckhoffPLCApi();
	~BeckhoffPLCApi();

public:
	long openCommunicationPort();//打开通信端口
	template<typename T>
	long sendDataToBeckhoffPLC(const std::string& plcVaribleName, T* bufferData,int size = 1);//发送PLC数据
	template<typename T>
	long receiveDataFromBeckhoffPLC(const std::string& plcVaribleName, T *bufferData);//接收PLC数据
	long readEventNotifyDriven(const std::string& plcVaribleName, PAdsNotificationFuncEx pNoteFunc);//读取事件

	long registerRouterNotification(PAmsRouterNotificationFuncEx pNoteFunc);//注册路由通知
	long readADSStatus(unsigned short*adsState, unsigned short* deviceStatus);//ADS状态
	bool checkADSState();//检查ADS状态，返回true表示RUN状态(正常)，false表示异常状态
	long detectStatusChangeForPLC(PAdsNotificationFuncEx pNoteFunc);//监控PLC状态变化
	long unRegisterRouterNotification();//取消注册路由通知
	long delDeviceNotificationReq(AmsAddr* pAddr, unsigned long hNotification);//删除设备通知
	long closeCommunicationPort();//关闭通讯端口

private:
	AmsAddr Addr;
	PAmsAddr pAddr = &Addr;
	unsigned long hNotification, hNotificationPLC;

	void configureRemoteControllerNetId(); // 配置远程控制器AMS网络ID
	void validateADSCommunication(); // 验证ADS通信状态
};



template<typename T>
inline long BeckhoffPLCApi::sendDataToBeckhoffPLC(const std::string& plcVaribleName, T* bufferData, int size)
{
	long nRet = 0;
	std::vector<char> sendPlcVariableName(plcVaribleName.begin(), plcVaribleName.end());
	unsigned long handVarible;
	pAddr->port = AMSPORT_R0_PLC_TC3;
	nRet = AdsSyncReadWriteReq(pAddr, ADSIGRP_SYM_HNDBYNAME, 0x0, sizeof(handVarible), &handVarible, 
		sizeof(sendPlcVariableName[0]) * sendPlcVariableName.size(), sendPlcVariableName.data());
	if (nRet != 0) {
		return nRet;
	}
	nRet = AdsSyncWriteReq(pAddr, ADSIGRP_SYM_VALBYHND, handVarible, size * sizeof(T), bufferData);
	if (nRet != 0) {
		return nRet;
	}
	nRet = AdsSyncWriteReq(pAddr, ADSIGRP_SYM_RELEASEHND, 0, sizeof(handVarible), &handVarible);
	if (nRet != 0) {
		return nRet;
	}
	return nRet;
}

template<typename T>
inline long BeckhoffPLCApi::receiveDataFromBeckhoffPLC(const std::string& plcVaribleName, T*bufferData)
{
	long nRet = 0;
	std::vector<char> sendPlcVariableName(plcVaribleName.begin(), plcVaribleName.end());
	unsigned long handVarible;
	pAddr->port = AMSPORT_R0_PLC_TC3;
	nRet = AdsSyncReadWriteReq(pAddr, ADSIGRP_SYM_HNDBYNAME, 0x0, sizeof(handVarible), &handVarible, 
		sizeof(sendPlcVariableName[0]) * sendPlcVariableName.size(), sendPlcVariableName.data());
	if (nRet != 0) {
		return nRet;
	}
	nRet = AdsSyncReadReq(pAddr, ADSIGRP_SYM_VALBYHND, handVarible, sizeof(T),bufferData);
	if (nRet != 0) {
		return nRet;
	}
	nRet = AdsSyncWriteReq(pAddr, ADSIGRP_SYM_RELEASEHND, 0, sizeof(handVarible), &handVarible);
	if (nRet != 0) {
		return nRet;
	}
	return nRet;
}

#endif //BECKHOFFPLCAPI_H