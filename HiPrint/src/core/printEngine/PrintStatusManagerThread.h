#ifndef STATUSTHREAD_H
#define STATUSTHREAD_H

#include <QThread>
#include <QString>
#include <QMutex>
#include <QWaitCondition>
#include "../printEngine/MeteorApi.h" 

class PrintStatusManagerThread : public QThread
{
    Q_OBJECT

public:
    explicit PrintStatusManagerThread(QObject* parent = nullptr);
    ~PrintStatusManagerThread();

    void stopThread();  // 用于停止线程

protected:
    void run() override; // 重写 run 函数

private:
    bool bExitWorkThread; // 控制线程退出的标志
    bool bCheckPCCIsIdle;
    bool bCheckHeadPowerIsBusy;

private:
    std::unique_ptr<MeteorApi> meteorApi;
    bool m_lastPccIdleStatus;
    bool m_lastHeadPowerBusyStatus;
    bool m_lastHeadRunStatus;

signals:
    void statusUpdate(bool isCheckNormal); // 定期发送状态更新信号
    void errorOccurred(const QString& error); // 发生错误时发送信号
    void pccIdleChanged(bool isIdle);
    void headPowerBusyChanged(bool isBusy);
    void headRunningChanged(bool isRunning);
};

#endif // STATUSTHREAD_H

