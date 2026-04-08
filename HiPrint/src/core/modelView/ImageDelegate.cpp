#include "ImageDelegate.h"
#include <QPainter>
#include <QMouseEvent>
#include <QFont>
#include <QApplication>
#include <QCache>
#include <QImageReader>

// 图片缓存，避免重复加载和缩放 - 减小缓存大小以节省内存
static QCache<QString, QImage> imageCache(50); // 缓存50张图片

ImageDelegate::ImageDelegate(QObject* parent)
    : QStyledItemDelegate(parent) 
{
}

void ImageDelegate::clearImageCache()
{
    imageCache.clear();
}

void ImageDelegate::paint(QPainter* painter, const QStyleOptionViewItem& option, 
    const QModelIndex& index) const  
{
    if (index.column() != 0) {
        QStyledItemDelegate::paint(painter, option, index);
        return;
    }
    painter->save();
    
    // 1. 绘制背景
    if (option.state & QStyle::State_Selected) {
        painter->fillRect(option.rect, option.palette.highlight().color().lighter(200));
    }

    // 2. 获取数据
    QString path = index.data(Qt::DisplayRole).toString();
    QFileInfo fileInfo(path);
    QString fileName = fileInfo.fileName();

    // 3. 计算布局
    const int contentHeight = IMAGE_SIZE + TEXT_HEIGHT + 2;
    const int startY = option.rect.y() + (option.rect.height() - contentHeight) / 2;

    // 4. 绘制图片 - 使用缓存优化性能
    QRect imageRect(option.rect.x() + MARGIN, startY, IMAGE_SIZE, IMAGE_SIZE);
    
    // 检查缓存中是否有已缩放的图片
    QImage* cachedImage = imageCache.object(path);
    if (!cachedImage) {
        // 缓存中没有，加载并缩放图片
        QImage originalImage(path);
        if (!originalImage.isNull()) {
            // 处理图片方向信息，确保图片正确显示
            QImageReader reader(path);
            QImage processedImage = reader.read();
            if (!processedImage.isNull()) {
                originalImage = processedImage;
            }
            
            // 计算缩放尺寸
            QSize scaledSize = originalImage.size();
            scaledSize.scale(IMAGE_SIZE, IMAGE_SIZE, Qt::KeepAspectRatio);
            
            // 创建缩放后的图片并缓存 - 使用快速缩放以提高性能
            QImage* scaledImage = new QImage(originalImage.scaled(scaledSize, Qt::KeepAspectRatio, Qt::FastTransformation));
            imageCache.insert(path, scaledImage);
            cachedImage = scaledImage;
        }
    }
    
    if (cachedImage && !cachedImage->isNull()) {
        // 计算居中位置
        int x = imageRect.x() + (IMAGE_SIZE - cachedImage->width()) / 2;
        int y = imageRect.y() + (IMAGE_SIZE - cachedImage->height()) / 2;
        QRect drawRect(x, y, cachedImage->width(), cachedImage->height());
        
        // 绘制浅蓝色虚线外边框
        QPen dashedPen(QColor(135, 206, 235), 2); // 浅蓝色边框，宽度2像素
        dashedPen.setStyle(Qt::DashLine); // 设置为虚线样式
        painter->setPen(dashedPen);
        painter->drawRect(drawRect.adjusted(-1, -1, 1, 1)); // 边框稍微扩大1像素
        
        // 直接绘制缓存的图片，无需再次缩放
        painter->drawImage(drawRect, *cachedImage);
        
        // 5. 绘制文件名 - 基于实际图片位置
        painter->setPen(Qt::black);
        painter->setFont(QFont(painter->font().family(), 9));
        QRect textRect(drawRect.x(), drawRect.bottom() + 2, drawRect.width(), TEXT_HEIGHT); // 基于实际图片位置
        painter->drawText(textRect, Qt::AlignCenter, QFontMetrics(painter->font()).elidedText(fileName, Qt::ElideMiddle, textRect.width()));
        
    } else {
        // 图片加载失败时显示占位符
        painter->setPen(Qt::gray);
        painter->setFont(QFont(painter->font().family(), 8));
        painter->drawText(imageRect, Qt::AlignCenter, tr("图片加载失败"));
        
        // 5. 绘制文件名 - 图片加载失败时
        painter->setPen(Qt::black);
        painter->setFont(QFont(painter->font().family(), 9));
        QRect textRect(imageRect.x(), imageRect.bottom() + 2, imageRect.width(), TEXT_HEIGHT);
        painter->drawText(textRect, Qt::AlignCenter, QFontMetrics(painter->font()).elidedText(fileName, Qt::ElideMiddle, textRect.width()));
    }
    
    painter->restore();
}

QSize ImageDelegate::sizeHint(const QStyleOptionViewItem& option, 
    const QModelIndex& index) const
{
    if (index.column() == 0) {
        return QSize(IMAGE_SIZE + MARGIN * 2, IMAGE_SIZE + TEXT_HEIGHT + 4); // 减少垂直间距
    }
    return QStyledItemDelegate::sizeHint(option, index);
}