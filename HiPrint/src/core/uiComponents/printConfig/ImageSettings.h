#pragma once

#include <QWidget>
#include "ui_ImageSettings.h"
#include "../../../common/global/ParameterManager.h"
#include "../../../common/global/ResourceManager.h"


class ImageSettings : public QWidget
{
	Q_OBJECT

public:
	ImageSettings(QWidget *parent = nullptr);
	~ImageSettings();

	void loadSettings();
	void saveSettings();

private:
	Ui::ImageSettingsClass ui;
};

