#pragma once

#include <QWidget>
#include <QListWidget>
#include <QTableWidget>
#include <QTableWidgetItem>
#include <QLineEdit>
#include <QSpinBox>
#include <QDoubleSpinBox>
#include <QComboBox>
#include <QCheckBox>
#include <QMessageBox>
#include <QFileDialog>
#include <QInputDialog>
#include "ui_PrintParamSettings.h"
#include "../../../common/global/ProcessParamManager.h"

class PrintParamSettings : public QWidget
{
	Q_OBJECT

public:
	PrintParamSettings(QWidget *parent = nullptr);
	~PrintParamSettings();

	// 公共接口：获取当前工艺参数配置
	int getCurrentProfileId() const { return m_currentProfileId; }
	QString getCurrentProfileName() const;
	ProcessParamProfile getCurrentProfile() const;
	QMap<QString, QVariant> getCurrentProfileParameters() const;
	
	// 公共接口：设置当前工艺参数配置
	void setCurrentProfile(int profileId);
	
	// 公共接口：获取特定参数值
	QVariant getParameterValue(const QString& paramName, const QVariant& defaultValue = QVariant()) const;
	
	// 公共接口：设置特定参数值
	void setParameterValue(const QString& paramName, const QVariant& value);
	
	// 公共接口：刷新工艺参数列表
	void refreshProfileList();

signals:
	// 信号：当前工艺参数配置发生变化
	void currentProfileChanged(int profileId, const QString& profileName);
	void parameterValueChanged(const QString& paramName, const QVariant& value);

private slots:
	// 工艺参数配置管理
	void onAddProfileClicked();
	void onDeleteProfileClicked();
	void onSetDefaultClicked();
	void onProfileSelectionChanged();
	
	// 参数编辑
	void onSaveClicked();
	void onResetClicked();
	
	// 导入导出
	void onImportClicked();
	void onExportClicked();
	void onImportDefaultClicked();

private:
	void initializeUI();
	void loadProfileList();
	void loadProfileParameters(int profileId);
	void saveCurrentProfile();
	void updateCurrentProfileLabel();
	void createParameterEditor(int row, const ProcessParamDefinition& paramDef, const QVariant& value);
	QWidget* createEditorWidget(int row, const ProcessParamDefinition& paramDef, const QVariant& value);
	void updateParameterValue(int row, const QVariant& value);
	
	// 参数编辑器控件
	QMap<int, QWidget*> m_editorWidgets;
	QMap<int, QString> m_paramNames;
	
	// 当前选中的配置
	int m_currentProfileId;
	bool m_isLoading;
	
	// UI界面
	Ui::PrintParamSettingsClass ui;
};

