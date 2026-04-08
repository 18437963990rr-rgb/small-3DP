#ifndef IMAGEPROCESSOR_H
#define IMAGEPROCESSOR_H

#include "AbstractImageProcessor.h"
#include <QImage>
#include <QString>
#include <vector>
#include <qDebug>
#include <qfileinfo.h>
#include "./3rdparty/meteor/Meteor.h"

class ImageProcessor : public AbstractImageProcessor {
public:
    ImageProcessor();
    ~ImageProcessor() override;
    bool loadImage(const QString& filePath) override;
    QImage convertToQImage() const override;
    unsigned char* getBuffer() const override;
    int getWidth() const override;
    int getHeight() const override;
    int getXDimension() const override;
    int getYDimension() const override;
    int getBitsPerSample() const override;
    int getSamplesPerPixel() const override;
    float getXResolution() const override;
    float getYResolution() const override;
    std::vector<unsigned int*> createImageCommand(unsigned int splitHeight, unsigned int& splitNum, 
        unsigned int yTop, unsigned int trueBpp, unsigned int plane, unsigned int xLeft,
        std::vector<int>& printPositionIndex, bool bEnableFastPrint) override;
private:
    // 图像属性
    int m_width;
    int m_height;
    int m_xDimension;
    int m_yDimension;
    int m_bitsPerSample;
    int m_samplesPerPixel;
    float m_xResolution;
    float m_yResolution;
    int  m_compression;
    std::vector<unsigned char> m_imageBuffer;// 图像数据缓冲区
    QString m_filePath; // 文件路径，用于EXIF处理
};

#endif // IMAGEPROCESSOR_H
