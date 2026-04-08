#ifndef PRINTJOBLISTPANEL_H
#define PRINTJOBLISTPANEL_H

#include <QWidget>
#include <QTreeView>
#include <QPushButton>
#include <QHBoxLayout>
#include <QVBoxLayout>
#include <QToolTip>

class ImageTreeModel;
class ImageDelegate;

class PrintJobListPanel : public QWidget
{
    Q_OBJECT
public:
    explicit PrintJobListPanel(QWidget* parent = nullptr);

    void setImageTreeModel(ImageTreeModel* model);
    void setImageDelegate(ImageDelegate* delegate);

    QTreeView* treeView() const { return m_treeView; }

signals:
    void sigShrinkAllFiles();
    void sigLoadImage();
    void sigLoadFiles();
    void sigDeleteCurrent();
    void sigClearAll();
    void sigDisplayImage(const QModelIndex&);
    void sigImageItemDoubleClicked(const QString& filePath);

public slots:
    void slotTreeViewDoubleClicked(const QModelIndex& index);

private:
    QTreeView* m_treeView;
    QPushButton* m_btnShrinkFiles;
    QPushButton* m_btnLoadFile;
    QPushButton* m_btnLoadFolder;
    QPushButton* m_btnDeleteCurrent;
    QPushButton* m_btnClearAll;

    void setupUi();
    void setupConnections();
};

#endif // PRINTJOBLISTPANEL_H

