#ifndef MAINSTATUSBAR_H
#define MAINSTATUSBAR_H

#include <QStatusBar>
#include <QLabel>
#include "../../../common/utils/StatusIndicatorLabel.h" 
#include "../../../common/global/ResourceManager.h"

class MainStatusBar : public QStatusBar
{
    Q_OBJECT
public:
    explicit MainStatusBar(QWidget* parent = nullptr);

    // 轴坐标显示
    QLabel* m_labelX;
    QLabel* m_labelY;
    QLabel* m_labelZ;
    QLabel* m_labelPS;
    QLabel* m_labelXS;

    // PLC连接状态标签
    QLabel* m_labelPlcStatus;

    // 状态指示灯
    StatusIndicatorLabel* m_labelPccStatus;
    StatusIndicatorLabel* m_labelHeadRunStatus;
 
    void retranslateUi();

    // 轴刷新接口
    void updateAxis(double x, double y, double z, double ps,double xs);

    // 状态灯刷新接口
    void setPccStatusColor(const QColor& color);
    void setHeadRunStatusColor(const QColor& color);
    
    // PLC状态管理接口
    void setPlcConnectionStatus(bool connected);

    // 可扩展更多状态
};

#endif // MAINSTATUSBAR_H

