#include "PrintCommandManagerThread.h"
#include <QDebug>


PrintCommandManagerThread::PrintCommandManagerThread(QObject* parent)
    : QThread(parent)
    , running(true)
    , meteorApi(std::make_unique<MeteorApi>())
    , started(false)
{
}

PrintCommandManagerThread::~PrintCommandManagerThread() {
    stopThread();
}

bool PrintCommandManagerThread::submitCommand(const DeviceCommand& cmd) {

    if (!started) {
        QMutexLocker locker(&startMutex);
        if (!started) {
            this->start();
            started = true;
        }
    }
    QMutexLocker locker(&commandQueueMutex);
    if (commandQueue.size() >= MAX_DEVICE_COMMAND_QUEUE_SIZE) {
        emit commandQueueOverflow(cmd);
        return false;
    }
    commandQueue.push(cmd);
    commandAvailable.wakeOne();
    return true;
}

void PrintCommandManagerThread::stopThread() {
    running = false;
    commandAvailable.wakeAll();
    if (isRunning()) wait();
    started = false;  
}

void PrintCommandManagerThread::run() {
    running = true;
    while (running) {
        DeviceCommand cmd;
        {
            QMutexLocker locker(&commandQueueMutex);
            if (commandQueue.empty()) {
                commandAvailable.wait(&commandQueueMutex, 100);
                continue;
            }
            cmd = commandQueue.top();
            commandQueue.pop();
        }
        int result = -1;
        switch (cmd.type) {
        case CMD_HEAD_POWER:
            result = meteorApi->setHeadPower(
                cmd.parameters.value("enabled").toBool());
            break;
        case CMD_SET_SPLIT_SIGNAL:
            if (cmd.parameters.contains("nozzleList")) {
                QList<int> nozzleList = cmd.parameters.value("nozzleList").value<QList<int>>();
                result = meteorApi->setSplitSignal(
                    cmd.parameters.value("pccNum").toInt(),
                    nozzleList,
                    cmd.parameters.value("spitCount").toInt()) ? 0 : -1;
            } else {
                result = meteorApi->setSplitSignal(
                    cmd.parameters.value("pccNum").toInt(), 
                    cmd.parameters.value("maxHnum").toInt(),
                    cmd.parameters.value("spitCount").toInt()) ? 0 : -1;
            }
            break;
        default:
            result = -99;
            break;
        }
        emit commandExecuted(cmd.type, result);
    }
}
