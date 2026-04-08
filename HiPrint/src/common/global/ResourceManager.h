
#ifndef RESOURCE_MANAGER_H
#define RESOURCE_MANAGER_H

#include <QColor>
#include <QFont>


// ==================== 最大轴数量 ====================
#define MAX_MOTOR_NUM                                  9

// ==================== 打印模式 ====================
#define PRINT_MODE_CONTINUOUS           "连续模式"          // 连续模式
#define PRINT_MODE_SINGLE_FILE          "单文件模式"        // 单文件模式

// ==================== 语言环境 ====================
#define LANG_EN_US                     "en_US"              // 英语
#define LANG_ZH_CN                     "zh_CN"              // 中文

// ==================== 路径相关 ====================
#define STYLE_SHEET_PATH               "./style.qss"           // 样式表文件路径，用于加载应用程序的 QSS 样式
#define USER_SETTING_DB                "./UserSetting.db"      // 用户设置数据库路径，用于存储和读取用户配置信息
#define DEFAULT_USERS_JSON             "./default_users.json"  // 默认用户数据文件路径，用于初始化默
#define DEVICE_MAPPING_JSON_PATH       "./device_mapping.json"// 设备映射配置文件
#define PRINT_IMAGE_FILE_DIR           "/PrintImages/Images"  //  打印图片文件目录

// ==================== Meteor监控程序路径 ====================
#define METEOR_MONITOR_PROGRAM_PATH    "C:/Program Files/Meteor Inkjet/Meteor/Monitor.exe"
#define METEOR_MONITOR_WORKING_DIR     "C:/Program Files/Meteor Inkjet/Meteor"
#define METEOR_PINTENGI_CONFIG_PATH    "D:/TS_25/MeteorConfig/Config/PccE/DefaultStarfire_PccE.cfg"

// ====================翻译文件路径 ====================
#define EN_TRANSLATION_PATH    "translations/HiPrintProject_en.qm"
#define ZH_TRANSLATION_PATH    "translations/HiPrintProject_zh.qm"

// ==================== 图标资源 ====================
#define OPEN_FILE_ICON_PATH             ":/res/LoadFile.png"         // 加载文件图标
#define OPEN_FOLDER_ICON_PATH           ":/res/LoadFiles.png"        // 文件夹图标
#define BACK_ICON_PATH                  ":/res/Back.png"             // 返回图标
#define WORK_ICON_PATH                  ":/res/WorkDir.png"          // 工作目录
#define PRINT_FILE_LIST                 ":/res/FileList.png"         // 文件列表
#define LOG_RECORD                      ":/res/LogRecord.png"        // 日志记录
#define MOTION_CONTROL                  ":/res/MotionControl.png"    // 运动控制


#define METEOR_SETTING_ICON_PATH        ":/res/MeteorSetting.png"    // 打印设置图标
#define PLC_ICON_PATH                   ":/res/PLC.png"              // PLC 设置图标
#define MONITOR_ICON_PATH               ":/res/Monitor.png"          // 引擎监控图标
#define START_PRINT_ICON_PATH           ":/res/Start.png"            // 开始打印图标
#define PAUSE_PRINT_ICON_PATH           ":/res/Pause.png"            // 暂停打印图标
#define CONTINE_PRINT_ICON_PATH         ":/res/Continue.png"         // 继续打印图标
#define ABORT_PRINT_ICON_PATH           ":/res/Abort.png"            // 停止打印图标
#define NOZZLE_CONTROL_ICON_PATH        ":/res/Nozzle.png"           // 喷头控制图标
#define HEAD_POWER_ON_ICON_PATH         ":/res/HeadPowerOn.png"      // 喷头上电图标
#define HEAD_POWER_OFF_ICON_PATH        ":/res/HeadPowerOff.png"     // 喷头下电图标
#define USER_ADMIN_ICON_PATH            ":/res/UserAdmin.png"        // 用户管理图标
#define USER_LOCK_WINDOW                ":/res/Lock.png"             // 用户锁住窗口
#define USER_UNLOCK_WINDOW              ":/res/UnLock.png"           // 用户解锁窗口
#define CLOSE_ICON_PATH                 ":/res/Close.png"            // 关闭图标
#define LANGUAGE_SWITCH_EN_ICON_PATH    ":/res/EN.png"               // 英文图标
#define LAMGUAGE_SWITCH_CH_ICON_PATH    ":/res/CH.png"               // 中文图标
#define USER_LOGIN_ICON_PATH            ":/res/UserLogin.png"        // 用户登录图标
#define APPLICATION_ICON_PATH           ":/res/3DP.png"              // 应用程序图标


#define SHRINKING_DOCUMENTS_PATH        ":/res/ShrinkingDocuments.png" // 压缩文档图标
#define LOAD_FILES_PATH                 ":/res/LoadFiles.png"         // 加载文件夹图标
#define DELETE_CURRENT_PATH             ":/res/Delete.png"            // 删除当前文件图标
#define CLEAR_ALL_PATH                  ":/res/ClearFiles.png"        // 清空所有文件图标
#define HEAD_POWER_ON_ICON_PATH         ":/res/HeadPowerOn.png"       // 喷头上电图标
#define HEAD_POWER_OFF_ICON_PATH        ":/res/HeadPowerOff.png"      // 喷头关闭图标


// ==================== 字体资源 ====================
#define DEFAULT_FONT                     QFont("Microsoft YaHei", 10)    // 默认字体，Microsoft YaHei，大小 10
#define HEADING_FONT                     QFont("Microsoft YaHei", 10, QFont::Normal)  // 标题字体，Microsoft YaHei，大小 10，粗体
#define BUTTON_FONT                      QFont("Microsoft YaHei", 9)     // 按钮字体，Microsoft YaHei，大小 9
#define LABEL_FONT                       QFont("Microsoft YaHei", 10)    // 标签字体，Microsoft YaHei，大小 10
#define TEXT_EDIT_FONT                   QFont("Microsoft YaHei", 10)    // 文本编辑框字体，Microsoft YaHei，大小 10



// ==================== 喷头 ====================
#define NOZZLE1                           1                   //1#喷头
#define NOZZLE2                           2                   //2#喷头
#define NOZZLE3                           3                   //3#喷头
#define NOZZLE4                           4                   //4#喷头

// ==================== 打印状态文本 ====================
#define PRINT_STATUS_NO_PRINTING         "未打印"                     // 未打印状态
#define PRINT_STATUS_PRINTING            "打印中"                     // 打印中状态
#define PRINT_STATUS_PAUSE_PRINTING      "暂停打印"                   // 暂停打印状态
#define PRINT_STATUS_PRINTING_FINISHED   "打印完成"                  // 打印完成状态
#define PRINT_STATUS_WAIT_TO_PRINTING    "等待打印"                  // 等待打印状态


// ==================== 图像列表文本 ====================
#define SHRINKING_DOCUMENTS              "收缩文件"
#define LOAD_FILE_FOLDER                 "加载文件夹"
#define LOAD_FILES                       "加载文件"
#define DELETE_CURRENT_FILES             "删除当前文件"
#define CLEAR_CURRENT_FILE               "清空当前文件"


// ==================== 数据库表名 ====================
#define CURING_SETTING_TABLE           "curing_settings"
#define PRINT_PROFILES_TABLE           "print_profiles"
#define IMAGE_PROFILES_TABLE           "image_profiles"
#define NOZZLE_CONFIGS_TABLE           "nozzle_configs"
#define PRINT_PARAMETERS_TABLE         "print_parameters"
#define USER_CREDENTIALS_TABLE         "user_credentials"



// ==================== 喷头设置文本 ====================
#define KEY_NOZZLE1                    "nozzle1"
#define KEY_NOZZLE2                    "nozzle2"
#define KEY_NOZZLE3                    "nozzle3"
#define KEY_NOZZLE4                    "nozzle4"
#define KEY_COUNT                      "count"
#define KEY_INTERVAL_TIME              "intervalTime"

// ==================== 打印参数文本 ====================
#define KEY_PRINT_MODE             "print_mode"
#define KEY_NUMBER_OF_CYCLES       "number_of_cycles"
#define KEY_INK_PRESSURE_TIME      "ink_pressure_time"
#define KEY_NEG_PRESSURE_TIME      "negative_pressurer_recovery_time"
#define KEY_SPACING_LAYER_COUNT    "spacing_layer_count"
#define KEY_WIPER_POSITION         "wiper_position"
#define KEY_START_POINT_X          "start_point_x"
#define KEY_START_POINT_Y          "start_point_y"
#define KEY_PAPER_FEED_LENGTH      "paper feed length"
#define KEY_AUTO_SPACING_LAYER     "auto_spacing_layer_count"
#define KEY_DEFAULT_PARAMS         "default_parameters"
#define KEY_X_SPEED                "x_speed"
#define KEY_X_RESOLUTION           "x_resolution"
#define KEY_LAYER_THICKNESS        "layer_thickness"
#define KEY_BASE_SAND_LAYERS       "base_sand_layers"
#define KEY_INTERMEDIATE_BLANK     "intermediate_blank_layer"
#define KEY_X_OFFSET               "x_offset"
#define KEY_Y_OFFSET               "y_offset"
#define KEY_AUTO_SETUP             "auto_setup"
#define KEY_POSITION_SETUP         "position_setup"
#define KEY_FOLLOW_PRINTING        "follow_printing"
#define KEY_AUTO_WHITE_SKIP        "auto_white_skip"
#define KEY_FEATHERING             "feathering"
#define KEY_MULTI_PASS_PRINTING    "multi_pass_printing"
#define KEY_SAND_SPREADING_ENABLE  "sand_spreading_enable"
#define KEY_ENABLE_Z_MOVEMENT      "enable_z_axis_movement"


// ==================== 图像设置文本 ====================
#define KEY_MODEL_START_X          "start_point_x"
#define KEY_MODEL_START_Y          "start_point_y"
#define KEY_MODEL_LENGTH           "length"
#define KEY_MODEL_WIDTH            "width"
#define KEY_CUSTOM_START_X        "custom_start_point_x"
#define KEY_CUSTOM_START_Y        "custom_start_point_y"
#define KEY_CUSTOM_LENGTH         "custom_length"
#define KEY_CUSTOM_WIDTH          "custom_width"
#define KEY_BLANK_BORDER           "blank_border"
#define KEY_DISPLAY_PRINT_SIZE     "display_print_size"
#define KEY_DISPLAY_THUMBNAILS     "display_thumbnails"

// ==================== 喷头设置文本 ====================
#define KEY_NOZZLE_X_POS           "x_pos"
#define KEY_NOZZLE_Y_POS           "y_pos"
#define KEY_WORK_TYPE              "work_type"
#define KEY_SWATH_NUMBER           "swath_number"
#define KEY_X_SIZE                 "x_size"
#define KEY_Y_SIZE                 "y_size"
#define KEY_FLASH_SPRAY_COUNT      "flash_spray_count"
#define KEY_PRINT_COUNT            "print_count"
#define KEY_EFFECTIVE_PIXELS       "effective_pixels"
#define KEY_NOZZLE_WIDTH           "width"
#define KEY_MICRO_ADJUSTMENT       "micro_adjustment"
#define KEY_FEATHERING_OVERLAP     "feathering_overlap"

#define KEY_NOZZLE1_TEMP           "nozzle1_temperature"
#define KEY_NOZZLE2_TEMP           "nozzle2_temperature"
#define KEY_NOZZLE3_TEMP           "nozzle3_temperature"
#define KEY_NOZZLE4_TEMP           "nozzle4_temperature"
#define KEY_NOZZLE5_TEMP           "nozzle5_temperature"
#define KEY_NOZZLE6_TEMP           "nozzle6_temperature"
#define KEY_NOZZLE7_TEMP           "nozzle7_temperature"
#define KEY_NOZZLE8_TEMP           "nozzle8_temperature"

#define KEY_NOZZLE1_VOLTAGE        "nozzle1_voltage"
#define KEY_NOZZLE2_VOLTAGE        "nozzle2_voltage"
#define KEY_NOZZLE3_VOLTAGE        "nozzle3_voltage"
#define KEY_NOZZLE4_VOLTAGE        "nozzle4_voltage"
#define KEY_NOZZLE5_VOLTAGE        "nozzle5_voltage"
#define KEY_NOZZLE6_VOLTAGE        "nozzle6_voltage"
#define KEY_NOZZLE7_VOLTAGE        "nozzle7_voltage"
#define KEY_NOZZLE8_VOLTAGE        "nozzle8_voltage"

#define KEY_NOZZLE1_FORWARD        "nozzle1_forward"
#define KEY_NOZZLE1_REVERSE        "nozzle1_reverse"
#define KEY_NOZZLE2_FORWARD        "nozzle2_forward"
#define KEY_NOZZLE2_REVERSE        "nozzle2_reverse"
#define KEY_NOZZLE3_FORWARD        "nozzle3_forward"
#define KEY_NOZZLE3_REVERSE        "nozzle3_reverse"
#define KEY_NOZZLE4_FORWARD        "nozzle4_forward"
#define KEY_NOZZLE4_REVERSE        "nozzle4_reverse"
#define KEY_NOZZLE5_FORWARD        "nozzle5_forward"
#define KEY_NOZZLE5_REVERSE        "nozzle5_reverse"
#define KEY_NOZZLE6_FORWARD        "nozzle6_forward"
#define KEY_NOZZLE6_REVERSE        "nozzle6_reverse"
#define KEY_NOZZLE7_FORWARD        "nozzle7_forward"
#define KEY_NOZZLE7_REVERSE        "nozzle7_reverse"
#define KEY_NOZZLE8_FORWARD        "nozzle8_forward"
#define KEY_NOZZLE8_REVERSE        "nozzle8_reverse"

#define KEY_NOZZLE_X_RESOLUTION    "x_resolution"
#define KEY_NOZZLE_X_SPEED         "x_speed"
#define KEY_REVERSE_OFFSET         "reverse_offset"

// ==================== 打印参数文本 ====================
#define KEY_SAND_TYPE                      "sand_type"
#define KEY_PRINT_LAYER_THICKNESS         "layer_thickness"
#define KEY_XY_SPEED                      "xy_speed"
#define KEY_PRINT_X_RESOLUTION            "x_resolution"
#define KEY_INK_VOLUME                    "ink_volume"
#define KEY_SPREADING_FORWARD_SPEED       "sand_spreading_forward_speed"
#define KEY_SPREADING_SPEED               "sand_spreading_speed"
#define KEY_DOSING_SPEED                  "sand_spreading_dosing_speed"
#define KEY_SPREADING_END_POSITION        "sand_spreading_end_position"
#define KEY_FEEDING_MODE                  "feeding_mode"
#define KEY_NEW_SAND_CATALYST_RATIO       "new_sand_catalyst_ratio"
#define KEY_OLD_SAND_CATALYST_RATIO       "old_sand_catalyst_ratio"
#define KEY_NEW_SAND_SUCTION_TIME         "new_sand_suction_time"
#define KEY_OLD_SAND_SUCTION_TIME         "old_sand_suction_time"
#define KEY_MIXED_SAND_NEW_SUCTION_TIME   "mixed_sand_new_suction_time"
#define KEY_MIXED_SAND_OLD_SUCTION_TIME   "mixed_sand_old_suction_time"
#define KEY_SAND_SCATTERING_SPEED         "sand_scattering_speed"


// ==================== 用户身份文本 ====================
#define KEY_OPERATOR_PASSWORD             "operator"
#define KEY_ADMIN_PASSWORD                "admin"


// ==================== 打印列表标题 ====================
#define PRINT_FILE_LIST_TITLE                   "打印文件列表"



namespace StatusColor
{
    // 绿色：表示激活、正常、运行等状态
    static const QColor Active(0, 200, 0);

    // 灰色：表示未激活、待机、空闲等状态
    static const QColor Inactive(180, 180, 180);

    // 黄色：警告状态（如异常提醒等）
    static const QColor Warning(255, 200, 0);

    // 红色：错误、故障、危险等状态
    static const QColor Error(200, 0, 0);
}





#endif // RESOURCE_MANAGER_H






