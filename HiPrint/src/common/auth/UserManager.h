#pragma once
#include <QString>
#include <QStringList>
#include "../db/SqlQuery.h"

class UserManager
{
public:
    static UserManager& instance();
    bool validateUser(const QString& username, const QString& password);
    QStringList getAllUsernames();
    bool registerUser(const QString& username, const QString& password);
    void initializeDefaultUsersIfNeeded(const QString& jsonPath);
    bool deleteUser(const QString& username); 

private:
    UserManager();
    ~UserManager();
    Q_DISABLE_COPY(UserManager)
    QByteArray m_key;
};

