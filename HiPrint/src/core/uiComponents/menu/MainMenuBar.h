#ifndef MAINMENUBAR_H
#define MAINMENUBAR_H

#include <QMenuBar>
#include <QMenu>
#include <QAction>
#include "../../../common/global/ResourceManager.h"

class MainMenuBar : public QMenuBar
{
    Q_OBJECT
public:
    explicit MainMenuBar(QWidget* parent = nullptr);

    // 文件菜单
    QMenu* m_fileMenu;
    QAction* m_openFileAction;
    QAction* m_openFolderAction;
    QAction* m_exitAction;

    // 视图菜单
    QMenu* m_viewMenu;
    QAction* m_workDirAction;
    QAction* m_printFilesAction;
    QAction* m_recordAction;
    QAction* m_mainControlPanelAction;

    // 设置菜单
    QMenu* m_settingsMenu;
    QAction* m_printSettingAction;
    QAction* m_plcSettingAction;

    // 工具菜单
    QMenu* m_toolsMenu;
    QAction* m_meteorMonitorAction;

    // 帮助菜单
    QMenu* m_helpMenu;
    QAction* m_aboutAction;


signals:
    // 文件
    void sigOpenFile();
    void sigOpenFolder();
    void sigExit();

    // 视图
    void sigWorkDirToggled(bool checked);
    void sigPrintFiles(bool checked);
    void sigRecord(bool checked);
    void sigMainControlPanelToggled(bool checked);

    // 设置
    void sigPrintSetting();
    void sigPLCSetting();

    // 工具
    void sigMeteorMonitor();

    // 帮助
    void sigAbout();

public:
    void retranslateUi();
};

#endif // MAINMENUBAR_H

