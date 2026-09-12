using System;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    /// <summary>
    /// Small-machine wipe cycle: X=155 mm press for 1.5 s, X=118 mm, then rotate
    /// scraper axis out and back to the planner position captured as the standby angle.
    /// </summary>
    public sealed class HeadCleaningWorkflow
    {
        private readonly SmallPrinterProfile _profile;
        private readonly GtsMotionController _motion;

        public HeadCleaningWorkflow(SmallPrinterProfile profile, GtsMotionController motion)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (motion == null) throw new ArgumentNullException("motion");
            _profile = profile;
            _motion = motion;
        }

        public void Execute(CancellationToken token, Action<string> report)
        {
            HeadCleaningProfile cleaning = _profile.HeadCleaning;
            if (cleaning == null || !cleaning.Enabled)
                throw new InvalidOperationException("自动刮墨配置未启用。");

            AxisProfile inkCar = _profile.GetAxis(AxisRole.InkCarX);
            if (inkCar == null) throw new InvalidOperationException("设备配置缺少墨车 X 轴。");
            if (cleaning.PressInkDurationMs < 1000 || cleaning.PressInkDurationMs > 2000)
                throw new InvalidOperationException("压墨时间必须位于1至2秒。");

            Report(report, "清洗开始：墨车前往压墨位 " + cleaning.PressInkPositionMm.ToString("F1") + " mm。");
            _motion.MoveAbsoluteLinear(AxisRole.InkCarX, cleaning.PressInkPositionMm, inkCar.VelocityMmPerSecond, token);

            try
            {
                token.ThrowIfCancellationRequested();
                _motion.SetDigitalOutput(OutputRole.PressInkEnable, true);
                Report(report, "压墨已开启，保持 " + (cleaning.PressInkDurationMs / 1000.0).ToString("F1") + " s。");
                token.WaitHandle.WaitOne(cleaning.PressInkDurationMs);
                token.ThrowIfCancellationRequested();
            }
            finally
            {
                _motion.SetDigitalOutput(OutputRole.PressInkEnable, false);
            }

            Report(report, "压墨已关闭，墨车前往清洗位 " + cleaning.CleanPositionMm.ToString("F1") + " mm。");
            _motion.MoveAbsoluteLinear(AxisRole.InkCarX, cleaning.CleanPositionMm, inkCar.VelocityMmPerSecond, token);
            Report(report, "墨车已在清洗位停稳，刮墨轴开始转动。");
            _motion.WipeScraperAndReturn(cleaning, token);
            Report(report, "刮墨轴已完成刮墨并返回待机角度，清洗流程结束。");
        }

        private static void Report(Action<string> report, string message)
        {
            if (report != null) report(message);
        }
    }
}
