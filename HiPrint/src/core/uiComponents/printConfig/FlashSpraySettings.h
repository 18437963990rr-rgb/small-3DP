#pragma once

#include <QWidget>
#include "ui_FlashSpraySettings.h"
#include "../../../common/global/ParameterManager.h"
#include "../../../common/global/ResourceManager.h"


class FlashSpraySettings : public QWidget
{
	Q_OBJECT

public:
	FlashSpraySettings(QWidget *parent = nullptr);
	~FlashSpraySettings();

	void loadSettings();
	void saveSettings();

private:
	Ui::FlashSpraySettingsClass ui;
};

