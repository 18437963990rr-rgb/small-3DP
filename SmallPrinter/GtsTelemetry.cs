using System;
using System.Runtime.InteropServices;

namespace LaserAdd.SmallPrinter
{
    public sealed class GtsAxisTelemetry
    {
        public short AxisNumber { get; set; }
        public double RawPositionPulse { get; set; }
        public bool HasMillimeterPosition { get; set; }
        public double PositionMm { get; set; }
        public bool ServoAlarm { get; set; }
        public bool PositiveLimit { get; set; }
        public bool NegativeLimit { get; set; }
        public bool Moving { get; set; }
        public int RawStatus { get; set; }
    }

    /// <summary>只读固高状态读取器。状态位与现有 GoogolMotionMap 保持一致：正限位 0x20、负限位 0x40、运动中 0x400。</summary>
    public sealed class GtsTelemetryReader
    {
        private readonly GtsTelemetryProfile _profile;

        public GtsTelemetryReader(GtsTelemetryProfile profile)
        {
            _profile = profile;
        }

        public bool TryRead(AxisProfile axis, out GtsAxisTelemetry telemetry, out string error)
        {
            telemetry = null;
            if (_profile == null || !_profile.Enabled)
            {
                error = "固高遥测未启用";
                return false;
            }
            try
            {
                uint clock; int status; double position;
                short statusResult = GtsNative.GT_GetSts(_profile.CardNumber, axis.AxisNumber, out status, 1, out clock);
                if (statusResult != 0)
                {
                    error = "GT_GetSts(轴 " + axis.AxisNumber + ") 返回 " + statusResult;
                    return false;
                }
                short positionResult = GtsNative.GT_GetPrfPos(_profile.CardNumber, axis.AxisNumber, out position, 1, out clock);
                if (positionResult != 0)
                {
                    error = "GT_GetPrfPos(轴 " + axis.AxisNumber + ") 返回 " + positionResult;
                    return false;
                }

                int sign = axis.PositionSign == 0 ? 1 : axis.PositionSign;
                telemetry = new GtsAxisTelemetry
                {
                    AxisNumber = axis.AxisNumber,
                    RawPositionPulse = position,
                    HasMillimeterPosition = axis.PulsesPerMillimeter > 0,
                    PositionMm = axis.PulsesPerMillimeter > 0 ? position * sign / axis.PulsesPerMillimeter : 0,
                    ServoAlarm = (status & 0x02) != 0,
                    PositiveLimit = (status & 0x20) != 0,
                    NegativeLimit = (status & 0x40) != 0,
                    Moving = (status & 0x400) != 0,
                    RawStatus = status
                };
                error = null;
                return true;
            }
            catch (DllNotFoundException)
            {
                error = "未找到 gts.dll；请部署与固高控制卡匹配的 x64 驱动。";
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                error = "当前 gts.dll 不包含所需状态读取接口。";
                return false;
            }
            catch (Exception exception)
            {
                error = "固高状态读取失败：" + exception.GetBaseException().Message;
                return false;
            }
        }
    }

    internal static class GtsNative
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct TrapParameters
        {
            internal double Acceleration;
            internal double Deceleration;
            internal double StartVelocity;
            internal short SmoothTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JogParameters
        {
            internal double Acceleration;
            internal double Deceleration;
            internal double Smooth;
        }

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_Open(short cardNum, short channel, short param);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_Close(short cardNum);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_Reset(short cardNum);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        internal static extern short GT_LoadConfig(short cardNum, string path);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_EncOff(short cardNum, short encoder);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_ClrSts(short cardNum, short axis, short count);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_AxisOn(short cardNum, short axis);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_PrfTrap(short cardNum, short profile);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_PrfJog(short cardNum, short profile);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_SetJogPrm(short cardNum, short profile, ref JogParameters parameters);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_SetTrapPrm(short cardNum, short profile, ref TrapParameters parameters);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_SetPos(short cardNum, short profile, int position);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_SetVel(short cardNum, short profile, double velocity);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_Update(short cardNum, int mask);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_Stop(short cardNum, int mask, int option);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_GetSts(short cardNum, short axis, out int pSts, short count, out uint pClock);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_GetPrfPos(short cardNum, short profile, out double pValue, short count, out uint pClock);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_ZeroPos(short cardNum, short axis, short count);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_SetEncPos(short cardNum, short encoder, int position);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_SetPrfPos(short cardNum, short profile, int position);

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern short GT_SetDoBit(short cardNum, short type, short bitIndex, short level);
    }
}
