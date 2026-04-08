
#include <QHeaderView>

#include "PrintJobListPanel.h"

#include "../../../core/modelView/ImageTreeModel.h"
#include "../../../core/modelView/ImageDelegate.h"
#include "../../../common/global/ResourceManager.h"


PrintJobListPanel::PrintJobListPanel(QWidget* parent)
    : QWidget(parent)
{
    setupUi();
    setupConnections();
}

void PrintJobListPanel::setupUi()
{
    m_btnLoadFile = new QPushButton(this);
    m_btnLoadFolder = new QPushButton(this);
    m_btnDeleteCurrent = new QPushButton(this);
    m_btnClearAll = new QPushButton(this);

    m_btnLoadFile->setObjectName("btnLoadImage");
    m_btnLoadFolder->setObjectName("btnLoadFiles");
    m_btnDeleteCurrent->setObjectName("btnDeleteCurrent");
    m_btnClearAll->setObjectName("btnClearAll");

    m_btnLoadFile->setToolTip(LOAD_FILES);
    m_btnLoadFolder->setToolTip(LOAD_FILE_FOLDER);
    m_btnDeleteCurrent->setToolTip(DELETE_CURRENT_FILES);
    m_btnClearAll->setToolTip(CLEAR_CURRENT_FILE);

    m_btnLoadFile->setFixedSize(30, 30);
    m_btnLoadFolder->setFixedSize(30, 30);
    m_btnDeleteCurrent->setFixedSize(30, 30);
    m_btnClearAll->setFixedSize(30, 30);

    QHBoxLayout* btnLayout = new QHBoxLayout;
    btnLayout->addStretch();
    btnLayout->addWidget(m_btnLoadFile);
    btnLayout->addWidget(m_btnLoadFolder);
    btnLayout->addWidget(m_btnDeleteCurrent);
    btnLayout->addWidget(m_btnClearAll);

    m_treeView = new QTreeView(this);
    m_treeView->setMinimumWidth(350);
    m_treeView->setEditTriggers(QAbstractItemView::NoEditTriggers);
    m_treeView->setSelectionMode(QAbstractItemView::SingleSelection);
    m_treeView->header()->setStretchLastSection(true);

    QVBoxLayout* mainLayout = new QVBoxLayout(this);
    mainLayout->addLayout(btnLayout);
    mainLayout->addWidget(m_treeView);
    mainLayout->setContentsMargins(2, 2, 2, 2);
    setLayout(mainLayout);
}

void PrintJobListPanel::setupConnections()
{
    connect(m_btnLoadFile, &QPushButton::clicked, this, &PrintJobListPanel::sigLoadImage);
    connect(m_btnLoadFolder, &QPushButton::clicked, this, &PrintJobListPanel::sigLoadFiles);
    connect(m_btnDeleteCurrent, &QPushButton::clicked, this, &PrintJobListPanel::sigDeleteCurrent);
    connect(m_btnClearAll, &QPushButton::clicked, this, &PrintJobListPanel::sigClearAll);
    connect(m_treeView, &QTreeView::doubleClicked, this, &PrintJobListPanel::slotTreeViewDoubleClicked);

}

void PrintJobListPanel::setImageTreeModel(ImageTreeModel* model)
{
    m_treeView->setModel(model);
    m_treeView->setColumnWidth(0, 250);
    m_treeView->setColumnWidth(1, 50);
}

void PrintJobListPanel::setImageDelegate(ImageDelegate* delegate)
{
    m_treeView->setItemDelegateForColumn(0, delegate);
}

void PrintJobListPanel::slotTreeViewDoubleClicked(const QModelIndex& index)
{
    QString filePath = m_treeView->model()->data(index, Qt::DisplayRole).toString();
    emit sigImageItemDoubleClicked(filePath);
}