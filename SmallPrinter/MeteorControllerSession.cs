using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    /// <summary>Standalone owner of the native Meteor Printer Interface connection and scan job.</summary>
    public sealed class MeteorControllerSession : IMeteorSinglePassPrinter, IDisposable
    {
        private const int RvalOk = 0;
        private const int RvalBusy = 0x0E;
        private const int RvalFull = 0x10;
        private const int RvalNoPrinter = 0x12;
        private const uint PcmdStartJob = 0x7A5535A1;
        private const uint PcmdImage = 0x7A5535A4;
        private const uint PcmdEndDoc = 0x7A5535A6;
        private const uint PcmdEndJob = 0x7A5535A7;
        private const uint PcmdStartScan = 0x7A5535A9;
        private const uint JobTypeScan = 4;
        private const uint ResolutionHigh = 1;
        private const uint ScanForward = 0;
        private const uint ScanReverse = 1;
        private const uint SignalSpit = 0x0B;
        private readonly object _sync = new object();
        private readonly SmallPrinterProfile _profile;
        private bool _jobActive;
        private bool _scanPending;
        private bool _homeEstablished;
        private int _preparedScans;
        private int _completedScans;
        private int _expectedScans;
        private int? _windowHighPixels;

        public MeteorControllerSession(SmallPrinterProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            _profile = profile;
        }

        public bool IsConnected { get; private set; }
        public bool IsJobActive { get { return _jobActive; } }
        public string ApiDirectory { get; private set; }

        public void Initialize()
        {
            lock (_sync)
            {
                if (IsConnected) return;
                MeteorTelemetryProfile settings = _profile.MeteorTelemetry;
                if (settings == null || !settings.Enabled)
                    throw new InvalidOperationException("Meteor 配置未启用。");

                string apiDirectory = FindApiDirectory(settings.ApiDirectory);
                if (apiDirectory == null)
                    throw new FileNotFoundException("未找到 Meteor PrinterInterface.dll，请配置 MeteorTelemetry.ApiDirectory。");
                if (!NativeMethods.SetDllDirectory(apiDirectory))
                    throw new InvalidOperationException("无法设置 Meteor API 加载目录：" + apiDirectory);

                int result = NativeMethods.PiOpenPrinter();
                if (result == RvalNoPrinter)
                {
                    string engineConfig = ResolvePrintEngineConfig(settings, apiDirectory);
                    if (engineConfig == null)
                        throw new FileNotFoundException("PrintEngine 未运行，且未找到 Meteor PrintEngine CFG。");
                    Check("PiStartPrintEngine", NativeMethods.PiStartPrintEngine(engineConfig));
                    result = NativeMethods.PiOpenPrinter();
                }
                Check("PiOpenPrinter", result);
                ApiDirectory = apiDirectory;
                IsConnected = true;
            }
        }

        /// <summary>Call only while the carriage is stationary at the configured print high end.</summary>
        public void EstablishHomeAtHighEnd(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                EnsureConnected();
                cancellationToken.ThrowIfCancellationRequested();
                int result = RetryBusy("PiSetHome", delegate { return NativeMethods.PiSetHome(); }, cancellationToken);
                Check("PiSetHome", result);
                int stable = 0;
                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow <= deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    NativePccStatus status;
                    if (TryGetPccStatus(out status) && Math.Abs(ToSigned24(status.AbsXCount)) <= 8)
                    {
                        stable++;
                        if (stable >= 3) { _homeEstablished = true; return; }
                    }
                    else stable = 0;
                    cancellationToken.WaitHandle.WaitOne(50);
                }
                throw new TimeoutException("PiSetHome 后 PCC AbsX 未在 3 秒内稳定到零点。");
            }
        }

        public void SetHeadPower(bool enabled)
        {
            lock (_sync)
            {
                EnsureConnected();
                Check("PiSetHeadPower", RetryBusy("PiSetHeadPower", delegate { return NativeMethods.PiSetHeadPower(enabled ? 1u : 0u); }, CancellationToken.None));
            }
        }

        public void Spit()
        {
            lock (_sync)
            {
                EnsureConnected();
                int count = _profile.MeteorTelemetry.SpitCount <= 0 ? 200 : _profile.MeteorTelemetry.SpitCount;
                Check("PiSetSignal(SIG_SPIT)", NativeMethods.PiSetSignal(SignalSpit, (uint)count));
            }
        }

        public bool TryGetPccStatus(out int statusBits, out int absXCount, out int encoderCount)
        {
            lock (_sync)
            {
                NativePccStatus status;
                if (!IsConnected || !TryGetPccStatus(out status))
                {
                    statusBits = 0; absXCount = 0; encoderCount = 0; return false;
                }
                statusBits = status.StatusBits;
                absXCount = ToSigned24(status.AbsXCount);
                encoderCount = status.EncoderCount;
                return true;
            }
        }

        public void PrepareScan(string rasterPath, int widthPixels, int heightPixels, bool towardsHighEnd, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                EnsureConnected();
                if (!_homeEstablished) throw new InvalidOperationException("尚未在 X=" + _profile.InkCarPrintMaximumMm + " mm 停稳后建立 Meteor 光栅原点。");
                if (_scanPending) throw new InvalidOperationException("上一个 Meteor 扫描尚未完成。");
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(rasterPath)) throw new FileNotFoundException("未找到待喷印光栅。", rasterPath);

                if (!_jobActive)
                {
                    _expectedScans = new SinglePassScanPlan(_profile).ScanCount;
                    _preparedScans = 0; _completedScans = 0; _windowHighPixels = null;
                    uint[] startJob = { PcmdStartJob, 4, 100, JobTypeScan, ResolutionHigh, (uint)widthPixels };
                    Check("PCMD_STARTJOB", SendCommandRetry(startJob, cancellationToken, true));
                    _jobActive = true;
                    cancellationToken.WaitHandle.WaitOne(400);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (_preparedScans >= _expectedScans)
                    throw new InvalidOperationException("Meteor 扫描数超出当前层的固化模式。");
                NativePccStatus pcc;
                if (!TryGetPccStatus(out pcc)) throw new InvalidOperationException("无法读取 PCC 光栅坐标，禁止喷印。");
                int absX = ToSigned24(pcc.AbsXCount);
                int dpi = _profile.SliceDpi;
                int offsetPixels = (int)Math.Round(new SinglePassScanPlan(_profile).PrintOffsetMm * dpi / 25.4);
                if (_preparedScans == 0) _windowHighPixels = absX - offsetPixels;
                if (!_windowHighPixels.HasValue || _windowHighPixels.Value < widthPixels)
                    throw new InvalidOperationException("PCC 喷印窗口未标定或跨越坐标零点。");

                uint scanDirection = towardsHighEnd ? ScanReverse : ScanForward;
                int imageX = towardsHighEnd ? _windowHighPixels.Value : _windowHighPixels.Value - widthPixels;
                uint[] image = BuildImageCommand(rasterPath, widthPixels, heightPixels, imageX, cancellationToken);
                uint[] startScan = { PcmdStartScan, 1, scanDirection };
                Check("PCMD_STARTSCAN", SendCommandRetry(startScan, cancellationToken, false));
                Check("PCMD_IMAGE", SendCommandRetry(image, cancellationToken, false));
                Check("PCMD_ENDDOC", SendCommandRetry(new uint[] { PcmdEndDoc, 0 }, cancellationToken, false));
                _preparedScans++;
                _scanPending = true;
            }
        }

        public void CompleteScan(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                EnsureConnected();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_scanPending) throw new InvalidOperationException("没有待完成的 Meteor 扫描。");
                _scanPending = false;
                _completedScans++;
                if (_completedScans == _expectedScans)
                {
                    Check("PCMD_ENDJOB", SendCommandRetry(new uint[] { PcmdEndJob, 0 }, cancellationToken, true));
                    ResetJobState();
                }
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (IsConnected)
                {
                    try { NativeMethods.PiAbort(); } catch { }
                }
                ResetJobState();
            }
        }

        internal static uint[] BuildImageCommand(string path, int width, int height, int imageX, CancellationToken token)
        {
            using (var source = new Bitmap(path))
            {
                if (source.Width != width || source.Height != height)
                    throw new InvalidDataException("光栅尺寸与作业参数不一致。");
                int rowWords = (width + 31) / 32;
                uint[] command = new uint[6 + rowWords * height];
                command[0] = PcmdImage;
                command[1] = (uint)(command.Length - 2);
                command[2] = 1;
                command[3] = (uint)imageX;
                command[4] = 0;
                command[5] = (uint)width;

                using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(source, 0, 0);
                    BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        byte[] row = new byte[Math.Abs(data.Stride)];
                        for (int y = 0; y < height; y++)
                        {
                            token.ThrowIfCancellationRequested();
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                            int destination = 6 + y * rowWords;
                            for (int x = 0; x < width; x++)
                            {
                                int pixel = x * 4;
                                int grey = (row[pixel + 2] * 30 + row[pixel + 1] * 59 + row[pixel] * 11) / 100;
                                if (grey < 128) command[destination + (x >> 5)] |= 0x80000000u >> (x & 31);
                            }
                        }
                    }
                    finally { bitmap.UnlockBits(data); }
                }
                return command;
            }
        }

        private int SendCommandRetry(uint[] command, CancellationToken token, bool retryBusy)
        {
            IntPtr buffer = Marshal.AllocHGlobal(command.Length * 4);
            try
            {
                int busyAttempts = 0;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    for (int i = 0; i < command.Length; i++) Marshal.WriteInt32(buffer, i * 4, unchecked((int)command[i]));
                    int result = NativeMethods.PiSendCommand(buffer);
                    if (result == RvalFull) { token.WaitHandle.WaitOne(10); continue; }
                    if (retryBusy && result == RvalBusy && busyAttempts++ < 10) { token.WaitHandle.WaitOne(200); continue; }
                    return result;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private bool TryGetPccStatus(out NativePccStatus status)
        {
            status = new NativePccStatus();
            IntPtr pointer = NativeMethods.PiGetPccStatus((uint)Math.Max(1, _profile.MeteorTelemetry.PccNumber));
            if (pointer == IntPtr.Zero) return false;
            status = (NativePccStatus)Marshal.PtrToStructure(pointer, typeof(NativePccStatus));
            return true;
        }

        private static int RetryBusy(string operation, Func<int> call, CancellationToken token)
        {
            int result = call();
            for (int attempt = 0; result == RvalBusy && attempt < 5; attempt++)
            {
                token.ThrowIfCancellationRequested();
                token.WaitHandle.WaitOne(200);
                result = call();
            }
            return result;
        }

        private static int ToSigned24(int value)
        {
            int result = value & 0x00FFFFFF;
            return result > 0x007FFFFF ? result - 0x01000000 : result;
        }

        private static string FindApiDirectory(string configured)
        {
            var candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
            candidates.Add(AppDomain.CurrentDomain.BaseDirectory);
            candidates.Add(@"C:\Program Files\Meteor Inkjet\Meteor\Api\amd64");
            foreach (string path in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(path);
                    if (File.Exists(Path.Combine(full, "PrinterInterface.dll"))) return full;
                }
                catch { }
            }
            return null;
        }

        private static string ResolvePrintEngineConfig(MeteorTelemetryProfile profile, string apiDirectory)
        {
            if (!String.IsNullOrWhiteSpace(profile.PrintEngineConfigPath) && File.Exists(profile.PrintEngineConfigPath))
                return Path.GetFullPath(profile.PrintEngineConfigPath);
            string known = @"C:\Users\Public\Documents\Meteor\Config\PccE\DefaultStarfire_PccE.cfg";
            if (File.Exists(known)) return known;
            return null;
        }

        private static void Check(string operation, int result)
        {
            if (result != RvalOk) throw new InvalidOperationException(operation + " 失败，Meteor 返回码：" + result);
        }

        private void EnsureConnected()
        {
            if (!IsConnected) throw new InvalidOperationException("Meteor Printer Interface 未连接。");
        }

        private void ResetJobState()
        {
            _jobActive = false; _scanPending = false; _preparedScans = 0; _completedScans = 0; _expectedScans = 0; _windowHighPixels = null;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                Stop();
                if (IsConnected) { try { NativeMethods.PiClosePrinter(); } catch { } }
                IsConnected = false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePccStatus
        {
            public int StructVersion, IoSignals, StatusBits, JobStatus, FpgaVersion, FwVersion, PdCount, PrintCount, FaultRegister, AbsXCount, EncoderCount, StatusBits2;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern bool SetDllDirectory(string path);
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiOpenPrinter")]
            internal static extern int PiOpenPrinter();
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiClosePrinter")]
            internal static extern int PiClosePrinter();
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiStartPrintEngine", CharSet = CharSet.Ansi)]
            internal static extern int PiStartPrintEngine([MarshalAs(UnmanagedType.LPStr)] string configPath);
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSetHome")]
            internal static extern int PiSetHome();
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSetHeadPower")]
            internal static extern int PiSetHeadPower(uint state);
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSetSignal")]
            internal static extern int PiSetSignal(uint signalId, uint state);
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSendCommand")]
            internal static extern int PiSendCommand(IntPtr command);
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiAbort")]
            internal static extern int PiAbort();
            [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiGetPccStatus")]
            internal static extern IntPtr PiGetPccStatus(uint pccNumber);
        }
    }
}
