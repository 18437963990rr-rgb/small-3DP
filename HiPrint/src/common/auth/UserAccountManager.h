#pragma once

#include <QDialog>
#include "ui_UserAccountManager.h"
#include "../auth/UserManager.h"


class UserAccountManager : public QDialog
{
    Q_OBJECT
public:
    explicit UserAccountManager(UserManager* userManager, const QString& currentUser, 
        QWidget* parent = nullptr);
    ~UserAccountManager();

private slots:
    void onLoginClicked();
    void onModifyPasswordClicked();
    void onAddNewUserClicked();
    void onDeleteUserClicked();

private:
    Ui::UserAccountManagerClass *ui;
    UserManager* m_userManager;
    QString m_currentUser;  // 当前登录用户

    void refreshUserTable();
    QString userRole(const QString& username) const;
    void updateUserComboBox();
    void updatePermissions();
};
