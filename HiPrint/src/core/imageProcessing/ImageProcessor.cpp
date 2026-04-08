#include "ImageProcessor.h"
#include <tiffio.h>
#include <QMessageBox>
#include <QImageReader>
#include "../../common/logging/LogManager.h"


ImageProcessor::ImageProcessor()
    : m_width(0)
    , m_height(0)
    , m_bitsPerSample(0)
    , m_samplesPerPixel(0)
    , m_xResolution(0.0f)
    , m_yResolution(0.0f)
    , m_compression(0)
{
}


ImageProcessor::~ImageProcessor() {
    
}

bool ImageProcessor::loadImage(const QString& filePath)
{
    QFileInfo fileInfo(filePath);
    QString fileName = fileInfo.fileName();
    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, QString("Load %1 Start!").arg(fileName).toStdString());

    m_filePath = filePath;
    std::wstring wpath = filePath.toStdWString();
    TIFF* tiff = TIFFOpenW(wpath.c_str(), "r");
    if (!tiff) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Failed to open TIFF file!");
       return false;
    }
    // 获取TIFF图片属性
    TIFFGetField(tiff, TIFFTAG_IMAGEWIDTH, &m_width);
    TIFFGetField(tiff, TIFFTAG_IMAGELENGTH, &m_height);
    TIFFGetField(tiff, TIFFTAG_BITSPERSAMPLE, &m_bitsPerSample);
    TIFFGetField(tiff, TIFFTAG_SAMPLESPERPIXEL, &m_samplesPerPixel);
    TIFFGetField(tiff, TIFFTAG_XRESOLUTION, &m_xResolution);
    TIFFGetField(tiff, TIFFTAG_YRESOLUTION, &m_yResolution);
    TIFFGetField(tiff, TIFFTAG_COMPRESSION, &m_compression);

    if (m_bitsPerSample != 1 || m_samplesPerPixel != 1) {
        TIFFClose(tiff);
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Only 1-bit grayscale images are supported!");
        return false;
    }

    // 读取TIFF图像数据到buffer
    tsize_t stripSize = TIFFStripSize(tiff);
    tstrip_t numStrips = TIFFNumberOfStrips(tiff);
    m_imageBuffer.resize(stripSize * numStrips);

    for (tstrip_t strip = 0; strip < numStrips; ++strip) {
        if (TIFFReadEncodedStrip(tiff, strip, &m_imageBuffer[strip * stripSize], stripSize) == -1) {
            TIFFClose(tiff);
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Failed to read strip from TIFF!");
            return false;
        }
    }
    TIFFClose(tiff);
    LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Load %1 End!").arg(fileName).toStdString());
    return true;
}

QImage ImageProcessor::convertToQImage() const
{
    //if (m_imageBuffer.empty()) {
    //    LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "No image data available!");
    //    return QImage();  // 返回空图像表示失败
    //}

    //QImage image(m_width, m_height, QImage::Format_Mono);
    //if (image.isNull()) {
    //    QMessageBox::warning(nullptr, "Image Processor", "Failed to create image!");
    //    return QImage();
    //}

    //unsigned char* imageBits = image.bits();
    //int bytesPerLine = image.bytesPerLine();

    //// 逐行转换
    //for (unsigned int y = 0; y < m_height; ++y) {
    //    unsigned char* lineStart = imageBits + static_cast<size_t>(y) * static_cast<size_t>(bytesPerLine);
    //    for (unsigned int x = 0; x < m_width; ++x) {
    //        unsigned int bufferIndex = (y * m_width + x) / 8;
    //        unsigned char byte = m_imageBuffer[bufferIndex];
    //        unsigned char bit = 7 - (x % 8);
    //        bool isSet = (byte >> bit) & 1;
    //        if (isSet) {
    //            // 设置为白色
    //            lineStart[x / 8] &= ~(1 << (7 - (x % 8)));
    //        }
    //        else {
    //            // 设置为黑色
    //            lineStart[x / 8] |= (1 << (7 - (x % 8)));
    //        }
    //    }
    //}
    
   
    QImageReader reader(m_filePath);
    QImage processedImage = reader.read();
    if (!processedImage.isNull()) {
        return processedImage;
    } else {
        // 添加调试信息
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, 
            QString("ImageProcessor - EXIF处理失败，使用原始图片，尺寸: %1x%2")
            .arg(m_width).arg(m_height).toStdString());
    }
    
   // return image;
}



// 图像数据缓冲区相关函数
unsigned char* ImageProcessor::getBuffer() const {
    // 返回图像数据的缓冲区
    return m_imageBuffer.empty() ? nullptr : const_cast<unsigned char*>(m_imageBuffer.data());
}

// 图像属性相关函数
int ImageProcessor::getWidth() const {
    return m_width;
}

int ImageProcessor::getHeight() const {
    return m_height;
}

int ImageProcessor::getXDimension() const
{
    return (m_width / m_xResolution) * 25.4;
}

int ImageProcessor::getYDimension() const
{
    return (m_height / m_yResolution) * 25.4;
}

int ImageProcessor::getBitsPerSample() const {
    return m_bitsPerSample;
}

int ImageProcessor::getSamplesPerPixel() const {
    return m_samplesPerPixel;
}

float ImageProcessor::getXResolution() const {
    return m_xResolution;
}

float ImageProcessor::getYResolution() const {
    return m_yResolution;
}


std::vector<unsigned int*> ImageProcessor::createImageCommand(unsigned int splitHeight, unsigned int& splitNums, unsigned int yTop,
    unsigned int trueBpp, unsigned int plane, unsigned int xLeft, std::vector<int>& printPositionIndex, bool bEnableFastPrint)
{
  
    if (m_imageBuffer.empty() || m_width == 0 || m_height == 0) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Invalid image data!");
        return std::vector<unsigned int*>();
    }
    std::vector<std::unique_ptr<unsigned char[]>> splitBuffers;
    std::vector<unsigned int> splitPartHeight;
    std::vector<unsigned int*> imageCommandBuffer;
    std::vector<unsigned int> actualPrintPositionIndex;

    const unsigned int bytesPerRow = (m_width + 7) / 8;
    splitNums = (m_height + splitHeight - 1) / splitHeight;

    for (unsigned int partIndex = 0; partIndex < splitNums; ++partIndex) {
        const unsigned int startRow = partIndex * splitHeight;
        const uint32_t endRow = startRow + splitHeight > m_height ? m_height : startRow + splitHeight;
        const uint32_t actualHeight = endRow - startRow;
        
        auto partBuffer = std::make_unique<unsigned char[]>(static_cast<size_t>(bytesPerRow) * actualHeight);
        if (!partBuffer) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Failed to allocate memory for image part!");
            continue;
        }

        for (uint32_t row = 0; row < actualHeight; ++row) {
            const unsigned char* srcRow = getBuffer() + ((static_cast<size_t>(startRow) + static_cast<size_t>(row)) * bytesPerRow);
            unsigned char* dstRow = partBuffer.get() + static_cast<size_t>(row) * bytesPerRow;
            std::memcpy(dstRow, srcRow, bytesPerRow);
        }
        
        splitBuffers.push_back(std::move(partBuffer));
        splitPartHeight.push_back(actualHeight);
    }
    if (bEnableFastPrint) {
        printPositionIndex.clear();
        printPositionIndex.resize(splitNums, 0);
        for (unsigned int splitNum = 0; splitNum < splitNums; ++splitNum) {
            const auto& currentBuffer = splitBuffers[splitNum];
            const auto currentHeight = splitPartHeight[splitNum];
            bool hasEmptyByteRow = false;
            for (unsigned int row = 0; row < currentHeight && !hasEmptyByteRow; ++row) {
                const unsigned char* rowData = currentBuffer.get() + static_cast<size_t>(row) * bytesPerRow;
                for (unsigned int byteIndex = 0; byteIndex < bytesPerRow; ++byteIndex) {
                    if (rowData[byteIndex] != 0) {
                        hasEmptyByteRow = true;
                        break;
                    }
                }
            }
            printPositionIndex[splitNum] = hasEmptyByteRow ? 1 : 0;
            if (hasEmptyByteRow) {
                actualPrintPositionIndex.push_back(splitNum);
            }
        }
    } else {
        actualPrintPositionIndex.resize(splitNums);
        std::iota(actualPrintPositionIndex.begin(), actualPrintPositionIndex.end(), 0);
        printPositionIndex.clear();
        printPositionIndex.resize(splitNums, 1);
    }
    for (size_t i = 0; i < actualPrintPositionIndex.size(); ++i) {
        const unsigned int splitIndex = actualPrintPositionIndex[i];
        const unsigned int imageSplitHeight = splitPartHeight[splitIndex];
        const int isize = ((m_width * trueBpp + 31) >> 5) * imageSplitHeight;
        unsigned int* pBuff = static_cast<unsigned int*>(calloc(static_cast<size_t>(isize) + 6, sizeof(int)));
        if (!pBuff) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, "Failed to allocate command buffer!");
            continue;
        }
        pBuff[0] = PCMD_IMAGE;
        pBuff[1] = isize + 4;
        pBuff[2] = plane;
        pBuff[3] = ((i & 1) == 0) ? xLeft : (xLeft + m_width);
        //pBuff[3] = xLeft;
        pBuff[4] = yTop;
        pBuff[5] = m_width;
        unsigned int* dp = &pBuff[6];
        const unsigned char* partStart = splitBuffers[splitIndex].get();
        for (unsigned int y = 0; y < imageSplitHeight; ++y) {
            const unsigned char* lbuff = partStart + static_cast<size_t>(bytesPerRow) * y;
            uint32_t w = 0; 
            for (unsigned int x = 0; x < m_width; ++x) {
                if ((x & 7) == 0) {
                    w = (w << 8) | lbuff[x >> 3];
                }
                if ((x & 31) == 31) {
                    *dp++ = w;
                    w = 0;
                }
            }   
            if (m_width & 31) {
                *dp++ = w << (32 - (m_width & 31));
            }
        }    
        imageCommandBuffer.push_back(pBuff);
    }
    return imageCommandBuffer;
}