using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LaserAdd.SmallPrinter
{
    /// <summary>
    /// 新设备操作台。已接入固高初始化、遥测、安全停止、X/铺粉车点动、落粉 IO 和 Meteor；
    /// 自动整机流程仍需通过实机适配器接入。
    /// </summary>
    public sealed class SmallPrinterForm : Form
    {
        private static readonly Color PageBackground = Color.FromArgb(247, 249, 252);
        private static readonly Color PanelBackground = Color.White;
        private static readonly Color BorderColor = Color.FromArgb(222, 228, 236);
        private static readonly Color TextColor = Color.FromArgb(27, 50, 77);
        private static readonly Color Blue = Color.FromArgb(22, 118, 220);
        private static readonly Color Green = Color.FromArgb(15, 158, 79);
        private static readonly Color Red = Color.FromArgb(220, 60, 60);

        private SmallPrinterProfile _profile;
        private Button _scanModeButton;
        private NumericUpDown _feedDuration;
        private readonly Dictionary<string, TextBox> _motionEditors = new Dictionary<string, TextBox>();
        private readonly LayoutCanvas _layoutCanvas;
        private readonly ListBox _eventLog;
        private readonly Label _profileHint;
        private readonly Button _startButton;
        private Label _slicePreviewStatus, _jobNameValue, _jobLayerValue, _jobImageSizeValue, _jobDirectoryValue, _layerProgressText;
        private ProgressBar _layerProgressBar;
        private Button _previousSliceButton, _nextSliceButton;
        private int _currentSliceIndex = -1;
        private bool _jobImportRunning;
        private System.Threading.CancellationTokenSource _jobImportCancellation;
        private Label _headTemperatureReading, _headTemperatureDetail, _headVoltageReading, _headVoltageDetail;
        private Timer _headTelemetryTimer;
        private MeteorHeadTelemetryReader _headTelemetryReader;
        private MeteorControllerSession _meteorControllerSession;
        private string _lastTelemetryError;
        private readonly Dictionary<AxisRole, Label> _axisMonitorReadings = new Dictionary<AxisRole, Label>();
        private readonly Dictionary<AxisRole, Label> _axisMonitorStates = new Dictionary<AxisRole, Label>();
        private readonly Dictionary<AxisRole, Label> _manualAxisReadings = new Dictionary<AxisRole, Label>();
        private readonly Dictionary<AxisRole, Label> _manualAxisStates = new Dictionary<AxisRole, Label>();
        private Timer _gtsTelemetryTimer;
        private GtsTelemetryReader _gtsTelemetryReader;
        private GtsControllerSession _gtsControllerSession;
        private GtsMotionController _gtsMotionController;
        private string _lastGtsTelemetryError;
        private Panel _deviceStatusDot;
        private Label _deviceConnection, _deviceState;
        private Label _meteorPccState, _meteorHeadState, _meteorPowerState;
        private bool _gtsTelemetryAvailable, _anyGtsAxisMoving;
        private bool _powderOutputInitialized;
        private bool _powderHomeRunning;
        private System.Threading.CancellationTokenSource _powderHomeCancellation;
        private System.Threading.Thread _powderHomeThread;
        private bool _headCleaningRunning;
        private System.Threading.CancellationTokenSource _headCleaningCancellation;
        private System.Threading.Thread _headCleaningThread;
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly Dictionary<string, Button> _navigationButtons = new Dictionary<string, Button>();
        private Control _autoLayoutCard, _jobCard, _processStrategy, _manualAxes, _maintenanceCard, _recipeManagement, _logQuery, _overviewCard;

        public SliceTask CurrentSliceTask { get; private set; }
        public string CurrentSlicePath { get; private set; }

        public SmallPrinterForm()
        {
            Text = "LaserAdd 单 PASS 3D 打印控制台";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1280, 780);
            Size = new Size(1500, 920);
            BackColor = PageBackground;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageBackground, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildHeader(), 0, 0);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            body.Controls.Add(BuildNavigation(), 0, 0);
            body.Controls.Add(BuildMain(out _layoutCanvas, out _startButton), 1, 0);
            body.Controls.Add(BuildStatusPanel(out _eventLog, out _profileHint), 2, 0);
            root.Controls.Add(body, 0, 1);
            Controls.Add(root);
            ShowWorkspace("auto");
            Load += delegate { LoadProfile(); };
            FormClosed += delegate
            {
                if (_jobImportCancellation != null) _jobImportCancellation.Cancel();
                if (_layoutCanvas != null) _layoutCanvas.ClearRaster();
                if (_powderHomeCancellation != null) _powderHomeCancellation.Cancel();
                if (_headCleaningCancellation != null) _headCleaningCancellation.Cancel();
                if (_gtsMotionController != null) { try { _gtsMotionController.StopAll(); } catch { } }
                if (_powderHomeThread != null && _powderHomeThread.IsAlive) _powderHomeThread.Join(1000);
                if (_headCleaningThread != null && _headCleaningThread.IsAlive) _headCleaningThread.Join(1000);
                if (PowderFeedOutput.IsOpen) { try { PowderFeedOutput.Set(false); } catch { } }
                if (_gtsMotionController != null) { try { _gtsMotionController.SetDigitalOutput(OutputRole.PressInkEnable, false); } catch { } }
                if (_headTelemetryTimer != null) _headTelemetryTimer.Dispose();
                if (_gtsTelemetryTimer != null) _gtsTelemetryTimer.Dispose();
                if (_meteorControllerSession != null) _meteorControllerSession.Dispose();
                if (_gtsControllerSession != null) _gtsControllerSession.Dispose();
            };
        }

        private Control BuildHeader()
        {
            var panel = CreatePanel(); panel.Dock = DockStyle.Fill;
            var left = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 370, Padding = new Padding(14, 14, 0, 0) };
            _deviceStatusDot = CreateStatusDot(Color.Gray); left.Controls.Add(_deviceStatusDot);
            _deviceConnection = CreateLabel("固高未连接", 10F, TextColor, new Padding(7, 3, 20, 0)); left.Controls.Add(_deviceConnection);
            left.Controls.Add(CreateLabel("设备状态：", 10F, Color.DimGray, new Padding(0, 3, 0, 0)));
            _deviceState = CreateLabel("等待初始化", 10F, Color.DimGray, new Padding(4, 3, 0, 0)); left.Controls.Add(_deviceState);
            panel.Controls.Add(left);
            var modeState = CreateLabel("当前运行模式：自动 / 任务未启动", 10F, Blue, new Padding(14, 10, 14, 0));
            modeState.AutoSize = false; modeState.Size = new Size(290, 40); modeState.TextAlign = ContentAlignment.MiddleCenter;
            modeState.BorderStyle = BorderStyle.FixedSingle; modeState.Location = new Point(535, 10); panel.Controls.Add(modeState);
            var emergency = CreateButton("◉  急停", Red, Color.White, 178, 40); emergency.Anchor = AnchorStyles.Top | AnchorStyles.Right; emergency.Location = new Point(panel.Width - 198, 10);
            emergency.Click += delegate { StopActiveOperations(true); };
            panel.Resize += delegate { emergency.Left = panel.ClientSize.Width - emergency.Width - 16; }; panel.Controls.Add(emergency);
            return panel;
        }

        private Control BuildNavigation()
        {
            var panel = CreatePanel(); panel.Dock = DockStyle.Fill; panel.Margin = new Padding(0, 0, 12, 0);
            var nav = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8, 10, 8, 8), WrapContents = false };
            var auto = CreateNavigationButton("auto", "▷   自动打印");
            var manual = CreateNavigationButton("manual", "⊙   手动运动（六轴）");
            var process = CreateNavigationButton("process", "≡   工艺策略");
            var maintenance = CreateNavigationButton("maintenance", "⚒   喷头维护");
            nav.Controls.Add(auto); nav.Controls.Add(manual); nav.Controls.Add(process); nav.Controls.Add(maintenance);
            var recipes = CreateNavigationButton("recipes", "▤   配方管理");
            var logs = CreateNavigationButton("logs", "▧   日志查询");
            nav.Controls.Add(recipes); nav.Controls.Add(logs); panel.Controls.Add(nav);
            return panel;
        }

        private Button CreateNavigationButton(string page, string text)
        {
            Button button = CreateNavButton(text, false);
            _navigationButtons[page] = button;
            button.Click += delegate { ShowWorkspace(page); };
            return button;
        }

        private Control BuildMain(out LayoutCanvas layoutCanvas, out Button startButton)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = new Padding(0, 0, 12, 0) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            var layoutCard = CreatePanel(); layoutCard.Dock = DockStyle.Fill; layoutCard.Padding = new Padding(12, 10, 12, 10);
            var title = CreateLabel("当前层成型幅面  ·  100 × 60 mm", 12F, TextColor, new Padding(0, 0, 0, 4)); title.Dock = DockStyle.Top; title.Height = 28; layoutCard.Controls.Add(title);
            _slicePreviewStatus = CreateLabel("尚未导入切片任务", 8.5F, Color.DimGray, new Padding(0, 3, 0, 0)); _slicePreviewStatus.Dock = DockStyle.Bottom; _slicePreviewStatus.Height = 24; _slicePreviewStatus.AutoEllipsis = true; _slicePreviewStatus.AutoSize = false;
            var sliceNavigation = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            _previousSliceButton = CreateSmallButton("上一层"); _previousSliceButton.Width = 72; _previousSliceButton.Enabled = false; _previousSliceButton.Click += delegate { ShowSlice(_currentSliceIndex - 1); };
            _nextSliceButton = CreateSmallButton("下一层"); _nextSliceButton.Width = 72; _nextSliceButton.Enabled = false; _nextSliceButton.Click += delegate { ShowSlice(_currentSliceIndex + 1); };
            sliceNavigation.Controls.Add(_previousSliceButton); sliceNavigation.Controls.Add(_nextSliceButton);
            var canvas = new LayoutCanvas { Dock = DockStyle.Fill, Margin = Padding.Empty };
            layoutCanvas = canvas;
            layoutCard.Controls.Add(canvas); layoutCard.Controls.Add(sliceNavigation); layoutCard.Controls.Add(_slicePreviewStatus); _autoLayoutCard = layoutCard; root.Controls.Add(layoutCard, 0, 0);

            var jobCard = CreatePanel(); _jobCard = jobCard; jobCard.Dock = DockStyle.Fill; jobCard.Padding = new Padding(16, 10, 16, 10);
            var job = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; job.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280)); job.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 }; info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98)); info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _jobNameValue = AddInfo(info, 0, "当前任务：", "未导入"); _jobLayerValue = AddInfo(info, 1, "当前层：", "— / —"); _jobImageSizeValue = AddInfo(info, 2, "图像尺寸：", "—"); _jobDirectoryValue = AddInfo(info, 3, "任务目录：", "—"); _jobDirectoryValue.AutoEllipsis = true; _jobDirectoryValue.AutoSize = false; _jobDirectoryValue.Dock = DockStyle.Fill; job.Controls.Add(info, 0, 0);
            var progressArea = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 }; progressArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); progressArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); progressArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _layerProgressText = CreateLabel("切片任务尚未导入", 10F, Blue, Padding.Empty); progressArea.Controls.Add(_layerProgressText, 0, 0); _layerProgressBar = new ProgressBar { Value = 0, Maximum = 100, Dock = DockStyle.Fill, ForeColor = Blue, Style = ProgressBarStyle.Continuous }; progressArea.Controls.Add(_layerProgressBar, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) }; startButton = CreateButton("▶  开始", Blue, Color.White, 116, 38); startButton.Enabled = false; startButton.Click += delegate { AddLog("整机自动流程尚未接入，不能开始自动打印。", Red); }; actions.Controls.Add(startButton);
            var importTask = CreateButton("导入任务", Color.FromArgb(240, 246, 255), Blue, 100, 38); importTask.Click += delegate { ImportSliceTask(importTask); }; actions.Controls.Add(importTask);
            var pause = CreateButton("Ⅱ  暂停", Color.White, TextColor, 94, 38); DisableUntilConfigured(pause, "尚无可恢复的整机作业状态机，暂停功能暂不开放。"); actions.Controls.Add(pause);
            var stop = CreateButton("■  停止", Color.White, Red, 94, 38); stop.Click += delegate { StopActiveOperations(false); }; actions.Controls.Add(stop);
            var preflight = CreateButton("打印前检查", Color.FromArgb(240, 246, 255), Blue, 108, 38); preflight.Click += delegate { RunPreflightCheck(); }; actions.Controls.Add(preflight);
            var recipe = CreateButton("作业参数", Color.White, TextColor, 94, 38); recipe.Click += delegate { ShowWorkspace("process"); }; actions.Controls.Add(recipe);
            _scanModeButton = CreateButton("去程喷墨", Color.White, Blue, 110, 38);
            _scanModeButton.Click += delegate
            {
                if (_profile == null) return;
                var previous = _profile.DirectionMode;
                try
                {
                    _profile.DirectionMode = previous == SinglePassDirectionMode.PrintBothDirections ? SinglePassDirectionMode.PrintOutboundOnly : SinglePassDirectionMode.PrintBothDirections;
                    _profile.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmallPrinterProfile.json"));
                    _scanModeButton.Text = _profile.DirectionMode == SinglePassDirectionMode.PrintBothDirections ? "往返喷墨" : "去程喷墨";
                    AddLog("已保存：" + _scanModeButton.Text + "，下一次打印生效。", Blue);
                }
                catch (Exception ex) { _profile.DirectionMode = previous; AddLog("保存喷印模式失败：" + ex.Message, Red); }
            };
            actions.Controls.Add(_scanModeButton);
            var feed = CreateButton("落粉开/关", Color.White, TextColor, 110, 38);
            feed.Click += delegate
            {
                try
                {
                    if ((_gtsControllerSession == null || !_gtsControllerSession.IsInitialized) && !PowderFeedOutput.IsOpen)
                        throw new InvalidOperationException("固高未完成打开、复位和 CFG 加载，不能开启落粉。");
                    PowderFeedOutput.Set(!PowderFeedOutput.IsOpen);
                    feed.Text = PowderFeedOutput.IsOpen ? "关闭落粉" : "开启落粉";
                    AddLog("EXO10 落粉" + (PowderFeedOutput.IsOpen ? "已开启" : "已关闭"), Blue);
                }
                catch (Exception ex) { AddLog(ex.Message, Red); }
            };
            actions.Controls.Add(feed);
            var safe = CreateButton("安全位", Color.White, TextColor, 78, 38); DisableUntilConfigured(safe, "各轴回零状态和安全移动顺序尚未确认。"); actions.Controls.Add(safe);
            progressArea.Controls.Add(actions, 0, 2); job.Controls.Add(progressArea, 1, 0); jobCard.Controls.Add(job); root.Controls.Add(jobCard, 0, 1);

            var processPage = new Panel { Dock = DockStyle.Fill, BackColor = PageBackground };
            _processStrategy = BuildProcessStrategy(); processPage.Controls.Add(_processStrategy);
            root.Controls.Add(processPage, 0, 0); root.SetRowSpan(processPage, 3);

            var axes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 10, 0, 0) }; for (int i = 0; i < 3; i++) axes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F)); axes.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); axes.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            axes.Controls.Add(CreateAxisCard(AxisRole.InkCarX, "墨车 X 向", true), 0, 0); axes.Controls.Add(CreateAxisCard(AxisRole.Scraper, "刮板", true), 1, 0); axes.Controls.Add(CreateAxisCard(AxisRole.PowderCar, "铺粉车", true), 2, 0); axes.Controls.Add(CreateAxisCard(AxisRole.RollerFront, "前粉辊", false), 0, 1); axes.Controls.Add(CreateAxisCard(AxisRole.RollerRear, "后粉辊", false), 1, 1); axes.Controls.Add(CreateAxisCard(AxisRole.BuildCylinder, "成型缸", false), 2, 1);
            var manualPage = new Panel { Dock = DockStyle.Fill, BackColor = PageBackground }; var manualBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 0, 0, 6), WrapContents = false };
            var homeAll = CreateButton("⌂ 全轴回零", Blue, Color.White, 116, 32); DisableUntilConfigured(homeAll, "各轴回零方向、脱限距离和零点偏移尚未确认。"); manualBar.Controls.Add(homeAll);
            var jogHint = CreateButton("按住点动", Color.White, TextColor, 92, 32); DisableUntilConfigured(jogHint, "X 与铺粉车：单箭头低速，双箭头全速，松开即停。"); manualBar.Controls.Add(jogHint);
            var speedHint = CreateButton("＜低 / ≪高", Color.White, TextColor, 98, 32); DisableUntilConfigured(speedHint, "低速为配置速度的 25%，高速为配置速度的 100%。"); manualBar.Controls.Add(speedHint);
            var refreshGts = CreateButton("读取轴状态", Color.FromArgb(240, 246, 255), Blue, 100, 32); refreshGts.Click += delegate { RefreshGtsTelemetry(); }; manualBar.Controls.Add(refreshGts);
            var powderOnly = CreateButton("单独铺粉", Color.FromArgb(240, 246, 255), Blue, 96, 32); DisableUntilConfigured(powderOnly, "粉辊、刮板与成型缸的步进参数和联锁尚未确认。"); manualBar.Controls.Add(powderOnly);
            var printOnly = CreateButton("单独打印", Color.FromArgb(240, 246, 255), Blue, 96, 32); DisableUntilConfigured(printOnly, "需先完成墨车运动与 Meteor 扫描触发的整机联调。"); manualBar.Controls.Add(printOnly);
            var singleLayer = CreateButton("单层铺粉 + 打印", Color.White, TextColor, 138, 32); DisableUntilConfigured(singleLayer, "单层状态机和所有轴安全联锁尚未完成。"); manualBar.Controls.Add(singleLayer);
            manualPage.Controls.Add(axes); manualPage.Controls.Add(manualBar); axes.Dock = DockStyle.Fill; axes.Padding = new Padding(0, 42, 0, 0); _manualAxes = manualPage; root.Controls.Add(manualPage, 0, 0); root.SetRowSpan(manualPage, 3);
            _maintenanceCard = BuildMaintenancePage(); root.Controls.Add(_maintenanceCard, 0, 0); root.SetRowSpan(_maintenanceCard, 3);
            _recipeManagement = BuildRecipeManagementPage(); root.Controls.Add(_recipeManagement, 0, 0); root.SetRowSpan(_recipeManagement, 3);
            _logQuery = BuildLogQueryPage(); root.Controls.Add(_logQuery, 0, 0); root.SetRowSpan(_logQuery, 3);
            var overview = CreatePanel(); overview.Dock = DockStyle.Fill; overview.Padding = new Padding(24); var heading = CreateLabel("设备总览 · 实时监测", 16F, TextColor, Padding.Empty); heading.Dock = DockStyle.Top; overview.Controls.Add(heading); var message = CreateLabel("六轴状态、位置、回零与限位状态将在此集中显示。\r\n\r\n温度、电压、气压、报警和事件日志位于右侧全局监测栏。\r\n\r\n操作入口请切换至“自动打印”或“手动运动”。", 11F, Color.DimGray, new Padding(0, 32, 0, 0)); message.Dock = DockStyle.Fill; overview.Controls.Add(message); _overviewCard = overview; root.Controls.Add(overview, 0, 0); root.SetRowSpan(overview, 3); overview.BringToFront();
            return root;
        }

        private void ShowWorkspace(string page)
        {
            if (_overviewCard == null) return;
            foreach (KeyValuePair<string, Button> item in _navigationButtons)
            {
                bool selected = String.Equals(item.Key, page, StringComparison.OrdinalIgnoreCase);
                item.Value.BackColor = selected ? Color.FromArgb(240, 246, 255) : Color.White;
                item.Value.ForeColor = selected ? Blue : TextColor;
                item.Value.FlatAppearance.BorderColor = selected ? Color.FromArgb(180, 207, 248) : Color.White;
                item.Value.Font = new Font("Microsoft YaHei UI", 10F, selected ? FontStyle.Bold : FontStyle.Regular);
            }
            _overviewCard.Visible = false;
            _autoLayoutCard.Visible = page == "auto";
            _jobCard.Visible = page == "auto";
            _processStrategy.Parent.Visible = page == "process";
            _manualAxes.Visible = page == "manual";
            _maintenanceCard.Visible = page == "maintenance";
            _recipeManagement.Visible = page == "recipes";
            _logQuery.Visible = page == "logs";
        }

        private Control BuildProcessStrategy()
        {
            var card = CreatePanel(); card.Dock = DockStyle.Fill; card.Padding = new Padding(16, 12, 16, 12);
            var title = CreateLabel("工艺策略", 12F, TextColor, Padding.Empty); title.Dock = DockStyle.Top; title.Height = 24;
            var hint = CreateLabel("当前配方：默认单 PASS 工艺  ·  修改后需保存并在打印前检查中确认", 8.8F, Color.DimGray, Padding.Empty); hint.Dock = DockStyle.Top; hint.Height = 22;
            var save = CreateButton("保存当前工艺", Blue, Color.White, 118, 32); save.Dock = DockStyle.Right;
            save.Text = "保存运动设置";
            save.Click += delegate { SaveMotionSettings(); };

            var values = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(0, 8, 132, 0) };
            for (var i = 0; i < 4; i++) values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            for (int row = 0; row < 3; row++) values.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            values.Controls.Add(CreateProcessField("铺粉全程速度", "50", "mm/s", true), 0, 0);
            values.Controls.Add(CreateProcessField("前辊速度", "320", "rpm", false), 1, 0);
            values.Controls.Add(CreateProcessField("后辊速度", "280", "rpm", false), 2, 0);
            values.Controls.Add(CreateProcessField("红外固化功率", "65", "%", false), 3, 0);
            values.Controls.Add(CreateDirectionField("前辊旋转方向", "正转", false), 0, 1);
            values.Controls.Add(CreateDirectionField("后辊旋转方向", "反转", false), 1, 1);
            values.Controls.Add(CreateProcessField("层厚", "0.10", "mm", false), 2, 1);
            values.Controls.Add(CreateProcessField("墨车全程速度", "100", "mm/s", true), 3, 1);
            var feedField = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            feedField.Controls.Add(CreateLabel("落粉开启时间（ms，0=未设置）", 8.5F, Color.DimGray, Padding.Empty));
            _feedDuration = new NumericUpDown { Minimum = 0, Maximum = 60000, Increment = 100, Width = 120 };
            feedField.Controls.Add(_feedDuration); values.Controls.Add(feedField, 0, 2); values.SetColumnSpan(feedField, 2);

            card.Controls.Add(values); card.Controls.Add(save); card.Controls.Add(hint); card.Controls.Add(title);
            return card;
        }

        private void SaveMotionSettings()
        {
            if (_profile == null) return;
            double ink, powder;
            if (!Double.TryParse(_motionEditors["墨车全程速度"].Text, out ink) || !Double.TryParse(_motionEditors["铺粉全程速度"].Text, out powder)
                || Double.IsNaN(ink) || Double.IsInfinity(ink) || Double.IsNaN(powder) || Double.IsInfinity(powder) || ink <= 0 || powder <= 0)
            { AddLog("速度必须为有效正数。", Red); return; }
            var inkAxis = _profile.GetAxis(AxisRole.InkCarX);
            var powderAxis = _profile.GetAxis(AxisRole.PowderCar);
            double oldInk = inkAxis.VelocityMmPerSecond, oldPowder = powderAxis.VelocityMmPerSecond;
            int oldDuration = _profile.PowderFeedDurationMs;
            try
            {
                inkAxis.VelocityMmPerSecond = ink; powderAxis.VelocityMmPerSecond = powder;
                _profile.PowderFeedDurationMs = (int)_feedDuration.Value;
                _profile.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmallPrinterProfile.json"));
                AddLog("已保存墨车/铺粉全程速度及落粉开启时间；下一次打印生效。", Blue);
            }
            catch (Exception ex)
            { inkAxis.VelocityMmPerSecond = oldInk; powderAxis.VelocityMmPerSecond = oldPowder; _profile.PowderFeedDurationMs = oldDuration; AddLog(ex.Message, Red); }
        }

        private Control CreateProcessField(string label, string value, string unit, bool enabled)
        {
            var field = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, 0, 10, 0) };
            field.Controls.Add(CreateLabel(label, 8.5F, Color.DimGray, Padding.Empty));
            var editor = new FlowLayoutPanel { Height = 30, Width = 160, WrapContents = false, Margin = new Padding(0, 2, 0, 0) };
            var text = new TextBox { Text = value, Width = 92, Height = 27, TextAlign = HorizontalAlignment.Center, Font = new Font("Microsoft YaHei UI", 9F) };
            text.ReadOnly = !enabled;
            if (!enabled) { text.BackColor = Color.FromArgb(242, 244, 247); _toolTip.SetToolTip(text, "该参数尚未接入设备配置。"); }
            _motionEditors[label] = text;
            editor.Controls.Add(text);
            editor.Controls.Add(CreateLabel(unit, 8.5F, Color.DimGray, new Padding(5, 5, 0, 0)));
            field.Controls.Add(editor);
            return field;
        }

        private Control CreateDirectionField(string label, string current, bool enabled)
        {
            var field = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, 0, 10, 0) };
            field.Controls.Add(CreateLabel(label, 8.5F, Color.DimGray, Padding.Empty));
            var direction = new ComboBox { Width = 116, Height = 27, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 9F), Margin = new Padding(0, 2, 0, 0) };
            direction.Items.AddRange(new object[] { "正转", "反转" }); direction.SelectedItem = current; direction.Enabled = enabled; if (!enabled) _toolTip.SetToolTip(direction, "粉辊步进方向尚未确认。"); field.Controls.Add(direction);
            return field;
        }

        private Control BuildRecipeManagementPage()
        {
            var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = PageBackground };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310)); page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = CreateLabel("配方管理", 15F, TextColor, new Padding(0, 11, 0, 0)); heading.Dock = DockStyle.Fill; page.Controls.Add(heading, 0, 0); page.SetColumnSpan(heading, 2);

            var listCard = CreatePanel(); listCard.Dock = DockStyle.Fill; listCard.Margin = new Padding(0, 0, 12, 0); listCard.Padding = new Padding(12);
            var listTitle = CreateLabel("工艺配方", 11F, TextColor, Padding.Empty); listTitle.Dock = DockStyle.Top; listTitle.Height = 28;
            var recipeList = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = PanelBackground, ForeColor = TextColor, Font = new Font("Microsoft YaHei UI", 9F) };
            recipeList.Items.AddRange(new object[] { "● 默认单 PASS 工艺   v1.2", "  精细层厚 0.08 mm   v1.0", "  高效率铺粉         v0.8（草稿）", "  材料 A 验证配方    v1.1" });
            var listActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, WrapContents = false };
            var newRecipe = CreateButton("+ 新建", Blue, Color.White, 72, 30); DisableUntilConfigured(newRecipe, "配方数据模型尚未接入。"); listActions.Controls.Add(newRecipe);
            var copyRecipe = CreateButton("复制", Color.White, TextColor, 60, 30); DisableUntilConfigured(copyRecipe, "配方数据模型尚未接入。"); listActions.Controls.Add(copyRecipe);
            var importRecipe = CreateButton("导入", Color.White, TextColor, 60, 30); DisableUntilConfigured(importRecipe, "配方文件格式和校验规则尚未确认。"); listActions.Controls.Add(importRecipe);
            listCard.Controls.Add(recipeList); listCard.Controls.Add(listActions); listCard.Controls.Add(listTitle); page.Controls.Add(listCard, 0, 1);

            var detail = CreatePanel(); detail.Dock = DockStyle.Fill; detail.Padding = new Padding(18, 14, 18, 14);
            var detailTitle = CreateLabel("默认单 PASS 工艺  ·  v1.2", 13F, TextColor, Padding.Empty); detailTitle.Dock = DockStyle.Top; detailTitle.Height = 30;
            var meta = CreateLabel("状态：已发布     修改时间：2026-08-19 10:24     适用设备：小型单 PASS 设备", 8.8F, Color.DimGray, Padding.Empty); meta.Dock = DockStyle.Top; meta.Height = 26;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, WrapContents = false };
            var apply = CreateButton("应用到当前作业", Blue, Color.White, 126, 32); DisableUntilConfigured(apply, "当前列表是界面样例，尚未接入真实配方。"); actions.Controls.Add(apply);
            var saveAs = CreateButton("另存为新版本", Color.White, TextColor, 112, 32); DisableUntilConfigured(saveAs, "配方版本管理尚未接入。"); actions.Controls.Add(saveAs);
            var exportRecipe = CreateButton("导出", Color.White, TextColor, 64, 32); DisableUntilConfigured(exportRecipe, "配方导出格式尚未确认。"); actions.Controls.Add(exportRecipe);
            var groups = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
            groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); groups.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); groups.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            groups.Controls.Add(CreateRecipeSummaryCard("铺粉与辊组", "铺粉速度 180 mm/s\r\n前辊 320 rpm · 正转\r\n后辊 280 rpm · 反转"), 0, 0);
            groups.Controls.Add(CreateRecipeSummaryCard("层与固化", "层厚 0.10 mm\r\n红外固化功率 65 %\r\n单 PASS 固定路径"), 1, 0);
            groups.Controls.Add(CreateRecipeSummaryCard("喷印设置", "喷头数量：配置读取\r\n分辨率：配置读取\r\n墨车 X：单 PASS"), 0, 1);
            groups.Controls.Add(CreateRecipeSummaryCard("校验与说明", "参数范围校验：通过\r\n发布后不可直接修改\r\n备注：标准材料工艺"), 1, 1);
            detail.Controls.Add(groups); detail.Controls.Add(actions); detail.Controls.Add(meta); detail.Controls.Add(detailTitle); page.Controls.Add(detail, 1, 1);
            return page;
        }

        private Control CreateRecipeSummaryCard(string title, string content)
        {
            var card = CreatePanel(); card.Dock = DockStyle.Fill; card.Margin = new Padding(0, 0, 10, 10); card.Padding = new Padding(14, 12, 14, 10);
            var heading = CreateLabel(title, 10F, TextColor, Padding.Empty); heading.Dock = DockStyle.Top; heading.Height = 24;
            var body = CreateLabel(content, 9F, Color.DimGray, Padding.Empty); body.Dock = DockStyle.Fill;
            card.Controls.Add(body); card.Controls.Add(heading); return card;
        }

        private Control BuildLogQueryPage()
        {
            var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = PageBackground };
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = CreateLabel("日志查询", 15F, TextColor, new Padding(0, 11, 0, 0)); heading.Dock = DockStyle.Fill; page.Controls.Add(heading, 0, 0);
            var filter = CreatePanel(); filter.Dock = DockStyle.Fill; filter.Padding = new Padding(12, 8, 12, 6);
            var filterItems = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            filterItems.Controls.Add(CreateLabel("时间范围", 8.5F, Color.DimGray, new Padding(0, 7, 5, 0)));
            var range = new ComboBox { Width = 118, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 9F) }; range.Items.AddRange(new object[] { "最近 24 小时", "最近 7 天", "自定义…" }); range.SelectedIndex = 0; filterItems.Controls.Add(range);
            filterItems.Controls.Add(CreateLabel("级别", 8.5F, Color.DimGray, new Padding(16, 7, 5, 0)));
            var level = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 9F) }; level.Items.AddRange(new object[] { "全部", "信息", "警告", "报警" }); level.SelectedIndex = 0; filterItems.Controls.Add(level);
            filterItems.Controls.Add(CreateLabel("模块", 8.5F, Color.DimGray, new Padding(16, 7, 5, 0)));
            var module = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 9F) }; module.Items.AddRange(new object[] { "全部模块", "运动控制", "喷墨系统", "安全联锁", "工艺执行" }); module.SelectedIndex = 0; filterItems.Controls.Add(module);
            var query = CreateButton("查询", Blue, Color.White, 70, 30); DisableUntilConfigured(query, "持久化日志源尚未接入。"); filterItems.Controls.Add(query);
            var exportCsv = CreateButton("导出 CSV", Color.White, TextColor, 82, 30); DisableUntilConfigured(exportCsv, "持久化日志源尚未接入。"); filterItems.Controls.Add(exportCsv);
            filter.Controls.Add(filterItems); page.Controls.Add(filter, 0, 1);
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 10, 0, 0) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            var tableCard = CreatePanel(); tableCard.Dock = DockStyle.Fill; tableCard.Margin = new Padding(0, 0, 12, 0); tableCard.Padding = new Padding(12);
            var entries = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = PanelBackground, ForeColor = TextColor, Font = new Font("Microsoft YaHei UI", 9F) };
            entries.Items.AddRange(new object[] { "10:28:41   信息   工艺执行     当前层 128 铺粉完成", "10:28:36   信息   运动控制     铺粉车到达工作位置", "10:27:58   信息   喷墨系统     Meteor 状态读取成功", "10:25:14   警告   安全联锁     气压短时波动：0.58 MPa", "10:24:02   信息   配方管理     已应用默认单 PASS 工艺 v1.2" });
            tableCard.Controls.Add(entries); content.Controls.Add(tableCard, 0, 0);
            var detail = CreatePanel(); detail.Dock = DockStyle.Fill; detail.Padding = new Padding(14, 12, 14, 12);
            var detailTitle = CreateLabel("事件详情", 11F, TextColor, Padding.Empty); detailTitle.Dock = DockStyle.Top; detailTitle.Height = 28;
            var detailText = CreateLabel("选择一条日志后显示完整内容。\r\n\r\n示例：\r\n类型：警告\r\n模块：安全联锁\r\n内容：气压在 0.58 MPa 持续 2 秒后恢复。\r\n处理状态：已恢复，待确认。", 9F, Color.DimGray, Padding.Empty); detailText.Dock = DockStyle.Fill;
            var confirm = CreateButton("确认报警", Color.FromArgb(240, 246, 255), Blue, 92, 30); DisableUntilConfigured(confirm, "报警源及确认回写接口尚未接入。"); confirm.Dock = DockStyle.Bottom; detail.Controls.Add(confirm); detail.Controls.Add(detailText); detail.Controls.Add(detailTitle); content.Controls.Add(detail, 1, 0);
            page.Controls.Add(content, 0, 2); return page;
        }

        private Control BuildMaintenancePage()
        {
            var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = PageBackground, Padding = new Padding(0), AutoScroll = true };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 176)); page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = CreateLabel("喷头维护 · 工程师模式", 15F, TextColor, new Padding(0, 10, 0, 0)); heading.Dock = DockStyle.Fill; page.Controls.Add(heading, 0, 0); page.SetColumnSpan(heading, 2);

            var health = CreatePanel(); health.Dock = DockStyle.Fill; health.Margin = new Padding(0, 0, 10, 10); health.Padding = new Padding(16, 12, 16, 10);
            var healthTitle = CreateLabel("Meteor 与喷头状态", 11F, TextColor, Padding.Empty); healthTitle.Dock = DockStyle.Top; health.Controls.Add(healthTitle);
            var status = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(0, 10, 0, 0) };
            for (int row = 0; row < 3; row++) status.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
            _meteorPccState = CreateLabel("●  PCC：待读取", 9F, Color.DimGray, new Padding(0, 0, 18, 0));
            _meteorHeadState = CreateLabel("●  HDC：待读取", 9F, Color.DimGray, new Padding(0, 0, 18, 0));
            _meteorPowerState = CreateLabel("●  喷头电源：未知", 9F, Color.DimGray, Padding.Empty);
            foreach (Label state in new[] { _meteorPccState, _meteorHeadState, _meteorPowerState }) { state.AutoSize = false; state.Dock = DockStyle.Fill; state.AutoEllipsis = true; state.TextAlign = ContentAlignment.MiddleLeft; }
            status.Controls.Add(_meteorPccState, 0, 0); status.Controls.Add(_meteorHeadState, 0, 1); status.Controls.Add(_meteorPowerState, 0, 2); health.Controls.Add(status); page.Controls.Add(health, 0, 1);

            var wave = CreatePanel(); wave.Dock = DockStyle.Fill; wave.Margin = new Padding(0, 0, 0, 10); wave.Padding = new Padding(16, 12, 16, 10);
            var waveTitle = CreateLabel("波形与配置", 11F, TextColor, Padding.Empty); waveTitle.Dock = DockStyle.Top; wave.Controls.Add(waveTitle);
            var waveDetail = CreateLabel("运行波形由 Meteor cfg / .rhdat 在 PrintEngine 启动时加载。\r\n当前：ricoh_phcfg16.rhdat（待实际读取）", 9F, Color.DimGray, new Padding(0, 8, 0, 0)); waveDetail.AutoSize = false; waveDetail.Dock = DockStyle.Fill; waveDetail.AutoEllipsis = true; wave.Controls.Add(waveDetail);
            var waveActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var reload = CreateButton("受控重新加载", Color.White, TextColor, 112, 30); DisableUntilConfigured(reload, "需要明确 PrintEngine 安全重启及配置校验流程。"); waveActions.Controls.Add(reload); wave.Controls.Add(waveActions); page.Controls.Add(wave, 1, 1);

            var operations = CreatePanel(); operations.Dock = DockStyle.Fill; operations.Margin = new Padding(0, 0, 10, 0); operations.Padding = new Padding(16, 14, 16, 14);
            var operationTitle = CreateLabel("维护操作", 11F, TextColor, Padding.Empty); operationTitle.Dock = DockStyle.Top; operations.Controls.Add(operationTitle);
            var operationHint = CreateLabel("仅在维护模式、运动停止且安全联锁正常时可执行。", 9F, Color.DimGray, new Padding(0, 5, 0, 8)); operationHint.Dock = DockStyle.Top; operations.Controls.Add(operationHint);
            var operationButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(0, 4, 0, 0) };
            var powerOn = CreateButton("喷头上电", Blue, Color.White, 104, 34); powerOn.Click += delegate { RequestHeadPower(true); }; operationButtons.Controls.Add(powerOn);
            var powerOff = CreateButton("喷头断电", Color.White, Red, 104, 34); powerOff.Click += delegate { RequestHeadPower(false); }; operationButtons.Controls.Add(powerOff);
            var flash = CreateButton("闪喷 200", Color.FromArgb(240, 246, 255), Blue, 102, 34); flash.Click += delegate { RequestSpit(); }; operationButtons.Controls.Add(flash);
            var clean = CreateButton("自动刮墨", Color.FromArgb(240, 246, 255), Blue, 102, 34); clean.Click += delegate { StartHeadCleaning(clean); }; operationButtons.Controls.Add(clean);
            var test = CreateButton("测试条打印", Color.White, TextColor, 112, 34); DisableUntilConfigured(test, "单 PASS 测试图、墨车运动和互锁流程尚未完成联调。"); operationButtons.Controls.Add(test); operations.Controls.Add(operationButtons); page.Controls.Add(operations, 0, 2);

            var parameters = CreatePanel(); parameters.Dock = DockStyle.Fill; parameters.Padding = new Padding(16, 14, 16, 14);
            var parameterTitle = CreateLabel("打印配置来源（只读）", 11F, TextColor, Padding.Empty); parameterTitle.Dock = DockStyle.Top; parameterTitle.Height = 28; parameters.Controls.Add(parameterTitle);
            var parameterHint = CreateLabel("喷头电压、驱动波形和喷头参数完全由 Meteor 波形文件或 PrintEngine cfg 加载，界面不提供写入入口。", 9F, Color.DimGray, new Padding(0, 6, 0, 8));
            parameterHint.AutoSize = false; parameterHint.Dock = DockStyle.Fill; parameters.Controls.Add(parameterHint);
            var parameterActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 4, 0, 0), WrapContents = false };
            var readStatus = CreateButton("读取喷头状态", Color.White, TextColor, 126, 34); readStatus.Click += delegate { RefreshMeteorMaintenanceStatus(); }; parameterActions.Controls.Add(readStatus); parameters.Controls.Add(parameterActions); page.Controls.Add(parameters, 1, 2);
            return page;
        }

        private Control BuildStatusPanel(out ListBox eventLog, out Label profileHint)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 1, AutoScroll = true };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 178)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            Label tempReading, tempDetail, voltageReading, voltageDetail, unusedReading, unusedDetail;
            root.Controls.Add(CreateMonitorCard("♨", "喷头温度", "—", "°C", "等待 Meteor 状态", Blue, out tempReading, out tempDetail), 0, 0);
            root.Controls.Add(CreateMonitorCard("ϟ", "喷头电压", "—", "V", "等待 Meteor 状态", Blue, out voltageReading, out voltageDetail), 0, 1);
            _headTemperatureReading = tempReading; _headTemperatureDetail = tempDetail; _headVoltageReading = voltageReading; _headVoltageDetail = voltageDetail;
            root.Controls.Add(CreateMonitorCard("◴", "气压", "0.62", "MPa", "设定 0.60 MPa", Blue, out unusedReading, out unusedDetail), 0, 2); root.Controls.Add(CreateMonitorCard("♧", "报警", "无报警", "", "所有安全状态正常", TextColor, out unusedReading, out unusedDetail), 0, 3);
            root.Controls.Add(CreateAxisMonitorCard(), 0, 4);
            var logCard = CreatePanel(); logCard.Dock = DockStyle.Fill; logCard.Padding = new Padding(12, 10, 12, 10); var logTitle = CreateLabel("事件日志", 11F, TextColor, Padding.Empty); logTitle.Dock = DockStyle.Top; eventLog = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = PanelBackground, ForeColor = Color.DimGray, Font = new Font("Microsoft YaHei UI", 8F) }; logCard.Controls.Add(eventLog); logCard.Controls.Add(logTitle); root.Controls.Add(logCard, 0, 5);
            profileHint = CreateLabel("加载设备配置中…", 8F, Color.DimGray, new Padding(8, 6, 8, 0)); profileHint.Dock = DockStyle.Fill; root.Controls.Add(profileHint, 0, 6); return root;
        }

        private Control CreateAxisMonitorCard()
        {
            var card = CreatePanel(); card.Dock = DockStyle.Fill; card.Padding = new Padding(12, 9, 12, 8);
            var title = CreateLabel("轴状态 / 限位", 10.5F, TextColor, Padding.Empty); title.Dock = DockStyle.Top; title.Height = 20;
            var summary = CreateLabel("● 6 轴就绪   ·   正 / 负限位未触发", 7.8F, Green, Padding.Empty); summary.Dock = DockStyle.Top; summary.Height = 17;
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 6, Margin = Padding.Empty, Padding = Padding.Empty };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            for (var i = 0; i < 6; i++) rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            AddAxisMonitorRow(rows, 0, AxisRole.InkCarX, "墨车 X");
            AddAxisMonitorRow(rows, 1, AxisRole.Scraper, "刮板");
            AddAxisMonitorRow(rows, 2, AxisRole.PowderCar, "铺粉车");
            AddAxisMonitorRow(rows, 3, AxisRole.RollerFront, "前粉辊");
            AddAxisMonitorRow(rows, 4, AxisRole.RollerRear, "后粉辊");
            AddAxisMonitorRow(rows, 5, AxisRole.BuildCylinder, "成型缸");
            card.Controls.Add(rows); card.Controls.Add(summary); card.Controls.Add(title);
            return card;
        }

        private void AddAxisMonitorRow(TableLayoutPanel panel, int row, AxisRole role, string axisName)
        {
            var axis = CreateLabel(axisName, 8F, TextColor, Padding.Empty); axis.Dock = DockStyle.Fill; axis.TextAlign = ContentAlignment.MiddleLeft;
            var value = CreateLabel("等待固高…", 8F, Color.DimGray, Padding.Empty); value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleRight;
            var status = CreateLabel("● 未读取", 7.4F, Color.DimGray, Padding.Empty); status.AutoSize = false; status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleRight; status.AutoEllipsis = true;
            value.AutoSize = false; value.AutoEllipsis = true;
            _axisMonitorReadings[role] = value; _axisMonitorStates[role] = status;
            panel.Controls.Add(axis, 0, row); panel.Controls.Add(value, 1, row); panel.Controls.Add(status, 2, row);
        }

        private Panel CreateAxisCard(AxisRole role, string name, bool longTravel)
        {
            var card = CreatePanel(); card.Dock = DockStyle.Fill; card.Margin = new Padding(0, 0, 10, 10); card.Padding = new Padding(12, 8, 12, 8); var title = CreateLabel(name, 10F, TextColor, Padding.Empty); title.Dock = DockStyle.Top; var ready = CreateLabel("● 等待固高", 8F, Color.DimGray, Padding.Empty); ready.Dock = DockStyle.Top; ready.TextAlign = ContentAlignment.TopRight; var value = CreateLabel("—", 12F, Blue, new Padding(0, 3, 0, 0)); value.Dock = DockStyle.Top; value.TextAlign = ContentAlignment.MiddleCenter; _manualAxisReadings[role] = value; _manualAxisStates[role] = ready;
            var controls = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34, WrapContents = false };
            var home = CreateSmallButton("⌂ 回零");
            if (role == AxisRole.PowderCar)
            {
                home.Click += delegate { StartPowderCarHome(home); };
                _toolTip.SetToolTip(home, "轴7反向寻找负限位，触发后正向退出20 mm并写入配置的逻辑原点。");
            }
            else
            {
                DisableUntilConfigured(home, "该轴回零方向、脱限距离和零点偏移尚未确认。");
            }
            controls.Controls.Add(home);
            bool jogEnabled = role == AxisRole.InkCarX || role == AxisRole.PowderCar;
            if (longTravel)
            {
                AddJogButton(controls, role, "≪", -1, 1.0, jogEnabled);
                AddJogButton(controls, role, "＜", -1, 0.25, jogEnabled);
                AddJogButton(controls, role, "＞", 1, 0.25, jogEnabled);
                AddJogButton(controls, role, "≫", 1, 1.0, jogEnabled);
            }
            else
            {
                var negative = CreateSmallButton("−"); DisableUntilConfigured(negative, "该轴的步进细分和传动参数尚未确认。"); controls.Controls.Add(negative);
                var positive = CreateSmallButton("+"); DisableUntilConfigured(positive, "该轴的步进细分和传动参数尚未确认。"); controls.Controls.Add(positive);
            }
            card.Controls.Add(controls); card.Controls.Add(value); card.Controls.Add(ready); card.Controls.Add(title); return card;
        }

        private void AddJogButton(FlowLayoutPanel controls, AxisRole role, string text, int direction, double speedScale, bool enabled)
        {
            var button = CreateSmallButton(text);
            if (!enabled)
            {
                DisableUntilConfigured(button, "该轴的步进细分、传动参数和运动方向尚未确认。");
            }
            else
            {
                button.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) StartManualJog(role, direction, speedScale); };
                button.MouseUp += delegate { StopManualJog(role); };
                button.MouseLeave += delegate { StopManualJog(role); };
                _toolTip.SetToolTip(button, (speedScale < 1.0 ? "低速" : "全速") + "按住点动，松开即停");
            }
            controls.Controls.Add(button);
        }

        private Panel CreateMonitorCard(string icon, string title, string value, string unit, string subtext, Color valueColor, out Label reading, out Label detail)
        {
            var card = CreatePanel(); card.Dock = DockStyle.Fill; card.Margin = new Padding(0, 0, 0, 8); card.Padding = new Padding(12, 9, 12, 8);
            var symbol = CreateLabel(icon, 24F, Blue, Padding.Empty); symbol.AutoSize = false; symbol.Dock = DockStyle.Left; symbol.Width = 46; symbol.TextAlign = ContentAlignment.MiddleCenter;
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = Padding.Empty, Padding = Padding.Empty };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 21)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var titleLabel = CreateLabel(title, 9.5F, TextColor, Padding.Empty); titleLabel.AutoSize = false; titleLabel.Dock = DockStyle.Fill; titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            var dot = CreateStatusDot(Green); dot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            reading = CreateLabel(value + (String.IsNullOrEmpty(unit) ? String.Empty : " " + unit), 14F, valueColor, Padding.Empty); reading.AutoSize = false; reading.Dock = DockStyle.Fill; reading.TextAlign = ContentAlignment.MiddleLeft; reading.AutoEllipsis = true;
            detail = CreateLabel(subtext, 8F, Color.DimGray, Padding.Empty); detail.AutoSize = false; detail.Dock = DockStyle.Fill; detail.TextAlign = ContentAlignment.TopLeft; detail.AutoEllipsis = true;
            content.Controls.Add(titleLabel, 0, 0); content.Controls.Add(dot, 1, 0); content.Controls.Add(reading, 0, 1); content.SetColumnSpan(reading, 2); content.Controls.Add(detail, 0, 2); content.SetColumnSpan(detail, 2);
            card.Controls.Add(content); card.Controls.Add(symbol); return card;
        }

        private void ImportSliceTask(Button importButton)
        {
            if (_jobImportRunning) return;
            if (_profile == null) { AddLog("任务导入失败：设备配置尚未加载。", Red); return; }

            string selectedDirectory;
            using (var dialog = new FolderBrowserDialog
            {
                Description = "选择包含逐层切片图的任务目录",
                ShowNewFolderButton = false,
                SelectedPath = CurrentSliceTask == null ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : CurrentSliceTask.DirectoryPath
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                selectedDirectory = dialog.SelectedPath;
            }

            _jobImportRunning = true;
            importButton.Enabled = false;
            _jobImportCancellation = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = _jobImportCancellation.Token;
            AddLog("正在扫描并校验切片任务：" + selectedDirectory, Blue);

            var worker = new System.Threading.Thread(delegate()
            {
                SliceTask task = null;
                Exception failure = null;
                try { task = SliceTaskLoader.LoadDirectory(selectedDirectory, _profile, token); }
                catch (Exception exception) { failure = exception.GetBaseException(); }
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(delegate
                {
                    _jobImportRunning = false;
                    importButton.Enabled = true;
                    if (_jobImportCancellation != null) { _jobImportCancellation.Dispose(); _jobImportCancellation = null; }
                    if (failure != null)
                    {
                        if (failure is OperationCanceledException) AddLog("切片任务导入已取消。", Color.DarkOrange);
                        else AddLog("切片任务导入失败：" + failure.Message, Red);
                        return;
                    }
                    ApplySliceTask(task);
                }));
            });
            worker.IsBackground = true;
            worker.Name = "SliceTaskImport";
            worker.Start();
        }

        private void ApplySliceTask(SliceTask task)
        {
            CurrentSliceTask = task;
            _jobNameValue.Text = task.Name;
            _jobImageSizeValue.Text = task.WidthPixels + " × " + task.HeightPixels + " px";
            _jobDirectoryValue.Text = task.DirectoryPath;
            _toolTip.SetToolTip(_jobDirectoryValue, task.DirectoryPath);
            ShowSlice(0);
            AddLog("切片任务已导入：" + task.Name + "，共 " + task.SliceFiles.Count + " 层。", Green);
        }

        private void ShowSlice(int index)
        {
            if (CurrentSliceTask == null || index < 0 || index >= CurrentSliceTask.SliceFiles.Count) return;
            string path = CurrentSliceTask.SliceFiles[index];
            try
            {
                Image image;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var source = Image.FromStream(stream, false, true))
                    image = new Bitmap(source);
                _layoutCanvas.SetRaster(image);

                _currentSliceIndex = index;
                CurrentSlicePath = path;
                int layer = index + 1;
                int total = CurrentSliceTask.SliceFiles.Count;
                int percent = (int)Math.Round(layer * 100.0 / total);
                _jobLayerValue.Text = layer + " / " + total;
                _slicePreviewStatus.Text = Path.GetFileName(path) + "  ·  " + CurrentSliceTask.WidthPixels + "×" + CurrentSliceTask.HeightPixels + " px";
                _layerProgressText.Text = "切片浏览     " + percent + "%";
                _layerProgressBar.Value = Math.Max(0, Math.Min(100, percent));
                _previousSliceButton.Enabled = index > 0;
                _nextSliceButton.Enabled = index < total - 1;
            }
            catch (Exception exception)
            {
                AddLog("切片显示失败：" + Path.GetFileName(path) + "；" + exception.GetBaseException().Message, Red);
            }
        }

        private void LoadProfile()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmallPrinterProfile.json");
                _profile = SmallPrinterProfile.Load(path);
                _scanModeButton.Text = _profile.DirectionMode == SinglePassDirectionMode.PrintBothDirections ? "往返喷墨" : "去程喷墨";
                _feedDuration.Value = Math.Max(0, Math.Min(60000, _profile.PowderFeedDurationMs));
                _motionEditors["墨车全程速度"].Text = _profile.GetAxis(AxisRole.InkCarX).VelocityMmPerSecond.ToString();
                _motionEditors["铺粉全程速度"].Text = _profile.GetAxis(AxisRole.PowderCar).VelocityMmPerSecond.ToString();
                IList<string> errors = _profile.Validate();
                _profileHint.Text = errors.Count == 0 ? "设备配置已通过校验" : "配置待完善：" + errors[0];
                AddLog("设备配置已加载：" + _profile.ProductName, errors.Count == 0 ? Green : Color.DarkOrange);
                AddLog("当前层成型幅面已就绪；导入任务后将显示真实切片图形与摆放位置。", Blue);
                InitializeGtsController();
                InitializeMeteorController();
                InitializeHeadTelemetry();
                InitializeGtsTelemetry();
            }
            catch (Exception exception)
            {
                _profileHint.Text = "设备初始化失败";
                _deviceConnection.Text = "固高连接失败"; _deviceConnection.ForeColor = Red;
                _deviceState.Text = "禁止运动"; _deviceState.ForeColor = Red; _deviceStatusDot.BackColor = Red;
                AddLog(exception.GetBaseException().Message, Red);
                _startButton.Enabled = false;
            }
        }

        private void InitializeGtsController()
        {
            short cardNumber = _profile.GtsTelemetry == null ? (short)0 : _profile.GtsTelemetry.CardNumber;
            _gtsControllerSession = new GtsControllerSession(cardNumber);
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GTS800-加限位.cfg");
            _gtsControllerSession.Initialize(configPath);
            _gtsMotionController = new GtsMotionController(_profile, _gtsControllerSession);
            _deviceConnection.Text = "固高已连接"; _deviceConnection.ForeColor = TextColor;
            _deviceState.Text = "CFG 已加载"; _deviceState.ForeColor = Green; _deviceStatusDot.BackColor = Green;
            AddLog("固高卡已打开、复位并加载机台 CFG；1–7 轴使用内部脉冲计数。", Green);
        }

        private void StartManualJog(AxisRole role, int direction, double speedScale)
        {
            try
            {
                if (_gtsMotionController == null) throw new InvalidOperationException("固高运动控制尚未初始化。");
                _gtsMotionController.StartJog(role, direction, speedScale);
                AddLog(role + " 已开始" + (direction < 0 ? "负向" : "正向") + (speedScale < 1.0 ? "低速" : "全速") + "点动。", Blue);
            }
            catch (Exception exception)
            {
                AddLog("点动失败：" + exception.GetBaseException().Message, Red);
            }
        }

        private void StopManualJog(AxisRole role)
        {
            try
            {
                if (_gtsMotionController != null) _gtsMotionController.StopAxis(role);
                RefreshGtsTelemetry();
            }
            catch (Exception exception)
            {
                AddLog("停止点动失败：" + exception.GetBaseException().Message, Red);
            }
        }

        private void StartPowderCarHome(Button button)
        {
            if (_powderHomeRunning) return;
            if (_gtsMotionController == null) { AddLog("铺粉车回零失败：固高运动控制尚未初始化。", Red); return; }

            _powderHomeRunning = true;
            button.Enabled = false;
            _powderHomeCancellation = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = _powderHomeCancellation.Token;
            AddLog("铺粉车开始回零：反向寻找负限位，触发后正向退出20 mm。", Blue);

            var worker = new System.Threading.Thread(delegate()
            {
                Exception failure = null;
                try { _gtsMotionController.HomePowderCar(token); }
                catch (Exception exception) { failure = exception.GetBaseException(); }
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(delegate
                {
                    _powderHomeRunning = false;
                    _powderHomeThread = null;
                    button.Enabled = true;
                    if (failure == null) AddLog("铺粉车回零完成：负限位释放，当前位置已写为逻辑" + _profile.GetAxis(AxisRole.PowderCar).HomePositionMm.ToString("F3") + " mm。", Green);
                    else if (failure is OperationCanceledException) AddLog("铺粉车回零已停止。", Color.DarkOrange);
                    else AddLog("铺粉车回零失败：" + failure.Message, Red);
                    RefreshGtsTelemetry();
                }));
            });
            worker.IsBackground = true;
            worker.Name = "PowderCarNegativeLimitHome";
            _powderHomeThread = worker;
            worker.Start();
        }

        private void StartHeadCleaning(Button button)
        {
            if (_headCleaningRunning)
            {
                if (_headCleaningCancellation != null) _headCleaningCancellation.Cancel();
                button.Enabled = false;
                AddLog("正在停止自动刮墨。", Color.DarkOrange);
                return;
            }
            if (_gtsMotionController == null || _profile == null)
            {
                AddLog("自动刮墨失败：固高运动控制尚未初始化。", Red);
                return;
            }

            _headCleaningRunning = true;
            button.Text = "停止刮墨";
            _headCleaningCancellation = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = _headCleaningCancellation.Token;
            var workflow = new HeadCleaningWorkflow(_profile, _gtsMotionController);
            var worker = new System.Threading.Thread(delegate()
            {
                Exception failure = null;
                try
                {
                    workflow.Execute(token, delegate(string message)
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke(new Action(delegate { AddLog(message, Blue); }));
                    });
                }
                catch (Exception exception) { failure = exception.GetBaseException(); }
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(delegate
                {
                    _headCleaningRunning = false;
                    _headCleaningThread = null;
                    if (_headCleaningCancellation != null) { _headCleaningCancellation.Dispose(); _headCleaningCancellation = null; }
                    button.Text = "自动刮墨";
                    button.Enabled = true;
                    if (failure == null) AddLog("自动刮墨完成：墨车位于118.0 mm，刮墨轴已返回待机角度。", Green);
                    else if (failure is OperationCanceledException) AddLog("自动刮墨已停止，压墨输出已关闭。", Color.DarkOrange);
                    else AddLog("自动刮墨失败：" + failure.Message, Red);
                    RefreshGtsTelemetry();
                }));
            });
            worker.IsBackground = true;
            worker.Name = "SmallPrinterHeadCleaning";
            _headCleaningThread = worker;
            worker.Start();
        }

        private void StopActiveOperations(bool emergency)
        {
            var errors = new List<string>();
            if (_powderHomeCancellation != null) _powderHomeCancellation.Cancel();
            if (_headCleaningCancellation != null) _headCleaningCancellation.Cancel();
            try { if (_gtsMotionController != null) _gtsMotionController.StopAll(); }
            catch (Exception exception) { errors.Add("固高停止失败：" + exception.GetBaseException().Message); }
            try { if (_meteorControllerSession != null) _meteorControllerSession.Stop(); }
            catch (Exception exception) { errors.Add("Meteor 终止失败：" + exception.GetBaseException().Message); }
            try { PowderFeedOutput.Set(false); }
            catch (Exception exception) { errors.Add("落粉关闭失败：" + exception.GetBaseException().Message); }
            try { if (_gtsMotionController != null) _gtsMotionController.SetDigitalOutput(OutputRole.PressInkEnable, false); }
            catch (Exception exception) { errors.Add("压墨关闭失败：" + exception.GetBaseException().Message); }

            AddLog(emergency ? "软件急停已执行：全轴停止、Meteor 终止、落粉及压墨关闭；硬件急停回路仍须独立生效。" : "停止已执行：全轴停止、Meteor 终止、落粉及压墨关闭。", emergency ? Red : Blue);
            foreach (string error in errors) AddLog(error, Red);
            RefreshGtsTelemetry();
        }

        private void InitializeMeteorController()
        {
            try
            {
                _meteorControllerSession = new MeteorControllerSession(_profile);
                _meteorControllerSession.Initialize();
                _profile.MeteorTelemetry.ApiDirectory = _meteorControllerSession.ApiDirectory;
                _deviceState.Text = "固高 + Meteor 已连接"; _deviceState.ForeColor = Green;
                AddLog("Meteor Printer Interface 已连接：" + _meteorControllerSession.ApiDirectory, Green);
            }
            catch (Exception exception)
            {
                _deviceState.Text = "Meteor 未连接"; _deviceState.ForeColor = Red;
                AddLog("Meteor 初始化失败：" + exception.GetBaseException().Message, Red);
                _startButton.Enabled = false;
            }
        }

        private void RunPreflightCheck()
        {
            var failures = new List<string>();
            if (_profile == null) failures.Add("设备配置未加载");
            else
            {
                IList<string> profileErrors = _profile.Validate();
                foreach (string error in profileErrors) failures.Add(error);
                if (_profile.PowderFeedDurationMs <= 0) failures.Add("落粉开启时间未设置");
            }
            if (_gtsControllerSession == null || !_gtsControllerSession.IsInitialized) failures.Add("固高未完成初始化");
            if (_meteorControllerSession == null || !_meteorControllerSession.IsConnected) failures.Add("Meteor Printer Interface 未连接");
            else
            {
                int bits, absX, encoder;
                if (!_meteorControllerSession.TryGetPccStatus(out bits, out absX, out encoder)) failures.Add("PCC 状态不可读");
            }
            if (CurrentSliceTask == null) failures.Add("尚未导入切片任务");
            else if (String.IsNullOrWhiteSpace(CurrentSlicePath) || !File.Exists(CurrentSlicePath)) failures.Add("当前切片图不可用");
            if (_anyGtsAxisMoving) failures.Add("存在运动中的轴");
            if (failures.Count == 0) AddLog("打印前检查通过：切片任务、固高、Meteor、PCC 和落粉参数就绪。", Green);
            else AddLog("打印前检查未通过：" + String.Join("；", failures), Red);
        }

        private void InitializeHeadTelemetry()
        {
            _headTelemetryReader = new MeteorHeadTelemetryReader(_profile.MeteorTelemetry);
            _headTelemetryTimer = new Timer { Interval = 1000 };
            _headTelemetryTimer.Tick += delegate { RefreshHeadTelemetry(); };
            RefreshHeadTelemetry();
            _headTelemetryTimer.Start();
        }

        private void RefreshHeadTelemetry()
        {
            HeadTelemetrySnapshot snapshot = null; string error = "Meteor 遥测未初始化";
            if (_headTelemetryReader != null && _headTelemetryReader.TryRead(out snapshot, out error))
            {
                _headTemperatureReading.Text = snapshot.TemperatureC.ToString("F1") + " °C";
                _headTemperatureDetail.Text = snapshot.Source + " · 实时读取";
                _headVoltageReading.Text = snapshot.HasVoltage ? snapshot.VoltageV.ToString("F3") + " V" : "未配置";
                _headVoltageDetail.Text = snapshot.HasVoltage ? snapshot.Source + " · 实时读取" : "当前喷头型号未配置电压字段";
                _lastTelemetryError = null;
                return;
            }

            _headTemperatureReading.Text = "— °C"; _headVoltageReading.Text = "— V";
            _headTemperatureDetail.Text = error; _headVoltageDetail.Text = error;
            if (!String.Equals(_lastTelemetryError, error, StringComparison.Ordinal)) { AddLog(error, Color.DarkOrange); _lastTelemetryError = error; }
        }

        private void RefreshMeteorMaintenanceStatus()
        {
            MeteorMaintenanceSnapshot status = null; string error = "Meteor 遥测未初始化";
            if (_headTelemetryReader == null || !_headTelemetryReader.TryReadMaintenance(out status, out error))
            {
                int bits, absX, encoder;
                if (_meteorControllerSession != null && _meteorControllerSession.TryGetPccStatus(out bits, out absX, out encoder))
                {
                    _meteorPccState.Text = "●  PCC：0x" + bits.ToString("X") + "  AbsX=" + absX; _meteorPccState.ForeColor = Green;
                    _meteorHeadState.Text = "●  HDC：未读取"; _meteorHeadState.ForeColor = Color.DarkOrange;
                    AddLog("PCC 原生状态已读取；HDC 详细遥测不可用。 " + error, Color.DarkOrange);
                    return;
                }
                _meteorPccState.Text = "●  PCC：未连接"; _meteorPccState.ForeColor = Color.DimGray;
                _meteorHeadState.Text = "●  HDC：未读取"; _meteorHeadState.ForeColor = Color.DimGray;
                AddLog(error ?? "Meteor 维护状态读取失败。", Color.DarkOrange);
                return;
            }
            _meteorPccState.Text = "●  PCC：0x" + status.PccStatusBits.ToString("X"); _meteorPccState.ForeColor = Green;
            _meteorHeadState.Text = "●  HDC：状态 " + status.HeadState + " / 0x" + status.HeadStatusBits.ToString("X"); _meteorHeadState.ForeColor = Green;
            _meteorPowerState.Text = "●  辅温 " + status.AuxiliaryTemperatureC.ToString("F1") + " °C · 功放 " + status.AmplifierTemperatureC.ToString("F1") + " °C"; _meteorPowerState.ForeColor = Blue;
            AddLog("Meteor 状态已读取：PCC=0x" + status.PccStatusBits.ToString("X") + "，HDC 状态=" + status.HeadState + "。", Green);
        }

        private bool ConfirmMeteorMaintenance(string action)
        {
            if (_gtsTelemetryAvailable && _anyGtsAxisMoving)
            {
                AddLog(action + "已拒绝：检测到固高轴仍在运动。", Red);
                return false;
            }
            if (_meteorControllerSession == null || !_meteorControllerSession.IsConnected)
            {
                AddLog(action + "已拒绝：Meteor Printer Interface 未连接。", Red);
                return false;
            }
            if (_meteorControllerSession.IsJobActive)
            {
                AddLog(action + "已拒绝：Meteor 作业正在进行。", Red);
                return false;
            }
            int pccBits, absX, encoder;
            if (!_meteorControllerSession.TryGetPccStatus(out pccBits, out absX, out encoder))
            {
                AddLog(action + "已拒绝：PCC 状态不可读。", Red);
                return false;
            }
            return MessageBox.Show(action + "将直接作用于 SG1024 喷头。\r\n确认设备处于维护状态、无打印任务且安全联锁正常后继续。", "维护操作确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
        }

        private void RequestHeadPower(bool enabled)
        {
            string action = enabled ? "喷头上电" : "喷头断电";
            if (!ConfirmMeteorMaintenance(action)) return;
            try
            {
                _meteorControllerSession.SetHeadPower(enabled);
                AddLog(action + "命令已发送：PiSetHeadPower(" + (enabled ? "1" : "0") + ")。", enabled ? Blue : Red);
                RefreshMeteorMaintenanceStatus();
            }
            catch (Exception exception) { AddLog(action + "失败：" + exception.GetBaseException().Message, Red); }
        }

        private void RequestSpit()
        {
            if (!ConfirmMeteorMaintenance("闪喷 " + (_profile.MeteorTelemetry.SpitCount <= 0 ? 200 : _profile.MeteorTelemetry.SpitCount))) return;
            try { _meteorControllerSession.Spit(); AddLog("SG1024 闪喷命令已发送。", Blue); }
            catch (Exception exception) { AddLog("闪喷失败：" + exception.GetBaseException().Message, Red); }
        }

        private void InitializeGtsTelemetry()
        {
            _gtsTelemetryReader = new GtsTelemetryReader(_profile.GtsTelemetry);
            int interval = _profile.GtsTelemetry == null ? 500 : Math.Max(200, _profile.GtsTelemetry.PollIntervalMs);
            _gtsTelemetryTimer = new Timer { Interval = interval };
            _gtsTelemetryTimer.Tick += delegate { RefreshGtsTelemetry(); };
            RefreshGtsTelemetry();
            _gtsTelemetryTimer.Start();
        }

        private void RefreshGtsTelemetry()
        {
            if (_profile == null || _gtsTelemetryReader == null) return;
            _gtsTelemetryAvailable = false; _anyGtsAxisMoving = false;
            foreach (AxisRole role in (AxisRole[])Enum.GetValues(typeof(AxisRole)))
            {
                AxisProfile axis = _profile.GetAxis(role);
                if (axis == null) continue;
                GtsAxisTelemetry telemetry; string error;
                if (_gtsTelemetryReader.TryRead(axis, out telemetry, out error))
                {
                    _gtsTelemetryAvailable = true; _anyGtsAxisMoving = _anyGtsAxisMoving || telemetry.Moving;
                    if (!_powderOutputInitialized)
                    {
                        try { PowderFeedOutput.Set(false); _powderOutputInitialized = true; }
                        catch (Exception ex) { AddLog("落粉初始关闭失败：" + ex.Message, Red); }
                    }
                    string position = telemetry.HasMillimeterPosition ? telemetry.PositionMm.ToString("F3") + " mm" : telemetry.RawPositionPulse.ToString("F0") + " pulse";
                    if (_axisMonitorReadings.ContainsKey(role)) _axisMonitorReadings[role].Text = position;
                    if (_manualAxisReadings.ContainsKey(role)) _manualAxisReadings[role].Text = position;
                    if (_axisMonitorStates.ContainsKey(role))
                        UpdateAxisStateLabel(_axisMonitorStates[role], telemetry);
                    if (_manualAxisStates.ContainsKey(role))
                        UpdateAxisStateLabel(_manualAxisStates[role], telemetry);
                    _lastGtsTelemetryError = null;
                }
                else
                {
                    if (_axisMonitorReadings.ContainsKey(role)) _axisMonitorReadings[role].Text = "—";
                    if (_axisMonitorStates.ContainsKey(role)) { _axisMonitorStates[role].Text = "● 未连接"; _axisMonitorStates[role].ForeColor = Color.DimGray; }
                    if (_manualAxisStates.ContainsKey(role)) { _manualAxisStates[role].Text = "● 未连接"; _manualAxisStates[role].ForeColor = Color.DimGray; }
                    if (!String.Equals(_lastGtsTelemetryError, error, StringComparison.Ordinal)) { AddLog(error, Color.DarkOrange); _lastGtsTelemetryError = error; }
                    break;
                }
            }
        }

        private static void UpdateAxisStateLabel(Label state, GtsAxisTelemetry telemetry)
        {
            if (telemetry.ServoAlarm) { state.Text = "● 驱动报警"; state.ForeColor = Red; }
            else if (telemetry.PositiveLimit) { state.Text = "● 正限位"; state.ForeColor = Red; }
            else if (telemetry.NegativeLimit) { state.Text = "● 负限位"; state.ForeColor = Red; }
            else if (telemetry.Moving) { state.Text = "● 运动中"; state.ForeColor = Blue; }
            else { state.Text = "● 就绪"; state.ForeColor = Green; }
        }

        private void AddLog(string message, Color color)
        {
            if (_eventLog == null) return; _eventLog.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + message); while (_eventLog.Items.Count > 12) _eventLog.Items.RemoveAt(_eventLog.Items.Count - 1);
        }

        private static Panel CreatePanel() { return new Panel { BackColor = PanelBackground, BorderStyle = BorderStyle.FixedSingle }; }
        private static Label CreateLabel(string text, float size, Color color, Padding padding) { return new Label { Text = text, AutoSize = true, ForeColor = color, Font = new Font("Microsoft YaHei UI", size, size >= 11F ? FontStyle.Bold : FontStyle.Regular), Padding = padding }; }
        private static Panel CreateStatusDot(Color color) { return new Panel { BackColor = color, Width = 12, Height = 12, Margin = new Padding(2, 5, 2, 0) }; }
        private static Button CreateButton(string text, Color background, Color foreground, int width, int height) { return new Button { Text = text, TextAlign = ContentAlignment.MiddleCenter, Padding = Padding.Empty, AutoEllipsis = true, BackColor = background, ForeColor = foreground, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = background }, Width = width, Height = height, Margin = new Padding(0, 0, 12, 0), Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), UseVisualStyleBackColor = false }; }
        private static Button CreateSmallButton(string text) { return new Button { Text = text, TextAlign = ContentAlignment.MiddleCenter, Padding = Padding.Empty, AutoEllipsis = true, Width = 48, Height = 28, Margin = new Padding(0, 0, 6, 0), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = BorderColor }, BackColor = Color.White, ForeColor = TextColor, UseVisualStyleBackColor = false }; }
        private void DisableUntilConfigured(Button button, string reason) { button.Enabled = false; button.AccessibleDescription = reason; _toolTip.SetToolTip(button, reason); }
        private static Button CreateModeButton(string text, bool selected) { return new Button { Text = text, TextAlign = ContentAlignment.MiddleCenter, Padding = Padding.Empty, AutoEllipsis = true, Width = 150, Height = 40, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = selected ? Blue : BorderColor }, BackColor = selected ? Color.FromArgb(245, 249, 255) : Color.White, ForeColor = selected ? Blue : TextColor, UseVisualStyleBackColor = false, Margin = new Padding(0, 0, 8, 0), Font = new Font("Microsoft YaHei UI", 10F, selected ? FontStyle.Bold : FontStyle.Regular) }; }
        private static Button CreateNavButton(string text, bool selected) { return new Button { Text = text, TextAlign = ContentAlignment.MiddleLeft, Width = 154, Height = 48, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = selected ? Color.FromArgb(180, 207, 248) : Color.White }, BackColor = selected ? Color.FromArgb(240, 246, 255) : Color.White, ForeColor = selected ? Blue : TextColor, UseVisualStyleBackColor = false, Margin = new Padding(0, 0, 0, 8), Font = new Font("Microsoft YaHei UI", 10F, selected ? FontStyle.Bold : FontStyle.Regular) }; }
        private static Label AddInfo(TableLayoutPanel panel, int row, string key, string value) { panel.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); panel.Controls.Add(CreateLabel(key, 9F, Color.DimGray, Padding.Empty), 0, row); Label valueLabel = CreateLabel(value, 9F, TextColor, Padding.Empty); panel.Controls.Add(valueLabel, 1, row); return valueLabel; }
    }

    /// <summary>按切片原始方向将当前层光栅映射到100×60 mm成型幅面；只读，不提供移动或删除操作。</summary>
    internal sealed class LayoutCanvas : Control
    {
        private const float BedWidthMm = 100F;
        private const float BedHeightMm = 60F;
        private Image _raster;

        public LayoutCanvas()
        {
            BackColor = Color.White;
            DoubleBuffered = true;
            Cursor = Cursors.Default;
        }

        public void SetRaster(Image raster)
        {
            if (raster == null) throw new ArgumentNullException("raster");
            Image previous = _raster;
            _raster = raster;
            if (previous != null) previous.Dispose();
            Invalidate();
        }

        public void ClearRaster()
        {
            Image previous = _raster;
            _raster = null;
            if (previous != null) previous.Dispose();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ClearRaster();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RectangleF bed = GetBedRectangle();
            using (var fill = new SolidBrush(Color.FromArgb(249, 252, 255))) e.Graphics.FillRectangle(fill, bed);

            if (_raster == null)
            {
                const string message = "导入切片任务后，在此显示当前层真实图形与摆放位置";
                SizeF size = e.Graphics.MeasureString(message, Font);
                e.Graphics.DrawString(message, Font, Brushes.DimGray, bed.Left + (bed.Width - size.Width) / 2F, bed.Top + (bed.Height - size.Height) / 2F);
            }
            else
            {
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.DrawImage(_raster, bed);
            }

            using (var outline = new Pen(Color.FromArgb(115, 174, 242), 2F)) e.Graphics.DrawRectangle(outline, bed.X, bed.Y, bed.Width, bed.Height);
            e.Graphics.DrawString("100 mm", Font, Brushes.DimGray, bed.Left + bed.Width / 2F - 20F, bed.Bottom + 5F);
            e.Graphics.DrawString("60 mm", Font, Brushes.DimGray, bed.Right + 5F, bed.Top + bed.Height / 2F - 8F);
            e.Graphics.DrawString("按切片原始方向显示", Font, Brushes.DimGray, bed.Left + 4F, bed.Top + 4F);
        }

        private RectangleF GetBedRectangle()
        {
            RectangleF available = RectangleF.Inflate(ClientRectangle, -42, -34);
            if (available.Width <= 0 || available.Height <= 0) return RectangleF.Empty;
            float ratio = BedWidthMm / BedHeightMm;
            if (available.Width / available.Height > ratio)
            {
                float width = available.Height * ratio;
                return new RectangleF(available.Left + (available.Width - width) / 2F, available.Top, width, available.Height);
            }
            float height = available.Width / ratio;
            return new RectangleF(available.Left, available.Top + (available.Height - height) / 2F, available.Width, height);
        }
    }
}
