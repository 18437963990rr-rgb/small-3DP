#include "PrintConfigManager.h"

PrintConfigManager::PrintConfigManager(QWidget *parent)
	: QWidget(parent)
{
	ui.setupUi(this);
    setWindowTitle(tr("打印设置"));
    setWindowIcon(QIcon(APPLICATION_ICON_PATH));
    setWindowFlags(windowFlags() | Qt::WindowCloseButtonHint | Qt::WindowTitleHint);
    setFixedSize(QSize(800, 600));
	initTabs();

}

PrintConfigManager::~PrintConfigManager()
{
    if (m_flashSpraySettings) {
        delete m_flashSpraySettings;
        m_flashSpraySettings = nullptr;
    }
    if (m_printSettings) {
        delete m_printSettings;
        m_printSettings = nullptr;
    }
    if (m_imageSettings) {
        delete m_imageSettings;
        m_imageSettings = nullptr;
    }
    if (m_nozzleSettings) {
        delete m_nozzleSettings;
        m_nozzleSettings = nullptr;
    }
    if (m_factorySettings) {
        delete m_factorySettings;
        m_factorySettings = nullptr;
    }
    if (m_printParamSettings) {
        delete m_printParamSettings;
        m_printParamSettings = nullptr;
    }
}

void PrintConfigManager::initTabs()
{
    m_flashSpraySettings = new FlashSpraySettings(this);
    m_printSettings = new PrintSettings(this);
    m_imageSettings = new ImageSettings(this);
    m_nozzleSettings = new NozzleSettings(this);
    m_factorySettings = new FactorySettings(this);
    m_printParamSettings = new PrintParamSettings(this);

    ui.tabWidget->addTab(m_flashSpraySettings, tr("闪喷设置"));
    ui.tabWidget->addTab(m_printSettings, tr("打印设置"));
    ui.tabWidget->addTab(m_imageSettings, tr("图像设置"));
    ui.tabWidget->addTab(m_nozzleSettings, tr("喷头设置"));
    ui.tabWidget->addTab(m_factorySettings, tr("工厂设置"));
    ui.tabWidget->addTab(m_printParamSettings, tr("打印参数设置"));
}

