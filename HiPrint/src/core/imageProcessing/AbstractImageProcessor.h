#ifndef ABSTRACTIMAGEPROCESSOR_H
#define ABSTRACTIMAGEPROCESSOR_H

#include <QString>
#include <QImage>
#include <vector>

class AbstractImageProcessor {
public:
    virtual ~AbstractImageProcessor() = default;

    virtual bool loadImage(const QString& filePath) = 0;
    virtual QImage convertToQImage() const = 0;
    virtual unsigned char* getBuffer() const = 0;

    virtual int getWidth() const = 0;
    virtual int getHeight() const = 0;
    virtual int getXDimension() const = 0;
    virtual int getYDimension() const = 0;
    virtual int getBitsPerSample() const = 0;
    virtual int getSamplesPerPixel() const = 0;
    virtual float getXResolution() const = 0;
    virtual float getYResolution() const = 0;

    virtual std::vector<unsigned int*> createImageCommand(
        unsigned int splitHeight, unsigned int& splitNum, unsigned int yTop,
        unsigned int trueBpp, unsigned int plane, unsigned int xLeft,
        std::vector<int>& printPositionIndex, bool bEnableFastPrint) = 0;
};

#endif // ABSTRACTIMAGEPROCESSOR_H
