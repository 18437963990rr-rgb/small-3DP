using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace BinderJetting
{
    public static class MeteorPrintEngine
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private const int MeteorRuntimeNotReadyCode = -200100;
        private const int MeteorApiInvokeFailedCode = -200101;
        private const int RVAL_OK = 0;
        private const int RVAL_BUSY = 0x0E;
        private const int RVAL_NO_PRINTER = 0x12;
        private const int RVAL_FULL = 0x10; // PiSendCommand 队列满时返回，需重试

        // SampleScanPrint 使用??PCMD 命令（值与 PrinterInterface.h 一致，若不符请??SDK 头文件核对）
        private const uint PCMD_STARTJOB = 0x7A5535A1;
        private const uint PCMD_STARTSCAN = 0x7A5535A9;
        private const uint PCMD_IMAGE = 0x7A5535A4;
        private const uint PCMD_ENDDOC = 0x7A5535A6;
        private const uint PCMD_ENDJOB = 0x7A5535A7;
        private const uint JT_SCAN = 0x00000004;
        private const uint RES_HIGH = 1;
        private const uint SD_FWD = 0;
        private const uint SD_REV = 1;
        private const uint CCP_BIDI_XADJUST = 0x00000002;
        private const uint SIG_FORCEPD = 0x0000000E; // 强制 Product Detect，扫描打印时用于推进
        private const int BM_SCANNING = 0x00000200;

        private static readonly object SyncRoot = new object();
        private static bool _assemblyLoadAttempted;
        private static bool _runtimeReady;
        private static Type _printerInterfaceType;
        private static object _printerInterfaceInstance;
        private static bool _printerOpened;
        private static string _nativePrinterInterfaceDir;
        private static uint _pendingScanJobWidth = 1;
        private static bool _scanJobStarted;
        private static bool _homeCommandIssued;
        private static uint _scanJobStartXEncPosUm;
        /// <summary>最近一次成功下??PCMD_STARTJOB ??UTC 时间，用于首??PiSetHome 前补足与 STARTJOB 的间隔??/summary>
        private static DateTime? _startJobUtc;
        /// <summary>多 PASS 在扫程开始前 batch 下发 swath 时置 true，跳过 STARTSCAN 后等 X 增量（否则会阻塞至超时）。</summary>
        public static bool SuppressScanMotionGateForNextWrite;

        /// <summary>固高墨车 X 光栅读数（GetEncPos 轴1 脉冲 + 换算 mm），供与 PCC 光栅域对照。</summary>
        public struct InkCarGoogolRasterSample
        {
            public bool Valid;
            public double GoogolEncPulse;
            public double GoogolEncMm;
            public int LayerK;
        }

        private static Func<InkCarGoogolRasterSample> _inkCarGoogolRasterLiveReader;
        private static InkCarGoogolRasterSample _inkCarRasterPublishedAt750;

        /// <summary>
        /// 官方扫描队列模式：固定 Home 仅建立一次；每层连续提交全部 swath，ENDJOB 后再放行物理扫描。
        /// 默认开启；仅 METEOR_OFFICIAL_QUEUED_SCAN_MODE=0/false/off/no 可回退历史诊断路径。
        /// </summary>
        public static bool IsOfficialQueuedScanModeEnabled()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_OFFICIAL_QUEUED_SCAN_MODE");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return true;
        }

        public static bool IsAllFwdDiagnosticModeEnabled()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_ALL_FWD_DIAG");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    return string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        /// <summary>每层在可配置 Home 点停稳后 PiSetHome，再 STARTJOB 并连续预灌 3 swath。</summary>
        public static bool IsPerLayerHome800ModeEnabled()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PER_LAYER_HOME_MODE");
                if (string.IsNullOrWhiteSpace(env))
                    env = Environment.GetEnvironmentVariable("METEOR_PER_LAYER_HOME_800_MODE");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return true;
        }

        public static double GetPerLayerMeteorHomeMm()
        {
            const double defaultMm = 658.0;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PER_LAYER_HOME_MM");
                if (!string.IsNullOrWhiteSpace(env) && double.TryParse(env.Trim(), out double mm) && mm >= 0.0 && mm <= 1000.0)
                    return mm;
            }
            catch { }
            return defaultMm;
        }

        /// <summary>由打印线程在 750mm 等待位发布光栅快照，供数据线程 PiSetHome 时对照（滑架静止时与实时读数应一致）。</summary>
        public static void PublishInkCarRasterAt750(double googolEncPulse, double googolEncMm, int layerK)
        {
            _inkCarRasterPublishedAt750 = new InkCarGoogolRasterSample
            {
                Valid = true,
                GoogolEncPulse = googolEncPulse,
                GoogolEncMm = googolEncMm,
                LayerK = layerK
            };
        }

        /// <summary>注册固高光栅实时读取（数据线程 SendStartJob/PiSetHome 时调用）。</summary>
        public static void RegisterInkCarGoogolRasterLiveReader(Func<InkCarGoogolRasterSample> reader)
        {
            _inkCarGoogolRasterLiveReader = reader;
        }

        private static bool TryReadInkCarGoogolRaster(out InkCarGoogolRasterSample sample)
        {
            sample = default;
            if (_inkCarGoogolRasterLiveReader != null)
            {
                try
                {
                    sample = _inkCarGoogolRasterLiveReader();
                    if (sample.Valid)
                        return true;
                }
                catch { }
            }
            if (_inkCarRasterPublishedAt750.Valid)
            {
                sample = _inkCarRasterPublishedAt750;
                return true;
            }
            return false;
        }

        private static void LogRasterEncoderSnapshot(string stage, string action, int layerK = -1,
            int? beforePccEncoder = null, int? beforeAbsX24 = null, double? beforeGoogolPulse = null)
        {
            bool valid = TryGetPccMotionSnapshot(1, out PccMotionSnapshot snap) && snap.Valid;
            int absX24 = valid ? GetAbsXCount24Signed(snap.AbsXCount) : 0;
            int pccEncoder = valid ? snap.EncoderCount : 0;

            double googolPulse = double.NaN;
            double googolMm = double.NaN;
            if (TryReadInkCarGoogolRaster(out InkCarGoogolRasterSample gSample))
            {
                googolPulse = gSample.GoogolEncPulse;
                googolMm = gSample.GoogolEncMm;
                if (layerK < 0 && gSample.LayerK > 0)
                    layerK = gSample.LayerK;
            }

            string layerPart = layerK > 0 ? $"layerK={layerK}" : "layerK=n/a";
            string gPulseText = double.IsNaN(googolPulse) ? "n/a" : googolPulse.ToString("F0");
            string gMmText = double.IsNaN(googolMm) ? "n/a" : googolMm.ToString("F3");
            string pccEncText = valid ? pccEncoder.ToString() : "n/a";
            string absText = valid ? absX24.ToString() : "n/a";

            string deltaPart = string.Empty;
            if (beforePccEncoder.HasValue && valid)
                deltaPart += $" deltaPccEncoder={pccEncoder - beforePccEncoder.Value}";
            if (beforeAbsX24.HasValue && valid)
                deltaPart += $" deltaAbsX24={absX24 - beforeAbsX24.Value}";
            if (beforeGoogolPulse.HasValue && !double.IsNaN(googolPulse))
                deltaPart += $" deltaGoogolEncPulse={(googolPulse - beforeGoogolPulse.Value):F0}";

            Log4Net.Info($"[MeteorRasterEncoder] stage={stage} action={action} {layerPart} googolEncPulse={gPulseText} googolEncMm={gMmText} pccEncoder={pccEncText} absX24={absText}{deltaPart} note=googolEnc=固高GetEncPos(1);pcc=Meteor光栅域 utc={DateTime.UtcNow:O}");
        }

        /// <summary>
        /// 一次 STARTJOB 内 Pass0 门控后连续发送全部 strip（历史 batch 模式）。
        /// 默认 true（当前调试）；METEOR_BATCH_SWATH_MODE=0/false 恢复逐 pass 门控。
        /// </summary>
        public static bool IsBatchSwathModeEnabled()
        {
            if (IsOfficialQueuedScanModeEnabled())
                return true;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_BATCH_SWATH_MODE");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return true;
        }

        /// <summary>诊断用：返回 METEOR_BATCH_SWATH_MODE 原始 env 与解析结果。</summary>
        public static string DescribeBatchSwathModeConfig()
        {
            string env = null;
            try { env = Environment.GetEnvironmentVariable("METEOR_BATCH_SWATH_MODE"); } catch { }
            string raw = string.IsNullOrWhiteSpace(env) ? "unset" : env.Trim();
            return $"METEOR_BATCH_SWATH_MODE={raw} => officialQueuedScanMode={IsOfficialQueuedScanModeEnabled()}, perLayerHomeMode={IsPerLayerHome800ModeEnabled()}, meteorHomeMm={GetPerLayerMeteorHomeMm():F3}, batchSwathMode={IsBatchSwathModeEnabled()}, batchStartScanGateSplit={IsBatchStartScanGateSplitEnabled()}, passLiveAbsXComp={IsPassIntraLayerLiveAbsXCompEnabled()}, passGateAnchorXStart={IsPassGateAnchorXStartEnabled()}, batchEndJobImmediate={IsBatchEndJobImmediateEnabled()}, splitJobPerPass={IsSplitJobPerPassEnabled()}";
        }

        /// <summary>
        /// Batch 模式下是否只预切/缓存 swath，而 STARTSCAN+IMAGE+ENDDOC 仍绑定各 pass gate。
        /// 默认 false，回到早期一次 STARTJOB 内连续发送所有 strip 的路径；设为 1 可恢复 gate-split 诊断模式。
        /// METEOR_BATCH_STARTSCAN_GATE_SPLIT=1/true/on/yes 启用按 pass gate 发送 STARTSCAN 的诊断模式。
        /// </summary>
        public static bool IsBatchStartScanGateSplitEnabled()
        {
            if (IsOfficialQueuedScanModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_BATCH_STARTSCAN_GATE_SPLIT");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 本层 swath 数据（ENDDOC）全部提交后是否立即 PCMD_ENDJOB。
        /// 默认 false：延后至 Pass2 物理扫程结束；METEOR_BATCH_ENDJOB_IMMEDIATE=1/true 才启用立即 EndJob 诊断。
        /// </summary>
        public static bool IsBatchEndJobImmediateEnabled()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_BATCH_ENDJOB_IMMEDIATE");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 诊断模式：每个 3PASS swath 使用独立 STARTJOB，并在该 pass 物理扫程结束后 ENDJOB。
        /// 默认关闭；METEOR_SPLIT_JOB_PER_PASS=1/true/on/yes 开启。
        /// </summary>
        public static bool IsSplitJobPerPassEnabled()
        {
            if (IsOfficialQueuedScanModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_SPLIT_JOB_PER_PASS");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>在每次下??PCMD_STARTJOB 之前的调用侧等待：与打印线程层首 PASS0（见 <see cref="SignalPrintThreadLayerPass0ReadyForMeteorSubmit"/>）对齐；不得??Raster/传输线程早于 <c>g_PrintSchedule</c> 推进的路径上 Wait??br/>
        /// <c>METEOR_GATE_SENDSTARTJOB_ON_PASS0</c>=1/true/on/yes<br/>
        /// <c>METEOR_PASS0_GATE_WAIT_MS</c>：正??毫秒上限（最??3600000），-1=无限等待?? 或未设置=默认 600000??br/>
        /// <c>METEOR_PASS0_GATE_SKIP_FIRST_LAYER_WAIT</c>=0/false/off：首帧也 Wait（慎用，易与首轮 schedule 交互死锁）；默认 1/true=??raster 层不??PASS0 Gate 阻塞??/summary>
        private static readonly Lazy<(bool Enabled, int WaitMs, bool SkipFirstLayerWait)> _pass0GateConfig = new Lazy<(bool, int, bool)>(LoadPass0GateConfig, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly ManualResetEventSlim _pass0GateSendStartJob = new ManualResetEventSlim(false);
        private static readonly ManualResetEventSlim _pass0GatePreheatStartJob = new ManualResetEventSlim(false);
        private static readonly ManualResetEventSlim _pass0PreheatStartJobDone = new ManualResetEventSlim(false);
        private static bool _pass0PreheatStartJobOk;
        private static readonly ManualResetEventSlim[] _passScanGateEvents = new ManualResetEventSlim[]
        {
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false)
        };
        private static bool? _pass0GateEnabledOverride;
        private static bool? _pass0GateSkipFirstLayerWaitOverride;
        /// <summary>本 JOB 内是否已在 SendStartJob 之前完成过 PASS0 门控 Wait（避免 RenderToWic 与 WriteImageLayer:FirstStartScan 重复 Wait 死锁）??/summary>
        private static bool _pass0SubmitGateConsumedForCurrentJob;
        /// <summary>PASS0 首条 swath 的 STARTSCAN+IMAGE+ENDDOC 已下发完成，供打印线程在 425→25 扫程前等待??/summary>
        private static readonly ManualResetEventSlim _pass0FirstSwathMeteorReady = new ManualResetEventSlim(false);
        private static bool _pass0FirstSwathMeteorSignaled;
        /// <summary>PASS1/2：数据线程 swath 下发完成后再启动扫程（与 PASS0 WaitPass0FirstSwathMeteorReady 对称）。</summary>
        private static readonly ManualResetEventSlim[] _passSwathMeteorReady = new ManualResetEventSlim[]
        {
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false)
        };
        private static readonly ManualResetEventSlim[] _passSwathPrepackedReady = new ManualResetEventSlim[]
        {
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false)
        };
        private static readonly ManualResetEventSlim[] _passMotionStartedForMeteorSend = new ManualResetEventSlim[]
        {
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false),
            new ManualResetEventSlim(false)
        };
        private static readonly bool[] _passSwathMeteorSignaled = new bool[3];
        /// <summary>打印线程在扫程起点 Signal 时锁定的 AbsX，供 PASS1/2 的 PCMD_IMAGE xStart（避免扫程中 live 采样错位）。</summary>
        private static readonly int[] _passImageXStartAbsXAnchor = new int[3];
        private static readonly bool[] _passImageXStartAbsXAnchorValid = new bool[3];
        private static int _pendingSwathPassIndex;
        /// <summary>供应商对齐模式：PASS0 首条 swath 锁定的 Xleft，PASS1/2 复用同一值（≈5115@425mm 侧）。</summary>
        private static int _jobUnifiedImageXStartAbsX;
        private static bool _jobUnifiedImageXStartValid;
        /// <summary>各 pass 扫程结束时的 PCC AbsX（由 <see cref="LogScanPassMotionCheck"/> 写入），供 Pass2 FWD live 对齐。</summary>
        private static readonly int[] _passScanEndAbsX = new int[3];
        private static readonly bool[] _passScanEndAbsXValid = new bool[3];
        /// <summary>默认延后 EndJob 时置 true，由打印线程 Pass2 扫程结束后 TryCompleteDeferredEndJob。</summary>
        private static bool _deferEndJobPending;

        private sealed class DeferredLegacyBatchSwath
        {
            public uint[] StartScanCmd;
            public uint[] ImageCmd;
            public uint[] EndDocCmd;
            public int PassIndex;
            public int LayerIndex;
            public bool MarkFirstHomeAfterStartScan;
            public bool IsBlankPrime;
        }

        private static readonly List<DeferredLegacyBatchSwath> _deferredLegacyBatchSwaths = new List<DeferredLegacyBatchSwath>();
        private static string _swathPayloadDiagDirectory;
        private static readonly int[] _swathPayloadDiagCenterX = { -1, -1, -1 };
        private static readonly string[] _swathPayloadDiagCsvRows = new string[3];

        /// <summary>仅当 <see cref="IsBatchEndJobImmediateEnabled"/> 为 false 时使用（量产默认路径）。</summary>
        public static void MarkDeferredEndJobAfterPassSwaths(string stage)
        {
            _deferEndJobPending = true;
            Log4Net.Info($"[MeteorJob] DeferredEndJobPending stage={stage} note=EndJob deferred until Pass2 physical scan (default; set METEOR_BATCH_ENDJOB_IMMEDIATE=1 to send immediately) utc={DateTime.UtcNow:O}");
        }

        /// <summary>延后 EndJob 时由打印线程在 Pass0/1/2 扫程节点调用；立即 EndJob 模式下通常为 no-op。</summary>
        public static bool TryCompleteDeferredEndJob(string stage)
        {
            if (!_deferEndJobPending)
                return false;
            _deferEndJobPending = false;
            bool preserveLayerGates = IsSplitJobPerPassEnabled()
                && !string.IsNullOrEmpty(stage)
                && (stage.IndexOf("Pass0", StringComparison.OrdinalIgnoreCase) >= 0
                    || stage.IndexOf("Pass1", StringComparison.OrdinalIgnoreCase) >= 0);
            bool endJobRet = preserveLayerGates ? SendEndJobPreserveLayerGates(stage) : SendEndJob();
            Log4Net.Info($"[MeteorJob] DeferredEndJobCompleted stage={stage} endJobRet={endJobRet} utc={DateTime.UtcNow:O}");
            if (endJobRet)
            {
                if (preserveLayerGates)
                {
                    LogPccMotionSnapshot(stage + "AfterDeferredEndJob");
                    return endJobRet;
                }
                int idleTimeoutMs = GetPccIdleWaitTimeoutMs();
                // Pass2 EndJob 后 PrintEngine 常需 6s 左右才从 CA00 释放到 C800。
                // 这里已经成功发过 EndJob，后续只自然轮询；重复 EndJob 会被板卡报
                // EndJob ignored [State=1]，且 17:15/17:16 日志显示并不能加速释放。
                int firstIdleWaitMs = Math.Min(idleTimeoutMs, 10000);
                bool idleReady = WaitForPccReadyForNewStartJob(firstIdleWaitMs, stage + ":AfterEndJob");
                if (!idleReady && idleTimeoutMs > firstIdleWaitMs)
                {
                    Log4Net.Info($"[MeteorJob] DeferredEndJobNaturalWaitContinue stage={stage} firstWaitMs={firstIdleWaitMs} totalTimeoutMs={idleTimeoutMs} note=skip duplicate EndJob after successful deferred EndJob");
                    idleReady = WaitForPccReadyForNewStartJob(idleTimeoutMs - firstIdleWaitMs, stage + ":AfterEndJobNaturalPoll");
                }
                LogPccMotionSnapshot(stage + "AfterDeferredEndJob");
                if (!idleReady)
                    Log4Net.Info($"[MeteorJob] DeferredEndJobCompleted warning: PCC not confirmed idle for next STARTJOB stage={stage}");
            }
            return endJobRet;
        }

        private static void ResetDeferredEndJobState()
        {
            _deferEndJobPending = false;
        }

        private static (bool Enabled, int WaitMs, bool SkipFirstLayerWait) LoadPass0GateConfig()
        {
            try
            {
                string en = Environment.GetEnvironmentVariable("METEOR_GATE_SENDSTARTJOB_ON_PASS0");
                bool enabled = string.Equals(en?.Trim(), "1", StringComparison.Ordinal)
                               || string.Equals(en?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(en?.Trim(), "yes", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(en?.Trim(), "on", StringComparison.OrdinalIgnoreCase);

                string w = Environment.GetEnvironmentVariable("METEOR_PASS0_GATE_WAIT_MS");
                int waitMs = 600000;
                if (!string.IsNullOrWhiteSpace(w) && int.TryParse(w.Trim(), out int parsed))
                {
                    if (parsed == -1)
                        waitMs = Timeout.Infinite;
                    else if (parsed > 0)
                        waitMs = Math.Min(parsed, 3600000);
                }

                bool skipFirst = true;
                string sk = Environment.GetEnvironmentVariable("METEOR_PASS0_GATE_SKIP_FIRST_LAYER_WAIT");
                if (!string.IsNullOrWhiteSpace(sk))
                {
                    string t = sk.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        skipFirst = false;
                    else if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        skipFirst = true;
                }

                return (enabled, waitMs, skipFirst);
            }
            catch { return (false, 600000, true); }
        }

        private static bool IsPass0GateEnabled() => _pass0GateEnabledOverride ?? _pass0GateConfig.Value.Enabled;

        private static bool ShouldSkipFirstLayerWait() => _pass0GateSkipFirstLayerWaitOverride ?? _pass0GateConfig.Value.SkipFirstLayerWait;

        /// <summary>
        /// 自动打印主流程显式配??PASS0 门控，避免依赖进程环境变量??
        /// enabled=true ??skipFirstLayerWait=false 时，首层 STARTJOB 必须等到打印线程层首 PASS0 机械入口信号??
        /// </summary>
        public static void ConfigurePass0GateForAutoPrint(bool enabled, bool skipFirstLayerWait = false)
        {
            _pass0GateEnabledOverride = enabled;
            _pass0GateSkipFirstLayerWaitOverride = enabled ? (bool?)skipFirstLayerWait : null;
            if (!enabled)
            {
                try { _pass0GateSendStartJob.Reset(); } catch { }
                try { _pass0GatePreheatStartJob.Reset(); } catch { }
                foreach (var gate in _passScanGateEvents)
                {
                    try { gate.Reset(); } catch { }
                }
            }
            Log4Net.Info($"[MeteorScanGate] RuntimeConfig enabled={enabled} skipFirstLayerWait={skipFirstLayerWait} source=AutoPrintFlow utc={DateTime.UtcNow:O}");
        }

        /// <summary>
        /// 当首??STARTJOB 绑定??PASS0 机械入口时，打印线程不能再等待旧??g_PrintSchedule 预热，否则首层会互等死锁??
        /// </summary>
        public static bool ShouldBypassInitialPrintScheduleWaitForPass0Gate()
        {
            return IsPass0GateEnabled() && !ShouldSkipFirstLayerWait();
        }

        /// <summary>
        /// HiPrint 对齐模式：
        /// 1. 跳过本软件额外的 PASS0 提交门控等待；
        /// 2. IMAGE 的 XStart 采用更接近 HiPrint 的固定正反向规则。
        /// 默认开启，便于先把链路退回到更直接的基线。
        /// </summary>
        private static bool IsHiPrintChainCompatModeEnabled()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_HIPRINT_CHAIN_COMPAT");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return true;
        }

        /// <summary>
        /// pass0 是否翻转 STARTSCAN 方向。默认 false（AbsX 累加时与固高 485→15 反向，不应强行 REV）；
        /// METEOR_PASS0_FLIP_STARTSCAN=1/true 恢复旧验证行为。
        /// </summary>
        private static bool ShouldFlipPass0StartScanDir()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS0_FLIP_STARTSCAN");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>pass0 扫程端实测 AbsX24（由 <see cref="LogDualCoordSnapshot"/> 写入，非固高 mm 换算）。</summary>
        private static int _pass0ScanAbsXAtApproach;
        private static int _pass0ScanAbsXAtLowEnd;
        private static int _pass0EncoderAtApproach;
        private static int _pass0EncoderAtLowEnd;
        private static bool _pass0ScanAbsXApproachValid;
        private static bool _pass0ScanAbsXLowEndValid;
        /// <summary>首层 Pass0 Flush（WriteImageLayer 首条 swath 前 liveAbsX）作层间差补参考（StopJob 前跨层保持）。</summary>
        private static int _layerAbsXDeltaCompRefAtFlush;
        private static bool _layerAbsXDeltaCompRefValid;
        /// <summary>本层 Flush 时实测 absX24；与 4110 标定同一时刻域，不被 StartJob 清锚点。</summary>
        private static int _layerAbsXDeltaCompMeasAtFlush;
        private static bool _layerAbsXDeltaCompMeasValid;

        private static void ResetLayerAbsXDeltaCompRef()
        {
            _layerAbsXDeltaCompRefValid = false;
            _layerAbsXDeltaCompRefAtFlush = 0;
            _layerAbsXDeltaCompMeasValid = false;
            _layerAbsXDeltaCompMeasAtFlush = 0;
        }

        /// <summary>层间 Flush liveAbsX 差补：fwdBase = 4110 + (首层 flush absX - 本层 flush absX)。默认关；METEOR_LAYER_ABSX_DELTA_COMP=1 开启。</summary>
        private static bool IsLayerAbsXDeltaCompEnabled()
        {
            if (IsOfficialQueuedScanModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_LAYER_ABSX_DELTA_COMP");
                if (string.IsNullOrWhiteSpace(env))
                    return false;
                string t = env.Trim();
                if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                    return false;
                return string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static int GetLayerAbsXDeltaCompDeadzonePx()
        {
            const int defaultPx = 30;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_LAYER_ABSX_DELTA_DEADZONE_PX");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int px) && px >= 0 && px <= 500)
                    return px;
            }
            catch { }
            return defaultPx;
        }

        private static void CaptureLayerAbsXDeltaCompAtFlush(int absXAtFlush, int layerK)
        {
            if (!IsLayerAbsXDeltaCompEnabled())
                return;
            _layerAbsXDeltaCompMeasAtFlush = absXAtFlush;
            _layerAbsXDeltaCompMeasValid = true;
            if (!_layerAbsXDeltaCompRefValid)
            {
                _layerAbsXDeltaCompRefAtFlush = absXAtFlush;
                _layerAbsXDeltaCompRefValid = true;
                string layerPart = layerK >= 0 ? $"layerK={layerK}" : "layerK=n/a";
                Log4Net.Info($"[MeteorLayerAbsXDeltaComp] FirstLayerRefCaptured refAbsX24AtFlush={absXAtFlush} {layerPart} utc={DateTime.UtcNow:O}");
            }
        }

        private static bool TryGetLayerCompensatedFwdBasePx(int nominalFwdBasePx, out int compensatedFwdBasePx, out int deltaCompPx)
        {
            compensatedFwdBasePx = nominalFwdBasePx;
            deltaCompPx = 0;
            if (!IsLayerAbsXDeltaCompEnabled() || nominalFwdBasePx <= 0)
                return false;
            if (!_layerAbsXDeltaCompRefValid || !_layerAbsXDeltaCompMeasValid)
                return false;
            deltaCompPx = _layerAbsXDeltaCompRefAtFlush - _layerAbsXDeltaCompMeasAtFlush;
            int deadzonePx = GetLayerAbsXDeltaCompDeadzonePx();
            if (Math.Abs(deltaCompPx) < deadzonePx)
                deltaCompPx = 0;
            compensatedFwdBasePx = Math.Max(GetSafeImageXStartMin(), nominalFwdBasePx + deltaCompPx);
            return deltaCompPx != 0;
        }

        public static void ResetPass0DualCoordScanAnchors()
        {
            _pass0ScanAbsXApproachValid = false;
            _pass0ScanAbsXLowEndValid = false;
            _pass0ScanAbsXAtApproach = 0;
            _pass0ScanAbsXAtLowEnd = 0;
            _pass0EncoderAtApproach = 0;
            _pass0EncoderAtLowEnd = 0;
        }

        private static bool TryGetPass0MeasuredAbsXScanRange(out int lowPx, out int highPx)
        {
            lowPx = 0;
            highPx = 0;
            if (!_pass0ScanAbsXApproachValid && !_pass0ScanAbsXLowEndValid)
                return false;
            if (_pass0ScanAbsXApproachValid && _pass0ScanAbsXLowEndValid)
            {
                lowPx = Math.Min(_pass0ScanAbsXAtApproach, _pass0ScanAbsXAtLowEnd);
                highPx = Math.Max(_pass0ScanAbsXAtApproach, _pass0ScanAbsXAtLowEnd);
                return true;
            }
            int only = _pass0ScanAbsXApproachValid ? _pass0ScanAbsXAtApproach : _pass0ScanAbsXAtLowEnd;
            lowPx = only;
            highPx = only;
            return true;
        }

        /// <summary>打印线程：记录固高光栅与 PCC AbsX/Encoder 对照（两套坐标系无硬同步，仅诊断/标定）。</summary>
        public static void LogDualCoordSnapshot(string stage, double googolXmm, int layerK = -1)
        {
            bool valid = TryGetPccMotionSnapshot(1, out PccMotionSnapshot snap) && snap.Valid;
            int absX24 = valid ? GetAbsXCount24Signed(snap.AbsXCount) : 0;
            int encoder = valid ? snap.EncoderCount : 0;

            double googolPulse = double.NaN;
            if (TryReadInkCarGoogolRaster(out InkCarGoogolRasterSample gSample))
            {
                googolPulse = gSample.GoogolEncPulse;
                if (layerK < 0 && gSample.LayerK > 0)
                    layerK = gSample.LayerK;
            }
            string gPulseText = double.IsNaN(googolPulse) ? "n/a" : googolPulse.ToString("F0");
            string layerPart = layerK > 0 ? $"layerK={layerK}" : "layerK=n/a";

            Log4Net.Info($"[MeteorDualCoord] stage={stage} {layerPart} googolXmm={googolXmm:F3} googolEncPulse={gPulseText} absX24={absX24} pccAbsX={(valid ? snap.AbsXCount.ToString() : "n/a")} encoder={(valid ? encoder.ToString() : "n/a")} note=googolEnc=固高GetEncPos(1);pcc=Meteor光栅域 utc={DateTime.UtcNow:O}");
            LogRasterEncoderSnapshot(stage, "snapshot", layerK);

            if (string.Equals(stage, "Pass0AtApproachHold", StringComparison.OrdinalIgnoreCase)
                || stage.IndexOf("ApproachHold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (valid)
                {
                    _pass0ScanAbsXAtApproach = absX24;
                    _pass0EncoderAtApproach = encoder;
                    _pass0ScanAbsXApproachValid = true;
                }
            }
            else if (string.Equals(stage, "Pass0AtScanLowEnd", StringComparison.OrdinalIgnoreCase))
            {
                if (valid)
                {
                    _pass0ScanAbsXAtLowEnd = absX24;
                    _pass0EncoderAtLowEnd = encoder;
                    _pass0ScanAbsXLowEndValid = true;
                }
            }
            else if (string.Equals(stage, "Pass2AtScanLowEnd", StringComparison.OrdinalIgnoreCase))
            {
                if (valid && _pass0ScanAbsXLowEndValid)
                {
                    int lowDeltaPx = absX24 - _pass0ScanAbsXAtLowEnd;
                    Log4Net.Info($"[MeteorPassLowAlign] stage=Pass2AtScanLowEnd pass0Low={_pass0ScanAbsXAtLowEnd} pass2Low={absX24} deltaPx={lowDeltaPx} deltaMm≈{AbsXCountToMm(Math.Abs(lowDeltaPx), 400):F2} utc={DateTime.UtcNow:O}");
                }
            }
        }

        public static void ResetPass0FirstSwathMeteorReady()
        {
            try { _pass0FirstSwathMeteorReady.Reset(); } catch { }
            _pass0FirstSwathMeteorSignaled = false;
        }

        private static void SignalPass0FirstSwathMeteorReadyIfNeeded(string stage)
        {
            if (_pass0FirstSwathMeteorSignaled)
                return;
            WaitForDocsQueuedLane1Ready(stage);
            _pass0FirstSwathMeteorSignaled = true;
            try { _pass0FirstSwathMeteorReady.Set(); } catch { }
            Log4Net.Info($"[MeteorScanGate] Pass0FirstSwathMeteorReady stage={stage} utc={DateTime.UtcNow:O}");
        }

        private static bool ShouldDeferLegacyBatchSwathSend(int passIndex)
        {
            if (IsAllFwdDiagnosticModeEnabled())
                return false;
            if (ShouldUsePassGateAnchorXStartBatchGateSplit())
                return true;
            if (!IsBatchSwathModeEnabled() || IsBatchStartScanGateSplitEnabled() || IsSplitJobPerPassEnabled())
                return false;
            if (IsPassIntraLayerLiveAbsXCompEnabled() && passIndex > 0)
                return false;
            if (IsPassGateAnchorXStartEnabled() && passIndex > 0)
                return false;
            return true;
        }

        /// <summary>方案 B：Pass1/2 在各自 gate 发 swath（非 Flush 预灌）并做 pass 内 live AbsX 差补。默认关；METEOR_PASS_LIVE_ABSX_COMP=1 开启诊断。</summary>
        public static bool IsPassIntraLayerLiveAbsXCompEnabled()
        {
            if (IsOfficialQueuedScanModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS_LIVE_ABSX_COMP");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        public static bool ShouldUsePassLiveAbsXCompBatchGateSplit()
        {
            return IsPassIntraLayerLiveAbsXCompEnabled() && IsBatchSwathModeEnabled()
                && !IsBatchStartScanGateSplitEnabled() && !IsSplitJobPerPassEnabled()
                && !IsPassGateAnchorXStartEnabled();
        }

        /// <summary>诊断：Pass1/2 在各自 gate 直接用 pass 锚点定 Xleft，不再叠加 unified 4110/8462 或 live comp。</summary>
        public static bool IsPassGateAnchorXStartEnabled()
        {
            if (IsOfficialQueuedScanModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS_GATE_ANCHOR_XSTART");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        public static bool ShouldUsePassGateAnchorXStartBatchGateSplit()
        {
            return IsPassGateAnchorXStartEnabled() && IsBatchSwathModeEnabled() && !IsSplitJobPerPassEnabled();
        }

        private static void ResetDeferredLegacyBatchSwaths()
        {
            _deferredLegacyBatchSwaths.Clear();
        }

        private static void ResetSwathPayloadDiagnostics()
        {
            _swathPayloadDiagDirectory = null;
            for (int i = 0; i < _swathPayloadDiagCenterX.Length; i++)
            {
                _swathPayloadDiagCenterX[i] = -1;
                _swathPayloadDiagCsvRows[i] = null;
            }
        }

        private static bool IsSwathPayloadDiagnosticsEnabled()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_SWATH_PAYLOAD_DIAG");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static uint ComputePayloadCrc32(uint[] cmd, int payloadOffset)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = payloadOffset; i < cmd.Length; i++)
            {
                uint word = cmd[i];
                for (int b = 0; b < 4; b++)
                {
                    crc ^= (byte)(word >> (8 * b));
                    for (int bit = 0; bit < 8; bit++)
                        crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }
            }
            return ~crc;
        }

        private static void WriteSwathPayloadDiagnosticBitmap(string path, uint[] cmd, int payloadOffset, int width, int height, int wordsPerRow)
        {
            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format1bppIndexed))
            {
                System.Drawing.Imaging.ColorPalette palette = bmp.Palette;
                palette.Entries[0] = System.Drawing.Color.White;
                palette.Entries[1] = System.Drawing.Color.Black;
                bmp.Palette = palette;
                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, width, height);
                System.Drawing.Imaging.BitmapData data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
                try
                {
                    int stride = Math.Abs(data.Stride);
                    byte[] row = new byte[stride];
                    for (int y = 0; y < height; y++)
                    {
                        Array.Clear(row, 0, row.Length);
                        int rowBase = payloadOffset + y * wordsPerRow;
                        for (int w = 0; w < wordsPerRow; w++)
                        {
                            uint word = cmd[rowBase + w];
                            int byteBase = w * 4;
                            if (byteBase < stride) row[byteBase] = (byte)(word >> 24);
                            if (byteBase + 1 < stride) row[byteBase + 1] = (byte)(word >> 16);
                            if (byteBase + 2 < stride) row[byteBase + 2] = (byte)(word >> 8);
                            if (byteBase + 3 < stride) row[byteBase + 3] = (byte)word;
                        }
                        IntPtr dst = IntPtr.Add(data.Scan0, y * data.Stride);
                        Marshal.Copy(row, 0, dst, stride);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
            }
        }

        private static void DiagnoseAndExportFinalSwathPayload(uint[] cmd, int payloadOffset, int width, int height, int wordsPerRow, int passIndex, int layerIndex)
        {
            if (!IsSwathPayloadDiagnosticsEnabled() || passIndex < 0 || passIndex > 2 || width <= 0 || height <= 0)
                return;
            try
            {
                int minX = width;
                int maxX = -1;
                int minY = height;
        int maxY = -1;
        long pixelCount = 0;
        long sumX = 0;
        long logicalLeftHalfPixels = 0;
        long logicalRightHalfPixels = 0;
        long[] logicalQuarterPixels = new long[4];
        int halfWidth = width / 2;
        for (int y = 0; y < height; y++)
                {
                    int rowBase = payloadOffset + y * wordsPerRow;
                    for (int x = 0; x < width; x++)
                    {
                        uint word = cmd[rowBase + (x >> 5)];
                        if ((word & (0x80000000u >> (x & 31))) == 0)
                            continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            pixelCount++;
            sumX += x;
            if (x < halfWidth)
                logicalLeftHalfPixels++;
            else
                logicalRightHalfPixels++;
            int quarterIndex = Math.Min(3, (x * 4) / width);
            logicalQuarterPixels[quarterIndex]++;
                    }
                }
                int centerX = pixelCount > 0 ? (int)Math.Round((double)sumX / pixelCount, MidpointRounding.AwayFromZero) : -1;
                if (maxX < 0)
                {
                    minX = -1;
                    minY = -1;
                    maxY = -1;
                }
                uint crc32 = ComputePayloadCrc32(cmd, payloadOffset);
                _swathPayloadDiagCenterX[passIndex] = centerX;
                int deltaFromPass0 = passIndex > 0 && _swathPayloadDiagCenterX[0] >= 0 && centerX >= 0
                    ? centerX - _swathPayloadDiagCenterX[0]
        : 0;
        Log4Net.Info($"[MeteorSwathBoundary] layer={layerIndex} pass={passIndex} width={width} height={height} pixels={pixelCount} xMin={minX} xMax={maxX} centerX={centerX} yMin={minY} yMax={maxY} deltaFromPass0Px={deltaFromPass0} deltaFromPass0Mm≈{deltaFromPass0 * 25.4 / 400.0:F3} crc32=0x{crc32:X8}");
        Log4Net.Info($"[MeteorSwathHdcDiag] layer={layerIndex} pass={passIndex} packedPayloadX=left-to-right logicalLeftHalfPixels={logicalLeftHalfPixels} logicalRightHalfPixels={logicalRightHalfPixels} q0={logicalQuarterPixels[0]} q1={logicalQuarterPixels[1]} q2={logicalQuarterPixels[2]} q3={logicalQuarterPixels[3]} note=counts are after all image-orientation transforms; compare Pass1 distribution with board HDC1/HDC2 translation");

                if (string.IsNullOrEmpty(_swathPayloadDiagDirectory))
                {
                    string root = Path.Combine(System.Windows.Forms.Application.StartupPath, "TEMP_METEOR_SWATH_DIAG");
                    _swathPayloadDiagDirectory = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
                    Directory.CreateDirectory(_swathPayloadDiagDirectory);
                }
                string prefix = $"layer{layerIndex}_pass{passIndex}_{width}x{height}";
                string binPath = Path.Combine(_swathPayloadDiagDirectory, prefix + "_payload.bin");
                using (BinaryWriter writer = new BinaryWriter(File.Create(binPath)))
                {
                    for (int i = payloadOffset; i < cmd.Length; i++)
                        writer.Write(cmd[i]);
                }
                WriteSwathPayloadDiagnosticBitmap(
                    Path.Combine(_swathPayloadDiagDirectory, prefix + "_payload.bmp"),
                    cmd, payloadOffset, width, height, wordsPerRow);
                _swathPayloadDiagCsvRows[passIndex] =
                    $"{layerIndex},{passIndex},{width},{height},{pixelCount},{minX},{maxX},{centerX},{minY},{maxY},{deltaFromPass0},{deltaFromPass0 * 25.4 / 400.0:F3},0x{crc32:X8}";
                string csvPath = Path.Combine(_swathPayloadDiagDirectory, "summary.csv");
                using (StreamWriter csv = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8))
                {
                    csv.WriteLine("Layer,Pass,Width,Height,SetPixels,XMin,XMax,CenterX,YMin,YMax,DeltaFromPass0Px,DeltaFromPass0Mm,CRC32");
                    for (int i = 0; i < _swathPayloadDiagCsvRows.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(_swathPayloadDiagCsvRows[i]))
                            csv.WriteLine(_swathPayloadDiagCsvRows[i]);
                    }
                }
                Log4Net.Info($"[MeteorSwathBoundary] exported pass={passIndex} directory={_swathPayloadDiagDirectory}");
            }
            catch (Exception ex)
            {
                Log4Net.Info($"[MeteorSwathBoundary] export failed layer={layerIndex} pass={passIndex}: {ex.Message}");
            }
        }

        public static bool IsBlankPrimeSwathEnabled()
        {
            if (IsAllFwdDiagnosticModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_BLANK_PRIME_SWATH");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return IsOfficialQueuedScanModeEnabled();
        }

        private static void PrependBlankPrimeSwathIfEnabled(string stage)
        {
            if (!IsOfficialQueuedScanModeEnabled() || !IsBlankPrimeSwathEnabled() || _deferredLegacyBatchSwaths.Count == 0)
                return;
            DeferredLegacyBatchSwath first = _deferredLegacyBatchSwaths[0];
            uint[] blankImageCmd = (uint[])first.ImageCmd.Clone();
            for (int i = 6; i < blankImageCmd.Length; i++)
                blankImageCmd[i] = 0;
            bool markFirstHome = first.MarkFirstHomeAfterStartScan;
            first.MarkFirstHomeAfterStartScan = false;
            _deferredLegacyBatchSwaths.Insert(0, new DeferredLegacyBatchSwath
            {
                StartScanCmd = (uint[])first.StartScanCmd.Clone(),
                ImageCmd = blankImageCmd,
                EndDocCmd = (uint[])first.EndDocCmd.Clone(),
                PassIndex = -1,
                LayerIndex = first.LayerIndex,
                MarkFirstHomeAfterStartScan = markFirstHome,
                IsBlankPrime = true
            });
            Log4Net.Info($"[MeteorBlankPrime] Queued stage={stage} dir={first.StartScanCmd[2]} xStart={blankImageCmd[3]} width={blankImageCmd[5]} payloadWords={Math.Max(0, blankImageCmd.Length - 6)} queueCount={_deferredLegacyBatchSwaths.Count} utc={DateTime.UtcNow:O}");
        }

        private static bool FlushDeferredLegacyBatchSwaths(string stage)
        {
            if (_deferredLegacyBatchSwaths.Count == 0)
                return true;
            Log4Net.Info($"[MeteorBatchDefer] FlushBegin stage={stage} swathCount={_deferredLegacyBatchSwaths.Count} utc={DateTime.UtcNow:O}");
            foreach (DeferredLegacyBatchSwath swath in _deferredLegacyBatchSwaths)
            {
                if (!WaitForCommandSpace((uint)swath.ImageCmd.Length, $"BatchDeferFlush IMAGE pass={swath.PassIndex} layer={swath.LayerIndex}"))
                {
                    Log4Net.Info($"[MeteorBatchDefer] FlushAbort passIndex={swath.PassIndex} reason=commandSpace utc={DateTime.UtcNow:O}");
                    return false;
                }
                int r = DoSendCommand(swath.StartScanCmd);
                if (r != RVAL_OK)
                {
                    Log4Net.Info($"[MeteorBatchDefer] FlushFail passIndex={swath.PassIndex} stage=STARTSCAN r={r} utc={DateTime.UtcNow:O}");
                    return false;
                }
                if (swath.MarkFirstHomeAfterStartScan)
                {
                    _homeCommandIssued = true;
                    LogPccStatus("BatchDeferFlush-AfterFirstStartScan");
                }
                r = DoSendCommand(swath.ImageCmd);
                if (r != RVAL_OK)
                {
                    Log4Net.Info($"[MeteorBatchDefer] FlushFail passIndex={swath.PassIndex} stage=IMAGE r={r} utc={DateTime.UtcNow:O}");
                    return false;
                }
                r = DoSendCommand(swath.EndDocCmd);
                if (r != RVAL_OK)
                {
                    Log4Net.Info($"[MeteorBatchDefer] FlushFail passIndex={swath.PassIndex} stage=ENDDOC r={r} utc={DateTime.UtcNow:O}");
                    return false;
                }
                Log4Net.Info($"[MeteorBatchDefer] FlushSwathDone passIndex={swath.PassIndex} blankPrime={swath.IsBlankPrime} layer={swath.LayerIndex} utc={DateTime.UtcNow:O}");
                if (!swath.IsBlankPrime)
                    SignalPassSwathMeteorReadyIfNeeded(swath.PassIndex, "BatchDeferFlush:SwathEndDoc");
            }
            _deferredLegacyBatchSwaths.Clear();
            Log4Net.Info($"[MeteorBatchDefer] FlushEnd stage={stage} utc={DateTime.UtcNow:O}");
            return true;
        }

        /// <summary>
        /// Legacy batch 模式下，Pass0 扫程必须等整批 swath 全部入 PCC 后再启动，避免 Pass1/2 STARTSCAN 在 Pass0 运动中插入。
        /// </summary>
        public static void SignalBatchSwathsMeteorReady(string stage)
        {
            if (_deferredLegacyBatchSwaths.Count > 0)
            {
                if (!FlushDeferredLegacyBatchSwaths(stage))
                    Log4Net.Info($"[MeteorBatchDefer] FlushFailed stage={stage} utc={DateTime.UtcNow:O}");
            }
            SignalPass0FirstSwathMeteorReadyIfNeeded(stage);
        }

        /// <summary>官方扫描顺序：连续提交全部 swath，默认延后 ENDJOB 至 Pass2 物理扫程结束，再放行 Pass0 运动。</summary>
        public static bool CompleteOfficialQueuedBatchBeforeMotion(string stage)
        {
            PrependBlankPrimeSwathIfEnabled(stage);
            if (!FlushDeferredLegacyBatchSwaths(stage + ":Flush"))
            {
                Log4Net.Info($"[MeteorOfficialQueue] CompleteFailed stage={stage} reason=FlushSwaths utc={DateTime.UtcNow:O}");
                return false;
            }
            if (IsBatchEndJobImmediateEnabled())
            {
                if (!SendEndJobPreserveLayerGates(stage + ":EndJobAfterAllSwaths"))
                {
                    Log4Net.Info($"[MeteorOfficialQueue] CompleteFailed stage={stage} reason=EndJob utc={DateTime.UtcNow:O}");
                    return false;
                }
                SignalPass0FirstSwathMeteorReadyIfNeeded(stage + ":AllSwathsQueuedAndEndJobSent");
                Log4Net.Info($"[MeteorOfficialQueue] CompleteOk stage={stage} sequence=STARTJOB+{(IsBlankPrimeSwathEnabled() ? "blank prime+" : string.Empty)}3x(STARTSCAN,IMAGE,ENDDOC)+ENDJOB(immediate) then motion utc={DateTime.UtcNow:O}");
            }
            else
            {
                MarkDeferredEndJobAfterPassSwaths(stage + ":OfficialQueuedDeferredEndJob");
                SignalPass0FirstSwathMeteorReadyIfNeeded(stage + ":AllSwathsQueuedDeferredEndJob");
                Log4Net.Info($"[MeteorOfficialQueue] CompleteOk stage={stage} sequence=STARTJOB+{(IsBlankPrimeSwathEnabled() ? "blank prime+" : string.Empty)}3x(STARTSCAN,IMAGE,ENDDOC)+deferred ENDJOB(after Pass2 scan) then motion utc={DateTime.UtcNow:O}");
            }
            return true;
        }

        /// <summary>
        /// 525mm 等停：swath 全部入 PCC 后、开扫前的额外缓冲（不改变 Pass0FirstSwath 门控逻辑）。
        /// METEOR_BATCH_POST_SEND_DEPART_DELAY_MS 可覆盖，默认 200ms；设 0 关闭。
        /// </summary>
        private static int GetBatchPostSendDepartDelayMs()
        {
            const int defaultMs = 200;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_BATCH_POST_SEND_DEPART_DELAY_MS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int ms) && ms >= 0 && ms <= 60000)
                    return ms;
            }
            catch { }
            return defaultMs;
        }

        /// <summary>打印线程在 425mm 放行数据线程后调用：等待整批 swath 进 PCC，再延迟后启动扫程。</summary>
        public static bool WaitPass0FirstSwathMeteorReady(int timeoutMs = 8000)
        {
            if (timeoutMs <= 0)
                timeoutMs = 8000;
            Log4Net.Info($"[MeteorScanGate] Pass0FirstSwathWaitBegin timeoutMs={timeoutMs} utc={DateTime.UtcNow:O}");
            bool got = _pass0FirstSwathMeteorReady.Wait(timeoutMs);
            if (!got)
            {
                Log4Net.Info($"[MeteorScanGate] Pass0FirstSwathWaitTimeout timeoutMs={timeoutMs} utc={DateTime.UtcNow:O}");
                return false;
            }
            Log4Net.Info($"[MeteorScanGate] Pass0FirstSwathWaitEnd released=true utc={DateTime.UtcNow:O}");
            if (IsBatchSwathModeEnabled() && !IsBatchStartScanGateSplitEnabled() && !IsSplitJobPerPassEnabled())
            {
                int departDelayMs = GetBatchPostSendDepartDelayMs();
                if (departDelayMs > 0)
                {
                    Log4Net.Info($"[MeteorScanGate] BatchPostSendDepartDelay begin delayMs={departDelayMs} note=supplier:sendAllDataThenWaitBeforeMotion utc={DateTime.UtcNow:O}");
                    LogPccMotionSnapshot("BatchPostSendDepartDelay-Before");
                    Thread.Sleep(departDelayMs);
                    LogPccMotionSnapshot("BatchPostSendDepartDelay-After");
                    Log4Net.Info($"[MeteorScanGate] BatchPostSendDepartDelay end delayMs={departDelayMs} utc={DateTime.UtcNow:O}");
                }
            }
            return true;
        }

        /// <summary>打印线程在每??PASS0（墨车队列入口，??LayerPass0BeforeCarMotion 一致）调用，放行数据线程发包??/summary>
        public static void SignalPrintThreadLayerPass0ReadyForMeteorSubmit(int printLoopLayerK)
        {
            SignalPrintThreadPassReadyForMeteorSubmit(printLoopLayerK, 0, "pass0");
        }

        public static void SignalPrintThreadPassReadyForMeteorSubmit(int printLoopLayerK, int passIndex, string motionPath)
        {
            if (!IsPass0GateEnabled())
                return;
            int safePassIndex = Math.Max(0, Math.Min(passIndex, _passScanGateEvents.Length - 1));
            CapturePassScanOriginAbsXAnchor(safePassIndex);
            if (safePassIndex > 0 && !ShouldUsePassGateAnchorXStartBatchGateSplit())
                ResetPassSwathMeteorReady(safePassIndex);
            if (safePassIndex == 0)
                _pass0GateSendStartJob.Set();
            _passScanGateEvents[safePassIndex].Set();
            Log4Net.Info($"[MeteorScanGate] SignalPrintThreadPass Ready printLoopK={printLoopLayerK} passIndex={safePassIndex} motionPath={motionPath} managedThreadId={Thread.CurrentThread.ManagedThreadId} utc={DateTime.UtcNow:O}");
        }

        public static void SignalPrintThreadLayerPass0PreheatReady(int printLoopLayerK, string motionPath)
        {
            if (!IsPass0GateEnabled())
                return;
            _pass0PreheatStartJobOk = false;
            try { _pass0PreheatStartJobDone.Reset(); } catch { }
            _pass0GatePreheatStartJob.Set();
            Log4Net.Info($"[MeteorScanGate] SignalPrintThreadLayerPass0 PreheatReady printLoopK={printLoopLayerK} motionPath={motionPath} managedThreadId={Thread.CurrentThread.ManagedThreadId} utc={DateTime.UtcNow:O}");
        }

        /// <summary>??SendStartJob 紧前调用??paramref name="rasterLayerIndex"/> 为排版层索引 j??paramref name="jobLayerStart"/> 为任务起始层 g_nLayerStart（首 raster 默认跳过 Gate 以避免与 g_PrintSchedule 死锁）??/summary>
        public static void WaitPass0GateBeforeMeteorSubmitIfEnabled(string stage, int rasterLayerIndex, int jobLayerStart = 0)
        {
            if (!IsPass0GateEnabled())
                return;

            bool skipCfg = ShouldSkipFirstLayerWait();
            bool isFirstRaster = rasterLayerIndex <= jobLayerStart;
            if (skipCfg && isFirstRaster)
            {
                Log4Net.Info($"[MeteorScanGate] SkipWaitFirstRaster stage={stage} rasterLayerIndex={rasterLayerIndex} jobLayerStart={jobLayerStart} METEOR_PASS0_GATE_SKIP_FIRST_LAYER_WAIT=1 avoidPrintScheduleDeadlock");
                return;
            }

            int wm = _pass0GateConfig.Value.WaitMs;
            string wmLog = wm == Timeout.Infinite ? "Infinite" : wm.ToString();

            bool got = wm == Timeout.Infinite ? _pass0GateSendStartJob.Wait(Timeout.Infinite) : _pass0GateSendStartJob.Wait(wm);

            if (!got)
                Log4Net.Info($"[MeteorScanGate] WaitTimeout stage={stage} rasterLayerIndex={rasterLayerIndex} waitMs={wmLog} ??继续发包，请排查打印线程??Signal 或与 schedule 互相等待");
            else
                _pass0SubmitGateConsumedForCurrentJob = true;

            try { _pass0GateSendStartJob.Reset(); } catch { /* 准备下一层下一??Wait */ }
        }

        public static void WaitPass0PreheatGateBeforeStartJobIfEnabled(string stage, int rasterLayerIndex, int jobLayerStart = 0)
        {
            if (!IsPass0GateEnabled())
                return;

            bool skipCfg = ShouldSkipFirstLayerWait();
            bool isFirstRaster = rasterLayerIndex <= jobLayerStart;
            if (skipCfg && isFirstRaster)
            {
                Log4Net.Info($"[MeteorScanGate] SkipPreheatWaitFirstRaster stage={stage} rasterLayerIndex={rasterLayerIndex} jobLayerStart={jobLayerStart} METEOR_PASS0_GATE_SKIP_FIRST_LAYER_WAIT=1");
                return;
            }

            int wm = _pass0GateConfig.Value.WaitMs;
            string wmLog = wm == Timeout.Infinite ? "Infinite" : wm.ToString();

            bool got = wm == Timeout.Infinite ? _pass0GatePreheatStartJob.Wait(Timeout.Infinite) : _pass0GatePreheatStartJob.Wait(wm);

            if (!got)
                Log4Net.Info($"[MeteorScanGate] PreheatWaitTimeout stage={stage} rasterLayerIndex={rasterLayerIndex} waitMs={wmLog} 继续发??STARTJOB，请检??PASS0 预热信号是否缺失");

            try { _pass0GatePreheatStartJob.Reset(); } catch { }
        }

        private static void SignalPass0PreheatStartJobDone(bool ok, string stage)
        {
            if (!IsPass0GateEnabled())
                return;
            _pass0PreheatStartJobOk = ok;
            try { _pass0PreheatStartJobDone.Set(); } catch { }
            Log4Net.Info($"[MeteorScanGate] Pass0PreheatStartJobDone stage={stage} ok={ok} managedThreadId={Thread.CurrentThread.ManagedThreadId} utc={DateTime.UtcNow:O}");
        }

        public static bool WaitPass0PreheatStartJobDoneIfEnabled(int timeoutMs = 10000)
        {
            if (!IsPass0GateEnabled())
                return true;
            int wm = timeoutMs;
            if (wm == 0)
                wm = _pass0GateConfig.Value.WaitMs;
            if (wm < -1)
                wm = 10000;
            string wmLog = wm == Timeout.Infinite ? "Infinite" : wm.ToString();
            bool got = wm == Timeout.Infinite ? _pass0PreheatStartJobDone.Wait(Timeout.Infinite) : _pass0PreheatStartJobDone.Wait(wm);
            if (!got)
            {
                Log4Net.Info($"[MeteorScanGate] Pass0PreheatStartJobWaitTimeout timeoutMs={wmLog} utc={DateTime.UtcNow:O}");
                return false;
            }
            Log4Net.Info($"[MeteorScanGate] Pass0PreheatStartJobWaitEnd ok={_pass0PreheatStartJobOk} timeoutMs={wmLog} utc={DateTime.UtcNow:O}");
            return _pass0PreheatStartJobOk;
        }

        public static void WaitPassGateBeforeMeteorSubmitIfEnabled(string stage, int rasterLayerIndex, int passIndex, int jobLayerStart = 0)
        {
            if (passIndex == 0)
            {
                WaitPass0GateBeforeMeteorSubmitIfEnabled(stage, rasterLayerIndex, jobLayerStart);
                return;
            }

            if (!IsPass0GateEnabled())
                return;

            int safePassIndex = Math.Max(0, Math.Min(passIndex, _passScanGateEvents.Length - 1));
            int wm = _pass0GateConfig.Value.WaitMs;
            string wmLog = wm == Timeout.Infinite ? "Infinite" : wm.ToString();

            ManualResetEventSlim gate = _passScanGateEvents[safePassIndex];
            bool got = wm == Timeout.Infinite ? gate.Wait(Timeout.Infinite) : gate.Wait(wm);

            if (!got)
                Log4Net.Info($"[MeteorScanGate] WaitTimeout stage={stage} rasterLayerIndex={rasterLayerIndex} passIndex={safePassIndex} waitMs={wmLog} continueWithoutSignal");

            try { gate.Reset(); } catch { }
        }

        private static void ResetPass0GateAfterMeteorJobEnd(bool clearAutoPrintOverride = false)
        {
            try
            {
                _pass0GateSendStartJob.Reset();
                _pass0GatePreheatStartJob.Reset();
                _pass0PreheatStartJobDone.Reset();
                _pass0PreheatStartJobOk = false;
                foreach (var gate in _passScanGateEvents)
                {
                    try { gate.Reset(); } catch { }
                }
                _pass0SubmitGateConsumedForCurrentJob = false;
                ResetPass0FirstSwathMeteorReady();
                ResetPass0DualCoordScanAnchors();
                ResetPassImageXStartAnchors();
                for (int i = 1; i < _passSwathMeteorReady.Length; i++)
                    ResetPassSwathMeteorReady(i);
                if (clearAutoPrintOverride)
                {
                    _pass0GateEnabledOverride = null;
                    _pass0GateSkipFirstLayerWaitOverride = null;
                }
                ResetDeferredEndJobState();
                ResetDeferredLegacyBatchSwaths();
                Log4Net.Info($"[MeteorScanGate] gate reset after job end/stop clearAutoPrintOverride={clearAutoPrintOverride} utc={DateTime.UtcNow:O}");
            }
            catch { }
        }

        /// <summary>
        /// 是否在 SendStartJob 发 PCMD_STARTJOB 前再调 PiSetHome。
        /// 默认 true：墨车从 750mm 越过 Home 区后，在 525mm 扫程入口静止对齐 PCC AbsX；METEOR_PISET_HOME_AT_JOB_START=0/false/off 可回退旧行为。
        /// </summary>
        private static bool IsPiSetHomeAtJobStartEnabled()
        {
            if (IsOfficialQueuedScanModeEnabled() && IsPerLayerHome800ModeEnabled())
                return true;
            if (IsOfficialQueuedScanModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PISET_HOME_AT_JOB_START");
                if (string.IsNullOrWhiteSpace(env))
                    return true;
                string t = env.Trim();
                if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                    return false;
                return string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }
        }

        /// <summary>
        /// PrintEngine 在 Rx:StartJob 后仍异步执行 HALT/波形等；首条 STARTSCAN 不宜过早。
        /// 默认要求自 STARTJOB 成功起至少间隔本毫秒数后再发首条 STARTSCAN（非 PiSetHome 时机）。
        /// 环境变量 METEOR_MIN_MS_AFTER_STARTJOB_FOR_SETHOME 可覆盖（0–15000）。
        /// </summary>
        private static int GetMinMsAfterStartJobForPiSetHome()
        {
            // 实测 200ms 可能导致个别喷头未稳定出墨，先回到 400ms 保留运动重叠优化。
            const int defaultMs = 400;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_MIN_MS_AFTER_STARTJOB_FOR_SETHOME");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int ms) && ms >= 0 && ms <= 15000)
                    return ms;
            }
            catch { }
            return defaultMs;
        }

        /// <summary>
        /// HiPrint 兼容模式是否跳过 STARTJOB 后首条 STARTSCAN 的最小等待。
        /// 默认 false（等待 METEOR_MIN_MS_AFTER_STARTJOB_FOR_SETHOME，避免 CLEAR_HALT 前发图导致无喷）；
        /// METEOR_HIPRINT_SKIP_STARTJOB_DELAY=1/true 可恢复旧行为。
        /// </summary>
        private static bool ShouldSkipStartJobDelayForHiPrintCompat()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_HIPRINT_SKIP_STARTJOB_DELAY");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>true 表示使用原生 PrinterInterface.dll P/Invoke 路径??NET 无可用实例时启用）??/summary>
        private static int GetStartJobBusyRetryCount()
        {
            const int defaultCount = 10;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_STARTJOB_BUSY_RETRY_COUNT");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int count) && count >= 0 && count <= 60)
                    return count;
            }
            catch { }
            return defaultCount;
        }

        private static int GetStartJobBusyRetryDelayMs()
        {
            const int defaultMs = 500;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_STARTJOB_BUSY_RETRY_DELAY_MS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int ms) && ms >= 0 && ms <= 10000)
                    return ms;
            }
            catch { }
            return defaultMs;
        }

        /// <summary>多层打印：若上一层 EndJob 仍延后，等待运动线程 Pass2 扫程结束后再发下一层 STARTJOB。</summary>
        private static bool WaitForDeferredEndJobCleared(int timeoutMs = 120000)
        {
            if (!_deferEndJobPending)
                return true;
            Log4Net.Info($"[MeteorJob] WaitDeferredEndJobCleared begin timeoutMs={timeoutMs} utc={DateTime.UtcNow:O}");
            var sw = Stopwatch.StartNew();
            while (_deferEndJobPending && sw.ElapsedMilliseconds < timeoutMs)
                Thread.Sleep(50);
            if (_deferEndJobPending)
            {
                Log4Net.Info($"[MeteorJob] WaitDeferredEndJobCleared TIMEOUT timeoutMs={timeoutMs} utc={DateTime.UtcNow:O}");
                return false;
            }
            Log4Net.Info($"[MeteorJob] WaitDeferredEndJobCleared ok elapsedMs={sw.ElapsedMilliseconds} utc={DateTime.UtcNow:O}");
            return true;
        }

        private static int GetPccIdleWaitTimeoutMs()
        {
            const int defaultMs = 30000;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PCC_IDLE_WAIT_MS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int ms) && ms >= 1000 && ms <= 120000)
                    return ms;
            }
            catch { }
            return defaultMs;
        }

        private static bool TryGetPccBmStatusBits(out int bmStatusBits)
        {
            bmStatusBits = 0;
            if (!TryGetPccMotionSnapshot(1, out PccMotionSnapshot snap) || !snap.Valid)
                return false;
            bmStatusBits = snap.BmStatusBits;
            return true;
        }

        /// <summary>CA00(含 BM_SCANNING) 表示 job 槽仍占用；C800 才可接受新 STARTJOB。</summary>
        private static bool IsPccBmStatusReadyForStartJob(int bmStatusBits)
        {
            return (bmStatusBits & BM_SCANNING) == 0 && (bmStatusBits & 0x00001000) == 0;
        }

        private static bool WaitForPccReadyForNewStartJob(int timeoutMs, string stage)
        {
            var sw = Stopwatch.StartNew();
            int lastBm = 0;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (TryGetPccBmStatusBits(out lastBm) && IsPccBmStatusReadyForStartJob(lastBm))
                {
                    Log4Net.Info($"Meteor WaitForPccReadyForNewStartJob: ready bmStatusBits=0x{lastBm:X8} waitedMs={sw.ElapsedMilliseconds} stage={stage}");
                    return true;
                }
                Thread.Sleep(50);
            }
            Log4Net.Info($"Meteor WaitForPccReadyForNewStartJob: TIMEOUT lastBm=0x{lastBm:X8} timeoutMs={timeoutMs} waitedMs={sw.ElapsedMilliseconds} stage={stage}");
            return false;
        }

        private static bool TrySendEndJobUnlocked(string stage)
        {
            uint[] endJob = { PCMD_ENDJOB, 0 };
            int r = DoSendCommand(endJob);
            if (r == RVAL_OK)
            {
                Log4Net.Info($"Meteor TrySendEndJobUnlocked: PCMD_ENDJOB sent stage={stage}");
                _scanJobStarted = false;
                _pendingScanJobWidth = 1;
                _homeCommandIssued = false;
                _startJobUtc = null;
                _deferEndJobPending = false;
                LogPccStatus($"TrySendEndJobUnlocked-{stage}");
                return true;
            }
            Log4Net.Info($"Meteor TrySendEndJobUnlocked: PCMD_ENDJOB failed r={r} stage={stage}");
            return false;
        }

        /// <summary>调用方须已持有 SyncRoot。ENDJOB（+ 可选 PiSetHome）循环直至 job 槽释放。</summary>
        private static bool TryForcePccIdleForNextStartJobUnlocked(string stage, int timeoutMs = 15000, bool allowPiSetHome = true)
        {
            var sw = Stopwatch.StartNew();
            int attempt = 0;
            int lastBm = 0;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (TryGetPccBmStatusBits(out lastBm) && IsPccBmStatusReadyForStartJob(lastBm))
                {
                    Log4Net.Info($"Meteor TryForcePccIdleForNextStartJob: ready bmStatusBits=0x{lastBm:X8} attempts={attempt} elapsedMs={sw.ElapsedMilliseconds} stage={stage}");
                    return true;
                }

                attempt++;
                Log4Net.Info($"Meteor TryForcePccIdleForNextStartJob: attempt={attempt} bmStatusBits=0x{lastBm:X8} allowPiSetHome={allowPiSetHome} stage={stage} utc={DateTime.UtcNow:O}");
                TrySendEndJobUnlocked(stage + $":Attempt{attempt}");
                Thread.Sleep(200);

                if (allowPiSetHome)
                {
                    TrySetHome();
                    WaitForPccAbsXHomeStable();
                }

                int remaining = timeoutMs - (int)sw.ElapsedMilliseconds;
                if (remaining > 0 && WaitForPccReadyForNewStartJob(Math.Min(3000, remaining), stage + ":Poll"))
                    return true;

                Thread.Sleep(100);
            }

            TryGetPccBmStatusBits(out lastBm);
            Log4Net.Info($"Meteor TryForcePccIdleForNextStartJob: failed lastBm=0x{lastBm:X8} attempts={attempt} elapsedMs={sw.ElapsedMilliseconds} stage={stage}");
            return false;
        }

        /// <summary>PCMD_STARTJOB 持续 busy 时 PiAbort+EndJob 恢复（调用方须已持有 SyncRoot）。</summary>
        private static bool TryAbortPrintJobUnlocked(string stage)
        {
            if (!_useNativePath)
                return false;
            try
            {
                int ret = NativePiAbort();
                Log4Net.Info($"Meteor TryAbortPrintJobUnlocked: PiAbort ret={ret} stage={stage}");
                _scanJobStarted = false;
                _homeCommandIssued = false;
                _startJobUtc = null;
                ResetDeferredEndJobState();
                Thread.Sleep(300);
                return ret == RVAL_OK;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TryAbortPrintJobUnlocked failed: {ex.Message} stage={stage}");
                return false;
            }
        }

        /// <summary>PCMD_STARTJOB 持续 busy 时尝试补发 ENDJOB 恢复 PCC 状态（调用方须已持有 SyncRoot）。</summary>
        private static bool TryRecoverPccForStartJobUnlocked(string stage)
        {
            if (_deferEndJobPending)
                return false;
            Log4Net.Info($"Meteor TryRecoverPccForStartJob: attempt recovery stage={stage} utc={DateTime.UtcNow:O}");
            TryAbortPrintJobUnlocked(stage + ":PiAbort");
            TrySendEndJobUnlocked(stage + ":AfterPiAbort");
            TrySetHome();
            WaitForPccAbsXHomeStable();
            int waitMs = Math.Min(GetPccIdleWaitTimeoutMs(), 8000);
            if (WaitForPccReadyForNewStartJob(waitMs, stage + ":AfterPiAbortEndJob"))
                return true;
            return TryForcePccIdleForNextStartJobUnlocked(stage + ":Fallback", waitMs, allowPiSetHome: false);
        }

        private static bool _useNativePath;

        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiOpenPrinter")]
        private static extern int NativePiOpenPrinter();
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiClosePrinter")]
        private static extern int NativePiClosePrinter();
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSetHome")]
        private static extern int NativePiSetHome();
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSetHeadPower")]
        private static extern int NativePiSetHeadPower(uint state);
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSetSignal")]
        private static extern int NativePiSetSignal(uint signalId, uint state);
        /// <summary>在本进程内启??PrintEngine（无需先启动厂商程序）。pConfigFile 为配置文件路径，可传 null/空使用默认??/summary>
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiStartPrintEngine", CharSet = CharSet.Ansi)]
        private static extern int NativePiStartPrintEngine([MarshalAs(UnmanagedType.LPStr)] string pConfigFile);
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiStopPrintEngine")]
        private static extern int NativePiStopPrintEngine(uint dwForce);
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSendCommand")]
        private static extern int NativePiSendCommand(IntPtr pCmd);

        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiSetParam")]
        private static extern int NativePiSetParam(uint paramId, uint value);
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiGetCommandSpaceDwords")]
        private static extern uint NativePiGetCommandSpaceDwords(uint lane);
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiAbort")]
        private static extern int NativePiAbort();
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiGetPrnStatus")]
        private static extern IntPtr NativePiGetPrnStatus();
        [DllImport("PrinterInterface.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "PiGetPccStatus")]
        private static extern IntPtr NativePiGetPccStatus(uint pccnum);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePreloadPathInfo
        {
            public int DocsSent;
            public int TotalCopies;
            public int CurrentDoc_obsolete;
            public int CopiesToGo_obsolete;
            public int DocsToGo_obsolete;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFifoPathInfo
        {
            public int DocsSent;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeAppStatus
        {
            public int StructVersion;
            public int PeVersion;
            public int HeadType;
            public int Control;
            public int NumPlanes;
            public int Yinterlace_deprecated;
            public int PccsRequired;
            public int PccsAttached;
            public int PrinterState;
            public int PrintSpeed;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public int[] DocIds;
            public int DocCount;
            public int PdCount;
            public int PrintCount;
            public int Xdpi_deprecated;
            public int Ydpi_deprecated;
            public NativePreloadPathInfo PreloadPath;
            public NativeFifoPathInfo FifoPath;
            public int HeadPowerState;
            public int bmMeteorStatus;
            public int PeBuildNumber;
            public int SupportedBppBitmask;
            public int BitsPerPixel;
            public int PrintInterval;
            public int HeadPrintLineLengthDWORDS;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] PCCsPresent;
            public int DocsQueuedLane1;
            public int DocsQueuedLane2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePccStatus
        {
            public int StructVersion;
            public int IoSignals;
            public int bmStatusBits;
            public int JobStatus_obsolete;
            public int FpgaVersion;
            public int FwVersion;
            public int PdCount;
            public int PrintCount;
            public int FaultRegister_obsolete;
            public int AbsXCount;
            public int EncoderCount;
            public int bmStatusBits2;
        }

        private struct PccMotionSnapshot
        {
            public bool Valid;
            public int PccNum;
            public int BmStatusBits;
            public int BmStatusBits2;
            public int AbsXCount;
            public int EncoderCount;
        }

        public struct PccRasterSample
        {
            public bool Valid;
            public int AbsXCount;
            public int AbsX24;
            public int EncoderCount;
            public int BmStatusBits;
            public int BmStatusBits2;
        }

        /// <summary>测试采集用：只读 PCC 光栅坐标，不改变打印状态。</summary>
        public static bool TryReadPccRasterSample(out PccRasterSample sample)
        {
            sample = default;
            if (!TryGetPccMotionSnapshot(1, out PccMotionSnapshot snapshot) || !snapshot.Valid)
                return false;

            sample.Valid = true;
            sample.AbsXCount = snapshot.AbsXCount;
            sample.AbsX24 = GetAbsXCount24Signed(snapshot.AbsXCount);
            sample.EncoderCount = snapshot.EncoderCount;
            sample.BmStatusBits = snapshot.BmStatusBits;
            sample.BmStatusBits2 = snapshot.BmStatusBits2;
            return true;
        }

        /// <summary>查询命令空间是否足够??/summary>
        private static bool WaitForCommandSpace(uint requiredDwords, string stage, int timeoutMs = 3000, uint lane = 0)
        {
            int waited = 0;
            while (true)
            {
                uint available = 0;
                try
                {
                    available = NativePiGetCommandSpaceDwords(lane);
                }
                catch (Exception ex)
                {
                    Log4Net.Info($"Meteor {stage}: PiGetCommandSpaceDwords 异常: {ex.Message}");
                    return false;
                }

                if (available >= requiredDwords)
                {
                    Log4Net.Info($"Meteor {stage}: 命令空间足够 required={requiredDwords} available={available} lane={lane}");
                    return true;
                }

                if (waited == 0 || waited % 500 == 0)
                    Log4Net.Info($"Meteor {stage}: 命令空间不足 required={requiredDwords} available={available} lane={lane} waitedMs={waited}");

                if (waited >= timeoutMs)
                {
                    Log4Net.Info($"Meteor {stage}: 命令空间等待超时 required={requiredDwords} lane={lane} timeoutMs={timeoutMs}");
                    return false;
                }

                System.Threading.Thread.Sleep(50);
                waited += 50;
            }
        }

        private static int GetScanMotionWaitTimeoutMs()
        {
            const int defaultMs = 2500;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_SCAN_MOTION_WAIT_MS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int ms) && ms >= 0 && ms <= 15000)
                    return ms;
            }
            catch { }
            return defaultMs;
        }

        private static int GetScanMotionThresholdCounts()
        {
            const int defaultCounts = 256;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_SCAN_MOTION_THRESHOLD_COUNTS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int counts) && counts >= 1 && counts <= 100000)
                    return counts;
            }
            catch { }
            return defaultCounts;
        }

        private static int GetScanMotionConsecutivePollsRequired()
        {
            const int defaultPolls = 2;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_SCAN_MOTION_CONSECUTIVE_POLLS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int polls) && polls >= 1 && polls <= 20)
                    return polls;
            }
            catch { }
            return defaultPolls;
        }

        private static int GetSafeImageXStartMin()
        {
            const int defaultMin = 8;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_MIN");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int value) && value >= 1 && value <= 4096)
                    return value;
            }
            catch { }
            return defaultMin;
        }

        private static int GetAllFwdPassStepOffsetPixels(int passIndex)
        {
            if (!IsAllFwdDiagnosticModeEnabled() || passIndex <= 0)
                return 0;
            try
            {
                int stepPx = 0;
                string stepEnv = Environment.GetEnvironmentVariable("METEOR_ALL_FWD_PASS_STEP_OFFSET_PX");
                if (!string.IsNullOrWhiteSpace(stepEnv))
                    int.TryParse(stepEnv.Trim(), out stepPx);

                int offsetPx = stepPx * passIndex;
                string passEnv = Environment.GetEnvironmentVariable("METEOR_ALL_FWD_PASS" + passIndex.ToString() + "_OFFSET_PX");
                if (!string.IsNullOrWhiteSpace(passEnv) && int.TryParse(passEnv.Trim(), out int passValue))
                    offsetPx = passValue;
                if (offsetPx > 4096)
                    offsetPx = 4096;
                if (offsetPx < -4096)
                    offsetPx = -4096;
                return offsetPx;
            }
            catch { }
            return 0;
        }

        private static bool TryGetPassSpecificImageXStartPixels(int passIndex, uint startScanDir, out int xStartPx, out string envName)
        {
            xStartPx = 0;
            envName = null;
            int safePassIndex = Math.Max(0, Math.Min(passIndex, 2));
            string directionalName = startScanDir == SD_REV
                ? "METEOR_PASS" + safePassIndex.ToString() + "_REV_XSTART_PX"
                : "METEOR_PASS" + safePassIndex.ToString() + "_FWD_XSTART_PX";
            string genericName = "METEOR_PASS" + safePassIndex.ToString() + "_IMAGE_XSTART_PX";
            try
            {
                string env = Environment.GetEnvironmentVariable(directionalName);
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int directionalValue) && directionalValue > 0)
                {
                    xStartPx = directionalValue;
                    envName = directionalName;
                    return true;
                }

                env = Environment.GetEnvironmentVariable(genericName);
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int genericValue) && genericValue > 0)
                {
                    xStartPx = genericValue;
                    envName = genericName;
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 现场对照 Meteor 自带软件：RightToLeft=0 时，按扫向强制 PCMD_IMAGE Xleft。
        /// METEOR_IMAGE_XSTART_FIXED_FWD_PX / _REV_PX 可分别覆盖；METEOR_IMAGE_XSTART_FIXED_PX 兼容旧的统一覆盖；
        /// 任一值设为负数可关闭本实验覆盖。默认 FWD=4110（750mm Home 位 PiSetHome 后的标定）；REV 仍走 unified fwdBase+图宽。
        /// </summary>
        private static bool TryGetForcedImageXStartPixels(uint startScanDir, out int forcedXStartPx)
        {
            if (IsOfficialQueuedScanModeEnabled())
            {
                int xDpi = 400;
                int fwdAt750Px = 4110;
                int homeShiftPx = (int)Math.Round(
                    (GetPerLayerMeteorHomeMm() - GetInkCarPiSetHomeReferenceMm()) * xDpi / 25.4,
                    MidpointRounding.AwayFromZero);
                int officialFwdPx = Math.Max(GetSafeImageXStartMin(), fwdAt750Px + homeShiftPx);
                forcedXStartPx = startScanDir == SD_REV ? -1 : officialFwdPx;
                try
                {
                    string legacyAt750Env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_LEGACY_FWD_AT_750_PX");
                    if (!string.IsNullOrWhiteSpace(legacyAt750Env) && int.TryParse(legacyAt750Env.Trim(), out int legacyAt750Value) && legacyAt750Value > 0)
                    {
                        officialFwdPx = Math.Max(GetSafeImageXStartMin(), legacyAt750Value + homeShiftPx);
                        if (startScanDir == SD_FWD)
                            forcedXStartPx = officialFwdPx;
                    }
                    string officialEnv = Environment.GetEnvironmentVariable(
                        startScanDir == SD_REV ? "METEOR_OFFICIAL_IMAGE_XSTART_REV_PX" : "METEOR_OFFICIAL_IMAGE_XSTART_FWD_PX");
                    if (!string.IsNullOrWhiteSpace(officialEnv) && int.TryParse(officialEnv.Trim(), out int officialValue))
                        forcedXStartPx = officialValue;
                }
                catch { }
                if (startScanDir == SD_FWD)
                    Log4Net.Info($"[MeteorOfficialHome] FwdXStart homeMm={GetPerLayerMeteorHomeMm():F3} referenceMm={GetInkCarPiSetHomeReferenceMm():F3} homeShiftPx={homeShiftPx} xStartPx={forcedXStartPx}");
                return forcedXStartPx >= 0;
            }

            int defaultForcedFwdXStartPx = GetDefaultForcedFwdXStartPx();
            const int defaultForcedRevXStartPx = -1;
            forcedXStartPx = startScanDir == SD_REV ? defaultForcedRevXStartPx : defaultForcedFwdXStartPx;
            if (forcedXStartPx < 0)
                return false;
            try
            {
                string legacyEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_FIXED_PX");
                if (!string.IsNullOrWhiteSpace(legacyEnv) && int.TryParse(legacyEnv.Trim(), out int legacyValue))
                {
                    if (legacyValue < 0)
                        return false;
                    forcedXStartPx = legacyValue;
                }

                string directionalEnv = Environment.GetEnvironmentVariable(
                    startScanDir == SD_REV ? "METEOR_IMAGE_XSTART_FIXED_REV_PX" : "METEOR_IMAGE_XSTART_FIXED_FWD_PX");
                if (!string.IsNullOrWhiteSpace(directionalEnv) && int.TryParse(directionalEnv.Trim(), out int directionalValue))
                {
                    if (directionalValue < 0)
                        return false;
                    forcedXStartPx = directionalValue;
                    return true;
                }
            }
            catch { }
            if (startScanDir == SD_FWD && forcedXStartPx > 0
                && TryGetLayerCompensatedFwdBasePx(forcedXStartPx, out int compensatedFwdPx, out int deltaCompPx))
            {
                Log4Net.Info($"[MeteorLayerAbsXDeltaComp] ForcedFwdAdjusted nominal={forcedXStartPx} refAbsX24AtFlush={_layerAbsXDeltaCompRefAtFlush} measAbsX24AtFlush={_layerAbsXDeltaCompMeasAtFlush} deltaCompPx={deltaCompPx} compensated={compensatedFwdPx} utc={DateTime.UtcNow:O}");
                forcedXStartPx = compensatedFwdPx;
            }
            return true;
        }

        /// <summary>AbsX/XCOUNT（400dpi 像素计数）换算为 mm：mm = absX * 25.4 / dpi。</summary>
        private static double AbsXCountToMm(int absXCount, int xDpi)
        {
            int safeDpi = xDpi > 0 ? xDpi : 400;
            return GetAbsXCount24Signed(absXCount) * 25.4 / safeDpi;
        }

        /// <summary>默认用 STARTSCAN 前 PCC 实时 AbsX 作为 PCMD_IMAGE xStart，与现场 XCOUNT 对齐。</summary>
        private static bool ShouldUseLiveAbsXForImageXStart()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_FROM_LIVE_ABS");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return true;
        }

        /// <summary>PASS1/2 FWD 合并扫程起点锚点与 live AbsX（默认开；METEOR_IMAGE_XSTART_USE_PASS_ANCHOR=0 可关）。</summary>
        private static bool ShouldUsePassAnchoredAbsXForImageXStart()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_USE_PASS_ANCHOR");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return true;
        }

        /// <summary>
        /// 对齐模式：PASS0 首条 swath 锁定 FWD fwdBase；Pass1 REV 用 HiPrint 式 fwdBase+图宽；Pass2 FWD 复用 fwdBase。
        /// batch 模式默认开；METEOR_IMAGE_XSTART_UNIFIED_ALIGN=0 可关。
        /// </summary>
        private static bool ShouldUseUnifiedAlignImageXStart()
        {
            if (IsBatchSwathModeEnabled())
                return true;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_UNIFIED_ALIGN");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>与 stripIndexSimple / passIndex 对齐，WriteImageLayer 据此选取 xStart 锚点。</summary>
        public static void SetPendingSwathPassIndex(int passIndex)
        {
            _pendingSwathPassIndex = Math.Max(0, Math.Min(passIndex, _passImageXStartAbsXAnchor.Length - 1));
        }

        private static void CapturePassScanOriginAbsXAnchor(int passIndex)
        {
            int idx = Math.Max(0, Math.Min(passIndex, _passImageXStartAbsXAnchor.Length - 1));
            if (TryGetPccMotionSnapshot(1, out PccMotionSnapshot snap) && snap.Valid)
            {
                int absX24 = GetAbsXCount24Signed(snap.AbsXCount);
                _passImageXStartAbsXAnchor[idx] = Math.Max(0, absX24);
                _passImageXStartAbsXAnchorValid[idx] = true;
                Log4Net.Info($"[MeteorXStart] PassScanOriginAnchor passIndex={idx} pccAbsX={snap.AbsXCount} absX24={absX24} utc={DateTime.UtcNow:O}");
            }
            else
            {
                _passImageXStartAbsXAnchorValid[idx] = false;
                Log4Net.Info($"[MeteorXStart] PassScanOriginAnchorMissing passIndex={idx} utc={DateTime.UtcNow:O}");
            }
        }

        private static void ResetPassSwathMeteorReady(int passIndex)
        {
            int idx = Math.Max(0, Math.Min(passIndex, _passSwathMeteorReady.Length - 1));
            _passSwathMeteorSignaled[idx] = false;
            try { _passSwathMeteorReady[idx].Reset(); } catch { }
            try { _passSwathPrepackedReady[idx].Reset(); } catch { }
            try { _passMotionStartedForMeteorSend[idx].Reset(); } catch { }
        }

        public static bool WaitPassSwathPrepackedReady(int passIndex, int timeoutMs = 10000)
        {
            int idx = Math.Max(0, Math.Min(passIndex, _passSwathPrepackedReady.Length - 1));
            if (timeoutMs <= 0)
                timeoutMs = 10000;
            bool got = _passSwathPrepackedReady[idx].Wait(timeoutMs);
            if (!got)
                Log4Net.Info($"[MeteorScanGate] PassSwathPrepackedWaitTimeout passIndex={idx} timeoutMs={timeoutMs} utc={DateTime.UtcNow:O}");
            return got;
        }

        public static void SignalPassMotionStartedForMeteorSend(int passIndex)
        {
            int idx = Math.Max(0, Math.Min(passIndex, _passMotionStartedForMeteorSend.Length - 1));
            _passMotionStartedForMeteorSend[idx].Set();
            Log4Net.Info($"[MeteorScanGate] PassMotionStartedForMeteorSend passIndex={idx} utc={DateTime.UtcNow:O}");
        }

        private static void SignalPassSwathMeteorReadyIfNeeded(int passIndex, string stage)
        {
            if (passIndex <= 0)
                return;
            int idx = Math.Max(0, Math.Min(passIndex, _passSwathMeteorReady.Length - 1));
            if (_passSwathMeteorSignaled[idx])
                return;
            bool docsReady = WaitForDocsQueuedLane1Ready(
                stage,
                GetPassSwathDocsAdmissionMinDocs(),
                GetPassSwathDocsAdmissionWaitMs(),
                GetPassSwathDocsAdmissionPollMs());
            if (!docsReady)
                Log4Net.Info($"[MeteorScanGate] PassSwathDocsAdmissionTimeout passIndex={idx} stage={stage} note=release after bounded wait; inspect DocsQueuedLane1 before changing scan timing");
            _passSwathMeteorSignaled[idx] = true;
            try { _passSwathMeteorReady[idx].Set(); } catch { }
            Log4Net.Info($"[MeteorScanGate] PassSwathMeteorReady passIndex={idx} stage={stage} docsReady={docsReady} utc={DateTime.UtcNow:O}");
        }

        /// <summary>打印线程：PASS1/2 在扫程起点 Signal 后，等待对应 swath 进 PCC 再启动 X 扫程；旧连续 batch 模式直接放行。</summary>
        public static bool WaitPassSwathMeteorReady(int passIndex, int timeoutMs = 10000)
        {
            if (IsBatchSwathModeEnabled() && !IsAllFwdDiagnosticModeEnabled() && !IsBatchStartScanGateSplitEnabled() && !IsPassIntraLayerLiveAbsXCompEnabled() && !IsPassGateAnchorXStartEnabled())
            {
                Log4Net.Info($"[MeteorScanGate] PassSwathWaitSkipped legacyBatchMode passIndex={passIndex} utc={DateTime.UtcNow:O}");
                return true;
            }
            if (passIndex <= 0)
                return true;
            int idx = Math.Max(0, Math.Min(passIndex, _passSwathMeteorReady.Length - 1));
            if (timeoutMs <= 0)
                timeoutMs = 10000;
            bool got = _passSwathMeteorReady[idx].Wait(timeoutMs);
            if (!got)
                Log4Net.Info($"[MeteorScanGate] PassSwathWaitTimeout passIndex={idx} timeoutMs={timeoutMs} utc={DateTime.UtcNow:O}");
            return got;
        }

        private static void ResetPassImageXStartAnchors()
        {
            for (int i = 0; i < _passImageXStartAbsXAnchorValid.Length; i++)
                _passImageXStartAbsXAnchorValid[i] = false;
            for (int i = 0; i < _passScanEndAbsXValid.Length; i++)
                _passScanEndAbsXValid[i] = false;
            _pendingSwathPassIndex = 0;
            _jobUnifiedImageXStartValid = false;
            _jobUnifiedImageXStartAbsX = 0;
        }

        private static bool ShouldPreserveLayerAnchorsForSplitPass()
        {
            return IsSplitJobPerPassEnabled() && _pendingSwathPassIndex > 0;
        }

        private static void RecordPassScanEndAbsXFromStage(string stage, int absX24)
        {
            if (string.IsNullOrEmpty(stage))
                return;
            int passIndex = -1;
            if (stage.IndexOf("Pass0AfterScan", StringComparison.OrdinalIgnoreCase) >= 0)
                passIndex = 0;
            else if (stage.IndexOf("Pass1AfterScan", StringComparison.OrdinalIgnoreCase) >= 0)
                passIndex = 1;
            else if (stage.IndexOf("Pass2AfterScan", StringComparison.OrdinalIgnoreCase) >= 0)
                passIndex = 2;
            if (passIndex < 0)
                return;
            _passScanEndAbsX[passIndex] = Math.Max(0, absX24);
            _passScanEndAbsXValid[passIndex] = true;
            Log4Net.Info($"[MeteorXStart] PassScanEndAbsX passIndex={passIndex} absX24={absX24} stage={stage} utc={DateTime.UtcNow:O}");
        }

        /// <summary>FWD pass&gt;0：live AbsX 与扫程起点锚点、上一 pass 结束 AbsX、Pass0 fwdBase 取 max。</summary>
        private static int ResolveLiveFwdImageXStartBasePixels(int pendingPass, int absXAtSwath)
        {
            int basePx = Math.Max(0, absXAtSwath);
            if (pendingPass > 0 && ShouldUsePassAnchoredAbsXForImageXStart()
                && _passImageXStartAbsXAnchorValid[pendingPass])
            {
                basePx = Math.Max(basePx, _passImageXStartAbsXAnchor[pendingPass]);
            }
            if (pendingPass >= 2 && _passScanEndAbsXValid[1])
                basePx = Math.Max(basePx, _passScanEndAbsX[1]);
            if (basePx <= 0 && _jobUnifiedImageXStartValid)
                basePx = _jobUnifiedImageXStartAbsX;
            if (pendingPass >= 2 && _jobUnifiedImageXStartValid)
                basePx = Math.Max(basePx, _jobUnifiedImageXStartAbsX);
            return basePx;
        }

        private static int GetImageXStartBasePixels(int xDpi)
        {
            int safeDpi = xDpi > 0 ? xDpi : 400;
            if (_scanJobStartXEncPosUm == 0)
                return 0;

            double pixels = (_scanJobStartXEncPosUm / 25400.0) * safeDpi;
            return Math.Max(0, (int)Math.Round(pixels, MidpointRounding.AwayFromZero));
        }

        private static int GetImageXStartBasePixels(float xDpi)
        {
            int roundedDpi = (int)Math.Round(xDpi > 0 ? xDpi : 400f, MidpointRounding.AwayFromZero);
            return GetImageXStartBasePixels(roundedDpi);
        }

        private static int GetImageXReverseStartDelta(int paddedWidth)
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_REV_DELTA");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int value))
                    return Math.Max(0, value);
            }
            catch { }
            // 3PASS 交替扫：REV 的 Xleft = FWD base + 图宽（HiPrint）；统一对齐模式走 useUnifiedRevXStart 分支
            return Math.Max(0, paddedWidth);
        }

        /// <summary>3PASS 单程扫程 mm（默认 450）。METEOR_SCAN_TRAVEL_MM 可覆盖（旧 400 写 400）。</summary>
        public static double GetInkCarScanTravelMm() => GetScanTravelMm();

        /// <summary>Pass0 提交 swath 前墨车停靠高端 mm，默认 525。</summary>
        public static double GetInkCarScanApproachHighEndMm()
        {
            const double defaultMm = 525.0;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_INKCAR_SCAN_APPROACH_HIGH_MM");
                if (!string.IsNullOrWhiteSpace(env) && double.TryParse(env.Trim(), out double mm) && mm > 1 && mm < 2000)
                    return mm;
            }
            catch { }
            return defaultMm;
        }

        /// <summary>
        /// 旧版 PiSetHome 参考等待位 mm（750 清洗站）。实际每层 PiSetHome 点由 METEOR_PER_LAYER_HOME_MM 指定。
        /// METEOR_PISET_HOME_REFERENCE_MM 可覆盖旧标定参考点。
        /// </summary>
        public static double GetInkCarPiSetHomeReferenceMm()
        {
            const double defaultMm = 750.0;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PISET_HOME_REFERENCE_MM");
                if (!string.IsNullOrWhiteSpace(env) && double.TryParse(env.Trim(), out double mm) && mm > 1 && mm < 2000)
                    return mm;
            }
            catch { }
            return defaultMm;
        }

        /// <summary>
        /// 墨车 X 扫程高端 mm（Pass1/2 扫程端 / 收口）。默认 525。
        /// METEOR_INKCAR_SCAN_HIGH_END_MM 可覆盖。
        /// </summary>
        public static double GetInkCarScanHighEndMm()
        {
            const double defaultHighMm = 525.0;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_INKCAR_SCAN_HIGH_END_MM");
                if (!string.IsNullOrWhiteSpace(env) && double.TryParse(env.Trim(), out double mm) && mm > 1 && mm < 2000)
                    return mm;
            }
            catch { }
            return defaultHighMm;
        }

        /// <summary>墨车 X 扫程低端 mm，默认 15（485−15=470mm 单程）。METEOR_INKCAR_SCAN_LOW_END_MM 可覆盖。</summary>
        public static double GetInkCarScanLowEndMm()
        {
            const double defaultLowMm = 15.0;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_INKCAR_SCAN_LOW_END_MM");
                if (!string.IsNullOrWhiteSpace(env) && double.TryParse(env.Trim(), out double mm) && mm > -200 && mm < 2000)
                    return mm;
            }
            catch { }
            return defaultLowMm;
        }

        /// <summary>3PASS 单程扫程 mm（默认 470，对齐 7323px≈465mm 整板宽 + 余量；旧 450/400 可用 METEOR_SCAN_TRAVEL_MM 覆盖）。</summary>
        private static double GetScanTravelMm()
        {
            const double defaultMm = 470.0;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_SCAN_TRAVEL_MM");
                if (!string.IsNullOrWhiteSpace(env) && double.TryParse(env.Trim(), out double mm) && mm > 1 && mm < 2000)
                    return mm;
            }
            catch { }
            return defaultMm;
        }

        private static int GetScanTravelWidthPixels(int xDpi)
        {
            int safeDpi = xDpi > 0 ? xDpi : 400;
            return Math.Max(1, (int)Math.Round(GetScanTravelMm() * safeDpi / 25.4, MidpointRounding.AwayFromZero));
        }

        private static int MmToAbsXPixels(double mm, int xDpi)
        {
            int safeDpi = xDpi > 0 ? xDpi : 400;
            return Math.Max(0, (int)Math.Round(mm * safeDpi / 25.4, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// 默认 FWD Xleft（AbsX 像素）。旧标定 4110 对应 METEOR_PISET_HOME_REFERENCE_MM（默认 750mm）；
        /// 每层 PiSetHome 点改动时按 METEOR_PER_LAYER_HOME_MM 与参考点距离折算。
        /// METEOR_IMAGE_XSTART_LEGACY_FWD_AT_750_PX 可覆盖旧标定；METEOR_IMAGE_XSTART_AT_PISET_HOME_PX 可直接覆盖新基准。
        /// </summary>
        private static int GetDefaultForcedFwdXStartPx(int xDpi = 400)
        {
            int legacyFwdXStartPx = 4110;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_LEGACY_FWD_AT_750_PX");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int legacyOverride) && legacyOverride > 0)
                    legacyFwdXStartPx = legacyOverride;

                string homeEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_AT_PISET_HOME_PX");
                if (!string.IsNullOrWhiteSpace(homeEnv) && int.TryParse(homeEnv.Trim(), out int homeOverride) && homeOverride > 0)
                    return homeOverride;
            }
            catch { }

            if (!IsPiSetHomeAtJobStartEnabled())
                return legacyFwdXStartPx;

            double referenceMm = GetInkCarPiSetHomeReferenceMm();
            double currentHomeMm = GetPerLayerMeteorHomeMm();
            int homeShiftPx = (int)Math.Round((currentHomeMm - referenceMm) * xDpi / 25.4, MidpointRounding.AwayFromZero);
            int adjustedXStartPx = legacyFwdXStartPx + homeShiftPx;
            Log4Net.Info($"[MeteorHomeXStart] legacyFwd={legacyFwdXStartPx} referenceMm={referenceMm:F3} homeMm={currentHomeMm:F3} homeShiftPx={homeShiftPx} adjustedFwd={adjustedXStartPx}");
            return Math.Max(GetSafeImageXStartMin(), adjustedXStartPx);
        }

        /// <summary>
        /// HiPrint 兼容：xStart 以 PCC live AbsX / pass0 实测 AbsX 扫程为准，不用固高 mm×DPI 冒充 AbsX。
        /// </summary>
        private static int ResolveHiPrintCompatImageCmdXStart(
            int xDpi,
            int printWidthPx,
            int safeMin,
            uint startScanDir,
            bool flipPass0,
            int pendingPass,
            int absXAtSwath,
            bool hasAbsXAtSwath,
            out int scanLowPx,
            out int scanHighPx,
            out int hiPrintBaseXStart,
            out int hiPrintRevXStart,
            out string anchorMode)
        {
            anchorMode = "unknown";
            scanLowPx = 0;
            scanHighPx = 0;
            hiPrintBaseXStart = Math.Max(safeMin, GetImageXStartBasePixels(xDpi));
            hiPrintRevXStart = hiPrintBaseXStart + printWidthPx;

            bool hasMeasuredRange = TryGetPass0MeasuredAbsXScanRange(out int measuredLow, out int measuredHigh);
            if (hasMeasuredRange)
            {
                scanLowPx = measuredLow;
                scanHighPx = measuredHigh;
            }

            if (flipPass0 && pendingPass == 0)
            {
                anchorMode = "legacyFlipPass0";
                if (startScanDir == SD_REV)
                {
                    anchorMode = hasAbsXAtSwath ? "legacyFlipPass0_liveAbsXPlusWidth_rev"
                        : hasMeasuredRange ? "legacyFlipPass0_measuredHigh_rev" : "legacyFlipPass0_jobRevXStart";
                    return ResolveHiPrintCompatRevImageXStart(
                        absXAtSwath, hasAbsXAtSwath, printWidthPx, measuredLow, measuredHigh, hasMeasuredRange, hiPrintRevXStart, safeMin, pendingPass);
                }
                if (hasAbsXAtSwath)
                {
                    anchorMode = "legacyFlipPass0_liveAbsX_fwd";
                    return Math.Max(safeMin, absXAtSwath);
                }
                if (hasMeasuredRange)
                {
                    anchorMode = "legacyFlipPass0_measuredLow_fwd";
                    return Math.Max(safeMin, measuredLow);
                }
                anchorMode = "legacyFlipPass0_jobBase_fwd";
                return Math.Max(safeMin, hiPrintBaseXStart);
            }

            // batch 预灌：pass1/2 在 pass0 扫程中连续下发，禁止用扫程中的 live AbsX（会随编码器漂移，导致 pass2 右下角错位）
            if (IsBatchSwathModeEnabled() && !IsSplitJobPerPassEnabled() && pendingPass > 0
                && _jobUnifiedImageXStartValid
                && (!IsPassGateAnchorXStartEnabled() || ShouldUsePassGateAnchorXStartBatchGateSplit()))
            {
                if (startScanDir == SD_REV)
                {
                    int pass1RevOffsetPx = IsPassGateAnchorXStartEnabled() ? GetPass1RevExtraOffsetPixels(xDpi) : 0;
                    anchorMode = IsPassGateAnchorXStartEnabled()
                        ? $"batch_layerAnchorRev_fwdBasePlusWidth_offset{pass1RevOffsetPx}"
                        : "batch_unifiedRev_fwdBasePlusWidth";
                    return Math.Max(safeMin, _jobUnifiedImageXStartAbsX + printWidthPx + pass1RevOffsetPx);
                }
                int pass2FwdOffsetPx = IsPassGateAnchorXStartEnabled() ? GetPass2FwdExtraOffsetPixels(xDpi) : 0;
                anchorMode = IsPassGateAnchorXStartEnabled()
                    ? $"batch_layerAnchorFwd_jobBase_offset{pass2FwdOffsetPx}"
                    : "batch_unifiedFwd_jobBase";
                return Math.Max(safeMin, _jobUnifiedImageXStartAbsX + pass2FwdOffsetPx);
            }

            if (pendingPass == 0)
            {
                if (startScanDir == SD_REV)
                {
                    anchorMode = hasAbsXAtSwath ? "pass0_liveAbsXPlusWidth_rev"
                        : hasMeasuredRange ? "pass0_measuredHigh_rev" : "pass0_jobBasePlusWidth_rev";
                    return ResolveHiPrintCompatRevImageXStart(
                        absXAtSwath, hasAbsXAtSwath, printWidthPx, measuredLow, measuredHigh, hasMeasuredRange, hiPrintRevXStart, safeMin, pendingPass);
                }
                if (TryGetForcedImageXStartPixels(SD_FWD, out int forcedPass0FwdPx))
                {
                    anchorMode = "pass0_fixedFwdPx";
                    return Math.Max(safeMin, forcedPass0FwdPx);
                }
                if (hasAbsXAtSwath)
                {
                    anchorMode = "pass0_liveAbsX_fwd";
                    return Math.Max(safeMin, absXAtSwath);
                }
                if (hasMeasuredRange)
                {
                    anchorMode = "pass0_measuredLow_fwd";
                    return Math.Max(safeMin, measuredLow);
                }
                anchorMode = "pass0_jobBase_fwd";
                return Math.Max(safeMin, hiPrintBaseXStart);
            }

            // 逐 pass 门控：Pass1 REV 用 Pass0 扫到 15mm 端的实测 AbsX 作右缘（比 pass 起点锚点/公式 fwdBase+scanTravel 更准）
            if (pendingPass == 1 && startScanDir == SD_REV
                && (!IsBatchSwathModeEnabled() || IsSplitJobPerPassEnabled() || IsPassGateAnchorXStartEnabled()))
            {
                if (_pass0ScanAbsXLowEndValid)
                {
                    int pass1RevOffsetPx = GetPass1RevExtraOffsetPixels(xDpi);
                    anchorMode = $"pass1_pass0MeasuredLowEndAbs_rev_offset{pass1RevOffsetPx}";
                    return Math.Max(safeMin, _pass0ScanAbsXAtLowEnd + pass1RevOffsetPx);
                }
                if (pendingPass < _passImageXStartAbsXAnchorValid.Length && _passImageXStartAbsXAnchorValid[pendingPass])
                {
                    int pass1RevOffsetPx = GetPass1RevExtraOffsetPixels(xDpi);
                    anchorMode = $"pass1_passScanOriginAnchor_rev_offset{pass1RevOffsetPx}";
                    return Math.Max(safeMin, _passImageXStartAbsXAnchor[pendingPass] + pass1RevOffsetPx);
                }
            }

            if (pendingPass >= 2 && startScanDir == SD_FWD
                && (!IsBatchSwathModeEnabled() || IsSplitJobPerPassEnabled() || IsPassGateAnchorXStartEnabled()))
            {
                int fallbackBase = 0;
                if (_passScanEndAbsXValid[1])
                    fallbackBase = Math.Max(fallbackBase, _passScanEndAbsX[1]);
                if (pendingPass < _passImageXStartAbsXAnchorValid.Length && _passImageXStartAbsXAnchorValid[pendingPass])
                    fallbackBase = Math.Max(fallbackBase, _passImageXStartAbsXAnchor[pendingPass]);
                if (hasAbsXAtSwath)
                    fallbackBase = Math.Max(fallbackBase, absXAtSwath);

                if (fallbackBase > 0)
                {
                    anchorMode = _passScanEndAbsXValid[1]
                        ? "pass2_pass1MeasuredHighEndAbs_fwd"
                        : "passN_passScanOriginAnchor_fwd";
                    return Math.Max(safeMin, fallbackBase);
                }
            }

            if (startScanDir == SD_REV)
            {
                anchorMode = hasAbsXAtSwath ? "passN_revHighOrLowSide_rev"
                    : hasMeasuredRange ? "passN_measuredHigh_rev" : "passN_jobBasePlusWidth_rev";
                return ResolveHiPrintCompatRevImageXStart(
                    absXAtSwath, hasAbsXAtSwath, printWidthPx, measuredLow, measuredHigh, hasMeasuredRange, hiPrintRevXStart, safeMin, pendingPass);
            }
            if (hasAbsXAtSwath)
            {
                anchorMode = "passN_liveAbsX_fwd";
                return Math.Max(safeMin, absXAtSwath);
            }
            anchorMode = "passN_jobBase_fwd";
            return Math.Max(safeMin, hiPrintBaseXStart);
        }

        /// <summary>REV 扫向：PCMD_IMAGE xStart 为图右缘。扫程低端起 REV 用 live+图宽；扫程高端起 REV（如 pass1@15mm）右缘即当前/锚点 AbsX。</summary>
        private static int ResolveHiPrintCompatRevImageXStart(
            int absXAtSwath,
            bool hasAbsXAtSwath,
            int printWidthPx,
            int measuredLow,
            int measuredHigh,
            bool hasMeasuredRange,
            int hiPrintRevXStart,
            int safeMin,
            int pendingPass = -1)
        {
            if (hasAbsXAtSwath)
            {
                int refAbs = absXAtSwath;
                if (pendingPass >= 0 && pendingPass < _passImageXStartAbsXAnchorValid.Length
                    && _passImageXStartAbsXAnchorValid[pendingPass])
                    refAbs = _passImageXStartAbsXAnchor[pendingPass];

                bool highSideRev = pendingPass > 0;
                if (hasMeasuredRange && measuredHigh > measuredLow)
                    highSideRev = refAbs >= (measuredLow + measuredHigh) / 2;
                else if (pendingPass <= 0 && refAbs > printWidthPx + 512)
                    highSideRev = true;

                if (highSideRev)
                    return Math.Max(safeMin, refAbs);
                return Math.Max(safeMin, refAbs + printWidthPx);
            }
            if (hasMeasuredRange)
                return Math.Max(safeMin, measuredHigh);
            return Math.Max(safeMin, hiPrintRevXStart);
        }

        /// <summary>
        /// 固定 FWD 端 Xleft（AbsX 像素），优先于 live AbsX。pass 覆盖≈[Xleft, Xleft+图形宽]；宜缩短 Xleft 使 Xleft+Width 落在单程扫程内。
        /// METEOR_IMAGE_XSTART_ABS_MM / METEOR_IMAGE_XSTART_ABS_PX 仅 env 显式设置时启用（非绝对 0 点 mm）。
        /// </summary>
        private static bool TryGetFixedFwdImageXStartAbsXPixels(int xDpi, out int fixedAbsXPixels)
        {
            fixedAbsXPixels = 0;
            try
            {
                string pxEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_ABS_PX");
                if (!string.IsNullOrWhiteSpace(pxEnv) && int.TryParse(pxEnv.Trim(), out int px) && px > 0)
                {
                    fixedAbsXPixels = px;
                    return true;
                }
                string mmEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_ABS_MM");
                if (!string.IsNullOrWhiteSpace(mmEnv) && double.TryParse(mmEnv.Trim(), out double mm) && mm > 0)
                {
                    fixedAbsXPixels = MmToAbsXPixels(mm, xDpi);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>在 live/anchor 基准上叠加偏移；默认 +5mm（扫程起点再进入 5mm 起喷，非绝对 Xleft）。</summary>
        private static int GetImageXStartOffsetPixels(int xDpi)
        {
            try
            {
                string pxEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_OFFSET_PX");
                if (!string.IsNullOrWhiteSpace(pxEnv) && int.TryParse(pxEnv.Trim(), out int px))
                    return px;
                string mmEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_OFFSET_MM");
                if (!string.IsNullOrWhiteSpace(mmEnv) && double.TryParse(mmEnv.Trim(), out double mm))
                    return (int)Math.Round(mm * (xDpi > 0 ? xDpi : 400) / 25.4, MidpointRounding.AwayFromZero);
            }
            catch { }
            return 0;
        }

        /// <summary>REV 是否叠加 PD 对齐偏移（默认开；METEOR_IMAGE_XSTART_REV_PD_ALIGN=0 可关）。</summary>
        private static bool ShouldApplyRevPdAlignOffset()
        {
            if (IsOfficialQueuedScanModeEnabled() && IsPerLayerHome800ModeEnabled())
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_REV_PD_ALIGN");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return true;
        }

        /// <summary>
        /// HiPrint 式 REV（fwdBase+图宽）与现场 natural PD 的默认偏差（px）。默认 -1968（11420→9452）。
        /// METEOR_IMAGE_XSTART_REV_PD_ALIGN_OFFSET_PX 可覆盖。
        /// </summary>
        private static int GetImageXStartRevPdAlignOffsetPixels(int xDpi)
        {
            if (!ShouldApplyRevPdAlignOffset())
                return 0;
            try
            {
                string pxEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_REV_PD_ALIGN_OFFSET_PX");
                if (!string.IsNullOrWhiteSpace(pxEnv) && int.TryParse(pxEnv.Trim(), out int px))
                    return px;
                string mmEnv = Environment.GetEnvironmentVariable("METEOR_IMAGE_XSTART_REV_PD_ALIGN_OFFSET_MM");
                if (!string.IsNullOrWhiteSpace(mmEnv) && double.TryParse(mmEnv.Trim(), out double mm))
                    return (int)Math.Round(mm * (xDpi > 0 ? xDpi : 400) / 25.4, MidpointRounding.AwayFromZero);
            }
            catch { }
            return -2048;
        }

        /// <summary>
        /// Pass1 REV 现场临时修正（px）。未设时默认 -800，使 pass1 REV 靠近 natural PD 窗口。
        /// METEOR_PASS1_REV_EXTRA_OFFSET_PX / _MM 可覆盖；设 0 可关闭。
        /// </summary>
        private static int GetPass1RevExtraOffsetPixels(int xDpi)
        {
            try
            {
                string pxEnv = Environment.GetEnvironmentVariable("METEOR_PASS1_REV_EXTRA_OFFSET_PX");
                if (!string.IsNullOrWhiteSpace(pxEnv) && int.TryParse(pxEnv.Trim(), out int px))
                    return px;
                string mmEnv = Environment.GetEnvironmentVariable("METEOR_PASS1_REV_EXTRA_OFFSET_MM");
                if (!string.IsNullOrWhiteSpace(mmEnv) && double.TryParse(mmEnv.Trim(), out double mm))
                    return (int)Math.Round(mm * (xDpi > 0 ? xDpi : 400) / 25.4, MidpointRounding.AwayFromZero);
            }
            catch { }
            return -800;
        }

        private static int GetPass2FwdExtraOffsetPixels(int xDpi)
        {
            try
            {
                string pxEnv = Environment.GetEnvironmentVariable("METEOR_PASS2_FWD_EXTRA_OFFSET_PX");
                if (!string.IsNullOrWhiteSpace(pxEnv) && int.TryParse(pxEnv.Trim(), out int px))
                    return px;
                string mmEnv = Environment.GetEnvironmentVariable("METEOR_PASS2_FWD_EXTRA_OFFSET_MM");
                if (!string.IsNullOrWhiteSpace(mmEnv) && double.TryParse(mmEnv.Trim(), out double mm))
                    return (int)Math.Round(mm * (xDpi > 0 ? xDpi : 400) / 25.4, MidpointRounding.AwayFromZero);
            }
            catch { }
            return 0;
        }

        private static int GetPassLiveAbsXCompDeadzonePx()
        {
            const int defaultPx = 30;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS_LIVE_ABSX_COMP_DEADZONE_PX");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int px) && px >= 0 && px <= 500)
                    return px;
            }
            catch { }
            return defaultPx;
        }

        private static int GetPassLiveAbsXCompMaxPx()
        {
            const int defaultPx = 100;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS_LIVE_ABSX_COMP_MAX_PX");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int px) && px >= 0 && px <= 500)
                    return px;
            }
            catch { }
            return defaultPx;
        }

        /// <summary>15mm 换向区 Pass1 开扫锚点与 Pass0 低端 absX 的名义差（金样约 46；非全部计入套准误差）。</summary>
        private static int GetPass1LiveAbsOverlapNominalPx()
        {
            const int defaultPx = 46;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS1_LIVE_ABS_OVERLAP_NOMINAL_PX");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int px) && px >= -500 && px <= 2000)
                    return px;
            }
            catch { }
            return defaultPx;
        }

        private static int ClampPassLiveAbsXCompPx(int compPx)
        {
            int maxPx = GetPassLiveAbsXCompMaxPx();
            if (maxPx <= 0)
                return compPx;
            return Math.Max(-maxPx, Math.Min(maxPx, compPx));
        }

        /// <summary>Pass1/2 开扫 gate 锚点相对 Pass0 的动态 Xleft 补偿（方案 B）。Pass0 不改。</summary>
        private static bool TryComputePassLiveAbsXCompPx(int passIndex, uint startScanDir, out int compPx, out string compMode)
        {
            compPx = 0;
            compMode = "none";
            if (!IsPassIntraLayerLiveAbsXCompEnabled() || passIndex <= 0)
                return false;
            if (IsPassGateAnchorXStartEnabled())
                return false;
            if (!IsBatchSwathModeEnabled() || IsSplitJobPerPassEnabled())
                return false;
            int deadzonePx = GetPassLiveAbsXCompDeadzonePx();
            if (passIndex == 1 && startScanDir == SD_REV)
            {
                if (!_passImageXStartAbsXAnchorValid[1] || !_pass0ScanAbsXLowEndValid)
                    return false;
                int rawPx = _passImageXStartAbsXAnchor[1] - _pass0ScanAbsXAtLowEnd;
                int stitchExcessPx = rawPx - GetPass1LiveAbsOverlapNominalPx();
                stitchExcessPx = Math.Max(-150, Math.Min(150, stitchExcessPx));
                compPx = ClampPassLiveAbsXCompPx(-stitchExcessPx);
                compMode = "pass1Rev_stitchExcess_vs_overlapNominal";
            }
            else if (passIndex == 2 && startScanDir == SD_FWD)
            {
                if (!_pass0ScanAbsXLowEndValid || !_passImageXStartAbsXAnchorValid[0] || !_passImageXStartAbsXAnchorValid[2])
                    return false;
                int pass1TurnRawPx = _passImageXStartAbsXAnchorValid[1]
                    ? _passImageXStartAbsXAnchor[1] - _pass0ScanAbsXAtLowEnd
                    : 0;
                int pass2OpenDriftPx = _passImageXStartAbsXAnchor[2] - _passImageXStartAbsXAnchor[0];
                int predictedLowExcessPx = pass2OpenDriftPx + pass1TurnRawPx - GetPass1LiveAbsOverlapNominalPx();
                predictedLowExcessPx = Math.Max(-150, Math.Min(150, predictedLowExcessPx));
                compPx = ClampPassLiveAbsXCompPx(-predictedLowExcessPx);
                compMode = "pass2Fwd_predicted15mmLowExcess";
            }
            else
            {
                return false;
            }
            if (Math.Abs(compPx) < deadzonePx)
                compPx = 0;
            return compPx != 0;
        }

        /// <summary>
        /// 是否将 PCMD_IMAGE 宽度裁到单程扫程像素（≈6299@400dpi/400mm）。
        /// 默认 false（供应商对齐模式：整板宽 7328 + 统一 Xleft + Ytop=0）；
        /// METEOR_CLAMP_IMAGE_WIDTH_TO_SCAN=1 可开启裁切（最小化数据试验用）。
        /// </summary>
        private static bool ShouldClampImageWidthToScanTravel()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_CLAMP_IMAGE_WIDTH_TO_SCAN");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static int GetImageMaxWidthPixels()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_MAX_WIDTH_PX");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int px) && px > 0)
                    return Math.Max(32, Math.Min(px, 20000));
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// physical_home_fast 的 Pass1 REV 保留已验证的 HDC1 命令窗口右缘，
        /// 同时把较窄的有效内容右移到完整 IMAGE 窗口内以维持图形物理位置。
        /// METEOR_PASS1_REV_HDC_WINDOW_PAD_PX=0 可关闭；物理 Home 快速模式默认 80px。
        /// </summary>
        private static int GetPass1RevHdcWindowPadPixels()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS1_REV_HDC_WINDOW_PAD_PX");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int px))
                    return Math.Max(0, Math.Min(px, 512));
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 正向/反向 PCMD_IMAGE 的 X 起点。对齐/unified 模式已在 imageXStart 中按扫向选好 fwdBase 或 fwdBase+scanTravel；
        /// 仅 legacy（非 live/unified/anchor）且 METEOR_IMAGE_XSTART_REV_DELTA&gt;0 时 REV 才用 imageXReverseStart。
        /// </summary>
        private static int ResolveImageCmdXStart(int imageXStart, int imageXReverseStart, int nPrtDir, bool useLiveAbsX)
        {
            if (nPrtDir == 1 || useLiveAbsX)
                return imageXStart;
            int revDelta = imageXReverseStart - imageXStart;
            return revDelta > 0 ? imageXReverseStart : imageXStart;
        }

        /// <summary>
        /// 扫描 JOB 的 nPrtXEncPos 锚点（µm）。仅在 METEOR_IMAGE_XSTART_FROM_LIVE_ABS=0 时用于 xStart。
        /// 默认 xStart 取 STARTSCAN 前 PCC AbsX（≈XCOUNT，mm=absX*25.4/dpi）。环境变量 METEOR_SCANJOB_START_X_UM 可覆盖。
        /// </summary>
        private static uint GetScanJobStartXEncPosUm(uint originalValueUm)
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_SCANJOB_START_X_UM");
                if (!string.IsNullOrWhiteSpace(env) && uint.TryParse(env.Trim(), out uint value))
                    return value;
            }
            catch { }
            return originalValueUm;
        }

        private static bool TryGetNativePrinterStatus(out NativeAppStatus status)
        {
            status = default;
            try
            {
                IntPtr ptr = NativePiGetPrnStatus();
                if (ptr == IntPtr.Zero)
                    return false;
                status = Marshal.PtrToStructure<NativeAppStatus>(ptr);
                return true;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TryGetNativePrinterStatus exception: {ex.Message}");
                return false;
            }
        }

        private static int GetPassSwathDocsAdmissionMinDocs()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS_SWATH_DOCS_MIN");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int minDocs))
                    return Math.Max(1, Math.Min(minDocs, 64));
            }
            catch { }
            // 每 pass 仅提交一条 swath，不能沿用供应商批量三条 swath 时的 docs>1 条件。
            return 1;
        }

        private static int GetPassSwathDocsAdmissionWaitMs()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS_SWATH_DOCS_WAIT_MS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int timeoutMs))
                    return Math.Max(0, Math.Min(timeoutMs, 10000));
            }
            catch { }
            return 1200;
        }

        private static int GetPassSwathDocsAdmissionPollMs()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PASS_SWATH_DOCS_POLL_MS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int pollMs))
                    return Math.Max(20, Math.Min(pollMs, 200));
            }
            catch { }
            return 200;
        }

        private static bool WaitForDocsQueuedLane1Ready(string stage, int minDocs = 2, int timeoutMs = 0, int pollMs = 20)
        {
            int waited = 0;
            int lastDocs = -1;
            while (waited <= timeoutMs)
            {
                if (TryGetNativePrinterStatus(out NativeAppStatus status))
                {
                    lastDocs = status.DocsQueuedLane1;
                    if (lastDocs >= minDocs)
                    {
                        Log4Net.Info($"[MeteorScanGate] DocsQueuedLane1Ready stage={stage} docs={lastDocs} minDocs={minDocs} waitedMs={waited}");
                        return true;
                    }
                }
                else
                {
                    Log4Net.Info($"[MeteorScanGate] DocsQueuedLane1StatusUnavailable stage={stage} waitedMs={waited}");
                    return false;
                }

                if (waited >= timeoutMs)
                    break;
                Thread.Sleep(pollMs);
                waited += pollMs;
            }
            Log4Net.Info($"[MeteorScanGate] DocsQueuedLane1SoftTimeout stage={stage} docs={lastDocs} minDocs={minDocs} timeoutMs={timeoutMs} note=continue with swath event gate");
            return false;
        }

        private static bool TryGetNativePccStatus(int pccnum, out NativePccStatus status)
        {
            status = default;
            int resolvedPccNum = pccnum > 0 ? pccnum : 1;
            try
            {
                IntPtr ptr = NativePiGetPccStatus((uint)resolvedPccNum);
                if (ptr == IntPtr.Zero)
                    return false;
                status = Marshal.PtrToStructure<NativePccStatus>(ptr);
                return true;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TryGetNativePccStatus exception: pccnum={resolvedPccNum}, {ex.Message}");
                return false;
            }
        }

        private static bool TryIsScanningModeActive(out int controlWord)
        {
            controlWord = 0;
            try
            {
                if (TryGetNativePrinterStatus(out NativeAppStatus nativeStatus))
                {
                    controlWord = nativeStatus.Control;
                    return (controlWord & BM_SCANNING) != 0;
                }
            }
            catch { }

            if (_printerInterfaceInstance == null || _printerInterfaceType == null)
                return false;

            try
            {
                MethodInfo miGetStatus = _printerInterfaceType.GetMethod("PiGetPrnStatus",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                if (miGetStatus == null)
                    return false;

                object target = miGetStatus.IsStatic ? null : _printerInterfaceInstance;
                object result = miGetStatus.Invoke(target, null);
                if (result == null)
                    return false;

                Type statusType = result.GetType();
                FieldInfo fiControl = statusType.GetField("Control", BindingFlags.Public | BindingFlags.Instance);
                if (fiControl == null)
                    return false;
                controlWord = Convert.ToInt32(fiControl.GetValue(result));
                return (controlWord & BM_SCANNING) != 0;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TryIsScanningModeActive exception: {ex.Message}");
                return false;
            }
        }

        private static bool TryGetPccMotionSnapshot(int pccnum, out PccMotionSnapshot snapshot)
        {
            snapshot = default;
            snapshot.PccNum = pccnum > 0 ? pccnum : 1;

            try
            {
                if (TryGetNativePccStatus(snapshot.PccNum, out NativePccStatus nativeStatus))
                {
                    snapshot.Valid = true;
                    snapshot.BmStatusBits = nativeStatus.bmStatusBits;
                    snapshot.BmStatusBits2 = nativeStatus.bmStatusBits2;
                    snapshot.AbsXCount = nativeStatus.AbsXCount;
                    snapshot.EncoderCount = nativeStatus.EncoderCount;
                    return true;
                }
            }
            catch { }

            if (_printerInterfaceInstance == null || _printerInterfaceType == null)
                return false;

            try
            {
                Assembly asm = _printerInterfaceType.Assembly;
                Type typePccStatus = asm.GetTypes().FirstOrDefault(t => t.Name == "TAppPccStatus");
                if (typePccStatus == null)
                    return false;

                MethodInfo miGetStatus = _printerInterfaceType.GetMethod("PiGetPccStatus",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                    null, new Type[] { typeof(int), typePccStatus.MakeByRefType() }, null);
                if (miGetStatus != null)
                {
                    object target = miGetStatus.IsStatic ? null : _printerInterfaceInstance;
                    object pccStatus = Activator.CreateInstance(typePccStatus);
                    object[] args = new object[] { snapshot.PccNum, pccStatus };
                    object result = miGetStatus.Invoke(target, args);
                    int ret = Convert.ToInt32(result);
                    if (ret != RVAL_OK)
                        return false;

                    snapshot.Valid = true;
                    snapshot.BmStatusBits = Convert.ToInt32(typePccStatus.GetField("bmStatusBits", BindingFlags.Public | BindingFlags.Instance)?.GetValue(args[1]) ?? 0);
                    snapshot.BmStatusBits2 = Convert.ToInt32(typePccStatus.GetField("bmStatusBits2", BindingFlags.Public | BindingFlags.Instance)?.GetValue(args[1]) ?? 0);
                    snapshot.AbsXCount = Convert.ToInt32(typePccStatus.GetField("AbsXCount", BindingFlags.Public | BindingFlags.Instance)?.GetValue(args[1]) ?? 0);
                    snapshot.EncoderCount = Convert.ToInt32(typePccStatus.GetField("EncoderCount", BindingFlags.Public | BindingFlags.Instance)?.GetValue(args[1]) ?? 0);
                    return true;
                }

                miGetStatus = _printerInterfaceType.GetMethod("PiGetPccStatus",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                    null, new Type[] { typeof(uint) }, null);
                if (miGetStatus == null)
                    return false;

                object target2 = miGetStatus.IsStatic ? null : _printerInterfaceInstance;
                object statusObj = miGetStatus.Invoke(target2, new object[] { (uint)snapshot.PccNum });
                if (statusObj == null)
                    return false;

                Type statusType2 = statusObj.GetType();
                snapshot.Valid = true;
                snapshot.BmStatusBits = Convert.ToInt32(statusType2.GetField("bmStatusBits", BindingFlags.Public | BindingFlags.Instance)?.GetValue(statusObj) ?? 0);
                snapshot.BmStatusBits2 = Convert.ToInt32(statusType2.GetField("bmStatusBits2", BindingFlags.Public | BindingFlags.Instance)?.GetValue(statusObj) ?? 0);
                snapshot.AbsXCount = Convert.ToInt32(statusType2.GetField("AbsXCount", BindingFlags.Public | BindingFlags.Instance)?.GetValue(statusObj) ?? 0);
                snapshot.EncoderCount = Convert.ToInt32(statusType2.GetField("EncoderCount", BindingFlags.Public | BindingFlags.Instance)?.GetValue(statusObj) ?? 0);
                return true;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TryGetPccMotionSnapshot exception: pccnum={snapshot.PccNum}, {ex.Message}");
                return false;
            }
        }

        public static void LogPccMotionSnapshot(string stage, int pccnum = 1)
        {
            if (TryGetPccMotionSnapshot(pccnum, out PccMotionSnapshot snap) && snap.Valid)
            {
                int absX24 = GetAbsXCount24Signed(snap.AbsXCount);
                Log4Net.Info($"[MeteorPccSnapshot] stage={stage} pcc={snap.PccNum} bmStatusBits=0x{snap.BmStatusBits:X8} bmStatusBits2=0x{snap.BmStatusBits2:X8} absX={snap.AbsXCount} absX24={absX24} encoder={snap.EncoderCount} utc={DateTime.UtcNow:O}");
            }
            else
            {
                Log4Net.Info($"[MeteorPccSnapshot] stage={stage} pcc={pccnum} unavailable utc={DateTime.UtcNow:O}");
            }
        }

        private static int GetSigned24Delta(int baselineAbsXCount, int currentAbsXCount)
        {
            int baseline24 = baselineAbsXCount & 0x00FFFFFF;
            int current24 = currentAbsXCount & 0x00FFFFFF;
            int delta = current24 - baseline24;
            if (delta > 0x007FFFFF)
                delta -= 0x01000000;
            else if (delta < -0x00800000)
                delta += 0x01000000;
            return delta;
        }

        private static bool WaitForInitialScanMotionAfterStartScan(int pccnum, PccMotionSnapshot baseline, string stage, out PccMotionSnapshot observed)
        {
            observed = baseline;
            int timeoutMs = GetScanMotionWaitTimeoutMs();
            int thresholdCounts = GetScanMotionThresholdCounts();
            int consecutiveNeeded = GetScanMotionConsecutivePollsRequired();
            const int pollMs = 25;
            int waitedMs = 0;
            int consecutiveMotionPolls = 0;
            bool scanModeReadable = false;

            while (waitedMs <= timeoutMs)
            {
                bool scanModeActive = TryIsScanningModeActive(out int controlWord);
                if (controlWord != 0 || scanModeActive)
                    scanModeReadable = true;

                if (TryGetPccMotionSnapshot(baseline.PccNum, out observed))
                {
                    int absXDelta = GetSigned24Delta(baseline.AbsXCount, observed.AbsXCount);
                    int encoderDelta = observed.EncoderCount - baseline.EncoderCount;
                    bool motionDetected = Math.Abs(absXDelta) >= thresholdCounts || Math.Abs(encoderDelta) >= thresholdCounts;
                    if (motionDetected)
                        consecutiveMotionPolls++;
                    else
                        consecutiveMotionPolls = 0;

                    if (motionDetected && consecutiveMotionPolls >= consecutiveNeeded && (!scanModeReadable || scanModeActive))
                        return true;
                }

                if (waitedMs >= timeoutMs)
                    break;

                Thread.Sleep(pollMs);
                waitedMs += pollMs;
            }

            Log4Net.Info($"[MeteorScanMotionGate] WaitTimeout stage={stage} pcc={baseline.PccNum} timeoutMs={timeoutMs} baselineAbsX={baseline.AbsXCount} baselineEncoder={baseline.EncoderCount} lastAbsX={observed.AbsXCount} lastEncoder={observed.EncoderCount} utc={DateTime.UtcNow:O}");
            return false;
        }

        /// <summary>
        /// 位图行序相对喷嘴 Y 向上/向下翻转（缺口朝上/朝下修正）。
        /// METEOR_IMAGE_FLIP_Y=1/true/on 启用；=0/false 关闭（默认）。
        /// 若 FLIP_Y=1 后缺口仍不变，试 METEOR_IMAGE_FLIP_X=1 或 METEOR_IMAGE_ROTATE_180=1（勿与 cfg Orientations=1 同开）。
        /// </summary>
        private static bool ShouldFlipImageRowsVertically()
        {
            if (ShouldRotateImage180Degrees())
                return true;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_FLIP_Y");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>扫程方向（X）镜像，METEOR_IMAGE_FLIP_X=1；ROTATE_180=1 时自动启用。</summary>
        private static bool ShouldFlipImageColumnsHorizontally()
        {
            if (ShouldRotateImage180Degrees())
                return true;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_FLIP_X");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return false;
        }

        /// <summary>等价于 FLIP_X + FLIP_Y（整图旋转 180°）。METEOR_IMAGE_ROTATE_180=1。</summary>
        private static bool ShouldRotateImage180Degrees()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_IMAGE_ROTATE_180");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static string DescribeImageOrientationTransformFlags()
        {
            if (ShouldRotateImage180Degrees())
                return "rotate180";
            bool flipY = ShouldFlipImageRowsVertically();
            bool flipX = ShouldFlipImageColumnsHorizontally();
            if (flipY && flipX)
                return "flipX+flipY";
            if (flipY)
                return "flipY";
            if (flipX)
                return "flipX";
            return "none";
        }

        private static int MapSourceImageRowIndex(int targetRow, int imageHeight, bool flipVertically)
        {
            if (!flipVertically || imageHeight <= 1)
                return targetRow;
            return imageHeight - 1 - targetRow;
        }

        private static int MapSourceImageColumnIndex(int targetColumn, int imageWidth, bool flipHorizontally)
        {
            if (!flipHorizontally || imageWidth <= 1)
                return targetColumn;
            return imageWidth - 1 - targetColumn;
        }

        /// <summary>??1bpp 图像??Meteor 命令缓冲??DWORD 行格式重包??/summary>
        private static void PackImageRowsToCommandBuffer(byte[] imageBytes, int imageStrideBytes, int imageWidth, int imageHeight, uint[] commandWords, int dataOffset, int rowWordCount, int targetXOffsetPixels = 0)
        {
            bool flipVertically = ShouldFlipImageRowsVertically();
            bool flipHorizontally = ShouldFlipImageColumnsHorizontally();
            for (int row = 0; row < imageHeight; row++)
            {
                int sourceRow = MapSourceImageRowIndex(row, imageHeight, flipVertically);
                int sourceRowOffset = sourceRow * imageStrideBytes;
                int targetRowOffset = dataOffset + row * rowWordCount;
                Array.Clear(commandWords, targetRowOffset, rowWordCount);

                for (int pixelX = 0; pixelX < imageWidth; pixelX++)
                {
                    int targetPixelX = pixelX + targetXOffsetPixels;
                    if (targetPixelX < 0 || targetPixelX >= rowWordCount * 32)
                        continue;
                    int sourceColumn = MapSourceImageColumnIndex(pixelX, imageWidth, flipHorizontally);
                    int sourceByteIndex = sourceRowOffset + (sourceColumn >> 3);
                    int bitIndex = 7 - (sourceColumn & 7);
                    uint bitValue = (uint)((imageBytes[sourceByteIndex] >> bitIndex) & 0x01);
                    if (bitValue != 0)
                        commandWords[targetRowOffset + (targetPixelX >> 5)] |= 0x80000000u >> (targetPixelX & 31);
                }
            }
        }

        /// <summary>??uint[] 拷贝到非托管内存，调??PiSendCommand；若返回 RVAL_FULL 则重试（参??SampleScanPrint main.cpp）??/summary>
        private static int DoSendCommand(uint[] cmd)
        {
            if (cmd == null || cmd.Length == 0) return -1;
            int len = cmd.Length * 4;
            IntPtr pCmd = Marshal.AllocHGlobal(len);
            try
            {
                int r;
                do
                {
                    for (int i = 0; i < cmd.Length; i++)
                        Marshal.WriteInt32(pCmd, i * 4, (int)cmd[i]);
                    r = NativePiSendCommand(pCmd);
                } while (r == RVAL_FULL);
                return r;
            }
            finally
            {
                Marshal.FreeHGlobal(pCmd);
            }
        }

        private static int GetMeteorBidiXAdjustSigned(int xDpi)
        {
            try
            {
                double reverseOffsetMm = 主界面.g_RYSYSParam != null ? 主界面.g_RYSYSParam.m_dXJetOff : 0.0;
                double bidiXAdjustValue = (reverseOffsetMm / 25.4) * xDpi * 100.0;
                return (int)Math.Round(bidiXAdjustValue, MidpointRounding.AwayFromZero);
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor GetMeteorBidiXAdjustSigned: exception, fallback to 0. {ex.Message}");
                return 0;
            }
        }

        private static bool TrySetMeteorBidiXAdjust(int xDpi)
        {
            int bidiXAdjustSigned = GetMeteorBidiXAdjustSigned(xDpi);
            uint bidiXAdjustUnsigned = unchecked((uint)bidiXAdjustSigned);
            int ret = NativePiSetParam(CCP_BIDI_XADJUST, bidiXAdjustUnsigned);
            Log4Net.Info($"Meteor TrySetMeteorBidiXAdjust: xDpi={xDpi} reverseOffsetMm={(主界面.g_RYSYSParam != null ? 主界面.g_RYSYSParam.m_dXJetOff.ToString("F3") : "n/a")} signed={bidiXAdjustSigned} unsigned=0x{bidiXAdjustUnsigned:X8} ret={ret}");
            return ret == RVAL_OK;
        }

        /// <summary>设置扫描作业的预估宽度，供旧 StartJob 入口使用??/summary>
        public static void SetPendingScanJobWidth(uint imageWidth)
        {
            lock (SyncRoot)
            {
                _pendingScanJobWidth = imageWidth > 0 ? imageWidth : 1;
            }
        }

        private static int GetAbsXCount24Signed(int absXCount)
        {
            int v = absXCount & 0x00FFFFFF;
            if (v > 0x007FFFFF)
                v -= 0x01000000;
            return v;
        }

        private static bool WaitForPccAbsXHomeStable(int pccnum = 1, int zeroToleranceCounts = 8, int stableSamples = 3, int pollMs = 50, int timeoutMs = 3000)
        {
            int waitedMs = 0;
            int stableCount = 0;
            int lastAbsX24 = int.MinValue;
            while (waitedMs <= timeoutMs)
            {
                if (TryGetPccMotionSnapshot(pccnum, out PccMotionSnapshot snapshot) && snapshot.Valid)
                {
                    int absX24 = GetAbsXCount24Signed(snapshot.AbsXCount);
                    lastAbsX24 = absX24;
                    if (Math.Abs(absX24) <= zeroToleranceCounts)
                    {
                        stableCount++;
                        if (stableCount >= stableSamples)
                        {
                            Log4Net.Info($"Meteor WaitForPccAbsXHomeStable: stable pcc={pccnum} absX24={absX24} tolerance={zeroToleranceCounts} stableSamples={stableSamples} waitedMs={waitedMs} encoder={snapshot.EncoderCount}");
                            return true;
                        }
                    }
                    else
                    {
                        stableCount = 0;
                    }
                }
                else
                {
                    stableCount = 0;
                }

                if (waitedMs >= timeoutMs)
                    break;
                Thread.Sleep(pollMs);
                waitedMs += pollMs;
            }

            Log4Net.Info($"Meteor WaitForPccAbsXHomeStable: timeout pcc={pccnum} lastAbsX24={lastAbsX24} tolerance={zeroToleranceCounts} stableCount={stableCount}/{stableSamples} timeoutMs={timeoutMs}");
            return false;
        }

        /// <summary>层末（第 3 PASS 收口回清洗站后）默认不自动开闪喷；仍回清洗站避让。设 METEOR_ENABLE_END_AUTO_FLASH=1 可恢复旧闪喷。</summary>
        public static bool ShouldSkipLayerEndAutoFlash()
        {
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_ENABLE_END_AUTO_FLASH");
                if (string.IsNullOrWhiteSpace(env))
                    env = Environment.GetEnvironmentVariable("METEOR_ENABLE_END_CLEAN_FLASH");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { }
            return true;
        }

        /// <summary>扫程结束后对比固高 X 与 PCC AbsX，用于确认 Meteor 编码器是否在跟轴。</summary>
        public static void LogScanPassMotionCheck(string stage, double googolXmm)
        {
            if (!TryGetPccMotionSnapshot(1, out PccMotionSnapshot snap) || !snap.Valid)
            {
                Log4Net.Info($"[MeteorEncoderDiag] stage={stage} googolXmm={googolXmm:F3} pcc=unavailable utc={DateTime.UtcNow:O}");
                return;
            }
            int absX24 = GetAbsXCount24Signed(snap.AbsXCount);
            RecordPassScanEndAbsXFromStage(stage, absX24);
        }

        /// <summary>PiSetHome：在 Home 传感器处将 Master PCC 绝对 X 对齐到 cfg/SIG_SET_HOME_OFFSET_PX（说明书要求滑架静止在 Home/暂停位）。</summary>
        public static bool TryAlignAbsXAtHome(string stage)
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened())
            {
                Log4Net.Info($"Meteor TryAlignAbsXAtHome({stage}): runtime or printer not ready");
                return false;
            }
            int absXBefore = int.MinValue;
            int absX24Before = 0;
            if (TryGetPccMotionSnapshot(1, out PccMotionSnapshot snapBefore) && snapBefore.Valid)
            {
                absXBefore = snapBefore.AbsXCount;
                absX24Before = GetAbsXCount24Signed(absXBefore);
            }
            bool forceHome = false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_FORCE_PISET_HOME_AT_PAUSE");
                forceHome = string.Equals(env?.Trim(), "1", StringComparison.Ordinal)
                    || string.Equals(env?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            const int maxAbsX24AtHome = 20000;
            if (!forceHome && absXBefore != int.MinValue && Math.Abs(absX24Before) > maxAbsX24AtHome)
            {
                Log4Net.Info($"Meteor TryAlignAbsXAtHome({stage}): skip PiSetHome absX24={absX24Before} (|.|>{maxAbsX24AtHome}, not at home sensor); use scanAnchorUm=0");
                LogPccStatus($"{stage}-SkipPiSetHome");
                return false;
            }
            LogPccStatus($"{stage}-BeforePiSetHome");
            bool ok = TrySetHome();
            if (TryGetPccMotionSnapshot(1, out PccMotionSnapshot snapAfter) && snapAfter.Valid && absXBefore != int.MinValue)
            {
                int absX24After = GetAbsXCount24Signed(snapAfter.AbsXCount);
                if (Math.Abs(absX24After - absX24Before) <= 32)
                    Log4Net.Info($"Meteor TryAlignAbsXAtHome({stage}): PiSetHome done but absX24 unchanged before={absX24Before} after={absX24After} (likely not on home sensor)");
            }
            LogPccStatus($"{stage}-AfterPiSetHome");
            return ok;
        }

        /// <summary>机械回零成功后建立一次 Meteor 固定 X 原点；逐层 STARTJOB 不再重复 PiSetHome。</summary>
        public static bool SetFixedHomeAfterMechanicalHome(string stage)
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened())
            {
                Log4Net.Info($"Meteor SetFixedHomeAfterMechanicalHome({stage}): runtime or printer not ready");
                return false;
            }
            LogPccStatus($"{stage}-BeforeFixedPiSetHome");
            if (!TrySetHome())
            {
                Log4Net.Info($"Meteor SetFixedHomeAfterMechanicalHome({stage}): PiSetHome failed");
                return false;
            }
            if (!WaitForPccAbsXHomeStable())
            {
                Log4Net.Info($"Meteor SetFixedHomeAfterMechanicalHome({stage}): PCC AbsX did not settle near zero");
                return false;
            }
            LogPccStatus($"{stage}-AfterFixedPiSetHomeStable");
            Log4Net.Info($"Meteor SetFixedHomeAfterMechanicalHome({stage}): fixed Home established; per-layer PiSetHome disabled in official queued scan mode");
            return true;
        }

        /// <summary>PiSetHome：在 Home 传感器处将 Master PCC 绝对 X 对齐到 cfg/SIG_SET_HOME_OFFSET_PX（说明书要求滑架静止在 Home 位）。</summary>
        private static bool TrySetHome()
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened())
                return false;

            if (_useNativePath)
            {
                int ret = NativePiSetHome();
                if (ret == RVAL_OK)
                {
                    Log4Net.Info("Meteor PiSetHome => OK (原生)");
                    return true;
                }

                for (int i = 0; i < 5 && ret == RVAL_BUSY; i++)
                {
                    System.Threading.Thread.Sleep(200);
                    ret = NativePiSetHome();
                    if (ret == RVAL_OK)
                    {
                        Log4Net.Info($"Meteor PiSetHome 原生 重试{i + 1} => OK");
                        return true;
                    }
                }

                Log4Net.Info($"Meteor PiSetHome 原生 返回 {ret}");
                return false;
            }

            try
            {
                MethodInfo mi = _printerInterfaceType.GetMethod("PiSetHome", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
                    ?? _printerInterfaceType.GetMethod("PiSetHome", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (mi == null)
                {
                    Log4Net.Info("Meteor PiSetHome: reflection path missing PiSetHome method");
                    return false;
                }

                object result = mi.Invoke(_printerInterfaceInstance, null);
                if (result is bool boolResult)
                {
                    if (boolResult)
                    {
                        Log4Net.Info("Meteor PiSetHome => OK");
                        return true;
                    }
                    Log4Net.Info("Meteor PiSetHome 返回 false");
                    return false;
                }

                int ret = result == null ? RVAL_OK : Convert.ToInt32(result);
                if (ret == RVAL_OK)
                {
                    Log4Net.Info("Meteor PiSetHome => OK");
                    return true;
                }

                if (ret == RVAL_BUSY)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        System.Threading.Thread.Sleep(200);
                        result = mi.Invoke(_printerInterfaceInstance, null);
                        if (result is bool retryBool)
                        {
                            if (retryBool)
                            {
                                Log4Net.Info($"Meteor PiSetHome => OK (重试 {i + 1} ??");
                                return true;
                            }
                        }
                        else
                        {
                            ret = result == null ? RVAL_OK : Convert.ToInt32(result);
                            if (ret == RVAL_OK)
                            {
                                Log4Net.Info($"Meteor PiSetHome => OK (重试 {i + 1} ??");
                                return true;
                            }
                        }
                    }
                }

                Log4Net.Info($"Meteor PiSetHome 返回??OK: {ret}");
                return false;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TrySetHome exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>打印 PCC 关键状态，用于排查 home / absolute X / 编码器计数是否同步??/summary>
        private static void LogPccStatus(string stage, int pccnum = 0)
        {
            int resolvedPccNum = pccnum > 0 ? pccnum : 1;

            if (TryGetPccMotionSnapshot(resolvedPccNum, out PccMotionSnapshot nativeSnapshot) && nativeSnapshot.Valid)
            {
                uint absXCount24 = (uint)nativeSnapshot.AbsXCount & 0x00FFFFFF;
                Log4Net.Info($"Meteor {stage}: PCC={resolvedPccNum} bmStatusBits=0x{nativeSnapshot.BmStatusBits:X8} bmStatusBits2=0x{nativeSnapshot.BmStatusBits2:X8} AbsXCount={nativeSnapshot.AbsXCount} AbsXCount24=0x{absXCount24:X6} EncoderCount={nativeSnapshot.EncoderCount}");
                return;
            }

            if (_printerInterfaceInstance == null || _printerInterfaceType == null)
                return;

            try
            {
                Assembly asm = _printerInterfaceType.Assembly;
                Type typePccStatus = asm.GetTypes().FirstOrDefault(t => t.Name == "TAppPccStatus");
                if (typePccStatus == null)
                    return;

                MethodInfo miGetStatus = _printerInterfaceType.GetMethod("PiGetPccStatus",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                    null, new Type[] { typeof(int), typePccStatus.MakeByRefType() }, null);
                if (miGetStatus == null)
                    return;

                object target = miGetStatus.IsStatic ? null : _printerInterfaceInstance;
                object pccStatus = Activator.CreateInstance(typePccStatus);
                object[] args = new object[] { resolvedPccNum, pccStatus };
                object result = miGetStatus.Invoke(target, args);
                int ret = Convert.ToInt32(result);
                if (ret != RVAL_OK)
                {
                    Log4Net.Info($"Meteor {stage}: PiGetPccStatus({resolvedPccNum}) 返回 {ret}");
                    return;
                }

                FieldInfo fiBmStatus = typePccStatus.GetField("bmStatusBits", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo fiBmStatus2 = typePccStatus.GetField("bmStatusBits2", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo fiAbsXCount = typePccStatus.GetField("AbsXCount", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo fiEncoderCount = typePccStatus.GetField("EncoderCount", BindingFlags.Public | BindingFlags.Instance);
                int bmStatusBits = fiBmStatus != null ? Convert.ToInt32(fiBmStatus.GetValue(args[1])) : 0;
                int bmStatusBits2 = fiBmStatus2 != null ? Convert.ToInt32(fiBmStatus2.GetValue(args[1])) : 0;
                int absXCount = fiAbsXCount != null ? Convert.ToInt32(fiAbsXCount.GetValue(args[1])) : int.MinValue;
                int encoderCount = fiEncoderCount != null ? Convert.ToInt32(fiEncoderCount.GetValue(args[1])) : int.MinValue;
                uint absXCount24 = (uint)absXCount & 0x00FFFFFF;
                Log4Net.Info($"Meteor {stage}: PCC={resolvedPccNum} bmStatusBits=0x{bmStatusBits:X8} bmStatusBits2=0x{bmStatusBits2:X8} AbsXCount={absXCount} AbsXCount24=0x{absXCount24:X6} EncoderCount={encoderCount}");
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor {stage}: LogPccStatus exception: {ex.Message}");
            }
        }

        // MeteorSwathConnect/MeteorSwathDisconnect 在扫描引擎独??DLL 中，不在 PrinterInterface.dll；喷头上??闪喷仅用 PiSetHeadPower/PiSetSignal 不受影响??

        /// <summary>喷头上电状态：供外部按??界面绑定。设??true 时执??PCC 空闲检查后调用 PiSetHeadPower(1)，设??false 时调??PiSetHeadPower(0)??/summary>
        private static bool _headPowerOn;
        public static bool HeadPowerOn
        {
            get => _headPowerOn;
            set
            {
                if (_headPowerOn == value) return;
                lock (SyncRoot)
                {
                    if (!EnsureRuntimeReady() || !EnsurePrinterOpened())
                    {
                        Log4Net.Info("Meteor HeadPowerOn: runtime or printer not ready; skip head power on");
                        return;
                    }
                    if (value)
                    {
                        if (!TryEnsurePccIdleBeforeHeadPower(1))
                        {
                            Log4Net.Info("Meteor HeadPowerOn: PCC1 not ready or disconnected; skip head power on");
                            _headPowerOn = false;
                            return;
                        }
                        _headPowerOn = TrySetHeadPower(true);
                    }
                    else
                    {
                        _headPowerOn = false;
                        TrySetHeadPower(false);
                    }
                }
            }
        }

        /// <summary>
        /// 记录作业参数并复??Meteor 扫描状态标志；不下??PiSetHome/STARTJOB??
        /// PiSetHome 在 <see cref="SendStartJob"/> 发 STARTJOB 之前执行（暂停位=Home 时对齐 AbsX）。
        /// </summary>
        public static int StartJob(ref royal.PRTJOB_ITEM jobItem)
        {
            if (!EnsureRuntimeReady())
                return MeteorRuntimeNotReadyCode;
            if (!EnsurePrinterOpened())
                return MeteorApiInvokeFailedCode;
            if (!_useNativePath)
            {
                Log4Net.Info("Meteor StartJob: only supported on native PrinterInterface path");
                return 0;
            }
            lock (SyncRoot)
            {
                uint jobId = jobItem.nJobID > 0 ? (uint)jobItem.nJobID : 100u;
                uint imageWidth = _pendingScanJobWidth > 0 ? _pendingScanJobWidth : 1;
                _scanJobStarted = false;
                _homeCommandIssued = false;
                // 勿清 _pass0SubmitGateConsumedForCurrentJob：RenderToWic 已 WaitReleased 后紧接 StartJob+SendStartJob 时，
                // WriteImageLayer:FirstStartScan 需 SkipDuplicateWait，否则会二次 Wait 已 Reset 的门控导致死锁、Meteor 无 swath。
                ResetPass0FirstSwathMeteorReady();
                ResetDeferredLegacyBatchSwaths();
                ResetSwathPayloadDiagnostics();
                bool preserveLayerAnchors = ShouldPreserveLayerAnchorsForSplitPass();
                if (!preserveLayerAnchors)
                {
                    ResetPass0DualCoordScanAnchors();
                    ResetPassImageXStartAnchors();
                }
                else
                {
                    Log4Net.Info($"Meteor StartJob: preserve layer anchors for split pass pendingPass={_pendingSwathPassIndex}");
                }
                for (int i = 1; i < _passSwathMeteorReady.Length; i++)
                    ResetPassSwathMeteorReady(i);
                _scanJobStartXEncPosUm = GetScanJobStartXEncPosUm(jobItem.nPrtXEncPos);
                Log4Net.Info($"Meteor StartJob: context reset JobId={jobId} pendingWidth={imageWidth} jobNprtXEncPos={jobItem.nPrtXEncPos} scanAnchorUm={_scanJobStartXEncPosUm} (0=near-zero xStart for 425→25 scan); officialQueuedScanMode={IsOfficialQueuedScanModeEnabled()} piSetHomeAtSendStartJob={IsPiSetHomeAtJobStartEnabled()}");
                return 0;
            }
        }

        /// <summary>
        /// Swath 扫描模式开始作业：暂停位/Home 处 PiSetHome（可选）→ PCMD_STARTJOB（与 HiPrint、Meteor 说明书一致）。
        /// 不在首条 STARTSCAN 之后再 Home，避免首 PD 后 AbsX 被二次清零。
        /// </summary>
        public static bool SendStartJob(int reserved, uint imageWidth)
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened() || !_useNativePath)
            {
                Log4Net.Info("Meteor SendStartJob: runtime/printer not ready or not native path; skipped");
                SignalPass0PreheatStartJobDone(false, "SendStartJob:notReady");
                return false;
            }
            if (!WaitForDeferredEndJobCleared())
            {
                Log4Net.Info("Meteor SendStartJob: previous layer deferred EndJob not completed; abort STARTJOB");
                SignalPass0PreheatStartJobDone(false, "SendStartJob:DeferredEndJobPending");
                return false;
            }
            lock (SyncRoot)
            {
                if (_scanJobStarted)
                {
                    Log4Net.Info($"Meteor SendStartJob: 已经启动过作业，忽略重复 StartJob。imageWidth={imageWidth}");
                    SignalPass0PreheatStartJobDone(true, "SendStartJob:alreadyStarted");
                    return true;
                }
                uint jobId = 100u; // ??StartJob 默认保持一??
                uint effectiveWidth = imageWidth > 0 ? imageWidth : 1;

                if (IsPiSetHomeAtJobStartEnabled())
                {
                    if (TryGetPccBmStatusBits(out int bmPreHome) && !IsPccBmStatusReadyForStartJob(bmPreHome))
                    {
                        Log4Net.Info($"Meteor SendStartJob: PCC job slot busy bmStatusBits=0x{bmPreHome:X8} before PiSetHome; EndJob recovery at home");
                        TryForcePccIdleForNextStartJobUnlocked("SendStartJob:PrePiSetHome", GetPccIdleWaitTimeoutMs(), allowPiSetHome: true);
                    }
                    Log4Net.Info($"Meteor SendStartJob: PiSetHome at per-layer Home point {GetPerLayerMeteorHomeMm():F3}mm before STARTJOB utc={DateTime.UtcNow:O}");
                    int beforePccEncoder = 0;
                    int beforeAbsX24 = 0;
                    double beforeGoogolPulse = double.NaN;
                    if (TryGetPccMotionSnapshot(1, out PccMotionSnapshot snapBeforePiSetHome) && snapBeforePiSetHome.Valid)
                    {
                        beforePccEncoder = snapBeforePiSetHome.EncoderCount;
                        beforeAbsX24 = GetAbsXCount24Signed(snapBeforePiSetHome.AbsXCount);
                    }
                    if (TryReadInkCarGoogolRaster(out InkCarGoogolRasterSample gBeforePiSetHome))
                        beforeGoogolPulse = gBeforePiSetHome.GoogolEncPulse;
                    LogRasterEncoderSnapshot("SendStartJob-BeforePiSetHome", "PiSetHome");
                    LogPccStatus("SendStartJob-BeforePiSetHome");
                    if (!TrySetHome())
                    {
                        Log4Net.Info("Meteor SendStartJob: PiSetHome failed at pause/home position; abort STARTJOB (carriage must be stationary on home sensor)");
                        SignalPass0PreheatStartJobDone(false, "SendStartJob:PiSetHomeFailed");
                        return false;
                    }
                    LogRasterEncoderSnapshot("SendStartJob-AfterPiSetHome", "PiSetHome",
                        beforePccEncoder: beforePccEncoder,
                        beforeAbsX24: beforeAbsX24,
                        beforeGoogolPulse: double.IsNaN(beforeGoogolPulse) ? (double?)null : beforeGoogolPulse);
                    LogPccStatus("SendStartJob-AfterPiSetHome");
                    if (!WaitForPccAbsXHomeStable())
                    {
                        Log4Net.Info("Meteor SendStartJob: PCC AbsX did not settle near zero after PiSetHome; abort STARTJOB");
                        SignalPass0PreheatStartJobDone(false, "SendStartJob:PccAbsXHomeTimeout");
                        return false;
                    }
                    LogPccStatus("SendStartJob-AfterPiSetHomeStable");
                    if (TryGetPccBmStatusBits(out int bmAfterHome) && !IsPccBmStatusReadyForStartJob(bmAfterHome))
                    {
                        Log4Net.Info($"Meteor SendStartJob: PCC still busy bmStatusBits=0x{bmAfterHome:X8} after PiSetHome stable; force idle before STARTJOB");
                        if (!TryForcePccIdleForNextStartJobUnlocked("SendStartJob:PostPiSetHomeStable", GetPccIdleWaitTimeoutMs(), allowPiSetHome: true))
                        {
                            Log4Net.Info("Meteor SendStartJob: PCC job slot did not release after recovery; abort STARTJOB");
                            SignalPass0PreheatStartJobDone(false, "SendStartJob:PccJobSlotBusy");
                            return false;
                        }
                        LogPccStatus("SendStartJob-AfterForceIdle");
                    }
                }
                else
                {
                    Log4Net.Info(IsOfficialQueuedScanModeEnabled()
                        ? "Meteor SendStartJob: per-layer PiSetHome skipped (official queued scan mode with METEOR_PER_LAYER_HOME_MODE=0)"
                        : "Meteor SendStartJob: PiSetHome skipped (METEOR_PISET_HOME_AT_JOB_START=0/false/off)");
                }

                if (!TrySetMeteorBidiXAdjust(400))
                {
                    Log4Net.Info("Meteor SendStartJob: CCP_BIDI_XADJUST set failed; continue STARTJOB for diagnostics.");
                }

                Log4Net.Info($"[MeteorSubmit] marker=BeforePCMD_STARTJOB JobId={jobId} imageWidth={effectiveWidth} note=JT_SCAN docWidth ignored by Meteor unless PD lockout managedThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId} utc={System.DateTime.UtcNow:O}");
                ResetPass0FirstSwathMeteorReady();
                bool preserveLayerAnchors = ShouldPreserveLayerAnchorsForSplitPass();
                if (!preserveLayerAnchors)
                {
                    ResetPass0DualCoordScanAnchors();
                    ResetPassImageXStartAnchors();
                }
                else
                {
                    Log4Net.Info($"Meteor SendStartJob: preserve layer anchors for split pass pendingPass={_pendingSwathPassIndex}");
                }
                for (int i = 1; i < _passSwathMeteorReady.Length; i++)
                    ResetPassSwathMeteorReady(i);
                ResetDeferredEndJobState();
                uint[] cmd = { PCMD_STARTJOB, 4, jobId, JT_SCAN, RES_HIGH, effectiveWidth };
                int retryCount = GetStartJobBusyRetryCount();
                int retryDelayMs = GetStartJobBusyRetryDelayMs();
                int r = RVAL_BUSY;
                int attemptsUsed = 0;
                bool recoveryEndJobTried = false;
                bool splitPassPccReadyAfterNaturalWait = false;
                for (int attempt = 0; attempt <= retryCount; attempt++)
                {
                    attemptsUsed = attempt + 1;
                    r = DoSendCommand(cmd);
                    if (r == RVAL_OK)
                        break;
                    if (r != RVAL_BUSY || attempt >= retryCount)
                        break;
                    if (!recoveryEndJobTried)
                    {
                        recoveryEndJobTried = true;
                        if (preserveLayerAnchors)
                        {
                            Log4Net.Info($"Meteor SendStartJob: split pass busy; natural wait without PiAbort/EndJob to preserve PCC AbsX and avoid duplicate EndJob pendingPass={_pendingSwathPassIndex}");
                            splitPassPccReadyAfterNaturalWait = WaitForPccReadyForNewStartJob(
                                Math.Min(GetPccIdleWaitTimeoutMs(), 5000),
                                "SendStartJob:SplitPassBusy:NaturalPoll");
                        }
                        else
                            TryRecoverPccForStartJobUnlocked("SendStartJob");
                    }
                    int effectiveRetryDelayMs = splitPassPccReadyAfterNaturalWait ? 0 : retryDelayMs;
                    Log4Net.Info($"Meteor SendStartJob: PCMD_STARTJOB busy(r=14), retry {attempt + 1}/{retryCount} after {effectiveRetryDelayMs}ms; JobId={jobId} imageWidth={effectiveWidth}; splitPassPccReady={splitPassPccReadyAfterNaturalWait}");
                    if (effectiveRetryDelayMs > 0)
                        System.Threading.Thread.Sleep(effectiveRetryDelayMs);
                }
                int scanTravelPxHint = GetScanTravelWidthPixels(400);
                Log4Net.Info($"Meteor SendStartJob: PCMD_STARTJOB JobId={jobId} imageWidth={effectiveWidth} r={r} attempts={attemptsUsed} scanTravelHintMm={GetScanTravelMm()} scanTravelHintPx={scanTravelPxHint} (actual pass travel=encoder during STARTSCAN, not STARTJOB width)");
                LogPccStatus("SendStartJob-AfterStartJobCmd");
                if (r != RVAL_OK)
                {
                    SignalPass0PreheatStartJobDone(false, "SendStartJob:StartJobFailed");
                    return false;
                }

                _scanJobStarted = true;
                _pendingScanJobWidth = effectiveWidth;
                _startJobUtc = DateTime.UtcNow;
                SignalPass0PreheatStartJobDone(true, "SendStartJob:StartJobOk");
                return true;
            }
        }

        /// <summary>自最近一??STARTJOB 起至少等??minMs 再启动打印扫描，避免 X 轴在 CLEAR_HALT 前运动导??PASS0 无图??/summary>
        public static void WaitUntilMinElapsedAfterStartJob(int minMs = 0)
        {
            if (minMs <= 0)
                minMs = GetMinMsAfterStartJobForPiSetHome();
            lock (SyncRoot)
            {
                if (!_startJobUtc.HasValue || minMs <= 0)
                    return;
                double elapsedMs = (DateTime.UtcNow - _startJobUtc.Value).TotalMilliseconds;
                int sleepMs = (int)Math.Max(0, minMs - elapsedMs);
                if (sleepMs > 0)
                {
                    Log4Net.Info($"Meteor WaitUntilMinElapsedAfterStartJob: elapsed {elapsedMs:F0}ms after STARTJOB, sleep {sleepMs}ms to reach >= {minMs}ms before scan start");
                    System.Threading.Thread.Sleep(sleepMs);
                }
            }
        }

        /// <summary>兼容旧接口：Swath 每条带开始（STARTSCAN），forward=true 表示正向??/summary>
        public static bool SendStartScan(bool forward)
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened() || !_useNativePath)
            {
                Log4Net.Info("Meteor SendStartScan: runtime/printer not ready or not native path; skipped");
                return false;
            }
            lock (SyncRoot)
            {
                uint[] cmd = { PCMD_STARTSCAN, 1, forward ? SD_FWD : SD_REV };
                int r = DoSendCommand(cmd);
                // 仅在失败时打更明显日志，避免刷屏
                if (r != RVAL_OK)
                    Log4Net.Info($"Meteor SendStartScan: r={r} forward={forward}");
                return r == RVAL_OK;
            }
        }

        /// <summary>兼容旧接口：Swath 每条带结束（ENDDOC）??/summary>
        public static bool SendEndDoc()
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened() || !_useNativePath)
            {
                Log4Net.Info("Meteor SendEndDoc: runtime/printer not ready or not native path; skipped");
                return false;
            }
            lock (SyncRoot)
            {
                uint[] endDoc = { PCMD_ENDDOC, 0 };
                int r = DoSendCommand(endDoc);
                if (r != RVAL_OK)
                    Log4Net.Info($"Meteor SendEndDoc: r={r}");
                return r == RVAL_OK;
            }
        }

        /// <summary>兼容旧接口：Swath 整层/整作业结束（ENDJOB）??/summary>
        public static bool SendEndJob()
        {
            int r = EndJob();
            return r == 0;
        }

        public static bool SendEndJobPreserveLayerGates(string stage)
        {
            int r = EndJob(false);
            Log4Net.Info($"[MeteorJob] EndJobPreserveLayerGates stage={stage} ret={r} note=split-job-per-pass keeps Pass1/Pass2 gates alive utc={DateTime.UtcNow:O}");
            return r == 0;
        }

        /// <summary>在所有图层发送完毕后调用，发??PCMD_ENDJOB 结束作业??/summary>
        public static int EndJob()
        {
            return EndJob(true);
        }

        private static int EndJob(bool resetLayerGatesAfterEndJob)
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened())
                return MeteorApiInvokeFailedCode;
            if (!_useNativePath)
            {
                Log4Net.Info("Meteor EndJob: only supported on native PrinterInterface path");
                return -1;
            }
            int outcome;
            lock (SyncRoot)
            {
                uint[] endJob = { PCMD_ENDJOB, 0 };
                int r = DoSendCommand(endJob);
                if (r == RVAL_OK)
                {
                    Log4Net.Info("Meteor EndJob: PCMD_ENDJOB sent");
                    _scanJobStarted = false;
                    _pendingScanJobWidth = 1;
                    _homeCommandIssued = false;
                    _startJobUtc = null;
                    _deferEndJobPending = false;
                    LogPccStatus("EndJob-AfterCommand");
                }
                outcome = r == RVAL_OK ? 0 : r;
            }
            if (outcome == 0 && resetLayerGatesAfterEndJob)
                ResetPass0GateAfterMeteorJobEnd();
            else if (outcome == 0)
                Log4Net.Info($"[MeteorScanGate] gate reset skipped after EndJob because resetLayerGatesAfterEndJob=False utc={DateTime.UtcNow:O}");
            return outcome;
        }

        public static int WriteImageLayer(ref royal.LPPRTIMG_LAYER layer, IntPtr imgPtr, int bytes)
        {
            if (!EnsureRuntimeReady())
                return MeteorRuntimeNotReadyCode;
            if (!EnsurePrinterOpened())
                return MeteorApiInvokeFailedCode;
            if (!_useNativePath)
            {
                Log4Net.Info("Meteor WriteImageLayer: only supported on native PrinterInterface path");
                return 1;
            }
            if (imgPtr == IntPtr.Zero || bytes <= 0 || layer.nWidth <= 0 || layer.nHeight <= 0)
                return -110001;

            Log4Net.Info($"Meteor WriteImageLayer: enter layer={layer.nLayerIndex} width={layer.nWidth} height={layer.nHeight} bytes={bytes} nBytesPerLine={layer.nBytesPerLine} nPrtDir={layer.nPrtDir} imgPtr=0x{imgPtr.ToInt64():X} threadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            lock (SyncRoot)
            {
                Log4Net.Info($"Meteor WriteImageLayer: lock acquired layer={layer.nLayerIndex} width={layer.nWidth} height={layer.nHeight} bytes={bytes} nBytesPerLine={layer.nBytesPerLine} threadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                bool needFirstHomeAfterStartScan = _scanJobStarted && !_homeCommandIssued;
                PccMotionSnapshot initialMotionBaseline = default;
                bool hasInitialMotionBaseline = false;
                if (needFirstHomeAfterStartScan)
                {
                    if (_pass0SubmitGateConsumedForCurrentJob)
                        Log4Net.Info($"[MeteorScanGate] SkipDuplicateWait stage=WriteImageLayer:FirstStartScan layer={layer.nLayerIndex} reason=pass0 gate already released before STARTJOB");
                    else
                        WaitPass0GateBeforeMeteorSubmitIfEnabled("WriteImageLayer:FirstStartScan", layer.nLayerIndex, layer.nLayerIndex);
                    int minGapMs = GetMinMsAfterStartJobForPiSetHome();
                    if (_startJobUtc.HasValue && minGapMs > 0)
                    {
                        bool skipFirstStartScanDelayForHiPrintCompat = IsHiPrintChainCompatModeEnabled() && ShouldSkipStartJobDelayForHiPrintCompat();
                        if (skipFirstStartScanDelayForHiPrintCompat)
                        {
                            double elapsedMs = (DateTime.UtcNow - _startJobUtc.Value).TotalMilliseconds;
                            Log4Net.Info($"Meteor WriteImageLayer: HiPrintCompat skip first STARTSCAN delay elapsed={elapsedMs:F0}ms configuredMinGapMs={minGapMs} note=METEOR_HIPRINT_SKIP_STARTJOB_DELAY=1");
                        }
                        else
                        {
                            double elapsedMs = (DateTime.UtcNow - _startJobUtc.Value).TotalMilliseconds;
                            int sleepMs = (int)Math.Max(0, minGapMs - elapsedMs);
                            if (sleepMs > 0)
                            {
                                Log4Net.Info($"Meteor WriteImageLayer: elapsed {elapsedMs:F0}ms after STARTJOB, need >= {minGapMs}ms before first STARTSCAN; extra sleep {sleepMs}ms");
                                System.Threading.Thread.Sleep(sleepMs);
                            }
                        }
                    }

                    hasInitialMotionBaseline = TryGetPccMotionSnapshot(1, out initialMotionBaseline) && initialMotionBaseline.Valid;
                    if (hasInitialMotionBaseline)
                    {
                        bool scanModeActive = TryIsScanningModeActive(out int controlWord);
                        Log4Net.Info($"[MeteorScanMotionGate] BaselineCaptured stage=WriteImageLayer-BeforeFirstStartScan pcc={initialMotionBaseline.PccNum} scanMode={(scanModeActive ? "On" : "OffOrUnknown")} control=0x{controlWord:X8} absX={initialMotionBaseline.AbsXCount} encoder={initialMotionBaseline.EncoderCount} utc={DateTime.UtcNow:O}");
                    }
                    else
                    {
                        Log4Net.Info("[MeteorScanMotionGate] BaselineMissing stage=WriteImageLayer-BeforeFirstStartScan note=unable to read PCC state");
                    }
                }
                int bitsPerPixel = layer.nWidth > 0 ? (layer.nBytesPerLine * 8) / layer.nWidth : 1;
                if (bitsPerPixel < 1) bitsPerPixel = 1;
                int actualWidth = layer.nWidth;
                int paddedWidth = ((actualWidth + 31) / 32) * 32;
                if (paddedWidth != actualWidth)
                    Log4Net.Info($"Meteor WriteImageLayer: width padded to DWORD boundary originalWidth={actualWidth} effectiveWidth={paddedWidth} layer={layer.nLayerIndex}");
                int safeImageXStartMin = GetSafeImageXStartMin();
                int xDpi = (int)(layer.nXDPI > 0 ? layer.nXDPI : 400);
                int absXAtSwath = 0;
                bool hasAbsXAtSwath = TryGetPccMotionSnapshot(1, out PccMotionSnapshot snapAtSwath) && snapAtSwath.Valid;
                if (hasAbsXAtSwath)
                    absXAtSwath = GetAbsXCount24Signed(snapAtSwath.AbsXCount);
                if (needFirstHomeAfterStartScan && hasAbsXAtSwath)
                    CaptureLayerAbsXDeltaCompAtFlush(absXAtSwath, layer.nLayerIndex);
                int pendingPass = Math.Max(0, Math.Min(_pendingSwathPassIndex, _passImageXStartAbsXAnchor.Length - 1));
                int scanTravelWidthPx = GetScanTravelWidthPixels(xDpi);
                bool clampWidthToScanEarly = ShouldClampImageWidthToScanTravel();
                int imageMaxWidthPx = GetImageMaxWidthPixels();
                int printWidthForRevPx = actualWidth;
                if (clampWidthToScanEarly && printWidthForRevPx > scanTravelWidthPx)
                    printWidthForRevPx = scanTravelWidthPx;
                bool useUnifiedRevXStart = ShouldUseUnifiedAlignImageXStart()
                    && !IsSplitJobPerPassEnabled()
                    && _jobUnifiedImageXStartValid
                    && pendingPass > 0
                    && layer.nPrtDir != 1
                    && !needFirstHomeAfterStartScan
                    && !(ShouldUsePassLiveAbsXCompBatchGateSplit() && pendingPass > 0);
                int fixedFwdXStartPx = 0;
                bool useFixedFwdXStart = !useUnifiedRevXStart
                    && TryGetFixedFwdImageXStartAbsXPixels(xDpi, out fixedFwdXStartPx);
                bool useLiveAbsXForFwd = ShouldUseLiveAbsXForImageXStart()
                    && !useUnifiedRevXStart && !useFixedFwdXStart
                    && (pendingPass == 0 || layer.nPrtDir == 1)
                    && !(IsBatchSwathModeEnabled() && !IsSplitJobPerPassEnabled() && pendingPass > 0);
                int revPdAlignOffsetPx = useUnifiedRevXStart ? GetImageXStartRevPdAlignOffsetPixels(xDpi) : 0;
                int imageXStartBasePixels = useUnifiedRevXStart
                    ? Math.Max(0, _jobUnifiedImageXStartAbsX + printWidthForRevPx + revPdAlignOffsetPx)
                    : useFixedFwdXStart
                        ? fixedFwdXStartPx
                        : useLiveAbsXForFwd
                            ? (pendingPass == 0
                                ? Math.Max(0, absXAtSwath)
                                : ResolveLiveFwdImageXStartBasePixels(pendingPass, absXAtSwath))
                            : GetImageXStartBasePixels(xDpi);
                if (needFirstHomeAfterStartScan && ShouldUseUnifiedAlignImageXStart() && !IsSplitJobPerPassEnabled()
                    && pendingPass == 0 && !_jobUnifiedImageXStartValid)
                {
                    int unifiedFwdBasePx = imageXStartBasePixels;
                    if (TryGetForcedImageXStartPixels(SD_FWD, out int forcedFwdBasePx))
                        unifiedFwdBasePx = forcedFwdBasePx;
                    if (unifiedFwdBasePx > 64)
                    {
                        _jobUnifiedImageXStartAbsX = unifiedFwdBasePx;
                        _jobUnifiedImageXStartValid = true;
                        Log4Net.Info($"[MeteorXStart] JobUnifiedAlignFwdBase captured={unifiedFwdBasePx} passIndex=0 liveAbsX={absXAtSwath} utc={DateTime.UtcNow:O}");
                    }
                }
                else if (needFirstHomeAfterStartScan && ShouldUseUnifiedAlignImageXStart() && !IsSplitJobPerPassEnabled()
                    && pendingPass == 0 && !_jobUnifiedImageXStartValid && imageXStartBasePixels <= 64)
                {
                    Log4Net.Info($"[MeteorXStart] JobUnifiedAlignFwdBaseSkipped invalidBase={imageXStartBasePixels} liveAbsX={absXAtSwath} passIndex={pendingPass} utc={DateTime.UtcNow:O}");
                }
                int imageXStartFineOffset = Math.Max(0, layer.nXEncOff - 1);
                int imageXStartOffsetPx = GetImageXStartOffsetPixels(xDpi);
                int leadInOffsetPx = layer.nPrtDir == 1 ? imageXStartOffsetPx : 0;
                int imageXStart = Math.Max(safeImageXStartMin, imageXStartBasePixels + imageXStartFineOffset + leadInOffsetPx);
                if (pendingPass >= 2 && layer.nPrtDir == 1 && imageXStart <= leadInOffsetPx + 64)
                {
                    int fallbackBase = 0;
                    if (_passScanEndAbsXValid[1])
                        fallbackBase = Math.Max(fallbackBase, _passScanEndAbsX[1]);
                    if (_passImageXStartAbsXAnchorValid[pendingPass])
                        fallbackBase = Math.Max(fallbackBase, _passImageXStartAbsXAnchor[pendingPass]);
                    if (_jobUnifiedImageXStartValid)
                        fallbackBase = Math.Max(fallbackBase, _jobUnifiedImageXStartAbsX);
                    if (fallbackBase > imageXStartBasePixels)
                    {
                        Log4Net.Info($"[MeteorXStart] Pass2XStartFallback badBase={imageXStartBasePixels} fallbackBase={fallbackBase} liveAbsX={absXAtSwath} passIndex={pendingPass} utc={DateTime.UtcNow:O}");
                        imageXStartBasePixels = fallbackBase;
                        imageXStart = Math.Max(safeImageXStartMin, imageXStartBasePixels + imageXStartFineOffset + leadInOffsetPx);
                    }
                }
                if (useFixedFwdXStart)
                    Log4Net.Info($"[MeteorXStart] FixedFwdXStart absPx={imageXStartBasePixels} absMm≈{AbsXCountToMm(imageXStartBasePixels, xDpi):F1} passIndex={pendingPass} utc={DateTime.UtcNow:O}");
                if (leadInOffsetPx != 0)
                    Log4Net.Info($"[MeteorXStart] XStartOffsetApplied offsetPx={leadInOffsetPx} offsetMm≈{AbsXCountToMm(leadInOffsetPx, xDpi):F1} baseBeforeOffset={imageXStartBasePixels + imageXStartFineOffset} final={imageXStart} passIndex={pendingPass} utc={DateTime.UtcNow:O}");
                if (useUnifiedRevXStart && layer.nPrtDir != 1)
                    Log4Net.Info($"[MeteorXStart] UnifiedAlignRevXStart fwdBase={_jobUnifiedImageXStartAbsX} printWidthPx={printWidthForRevPx} revPdAlignOffsetPx={revPdAlignOffsetPx} revBase={imageXStartBasePixels} passIndex={pendingPass} utc={DateTime.UtcNow:O}");
                if (useLiveAbsXForFwd && pendingPass > 0 && layer.nPrtDir == 1)
                    Log4Net.Info($"[MeteorXStart] LiveFwdPassXStart passIndex={pendingPass} liveAbsX={absXAtSwath} passAnchor={(_passImageXStartAbsXAnchorValid[pendingPass] ? _passImageXStartAbsXAnchor[pendingPass].ToString() : "n/a")} pass1EndAbsX={(_passScanEndAbsXValid[1] ? _passScanEndAbsX[1].ToString() : "n/a")} resolvedBase={imageXStartBasePixels} utc={DateTime.UtcNow:O}");
                bool clampWidthToScan = ShouldClampImageWidthToScanTravel();
                int printWidthPx = actualWidth;
                if (clampWidthToScan && printWidthPx > scanTravelWidthPx)
                    printWidthPx = scanTravelWidthPx;
        int contentWidthPx = Math.Min(actualWidth, printWidthPx);
                if (imageMaxWidthPx > 0 && contentWidthPx > imageMaxWidthPx)
                {
                    Log4Net.Info($"[MeteorXStart] ImageMaxContentWidthApplied maxWidthPx={imageMaxWidthPx} originalWidthPx={actualWidth} imageWindowWidthPx={printWidthPx} passIndex={pendingPass} note=keep IMAGE window width for HDC edge margin; zero tail content utc={DateTime.UtcNow:O}");
                    contentWidthPx = imageMaxWidthPx;
                }
                int imageXReverseDelta = GetImageXReverseStartDelta(actualWidth);
                int imageXReverseStart = imageXStart + imageXReverseDelta;
                bool hiPrintCompatMode = IsHiPrintChainCompatModeEnabled();
                string requestedScanDir = layer.nPrtDir == 1 ? "FWD" : "REV";
                uint startScanDir = (layer.nPrtDir == 1) ? SD_FWD : SD_REV;
                bool flipPass0StartScanDirForValidation = hiPrintCompatMode && pendingPass == 0 && ShouldFlipPass0StartScanDir();
                if (flipPass0StartScanDirForValidation)
                    startScanDir = startScanDir == SD_FWD ? SD_REV : SD_FWD;
                string actualScanDir = startScanDir == SD_FWD ? "FWD" : "REV";
                int scanLowPxForLog = 0;
                int scanHighPxForLog = 0;
                int hiPrintBaseForLog = 0;
                int hiPrintRevForLog = 0;
                string xStartAnchorMode = "n/a";
                if (hiPrintCompatMode)
                {
                    imageXStart = Math.Max(safeImageXStartMin, GetImageXStartBasePixels(xDpi));
                    imageXReverseStart = imageXStart + printWidthPx;
                }
                int printRowWordCount = ((printWidthPx * bitsPerPixel) + 31) / 32;
                int printImageWordCount = layer.nHeight * printRowWordCount;
                bool unifiedAlignX = useLiveAbsXForFwd || useUnifiedRevXStart || useFixedFwdXStart;
                int imageCmdXStart = hiPrintCompatMode
                    ? ResolveHiPrintCompatImageCmdXStart(
                        xDpi, printWidthPx, safeImageXStartMin, startScanDir,
                        flipPass0StartScanDirForValidation, pendingPass,
                        absXAtSwath, hasAbsXAtSwath,
                        out scanLowPxForLog, out scanHighPxForLog, out hiPrintBaseForLog, out hiPrintRevForLog,
                        out xStartAnchorMode)
                    : ResolveImageCmdXStart(imageXStart, imageXReverseStart, layer.nPrtDir, unifiedAlignX);
                if (hiPrintCompatMode && startScanDir == SD_FWD && imageXStartOffsetPx != 0)
                {
                    int imageCmdXStartBeforeLeadIn = imageCmdXStart;
                    imageCmdXStart = Math.Max(safeImageXStartMin, imageCmdXStart + imageXStartOffsetPx);
                    Log4Net.Info($"[MeteorXStart] HiPrintCompatFwdLeadInOffset offsetPx={imageXStartOffsetPx} offsetMm≈{AbsXCountToMm(imageXStartOffsetPx, xDpi):F1} before={imageCmdXStartBeforeLeadIn} after={imageCmdXStart} passIndex={pendingPass} anchor={xStartAnchorMode} requestedScanDir={requestedScanDir} actualScanDir={actualScanDir} utc={DateTime.UtcNow:O}");
                }
                bool skipForcedXStartForPassGateAnchor = IsPassGateAnchorXStartEnabled() && pendingPass > 0;
                if (!skipForcedXStartForPassGateAnchor && TryGetForcedImageXStartPixels(startScanDir, out int forcedImageXStartPx))
                {
                    int originalImageCmdXStart = imageCmdXStart;
                    imageCmdXStart = Math.Max(safeImageXStartMin, forcedImageXStartPx);
                    Log4Net.Info($"[MeteorXStart] FixedOverride env=METEOR_IMAGE_XSTART_FIXED_FWD_PX/REV_PX original={originalImageCmdXStart} forced={imageCmdXStart} passIndex={pendingPass} requestedScanDir={requestedScanDir} actualScanDir={actualScanDir} utc={DateTime.UtcNow:O}");
                }
                if (TryComputePassLiveAbsXCompPx(pendingPass, startScanDir, out int passLiveAbsXCompPx, out string passLiveAbsXCompMode))
                {
                    int imageCmdXStartBeforeComp = imageCmdXStart;
                    imageCmdXStart = Math.Max(safeImageXStartMin, imageCmdXStart + passLiveAbsXCompPx);
                    int pass1RawOverlapPx = (pendingPass == 1 && _passImageXStartAbsXAnchorValid[1] && _pass0ScanAbsXLowEndValid)
                        ? _passImageXStartAbsXAnchor[1] - _pass0ScanAbsXAtLowEnd : 0;
                    int pass2PredictedLowExcessPx = 0;
                    if (pendingPass == 2 && _pass0ScanAbsXLowEndValid && _passImageXStartAbsXAnchorValid[0] && _passImageXStartAbsXAnchorValid[2])
                    {
                        int pass1TurnRawPx = _passImageXStartAbsXAnchorValid[1] ? _passImageXStartAbsXAnchor[1] - _pass0ScanAbsXAtLowEnd : 0;
                        pass2PredictedLowExcessPx = (_passImageXStartAbsXAnchor[2] - _passImageXStartAbsXAnchor[0]) + pass1TurnRawPx - GetPass1LiveAbsOverlapNominalPx();
                    }
                    Log4Net.Info($"[MeteorPassLiveAbsXComp] passIndex={pendingPass} mode={passLiveAbsXCompMode} compPx={passLiveAbsXCompPx} before={imageCmdXStartBeforeComp} after={imageCmdXStart} pass0Low={(_pass0ScanAbsXLowEndValid ? _pass0ScanAbsXAtLowEnd.ToString() : "n/a")} pass1RawOverlap={pass1RawOverlapPx} pass2PredictedLowExcess={pass2PredictedLowExcessPx} overlapNominal={GetPass1LiveAbsOverlapNominalPx()} compMax={GetPassLiveAbsXCompMaxPx()} anchor0={(_passImageXStartAbsXAnchorValid[0] ? _passImageXStartAbsXAnchor[0].ToString() : "n/a")} anchor1={(_passImageXStartAbsXAnchorValid[1] ? _passImageXStartAbsXAnchor[1].ToString() : "n/a")} anchor2={(_passImageXStartAbsXAnchorValid[2] ? _passImageXStartAbsXAnchor[2].ToString() : "n/a")} requestedScanDir={requestedScanDir} utc={DateTime.UtcNow:O}");
                }
                int allFwdPassStepOffsetPx = GetAllFwdPassStepOffsetPixels(pendingPass);
                if (allFwdPassStepOffsetPx != 0 && startScanDir == SD_FWD)
                {
                    int imageCmdXStartBeforeAllFwdPassStep = imageCmdXStart;
                    imageCmdXStart = Math.Max(safeImageXStartMin, imageCmdXStart + allFwdPassStepOffsetPx);
                    Log4Net.Info($"[MeteorAllFwdDiag] PassStepXStartOffset passIndex={pendingPass} offsetPx={allFwdPassStepOffsetPx} before={imageCmdXStartBeforeAllFwdPassStep} after={imageCmdXStart} offsetMm≈{AbsXCountToMm(allFwdPassStepOffsetPx, xDpi):F3} utc={DateTime.UtcNow:O}");
                }
                if (TryGetPassSpecificImageXStartPixels(pendingPass, startScanDir, out int passSpecificImageXStartPx, out string passSpecificEnvName))
                {
                    int imageCmdXStartBeforePassSpecific = imageCmdXStart;
                    imageCmdXStart = Math.Max(safeImageXStartMin, passSpecificImageXStartPx);
                    Log4Net.Info($"[MeteorXStart] PassSpecificOverride env={passSpecificEnvName} original={imageCmdXStartBeforePassSpecific} forced={imageCmdXStart} passIndex={pendingPass} requestedScanDir={requestedScanDir} actualScanDir={actualScanDir} utc={DateTime.UtcNow:O}");
                }

                int contentTargetXOffsetPx = 0;
                if (pendingPass == 1 && startScanDir == SD_REV && printWidthPx > contentWidthPx)
                {
                    int commandWindowPadPx = GetPass1RevHdcWindowPadPixels();
                    if (commandWindowPadPx > 0)
                    {
                        int availableContentPadPx = printWidthPx - contentWidthPx;
                        contentTargetXOffsetPx = Math.Min(commandWindowPadPx, availableContentPadPx);
                        int imageCmdXStartBeforeHdcWindowPad = imageCmdXStart;
                        imageCmdXStart = Math.Max(safeImageXStartMin, imageCmdXStart + commandWindowPadPx);
                        Log4Net.Info($"[MeteorRevHdcWindow] passIndex=1 commandXStartBefore={imageCmdXStartBeforeHdcWindowPad} commandXStartAfter={imageCmdXStart} commandWindowPadPx={commandWindowPadPx} contentTargetPadPx={contentTargetXOffsetPx} contentWidthPx={contentWidthPx} imageWindowWidthPx={printWidthPx} note=preserve HDC1 REV trigger margin while retaining content geometry utc={DateTime.UtcNow:O}");
                    }
                }
                if (hiPrintCompatMode)
                {
                    Log4Net.Info($"[MeteorXStart] HiPrintCompat baseXStart={hiPrintBaseForLog} hiPrintRevXStart={hiPrintRevForLog} measuredAbsXRange=[{scanLowPxForLog},{scanHighPxForLog}] imageCmdXStart={imageCmdXStart} printWidthPx={printWidthPx} passIndex={pendingPass} requestedScanDir={requestedScanDir} actualScanDir={actualScanDir} pass0DirFlip={flipPass0StartScanDirForValidation} liveAbsX24={absXAtSwath} anchor={xStartAnchorMode} utc={DateTime.UtcNow:O}");
                }
                int headerWordCount = 6;
                int commandWordCount = headerWordCount + printImageWordCount;
                uint[] cmd = new uint[commandWordCount];
                double absXmm = hasAbsXAtSwath ? AbsXCountToMm(snapAtSwath.AbsXCount, xDpi) : double.NaN;
                double scanTravelMm = GetScanTravelMm();
                double printWidthMm = printWidthPx * 25.4 / xDpi;
                double contentWidthMm = contentWidthPx * 25.4 / xDpi;
                double xStartMm = AbsXCountToMm(imageCmdXStart, xDpi);
                int encoderStartNeededPx;
                int encoderEndNeededPx;
                if (startScanDir == SD_REV)
                {
                    encoderEndNeededPx = imageCmdXStart;
                    encoderStartNeededPx = imageCmdXStart - printWidthPx;
                }
                else
                {
                    encoderStartNeededPx = imageCmdXStart;
                    encoderEndNeededPx = imageCmdXStart + printWidthPx;
                }
                int coverageScanLowPx = 0;
                int coverageScanHighPx = 0;
                bool hasMeasuredScanRange = pendingPass == 0 && TryGetPass0MeasuredAbsXScanRange(out coverageScanLowPx, out coverageScanHighPx);
                string coverageRangeSource = hasMeasuredScanRange ? "pass0_measuredAbsX" : "none";
                if (!hasMeasuredScanRange && scanLowPxForLog > 0 && scanHighPxForLog > scanLowPxForLog)
                {
                    coverageScanLowPx = scanLowPxForLog;
                    coverageScanHighPx = scanHighPxForLog;
                    hasMeasuredScanRange = true;
                    coverageRangeSource = "pass0_partialMeasuredAbsX";
                }
                bool revRangeInScan = false;
                bool fwdRangeInScan = false;
                string coverageNote;
                int coverageShortfallPx = 0;
                if (hasMeasuredScanRange)
                {
                    revRangeInScan = startScanDir == SD_REV
                        && encoderStartNeededPx >= coverageScanLowPx - 64
                        && encoderEndNeededPx <= coverageScanHighPx + 64;
                    fwdRangeInScan = startScanDir == SD_FWD
                        && encoderStartNeededPx >= coverageScanLowPx - 64
                        && encoderEndNeededPx <= coverageScanHighPx + 64;
                    coverageShortfallPx = startScanDir == SD_REV
                        ? Math.Max(0, coverageScanLowPx - encoderStartNeededPx)
                        : Math.Max(0, encoderEndNeededPx - coverageScanHighPx);
                    coverageNote = (startScanDir == SD_REV ? revRangeInScan : fwdRangeInScan)
                        ? "OK"
                        : (clampWidthToScan ? "CLAMPED" : "SHORTFALL");
                }
                else
                {
                    coverageScanLowPx = hasAbsXAtSwath ? absXAtSwath : 0;
                    coverageScanHighPx = hasAbsXAtSwath ? absXAtSwath + scanTravelWidthPx : 0;
                    coverageRangeSource = "liveAbsX_only";
                    revRangeInScan = startScanDir == SD_REV && hasAbsXAtSwath;
                    fwdRangeInScan = startScanDir == SD_FWD && hasAbsXAtSwath;
                    coverageNote = hasAbsXAtSwath ? "UNCALIBRATED_LIVE" : "NO_ABSX";
                }
                Log4Net.Info($"[MeteorXCoverage] passIndex={pendingPass} requestedScanDir={requestedScanDir} actualScanDir={actualScanDir} xStart={imageCmdXStart} xStartMm≈{xStartMm:F1} encRange=[{encoderStartNeededPx},{encoderEndNeededPx}] scanRangePx=[{coverageScanLowPx},{coverageScanHighPx}] rangeSource={coverageRangeSource} printWidthPx={printWidthPx} printWidthMm≈{printWidthMm:F1} contentWidthPx={contentWidthPx} contentWidthMm≈{contentWidthMm:F1} scanTravelPx={scanTravelWidthPx} scanTravelMm={scanTravelMm:F1} shortfallPx={coverageShortfallPx} shortfallMm≈{AbsXCountToMm(coverageShortfallPx, xDpi):F1} status={coverageNote}");
        Log4Net.Info($"Meteor WriteImageLayer: swath起点 layer={layer.nLayerIndex} passIndex={pendingPass} nXDPI={xDpi} useFixedFwdXStart={useFixedFwdXStart} useUnifiedRevXStart={useUnifiedRevXStart} unifiedFwdBase={(_jobUnifiedImageXStartValid ? _jobUnifiedImageXStartAbsX.ToString() : "n/a")} usePassAnchor={ShouldUsePassAnchoredAbsXForImageXStart()} passAnchorAbsX={(_passImageXStartAbsXAnchorValid[pendingPass] ? _passImageXStartAbsXAnchor[pendingPass].ToString() : "n/a")} useLiveAbsXForFwd={useLiveAbsXForFwd} pccAbsX={(hasAbsXAtSwath ? snapAtSwath.AbsXCount.ToString() : "n/a")} absX24={absXAtSwath} absXmm≈{(hasAbsXAtSwath ? absXmm.ToString("F1") : "n/a")} nPrtDir={layer.nPrtDir} requestedScanDir={requestedScanDir} actualScanDir={actualScanDir} imageCmdXStart={imageCmdXStart} imageXStart={imageXStart} imageXReverseStart={imageXReverseStart} legacyRevDelta={imageXReverseDelta} paddedWidth={paddedWidth} printWidthPx={printWidthPx} contentWidthPx={contentWidthPx} contentTargetXOffsetPx={contentTargetXOffsetPx} scanTravelWidthPx={scanTravelWidthPx} clampWidth={clampWidthToScan} yJetOff={layer.nYJetOff}");
                cmd[0] = PCMD_IMAGE;
                cmd[1] = (uint)(commandWordCount - 2);
                cmd[2] = 1; // plane
                cmd[3] = (uint)imageCmdXStart;
                cmd[4] = (uint)Math.Max(0, layer.nYJetOff);
                cmd[5] = (uint)printWidthPx;
                Log4Net.Info($"[MeteorDirChain] stage=BeforeImagePack passIndex={pendingPass} nPrtDir={layer.nPrtDir} requestedScanDir={requestedScanDir} actualStartScanDir={actualScanDir} imageCmdHeader=[0x{cmd[0]:X8},0x{cmd[1]:X8},0x{cmd[2]:X8},0x{cmd[3]:X8},0x{cmd[4]:X8},0x{cmd[5]:X8}] utc={DateTime.UtcNow:O}");
                Log4Net.Info($"Meteor WriteImageLayer: IMAGE xStart={cmd[3]} yStart={cmd[4]} width={cmd[5]} layer={layer.nLayerIndex} prtDir={layer.nPrtDir} requestedScanDir={requestedScanDir} actualScanDir={actualScanDir} imageCmdXStart={imageCmdXStart} revDeltaApplied={(imageCmdXStart - imageXStart)} scanTravelHintPx={scanTravelWidthPx}");
                int bitmapStrideBytes = layer.nBytesPerLine;
                if (bitsPerPixel == 1)
                {
                    byte[] imageBytes = new byte[bytes];
                    Marshal.Copy(imgPtr, imageBytes, 0, bytes);
                    string imageTransform = DescribeImageOrientationTransformFlags();
                    if (!string.Equals(imageTransform, "none", StringComparison.Ordinal))
                        Log4Net.Info($"Meteor WriteImageLayer: image orientation transform={imageTransform} layer={layer.nLayerIndex} passIndex={pendingPass} height={layer.nHeight} width={contentWidthPx}");
                    PackImageRowsToCommandBuffer(imageBytes, bitmapStrideBytes, contentWidthPx, layer.nHeight, cmd, headerWordCount, printRowWordCount, contentTargetXOffsetPx);
                }
                else
                {
                    bool flipVertically = ShouldFlipImageRowsVertically();
                    bool flipHorizontally = ShouldFlipImageColumnsHorizontally();
                    string imageTransform = DescribeImageOrientationTransformFlags();
                    if (!string.Equals(imageTransform, "none", StringComparison.Ordinal))
                        Log4Net.Info($"Meteor WriteImageLayer: image orientation transform={imageTransform} layer={layer.nLayerIndex} passIndex={pendingPass} height={layer.nHeight}");
                    int contentRowWordCount = ((contentWidthPx * bitsPerPixel) + 31) / 32;
                    int sourceWordCount = Math.Min(contentRowWordCount, (bitmapStrideBytes + 3) / 4);
                    for (int y = 0; y < layer.nHeight; y++)
                    {
                        int srcRow = MapSourceImageRowIndex(y, layer.nHeight, flipVertically);
                        int srcOffset = srcRow * bitmapStrideBytes;
                        int dstBase = headerWordCount + y * printRowWordCount;
                        for (int x = 0; x < printRowWordCount; x++)
                        {
                            int srcWordIndex = flipHorizontally ? (sourceWordCount - 1 - x) : x;
                            if (srcWordIndex >= 0 && srcWordIndex < sourceWordCount && srcOffset + (srcWordIndex + 1) * 4 <= bytes && dstBase + x < commandWordCount)
                                cmd[dstBase + x] = (uint)Marshal.ReadInt32(imgPtr, srcOffset + srcWordIndex * 4);
                            else if (dstBase + x < commandWordCount)
                                cmd[dstBase + x] = 0;
                        }
                    }
                }
                DiagnoseAndExportFinalSwathPayload(cmd, headerWordCount, printWidthPx, layer.nHeight, printRowWordCount, pendingPass, layer.nLayerIndex);
                uint[] startScanCmd = { PCMD_STARTSCAN, 1, startScanDir };
                if (IsPassGateAnchorXStartEnabled() && pendingPass > 0)
                {
                    _passSwathPrepackedReady[pendingPass].Set();
                    Log4Net.Info($"[MeteorScanGate] PassSwathPrepackedReady passIndex={pendingPass} imageCmdXStart={imageCmdXStart} utc={DateTime.UtcNow:O}");
                    if (!ShouldUsePassGateAnchorXStartBatchGateSplit()
                        && !_passMotionStartedForMeteorSend[pendingPass].Wait(10000))
                    {
                        Log4Net.Info($"[MeteorScanGate] PassMotionStartWaitTimeout passIndex={pendingPass} timeoutMs=10000 utc={DateTime.UtcNow:O}");
                        return -200103;
                    }
                }
                if (ShouldDeferLegacyBatchSwathSend(pendingPass))
                {
                    _deferredLegacyBatchSwaths.Add(new DeferredLegacyBatchSwath
                    {
                        StartScanCmd = (uint[])startScanCmd.Clone(),
                        ImageCmd = (uint[])cmd.Clone(),
                        EndDocCmd = new uint[] { PCMD_ENDDOC, 0 },
                        PassIndex = pendingPass,
                        LayerIndex = layer.nLayerIndex,
                        MarkFirstHomeAfterStartScan = needFirstHomeAfterStartScan
                    });
                    Log4Net.Info($"[MeteorBatchDefer] Queued passIndex={pendingPass} layer={layer.nLayerIndex} imageCmdXStart={imageCmdXStart} printWidthPx={printWidthPx} queueCount={_deferredLegacyBatchSwaths.Count} utc={DateTime.UtcNow:O}");
                    return 1;
                }
                if (!WaitForCommandSpace((uint)cmd.Length, $"WriteImageLayer IMAGE layer={layer.nLayerIndex}"))
                {
                    Log4Net.Info($"Meteor WriteImageLayer: IMAGE 前命令空间不足，放弃发??layer={layer.nLayerIndex} requiredDwords={cmd.Length}");
                    return -200102;
                }
                Log4Net.Info($"[MeteorDirChain] stage=BeforeStartScan passIndex={pendingPass} nPrtDir={layer.nPrtDir} requestedScanDir={requestedScanDir} actualStartScanDir={actualScanDir} startScanCmd=[0x{startScanCmd[0]:X8},0x{startScanCmd[1]:X8},0x{startScanCmd[2]:X8}] imageCmdXStart={imageCmdXStart} printWidthPx={printWidthPx} note=imageCommandPrepacked utc={DateTime.UtcNow:O}");
                Log4Net.Info($"Meteor WriteImageLayer: sending STARTSCAN layer={layer.nLayerIndex} dir={startScanCmd[2]} requestedDir={(layer.nPrtDir == 1 ? SD_FWD : SD_REV)} actualScanDir={actualScanDir} passIndex={pendingPass} hiPrintCompat={hiPrintCompatMode} pass0DirFlipValidation={flipPass0StartScanDirForValidation} threadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                int r = DoSendCommand(startScanCmd);
                Log4Net.Info($"Meteor WriteImageLayer: STARTSCAN returned r={r} layer={layer.nLayerIndex}");
                if (r != RVAL_OK) return r;

                if (needFirstHomeAfterStartScan)
                {
                    _homeCommandIssued = true;
                    LogPccStatus("WriteImageLayer-AfterFirstStartScan");
                    Log4Net.Info("Meteor WriteImageLayer: first swath STARTSCAN sent after IMAGE prepack; no PiSetHome call here, only marks first-startscan handled");
                }
                Log4Net.Info($"[MeteorDirChain] stage=BeforeImageSend passIndex={pendingPass} nPrtDir={layer.nPrtDir} requestedScanDir={requestedScanDir} actualStartScanDir={actualScanDir} imageCmdHeader=[0x{cmd[0]:X8},0x{cmd[1]:X8},0x{cmd[2]:X8},0x{cmd[3]:X8},0x{cmd[4]:X8},0x{cmd[5]:X8}] utc={DateTime.UtcNow:O}");
                Log4Net.Info($"Meteor WriteImageLayer: sending IMAGE layer={layer.nLayerIndex} totalDwords={commandWordCount} threadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                r = DoSendCommand(cmd);
                Log4Net.Info($"Meteor WriteImageLayer: IMAGE returned r={r} layer={layer.nLayerIndex}");
                if (r != RVAL_OK) return r;

                uint[] endDoc = { PCMD_ENDDOC, 0 };
                Log4Net.Info($"Meteor WriteImageLayer: sending ENDDOC layer={layer.nLayerIndex} threadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                r = DoSendCommand(endDoc);
                Log4Net.Info($"Meteor WriteImageLayer: ENDDOC returned r={r} layer={layer.nLayerIndex}");
                if (r != RVAL_OK) return r;
                Log4Net.Info($"Meteor WriteImageLayer: PCMD_STARTSCAN+IMAGE+ENDDOC Layer={layer.nLayerIndex} {layer.nWidth}x{layer.nHeight} bpp={bitsPerPixel} sent");
                bool deferPass0ReadyUntilBatchComplete = IsBatchSwathModeEnabled()
                    && !IsBatchStartScanGateSplitEnabled()
                    && !IsSplitJobPerPassEnabled()
                    && !IsAllFwdDiagnosticModeEnabled();
                if (_scanJobStarted && !_pass0FirstSwathMeteorSignaled && !deferPass0ReadyUntilBatchComplete)
                    SignalPass0FirstSwathMeteorReadyIfNeeded("WriteImageLayer:FirstSwathEndDoc");
                SignalPassSwathMeteorReadyIfNeeded(_pendingSwathPassIndex, "WriteImageLayer:SwathEndDoc");
                return 1; // 调用方以 nRet > 0 判断成功
            }
        }
        public static bool TryGetPassItem(uint layerIndex, int passId, ref royal.LPPassDataItem passData)
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened() || passId < 0)
            {
                // 失败时把 procState 置为 3，避免上??while 循环无穷等待
                passData.nProcState = 3;
                return false;
            }

            bool ok = false;
            try
            {
                ok = royal.royal.IDP_GetPassItem2(layerIndex, passId, ref passData);
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TryGetPassItem: 调用 IDP_GetPassItem2 异常：{ex.Message}");
                passData.nProcState = 3;
                return false;
            }

            // 把真实的有效数据打印出来，便于你确认“为什么不喷??
            Log4Net.Info(
                $"Meteor TryGetPassItem: layerIndex={layerIndex} passId={passId} ok={ok} " +
                $"nProcState={passData.nProcState} " +
                $"nValidPassJets={passData.nValidPassJets} " +
                $"nValidPrtCols={passData.nValidPrtCols} " +
                $"nValidPrtCtlCnts={passData.nValidPrtCtlCnts} " +
                $"nMinJet0ImgLinePos={passData.nMinJet0ImgLinePos} " +
                $"nPrtPrecession={passData.nPrtPrecession} " +
                $"nStartEncPos={passData.nStartEncPos}"
            );

            if (!ok)
            {
                // 失败时同样置??3，避免上??while 循环卡死
                passData.nProcState = 3;
            }

            return ok;
        }

        /// <summary>触发扫描推进（强??Product Detect）。扫描打印时用于推进到下一扫描行，等效设置 home 位置??/summary>
        public static bool TriggerPass(uint layerIndex, int passId)
        {
            if (!EnsureRuntimeReady() || !EnsurePrinterOpened())
                return false;
            lock (SyncRoot)
            {
                return TryPiSetSignal((int)SIG_FORCEPD, 1);
            }
        }

        /// <summary>扫描模式默认不用 ForcePD（供应商）；仅 METEOR_PURE_METEOR_TRIGGER_PASS=1/all/pass2 等显式开启时触发。</summary>
        private static bool ShouldTriggerPassForPureMeteorPass(int passIndex)
        {
            if (passIndex <= 0)
                return false;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PURE_METEOR_TRIGGER_PASS");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    string t = env.Trim();
                    if (string.Equals(t, "0", StringComparison.Ordinal) || string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "no", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (string.Equals(t, "1", StringComparison.Ordinal) || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "on", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase))
                        return passIndex >= 1;
                    if (string.Equals(t, "2", StringComparison.Ordinal) || string.Equals(t, "pass2", StringComparison.OrdinalIgnoreCase))
                        return passIndex >= 2;
                }
            }
            catch { }
            return false;
        }

        private static int GetPureMeteorTriggerDelayMs()
        {
            const int defaultDelayMs = 200;
            try
            {
                string env = Environment.GetEnvironmentVariable("METEOR_PURE_METEOR_TRIGGER_DELAY_MS");
                if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env.Trim(), out int ms))
                    return Math.Max(0, Math.Min(ms, 3000));
            }
            catch { }
            return defaultDelayMs;
        }

        /// <summary>可选 ForcePD（默认关闭）；扫描 pass 切换依赖 natural PD。</summary>
        public static bool TriggerPassForPureMeteorSchedule(uint layerIndex, int passId, string motionStage)
        {
            if (passId <= 0)
                return true;
            if (!ShouldTriggerPassForPureMeteorPass(passId))
            {
                Log4Net.Info($"[MeteorScanGate] PureMeteorTriggerPassSkipped passId={passId} layer={layerIndex} stage={motionStage} note=scanModeUsesNaturalPd utc={DateTime.UtcNow:O}");
                return true;
            }
            int delayMs = GetPureMeteorTriggerDelayMs();
            if (delayMs > 0)
            {
                Log4Net.Info($"[MeteorScanGate] PureMeteorTriggerPassDelay passId={passId} layer={layerIndex} stage={motionStage} delayMs={delayMs} utc={DateTime.UtcNow:O}");
                Thread.Sleep(delayMs);
            }
            Log4Net.Info($"[MeteorScanGate] PureMeteorTriggerPassBegin passId={passId} layer={layerIndex} stage={motionStage} utc={DateTime.UtcNow:O}");
            LogPccStatus($"PureMeteorTriggerPass-BeforeForcePD-P{passId}");
            bool ok = TriggerPass(layerIndex, passId);
            LogPccStatus($"PureMeteorTriggerPass-AfterForcePD-P{passId}");
            Log4Net.Info($"[MeteorScanGate] PureMeteorTriggerPassEnd passId={passId} layer={layerIndex} stage={motionStage} ok={ok} utc={DateTime.UtcNow:O}");
            return ok;
        }

        public static bool GetPrintState(ref royal.LPPrtRunInfo runInfo)
        {
            if (!EnsureRuntimeReady())
            {
                return false;
            }

            if (!EnsurePrinterOpened())
            {
                return false;
            }

            // TODO: map PiGetStatusEx result to LPPrtRunInfo.
            return true;
        }

        /// <summary>终止当前打印作业：先 PiAbort 停止任务，不关闭打印机连接以便可重新启动作业??/summary>
        public static bool StopJob()
        {
            if (!EnsureRuntimeReady())
                return false;
            bool resetPass0GateAfter = false;
            bool ok;
            if (!Monitor.TryEnter(SyncRoot, 3000))
            {
                Log4Net.Info("Meteor StopJob: SyncRoot busy for 3000ms; skip blocking UI close path");
                return false;
            }
            try
            {
                if (_useNativePath)
                {
                    int ret = NativePiAbort();
                    if (ret == RVAL_OK)
                        Log4Net.Info("Meteor StopJob: PiAbort executed; print job stopped");
                    _scanJobStarted = false;
                    _pendingScanJobWidth = 1;
                    _homeCommandIssued = false;
                    _startJobUtc = null;
                    resetPass0GateAfter = true;
                    ok = ret == RVAL_OK;
                }
                else if (!_printerOpened || _printerInterfaceInstance == null || _printerInterfaceType == null)
                {
                    ok = true;
                }
                else
                {
                    ok = false;
                    try
                    {
                        MethodInfo closeMethod = _printerInterfaceType.GetMethod("PiClosePrinter", BindingFlags.Public | BindingFlags.Instance);
                        if (closeMethod != null)
                        {
                            object result = closeMethod.Invoke(_printerInterfaceInstance, null);
                            int closeCode = Convert.ToInt32(result);
                            if (closeCode == 0)
                            {
                                _printerOpened = false;
                                _scanJobStarted = false;
                                _pendingScanJobWidth = 1;
                                _homeCommandIssued = false;
                                _startJobUtc = null;
                                resetPass0GateAfter = true;
                                ok = true;
                            }
                            else
                            {
                                Log4Net.Info($"Meteor StopJob failed, PiClosePrinter return code: {closeCode}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log4Net.Info($"Meteor StopJob exception: {ex}");
                    }
                }
            }
            finally
            {
                Monitor.Exit(SyncRoot);
            }
            if (resetPass0GateAfter)
            {
                ResetPass0GateAfterMeteorJobEnd(true);
                ResetLayerAbsXDeltaCompRef();
            }
            return ok;
        }

        /// <summary>闪喷信号 ID，参??SUM_CN_PrintEngine_SDK 用户手册??/summary>
        private const int SIG_SPIT = 0x0B;
        /// <summary>喷头上电信号 ID（仅当无 PiSetHeadPower 时用 PiSetSignal 占位）??/summary>
        private const int SIG_HDPOWER = 0x08;

        public static bool SetFlash(bool enable)
        {
            if (!EnsureRuntimeReady())
                return false;
            if (!EnsurePrinterOpened())
                return false;

            lock (SyncRoot)
            {
                // 原生路径下无 .NET 实例，仅??NativePiSetHeadPower / NativePiSetSignal
                if (!_useNativePath && (_printerInterfaceInstance == null || _printerInterfaceType == null))
                    return false;

                try
                {
                    if (enable)
                    {
                        // 仅触发闪喷，不控制喷头卡上电/断电（由单独喷头上电按钮控制??
                        int pccnum = 0;
                        int hnum = 0;
                        int spitCount = 200;
                        int signal = SIG_SPIT | (hnum << 8) | (pccnum << 16);
                        if (!TryPiSetSignal(signal, spitCount))
                        {
                            Log4Net.Info("Meteor SetFlash: PiSetSignal failed; please verify PrinterInterfaceCLS availability");
                            return false;
                        }
                        Log4Net.Info($"Meteor SetFlash(true): PiSetSignal sent signal=0x{signal:X}, count={spitCount}; head power unchanged");
                    }
                    else
                    {
                        // 关闭闪喷：不操作喷头卡电源，仅表示“结束闪喷”状??
                        Log4Net.Info("Meteor SetFlash(false): flash stopped; head power unchanged");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Log4Net.Info($"Meteor SetFlash exception: {ex}");
                    return false;
                }
            }
        }

        private static int GetRvalBusyFromAssembly(Type typeRet)
        {
            try
            {
                if (typeRet != null && Enum.IsDefined(typeRet, "RVAL_BUSY"))
                    return Convert.ToInt32(Enum.Parse(typeRet, "RVAL_BUSY"));
                if (typeRet != null && Enum.IsDefined(typeRet, "RCAL_BUSY"))
                    return Convert.ToInt32(Enum.Parse(typeRet, "RCAL_BUSY"));
            }
            catch { }
            return 1;
        }

        /// <summary>喷头上电前检查：等待 PCC 状态为 IDLE（等??IsPCCStateIdle）。原生路径下跳过??/summary>
        private static bool TryEnsurePccIdleBeforeHeadPower(int pccnum)
        {
            if (_useNativePath) return true;
            try
            {
                Assembly asm = _printerInterfaceType.Assembly;
                Type typePccStatus = asm.GetTypes().FirstOrDefault(t => t.Name == "TAppPccStatus");
                Type typeRet = asm.GetTypes().FirstOrDefault(t => t.Name == "eRET");
                if (typePccStatus == null || typeRet == null)
                    return false;

                MethodInfo miGetStatus = _printerInterfaceType.GetMethod("PiGetPccStatus",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                    null, new Type[] { typeof(int), typePccStatus.MakeByRefType() }, null);
                if (miGetStatus == null)
                    return false;

                object target = miGetStatus.IsStatic ? null : _printerInterfaceInstance;
                FieldInfo fiBmStatus = typePccStatus.GetField("bmStatusBits", BindingFlags.Public | BindingFlags.Instance);
                if (fiBmStatus == null)
                    return false;

                Type typeBmps = asm.GetTypes().FirstOrDefault(t => t.Name == "Bmps");
                Type typePccState = asm.GetTypes().FirstOrDefault(t => t.Name == "ePCCSTATE");
                if (typeBmps == null || typePccState == null)
                    return false;
                FieldInfo fiBmpsPccState = typeBmps.GetField("BMPS_PCC_STATE", BindingFlags.Public | BindingFlags.Static);
                FieldInfo fiShPccState = typeBmps.GetField("SH_PCC_STATE", BindingFlags.Public | BindingFlags.Static);
                if (fiBmpsPccState == null || fiShPccState == null)
                    return false;
                int bmpsPccState = Convert.ToInt32(fiBmpsPccState.GetValue(null));
                int shPccState = Convert.ToInt32(fiShPccState.GetValue(null));
                object psIdle = Enum.Parse(typePccState, "PS_IDLE");

                int rvalBusy = GetRvalBusyFromAssembly(typeRet);
                int timeout = 0;
                while (timeout <= 5)
                {
                    object pccStatus = Activator.CreateInstance(typePccStatus);
                    object[] args = new object[] { pccnum, pccStatus };
                    object result = miGetStatus.Invoke(target, args);
                    int ret = Convert.ToInt32(result);
                    if (ret == rvalBusy)
                    {
                        System.Threading.Thread.Sleep(100);
                        timeout++;
                        continue;
                    }
                    if (ret != RVAL_OK)
                        return false;
                    object bmVal = fiBmStatus.GetValue(args[1]);
                    int bmStatusBits = Convert.ToInt32(bmVal);
                    int pccstateVal = (bmStatusBits & bmpsPccState) >> shPccState;
                    if (pccstateVal == Convert.ToInt32(psIdle))
                    {
                        Log4Net.Info($"Meteor IsPCCStateIdle: pccnum={pccnum} is idle");
                        return true;
                    }
                    System.Threading.Thread.Sleep(100);
                    timeout++;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TryEnsurePccIdleBeforeHeadPower exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>9.8 PiSetHeadPower(DWORD State)：State ??0 关闭喷头，非 0 启动喷头。忙时返??RVAL_BUSY，会重试??/summary>
        private static bool TrySetHeadPower(bool on)
        {
            uint state = on ? 1u : 0u;
            if (_useNativePath)
            {
                int ret = NativePiSetHeadPower(state);
                if (ret == RVAL_OK) { Log4Net.Info($"Meteor PiSetHeadPower({(on ? "on" : "off")}) => OK (native)"); return true; }
                for (int i = 0; i < 5 && ret == RVAL_BUSY; i++)
                {
                    System.Threading.Thread.Sleep(200);
                    ret = NativePiSetHeadPower(state);
                    if (ret == RVAL_OK) { Log4Net.Info($"Meteor PiSetHeadPower 原生 重试{i + 1} => OK"); return true; }
                }
                Log4Net.Info($"Meteor PiSetHeadPower 原生 返回 {ret}");
                return false;
            }
            try
            {
                MethodInfo mi = _printerInterfaceType.GetMethod("PiSetHeadPower", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(uint) }, null)
                    ?? _printerInterfaceType.GetMethod("PiSetHeadPower", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
                if (mi == null)
                {
                    if (TryPiSetSignal(SIG_HDPOWER, on ? 1 : 0))
                    {
                        Log4Net.Info($"Meteor 喷头上电(备用): PiSetSignal(SIG_HDPOWER, {(on ? 1 : 0)})");
                        return true;
                    }
                    return false;
                }

                int ret = Convert.ToInt32(mi.Invoke(_printerInterfaceInstance, new object[] { state }));
                if (ret == RVAL_OK)
                {
                    Log4Net.Info($"Meteor PiSetHeadPower({(on ? "on" : "off")}) => OK");
                    return true;
                }
                if (ret != RVAL_OK)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        System.Threading.Thread.Sleep(200);
                        ret = Convert.ToInt32(mi.Invoke(_printerInterfaceInstance, new object[] { state }));
                        if (ret == RVAL_OK)
                        {
                            Log4Net.Info($"Meteor PiSetHeadPower({(on ? "on" : "off")}) => OK (retry {i + 1})");
                            return true;
                        }
                    }
                    Log4Net.Info($"Meteor PiSetHeadPower 返回??OK (??RVAL_BUSY): {ret}");
                }
                return false;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor TrySetHeadPower exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>通过反射或原??PiSetSignal(signal, count)??/summary>
        private static bool TryPiSetSignal(int signal, int count)
        {
            if (_useNativePath)
            {
                int ret = NativePiSetSignal((uint)signal, (uint)count);
                if (ret == RVAL_OK) return true;
                Log4Net.Info($"Meteor PiSetSignal 原生 返回 {ret}");
                return false;
            }
            try
            {
                MethodInfo mi = _printerInterfaceType.GetMethod("PiSetSignal", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(int) }, null);
                if (mi == null)
                    return false;
                object result = mi.Invoke(_printerInterfaceInstance, new object[] { signal, count });
                if (result != null && result is int code)
                    return code == 0;
                return true;
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor PiSetSignal exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试打开打印机连接，供同程序集内使用??
        /// </summary>
        internal static bool EnsurePrinterOpened()
        {
            if (!EnsureRuntimeReady())
                return false;
            return EnsurePrinterOpenedInternal();
        }

        private static bool EnsureRuntimeReady()
        {
            lock (SyncRoot)
            {
                if (_runtimeReady)
                {
                    return true;
                }

                if (_assemblyLoadAttempted)
                {
                    return false;
                }

                _assemblyLoadAttempted = true;
                TryLoadMeteorAssembly();

                if (!_runtimeReady)
                {
                    Log4Net.Info("Meteor runtime not ready: PrinterInterfaceCLS.dll not found or load failed.");
                }

                return _runtimeReady;
            }
        }

        /// <summary>
        /// 获取用于查找 PrinterInterfaceCLS.dll 的候选目录：先本程序目录，再环境变量/配置，再 C 盘常??Meteor 安装路径??
        /// </summary>
        private static string[] GetMeteorSearchPaths()
        {
            var list = new List<string>();
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            list.Add(appDir);

            // 环境变量或配置文件中??Meteor 安装根目录（若有??
            string envPath = Environment.GetEnvironmentVariable("METEOR_HOME");
            if (string.IsNullOrEmpty(envPath))
                envPath = Environment.GetEnvironmentVariable("METEOR_INSTALL_PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                envPath = envPath.TrimEnd('\\', '/');
                if (Directory.Exists(envPath))
                    list.Add(envPath);
            }

            // C 盘常见安装路径（??SimPrint、PrintEngine、Api 等子目录??
            string[] commonRoots = new[]
            {
                @"C:\Program Files\Meteor Inkjet\Meteor\SimPrint",           // 可能含具体实现的 PrinterInterfaceCLS
                @"C:\Program Files\Meteor Inkjet\Meteor\PrintEngine\amd64",
                @"C:\Program Files\Meteor Inkjet\Meteor\PrintEngine",
                @"C:\Program Files\Meteor Inkjet\Meteor\Api\amd64",
                @"C:\Program Files\Meteor Inkjet\Meteor\Api\x86",
                @"C:\Program Files\Meteor Inkjet\Meteor",
                @"C:\Program Files\Meteor Inkjet",
                @"C:\Program Files\Meteor",
                @"C:\Program Files (x86)\Meteor",
                @"C:\Program Files (x86)\Meteor Inkjet\Meteor\Api\x86",
                @"C:\Program Files\TTP\Meteor",
                @"C:\Meteor",
                @"C:\Meteor Inkjet",
            };
            foreach (string root in commonRoots)
            {
                if (Directory.Exists(root))
                    list.Add(root);
            }

            return list.ToArray();
        }

        /// <summary>收集可能包含 Meteor 依赖/实现 DLL 的目录：DLL 所在目录、多级父目录、以及已有搜索路径??/summary>
        private static List<string> GetProbeDirectories(string dllDir)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(dllDir))
            {
                list.Add(dllDir);
                string parent = dllDir;
                for (int i = 0; i < 5; i++)
                {
                    parent = Path.GetDirectoryName(parent);
                    if (string.IsNullOrEmpty(parent) || list.Contains(parent)) break;
                    list.Add(parent);
                }
            }
            foreach (string dir in GetMeteorSearchPaths())
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !list.Contains(dir))
                    list.Add(dir);
            }
            return list;
        }

        private static void TryLoadMeteorAssembly()
        {
            try
            {
                string[] searchPaths = GetMeteorSearchPaths();
                string dllPath = null;

                // 首先在每个目录的根目录查??
                foreach (string dir in searchPaths)
                {
                    string candidate = Path.Combine(dir, "PrinterInterfaceCLS.dll");
                    if (File.Exists(candidate))
                    {
                        dllPath = candidate;
                        Log4Net.Info($"Meteor: found PrinterInterfaceCLS.dll at [{dllPath}]");
                        break;
                    }
                }
                
                // 如果在根目录没找到，递归搜索子目录（最??层）
                if (string.IsNullOrEmpty(dllPath))
                {
                    foreach (string dir in searchPaths)
                    {
                        if (!Directory.Exists(dir)) continue;
                        
                        try
                        {
                            // 搜索当前目录及其子目??
                            string[] files = Directory.GetFiles(dir, "PrinterInterfaceCLS.dll", SearchOption.AllDirectories);
                            if (files.Length > 0)
                            {
                                // 优先顺序：SimPrint（可能含具体实现??> amd64??4位） > x86
                                dllPath = files.FirstOrDefault(f => f.IndexOf("SimPrint", StringComparison.OrdinalIgnoreCase) >= 0)
                                    ?? files.FirstOrDefault(f => f.IndexOf("amd64", StringComparison.OrdinalIgnoreCase) >= 0)
                                    ?? files.FirstOrDefault(f => f.IndexOf("x86", StringComparison.OrdinalIgnoreCase) >= 0)
                                    ?? files[0];
                                if (files.Length > 1)
                                    Log4Net.Info($"Meteor: ??{files.Length} 个路径中找到 PrinterInterfaceCLS.dll，选用 [{dllPath}]");
                                else
                                    Log4Net.Info($"Meteor: found PrinterInterfaceCLS.dll in subdirectory [{dllPath}]");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log4Net.Info($"Meteor: error searching in {dir}: {ex.Message}");
                        }
                    }
                }

                if (string.IsNullOrEmpty(dllPath))
                {
                    Log4Net.Info("Meteor: PrinterInterfaceCLS.dll not found in app dir or common C: paths. Set METEOR_HOME to install path if needed.");
                    return;
                }

                string dllDir = Path.GetDirectoryName(dllPath);
                // 在加载任??Meteor DLL 前必??SetDllDirectory，否则原??PrinterInterface.dll/PrintEngine.dll 无法被找??
                if (!string.IsNullOrEmpty(dllDir))
                {
                    if (SetDllDirectory(dllDir))
                        Log4Net.Info($"Meteor: SetDllDirectory=[{dllDir}] for native dependency loading");
                    else
                        Log4Net.Info($"Meteor: SetDllDirectory failed, error={Marshal.GetLastWin32Error()}");
                }
                // 收集可能包含依赖/实现类的目录：DLL 所在目??+ 多级父目??+ 已有搜索路径（避??DLL 在其他目录导致找不到??
                string[] probeDirs = GetProbeDirectories(dllDir).ToArray();
                Log4Net.Info($"Meteor: 程序集解析将搜索以下目录: [{string.Join("; ", probeDirs)}]");

                ResolveEventHandler resolveFromDllDir = null;
                if (probeDirs.Length > 0)
                {
                    resolveFromDllDir = (sender, args) =>
                    {
                        string simpleName = new AssemblyName(args.Name).Name;
                        foreach (string dir in probeDirs)
                        {
                            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                            string candidate = Path.Combine(dir, simpleName + ".dll");
                            if (File.Exists(candidate))
                            {
                                try
                                {
                                    return Assembly.LoadFrom(candidate);
                                }
                                catch { }
                            }
                        }
                        return null;
                    };
                    AppDomain.CurrentDomain.AssemblyResolve += resolveFromDllDir;
                }

                Assembly piAssembly = null;
                try
                {
                    piAssembly = Assembly.LoadFrom(dllPath);
                }
                catch (Exception ex)
                {
                    if (resolveFromDllDir != null) AppDomain.CurrentDomain.AssemblyResolve -= resolveFromDllDir;
                    Log4Net.Info($"Meteor: Assembly.LoadFrom failed [{dllPath}]: {ex.Message}");
                    return;
                }

                Type abstractType = piAssembly.GetType("Ttp.Meteor.PrinterInterfaceCLS")
                    ?? piAssembly.GetType("PrinterInterfaceCLS");
                if (abstractType == null)
                {
                    Type found = piAssembly.GetTypes().FirstOrDefault(t => t.Name == "PrinterInterfaceCLS");
                    if (found != null)
                        abstractType = found;
                }
                if (abstractType == null)
                {
                    Log4Net.Info($"Meteor: 程序集中未找??PrinterInterfaceCLS 类型。程序集内类?? [{string.Join(", ", piAssembly.GetTypes().Select(t => t.FullName).Take(10))}]...");
                    return;
                }

                Type[] allInAsm = piAssembly.GetTypes();
                Log4Net.Info($"Meteor: PrinterInterfaceCLS.dll 内共 {allInAsm.Length} 个类?? [{string.Join("; ", allInAsm.Select(t => t.Name + (t.IsAbstract ? "(抽象)" : "")))}]");

                // PrinterInterfaceCLS ??DLL 中为抽象类，需获取其非抽象子类并实例化
                _printerInterfaceType = null;
                _printerInterfaceInstance = null;

                if (abstractType.IsAbstract)
                {
                    Type[] concreteTypes = allInAsm
                        .Where(t => !t.IsAbstract && !t.IsInterface && abstractType.IsAssignableFrom(t))
                        .ToArray();
                    Log4Net.Info($"Meteor: 找到 {concreteTypes.Length} 个可实例化子?? [{string.Join(", ", concreteTypes.Select(x => x.FullName))}]");

                    foreach (Type t in concreteTypes)
                    {
                        if (_printerInterfaceInstance != null) break;
                        try
                        {
                            _printerInterfaceInstance = Activator.CreateInstance(t);
                            _printerInterfaceType = t;
                            Log4Net.Info($"Meteor: created instance of concrete type [{t.FullName}]");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Log4Net.Info($"Meteor: skip type {t.FullName}, CreateInstance failed: {ex.Message}");
                        }
                        // 尝试带参构造函数：无参、string、int ??
                        foreach (ConstructorInfo ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (_printerInterfaceInstance != null) break;
                            ParameterInfo[] ps = ctor.GetParameters();
                            object[] args = new object[ps.Length];
                            for (int i = 0; i < ps.Length; i++)
                            {
                                Type p = ps[i].ParameterType;
                                if (p == typeof(string)) args[i] = "";
                                else if (p == typeof(IntPtr)) args[i] = IntPtr.Zero;
                                else if (p == typeof(int)) args[i] = 0;
                                else if (p == typeof(uint)) args[i] = 0u;
                                else if (p.IsValueType && Nullable.GetUnderlyingType(p) == null) try { args[i] = Activator.CreateInstance(p); } catch { args[i] = null; }
                                else args[i] = null;
                            }
                            try
                            {
                                _printerInterfaceInstance = ctor.Invoke(args);
                                _printerInterfaceType = t;
                                Log4Net.Info($"Meteor: created instance via ctor({string.Join(", ", ps.Select(x => x.ParameterType.Name))})");
                                break;
                            }
                            catch (Exception ex)
                            {
                                Log4Net.Info($"Meteor: ctor({string.Join(", ", ps.Select(x => x.Name))}) failed: {ex.Message}");
                            }
                        }
                        if (_printerInterfaceInstance != null) break;
                    }

                    if (_printerInterfaceInstance == null)
                    {
                        // 备??：静态属??Instance / Default / Current
                        foreach (string propName in new[] { "Instance", "Default", "Current", "Singleton" })
                        {
                            PropertyInfo prop = abstractType.GetProperty(propName, BindingFlags.Public | BindingFlags.Static);
                            if (prop != null && prop.CanRead && abstractType.IsAssignableFrom(prop.PropertyType))
                            {
                                try
                                {
                                    _printerInterfaceInstance = prop.GetValue(null);
                                    if (_printerInterfaceInstance != null)
                                    {
                                        _printerInterfaceType = abstractType;
                                        Log4Net.Info($"Meteor: 通过静态属??{abstractType.Name}.{propName} 获取实例");
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log4Net.Info($"Meteor: {propName} get failed: {ex.Message}");
                                }
                            }
                        }
                        // 备??：抽象类上的静态工厂（无参 + 带参；string 参数传入 dllDir 便于 SDK 加载原生依赖??
                        if (_printerInterfaceInstance == null)
                        foreach (string methodName in new[] { "Create", "GetInstance", "CreateInstance" })
                        {
                            if (_printerInterfaceInstance != null) break;
                            foreach (MethodInfo factory in abstractType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == methodName && abstractType.IsAssignableFrom(m.ReturnType)))
                            {
                                ParameterInfo[] fp = factory.GetParameters();
                                object[] fargs = new object[fp.Length];
                                for (int i = 0; i < fp.Length; i++)
                                {
                                    Type p = fp[i].ParameterType;
                                    if (p == typeof(string)) fargs[i] = string.IsNullOrEmpty(dllDir) ? "" : dllDir;
                                    else if (p == typeof(int) || p == typeof(uint)) fargs[i] = 0;
                                    else if (p.IsValueType && Nullable.GetUnderlyingType(p) == null) try { fargs[i] = Activator.CreateInstance(p); } catch { fargs[i] = null; }
                                    else fargs[i] = null;
                                }
                                try
                                {
                                    _printerInterfaceInstance = factory.Invoke(null, fargs);
                                    _printerInterfaceType = abstractType;
                                    Log4Net.Info($"Meteor: created via {abstractType.Name}.{methodName}({string.Join(", ", fp.Select(x => x.ParameterType.Name))})");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Log4Net.Info($"Meteor: {methodName}(...) failed: {ex.Message}");
                                }
                            }
                        }
                        if (_printerInterfaceInstance == null)
                        {
                            // 实现在其他目录的 .NET DLL 中：??DLL 所在目录及父目录、Meteor 搜索路径下查找（跳过原生 DLL??
                            var otherDlls = new List<string>();
                            foreach (string dir in probeDirs)
                            {
                                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                                try
                                {
                                    foreach (string f in Directory.GetFiles(dir, "*.dll"))
                                    {
                                        if (string.Equals(Path.GetFileName(f), "PrinterInterfaceCLS.dll", StringComparison.OrdinalIgnoreCase)) continue;
                                        try { AssemblyName.GetAssemblyName(f); otherDlls.Add(f); } catch { }
                                    }
                                }
                                catch { }
                            }
                            otherDlls = otherDlls.Distinct().ToList();
                            Log4Net.Info($"Meteor: found {otherDlls.Count} .NET DLLs under {probeDirs.Length} probe directories; trying to create PrinterInterfaceCLS instance");
                            foreach (string otherPath in otherDlls)
                                {
                                    if (_printerInterfaceInstance != null) break;
                                    try
                                    {
                                        Assembly otherAsm = Assembly.LoadFrom(otherPath);
                                        foreach (Type ot in otherAsm.GetTypes())
                                        {
                                            if (ot.IsAbstract || ot.IsInterface) continue;
                                            MethodInfo openMi = ot.GetMethod("PiOpenPrinter", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                                            if (openMi == null) continue;
                                            if (!abstractType.IsAssignableFrom(ot))
                                            {
                                                if (ot.GetMethod("PiSetHeadPower", BindingFlags.Public | BindingFlags.Instance) == null) continue;
                                            }
                                            try
                                            {
                                                _printerInterfaceInstance = Activator.CreateInstance(ot);
                                                _printerInterfaceType = ot;
                                                Log4Net.Info($"Meteor: 从同目录 [{Path.GetFileName(otherPath)}] 创建实例 [{ot.FullName}]");
                                                break;
                                            }
                                            catch (Exception ex)
                                            {
                                                Log4Net.Info($"Meteor: ??{Path.GetFileName(otherPath)}.{ot.Name} CreateInstance 失败: {ex.Message}");
                                            }
                                        }
                                    }
                                    
                                    catch (Exception ex)
                                    {
                                        Log4Net.Info($"Meteor: 加载同目??DLL [{Path.GetFileName(otherPath)}] 失败: {ex.Message}");
                                    }
                                }
                            }
                            if (_printerInterfaceInstance == null)
                                Log4Net.Info("Meteor: failed to create PrinterInterfaceCLS instance from discovered assemblies");
                        }
                    }
                
                else
                {
                    try
                    {
                        _printerInterfaceInstance = Activator.CreateInstance(abstractType);
                        _printerInterfaceType = abstractType;
                    }
                    catch (Exception ex)
                    {
                        Log4Net.Info($"Meteor: 直接创建抽象类实例失?? {ex.Message}");
                    }
                }

                _runtimeReady = _printerInterfaceInstance != null;
                if (!_runtimeReady && !string.IsNullOrEmpty(dllDir))
                    TryNativePrinterInterfacePath(dllDir);
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor runtime load exception: {ex}");
                _runtimeReady = false;
            }
        }

        /// <summary>??.NET 无可用实例时，用原生 PrinterInterface.dll。说明书：接口以 PrinterInterface.dll ??PrinterInterfaceCLS.dll 提供；PrintEngine 为核心，原生 DLL 可能??Api ??PrintEngine 目录。多目录尝试并支持本进程内启动引擎??/summary>
        private static void TryNativePrinterInterfacePath(string dllDir)
        {
            string[] candidates = GetNativePrinterInterfaceCandidateDirs(dllDir);
            foreach (string dir in candidates)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    bool hasDll = File.Exists(Path.Combine(dir, "PrinterInterface.dll"));
                    Log4Net.Info($"Meteor: 尝试原生路径 目录=[{dir}] PrinterInterface.dll 存在={hasDll}");
                    if (!SetDllDirectory(dir))
                        continue;
                    int ret = NativePiOpenPrinter();
                    if (ret == RVAL_OK)
                    {
                        _runtimeReady = true;
                        _useNativePath = true;
                        _printerOpened = true;
                        _nativePrinterInterfaceDir = dir;
                        Log4Net.Info($"Meteor: 已连接已??PrintEngine（原??DLL），目录=[{dir}]");
                        TryMeteorSwathConnect(dir);
                        return;
                    }
                    if (ret == RVAL_NO_PRINTER)
                    {
                        string configPath = GetMeteorConfigPath(dir);
                        int startRet = NativePiStartPrintEngine(configPath ?? "");
                        if (startRet == RVAL_OK)
                        {
                            ret = NativePiOpenPrinter();
                            if (ret == RVAL_OK)
                            {
                                _runtimeReady = true;
                                _useNativePath = true;
                                _printerOpened = true;
                                _nativePrinterInterfaceDir = dir;
                                Log4Net.Info($"Meteor: 已在本进程内启动 PrintEngine 并连接（无需外部厂商程序），配置=[{configPath ?? "默认"}]，目??[{dir}]");
                                TryMeteorSwathConnect(dir);
                                return;
                            }
                        }
                        // 原生 DLL 已成功加载（PiOpenPrinter 返回 0x12=无引擎），仅引擎未启动；不再尝试其他目录，避免误报“均失败??
                        if (hasDll)
                        {
                            _runtimeReady = true;
                            _useNativePath = true;
                            _printerOpened = false;
                            _nativePrinterInterfaceDir = dir;
                            Log4Net.Info($"Meteor: native PrinterInterface.dll loaded from [{dir}], but PrintEngine not ready; PiStartPrintEngine returned {startRet}");
                            return;
                        }
                        Log4Net.Info($"Meteor: PiStartPrintEngine returned {startRet}, config=[{configPath ?? "default"}]");
                    }
                    else
                        Log4Net.Info($"Meteor: native PiOpenPrinter failed, ret={ret}, dir=[{dir}]");
                }
                catch (DllNotFoundException ex)
                {
                    Log4Net.Info($"Meteor: 目录 [{dir}] 下原??PrinterInterface.dll 未找到或依赖缺失: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log4Net.Info($"Meteor: TryNativePrinterInterfacePath 目录=[{dir}] 异常: {ex.Message}");
                }
            }
            Log4Net.Info("Meteor: all native PrinterInterface.dll candidate directories failed");
        }

        /// <summary>说明书：打印引擎为核心，接口??PrinterInterface.dll ??PrinterInterfaceCLS.dll。优先尝试厂商标准安装路径??/summary>
        private static readonly string DefaultMeteorApiAmd64 = @"C:\Program Files\Meteor Inkjet\Meteor\Api\amd64";

        /// <summary>返回待尝试的原生 PrinterInterface.dll 所在目录列表，优先使用 C:\Program Files\Meteor Inkjet\Meteor\Api\amd64??/summary>
        private static string[] GetNativePrinterInterfaceCandidateDirs(string apiDllDir)
        {
            var list = new List<string>();
            if (Directory.Exists(DefaultMeteorApiAmd64))
                list.Add(DefaultMeteorApiAmd64);
            if (!string.IsNullOrEmpty(apiDllDir) && !list.Contains(apiDllDir))
                list.Add(apiDllDir);
            try
            {
                string root = !string.IsNullOrEmpty(apiDllDir)
                    ? Path.GetFullPath(Path.Combine(apiDllDir, "..", ".."))
                    : Path.GetFullPath(Path.Combine(DefaultMeteorApiAmd64, "..", ".."));
                string peAmd64 = Path.Combine(root, "PrintEngine", "amd64");
                string pe = Path.Combine(root, "PrintEngine");
                if (Directory.Exists(peAmd64) && !list.Contains(peAmd64)) list.Add(peAmd64);
                if (Directory.Exists(pe) && !list.Contains(pe)) list.Add(pe);
                if (!list.Contains(root)) list.Add(root);
            }
            catch { }
            return list.ToArray();
        }

        /// <summary>Monitor 常用配置路径，优先使用（若存在）??/summary>
        private static readonly string MonitorConfigPath = @"C:\Users\Public\Documents\Meteor\Config\PccE\DefaultStarfire_PccE.cfg";

        /// <summary>??Meteor 安装目录下查找配置文件（.cfg），??PiStartPrintEngine 使用。优先使??Monitor 下的 DefaultStarfire_PccE.cfg，否则在安装目录/SW/PrintEngine 下查找??/summary>
        private static string GetMeteorConfigPath(string apiDllDir)
        {
            try
            {
                if (!string.IsNullOrEmpty(MonitorConfigPath) && File.Exists(MonitorConfigPath))
                {
                    Log4Net.Info($"Meteor: 使用 Monitor 配置路径 [{MonitorConfigPath}]");
                    return MonitorConfigPath;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(apiDllDir)) return null;
            try
            {
                string meteorRoot = Path.GetFullPath(Path.Combine(apiDllDir, "..", ".."));
                foreach (string name in new[] { "Meteor.cfg", "PrintEngine.cfg", "default.cfg" })
                {
                    string p = Path.Combine(meteorRoot, name);
                    if (File.Exists(p)) return p;
                }
                string sw = Path.Combine(meteorRoot, "SW");
                if (Directory.Exists(sw))
                {
                    string[] cfgs = Directory.GetFiles(sw, "*.cfg", SearchOption.TopDirectoryOnly);
                    if (cfgs.Length > 0) return cfgs[0];
                }
                string pe = Path.Combine(meteorRoot, "PrintEngine");
                if (Directory.Exists(pe))
                {
                    string[] cfgs = Directory.GetFiles(pe, "*.cfg", SearchOption.TopDirectoryOnly);
                    if (cfgs.Length > 0) return cfgs[0];
                }
            }
            catch { }
            return null;
        }

        /// <summary>Swath 扫描引擎接口在独??DLL 中，当前未调用；仅喷头上??闪喷时无需此接口??/summary>
        private static void TryMeteorSwathConnect(string dir)
        {
            Log4Net.Info("Meteor: MeteorSwathConnect not called; scan-engine DLL is separate and head power/flash are unaffected");
        }

        private static void TryMeteorSwathDisconnect()
        {
            // 未调??MeteorSwathDisconnect，无需操作??
        }

        private static bool EnsurePrinterOpenedInternal()
        {
            lock (SyncRoot)
            {
                if (!_runtimeReady)
                    return false;
                if (_useNativePath)
                {
                    if (_printerOpened)
                        return true;
                    return TryEnsureNativePrinterOpened();
                }
                if (_printerInterfaceType == null || _printerInterfaceInstance == null)
                    return false;

                if (_printerOpened)
                    return true;

                try
                {
                    MethodInfo openMethod = _printerInterfaceType.GetMethod("PiOpenPrinter", BindingFlags.Public | BindingFlags.Instance);
                    if (openMethod == null)
                    {
                        Log4Net.Info("Meteor API not ready: PiOpenPrinter method missing.");
                        return false;
                    }

                    object result = openMethod.Invoke(_printerInterfaceInstance, null);
                    int openCode = Convert.ToInt32(result);
                    if (openCode == 0)
                    {
                        _printerOpened = true;
                        return true;
                    }

                    Log4Net.Info($"Meteor PiOpenPrinter failed, return code: {openCode}");
                    return false;
                }
                catch (Exception ex)
                {
                    Log4Net.Info($"Meteor PiOpenPrinter exception: {ex}");
                    return false;
                }
            }
        }

        private static bool TryEnsureNativePrinterOpened()
        {
            try
            {
                if (!string.IsNullOrEmpty(_nativePrinterInterfaceDir) && Directory.Exists(_nativePrinterInterfaceDir))
                    SetDllDirectory(_nativePrinterInterfaceDir);

                int ret = NativePiOpenPrinter();
                if (ret == RVAL_OK)
                {
                    _printerOpened = true;
                    Log4Net.Info("Meteor: native retry PiOpenPrinter succeeded");
                    return true;
                }

                if (ret == RVAL_NO_PRINTER)
                {
                    string configPath = GetMeteorConfigPath(_nativePrinterInterfaceDir);
                    int startRet = NativePiStartPrintEngine(configPath ?? "");
                    Log4Net.Info($"Meteor: native retry PiStartPrintEngine returned {startRet}, config=[{configPath ?? "default"}]");
                    if (startRet == RVAL_OK)
                    {
                        ret = NativePiOpenPrinter();
                        if (ret == RVAL_OK)
                        {
                            _printerOpened = true;
                            Log4Net.Info("Meteor: native retry start PrintEngine then PiOpenPrinter succeeded");
                            return true;
                        }
                    }
                }

                Log4Net.Info($"Meteor: native retry open PrintEngine failed, PiOpenPrinter returned {ret}");
            }
            catch (Exception ex)
            {
                Log4Net.Info($"Meteor: TryEnsureNativePrinterOpened exception: {ex.Message}");
            }

            _printerOpened = false;
            return false;
        }
    }
}
