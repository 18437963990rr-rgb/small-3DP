#include "BeckhoffPLCApi.h"
#include "AdsErrorMapper.h"

BeckhoffPLCApi::BeckhoffPLCApi()
{
	
}

BeckhoffPLCApi::~BeckhoffPLCApi()
{
	closeCommunicationPort();
	unRegisterRouterNotification();
	delDeviceNotificationReq(pAddr, hNotification);
}

long BeckhoffPLCApi::openCommunicationPort()
{
 
	long portNum = AdsPortOpen();
	if (portNum == 0) {
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS通信端口打开失败").toStdString());
		return -1;
	}


	long errorId = AdsGetLocalAddress(pAddr);
	if (errorId != 0) {
		QString errorDesc = AdsErrorMapper::getErrorDescription(errorId);
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("获取本地AMS地址失败，错误码: %1, 错误描述: %2").arg(errorId).arg(errorDesc).toStdString());
		return errorId;
	}

	configureRemoteControllerNetId();

	validateADSCommunication();

	return 0;
}

long BeckhoffPLCApi::readEventNotifyDriven(const std::string& plcVaribleName, PAdsNotificationFuncEx pNoteFunc)
{
	std::vector<char> sendPlcVariableName(plcVaribleName.begin(), plcVaribleName.end());
	unsigned long handVarible;
	AdsNotificationAttrib  adsNotificationAttrib;
	adsNotificationAttrib.cbLength = 4;
	adsNotificationAttrib.nTransMode = ADSTRANS_SERVERONCHA;
	adsNotificationAttrib.nMaxDelay = 0;
	adsNotificationAttrib.nCycleTime = 10000000; // 1sec 
	pAddr->port = AMSPORT_R0_PLC_TC3;
	long nRet = AdsSyncReadWriteReq(pAddr, 
		ADSIGRP_SYM_HNDBYNAME, 
		0x0, sizeof(handVarible), 
		&handVarible, 
		sizeof(sendPlcVariableName[0]) * sendPlcVariableName.size(),
		sendPlcVariableName.data());
	if (nRet != 0){
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("readEventNotifyDriven handle is error ,errorId is %1").arg(nRet).toStdString());
		return nRet;
	}
	long nErr = AdsSyncAddDeviceNotificationReq(pAddr, 
		ADSIGRP_SYM_VALBYHND, 
		handVarible, 
		&adsNotificationAttrib, 
		pNoteFunc, 
		handVarible, 
		&hNotification);
	if (nErr != 0) {
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("readSyncAddDeviceNotificationReq is error ,errorId is %1").arg(nRet).toStdString());
		return nRet;
	}
	return nRet;
}

long BeckhoffPLCApi::registerRouterNotification(PAmsRouterNotificationFuncEx pNoteFunc)
{
	long nRet = AdsAmsRegisterRouterNotification(pNoteFunc);
	return nRet;
}

long BeckhoffPLCApi::readADSStatus(unsigned short* adsState, unsigned short* deviceStatus)
{
	long nRet = AdsSyncReadStateReq(pAddr, adsState, deviceStatus);
	return nRet;
}

bool BeckhoffPLCApi::checkADSState()
{
	unsigned short adsState = 0;
	unsigned short deviceStatus = 0;
	long nRet = readADSStatus(&adsState, &deviceStatus);
	if (nRet != 0) {
		QString errorDesc = AdsErrorMapper::getErrorDescription(nRet);
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("读取ADS状态失败，错误码: %1, 错误描述: %2").arg(nRet).arg(errorDesc).toStdString());
		return false;
	}
	
	// 判断ADS状态
	switch (adsState) {
	case 5: // ADSSTATE_RUN - 正常运行状态
		LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
			QString("ADS状态正常: RUN状态 (状态码: %1)").arg(adsState).toStdString());
		return true;
		
	case 0: // ADSSTATE_INVALID
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: INVALID状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 1: // ADSSTATE_IDLE
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: IDLE状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 2: // ADSSTATE_RESET
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: RESET状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 3: // ADSSTATE_INIT
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: INIT状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 4: // ADSSTATE_START
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: START状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 6: // ADSSTATE_STOP
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: STOP状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 7: // ADSSTATE_SAVECFG
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: SAVECFG状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 8: // ADSSTATE_LOADCFG
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: LOADCFG状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 9: // ADSSTATE_POWERFAILURE
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: POWERFAILURE状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 10: // ADSSTATE_POWERGOOD
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: POWERGOOD状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 11: // ADSSTATE_ERROR
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: ERROR状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 12: // ADSSTATE_SHUTDOWN
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: SHUTDOWN状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 13: // ADSSTATE_SUSPEND
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: SUSPEND状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 14: // ADSSTATE_RESUME
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: RESUME状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 15: // ADSSTATE_CONFIG
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: CONFIG状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 16: // ADSSTATE_RECONFIG
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: RECONFIG状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	case 17: // ADSSTATE_STOPPING
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态异常: STOPPING状态 (状态码: %1)").arg(adsState).toStdString());
		break;
	default:
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态未知: 未知状态码 %1").arg(adsState).toStdString());
		break;
	}
	
	return false;
}

long BeckhoffPLCApi::detectStatusChangeForPLC(PAdsNotificationFuncEx pNoteFunc)
{
	long nRet = 0;
	unsigned long handVarible = 0;
	AdsNotificationAttrib adsNotificationAttrib;
	adsNotificationAttrib.cbLength = sizeof(AdsNotificationAttrib);
	adsNotificationAttrib.nTransMode = ADSTRANS_SERVERONCHA;
	adsNotificationAttrib.nMaxDelay = 0;
	adsNotificationAttrib.dwChangeFilter = 0;
	nRet = AdsSyncAddDeviceNotificationReq(pAddr,
		ADSIGRP_DEVICE_DATA, 
		ADSIOFFS_DEVDATA_ADSSTATE,
		&adsNotificationAttrib, 
		pNoteFunc, 
		handVarible, 
		&hNotificationPLC);
	if (nRet != 0) {
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("adsSyncAddDeviceNotification is error ,errorId is %1").arg(nRet).toStdString());
		return nRet;
	}
	return nRet;
}

long BeckhoffPLCApi::unRegisterRouterNotification()
{
	long nRet = AdsAmsUnRegisterRouterNotification();
	if (nRet != 0) {
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("adsAmsUnRegisterRouterNotification is error ,errorId is %1").arg(nRet).toStdString());
		return nRet;
	}
	return nRet;
}

long BeckhoffPLCApi::delDeviceNotificationReq(AmsAddr* pAddr, unsigned long hNotification)
{
	long nRet = AdsSyncDelDeviceNotificationReq(pAddr, hNotification);
	if (nRet != 0) {
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("delDeviceNotificationReq is error ,errorId is %1").arg(nRet).toStdString());
		return nRet;
	}
	return nRet;
}

long BeckhoffPLCApi::closeCommunicationPort()
{
	long errorId = AdsPortClose();
	return errorId;
}

void BeckhoffPLCApi::configureRemoteControllerNetId()
{
	// 配置远程控制器的AMS网络ID
     pAddr->netId.b[0] = 5;
	 pAddr->netId.b[1] = 127;
	 pAddr->netId.b[2] = 157;
	 pAddr->netId.b[3] = 192;
	 pAddr->netId.b[4] = 1;
	 pAddr->netId.b[5] = 1;
	 pAddr->port = 851;
	
	LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
		QString("远程控制器AMS网络ID配置完成: %1.%2.%3.%4.%5.%6")
		.arg(pAddr->netId.b[0])
		.arg(pAddr->netId.b[1])
		.arg(pAddr->netId.b[2])
		.arg(pAddr->netId.b[3])
		.arg(pAddr->netId.b[4])
		.arg(pAddr->netId.b[5]).toStdString());
}

void BeckhoffPLCApi::validateADSCommunication()
{
	LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
		QString("开始验证ADS通信状态...").toStdString());
	
	if (!checkADSState()) {
		LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
			QString("ADS状态检查失败，但通信端口已成功打开").toStdString());
	} else {
		LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
			QString("ADS状态检查成功，通信连接正常").toStdString());
	}
}




