#pragma once
#include <QObject>
#include <QSqlDatabase>
#include <QVariant>
#include <QMap>

class SqlQuery : public QObject {

    Q_OBJECT

public:
    static SqlQuery& instance();
    bool connectDB(const QString& dbFilePath);
    bool insertValue(const QString& table, const QString& key, const QVariant& value);
    bool updateValue(const QString& table, const QString& key, const QVariant& value);
    QVariant getValue(const QString& table, const QString& key, const QVariant& def = {}) const;
    bool deleteValue(const QString& table, const QString& key, const QVariant& def = {}) const;
    bool deleteTabValues(const QString& table)const;
    QMap<QString, QVariant> getTabValues(const QString& table);
    bool updateTabValues(const QString& table, const QMap<QString, QVariant>& map);
    bool beginTransaction();
    bool commitTransaction();
    bool rollbackTransaction();
    
    QSqlDatabase getDataBase();
    QString lastError() const;
private:
    explicit SqlQuery(QObject* parent = nullptr);
    ~SqlQuery();
    Q_DISABLE_COPY(SqlQuery)
    QSqlDatabase m_db;
    QString m_lastError;
    void setLastError(const QString& err);
};
