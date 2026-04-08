#include "LogManager.h"
#include "QTextEditLogSink.h" // 引入自定义 Sink 头文件

void LogManager::init(QTextEdit* logWidget, int maxLines, int checkInterval) {
    if (!logWidget) {
        spdlog::error("LogManager init failed: QTextEdit pointer is null.");
        return;
    }

    try {
        logWidget_ = logWidget;
        // 创建旋转文件 Sink
        auto rotatingSink = std::make_shared<spdlog::sinks::rotating_file_sink_mt>(
            "mylog.txt", 10 * 1024 * 1024, 5);
        rotatingSink->set_level(spdlog::level::debug);
        rotatingSink->set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%l] %v");

        // 创建自定义 QTextEdit Sink
        auto textSink = std::make_shared<QTextEditLogSink<spdlog::details::null_mutex>>(logWidget, maxLines, checkInterval);
        textSink->set_level(spdlog::level::debug);
        textSink->set_pattern("[%Y-%m-%d %H:%M:%S] [%l] %v");

        std::vector<spdlog::sink_ptr> sinks{ rotatingSink, textSink };

        // 创建 Logger 并添加 sinks
        logger = std::make_shared<spdlog::logger>("LogManagerLogger", sinks.begin(), sinks.end());
        logger->set_level(spdlog::level::debug);
        spdlog::register_logger(logger);
    }
    catch (const std::exception& ex) {
        spdlog::critical("LogManager initialization failed: {}", ex.what());
    }
}

void LogManager::logMessage(LOG_LEVEL level, const std::string& message) {
    if (!logger) {
        spdlog::error("Logger is not initialized.");
        return;
    }

    try {
        switch (level) {
        case LVL_TRACE:
            logger->trace(message);
            break;
        case LVL_DEBUG:
            logger->debug(message);
            break;
        case LVL_INFO:
            logger->info(message);
            break;
        case LVL_WARN:
            logger->warn(message);
            break;
        case LVL_ERROR:
            logger->error(message);
            break;
        case LVL_CRITICAL:
            logger->critical(message);
            break;
        }
        flush();
    }
    catch (const std::exception& ex) {
        spdlog::critical("Error while logging message: {}", ex.what());
    }
}

void LogManager::flush() {
    if (logger) {
        logger->flush();  // 刷新所有 sinks
    }
    else {
        spdlog::warn("Logger is not initialized, cannot flush.");
    }
}
