#pragma once

#include <QWidget>
#include "ui_PrintSettings.h"
#include "../../../common/global/ParameterManager.h"

class PrintSettings : public QWidget
{
	Q_OBJECT

public:
	PrintSettings(QWidget *parent = nullptr);
	~PrintSettings();

	void loadSettings();
	void saveSettings();

public slots:
	void slotSaveSettings();

private:
	Ui::PrintSettingsClass ui;
};

