#include "PasswordHelper.h"
#include "./3rdparty/aes/QAESEncryption.h"
#include "../db/SqlQuery.h"



QByteArray PasswordHelper::getOrCreateAesKey(int len)
{
    QByteArray base64Key = SqlQuery::instance().getValue("config_key", "user_aes_key").toByteArray();
    QByteArray key;
    if (base64Key.isEmpty()) {
        key.resize(len);
        for (int i = 0; i < len; ++i)
            key[i] = static_cast<char>(QRandomGenerator::system()->bounded(256));
        SqlQuery::instance().insertValue("config_key", "user_aes_key", key.toBase64());
    }
    else {
        key = QByteArray::fromBase64(base64Key);
        if (key.size() != len) {
            // 密钥长度异常，重新生成
            key.resize(len);
            for (int i = 0; i < len; ++i)
                key[i] = static_cast<char>(QRandomGenerator::system()->bounded(256));
            SqlQuery::instance().insertValue("config_key", "user_aes_key", key.toBase64());
        }
    }
    return key;
}


QByteArray PasswordHelper::encryptPassword(const QString& password, const QByteArray& key)
{
    QAESEncryption enc(QAESEncryption::AES_128, QAESEncryption::ECB, QAESEncryption::PKCS7);
    return enc.encode(password.toUtf8(), key).toBase64();
}

bool PasswordHelper::verifyPassword(const QString& input, const QByteArray& dbCipher, const QByteArray& key)
{
    QByteArray inputCipher = encryptPassword(input, key);
    return inputCipher == dbCipher;
}
