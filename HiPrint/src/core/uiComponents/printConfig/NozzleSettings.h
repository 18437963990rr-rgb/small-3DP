#pragma once

#include <QWidget>
#include "ui_NozzleSettings.h"

#include "../../../common/global/ParameterManager.h"
#include "../../../common/global/ResourceManager.h"

class NozzleSettings : public QWidget
{
	Q_OBJECT

public:
	NozzleSettings(QWidget *parent = nullptr);
	~NozzleSettings();

	void loadSettings();
	void saveSettings();

private:
	Ui::NozzleSettingsClass ui;
};

