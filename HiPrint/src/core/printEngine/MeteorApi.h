#ifndef METEORAPI_H
#define METEORAPI_H

#include <QObject>
#include <QDateTime>
#include <QThread>
#include <QEventLoop>
#include <QTimer>

#include "./3rdparty/meteor/Meteor.h"
#include "./3rdparty/meteor/PrinterInterface.h"
#include "./3rdparty/meteor/typedef.h"


class MeteorApi :public QObject
{
	Q_OBJECT

public:
	MeteorApi();
	~MeteorApi();

public:
	int startPrintEngine(const char*configFileName);//开始打印引擎
	int connectPrinter();//连接打印机
	int sendCommand(unsigned int* buffCommand, int maxRetries = 10, int timeoutMs = 1000);//发送打印指令
	int setAndValidateParam(unsigned int paramId, unsigned value);//设置参数
	int setHome();//设置回零
	int setSplitSignal(unsigned int signalId, unsigned int state);//设置控制信号
	bool setSplitSignal(int pccNumber, int hdcNumber, int splitCount);//设置闪喷信号
	bool setSplitSignal(int pccNumber, const QList<int>& nozzleList, int splitCount);//设置指定喷头列表的闪喷信号
	int setHeadPower(int state);//设置喷头开关信号
	TAppStatus* getPrinterStatus();//获取打印机状态
	TAppHeadStatus* getHeadStatus(unsigned int pccNum,unsigned headNum);//获取喷头状态
	TAppPccStatus* getPccStatus(unsigned int pccNum);//获取PCC状态
	int getLastStatusError();//获取上一次状态错误
	int getPrintEngineError(char* buff, unsigned int buffSize);//获取打印引擎错误状态
	int setPrinterParameter(unsigned int paramId, unsigned int value);//设置参数
	bool getHeadTemperature(int pccNum,int hdcNum,int &headTemperature,int &hdcAmplifier1Temperature, int& hdcAmplifier2Temperature);//获取喷头温度
	bool checkPCCStatusIdle(const TAppStatus* appStatus);//检查PCC状态
	bool checkHeadPowerStatus(const TAppStatus* appStatus);//检查电源状态
    bool checkHeadStatusIsRunning(const TAppStatus* appStatus, int timeoutMs = 1000);//检查喷头是否正在运行,每个HDC都返回TRUE
	int checkPrinterIsScanMode();//检查是否是扫描模式
	int sendAbortCommand();//发送停止指令
	int closePrinter();//关闭打印机
	int stopPrintEngine(int dwfoce);//停止打印引擎

};

#endif // !METEORAPI_H



