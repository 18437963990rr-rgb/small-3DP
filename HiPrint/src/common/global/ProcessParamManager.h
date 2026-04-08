#pragma once

#include <QObject>
#include <QMap>
#include <QVariant>
#include <QList>
#include "../db/ProcessParamTables.h"
#include "../db/SqlQuery.h"
#include <qsqlerror.h>

class ProcessParamManager : public QObject
{
    Q_OBJECT

public:
    static ProcessParamManager& instance();

    // 初始化数据库表
    bool initTables();
    
    // 工艺参数配置管理
    bool createProfile(const QString& name, const QString& description = "");
    bool deleteProfile(int profileId);
    bool updateProfile(int profileId, const QString& name, const QString& description);
    bool setDefaultProfile(int profileId);
    
    // 获取工艺参数配置列表
    QList<ProcessParamProfile> getProfileList();
    ProcessParamProfile getProfile(int profileId);
    ProcessParamProfile getDefaultProfile();
    
    // 工艺参数值管理
    bool setParameterValue(int profileId, const QString& paramName, const QVariant& value);
    QVariant getParameterValue(int profileId, const QString& paramName, const QVariant& defaultValue = QVariant());
    QMap<QString, QVariant> getProfileParameters(int profileId);
    
    // 获取参数定义
    QList<ProcessParamDefinition> getParameterDefinitions();
    ProcessParamDefinition getParameterDefinition(const QString& paramName);
    
    // 便捷方法：获取当前默认配置的参数值
    QVariant getDefaultParameterValue(const QString& paramName, const QVariant& defaultValue = QVariant());
    QMap<QString, QVariant> getDefaultProfileParameters();
    
    // 便捷方法：根据配置名称获取配置
    ProcessParamProfile getProfileByName(const QString& name);
    int getProfileIdByName(const QString& name);
    
    // 便捷方法：获取所有配置名称列表
    QStringList getProfileNames();
    
    // 导入导出功能
    bool exportProfile(int profileId, const QString& filePath);
    bool importProfile(const QString& filePath);
    bool exportAllProfiles(const QString& filePath);
    bool importAllProfiles(const QString& filePath);

private:
    ProcessParamManager(QObject *parent = nullptr);
    ~ProcessParamManager();

    // 创建默认参数定义
    void createDefaultParameterDefinitions();
    
    // 创建默认工艺参数配置
    void createDefaultProfiles();

private:
    QList<ProcessParamDefinition> m_paramDefinitions;
    QMap<QString, ProcessParamDefinition> m_paramDefinitionMap;
}; 