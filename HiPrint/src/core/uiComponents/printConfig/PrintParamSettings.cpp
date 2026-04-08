#include "PrintParamSettings.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QApplication>

PrintParamSettings::PrintParamSettings(QWidget *parent)
	: QWidget(parent)
	, m_currentProfileId(0)
	, m_isLoading(false)
{
	ui.setupUi(this);
	initializeUI();
	ProcessParamManager::instance().initTables();
	loadProfileList();
}

PrintParamSettings::~PrintParamSettings()
{
}

QString PrintParamSettings::getCurrentProfileName() const
{
	if (m_currentProfileId == 0) {
		return QString();
	}
	
	ProcessParamProfile profile = ProcessParamManager::instance().getProfile(m_currentProfileId);
	return profile.name;
}

ProcessParamProfile PrintParamSettings::getCurrentProfile() const
{
	if (m_currentProfileId == 0) {
		return ProcessParamProfile();
	}
	
	return ProcessParamManager::instance().getProfile(m_currentProfileId);
}

QMap<QString, QVariant> PrintParamSettings::getCurrentProfileParameters() const
{
	if (m_currentProfileId == 0) {
		return QMap<QString, QVariant>();
	}
	
	return ProcessParamManager::instance().getProfileParameters(m_currentProfileId);
}

void PrintParamSettings::setCurrentProfile(int profileId)
{
	if (profileId == m_currentProfileId) {
		return;
	}
	
	// 查找对应的列表项并选中
	for (int i = 0; i < ui.profileListWidget->count(); ++i) {
		QListWidgetItem* item = ui.profileListWidget->item(i);
		int itemProfileId = item->data(Qt::UserRole).toInt();
		if (itemProfileId == profileId) {
			ui.profileListWidget->setCurrentRow(i);
			break;
		}
	}
}

QVariant PrintParamSettings::getParameterValue(const QString& paramName, const QVariant& defaultValue) const
{
	if (m_currentProfileId == 0) {
		return defaultValue;
	}
	
	return ProcessParamManager::instance().getParameterValue(m_currentProfileId, paramName, defaultValue);
}

void PrintParamSettings::setParameterValue(const QString& paramName, const QVariant& value)
{
	if (m_currentProfileId == 0) {
		return;
	}
	
	ProcessParamManager::instance().setParameterValue(m_currentProfileId, paramName, value);
	
	// 更新UI显示
	loadProfileParameters(m_currentProfileId);
	
	// 发送参数值变化信号
	emit parameterValueChanged(paramName, value);
	
	// 启用保存和重置按钮
	ui.saveButton->setEnabled(true);
	ui.resetButton->setEnabled(true);
}

void PrintParamSettings::refreshProfileList()
{
	loadProfileList();
}

void PrintParamSettings::initializeUI()
{

	ui.paramTableWidget->setColumnCount(5);
	ui.paramTableWidget->setHorizontalHeaderLabels({"参数名称", "参数值", "单位", "范围", "描述"});
	
	QHeaderView* header = ui.paramTableWidget->horizontalHeader();
	header->setSectionResizeMode(0, QHeaderView::ResizeToContents);
	header->setSectionResizeMode(1, QHeaderView::Stretch);
	header->setSectionResizeMode(2, QHeaderView::ResizeToContents);
	header->setSectionResizeMode(3, QHeaderView::ResizeToContents);
	header->setSectionResizeMode(4, QHeaderView::Stretch);
	
	// 连接信号槽
	connect(ui.addProfileButton, &QPushButton::clicked, this, &PrintParamSettings::onAddProfileClicked);
	connect(ui.deleteProfileButton, &QPushButton::clicked, this, &PrintParamSettings::onDeleteProfileClicked);
	connect(ui.setDefaultButton, &QPushButton::clicked, this, &PrintParamSettings::onSetDefaultClicked);
	connect(ui.profileListWidget, &QListWidget::currentRowChanged, this, &PrintParamSettings::onProfileSelectionChanged);
	
	connect(ui.saveButton, &QPushButton::clicked, this, &PrintParamSettings::onSaveClicked);
	connect(ui.resetButton, &QPushButton::clicked, this, &PrintParamSettings::onResetClicked);
	
	connect(ui.importButton, &QPushButton::clicked, this, &PrintParamSettings::onImportClicked);
	connect(ui.exportButton, &QPushButton::clicked, this, &PrintParamSettings::onExportClicked);
	connect(ui.importDefaultButton, &QPushButton::clicked, this, &PrintParamSettings::onImportDefaultClicked);
	
	// 初始状态设置
	ui.deleteProfileButton->setEnabled(false);
	ui.setDefaultButton->setEnabled(false);
	ui.saveButton->setEnabled(false);
	ui.resetButton->setEnabled(false);
}

void PrintParamSettings::loadProfileList()
{
	ui.profileListWidget->clear();
	
	QList<ProcessParamProfile> profiles = ProcessParamManager::instance().getProfileList();
	for (const auto& profile : profiles) {
		QString displayText = profile.name;
		if (profile.isDefault) {
			displayText += " [默认]";
		}
		
		QListWidgetItem* item = new QListWidgetItem(displayText);
		item->setData(Qt::UserRole, profile.id);
		ui.profileListWidget->addItem(item);
	}
	
	if (profiles.isEmpty()) {
		ui.currentProfileLabel->setText("当前配置：无");
	} else {
		// 选择默认配置或第一个配置
		for (int i = 0; i < ui.profileListWidget->count(); ++i) {
			QListWidgetItem* item = ui.profileListWidget->item(i);
			int profileId = item->data(Qt::UserRole).toInt();
			ProcessParamProfile profile = ProcessParamManager::instance().getProfile(profileId);
			if (profile.isDefault) {
				ui.profileListWidget->setCurrentRow(i);
				break;
			}
		}
		if (ui.profileListWidget->currentRow() == -1) {
			ui.profileListWidget->setCurrentRow(0);
		}
	}
}

void PrintParamSettings::loadProfileParameters(int profileId)
{
	m_isLoading = true;
	
	// 清空表格
	ui.paramTableWidget->setRowCount(0);
	m_editorWidgets.clear();
	m_paramNames.clear();
	
	if (profileId == 0) {
		m_isLoading = false;
		return;
	}
	
	ProcessParamProfile profile = ProcessParamManager::instance().getProfile(profileId);
	QList<ProcessParamDefinition> paramDefs = ProcessParamManager::instance().getParameterDefinitions();
	
	ui.paramTableWidget->setRowCount(paramDefs.size());
	
	for (int i = 0; i < paramDefs.size(); ++i) {
		const ProcessParamDefinition& paramDef = paramDefs[i];
		QVariant value = profile.parameters.value(paramDef.name, paramDef.defaultValue);
		
		// 参数名称
		QTableWidgetItem* nameItem = new QTableWidgetItem(paramDef.displayName);
		nameItem->setFlags(nameItem->flags() & ~Qt::ItemIsEditable);
		ui.paramTableWidget->setItem(i, 0, nameItem);
		
		// 参数值（使用编辑器控件）
		createParameterEditor(i, paramDef, value);
		
		// 单位
		QTableWidgetItem* unitItem = new QTableWidgetItem(paramDef.unit);
		unitItem->setFlags(unitItem->flags() & ~Qt::ItemIsEditable);
		ui.paramTableWidget->setItem(i, 2, unitItem);
		
		// 范围
		QString rangeText;
		if (paramDef.type == PARAM_TYPE_BOOL) {
			rangeText = "是/否";
		} else {
			rangeText = QString("%1 - %2").arg(paramDef.minValue.toString()).arg(paramDef.maxValue.toString());
		}
		QTableWidgetItem* rangeItem = new QTableWidgetItem(rangeText);
		rangeItem->setFlags(rangeItem->flags() & ~Qt::ItemIsEditable);
		ui.paramTableWidget->setItem(i, 3, rangeItem);
		
		// 描述
		QTableWidgetItem* descItem = new QTableWidgetItem(paramDef.description);
		descItem->setFlags(descItem->flags() & ~Qt::ItemIsEditable);
		ui.paramTableWidget->setItem(i, 4, descItem);
		
		m_paramNames[i] = paramDef.name;
	}
	
	m_isLoading = false;
}

void PrintParamSettings::createParameterEditor(int row, const ProcessParamDefinition& paramDef, const QVariant& value)
{
	QWidget* editorWidget = createEditorWidget(row, paramDef, value);
	if (editorWidget) {
		ui.paramTableWidget->setCellWidget(row, 1, editorWidget);
		m_editorWidgets[row] = editorWidget;
	}
}

QWidget* PrintParamSettings::createEditorWidget(int row, const ProcessParamDefinition& paramDef, const QVariant& value)
{
	QWidget* container = new QWidget();
	QHBoxLayout* layout = new QHBoxLayout(container);
	layout->setContentsMargins(2, 2, 2, 2);
	layout->setSpacing(2);
	
	switch (paramDef.type) {
	case PARAM_TYPE_INT: {
		QSpinBox* spinBox = new QSpinBox();
		spinBox->setRange(paramDef.minValue.toInt(), paramDef.maxValue.toInt());
		spinBox->setValue(value.toInt());
		layout->addWidget(spinBox);
		
		connect(spinBox, QOverload<int>::of(&QSpinBox::valueChanged), [this, row](int value) {
			if (!m_isLoading) {
				updateParameterValue(row, value);
			}
		});
		break;
	}
	case PARAM_TYPE_FLOAT: {
		QDoubleSpinBox* spinBox = new QDoubleSpinBox();
		spinBox->setRange(paramDef.minValue.toDouble(), paramDef.maxValue.toDouble());
		spinBox->setDecimals(2);
		spinBox->setValue(value.toDouble());
		layout->addWidget(spinBox);
		
		connect(spinBox, QOverload<double>::of(&QDoubleSpinBox::valueChanged), [this, row](double value) {
			if (!m_isLoading) {
				updateParameterValue(row, value);
			}
		});
		break;
	}
	case PARAM_TYPE_BOOL: {
		QComboBox* comboBox = new QComboBox();
		comboBox->addItem("否", false);
		comboBox->addItem("是", true);
		comboBox->setCurrentIndex(value.toBool() ? 1 : 0);
		layout->addWidget(comboBox);
		
		connect(comboBox, QOverload<int>::of(&QComboBox::currentIndexChanged), [this, row, comboBox](int index) {
			if (!m_isLoading) {
				updateParameterValue(row, comboBox->itemData(index));
			}
		});
		break;
	}
	case PARAM_TYPE_STRING: {
		QLineEdit* lineEdit = new QLineEdit();
		lineEdit->setText(value.toString());
		layout->addWidget(lineEdit);
		
		connect(lineEdit, &QLineEdit::textChanged, [this, row](const QString& text) {
			if (!m_isLoading) {
				updateParameterValue(row, text);
			}
		});
		break;
	}
	}
	
	return container;
}

void PrintParamSettings::updateParameterValue(int row, const QVariant& value)
{
	if (m_currentProfileId == 0 || m_isLoading) {
		return;
	}
	
	QString paramName = m_paramNames.value(row);
	if (!paramName.isEmpty()) {
		ProcessParamManager::instance().setParameterValue(m_currentProfileId, paramName, value);
		
		// 发送参数值变化信号
		emit parameterValueChanged(paramName, value);
		
		ui.saveButton->setEnabled(true);
		ui.resetButton->setEnabled(true);
	}
}

void PrintParamSettings::onAddProfileClicked()
{
	bool ok;
	QString name = QInputDialog::getText(this, "添加配置", "请输入配置名称:", QLineEdit::Normal, "", &ok);
	if (ok && !name.isEmpty()) {
		QString description = QInputDialog::getText(this, "添加配置", "请输入配置描述:", QLineEdit::Normal, "", &ok);
		
		if (ProcessParamManager::instance().createProfile(name, description)) {
			loadProfileList();
			QMessageBox::information(this, "成功", "配置添加成功！");
		} else {
			QMessageBox::warning(this, "错误", "配置添加失败！");
		}
	}
}

void PrintParamSettings::onDeleteProfileClicked()
{
	QListWidgetItem* currentItem = ui.profileListWidget->currentItem();
	if (!currentItem) {
		return;
	}
	
	int profileId = currentItem->data(Qt::UserRole).toInt();
	ProcessParamProfile profile = ProcessParamManager::instance().getProfile(profileId);
	
	if (profile.isDefault) {
		QMessageBox::warning(this, "警告", "不能删除默认配置！");
		return;
	}
	
	QMessageBox::StandardButton reply = QMessageBox::question(this, "确认删除", 
		QString("确定要删除配置 '%1' 吗？").arg(profile.name),
		QMessageBox::Yes | QMessageBox::No);
	
	if (reply == QMessageBox::Yes) {
		if (ProcessParamManager::instance().deleteProfile(profileId)) {
			loadProfileList();
			QMessageBox::information(this, "成功", "配置删除成功！");
		} else {
			QMessageBox::warning(this, "错误", "配置删除失败！");
		}
	}
}

void PrintParamSettings::onSetDefaultClicked()
{
	QListWidgetItem* currentItem = ui.profileListWidget->currentItem();
	if (!currentItem) {
		return;
	}
	
	int profileId = currentItem->data(Qt::UserRole).toInt();
	
	if (ProcessParamManager::instance().setDefaultProfile(profileId)) {
		loadProfileList();
		QMessageBox::information(this, "成功", "默认配置设置成功！");
	} else {
		QMessageBox::warning(this, "错误", "默认配置设置失败！");
	}
}

void PrintParamSettings::onProfileSelectionChanged()
{
	QListWidgetItem* currentItem = ui.profileListWidget->currentItem();
	if (currentItem) {
		int profileId = currentItem->data(Qt::UserRole).toInt();
		m_currentProfileId = profileId;
		loadProfileParameters(profileId);
		updateCurrentProfileLabel();
		
		// 发送配置变化信号
		emit currentProfileChanged(profileId, getCurrentProfileName());
		
		ui.deleteProfileButton->setEnabled(true);
		ui.setDefaultButton->setEnabled(true);
		ui.saveButton->setEnabled(false);
		ui.resetButton->setEnabled(false);
	} else {
		m_currentProfileId = 0;
		ui.paramTableWidget->setRowCount(0);
		updateCurrentProfileLabel();
		
		// 发送配置变化信号
		emit currentProfileChanged(0, QString());
		
		ui.deleteProfileButton->setEnabled(false);
		ui.setDefaultButton->setEnabled(false);
		ui.saveButton->setEnabled(false);
		ui.resetButton->setEnabled(false);
	}
}

void PrintParamSettings::updateCurrentProfileLabel()
{
	if (m_currentProfileId == 0) {
		ui.currentProfileLabel->setText("当前配置：无");
	} else {
		ProcessParamProfile profile = ProcessParamManager::instance().getProfile(m_currentProfileId);
		QString text = QString("当前配置：%1").arg(profile.name);
		if (profile.isDefault) {
			text += " [默认]";
		}
		ui.currentProfileLabel->setText(text);
	}
}

void PrintParamSettings::onSaveClicked()
{
	if (m_currentProfileId == 0) {
		return;
	}
	
	// 保存当前配置
	saveCurrentProfile();
	QMessageBox::information(this, "成功", "配置保存成功！");
	
	ui.saveButton->setEnabled(false);
	ui.resetButton->setEnabled(false);
}

void PrintParamSettings::onResetClicked()
{
	if (m_currentProfileId == 0) {
		return;
	}
	
	QMessageBox::StandardButton reply = QMessageBox::question(this, "确认重置", 
		"确定要重置当前配置的参数值吗？",
		QMessageBox::Yes | QMessageBox::No);
	
	if (reply == QMessageBox::Yes) {
		loadProfileParameters(m_currentProfileId);
		ui.saveButton->setEnabled(false);
		ui.resetButton->setEnabled(false);
	}
}

void PrintParamSettings::saveCurrentProfile()
{
	// 参数值已经在编辑时自动保存到数据库
	// 这里只需要更新配置的修改时间
	ProcessParamProfile profile = ProcessParamManager::instance().getProfile(m_currentProfileId);
	ProcessParamManager::instance().updateProfile(m_currentProfileId, profile.name, profile.description);
}

void PrintParamSettings::onImportClicked()
{
	QString filePath = QFileDialog::getOpenFileName(this, "导入配置", "", "JSON文件 (*.json)");
	if (!filePath.isEmpty()) {
		if (ProcessParamManager::instance().importProfile(filePath)) {
			loadProfileList();
			QMessageBox::information(this, "成功", "配置导入成功！");
		} else {
			QMessageBox::warning(this, "错误", "配置导入失败！");
		}
	}
}

void PrintParamSettings::onExportClicked()
{
	if (m_currentProfileId == 0) {
		QMessageBox::warning(this, "警告", "请先选择一个配置！");
		return;
	}
	
	ProcessParamProfile profile = ProcessParamManager::instance().getProfile(m_currentProfileId);
	QString defaultName = QString("%1.json").arg(profile.name);
	QString filePath = QFileDialog::getSaveFileName(this, "导出配置", defaultName, "JSON文件 (*.json)");
	
	if (!filePath.isEmpty()) {
		if (ProcessParamManager::instance().exportProfile(m_currentProfileId, filePath)) {
			QMessageBox::information(this, "成功", "配置导出成功！");
		} else {
			QMessageBox::warning(this, "错误", "配置导出失败！");
		}
	}
}

void PrintParamSettings::onImportDefaultClicked()
{
	QString filePath = QFileDialog::getOpenFileName(this, "导入默认设置", "", "JSON文件 (*.json)");
	if (!filePath.isEmpty()) {
		if (ProcessParamManager::instance().importAllProfiles(filePath)) {
			loadProfileList();
			QMessageBox::information(this, "成功", "默认设置导入成功！");
		} else {
			QMessageBox::warning(this, "错误", "默认设置导入失败！");
		}
	}
}

