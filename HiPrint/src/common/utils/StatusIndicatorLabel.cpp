#include "StatusIndicatorLabel.h"
#include <QPainter>
#include <QFontMetrics>
#include <QPaintEvent>

StatusIndicatorLabel::StatusIndicatorLabel(const QString& text, QWidget* parent)
    : QLabel(parent), m_text(text), m_color(Qt::gray)
{
    setMinimumWidth(60);
    setMaximumHeight(24);
}

void StatusIndicatorLabel::setStateColor(const QColor& color)
{
    m_color = color;
    update();
}

void StatusIndicatorLabel::paintEvent(QPaintEvent* event)
{
    QLabel::paintEvent(event);
    QPainter painter(this);
    painter.setRenderHint(QPainter::Antialiasing);

    QFontMetrics fm(font());
    int textWidth = fm.horizontalAdvance(m_text);
    int circleDiameter = qMin(height() - 4, 16);
    int circleX = textWidth + 8;
    int circleY = (height() - circleDiameter) / 2;

    // 文字
    painter.setPen(Qt::black);
    painter.drawText(0, height() / 2 + fm.ascent() / 2 - 2, m_text);

    // 圆点
    painter.setBrush(m_color);
    painter.setPen(Qt::NoPen);
    painter.drawEllipse(circleX, circleY, circleDiameter, circleDiameter);
}
