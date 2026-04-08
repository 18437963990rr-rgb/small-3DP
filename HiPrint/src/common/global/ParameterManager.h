#pragma once
#include <QObject>
#include <QVariant>
#include <QMap>
#include <QHash>
#include <functional>
#include "../../common/db/SqlQuery.h"
#include "../../common/global/ResourceManager.h"


namespace ParamKey {
    /******************** 闪喷设置 ********************/
    const QString flash_spray1 = "curing_settings.nozzle1";  // 喷头1闪喷开关
    const QString flash_spray2 = "curing_settings.nozzle2";  // 喷头2闪喷开关
    const QString flash_spray3 = "curing_settings.nozzle3";  // 喷头3闪喷开关
    const QString flash_spray4 = "curing_settings.nozzle4";  // 喷头4闪喷开关
    const QString count = "curing_settings.count";           // 闪喷次数
    const QString interval_time = "curing_settings.intervalTime"; // 闪喷间隔时间(ms)

    /******************** 打印设置 ********************/
    const QString print_mode = "print_profiles.print_mode";               // 打印模式
    const QString number_of_cycles = "print_profiles.number_of_cycles";   // 打印循环次数
    const QString ink_pressure_time = "print_profiles.ink_pressure_time"; // 压墨时间(ms)
    const QString negative_pressure_recovery_time = "print_profiles.negative_pressurer_recovery_time"; // 负压恢复时间(ms)
    const QString spacing_layer_count = "print_profiles.spacing_layer_count"; // 间隔层数
    const QString wiper_position = "print_profiles.wiper_position";       // 刮墨刀位置(0-100%)
    const QString start_point_x = "print_profiles.start_point_x";         // 打印起始点X坐标(mm)
    const QString start_point_y = "print_profiles.start_point_y";         // 打印起始点Y坐标(mm)
    const QString paper_feed_length = "print_profiles.paper feed length"; // 走纸长度(mm)
    const QString auto_spacing_layer_count = "print_profiles.auto_spacing_layer_count"; // 自动间隔层数
    const QString default_parameters = "print_profiles.default_parameters"; // 使用默认参数
    const QString x_speed = "print_profiles.x_speed";                     // X轴移动速度(mm/s)
    const QString x_resolution = "print_profiles.x_resolution";           // X轴分辨率(dpi)
    const QString layer_thickness = "print_profiles.layer_thickness";     // 打印层厚(mm)
    const QString base_sand_layers = "print_profiles.base_sand_layers";   // 底砂层数
    const QString intermediate_blank_layer = "print_profiles.intermediate_blank_layer"; // 中间空白层数
    const QString x_offset = "print_profiles.x_offset";                   // X轴偏移量(mm)
    const QString y_offset = "print_profiles.y_offset";                   // Y轴偏移量(mm)
    const QString auto_setup = "print_profiles.auto_setup";               // 自动设置
    const QString position_setup = "print_profiles.position_setup";       // 位置设置模式
    const QString follow_printing = "print_profiles.follow_printing";     // 跟随打印
    const QString auto_white_skip = "print_profiles.auto_white_skip";     // 自动跳白
    const QString feathering = "print_profiles.feathering";               // 羽化效果
    const QString multi_pass_printing = "print_profiles.multi_pass_printing"; // 多遍打印
    const QString sand_spreading_enable = "print_profiles.sand_spreading_enable"; // 铺砂使能
    const QString enable_z_axis_movement = "print_profiles.enable_z_axis_movement"; // Z轴移动使能

    /******************** 图像设置 ********************/
    const QString model_start_point_x = "image_profiles.start_point_x"; // 模型起始点X坐标(mm)
    const QString model_start_point_y = "image_profiles.start_point_y"; // 模型起始点Y坐标(mm)
    const QString model_length = "image_profiles.length";           // 模型长度(mm)
    const QString model_width = "image_profiles.width";             // 模型宽度(mm)
    const QString custom_start_point_x = "image_profiles.custom_start_point_x"; // 自定义起始点X坐标(mm)
    const QString custom_start_point_y = "image_profiles.custom_start_point_y"; // 自定义起始点Y坐标(mm)
    const QString custom_length = "image_profiles.custom_length";         // 自定义长度(mm)
    const QString custom_width = "image_profiles.custom_width";           // 自定义宽度(mm)
    const QString blank_border = "image_profiles.blank_border";           // 空白边框大小(mm)
    const QString display_print_size = "image_profiles.display_print_size"; // 显示打印尺寸
    const QString display_thumbnails = "image_profiles.display_thumbnails"; // 显示缩略图

    /******************** 喷头设置 ********************/
    const QString x_pos = "nozzle_configs.x_pos";                       // 喷头X轴位置(mm)
    const QString y_pos = "nozzle_configs.y_pos";                       // 喷头Y轴位置(mm)
    const QString work_type = "nozzle_configs.work_type";               // 工作类型
    const QString swath_number = "nozzle_configs.swath_number";         // 扫描道次数
    const QString x_size = "nozzle_configs.x_size";                     // X方向尺寸(mm)
    const QString y_size = "nozzle_configs.y_size";                     // Y方向尺寸(mm)
    const QString flash_spray_count = "nozzle_configs.flash_spray_count"; // 闪喷计数
    const QString print_count = "nozzle_configs.print_count";           // 打印计数
    const QString nozzle1_temperature = "nozzle_configs.nozzle1_temperature"; // 喷头1温度(℃)
    const QString nozzle2_temperature = "nozzle_configs.nozzle2_temperature"; // 喷头2温度(℃)
    const QString nozzle3_temperature = "nozzle_configs.nozzle3_temperature"; // 喷头3温度(℃)
    const QString nozzle4_temperature = "nozzle_configs.nozzle4_temperature"; // 喷头4温度(℃)
    const QString nozzle5_temperature = "nozzle_configs.nozzle5_temperature"; // 喷头5温度(℃)
    const QString nozzle6_temperature = "nozzle_configs.nozzle6_temperature"; // 喷头6温度(℃)
    const QString nozzle7_temperature = "nozzle_configs.nozzle7_temperature"; // 喷头7温度(℃)
    const QString nozzle8_temperature = "nozzle_configs.nozzle8_temperature"; // 喷头8温度(℃)
    const QString effective_pixels = "nozzle_configs.effective_pixels"; // 有效像素数
    const QString width = "nozzle_configs.width";                       // 喷头宽度(mm)
    const QString micro_adjustment = "nozzle_configs.micro_adjustment"; // 微调值
    const QString feathering_overlap = "nozzle_configs.feathering_overlap"; // 羽化重叠量
    const QString nozzle1_voltage = "nozzle_configs.nozzle1_voltage";   // 喷头1电压(V)
    const QString nozzle2_voltage = "nozzle_configs.nozzle2_voltage";   // 喷头2电压(V)
    const QString nozzle3_voltage = "nozzle_configs.nozzle3_voltage";   // 喷头3电压(V)
    const QString nozzle4_voltage = "nozzle_configs.nozzle4_voltage";   // 喷头4电压(V)
    const QString nozzle5_voltage = "nozzle_configs.nozzle5_voltage";   // 喷头5电压(V)
    const QString nozzle6_voltage = "nozzle_configs.nozzle6_voltage";   // 喷头6电压(V)
    const QString nozzle7_voltage = "nozzle_configs.nozzle7_voltage";   // 喷头7电压(V)
    const QString nozzle8_voltage = "nozzle_configs.nozzle8_voltage";   // 喷头8电压(V)
    const QString nozzle_x_resolution = "nozzle_configs.x_resolution";  // 喷头X分辨率(dpi)
    const QString nozzle_x_speed = "nozzle_configs.x_speed";            // 喷头X速度(mm/s)
    const QString reverse_offset = "nozzle_configs.reverse_offset";     // 反向偏移量(mm)
    const QString nozzle1_forward = "nozzle_configs.nozzle1_forward";   // 喷头1正向参数
    const QString nozzle1_reverse = "nozzle_configs.nozzle1_reverse";   // 喷头1反向参数
    const QString nozzle2_forward = "nozzle_configs.nozzle2_forward";   // 喷头2正向参数
    const QString nozzle2_reverse = "nozzle_configs.nozzle2_reverse";   // 喷头2反向参数
    const QString nozzle3_forward = "nozzle_configs.nozzle3_forward";   // 喷头3正向参数
    const QString nozzle3_reverse = "nozzle_configs.nozzle3_reverse";   // 喷头3反向参数
    const QString nozzle4_forward = "nozzle_configs.nozzle4_forward";   // 喷头4正向参数
    const QString nozzle4_reverse = "nozzle_configs.nozzle4_reverse";   // 喷头4反向参数
    const QString nozzle5_forward = "nozzle_configs.nozzle5_forward";   // 喷头5正向参数
    const QString nozzle5_reverse = "nozzle_configs.nozzle5_reverse";   // 喷头5反向参数
    const QString nozzle6_forward = "nozzle_configs.nozzle6_forward";   // 喷头6正向参数
    const QString nozzle6_reverse = "nozzle_configs.nozzle6_reverse";   // 喷头6反向参数
    const QString nozzle7_forward = "nozzle_configs.nozzle7_forward";   // 喷头7正向参数
    const QString nozzle7_reverse = "nozzle_configs.nozzle7_reverse";   // 喷头7反向参数
    const QString nozzle8_forward = "nozzle_configs.nozzle8_forward";   // 喷头8正向参数
    const QString nozzle8_reverse = "nozzle_configs.nozzle8_reverse";   // 喷头8反向参数

    /******************** 打印参数设置 ********************/
    const QString sand_type = "print_parameters.sand_type";          // 砂型类型
    const QString print_layer_thickness = "print_parameters.layer_thickness"; // 打印层厚(mm)
    const QString print_xy_speed = "print_parameters.xy_speed";            // XY轴移动速度(mm/s)
    const QString print_x_resolution = "print_parameters.x_resolution";    // X轴分辨率(dpi)
    const QString ink_volume = "print_parameters.ink_volume";        // 墨量(ml)
    const QString sand_spreading_forward_speed = "print_parameters.sand_spreading_forward_speed"; // 铺砂正向速度(mm/s)
    const QString sand_spreading_speed = "print_parameters.sand_spreading_speed"; // 铺砂速度(mm/s)
    const QString sand_spreading_dosing_speed = "print_parameters.sand_spreading_dosing_speed"; // 铺砂定量速度(mm/s)
    const QString sand_spreading_end_position = "print_parameters.sand_spreading_end_position"; // 铺砂结束位置(mm)
    const QString feeding_mode = "print_parameters.feeding_mode";    // 供砂模式
    const QString new_sand_catalyst_ratio = "print_parameters.new_sand_catalyst_ratio"; // 新砂催化剂比例(%)
    const QString old_sand_catalyst_ratio = "print_parameters.old_sand_catalyst_ratio"; // 旧砂催化剂比例(%)
    const QString new_sand_suction_time = "print_parameters.new_sand_suction_time"; // 新砂抽砂时间(s)
    const QString old_sand_suction_time = "print_parameters.old_sand_suction_time"; // 旧砂抽砂时间(s)
    const QString mixed_sand_new_suction_time = "print_parameters.mixed_sand_new_suction_time"; // 混合砂新砂抽砂时间(s)
    const QString mixed_sand_old_suction_time = "print_parameters.mixed_sand_old_suction_time"; // 混合砂旧砂抽砂时间(s)
    const QString sand_scattering_speed = "print_parameters.sand_scattering_speed"; // 撒砂速度(mm/s)


}

class ParameterManager : public QObject {

    Q_OBJECT

public:
    static ParameterManager& instance();

    bool init(const QString& dbFile);
    bool load();

    bool setValue(const QString& tableName, const QString& key, const QVariant& value);
    QVariant getValue(const QString& tableName, const QString& key, const QVariant& def = QVariant());
    QMap<QString, QVariant> getTabValues(const QString& tableName);
    bool setTabValues(const QString& tableName, const QMap<QString, QVariant>& values);
  

private:

    // -------------------- 闪喷设置 --------------------
    bool m_isCheckNozzle1 = false;  // 喷头1闪喷开关：true=开启，false=关闭
    bool m_isCheckNozzle2 = false;  // 喷头2闪喷开关：true=开启，false=关闭
    bool m_isCheckNozzle3 = false;  // 喷头3闪喷开关：true=开启，false=关闭
    bool m_isCheckNozzle4 = false;  // 喷头4闪喷开关：true=开启，false=关闭
    int m_count = 200;              // 闪喷次数：范围1-1000
    int m_intervalTime = 0;         // 闪喷间隔时间(ms)：范围0-10000

     // -------------------- 打印设置 --------------------
    bool m_printMode = false;               // 打印模式：true=自动模式，false=手动模式
    int m_numberOfCycles = 0;               // 打印循环次数：范围1-100
    int m_inkPressureTime = 0;              // 压墨时间(ms)：范围0-5000
    int m_negativePressureRecoveryTime = 0; // 负压恢复时间(ms)：范围0-10000
    int m_spacingLayerCount = 0;            // 间隔层数：范围0-20
    int m_wiperPosition = 0;                // 刮墨刀位置(0-100%)：范围0-100
    int m_startPointX = 0;                  // 打印起始点X坐标(mm)：范围0-2000
    int m_startPointY = 0;                  // 打印起始点Y坐标(mm)：范围0-2000
    int m_paperFeedLength = 0;              // 走纸长度(mm)：范围0-10000
    int m_autoSpacingLayerCount = 0;        // 自动间隔层数：范围0-20
    bool m_defaultParameters = false;       // 使用默认参数：true=使用默认，false=使用自定义
    int m_xSpeed = 0;                       // X轴移动速度(mm/s)：范围1-100
    int m_xResolution = 0;                  // X轴分辨率(dpi)：范围100-1200
    float m_layerThickness = 0.0f;          // 打印层厚(mm)：范围0.01-0.5
    int m_baseSandLayers = 0;               // 底砂层数：范围0-10
    int m_intermediateBlankLayer = 0;       // 中间空白层数：范围0-10
    int m_xOffset = 0;                      // X轴偏移量(mm)：范围-50-50
    int m_yOffset = 0;                      // Y轴偏移量(mm)：范围-50-50
    bool m_autoSetup = false;               // 自动设置：true=自动，false=手动
    int m_positionSetup = 0;                // 位置设置模式：0=默认，1=自定义
    bool m_followPrinting = false;          // 跟随打印：true=开启，false=关闭
    bool m_autoWhiteSkip = false;           // 自动跳白：true=开启，false=关闭
    bool m_feathering = false;              // 羽化效果：true=开启，false=关闭
    bool m_multiPassPrinting = false;       // 多遍打印：true=开启，false=关闭
    bool m_sandSpreadingEnable = false;     // 铺砂使能：true=开启，false=关闭
    bool m_enableZAxisMovement = false;     // Z轴移动使能：true=开启，false=关闭

     // -------------------- 图像设置 --------------------
    float m_modelStartPointX = 0.0f;        // 模型起始点X坐标(mm)：范围0-2000
    float m_modelStartPointY = 0.0f;        // 模型起始点Y坐标(mm)：范围0-2000
    int m_modelLength = 0;                  // 模型长度(mm)：范围0-2000
    int m_modelWidth = 0;                   // 模型宽度(mm)：范围0-2000
    float m_customStartPointX = 0.0f;       // 自定义起始点X坐标(mm)：范围0-2000
    float m_customStartPointY = 0.0f;       // 自定义起始点Y坐标(mm)：范围0-2000
    int m_customLength = 0;                 // 自定义长度(mm)：范围0-2000
    int m_customWidth = 0;                  // 自定义宽度(mm)：范围0-2000
    float m_blankBorder = 0.0f;             // 空白边框大小(mm)：范围0-50
    bool m_displayPrintSize = false;        // 显示打印尺寸：true=显示，false=隐藏
    bool m_displayThumbnails = false;       // 显示缩略图：true=显示，false=隐藏

     // -------------------- 喷头设置 --------------------
    int m_xPos = 0;                         // 喷头X轴位置(mm)：范围0-2000
    int m_yPos = 0;                         // 喷头Y轴位置(mm)：范围0-2000
    int m_workType = 0;                     // 工作类型：0=正常模式，1=维护模式
    int m_swathNumber = 0;                  // 扫描道次数：范围1-100
    int m_xSize = 0;                        // X方向尺寸(mm)：范围0-2000
    int m_ySize = 0;                        // Y方向尺寸(mm)：范围0-2000
    int m_flashSprayCount = 0;              // 闪喷计数：范围0-10000
    int m_printCount = 0;                   // 打印计数：范围0-1000000
    float m_nozzle1Temperature = 0.0f;      // 喷头1温度(℃)：范围20-80
    float m_nozzle2Temperature = 0.0f;      // 喷头2温度(℃)：范围20-80
    float m_nozzle3Temperature = 0.0f;      // 喷头3温度(℃)：范围20-80
    float m_nozzle4Temperature = 0.0f;      // 喷头4温度(℃)：范围20-80
    float m_nozzle5Temperature = 0.0f;      // 喷头5温度(℃)：范围20-80
    float m_nozzle6Temperature = 0.0f;      // 喷头6温度(℃)：范围20-80
    float m_nozzle7Temperature = 0.0f;      // 喷头7温度(℃)：范围20-80
    float m_nozzle8Temperature = 0.0f;      // 喷头8温度(℃)：范围20-80
    int m_effectivePixels = 0;              // 有效像素数：范围0-10000
    int m_nozzleWidth = 0;                  // 喷头宽度(mm)：范围0-100
    int m_microAdjustment = 0;              // 微调值：范围-10-10
    int m_featheringOverlap = 0;            // 羽化重叠量：范围0-100
    float m_nozzle1Voltage = 0.0f;          // 喷头1电压(V)：范围0-50
    float m_nozzle2Voltage = 0.0f;          // 喷头2电压(V)：范围0-50
    float m_nozzle3Voltage = 0.0f;          // 喷头3电压(V)：范围0-50
    float m_nozzle4Voltage = 0.0f;          // 喷头4电压(V)：范围0-50
    float m_nozzle5Voltage = 0.0f;          // 喷头5电压(V)：范围0-50
    float m_nozzle6Voltage = 0.0f;          // 喷头6电压(V)：范围0-50
    float m_nozzle7Voltage = 0.0f;          // 喷头7电压(V)：范围0-50
    float m_nozzle8Voltage = 0.0f;          // 喷头8电压(V)：范围0-50
    int m_nozzleXResolution = 300;            // 喷头X分辨率(dpi)：范围100-1200
    int m_nozzleXSpeed = 0;                 // 喷头X速度(mm/s)：范围1-100
    float m_reverseOffset = 0.0f;           // 反向偏移量(mm)：范围-10-10
    float m_nozzle1Forward = 0.0f;          // 喷头1正向参数：范围0-1
    float m_nozzle1Reverse = 0.0f;          // 喷头1反向参数：范围0-1
    float m_nozzle2Forward = 0.0f;          // 喷头2正向参数：范围0-1
    float m_nozzle2Reverse = 0.0f;          // 喷头2反向参数：范围0-1
    float m_nozzle3Forward = 0.0f;          // 喷头3正向参数：范围0-1
    float m_nozzle3Reverse = 0.0f;          // 喷头3反向参数：范围0-1
    float m_nozzle4Forward = 0.0f;          // 喷头4正向参数：范围0-1
    float m_nozzle4Reverse = 0.0f;          // 喷头4反向参数：范围0-1
    float m_nozzle5Forward = 0.0f;          // 喷头5正向参数：范围0-1
    float m_nozzle5Reverse = 0.0f;          // 喷头5反向参数：范围0-1
    float m_nozzle6Forward = 0.0f;          // 喷头6正向参数：范围0-1
    float m_nozzle6Reverse = 0.0f;          // 喷头6反向参数：范围0-1
    float m_nozzle7Forward = 0.0f;          // 喷头7正向参数：范围0-1
    float m_nozzle7Reverse = 0.0f;          // 喷头7反向参数：范围0-1
    float m_nozzle8Forward = 0.0f;          // 喷头8正向参数：范围0-1
    float m_nozzle8Reverse = 0.0f;          // 喷头8反向参数：范围0-1

   // -------------------- 打印参数设置 --------------------
    int m_sandType = 0;                     // 砂型类型：0=新砂，1=旧砂，2=混合砂
    float m_printLayerThickness = 0.0f;     // 打印层厚(mm)：范围0.01-0.5
    float m_xySpeed = 0.0f;                 // XY轴移动速度(mm/s)：范围1-100
    float m_printXResolution = 0.0f;        // X轴分辨率(dpi)：范围100-1200
    float m_inkVolume = 0.0f;               // 墨量(ml)：范围0-1000
    float m_sandSpreadingForwardSpeed = 0.0f; // 铺砂正向速度(mm/s)：范围1-100
    float m_sandSpreadingSpeed = 0.0f;      // 铺砂速度(mm/s)：范围1-100
    float m_sandSpreadingDosingSpeed = 0.0f; // 铺砂定量速度(mm/s)：范围1-100
    float m_sandSpreadingEndPosition = 0.0f; // 铺砂结束位置(mm)：范围0-2000
    int m_feedingMode = 0;                  // 供砂模式：0=自动，1=手动
    float m_newSandCatalystRatio = 0.0f;    // 新砂催化剂比例(%)：范围0-100
    float m_oldSandCatalystRatio = 0.0f;    // 旧砂催化剂比例(%)：范围0-100
    float m_newSandSuctionTime = 0.0f;      // 新砂抽砂时间(s)：范围0-60
    float m_oldSandSuctionTime = 0.0f;      // 旧砂抽砂时间(s)：范围0-60
    float m_mixedSandNewSuctionTime = 0.0f; // 混合砂新砂抽砂时间(s)：范围0-60
    float m_mixedSandOldSuctionTime = 0.0f; // 混合砂旧砂抽砂时间(s)：范围0-60
    float m_sandScatteringSpeed = 0.0f;     // 撒砂速度(mm/s)：范围1-100

     // -------------------- 用户密码设置 --------------------
    QString m_operatorPassWord = "";        // 操作员密码
    QString m_adminPassWord = "";           // 管理员密码
    QString m_language = "zh_CN"; // 默认中文


public:
    // -------------------- 闪喷设置 --------------------
    bool getIsCheckNozzle1() const { return m_isCheckNozzle1; }
    bool getIsCheckNozzle2() const { return m_isCheckNozzle2; }
    bool getIsCheckNozzle3() const { return m_isCheckNozzle3; }
    bool getIsCheckNozzle4() const { return m_isCheckNozzle4; }
    int getFlashSprayCount() const { return m_count; }
    int getIntervalTime() const { return m_intervalTime; }

    // -------------------- 打印设置 --------------------
    bool getPrintMode() const { return m_printMode; }
    int getNumberOfCycles() const { return m_numberOfCycles; }
    int getInkPressureTime() const { return m_inkPressureTime; }
    int getNegativePressureRecoveryTime() const { return m_negativePressureRecoveryTime; }
    int getSpacingLayerCount() const { return m_spacingLayerCount; }
    int getWiperPosition() const { return m_wiperPosition; }
    int getStartPointX() const { return m_startPointX; }
    int getStartPointY() const { return m_startPointY; }
    int getPaperFeedLength() const { return m_paperFeedLength; }
    int getAutoSpacingLayerCount() const { return m_autoSpacingLayerCount; }
    bool getDefaultParameters() const { return m_defaultParameters; }
    int getXSpeed() const { return m_xSpeed; }
    int getXResolution() const { return m_xResolution; }
    float getLayerThickness() const { return m_layerThickness; }
    int getBaseSandLayers() const { return m_baseSandLayers; }
    int getIntermediateBlankLayer() const { return m_intermediateBlankLayer; }
    int getXOffset() const { return m_xOffset; }
    int getYOffset() const { return m_yOffset; }
    bool getAutoSetup() const { return m_autoSetup; }
    int getPositionSetup() const { return m_positionSetup; }
    bool getFollowPrinting() const { return m_followPrinting; }
    bool getAutoWhiteSkip() const { return m_autoWhiteSkip; }
    bool getFeathering() const { return m_feathering; }
    bool getMultiPassPrinting() const { return m_multiPassPrinting; }
    bool getSandSpreadingEnable() const { return m_sandSpreadingEnable; }
    bool getEnableZAxisMovement() const { return m_enableZAxisMovement; }

    // -------------------- 图像设置 --------------------
    float getModelStartPointX() const { return m_modelStartPointX; }
    float getModelStartPointY() const { return m_modelStartPointY; }
    int getModelLength() const { return m_modelLength; }
    int getModelWidth() const { return m_modelWidth; }
    float getCustomStartPointX() const { return m_customStartPointX; }
    float getCustomStartPointY() const { return m_customStartPointY; }
    int getCustomLength() const { return m_customLength; }
    int getCustomWidth() const { return m_customWidth; }
    float getBlankBorder() const { return m_blankBorder; }
    bool getDisplayPrintSize() const { return m_displayPrintSize; }
    bool getDisplayThumbnails() const { return m_displayThumbnails; }

    // -------------------- 喷头设置 --------------------
    int getXPos() const { return m_xPos; }
    int getYPos() const { return m_yPos; }
    int getWorkType() const { return m_workType; }
    int getSwathNumber() const { return m_swathNumber; }
    int getXSize() const { return m_xSize; }
    int getYSize() const { return m_ySize; }
    int getFlashSprayCounter() const { return m_flashSprayCount; }
    int getPrintCount() const { return m_printCount; }
    float getNozzle1Temperature() const { return m_nozzle1Temperature; }
    float getNozzle2Temperature() const { return m_nozzle2Temperature; }
    float getNozzle3Temperature() const { return m_nozzle3Temperature; }
    float getNozzle4Temperature() const { return m_nozzle4Temperature; }
    float getNozzle5Temperature() const { return m_nozzle5Temperature; }
    float getNozzle6Temperature() const { return m_nozzle6Temperature; }
    float getNozzle7Temperature() const { return m_nozzle7Temperature; }
    float getNozzle8Temperature() const { return m_nozzle8Temperature; }
    int getEffectivePixels() const { return m_effectivePixels; }
    int getNozzleWidth() const { return m_nozzleWidth; }
    int getMicroAdjustment() const { return m_microAdjustment; }
    int getFeatheringOverlap() const { return m_featheringOverlap; }
    float getNozzle1Voltage() const { return m_nozzle1Voltage; }
    float getNozzle2Voltage() const { return m_nozzle2Voltage; }
    float getNozzle3Voltage() const { return m_nozzle3Voltage; }
    float getNozzle4Voltage() const { return m_nozzle4Voltage; }
    float getNozzle5Voltage() const { return m_nozzle5Voltage; }
    float getNozzle6Voltage() const { return m_nozzle6Voltage; }
    float getNozzle7Voltage() const { return m_nozzle7Voltage; }
    float getNozzle8Voltage() const { return m_nozzle8Voltage; }
    int getNozzleXResolution() const { return m_nozzleXResolution; }
    int getNozzleXSpeed() const { return m_nozzleXSpeed; }
    float getReverseOffset() const { return m_reverseOffset; }
    float getNozzle1Forward() const { return m_nozzle1Forward; }
    float getNozzle1Reverse() const { return m_nozzle1Reverse; }
    float getNozzle2Forward() const { return m_nozzle2Forward; }
    float getNozzle2Reverse() const { return m_nozzle2Reverse; }
    float getNozzle3Forward() const { return m_nozzle3Forward; }
    float getNozzle3Reverse() const { return m_nozzle3Reverse; }
    float getNozzle4Forward() const { return m_nozzle4Forward; }
    float getNozzle4Reverse() const { return m_nozzle4Reverse; }
    float getNozzle5Forward() const { return m_nozzle5Forward; }
    float getNozzle5Reverse() const { return m_nozzle5Reverse; }
    float getNozzle6Forward() const { return m_nozzle6Forward; }
    float getNozzle6Reverse() const { return m_nozzle6Reverse; }
    float getNozzle7Forward() const { return m_nozzle7Forward; }
    float getNozzle7Reverse() const { return m_nozzle7Reverse; }
    float getNozzle8Forward() const { return m_nozzle8Forward; }
    float getNozzle8Reverse() const { return m_nozzle8Reverse; }

    // -------------------- 打印参数设置 --------------------
    int getSandType() const { return m_sandType; }
    float getPrintLayerThickness() const { return m_printLayerThickness; }
    float getXYSpeed() const { return m_xySpeed; }
    float getPrintXResolution() const { return m_printXResolution; }
    float getInkVolume() const { return m_inkVolume; }
    float getSandSpreadingForwardSpeed() const { return m_sandSpreadingForwardSpeed; }
    float getSandSpreadingSpeed() const { return m_sandSpreadingSpeed; }
    float getSandSpreadingDosingSpeed() const { return m_sandSpreadingDosingSpeed; }
    float getSandSpreadingEndPosition() const { return m_sandSpreadingEndPosition; }
    int getFeedingMode() const { return m_feedingMode; }
    float getNewSandCatalystRatio() const { return m_newSandCatalystRatio; }
    float getOldSandCatalystRatio() const { return m_oldSandCatalystRatio; }
    float getNewSandSuctionTime() const { return m_newSandSuctionTime; }
    float getOldSandSuctionTime() const { return m_oldSandSuctionTime; }
    float getMixedSandNewSuctionTime() const { return m_mixedSandNewSuctionTime; }
    float getMixedSandOldSuctionTime() const { return m_mixedSandOldSuctionTime; }
    float getSandScatteringSpeed() const { return m_sandScatteringSpeed; }

    // -------------------- 用户密码设置 --------------------
    QString getOperatorPassword() const { return m_operatorPassWord; }
    QString getAdminPassword() const { return m_adminPassWord; }
   

private:
    explicit ParameterManager(QObject* p = nullptr);
    Q_DISABLE_COPY(ParameterManager);

     void initSyncMap();
    bool syncMember(const QString& fullKey, const QVariant& v);

    QHash<QString, std::function<void(const QVariant&)>> m_syncMap;
};