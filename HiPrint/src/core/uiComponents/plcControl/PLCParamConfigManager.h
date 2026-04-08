#ifndef PLCPARAM_CONFIG_MANAGER_H
#define PLCPARAM_CONFIG_MANAGER_H

#include <QWidget>
#include "ui_PLCParamConfigManager.h"
#include "../plcControl/PlcFeatureSwitchPage.h"
#include "../plcControl/PlcParamSettingsPage.h"
#include "../plcControl/PlcManualControlPage.h"
#include "../plcControl/IoMonitorPage.h"
#include "../plcControl/AuxiliaryMachinePage.h"
#include "../plcControl/OtherParamsPage.h"
#include "../../../common/global/ResourceManager.h"


class PLCParamConfigManager : public QWidget
{
	Q_OBJECT

public:
	PLCParamConfigManager(QWidget *parent = nullptr);
	~PLCParamConfigManager();
	PlcFeatureSwitchPage* m_plcFeatureSwitchPage;
	PlcParamSettingsPage* m_plcParamSettingsPage;
	PlcManualControlPage* m_plcManualControlPage;
	IoMonitorPage* m_ioMonitorPage;
	AuxiliaryMachinePage* m_auxilaryMachinePage;
	OtherParamsPage* m_otherParamPage;

private:
	void initTabs();

private:
	Ui::PLCParamConfigManagerClass ui;
};

#endif // !PLCPARAM_CONFIG_MANAGER_H