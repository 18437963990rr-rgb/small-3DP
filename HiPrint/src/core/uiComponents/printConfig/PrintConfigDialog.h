#ifndef PRINTCONFIGDIALOG_H
#define PRINTCONFIGDIALOG_H

#include <QDialog>
#include <QButtonGroup>
#include <QRadioButton>
#include <QListWidget>
#include <QSpinBox>
#include <QLabel>
#include <QPushButton>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QStackedWidget>

class PrintConfigDialog : public QDialog
{
    Q_OBJECT

public:
    enum PrintMode {
        CONTINUOUS_MODE,    // 连续打印
        SINGLE_FILE_MODE    // 单文件打印
    };

    struct PrintTask {
        PrintMode mode;
        
        // 连续模式配置
        struct ContinuousConfig {
            int totalFiles;
            bool confirmExecution;
        };
        
        // 单文件模式配置
        struct SingleFileConfig {
            int selectedFileIndex;
            QString selectedFileName;
            int repeatCount;
        };
        
        ContinuousConfig continuous;
        SingleFileConfig singleFile;
    };

    explicit PrintConfigDialog(const QStringList& fileList, QWidget* parent = nullptr);
    ~PrintConfigDialog();

    PrintTask getPrintTask() const;
    bool isConfirmed() const;

private slots:
    void onModeChanged(QAbstractButton* button);
    void onFileSelectionChanged();
    void onRepeatCountChanged(int value);
    void onConfirmClicked();
    void onCancelClicked();

private:
    void setupUI();
    void setupConnections();
    void updatePrintPreview();
    bool validateConfiguration();
    void showError(const QString& message);
    void populateFileList();

private:
    QStringList m_fileList;
    PrintTask m_printTask;
    bool m_isConfirmed;

    // UI 组件
    QButtonGroup* m_modeGroup;
    QRadioButton* m_continuousRadio;
    QRadioButton* m_singleFileRadio;
    
    QStackedWidget* m_configStack;
    
    // 连续模式组件
    QWidget* m_continuousWidget;
    QLabel* m_fileCountLabel;
    QLabel* m_confirmLabel;
    
    // 单文件模式组件
    QWidget* m_singleFileWidget;
    QListWidget* m_fileListWidget;
    QSpinBox* m_repeatCountSpinBox;
    
    // 通用组件
    QLabel* m_previewLabel;
    QPushButton* m_confirmBtn;
    QPushButton* m_cancelBtn;
    
    static const int MAX_REPEAT_COUNT = 999;
};

#endif // PRINTCONFIGDIALOG_H 