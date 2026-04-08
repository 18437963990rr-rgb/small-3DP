#ifndef FILEOP_H
#define FILEOP_H

#include <QDir>
#include <QVector>
#include <QString>
#include "../logging/LogManager.h"

class FileOp {
public:
    // 构造函数，接受文件夹路径
    explicit FileOp();

    // 获取当前文件夹内所有文件的完整路径
     QVector<QString> getFilesList(const QString& folderPath) const;
};

#endif // FILEOP_H

