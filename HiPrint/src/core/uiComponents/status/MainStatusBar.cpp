#include "MainStatusBar.h"

MainStatusBar::MainStatusBar(QWidget* parent)
    : QStatusBar(parent)
{
    m_labelX = new QLabel(this);
    m_labelY = new QLabel(this);
    m_labelZ = new QLabel(this);
    m_labelPS = new QLabel(this);
    m_labelXS = new QLabel(this);
    m_labelPlcStatus = new QLabel(this);

    updateAxis(0, 0, 0, 0, 0);

    m_labelPccStatus = new StatusIndicatorLabel(tr("主板"), this);
    m_labelHeadRunStatus = new StatusIndicatorLabel(tr("头板"), this);
    m_labelPccStatus->setStateColor(QColor(180, 180, 180));
    m_labelHeadRunStatus->setStateColor(QColor(180, 180, 180));
    
    addPermanentWidget(m_labelX);
    addPermanentWidget(m_labelY);
    addPermanentWidget(m_labelZ);
    addPermanentWidget(m_labelPS);
    addPermanentWidget(m_labelXS);
    addPermanentWidget(m_labelPlcStatus);
    addPermanentWidget(m_labelPccStatus);
    addPermanentWidget(m_labelHeadRunStatus);
   
    m_labelX->setFont(HEADING_FONT);
    m_labelY->setFont(HEADING_FONT);
    m_labelZ->setFont(HEADING_FONT);
    m_labelPS->setFont(HEADING_FONT);
    m_labelXS->setFont(HEADING_FONT);
    m_labelPccStatus->setFont(HEADING_FONT);
    m_labelHeadRunStatus->setFont(HEADING_FONT);
    m_labelPlcStatus->setFont(HEADING_FONT);
    m_labelPlcStatus->setText("PLC: 未连接");
    m_labelPlcStatus->setObjectName("plcStatusLabel");
}

void MainStatusBar::retranslateUi()
{
    //m_labelPccStatus->setText(tr("主板"));
    //m_labelHeadRunStatus->setText(tr("头板"));
}

void MainStatusBar::updateAxis(double x, double y, double z, double ps, double xs)
{
    m_labelX->setText(QString("X:%1").arg(x,  0, 'f', 2));
    m_labelY->setText(QString("Y:%1").arg(y,  0, 'f', 2));
    m_labelZ->setText(QString("Z:%1").arg(z,  0, 'f', 2));
    m_labelPS->setText(QString("PS:%1").arg(ps, 0, 'f', 2));
    m_labelXS->setText(QString("XS:%1").arg(xs, 0, 'f', 2));
}

void MainStatusBar::setPccStatusColor(const QColor& color)
{
    m_labelPccStatus->setStateColor(color);
}
void MainStatusBar::setHeadRunStatusColor(const QColor& color)
{
    m_labelHeadRunStatus->setStateColor(color);
}

void MainStatusBar::setPlcConnectionStatus(bool connected)
{
    if (connected) {
        m_labelPlcStatus->setText("PLC: 已连接"); 
    } else {
        m_labelPlcStatus->setText("PLC: 未连接");   
    }
}
