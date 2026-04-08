#pragma once

#include <QAbstractItemModel>
#include <QFileInfo>
#include <QDir>
#include "TreeItem.h"
#include "../../common/logging/LogManager.h"

//自定义模型
class ImageTreeModel : public QAbstractItemModel {

    Q_OBJECT

public:
    ImageTreeModel(QObject* parent, const QString& path);
    ~ImageTreeModel();
    QModelIndex index(int row, int column, const QModelIndex& parent = QModelIndex()) const override;
    QModelIndex parent(const QModelIndex& index) const override;
    int rowCount(const QModelIndex& parent = QModelIndex()) const override;
    int columnCount(const QModelIndex&) const override;
    QVariant data(const QModelIndex& index, int role) const override;
    bool setData(const QModelIndex& index, const QVariant& value, int role = Qt::EditRole) override;
    QVariant headerData(int section, Qt::Orientation orientation, int role = Qt::DisplayRole) const override;
    Qt::ItemFlags flags(const QModelIndex& index) const override;
    bool canFetchMore(const QModelIndex& parent) const override;
    void fetchMore(const QModelIndex& parent) override;
    TreeItem* root();
    void updateImageStatus(int row, const QString& newStatus);
    void moveToNextImage(int row);
    void loadImageList(const QString& imageDirName);
    void clear();
    bool removeRow(int row, const QModelIndex& parent);
    QStringList getAllFilePaths() const;
   
private:
    void setupModelData(const QString& path, TreeItem* rootItem);
    TreeItem* rootItem;
    int batchSize = 10;  // 每次加载的文件数量
    QStringList pendingFiles;  // 待加载的文件列表
    QString dirPath;
};
