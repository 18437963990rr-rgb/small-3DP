#pragma once

#include <QWidget>
#include "ui_inkPathSettings.h"

class inkPathSettings : public QWidget
{
	Q_OBJECT

public:
	inkPathSettings(QWidget *parent = nullptr);
	~inkPathSettings();

private:
	Ui::inkPathSettingsClass ui;
};

