#include "WorkerThreadManager.h"

WorkerThreadManager::WorkerThreadManager(QObject* parent)
    : QObject(parent)
{
    m_printJobThread = new PrintJobManagerThread(this);
    m_statusThread = new PrintStatusManagerThread(this);
    m_commandThread = new PrintCommandManagerThread(this);
}

WorkerThreadManager::~WorkerThreadManager()
{
   
}


