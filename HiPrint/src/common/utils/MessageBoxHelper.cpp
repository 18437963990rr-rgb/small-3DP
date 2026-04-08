#include "MessageBoxHelper.h"
#include <QMessageBox>
#include <QApplication>
#include <memory>

void MessageBoxHelper::showError(QWidget* parent, const QString& title,
    const QString& text, const QString& detailedText)
{
    std::unique_ptr<QMessageBox> messageBox(new QMessageBox(parent));
    messageBox->setWindowModality(parent ? Qt::WindowModal : Qt::NonModal);
    messageBox->setWindowTitle(QApplication::applicationName() + " - " + title);
    messageBox->setText(text);
    if (!detailedText.isEmpty())
        messageBox->setInformativeText(detailedText);
    messageBox->setIcon(QMessageBox::Critical);
    messageBox->addButton(QMessageBox::Ok);
    messageBox->exec();
}

void MessageBoxHelper::showInfo(QWidget* parent, const QString& title,
    const QString& text, const QString& detailedText)
{
    std::unique_ptr<QMessageBox> messageBox(new QMessageBox(parent));
    messageBox->setWindowModality(parent ? Qt::WindowModal : Qt::NonModal);
    messageBox->setWindowTitle(QApplication::applicationName() + " - " + title);
    messageBox->setText(text);
    if (!detailedText.isEmpty())
        messageBox->setInformativeText(detailedText);
    messageBox->setIcon(QMessageBox::Information);
    messageBox->addButton(QMessageBox::Ok);
    messageBox->exec();
}

void MessageBoxHelper::showWarning(QWidget* parent, const QString& title,
    const QString& text, const QString& detailedText)
{
    std::unique_ptr<QMessageBox> messageBox(new QMessageBox(parent));
    messageBox->setWindowModality(parent ? Qt::WindowModal : Qt::NonModal);
    messageBox->setWindowTitle(QApplication::applicationName() + " - " + title);
    messageBox->setText(text);
    if (!detailedText.isEmpty())
        messageBox->setInformativeText(detailedText);
    messageBox->setIcon(QMessageBox::Warning);
    messageBox->addButton(QMessageBox::Ok);
    messageBox->exec();
}
QMessageBox::StandardButton MessageBoxHelper::showQuestion(
    QWidget* parent,
    const QString& title,
    const QString& text,
    const QString& detailedText,
    QMessageBox::StandardButton defaultButton)
{
    std::unique_ptr<QMessageBox> messageBox(new QMessageBox(parent));
    messageBox->setWindowModality(parent ? Qt::WindowModal : Qt::NonModal);
    messageBox->setWindowTitle(QApplication::applicationName() + " - " + title);
    messageBox->setText(text);
    if (!detailedText.isEmpty())
        messageBox->setInformativeText(detailedText);
    messageBox->setIcon(QMessageBox::Question);
    messageBox->setStandardButtons(QMessageBox::Yes | QMessageBox::No);
    messageBox->setDefaultButton(defaultButton);
    return static_cast<QMessageBox::StandardButton>(messageBox->exec());
}