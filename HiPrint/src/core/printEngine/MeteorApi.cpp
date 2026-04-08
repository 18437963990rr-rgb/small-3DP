#include "MeteorApi.h"
#include <QDebug>
#include <QList>

MeteorApi::MeteorApi()
{

}

MeteorApi::~MeteorApi()
{
	sendAbortCommand();
	closePrinter();
	stopPrintEngine(0);
}

int MeteorApi::startPrintEngine(const char* configFileName)
{
	eRET ret = PiStartPrintEngine(configFileName);
	return ret;
}

int MeteorApi::connectPrinter()
{
	eRET ret = PiOpenPrinter();
	return ret;
}

bool MeteorApi::checkPCCStatusIdle(const TAppStatus* appStatus)
{
	if (appStatus == nullptr || appStatus->PccsAttached == 0) {
		return false;
	}
	constexpr int MAX_RETRY_COUNT = 30;      
	constexpr int RETRY_INTERVAL_MS = 100;   
	const auto timeoutTime = QDateTime::currentDateTime().addMSecs(3000); 
	for (int retry = 0; retry < MAX_RETRY_COUNT; ++retry)
	{
		bool allIdle = true;
		bool atLeastOneChecked = false;
		for (int pccNum = 0; pccNum < appStatus->PccsAttached; pccNum++){
			TAppPccStatus* pccStatus = PiGetPccStatus(pccNum + 1);
			if (nullptr == pccStatus) {
				return false;
			}
			atLeastOneChecked = true;  // 至少检查了一个PCC
			ePCCSTATE pccstate = static_cast<ePCCSTATE>((pccStatus->bmStatusBits & BMPS_PCC_STATE) >> SH_PCC_STATE);
			if (pccstate != PS_IDLE) {
				allIdle = false;
				break;  // 发现非空闲PCC立即跳出循环
			}
		}
		if (atLeastOneChecked && allIdle) {
			return true;  // 所有PCC空闲
		}
		if (QDateTime::currentDateTime() >= timeoutTime) {
			break;
		}
		QThread::msleep(RETRY_INTERVAL_MS);
	}
	return false;  // 超时返回忙状态
}


bool MeteorApi::checkHeadPowerStatus(const TAppStatus* appStatus)
{
	if (appStatus == nullptr || appStatus->PccsAttached == 0) {
		return false;
	}
	bool headPowerBusy = false;
	for (int pccNum = 0; pccNum < appStatus->PccsAttached; pccNum++) {
		TAppPccStatus* pccStatus = PiGetPccStatus(pccNum + 1);
		if (pccStatus != nullptr && (pccStatus->bmStatusBits2 & BMPS2_HEAD_POWER_IN_PROGRESS)) {
			headPowerBusy = true;
		}
	}
	return headPowerBusy;
}

bool MeteorApi::checkHeadStatusIsRunning(const TAppStatus* appStatus, int timeoutMs)
{
	if (appStatus == nullptr) {
		return false;
	}
    if (appStatus->PccsAttached == 0) {
        return false;
    }
    auto startTime = QDateTime::currentDateTime();
    while (true) {
        bool allRunning = true;
        bool atLeastOneChecked = false;  
        for (int pccNum = 0; pccNum < appStatus->PccsAttached; pccNum++) {
            TAppPccStatus* pccStatus = PiGetPccStatus(pccNum + 1);
            if (pccStatus == nullptr) {
                continue;
            }
            for (int hdcNum = 0; hdcNum < 1; hdcNum++) {
                TAppHeadStatus* hdcStatus = getHeadStatus(pccNum + 1, hdcNum + 1);
                if (hdcStatus == nullptr) {
                    return PiGetLastStatusError();
                }
                atLeastOneChecked = true;  // 至少检查了一个HDC
                if (hdcStatus->HeadState != eHeadState::ST_RUNNING) {
                    allRunning = false;
                    break;
                }
            }
            if (!allRunning) break;
        }
        if (atLeastOneChecked && allRunning) {
            return true;
        }
        if (startTime.msecsTo(QDateTime::currentDateTime()) >= timeoutMs) {
            return false;
        }
        QThread::msleep(50); 
    }
}

int MeteorApi::checkPrinterIsScanMode()
{
	const TAppStatus* appStatus = PiGetPrnStatus();
	if (nullptr == appStatus) {
		int rVal = PiGetLastStatusError();
		return rVal;
	}
	if (!(appStatus->Control & BM_SCANNING)) {
		return RVAL_FAULT;
	}
	return RVAL_OK;
}

int MeteorApi::sendCommand(unsigned int* buffCommand, int maxRetries, int timeoutMs)
{
	eRET rVal = RVAL_OK;
	int retryCount = 0;
	auto startTime = QDateTime::currentDateTime();
	while ((rVal = PiSendCommand(buffCommand)) == RVAL_FULL) {
		if (++retryCount >= maxRetries) {
			return RVAL_FULL; 
		}
		if (startTime.msecsTo(QDateTime::currentDateTime()) >= timeoutMs) {
			return RVAL_TIMEOUT;
		}
		QThread::msleep(10); 
	}
	return rVal;
}

int MeteorApi::setAndValidateParam(unsigned int paramId, unsigned value)
{
	eRET ret = PiSetAndValidateParam(paramId,  value);
	return ret;
}

int MeteorApi::setHome()
{
	eRET ret = PiSetHome();
	return ret;
}

int MeteorApi::setSplitSignal(unsigned int signalId, unsigned int state)
{
	eRET ret = PiSetSignal(signalId, state);
	return ret;
}

bool MeteorApi::setSplitSignal(int pccNumber, int hdcNumber, int splitCount)
{
	bool allSuccess = true;
	for (int humNum = 1; humNum <= hdcNumber; humNum++) {
		int ret = PiSetSignal(SIG_SPIT | (humNum << 8) | (pccNumber << 16), splitCount);
		if (ret != RVAL_OK) {
			allSuccess = false;
			qDebug() << "喷头" << humNum << "闪喷失败，错误码:" << ret;
		}
	}
	return allSuccess;
}

bool MeteorApi::setSplitSignal(int pccNumber, const QList<int>& nozzleList, int splitCount)
{
	if (nozzleList.isEmpty()) {
		qDebug() << "喷头列表为空，无法执行闪喷操作";
		return false;
	}
	
	bool allSuccess = true;
	
	// 遍历指定的喷头列表
	for (int nozzleNum : nozzleList) {
		if (nozzleNum < 1 || nozzleNum > 8) { 
			qDebug() << "无效的喷头编号:" << nozzleNum << "，跳过此喷头";
			allSuccess = false;
			continue;
		}
		int ret = PiSetSignal(SIG_SPIT | (nozzleNum << 8) | (pccNumber << 16), splitCount);
		if (ret != RVAL_OK) {
			// 记录失败但继续处理其他喷头
			allSuccess = false;
			qDebug() << "喷头" << nozzleNum << "闪喷失败，错误码:" << ret;
		} else {
			qDebug() << "喷头" << nozzleNum << "闪喷成功";
		}
	}
	
	// 只有当所有喷头都成功时才返回true
	return allSuccess;
}

int MeteorApi::setHeadPower(int state)
{
	eRET ret = PiSetHeadPower(state);
	return ret;
}

TAppStatus* MeteorApi::getPrinterStatus()
{
	TAppStatus *appStatus = PiGetPrnStatus();
	return appStatus;
}

TAppHeadStatus* MeteorApi::getHeadStatus(unsigned int pccNum, unsigned headNum)
{
	TAppHeadStatus* headStatus = PiGetHeadStatus(pccNum,headNum);
	return headStatus;
}

TAppPccStatus* MeteorApi::getPccStatus(unsigned int pccNum)
{
	TAppPccStatus* pccStatus = PiGetPccStatus(pccNum);
	return pccStatus;
}

int MeteorApi::getLastStatusError()
{
	eRET ret = PiGetLastStatusError();
	return ret;
}

int MeteorApi::getPrintEngineError(char* buff, unsigned int buffSize)
{
	eRET ret = PiGetPrintEngineError(buff, buffSize);
	return ret;
}

int MeteorApi::setPrinterParameter(unsigned int paramId, unsigned int value)
{
	eRET ret = PiSetParam(paramId, value);
	return ret;
}

int MeteorApi::stopPrintEngine(int dwfoce)
{
	eRET ret = PiStopPrintEngine(dwfoce);
	return ret;
}

bool MeteorApi::getHeadTemperature(int pccNum, int hdcNum, int& headTemperature, int& hdcAmplifier1Temperature, int& hdcAmplifier2Temperature)
{
	TAppHeadStatus *headStatus = PiGetHeadStatus(pccNum, hdcNum);
	if (headStatus) {
		headTemperature = headStatus->Temperature1 * 10;
		hdcAmplifier1Temperature = headStatus->Temperature3 * 10;
		hdcAmplifier2Temperature = headStatus->Temperature4 * 10;
		return true;
	}
	else {
		headTemperature = 0;
		hdcAmplifier1Temperature = 0;
		hdcAmplifier2Temperature = 0;
		return false;
	}
	
}

int MeteorApi::sendAbortCommand()
{
	constexpr int timeoutMs = 5000;
	constexpr int sleepMs = 100;
	eRET ret = PiAbort();
	if (ret != eRET::RVAL_OK)
		return ret;
	int waited = 0;
	while (PiIsBusy()) {
		QThread::msleep(sleepMs);
		waited += sleepMs;
		if (waited >= timeoutMs)
			return eRET::RVAL_BUSY;  
	}
	return eRET::RVAL_OK;
}

int MeteorApi::closePrinter()
{
	eRET ret = PiClosePrinter();
	return ret;
}
