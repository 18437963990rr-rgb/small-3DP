#ifndef CRASH_HANDLER_H
#define CRASH_HANDLER_H

#include <windows.h>
#include <DbgHelp.h>
#include <QString>
#include <QDateTime>
#include "../Logging/LogManager.h"  // 确保 LogManager 正常工作

#pragma comment(lib, "Dbghelp.lib")

class CrashHandler
{
public:
    // 获取单例实例
    static CrashHandler& instance() {
        static CrashHandler instance;  
        return instance;
    }

    // 删除拷贝构造函数和赋值运算符，确保无法复制
    CrashHandler(const CrashHandler&) = delete;
    CrashHandler& operator=(const CrashHandler&) = delete;

    // 初始化异常处理器
    void initialize(const QString& dumpDir = "");

private:
    // 构造函数和析构函数
    CrashHandler();
    ~CrashHandler();

    // 异常处理回调函数
    static LONG WINAPI unhandledExceptionFilter(EXCEPTION_POINTERS* exceptionInfo);

    // 生成唯一的转储文件名（带时间戳）
    static QString generateDumpFileName(const QString& dumpDir);

    // 设置异常转换函数
    static void exceptionTranslator(unsigned int code, EXCEPTION_POINTERS* ep);
};

#endif // CRASH_HANDLER_H
