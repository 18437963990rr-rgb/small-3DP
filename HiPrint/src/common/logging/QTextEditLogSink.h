#ifndef TEXTEDIT_LOG_SINK_H
#define TEXTEDIT_LOG_SINK_H

#include <spdlog/sinks/base_sink.h>
#include <QTextEdit>
#include <QTextCharFormat>
#include <QTextCursor>
#include <QMetaObject>
#include <QString>
#include <QTextBlock>
#include <cassert>
#include <QScrollBar>

template<typename Mutex>
class QTextEditLogSink : public spdlog::sinks::base_sink<Mutex> {
public:
    explicit QTextEditLogSink(QTextEdit* widget, int maxLines = 1000, int checkInterval = 100);

protected:
    void sink_it_(const spdlog::details::log_msg& msg) override;
    void flush_() override;

private:
    QTextEdit* m_widget;
    int insertCount;
    const int checkInterval;  // 每插入 checkInterval 条日志检查一次
    const int maxLines;       // 限制最大行数

    void setLogLevelColor(spdlog::level::level_enum level, QTextCharFormat& format);
};

#endif // TEXTEDIT_LOG_SINK_H
