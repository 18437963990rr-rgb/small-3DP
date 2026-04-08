#ifndef DEVICE_CHANNEL_DATA_H
#define DEVICE_CHANNEL_DATA_H


// ==================== 绝对位置运动参数 ====================
struct AxisAbsoluteMoveParam
{
    bool   bExecute = false;   // 是否执行
    double dAbsMovePos = 0.0;     // 目标绝对位置
    double dAbsMoveVel = 0.0;     // 运动速度
};

// ==================== 相对位置运动参数 ====================
struct AxisRelativeMoveParam
{
    bool   bExecute = false;   // 是否执行
    double dRelMovePos = 0.0;     // 相对运动距离
    double dRelMoveVel = 0.0;     // 运动速度
};

// ==================== 轴速度参数 ====================
struct AxisVelocityParam
{
    bool   bExecute = false;   // 是否执行
    double dMoveSpeed = 0.0;     // 移动速度
};

// ==================== 轴位置参数 ====================
struct AxisPositionParam
{
    bool   bExecute = false;  // 是否执行
    double dPosition = 0.0;    // 目标位置
    bool   bRelativeMode = false;  // true:相对运动，false:绝对运动
};

// ==================== 轴运动状态数据 ====================
struct AxisMotionState
{
    double fReadActPos = 0.0;   // 实际位置
    double fReadActVel = 0.0;   // 实际速度
    bool   bAxisEnable = false; // 使能状态
    bool   bAxisHome = false;   // 是否回零
    bool   bStop = false;       // 是否停止
    bool   bSoftLimitMinExceeded = false; // 超最小软限位
    bool   bSoftLimitMaxExceeded = false; // 超最大软限位
    double nAxisErrorId = 0.0;   // 错误码
};

// ==================== 工艺参数结构体 ====================
struct ProcessParameters
{
    // 打印参数
    double layerThickness = 0.3;        // 打印层厚(mm)：范围0.01-0.5
    double xySpeed = 50.0;              // XY轴移动速度(mm/s)：范围1-100
    double printXResolution = 300.0;    // X轴分辨率(dpi)：范围100-1200
    double printYResolution = 300.0;    // Y轴分辨率(dpi)：范围100-1200
    double inkVolume = 100.0;           // 墨量(ml)：范围0-1000
    
    // 铺砂参数
    double sandSpreadingSpeed = 30.0;   // 铺砂速度(mm/s)：范围1-100
    double sandSpreadingDosingSpeed = 20.0; // 铺砂定量速度(mm/s)：范围1-100
    double sandSpreadingEndPosition = 1000.0; // 铺砂结束位置(mm)：范围0-2000
    
   
};

#endif // DEVICE_CHANNEL_DATA_H




