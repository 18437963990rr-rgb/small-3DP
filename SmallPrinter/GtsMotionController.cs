using System;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    /// <summary>Safe subset of the legacy GTS motion path used by the standalone UI.</summary>
    public sealed class GtsMotionController
    {
        private readonly SmallPrinterProfile _profile;
        private readonly GtsControllerSession _session;
        private readonly object _sync = new object();

        public GtsMotionController(SmallPrinterProfile profile, GtsControllerSession session)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (session == null) throw new ArgumentNullException("session");
            _profile = profile;
            _session = session;
        }

        public void MoveRelative(AxisRole role, double distanceMm, double speedMmPerSecond, CancellationToken token)
        {
            lock (_sync)
            {
                EnsureReady();
                AxisProfile axis = _profile.GetAxis(role);
                if (axis == null) throw new InvalidOperationException("设备配置缺少轴：" + role);
                if (role != AxisRole.InkCarX && role != AxisRole.PowderCar)
                    throw new InvalidOperationException("该轴的步进细分和传动参数尚未确认，禁止从新 UI 点动：" + role);
                if (axis.PulsesPerMillimeter <= 0) throw new InvalidOperationException(role + " 未配置每毫米脉冲数。");
                if (distanceMm == 0 || Double.IsNaN(distanceMm) || Double.IsInfinity(distanceMm))
                    throw new ArgumentOutOfRangeException("distanceMm");
                if (speedMmPerSecond <= 0 || Double.IsNaN(speedMmPerSecond) || Double.IsInfinity(speedMmPerSecond))
                    throw new ArgumentOutOfRangeException("speedMmPerSecond");

                int status; uint clock; double currentPulse;
                Check("GT_GetSts", GtsNative.GT_GetSts(_profile.GtsTelemetry.CardNumber, axis.AxisNumber, out status, 1, out clock));
                Check("GT_GetPrfPos", GtsNative.GT_GetPrfPos(_profile.GtsTelemetry.CardNumber, axis.AxisNumber, out currentPulse, 1, out clock));
                if ((status & 0x02) != 0) throw new InvalidOperationException(role + " 驱动报警。");
                if ((status & 0x400) != 0) throw new InvalidOperationException(role + " 正在运动。");
                if (distanceMm > 0 && (status & 0x20) != 0) throw new InvalidOperationException(role + " 正限位已触发。");
                if (distanceMm < 0 && (status & 0x40) != 0) throw new InvalidOperationException(role + " 负限位已触发。");

                int sign = axis.PositionSign == 0 ? 1 : axis.PositionSign;
                double currentMm = currentPulse * sign / axis.PulsesPerMillimeter;
                double targetMm = currentMm + distanceMm;
                if (targetMm < axis.MinimumPositionMm || targetMm > axis.MaximumPositionMm)
                    throw new InvalidOperationException(role + " 目标 " + targetMm.ToString("F3") + " mm 超出软限位。");

                int targetPulse = checked((int)Math.Round(targetMm * axis.PulsesPerMillimeter / sign));
                double velocityPulsePerMs = speedMmPerSecond * axis.PulsesPerMillimeter / 1000.0;
                double accelerationPulsePerMs2 = Math.Max(0.01, axis.AccelerationMmPerSecond2 * axis.PulsesPerMillimeter / 1000000.0);
                var trap = new GtsNative.TrapParameters
                {
                    Acceleration = accelerationPulsePerMs2,
                    Deceleration = accelerationPulsePerMs2,
                    StartVelocity = 0,
                    SmoothTime = 25
                };

                token.ThrowIfCancellationRequested();
                short card = _profile.GtsTelemetry.CardNumber;
                Check("GT_ClrSts", GtsNative.GT_ClrSts(card, axis.AxisNumber, 1));
                Check("GT_AxisOn", GtsNative.GT_AxisOn(card, axis.AxisNumber));
                Check("GT_PrfTrap", GtsNative.GT_PrfTrap(card, axis.AxisNumber));
                Check("GT_SetTrapPrm", GtsNative.GT_SetTrapPrm(card, axis.AxisNumber, ref trap));
                Check("GT_SetPos", GtsNative.GT_SetPos(card, axis.AxisNumber, targetPulse));
                Check("GT_SetVel", GtsNative.GT_SetVel(card, axis.AxisNumber, velocityPulsePerMs));
                Check("GT_Update", GtsNative.GT_Update(card, 1 << (axis.AxisNumber - 1)));
                WaitForIdle(axis.AxisNumber, TimeSpan.FromSeconds(30), token);
            }
        }

        public void MoveAbsoluteLinear(AxisRole role, double targetMm, double speedMmPerSecond, CancellationToken token)
        {
            lock (_sync)
            {
                EnsureReady();
                AxisProfile axis = _profile.GetAxis(role);
                if (axis == null) throw new InvalidOperationException("设备配置缺少轴：" + role);
                if (role != AxisRole.InkCarX && role != AxisRole.PowderCar)
                    throw new InvalidOperationException("该接口仅用于直线运动轴。" + role);
                if (axis.PulsesPerMillimeter <= 0) throw new InvalidOperationException(role + " 未配置每毫米脉冲数。");
                if (targetMm < axis.MinimumPositionMm || targetMm > axis.MaximumPositionMm)
                    throw new InvalidOperationException(role + " 目标 " + targetMm.ToString("F3") + " mm 超出软限位。");
                if (speedMmPerSecond <= 0 || Double.IsNaN(speedMmPerSecond) || Double.IsInfinity(speedMmPerSecond))
                    throw new ArgumentOutOfRangeException("speedMmPerSecond");

                int status; uint clock; double currentPulse;
                short card = _profile.GtsTelemetry.CardNumber;
                Check("GT_GetSts", GtsNative.GT_GetSts(card, axis.AxisNumber, out status, 1, out clock));
                Check("GT_GetPrfPos", GtsNative.GT_GetPrfPos(card, axis.AxisNumber, out currentPulse, 1, out clock));
                int sign = axis.PositionSign == 0 ? 1 : axis.PositionSign;
                double currentMm = currentPulse * sign / axis.PulsesPerMillimeter;
                if (currentMm < axis.MinimumPositionMm - 0.1 || currentMm > axis.MaximumPositionMm + 0.1)
                    throw new InvalidOperationException(role + " 尚未建立有效逻辑坐标，当前位置 " + currentMm.ToString("F3") + " mm 不在配置行程内，禁止绝对运动。");
                if ((status & 0x02) != 0) throw new InvalidOperationException(role + " 驱动报警。");
                if ((status & 0x400) != 0) throw new InvalidOperationException(role + " 正在运动。");

                int targetPulse = checked((int)Math.Round(targetMm * axis.PulsesPerMillimeter / sign));
                double velocityPulsePerMs = speedMmPerSecond * axis.PulsesPerMillimeter / 1000.0;
                double accelerationPulsePerMs2 = Math.Max(0.01, axis.AccelerationMmPerSecond2 * axis.PulsesPerMillimeter / 1000000.0);
                MoveTrapPulse(axis, targetPulse, velocityPulsePerMs, accelerationPulsePerMs2, token);
            }
        }

        public void WipeScraperAndReturn(HeadCleaningProfile cleaning, CancellationToken token)
        {
            if (cleaning == null) throw new ArgumentNullException("cleaning");
            lock (_sync)
            {
                EnsureReady();
                AxisProfile axis = _profile.GetAxis(AxisRole.Scraper);
                if (axis == null) throw new InvalidOperationException("设备配置缺少刮墨轴。");
                if (cleaning.ScraperPulsesPerRevolution <= 0) throw new InvalidOperationException("刮墨轴每转脉冲数未配置。");
                if (cleaning.ScraperDirectionSign != -1 && cleaning.ScraperDirectionSign != 1) throw new InvalidOperationException("刮墨轴方向必须为-1或1。");

                short card = _profile.GtsTelemetry.CardNumber;
                int status; uint clock; double initialPosition;
                Check("GT_GetSts", GtsNative.GT_GetSts(card, axis.AxisNumber, out status, 1, out clock));
                if ((status & 0x02) != 0) throw new InvalidOperationException("刮墨轴驱动报警。");
                if ((status & 0x400) != 0) throw new InvalidOperationException("刮墨轴正在运动。");
                Check("GT_GetPrfPos", GtsNative.GT_GetPrfPos(card, axis.AxisNumber, out initialPosition, 1, out clock));

                int standbyPulse = checked((int)Math.Round(initialPosition));
                int wipeDeltaPulse = checked((int)Math.Round(cleaning.ScraperDirectionSign * cleaning.ScraperWipeAngleDegrees / 360.0 * cleaning.ScraperPulsesPerRevolution));
                int wipePulse = checked(standbyPulse + wipeDeltaPulse);
                double wipeDegrees = wipePulse * 360.0 / cleaning.ScraperPulsesPerRevolution;
                if (wipeDegrees < axis.MinimumPositionMm || wipeDegrees > axis.MaximumPositionMm)
                    throw new InvalidOperationException("刮墨轴目标角度超出配置软限位。");

                double velocityPulsePerMs = cleaning.ScraperSpeedRevolutionsPerSecond * cleaning.ScraperPulsesPerRevolution / 1000.0;
                double accelerationPulsePerMs2 = Math.Max(0.01, cleaning.ScraperAccelerationRevolutionsPerSecond2 * cleaning.ScraperPulsesPerRevolution / 1000000.0);
                MoveTrapPulse(axis, wipePulse, velocityPulsePerMs, accelerationPulsePerMs2, token);
                MoveTrapPulse(axis, standbyPulse, velocityPulsePerMs, accelerationPulsePerMs2, token);

                double returnedPosition;
                Check("GT_GetPrfPos", GtsNative.GT_GetPrfPos(card, axis.AxisNumber, out returnedPosition, 1, out clock));
                double tolerancePulse = Math.Max(5.0, cleaning.ScraperPulsesPerRevolution / 1440.0);
                if (Math.Abs(returnedPosition - standbyPulse) > tolerancePulse)
                    throw new InvalidOperationException("刮墨轴未返回清洗开始时的待机角度。");
            }
        }

        public void SetDigitalOutput(OutputRole role, bool energized)
        {
            lock (_sync)
            {
                EnsureReady();
                DigitalOutputProfile output = _profile.GetOutput(role);
                if (output == null || output.Channel <= 0) throw new InvalidOperationException(role + " 的数字输出未配置。");
                short level = (short)(output.ActiveLow ? (energized ? 0 : 1) : (energized ? 1 : 0));
                Check("GT_SetDoBit", GtsNative.GT_SetDoBit(_profile.GtsTelemetry.CardNumber, 12, output.Channel, level));
            }
        }

        private void MoveTrapPulse(AxisProfile axis, int targetPulse, double velocityPulsePerMs, double accelerationPulsePerMs2, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            short card = _profile.GtsTelemetry.CardNumber;
            int status; uint clock;
            Check("GT_GetSts", GtsNative.GT_GetSts(card, axis.AxisNumber, out status, 1, out clock));
            if ((status & 0x02) != 0) throw new InvalidOperationException("轴 " + axis.AxisNumber + " 驱动报警。");
            if ((status & 0x400) != 0) throw new InvalidOperationException("轴 " + axis.AxisNumber + " 正在运动。");
            Check("GT_ClrSts", GtsNative.GT_ClrSts(card, axis.AxisNumber, 1));
            Check("GT_AxisOn", GtsNative.GT_AxisOn(card, axis.AxisNumber));
            Check("GT_PrfTrap", GtsNative.GT_PrfTrap(card, axis.AxisNumber));
            var trap = new GtsNative.TrapParameters
            {
                Acceleration = accelerationPulsePerMs2,
                Deceleration = accelerationPulsePerMs2,
                StartVelocity = 0,
                SmoothTime = 25
            };
            Check("GT_SetTrapPrm", GtsNative.GT_SetTrapPrm(card, axis.AxisNumber, ref trap));
            Check("GT_SetPos", GtsNative.GT_SetPos(card, axis.AxisNumber, targetPulse));
            Check("GT_SetVel", GtsNative.GT_SetVel(card, axis.AxisNumber, Math.Abs(velocityPulsePerMs)));
            Check("GT_Update", GtsNative.GT_Update(card, 1 << (axis.AxisNumber - 1)));
            WaitForIdle(axis.AxisNumber, TimeSpan.FromSeconds(30), token);
        }

        public void StartJog(AxisRole role, int direction, double speedScale)
        {
            lock (_sync)
            {
                EnsureReady();
                AxisProfile axis = _profile.GetAxis(role);
                if (axis == null) throw new InvalidOperationException("设备配置缺少轴：" + role);
                if (role != AxisRole.InkCarX && role != AxisRole.PowderCar)
                    throw new InvalidOperationException("该轴的参数未确认，禁止点动：" + role);
                if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException("direction");
                if (axis.PulsesPerMillimeter <= 0) throw new InvalidOperationException(role + " 未配置每毫米脉冲数。");

                short card = _profile.GtsTelemetry.CardNumber;
                int status; uint clock;
                Check("GT_GetSts", GtsNative.GT_GetSts(card, axis.AxisNumber, out status, 1, out clock));
                if ((status & 0x02) != 0) throw new InvalidOperationException(role + " 驱动报警。");
                if (direction > 0 && (status & 0x20) != 0) throw new InvalidOperationException(role + " 正限位已触发。");
                if (direction < 0 && (status & 0x40) != 0) throw new InvalidOperationException(role + " 负限位已触发。");

                double acceleration = Math.Max(0.01, axis.AccelerationMmPerSecond2 * axis.PulsesPerMillimeter / 1000000.0);
                var jog = new GtsNative.JogParameters { Acceleration = acceleration, Deceleration = acceleration, Smooth = 0 };
                double velocity = direction * axis.VelocityMmPerSecond * Math.Max(0.05, Math.Min(1.0, speedScale)) * axis.PulsesPerMillimeter / 1000.0;
                Check("GT_Stop", GtsNative.GT_Stop(card, 1 << (axis.AxisNumber - 1), 1 << (axis.AxisNumber - 1)));
                Check("GT_ClrSts", GtsNative.GT_ClrSts(card, axis.AxisNumber, 1));
                Check("GT_AxisOn", GtsNative.GT_AxisOn(card, axis.AxisNumber));
                Check("GT_PrfJog", GtsNative.GT_PrfJog(card, axis.AxisNumber));
                Check("GT_SetJogPrm", GtsNative.GT_SetJogPrm(card, axis.AxisNumber, ref jog));
                Check("GT_SetVel", GtsNative.GT_SetVel(card, axis.AxisNumber, velocity));
                Check("GT_Update", GtsNative.GT_Update(card, 1 << (axis.AxisNumber - 1)));
            }
        }

        /// <summary>
        /// 轴7反向寻找负限位；触发后正向退出20 mm，并把该机械点写成配置中的逻辑原点。
        /// 铺粉车是开环步进轴，因此全程使用规划/内部脉冲位置，不依赖 Home 捕获或外部编码器。
        /// </summary>
        public void HomePowderCar(CancellationToken token)
        {
            lock (_sync)
            {
                EnsureReady();
                AxisProfile axis = _profile.GetAxis(AxisRole.PowderCar);
                if (axis == null) throw new InvalidOperationException("设备配置缺少铺粉车轴。");
                if (axis.AxisNumber != 7) throw new InvalidOperationException("铺粉车负限位回零仅允许固高轴7。");
                if (axis.PulsesPerMillimeter <= 0) throw new InvalidOperationException("铺粉车未配置每毫米脉冲数。");

                short card = _profile.GtsTelemetry.CardNumber;
                int mask = 1 << (axis.AxisNumber - 1);
                double acceleration = Math.Max(0.01, axis.AccelerationMmPerSecond2 * axis.PulsesPerMillimeter / 1000000.0);
                double searchVelocity = -Math.Min(20.0, axis.VelocityMmPerSecond) * axis.PulsesPerMillimeter / 1000.0;
                int retreatPulse = checked((int)Math.Round(20.0 * axis.PulsesPerMillimeter));
                int sign = axis.PositionSign == 0 ? 1 : axis.PositionSign;
                int homePulse = checked((int)Math.Round(axis.HomePositionMm * axis.PulsesPerMillimeter / sign));
                bool completed = false;

                try
                {
                    token.ThrowIfCancellationRequested();
                    Check("GT_Stop", GtsNative.GT_Stop(card, mask, mask));
                    Check("GT_EncOff", GtsNative.GT_EncOff(card, axis.AxisNumber));
                    Check("GT_AxisOn", GtsNative.GT_AxisOn(card, axis.AxisNumber));

                    int status; uint clock;
                    Check("GT_GetSts", GtsNative.GT_GetSts(card, axis.AxisNumber, out status, 1, out clock));
                    bool negativeLimitReached = (status & 0x40) != 0;
                    if ((status & 0x02) != 0) throw new InvalidOperationException("铺粉车驱动报警，不能回零。");

                    Check("GT_ClrSts", GtsNative.GT_ClrSts(card, axis.AxisNumber, 1));
                    if (!negativeLimitReached)
                    {
                        var jog = new GtsNative.JogParameters { Acceleration = acceleration, Deceleration = acceleration, Smooth = 0 };
                        Check("GT_PrfJog", GtsNative.GT_PrfJog(card, axis.AxisNumber));
                        Check("GT_SetJogPrm", GtsNative.GT_SetJogPrm(card, axis.AxisNumber, ref jog));
                        Check("GT_SetVel", GtsNative.GT_SetVel(card, axis.AxisNumber, searchVelocity));
                        Check("GT_Update", GtsNative.GT_Update(card, mask));

                        DateTime searchDeadline = DateTime.UtcNow.AddSeconds(30);
                        while (DateTime.UtcNow <= searchDeadline)
                        {
                            token.ThrowIfCancellationRequested();
                            Check("GT_GetSts", GtsNative.GT_GetSts(card, axis.AxisNumber, out status, 1, out clock));
                            if ((status & 0x02) != 0) throw new InvalidOperationException("铺粉车寻负限位时驱动报警。");
                            if ((status & 0x40) != 0) { negativeLimitReached = true; break; }
                            if ((status & 0x400) == 0) throw new InvalidOperationException("铺粉车运动已停止，但负限位未触发。");
                            token.WaitHandle.WaitOne(20);
                        }
                        if (!negativeLimitReached) throw new TimeoutException("铺粉车反向寻找负限位超时。");
                    }

                    Check("GT_Stop", GtsNative.GT_Stop(card, mask, mask));
                    Check("GT_ClrSts", GtsNative.GT_ClrSts(card, axis.AxisNumber, 1));
                    Check("GT_ZeroPos", GtsNative.GT_ZeroPos(card, axis.AxisNumber, 1));
                    Check("GT_SetEncPos", GtsNative.GT_SetEncPos(card, axis.AxisNumber, 0));
                    Check("GT_SetPrfPos", GtsNative.GT_SetPrfPos(card, axis.AxisNumber, 0));

                    var trap = new GtsNative.TrapParameters
                    {
                        Acceleration = acceleration,
                        Deceleration = acceleration,
                        StartVelocity = 0,
                        SmoothTime = 25
                    };
                    Check("GT_PrfTrap", GtsNative.GT_PrfTrap(card, axis.AxisNumber));
                    Check("GT_SetTrapPrm", GtsNative.GT_SetTrapPrm(card, axis.AxisNumber, ref trap));
                    Check("GT_SetPos", GtsNative.GT_SetPos(card, axis.AxisNumber, retreatPulse));
                    Check("GT_SetVel", GtsNative.GT_SetVel(card, axis.AxisNumber, Math.Abs(searchVelocity)));
                    Check("GT_Update", GtsNative.GT_Update(card, mask));
                    WaitForIdle(axis.AxisNumber, TimeSpan.FromSeconds(15), token);

                    Check("GT_GetSts", GtsNative.GT_GetSts(card, axis.AxisNumber, out status, 1, out clock));
                    if ((status & 0x40) != 0) throw new InvalidOperationException("铺粉车正向退出20 mm后负限位仍未释放。");
                    double position;
                    Check("GT_GetPrfPos", GtsNative.GT_GetPrfPos(card, axis.AxisNumber, out position, 1, out clock));
                    double tolerancePulse = Math.Max(30.0, axis.PulsesPerMillimeter * 0.1);
                    if (Math.Abs(position - retreatPulse) > tolerancePulse)
                        throw new InvalidOperationException("铺粉车退出负限位后的到位误差超限。");

                    Check("GT_ZeroPos", GtsNative.GT_ZeroPos(card, axis.AxisNumber, 1));
                    Check("GT_SetEncPos", GtsNative.GT_SetEncPos(card, axis.AxisNumber, homePulse));
                    Check("GT_SetPrfPos", GtsNative.GT_SetPrfPos(card, axis.AxisNumber, homePulse));
                    completed = true;
                }
                finally
                {
                    if (!completed)
                    {
                        try { GtsNative.GT_Stop(card, mask, mask); } catch { }
                    }
                }
            }
        }

        public void StopAxis(AxisRole role)
        {
            AxisProfile axis = _profile.GetAxis(role);
            if (axis == null || !_session.IsInitialized) return;
            Check("GT_Stop", GtsNative.GT_Stop(_profile.GtsTelemetry.CardNumber, 1 << (axis.AxisNumber - 1), 1 << (axis.AxisNumber - 1)));
        }

        public void StopAll()
        {
            if (!_session.IsInitialized) return;
            Check("GT_Stop", GtsNative.GT_Stop(_profile.GtsTelemetry.CardNumber, 0xFF, 0xFF));
        }

        private void WaitForIdle(short axis, TimeSpan timeout, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow <= deadline)
            {
                token.ThrowIfCancellationRequested();
                int status; uint clock;
                Check("GT_GetSts", GtsNative.GT_GetSts(_profile.GtsTelemetry.CardNumber, axis, out status, 1, out clock));
                if ((status & 0x02) != 0) throw new InvalidOperationException("轴 " + axis + " 运动中驱动报警。");
                if ((status & 0x400) == 0) return;
                token.WaitHandle.WaitOne(20);
            }
            try { GtsNative.GT_Stop(_profile.GtsTelemetry.CardNumber, 1 << (axis - 1), 1 << (axis - 1)); } catch { }
            throw new TimeoutException("轴 " + axis + " 运动超时。");
        }

        private void EnsureReady()
        {
            if (!_session.IsInitialized) throw new InvalidOperationException("固高卡未完成初始化。");
        }

        private static void Check(string operation, short result)
        {
            if (result != 0) throw new InvalidOperationException(operation + " 失败，固高返回码：" + result);
        }
    }
}
