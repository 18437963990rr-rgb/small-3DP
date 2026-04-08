#include "ImageTreeModel.h"


ImageTreeModel::ImageTreeModel(QObject* parent ,const QString& path)
    : QAbstractItemModel(parent)
    , dirPath(path) 
{

    rootItem = new TreeItem("文件", "状态");  
}

ImageTreeModel::~ImageTreeModel() {
    delete rootItem;
}

QModelIndex ImageTreeModel::index(int row, int column, const QModelIndex& parent) const {
    if (!hasIndex(row, column, parent)) return QModelIndex();
    TreeItem* parentItem = parent.isValid() ? static_cast<TreeItem*>(parent.internalPointer()) : rootItem;
    TreeItem* childItem = parentItem->child(row);
    return childItem ? createIndex(row, column, childItem) : QModelIndex();
}

QModelIndex ImageTreeModel::parent(const QModelIndex& index) const {
    if (!index.isValid()) return QModelIndex();
    TreeItem* childItem = static_cast<TreeItem*>(index.internalPointer());
    TreeItem* parentItem = childItem->parent();
    return parentItem && parentItem != rootItem ? createIndex(parentItem->row(), 0, parentItem) : QModelIndex();
}

int ImageTreeModel::rowCount(const QModelIndex& parent) const {
    TreeItem* parentItem = parent.isValid() ? static_cast<TreeItem*>(parent.internalPointer()) : rootItem;
    return parentItem->childCount();
}

int ImageTreeModel::columnCount(const QModelIndex&) const {
    return 2;  // 图片路径列和状态列
}

QVariant ImageTreeModel::data(const QModelIndex& index, int role) const {
    if (!index.isValid()) return QVariant();
    TreeItem* item = static_cast<TreeItem*>(index.internalPointer());
    
    if (role == Qt::DisplayRole || role == Qt::DecorationRole) {
        return item->data(index.column());
    } 
    return QVariant();
}

bool ImageTreeModel::setData(const QModelIndex& index, const QVariant& value, int role) {
    if (!index.isValid())
        return false;
    TreeItem* item = static_cast<TreeItem*>(index.internalPointer());
    return false;
}

QVariant ImageTreeModel::headerData(int section, Qt::Orientation orientation, int role) const {
    if (orientation == Qt::Horizontal && role == Qt::DisplayRole)
        return rootItem->data(section);
    return QVariant();
}

Qt::ItemFlags ImageTreeModel::flags(const QModelIndex& index) const {
    if (!index.isValid())
        return Qt::NoItemFlags;  
    Qt::ItemFlags flags = QAbstractItemModel::flags(index);
    return flags | Qt::ItemIsSelectable;
}

TreeItem* ImageTreeModel::root() {
    return rootItem;
}

void ImageTreeModel::setupModelData(const QString& path, TreeItem* rootItem) {
    QFileInfo info(path);
    static const QStringList supportedFilters{ "*.tif", "*.tiff" };

    if (info.isFile()) {
        // 处理单个文件
        if (supportedFilters.contains("*." + info.suffix().toLower())) {
            pendingFiles.append(info.absoluteFilePath());
        }
    }
    else if (info.isDir()) {
        // 处理目录（非递归）
        QDir dir(path);
        QFileInfoList files = dir.entryInfoList(
            supportedFilters,
            QDir::Files | QDir::NoDotAndDotDot | QDir::Readable
        );

        for (const QFileInfo& file : files) {
            pendingFiles.append(file.absoluteFilePath());
        }
    }
  
    if (!pendingFiles.isEmpty()) {
        fetchMore(QModelIndex());
    }
}

bool ImageTreeModel::canFetchMore(const QModelIndex& parent) const {
    if (parent.isValid()) {
        return false;
    }
    return !pendingFiles.isEmpty();
}

void ImageTreeModel::fetchMore(const QModelIndex& parent) {
    if (parent.isValid()) {
        return;
    }
    int remainder = pendingFiles.size();
    int itemsToFetch = qMin(batchSize, remainder);
    if (itemsToFetch <= 0) {
        return;
    }
    beginInsertRows(parent, rootItem->childCount(),rootItem->childCount() + itemsToFetch - 1);
    for (int i = 0; i < itemsToFetch; ++i) {
        QString filePath = pendingFiles.takeFirst();
        rootItem->appendChild(new TreeItem(filePath, "未打印"));
    }
    endInsertRows();
}

void ImageTreeModel::updateImageStatus(int row, const QString& newStatus) {

    TreeItem* item = rootItem->child(row);
    if (item) {
        item->setStatus(newStatus);
        emit dataChanged(index(row, 1), index(row, 1));
    }
   
}

void ImageTreeModel::moveToNextImage(int row) {

    int nextRow = row + 1;
       // 检查是否需要加载更多数据
    if (nextRow >= rowCount() && canFetchMore(QModelIndex())) {
        // 加载下一批数据
        fetchMore(QModelIndex());
    }
    if (nextRow < rowCount()) {
        TreeItem* nextItem = rootItem->child(nextRow);
        if (nextItem) {
            nextItem->setStatus("等待打印");
            emit dataChanged(index(nextRow, 1), index(nextRow, 1));
        }
    }
}

void ImageTreeModel::loadImageList(const QString& imageDirName)
{
    beginResetModel();
    rootItem->clearChildren();
    pendingFiles.clear();
    setupModelData(imageDirName, rootItem);
    endResetModel();
}

void ImageTreeModel::clear()
{
    beginResetModel();
    rootItem->clearChildren();
    pendingFiles.clear();
    endResetModel();
}

bool ImageTreeModel::removeRow(int row, const QModelIndex& parent)
{
    if (parent.isValid()) {
        return false; // 不支持删除子项
    }
    
    if (row < 0 || row >= rootItem->childCount()) {
        return false;
    }
    
    beginRemoveRows(parent, row, row);
    rootItem->removeChild(row);
    endRemoveRows();
    
    return true;
}

QStringList ImageTreeModel::getAllFilePaths() const
{
    QStringList allFiles;
    if (!rootItem) {
        return allFiles;
    }
    for (int i = 0; i < rootItem->childCount(); ++i) {
        TreeItem* item = rootItem->child(i);
        if (item != nullptr) {
            QString filePath = item->data(0).toString();  // 第0列是文件路径
            if (!filePath.isEmpty()) {
                allFiles.append(filePath);
            }
        }
    }
    allFiles.append(pendingFiles);
    return allFiles;
}


