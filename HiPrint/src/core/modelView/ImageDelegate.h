#pragma once
#include <QStyledItemDelegate>
#include <QPainter>
#include <QImage>
#include <QApplication>
#include <QStyleOptionButton>
#include <QTextOption>
#include <QFontMetrics>
#include <qDebug>
#include <QFileInfo>

class ImageDelegate : public QStyledItemDelegate {
    Q_OBJECT
public:
    explicit ImageDelegate(QObject* parent = nullptr);
    
    // 清理图片缓存
    static void clearImageCache();

    void paint(QPainter* painter, const QStyleOptionViewItem& option, const QModelIndex& index) const override;
    QSize sizeHint(const QStyleOptionViewItem& option, const QModelIndex& index) const override;

private:
    // 布局常量
    static const int MARGIN = 5;            // 边距
    static const int IMAGE_SIZE = 180;      // 图片大小
    static const int TEXT_HEIGHT = 16;      // 文本高度
};

