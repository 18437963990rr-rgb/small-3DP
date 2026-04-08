#include "MainToolBar.h"

MainToolBar::MainToolBar(QWidget* parent) : QToolBar(tr("主工具栏"), parent)
{
    setMovable(false);                 
    setFloatable(false);               
    setAllowedAreas(Qt::NoToolBarArea);
    setToolButtonStyle(Qt::ToolButtonTextUnderIcon);
 
    // 1. 打开文件
    m_openFileAction = addAction(QIcon(OPEN_FILE_ICON_PATH), tr("打开文件"));
    m_openFileAction->setToolTip(tr("打开文件 (Ctrl+O)"));
    m_openFileAction->setShortcut(QKeySequence("Ctrl+O"));
    connect(m_openFileAction, &QAction::triggered, this, &MainToolBar::sigOpenFile);

    // 2. 打开文件夹
    m_openFolderAction = addAction(QIcon(OPEN_FOLDER_ICON_PATH), tr("打开文件夹"));
    m_openFolderAction->setToolTip(tr("打开文件夹 (Ctrl+Shift+O)"));
    m_openFolderAction->setShortcut(QKeySequence("Ctrl+Shift+O"));
    connect(m_openFolderAction, &QAction::triggered, this, &MainToolBar::sigOpenFolder);

    // 3. 打印参数
    m_setPrintParamsAction = addAction(QIcon(METEOR_SETTING_ICON_PATH), tr("打印参数"));
    m_setPrintParamsAction->setToolTip(tr("打印参数 (Ctrl+P)"));
    m_setPrintParamsAction->setShortcut(QKeySequence("Ctrl+P"));
    connect(m_setPrintParamsAction, &QAction::triggered, this, &MainToolBar::sigSetPrintParams);

    // 4. PLC参数
    m_controlPLCAction = addAction(QIcon(PLC_ICON_PATH), tr("PLC参数"));
    m_controlPLCAction->setToolTip(tr("PLC参数 (Ctrl+L)"));
    m_controlPLCAction->setShortcut(QKeySequence("Ctrl+L"));
    connect(m_controlPLCAction, &QAction::triggered, this, &MainToolBar::sigControlPLC);

  
    addSeparator();

    // 5. 开始打印
    m_startPrintAction = addAction(QIcon(START_PRINT_ICON_PATH), tr("开始打印"));
    m_startPrintAction->setToolTip(tr("开始打印 (Ctrl+S)"));
    m_startPrintAction->setShortcut(QKeySequence("Ctrl+S"));
    connect(m_startPrintAction, &QAction::triggered, this, &MainToolBar::sigStartPrint);

    // 6. 暂停
    m_pausePrintAction = addAction(QIcon(PAUSE_PRINT_ICON_PATH), tr("暂停"));
    m_pausePrintAction->setToolTip(tr("暂停 (Ctrl+U)"));
    m_pausePrintAction->setShortcut(QKeySequence("Ctrl+U"));
    connect(m_pausePrintAction, &QAction::triggered, this, &MainToolBar::sigPausePrint);

    // 7. 停止
    m_abortPrintAction = addAction(QIcon(ABORT_PRINT_ICON_PATH), tr("停止"));
    m_abortPrintAction->setToolTip(tr("停止 (Ctrl+T)"));
    m_abortPrintAction->setShortcut(QKeySequence("Ctrl+T"));
    connect(m_abortPrintAction, &QAction::triggered, this, &MainToolBar::sigAbortPrint);

    // 8. 喷头控制
    m_controlNozzleAction = addAction(QIcon(NOZZLE_CONTROL_ICON_PATH), tr("喷头控制"));
    m_controlNozzleAction->setToolTip(tr("喷头控制 (Ctrl+N)"));
    m_controlNozzleAction->setShortcut(QKeySequence("Ctrl+N"));
    connect(m_controlNozzleAction, &QAction::triggered, this, &MainToolBar::sigControlNozzle);

    // 9. 喷头上电
    m_headPowerOnAction = addAction(QIcon(HEAD_POWER_OFF_ICON_PATH), tr("喷头上电"));
    m_headPowerOnAction->setToolTip(tr("喷头上电 (Ctrl+H)"));
    m_headPowerOnAction->setShortcut(QKeySequence("Ctrl+H"));
    connect(m_headPowerOnAction, &QAction::triggered, this, &MainToolBar::sigHeadPowerOn);

    // 10. 引擎监控
    m_monitorEngineAction = addAction(QIcon(MONITOR_ICON_PATH), tr("引擎监控"));
    m_monitorEngineAction->setToolTip(tr("引擎监控 (Ctrl+M)"));
    m_monitorEngineAction->setShortcut(QKeySequence("Ctrl+M"));
    connect(m_monitorEngineAction, &QAction::triggered, this, &MainToolBar::sigMonitorEngine);

    addSeparator();

    // 11. 用户管理
    m_userAdminAction = addAction(QIcon(USER_ADMIN_ICON_PATH), tr("用户管理"));
    m_userAdminAction->setToolTip(tr("用户管理 (Ctrl+A)"));
    m_userAdminAction->setShortcut(QKeySequence("Ctrl+A"));
    connect(m_userAdminAction, &QAction::triggered, this, &MainToolBar::sigUserAdmin);

    // 12. 缩放功能
    m_zoomInAction = addAction(QIcon(":/res/ImageZoomIn.png"), tr("放大"));
    m_zoomInAction->setToolTip(tr("放大图片 (Ctrl+滚轮向上)"));
    m_zoomInAction->setShortcut(QKeySequence("Ctrl+="));
    connect(m_zoomInAction, &QAction::triggered, this, &MainToolBar::sigZoomIn);

    m_zoomOutAction = addAction(QIcon(":/res/ImageZoomOut.png"), tr("缩小"));
    m_zoomOutAction->setToolTip(tr("缩小图片 (Ctrl+滚轮向下)"));
    m_zoomOutAction->setShortcut(QKeySequence("Ctrl+-"));
    connect(m_zoomOutAction, &QAction::triggered, this, &MainToolBar::sigZoomOut);

    m_resetZoomAction = addAction(QIcon(":/res/ImageZoomCenter.png"), tr("重置缩放"));
    m_resetZoomAction->setToolTip(tr("重置图片缩放"));
    m_resetZoomAction->setShortcut(QKeySequence("Ctrl+0"));
    connect(m_resetZoomAction, &QAction::triggered, this, &MainToolBar::sigResetZoom);

  
    // 13. 中英文切换
    m_languageSwitchAction = addAction(QIcon(LAMGUAGE_SWITCH_CH_ICON_PATH), tr("中英文切换"));
    m_languageSwitchAction->setToolTip(tr("中英文切换 (Ctrl+E)"));
    m_languageSwitchAction->setShortcut(QKeySequence("Ctrl+E"));
    connect(m_languageSwitchAction, &QAction::triggered, this, &MainToolBar::sigLanguageSwitch);

    // 14. 退出
    m_exitAppAction = addAction(QIcon(BACK_ICON_PATH), tr("退出"));
    m_exitAppAction->setToolTip(tr("退出 (Ctrl+Q)"));
    m_exitAppAction->setShortcut(QKeySequence("Ctrl+Q"));
    connect(m_exitAppAction, &QAction::triggered, this, &MainToolBar::sigExitApp);

}
void MainToolBar::retranslateUi()
{
    this->setWindowTitle(tr("主工具栏"));

    m_openFileAction->setText(tr("打开文件"));
    m_openFileAction->setToolTip(tr("打开文件 (Ctrl+O)"));

    m_openFolderAction->setText(tr("打开文件夹"));
    m_openFolderAction->setToolTip(tr("打开文件夹 (Ctrl+Shift+O)"));

    m_setPrintParamsAction->setText(tr("打印参数"));
    m_setPrintParamsAction->setToolTip(tr("打印参数 (Ctrl+P)"));

    m_controlPLCAction->setText(tr("PLC参数"));
    m_controlPLCAction->setToolTip(tr("PLC参数 (Ctrl+L)"));

   
    m_startPrintAction->setText(tr("开始打印"));
    m_startPrintAction->setToolTip(tr("开始打印 (Ctrl+S)"));

    m_pausePrintAction->setText(tr("暂停"));
    m_pausePrintAction->setToolTip(tr("暂停 (Ctrl+U)"));

    m_abortPrintAction->setText(tr("停止"));
    m_abortPrintAction->setToolTip(tr("停止 (Ctrl+T)"));

    m_controlNozzleAction->setText(tr("喷头控制"));
    m_controlNozzleAction->setToolTip(tr("喷头控制 (Ctrl+N)"));

    m_headPowerOnAction->setText(tr("喷头上电"));
    m_headPowerOnAction->setToolTip(tr("喷头上电 (Ctrl+H)"));

    m_monitorEngineAction->setText(tr("引擎监控"));
    m_monitorEngineAction->setToolTip(tr("引擎监控 (Ctrl+M)"));

    m_userAdminAction->setText(tr("用户管理"));
    m_userAdminAction->setToolTip(tr("用户管理 (Ctrl+A)"));


    m_exitAppAction->setText(tr("退出"));
    m_exitAppAction->setToolTip(tr("退出 (Ctrl+Q)"));

    m_languageSwitchAction->setText(tr("中英文切换"));
    m_languageSwitchAction->setToolTip(tr("中英文切换 (Ctrl+E)"));

    m_zoomInAction->setText(tr("放大"));
    m_zoomInAction->setToolTip(tr("放大图片 (Ctrl+滚轮向上)"));

    m_zoomOutAction->setText(tr("缩小"));
    m_zoomOutAction->setToolTip(tr("缩小图片 (Ctrl+滚轮向下)"));

    m_resetZoomAction->setText(tr("重置缩放"));
    m_resetZoomAction->setToolTip(tr("重置图片缩放"));
    
}
