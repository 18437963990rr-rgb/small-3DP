#include "PrintConfigDialog.h"
#include <QMessageBox>
#include <QFileInfo>
#include <QApplication>
#include <QStyle>
#include <QGroupBox>
#include "../../../common/utils/MessageBoxHelper.h"

PrintConfigDialog::PrintConfigDialog(const QStringList& fileList, QWidget* parent)
    : QDialog(parent)
    , m_fileList(fileList)
    , m_isConfirmed(false)
{
    // 初始化打印任务
    m_printTask.mode = SINGLE_FILE_MODE;
    m_printTask.continuous.totalFiles = fileList.size();
    m_printTask.continuous.confirmExecution = false;
    m_printTask.singleFile.selectedFileIndex = 0;
    m_printTask.singleFile.selectedFileName = fileList.isEmpty() ? "" : fileList.first();
    m_printTask.singleFile.repeatCount = 1;

    setupUI();
    setupConnections();
    populateFileList();
    
    setWindowTitle(tr("打印任务配置"));
    setModal(true);
    setFixedSize(550, 650); 
   
    m_singleFileRadio->setChecked(true);
    onModeChanged(m_singleFileRadio);
}

PrintConfigDialog::~PrintConfigDialog()
{
}

PrintConfigDialog::PrintTask PrintConfigDialog::getPrintTask() const
{
    return m_printTask;
}

bool PrintConfigDialog::isConfirmed() const
{
    return m_isConfirmed;
}

void PrintConfigDialog::setupUI()
{
    QVBoxLayout* mainLayout = new QVBoxLayout(this);
    mainLayout->setSpacing(20); 
    
    QFont defaultFont = font();
    defaultFont.setPointSize(10);
    setFont(defaultFont);
    
    QGroupBox* modeGroupBox = new QGroupBox(tr("打印模式"), this);
    QFont groupFont = defaultFont;
    groupFont.setPointSize(13);
    groupFont.setBold(true);
    modeGroupBox->setFont(groupFont);
    QHBoxLayout* modeLayout = new QHBoxLayout(modeGroupBox);
    modeLayout->setSpacing(60);
    modeLayout->setContentsMargins(35, 35, 35, 35); // 增加内边距
    
    m_continuousRadio = new QRadioButton(tr("连续打印"), modeGroupBox);
    m_singleFileRadio = new QRadioButton(tr("单文件打印"), modeGroupBox);
    
    QFont radioFont = defaultFont;
    radioFont.setPointSize(14);
    radioFont.setBold(true);
    m_continuousRadio->setFont(radioFont);
    m_singleFileRadio->setFont(radioFont);
    
    modeLayout->addWidget(m_continuousRadio);
    modeLayout->addWidget(m_singleFileRadio);
    modeLayout->addStretch();
    
    mainLayout->addWidget(modeGroupBox);
    
    m_configStack = new QStackedWidget(this);
    
    m_continuousWidget = new QWidget(m_configStack);
    QVBoxLayout* continuousLayout = new QVBoxLayout(m_continuousWidget);
    continuousLayout->setSpacing(15);
    continuousLayout->setContentsMargins(15, 15, 15, 15);
    
    m_fileCountLabel = new QLabel(m_continuousWidget);
    m_confirmLabel = new QLabel(tr("确认执行连续打印？"), m_continuousWidget);
    m_confirmLabel->setObjectName("confirmLabel");
    
    // 设置连续模式标签字体
    QFont continuousFont = defaultFont;
    continuousFont.setPointSize(12);
    m_fileCountLabel->setFont(continuousFont);
    
    QFont confirmFont = defaultFont;
    confirmFont.setPointSize(14);
    confirmFont.setBold(true);
    m_confirmLabel->setFont(confirmFont);
   
    
    continuousLayout->addWidget(m_fileCountLabel);
    continuousLayout->addWidget(m_confirmLabel);
    continuousLayout->addStretch();
    
    m_configStack->addWidget(m_continuousWidget);
    
    // 单文件模式界面
    m_singleFileWidget = new QWidget(m_configStack);
    QVBoxLayout* singleFileLayout = new QVBoxLayout(m_singleFileWidget);
    singleFileLayout->setSpacing(15); // 增加间距
    singleFileLayout->setContentsMargins(15, 15, 15, 15); // 增加内边距
    
    // 文件选择列表
    QLabel* fileSelectLabel = new QLabel(tr("选择文件："), m_singleFileWidget);
    QFont fileSelectFont = defaultFont;
    fileSelectFont.setPointSize(13);
    fileSelectFont.setBold(true);
    fileSelectLabel->setFont(fileSelectFont);
    
    m_fileListWidget = new QListWidget(m_singleFileWidget);
    m_fileListWidget->setMaximumHeight(250);
    m_fileListWidget->setFont(defaultFont);
 
    
    singleFileLayout->addWidget(fileSelectLabel);
    singleFileLayout->addWidget(m_fileListWidget);
    
   
    QLabel* repeatLabel = new QLabel(tr("打印次数："), m_singleFileWidget);
    QFont repeatLabelFont = defaultFont;
    repeatLabelFont.setPointSize(12); 
    repeatLabelFont.setBold(true);
    repeatLabel->setFont(repeatLabelFont);
    
    m_repeatCountSpinBox = new QSpinBox(m_singleFileWidget);
    m_repeatCountSpinBox->setRange(1, MAX_REPEAT_COUNT);
    m_repeatCountSpinBox->setValue(1);
    QFont spinBoxFont = defaultFont;
    spinBoxFont.setPointSize(15); 
    m_repeatCountSpinBox->setFont(spinBoxFont);
    m_repeatCountSpinBox->setMinimumHeight(40); 
    m_repeatCountSpinBox->setMinimumWidth(80);

    
    QHBoxLayout* repeatLayout = new QHBoxLayout();
    repeatLayout->setSpacing(10); 
    repeatLayout->addWidget(repeatLabel);
    repeatLayout->addWidget(m_repeatCountSpinBox);
    repeatLayout->addStretch();
    
    singleFileLayout->addLayout(repeatLayout);
    singleFileLayout->addStretch();
    
    m_configStack->addWidget(m_singleFileWidget);
    
    mainLayout->addWidget(m_configStack);
    
    QGroupBox* previewGroupBox = new QGroupBox(tr("打印预览"), this);
    QFont previewGroupFont = defaultFont;
    previewGroupFont.setPointSize(13);
    previewGroupFont.setBold(true);
    previewGroupBox->setFont(previewGroupFont);
    QVBoxLayout* previewLayout = new QVBoxLayout(previewGroupBox);
    previewLayout->setContentsMargins(35, 35, 35, 35);
    
    m_previewLabel = new QLabel(previewGroupBox);
    m_previewLabel->setObjectName("previewLabel");
    QFont previewFont = defaultFont;
    previewFont.setPointSize(15);
    previewFont.setBold(true);
    m_previewLabel->setFont(previewFont);
  
    m_previewLabel->setWordWrap(true);
    m_previewLabel->setAlignment(Qt::AlignCenter);
    
    previewLayout->addWidget(m_previewLabel);
    mainLayout->addWidget(previewGroupBox);
    
    QHBoxLayout* buttonLayout = new QHBoxLayout();
    buttonLayout->setSpacing(15);
    
    m_confirmBtn = new QPushButton(tr("确定"), this);
    m_confirmBtn->setObjectName("confirmBtn");
    m_confirmBtn->setDefault(true);
    m_cancelBtn = new QPushButton(tr("取消"), this);
    m_cancelBtn->setObjectName("cancelBtn");
    
    // 设置按钮字体和样式
    QFont buttonFont = defaultFont;
    buttonFont.setPointSize(11);
    buttonFont.setBold(true);
    m_confirmBtn->setFont(buttonFont);
    m_cancelBtn->setFont(buttonFont);
    
    buttonLayout->addStretch();
    buttonLayout->addWidget(m_confirmBtn);
    buttonLayout->addWidget(m_cancelBtn);
    
    mainLayout->addLayout(buttonLayout);
}

void PrintConfigDialog::setupConnections()
{
    // 模式选择
    m_modeGroup = new QButtonGroup(this);
    m_modeGroup->addButton(m_continuousRadio);
    m_modeGroup->addButton(m_singleFileRadio);
    
    connect(m_modeGroup, QOverload<QAbstractButton*>::of(&QButtonGroup::buttonClicked),
            this, &PrintConfigDialog::onModeChanged);
    
    // 文件选择
    connect(m_fileListWidget, &QListWidget::currentItemChanged,
            this, &PrintConfigDialog::onFileSelectionChanged);
    
    // 打印次数
    connect(m_repeatCountSpinBox, QOverload<int>::of(&QSpinBox::valueChanged),
            this, &PrintConfigDialog::onRepeatCountChanged);
    
    // 按钮
    connect(m_confirmBtn, &QPushButton::clicked,
            this, &PrintConfigDialog::onConfirmClicked);
    connect(m_cancelBtn, &QPushButton::clicked,
            this, &PrintConfigDialog::onCancelClicked);
}

void PrintConfigDialog::populateFileList()
{
    m_fileListWidget->clear();
    
    for (int i = 0; i < m_fileList.size(); ++i) {
        QString fileName = m_fileList[i];
        QFileInfo fileInfo(fileName);
        QString displayName = fileInfo.fileName();
        
        QString itemText = QString("[%1] %2").arg(i + 1).arg(displayName);
        QListWidgetItem* item = new QListWidgetItem(itemText);
        item->setData(Qt::UserRole, i); 
        item->setToolTip(fileName); 
        
        QFont itemFont = m_fileListWidget->font();
        itemFont.setPointSize(11);
        item->setFont(itemFont);
        
        m_fileListWidget->addItem(item);
    }
  
    if (m_fileListWidget->count() > 0) {
        m_fileListWidget->setCurrentRow(0);
    }
}

void PrintConfigDialog::onModeChanged(QAbstractButton* button)
{
    if (button == m_continuousRadio) {
        m_printTask.mode = CONTINUOUS_MODE;
        m_configStack->setCurrentWidget(m_continuousWidget);
        
        // 更新文件数量显示
        m_fileCountLabel->setText(tr("共 %1 个文件").arg(m_fileList.size()));
        
    } else if (button == m_singleFileRadio) {
        m_printTask.mode = SINGLE_FILE_MODE;
        m_configStack->setCurrentWidget(m_singleFileWidget);
    }
    
    updatePrintPreview();
}

void PrintConfigDialog::onFileSelectionChanged()
{
    QListWidgetItem* currentItem = m_fileListWidget->currentItem();
    if (currentItem) {
        int fileIndex = currentItem->data(Qt::UserRole).toInt();
        if (fileIndex >= 0 && fileIndex < m_fileList.size()) {
            m_printTask.singleFile.selectedFileIndex = fileIndex;
            m_printTask.singleFile.selectedFileName = m_fileList[fileIndex];
            updatePrintPreview();
        }
    }
}

void PrintConfigDialog::onRepeatCountChanged(int value)
{
    m_printTask.singleFile.repeatCount = value;
    updatePrintPreview();
}

void PrintConfigDialog::updatePrintPreview()
{
    QString previewText;
    
    if (m_printTask.mode == CONTINUOUS_MODE) {
        previewText = tr("将连续打印所有 %1 个文件").arg(m_fileList.size());
    } else if (m_printTask.mode == SINGLE_FILE_MODE) {
        if (m_printTask.singleFile.selectedFileIndex >= 0 && 
            m_printTask.singleFile.selectedFileIndex < m_fileList.size()) {
            
            QString fileName = m_fileList[m_printTask.singleFile.selectedFileIndex];
            QFileInfo fileInfo(fileName);
            QString displayName = fileInfo.fileName();
            
            if (m_printTask.singleFile.repeatCount == 1) {
                previewText = tr("将打印：%1").arg(displayName);
            } else {
                previewText = tr("将打印：%1 × %2次").arg(displayName).arg(m_printTask.singleFile.repeatCount);
            }
        } else {
            previewText = tr("请选择一个文件");
        }
    }
    
    m_previewLabel->setText(previewText);
}

bool PrintConfigDialog::validateConfiguration()
{
    if (m_fileList.isEmpty()) {
        showError(tr("没有可打印的文件"));
        return false;
    }
    
    if (m_printTask.mode == SINGLE_FILE_MODE) {
        if (m_printTask.singleFile.selectedFileIndex < 0 || 
            m_printTask.singleFile.selectedFileIndex >= m_fileList.size()) {
            showError(tr("请选择一个有效的文件"));
            return false;
        }
        
        if (m_printTask.singleFile.repeatCount <= 0 || 
            m_printTask.singleFile.repeatCount > MAX_REPEAT_COUNT) {
            showError(tr("打印次数必须在 1-%1 之间").arg(MAX_REPEAT_COUNT));
            return false;
        }
    }
    
    return true;
}

void PrintConfigDialog::showError(const QString& message)
{
    MessageBoxHelper::showWarning(this, tr("配置错误"), message);
}

void PrintConfigDialog::onConfirmClicked()
{
    if (!validateConfiguration()) {
        return;
    }
    
    if (m_printTask.mode == CONTINUOUS_MODE) {
       
        QMessageBox::StandardButton result = MessageBoxHelper::showQuestion(
            this,
            tr("确认连续打印"),
            tr("确定要连续打印所有 %1 个文件吗？").arg(m_fileList.size())
        );
        
        if (result != QMessageBox::Yes) {
            return;
        }
        
        m_printTask.continuous.confirmExecution = true;
    }
    
    m_isConfirmed = true;
    accept();
}

void PrintConfigDialog::onCancelClicked()
{
    m_isConfirmed = false;
    reject();
} 