// AdsErrorMapper.h

#ifndef ADERRORMAPPER_H
#define ADERRORMAPPER_H

#include <QtCore/QHash>
#include <QtCore/QString>
#include <iostream>

class AdsErrorMapper {
public:
    // 构造函数，初始化错误码映射
    AdsErrorMapper();
    // 获取错误描述（int 类型参数）
    static QString getErrorDescription(int errorCode);
    static void initializeErrorDescriptions();
private:
    static QHash<int, QString> m_errorMap;  // 错误码到描述的映射
};

#endif // ADERRORMAPPER_H

