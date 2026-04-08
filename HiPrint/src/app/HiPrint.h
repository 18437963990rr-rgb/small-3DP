#ifndef HIPRINT_H
#define HIPRINT_H

// ========== Qt 基础依赖 ==========
#include <QtWidgets/QMainWindow>
#include "ui_HiPrint.h"
#include <QGraphicsView>
#include <QGraphicsPixmapItem>
#include <QFileDialog>
#include <QMessageBox>
#include <QTreeWidgetItem>
#include <QMetaObject>
#include <QTimer>
#include <QtConcurrent/QtConcurrent>
#include <QStatusBar>
#include <QTextEdit>
#include <QPushButton>
#include <QProcess>
#include <QCloseEvent>
#include <QApplication>
#include <QTranslator>
#include <QSettings>
#include <QDockWidget>
#include <QScrollBar>


// ========== C++标准库 & 系统 ==========
#include <thread>
#include <vector>
#include <future>
#include <stdio.h>
#include <Windows.h>

// ========== 业务&三方依赖 ==========
#include "./3rdparty/meteor/Meteor.h"
#include "./3rdparty/meteor/PrinterInterface.h"
#include "./3rdparty/meteor/typedef.h"
#include "./3rdparty/tiff/include/tiffio.h"
#include "../core/printEngine/MeteorErrorMap.h"
#include "../core/modelView/ImageDelegate.h"
#include "../core/modelView/ImageTreeModel.h"
#include "../core/modelView/TreeItem.h"
#include "../core/plc/DeviceChannelData.h"
#include "../core/plc/BeckhoffPlc.h"
#include "../core/plc/DeviceMappingConfig.h"
#include "../core/plc/BeckhoffPlcManager.h"
#include "../core/plc/AdsErrorMapper.h"
#include "../common/logging/LogManager.h"
#include "../core/imageProcessing/ImageProcessor.h"
#include "../core/printEngine/MeteorApi.h"
#include "../common/utils/FileOp.h"
#include "../core/printEngine/PrintJobManagerThread.h"
#include "../core/printEngine/PrintCommandManagerThread.h"
#include "../core/printEngine/PrintStatusManagerThread.h"
#include "../common/config/PrintJobTypeDef.h"
#include "../common/global/ParameterManager.h"
#include "../common/auth/UserAccountManager.h"
#include "../common/auth/UserSession.h"
#include "../common/utils/StatusIndicatorLabel.h"
#include "../common/utils/MessageBoxHelper.h"
#include "../core/uiComponents/toolBars/MainToolBar.h"
#include "../core/uiComponents/menu/MainMenuBar.h"
#include "../core/uiComponents/status/MainStatusBar.h"
#include "../core/uiComponents/printConfig/PrintJobListPanel.h"
#include "../core/uiComponents/printConfig/PrintConfigManager.h"
#include "../core/printEngine/WorkerThreadManager.h"
#include "../common/utils/AppSettings.h"
#include "../core/uiComponents/printConfig/PrintConfigDialog.h"
#include "../core/uiComponents/plcControl/PLCParamConfigManager.h"

// ========== 编码兼容性 ==========
#if defined(_MSC_VER) && (_MSC_VER >= 1600)
# pragma execution_character_set("utf-8")
#endif



// ========================== HiPrint 主窗类 ==========================
class HiPrint : public QMainWindow
{
    Q_OBJECT

public:
    HiPrint(QWidget* parent = nullptr);
    ~HiPrint();

    // ========================== 核心初始化相关 ==========================
public:
    void initToolbar();        // 初始化工具栏
    void initMenuBar();        // 初始化菜单栏
    void initTreeView();       // 初始化树形视图
    void initStatusBar();      // 初始化状态栏
    void initializeMeteorPrinterDriver();  // 初始化Meteor打印引擎驱动
    bool initializeBeckhoffPLCConnection();    // 初始化Beckhoff PLC连接，返回连接状态
    void initMembers();        // 初始化成员变量
    void loadGlobalConfig();   // 加载全局配置
    void initLogger();         // 初始化系统日志和诊断
    void initSystem();         // 系统本体初始化
    void initializeMeteorEngine();         // 初始化Meteor打印引擎
    void initializePLCCommunicationModule();    // 初始化PLC通信模块
    void initImageModule();    // 图像模块初始化
    void setupConnections();   // 信号槽连接
    void initUIState();        // 初始化UI状态
    void systemSelfCheck();    // 启动自检与资源检测
    void showStartupScreen();  // 显示欢迎界面或主窗口
    void initPrintJobDock();
    void autoLoadLastImageDirectory();


  // ========================== 菜单栏/状态栏/工具栏 ==========================
    MainToolBar* m_toolBar;
    MainMenuBar* m_menuBar;
    MainStatusBar* m_statusBar;
    QDockWidget* m_printJobDock = nullptr;
    PrintJobListPanel* m_printJobPanel = nullptr;
    QTabWidget* m_tabWidget = nullptr; // 保存TabWidget引用
    
public:
    void registerMetaTypes();
    void initPrintJobPanelSignals();
    void configurePrinterParameters();

// ========================== PLC/运动相关 ==========================
private:
    QVector<QString>           m_enableAxis;
    QVector<QString>           m_axisNameList;
    QVector<QString>           m_relMove;
    QVector<QString>           m_absMove;
    QVector<QString>           m_jogForWard;
    QVector<QString>           m_jogForReverse;
    QVector<QString>           m_home;
    QVector<QString>           m_stop;
    QVector<QPushButton*>      m_btnMove;
    QVector<QPushButton*>      m_btnJogForWard;
    QVector<QPushButton*>      m_btnJogReverse;
    QVector<QPushButton*>      m_btnHome;
    QVector<QPushButton*>      m_btnStop;
    QVector<AxisRelativeMoveParam> m_relMoveParam;
    QVector<AxisAbsoluteMoveParam> m_absMoveParam;
    QVector<bool>              m_isCheck;
    ImageTreeModel* m_treeModel;
    ImageDelegate* m_imageDelegate;
    AxisMotionState         m_axisMoveStatusData[MAX_MOTOR_NUM];
    BeckhoffPlcManager* m_beckhoffPlcManager;

    // ======= PLC UI及操作初始化（公共接口） =======
public:
    void initializePLCUserInterface();
    void setupPlcAxisMappings();
    void setupAxisEnableActions();
    void bindAxisMoveControls();
    void connectJogControls();
    void setupAxisHomeLogic();
    void setupAxisStopHandlers();
    void setupQuickPositionActions();
    
    // ========================== X/Y快速位置操作相关 ==========================
private slots:
    void onMotionOperationClicked();//X&Y运动操作
    void onSpreadingModeClicked();//铺砂模式选择

private:

    PLCParamConfigManager* m_plcParamConfigManager;
    // X/Y运动操作映射
    QMap<int, QString> m_xyMotionOperationMap;
    QMap<QString, QString> m_plcVariableMap;
    QMap<int, QString> m_spreadingOperationMap;
    QMap<QString, QString> m_spreadingPlcVariableMap;

    
    // 防重复点击保护
    bool m_isMotionOperationInProgress;
    qint64 m_lastMotionOperationTime;
    static const int MOTION_OPERATION_MIN_INTERVAL_MS = 300; // 最小操作间隔（毫秒）
    
    // PLC通信心跳检测相关
    bool m_isPLCConnected;
    int m_plcConnectionFailures;
    int m_plcConnectionSuccesses;
    
    // PLC连接状态变化日志管理
    bool m_plcConnectionStatusReported;
    
    // 执行X/Y运动操作
    void executeXYMotionOperation(const QString& operationType);
    //执行铺砂模式动作
    void executeSpreadingModeOperation(const QString& operationType);
    bool sendMotionCommandToPLC(const QString& plcVariable, bool value = true);

    // ========================== 打印/图像/业务相关 ==========================
private:
    MeteorApi* m_meteorApi;
    WorkerThreadManager* m_workerThreadManager = nullptr;
    PrintConfigManager* m_printSetting;

    bool                       m_isPrinterConnected = false;
    bool                       m_isPrintHeadPowered = false;
    bool                       m_isPCCIdle = false;
    bool                       m_isHeadRunning = false;
    bool                       m_isPrintHeadBusy = false;
    QString                    m_printImageDirectory = QString();
    QGraphicsScene* m_scene;
    
    // 图片缩放和平移相关
    QGraphicsPixmapItem* m_currentPixmapItem = nullptr;
    QGraphicsRectItem* m_imageBorderItem = nullptr;
    double m_zoomFactor = 1.0;
    bool m_isPanning = false;
    QPoint m_lastPanPoint;
    QString m_currentShowImageName;

    QSize m_fixedNormalSize = QSize(1512, 937);
    bool m_ignoreResize = false; 
    int m_yTop = 0;
    int m_trueBpp = 1;
    int m_plane = 1;
    int m_xLeft = 700;
    int m_printHeadNum = 2;
    int m_maxWaitTimeMs = 5 * 60 * 1000;

    // 打印进度信息相关
    QDateTime m_printStartTime;           // 打印开始时间
    int m_totalPrintFiles = 0;            // 总文件数
    int m_printedFiles = 0;               // 已打印文件数
    QString m_currentPrintParams;         // 当前打印参数
    bool m_isPrintJobCompleted;           // 打印任务是否已完成的标志

    
    // ======= 打印/图像相关接口 =======
public:
    void initializePrintWorkerThreads();
    void showPrintImage(const QString& imagePath, const PrintImageParam& printImageParam);
    PrintJobParam createPrintJobParam() const;
    bool checkPrinterConnected();
    bool checkScanMode();
    bool checkHeadStatus(const TAppStatus* appStatus);
    bool checkAxisZeroSignals(); 
    bool startPrintEnginer();
    
    // PLC通信状态检查
    bool isPLCCommunicationAvailable() const;
    
    void updatePrintProgress(int printedFiles, int totalFiles,QString fileName, PrintImageParam& printImageParam);
    
    // PLC通信心跳检测相关
    void onPLCConnectionStatusChanged(bool connected);
    bool isPLCConnected() const;
    void resetProgressInfo();
    QString formatDateTime(const QDateTime& dateTime);
    void showPrintCompletionMessage();  // 显示打印完成提示信息

    // ========================== 设备/安全检测辅助 ==========================
private:
    bool checkHeadPowerPreconditions();

    // ========================== PLC通信初始化辅助方法 ==========================
    void createAndConnectPLCManager();
    bool initializePLCCommunication();
    void completePLCInitialization();

    // ========================== 事件重载 ==========================
protected:
    void closeEvent(QCloseEvent* event)override;
    void changeEvent(QEvent* event) override;
    void resizeEvent(QResizeEvent* event) override;
    void mousePressEvent(QMouseEvent* event) override;
    void mouseMoveEvent(QMouseEvent* event) override;
    bool event(QEvent* event) override;
    
    // 图片缩放和平移相关方法
    void setupGraphicsView();
    void addImageBorder();

    void executeContinuousPrint(const QStringList& fileList);
    void executeSingleFilePrint(const QStringList& fileList,const QString& fileName, int repeatCount);

    void zoomIn();
    void zoomOut();
    void resetZoom();
    
    // 更新图片信息显示
    void updateImageInfoDisplay(const QString& imagePath, const PrintImageParam& printImageParam);
    
    // 发送工艺参数到PLC
    void sendProcessParametersToPLC();
    
    // 更新暂停按钮状态（图标和文本动态切换）
    void updatePauseButtonState();

    // ===================== 信号区 =====================
signals:
    // PLC通信状态变化信号
    void plcConnectionStatusChanged(bool connected);

    // ===================== 槽函数区（按功能分组） =====================
public slots:

    // ---- 菜单栏 ----
    void slotOpenPrintSettingDialog();
    void slotOpenPLCSettingDialog();
    void slotOpenMeteorMonitor();
    void slotShowAbout();

    // ---- 打印控制 ----
    void slotStartPrint();          // 启动打印
    void slotPausePrint();          // 暂停打印
    void slotStopPrint();           // 停止打印

    // ---- 运动与位置 ----
    void slotMoveToHome();          // 回原点
    void slotMoveToStandby();       // 回待机位
    void slotPrintStartPosition();  // 打印开始位置
    void slotPrintEndPosition();    // 打印结束位置
    void slotCleanPosition();       // 清洗位置
    void slotFlashSprayPosition();  // 闪喷位置
    void slotHydrationPosition();   // 保湿位置

    // ---- 文件与图片 ----
    void slotOpenFile();            // 打开文件
    void slotOpenFolder();          // 打开文件夹
    void slotDisplayImage(const QString &filePath); // 显示图片
    void slotLoadTiffImage();       // 加载tiff图片
    void slotLoadAllFiles();        // 加载所有文件夹
    void slotDeleteCurrentPrintImage(); // 删除当前打印图片
    void slotClearAllImages();      // 清除所有图片

    // ---- 打印参数与命令 ----
    void slotOpenMeteorPrintParamsDialog(); // 设置参数
    void slotSplitCommand();         // 触发闪喷
    void slotSetHeadPowerSwitch();   // 设置喷头电源
    void slotStartPrintMonitor();    // 启动打印监控
    void slotPrintModeChanged(int printMode);//打印模式

    // ---- 用户与系统 ----
    void slotOpenUserAccountManagerDialog(); // 用户账号管理
    void slotLockSoftWareWindow();           // 锁定窗口
    void slotCloseSoftWare();                // 关闭软件
    void slotLanguageSwitch();              // 中英文切换

    // ---- PLC/硬件状态 ----
    void slotUpdateImagePrintStatus(int row, PRINT_STATUS printStatus);
    void slotOpenPlcParamsDialog();          // PLC参数设置
    void slotShrinkAllFiles();               // 收缩所有文件
    void slotPccIdleChanged(bool isIdle);    // PCC状态
    void slotHeadPowerBusyChanged(bool isBusy); // 电源状态
    void slotHeadRunningChanged(bool isRunning); // HDC状态
    void slotCommandExecuted(COMMAND_TYPE type, int resultCode); // 命令执行结果
    void slotAxisError(const QString& axis, long errorCode, const QString& message);
    void slotAxisStatusUpdated(const std::vector<AxisMotionState>& axisData);

    // ---- 视图菜单相关 ----
    void slotPrintFilesToggled();
    void slotRecordToggled();
    void slotMainControlPanelToggled(bool checked);
    void onTabChanged(int index);
    void onDockWidgetVisibilityChanged(bool visible);
    void loadPrintImageFile();




private:
    Ui::HiPrintClass ui;
    QString m_currentLanguage = "zh_CN"; // 默认中文
    int m_printMode = 0;
    

  
};

#endif // HIPRINT_H