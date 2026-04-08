#pragma once

#include <QDialog>
#include "ui_FactorySettings.h"

class FactorySettings : public QDialog
{
	Q_OBJECT

public:
	FactorySettings(QWidget *parent = nullptr);
	~FactorySettings();

private:
	Ui::FactorySettingsClass ui;
};

