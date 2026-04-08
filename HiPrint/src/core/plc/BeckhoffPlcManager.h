#pragma once
#include <QObject>
#include <thread>
#include <atomic>
#include <vector>
#include <mutex>
#include "BeckhoffPlc.h"
#include "../../common/global/ResourceManager.h"
#include "../../core/plc/DeviceMappingConfig.h"


class BeckhoffPlcManager : public QObject {

    Q_OBJECT

public:
    BeckhoffPlcManager();
    ~BeckhoffPlcManager();

public:
    long enableAxis(const std::string& axisName, bool enable);
    long moveAxisRelative(const std::string& axisName, AxisRelativeMoveParam sendData);
    long moveAxisAbsolute(const std::string& axisName, AxisAbsoluteMoveParam sendData);
    long motorJog(const std::string& plcVaribleName, bool isExecute);

    long homeAxis(const std::string& plcVaribleName, bool isHome);
    long stopAxis(const std::string& axisName,bool isStop);
    
    // IO操作
    long writeIOPort(const std::string& plcVaribleName, bool outPut);
    long readIOInPut(const std::string& plcVaribleName, bool& inPut);
    long writeInt(const std::string& plcVaribleName, int value);

    long sendScanTimes(const std::string& plcVaribleName, int scanTimes);
    
    // 工艺参数操作
    long sendProcessParameters(const std::string& plcVaribleName, const ProcessParameters& processParams);
    
    // ADS状态检查
    bool checkADSState();
    
    // PLC通信心跳检测
    bool checkPLCHeartbeat();
    bool isCommunicationHealthy() const;
    void startHeartbeatDetection();
    void stopHeartbeatDetection();
    
    // 采集控制
    void stopStatusThread();
    
    // 通信端口管理
    int openCommunicationPort();
    
    // 轴状态采集相关
    void collectAxisStatus();

    //获取BeckhoffPlc对象
    BeckhoffPlc* getBeckhoffPlc() { return m_plc; }

signals:
    void axisStatusUpdated(const std::vector<AxisMotionState>&);
    void axisError(const QString& axis, long errorCode, const QString& message);
    void plcConnectionStatusChanged(bool connected);
  
private:
    void statusThreadProc(int intervalMs);
    BeckhoffPlc* m_plc;
    long m_lastAxisStatus = 0;
    std::thread* m_statusThread = nullptr;
    std::atomic<bool> m_threadRunning{ false };
    std::mutex m_statusMutex;
    QString m_deviceAxisStatusName;
    
    // PLC通信心跳检测相关
    std::atomic<bool> m_isCommunicationHealthy{ false };
    std::atomic<int> m_consecutiveFailures{ 0 };
    std::atomic<int> m_consecutiveSuccesses{ 0 };
    std::atomic<bool> m_heartbeatDetectionEnabled{ false };
    
    // 心跳检测失败状态管理
    std::atomic<bool> m_heartbeatFailureReported{ false };
};
