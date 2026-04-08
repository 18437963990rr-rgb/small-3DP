#include "HiPrint.h"
#include <QtWidgets/QApplication>
#include <QObject>
#include <QTranslator>
#include <QSettings>
#include <QGuiApplication>
#include <QRect>
#include <QScreen>

#include "../common/crash/CrashHandler.h"
#include "../common/global/ParameterManager.h"
#include "../common/auth/UserManager.h"
#include "../common/auth/LoginController.h"
#include "../common/auth/UserSession.h"
#include "../common/utils/MessageBoxHelper.h"
#include "../common/utils/AppSettings.h"


int main(int argc, char *argv[])
{
    QApplication a(argc, argv);

    //初始化异常崩溃处理器
    CrashHandler::instance().initialize();

    // 加载上次保存的语言
    QString lang = AppSettings::getLanguage();
    QTranslator translator;
    if (lang == LANG_EN_US) {
        translator.load(EN_TRANSLATION_PATH);
        a.installTranslator(&translator);
    } else {
        translator.load(ZH_TRANSLATION_PATH);
        a.installTranslator(&translator);
    }
 
    //初始化样式表
    QFile file(STYLE_SHEET_PATH);
    if (!file.open(QFile::ReadOnly | QFile::Text)) {
        MessageBoxHelper::showError(nullptr, QObject::tr("错误"), 
            QObject::tr("无法加载样式表文件"));
        return false; 
    }
    QString styleSheet = QString::fromUtf8(file.readAll());
    if (styleSheet.isEmpty()) {
        MessageBoxHelper::showError(nullptr, QObject::tr("错误"), 
            QObject::tr("样式表内容为空！"));
    }
    else {
        qApp->setStyleSheet(styleSheet); 
    }
    //初始化连接数据库
    bool ret = ParameterManager::instance().init(USER_SETTING_DB);
    if (!ret) {
        MessageBoxHelper::showError(nullptr, QObject::tr("提示"),
            QObject::tr("数据库连接失败"));
        return false;
    }
    UserManager::instance().initializeDefaultUsersIfNeeded(DEFAULT_USERS_JSON);

    HiPrint W;
    W.show();
    return a.exec();

    //LoginController loginDlg(&UserManager::instance());
    //if (loginDlg.exec() == QDialog::Accepted && loginDlg.isLoginSuccess()) {
    //    QString user = loginDlg.getCurrentUser();
    //    UserSession::instance().setCurrentUser(user);
    //    HiPrint w;
    //    const QRect screenGeometry = QGuiApplication::primaryScreen()->availableGeometry();
    //    w.move(screenGeometry.left() + (screenGeometry.width() - w.width()) / 2,screenGeometry.top() + (screenGeometry.height() - w.height()) / 2);
    //    w.show();
    //    return a.exec();
    //}
    //else {
    //    return false;
    //}
}
