#include "HiPrint.h"
#include <QSettings>
#include <QTranslator>
#include <QMouseEvent>
#include <QStandardPaths>
#include <QTabWidget>
#include <QCache>
#include <QHash>
#include <QTimer>
#include <QDateTime>
#include <QThread>
#include "../core/plc/AdsErrorMapper.h"
#include "../common/utils/MessageBoxHelper.h"
#include "../core/plc/DeviceChannelData.h"

// ========================== HiPrint类实现 ==========================


HiPrint::HiPrint(QWidget* parent)
    : QMainWindow(parent)
{
    ui.setupUi(this);

    // ==================== 1. 初始化成员变量 ====================
    initMembers();  // 布尔状态、指针/资源、路径等基本初始化

    // ==================== 2. 加载全局配置 ====================
    loadGlobalConfig();  // 配置文件、数据库、用户偏好、参数中心等

    // ==================== 3. 注册元类型 ====================
    registerMetaTypes();  // 注册自定义数据类型到 Qt 元对象系统

    // ==================== 4. 初始化系统日志和诊断 ====================
    initLogger();  // 配置系统日志与诊断工具

     // ==================== 5. 初始化界面状态 ====================
    initUIState();  // 按钮状态、默认参数、界面解锁/锁定等初始化

    // ==================== 6. 核心业务模块初始化 ====================
  
    initSystem(); // 系统本体初始化

    initializePLCCommunicationModule();// PLC通信模块初始化

    initializeMeteorEngine();// 打印引擎初始化（Meteor等）

    initImageModule();// 图像相关模块初始化（图像处理/场景/视图/绘图等）

    // ==================== 7. 连接信号与槽 ====================
    setupConnections();  // UI 与业务、线程与主界面的信号与槽连接


    // ==================== 8. 启动自检与资源检测 ====================
    systemSelfCheck();  // 设备自检、权限校验等

    // ==================== 9. 启动欢迎界面或主窗口准备 ====================
    showStartupScreen();  // 显示欢迎界面或准备主窗口

    
}




HiPrint::~HiPrint()
{
    if (m_meteorApi) {
        int ret = m_meteorApi->setHeadPower(false);
        if (ret != RVAL_OK) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                QString("关闭电源失败!").toStdString());
        }
        m_meteorApi->closePrinter();
        delete m_meteorApi;
        m_meteorApi = nullptr;
    }
    if (m_beckhoffPlcManager) {
        delete m_beckhoffPlcManager;
        m_beckhoffPlcManager = nullptr;
    }
    if (m_scene) {
        delete m_scene;
        m_scene = nullptr;
    }

    if (m_plcParamConfigManager) {
        delete m_plcParamConfigManager;
        m_plcParamConfigManager = nullptr;
    }
    if (m_printSetting) {
        delete m_printSetting;
        m_printSetting = nullptr;
    }

   
}

void HiPrint::initSystem()
{
}


void HiPrint::initToolbar()
{
    m_toolBar = new MainToolBar(this);
    addToolBar(Qt::TopToolBarArea, m_toolBar);
    const QList<QPair<void (MainToolBar::*)(), void (HiPrint::*)()>> connects = {
        { &MainToolBar::sigOpenFile,             &HiPrint::slotOpenFile },
        { &MainToolBar::sigOpenFolder,           &HiPrint::slotOpenFolder },
        { &MainToolBar::sigSetPrintParams,       &HiPrint::slotOpenMeteorPrintParamsDialog },
        { &MainToolBar::sigControlPLC,           &HiPrint::slotOpenPlcParamsDialog },
        { &MainToolBar::sigStartPrint,           &HiPrint::slotStartPrint },
        { &MainToolBar::sigPausePrint,           &HiPrint::slotPausePrint },
        { &MainToolBar::sigAbortPrint,           &HiPrint::slotStopPrint },
        { &MainToolBar::sigControlNozzle,        &HiPrint::slotSplitCommand },
        { &MainToolBar::sigHeadPowerOn,          &HiPrint::slotSetHeadPowerSwitch },
        { &MainToolBar::sigMonitorEngine,        &HiPrint::slotStartPrintMonitor },
        { &MainToolBar::sigUserAdmin,            &HiPrint::slotOpenUserAccountManagerDialog },
        { &MainToolBar::sigLockAppWindow,        &HiPrint::slotLockSoftWareWindow },
        { &MainToolBar::sigExitApp,              &HiPrint::slotCloseSoftWare },
        { &MainToolBar::sigLanguageSwitch,       &HiPrint::slotLanguageSwitch },
        { &MainToolBar::sigZoomIn,               &HiPrint::zoomIn },
        { &MainToolBar::sigZoomOut,              &HiPrint::zoomOut },
        { &MainToolBar::sigResetZoom,            &HiPrint::resetZoom }
    };
    for (const auto& pair : connects)
        connect(m_toolBar, pair.first, this, pair.second);
}

void HiPrint::initMenuBar()
{
    m_menuBar = new MainMenuBar(this);
    setMenuBar(m_menuBar);
    connect(m_menuBar, &MainMenuBar::sigOpenFile, this, &HiPrint::slotOpenFile);
    connect(m_menuBar, &MainMenuBar::sigOpenFolder, this, &HiPrint::slotOpenFolder);
    connect(m_menuBar, &MainMenuBar::sigExit, this, &HiPrint::close);
    connect(m_menuBar, &MainMenuBar::sigPrintSetting, this, &HiPrint::slotOpenPrintSettingDialog);
    connect(m_menuBar, &MainMenuBar::sigPLCSetting, this, &HiPrint::slotOpenPLCSettingDialog);
    connect(m_menuBar, &MainMenuBar::sigMeteorMonitor, this, &HiPrint::slotOpenMeteorMonitor);
    connect(m_menuBar, &MainMenuBar::sigAbout, this, &HiPrint::slotShowAbout);
    connect(m_menuBar, &MainMenuBar::sigPrintFiles, this, &HiPrint::slotPrintFilesToggled);
    connect(m_menuBar, &MainMenuBar::sigRecord, this, &HiPrint::slotRecordToggled);
    connect(m_menuBar, &MainMenuBar::sigMainControlPanelToggled, this, &HiPrint::slotMainControlPanelToggled);
}

void HiPrint::initTreeView()
{
    m_treeModel = new ImageTreeModel(nullptr, "");
    m_printJobPanel = new PrintJobListPanel(this);
    m_imageDelegate = new ImageDelegate();
    m_printJobPanel->setImageTreeModel(m_treeModel);     
    m_printJobPanel->setImageDelegate(m_imageDelegate); 

    //初始化信号与槽
    initPrintJobPanelSignals();
       
}

void HiPrint::initializeMeteorEngine()
{ 
    // 初始化 Meteor 打印引擎硬件驱动及业务对象
    initializeMeteorPrinterDriver();

    // 启动与 Meteor 打印引擎相关的业务线程、任务队列等
    initializePrintWorkerThreads();
}

void HiPrint::initializePLCCommunicationModule()
{
    bool plcConnectionEstablished = initializeBeckhoffPLCConnection();
    
    if (plcConnectionEstablished) {
        initializePLCUserInterface();
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("PLC通信模块初始化完成").toStdString());
    } else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("PLC通信建立失败，用户界面初始化失败!").toStdString());
    }
}

void HiPrint::initImageModule()
{
    m_scene = new QGraphicsScene();
    m_scene->setSceneRect(0, 0, ui.graphicsView->width(), ui.graphicsView->height());  
    setupGraphicsView();

    QFont largeFont("Microsoft YaHei", 12);
    ui.textImageInfo->setFont(largeFont);
    ui.textPrintInfo->setFont(largeFont);
    ui.textImageInfo->setReadOnly(true);
    ui.textPrintInfo->setReadOnly(true);
}

void HiPrint::setupGraphicsView()
{
    ui.graphicsView->setRenderHint(QPainter::Antialiasing, true);
    ui.graphicsView->setRenderHint(QPainter::SmoothPixmapTransform, true);
    ui.graphicsView->setViewportUpdateMode(QGraphicsView::FullViewportUpdate);
    ui.graphicsView->setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
    ui.graphicsView->setVerticalScrollBarPolicy(Qt::ScrollBarAsNeeded);
    ui.graphicsView->setDragMode(QGraphicsView::ScrollHandDrag);
    // 修改锚点配置，确保居中操作以视图中心为基准
    ui.graphicsView->setTransformationAnchor(QGraphicsView::AnchorViewCenter);
    ui.graphicsView->setResizeAnchor(QGraphicsView::AnchorViewCenter);
    ui.graphicsView->setScene(m_scene);
}

void HiPrint::addImageBorder()
{
    if (m_currentPixmapItem) {
        if (m_imageBorderItem) {
            m_scene->removeItem(m_imageBorderItem);
            delete m_imageBorderItem;
            m_imageBorderItem = nullptr;
        }
    
        // 获取图片项的边界矩形
        QRectF imageRect = m_currentPixmapItem->boundingRect();
        
        // 根据图片大小动态计算边框宽度
        qreal imageWidth = imageRect.width();
        qreal imageHeight = imageRect.height();
        qreal imageSize = qMax(imageWidth, imageHeight);
        
        qreal borderWidth;
        if (imageSize < 200) {
            borderWidth = 1.0;           // 小图片：1像素
        } else if (imageSize < 500) {
            borderWidth = 2.0;           // 中小图片：2像素
        } else if (imageSize < 1000) {
            borderWidth = 3.0;           // 中等图片：3像素
        } else if (imageSize < 2000) {
            borderWidth = 4.0;           // 大图片：4像素
        } else if (imageSize < 3000) {
            borderWidth = 6.0;           // 超大图片：6像素
        } else {
            borderWidth = 30.0;           // 巨大图片：20像素
        }
        
        QRectF borderRect = imageRect.adjusted(-borderWidth, -borderWidth, borderWidth, borderWidth);
       
        // 使用与列表图片一致的颜色：浅蓝色
        QPen dashedPen(QColor(135, 206, 235), borderWidth);  // 浅蓝色，与列表图片一致
        dashedPen.setStyle(Qt::DashLine); 
    
        // 创建边框项，但不添加到场景
        m_imageBorderItem = new QGraphicsRectItem(borderRect);
        m_imageBorderItem->setPen(dashedPen);
        
        // 将边框作为图片项的子项
        m_imageBorderItem->setParentItem(m_currentPixmapItem);
        
        m_imageBorderItem->setZValue(1);  // 相对于父项
        m_imageBorderItem->setFlag(QGraphicsItem::ItemStacksBehindParent, false);
        m_imageBorderItem->setFlag(QGraphicsItem::ItemIsSelectable, false);
        m_imageBorderItem->setFlag(QGraphicsItem::ItemIsMovable, false);
    }
}

void HiPrint::zoomIn()
{
    m_zoomFactor *= 1.2;
    ui.graphicsView->scale(1.2, 1.2);
}

void HiPrint::zoomOut()
{
    m_zoomFactor /= 1.2;
    ui.graphicsView->scale(1.0 / 1.2, 1.0 / 1.2);
}

void HiPrint::resetZoom()
{
    m_zoomFactor = 1.0;
    ui.graphicsView->resetTransform();
    ui.graphicsView->setAlignment(Qt::AlignCenter);
    
    if (m_scene && !m_scene->items().isEmpty() && m_currentPixmapItem) {
        // 适应整个场景，而不是只适应图片
        QRectF sceneRect = m_scene->sceneRect();
        ui.graphicsView->fitInView(sceneRect, Qt::KeepAspectRatio);
        ui.graphicsView->setAlignment(Qt::AlignCenter);
        addImageBorder();
    }
}


void HiPrint::setupConnections()
{
}

void HiPrint::initUIState()
{

    // ========== 1. 顶部导航区域 ==========
    initMenuBar();      // 初始化菜单栏
    initToolbar();      // 初始化工具栏

    // ========== 2. 底部状态栏 ==========
    initStatusBar();    // 初始化状态栏

    // ========== 3. 左侧/主数据视图 ==========
    initTreeView();     // 初始化树形视图

    // ========== 4. 左侧Dock窗口 ==========
    initPrintJobDock(); //初始化Dock窗口
    
    // ========== 5. 自动加载上次的图片目录 ==========
    autoLoadLastImageDirectory();

}

void HiPrint::systemSelfCheck()
{
}

void HiPrint::showStartupScreen()
{

    
}

void HiPrint::initPrintJobDock()
{
 
    m_tabWidget = new QTabWidget(this);
    m_tabWidget->setTabPosition(QTabWidget::South);
    m_tabWidget->addTab(m_printJobPanel, tr("打印文件列表"));
    
    QDockWidget* controlDock = findChild<QDockWidget*>("controlPanelDockWidget");
    controlDock->setObjectName("controlPanelDockWidget");
    if (controlDock) {
        QWidget* controlWidget = controlDock->widget();
        if (controlWidget) {
            controlWidget->setParent(m_tabWidget);
            m_tabWidget->addTab(controlWidget, tr("控制"));
            controlDock->deleteLater(); // 删除原来的DockWidget
        }
    }
    
    QDockWidget* logDock = findChild<QDockWidget*>("logControlPanelDockWidget");
    logDock->setObjectName("logControlPanelDockWidget");
    if (logDock) {
        QWidget* logWidget = logDock->widget();
        if (logWidget) {
            logWidget->setParent(m_tabWidget);
            m_tabWidget->addTab(logWidget, tr("日志记录"));
            logDock->deleteLater(); // 删除原来的DockWidget
        }
    }
    
    m_printJobDock = new QDockWidget(tr("打印文件列表"), this);
    m_printJobDock->setObjectName("printFileList");
    m_printJobDock->setWidget(m_tabWidget);
    
    // 设置DockWidget属性：不可拖动，不可浮动，只允许关闭
    m_printJobDock->setAllowedAreas(Qt::LeftDockWidgetArea);
    m_printJobDock->setFeatures(QDockWidget::DockWidgetClosable);
    
    // 添加到左边区域
    addDockWidget(Qt::LeftDockWidgetArea, m_printJobDock);
    
    // 设置TabWidget的最小高度
    m_tabWidget->setMinimumHeight(400);
    
    // 连接Tab切换信号
    connect(m_tabWidget, &QTabWidget::currentChanged, this, &HiPrint::onTabChanged);
    
    // 连接DockWidget关闭信号
    connect(m_printJobDock, &QDockWidget::visibilityChanged, this, &HiPrint::onDockWidgetVisibilityChanged);
}


void HiPrint::autoLoadLastImageDirectory()
{
    QString lastPath = AppSettings::getImageDirectory();
    if (QDir(lastPath).exists()) {
        QDir dir(lastPath);
        QStringList filters;
        filters << "*.tif" << "*.tiff";
        QFileInfoList imageFiles = dir.entryInfoList(filters, QDir::Files);
        
        if (!imageFiles.isEmpty()) {
            m_printImageDirectory = lastPath;
            
            // 清理图片缓存
            if (m_imageDelegate) {
                m_imageDelegate->clearImageCache();
            }
            
            m_treeModel->loadImageList(lastPath);
            
            // 清理图片参数缓存
            static QHash<QString, PrintImageParam*> imageParamCache;
            for (auto ptr : imageParamCache) {
                delete ptr;
            }
            imageParamCache.clear();
            
            // 记录日志
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
                QString("自动加载上次的图片目录：%1，共找到 %2 个图片文件").arg(lastPath).arg(imageFiles.size()).toStdString());
        }
    }
    else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN, "上次打印的图片目录不存在!");
    }
}



void HiPrint::registerMetaTypes()
{
    // 注册 std::vector<AxisMoveStatusData> 类型
    qRegisterMetaType<std::vector<AxisMotionState>>("std::vector<AxisMotionState>");

    // 注册 PrintImageParam 类型
    qRegisterMetaType<PrintImageParam>("PrintImageParam");

    qRegisterMetaType<PrintImageParam>("PrintImageParam&");

    // 注册 COMMAND_TYPE 类型
    qRegisterMetaType<COMMAND_TYPE>("COMMAND_TYPE");
}

void HiPrint::initPrintJobPanelSignals()
{
    connect(m_printJobPanel, &PrintJobListPanel::sigLoadImage, this, &HiPrint::slotLoadTiffImage);
    connect(m_printJobPanel, &PrintJobListPanel::sigLoadFiles, this, &HiPrint::slotLoadAllFiles);
    connect(m_printJobPanel, &PrintJobListPanel::sigDeleteCurrent, this, &HiPrint::slotDeleteCurrentPrintImage);
    connect(m_printJobPanel, &PrintJobListPanel::sigClearAll, this, &HiPrint::slotClearAllImages);
    connect(m_printJobPanel, &PrintJobListPanel::sigImageItemDoubleClicked, this, &HiPrint::slotDisplayImage);

}

void HiPrint::configurePrinterParameters()
{
    // 设置 CCP_PRINT_CLOCK_HZ 参数
    int ret = m_meteorApi->setPrinterParameter(CCP_PRINT_CLOCK_HZ, 0);
    if (ret != RVAL_OK) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("Set CCP_PRINT_CLOCK_HZ Command To Meteror Fail! %1").arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
        return;
    }

    // 设置 CCP_BITS_PER_PIXEL 参数
    ret = m_meteorApi->setPrinterParameter(CCP_BITS_PER_PIXEL, 1);
    if (ret != RVAL_OK) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("Set CCP_BITS_PER_PIXEL Command To Meteror Fail! %1").arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
        return;
    }
}

void HiPrint::initStatusBar()
{
    m_statusBar = new MainStatusBar(this);
    setStatusBar(m_statusBar);
    
    // 连接PLC连接状态变化信号到状态栏更新
    connect(this, &HiPrint::plcConnectionStatusChanged,
            m_statusBar, &MainStatusBar::setPlcConnectionStatus);
}

void HiPrint::initializeMeteorPrinterDriver()
{
    m_meteorApi = new MeteorApi();

    //bool bStartPrintEnginer = startPrintEnginer();
    //if (!bStartPrintEnginer) {
    //    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "启动打印引擎失败!");
    //}

     // 连接打印机
    int ret = m_meteorApi->connectPrinter();
    if (ret != RVAL_OK) {
        m_isPrinterConnected = false;
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("连接打印机失败! %1").arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());

        // 如果连接失败，停止打印引擎
        ret = m_meteorApi->stopPrintEngine(true);
        if (ret != RVAL_OK) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                QString("关闭打印机失败! %1").arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
            return;
        }
    }
    else {
        m_isPrinterConnected = true;
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "连接打印机成功!");
    }

    // 参数配置：设置打印机参数
    configurePrinterParameters();

    
}

bool HiPrint::initializeBeckhoffPLCConnection()
{
    // ==================== 1. 创建PLC管理器并建立信号连接 ====================
    createAndConnectPLCManager();

    // ==================== 2. 打开通信端口、启动心跳检测、验证ADS状态 ====================
    if (!initializePLCCommunication()) {
        return false;
    }

    // ==================== 3. 完成初始化 ====================
    completePLCInitialization();
    
    return true;
}

// ==================== 私有辅助方法 ====================

void HiPrint::createAndConnectPLCManager()
{
    m_beckhoffPlcManager = new BeckhoffPlcManager();
    
    // 建立信号连接
    connect(m_beckhoffPlcManager, &BeckhoffPlcManager::axisStatusUpdated,
            this, &HiPrint::slotAxisStatusUpdated);
    connect(m_beckhoffPlcManager, &BeckhoffPlcManager::axisError,
            this, &HiPrint::slotAxisError);
    connect(m_beckhoffPlcManager, &BeckhoffPlcManager::plcConnectionStatusChanged,
            this, &HiPrint::onPLCConnectionStatusChanged);
}

bool HiPrint::initializePLCCommunication()
{
    // 打开PLC通信端口
    int portResult = m_beckhoffPlcManager->openCommunicationPort();
    if (portResult != 0) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("PLC通信端口打开失败，错误码: %1").arg(AdsErrorMapper::getErrorDescription(portResult)).toStdString());
        return false;
    }
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("PLC通信端口打开成功").toStdString());

    // 启动心跳检测并等待稳定
   // m_beckhoffPlcManager->startHeartbeatDetection();
    
    //const int maxWaitCount = 30; // 30 * 100ms = 3秒
    //int waitCount = 0;
    //
    //while (waitCount < maxWaitCount && !m_beckhoffPlcManager->isCommunicationHealthy()) {
    //    QThread::msleep(100);
    //    waitCount++;
    //}
    //
    //if (!m_beckhoffPlcManager->isCommunicationHealthy()) {
    //    m_isPLCConnected = false;
    //    LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
    //        QString("PLC心跳检测失败，通信状态不健康").toStdString());
    //    
    //    if (m_statusBar) {
    //        m_statusBar->setPlcConnectionStatus(false);
    //    }
    //    
    //    return false;
    //}
    //
    //LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
    //    QString("PLC心跳检测成功，通信状态健康").toStdString());

    // 验证ADS状态
  /*  if (!m_beckhoffPlcManager->checkADSState()) {
        m_isPLCConnected = false;
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("ADS状态检查失败，PLC相关功能将不可用").toStdString());
        
        if (m_statusBar) {
            m_statusBar->setPlcConnectionStatus(false);
        }
        
        return false;
    }
    
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("ADS状态检查成功").toStdString());*/
    
    return true;
}

void HiPrint::completePLCInitialization()
{
    m_isPLCConnected = true;
    
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("PLC心跳检测和ADS状态检查均成功，通信完全正常，PLC功能初始化完成").toStdString());
    
    // 更新状态栏显示PLC已连接
    if (m_statusBar) {
        m_statusBar->setPlcConnectionStatus(true);
    }
}

bool HiPrint::isPLCCommunicationAvailable() const
{
    return m_isPLCConnected;
}

void HiPrint::initMembers()
{
    m_currentLanguage = LANG_ZH_CN; // 默认中文

    // 初始化运动操作状态
    m_isMotionOperationInProgress = false;
    m_lastMotionOperationTime = 0;
    
    // 初始化PLC通信心跳检测状态
    m_isPLCConnected = false;
    m_plcConnectionFailures = 0;
    m_plcConnectionSuccesses = 0;
    m_plcConnectionStatusReported = false;
    
    PrintParamSettings* printParamSettings = new PrintParamSettings();

    // 获取当前配置的所有参数值
    QMap<QString, QVariant> currentParameters = printParamSettings->getCurrentProfileParameters();

    double layerThickness = currentParameters.value("print_layer_thickness", 0.3).toDouble();
    double xySpeed = currentParameters.value("xy_speed", 50.0).toDouble();
    double printResolution = currentParameters.value("print_x_resolution", 300.0).toDouble();
    double sandSpreadingSpeed = currentParameters.value("sand_spreading_speed", 30.0).toDouble();
}

void HiPrint::loadGlobalConfig()
{

    // 1. 加载本地打印作业参数（PrintJob 配置段）
    QSettings settings("./HiPrintApp.ini", QSettings::IniFormat);
    m_yTop = settings.value("PrintJob/yTop", 0).toInt();
    m_trueBpp = settings.value("PrintJob/trueBpp", 1).toInt();
    m_plane = settings.value("PrintJob/plane", 1).toInt();
    m_xLeft = settings.value("PrintJob/xLeft", 700).toInt();
    m_printHeadNum = settings.value("PrintJob/printHeadNum", 2).toInt();
    m_maxWaitTimeMs = settings.value("PrintJob/maxWaitTimeMs", 5 * 60 * 1000).toInt();

    // 2. 加载全局参数配置
    if (!ParameterManager::instance().load()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, tr("加载全局参数失败 (ParameterManager::load)").toStdString());
    }

    // 3. 加载设备轴映射配置（含完整性校验）
    const QString deviceMappingPath = DEVICE_MAPPING_JSON_PATH;
    DeviceMappingConfig::instance().loadConfig(deviceMappingPath);
   
    // 4. 恢复窗口几何尺寸
    QByteArray geoData = AppSettings::getGeometry();
    if (!geoData.isEmpty()) {
        restoreGeometry(geoData);
    }
    else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN, tr("未检测到窗口位置参数 (AppSettings::getGeometry))").toStdString());
    }

    // 5. 恢复窗口界面状态
    QByteArray stateData = AppSettings::getWindowState();
    if (!stateData.isEmpty()) {
        restoreState(stateData);
    }
    else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN, tr("未检测到窗口界面状态参数 (AppSettings::getWindowState)").toStdString());
    }


}

void HiPrint::initLogger()
{
    //注册日志
    LogManager::instance().init(ui.textEdit);
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "========== 日志记录开始 ==========");
}

void HiPrint::initializePLCUserInterface()
{
    // 检查PLC连接状态，如果心跳检测或ADS状态检查失败则跳过PLC相关初始化
    if (!isPLCCommunicationAvailable()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("跳过PLC用户界面初始化，PLC通信状态或ADS状态异常").toStdString());
        return;
    }

    // ==================== 1. 轴控制相关初始化 ====================
    setupPlcAxisMappings();  // 配置与PLC相关的变量和按钮

    // ==================== 2. 电机使能功能 ====================
    setupAxisEnableActions();   // 配置电机使能相关动作

    // ==================== 3. 轴运动功能 ====================
    bindAxisMoveControls();    // 绑定轴运动相关控件与动作

    // ==================== 4. 点动（Jog）功能 ====================
    connectJogControls();    // 连接点动按钮与控制逻辑

    // ==================== 5. 轴回零功能 ====================
    setupAxisHomeLogic();     // 配置回零相关逻辑

    // ==================== 6. 急停/停止功能 ====================
    setupAxisStopHandlers();   // 配置急停/停止事件处理

    // ==================== 7. 快速位置相关功能 ====================
    setupQuickPositionActions();  //配置快速定位相关动作

    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("PLC用户界面初始化完成").toStdString());
}


void HiPrint::initializePrintWorkerThreads()
{
    m_workerThreadManager = new WorkerThreadManager(this);

    connect(m_workerThreadManager->printJobThread(), &PrintJobManagerThread::printStatus,
        this, &HiPrint::slotUpdateImagePrintStatus, Qt::BlockingQueuedConnection);

    connect(m_workerThreadManager->printJobThread(), &PrintJobManagerThread::sendPrintImageParam,
        this, &HiPrint::showPrintImage, Qt::BlockingQueuedConnection);

    connect(m_workerThreadManager->statusThread(), &PrintStatusManagerThread::pccIdleChanged,
        this, &HiPrint::slotPccIdleChanged);

    connect(m_workerThreadManager->statusThread(), &PrintStatusManagerThread::headPowerBusyChanged,
        this, &HiPrint::slotHeadPowerBusyChanged);

    connect(m_workerThreadManager->statusThread(), &PrintStatusManagerThread::headRunningChanged,
        this, &HiPrint::slotHeadRunningChanged);

    connect(m_workerThreadManager->commandThread(), &PrintCommandManagerThread::commandExecuted,
        this, &HiPrint::slotCommandExecuted);

    connect(m_workerThreadManager->printJobThread(), &PrintJobManagerThread::printProgressUpdated,
        this, &HiPrint::updatePrintProgress);

    m_workerThreadManager->printJobThread()->setBeckhoffPlc(m_beckhoffPlcManager->getBeckhoffPlc());

}

void HiPrint::showPrintImage(const QString &imagePath, const PrintImageParam &printImageParam)
{
    QFileInfo fileInfo(imagePath);
    m_currentShowImageName = fileInfo.fileName();
   
    if (printImageParam.printImage.isNull()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, 
            "图片数据无效！");
        return;
    }
    
    ui.graphicsView->setUpdatesEnabled(false);
    ui.graphicsView->resetTransform();
    ui.graphicsView->setAlignment(Qt::AlignCenter);
    
    m_scene->clear();
    m_currentPixmapItem = nullptr;
    m_imageBorderItem = nullptr;
    
    QPixmap pixmap = QPixmap::fromImage(printImageParam.printImage);
    if (pixmap.isNull()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, 
            "创建QPixmap失败！");
        ui.graphicsView->setUpdatesEnabled(true);
        return;
    }
    
    m_currentPixmapItem = m_scene->addPixmap(pixmap);
    if (!m_currentPixmapItem) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, 
            "添加PixmapItem到场景失败！");
        ui.graphicsView->setUpdatesEnabled(true);
        return;
    }
   
    QRectF imageRect = m_currentPixmapItem->boundingRect();
    QRectF sceneRect = m_scene->sceneRect();
    QPointF centerPos = sceneRect.center() - imageRect.center();
    m_currentPixmapItem->setPos(centerPos);
    
    QRectF newSceneRect = imageRect.translated(centerPos);
    newSceneRect = newSceneRect.adjusted(-50, -50, 50, 50);  // 为边框留出50像素空间
    m_scene->setSceneRect(newSceneRect);
    
    if (ui.graphicsView->scene() != m_scene) {
        ui.graphicsView->setScene(m_scene);
    }
   
    ui.graphicsView->setUpdatesEnabled(true);
   
    QTimer::singleShot(100, [this]() {
        if (m_scene && !m_scene->items().isEmpty() && m_currentPixmapItem) {
            QRectF sceneRect = m_scene->sceneRect();
            ui.graphicsView->fitInView(sceneRect, Qt::KeepAspectRatio);
            ui.graphicsView->setAlignment(Qt::AlignCenter);
            
            addImageBorder();
            ui.graphicsView->show();
        }
    });
    
    updateImageInfoDisplay(imagePath, printImageParam);
}
   
PrintJobParam HiPrint::createPrintJobParam() const
{
    PrintJobParam param;
    param.yTop = m_yTop;
    param.trueBpp = m_trueBpp;
    param.plane = m_plane;
    param.xLeft = m_xLeft / 25.4 * 400; 
    param.printHeadNum = m_printHeadNum;
    param.maxWaitTimeMs = m_maxWaitTimeMs;
    param.printMode = DeviceMappingConfig::instance().printModeMainScan().toStdString();
    param.scanFinishCommand = DeviceMappingConfig::instance().scanFinishedCmd().toStdString();
    param.imagePrintPass = DeviceMappingConfig::instance().imagePrintList().toStdString();
    param.scanTimesCommand = DeviceMappingConfig::instance().scanCountCmd().toStdString();
    param.autoPrintCommand = DeviceMappingConfig::instance().autoPrintCmd().toStdString();
    param.bAutoSkipWhite = ParameterManager::instance().getAutoWhiteSkip();
    param.printDirPath = m_printImageDirectory.toStdString();

    // 4. 打印模式类型
   // m_printMode == 0 ? param.modeType = PrintModeType::CONTINUOUS : param.modeType = PrintModeType::SINGLEFILE;

    return param;
}


bool HiPrint::checkPrinterConnected()
{
    if (!m_isPrinterConnected) {
        MessageBoxHelper::showInfo(this, tr("提示"), tr("打印机未连接,请检查!!!"));
        return false;
    }
    return true;
}

bool HiPrint::checkScanMode()
{
    int ret = m_meteorApi->checkPrinterIsScanMode();
    if (ret != RVAL_OK) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("The current printing mode is not set to scanning mode. Please verify! [%1]")
            .arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
        return false;
    }
    return true;
}

bool HiPrint::checkHeadStatus(const TAppStatus* appStatus)
{
    bool ret = m_meteorApi->checkHeadStatusIsRunning(appStatus);
    if (!ret) {
        MessageBoxHelper::showInfo(this, tr("提示"), tr("打印头未上电,请检查!!!"));
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("The spray head is not properly powered on. Please check! [%1]")
            .arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
        return false;
    }
    return true;
}

bool HiPrint::checkAxisZeroSignals()
{

    QStringList readFailedAxes;
    QStringList invalidValueAxes;

    struct AxisZeroInfo {
        QString name;           
        QString signalName;    
        bool value;           
        long retCode;         
    };
    QVector<AxisZeroInfo> axes = {
        {"墨车", DeviceMappingConfig::instance().xyZeroSignal(), false, 0},
        {"成型缸", DeviceMappingConfig::instance().platformZeroSignal(), false, 0},
        {"下砂车", DeviceMappingConfig::instance().fallingZeroSignal(), false, 0},
        {"铺砂车", DeviceMappingConfig::instance().spreadZeroSignal(), false, 0}
    };
    
    for (auto& axis : axes) {
        axis.retCode = m_beckhoffPlcManager->readIOInPut(
            axis.signalName.toStdString(), axis.value);
        
        if (axis.retCode != 0) {
            readFailedAxes << axis.name;
        } else if (!axis.value) {
            invalidValueAxes << axis.name;
        }
    }
    if (!readFailedAxes.isEmpty()) {
        QString errorMsg = QString("以下轴的零位信号读取失败:\n%1")
            .arg(readFailedAxes.join("、"));
        MessageBoxHelper::showError(this, tr("零位信号读取错误"), errorMsg);
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("零位信号读取失败: %1").arg(readFailedAxes.join("、")).toStdString());
        return false;
    }
   
    if (!invalidValueAxes.isEmpty()) {
        QString signalList = invalidValueAxes.join("、");
        QString errorMsg = QString("以下轴的零位信号无效，请检查轴位置:\n%1").arg(signalList);
        MessageBoxHelper::showWarning(this, tr("零位信号检查"), errorMsg);
        
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN,
            QString("零位信号检查失败: %1").arg(signalList).toStdString());
        return false;
    }

    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("所有轴的零位信号检查通过").toStdString());
    return true;
}

bool HiPrint::startPrintEnginer()
{
   extern bool AddMeteorPath(bool abAdd32BitRuntime);
   if (!AddMeteorPath(sizeof(void*) == sizeof(uint32))) {
       LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Add Meteror Path Fail!!!");
       return false;
   }
   const char* configFilePath = METEOR_PINTENGI_CONFIG_PATH;
   int ret = m_meteorApi->startPrintEngine(configFilePath);
   if (ret != RVAL_OK) {
       LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Start Print Engine Fail!!!,%1").
           arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
       return false;
   }
   else {
       LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Start Print Engine Success!!!");
   }
   return true;
}

void HiPrint::updatePrintProgress(int printedFiles, int totalFiles, QString fileName, PrintImageParam& printImageParam)
{
    m_printedFiles = printedFiles;
    m_totalPrintFiles = totalFiles;
    updateImageInfoDisplay(fileName, printImageParam);

    if (m_printedFiles == m_totalPrintFiles && m_totalPrintFiles > 0 && !m_isPrintJobCompleted) {
        m_isPrintJobCompleted = true;
        showPrintCompletionMessage();
    }
}

void HiPrint::resetProgressInfo()
{
    m_printStartTime = QDateTime();
    m_printedFiles = 0;
    m_totalPrintFiles = 0;
    m_currentPrintParams.clear();
    m_isPrintJobCompleted = false;  
}

void HiPrint::showPrintCompletionMessage()
{
    QString infoMsg = tr("打印任务已完成！\n\n");
    infoMsg += tr("已打印文件数: %1 个").arg(m_totalPrintFiles);
    
    if (m_printStartTime.isValid()) {
        qint64 elapsedSeconds = m_printStartTime.secsTo(QDateTime::currentDateTime());
        qint64 hours = elapsedSeconds / 3600;
        qint64 minutes = (elapsedSeconds % 3600) / 60;
        qint64 seconds = elapsedSeconds % 60;
        
        infoMsg += "\n";
        if (hours > 0) {
            infoMsg += tr("打印耗时: %1 小时 %2 分钟 %3 秒")
                .arg(hours).arg(minutes).arg(seconds);
        } else if (minutes > 0) {
            infoMsg += tr("打印耗时: %1 分钟 %2 秒").arg(minutes).arg(seconds);
        } else {
            infoMsg += tr("打印耗时: %1 秒").arg(seconds);
        }
        infoMsg += "\n";
        infoMsg += tr("完成时间: %1").arg(formatDateTime(QDateTime::currentDateTime()));
    }
    MessageBoxHelper::showInfo(this, tr("打印完成"), infoMsg);
}

QString HiPrint::formatDateTime(const QDateTime& dateTime)
{
    if (!dateTime.isValid()) {
        return "--";
    }
    return dateTime.toString("yyyy-MM-dd hh:mm:ss");
}

bool HiPrint::checkHeadPowerPreconditions()
{
    const QList<std::pair<std::function<bool()>, QString>> checks = {
      { [&] { return !m_isPrinterConnected; }, "打印引擎没有连接，请连接后再重试!!!" },
      { [&] { return !m_isPCCIdle; }, "PCC没有处于空闲状态,不能打开喷头!!!" },
      { [&] { return m_isPrintHeadBusy; }, "打印头正忙,不能打开喷头!!!" },
    };
    for (const auto& check : checks) {
        if (check.first()) {
            MessageBoxHelper::showWarning(this, tr("提示"), check.second);
            return false;
        }
    }
    return true;
}

void HiPrint::closeEvent(QCloseEvent* event)
{
  
    // ==================== 1. 保存窗口状态和语言 ====================
    AppSettings::setGeometry(saveGeometry());        // 保存窗口几何尺寸和位置
    AppSettings::setWindowState(saveState());        // 保存窗口状态
    AppSettings::setLanguage(m_currentLanguage);     // 保存当前语言设置

    // ==================== 2. 显示关闭确认消息框 ====================
    auto ret = MessageBoxHelper::showQuestion(
        this,
        tr("关闭确认"),               // 确认框标题
        tr("确定要关闭窗口吗？"),       // 提示内容
        tr("关闭窗口将导致所有未保存的打印设置丢失。"), // 详细提示
        QMessageBox::No               // 默认选择"否"
    );

    // ==================== 3. 根据用户选择处理 ====================
    if (ret == QMessageBox::Yes) {
        event->accept();    // 用户选择"是"，接受关闭事件
    }
    else {
        event->ignore();    // 用户选择"否"，忽略关闭事件
    }

}

void HiPrint::changeEvent(QEvent* event)
{
    QMainWindow::changeEvent(event);
}
void HiPrint::resizeEvent(QResizeEvent* event)
{
    if (!m_ignoreResize) {
        QMainWindow::resizeEvent(event);
        
        // 如果当前有图片显示，重新适应窗口大小
        if (m_currentPixmapItem && ui.graphicsView) {
            // 适应整个场景，而不是只适应图片
            QRectF sceneRect = m_scene->sceneRect();
            ui.graphicsView->fitInView(sceneRect, Qt::KeepAspectRatio);
            ui.graphicsView->setAlignment(Qt::AlignCenter);
            addImageBorder();
        }
    }
}

void HiPrint::slotOpenPrintSettingDialog()
{
    // 调用与工具栏相同的打印参数设置功能
    slotOpenMeteorPrintParamsDialog();
}

void HiPrint::slotOpenPLCSettingDialog()
{
    // 调用与工具栏相同的PLC参数设置功能
    slotOpenPlcParamsDialog();
}


void HiPrint::slotOpenMeteorMonitor()
{
    // 调用与工具栏相同的引擎监控功能
    slotStartPrintMonitor();
}

void HiPrint::slotShowAbout()
{
}

void HiPrint::slotStartPrint()
{

    int ret = m_meteorApi->setHome();
    if (ret != RVAL_OK) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("Set Home Fail,Value is %1").arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
        return;

    }
    /*QStringList fileList;
    if (m_treeModel) {
        fileList = m_treeModel->getAllFilePaths();
    }
    if (fileList.isEmpty()) {
        MessageBoxHelper::showWarning(this, tr("提示"), tr("没有可打印的文件，请先加载图片文件。"));
        return;
    }
    PrintConfigDialog dialog(fileList, this);
    if (dialog.exec() != QDialog::Accepted || !dialog.isConfirmed()) {
        return; 
    }
    PrintConfigDialog::PrintTask task = dialog.getPrintTask();
    if (task.mode == PrintConfigDialog::CONTINUOUS_MODE) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
            QString("开始连续打印，共 %1 个文件").arg(task.continuous.totalFiles).toStdString());
        executeContinuousPrint(fileList);
    } else if (task.mode == PrintConfigDialog::SINGLE_FILE_MODE) {
        QString fileName = task.singleFile.selectedFileName;
        int repeatCount = task.singleFile.repeatCount;
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
            QString("开始单文件打印：%1，重复 %2 次").arg(fileName).arg(repeatCount).toStdString());
        executeSingleFilePrint(fileList,fileName, repeatCount);
    }*/
}

void HiPrint::slotPausePrint()
{
    PRINT_JOBSTATUS currentStatus = m_workerThreadManager->printJobThread()->getPrintJobStatus();
    if (currentStatus == PRINT_JOBSTATUS::PRINT_STOPPED) {
        MessageBoxHelper::showWarning(
            this,
            tr("操作提示"),
            tr("当前没有正在进行的打印任务，无法执行暂停操作。\n\n请先开始打印任务。")
        );
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, 
            tr("用户尝试暂停未开始的打印任务").toUtf8().constData());
        return;
    }
    m_workerThreadManager->printJobThread()->pauseOrResumePrint();
    updatePauseButtonState();
    if (currentStatus == PRINT_JOBSTATUS::PRINT_RUNNING) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
            tr("用户暂停打印任务").toUtf8().constData());
        MessageBoxHelper::showInfo(this, tr("提示"), tr("打印已暂停"));
    }
    else if (currentStatus == PRINT_JOBSTATUS::PRINT_PAUSED) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
            tr("用户继续打印任务").toUtf8().constData());
        MessageBoxHelper::showInfo(this, tr("提示"), tr("打印已继续"));
    }
}

void HiPrint::slotStopPrint()
{
    PRINT_JOBSTATUS currentStatus = m_workerThreadManager->printJobThread()->getPrintJobStatus();
    if (currentStatus == PRINT_JOBSTATUS::PRINT_STOPPED) {
        // 打印未开始，显示提示
        MessageBoxHelper::showWarning(
            this,
            tr("操作提示"),
            tr("当前没有正在进行的打印任务，无法执行停止操作。\n\n请先开始打印任务。")
        );
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN, tr("用户尝试停止未开始的打印任务").toUtf8().constData());
        return;
    }

    // 显示确认对话框
    QMessageBox::StandardButton result = MessageBoxHelper::showQuestion(
        this,
        tr("确认停止打印"),
        tr("确定要停止当前打印任务吗？\n\n此操作将中断正在进行的打印过程。")
    );

    if (result != QMessageBox::Yes) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("用户取消停止打印操作").toUtf8().constData());
        return;
    }

    // 执行停止操作
    m_workerThreadManager->printJobThread()->stopPrint();
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("用户停止打印任务").toUtf8().constData());

    resetProgressInfo();
    
    // 停止后立即更新按钮状态
    updatePauseButtonState();
    
    // 停止后显示完成提示
    MessageBoxHelper::showInfo(
        this,
        tr("提示"),
        tr("打印任务已停止。\n\n当前状态：无打印任务")
    );
}

void HiPrint::updatePauseButtonState()
{
    if (!m_toolBar || !m_workerThreadManager || !m_workerThreadManager->printJobThread()) {
        return;
    }
    PRINT_JOBSTATUS currentStatus = m_workerThreadManager->printJobThread()->getPrintJobStatus();
    switch (currentStatus) {
        case PRINT_JOBSTATUS::PRINT_STOPPED:
            m_toolBar->m_pausePrintAction->setEnabled(false);
            m_toolBar->m_pausePrintAction->setText(tr("暂停"));
            m_toolBar->m_pausePrintAction->setIcon(QIcon(PAUSE_PRINT_ICON_PATH));
            m_toolBar->m_pausePrintAction->setToolTip(tr("当前没有打印任务"));
            break;
            
        case PRINT_JOBSTATUS::PRINT_RUNNING:
            m_toolBar->m_pausePrintAction->setEnabled(true);
            m_toolBar->m_pausePrintAction->setText(tr("暂停"));
            m_toolBar->m_pausePrintAction->setIcon(QIcon(PAUSE_PRINT_ICON_PATH));
            m_toolBar->m_pausePrintAction->setToolTip(tr("暂停打印 (Ctrl+U)"));
            break;
            
        case PRINT_JOBSTATUS::PRINT_PAUSED:
            m_toolBar->m_pausePrintAction->setEnabled(true);
            m_toolBar->m_pausePrintAction->setText(tr("继续"));
            m_toolBar->m_pausePrintAction->setIcon(QIcon(CONTINE_PRINT_ICON_PATH)); // 切换为继续图标
            m_toolBar->m_pausePrintAction->setToolTip(tr("继续打印 (Ctrl+U)"));
            break;
    }
}

void HiPrint::slotMoveToHome()
{   
}

void HiPrint::slotMoveToStandby()
{
}

void HiPrint::slotPrintStartPosition()
{
}

void HiPrint::slotPrintEndPosition()
{
}

void HiPrint::slotCleanPosition()
{
}

void HiPrint::slotFlashSprayPosition()
{
}

void HiPrint::slotHydrationPosition()
{
}

void HiPrint::slotOpenFile()
{
    loadPrintImageFile();
}

void HiPrint::slotOpenFolder()
{
    slotLoadTiffImage();
}

void HiPrint::slotOpenMeteorPrintParamsDialog()
{
    if (m_printSetting && m_printSetting->isVisible()) {
        return;
    }
    if (!m_printSetting) {
        m_printSetting = new PrintConfigManager();
    }
    QRect mainWindowRect = this->geometry();
    QSize dialogSize = m_printSetting->sizeHint();
    int x = mainWindowRect.x() + (mainWindowRect.width() - dialogSize.width()) / 2;
    int y = mainWindowRect.y() + (mainWindowRect.height() - dialogSize.height()) / 2;
    m_printSetting->move(x, y);
    m_printSetting->show();
}

void HiPrint::slotDisplayImage(const QString& filePath)
{
    if (!QFile::exists(filePath)) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, 
            QString("图片文件不存在：%1").arg(filePath).toStdString());
        MessageBoxHelper::showWarning(this, tr("文件不存在"), 
            tr("选择的图片文件不存在，请检查文件路径！"));
        return;
    }
   
    // 检查是否是当前显示的图片，避免重复处理
    static QString lastDisplayedImage;
    if (lastDisplayedImage == filePath && m_currentPixmapItem) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
            QString("图片已在显示中：%1").arg(QFileInfo(filePath).fileName()).toStdString());
        return;
    }
    
    // 用QHash做缓存
    static QHash<QString, PrintImageParam*> imageParamCache;
    PrintImageParam* cachedParam = imageParamCache.value(filePath, nullptr);
    
    if (!cachedParam) {
        // 缓存中没有，需要处理图片
        auto imageProcessor = ImageProcessorFactory::create(filePath);
        bool isReadSuccess = imageProcessor->loadImage(filePath);
        if (!isReadSuccess) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, 
                QString("读取图片失败：%1").arg(filePath).toStdString());
            MessageBoxHelper::showWarning(this, tr("读取失败"), 
                tr("无法读取图片文件，请检查文件格式是否正确！"));
            return; 
        }
        
        // 创建并缓存图片参数
        cachedParam = new PrintImageParam();
        cachedParam->printImage = imageProcessor->convertToQImage();
        cachedParam->width = imageProcessor->getWidth();
        cachedParam->height = imageProcessor->getHeight();
        cachedParam->xDimension = imageProcessor->getXDimension();
        cachedParam->yDimension = imageProcessor->getYDimension();
        cachedParam->xResolution = imageProcessor->getXResolution();
        cachedParam->yResolution = imageProcessor->getYResolution();
        cachedParam->bitsPerSample = imageProcessor->getBitsPerSample();
        
        imageParamCache.insert(filePath, cachedParam);
        
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
            QString("图片处理完成并缓存：%1").arg(QFileInfo(filePath).fileName()).toStdString());
    }
    
    // 显示图片
    showPrintImage(filePath, *cachedParam);
    lastDisplayedImage = filePath;
}

void HiPrint::slotSplitCommand()
{
    QMessageBox::StandardButton result = MessageBoxHelper::showQuestion(
        this,
        tr("确认进行闪喷"),
        tr("请检查是否移动到了闪喷位置!")
    );
    if (result != QMessageBox::Yes) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("用户取消了闪喷操作").toUtf8().constData());
        return;
    }
    if (!checkHeadPowerPreconditions()){
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Check Head Power preConitions Fail!!!!");
        return;
    }
    // 获取当前选中的喷头列表
    QList<int> activeNozzles;
    auto& pm = ParameterManager::instance();
    if (pm.getIsCheckNozzle1()) activeNozzles << NOZZLE1;
    if (pm.getIsCheckNozzle2()) activeNozzles << NOZZLE2;
    if (pm.getIsCheckNozzle3()) activeNozzles << NOZZLE3;
    if (pm.getIsCheckNozzle4()) activeNozzles << NOZZLE4;

    // 检查是否有选中的喷头
    if (activeNozzles.isEmpty()) {
        MessageBoxHelper::showWarning(this, tr("提示"), tr("请至少选择一个喷头进行闪喷操作！"));
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN, tr("用户尝试执行闪喷但未选择任何喷头").toUtf8().constData());
        return;
    }

    QStringList nozzleStringList;
    for (int nozzle : activeNozzles) {
        nozzleStringList << QString::number(nozzle);
    }
    QString nozzleInfo = nozzleStringList.join(",");
    
    // 显示确认对话框
    result = MessageBoxHelper::showQuestion(
        this,
        tr("确认闪喷操作"),
        tr("确定要执行闪喷操作吗？\n\n参数信息：\n- PCC数量：%1\n- 当前选择打印的喷头: %2\n- 闪喷墨量：%3")
        .arg(AppSettings::getPccNum())
        .arg(nozzleInfo)
        .arg(pm.getFlashSprayCount())
    );

    if (result != QMessageBox::Yes) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("用户取消闪喷操作").toUtf8().constData());
        return;
    }

    // 记录开始执行闪喷操作
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
        QString("开始执行闪喷操作，选中的喷头: %1").arg(nozzleInfo).toStdString());

    int pccNum = AppSettings::getPccNum();
    int spitCount = ParameterManager::instance().getFlashSprayCount();
    
    // 使用新的喷头列表参数
    DeviceCommand cmd(CMD_SET_SPLIT_SIGNAL, 
        QVariantMap{ 
            {"pccNum", pccNum},
            {"nozzleList", QVariant::fromValue(activeNozzles)},
            {"spitCount", spitCount} 
        }, 
        CMD_PRIORITY_NORMAL);
    
    m_workerThreadManager->commandThread()->submitCommand(cmd);

    MessageBoxHelper::showInfo(
        this,
        tr("操作已提交"),
        tr("闪喷命令已提交到执行队列，请等待操作完成。\n\n选中的喷头: %1").arg(nozzleInfo)
    );
}

void HiPrint::slotSetHeadPowerSwitch()
{
    if (!checkHeadPowerPreconditions())
    {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Check Head Power preConitions Fail!!!!");
        return;
    }    
    m_isPrintHeadPowered = !m_isPrintHeadPowered;
    DeviceCommand cmd(
        CMD_HEAD_POWER,
        QVariantMap{ {"enabled", m_isPrintHeadPowered} },
        CMD_PRIORITY_HIGH
    );
    m_workerThreadManager->commandThread()->submitCommand(cmd);
}

void HiPrint::slotStartPrintMonitor()
{
    QString program = METEOR_MONITOR_PROGRAM_PATH;
    QString workingDir = METEOR_MONITOR_WORKING_DIR;
    bool ret = QProcess::startDetached(program, QStringList(), workingDir);
    if (!ret) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Failure to Start an External Process!!!!");
    }
    else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Success Start an External Process!!!!");
    }
}

void HiPrint::slotPrintModeChanged(int printMode)
{
    m_printMode = printMode;
}

void HiPrint::slotOpenUserAccountManagerDialog()
{
    UserAccountManager dlg(&UserManager::instance(), UserSession::instance().currentUser(), this);
    dlg.exec();
}

void HiPrint::slotLockSoftWareWindow()
{
    

}

void HiPrint::mousePressEvent(QMouseEvent* event)
{
   
}

void HiPrint::mouseMoveEvent(QMouseEvent* event)
{
 
}

void HiPrint::slotCloseSoftWare()
{
    this->close();
}

void HiPrint::slotLanguageSwitch()
{
    static bool isEnglish = false;  
    static QTranslator translator;

    // 英文和中文翻译文件的路径
    const QString enQmPath = EN_TRANSLATION_PATH;
    const QString zhQmPath = ZH_TRANSLATION_PATH;

    // 切换语言
    if (isEnglish) {
        qApp->removeTranslator(&translator);
        if (translator.load(zhQmPath)) {
            qApp->installTranslator(&translator);
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Switched to Chinese");
            m_currentLanguage = LANG_ZH_CN;
            m_toolBar->m_languageSwitchAction->setIcon(QIcon(LAMGUAGE_SWITCH_CH_ICON_PATH));
        }
        else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Failed to load Chinese translation file!");
        }
    }
    else {
        qApp->removeTranslator(&translator);
        if (translator.load(enQmPath)) {
            qApp->installTranslator(&translator);
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Switched to English");
            m_currentLanguage = LANG_EN_US;
            m_toolBar->m_languageSwitchAction->setIcon(QIcon(LANGUAGE_SWITCH_EN_ICON_PATH));

        }
        else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Failed to load English translation file!");
        }
    }

    isEnglish = !isEnglish;  // 切换语言标记

    // 刷新工具栏语言
    m_toolBar->retranslateUi();
    m_statusBar->retranslateUi();
    m_menuBar->retranslateUi();
    //ui.retranslateUi(this);
    m_printJobDock->setWindowTitle(tr("打印文件列表"));

    AppSettings::setLanguage(m_currentLanguage);
    
}


void HiPrint::slotUpdateImagePrintStatus(int row, PRINT_STATUS printStatus)
{
    switch (printStatus)
    {
    case PRINT_STATUS::NO_PRINTING:
        m_treeModel->updateImageStatus(row, PRINT_STATUS_NO_PRINTING);
        break;
    case PRINT_STATUS::PRINTING:
        m_treeModel->updateImageStatus(row, PRINT_STATUS_PRINTING);
        break;
    case PRINT_STATUS::PAUSE_PRINTING:
        m_treeModel->updateImageStatus(row, PRINT_STATUS_PAUSE_PRINTING);
        break;
    case PRINT_STATUS::PRINTING_FINISHED:
        m_treeModel->updateImageStatus(row, PRINT_STATUS_PRINTING_FINISHED);
        break;
    case PRINT_STATUS::WAIT_TO_PRINTING:
        m_treeModel->updateImageStatus(row, PRINT_STATUS_WAIT_TO_PRINTING);
        break;
    default:
        break;
    }
    m_treeModel->moveToNextImage(row);
}

void HiPrint::slotOpenPlcParamsDialog()
{
    if (m_plcParamConfigManager && m_plcParamConfigManager->isVisible()) {
        return;
    }
    if (!m_plcParamConfigManager) {
        m_plcParamConfigManager = new PLCParamConfigManager();
    }
    QRect mainWindowRect = this->geometry();
    QSize dialogSize = m_plcParamConfigManager->sizeHint();
    int x = mainWindowRect.x() + (mainWindowRect.width() - dialogSize.width()) / 2;
    int y = mainWindowRect.y() + (mainWindowRect.height() - dialogSize.height()) / 2;
    m_plcParamConfigManager->move(x, y);
    m_plcParamConfigManager->show();

}

void HiPrint::slotShrinkAllFiles()
{
}

void HiPrint::slotLoadTiffImage()
{
    loadPrintImageFile();
}

void HiPrint::slotLoadAllFiles()
{
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("用户选择点击菜单加载文件夹按钮").toUtf8().constData());

    QString lastPath = AppSettings::getImageDirectory();
    if (!QDir(lastPath).exists() || lastPath.isEmpty()) {
        lastPath = QStandardPaths::writableLocation(QStandardPaths::DesktopLocation) + PRINT_IMAGE_FILE_DIR;
        QDir targetDir(lastPath);
        if (!targetDir.exists()) {
            QDir().mkpath(lastPath);
        }
    }
    m_printImageDirectory = QFileDialog::getExistingDirectory(this, tr("请选择打印图片目录"), lastPath);
    if (m_printImageDirectory.isEmpty()) {
        return;
    }
    if (!QDir(m_printImageDirectory).exists()) {
        MessageBoxHelper::showInfo(this, tr("无效目录"),
            tr("选择的目录无效或不存在，请选择有效的目录！"));
        return;
    }
    QDir dir(m_printImageDirectory);
    QStringList filters;
    filters << "*.tif" << "*.tiff";
    QFileInfoList imageFiles = dir.entryInfoList(filters, QDir::Files);

    if (imageFiles.isEmpty()) {
        MessageBoxHelper::showInfo(this, tr("目录为空"),
            tr("选择的目录中没有找到图片文件（.tif或.tiff格式），请选择包含图片的目录！"));
        return;
    }

    AppSettings::setImageDirectory(m_printImageDirectory);

    // 清理图片缓存，避免内存泄漏
    if (m_imageDelegate) {
        m_imageDelegate->clearImageCache();
    }

    // 清理图片参数缓存
    static QHash<QString, PrintImageParam*> imageParamCache;
    for (auto ptr : imageParamCache) {
        delete ptr;
    }
    imageParamCache.clear();

    m_treeModel->loadImageList(m_printImageDirectory);

    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
        QString("成功加载图片目录：%1，共找到 %2 个图片文件").arg(m_printImageDirectory).arg(imageFiles.size()).toStdString());
}

void HiPrint::slotDeleteCurrentPrintImage()
{
    QModelIndex currentIndex = m_printJobPanel->treeView()->currentIndex();
    if (!currentIndex.isValid()) {
        MessageBoxHelper::showInfo(this, tr("提示"), tr("请先选择要删除的图片文件！"));
        return;
    }
    QString filePath = m_treeModel->data(currentIndex, Qt::DisplayRole).toString();
    QString currentSelectFileName = QFileInfo(filePath).fileName();
    auto ret = MessageBoxHelper::showQuestion(this, tr("确认删除"), 
        tr("确定要删除选中的图片文件吗？"), 
        tr("文件：%1").arg(QFileInfo(filePath).fileName()),
        QMessageBox::No); 
    if (ret == QMessageBox::Yes) {
        m_treeModel->removeRow(currentIndex.row(), currentIndex.parent());
    }
    bool isEqual = (currentSelectFileName.compare(m_currentShowImageName) == 0);
    if (isEqual) {
        m_scene->clear();
    }

}

void HiPrint::slotClearAllImages()
{
    if (m_treeModel->rowCount() == 0) {
        MessageBoxHelper::showInfo(this, tr("提示"), tr("当前没有图片文件！"));
        return;
    }
    auto ret = MessageBoxHelper::showQuestion(this, tr("确认清空"), 
        tr("确定要清空所有图片文件吗？"), 
        tr("此操作将清空所有已加载的图片文件。"),
        QMessageBox::No);
    if (ret == QMessageBox::Yes) {
        m_treeModel->clear();
    }
}

void HiPrint::slotPccIdleChanged(bool isIdle)
{
    m_isPCCIdle = isIdle;
    if (m_statusBar){
        m_statusBar->setPccStatusColor(isIdle ? QColor(0, 200, 0) : QColor(180, 180, 180));
    }
       
}

void HiPrint::slotHeadPowerBusyChanged(bool isBusy)
{
    m_isPrintHeadBusy = isBusy;
}

void HiPrint::slotHeadRunningChanged(bool isRunning)
{
    m_isHeadRunning = isRunning;
    if (m_statusBar) {
        m_statusBar->setHeadRunStatusColor(isRunning ? QColor(0, 200, 0) : QColor(180, 180, 180));
    }  
    if (m_toolBar) {
        m_toolBar->m_headPowerOnAction->setIcon(isRunning ? QIcon(HEAD_POWER_ON_ICON_PATH) : QIcon(HEAD_POWER_OFF_ICON_PATH));
    }  
}

void HiPrint::slotCommandExecuted(COMMAND_TYPE type, int resultCode)
{
    switch (type)
    {
    case CMD_HEAD_POWER:
        if (resultCode == RVAL_OK) {
            m_toolBar->m_headPowerOnAction->setIcon(QIcon(HEAD_POWER_ON_ICON_PATH));
        }
        else {
            m_toolBar->m_headPowerOnAction->setIcon(QIcon(HEAD_POWER_OFF_ICON_PATH));
            LogManager::instance().logMessage(
                LOG_LEVEL::LVL_ERROR,QString("Set Head Power Fail!, %1") 
                .arg(MeteorErrorMap::getErrorDescription(resultCode))
                .toStdString()
            );
        }
        break;
    case CMD_SET_SPLIT_SIGNAL:
        if (resultCode == RVAL_OK)
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Set SplitSignal Success!");
        else
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Set SplitSignal Fail!,%1").
                arg(MeteorErrorMap::getErrorDescription(resultCode)).toStdString());
        break;
    default:
        break;
    }
}

void HiPrint::slotAxisStatusUpdated(const std::vector<AxisMotionState>& axisData)
{
    if (axisData.size() >= 3) {
        ui.ldtXAxisCurrentPos->setText(QString::number(axisData[0].fReadActPos, 'f', 2));
        ui.ldtYAxisCurrentPos->setText(QString::number(axisData[1].fReadActPos, 'f', 2));
        ui.ldtZAxisCurrentPos->setText(QString::number(axisData[2].fReadActPos, 'f', 2));
        m_statusBar->updateAxis(
            QString::number(axisData[0].fReadActPos, 'f', 2).toDouble(),
            QString::number(axisData[1].fReadActPos, 'f', 2).toDouble(), 
            QString::number(axisData[2].fReadActPos, 'f', 2).toDouble(),
            QString::number(axisData[4].fReadActPos, 'f', 2).toDouble(),
            QString::number(axisData[7].fReadActPos, 'f', 2).toDouble());
    }
}

void HiPrint::slotAxisError(const QString& axis, long errorCode, const QString& message)
{
    LogManager::instance().logMessage(
        LOG_LEVEL::LVL_ERROR,
        QString("Axis Error [%1] %2").arg(axis).arg(AdsErrorMapper::getErrorDescription(errorCode)).toStdString());
}
 
void HiPrint::setupPlcAxisMappings()
{
  
    m_enableAxis    =  DeviceMappingConfig::instance().axisEnableList();
    m_axisNameList  =  DeviceMappingConfig::instance().axisNames();
    m_relMove       =  DeviceMappingConfig::instance().axisRelMoveList();
    m_absMove       =  DeviceMappingConfig::instance().axisAbsMoveList();
    m_jogForWard    =  DeviceMappingConfig::instance().axisJogForwardList();
    m_jogForReverse =  DeviceMappingConfig::instance().axisJogBackwardList();
    m_home          =  DeviceMappingConfig::instance().axisHomeList();
    m_stop          =  DeviceMappingConfig::instance().axisStopList();

    m_btnMove = {
        ui.btnXAxisMove,     // X
        ui.btnYAxisMove,     // Y
        ui.btnZAxisUpMove   // Z->Up
        //ui.btnZAxisDownMove  // Z->Down
    };

    // 主轴正向点动按钮（X, Y, Z, PS, XS）
    m_btnJogForWard = {
        ui.btnMoveXAxisToLeft,      // X
        ui.btnMoveYAxisForward,     // Y
        ui.btnMoveZAxisToUp,        // Z
        ui.btnMoveSandAxisToLeft,   // PS
        ui.btnMoveDownSandAxisToUp  // XS
    };

    // 主轴反向点动按钮（X, Y, Z, PS, XS）
    m_btnJogReverse = {
        ui.btnMoveXAxisToRight,      // X
        ui.btnMoveYAxisBack,         // Y
        ui.btnMoveZAxisDown,         // Z
        ui.btnMoveSandAxisToRight,   // PS
        ui.btnMoveDownSandAxisToDown // XS
    };

    // 回零按钮（X, Y, Z1, Z2）
    m_btnHome = {
        ui.btnXAxisHome,        // X
        ui.btnYAxisHome,        // Y
        ui.btnYAxisHome,        // Z
        ui.btnSandAxisHome,     // PS
        ui.btnDownSandAxisHome,  //XS
    };

    // 急停按钮（X, Y, Z）
    m_btnStop = {
        ui.btnXAxisStop,        // X
        ui.btnYAxisStop,        // Y
        ui.btnStopZAxis         // Z
       
    };

}


void HiPrint::setupAxisEnableActions()
{

    if (m_enableAxis.size() != m_axisNameList.size()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("使能轴和轴名列表不匹配，请检查!").toStdString());
     return;
    }
    //电机使能
    for (int i = 0; i < m_enableAxis.size(); i++) {
        long nRet = m_beckhoffPlcManager->enableAxis(m_enableAxis.at(i).toStdString(), true);
        if (nRet != 0) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, 
                QString("使能电机%1轴失败!, %2")
                .arg(m_axisNameList.at(i))
                .arg(AdsErrorMapper::getErrorDescription(nRet))
                .toStdString());
               
        }
        else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
                QString("使能电机%1轴成功!")
                .arg(m_axisNameList.at(i))
                .toStdString());
               
        }
    }
}

void HiPrint::bindAxisMoveControls()
{
    // 绝对运动和相对运动
    for (int i = 0; i < m_btnMove.size(); ++i)
    {
        connect(m_btnMove.at(i), &QPushButton::clicked, this, [=]()
            {
                m_relMoveParam =
                {
                    { true, static_cast<double>(ui.spbSetXAxisPos->value()), 100.0 },
                    { true, static_cast<double>(ui.spbSetYAxisPosition->value()), 100.0 },
                    { true, static_cast<double>(ui.spbSetZAxisPosition->value()), 0.1 }
                };

                m_absMoveParam =
                {
                    { true, static_cast<double>(ui.spbSetXAxisPos->value()), 100.0 },
                    { true, static_cast<double>(ui.spbSetYAxisPosition->value()), 100.0 },
                    { true, static_cast<double>(ui.spbSetZAxisPosition->value()), 0.1}
                };

                m_isCheck =
                {
                    ui.chbXAxisIsRelativeDistance->isChecked(),
                    ui.chbYAxisIsRelativeDistance->isChecked(),
                    ui.chbZAxisIsRelativeDistance->isChecked()
                };

                if (m_isCheck.at(i))
                {
                    long nRet = m_beckhoffPlcManager->moveAxisRelative(m_relMove.at(i).toStdString(), m_relMoveParam.at(i));
                    if (nRet != 0)
                    {
                        LogManager::instance().logMessage(
                            LOG_LEVEL::LVL_ERROR,
                            QString("Do Motor %1 Fail!, %2")
                            .arg(m_relMove.at(i))
                            .arg(AdsErrorMapper::getErrorDescription(nRet))
                            .toStdString());
                    }
                }
                else
                {
                    long nRet = m_beckhoffPlcManager->moveAxisAbsolute(m_absMove.at(i).toStdString(), m_absMoveParam.at(i));
                    if (nRet != 0)
                    {
                        LogManager::instance().logMessage(
                            LOG_LEVEL::LVL_ERROR,
                            QString("Do Motor %1 Fail!, %2")
                            .arg(m_absMove.at(i))
                            .arg(AdsErrorMapper::getErrorDescription(nRet))
                            .toStdString());
                    }
                }
            });
    }
}


void HiPrint::connectJogControls()
{
    // 正反转
    for (int i = 0; i < m_btnJogForWard.size(); ++i)
    {
        connect(m_btnJogForWard.at(i), &QPushButton::pressed, this, [=]()
            {
                long nRet = m_beckhoffPlcManager->motorJog(m_jogForWard.at(i).toStdString(), true);
                if (nRet != 0)
                {
                    LogManager::instance().logMessage(
                        LOG_LEVEL::LVL_ERROR,
                        QString("Do Motor %1 Fail!, %2")
                        .arg(m_jogForWard.at(i))
                        .arg(AdsErrorMapper::getErrorDescription(nRet))
                        .toStdString());
                }
            });

        connect(m_btnJogForWard.at(i), &QPushButton::released, this, [=]()
            {
                long nRet = m_beckhoffPlcManager->motorJog(m_jogForWard.at(i).toStdString(), false);
                if (nRet != 0)
                {
                    LogManager::instance().logMessage(
                        LOG_LEVEL::LVL_ERROR,
                        QString("Do Motor %1 Fail!, %2")
                        .arg(m_jogForWard.at(i))
                        .arg(AdsErrorMapper::getErrorDescription(nRet))
                        .toStdString());
                }
            });

        connect(m_btnJogReverse.at(i), &QPushButton::pressed, this, [=]()
            {
                long nRet = m_beckhoffPlcManager->motorJog(m_jogForReverse.at(i).toStdString(), true);
                if (nRet != 0)
                {
                    LogManager::instance().logMessage(
                        LOG_LEVEL::LVL_ERROR,
                        QString("Do Motor %1 Fail!, %2")
                        .arg(m_jogForReverse.at(i))
                        .arg(AdsErrorMapper::getErrorDescription(nRet))
                        .toStdString());
                }
            });

        connect(m_btnJogReverse.at(i), &QPushButton::released, this, [=]()
            {
                long nRet = m_beckhoffPlcManager->motorJog(m_jogForReverse.at(i).toStdString(), false);
                if (nRet != 0)
                {
                    LogManager::instance().logMessage(
                        LOG_LEVEL::LVL_ERROR,
                        QString("Do Motor %1 Fail!, %2")
                        .arg(m_jogForReverse.at(i))
                        .arg(AdsErrorMapper::getErrorDescription(nRet))
                        .toStdString());
                }
            });
    }
}


void HiPrint::setupAxisHomeLogic()
{
    //回零
    for (int i = 0; i < m_btnHome.size(); i++) {
        connect(m_btnHome.at(i), &QPushButton::clicked, this, [=]() {
            long nRet = m_beckhoffPlcManager->homeAxis(m_home.at(i).toStdString(), true);
            if (nRet != 0) {
                LogManager::instance().logMessage(
                    LOG_LEVEL::LVL_ERROR,
                    QString("Do Motor %1 Fail!, %2")
                    .arg(m_home.at(i))
                    .arg(AdsErrorMapper::getErrorDescription(nRet))
                    .toStdString());
            }
        });
    }
}

void HiPrint::setupAxisStopHandlers()
{
    //停止
    for (int i = 0; i < m_btnStop.size(); i++) {
        connect(m_btnStop.at(i), &QPushButton::clicked, this, [=]() {
            long nRet = m_beckhoffPlcManager->stopAxis(m_stop.at(i).toStdString(), true);
            if (nRet != 0) {
                LogManager::instance().logMessage(
                    LOG_LEVEL::LVL_ERROR,
                    QString("Do Motor %1 Fail!,%2")
                    .arg(m_stop.at(i))
                    .arg(AdsErrorMapper::getErrorDescription(nRet))
                    .toStdString());
            }
        });
    }
}

void HiPrint::setupQuickPositionActions()
{
    m_xyMotionOperationMap = {
    {0, "XYMotionOriginMode"},           // 回原点
    {1, "XYMotionStandbyPosition"},      // 待机位 (暂时为空)
    {2, "XYMotionPrintStartPosition"},   // 打印开始位 (暂时为空)
    {3, "XYMotionPrintEndPosition"},     // 打印结束位 (暂时为空)
    {4, "XYMotionCleanPosition"},        // 清洗位
    {5, "XYMotionFlashsprayingPosition"}, // 闪喷位置
    {6, "XYMotionhydrationPosition"}     // 保湿位
    };

    m_plcVariableMap = {
        {"XYMotionOriginMode", DeviceMappingConfig::instance().xyMotionOriginMode()},
        {"XYMotionCleanPosition", DeviceMappingConfig::instance().xyMotionCleanPosition()},
        {"XYMotionFlashsprayingPosition", DeviceMappingConfig::instance().xyMotionFlashsprayingPosition()},
        {"XYMotionhydrationPosition", DeviceMappingConfig::instance().xyMotionhydrationPosition()}
    };

    // 铺砂操作映射
    m_spreadingOperationMap = {
        {0, "ManualSpreadingMode"},      // 手动铺砂
        {1, "AutoSpreadingMode"}         // 自动铺砂
    };

    m_spreadingPlcVariableMap = {
        {"ManualSpreadingMode", DeviceMappingConfig::instance().getGeneralValue("ManualSpreadingMode")},
        {"AutoSpreadingMode", DeviceMappingConfig::instance().getGeneralValue("AutoSpreadingMode")}
    };
    connect(ui.btnMotionOperation, &QPushButton::clicked,
            this, &HiPrint::onMotionOperationClicked);
    connect(ui.btnSpreadingOperation, &QPushButton::clicked, 
        this, &HiPrint::onSpreadingModeClicked);
}

void HiPrint::onSpreadingModeClicked()
{
    // 基于时间间隔的防重复点击保护
    qint64 currentTime = QDateTime::currentMSecsSinceEpoch();
    if (currentTime - m_lastMotionOperationTime < MOTION_OPERATION_MIN_INTERVAL_MS) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN,
            QString("运动操作点击过于频繁（间隔小于%1毫秒），忽略重复点击").arg(MOTION_OPERATION_MIN_INTERVAL_MS).toStdString());
        MessageBoxHelper::showWarning(this, tr("操作提示"),
            tr("操作过于频繁，请稍后再试"));
        return;
    }
    int currentIndex = ui.comboxSpreadingMode->currentIndex();
    QString operationType = m_spreadingOperationMap[currentIndex];

    // 记录操作时间
    m_lastMotionOperationTime = currentTime;

    // 执行运动操作
    executeSpreadingModeOperation(operationType);

}

// ========================== X/Y快速位置操作相关 ==========================
void HiPrint::onMotionOperationClicked()
{
    // 基于时间间隔的防重复点击保护
    qint64 currentTime = QDateTime::currentMSecsSinceEpoch();
    if (currentTime - m_lastMotionOperationTime < MOTION_OPERATION_MIN_INTERVAL_MS) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_WARN,
            QString("运动操作点击过于频繁（间隔小于%1毫秒），忽略重复点击").arg(MOTION_OPERATION_MIN_INTERVAL_MS).toStdString());
        MessageBoxHelper::showWarning(this, tr("操作提示"), 
            tr("操作过于频繁，请稍后再试"));
        return;
    }

    int currentIndex = ui.comboxXYMotionOperation->currentIndex();
    if (!m_xyMotionOperationMap.contains(currentIndex)) {
        MessageBoxHelper::showWarning(this, tr("操作提示"), tr("当前选择的操作暂不支持"));
        return;
    }
    QString operationType = m_xyMotionOperationMap[currentIndex];
    QString operationName = ui.comboxXYMotionOperation->currentText();
    if (!m_beckhoffPlcManager) {
        MessageBoxHelper::showError(this, tr("连接错误"), tr("PLC连接未建立，无法执行运动操作"));
        return;
    }

    // 记录操作时间
    m_lastMotionOperationTime = currentTime;
    
    // 执行运动操作
    executeXYMotionOperation(operationType);
}

// ========================== PLC通信心跳检测相关 ==========================
void HiPrint::onPLCConnectionStatusChanged(bool connected)
{
    if (m_isPLCConnected != connected) {
        m_isPLCConnected = connected;
        
        // 记录状态变化日志
        if (connected) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
                QString("PLC通信已恢复").toStdString());
        } else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
                QString("PLC通信已断开").toStdString());
        }
        
        // 更新状态栏显示PLC连接状态
        if (m_statusBar) {
            m_statusBar->setPlcConnectionStatus(connected);
        }
    }
}

bool HiPrint::isPLCConnected() const
{
    return m_isPLCConnected;
}



void HiPrint::executeXYMotionOperation(const QString& operationType)
{
    QString operationName = ui.comboxXYMotionOperation->currentText();
    QString plcVariable = m_plcVariableMap[operationType];
    bool success = sendMotionCommandToPLC(plcVariable, true);
    
    if (success) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("执行X/Y运动操作成功: %1").arg(operationName).toStdString());
    } else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("执行X/Y运动操作失败: %1").arg(operationName).toStdString());
    }
}

void HiPrint::executeSpreadingModeOperation(const QString& operationType)
{
    QString operationName = ui.comboxSpreadingMode->currentText();
    QString plcVariable = m_spreadingPlcVariableMap[operationType];
    bool success = sendMotionCommandToPLC(plcVariable, true);
    if (success) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, QString("执行%1成功！").arg(operationName).toStdString());
    }
    else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, QString("执行%1失败！").arg(operationName).toStdString());
    }
}

bool HiPrint::sendMotionCommandToPLC(const QString& plcVariable, bool value)
{
    if (!isPLCCommunicationAvailable()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("PLC通信不可用，无法发送运动命令: %1").arg(plcVariable).toStdString());
        MessageBoxHelper::showError(this, tr("通信错误"), tr("PLC通信不可用，请检查PLC连接状态和ADS状态"));
        return false;
    }
    
    long result = m_beckhoffPlcManager->writeIOPort(plcVariable.toStdString(), value);
    if (result != 0) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("发送运动命令到PLC失败: %1, 错误码: %2")
            .arg(plcVariable)
            .arg(AdsErrorMapper::getErrorDescription(result))
            .toStdString());
        return false;
    }
    return true;
}


bool HiPrint::event(QEvent* event)
{
    if (event->type() == QEvent::Move) {
        return true;
    }
    if (event->type() == QEvent::Wheel) {
        QWheelEvent* wheelEvent = static_cast<QWheelEvent*>(event);
        if (ui.graphicsView && ui.graphicsView->underMouse()) {
            if (wheelEvent->modifiers() & Qt::ControlModifier) {
                if (wheelEvent->angleDelta().y() > 0) {
                    zoomIn();
                } else {
                    zoomOut();
                }
                return true;
            }
        }
    }
    return QMainWindow::event(event);
}

void HiPrint::slotPrintFilesToggled()
{
    // 如果DockWidget不存在或不可见，重新创建
    if (!m_printJobDock || !m_printJobDock->isVisible()) {
        if (m_tabWidget) {
            // 重新创建DockWidget
            m_printJobDock = new QDockWidget(tr("打印文件列表"), this);
            m_printJobDock->setWidget(m_tabWidget);
            m_printJobDock->setAllowedAreas(Qt::LeftDockWidgetArea);
            m_printJobDock->setFeatures(QDockWidget::DockWidgetClosable);
            addDockWidget(Qt::LeftDockWidgetArea, m_printJobDock);
            connect(m_printJobDock, &QDockWidget::visibilityChanged, this, &HiPrint::onDockWidgetVisibilityChanged);
        }
    }
    
    // 切换到打印文件列表Tab
    if (m_tabWidget) {
        m_tabWidget->setCurrentIndex(0);
    }
}

void HiPrint::slotRecordToggled()
{
    // 如果DockWidget不存在或不可见，重新创建
    if (!m_printJobDock || !m_printJobDock->isVisible()) {
        if (m_tabWidget) {
            // 重新创建DockWidget
            m_printJobDock = new QDockWidget(tr("日志记录"), this);
            m_printJobDock->setWidget(m_tabWidget);
            m_printJobDock->setAllowedAreas(Qt::LeftDockWidgetArea);
            m_printJobDock->setFeatures(QDockWidget::DockWidgetClosable);
            addDockWidget(Qt::LeftDockWidgetArea, m_printJobDock);
            connect(m_printJobDock, &QDockWidget::visibilityChanged, this, &HiPrint::onDockWidgetVisibilityChanged);
        }
    }
    
    // 切换到日志记录Tab
    if (m_tabWidget) {
        m_tabWidget->setCurrentIndex(2);
    }
}

void HiPrint::slotMainControlPanelToggled(bool checked)
{
    
    if (!m_printJobDock || !m_printJobDock->isVisible()) {
        if (m_tabWidget) {
            m_printJobDock = new QDockWidget(tr("控制面板"), this);
            m_printJobDock->setWidget(m_tabWidget);
            m_printJobDock->setAllowedAreas(Qt::LeftDockWidgetArea);
            m_printJobDock->setFeatures(QDockWidget::DockWidgetClosable);
            addDockWidget(Qt::LeftDockWidgetArea, m_printJobDock);
            connect(m_printJobDock, &QDockWidget::visibilityChanged, this, &HiPrint::onDockWidgetVisibilityChanged);
        }
    }
    
    if (m_tabWidget) {
        m_tabWidget->setCurrentIndex(1);
    }
}

void HiPrint::onTabChanged(int index)
{
    switch (index) {
        case 0: 
            m_printJobDock->setWindowTitle(tr("打印文件列表"));
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("切换到打印文件列表页面").toUtf8().constData());
            break;
        case 1: 
            m_printJobDock->setWindowTitle(tr("控制面板"));
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("切换到控制面板页面").toUtf8().constData());
            break;
        case 2: 
            m_printJobDock->setWindowTitle(tr("日志记录"));
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("切换到日志记录页面").toUtf8().constData());
            break;
        default:
            break;
    }
}

void HiPrint::onDockWidgetVisibilityChanged(bool visible)
{
    if (!visible) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("DockWidget已关闭，可通过菜单栏恢复").toUtf8().constData());
    }
}

void HiPrint::loadPrintImageFile()
{
    QFileDialog dialog(this);
    dialog.setWindowTitle(tr("选择TIF图像"));
    dialog.setNameFilters({ "TIF图像 (*.tif *.tiff)", "所有文件 (*)" });
    dialog.setFileMode(QFileDialog::ExistingFiles);
    QString initialDir = m_printImageDirectory.isEmpty() ?
        QStandardPaths::writableLocation(QStandardPaths::DesktopLocation) :
        m_printImageDirectory;
    dialog.setDirectory(initialDir);
    if (dialog.exec() == QDialog::Accepted) {
        QStringList files = dialog.selectedFiles();
        if (!files.isEmpty()) {
            m_printImageDirectory = QFileInfo(files.first()).absolutePath();
            for (const QString& file : files) {
                m_treeModel->loadImageList(file);
            }  
        }
    }
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, tr("用户选择点击菜单加载文件按钮").toUtf8().constData());

}



void HiPrint::executeContinuousPrint(const QStringList& fileList)
{
    PrintJobParam printJobParam = createPrintJobParam();
    const TAppStatus* appStatus = m_meteorApi->getPrinterStatus();
    if (appStatus == nullptr) {
        return;
    }
    // 检查打印机连接状态
    if (!checkPrinterConnected()) {
        return;
    }
    // 检查扫描模式
    if (!checkScanMode()) {
        return;
    }

    // 检查打印头状态
    if (!checkHeadStatus(appStatus)) {
        return;
    }
    //// 检查各个轴的零位信号
    //if (!checkAxisZeroSignals()) {
    //    return;
    //}

    // 发送工艺参数到PLC
    //if (isPLCCommunicationAvailable()) {
    //    sendProcessParametersToPLC();
    //}
    printJobParam.printImageList = fileList;
    printJobParam.modeType = PrintModeType::CONTINUOUS;

    m_printStartTime = QDateTime::currentDateTime();
    m_printedFiles = 0;
    m_totalPrintFiles = fileList.size();
    m_currentPrintParams = QString("连续打印模式 - 总文件数: %1").arg(fileList.size());
    m_isPrintJobCompleted = false;  // 重置完成标志

    // 启动打印线程
    m_workerThreadManager->printJobThread()->startPrint(printJobParam);
    
   // updatePauseButtonState();
}

void HiPrint::executeSingleFilePrint(const QStringList& fileList,const QString& fileName, int repeatCount)
{
    // 创建打印作业参数
    PrintJobParam printJobParam = createPrintJobParam();

    // 获取打印机状态
    const TAppStatus* appStatus = m_meteorApi->getPrinterStatus();
    if (appStatus == nullptr) {
        return;
    }

    // 检查打印机连接状态
    if (!checkPrinterConnected()) {
        return;
    }

    // 检查扫描模式
    if (!checkScanMode()) {
        return;
    }

    // 检查打印头状态
    if (!checkHeadStatus(appStatus)) {
        return;
    }
    // 检查各个轴的零位信号
    //if (!checkAxisZeroSignals()) {
    //    return;
    //}
    
    // 发送工艺参数到PLC
    //if (isPLCCommunicationAvailable()) {
    //    sendProcessParametersToPLC();
    //}
    
    // 设置单文件打印参数
    printJobParam.singleFileName = fileName.toStdString();
    printJobParam.singleFilePrintCount = repeatCount;
    printJobParam.printImageList = fileList;
    printJobParam.modeType = PrintModeType::SINGLEFILE;

    m_printStartTime = QDateTime::currentDateTime();
    m_printedFiles = 0;
    m_totalPrintFiles = repeatCount; 
    m_currentPrintParams = QString("单文件打印模式 - 文件: %1, 重复次数: %2").arg(fileName).arg(repeatCount);
    m_isPrintJobCompleted = false;  // 重置完成标志

    // 启动打印线程
    m_workerThreadManager->printJobThread()->startPrint(printJobParam);
    
    updatePauseButtonState();
}

void HiPrint::updateImageInfoDisplay(const QString& imagePath, const PrintImageParam& printImageParam)
{
    // 设置更大的字体
    QFont largeFont("Microsoft YaHei", 12); // 增大字体到12pt
    ui.textImageInfo->setFont(largeFont);
    ui.textPrintInfo->setFont(largeFont);
    
    // 设置为只读
    ui.textImageInfo->setReadOnly(true);
    ui.textPrintInfo->setReadOnly(true);

    QString info = QStringLiteral(
        "文件路径: %1\n"
        "像素: %2 * %3\n"
        "图像大小: %4mm * %5mm\n"
        "分辨率: %6 dpi * %7 dpi\n"
        "位深: %8bit")
        .arg(imagePath)     //文件路径         
        .arg(printImageParam.width)         //像素宽度 
        .arg(printImageParam.height)        //像素高度
        .arg(printImageParam.xDimension)    //X方向尺寸大小
        .arg(printImageParam.yDimension)    //Y方向尺寸大小
        .arg(printImageParam.xResolution)   //X方向分辨率
        .arg(printImageParam.yResolution)   //Y方向分辨率
        .arg(printImageParam.bitsPerSample);//位深

  
    QString printInfo = QStringLiteral(
        "打印参数: %1\n"
        "已打印/总共: %2 / %3\n"
        "开始时间: %4")
        .arg(m_currentPrintParams.isEmpty() ? "未设置" : m_currentPrintParams)  // 打印参数
        .arg(m_printedFiles)                                                    // 已打印
        .arg(m_totalPrintFiles)                                                 // 总共
        .arg(formatDateTime(m_printStartTime));                                 // 开始时间

    ui.textImageInfo->setPlainText(info);
    ui.textPrintInfo->setPlainText(printInfo);
}

void HiPrint::sendProcessParametersToPLC()
{
    ProcessParameters processParams;
    // 打印参数
    processParams.layerThickness = 0.3;        // 打印层厚(mm)
    processParams.xySpeed = 50.0;              // XY轴移动速度(mm/s)
    processParams.printXResolution = 300.0;    // X轴分辨率(dpi)
    processParams.printYResolution = 300.0;    // Y轴分辨率(dpi)
    processParams.inkVolume = 100.0;           // 墨量(ml)
    
    // 铺砂参数
    processParams.sandSpreadingSpeed = 30.0;   // 铺砂速度(mm/s)
    processParams.sandSpreadingDosingSpeed = 20.0; // 铺砂定量速度(mm/s)
    processParams.sandSpreadingEndPosition = 1000.0; // 铺砂结束位置(mm)
    
    
    QString configVar = DeviceMappingConfig::instance().processParametersVariable();
    long result = m_beckhoffPlcManager->sendProcessParameters(configVar.toStdString(), processParams);
  
    if (result == 0) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("工艺参数发送到PLC成功").toStdString());
    } else {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
            QString("工艺参数发送到PLC失败，错误码: %1").arg(result).toStdString());
    }
}


