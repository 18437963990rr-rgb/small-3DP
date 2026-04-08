#pragma once

#include <QString>
#include <QList>
#include <QVariant>

class TreeItem {
public:
    TreeItem(const QString& imagePath, const QString& status, TreeItem* parent = nullptr);
    ~TreeItem();

    void appendChild(TreeItem* child);
    TreeItem* child(int row);
    int childCount() const;
    int columnCount() const;
    QVariant data(int column) const;
    TreeItem* parent();
    int row() const;
    void clearChildren();
    bool removeChild(int row);

    // 设置状态
    void setStatus(const QString& newStatus);

private:
    QString imagePath;
    QString status;
    QList<TreeItem*> childItems;
    TreeItem* parentItem;
};
