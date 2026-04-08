#ifndef LOGMANAGER_H
#define LOGMANAGER_H

#include <spdlog/spdlog.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/sinks/base_sink.h>
#include <QTextEdit>
#include <memory>
#include <vector>
#include <QPointer>


// 日志级别定义
enum LOG_LEVEL {
    LVL_TRACE = 0,
    LVL_DEBUG = 1,
    LVL_INFO = 2,
    LVL_WARN = 3,
    LVL_ERROR = 4,
    LVL_CRITICAL = 5
};

class LogManager {
public:
    // 获取单例实例
    static LogManager& instance() {
        static LogManager instance;
        return instance;
    }

    // 初始化 LogManager，接收 QTextEdit 对象来输出日志
    void init(QTextEdit* logWidget, int maxLines = 1000, int checkInterval = 100);

    // 记录日志
    void logMessage(LOG_LEVEL level, const std::string& message);

    // 强制刷新所有日志 sink
    void flush();

private:
    // 防止拷贝构造和赋值
    LogManager(const LogManager&) = delete;
    LogManager& operator=(const LogManager&) = delete;
    LogManager() = default;

private:
    std::shared_ptr<spdlog::logger> logger;
    QPointer<QTextEdit> logWidget_;
};

#endif // LOGMANAGER_H
