#pragma once

#include <QWidget>
#include "ui_MasterDeviceSettings.h"

class MasterDeviceSettings : public QWidget
{
	Q_OBJECT

public:
	MasterDeviceSettings(QWidget *parent = nullptr);
	~MasterDeviceSettings();

private:
	Ui::MasterDeviceSettingsClass ui;
};

