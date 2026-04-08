#include "MainMenuBar.h"
#include <QIcon>

MainMenuBar::MainMenuBar(QWidget* parent)
    : QMenuBar(parent)
{
    // ==== 文件菜单 ====
    m_fileMenu = addMenu(tr("文件(&F)"));
    m_openFileAction = m_fileMenu->addAction(QIcon(OPEN_FILE_ICON_PATH), tr("打开文件(&O)"));

    m_openFileAction->setShortcut(QKeySequence("Ctrl+O"));
    m_openFileAction->setToolTip(tr("打开文件 (Ctrl+O)"));
    connect(m_openFileAction, &QAction::triggered, this, &MainMenuBar::sigOpenFile);

    m_openFolderAction = m_fileMenu->addAction(QIcon(OPEN_FOLDER_ICON_PATH), tr("打开目录(&D)"));
    m_openFolderAction->setShortcut(QKeySequence("Ctrl+Shift+O"));
    m_openFolderAction->setToolTip(tr("打开目录 (Ctrl+Shift+O)"));
    connect(m_openFolderAction, &QAction::triggered, this, &MainMenuBar::sigOpenFolder);

    m_fileMenu->addSeparator();

    m_exitAction = m_fileMenu->addAction(QIcon(BACK_ICON_PATH), tr("退出(&Q)"));
    m_exitAction->setShortcut(QKeySequence("Ctrl+Q"));
    m_exitAction->setToolTip(tr("退出 (Ctrl+Q)"));
    connect(m_exitAction, &QAction::triggered, this, &MainMenuBar::sigExit);

    // ==== 视图菜单 ====
    m_viewMenu = addMenu(tr("视图(&V)"));

    m_workDirAction = m_viewMenu->addAction(QIcon(WORK_ICON_PATH),tr("工作目录(&W)"));
    m_workDirAction->setCheckable(true);
    m_workDirAction->setChecked(true);
    connect(m_workDirAction, &QAction::toggled, this, &MainMenuBar::sigWorkDirToggled);

    m_printFilesAction = m_viewMenu->addAction(QIcon(PRINT_FILE_LIST),tr("打印文件列表(&L)"));
    m_printFilesAction->setCheckable(true);
    m_printFilesAction->setChecked(true);
    connect(m_printFilesAction, &QAction::toggled, this, &MainMenuBar::sigPrintFiles);

    m_recordAction = m_viewMenu->addAction(QIcon(LOG_RECORD),tr("日志记录(&R)"));
    m_recordAction->setCheckable(true);
    m_recordAction->setChecked(true);
    connect(m_recordAction, &QAction::toggled, this, &MainMenuBar::sigRecord);

    m_mainControlPanelAction = m_viewMenu->addAction(QIcon(MOTION_CONTROL),tr("主控制栏(&M)"));
    m_mainControlPanelAction->setCheckable(true);
    m_mainControlPanelAction->setChecked(true);
    connect(m_mainControlPanelAction, &QAction::toggled, this, &MainMenuBar::sigMainControlPanelToggled);

    // ==== 设置菜单 ====
    m_settingsMenu = addMenu(tr("设置(&S)"));

    m_printSettingAction = m_settingsMenu->addAction(QIcon(METEOR_SETTING_ICON_PATH), tr("打印设置(&P)"));
    connect(m_printSettingAction, &QAction::triggered, this, &MainMenuBar::sigPrintSetting);

    m_plcSettingAction = m_settingsMenu->addAction(QIcon(PLC_ICON_PATH), tr("PLC设置(&L)"));
    connect(m_plcSettingAction, &QAction::triggered, this, &MainMenuBar::sigPLCSetting);

    // ==== 工具菜单 ====
    m_toolsMenu = addMenu(tr("工具(&T)"));

    m_meteorMonitorAction = m_toolsMenu->addAction(QIcon(MONITOR_ICON_PATH), tr("Meteor引擎监控(&M)"));
    connect(m_meteorMonitorAction, &QAction::triggered, this, &MainMenuBar::sigMeteorMonitor);
}

void MainMenuBar::retranslateUi() {
    // 文件菜单
    m_fileMenu->setTitle(tr("文件(&F)"));
    m_openFileAction->setText(tr("打开文件(&O)"));
    m_openFileAction->setToolTip(tr("打开文件 (Ctrl+O)"));
    m_openFileAction->setShortcut(QKeySequence("Ctrl+O"));
    m_openFolderAction->setText(tr("打开目录(&D)"));
    m_openFolderAction->setToolTip(tr("打开目录 (Ctrl+Shift+O)"));
    m_openFolderAction->setShortcut(QKeySequence("Ctrl+Shift+O"));
    m_exitAction->setText(tr("退出(&Q)"));
    m_exitAction->setToolTip(tr("退出 (Ctrl+Q)"));
    m_exitAction->setShortcut(QKeySequence("Ctrl+Q"));

    // 视图菜单
    m_viewMenu->setTitle(tr("视图(&V)"));
    m_workDirAction->setText(tr("工作目录(&W)"));
    m_workDirAction->setCheckable(true);
    m_printFilesAction->setText(tr("打印文件列表(&L)"));
    m_printFilesAction->setCheckable(true);
    m_recordAction->setText(tr("日志记录(&R)"));
    m_recordAction->setCheckable(true);
    m_mainControlPanelAction->setText(tr("主控制栏(&M)"));
    m_mainControlPanelAction->setCheckable(true);

    // 设置菜单
    m_settingsMenu->setTitle(tr("设置(&S)"));
    m_printSettingAction->setText(tr("打印设置(&P)"));
    m_plcSettingAction->setText(tr("PLC设置(&L)"));

    // 工具菜单
    m_toolsMenu->setTitle(tr("工具(&T)"));
    m_meteorMonitorAction->setText(tr("Meteor引擎监控(&M)"));
}