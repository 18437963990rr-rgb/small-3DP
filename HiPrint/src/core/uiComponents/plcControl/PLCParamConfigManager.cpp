#include "PLCParamConfigManager.h"

PLCParamConfigManager::PLCParamConfigManager(QWidget *parent)
	: QWidget(parent)
{
	ui.setupUi(this);

	setWindowTitle(tr("PLC参数设置"));
	setWindowIcon(QIcon(APPLICATION_ICON_PATH));
	setWindowFlags(windowFlags() | Qt::WindowCloseButtonHint | Qt::WindowTitleHint);
	setFixedSize(QSize(850, 600));
	initTabs();

}

PLCParamConfigManager::~PLCParamConfigManager()
{
	if (m_plcFeatureSwitchPage) {
		delete m_plcFeatureSwitchPage;
		m_plcFeatureSwitchPage = nullptr;
	}
	if (m_plcParamSettingsPage) {
		delete m_plcParamSettingsPage;
		m_plcParamSettingsPage = nullptr;
	}

	if (m_plcManualControlPage) {
		delete m_plcManualControlPage;
		m_plcManualControlPage = nullptr;
	}
	if (m_auxilaryMachinePage) {
		delete m_auxilaryMachinePage;
		m_auxilaryMachinePage = nullptr;
	}
	if (m_otherParamPage) {
		delete m_otherParamPage;
		m_otherParamPage = nullptr;
	}

}

void PLCParamConfigManager::initTabs()
{
	m_plcFeatureSwitchPage = new PlcFeatureSwitchPage(this);
	m_plcParamSettingsPage = new PlcParamSettingsPage(this);
	m_plcManualControlPage = new PlcManualControlPage(this);
	m_ioMonitorPage = new IoMonitorPage(this);
	m_auxilaryMachinePage = new AuxiliaryMachinePage(this);
	m_otherParamPage = new OtherParamsPage(this);

	ui.tabWidget->addTab(m_plcFeatureSwitchPage, tr("功能开关"));
	ui.tabWidget->addTab(m_plcParamSettingsPage, tr("参数设置"));
	ui.tabWidget->addTab(m_plcManualControlPage, tr("手动控制"));
	ui.tabWidget->addTab(m_ioMonitorPage, tr("IO监控"));
	ui.tabWidget->addTab(m_auxilaryMachinePage, tr("辅机状态"));
	ui.tabWidget->addTab(m_otherParamPage, tr("其他"));

}

