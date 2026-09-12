using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    /// <summary>EXO10 是零基编号；原绿灯的 GT_SetDoBit bitIndex 为 11，0=得电，1=失电。</summary>
    public static class PowderFeedOutput
    {
        public const short ApiChannel = 11;
        private static readonly object Sync = new object();
        private static readonly ManualResetEventSlim Closed = new ManualResetEventSlim(true);
        public static bool IsOpen { get; private set; }

        [DllImport("gts.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern short GT_SetDoBit(short card, short type, short bitIndex, short level);
        internal static Func<short, short, short, short, short> WriteOutput = GT_SetDoBit;

        public static void Set(bool open)
        {
            lock (Sync)
            {
                // 关闭请求先取消等待，哪怕驱动写失败，自动接粉也不得继续。
                if (!open) Closed.Set();
                short result = WriteOutput(0, 12, ApiChannel, (short)(open ? 0 : 1));
                if (result != 0) throw new InvalidOperationException("EXO10 落粉输出失败：" + result);
                IsOpen = open;
                if (open) Closed.Reset();
            }
        }

        public static void Feed(int milliseconds)
        {
            if (milliseconds <= 0) throw new InvalidOperationException("请设置落粉开启时间（ms）。");
            try
            {
                Set(true);
                if (Closed.Wait(milliseconds)) throw new OperationCanceledException("落粉已停止。");
            }
            finally { Set(false); }
        }
    }
}
