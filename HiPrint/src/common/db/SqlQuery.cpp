#include "SqlQuery.h"
#include <QSqlQuery>
#include <QSqlError>
#include <QDebug>


SqlQuery& SqlQuery::instance()
{
    static SqlQuery instance;
    return instance;
}


SqlQuery::SqlQuery(QObject* parent)
    : QObject(parent)
{
}

SqlQuery::~SqlQuery()
{
    if (m_db.isOpen()) {
        m_db.close();
    }
}

bool SqlQuery::connectDB(const QString& dbFilePath)
{
    if (QSqlDatabase::contains("qt_sql_default_connection")) {
        m_db = QSqlDatabase::database("qt_sql_default_connection");
    }
    else {
        m_db = QSqlDatabase::addDatabase("QSQLITE");
    }
    m_db.setDatabaseName(dbFilePath);
    if (!m_db.open()) {
        setLastError("Failed to open database: " + m_db.lastError().text());
        return false;
    }

    return true;
}

bool SqlQuery::insertValue(const QString& table, const QString& key, const QVariant& value)
{
    QSqlQuery query(m_db);
    QString querySentence = QString("REPLACE INTO %1(key, value) VALUES('%2', '%3')")
        .arg(table).arg(key).arg(value.toString());
    return query.exec(querySentence);
    
}

// 设置值
bool SqlQuery::updateValue(const QString& tableName, const QString& key, const QVariant& value)
{
    QSqlQuery query(m_db);
    QString querySentence = QString("UPDATE %1 SET value = '%2' WHERE  key = '%3'")
        .arg(tableName).arg(value.toString()).arg(key);
    if (!query.exec(querySentence)) {
        QString errMessage = query.lastError().text();
        setLastError("无法设置值: " + query.lastError().text());
        return false;
    }
    return true;
}

QVariant SqlQuery::getValue(const QString& table, const QString& key, const QVariant& def) const
{
    QSqlQuery query(m_db);
    QString querySentence = QString("SELECT value FROM %1 WHERE key = '%2'").arg(table).arg(key);
    query.prepare(querySentence);
    if (query.exec() && query.next()) {
        return query.value(0);
    }
    else {
        return def;
    }
}

bool SqlQuery::deleteValue(const QString& table, const QString& key, const QVariant& def) const
{
    QSqlQuery query(m_db);
    QString querySentence = QString("DELETE FROM %1 WHERE key = '%2'").arg(table).arg(key);
    query.prepare(querySentence);
    return query.exec();
}

bool SqlQuery::deleteTabValues(const QString& table)const
{
    QSqlQuery query(m_db);
    QString querySentence = QString("DELETE FROM %1").arg(table);
    query.prepare(querySentence);
    return query.exec();
}

QMap<QString, QVariant> SqlQuery::getTabValues(const QString& tableName)
{
    QMap<QString, QVariant> values;
    QSqlQuery query(m_db);
    query.prepare("SELECT key, value FROM " + tableName);
    if (query.exec()) {
        while (query.next()) {
            values[query.value(0).toString()] = query.value(1);
        }
    }
    else {
        setLastError("无法获取表值: " + query.lastError().text());
    }
    return values;
}

// 批量设置键值对
bool SqlQuery::updateTabValues(const QString& tableName, const QMap<QString, QVariant>& values)
{
    beginTransaction();
    bool success = true;
    QMapIterator<QString, QVariant> it(values);
    while (it.hasNext()) {
        it.next();
        if (!updateValue(tableName, it.key(), it.value())) {
            success = false;
            break;
        }
    }
    if (success) {
        return commitTransaction();
    }
    else {
        rollbackTransaction();
        return false;
    }
}

// 事务控制
bool SqlQuery::beginTransaction()
{
    return m_db.transaction();
}

bool SqlQuery::commitTransaction()
{
    return m_db.commit();
}

bool SqlQuery::rollbackTransaction()
{
    return m_db.rollback();
}

QSqlDatabase SqlQuery::getDataBase()
{
    return m_db;
}

QString SqlQuery::lastError() const
{
    return m_lastError;
}

void SqlQuery::setLastError(const QString& error)
{
    m_lastError = error;
}
