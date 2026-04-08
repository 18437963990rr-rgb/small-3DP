#include "FileOp.h"
#include <QDebug>

// 构造函数
FileOp::FileOp() {}

// 获取当前文件夹中的所有文件完整路径
QVector<QString> FileOp::getFilesList(const QString& folderPath) const {
    QVector<QString> filePaths;
    QDir dir(folderPath);

    // 确保目录存在
    if (!dir.exists()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, QString("directory does not exist:%1").arg(folderPath).toStdString());
        return filePaths;
    }

    // 获取所有文件（不包含子文件夹）
    QStringList fileList = dir.entryList({ "*.tif", "*.tiff" },QDir::Files | QDir::NoDotAndDotDot);

    // 遍历文件列表，保存完整路径
    for (const QString& fileName : fileList) {
        filePaths.append(dir.absoluteFilePath(fileName));
    }

    return filePaths;
}
