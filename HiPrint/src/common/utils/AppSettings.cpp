#include "AppSettings.h"
#include <QStandardPaths>

// 静态常量配置文件路径
const QString AppSettings::m_settingsFile = "./HiPrintApp.ini";

// 初始化静态成员变量
QSettings AppSettings::m_settings(AppSettings::m_settingsFile, QSettings::IniFormat);
QMutex AppSettings::m_mutex;

// 设置语言
void AppSettings::setLanguage(const QString& lang) {
    QMutexLocker locker(&m_mutex);
    m_settings.setValue("language", lang);
}

// 获取语言
QString AppSettings::getLanguage() {
    QMutexLocker locker(&m_mutex);
    if (m_settings.contains("language")) {
        return m_settings.value("language").toString();
    }
    return "zh_CN"; 
}

// 设置窗口几何信息
void AppSettings::setGeometry(const QByteArray& geometry) {
    QMutexLocker locker(&m_mutex);
    m_settings.setValue("geometry", geometry);
}

// 获取窗口几何信息
QByteArray AppSettings::getGeometry() {
    QMutexLocker locker(&m_mutex);
    return m_settings.value("geometry").toByteArray();
}

// 设置窗口状态
void AppSettings::setWindowState(const QByteArray& state) {
    QMutexLocker locker(&m_mutex);
    m_settings.setValue("windowState", state);
}

// 获取窗口状态
QByteArray AppSettings::getWindowState() {
    QMutexLocker locker(&m_mutex);
    return m_settings.value("windowState").toByteArray();
}

int AppSettings::getPccNum()
{
    return m_settings.value("System/pccNum", 1).toInt();
}

int AppSettings::getMaxHnum()
{
    return m_settings.value("System/maxHnum", 2).toInt();
}

// 设置图片目录路径
void AppSettings::setImageDirectory(const QString& path) {
    QMutexLocker locker(&m_mutex);
    m_settings.setValue("ImageDirectory/path", path);
}

// 获取图片目录路径
QString AppSettings::getImageDirectory() {
    QMutexLocker locker(&m_mutex);
    if (m_settings.contains("ImageDirectory/path")) {
        return m_settings.value("ImageDirectory/path").toString();
    }
    QString defaultPath = QStandardPaths::writableLocation(QStandardPaths::DocumentsLocation) + "/HiPrint/Images";
    return defaultPath;
}