using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace LaserAdd.SmallPrinter
{
    public enum SinglePassDirectionMode
    {
        Unconfigured,
        AlternateLayers,
        PrintBothDirections,
        PrintOutboundOnly
    }
    public enum AxisRole
    {
        Scraper,
        PowderCar,
        BuildCylinder,
        RollerFront,
        RollerRear,
        InkCarX
    }

    public enum OutputRole
    {
        InfraredEnable,
        VacuumEnable,
        PowderFeedEnable,
        PressInkEnable
    }

    public sealed class AxisProfile
    {
        public AxisRole Role { get; set; }
        public short AxisNumber { get; set; }
        public double HomePositionMm { get; set; }
        public double MinimumPositionMm { get; set; }
        public double MaximumPositionMm { get; set; }
        public double VelocityMmPerSecond { get; set; }
        public double AccelerationMmPerSecond2 { get; set; }
        public double PulsesPerMillimeter { get; set; }
        public int PositionSign { get; set; }
    }

    public sealed class DigitalOutputProfile
    {
        public OutputRole Role { get; set; }
        public short Channel { get; set; }
        public bool ActiveLow { get; set; }
    }

    public sealed class InfraredProfile
    {
        public bool Enabled { get; set; }
        public short DacChannel { get; set; }
        public double DefaultPowerPercent { get; set; }
    }

    /// <summary>Meteor HDC 状态字段映射。不同喷头型号对电压字段的解释不同，必须由设备配置明确指定。</summary>
    public sealed class MeteorTelemetryProfile
    {
        public bool Enabled { get; set; }
        public int PccNumber { get; set; }
        public int HeadNumber { get; set; }
        public string ApiDirectory { get; set; }
        public string PrintEngineConfigPath { get; set; }
        public string TemperatureField { get; set; }
        public double TemperatureScale { get; set; }
        public string VoltageField { get; set; }
        public double VoltageScale { get; set; }
        public int SpitCount { get; set; }
    }

    public sealed class GtsTelemetryProfile
    {
        public bool Enabled { get; set; }
        public short CardNumber { get; set; }
        public int PollIntervalMs { get; set; }
    }

    public sealed class HeadCleaningProfile
    {
        public bool Enabled { get; set; }
        public double PressInkPositionMm { get; set; }
        public double CleanPositionMm { get; set; }
        public int PressInkDurationMs { get; set; }
        public int ScraperPulsesPerRevolution { get; set; }
        public double ScraperWipeAngleDegrees { get; set; }
        public int ScraperDirectionSign { get; set; }
        public double ScraperSpeedRevolutionsPerSecond { get; set; }
        public double ScraperAccelerationRevolutionsPerSecond2 { get; set; }
    }

    /// <summary>
    /// 小型机的唯一设备清单。轴号、IO 和行程只能在该文件中确定，不能由业务代码猜测。
    /// </summary>
    public sealed class SmallPrinterProfile
    {
        public SmallPrinterProfile() { PrintStartOffsetMm = -1; }
        public string ProductName { get; set; }
        public double PlateWidthMm { get; set; }
        public double PlateHeightMm { get; set; }
        public int SliceDpi { get; set; }
        public int PrintHeadCount { get; set; }
        public int PassesPerLayer { get; set; }
        public int SwathRowsPerHead { get; set; }
        // 清洗、暂停和等待共用一个坐标，避免三处参数发生漂移。
        public double InkCarParkPositionMm { get; set; }
        // 保湿位属于维护行程；打印扫描区间由配置单独指定。
        public double InkCarMoisturizingPositionMm { get; set; }
        public double InkCarPrintMinimumMm { get; set; }
        public double InkCarPrintMaximumMm { get; set; }
        public int PowderFeedDurationMs { get; set; }
        public SinglePassDirectionMode DirectionMode { get; set; }
        // -1 表示有效喷印窗口居中放在机械行程内；非负值为距低端的偏移。
        public double PrintStartOffsetMm { get; set; }
        public bool EnableUv { get; set; }
        public bool EnableCamera { get; set; }
        public bool EnableVacuum { get; set; }
        public IList<AxisProfile> Axes { get; set; }
        public IList<DigitalOutputProfile> DigitalOutputs { get; set; }
        public InfraredProfile Infrared { get; set; }
        public MeteorTelemetryProfile MeteorTelemetry { get; set; }
        public GtsTelemetryProfile GtsTelemetry { get; set; }
        public HeadCleaningProfile HeadCleaning { get; set; }

        public AxisProfile GetAxis(AxisRole role)
        {
            return Axes == null ? null : Axes.FirstOrDefault(axis => axis.Role == role);
        }

        public DigitalOutputProfile GetOutput(OutputRole role)
        {
            return DigitalOutputs == null ? null : DigitalOutputs.FirstOrDefault(output => output.Role == role);
        }

        public int RasterWidthPixels
        {
            get { return MmToPixelsPlusOne(PlateWidthMm); }
        }

        public int RasterHeightPixels
        {
            get { return MmToPixelsPlusOne(PlateHeightMm); }
        }

        public int SwathHeightPixels
        {
            get { return SwathRowsPerHead * PrintHeadCount; }
        }

        public int MmToPixelsPlusOne(double millimeters)
        {
            return (int)(millimeters * SliceDpi / 25.4) + 1;
        }

        public IList<string> Validate(bool validateAuxiliaryOutputs = true)
        {
            var errors = new List<string>();
            if (!IsFinite(PlateWidthMm) || !IsFinite(PlateHeightMm) || PlateWidthMm <= 0 || PlateHeightMm <= 0)
                errors.Add("打印幅面必须为有效正数。");
            if (SliceDpi > 0 && IsFinite(PlateHeightMm) && PlateHeightMm > 0 && RasterHeightPixels > SwathHeightPixels)
                errors.Add("打印幅面高度超出单 PASS 喷头覆盖范围。");
            AxisProfile inkCar = GetAxis(AxisRole.InkCarX);
            if (inkCar != null && (!IsFinite(InkCarParkPositionMm) || InkCarParkPositionMm < inkCar.MinimumPositionMm || InkCarParkPositionMm > inkCar.MaximumPositionMm))
                errors.Add("墨车清洗/暂停/等待位置超出 X 行程。");
            if (inkCar != null && (!IsFinite(InkCarMoisturizingPositionMm) || InkCarMoisturizingPositionMm < inkCar.MinimumPositionMm || InkCarMoisturizingPositionMm > inkCar.MaximumPositionMm))
                errors.Add("墨车保湿位置超出 X 机械行程。");
            if (inkCar != null && (!IsFinite(InkCarPrintMinimumMm) || !IsFinite(InkCarPrintMaximumMm)
                || InkCarPrintMinimumMm < inkCar.MinimumPositionMm || InkCarPrintMaximumMm > inkCar.MaximumPositionMm
                || InkCarPrintMinimumMm >= InkCarPrintMaximumMm))
                errors.Add("墨车打印扫描区间超出 X 机械行程或区间无效。");
            if (inkCar != null && PlateWidthMm > InkCarPrintMaximumMm - InkCarPrintMinimumMm)
                errors.Add("打印幅面宽度超出墨车 X 行程。");
            if (SliceDpi <= 0)
                errors.Add("切片 DPI 必须为正数。");
            if (PrintHeadCount != 1)
                errors.Add("该产品限定单喷头。");
            if (PassesPerLayer != 1)
                errors.Add("该产品限定每层单 PASS。");
            if (DirectionMode != SinglePassDirectionMode.PrintOutboundOnly && DirectionMode != SinglePassDirectionMode.PrintBothDirections)
                errors.Add("请选择去程喷墨或往返喷墨。");
            if (SwathRowsPerHead <= 0)
                errors.Add("喷嘴条带行数必须为正数。");
            if (EnableUv)
                errors.Add("小型设备不支持 UV，EnableUv 必须为 false。");
            if (EnableCamera)
                errors.Add("小型设备不支持拍照，EnableCamera 必须为 false。");

            var requiredRoles = Enum.GetValues(typeof(AxisRole)).Cast<AxisRole>().ToList();
            if (Axes == null || Axes.Count != requiredRoles.Count)
            {
                errors.Add("必须配置且仅配置 6 个工艺轴（无墨车 Y 轴、无旋转落粉轴）。");
            }
            else
            {
                foreach (AxisRole role in requiredRoles)
                {
                    AxisProfile axis = GetAxis(role);
                    if (axis == null)
                    {
                        errors.Add("缺少轴角色：" + role + "。");
                        continue;
                    }
                    if (axis.AxisNumber <= 0)
                        errors.Add(role + " 的固高轴号未配置。");
                    if ((role == AxisRole.InkCarX || role == AxisRole.PowderCar) && axis.PulsesPerMillimeter <= 0)
                        errors.Add(role + " 的每毫米脉冲数未配置。");
                    if (!IsFinite(axis.MinimumPositionMm) || !IsFinite(axis.MaximumPositionMm) || axis.MinimumPositionMm >= axis.MaximumPositionMm)
                        errors.Add(role + " 的正负行程无效。");
                    if (!IsFinite(axis.HomePositionMm) || axis.HomePositionMm < axis.MinimumPositionMm || axis.HomePositionMm > axis.MaximumPositionMm)
                        errors.Add(role + " 的回零坐标不在机械行程内。");
                    if (!IsFinite(axis.VelocityMmPerSecond) || !IsFinite(axis.AccelerationMmPerSecond2) || axis.VelocityMmPerSecond <= 0 || axis.AccelerationMmPerSecond2 <= 0)
                        errors.Add(role + " 的速度或加速度未配置。");
                }
                if (Axes.GroupBy(axis => axis.Role).Any(group => group.Count() > 1))
                    errors.Add("每个轴角色只能配置一次。");
                if (Axes.GroupBy(axis => axis.AxisNumber).Any(group => group.Key > 0 && group.Count() > 1))
                    errors.Add("固高轴号不能被多个轴角色共用。");
            }

            ValidateOutput(OutputRole.InfraredEnable, Infrared != null && Infrared.Enabled, errors);
            ValidateOutput(OutputRole.VacuumEnable, validateAuxiliaryOutputs && EnableVacuum, errors);
            ValidateOutput(OutputRole.PowderFeedEnable, true, errors);
            if (HeadCleaning != null && HeadCleaning.Enabled)
            {
                ValidateOutput(OutputRole.PressInkEnable, true, errors);
                if (inkCar == null || !IsFinite(HeadCleaning.PressInkPositionMm) || HeadCleaning.PressInkPositionMm < inkCar.MinimumPositionMm || HeadCleaning.PressInkPositionMm > inkCar.MaximumPositionMm)
                    errors.Add("压墨位置超出墨车 X 行程。");
                if (inkCar == null || !IsFinite(HeadCleaning.CleanPositionMm) || HeadCleaning.CleanPositionMm < inkCar.MinimumPositionMm || HeadCleaning.CleanPositionMm > inkCar.MaximumPositionMm)
                    errors.Add("清洗位置超出墨车 X 行程。");
                if (!IsFinite(InkCarParkPositionMm) || Math.Abs(HeadCleaning.CleanPositionMm - InkCarParkPositionMm) > 0.001)
                    errors.Add("清洗位置必须与暂停/等待位置一致。");
                if (HeadCleaning.PressInkDurationMs < 1000 || HeadCleaning.PressInkDurationMs > 2000)
                    errors.Add("压墨时间必须位于1至2秒。");
                if (HeadCleaning.ScraperPulsesPerRevolution <= 0)
                    errors.Add("刮墨轴每转脉冲数未配置。");
                if (!IsFinite(HeadCleaning.ScraperWipeAngleDegrees) || HeadCleaning.ScraperWipeAngleDegrees <= 0 || HeadCleaning.ScraperWipeAngleDegrees > 360)
                    errors.Add("刮墨角度必须位于0至360度。");
                if (HeadCleaning.ScraperDirectionSign != -1 && HeadCleaning.ScraperDirectionSign != 1)
                    errors.Add("刮墨轴方向必须为-1或1。");
                if (!IsFinite(HeadCleaning.ScraperSpeedRevolutionsPerSecond) || HeadCleaning.ScraperSpeedRevolutionsPerSecond <= 0 || HeadCleaning.ScraperSpeedRevolutionsPerSecond > 2)
                    errors.Add("刮墨轴速度必须大于0且不超过2转/秒。");
                if (!IsFinite(HeadCleaning.ScraperAccelerationRevolutionsPerSecond2) || HeadCleaning.ScraperAccelerationRevolutionsPerSecond2 <= 0)
                    errors.Add("刮墨轴加速度必须为正数。");
            }
            DigitalOutputProfile feed = GetOutput(OutputRole.PowderFeedEnable);
            if (feed != null && feed.Channel > 0 && (feed.Channel != 11 || !feed.ActiveLow))
                errors.Add("落粉固定使用 EXO10（API 通道11），沿用原绿灯的低电平使能接线。");
            if (DigitalOutputs != null && DigitalOutputs.GroupBy(output => output.Role).Any(group => group.Count() > 1))
                errors.Add("每个数字输出角色只能配置一次。");
            if (DigitalOutputs != null && DigitalOutputs.Where(output => output.Channel > 0).GroupBy(output => output.Channel).Any(group => group.Count() > 1))
                errors.Add("数字输出通道不能被多个角色共用。");
            if (Infrared != null && Infrared.Enabled && Infrared.DacChannel <= 0)
                errors.Add("红外 DAC 通道未配置。");
            if (Infrared != null && (Infrared.DefaultPowerPercent < 0 || Infrared.DefaultPowerPercent > 100))
                errors.Add("红外默认功率必须位于 0 到 100%。");
            if (MeteorTelemetry != null && MeteorTelemetry.Enabled)
            {
                if (MeteorTelemetry.PccNumber <= 0 || MeteorTelemetry.HeadNumber <= 0)
                    errors.Add("Meteor 遥测必须配置有效的 PCC 与喷头编号。");
                if (String.IsNullOrWhiteSpace(MeteorTelemetry.TemperatureField) || MeteorTelemetry.TemperatureScale <= 0)
                    errors.Add("Meteor 温度字段或倍率未配置。");
                if (String.IsNullOrWhiteSpace(MeteorTelemetry.VoltageField) || MeteorTelemetry.VoltageScale <= 0)
                    errors.Add("Meteor 电压字段或倍率未配置。");
            }
            return errors;
        }

        private void ValidateOutput(OutputRole role, bool required, ICollection<string> errors)
        {
            DigitalOutputProfile output = GetOutput(role);
            if (required && (output == null || output.Channel <= 0))
                errors.Add(role + " 的固高 DO 通道未配置。");
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }

        public static SmallPrinterProfile Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("必须提供设备配置文件路径。", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException("未找到设备配置文件。", path);

            var serializer = new JavaScriptSerializer();
            SmallPrinterProfile profile = serializer.Deserialize<SmallPrinterProfile>(File.ReadAllText(path));
            if (profile == null)
                throw new InvalidDataException("设备配置文件为空或格式无效。");
            return profile;
        }

        public void Save(string path)
        {
            var serializer = new JavaScriptSerializer();
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(this));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }

    public sealed class SinglePassScanPlan
    {
        public readonly double LowMm, HighMm, VelocityMmPerSecond, PrintOffsetMm;
        public readonly SinglePassDirectionMode Mode;
        public int ScanCount { get { return Mode == SinglePassDirectionMode.PrintBothDirections ? 2 : 1; } }

        public SinglePassScanPlan(SmallPrinterProfile profile)
        {
            Mode = profile.DirectionMode;
            if (Mode != SinglePassDirectionMode.PrintBothDirections && Mode != SinglePassDirectionMode.PrintOutboundOnly)
                throw new InvalidOperationException("喷印模式无效。");
            AxisProfile ink = profile.GetAxis(AxisRole.InkCarX);
            LowMm = profile.InkCarPrintMinimumMm;
            HighMm = profile.InkCarPrintMaximumMm;
            VelocityMmPerSecond = ink.VelocityMmPerSecond;
            PrintOffsetMm = profile.PrintStartOffsetMm < 0 ? (HighMm - LowMm - profile.PlateWidthMm) / 2 : profile.PrintStartOffsetMm;
            if (Double.IsNaN(PrintOffsetMm) || Double.IsInfinity(PrintOffsetMm) || PrintOffsetMm < 0 || PrintOffsetMm + profile.PlateWidthMm > HighMm - LowMm)
                throw new InvalidOperationException("有效喷印窗口超出墨车机械行程。");
        }

        public bool TowardsHighEnd(int scanIndex)
        {
            if (scanIndex < 0 || scanIndex >= ScanCount) throw new ArgumentOutOfRangeException("scanIndex");
            return scanIndex == 0;
        }
        public double Start(int scanIndex) { return TowardsHighEnd(scanIndex) ? LowMm : HighMm; }
        public double End(int scanIndex) { return TowardsHighEnd(scanIndex) ? HighMm : LowMm; }
    }
}
