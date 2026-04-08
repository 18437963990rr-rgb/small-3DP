#pragma once

#include <QString>
#include <QDateTime>

// 工艺参数配置表
const QString PROCESS_PARAM_PROFILES_TABLE = "process_param_profiles";
const QString PROCESS_PARAM_VALUES_TABLE = "process_param_values";

// 工艺参数配置表字段
const QString KEY_PROFILE_ID = "profile_id";
const QString KEY_PROFILE_NAME = "profile_name";
const QString KEY_PROFILE_DESCRIPTION = "profile_description";
const QString KEY_CREATE_TIME = "create_time";
const QString KEY_UPDATE_TIME = "update_time";
const QString KEY_IS_DEFAULT = "is_default";

// 工艺参数值表字段
const QString KEY_PARAM_ID = "param_id";
const QString KEY_PARAM_NAME = "param_name";
const QString KEY_PARAM_VALUE = "param_value";
const QString KEY_PARAM_TYPE = "param_type";
const QString KEY_PARAM_UNIT = "param_unit";
const QString KEY_PARAM_MIN = "param_min";
const QString KEY_PARAM_MAX = "param_max";
const QString KEY_PARAM_DESCRIPTION = "param_description";

// 工艺参数类型枚举
enum ProcessParamType {
    PARAM_TYPE_INT = 0,
    PARAM_TYPE_FLOAT = 1,
    PARAM_TYPE_BOOL = 2,
    PARAM_TYPE_STRING = 3
};

// 工艺参数定义
struct ProcessParamDefinition {
    QString name;           // 参数名称
    QString displayName;    // 显示名称
    ProcessParamType type;  // 参数类型
    QString unit;           // 单位
    QVariant minValue;      // 最小值
    QVariant maxValue;      // 最大值
    QString description;    // 描述
    QVariant defaultValue; // 默认值
};

// 工艺参数配置
struct ProcessParamProfile {
    int id;                 // 配置ID
    QString name;           // 配置名称
    QString description;    // 配置描述
    QDateTime createTime;   // 创建时间
    QDateTime updateTime;   // 更新时间
    bool isDefault;         // 是否为默认配置
    QMap<QString, QVariant> parameters; // 参数值映射
}; 