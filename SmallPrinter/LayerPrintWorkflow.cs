using System;
using System.Collections.Generic;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    public enum PrintStep
    {
        SafetyCheck,
        PowderFeed,
        Scraper,
        PowderSpread,
        BuildCylinderStep,
        RollerLevel,
        PrintSinglePass,
        InfraredCure,
        Vacuum,
        Completed,
        Stopped,
        Faulted
    }

    public interface ISmallPrinterMotion
    {
        void EnsureAxisHomed(short axisNumber);
        void MoveTo(short axisNumber, double positionMm, double velocityMmPerSecond);
        void WaitForIdle(short axisNumber, TimeSpan timeout, CancellationToken cancellationToken);
        void StopAll();
    }

    public interface ISmallPrinterIo
    {
        void SetDigitalOutput(short channel, bool energized, bool activeLow);
        void SetAnalogOutputPercent(short channel, double percent);
        void DisableAllOutputs();
    }

    public interface IMeteorSinglePassPrinter
    {
        // 返回时数据必须已被控制器接收，运动才可开始。每个方向复用同一层光栅，无 Y 步进。
        void PrepareScan(string rasterPath, int widthPixels, int heightPixels, bool towardsHighEnd, CancellationToken cancellationToken);
        // 在机械扫程结束后确认该方向喷印完成，结束控制器文档。
        void CompleteScan(CancellationToken cancellationToken);
        void Stop();
    }

    public sealed class LayerPrintRequest
    {
        public string RasterPath { get; set; }
        public int RasterWidthPixels { get; set; }
        public int RasterHeightPixels { get; set; }
        public double LayerThicknessMm { get; set; }
        public int PowderFeedDurationMs { get; set; }
        public double ScraperPositionMm { get; set; }
        public double PowderCarPositionMm { get; set; }
        public double BuildCylinderTargetMm { get; set; }
        public double RollerFrontPositionMm { get; set; }
        public double RollerRearPositionMm { get; set; }
        public int InfraredCureDurationMs { get; set; }
        public bool VacuumDuringPrint { get; set; }
        public bool VacuumAfterCure { get; set; }
    }

    /// <summary>
    /// 小型机单层顺序控制。固高和 Meteor 的具体适配由接口实现提供，
    /// 任何取消、异常或安全检查失败都会关闭输出并停止运动/喷墨。
    /// </summary>
    public sealed class LayerPrintWorkflow
    {
        private readonly SmallPrinterProfile _profile;
        private readonly ISmallPrinterMotion _motion;
        private readonly ISmallPrinterIo _io;
        private readonly IMeteorSinglePassPrinter _meteor;

        public LayerPrintWorkflow(
            SmallPrinterProfile profile,
            ISmallPrinterMotion motion,
            ISmallPrinterIo io,
            IMeteorSinglePassPrinter meteor)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (motion == null) throw new ArgumentNullException("motion");
            if (io == null) throw new ArgumentNullException("io");
            if (meteor == null) throw new ArgumentNullException("meteor");
            _profile = profile;
            _motion = motion;
            _io = io;
            _meteor = meteor;
        }

        public event Action<PrintStep> StepChanged;

        public void PrintLayer(LayerPrintRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");
            try
            {
                Report(PrintStep.SafetyCheck);
                EnsureProfileIsRunnable();
                var scanPlan = new SinglePassScanPlan(_profile);
                RasterValidator.ValidateOrThrow(_profile, request.RasterWidthPixels, request.RasterHeightPixels);
                if (request.LayerThicknessMm <= 0)
                    throw new InvalidOperationException("层厚必须为正数。");
                int feedDuration = request.PowderFeedDurationMs > 0 ? request.PowderFeedDurationMs : _profile.PowderFeedDurationMs;
                if (feedDuration <= 0)
                    throw new InvalidOperationException("推动式落粉开启时间必须为正数。");

                HomeRequiredAxes(cancellationToken);
                MoveAxis(AxisRole.InkCarX, _profile.InkCarParkPositionMm, cancellationToken, PrintStep.SafetyCheck);
                MoveAxis(AxisRole.PowderCar, GetAxis(AxisRole.PowderCar).HomePositionMm, cancellationToken, PrintStep.PowderFeed);
                FeedPowder(feedDuration, cancellationToken);
                MoveAxis(AxisRole.Scraper, request.ScraperPositionMm, cancellationToken, PrintStep.Scraper);
                MoveAxis(AxisRole.PowderCar, request.PowderCarPositionMm, cancellationToken, PrintStep.PowderSpread);
                MoveAxis(AxisRole.BuildCylinder, request.BuildCylinderTargetMm, cancellationToken, PrintStep.BuildCylinderStep);
                MoveAxis(AxisRole.RollerFront, request.RollerFrontPositionMm, cancellationToken, PrintStep.RollerLevel);
                MoveAxis(AxisRole.RollerRear, request.RollerRearPositionMm, cancellationToken, PrintStep.RollerLevel);
                MoveAxis(AxisRole.PowderCar, GetAxis(AxisRole.PowderCar).HomePositionMm, cancellationToken, PrintStep.PowderSpread);

                SetOutput(OutputRole.VacuumEnable, request.VacuumDuringPrint);
                Report(PrintStep.PrintSinglePass);
                for (int scan = 0; scan < scanPlan.ScanCount; scan++)
                {
                    MoveAxis(AxisRole.InkCarX, scanPlan.Start(scan), cancellationToken, PrintStep.PrintSinglePass, scanPlan.VelocityMmPerSecond);
                    _meteor.PrepareScan(request.RasterPath, request.RasterWidthPixels, request.RasterHeightPixels, scanPlan.TowardsHighEnd(scan), cancellationToken);
                    MoveAxis(AxisRole.InkCarX, scanPlan.End(scan), cancellationToken, PrintStep.PrintSinglePass, scanPlan.VelocityMmPerSecond);
                    _meteor.CompleteScan(cancellationToken);
                }
                MoveAxis(AxisRole.InkCarX, _profile.InkCarParkPositionMm, cancellationToken, PrintStep.PrintSinglePass, scanPlan.VelocityMmPerSecond);

                CureWithInfrared(request.InfraredCureDurationMs, cancellationToken);
                SetOutput(OutputRole.VacuumEnable, request.VacuumAfterCure);
                Report(PrintStep.Vacuum);
                Report(PrintStep.Completed);
            }
            catch (OperationCanceledException)
            {
                StopSafely();
                Report(PrintStep.Stopped);
                throw;
            }
            catch
            {
                StopSafely();
                Report(PrintStep.Faulted);
                throw;
            }
            finally
            {
                SetOutput(OutputRole.VacuumEnable, false);
                SetOutput(OutputRole.InfraredEnable, false);
                if (_profile.Infrared != null && _profile.Infrared.Enabled)
                    _io.SetAnalogOutputPercent(_profile.Infrared.DacChannel, 0);
            }
        }

        public void StopSafely()
        {
            try { _meteor.Stop(); } catch { }
            try { _motion.StopAll(); } catch { }
            try { _io.DisableAllOutputs(); } catch { }
        }

        private void EnsureProfileIsRunnable()
        {
            IList<string> errors = _profile.Validate();
            if (errors.Count != 0)
                throw new InvalidOperationException("设备配置不允许联机：" + String.Join("；", errors));
        }

        private void FeedPowder(int durationMs, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(PrintStep.PowderFeed);
            try
            {
                SetOutput(OutputRole.PowderFeedEnable, true);
                cancellationToken.WaitHandle.WaitOne(durationMs);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                SetOutput(OutputRole.PowderFeedEnable, false);
            }
        }

        private void HomeRequiredAxes(CancellationToken cancellationToken)
        {
            foreach (AxisRole role in (AxisRole[])Enum.GetValues(typeof(AxisRole)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _motion.EnsureAxisHomed(GetAxis(role).AxisNumber);
            }
        }

        private void MoveAxis(AxisRole role, double targetMm, CancellationToken cancellationToken, PrintStep step, double? velocityOverride = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AxisProfile axis = GetAxis(role);
            if (Double.IsNaN(targetMm) || Double.IsInfinity(targetMm) || targetMm < axis.MinimumPositionMm || targetMm > axis.MaximumPositionMm)
                throw new InvalidOperationException(role + " 的目标坐标超出配置行程。");

            Report(step);
            _motion.MoveTo(axis.AxisNumber, targetMm, velocityOverride ?? axis.VelocityMmPerSecond);
            _motion.WaitForIdle(axis.AxisNumber, TimeSpan.FromSeconds(30), cancellationToken);
        }

        private void CureWithInfrared(int durationMs, CancellationToken cancellationToken)
        {
            if (_profile.Infrared == null || !_profile.Infrared.Enabled)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            Report(PrintStep.InfraredCure);
            _io.SetAnalogOutputPercent(_profile.Infrared.DacChannel, _profile.Infrared.DefaultPowerPercent);
            SetOutput(OutputRole.InfraredEnable, true);
            if (durationMs <= 0)
                throw new InvalidOperationException("红外固化时间必须为正数。");
            cancellationToken.WaitHandle.WaitOne(durationMs);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private void SetOutput(OutputRole role, bool enabled)
        {
            DigitalOutputProfile output = _profile.GetOutput(role);
            if (output != null && output.Channel > 0)
                _io.SetDigitalOutput(output.Channel, enabled, output.ActiveLow);
        }

        private AxisProfile GetAxis(AxisRole role)
        {
            AxisProfile axis = _profile.GetAxis(role);
            if (axis == null)
                throw new InvalidOperationException("设备配置缺少轴角色：" + role);
            return axis;
        }

        private void Report(PrintStep step)
        {
            Action<PrintStep> handler = StepChanged;
            if (handler != null)
                handler(step);
        }
    }
}
