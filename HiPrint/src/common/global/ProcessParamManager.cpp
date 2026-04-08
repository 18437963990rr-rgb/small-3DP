#include "ProcessParamManager.h"
#include <QDateTime>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QFile>
#include <QDebug>
#include <QSqlQuery>

ProcessParamManager::ProcessParamManager(QObject *parent)
    : QObject(parent)
{
    createDefaultParameterDefinitions();
}

ProcessParamManager::~ProcessParamManager()
{
}

ProcessParamManager& ProcessParamManager::instance()
{
    static ProcessParamManager instance;
    return instance;
}

bool ProcessParamManager::initTables()
{
    auto& db = SqlQuery::instance();
    
    // 创建工艺参数配置表
    QString createProfilesTable = QString(
        "CREATE TABLE IF NOT EXISTS %1 ("
        "%2 INTEGER PRIMARY KEY AUTOINCREMENT, "
        "%3 TEXT NOT NULL, "
        "%4 TEXT, "
        "%5 TEXT NOT NULL, "
        "%6 TEXT NOT NULL, "
        "%7 INTEGER DEFAULT 0"
        ")").arg(PROCESS_PARAM_PROFILES_TABLE)
           .arg(KEY_PROFILE_ID)
           .arg(KEY_PROFILE_NAME)
           .arg(KEY_PROFILE_DESCRIPTION)
           .arg(KEY_CREATE_TIME)
           .arg(KEY_UPDATE_TIME)
           .arg(KEY_IS_DEFAULT);
    
    QSqlQuery query(db.getDataBase());
    if (!query.exec(createProfilesTable)) {
        qDebug() << "Failed to create process param profiles table:" << query.lastError().text();
        return false;
    }
    
    // 创建工艺参数值表
    QString createValuesTable = QString(
        "CREATE TABLE IF NOT EXISTS %1 ("
        "%2 INTEGER PRIMARY KEY AUTOINCREMENT, "
        "%3 INTEGER NOT NULL, "
        "%4 TEXT NOT NULL, "
        "%5 TEXT NOT NULL, "
        "%6 INTEGER NOT NULL, "
        "%7 TEXT, "
        "%8 TEXT, "
        "%9 TEXT, "
        "%10 TEXT, "
        "FOREIGN KEY(%3) REFERENCES %11(%12)"
        ")").arg(PROCESS_PARAM_VALUES_TABLE)
           .arg(KEY_PARAM_ID)
           .arg(KEY_PROFILE_ID)
           .arg(KEY_PARAM_NAME)
           .arg(KEY_PARAM_VALUE)
           .arg(KEY_PARAM_TYPE)
           .arg(KEY_PARAM_UNIT)
           .arg(KEY_PARAM_MIN)
           .arg(KEY_PARAM_MAX)
           .arg(KEY_PARAM_DESCRIPTION)
           .arg(PROCESS_PARAM_PROFILES_TABLE)
           .arg(KEY_PROFILE_ID);
    
    if (!query.exec(createValuesTable)) {
        qDebug() << "Failed to create process param values table:" << query.lastError().text();
        return false;
    }
    
    // 创建默认配置
    createDefaultProfiles();
    
    return true;
}

bool ProcessParamManager::createProfile(const QString& name, const QString& description)
{
    auto& db = SqlQuery::instance();
    
    QString currentTime = QDateTime::currentDateTime().toString("yyyy-MM-dd hh:mm:ss");
    
    QString insertQuery = QString(
        "INSERT INTO %1 (%2, %3, %4, %5, %6) VALUES (?, ?, ?, ?, ?)")
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_PROFILE_NAME)
        .arg(KEY_PROFILE_DESCRIPTION)
        .arg(KEY_CREATE_TIME)
        .arg(KEY_UPDATE_TIME)
        .arg(KEY_IS_DEFAULT);
    
    QSqlQuery query(db.getDataBase());
    query.prepare(insertQuery);
    query.addBindValue(name);
    query.addBindValue(description);
    query.addBindValue(currentTime);
    query.addBindValue(currentTime);
    query.addBindValue(0);
    
    if (!query.exec()) {
        qDebug() << "Failed to create profile:" << name << query.lastError().text();
        return false;
    }
    
    int profileId = query.lastInsertId().toInt();
    
    // 为所有参数创建默认值
    for (const auto& paramDef : m_paramDefinitions) {
        setParameterValue(profileId, paramDef.name, paramDef.defaultValue);
    }
    
    return true;
}

bool ProcessParamManager::deleteProfile(int profileId)
{
    auto& db = SqlQuery::instance();
    
    // 删除参数值
    QString deleteValuesQuery = QString(
        "DELETE FROM %1 WHERE %2 = ?")
        .arg(PROCESS_PARAM_VALUES_TABLE)
        .arg(KEY_PROFILE_ID);
    
    QSqlQuery query1(db.getDataBase());
    query1.prepare(deleteValuesQuery);
    query1.addBindValue(profileId);
    
    if (!query1.exec()) {
        qDebug() << "Failed to delete profile values:" << query1.lastError().text();
        return false;
    }
    
    // 删除配置
    QString deleteProfileQuery = QString(
        "DELETE FROM %1 WHERE %2 = ?")
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_PROFILE_ID);
    
    QSqlQuery query2(db.getDataBase());
    query2.prepare(deleteProfileQuery);
    query2.addBindValue(profileId);
    
    return query2.exec();
}

bool ProcessParamManager::updateProfile(int profileId, const QString& name, const QString& description)
{
    auto& db = SqlQuery::instance();
    
    QString currentTime = QDateTime::currentDateTime().toString("yyyy-MM-dd hh:mm:ss");
    
    QString updateQuery = QString(
        "UPDATE %1 SET %2 = ?, %3 = ?, %4 = ? WHERE %5 = ?")
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_PROFILE_NAME)
        .arg(KEY_PROFILE_DESCRIPTION)
        .arg(KEY_UPDATE_TIME)
        .arg(KEY_PROFILE_ID);
    
    QSqlQuery query(db.getDataBase());
    query.prepare(updateQuery);
    query.addBindValue(name);
    query.addBindValue(description);
    query.addBindValue(currentTime);
    query.addBindValue(profileId);
    
    return query.exec();
}

bool ProcessParamManager::setDefaultProfile(int profileId)
{
    auto& db = SqlQuery::instance();
    
    // 先清除所有默认标记
    QString clearDefaultQuery = QString(
        "UPDATE %1 SET %2 = 0")
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_IS_DEFAULT);
    
    QSqlQuery query1(db.getDataBase());
    if (!query1.exec(clearDefaultQuery)) {
        qDebug() << "Failed to clear default profiles:" << query1.lastError().text();
        return false;
    }
    
    // 设置新的默认配置
    QString setDefaultQuery = QString(
        "UPDATE %1 SET %2 = 1 WHERE %3 = ?")
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_IS_DEFAULT)
        .arg(KEY_PROFILE_ID);
    
    QSqlQuery query2(db.getDataBase());
    query2.prepare(setDefaultQuery);
    query2.addBindValue(profileId);
    
    return query2.exec();
}

QList<ProcessParamProfile> ProcessParamManager::getProfileList()
{
    auto& db = SqlQuery::instance();
    QList<ProcessParamProfile> profiles;
    
    QString query = QString(
        "SELECT %1, %2, %3, %4, %5, %6 FROM %7 ORDER BY %8")
        .arg(KEY_PROFILE_ID)
        .arg(KEY_PROFILE_NAME)
        .arg(KEY_PROFILE_DESCRIPTION)
        .arg(KEY_CREATE_TIME)
        .arg(KEY_UPDATE_TIME)
        .arg(KEY_IS_DEFAULT)
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_PROFILE_ID);
    
    QSqlQuery sqlQuery(db.getDataBase());
    if (sqlQuery.exec(query)) {
        while (sqlQuery.next()) {
            ProcessParamProfile profile;
            profile.id = sqlQuery.value(KEY_PROFILE_ID).toInt();
            profile.name = sqlQuery.value(KEY_PROFILE_NAME).toString();
            profile.description = sqlQuery.value(KEY_PROFILE_DESCRIPTION).toString();
            profile.createTime = QDateTime::fromString(sqlQuery.value(KEY_CREATE_TIME).toString(), "yyyy-MM-dd hh:mm:ss");
            profile.updateTime = QDateTime::fromString(sqlQuery.value(KEY_UPDATE_TIME).toString(), "yyyy-MM-dd hh:mm:ss");
            profile.isDefault = sqlQuery.value(KEY_IS_DEFAULT).toBool();
            profile.parameters = getProfileParameters(profile.id);
            profiles.append(profile);
        }
    } else {
        qDebug() << "Failed to get profile list:" << sqlQuery.lastError().text();
    }
    
    return profiles;
}

ProcessParamProfile ProcessParamManager::getProfile(int profileId)
{
    auto& db = SqlQuery::instance();
    ProcessParamProfile profile;
    
    QString query = QString(
        "SELECT %1, %2, %3, %4, %5, %6 FROM %7 WHERE %8 = ?")
        .arg(KEY_PROFILE_ID)
        .arg(KEY_PROFILE_NAME)
        .arg(KEY_PROFILE_DESCRIPTION)
        .arg(KEY_CREATE_TIME)
        .arg(KEY_UPDATE_TIME)
        .arg(KEY_IS_DEFAULT)
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_PROFILE_ID);
    
    QSqlQuery sqlQuery(db.getDataBase());
    sqlQuery.prepare(query);
    sqlQuery.addBindValue(profileId);
    
    if (sqlQuery.exec() && sqlQuery.next()) {
        profile.id = sqlQuery.value(KEY_PROFILE_ID).toInt();
        profile.name = sqlQuery.value(KEY_PROFILE_NAME).toString();
        profile.description = sqlQuery.value(KEY_PROFILE_DESCRIPTION).toString();
        profile.createTime = QDateTime::fromString(sqlQuery.value(KEY_CREATE_TIME).toString(), "yyyy-MM-dd hh:mm:ss");
        profile.updateTime = QDateTime::fromString(sqlQuery.value(KEY_UPDATE_TIME).toString(), "yyyy-MM-dd hh:mm:ss");
        profile.isDefault = sqlQuery.value(KEY_IS_DEFAULT).toBool();
        profile.parameters = getProfileParameters(profileId);
    } else {
        qDebug() << "Failed to get profile:" << sqlQuery.lastError().text();
    }
    
    return profile;
}

ProcessParamProfile ProcessParamManager::getDefaultProfile()
{
    auto& db = SqlQuery::instance();
    ProcessParamProfile profile;
    
    QString query = QString(
        "SELECT %1, %2, %3, %4, %5, %6 FROM %7 WHERE %8 = 1 LIMIT 1")
        .arg(KEY_PROFILE_ID)
        .arg(KEY_PROFILE_NAME)
        .arg(KEY_PROFILE_DESCRIPTION)
        .arg(KEY_CREATE_TIME)
        .arg(KEY_UPDATE_TIME)
        .arg(KEY_IS_DEFAULT)
        .arg(PROCESS_PARAM_PROFILES_TABLE)
        .arg(KEY_IS_DEFAULT);
    
    QSqlQuery sqlQuery(db.getDataBase());
    if (sqlQuery.exec(query) && sqlQuery.next()) {
        int profileId = sqlQuery.value(KEY_PROFILE_ID).toInt();
        profile = getProfile(profileId);
    } else {
        qDebug() << "Failed to get default profile:" << sqlQuery.lastError().text();
    }
    
    return profile;
}

bool ProcessParamManager::setParameterValue(int profileId, const QString& paramName, const QVariant& value)
{
    auto& db = SqlQuery::instance();
    
    // 检查是否已存在
    QString checkQuery = QString(
        "SELECT COUNT(*) FROM %1 WHERE %2 = ? AND %3 = ?")
        .arg(PROCESS_PARAM_VALUES_TABLE)
        .arg(KEY_PROFILE_ID)
        .arg(KEY_PARAM_NAME);
    
    QSqlQuery checkQueryObj(db.getDataBase());
    checkQueryObj.prepare(checkQuery);
    checkQueryObj.addBindValue(profileId);
    checkQueryObj.addBindValue(paramName);
    
    if (checkQueryObj.exec() && checkQueryObj.next()) {
        int count = checkQueryObj.value(0).toInt();
        
        if (count > 0) {
            // 更新现有值
            QString updateQuery = QString(
                "UPDATE %1 SET %2 = ? WHERE %3 = ? AND %4 = ?")
                .arg(PROCESS_PARAM_VALUES_TABLE)
                .arg(KEY_PARAM_VALUE)
                .arg(KEY_PROFILE_ID)
                .arg(KEY_PARAM_NAME);
            
            QSqlQuery updateQueryObj(db.getDataBase());
            updateQueryObj.prepare(updateQuery);
            updateQueryObj.addBindValue(value);
            updateQueryObj.addBindValue(profileId);
            updateQueryObj.addBindValue(paramName);
            
            return updateQueryObj.exec();
        } else {
            // 插入新值
            ProcessParamDefinition paramDef = getParameterDefinition(paramName);
            QString insertQuery = QString(
                "INSERT INTO %1 (%2, %3, %4, %5, %6, %7, %8, %9) VALUES (?, ?, ?, ?, ?, ?, ?, ?)")
                .arg(PROCESS_PARAM_VALUES_TABLE)
                .arg(KEY_PROFILE_ID)
                .arg(KEY_PARAM_NAME)
                .arg(KEY_PARAM_VALUE)
                .arg(KEY_PARAM_TYPE)
                .arg(KEY_PARAM_UNIT)
                .arg(KEY_PARAM_MIN)
                .arg(KEY_PARAM_MAX)
                .arg(KEY_PARAM_DESCRIPTION);
            
            QSqlQuery insertQueryObj(db.getDataBase());
            insertQueryObj.prepare(insertQuery);
            insertQueryObj.addBindValue(profileId);
            insertQueryObj.addBindValue(paramName);
            insertQueryObj.addBindValue(value);
            insertQueryObj.addBindValue((int)paramDef.type);
            insertQueryObj.addBindValue(paramDef.unit);
            insertQueryObj.addBindValue(paramDef.minValue.toString());
            insertQueryObj.addBindValue(paramDef.maxValue.toString());
            insertQueryObj.addBindValue(paramDef.description);
            
            return insertQueryObj.exec();
        }
    }
    
    return false;
}

QVariant ProcessParamManager::getParameterValue(int profileId, const QString& paramName, const QVariant& defaultValue)
{
    auto& db = SqlQuery::instance();
    
    QString query = QString(
        "SELECT %1 FROM %2 WHERE %3 = ? AND %4 = ?")
        .arg(KEY_PARAM_VALUE)
        .arg(PROCESS_PARAM_VALUES_TABLE)
        .arg(KEY_PROFILE_ID)
        .arg(KEY_PARAM_NAME);
    
    QSqlQuery sqlQuery(db.getDataBase());
    sqlQuery.prepare(query);
    sqlQuery.addBindValue(profileId);
    sqlQuery.addBindValue(paramName);
    
    if (sqlQuery.exec() && sqlQuery.next()) {
        return sqlQuery.value(KEY_PARAM_VALUE);
    }
    
    return defaultValue;
}

QMap<QString, QVariant> ProcessParamManager::getProfileParameters(int profileId)
{
    auto& db = SqlQuery::instance();
    QMap<QString, QVariant> parameters;
    
    QString query = QString(
        "SELECT %1, %2 FROM %3 WHERE %4 = ?")
        .arg(KEY_PARAM_NAME)
        .arg(KEY_PARAM_VALUE)
        .arg(PROCESS_PARAM_VALUES_TABLE)
        .arg(KEY_PROFILE_ID);
    
    QSqlQuery sqlQuery(db.getDataBase());
    sqlQuery.prepare(query);
    sqlQuery.addBindValue(profileId);
    
    if (sqlQuery.exec()) {
        while (sqlQuery.next()) {
            QString paramName = sqlQuery.value(KEY_PARAM_NAME).toString();
            QVariant paramValue = sqlQuery.value(KEY_PARAM_VALUE);
            parameters[paramName] = paramValue;
        }
    } else {
        qDebug() << "Failed to get profile parameters:" << sqlQuery.lastError().text();
    }
    
    return parameters;
}

QList<ProcessParamDefinition> ProcessParamManager::getParameterDefinitions()
{
    return m_paramDefinitions;
}

ProcessParamDefinition ProcessParamManager::getParameterDefinition(const QString& paramName)
{
    return m_paramDefinitionMap.value(paramName);
}

// 便捷方法实现
QVariant ProcessParamManager::getDefaultParameterValue(const QString& paramName, const QVariant& defaultValue)
{
    ProcessParamProfile defaultProfile = getDefaultProfile();
    if (defaultProfile.id == 0) {
        return defaultValue;
    }
    
    return getParameterValue(defaultProfile.id, paramName, defaultValue);
}

QMap<QString, QVariant> ProcessParamManager::getDefaultProfileParameters()
{
    ProcessParamProfile defaultProfile = getDefaultProfile();
    if (defaultProfile.id == 0) {
        return QMap<QString, QVariant>();
    }
    
    return getProfileParameters(defaultProfile.id);
}

ProcessParamProfile ProcessParamManager::getProfileByName(const QString& name)
{
    QList<ProcessParamProfile> profiles = getProfileList();
    for (const auto& profile : profiles) {
        if (profile.name == name) {
            return profile;
        }
    }
    
    return ProcessParamProfile();
}

int ProcessParamManager::getProfileIdByName(const QString& name)
{
    ProcessParamProfile profile = getProfileByName(name);
    return profile.id;
}

QStringList ProcessParamManager::getProfileNames()
{
    QStringList names;
    QList<ProcessParamProfile> profiles = getProfileList();
    for (const auto& profile : profiles) {
        names.append(profile.name);
    }
    
    return names;
}

void ProcessParamManager::createDefaultParameterDefinitions()
{
    // 打印参数
    ProcessParamDefinition printLayerThickness = {
        "print_layer_thickness", "打印层厚", PARAM_TYPE_FLOAT, "mm", 0.01, 0.5, "打印层厚度", 0.3
    };
    
    ProcessParamDefinition xySpeed = {
        "xy_speed", "XY轴移动速度", PARAM_TYPE_FLOAT, "mm/s", 1.0, 100.0, "XY轴移动速度", 50.0
    };
    
    ProcessParamDefinition printXResolution = {
        "print_x_resolution", "X轴分辨率", PARAM_TYPE_FLOAT, "dpi", 100.0, 1200.0, "X轴打印分辨率", 300.0
    };
    
    ProcessParamDefinition inkVolume = {
        "ink_volume", "墨量", PARAM_TYPE_FLOAT, "ml", 0.0, 1000.0, "打印墨量", 100.0
    };
    
    // 铺砂参数
    ProcessParamDefinition sandSpreadingSpeed = {
        "sand_spreading_speed", "铺砂速度", PARAM_TYPE_FLOAT, "mm/s", 1.0, 100.0, "铺砂速度", 30.0
    };
    
    ProcessParamDefinition sandSpreadingDosingSpeed = {
        "sand_spreading_dosing_speed", "铺砂定量速度", PARAM_TYPE_FLOAT, "mm/s", 1.0, 100.0, "铺砂定量速度", 20.0
    };
    
    ProcessParamDefinition sandSpreadingEndPosition = {
        "sand_spreading_end_position", "铺砂结束位置", PARAM_TYPE_FLOAT, "mm", 0.0, 2000.0, "铺砂结束位置", 1000.0
    };
    
    // 供砂参数
    ProcessParamDefinition feedingMode = {
        "feeding_mode", "供砂模式", PARAM_TYPE_INT, "", 0, 1, "供砂模式：0=自动，1=手动", 0
    };
    
    ProcessParamDefinition newSandCatalystRatio = {
        "new_sand_catalyst_ratio", "新砂催化剂比例", PARAM_TYPE_FLOAT, "%", 0.0, 100.0, "新砂催化剂比例", 5.0
    };
    
    ProcessParamDefinition oldSandCatalystRatio = {
        "old_sand_catalyst_ratio", "旧砂催化剂比例", PARAM_TYPE_FLOAT, "%", 0.0, 100.0, "旧砂催化剂比例", 3.0
    };
    
    // 抽砂参数
    ProcessParamDefinition newSandSuctionTime = {
        "new_sand_suction_time", "新砂抽砂时间", PARAM_TYPE_FLOAT, "s", 0.0, 60.0, "新砂抽砂时间", 10.0
    };
    
    ProcessParamDefinition oldSandSuctionTime = {
        "old_sand_suction_time", "旧砂抽砂时间", PARAM_TYPE_FLOAT, "s", 0.0, 60.0, "旧砂抽砂时间", 8.0
    };
    
    ProcessParamDefinition mixedSandNewSuctionTime = {
        "mixed_sand_new_suction_time", "混合砂新砂抽砂时间", PARAM_TYPE_FLOAT, "s", 0.0, 60.0, "混合砂新砂抽砂时间", 12.0
    };
    
    ProcessParamDefinition mixedSandOldSuctionTime = {
        "mixed_sand_old_suction_time", "混合砂旧砂抽砂时间", PARAM_TYPE_FLOAT, "s", 0.0, 60.0, "混合砂旧砂抽砂时间", 10.0
    };
    
    // 撒砂参数
    ProcessParamDefinition sandScatteringSpeed = {
        "sand_scattering_speed", "撒砂速度", PARAM_TYPE_FLOAT, "mm/s", 1.0, 100.0, "撒砂速度", 25.0
    };
    
    // 添加到列表和映射
    m_paramDefinitions = {
        printLayerThickness, xySpeed, printXResolution, inkVolume,
        sandSpreadingSpeed, sandSpreadingDosingSpeed, sandSpreadingEndPosition,
        feedingMode, newSandCatalystRatio, oldSandCatalystRatio,
        newSandSuctionTime, oldSandSuctionTime, mixedSandNewSuctionTime, mixedSandOldSuctionTime,
        sandScatteringSpeed
    };
    
    for (const auto& paramDef : m_paramDefinitions) {
        m_paramDefinitionMap[paramDef.name] = paramDef;
    }
}

void ProcessParamManager::createDefaultProfiles()
{
    // 检查是否已有默认配置
    auto profiles = getProfileList();
    if (!profiles.isEmpty()) {
        return;
    }
    
    // 创建默认配置
    createProfile("高砂硅,层厚0.3mm,高速，300DPI", "适用于高精度打印的工艺参数配置");
    createProfile("标准配置", "标准工艺参数配置");
    createProfile("快速打印配置", "适用于快速打印的工艺参数配置");
    
    // 设置第一个为默认配置
    auto profileList = getProfileList();
    if (!profileList.isEmpty()) {
        setDefaultProfile(profileList.first().id);
    }
}

// 导入导出功能实现
bool ProcessParamManager::exportProfile(int profileId, const QString& filePath)
{
    ProcessParamProfile profile = getProfile(profileId);
    if (profile.id == 0) {
        return false;
    }
    
    QJsonObject jsonProfile;
    jsonProfile["id"] = profile.id;
    jsonProfile["name"] = profile.name;
    jsonProfile["description"] = profile.description;
    jsonProfile["createTime"] = profile.createTime.toString("yyyy-MM-dd hh:mm:ss");
    jsonProfile["updateTime"] = profile.updateTime.toString("yyyy-MM-dd hh:mm:ss");
    jsonProfile["isDefault"] = profile.isDefault;
    
    QJsonObject jsonParams;
    for (auto it = profile.parameters.begin(); it != profile.parameters.end(); ++it) {
        jsonParams[it.key()] = QJsonValue::fromVariant(it.value());
    }
    jsonProfile["parameters"] = jsonParams;
    
    QJsonDocument doc(jsonProfile);
    QFile file(filePath);
    if (file.open(QIODevice::WriteOnly)) {
        file.write(doc.toJson());
        return true;
    }
    
    return false;
}

bool ProcessParamManager::importProfile(const QString& filePath)
{
    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly)) {
        return false;
    }
    
    QJsonDocument doc = QJsonDocument::fromJson(file.readAll());
    QJsonObject jsonProfile = doc.object();
    
    QString name = jsonProfile["name"].toString();
    QString description = jsonProfile["description"].toString();
    
    if (!createProfile(name, description)) {
        return false;
    }
    
    auto profileList = getProfileList();
    int profileId = profileList.last().id;
    
    QJsonObject jsonParams = jsonProfile["parameters"].toObject();
    for (auto it = jsonParams.begin(); it != jsonParams.end(); ++it) {
        setParameterValue(profileId, it.key(), it.value().toVariant());
    }
    
    return true;
}

bool ProcessParamManager::exportAllProfiles(const QString& filePath)
{
    QList<ProcessParamProfile> profiles = getProfileList();
    
    QJsonArray jsonProfiles;
    for (const auto& profile : profiles) {
        QJsonObject jsonProfile;
        jsonProfile["id"] = profile.id;
        jsonProfile["name"] = profile.name;
        jsonProfile["description"] = profile.description;
        jsonProfile["createTime"] = profile.createTime.toString("yyyy-MM-dd hh:mm:ss");
        jsonProfile["updateTime"] = profile.updateTime.toString("yyyy-MM-dd hh:mm:ss");
        jsonProfile["isDefault"] = profile.isDefault;
        
        QJsonObject jsonParams;
        for (auto it = profile.parameters.begin(); it != profile.parameters.end(); ++it) {
            jsonParams[it.key()] = QJsonValue::fromVariant(it.value());
        }
        jsonProfile["parameters"] = jsonParams;
        
        jsonProfiles.append(jsonProfile);
    }
    
    QJsonDocument doc(jsonProfiles);
    QFile file(filePath);
    if (file.open(QIODevice::WriteOnly)) {
        file.write(doc.toJson());
        return true;
    }
    
    return false;
}

bool ProcessParamManager::importAllProfiles(const QString& filePath)
{
    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly)) {
        return false;
    }
    
    QJsonDocument doc = QJsonDocument::fromJson(file.readAll());
    QJsonArray jsonProfiles = doc.array();
    
    for (const auto& jsonValue : jsonProfiles) {
        QJsonObject jsonProfile = jsonValue.toObject();
        QString name = jsonProfile["name"].toString();
        QString description = jsonProfile["description"].toString();
        
        if (createProfile(name, description)) {
            auto profileList = getProfileList();
            int profileId = profileList.last().id;
            
            QJsonObject jsonParams = jsonProfile["parameters"].toObject();
            for (auto it = jsonParams.begin(); it != jsonParams.end(); ++it) {
                setParameterValue(profileId, it.key(), it.value().toVariant());
            }
        }
    }
    
    return true;
} 