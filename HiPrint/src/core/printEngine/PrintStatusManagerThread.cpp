#include "PrintStatusManagerThread.h"
#include <QThread>
#include <QMutexLocker>
#include "../../common/logging/LogManager.h" 

PrintStatusManagerThread::PrintStatusManagerThread(QObject* parent)
    : QThread(parent), bExitWorkThread(false),bCheckPCCIsIdle(false), 
    bCheckHeadPowerIsBusy(false), meteorApi(std::make_unique<MeteorApi>()),
    m_lastPccIdleStatus(false),m_lastHeadPowerBusyStatus(false),m_lastHeadRunStatus(false) {
   
    start();//启动线程
}

PrintStatusManagerThread::~PrintStatusManagerThread() {
    stopThread();
}

void PrintStatusManagerThread::stopThread() {
    bExitWorkThread = true;
    wait();  // 等待线程退出
}

void PrintStatusManagerThread::run() {
    while (!bExitWorkThread) {
        if (!meteorApi) {
            QThread::msleep(100);
            continue;
        }
        // 获取和检查打印机状态
        char meteorError[1024];
        while (RVAL_OK == meteorApi->getPrintEngineError(meteorError, sizeof(meteorError))) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Meteor error occurred!,%1")
                .arg(QString::fromUtf8(meteorError)).toStdString());
        }
        // 获取打印机状态
        const TAppStatus* appStatus = meteorApi->getPrinterStatus();
        if (nullptr == appStatus) {
            emit errorOccurred("Failed to get printer status");
            continue;
        }
        // 检查 PCC 状态
        bool pccIdle = meteorApi->checkPCCStatusIdle(appStatus);
        if (pccIdle != m_lastPccIdleStatus) {
            m_lastPccIdleStatus = pccIdle;
            emit pccIdleChanged(pccIdle);
        }
        // 检查喷头电源状态
        bool headPowerBusy = meteorApi->checkHeadPowerStatus(appStatus);
        if (headPowerBusy != m_lastHeadPowerBusyStatus) {
            m_lastHeadPowerBusyStatus = headPowerBusy;
            emit headPowerBusyChanged(headPowerBusy);
        }
        // 检查HDC状态
        bool headRunning = meteorApi->checkHeadStatusIsRunning(appStatus);
        if (headRunning != m_lastHeadRunStatus) {
            m_lastHeadRunStatus = headRunning;
            emit headRunningChanged(headRunning);
        }
        QThread::msleep(50);  // 延迟50 毫秒
    }
}





