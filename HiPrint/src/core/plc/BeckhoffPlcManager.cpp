#include "BeckhoffPlcManager.h"
#include <QMetaObject>
#include "AdsErrorMapper.h"
#include "../../common/logging/LogManager.h"
#include "DeviceChannelData.h"  

BeckhoffPlcManager::BeckhoffPlcManager()
{
    m_plc = new BeckhoffPlc();
    m_threadRunning = true;
    m_deviceAxisStatusName = DeviceMappingConfig::instance().deviceAxisStatus();
    m_statusThread = new std::thread(&BeckhoffPlcManager::statusThreadProc, this, 100);
}

BeckhoffPlcManager::~BeckhoffPlcManager() {
    stopStatusThread();
}

void BeckhoffPlcManager::stopStatusThread() {
    m_threadRunning = false;
    if (m_statusThread) {
        if (m_statusThread->joinable())
            m_statusThread->join();
        delete m_statusThread;
        m_statusThread = nullptr;
    }
}

int BeckhoffPlcManager::openCommunicationPort() {
 
    m_plc->openCommunicationPort();
    
    QString heartbeatVar = DeviceMappingConfig::instance().plcHeartbeatVariable();
    if (!heartbeatVar.isEmpty()) {
        bool testValue = false;
        long result = m_plc->readIOInPut(heartbeatVar.toStdString(), testValue);
        if (result == 0) {
            m_isCommunicationHealthy = true;
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
                QString("PLC通信端口打开成功，可以正常读取PLC变量").toStdString());
            return 0;  // 返回0表示成功
        } else {
            m_isCommunicationHealthy = false;
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                QString("PLC通信端口打开失败，无法读取PLC变量，错误码: %1,读取的变量为%2").
                arg(AdsErrorMapper::getErrorDescription(result)).arg(heartbeatVar).toStdString());
            return result;  // 返回具体的错误码
        }
    }
}

void BeckhoffPlcManager::statusThreadProc(int intervalMs)
{
    int heartbeatCounter = 0;  // 心跳检测计数器
    int axisCounter = 0;       // 轴状态检测计数器
    
    while (m_threadRunning) {
        // ==================== 1. 心跳检测：每500ms执行一次 ====================
        heartbeatCounter++;
        if (heartbeatCounter >= 5) {  // 100ms * 5 = 500ms
            if (m_heartbeatDetectionEnabled) {
                checkPLCHeartbeat();
            }
            heartbeatCounter = 0;  // 重置计数器
        }
        
        // ==================== 2. 轴状态检测：每100ms执行一次，但只在心跳连接正常时执行 ====================
        axisCounter++;
        if (axisCounter >= 1) {  // 100ms * 1 = 100ms
            if (m_isCommunicationHealthy) {
                // 心跳连接正常，采集轴状态
                collectAxisStatus();
            }
            // 心跳连接异常时，不进行任何轴状态处理
            axisCounter = 0;  // 重置计数器
        }
        
        // 基础间隔：100ms
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
}

long BeckhoffPlcManager::enableAxis(const std::string & axisName, bool enable) {
    return m_plc->motorEnable(axisName, enable);
}
long BeckhoffPlcManager::moveAxisRelative(const std::string& axisName, AxisRelativeMoveParam sendData) {
    return m_plc->motorRelativeMove(axisName, sendData);
}
long BeckhoffPlcManager::moveAxisAbsolute(const std::string& axisName, AxisAbsoluteMoveParam sendData) {
    return m_plc->motorAbsoluteMove(axisName, sendData);
}
long BeckhoffPlcManager::motorJog(const std::string& plcVaribleName, bool isExecute) {
    return m_plc->motorJog(plcVaribleName, isExecute);
}
long BeckhoffPlcManager::homeAxis(const std::string& plcVaribleName, bool isHome) {
    return m_plc->motorHome(plcVaribleName, isHome);
}
long BeckhoffPlcManager::stopAxis(const std::string& axisName,bool isStop) {
    return m_plc->motorStop(axisName, isStop);
}

long BeckhoffPlcManager::writeIOPort(const std::string& plcVaribleName, bool outPut) {
    return m_plc->writeIOPort(plcVaribleName, outPut);
}

long BeckhoffPlcManager::readIOInPut(const std::string& plcVaribleName, bool& inPut) {
    return m_plc->readIOInPut(plcVaribleName, inPut);
}

long BeckhoffPlcManager::writeInt(const std::string& plcVaribleName, int value) {
    return m_plc->writeInt(plcVaribleName, value);
}

long BeckhoffPlcManager::sendScanTimes(const std::string& plcVaribleName, int scanTimes)
{
    return m_plc->sendScanTimes(plcVaribleName,scanTimes);
}

long BeckhoffPlcManager::sendProcessParameters(const std::string& plcVaribleName, const ProcessParameters& processParams) {
    return m_plc->sendProcessParameters(plcVaribleName, processParams);
}

bool BeckhoffPlcManager::checkADSState() {
    if (!m_plc) {
        return false;
    }
    unsigned short adsState = 0;
    unsigned short deviceStatus = 0;
    long result = m_plc->readADSStatus(&adsState, &deviceStatus);
    
    if (result != 0) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("读取ADS状态失败，错误码: %1").arg(result).toStdString());
        return false;

    } 
    return (adsState == 5);
}

bool BeckhoffPlcManager::checkPLCHeartbeat() {
    if (!m_plc) {
        m_consecutiveFailures++;
        m_consecutiveSuccesses = 0;
        return false;
    }
    
    // 读取PLC心跳变量
    QString heartbeatVar = DeviceMappingConfig::instance().plcHeartbeatVariable();
    if (heartbeatVar.isEmpty()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("PLC心跳变量未配置").toStdString());
        m_consecutiveFailures++;
        m_consecutiveSuccesses = 0;
        return false;
    }
    
    // 尝试读取心跳变量（布尔值）
    bool heartbeatValue = false;
    long result = m_plc->readIOInPut(heartbeatVar.toStdString(), heartbeatValue);
    
    if (result != 0) {
        // 读取失败，增加失败计数
        m_consecutiveFailures++;
        m_consecutiveSuccesses = 0;
        
        // 只在首次失败或状态变化时记录日志，避免日志刷屏
        if (!m_heartbeatFailureReported) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN,
                QString("PLC心跳检测失败，错误码: %1").arg(result).toStdString());
            m_heartbeatFailureReported = true;
        }
        
        // 判断是否需要标记为通信异常
        if (m_consecutiveFailures >= 3) {
            m_isCommunicationHealthy = false;
            // 发送状态变化信号
            QMetaObject::invokeMethod(this, [this]() {
                emit plcConnectionStatusChanged(false);
            }, Qt::QueuedConnection);
        }
        
        return false;
    } else {
        // 读取成功，增加成功计数
        m_consecutiveSuccesses++;
        m_consecutiveFailures = 0;
        
        // 重置心跳失败报告标志
        m_heartbeatFailureReported = false;
        
        // 判断是否可以恢复通信正常状态
        if (m_consecutiveSuccesses >= 2) {
            m_isCommunicationHealthy = true;
            // 发送状态变化信号
            QMetaObject::invokeMethod(this, [this]() {
                emit plcConnectionStatusChanged(true);
            }, Qt::QueuedConnection);
        }
        
        return true;
    }
}

bool BeckhoffPlcManager::isCommunicationHealthy() const {
    return m_isCommunicationHealthy;
}

void BeckhoffPlcManager::startHeartbeatDetection() {
    m_heartbeatDetectionEnabled = true;
    // 重置心跳失败报告标志
    m_heartbeatFailureReported = false;
    
    // 立即进行一次心跳检测，设置初始状态
    checkPLCHeartbeat();
    
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("PLC通信心跳检测已启动").toStdString());
}

void BeckhoffPlcManager::stopHeartbeatDetection() {
    m_heartbeatDetectionEnabled = false;
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("PLC通信心跳检测已停止").toStdString());
}

void BeckhoffPlcManager::collectAxisStatus() {
    AxisMotionState data[MAX_MOTOR_NUM];
    long currentAxisRet = m_plc->readMotorStatusData(m_deviceAxisStatusName.toStdString(), data);
    std::vector<AxisMotionState> axisData; 
    if (currentAxisRet == 0) {
        for (int i = 0; i < MAX_MOTOR_NUM; ++i) {
            axisData.push_back(data[i]);
        }
        QMetaObject::invokeMethod(this, [this, axisData]() {
            emit axisStatusUpdated(axisData);
        }, Qt::QueuedConnection);
        
        m_lastAxisStatus = 0;
    } else {
        if (currentAxisRet != m_lastAxisStatus) {
            m_lastAxisStatus = currentAxisRet;
            QMetaObject::invokeMethod(this, [this, currentAxisRet]() {
                emit axisError("ALL", currentAxisRet, AdsErrorMapper::getErrorDescription(currentAxisRet));
            }, Qt::QueuedConnection);
        }
    }
}


