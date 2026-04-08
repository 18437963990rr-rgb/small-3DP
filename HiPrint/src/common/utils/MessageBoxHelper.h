#pragma once

#include <QWidget>
#include <QString>
#include <QMessageBox>

class MessageBoxHelper
{
public:
    static void showError(QWidget* parent, const QString& title,
        const QString& text, const QString& detailedText = QString());

    static void showInfo(QWidget* parent, const QString& title,
        const QString& text, const QString& detailedText = QString());

    static void showWarning(QWidget* parent, const QString& title,
        const QString& text, const QString& detailedText = QString());

    static QMessageBox::StandardButton showQuestion(QWidget* parent, const QString& title,
        const QString& text, const QString& detailedText = QString(),
        QMessageBox::StandardButton defaultButton = QMessageBox::No);
};
