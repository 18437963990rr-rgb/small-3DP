#include "PrintSettings.h"

PrintSettings::PrintSettings(QWidget *parent)
	: QWidget(parent)
{
	ui.setupUi(this);

	loadSettings();
	connect(ui.saveButton, &QPushButton::clicked, this, &PrintSettings::slotSaveSettings);
}

PrintSettings::~PrintSettings()
{}

void PrintSettings::loadSettings()
{
	ui.printModeComboBox->setCurrentIndex(ParameterManager::instance().getPrintMode()); // 打印模式
	ui.repeatCountSpinBox->setValue(ParameterManager::instance().getNumberOfCycles()); // 循环次数
	ui.inkingTimeSpinBox->setValue(ParameterManager::instance().getInkPressureTime()); // 压墨时间
	ui.inkPressureRecoveryTimeSpinBox->setValue(ParameterManager::instance().getNegativePressureRecoveryTime()); // 墨路负压恢复时间
	ui.intervalLayersSpinBox->setValue(ParameterManager::instance().getSpacingLayerCount()); // 间隔层数
	ui.wiperPositionSpinBox->setValue(ParameterManager::instance().getWiperPosition()); // 雨刷位置
	ui.startXSpinBox->setValue(ParameterManager::instance().getStartPointX()); // 起点X
	ui.startYSpinBox->setValue(ParameterManager::instance().getStartPointY()); // 起点Y
	ui.paperFeedLengthSpinBox->setValue(ParameterManager::instance().getPaperFeedLength()); // 走纸长度
	ui.intervalLengthSpinBox->setValue(ParameterManager::instance().getAutoSpacingLayerCount()); // 自动间隔层数
	ui.followPrintingCheckBox->setChecked(ParameterManager::instance().getFollowPrinting()); // 跟随打印
	ui.autoSkipWhiteCheckBox->setChecked(ParameterManager::instance().getAutoWhiteSkip()); // 自动跳白
	ui.featheringCheckBox->setChecked(ParameterManager::instance().getFeathering()); // 羽化
	ui.multiPassPrintingCheckBox->setChecked(ParameterManager::instance().getMultiPassPrinting()); // 多Pass打印
	ui.sandingEnableCheckBox->setChecked(ParameterManager::instance().getSandSpreadingEnable()); // 铺砂轴使能
	ui.zAxisMovementEnableCheckBox->setChecked(ParameterManager::instance().getEnableZAxisMovement()); // 使能Z轴移动

	ui.presetParametersComBox->setCurrentIndex(ParameterManager::instance().getDefaultParameters()); // 预设参数
	ui.xSpeedSpinBox->setCurrentIndex(ParameterManager::instance().getXSpeed()); // X轴速度
	ui.xResolutionSpinBox->setCurrentIndex(ParameterManager::instance().getXResolution()); // X分辨率
	ui.layerThicknessSpinBox->setCurrentIndex(ParameterManager::instance().getLayerThickness()); // 层厚
	ui.lowSandLayersSpinBox->setValue(ParameterManager::instance().getBaseSandLayers()); // 低砂层数
	ui.middleBlankLayersSpinBox->setValue(ParameterManager::instance().getIntermediateBlankLayer()); // 中间空白层
	ui.xOffsetSpinBox->setValue(ParameterManager::instance().getXOffset()); // X偏移
	ui.yOffsetSpinBox->setValue(ParameterManager::instance().getYOffset()); // Y偏移
	ui.autoSettingCheckBox->setChecked(ParameterManager::instance().getAutoSetup()); // 自动设置


}

//void PrintSettings::saveSettings()
//{
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_PRINT_MODE, ui.printModeComboBox->currentIndex());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_NUMBER_OF_CYCLES, ui.repeatCountSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_INK_PRESSURE_TIME, ui.inkingTimeSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_NEG_PRESSURE_TIME, ui.inkPressureRecoveryTimeSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_SPACING_LAYER_COUNT, ui.intervalLayersSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_WIPER_POSITION, ui.wiperPositionSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_START_POINT_X, ui.startXSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_START_POINT_Y, ui.startYSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_PAPER_FEED_LENGTH, ui.paperFeedLengthSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_AUTO_SPACING_LAYER, ui.intervalLengthSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_FOLLOW_PRINTING, ui.followPrintingCheckBox->isChecked());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_AUTO_WHITE_SKIP, ui.autoSkipWhiteCheckBox->isChecked());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_FEATHERING, ui.featheringCheckBox->isChecked());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_MULTI_PASS_PRINTING, ui.multiPassPrintingCheckBox->isChecked());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_SAND_SPREADING_ENABLE, ui.sandingEnableCheckBox->isChecked());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_ENABLE_Z_MOVEMENT, ui.zAxisMovementEnableCheckBox->isChecked());
//	
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_DEFAULT_PARAMS, ui.presetParametersComBox->currentIndex());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_X_SPEED, ui.xSpeedSpinBox->currentIndex());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_X_RESOLUTION, ui.xResolutionSpinBox->currentIndex());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_LAYER_THICKNESS, ui.layerThicknessSpinBox->currentIndex());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_BASE_SAND_LAYERS, ui.lowSandLayersSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_INTERMEDIATE_BLANK, ui.middleBlankLayersSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_X_OFFSET, ui.xOffsetSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_Y_OFFSET, ui.yOffsetSpinBox->value());
//	ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, KEY_AUTO_SETUP, ui.autoSettingCheckBox->isChecked());
//
//
//}

void PrintSettings::saveSettings()
{
	struct SettingEntry {
		const char* key;
		std::function<QVariant()> getter;
	};

	const std::vector<SettingEntry> settings = {
		{ KEY_PRINT_MODE,             [this]() { return ui.printModeComboBox->currentIndex(); } },
		{ KEY_NUMBER_OF_CYCLES,       [this]() { return ui.repeatCountSpinBox->value(); } },
		{ KEY_INK_PRESSURE_TIME,      [this]() { return ui.inkingTimeSpinBox->value(); } },
		{ KEY_NEG_PRESSURE_TIME,      [this]() { return ui.inkPressureRecoveryTimeSpinBox->value(); } },
		{ KEY_SPACING_LAYER_COUNT,    [this]() { return ui.intervalLayersSpinBox->value(); } },
		{ KEY_WIPER_POSITION,         [this]() { return ui.wiperPositionSpinBox->value(); } },
		{ KEY_START_POINT_X,          [this]() { return ui.startXSpinBox->value(); } },
		{ KEY_START_POINT_Y,          [this]() { return ui.startYSpinBox->value(); } },
		{ KEY_PAPER_FEED_LENGTH,      [this]() { return ui.paperFeedLengthSpinBox->value(); } },
		{ KEY_AUTO_SPACING_LAYER,     [this]() { return ui.intervalLengthSpinBox->value(); } },
		{ KEY_FOLLOW_PRINTING,        [this]() { return ui.followPrintingCheckBox->isChecked(); } },
		{ KEY_AUTO_WHITE_SKIP,        [this]() { return ui.autoSkipWhiteCheckBox->isChecked(); } },
		{ KEY_FEATHERING,             [this]() { return ui.featheringCheckBox->isChecked(); } },
		{ KEY_MULTI_PASS_PRINTING,    [this]() { return ui.multiPassPrintingCheckBox->isChecked(); } },
		{ KEY_SAND_SPREADING_ENABLE,  [this]() { return ui.sandingEnableCheckBox->isChecked(); } },
		{ KEY_ENABLE_Z_MOVEMENT,      [this]() { return ui.zAxisMovementEnableCheckBox->isChecked(); } },

		{ KEY_DEFAULT_PARAMS,         [this]() { return ui.presetParametersComBox->currentIndex(); } },
		{ KEY_X_SPEED,                [this]() { return ui.xSpeedSpinBox->currentIndex(); } },
		{ KEY_X_RESOLUTION,           [this]() { return ui.xResolutionSpinBox->currentIndex(); } },
		{ KEY_LAYER_THICKNESS,        [this]() { return ui.layerThicknessSpinBox->currentIndex(); } },
		{ KEY_BASE_SAND_LAYERS,       [this]() { return ui.lowSandLayersSpinBox->value(); } },
		{ KEY_INTERMEDIATE_BLANK,     [this]() { return ui.middleBlankLayersSpinBox->value(); } },
		{ KEY_X_OFFSET,               [this]() { return ui.xOffsetSpinBox->value(); } },
		{ KEY_Y_OFFSET,               [this]() { return ui.yOffsetSpinBox->value(); } },
		{ KEY_AUTO_SETUP,             [this]() { return ui.autoSettingCheckBox->isChecked(); } },
	};

	for (const auto& entry : settings) {
		const QString key = entry.key;
		QVariant newValue = entry.getter();
		QVariant oldValue = ParameterManager::instance().getValue(PRINT_PROFILES_TABLE, key);
		if (newValue != oldValue) {
			ParameterManager::instance().setValue(PRINT_PROFILES_TABLE, key, newValue);
		}
	}
}

void PrintSettings::slotSaveSettings()
{
	this->saveSettings();
}