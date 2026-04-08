#ifndef APPSETTINGS_H
#define APPSETTINGS_H

#include <QString>
#include <QByteArray>
#include <QSettings>
#include <QMutex>

class AppSettings {
public:
    // 设置语言
    static void setLanguage(const QString& lang);

    // 获取语言
    static QString getLanguage();

    // 设置窗口几何信息
    static void setGeometry(const QByteArray& geometry);

    // 获取窗口几何信息
    static QByteArray getGeometry();

    // 设置窗口状态
    static void setWindowState(const QByteArray& state);

    // 获取窗口状态
    static QByteArray getWindowState();

    static int getPccNum();
    static int getMaxHnum();

    // 设置图片目录路径
    static void setImageDirectory(const QString& path);
    
    // 获取图片目录路径
    static QString getImageDirectory();

private:
    // 静态常量的配置文件路径
    static const QString m_settingsFile;
    static QSettings m_settings;
    static QMutex m_mutex;
};

#endif // APPSETTINGS_H
