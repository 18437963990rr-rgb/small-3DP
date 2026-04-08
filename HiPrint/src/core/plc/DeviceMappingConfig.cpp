#include "DeviceMappingConfig.h"
#include <QJsonArray>
#include <QJsonObject>
#include <QDebug>

DeviceMappingConfig& DeviceMappingConfig::instance()
{
    static DeviceMappingConfig instance;
    return instance;
}

DeviceMappingConfig::DeviceMappingConfig(QObject* parent)
    : QObject(parent)
{
    //loadDefault();
}

void DeviceMappingConfig::loadDefault()
{
    m_axisNameList = { "X", "Y", "Z" };
    m_deviceAxisStatus = "MAIN.axis_status";
    m_deviceStartScan = "MAIN.bEnableScan";
    m_printModeMainScan = "MAIN.bScan";
    m_printModeFastScan = "MAIN.bScan_op";
    m_scanFinishedCmd = "MAIN.bScandone";
    m_imagePrintList = "MAIN.blist";
    m_scanCountCmd = "MAIN.bTollcount";
    
    // XY运动操作相关变量默认值
    m_xyMotionOriginMode = "MAIN.XYHome";
    m_xyMotionStandbyPosition = "";  // 暂时为空
    m_xyMotionPrintStartPosition = "";  // 暂时为空
    m_xyMotionPrintEndPosition = "";  // 暂时为空
    m_xyMotionCleanPosition = "MAIN.CLEANPosition";
    m_xyMotionFlashsprayingPosition = "MAIN.XYHome";
    m_xyMotionhydrationPosition = "MAIN.XYHome";
    
    // 零位信号相关变量默认值
    m_xyZeroSignal = "ZST.XYZeroSignal";
    m_platformZeroSignal = "ZST.PlatformZeroSignal";
    m_fallingZeroSignal = "ZST.FallingZeroSignal";
    m_spreadZeroSignal = "ZST.SpreadZeroSignal";
    
    // PLC通信心跳检测相关变量默认值
    //m_plcHeartbeatVariable = "GVL.Comm.LinkProbe";
    m_plcHeartbeatVariable = "GVL.test1";
    
    // 工艺参数相关变量默认值
    m_processParametersVariable = "MAIN.ProcessParameters";
}

void DeviceMappingConfig::loadConfig(const QString& jsonFilePath)
{
    loadDefault();

    if (!m_reader.load(jsonFilePath)) {
        return;
    }

    // AxisList
    m_axisNameList.clear();
    QJsonArray axisList = m_reader.array("AxisList");
    for (const QJsonValue& v : axisList)
        m_axisNameList << v.toString().trimmed();

    m_printAxisList.clear();
    QJsonArray printAxisList = m_reader.array("PrintAxisList");
    for (const QJsonValue& v : printAxisList)
        m_printAxisList << v.toString().trimmed();

    // General
    QJsonObject general = m_reader.object("General");
    if (!general.isEmpty()) {
        m_deviceAxisStatus = general.value("DeviceAxisStatus").toString(m_deviceAxisStatus);
        m_deviceStartScan = general.value("DeviceStartScan").toString(m_deviceStartScan);
        m_printModeMainScan = general.value("PrintModeMainScan").toString(m_printModeMainScan);
        m_printModeFastScan = general.value("PrintModeFastScan").toString(m_printModeFastScan);
        m_scanFinishedCmd = general.value("ScanFinishedCmd").toString(m_scanFinishedCmd);
        m_imagePrintList = general.value("ImagePrintList").toString(m_imagePrintList);
        m_scanCountCmd = general.value("ScanCountCmd").toString(m_scanCountCmd);
        m_xyMotionOriginMode = general.value("XYMotionOriginMode").toString(m_xyMotionOriginMode);
        m_xyMotionStandbyPosition = general.value("XYMotionStandbyPosition").toString(m_xyMotionStandbyPosition);
        m_xyMotionPrintStartPosition = general.value("XYMotionPrintStartPosition").toString(m_xyMotionPrintStartPosition);
        m_xyMotionPrintEndPosition = general.value("XYMotionPrintEndPosition").toString(m_xyMotionPrintEndPosition);
        m_xyMotionCleanPosition = general.value("XYMotionCleanPosition").toString(m_xyMotionCleanPosition);
        m_xyMotionFlashsprayingPosition = general.value("XYMotionFlashsprayingPosition").toString(m_xyMotionFlashsprayingPosition);
        m_xyMotionhydrationPosition = general.value("XYMotionhydrationPosition").toString(m_xyMotionhydrationPosition);
        m_autoScanCommand = general.value("AutoScan").toString(m_autoScanCommand);
    }
    
    // ZeroSignals
    QJsonObject zeroSignals = m_reader.object("ZeroSignals");
    if (!zeroSignals.isEmpty()) {
        m_xyZeroSignal = zeroSignals.value("XYZeroSignal").toString(m_xyZeroSignal);
        m_platformZeroSignal = zeroSignals.value("PlatformZeroSignal").toString(m_platformZeroSignal);
        m_fallingZeroSignal = zeroSignals.value("FallingZeroSignal").toString(m_fallingZeroSignal);
        m_spreadZeroSignal = zeroSignals.value("SpreadZeroSignal").toString(m_spreadZeroSignal);
    }
    
    // PLC通信心跳检测配置
    QJsonObject heartbeatConfig = m_reader.object("PLCHeartbeat");
    if (!heartbeatConfig.isEmpty()) {
        m_plcHeartbeatVariable = heartbeatConfig.value("HeartbeatVariable").toString(m_plcHeartbeatVariable);
    }
    
    // 工艺参数配置
    QJsonObject processConfig = m_reader.object("ProcessParameters");
    if (!processConfig.isEmpty()) {
        m_processParametersVariable = processConfig.value("ProcessParametersVariable").toString(m_processParametersVariable);
    }
}

QVector<QString> DeviceMappingConfig::axisEnableList() const
{
    QVector<QString> list;
    QJsonObject obj = m_reader.object("AxisEnable");
    for (const QString& name : m_axisNameList)
        list << obj.value(name).toString("");
    return list;
}

QVector<QString> DeviceMappingConfig::axisRelMoveList() const
{
    QVector<QString> list;
    QJsonObject obj = m_reader.object("AxisRelMove");
    for (const QString& name : m_printAxisList)
        list << obj.value(name).toString("");
    return list;
}

QVector<QString> DeviceMappingConfig::axisAbsMoveList() const
{
    QVector<QString> list;
    QJsonObject obj = m_reader.object("AxisAbsMove");
    for (const QString& name : m_printAxisList)
        list << obj.value(name).toString("");
    return list;
}

QVector<QString> DeviceMappingConfig::axisJogForwardList() const
{
    QVector<QString> list;
    QJsonObject obj = m_reader.object("AxisJogForward");
    for (const QString& name : m_axisNameList)
        list << obj.value(name).toString("");
    return list;
}

QVector<QString> DeviceMappingConfig::axisJogBackwardList() const
{
    QVector<QString> list;
    QJsonObject obj = m_reader.object("AxisJogBackward");
    for (const QString& name : m_axisNameList)
        list << obj.value(name).toString("");
    return list;
}

QVector<QString> DeviceMappingConfig::axisHomeList() const
{
    QVector<QString> list;
    QJsonObject obj = m_reader.object("AxisHome");
    for (const QString& name : m_axisNameList)
        list << obj.value(name).toString("");
    return list;
}

QVector<QString> DeviceMappingConfig::axisStopList() const
{
    QVector<QString> list;
    QJsonObject obj = m_reader.object("AxisStop");
    for (const QString& name : m_printAxisList)
        list << obj.value(name).toString("");
    return list;
}

QString DeviceMappingConfig::axisEnable(const QString& axisName) const
{
    return m_reader.object("AxisEnable").value(axisName).toString("");
}

QString DeviceMappingConfig::axisRelMove(const QString& axisName) const
{
    return m_reader.object("AxisRelMove").value(axisName).toString("");
}

QString DeviceMappingConfig::axisAbsMove(const QString& axisName) const
{
    return m_reader.object("AxisAbsMove").value(axisName).toString("");
}

QString DeviceMappingConfig::axisJogForward(const QString& axisName) const
{
    return m_reader.object("AxisJogForward").value(axisName).toString("");
}

QString DeviceMappingConfig::axisJogBackward(const QString& axisName) const
{
    return m_reader.object("AxisJogBackward").value(axisName).toString("");
}

QString DeviceMappingConfig::axisHome(const QString& axisName) const
{
    return m_reader.object("AxisHome").value(axisName).toString("");
}

QString DeviceMappingConfig::axisStop(const QString& axisName) const
{
    return m_reader.object("AxisStop").value(axisName).toString("");
}

QString DeviceMappingConfig::getGeneralValue(const QString& key) const
{
    QJsonObject general = m_reader.object("General");
    return general.value(key).toString("");
}

QString DeviceMappingConfig::getZeroSignal(const QString& key) const
{
    QJsonObject zeroSignals = m_reader.object("ZeroSignals");
    return zeroSignals.value(key).toString("");
}

bool DeviceMappingConfig::readXYZeroSignal() const
{
    return readZeroSignal("XYZeroSignal");
}

bool DeviceMappingConfig::readPlatformZeroSignal() const
{
    return readZeroSignal("PlatformZeroSignal");
}

bool DeviceMappingConfig::readFallingZeroSignal() const
{
    return readZeroSignal("FallingZeroSignal");
}

bool DeviceMappingConfig::readSpreadZeroSignal() const
{
    return readZeroSignal("SpreadZeroSignal");
}

bool DeviceMappingConfig::readZeroSignal(const QString& key) const
{
    QString variableName = getZeroSignal(key);
    return !variableName.isEmpty(); // 如果变量名不为空，返回true
}



