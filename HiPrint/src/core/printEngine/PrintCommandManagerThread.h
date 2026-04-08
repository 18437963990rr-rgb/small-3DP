#ifndef COMMANDTHREAD_H
#define COMMANDTHREAD_H

#include <QThread>
#include <QMutex>
#include <QWaitCondition>
#include <QVector>
#include <queue>
#include <atomic>
#include <QVariant>
#include <QDateTime>

#include "../printEngine/MeteorApi.h" 

#define MAX_DEVICE_COMMAND_QUEUE_SIZE 100

enum COMMAND_PRIORITY {
    CMD_PRIORITY_HIGH = 0,
    CMD_PRIORITY_NORMAL = 1,
    CMD_PRIORITY_LOW = 2
};


enum COMMAND_TYPE {
    CMD_CUSTOM = 0,
    CMD_HEAD_POWER = 1,
    CMD_SET_SPLIT_SIGNAL = 2,
};


struct DeviceCommand {
    COMMAND_TYPE type;
    QVariantMap parameters;
    COMMAND_PRIORITY priority;
    qint64 submitTimestamp;

    DeviceCommand()
        : type(COMMAND_TYPE::CMD_CUSTOM), priority(COMMAND_PRIORITY::CMD_PRIORITY_NORMAL),
        submitTimestamp(QDateTime::currentMSecsSinceEpoch()) {}
    DeviceCommand(COMMAND_TYPE t, COMMAND_PRIORITY p = CMD_PRIORITY_NORMAL)
        : type(t), priority(p),
        submitTimestamp(QDateTime::currentMSecsSinceEpoch()) {}
    DeviceCommand(COMMAND_TYPE t, const QVariantMap& paramMap, COMMAND_PRIORITY p = CMD_PRIORITY_NORMAL)
        : type(t), parameters(paramMap), priority(p),
        submitTimestamp(QDateTime::currentMSecsSinceEpoch()) {}
    QString toString() const {
        QStringList kv;
        for (auto it = parameters.begin(); it != parameters.end(); ++it)
            kv << QString("%1=%2").arg(it.key()).arg(it.value().toString());
        return QString("DeviceCommand[type=%1, priority=%2, timestamp=%3, params={%4}]")
            .arg(int(type)).arg(int(priority)).arg(submitTimestamp).arg(kv.join(","));
    }
};

struct DeviceCommandPriorityLess {
    bool operator()(const DeviceCommand& a, const DeviceCommand& b) const {
        if (a.priority != b.priority)
            return a.priority > b.priority; // 小的优先
        return a.submitTimestamp > b.submitTimestamp; // 早的优先
    }
};

class PrintCommandManagerThread : public QThread {
    Q_OBJECT
public:
    explicit PrintCommandManagerThread(QObject* parent = nullptr);
    ~PrintCommandManagerThread();
    bool submitCommand(const DeviceCommand& cmd);
    void stopThread();

signals:
    void commandExecuted(COMMAND_TYPE type, int resultCode);
    void commandQueueOverflow(const DeviceCommand& cmd);

protected:
    void run() override;

private:
    std::priority_queue<DeviceCommand, QVector<DeviceCommand>, DeviceCommandPriorityLess> commandQueue;
    QMutex commandQueueMutex;
    QWaitCondition commandAvailable;
    std::atomic<bool> running;
    std::unique_ptr<MeteorApi> meteorApi;
    std::atomic<bool> started;        
    QMutex startMutex;                
};

#endif // COMMANDTHREAD_H

