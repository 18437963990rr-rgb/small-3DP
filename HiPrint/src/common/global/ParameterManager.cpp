#include "ParameterManager.h"
#include <QDebug>

ParameterManager& ParameterManager::instance() {
    static ParameterManager instance;
    return instance;
}

ParameterManager::ParameterManager(QObject* p) : QObject(p) {
    initSyncMap();
}

void ParameterManager::initSyncMap() {
   
    // 闪喷设置
    m_syncMap[ParamKey::flash_spray1] = [this](const QVariant& v) { m_isCheckNozzle1 = v.toBool(); };
    m_syncMap[ParamKey::flash_spray2] = [this](const QVariant& v) { m_isCheckNozzle2 = v.toBool(); };
    m_syncMap[ParamKey::flash_spray3] = [this](const QVariant& v) { m_isCheckNozzle3 = v.toBool(); };
    m_syncMap[ParamKey::flash_spray4] = [this](const QVariant& v) { m_isCheckNozzle4 = v.toBool(); };
    m_syncMap[ParamKey::count] = [this](const QVariant& v) { m_count = v.toInt(); };
    m_syncMap[ParamKey::interval_time] = [this](const QVariant& v) { m_intervalTime = v.toInt(); };

    // 打印设置
    m_syncMap[ParamKey::print_mode] = [this](const QVariant& v) { m_printMode = v.toBool(); };
    m_syncMap[ParamKey::number_of_cycles] = [this](const QVariant& v) { m_numberOfCycles = v.toInt(); };
    m_syncMap[ParamKey::ink_pressure_time] = [this](const QVariant& v) { m_inkPressureTime = v.toInt(); };
    m_syncMap[ParamKey::negative_pressure_recovery_time] = [this](const QVariant& v) { m_negativePressureRecoveryTime = v.toInt(); };
    m_syncMap[ParamKey::spacing_layer_count] = [this](const QVariant& v) { m_spacingLayerCount = v.toInt(); };
    m_syncMap[ParamKey::wiper_position] = [this](const QVariant& v) { m_wiperPosition = v.toInt(); };
    m_syncMap[ParamKey::start_point_x] = [this](const QVariant& v) { m_startPointX = v.toInt(); };
    m_syncMap[ParamKey::start_point_y] = [this](const QVariant& v) { m_startPointY = v.toInt(); };
    m_syncMap[ParamKey::paper_feed_length] = [this](const QVariant& v) { m_paperFeedLength = v.toInt(); };
    m_syncMap[ParamKey::auto_spacing_layer_count] = [this](const QVariant& v) { m_autoSpacingLayerCount = v.toInt(); };
    m_syncMap[ParamKey::default_parameters] = [this](const QVariant& v) { m_defaultParameters = v.toBool(); };
    m_syncMap[ParamKey::x_speed] = [this](const QVariant& v) { m_xSpeed = v.toInt(); };
    m_syncMap[ParamKey::x_resolution] = [this](const QVariant& v) { m_xResolution = v.toInt(); };
    m_syncMap[ParamKey::layer_thickness] = [this](const QVariant& v) { m_layerThickness = v.toFloat(); };
    m_syncMap[ParamKey::base_sand_layers] = [this](const QVariant& v) { m_baseSandLayers = v.toInt(); };
    m_syncMap[ParamKey::intermediate_blank_layer] = [this](const QVariant& v) { m_intermediateBlankLayer = v.toInt(); };
    m_syncMap[ParamKey::x_offset] = [this](const QVariant& v) { m_xOffset = v.toInt(); };
    m_syncMap[ParamKey::y_offset] = [this](const QVariant& v) { m_yOffset = v.toInt(); };
    m_syncMap[ParamKey::auto_setup] = [this](const QVariant& v) { m_autoSetup = v.toBool(); };
    m_syncMap[ParamKey::position_setup] = [this](const QVariant& v) { m_positionSetup = v.toInt(); };
    m_syncMap[ParamKey::follow_printing] = [this](const QVariant& v) { m_followPrinting = v.toBool(); };
    m_syncMap[ParamKey::auto_white_skip] = [this](const QVariant& v) { m_autoWhiteSkip = v.toBool(); };
    m_syncMap[ParamKey::feathering] = [this](const QVariant& v) { m_feathering = v.toBool(); };
    m_syncMap[ParamKey::multi_pass_printing] = [this](const QVariant& v) { m_multiPassPrinting = v.toBool(); };
    m_syncMap[ParamKey::sand_spreading_enable] = [this](const QVariant& v) { m_sandSpreadingEnable = v.toBool(); };
    m_syncMap[ParamKey::enable_z_axis_movement] = [this](const QVariant& v) { m_enableZAxisMovement = v.toBool(); };

    // 图像设置
    m_syncMap[ParamKey::model_start_point_x] = [this](const QVariant& v) { m_modelStartPointX = v.toFloat(); };
    m_syncMap[ParamKey::model_start_point_y] = [this](const QVariant& v) { m_modelStartPointY = v.toFloat(); };
    m_syncMap[ParamKey::model_length] = [this](const QVariant& v) { m_modelLength = v.toInt(); };
    m_syncMap[ParamKey::model_width] = [this](const QVariant& v) { m_modelWidth = v.toInt(); };
    m_syncMap[ParamKey::custom_start_point_x] = [this](const QVariant& v) { m_customStartPointX = v.toFloat(); };
    m_syncMap[ParamKey::custom_start_point_y] = [this](const QVariant& v) { m_customStartPointY = v.toFloat(); };
    m_syncMap[ParamKey::custom_length] = [this](const QVariant& v) { m_customLength = v.toInt(); };
    m_syncMap[ParamKey::custom_width] = [this](const QVariant& v) { m_customWidth = v.toInt(); };
    m_syncMap[ParamKey::blank_border] = [this](const QVariant& v) { m_blankBorder = v.toFloat(); };
    m_syncMap[ParamKey::display_print_size] = [this](const QVariant& v) { m_displayPrintSize = v.toBool(); };
    m_syncMap[ParamKey::display_thumbnails] = [this](const QVariant& v) { m_displayThumbnails = v.toBool(); };

    // 喷头设置
    m_syncMap[ParamKey::x_pos] = [this](const QVariant& v) { m_xPos = v.toInt(); };
    m_syncMap[ParamKey::y_pos] = [this](const QVariant& v) { m_yPos = v.toInt(); };
    m_syncMap[ParamKey::work_type] = [this](const QVariant& v) { m_workType = v.toInt(); };
    m_syncMap[ParamKey::swath_number] = [this](const QVariant& v) { m_swathNumber = v.toInt(); };
    m_syncMap[ParamKey::x_size] = [this](const QVariant& v) { m_xSize = v.toInt(); };
    m_syncMap[ParamKey::y_size] = [this](const QVariant& v) { m_ySize = v.toInt(); };
    m_syncMap[ParamKey::flash_spray_count] = [this](const QVariant& v) { m_flashSprayCount = v.toInt(); };
    m_syncMap[ParamKey::print_count] = [this](const QVariant& v) { m_printCount = v.toInt(); };
    m_syncMap[ParamKey::nozzle1_temperature] = [this](const QVariant& v) { m_nozzle1Temperature = v.toFloat(); };
    m_syncMap[ParamKey::nozzle2_temperature] = [this](const QVariant& v) { m_nozzle2Temperature = v.toFloat(); };
    m_syncMap[ParamKey::nozzle3_temperature] = [this](const QVariant& v) { m_nozzle3Temperature = v.toFloat(); };
    m_syncMap[ParamKey::nozzle4_temperature] = [this](const QVariant& v) { m_nozzle4Temperature = v.toFloat(); };
    m_syncMap[ParamKey::nozzle5_temperature] = [this](const QVariant& v) { m_nozzle5Temperature = v.toFloat(); };
    m_syncMap[ParamKey::nozzle6_temperature] = [this](const QVariant& v) { m_nozzle6Temperature = v.toFloat(); };
    m_syncMap[ParamKey::nozzle7_temperature] = [this](const QVariant& v) { m_nozzle7Temperature = v.toFloat(); };
    m_syncMap[ParamKey::nozzle8_temperature] = [this](const QVariant& v) { m_nozzle8Temperature = v.toFloat(); };
    m_syncMap[ParamKey::effective_pixels] = [this](const QVariant& v) { m_effectivePixels = v.toInt(); };
    m_syncMap[ParamKey::width] = [this](const QVariant& v) { m_nozzleWidth = v.toInt(); };
    m_syncMap[ParamKey::micro_adjustment] = [this](const QVariant& v) { m_microAdjustment = v.toInt(); };
    m_syncMap[ParamKey::feathering_overlap] = [this](const QVariant& v) { m_featheringOverlap = v.toInt(); };
    m_syncMap[ParamKey::nozzle1_voltage] = [this](const QVariant& v) { m_nozzle1Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle2_voltage] = [this](const QVariant& v) { m_nozzle2Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle3_voltage] = [this](const QVariant& v) { m_nozzle3Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle4_voltage] = [this](const QVariant& v) { m_nozzle4Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle5_voltage] = [this](const QVariant& v) { m_nozzle5Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle6_voltage] = [this](const QVariant& v) { m_nozzle6Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle7_voltage] = [this](const QVariant& v) { m_nozzle7Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle8_voltage] = [this](const QVariant& v) { m_nozzle8Voltage = v.toFloat(); };
    m_syncMap[ParamKey::nozzle_x_resolution] = [this](const QVariant& v) { m_nozzleXResolution = v.toInt(); };
    m_syncMap[ParamKey::nozzle_x_speed] = [this](const QVariant& v) { m_nozzleXSpeed = v.toInt(); };
    m_syncMap[ParamKey::reverse_offset] = [this](const QVariant& v) { m_reverseOffset = v.toFloat(); };
    m_syncMap[ParamKey::nozzle1_forward] = [this](const QVariant& v) { m_nozzle1Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle1_reverse] = [this](const QVariant& v) { m_nozzle1Reverse = v.toFloat(); };
    m_syncMap[ParamKey::nozzle2_forward] = [this](const QVariant& v) { m_nozzle2Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle2_reverse] = [this](const QVariant& v) { m_nozzle2Reverse = v.toFloat(); };
    m_syncMap[ParamKey::nozzle3_forward] = [this](const QVariant& v) { m_nozzle3Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle3_reverse] = [this](const QVariant& v) { m_nozzle3Reverse = v.toFloat(); };
    m_syncMap[ParamKey::nozzle4_forward] = [this](const QVariant& v) { m_nozzle4Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle4_reverse] = [this](const QVariant& v) { m_nozzle4Reverse = v.toFloat(); };
    m_syncMap[ParamKey::nozzle5_forward] = [this](const QVariant& v) { m_nozzle5Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle5_reverse] = [this](const QVariant& v) { m_nozzle5Reverse = v.toFloat(); };
    m_syncMap[ParamKey::nozzle6_forward] = [this](const QVariant& v) { m_nozzle6Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle6_reverse] = [this](const QVariant& v) { m_nozzle6Reverse = v.toFloat(); };
    m_syncMap[ParamKey::nozzle7_forward] = [this](const QVariant& v) { m_nozzle7Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle7_reverse] = [this](const QVariant& v) { m_nozzle7Reverse = v.toFloat(); };
    m_syncMap[ParamKey::nozzle8_forward] = [this](const QVariant& v) { m_nozzle8Forward = v.toFloat(); };
    m_syncMap[ParamKey::nozzle8_reverse] = [this](const QVariant& v) { m_nozzle8Reverse = v.toFloat(); };

    // 打印参数设置
    m_syncMap[ParamKey::sand_type] = [this](const QVariant& v) { m_sandType = v.toInt(); };
    m_syncMap[ParamKey::print_layer_thickness] = [this](const QVariant& v) { m_printLayerThickness = v.toFloat(); };
    m_syncMap[ParamKey::print_xy_speed] = [this](const QVariant& v) { m_xySpeed = v.toFloat(); };
    m_syncMap[ParamKey::print_x_resolution] = [this](const QVariant& v) { m_printXResolution = v.toFloat(); };
    m_syncMap[ParamKey::ink_volume] = [this](const QVariant& v) { m_inkVolume = v.toFloat(); };
    m_syncMap[ParamKey::sand_spreading_forward_speed] = [this](const QVariant& v) { m_sandSpreadingForwardSpeed = v.toFloat(); };
    m_syncMap[ParamKey::sand_spreading_speed] = [this](const QVariant& v) { m_sandSpreadingSpeed = v.toFloat(); };
    m_syncMap[ParamKey::sand_spreading_dosing_speed] = [this](const QVariant& v) { m_sandSpreadingDosingSpeed = v.toFloat(); };
    m_syncMap[ParamKey::sand_spreading_end_position] = [this](const QVariant& v) { m_sandSpreadingEndPosition = v.toFloat(); };
    m_syncMap[ParamKey::feeding_mode] = [this](const QVariant& v) { m_feedingMode = v.toInt(); };
    m_syncMap[ParamKey::new_sand_catalyst_ratio] = [this](const QVariant& v) { m_newSandCatalystRatio = v.toFloat(); };
    m_syncMap[ParamKey::old_sand_catalyst_ratio] = [this](const QVariant& v) { m_oldSandCatalystRatio = v.toFloat(); };
    m_syncMap[ParamKey::new_sand_suction_time] = [this](const QVariant& v) { m_newSandSuctionTime = v.toFloat(); };
    m_syncMap[ParamKey::old_sand_suction_time] = [this](const QVariant& v) { m_oldSandSuctionTime = v.toFloat(); };
    m_syncMap[ParamKey::mixed_sand_new_suction_time] = [this](const QVariant& v) { m_mixedSandNewSuctionTime = v.toFloat(); };
    m_syncMap[ParamKey::mixed_sand_old_suction_time] = [this](const QVariant& v) { m_mixedSandOldSuctionTime = v.toFloat(); };
    m_syncMap[ParamKey::sand_scattering_speed] = [this](const QVariant& v) { m_sandScatteringSpeed = v.toFloat(); };
}

bool ParameterManager::init(const QString& dbFile) {
    return SqlQuery::instance().connectDB(dbFile);
}

bool ParameterManager::load() {
    
    auto& config = SqlQuery::instance();

    // 闪喷设置
    m_isCheckNozzle1 = config.getValue(CURING_SETTING_TABLE, KEY_NOZZLE1, m_isCheckNozzle1).toBool();
    m_isCheckNozzle2 = config.getValue(CURING_SETTING_TABLE, KEY_NOZZLE2, m_isCheckNozzle2).toBool();
    m_isCheckNozzle3 = config.getValue(CURING_SETTING_TABLE, KEY_NOZZLE3, m_isCheckNozzle3).toBool();
    m_isCheckNozzle4 = config.getValue(CURING_SETTING_TABLE, KEY_NOZZLE4, m_isCheckNozzle4).toBool();
    m_count = config.getValue(CURING_SETTING_TABLE, KEY_COUNT, m_count).toInt();
    m_intervalTime = config.getValue(CURING_SETTING_TABLE, KEY_INTERVAL_TIME, m_intervalTime).toInt();

    // 打印设置
    m_printMode = config.getValue(PRINT_PROFILES_TABLE, KEY_PRINT_MODE, m_printMode).toBool();
    m_numberOfCycles = config.getValue(PRINT_PROFILES_TABLE, KEY_NUMBER_OF_CYCLES, m_numberOfCycles).toInt();
    m_inkPressureTime = config.getValue(PRINT_PROFILES_TABLE, KEY_INK_PRESSURE_TIME, m_inkPressureTime).toInt();
    m_negativePressureRecoveryTime = config.getValue(PRINT_PROFILES_TABLE, KEY_NEG_PRESSURE_TIME, m_negativePressureRecoveryTime).toInt();
    m_spacingLayerCount = config.getValue(PRINT_PROFILES_TABLE, KEY_SPACING_LAYER_COUNT, m_spacingLayerCount).toInt();
    m_wiperPosition = config.getValue(PRINT_PROFILES_TABLE, KEY_WIPER_POSITION, m_wiperPosition).toInt();
    m_startPointX = config.getValue(PRINT_PROFILES_TABLE, KEY_START_POINT_X, m_startPointX).toInt();
    m_startPointY = config.getValue(PRINT_PROFILES_TABLE, KEY_START_POINT_Y, m_startPointY).toInt();
    m_paperFeedLength = config.getValue(PRINT_PROFILES_TABLE, KEY_PAPER_FEED_LENGTH, m_paperFeedLength).toInt();
    m_autoSpacingLayerCount = config.getValue(PRINT_PROFILES_TABLE, KEY_AUTO_SPACING_LAYER, m_autoSpacingLayerCount).toInt();
    m_defaultParameters = config.getValue(PRINT_PROFILES_TABLE, KEY_DEFAULT_PARAMS, m_defaultParameters).toBool();
    m_xSpeed = config.getValue(PRINT_PROFILES_TABLE, KEY_X_SPEED, m_xSpeed).toInt();
    m_xResolution = config.getValue(PRINT_PROFILES_TABLE, KEY_X_RESOLUTION, m_xResolution).toInt();
    m_layerThickness = config.getValue(PRINT_PROFILES_TABLE, KEY_LAYER_THICKNESS, m_layerThickness).toFloat();
    m_baseSandLayers = config.getValue(PRINT_PROFILES_TABLE, KEY_BASE_SAND_LAYERS, m_baseSandLayers).toInt();
    m_intermediateBlankLayer = config.getValue(PRINT_PROFILES_TABLE, KEY_INTERMEDIATE_BLANK, m_intermediateBlankLayer).toInt();
    m_xOffset = config.getValue(PRINT_PROFILES_TABLE, KEY_X_OFFSET, m_xOffset).toInt();
    m_yOffset = config.getValue(PRINT_PROFILES_TABLE, KEY_Y_OFFSET, m_yOffset).toInt();
    m_autoSetup = config.getValue(PRINT_PROFILES_TABLE, KEY_AUTO_SETUP, m_autoSetup).toBool();
    m_positionSetup = config.getValue(PRINT_PROFILES_TABLE, KEY_POSITION_SETUP, m_positionSetup).toInt();
    m_followPrinting = config.getValue(PRINT_PROFILES_TABLE, KEY_FOLLOW_PRINTING, m_followPrinting).toBool();
    m_autoWhiteSkip = config.getValue(PRINT_PROFILES_TABLE, KEY_AUTO_WHITE_SKIP, m_autoWhiteSkip).toBool();
    m_feathering = config.getValue(PRINT_PROFILES_TABLE, KEY_FEATHERING, m_feathering).toBool();
    m_multiPassPrinting = config.getValue(PRINT_PROFILES_TABLE, KEY_MULTI_PASS_PRINTING, m_multiPassPrinting).toBool();
    m_sandSpreadingEnable = config.getValue(PRINT_PROFILES_TABLE, KEY_SAND_SPREADING_ENABLE, m_sandSpreadingEnable).toBool();
    m_enableZAxisMovement = config.getValue(PRINT_PROFILES_TABLE, KEY_ENABLE_Z_MOVEMENT, m_enableZAxisMovement).toBool();

    // 打印设置
    m_printMode = config.getValue(PRINT_PROFILES_TABLE, KEY_PRINT_MODE, m_printMode).toBool();
    m_numberOfCycles = config.getValue(PRINT_PROFILES_TABLE, KEY_NUMBER_OF_CYCLES, m_numberOfCycles).toInt();
    m_inkPressureTime = config.getValue(PRINT_PROFILES_TABLE, KEY_INK_PRESSURE_TIME, m_inkPressureTime).toInt();
    m_negativePressureRecoveryTime = config.getValue(PRINT_PROFILES_TABLE, KEY_NEG_PRESSURE_TIME, m_negativePressureRecoveryTime).toInt();
    m_spacingLayerCount = config.getValue(PRINT_PROFILES_TABLE, KEY_SPACING_LAYER_COUNT, m_spacingLayerCount).toInt();
    m_wiperPosition = config.getValue(PRINT_PROFILES_TABLE, KEY_WIPER_POSITION, m_wiperPosition).toInt();
    m_startPointX = config.getValue(PRINT_PROFILES_TABLE, KEY_START_POINT_X, m_startPointX).toInt();
    m_startPointY = config.getValue(PRINT_PROFILES_TABLE, KEY_START_POINT_Y, m_startPointY).toInt();
    m_paperFeedLength = config.getValue(PRINT_PROFILES_TABLE, KEY_PAPER_FEED_LENGTH, m_paperFeedLength).toInt();
    m_autoSpacingLayerCount = config.getValue(PRINT_PROFILES_TABLE, KEY_AUTO_SPACING_LAYER, m_autoSpacingLayerCount).toInt();
    m_defaultParameters = config.getValue(PRINT_PROFILES_TABLE, KEY_DEFAULT_PARAMS, m_defaultParameters).toBool();
    m_xSpeed = config.getValue(PRINT_PROFILES_TABLE, KEY_X_SPEED, m_xSpeed).toInt();
    m_xResolution = config.getValue(PRINT_PROFILES_TABLE, KEY_X_RESOLUTION, m_xResolution).toInt();
    m_layerThickness = config.getValue(PRINT_PROFILES_TABLE, KEY_LAYER_THICKNESS, m_layerThickness).toFloat();
    m_baseSandLayers = config.getValue(PRINT_PROFILES_TABLE, KEY_BASE_SAND_LAYERS, m_baseSandLayers).toInt();
    m_intermediateBlankLayer = config.getValue(PRINT_PROFILES_TABLE, KEY_INTERMEDIATE_BLANK, m_intermediateBlankLayer).toInt();
    m_xOffset = config.getValue(PRINT_PROFILES_TABLE, KEY_X_OFFSET, m_xOffset).toInt();
    m_yOffset = config.getValue(PRINT_PROFILES_TABLE, KEY_Y_OFFSET, m_yOffset).toInt();
    m_autoSetup = config.getValue(PRINT_PROFILES_TABLE, KEY_AUTO_SETUP, m_autoSetup).toBool();
    m_positionSetup = config.getValue(PRINT_PROFILES_TABLE, KEY_POSITION_SETUP, m_positionSetup).toInt();
    m_followPrinting = config.getValue(PRINT_PROFILES_TABLE, KEY_FOLLOW_PRINTING, m_followPrinting).toBool();
    m_autoWhiteSkip = config.getValue(PRINT_PROFILES_TABLE, KEY_AUTO_WHITE_SKIP, m_autoWhiteSkip).toBool();
    m_feathering = config.getValue(PRINT_PROFILES_TABLE, KEY_FEATHERING, m_feathering).toBool();
    m_multiPassPrinting = config.getValue(PRINT_PROFILES_TABLE, KEY_MULTI_PASS_PRINTING, m_multiPassPrinting).toBool();
    m_sandSpreadingEnable = config.getValue(PRINT_PROFILES_TABLE, KEY_SAND_SPREADING_ENABLE, m_sandSpreadingEnable).toBool();
    m_enableZAxisMovement = config.getValue(PRINT_PROFILES_TABLE, KEY_ENABLE_Z_MOVEMENT, m_enableZAxisMovement).toBool();

    // 图像设置
    m_modelStartPointX = config.getValue(IMAGE_PROFILES_TABLE, KEY_MODEL_START_X, m_modelStartPointX).toFloat();
    m_modelStartPointY = config.getValue(IMAGE_PROFILES_TABLE, KEY_MODEL_START_Y, m_modelStartPointY).toFloat();
    m_modelLength = config.getValue(IMAGE_PROFILES_TABLE, KEY_MODEL_LENGTH, m_modelLength).toInt();
    m_modelWidth = config.getValue(IMAGE_PROFILES_TABLE, KEY_MODEL_WIDTH, m_modelWidth).toInt();
    m_customStartPointX = config.getValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_START_X, m_customStartPointX).toFloat();
    m_customStartPointY = config.getValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_START_Y, m_customStartPointY).toFloat();
    m_customLength = config.getValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_LENGTH, m_customLength).toInt();
    m_customWidth = config.getValue(IMAGE_PROFILES_TABLE, KEY_CUSTOM_WIDTH, m_customWidth).toInt();
    m_blankBorder = config.getValue(IMAGE_PROFILES_TABLE, KEY_BLANK_BORDER, m_blankBorder).toFloat();
    m_displayPrintSize = config.getValue(IMAGE_PROFILES_TABLE, KEY_DISPLAY_PRINT_SIZE, m_displayPrintSize).toBool();
    m_displayThumbnails = config.getValue(IMAGE_PROFILES_TABLE, KEY_DISPLAY_THUMBNAILS, m_displayThumbnails).toBool();

    // 喷头设置
    m_xPos = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_X_POS, m_xPos).toInt();
    m_yPos = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_Y_POS, m_yPos).toInt();
    m_workType = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_WORK_TYPE, m_workType).toInt();
    m_swathNumber = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_SWATH_NUMBER, m_swathNumber).toInt();
    m_xSize = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_X_SIZE, m_xSize).toInt();
    m_ySize = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_Y_SIZE, m_ySize).toInt();
    m_flashSprayCount = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_FLASH_SPRAY_COUNT, m_flashSprayCount).toInt();
    m_printCount = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_PRINT_COUNT, m_printCount).toInt();

    m_nozzle1Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_TEMP, m_nozzle1Temperature).toFloat();
    m_nozzle2Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_TEMP, m_nozzle2Temperature).toFloat();
    m_nozzle3Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_TEMP, m_nozzle3Temperature).toFloat();
    m_nozzle4Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_TEMP, m_nozzle4Temperature).toFloat();
    m_nozzle5Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_TEMP, m_nozzle5Temperature).toFloat();
    m_nozzle6Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_TEMP, m_nozzle6Temperature).toFloat();
    m_nozzle7Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_TEMP, m_nozzle7Temperature).toFloat();
    m_nozzle8Temperature = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_TEMP, m_nozzle8Temperature).toFloat();

    m_nozzle1Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_VOLTAGE, m_nozzle1Voltage).toFloat();
    m_nozzle2Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_VOLTAGE, m_nozzle2Voltage).toFloat();
    m_nozzle3Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_VOLTAGE, m_nozzle3Voltage).toFloat();
    m_nozzle4Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_VOLTAGE, m_nozzle4Voltage).toFloat();
    m_nozzle5Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_VOLTAGE, m_nozzle5Voltage).toFloat();
    m_nozzle6Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_VOLTAGE, m_nozzle6Voltage).toFloat();
    m_nozzle7Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_VOLTAGE, m_nozzle7Voltage).toFloat();
    m_nozzle8Voltage = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_VOLTAGE, m_nozzle8Voltage).toFloat();

    m_effectivePixels = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_EFFECTIVE_PIXELS, m_effectivePixels).toInt();
    m_nozzleWidth = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_WIDTH, m_nozzleWidth).toInt();
    m_microAdjustment = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_MICRO_ADJUSTMENT, m_microAdjustment).toInt();
    m_featheringOverlap = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_FEATHERING_OVERLAP, m_featheringOverlap).toInt();

    m_nozzle1Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_FORWARD, m_nozzle1Forward).toFloat();
    m_nozzle1Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE1_REVERSE, m_nozzle1Reverse).toFloat();
    m_nozzle2Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_FORWARD, m_nozzle2Forward).toFloat();
    m_nozzle2Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE2_REVERSE, m_nozzle2Reverse).toFloat();
    m_nozzle3Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_FORWARD, m_nozzle3Forward).toFloat();
    m_nozzle3Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE3_REVERSE, m_nozzle3Reverse).toFloat();
    m_nozzle4Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_FORWARD, m_nozzle4Forward).toFloat();
    m_nozzle4Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE4_REVERSE, m_nozzle4Reverse).toFloat();
    m_nozzle5Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_FORWARD, m_nozzle5Forward).toFloat();
    m_nozzle5Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE5_REVERSE, m_nozzle5Reverse).toFloat();
    m_nozzle6Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_FORWARD, m_nozzle6Forward).toFloat();
    m_nozzle6Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE6_REVERSE, m_nozzle6Reverse).toFloat();
    m_nozzle7Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_FORWARD, m_nozzle7Forward).toFloat();
    m_nozzle7Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE7_REVERSE, m_nozzle7Reverse).toFloat();
    m_nozzle8Forward = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_FORWARD, m_nozzle8Forward).toFloat();
    m_nozzle8Reverse = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE8_REVERSE, m_nozzle8Reverse).toFloat();
    m_nozzleXResolution = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_X_RESOLUTION, m_nozzleXResolution).toInt();
    m_nozzleXSpeed = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_NOZZLE_X_SPEED, m_nozzleXSpeed).toInt();
    m_reverseOffset = config.getValue(NOZZLE_CONFIGS_TABLE, KEY_REVERSE_OFFSET, m_reverseOffset).toFloat();

    //打印参数
    m_sandType = config.getValue(PRINT_PARAMETERS_TABLE, KEY_SAND_TYPE, m_sandType).toInt();
    m_printLayerThickness = config.getValue(PRINT_PARAMETERS_TABLE, KEY_PRINT_LAYER_THICKNESS, m_printLayerThickness).toFloat();
    m_xySpeed = config.getValue(PRINT_PARAMETERS_TABLE, KEY_XY_SPEED, m_xySpeed).toFloat();
    m_printXResolution = config.getValue(PRINT_PARAMETERS_TABLE, KEY_PRINT_X_RESOLUTION, m_printXResolution).toFloat();
    m_inkVolume = config.getValue(PRINT_PARAMETERS_TABLE, KEY_INK_VOLUME, m_inkVolume).toFloat();
    m_sandSpreadingForwardSpeed = config.getValue(PRINT_PARAMETERS_TABLE, KEY_SPREADING_FORWARD_SPEED, m_sandSpreadingForwardSpeed).toFloat();
    m_sandSpreadingSpeed = config.getValue(PRINT_PARAMETERS_TABLE, KEY_SPREADING_SPEED, m_sandSpreadingSpeed).toFloat();
    m_sandSpreadingDosingSpeed = config.getValue(PRINT_PARAMETERS_TABLE, KEY_DOSING_SPEED, m_sandSpreadingDosingSpeed).toFloat();
    m_sandSpreadingEndPosition = config.getValue(PRINT_PARAMETERS_TABLE, KEY_SPREADING_END_POSITION, m_sandSpreadingEndPosition).toFloat();
    m_feedingMode = config.getValue(PRINT_PARAMETERS_TABLE, KEY_FEEDING_MODE, m_feedingMode).toInt();
    m_newSandCatalystRatio = config.getValue(PRINT_PARAMETERS_TABLE, KEY_NEW_SAND_CATALYST_RATIO, m_newSandCatalystRatio).toFloat();
    m_oldSandCatalystRatio = config.getValue(PRINT_PARAMETERS_TABLE, KEY_OLD_SAND_CATALYST_RATIO, m_oldSandCatalystRatio).toFloat();
    m_newSandSuctionTime = config.getValue(PRINT_PARAMETERS_TABLE, KEY_NEW_SAND_SUCTION_TIME, m_newSandSuctionTime).toFloat();
    m_oldSandSuctionTime = config.getValue(PRINT_PARAMETERS_TABLE, KEY_OLD_SAND_SUCTION_TIME, m_oldSandSuctionTime).toFloat();
    m_mixedSandNewSuctionTime = config.getValue(PRINT_PARAMETERS_TABLE, KEY_MIXED_SAND_NEW_SUCTION_TIME, m_mixedSandNewSuctionTime).toFloat();
    m_mixedSandOldSuctionTime = config.getValue(PRINT_PARAMETERS_TABLE, KEY_MIXED_SAND_OLD_SUCTION_TIME, m_mixedSandOldSuctionTime).toFloat();
    m_sandScatteringSpeed = config.getValue(PRINT_PARAMETERS_TABLE, KEY_SAND_SCATTERING_SPEED, m_sandScatteringSpeed).toFloat();

    // 密码管理
    m_operatorPassWord = config.getValue(USER_CREDENTIALS_TABLE, KEY_OPERATOR_PASSWORD, m_operatorPassWord).toString();
    m_adminPassWord = config.getValue(USER_CREDENTIALS_TABLE, KEY_ADMIN_PASSWORD, m_adminPassWord).toString();

    return true;

}

bool ParameterManager::setValue(const QString& table, const QString& key, const QVariant& value) {
    const QString full = table + '.' + key;
    if (!syncMember(full, value)){
        return false;
    }
    return SqlQuery::instance().updateValue(table, key, value);
}

QVariant ParameterManager::getValue(const QString& table, const QString& key, const QVariant& def) {
    const QString full = table + '.' + key;
    QVariant val = SqlQuery::instance().getValue(table, key, def);
    syncMember(full, val);
    return val;
}

QMap<QString, QVariant> ParameterManager::getTabValues(const QString& table) {
    QMap<QString, QVariant> map = SqlQuery::instance().getTabValues(table);
    for (auto it = map.begin(); it != map.end(); ++it)
        syncMember(table + '.' + it.key(), it.value());
    return map;
}

bool ParameterManager::setTabValues(const QString& table, const QMap<QString, QVariant>& values) {
    if (values.isEmpty()) return true;
    auto& db = SqlQuery::instance();
    if (!db.beginTransaction()) return false;

    bool ok = true;
    for (auto it = values.cbegin(); it != values.cend(); ++it) {
        if (!syncMember(table + '.' + it.key(), it.value()) ||
            !db.updateValue(table, it.key(), it.value())) {
            ok = false; break;
        }
    }
    return ok ? db.commitTransaction() : (db.rollbackTransaction(), false);
}

bool ParameterManager::syncMember(const QString& full, const QVariant& v) {
    auto it = m_syncMap.find(full);
    if (it != m_syncMap.end()) {
        (*it)(v);
        return true;
    }
    return false;
}
