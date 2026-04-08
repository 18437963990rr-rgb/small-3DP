#ifndef MAINTOOLBAR_H
#define MAINTOOLBAR_H

#include <QToolBar>
#include <QAction>
#include <QComboBox>
#include "../../../common/global/ResourceManager.h"

class MainToolBar : public QToolBar
{
    Q_OBJECT
public:
    explicit MainToolBar(QWidget* parent = nullptr);

    QAction* m_openFileAction;
    QAction* m_openFolderAction;
    QAction* m_setPrintParamsAction;
    QAction* m_controlPLCAction;
    QAction* m_startPrintAction;
    QAction* m_pausePrintAction;
    QAction* m_abortPrintAction;
    QAction* m_controlNozzleAction;
    QAction* m_headPowerOnAction;
    QAction* m_monitorEngineAction;
    QAction* m_userAdminAction;
    QAction* m_lockAppWindowAction;
    QAction* m_exitAppAction;
    QAction* m_languageSwitchAction;
    QAction* m_zoomInAction;
    QAction* m_zoomOutAction;
    QAction* m_resetZoomAction;

    QComboBox* m_printModeComboBox;

    void retranslateUi();

signals:
    void sigOpenFile();
    void sigOpenFolder();
    void sigSetPrintParams();
    void sigControlPLC();
    void sigStartPrint();
    void sigPausePrint();
    void sigAbortPrint();
    void sigControlNozzle();
    void sigHeadPowerOn();
    void sigMonitorEngine();
    void sigUserAdmin();
    void sigLockAppWindow();
    void sigExitApp();
    void sigLanguageSwitch();
    void sigZoomIn();
    void sigZoomOut();
    void sigResetZoom();
    void sigPrintModeChanged(int modeIndex);
};

#endif // MAINTOOLBAR_H
