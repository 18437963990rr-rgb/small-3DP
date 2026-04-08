#pragma once

#include <QObject>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QVariant>

class JsonFileReader : public QObject
{
    Q_OBJECT
public:
    explicit JsonFileReader(QObject* parent = nullptr);

    // 读取并解析json文件，成功返回true
    bool load(const QString& filePath);

    // 获取根对象
    QJsonObject rootObject() const;

    // 按key获取子对象（自动跳过以"_comment"开头的字段）
    QJsonObject object(const QString& key) const;

    // 按key获取数组
    QJsonArray array(const QString& key) const;

    // 按key获取字符串
    QString string(const QString& key, const QString& defaultValue = "") const;

    // 按key获取值（QVariant，适合各种基本类型）
    QVariant value(const QString& key, const QVariant& defaultValue = QVariant()) const;

    // 检查某个key是否存在
    bool contains(const QString& key) const;

    // 返回所有一级key（不含_comment）
    QStringList keys() const;

private:
    void removeCommentFields(QJsonObject& obj) const;

    QJsonObject m_root;
};
