#include "JsonFileReader.h"
#include <QFile>
#include <QJsonParseError>
#include <QDebug>

JsonFileReader::JsonFileReader(QObject* parent)
    : QObject(parent)
{
}

bool JsonFileReader::load(const QString& filePath)
{
    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly)) {
        qWarning() << "[JsonFileReader] 无法打开文件:" << filePath;
        return false;
    }
    QByteArray jsonData = file.readAll();
    file.close();

    QJsonParseError err;
    QJsonDocument doc = QJsonDocument::fromJson(jsonData, &err);
    if (err.error != QJsonParseError::NoError) {
        qWarning() << "[JsonFileReader] 解析错误:" << err.errorString();
        return false;
    }
    if (!doc.isObject()) {
        qWarning() << "[JsonFileReader] 文件根不是对象";
        return false;
    }

    m_root = doc.object();
    removeCommentFields(m_root); // 自动移除所有 _comment 字段
    return true;
}

QJsonObject JsonFileReader::rootObject() const
{
    return m_root;
}

QJsonObject JsonFileReader::object(const QString& key) const
{
    QJsonObject obj;
    if (m_root.contains(key) && m_root[key].isObject())
        obj = m_root[key].toObject();
    removeCommentFields(obj);
    return obj;
}

QJsonArray JsonFileReader::array(const QString& key) const
{
    if (m_root.contains(key) && m_root[key].isArray())
        return m_root[key].toArray();
    return QJsonArray();
}

QString JsonFileReader::string(const QString& key, const QString& defaultValue) const
{
    if (m_root.contains(key))
        return m_root.value(key).toString(defaultValue);
    return defaultValue;
}

QVariant JsonFileReader::value(const QString& key, const QVariant& defaultValue) const
{
    if (!m_root.contains(key))
        return defaultValue;
    const QJsonValue& v = m_root[key];
    if (v.isString()) return v.toString();
    if (v.isDouble()) return v.toDouble();
    if (v.isBool())   return v.toBool();
    if (v.isArray())  return v.toArray();
    if (v.isObject()) return v.toObject();
    return defaultValue;
}

bool JsonFileReader::contains(const QString& key) const
{
    return m_root.contains(key);
}

QStringList JsonFileReader::keys() const
{
    QStringList list;
    for (const QString& k : m_root.keys()) {
        if (!k.startsWith("_comment"))
            list << k;
    }
    return list;
}

// 递归移除以_comment开头的字段
void JsonFileReader::removeCommentFields(QJsonObject& obj) const
{
    QList<QString> toRemove;
    for (auto it = obj.begin(); it != obj.end(); ++it) {
        if (it.key().startsWith("_comment"))
            toRemove << it.key();
        else if (it.value().isObject()) {
            QJsonObject child = it.value().toObject();
            removeCommentFields(child);
            obj[it.key()] = child;
        }
    }
    for (const QString& k : toRemove)
        obj.remove(k);
}
