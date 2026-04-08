#pragma once

#include <QWidget>
#include "ui_IoMonitorPage.h"

class IoMonitorPage : public QWidget
{
	Q_OBJECT

public:
	IoMonitorPage(QWidget *parent = nullptr);
	~IoMonitorPage();

private:
	Ui::IoMonitorPageClass ui;
};

