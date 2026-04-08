#ifndef PRINT_CONFIG_MANAGER_H
#define PRINT_CONFIG_MANAGER_H

#include <QWidget>
#include "ui_PrintConfigManager.h"
#include "../printConfig/FlashSpraySettings.h"
#include "../printConfig/PrintSettings.h"
#include "../printConfig/ImageSettings.h"
#include "../printConfig/NozzleSettings.h"
#include "../printConfig/FactorySettings.h"
#include "../printConfig/PrintParamSettings.h"


namespace Ui {
    class PrintConfigManager;
}

class PrintConfigManager : public QWidget
{
	Q_OBJECT

public:
	PrintConfigManager(QWidget *parent = nullptr);
	~PrintConfigManager();

private:
    FlashSpraySettings* m_flashSpraySettings;
    PrintSettings* m_printSettings;
    ImageSettings* m_imageSettings;
    NozzleSettings* m_nozzleSettings;
    FactorySettings* m_factorySettings;
    PrintParamSettings* m_printParamSettings;

private:
    void initTabs();

private:
	Ui::PrintConfigManagerClass ui;
};

#endif // !PRINT_CONFIG_MANAGER_H
