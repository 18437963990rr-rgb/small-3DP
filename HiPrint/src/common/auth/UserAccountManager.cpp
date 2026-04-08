#include "UserAccountManager.h"
#include "PasswordHelper.h"
#include "../../common/db/SqlQuery.h"
#include "../../common/utils/MessageBoxHelper.h"
#include <QStandardItemModel>
#include <QHeaderView>

UserAccountManager::UserAccountManager(UserManager* userManager, const QString& currentUser, QWidget* parent)
    : QDialog(parent)
    , ui(new Ui::UserAccountManagerClass)
    , m_userManager(userManager)
    , m_currentUser(currentUser)
{
    ui->setupUi(this);

    setFixedSize(this->size());
    connect(ui->btnLogin, &QPushButton::clicked, this, &UserAccountManager::onLoginClicked);
    connect(ui->btnModifyPassWord, &QPushButton::clicked, this, &UserAccountManager::onModifyPasswordClicked);
    connect(ui->btnAddNewUser, &QPushButton::clicked, this, &UserAccountManager::onAddNewUserClicked);
    connect(ui->btnDeleteUser, &QPushButton::clicked, this, &UserAccountManager::onDeleteUserClicked);

    refreshUserTable();
    updateUserComboBox();
    ui->ldtPassWord->setEchoMode(QLineEdit::Password);
    ui->ldtNewPassWord->setEchoMode(QLineEdit::Password);
    ui->ldtAddPassWord->setEchoMode(QLineEdit::Password);

    // 显示当前用户
    ui->currentUser->setText(m_currentUser);

    // 权限控制
    updatePermissions();
}

UserAccountManager::~UserAccountManager()
{
    delete ui;
}

void UserAccountManager::updatePermissions()
{
    // 只有 admin 能新增和删除用户
    bool isAdmin = (m_currentUser == "admin");
    ui->btnAddNewUser->setEnabled(isAdmin);
    ui->btnDeleteUser->setEnabled(isAdmin);
    ui->ldtAddUserName->setEnabled(isAdmin);
    ui->ldtAddPassWord->setEnabled(isAdmin);
    ui->combRole->setEnabled(isAdmin);
}

void UserAccountManager::refreshUserTable()
{
    QStandardItemModel* model = new QStandardItemModel(this);
    model->setHorizontalHeaderLabels({ "用户名", "角色" });

    QStringList users = m_userManager->getAllUsernames();
    for (const QString& username : users) {
        QString role = userRole(username);
        QList<QStandardItem*> row;
        row << new QStandardItem(username)
            << new QStandardItem(role);
        model->appendRow(row);
    }

    ui->tableView->setModel(model);
    ui->tableView->horizontalHeader()->setSectionResizeMode(QHeaderView::Stretch);
    ui->tableView->setEditTriggers(QAbstractItemView::NoEditTriggers);
}

void UserAccountManager::updateUserComboBox()
{
    ui->combUserName->clear();
    ui->combUserName->addItems(m_userManager->getAllUsernames());
}

QString UserAccountManager::userRole(const QString& username) const
{
    if (username == "admin") return "管理员";
    if (username == "operator") return "操作员";
    return "普通用户";
}

void UserAccountManager::onLoginClicked()
{
    QString user = ui->combUserName->currentText().trimmed();
    QString pass = ui->ldtPassWord->text();

    if (user.isEmpty()) {
        MessageBoxHelper::showWarning(this, "提示", "请选择用户名！");
        ui->combUserName->setFocus();
        return;
    }
    if (pass.isEmpty()) {
        MessageBoxHelper::showWarning(this, "提示", "请输入密码！");
        ui->ldtPassWord->setFocus();
        return;
    }
    if (!m_userManager->validateUser(user, pass)) {
        MessageBoxHelper::showWarning(this, "错误", "用户名或密码错误！");
        ui->ldtPassWord->clear();
        ui->ldtPassWord->setFocus();
        return;
    }

    m_currentUser = user;
    ui->currentUser->setText(user);
    updatePermissions();
    MessageBoxHelper::showInfo(this, "登录成功", user + " 登录成功");
}

void UserAccountManager::onModifyPasswordClicked()
{
    QString user = m_currentUser; // 只能改自己
    QString newPass = ui->ldtNewPassWord->text();

    if (newPass.isEmpty()) {
        MessageBoxHelper::showWarning(this, "提示", "新密码不能为空。");
        ui->ldtNewPassWord->setFocus();
        return;
    }
    if (user == "admin" && newPass == "admin") {
        MessageBoxHelper::showWarning(this, "提示", "admin 密码不能设置为默认值。");
        return;
    }
    SqlQuery::instance().insertValue("user_credentials", user,
        PasswordHelper::encryptPassword(newPass, PasswordHelper::getOrCreateAesKey(16)));
    MessageBoxHelper::showInfo(this, "成功", "密码修改成功");
    ui->ldtNewPassWord->clear();
}

void UserAccountManager::onAddNewUserClicked()
{
    if (m_currentUser != "admin") {
        MessageBoxHelper::showWarning(this, "权限限制", "只有管理员才能新增用户！");
        return;
    }
    QString username = ui->ldtAddUserName->text().trimmed();
    QString password = ui->ldtAddPassWord->text();
    QString role = ui->combRole->currentText();

    if (username.isEmpty() || password.isEmpty()) {
        MessageBoxHelper::showWarning(this, "错误", "用户名和密码不能为空！");
        return;
    }
    if (username == "admin") {
        MessageBoxHelper::showWarning(this, "错误", "不能新增 admin 用户！");
        return;
    }
    if (m_userManager->getAllUsernames().contains(username)) {
        MessageBoxHelper::showWarning(this, "错误", "该用户名已存在！");
        return;
    }
    if (!m_userManager->registerUser(username, password)) {
        MessageBoxHelper::showWarning(this, "失败", "添加失败！");
        return;
    }

    MessageBoxHelper::showInfo(this, "成功", QString("新增用户成功，角色：%1").arg(role));
    refreshUserTable();
    updateUserComboBox();
}

void UserAccountManager::onDeleteUserClicked()
{
    if (m_currentUser != "admin") {
        MessageBoxHelper::showWarning(this, "权限限制", "只有管理员才能删除用户！");
        return;
    }
    QString username = ui->ldtAddUserName->text().trimmed();

    if (username.isEmpty()) {
        MessageBoxHelper::showWarning(this, "错误", "请输入要删除的用户名！");
        return;
    }
    if (username == "admin") {
        MessageBoxHelper::showWarning(this, "限制", "admin 用户不可删除");
        return;
    }
    if (!m_userManager->getAllUsernames().contains(username)) {
        MessageBoxHelper::showWarning(this, "错误", "该用户不存在！");
        return;
    }

    if (m_userManager->deleteUser(username)) {
        MessageBoxHelper::showInfo(this, "成功", "用户已删除");
        refreshUserTable();
        updateUserComboBox();
    }
    else {
        MessageBoxHelper::showWarning(this, "失败", "删除失败！");
    }
}
