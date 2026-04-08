#ifndef WORKERTHREADMANAGER_H
#define  WORKERTHREADMANAGER_H

#include <QObject>

#include "../../core/printEngine/PrintJobManagerThread.h"
#include "../../core/printEngine/PrintStatusManagerThread.h"
#include "../../core/printEngine/PrintCommandManagerThread.h"


class WorkerThreadManager : public QObject
{
    Q_OBJECT

public:
    explicit WorkerThreadManager(QObject* parent = nullptr);
    ~WorkerThreadManager();
    WorkerThreadManager(const WorkerThreadManager&) = delete;
    WorkerThreadManager& operator=(const WorkerThreadManager&) = delete;

    PrintJobManagerThread* printJobThread() const noexcept { return m_printJobThread; }
    PrintStatusManagerThread* statusThread() const noexcept { return m_statusThread ; }
    PrintCommandManagerThread* commandThread() const noexcept { return m_commandThread ; }

private:
    PrintJobManagerThread* m_printJobThread = nullptr;
    PrintStatusManagerThread* m_statusThread = nullptr;
    PrintCommandManagerThread* m_commandThread = nullptr;
};

#endif // WORKERTHREADMANAGER_H
