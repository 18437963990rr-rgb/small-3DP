#include "BeckhoffPlc.h"

BeckhoffPlc::BeckhoffPlc()
{
	beckhoffPLCApi = new BeckhoffPLCApi();
}

BeckhoffPlc::~BeckhoffPlc()
{
	closeCommunicationPort(); 
}

void BeckhoffPlc::initBeckhoffPlc()
{
	openCommunicationPort();
}

void BeckhoffPlc::openCommunicationPort()
{
	beckhoffPLCApi->openCommunicationPort();
}

long BeckhoffPlc::motorHome(const std::string& plcVaribleName, bool isHome)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isHome);
	return nRet;	
}

long BeckhoffPlc::motorEnable(const std::string& plcVaribleName, bool isEnable)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isEnable);
	return nRet;
}

long BeckhoffPlc::motorStop(const std::string& plcVaribleName, bool isStop)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isStop);
	return nRet;
}

long BeckhoffPlc::motorReset(const std::string& plcVaribleName, bool isReset)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isReset);
	return nRet;

}

long BeckhoffPlc::motorJog(const std::string& plcVaribleName,  bool isExecute)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isExecute);
	return nRet;
}

long BeckhoffPlc::motorAbsoluteMove(const std::string& plcVaribleName, AxisAbsoluteMoveParam sendData)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &sendData);
	return nRet;
}

long BeckhoffPlc::motorRelativeMove(const std::string& plcVaribleName, AxisRelativeMoveParam sendData)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &sendData);
	return nRet;
}

long BeckhoffPlc::readMotorStatusData(const std::string& plcVaribleName, AxisMotionState(&receiveData)[MAX_MOTOR_NUM])
{
	long nRet = beckhoffPLCApi->receiveDataFromBeckhoffPLC(plcVaribleName, &receiveData);
	return nRet;
}

long BeckhoffPlc::gearIn(const std::string& plcVaribleName, bool isExecute)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isExecute);
	return nRet;

}

long BeckhoffPlc::gearOut(const std::string& plcVaribleName, bool isExecute)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isExecute);
	return nRet;

}

long BeckhoffPlc::setMotorVelocity(const std::string& plcVaribleName, AxisVelocityParam velocityParam)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &velocityParam);
	return nRet;

}

long BeckhoffPlc::setMotorPosition(const std::string& plcVaribleName, AxisPositionParam positionParam)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &positionParam);
	return nRet;

}

long BeckhoffPlc::setAllMotorEnable(const std::string& plcVaribleName)
{
	long nRet = 0;
	return nRet;

}

long BeckhoffPlc::writeIOPort(const std::string& plcVaribleName, bool outPut)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &outPut);
	return nRet;

}

long BeckhoffPlc::readIOInPut(const std::string& plcVaribleName, bool &inPut)
{
	long nRet = beckhoffPLCApi->receiveDataFromBeckhoffPLC(plcVaribleName, &inPut);
	return nRet;

}

long BeckhoffPlc::writeInt(const std::string& plcVaribleName, int value)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &value);
	return nRet;
}

long BeckhoffPlc::readEventNotifyDriven(const std::string& plcVaribleName, PAdsNotificationFuncEx pNoteFunc)
{
	long nRet = beckhoffPLCApi->readEventNotifyDriven(plcVaribleName, pNoteFunc);
	return nRet;

}

long BeckhoffPlc::registerRouterNotification(PAmsRouterNotificationFuncEx pNoteFunc)
{
	long nRet = beckhoffPLCApi->registerRouterNotification(pNoteFunc);
	return nRet;

}

long BeckhoffPlc::detectStatusChangeForPLC(PAdsNotificationFuncEx pNoteFunc)
{
	long nRet = beckhoffPLCApi->detectStatusChangeForPLC(pNoteFunc);
	return nRet;
}

long BeckhoffPlc::unRegisterRouterNotification()
{
	long nRet = beckhoffPLCApi->unRegisterRouterNotification();
	return nRet;
}

long BeckhoffPlc::delDeviceNotificationReq(AmsAddr* pAddr, unsigned long hNotification)
{
	long nRet = beckhoffPLCApi->delDeviceNotificationReq(pAddr, hNotification);
	return nRet;
}

long BeckhoffPlc::sendScanCommand(const std::string& plcVaribleName, bool isEnableScan)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &isEnableScan);
	return nRet;
}

long BeckhoffPlc::receiveScanCommand(const std::string& plcVaribleName, bool &isEnableScan)
{
	long nRet = beckhoffPLCApi->receiveDataFromBeckhoffPLC(plcVaribleName, &isEnableScan);
	return nRet;
}

long BeckhoffPlc::readADSStatus(unsigned short* adsStatus, unsigned short* deviceStatus)
{
	long nRet = beckhoffPLCApi->readADSStatus(adsStatus, deviceStatus);
	return nRet;
}

long BeckhoffPlc::sendPrintPassPosition(const std::string& plcVaribleName, int* arrayName, int arraySize)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, arrayName, arraySize);
	return nRet;
}

long BeckhoffPlc::sendScanTimes(const std::string& plcVaribleName, int scanTimes)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &scanTimes);
	return nRet;
}

long BeckhoffPlc::closeCommunicationPort()
{
	long nRet = beckhoffPLCApi->closeCommunicationPort();
	return nRet;
}

long BeckhoffPlc::setAllMotorHome(const std::string& plcVaribleName)
{
	long nRet = 0;
	return nRet;
}

long BeckhoffPlc::sendProcessParameters(const std::string& plcVaribleName, ProcessParameters processParams)
{
	long nRet = beckhoffPLCApi->sendDataToBeckhoffPLC(plcVaribleName, &processParams);
	return nRet;
	
}







