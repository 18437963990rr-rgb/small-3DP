QT += core gui widgets

TARGET = HiPrintProject
TEMPLATE = app

SOURCES += \
    src/core/uiComponents/MainMenuBar.cpp \
    src/core/uiComponents/MainToolBar.cpp \
	src/core/uiComponents/PrintConfigManager.cpp\
	src/core/uiComponents/MainStatusBar.cpp \
	src/core/uiComponents/PrintParamSettings.cpp \
	src/core/uiComponents/ProcessParamDialog.cpp \
	src/app/main.cpp\
	src/app/HiPrint.cpp\
	src/common/auth/LoginController.cpp \
	src/common/global/ProcessParamManager.cpp
	
	
HEADERS += \
    src/core/uiComponents/MainMenuBar.h \
    src/core/uiComponents/MainToolBar.h \
    src/core/uiComponents/PrintParamSettings.h \
    src/core/uiComponents/ProcessParamDialog.h \
    src/common/global/ProcessParamManager.h \
    src/common/db/ProcessParamTables.h

	
FORMS += \
    src/ui/AuxiliaryMachinePage.ui \
    src/ui/FactorySettings.ui \
    src/ui/FlashSpraySettings.ui \
    src/ui/HiPrint.ui \
    src/ui/ImageSettings.ui \
    src/ui/inkPathSettings.ui \
    src/ui/IoMonitorPage.ui \
    src/ui/LoginController.ui \
    src/ui/MasterDeviceSettings.ui \
    src/ui/NozzleSettings.ui \
    src/ui/OtherParamsPage.ui \
    src/ui/PlcFeatureSwitchPage.ui \
    src/ui/PlcManualControlPage.ui \
    src/ui/PlcParamSettings.ui \
    src/ui/PLCSettingsManager.ui \
    src/ui/PrintConfigManager.ui \
    src/ui/PrintParamSettings.ui \
    src/ui/PrintSettings.ui \
    src/ui/SlaveDeviceSettings.ui \
    src/ui/UserAccountManager.ui 
	
TRANSLATIONS += \
    translations/HiPrintProject_en.ts \
    translations/HiPrintProject_zh.ts  # 添加中文翻译文件


