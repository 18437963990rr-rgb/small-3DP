#pragma once

#include <QWidget>
#include "ui_SlaveDeviceSettings.h"

class SlaveDeviceSettings : public QWidget
{
	Q_OBJECT

public:
	SlaveDeviceSettings(QWidget *parent = nullptr);
	~SlaveDeviceSettings();

private:
	Ui::SlaveDeviceSettingsClass ui;
};

