#pragma once

#include <QDialog>
#include <QMessageBox>
#include "ui_LoginController.h"
#include "../../common/utils/MessageBoxHelper.h"
#include "../../common/global/ResourceManager.h"

class UserManager;


class LoginController : public QDialog
{
	Q_OBJECT

public:
	LoginController(UserManager* userManager, QWidget* parent = nullptr);
	~LoginController();

	bool isLoginSuccess() const { return m_loginSuccess; }
	QString getCurrentUser() const { return m_loginUser; } // 获取当前登录用户名

private slots:
	void onLoginClicked();


private:
	Ui::LoginControllerClass ui;
	bool m_loginSuccess = false;
	QString m_loginUser;      // 记录登录成功的用户名
	UserManager* m_userManager;

	void initUserList();

};
