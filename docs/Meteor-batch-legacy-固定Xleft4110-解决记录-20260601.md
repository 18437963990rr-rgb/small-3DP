# Meteor Legacy Batch + 固定 Xleft 4110 — 修改过程与解决记录（2026-06-01）

> **文档版本：** 2026-06-01  
> **适用项目：** LaserAdd_3DP 主控 + Meteor PCC-E / Starfire  
> **涉及模块：** `4-MeteorPrintEngine.cs`、`5-SharpControl.cs`、`手动操作界面.cs`  
> **前置阅读（按时间）：**  
> - [Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md](./Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md)（层间左偏闭环，建议与本篇一并阅读）  
> - [Meteor-STARTSCAN-PD1提前触发排查记录-20260529.md](./Meteor-STARTSCAN-PD1提前触发排查记录-20260529.md)  
> - [Meteor-3PASS-同Job窗口与REV续查记录-20260528.md](./Meteor-3PASS-同Job窗口与REV续查记录-20260528.md)  
> - [Meteor-Pass1-Pass2恢复排查与解决记录.md](./Meteor-Pass1-Pass2恢复排查与解决记录.md)  

---

## 1. 本文档范围

自 **2026-05-29** 文档（STARTSCAN 后 PD1 提前触发、回归 batch 方向）之后，到 **2026-06-01** 现场验证 **Pass0/1/2 均可出图** 为止的：

- 问题现象与根因归纳  
- 修改思路（为何 batch + 固定 Xleft 配套）  
- 代码改动要点与环境变量  
- 日志验收标准  
- 尚未纳入本版的长期项（PCC AbsX=0 原点等）  

**结论摘要：** 量产基线定为 **Legacy batch 预灌（defer + Flush）+ 发完延迟再动 + 固定 FWD Xleft=4110 + unified 基准**；**525mm** 高端停靠保留（供应商「太极限」指旧 **485mm**，非 525）。

---

## 2. 接续背景：05-29 之后仍卡在哪

### 2.1 05-29 文档已确认的方向

| 项 | 状态 |
|----|------|
| 同一 `STARTJOB` 内 3 条 swath | 继续作为主路径 |
| `METEOR_BATCH_SWATH_MODE` 默认 true | 保持 |
| `METEOR_BATCH_STARTSCAN_GATE_SPLIT` 默认 false | Legacy 连续预灌 |
| IMAGE 先打包再连续 `STARTSCAN→IMAGE→ENDDOC` | 已做 |
| Pass0 等「整批 swath 入队」再放行扫程 | 已做 |
| STARTSCAN 静止即 PD1 | 硬件/等待位仍待现场；软件用「先灌满再动」规避 |

### 2.2 05-29 之后仍失败的主要现象

| 现象 | 说明 |
|------|------|
| Pass0/1 有图，**Pass2 无图** | batch 时序改善后仍偶发 |
| 日志 Xleft 出现 **3482 / 2835 / 7187** 等 | 与期望 **4110 / 8462** 不一致 |
| Pass1 曾写坏 unified 基准 | batch 排队时 Pass1 仍走「首条 STARTSCAN」捕获，用 live/`GetImageXStartBasePixels`（≈2835）覆盖 Pass0 锁定的 fwdBase |
| Pass1 回程 AbsX 未明显 **&lt; Pass0 起点** | 供应商要求越过起点才触发 Pass2；与坐标、行程有关 |
| 误将 **525mm** 改回 485 | 现场澄清：485 对 PCC 余量不足；**525 保留** |

### 2.3 坐标系澄清（避免后续文档再混）

| 概念 | 含义 |
|------|------|
| **固高停靠 mm**（如 525→15） | 墨车机械坐标；与 PCC **无硬同步** |
| **PCC AbsXCount** | Meteor 编码器像素域；`Xleft` 落在此域 |
| **4110 px** | 现场标定的 FWD 发图左缘（≈261mm@400dpi），来自历史 485mm 停靠 + 固定像素试验，**不是** cfg 里单独一项 |
| **用户说的「原点」** | 指 **PCC AbsXCount=0**；与 525mm 停靠 **不是同一概念**，需另做 Home/绑定设计 |

---

## 3. 思路演进（按决策顺序）

### 3.1 时序：供应商「原点发完全部 swath → 再延迟 → 再动」

**问题：** 若在 Pass0 扫程中才插入 Pass1/2 的 `STARTSCAN`，或首条 swath 后即动，易出现 PD/窗口与物理扫程错位（与 05-29 PD 提前触发讨论一致）。

**做法：**

1. **Legacy batch defer**（`ShouldDeferLegacyBatchSwathSend`）：`WriteImageLayer` 只 **Queued**，不立即 `DoSendCommand(STARTSCAN)`。  
2. 三条 strip 渲染结束后，`5-SharpControl` 调用 `SignalBatchSwathsMeteorReady` → `FlushDeferredLegacyBatchSwaths` **连续**下发 3×（STARTSCAN+IMAGE+ENDDOC）。  
3. `WaitPass0FirstSwathMeteorReady`：在 `FlushEnd` 后可选 **`METEOR_BATCH_POST_SEND_DEPART_DELAY_MS`（默认 1000ms）**，再允许打印线程 `BackToStation` 启动 Pass0 扫程。

```text
数据线程（Pass0 门控后）          打印线程
  Queued pass0/1/2（内存）           525mm 停靠 / 等 ready
  Flush 连续 3 条 ENDDOC      →     BatchPostSendDepartDelay 1s
  Signal Pass0 ready          →     485→15（或 525→15）扫程
```

### 3.2 空间：batch 预灌不能用「扫程中的 live AbsX」

**问题：** Pass1/2 在 Pass0 **尚未动或刚 Flush** 时已写入 PCC；若 `imageCmdXStart` 取发图瞬间 live AbsX（曾约 **3482**），Pass2 会与 Pass0 扫程中漂移的编码器不一致 → **右下角错位 / Pass2 无图**。

**做法：**

- `ShouldUseUnifiedAlignImageXStart()`：batch 模式下 **强制 true**。  
- Pass0 首次捕获 `JobUnifiedAlignFwdBase`（**仅 `pendingPass==0`**，避免 Pass1 覆盖）。  
- Pass1/2 走 `batch_unifiedRev_fwdBasePlusWidth` / `batch_unifiedFwd_jobBase`（见 `ResolveHiPrintCompatImageCmdXStart`）。

### 3.3 标定：固定 FWD Xleft = 4110（默认，可 env 关闭）

**问题：** live 3482 随停靠/ Home 变化；`jobNprtXEncPos` 换算的 hiPrintBase（≈2835）是 **另一套域**，不能当 batch 的 unified 基准。

**做法：**

- `TryGetForcedImageXStartPixels`：**默认 `defaultForcedFwdXStartPx = 4110`**（`METEOR_IMAGE_XSTART_FIXED_FWD_PX=-1` 可关）。  
- Pass0 FWD 在 `ResolveHiPrintCompat` 中优先 **`pass0_fixedFwdPx`**。  
- 捕获 unified 时：若 forced FWD 有效，则 **`JobUnifiedAlignFwdBase captured=4110`**，勿用 `imageXStartBasePixels` 的 live 值。

**当前测试图宽（临时）：** `5-SharpControl` 裁剪 **4323px**，Meteor padding 后 **4352px** → Pass1 REV Xleft = **4110+4352=8462**。

| Pass | 方向 | Xleft（当前默认） | anchor 日志关键字 |
|------|------|-------------------|-------------------|
| 0 | FWD | **4110** | `pass0_fixedFwdPx` |
| 1 | REV | **8462** | `batch_unifiedRev_fwdBasePlusWidth` |
| 2 | FWD | **4110** | `batch_unifiedFwd_jobBase` |

### 3.4 行程：525mm 高端

- `GetInkCarScanApproachHighEndMm` / `GetInkCarScanHighEndMm` 默认 **525.0**。  
- 供应商「太极限」针对的是旧 **485mm** 余量不足，**不要把 525 改回 485** 除非重新标定 Xleft 与 PD 窗口。

### 3.5 设计关系：batch 与固定 Xleft 是否「合适」？

二者解决 **不同维度**，当前组合是 **配套** 而非凑合：

| 维度 | Batch legacy | 固定 4110 + unified |
|------|----------------|---------------------|
| 解决什么 | **何时**把 3 条 swath 交给 PCC；避免扫程中插入 STARTSCAN | **何处**在 AbsX 域开窗；预灌时禁止 live 漂移 |
| 若不 batch | 可逐 pass 门控 + 仍可用 4110；但不再符合「原点一次灌满」 | 仍建议每 pass 发图时用锚点/固定，而非扫程中 live |
| 若不固定 | batch 下 Pass1/2 极易用错 Xleft | — |

**量产建议：** 保持 `METEOR_BATCH_SWATH_MODE=1` + 默认 4110；对比实验可用 `METEOR_BATCH_SWATH_MODE=0` 验证逐 pass 是否同样稳定（见 §8）。

---

## 4. 代码改动清单（相对 05-29 基线）

### 4.1 `4-MeteorPrintEngine.cs`

| 改动 | 说明 |
|------|------|
| `DeferredLegacyBatchSwath` + `_deferredLegacyBatchSwaths` | Queued / FlushBegin / FlushEnd 日志 |
| `ShouldDeferLegacyBatchSwathSend()` | batch 且非 gate-split 且非 split-job-per-pass |
| `SignalBatchSwathsMeteorReady` | Render 完成后 Flush + `SignalPass0FirstSwathMeteorReady` |
| `GetBatchPostSendDepartDelayMs` + `WaitPass0FirstSwathMeteorReady` | Flush 后默认 sleep 1000ms |
| `JobUnifiedAlignFwdBase` 捕获 | **仅 pass0**；优先 `TryGetForcedImageXStartPixels(SD_FWD)` |
| `TryGetForcedImageXStartPixels` | 默认 FWD=**4110**，REV=-1（不强制，走 unified+width） |
| `ResolveHiPrintCompat` pass0 FWD | 优先 `pass0_fixedFwdPx` |
| `GetInkCarScan*HighEndMm` | 默认 **525** |
| `WaitPassSwathMeteorReady` | legacy batch 下跳过 Pass1/2 等待 |

### 4.2 `5-SharpControl.cs`（SimplifiedMeteorAutoFlowMode）

| 改动 | 说明 |
|------|------|
| BATCH-LEGACY 分支 | Pass0 门控后连续 `WriteImgLayerData`，`SuppressScanMotionGate` pass1/2 |
| `SignalBatchSwathsMeteorReady("...LegacyBatchAllSwathsEndDoc")` | 三条 strip 渲染完后 Flush |
| `temporaryMeteorTestWidthPx = 4323` | 临时裁宽（调试期） |
| `MarkDeferredEndJobAfterPassSwaths` | EndJob 仍默认延至 Pass2 物理扫程结束 |

### 4.3 `手动操作界面.cs`（运动线程）

| 改动 | 说明 |
|------|------|
| Pass0 | 高端 **525mm** 停靠 → `WaitPass0FirstSwathMeteorReady`（含 batch 延迟）→ 再 `BackToStation` 扫程 |
| Pass1/2 | legacy batch 下 `WaitPassSwathMeteorReady` 已短路；物理扫程仍按原 3PASS 序列 |

### 4.4 已修复的逻辑 Bug（文档化便于 Code Review）

**`JobUnifiedAlignFwdBase` 被 Pass1 覆盖：**

- **旧行为：** batch 排队时 Pass1 仍满足 `needFirstHomeAfterStartScan`，在 Pass1 的 `WriteImageLayer` 里再次捕获 unified，把 `_jobUnifiedImageXStartAbsX` 写成 **2835**（encPos 换算）或 live 值。  
- **结果：** Pass1 Xleft≈7187、Pass2≈2835，Pass2 无图。  
- **现行为：** 仅 `pendingPass == 0 && !_jobUnifiedImageXStartValid` 捕获；基准 **4110**。

---

## 5. 默认模式与环境变量速查

### 5.1 推荐量产默认（代码内 default）

```text
METEOR_BATCH_SWATH_MODE          → true（未设 env 时）
METEOR_BATCH_STARTSCAN_GATE_SPLIT → false
METEOR_SPLIT_JOB_PER_PASS      → false
METEOR_BATCH_POST_SEND_DEPART_DELAY_MS → 1000（0=关闭延迟）
METEOR_IMAGE_XSTART_FIXED_FWD_PX → 代码默认 4110（env 未设时）
METEOR_INKCAR_SCAN_APPROACH_HIGH_MM / HIGH_END_MM → 525（代码默认）
EndJob                           → 延后至 Pass2 扫程结束（非 BATCH_ENDJOB_IMMEDIATE）
```

### 5.2 诊断 / 对比实验

| 变量 | 用途 |
|------|------|
| `METEOR_BATCH_SWATH_MODE=0` | 逐 pass 门控，对比 batch 是否必要 |
| `METEOR_BATCH_STARTSCAN_GATE_SPLIT=1` | 预切缓存，按 pass gate 发 STARTSCAN（非 legacy） |
| `METEOR_SPLIT_JOB_PER_PASS=1` | 每 pass 独立 STARTJOB/ENDJOB |
| `METEOR_IMAGE_XSTART_FIXED_FWD_PX=-1` | 关闭默认 4110，回到 live/公式 |
| `METEOR_IMAGE_XSTART_FROM_LIVE_ABS=0` | 关闭 live FWD（与 forced 联调） |
| `METEOR_BATCH_POST_SEND_DEPART_DELAY_MS=0` | 去掉发完后的 1s 等待 |

运行时可看：`MeteorPrintEngine.DescribeBatchSwathModeConfig()` 及 Render 日志中的 `BATCH-LEGACY` / `BATCH-GATE-SPLIT` 字样。

---

## 6. 日志验收标准（通过后视为本阶段闭环）

### 6.1 时序链

```text
[MeteorBatchDefer] Queued passIndex=0/1/2
[MeteorBatchDefer] FlushBegin … swathCount=3
[MeteorBatchDefer] FlushSwathDone passIndex=0/1/2
[MeteorBatchDefer] FlushEnd
[MeteorScanGate] Pass0FirstSwathWaitEnd released=true
[MeteorScanGate] BatchPostSendDepartDelay begin delayMs=1000
[MeteorScanGate] BatchPostSendDepartDelay end
（此后才出现 Pass0 固高运动 / BackToStation）
```

### 6.2 Xleft 链

```text
[MeteorXStart] JobUnifiedAlignFwdBase captured=4110 passIndex=0
[MeteorXStart] HiPrintCompat … anchor=pass0_fixedFwdPx imageCmdXStart=4110
… passIndex=1 anchor=batch_unifiedRev_fwdBasePlusWidth imageCmdXStart=8462
… passIndex=2 anchor=batch_unifiedFwd_jobBase imageCmdXStart=4110
```

可选：`[MeteorXStart] FixedOverride`（若 env 覆盖 forced 时出现）。

### 6.3 运动 / 双坐标

```text
[MeteorDualCoord] Pass0AtApproachHold googolXmm≈525 …
[MeteorEncoderDiag] Pass0AfterScan / Pass1AfterScan / Pass2AfterScan
```

**Pass1 回程：** 关注 Pass1 结束后 `absX24` 是否 **小于 4110**（供应商「越过 Pass0 起点」）；若长期不满足，需机械行程或 REV Xleft 微调，而非回退 batch。

---

## 7. 时序简图（Legacy batch + defer + 4110，2026-06-01）

```text
打印线程                          数据线程（Raster）
    │                                 │
    ├─ 525mm 停靠（ApproachHold）      │
    ├─ Signal Pass0 门控 ────────────►│ WaitPassGate Pass0
    │                                 ├─ WriteImg×3 → Queued（Xleft 已算好）
    │                                 ├─ SignalBatchSwathsMeteorReady
    │                                 │     Flush 3× STARTSCAN+IMAGE+ENDDOC
    │                                 │     （4110 / 8462 / 4110）
    ├─ WaitPass0FirstSwathReady       │
    ├─ BatchPostSendDepartDelay 1s    │
    ├─ 525→15 扫程（Pass0 物理）       │  PCC 内 3 doc 已 armed
    ├─ Pass1 REV 扫程                 │
    ├─ Pass2 FWD 扫程                 │
    └─ TryCompleteDeferredEndJob      │
```

---

## 8. 后续工作（未在本阶段 closure）

| 优先级 | 项 | 说明 |
|--------|-----|------|
| P1 | 去掉临时 `temporaryMeteorTestWidthPx=4323` | 恢复整板宽后重标 4110 或改为公式 |
| P1 | Pass1 回程 AbsX &lt; 4110 实测统计 | 与 Pass2 触发稳定性相关 |
| ~~P2~~ | **PCC 层间 AbsX 对齐** | **已闭环（2026-06-04）**：750mm 等待位 `PiSetHome` + Preheat 时序，见 [层间错位记录](./Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md)；恢复 7323 宽时仍需复核 4110 |
| P2 | `METEOR_BATCH_SWATH_MODE=0` A/B | 验证「仅固定 4110、不 batch」是否同等稳定 |
| P3 | 05-29 PD 等待位 / PL7-2 硬件 | 与软件「先灌再动」并行推进 |
| P3 | `WaitPass0FirstSwathMeteorReady` 返回值检查 | 失败时不应静默开扫 |

---

## 9. 相关源码索引

| 主题 | 文件 | 入口 |
|------|------|------|
| Batch 开关 / defer / Flush | `4-MeteorPrintEngine.cs` | `IsBatchSwathModeEnabled`, `ShouldDeferLegacyBatchSwathSend`, `FlushDeferredLegacyBatchSwaths` |
| Xleft / unified / forced 4110 | 同上 | `TryGetForcedImageXStartPixels`, `ResolveHiPrintCompatImageCmdXStart`, `WriteImageLayer` |
| Render 触发 Flush | `5-SharpControl.cs` | `RenderToWic` → `SimplifiedMeteorAutoFlowMode` → `SignalBatchSwathsMeteorReady` |
| 运动与等待 | `手动操作界面.cs` | `AutoPrintThread5`、Pass0 `WaitPass0FirstSwathMeteorReady` |
| 525mm | `4-MeteorPrintEngine.cs` | `GetInkCarScanApproachHighEndMm`, `GetInkCarScanHighEndMm` |

---

## 10. 附录：与旧文档表述的差异

| 旧表述（05-27/05-28） | 本文档（06-01） |
|------------------------|----------------|
| 高端默认 485mm | 默认 **525mm**（现场余量） |
| fwdBase 来自 live ≈5115/3482 | 默认 **固定 4110** + unified |
| batch 在扫程内连续发 STARTSCAN | **defer：先 Queued，Render 末 Flush，再动** |
| 逐 pass 为主调试路径 | **Legacy batch 为当前通过验证的量产基线** |

---

*最后更新：2026-06-01 — Pass2 无图问题在 batch defer + unified 捕获修复 + 默认 Xleft 4110 后现场验证通过；并记录 batch 与固定坐标的配套关系。*
