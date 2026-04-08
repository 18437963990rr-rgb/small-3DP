#include "PlcManualControlPage.h"
#include <QDebug>
#include <QMessageBox>

PlcManualControlPage::PlcManualControlPage(QWidget *parent)
	: QWidget(parent)
	, m_clickProtectionTimer(new QTimer(this))
{
	ui.setupUi(this);
	
	// 初始化按钮状态和连接
	initializeButtons();
	setupButtonConnections();
	
	// 设置防重复点击定时器
	m_clickProtectionTimer->setSingleShot(true);
	m_clickProtectionTimer->setInterval(CLICK_PROTECTION_INTERVAL_MS);
}

PlcManualControlPage::~PlcManualControlPage()
{}

void PlcManualControlPage::initializeButtons()
{
	// 定义按钮配置映射
	QMap<QString, QPair<QString, QString>> buttonConfigs = {
		// 新砂罐相关按钮
		{"btnNewSandTankBlower", {"新砂罐风机启动", "新砂罐风机停止"}},
		{"btnNewSandTankDischargeValue", {"新砂罐下砂阀开", "新砂罐下砂阀关"}},
		{"btnNewSandTankIntakeValue", {"新砂罐进气阀开", "新砂罐进气阀关"}},
		{"btnNewSandTankReversePulseSolenoidValve", {"新砂罐反吹电磁阀开", "新砂罐反吹电磁阀关"}},
		{"btnSolenoidValveReset", {"新砂罐复位开", "新砂罐复位关"}},
	};
	
	// 初始化按钮状态
	for (auto it = buttonConfigs.begin(); it != buttonConfigs.end(); ++it) {
		QString buttonName = it.key();
		QPushButton* button = findChild<QPushButton*>(buttonName);
		
		if (button) {
			ButtonState state;
			state.isOn = false;  // 默认关闭状态
			state.onText = it.value().first;
			state.offText = it.value().second;
			state.plcVariable = buttonName;  // 使用按钮名作为PLC变量名
			state.button = button;
			
			m_buttonStates[buttonName] = state;
			
			// 设置初始文本和样式
			updateButtonAppearance(buttonName);
		}
	}
}

void PlcManualControlPage::setupButtonConnections()
{
	// 为所有按钮连接统一的槽函数
	for (auto it = m_buttonStates.begin(); it != m_buttonStates.end(); ++it) {
		connect(it.value().button, &QPushButton::clicked, this, &PlcManualControlPage::onToggleButtonClicked);
	}
}

void PlcManualControlPage::onToggleButtonClicked()
{
	QPushButton* senderButton = qobject_cast<QPushButton*>(sender());
	if (!senderButton) return;
	
	QString buttonName = senderButton->objectName();
	
	// 防重复点击保护
	if (m_lastClickedButton == buttonName && m_clickProtectionTimer->isActive()) {
		return;
	}
	
	m_lastClickedButton = buttonName;
	m_clickProtectionTimer->start();
	
	// 查找按钮状态
	auto it = m_buttonStates.find(buttonName);
	if (it == m_buttonStates.end()) {
		qWarning() << "未找到按钮:" << buttonName;
		return;
	}
	ButtonState& state = it.value();
	state.isOn = !state.isOn;
	updateButtonAppearance(buttonName);
	sendPLCCommand(state.plcVariable, state.isOn);
	
}

void PlcManualControlPage::updateButtonAppearance(const QString& buttonName)
{
	auto it = m_buttonStates.find(buttonName);
	if (it == m_buttonStates.end()) return;
	
	ButtonState& state = it.value();
	QPushButton* button = state.button;
	

	button->setText(state.isOn ? state.onText : state.offText);
	QString styleSheet = state.isOn ? 
		"QPushButton { background-color: #4CAF50; color: white; font-weight: bold; }" :
		"QPushButton { background-color: #f44336; color: white; font-weight: bold; }";
	button->setStyleSheet(styleSheet);
}

void PlcManualControlPage::sendPLCCommand(const QString& plcVariable, bool value)
{
	
	emit plcCommandSent(plcVariable, value);
}

void PlcManualControlPage::onPLCCommandSent(const QString& command, bool success)
{
	if (!success) {
		auto it = m_buttonStates.find(command);
		if (it != m_buttonStates.end()) {
			it.value().isOn = !it.value().isOn; 
			updateButtonAppearance(command);
			QMessageBox::warning(this, "警告", 
				QString("PLC命令发送失败: %1").arg(command));
		}
	}
}

