#include "LoginController.h"
#include "UserManager.h"


LoginController::LoginController(UserManager* userManager, QWidget* parent)
    : QDialog(parent)
    , m_userManager(userManager)
{
    ui.setupUi(this);

    // 设置对象名称以便应用CSS样式
    this->setObjectName("LoginControllerClass");

    initUserList();
    setFixedSize(this->size());
    setWindowIcon(QIcon(USER_LOGIN_ICON_PATH));
    ui.ldtPassWord->setEchoMode(QLineEdit::Password);
    connect(ui.btnLogin, &QPushButton::clicked, this, &LoginController::onLoginClicked);
}


LoginController::~LoginController()
{}

void LoginController::initUserList()
{
    ui.combUserName->clear();
    ui.combUserName->addItems(m_userManager->getAllUsernames());
}

void LoginController::onLoginClicked()
{
    QString username = ui.combUserName->currentText().trimmed();
    QString password = ui.ldtPassWord->text();

    if (username.isEmpty()) {
        MessageBoxHelper::showWarning(this, tr("提示"), tr("请输入用户名！"));
        ui.combUserName->setFocus();
        return;
    }
    if (password.isEmpty()) {
        MessageBoxHelper::showWarning(this, tr("提示"), tr("请输入密码！"));
        ui.ldtPassWord->setFocus();
        return;
    }

    if (!m_userManager->validateUser(username, password)) {
        MessageBoxHelper::showWarning(this, tr("错误"), tr("用户名或密码错误！"));
        ui.ldtPassWord->clear();
        ui.ldtPassWord->setFocus();
        return;
    }

    MessageBoxHelper::showInfo(this, tr("登录成功"), QString(tr("欢迎，%1！").arg(username)));
    m_loginUser = username;
    m_loginSuccess = true;
    accept();
}