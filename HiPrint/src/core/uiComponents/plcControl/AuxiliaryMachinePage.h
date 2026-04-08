#pragma once

#include <QWidget>
#include "ui_AuxiliaryMachinePage.h"

class AuxiliaryMachinePage : public QWidget
{
	Q_OBJECT

public:
	AuxiliaryMachinePage(QWidget *parent = nullptr);
	~AuxiliaryMachinePage();

private:
	Ui::AuxiliaryMachinePageClass ui;
};

