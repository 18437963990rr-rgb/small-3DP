#include "CrashHandler.h"
#include <QCoreApplication>
#include <QDir>
#include <spdlog/spdlog.h>  
#include <exception> // for std::set_terminate

// 构造函数：初始化时设置异常处理器
CrashHandler::CrashHandler()
{
    // 设置未处理的异常处理器
    SetUnhandledExceptionFilter(unhandledExceptionFilter);

    // 设置异常转换器（捕获异常并调用异常处理器）
    _set_se_translator(exceptionTranslator);
}

// 析构函数
CrashHandler::~CrashHandler() {}

// 初始化异常处理器
void CrashHandler::initialize(const QString& dumpDir)
{
    // 设置异常过滤器，确保它在应用程序启动时就设置好
    SetUnhandledExceptionFilter(unhandledExceptionFilter);

    // 如果有指定的转储目录路径，使用它，否则使用默认的路径
    if (!dumpDir.isEmpty()) {
        spdlog::info("Crash dump will be saved to: {}", dumpDir.toStdString());
    }
    else {
        spdlog::info("Crash dump will be saved to the default directory.");
    }

    // 记录程序已开始监控异常
    spdlog::info("Program Exception Monitoring started.");
}

// 生成带时间戳的唯一转储文件名
QString CrashHandler::generateDumpFileName(const QString& dumpDir)
{
    // 获取当前时间
    QString timestamp = QDateTime::currentDateTime().toString("yyyyMMdd_hhmmss");
    QString dumpFileName = dumpDir + "\\dump_" + timestamp + ".dmp";
    return dumpFileName;
}

void CrashHandler::exceptionTranslator(unsigned int code, EXCEPTION_POINTERS* ep)
{
    // 根据异常代码处理常见的异常
    switch (code) {
    case EXCEPTION_ACCESS_VIOLATION:
        spdlog::error("Access violation exception (memory access violation) occurred.");
        break;
    case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
        spdlog::error("Array bounds exceeded exception occurred.");
        break;
    case EXCEPTION_FLT_DIVIDE_BY_ZERO:
        spdlog::error("Floating point divide by zero exception occurred.");
        break;
    case EXCEPTION_INT_DIVIDE_BY_ZERO:
        spdlog::error("Integer divide by zero exception occurred.");
        break;
    case EXCEPTION_STACK_OVERFLOW:
        spdlog::error("Stack overflow exception occurred.");
        break;
    default:
        spdlog::error("Unhandled exception occurred. Exception code: {}", code);
        break;
    }
    // 在异常发生时抛出标准异常，以便 _set_se_translator 捕获并进一步处理
    throw std::exception();
}

// 异常处理回调函数
LONG WINAPI CrashHandler::unhandledExceptionFilter(EXCEPTION_POINTERS* exceptionInfo)
{
    // 获取转储目录路径，使用默认目录，如果未提供自定义目录
    QString dumpDirPath = QCoreApplication::applicationDirPath() + "/dumps";
    QDir dir(dumpDirPath);

    // 如果目录不存在，则创建它
    if (!dir.exists()) {
        dir.mkpath(dumpDirPath);
    }

    // 生成带时间戳的转储文件名
    QString dumpFilePath = generateDumpFileName(dumpDirPath);

    // 打开转储文件
    HANDLE hFile = CreateFile(reinterpret_cast<LPCWSTR>(dumpFilePath.utf16()), GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile != INVALID_HANDLE_VALUE) {
        // 准备生成转储文件
        MINIDUMP_EXCEPTION_INFORMATION excInfo;
        excInfo.ThreadId = GetCurrentThreadId();  // 当前线程ID
        excInfo.ExceptionPointers = exceptionInfo;  // 异常信息
        excInfo.ClientPointers = FALSE;

        // 调用 MiniDumpWriteDump 生成转储文件
        bool ret = MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), hFile, MiniDumpNormal, (exceptionInfo ? &excInfo : NULL), NULL, NULL);

        // 关闭文件
        CloseHandle(hFile);
    }
    // 返回 EXCEPTION_CONTINUE_SEARCH 继续调用默认的处理程序
    return EXCEPTION_CONTINUE_SEARCH;
}


