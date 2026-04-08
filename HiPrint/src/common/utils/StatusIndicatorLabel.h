#pragma once

#include <QLabel>
#include <QColor>

class StatusIndicatorLabel : public QLabel
{
    Q_OBJECT
public:
    explicit StatusIndicatorLabel(const QString& text, QWidget* parent = nullptr);

    void setStateColor(const QColor& color);

protected:
    void paintEvent(QPaintEvent* event) override;

private:
    QString m_text;
    QColor m_color;
};

