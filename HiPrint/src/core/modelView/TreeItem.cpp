#include "TreeItem.h"

TreeItem::TreeItem(const QString& imagePath, const QString& status, TreeItem* parent)
    : imagePath(imagePath)
    , status(status) 
    , parentItem(parent) 
{
}

TreeItem::~TreeItem() {
    qDeleteAll(childItems);
}

void TreeItem::appendChild(TreeItem* child) {
    childItems.append(child);
}

TreeItem* TreeItem::child(int row) {
    return childItems.value(row);
}

int TreeItem::childCount() const {
    return childItems.count();
}

int TreeItem::columnCount() const {
    return 2; // 图片路径和状态
}

QVariant TreeItem::data(int column) const {
    if (column == 0) return imagePath; // 图片路径
    if (column == 1) return status;    // 图片状态
    return QVariant();
}

TreeItem* TreeItem::parent() {
    return parentItem;
}

int TreeItem::row() const {
    return parentItem ? parentItem->childItems.indexOf(const_cast<TreeItem*>(this)) : 0;
}

void TreeItem::setStatus(const QString& newStatus) {
    status = newStatus;
}

void TreeItem::clearChildren() {
    qDeleteAll(childItems);
    childItems.clear();
}

bool TreeItem::removeChild(int row) {
    if (row < 0 || row >= childItems.size()) {
        return false;
    }
    
    TreeItem* child = childItems.takeAt(row);
    delete child;
    return true;
}
