#pragma once

#include <QWidget>
#include "ui_PlcParamSettingsPage.h"

class PlcParamSettingsPage : public QWidget
{
	Q_OBJECT

public:
	PlcParamSettingsPage(QWidget *parent = nullptr);
	~PlcParamSettingsPage();

private:
	Ui::PlcParamSettingsPageClass ui;
};

