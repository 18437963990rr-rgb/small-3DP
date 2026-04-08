#include "NozzleSettings.h"

NozzleSettings::NozzleSettings(QWidget *parent)
	: QWidget(parent)
{
	ui.setupUi(this);
	loadSettings();
}

NozzleSettings::~NozzleSettings()
{}

void NozzleSettings::loadSettings()
{
	ui.startPointXPosSpinBox->setValue(ParameterManager::instance().getXPos());
	ui.startPointYPosSpinBox->setValue(ParameterManager::instance().getYPos());
	ui.printTypeComBox->setCurrentIndex(ParameterManager::instance().getWorkType());
	ui.swathsSpinBox->setValue(ParameterManager::instance().getSwathNumber());
	ui.xInkDropletSizeSpinBox->setValue(ParameterManager::instance().getXSize());
	ui.yInkDropletSizeSpinBox->setValue(ParameterManager::instance().getYSize());
	ui.pulseCountSpinBox->setValue(ParameterManager::instance().getFlashSprayCounter());
	ui.printTimesSpinBox->setValue(ParameterManager::instance().getPrintCount());
	ui.nozzle1TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle1Temperature());
	ui.nozzle2TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle2Temperature());
	ui.nozzle3TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle3Temperature());
	ui.nozzle4TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle4Temperature());
	ui.nozzle5TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle5Temperature());
	ui.nozzle6TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle6Temperature());
	ui.nozzle7TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle7Temperature());
	ui.nozzle8TemperatureSpinBox->setValue(ParameterManager::instance().getNozzle8Temperature());

	ui.effectivePixelsLineEdit->setText(QString::number(ParameterManager::instance().getEffectivePixels(), 'f', 3));
	ui.widthLineEdit->setText(QString::number(ParameterManager::instance().getNozzleWidth(), 'f', 3));
	ui.fineTuneSpinBox->setValue(ParameterManager::instance().getMicroAdjustment());
	ui.featheredOverlapSpinBox->setValue(ParameterManager::instance().getFeatheringOverlap());

	ui.nozzle1VoltageSpinBox->setValue(ParameterManager::instance().getNozzle1Voltage());
	ui.nozzle2VoltageSpinBox->setValue(ParameterManager::instance().getNozzle2Voltage());
	ui.nozzle3VoltageSpinBox->setValue(ParameterManager::instance().getNozzle3Voltage());
	ui.nozzle4VoltageSpinBox->setValue(ParameterManager::instance().getNozzle4Voltage());
	ui.nozzle5VoltageSpinBox->setValue(ParameterManager::instance().getNozzle5Voltage());
	ui.nozzle6VoltageSpinBox->setValue(ParameterManager::instance().getNozzle6Voltage());
	ui.nozzle7VoltageSpinBox->setValue(ParameterManager::instance().getNozzle7Voltage());
	ui.nozzle8VoltageSpinBox->setValue(ParameterManager::instance().getNozzle8Voltage());
	//ui.xResolutionComBox->setCurrentText(QString::number(ParameterManager::instance().getNozzleXResolution()));
	ui.xSpeedComBox->setCurrentText(QString::number(ParameterManager::instance().getNozzleXSpeed()));
	ui.reverseOffsetSpinBox->setValue(ParameterManager::instance().getReverseOffset());

	ui.nozzle1ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle1Forward());
	ui.nozzle2ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle2Forward());
	ui.nozzle3ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle3Forward());
	ui.nozzle4ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle4Forward());
	ui.nozzle5ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle5Forward());
	ui.nozzle6ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle6Forward());
	ui.nozzle7ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle7Forward());
	ui.nozzle8ForwardDirectionSpinBox->setValue(ParameterManager::instance().getNozzle8Forward());

	ui.nozzle1ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle1Reverse());
	ui.nozzle2ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle2Reverse());
	ui.nozzle3ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle3Reverse());
	ui.nozzle4ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle4Reverse());
	ui.nozzle5ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle5Reverse());
	ui.nozzle6ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle6Reverse());
	ui.nozzle7ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle7Reverse());
	ui.nozzle8ReverseDirectionSpinBox->setValue(ParameterManager::instance().getNozzle8Reverse());

	connect(ui.saveButton, &QPushButton::clicked, this, &NozzleSettings::saveSettings);

}

void NozzleSettings::saveSettings()
{
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_X_POS, ui.startPointXPosSpinBox->text());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_Y_POS, ui.startPointYPosSpinBox->text());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_WORK_TYPE, ui.printTypeComBox->currentIndex());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_SWATH_NUMBER, ui.swathsSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_X_SIZE, ui.xInkDropletSizeSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_Y_SIZE, ui.yInkDropletSizeSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_FLASH_SPRAY_COUNT, ui.pulseCountSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_PRINT_COUNT, ui.printTimesSpinBox->value());

	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_TEMP, ui.nozzle1TemperatureSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_TEMP, ui.nozzle2TemperatureSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_TEMP, ui.nozzle3TemperatureSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_TEMP, ui.nozzle4TemperatureSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_TEMP, ui.nozzle5TemperatureSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_TEMP, ui.nozzle6TemperatureSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_TEMP, ui.nozzle7TemperatureSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_TEMP, ui.nozzle8TemperatureSpinBox->value());

	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_VOLTAGE, ui.nozzle1VoltageSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_VOLTAGE, ui.nozzle2VoltageSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_VOLTAGE, ui.nozzle3VoltageSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_VOLTAGE, ui.nozzle4VoltageSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_VOLTAGE, ui.nozzle5VoltageSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_VOLTAGE, ui.nozzle6VoltageSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_VOLTAGE, ui.nozzle7VoltageSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_VOLTAGE, ui.nozzle8VoltageSpinBox->value());

	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_EFFECTIVE_PIXELS, ui.effectivePixelsLineEdit->text());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_WIDTH, ui.widthLineEdit->text());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_MICRO_ADJUSTMENT, ui.fineTuneSpinBox->text());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_FEATHERING_OVERLAP, ui.featheredOverlapSpinBox->text());

	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_FORWARD, ui.nozzle1ForwardDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_FORWARD, ui.nozzle2ForwardDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_FORWARD, ui.nozzle3ForwardDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_FORWARD, ui.nozzle4ForwardDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_FORWARD, ui.nozzle5ForwardDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_FORWARD, ui.nozzle6ForwardDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_FORWARD, ui.nozzle7ForwardDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_FORWARD, ui.nozzle8ForwardDirectionSpinBox->value());

	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_REVERSE, ui.nozzle1ReverseDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_REVERSE, ui.nozzle2ReverseDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_REVERSE, ui.nozzle3ReverseDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_REVERSE, ui.nozzle4ReverseDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_REVERSE, ui.nozzle5ReverseDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_REVERSE, ui.nozzle6ReverseDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_REVERSE, ui.nozzle7ReverseDirectionSpinBox->value());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_REVERSE, ui.nozzle8ReverseDirectionSpinBox->value());
	//ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_X_RESOLUTION, ui.xResolutionComBox->currentText());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_X_SPEED, ui.xSpeedComBox->currentText());
	ParameterManager::instance().setValue(NOZZLE_CONFIGS_TABLE, KEY_REVERSE_OFFSET, ui.reverseOffsetSpinBox->value());
}

