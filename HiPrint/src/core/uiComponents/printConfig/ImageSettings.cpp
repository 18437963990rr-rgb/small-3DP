#include "ImageSettings.h"

ImageSettings::ImageSettings(QWidget *parent)
	: QWidget(parent)
{
	ui.setupUi(this);
	loadSettings();
}

ImageSettings::~ImageSettings()
{}

void ImageSettings::loadSettings()
{
	ui.startPointXLabel->setText(QString::number(ParameterManager::instance().getModelStartPointX(), 'f', 3));
	ui.startPointYLabel->setText(QString::number(ParameterManager::instance().getModelStartPointY(), 'f', 3));
	ui.modelLengthLabel->setText(QString::number(ParameterManager::instance().getModelLength(), 'f', 3));
	ui.modelWidthLabel->setText(QString::number(ParameterManager::instance().getModelWidth(), 'f', 3));
	ui.userDefinedStartPointXSpinBox->setValue(ParameterManager::instance().getCustomStartPointX());
	ui.userDefinedStartPointYSpinBox->setValue(ParameterManager::instance().getCustomStartPointY());
	ui.userDefinedXLengthSpinBox->setValue(ParameterManager::instance().getCustomLength());
	ui.userDefinedWidthSpinBox->setValue(ParameterManager::instance().getCustomWidth());
	ui.borderWidthSpinBox->setValue(ParameterManager::instance().getBlankBorder());
	ui.showPrintSizeCheckBox->setChecked(ParameterManager::instance().getDisplayPrintSize());
	ui.showThumbnailsCheckBox->setChecked(ParameterManager::instance().getDisplayThumbnails());

}

void ImageSettings::saveSettings()
{
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_MODEL_START_X, ui.startPointXLabel->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_MODEL_START_Y, ui.startPointYLabel->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_MODEL_LENGTH, ui.modelLengthLabel->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_MODEL_WIDTH, ui.modelWidthLabel->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_START_X, ui.userDefinedStartPointXSpinBox->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_START_Y, ui.userDefinedStartPointYSpinBox->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_LENGTH, ui.userDefinedXLengthSpinBox->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_WIDTH, ui.userDefinedWidthSpinBox->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_BLANK_BORDER, ui.borderWidthSpinBox->text());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_DISPLAY_PRINT_SIZE, ui.showPrintSizeCheckBox->isChecked());
	ParameterManager::instance().setValue(IMAGE_PROFILES_TABLE, KEY_DISPLAY_THUMBNAILS, ui.showThumbnailsCheckBox->isChecked());

}

