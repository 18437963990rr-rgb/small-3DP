#include "UserManager.h"
#include "PasswordHelper.h"
#include <QFile>
#include <QJsonDocument>
#include <QJsonArray>
#include <QJsonObject>

UserManager::UserManager()
{
    m_key = PasswordHelper::getOrCreateAesKey(16);
}

UserManager::~UserManager()
{
}

UserManager& UserManager::instance()
{
    static UserManager instance;
    return instance;
}

bool UserManager::validateUser(const QString& username, const QString& password)
{
    QVariant dbValue = SqlQuery::instance().getValue("user_credentials", username, QVariant());
    if (!dbValue.isValid()) return false;
    QByteArray dbCipher = dbValue.toByteArray();
    return PasswordHelper::verifyPassword(password, dbCipher, m_key);
}

QStringList UserManager::getAllUsernames()
{
    QMap<QString, QVariant> users = SqlQuery::instance().getTabValues("user_credentials");
    return users.keys();
}

bool UserManager::registerUser(const QString& username, const QString& password)
{
    if (SqlQuery::instance().getValue("user_credentials", username).isValid()) return false;
    QByteArray cipher = PasswordHelper::encryptPassword(password, m_key);
    return SqlQuery::instance().insertValue("user_credentials", username, cipher);
}

void UserManager::initializeDefaultUsersIfNeeded(const QString& jsonPath)
{
    if (!getAllUsernames().isEmpty()) return;

    QFile file(jsonPath);
    if (!file.open(QIODevice::ReadOnly)) return;
    QByteArray data = file.readAll();
    file.close();

    QJsonDocument doc = QJsonDocument::fromJson(data);
    if (!doc.isArray()) return;

    for (const QJsonValue& v : doc.array()) {
        QJsonObject obj = v.toObject();
        QString username = obj.value("username").toString();
        QString password = obj.value("password").toString();
        if (!username.isEmpty() && !password.isEmpty()) {
            registerUser(username, password);
        }
    }
}

bool UserManager::deleteUser(const QString& username)
{
    if (username == "admin") return false; 
    return SqlQuery::instance().deleteValue("user_credentials", username);
}
