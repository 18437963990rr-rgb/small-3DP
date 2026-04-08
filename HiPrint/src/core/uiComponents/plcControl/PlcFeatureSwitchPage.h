#pragma once

#include <QWidget>
#include "ui_PlcFeatureSwitchPage.h"

class PlcFeatureSwitchPage : public QWidget
{
	Q_OBJECT

public:
	PlcFeatureSwitchPage(QWidget *parent = nullptr);
	~PlcFeatureSwitchPage();

private:
	Ui::PlcFeatureSwitchPageClass ui;
};

