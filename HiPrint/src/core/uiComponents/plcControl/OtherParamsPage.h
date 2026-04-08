#pragma once

#include <QWidget>
#include "ui_OtherParamsPage.h"

class OtherParamsPage : public QWidget
{
	Q_OBJECT

public:
	OtherParamsPage(QWidget *parent = nullptr);
	~OtherParamsPage();

private:
	Ui::OtherParamsPageClass ui;
};

