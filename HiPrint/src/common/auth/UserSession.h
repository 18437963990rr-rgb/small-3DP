#pragma once
#include <QString>

class UserSession
{
public:
    static UserSession& instance() {
        static UserSession instance;
        return instance;
    }
    void setCurrentUser(const QString& user) { m_user = user; }
    QString currentUser() const { return m_user; }
private:
    UserSession() = default;
    QString m_user;
};
