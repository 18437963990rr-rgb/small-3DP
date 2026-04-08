#include "ImageProcessorFactory.h"
#include "ImageProcessor.h"
#include <QFileInfo>

std::unique_ptr<AbstractImageProcessor> ImageProcessorFactory::create(const QString& filePath) {
    QFileInfo info(filePath);
    QString suffix = info.suffix().toLower();
    if (suffix == "tif" || suffix == "tiff") {
        return std::make_unique<ImageProcessor>();
    }
    return nullptr;
} 