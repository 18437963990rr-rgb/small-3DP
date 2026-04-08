#include "QTextEditLogSink.h"
#include <QTextDocument>

// 构造函数实现
template<typename Mutex>
QTextEditLogSink<Mutex>::QTextEditLogSink(QTextEdit* widget, int maxLines, int checkInterval)
    : m_widget(widget), insertCount(0), maxLines(maxLines), checkInterval(checkInterval) {
    assert(m_widget);  // 确保 widget 不为空
}

// 实现 sink_it_ 方法
template<typename Mutex>
void QTextEditLogSink<Mutex>::sink_it_(const spdlog::details::log_msg& msg) {
    // 格式化日志消息
    spdlog::memory_buf_t formatted;
    this->formatter_->format(msg, formatted);
    QString logLine = QString::fromUtf8(formatted.data(), static_cast<int>(formatted.size()));

    // 根据日志级别设置颜色
    QTextCharFormat logFormat;
    setLogLevelColor(msg.level, logFormat);

    if (m_widget) {
        // 使用 QMetaObject::invokeMethod 确保在 GUI 线程中操作 QTextEdit
        QMetaObject::invokeMethod(m_widget, [this, logLine, logFormat]() {
            if (!m_widget) return;

            QTextCursor cursor(m_widget->document());
            cursor.movePosition(QTextCursor::End);
            cursor.insertText(logLine + "\n", logFormat);

            insertCount++;

            if (insertCount >= checkInterval) {
                // 限制最大行数
                QTextDocument* doc = m_widget->document();
                while (doc->blockCount() > maxLines) {
                    QTextBlock lastBlock = doc->lastBlock();
                    cursor.setPosition(lastBlock.position());
                    cursor.select(QTextCursor::Document);
                    cursor.removeSelectedText();
                    //cursor.deleteChar();
                }
                insertCount = 0;
            }
            // 添加自动滚动到底部
            QScrollBar* scrollBar = m_widget->verticalScrollBar();
            if (scrollBar) {
                scrollBar->setValue(scrollBar->maximum());
            }
        }, Qt::QueuedConnection);
    }
}

// 实现 flush_ 方法
template<typename Mutex>
void QTextEditLogSink<Mutex>::flush_() {
    // 通常不需要实现刷新逻辑
}

// 实现 setLogLevelColor 方法
template<typename Mutex>
void QTextEditLogSink<Mutex>::setLogLevelColor(spdlog::level::level_enum level, QTextCharFormat& format) {
    switch (level) {
    case spdlog::level::trace:
        format.setForeground(Qt::gray);
        break;
    case spdlog::level::debug:
        format.setForeground(Qt::blue);
        break;
    case spdlog::level::info:
        format.setForeground(Qt::black);
        break;
    case spdlog::level::warn:
        format.setForeground(QColor(255, 140, 0));
        break;
    case spdlog::level::err:
        format.setForeground(Qt::red);
        break;
    case spdlog::level::critical:
        format.setForeground(Qt::darkRed);
        break;
    case spdlog::level::off:
        break;
    }
}

// 显式实例化模板类
template class QTextEditLogSink<std::mutex>;
template class QTextEditLogSink<spdlog::details::null_mutex>;
