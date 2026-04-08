
#pragma once
#include <QString>
#include <QByteArray>
#include <QRandomGenerator>

class PasswordHelper
{
public:
    static QByteArray getOrCreateAesKey(int len = 16);
    static QByteArray encryptPassword(const QString& password, const QByteArray& key);
    static bool verifyPassword(const QString& input, const QByteArray& dbCipher, const QByteArray& key);
};
