
#ifndef DEVICE_MAPPING_CONFIG_H
#define DEVICE_MAPPING_CONFIG_H

#include <QObject>
#include <QString>
#include <QMap>
#include <QVector>
#include "../../common/utils/JsonFileReader.h"   

class DeviceMappingConfig : public QObject
{
    Q_OBJECT
public:
    static DeviceMappingConfig& instance();

    void loadConfig(const QString& jsonFilePath);
 
    const QVector<QString>& axisNames() const { return m_axisNameList; }

    QVector<QString> axisEnableList()     const;
    QVector<QString> axisRelMoveList()    const;
    QVector<QString> axisAbsMoveList()    const;
    QVector<QString> axisJogForwardList() const;
    QVector<QString> axisJogBackwardList()const;
    QVector<QString> axisHomeList()       const;
    QVector<QString> axisStopList()       const;

    QString axisEnable(const QString& axisName)     const;
    QString axisRelMove(const QString& axisName)    const;
    QString axisAbsMove(const QString& axisName)    const;
    QString axisJogForward(const QString& axisName) const;
    QString axisJogBackward(const QString& axisName)const;
    QString axisHome(const QString& axisName)       const;
    QString axisStop(const QString& axisName)       const;

    QString deviceAxisStatus()   const { return m_deviceAxisStatus; }
    QString deviceStartScan()    const { return m_deviceStartScan; }
    QString printModeMainScan()  const { return m_printModeMainScan; }
    QString printModeFastScan()  const { return m_printModeFastScan; }
    QString scanFinishedCmd()    const { return m_scanFinishedCmd; }
    QString imagePrintList()     const { return m_imagePrintList; }
    QString scanCountCmd()       const { return m_scanCountCmd; }
    QString autoPrintCmd()       const { return m_autoScanCommand; }
    
    // XY运动操作相关
    QString xyMotionOriginMode() const { return m_xyMotionOriginMode; }
    QString xyMotionStandbyPosition() const { return m_xyMotionStandbyPosition; }
    QString xyMotionPrintStartPosition() const { return m_xyMotionPrintStartPosition; }
    QString xyMotionPrintEndPosition() const { return m_xyMotionPrintEndPosition; }
    QString xyMotionCleanPosition() const { return m_xyMotionCleanPosition; }
    QString xyMotionFlashsprayingPosition() const { return m_xyMotionFlashsprayingPosition; }
    QString xyMotionhydrationPosition() const { return m_xyMotionhydrationPosition; }
    
    // 获取General配置中的值
    QString getGeneralValue(const QString& key) const;

    // 零位信号相关方法
    QString xyZeroSignal() const { return m_xyZeroSignal; }
    QString platformZeroSignal() const { return m_platformZeroSignal; }
    QString fallingZeroSignal() const { return m_fallingZeroSignal; }
    QString spreadZeroSignal() const { return m_spreadZeroSignal; }
    
    // 获取零位信号配置中的值
    QString getZeroSignal(const QString& key) const;
    
        // 零位信号读取方法（返回BOOL值）
    bool readXYZeroSignal() const;
    bool readPlatformZeroSignal() const;
    bool readFallingZeroSignal() const;
    bool readSpreadZeroSignal() const;
    bool readZeroSignal(const QString& key) const;
    
    // PLC通信心跳检测相关
    QString plcHeartbeatVariable() const { return m_plcHeartbeatVariable; }
    
    // 工艺参数相关配置
    QString processParametersVariable() const { return m_processParametersVariable; }

private:
    explicit DeviceMappingConfig(QObject* parent = nullptr);
    void loadDefault();

    JsonFileReader m_reader;

    QVector<QString> m_axisNameList;
    QVector<QString> m_printAxisList;
    QString m_deviceAxisStatus;
    QString m_deviceStartScan;
    QString m_printModeMainScan;
    QString m_printModeFastScan;
    QString m_scanFinishedCmd;
    QString m_imagePrintList;
    QString m_scanCountCmd;
    QString m_autoScanCommand;
    
    // XY运动操作相关变量
    QString m_xyMotionOriginMode;
    QString m_xyMotionStandbyPosition;
    QString m_xyMotionPrintStartPosition;
    QString m_xyMotionPrintEndPosition;
    QString m_xyMotionCleanPosition;
    QString m_xyMotionFlashsprayingPosition;
    QString m_xyMotionhydrationPosition;
    
    // 零位信号相关变量
    QString m_xyZeroSignal;
    QString m_platformZeroSignal;
    QString m_fallingZeroSignal;
    QString m_spreadZeroSignal;
    
    // PLC通信心跳检测相关变量
    QString m_plcHeartbeatVariable;
    
    // 工艺参数相关变量
    QString m_processParametersVariable;
};

#endif // !DEVICE_MAPPING_CONFIG_H
