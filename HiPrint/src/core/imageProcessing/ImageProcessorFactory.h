#ifndef IMAGEPROCESSORFACTORY_H
#define IMAGEPROCESSORFACTORY_H

#include <memory>
#include <QString>
#include "AbstractImageProcessor.h"


class ImageProcessorFactory {
public:
    static std::unique_ptr<AbstractImageProcessor> create(const QString& filePath);
};

#endif // IMAGEPROCESSORFACTORY_H 