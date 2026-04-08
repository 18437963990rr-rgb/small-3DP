#include "FlashSpraySettings.h"

FlashSpraySettings::FlashSpraySettings(QWidget *parent)
	: QWidget(parent)
{
	ui.setupUi(this);

    QPixmap pixmap(NOZZLE_CONTROL_ICON_PATH);
    pixmap = pixmap.scaled(30, 30, Qt::KeepAspectRatio, Qt::SmoothTransformation);
    ui.nozzle1CheckBox->setIcon(QIcon(pixmap));
    ui.nozzle2CheckBox->setIcon(QIcon(pixmap));
    ui.nozzle3CheckBox->setIcon(QIcon(pixmap));
    ui.nozzle4CheckBox->setIcon(QIcon(pixmap));
    ui.nozzle1CheckBox->setIconSize(QSize(30, 30));
    ui.nozzle2CheckBox->setIconSize(QSize(30, 30));
    ui.nozzle3CheckBox->setIconSize(QSize(30, 30));
    ui.nozzle4CheckBox->setIconSize(QSize(30, 30));
 
    loadSettings();

    connect(ui.saveButton, &QPushButton::clicked, this, &FlashSpraySettings::saveSettings);

}

FlashSpraySettings::~FlashSpraySettings()
{}

void FlashSpraySettings::loadSettings()
{
    ui.nozzle1CheckBox->setChecked(ParameterManager::instance().getIsCheckNozzle1());
    ui.nozzle2CheckBox->setChecked(ParameterManager::instance().getIsCheckNozzle2());
    ui.nozzle3CheckBox->setChecked(ParameterManager::instance().getIsCheckNozzle3());
    ui.nozzle4CheckBox->setChecked(ParameterManager::instance().getIsCheckNozzle4());
    ui.sprayCountSpintBox->setValue(ParameterManager::instance().getFlashSprayCount());
    ui.interValTimeSpinBox->setValue(ParameterManager::instance().getIntervalTime());

}

void FlashSpraySettings::saveSettings()
{
   ParameterManager::instance().setValue(CURING_SETTING_TABLE, KEY_NOZZLE1, ui.nozzle1CheckBox->isChecked());
   ParameterManager::instance().setValue(CURING_SETTING_TABLE, KEY_NOZZLE2, ui.nozzle2CheckBox->isChecked());
   ParameterManager::instance().setValue(CURING_SETTING_TABLE, KEY_NOZZLE3, ui.nozzle3CheckBox->isChecked());
   ParameterManager::instance().setValue(CURING_SETTING_TABLE, KEY_NOZZLE4, ui.nozzle4CheckBox->isChecked());
   ParameterManager::instance().setValue(CURING_SETTING_TABLE, KEY_COUNT, ui.sprayCountSpintBox->value());
   ParameterManager::instance().setValue(CURING_SETTING_TABLE, KEY_INTERVAL_TIME, ui.interValTimeSpinBox->value());
}

