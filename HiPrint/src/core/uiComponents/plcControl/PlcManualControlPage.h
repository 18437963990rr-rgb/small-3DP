#pragma once

#include <QWidget>
#include <QPushButton>
#include <QMap>
#include <QTimer>
#include "ui_PlcManualControlPage.h"

class PlcManualControlPage : public QWidget
{
	Q_OBJECT

public:
	PlcManualControlPage(QWidget *parent = nullptr);
	~PlcManualControlPage();

private slots:
	// 统一的按钮点击处理槽函数
	void onToggleButtonClicked();
	
	// PLC通信相关槽函数
	void onPLCCommandSent(const QString& command, bool success);

private:
	Ui::PlcManualControlPageClass ui;
	
	// 按钮状态管理
	struct ButtonState {
		bool isOn;                    // 当前状态（true=开，false=关）
		QString onText;               // 开启状态文本
		QString offText;              // 关闭状态文本
		QString plcVariable;          // 对应的PLC变量名
		QPushButton* button;          // 按钮指针
	};
	
	QMap<QString, ButtonState> m_buttonStates;  // 按钮状态映射
	
	// 初始化方法
	void initializeButtons();
	void setupButtonConnections();
	void updateButtonAppearance(const QString& buttonName);
	
	// PLC通信方法
	void sendPLCCommand(const QString& plcVariable, bool value);
	
	// 防重复点击保护
	QTimer* m_clickProtectionTimer;
	QString m_lastClickedButton;
	static const int CLICK_PROTECTION_INTERVAL_MS = 1500; // 500ms防重复点击

signals:
	// PLC命令发送信号
	void plcCommandSent(const QString& plcVariable, bool value);
};

