using System;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    /// <summary>
    /// 手动操作只接受轴角色，不允许界面或调用方直接使用固高轴号。
    /// 这样更换电气接线时只需更新设备清单，不会误动错误的机械部件。
    /// </summary>
    public sealed class ManualMotionController
    {
        private readonly SmallPrinterProfile _profile;
        private readonly ISmallPrinterMotion _motion;

        public ManualMotionController(SmallPrinterProfile profile, ISmallPrinterMotion motion)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (motion == null) throw new ArgumentNullException("motion");
            _profile = profile;
            _motion = motion;
        }

        public void Home(AxisRole role, CancellationToken cancellationToken)
        {
            AxisProfile axis = GetAxis(role);
            cancellationToken.ThrowIfCancellationRequested();
            CheckTarget(axis, axis.HomePositionMm);
            _motion.EnsureAxisHomed(axis.AxisNumber);
            _motion.MoveTo(axis.AxisNumber, axis.HomePositionMm, axis.VelocityMmPerSecond);
            _motion.WaitForIdle(axis.AxisNumber, TimeSpan.FromSeconds(30), cancellationToken);
        }

        public void MoveTo(AxisRole role, double targetMm, CancellationToken cancellationToken)
        {
            AxisProfile axis = GetAxis(role);
            CheckTarget(axis, targetMm);

            cancellationToken.ThrowIfCancellationRequested();
            _motion.EnsureAxisHomed(axis.AxisNumber);
            _motion.MoveTo(axis.AxisNumber, targetMm, axis.VelocityMmPerSecond);
            _motion.WaitForIdle(axis.AxisNumber, TimeSpan.FromSeconds(30), cancellationToken);
        }

        public void Stop()
        {
            _motion.StopAll();
        }

        public void ParkInkCar(CancellationToken cancellationToken)
        {
            MoveTo(AxisRole.InkCarX, _profile.InkCarParkPositionMm, cancellationToken);
        }

        private static void CheckTarget(AxisProfile axis, double targetMm)
        {
            if (Double.IsNaN(targetMm) || Double.IsInfinity(targetMm)
                || targetMm < axis.MinimumPositionMm || targetMm > axis.MaximumPositionMm)
                throw new ArgumentOutOfRangeException("targetMm", "目标坐标超出配置行程。");
        }

        private AxisProfile GetAxis(AxisRole role)
        {
            AxisProfile axis = _profile.GetAxis(role);
            if (axis == null || axis.AxisNumber <= 0)
                throw new InvalidOperationException("轴角色未完成配置：" + role);
            return axis;
        }
    }
}
