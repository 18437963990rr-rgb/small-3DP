# Meteor 扫描打印调试记录 - 2026-07-09

本文档整理本轮对话中的问题、日志判断、根因分析、代码修改和后续调试建议。它不是逐字稿，而是面向后续继续调试的工程记录。

**最后更新**：2026-07-09 15:40（含 physical_home_fast 三轮实机 bug 迭代）

---

## 1. 本轮初始问题

用户反馈自动打印存在以下现象（在 465×370 mm 幅面、SwathImageSplitter 切片对齐已修复的前提下）：

1. **第二层 / Pass1 无法出图**：Pass0 能喷，Pass1 模拟/切片正常，但物理无墨。
2. **「切到PiSetHome固定XStart」按钮激活后仍无法节约时间**：墨车仍按各检查点停稳逻辑运行。
3. **修改后更严重**：出现不打印、墨车回清洗站（750 mm）、铺粉层循环直接进入下一层。
4. **用户明确需求**：复制一条与现有精扫链路**完全独立**的快速打印运动分支，通过按钮切换，不干扰现有逻辑。

---

## 2. 已确认技术结论

| 结论 | 说明 |
|------|------|
| 不必缩减 465 mm 幅面 | Pass0 全宽 7323 px 能出图；单程扫程 470 mm 足够 |
| Y 对齐已修复 | `SimplifiedMeteorAutoFlowMode` 主路径由 `PassPlatformYCrop` 换回 `SwathImageSplitter`（row0 向下 2048 行）后，UI/切片百分位已对齐 |
| passIndex 与物理 Y 带存在颠倒映射 | 属长期 Y 映射问题，非本轮 Pass1 无墨主因 |
| Pass1 无墨主因（精扫链） | 非幅面、非 `METEOR_PASS1_REV_XSTART_PX=10071` 标定错误；是 **Official Queued + BATCH-LEGACY 预灌** 与 **fastOfficialQueued 运动** 错位 |
| PiSetHome 按钮不是省时方案 | 该模式强化每层 663.5 mm Home + OfficialQueued 预灌，与「750→525 丝滑过 Home」快速链路方向相反 |
| 用户记忆中的最快链路 | 备份 `4-MeteorPrintEngine -稳定打印版本层间细微偏差.cs`：默认不发每层 PiSetHome，靠 750→525 物理过 Home + BATCH-LEGACY + 固定 Xleft |
| **快速分支不能用精扫 XStart** | 用户级 `METEOR_IMAGE_XSTART_FIXED_FWD_PX=4110` 等仅在 **PiSetHome 重置 AbsX 域** 后有效；`physical_home_fast` 须用 **live AbsX** |
| **Meteor 打印时序** | STARTSCAN 在静止点下发，喷墨发生在**之后** encoder 扫程运动；不得在 809→525 接近段提前放行 STARTSCAN |
| **引擎 AbsX 锚点 stage 名** | `LogDualCoordSnapshot` 仅认固定 stage 字符串（如 `Pass0AtScanLowEnd`），自定义后缀会导致 Pass1 REV 锚点丢失 |

---

## 3. Pass1 无墨（精扫链）：日志证据链

典型配置（PiSetHome 按钮激活后）：

```text
officialQueuedScanMode=True
BATCH-LEGACY
conservativeTiming=True
fastOfficialQueued=True（绑定精扫 UI 模式）
```

关键日志特征：

1. **预灌**：`FlushSwathDone pass0/1/2` 在 absX≈2182、车仍在 525 mm 侧一次性完成。
2. **Pass1**：`PassSpecificOverride forced=10071`，`encRange=[2748,10071]`，`setPixels>0`，数据侧正常。
3. **门控跳过**：`PassSwathWaitSkipped legacyBatchMode passIndex=1`。
4. **物理扫程**：Pass1 REV 15→525 mm 正常（absX 10214→2197），仍无喷。
5. **Pass0 扫完**：absX≈10163–10187，与 xStart=2748+7323 一致，说明 Pass0 FWD 触发窗口匹配。

判断：

- Pass0 FWD 在物理扫程中触发，能出图。
- Pass1 REV 的 swath 已在 525 mm 静止侧灌入 PCC，REV 扫程的 Product Detect / 播放窗口与灌图时 absX **不对齐**，物理在动但无墨。
- `fastOfficialQueued` 使 Pass1 **跳过** `SignalPrintThreadPassReady` / `WaitPassSwathMeteorReady`，完全依赖错误时刻的预灌。

---

## 4. 「PiSetHome固定XStart」按钮：为何不能省时

### 4.1 按钮实际配置的是「精扫数据链」，不是「快速运动链」

点按钮后 `ApplyMeteorPiSetHomePhysicalHomeFixedXStartMode()` 写入：

```text
METEOR_AUTO_PRINT_MOTION_BRANCH=precision_pisethome
METEOR_OFFICIAL_QUEUED_SCAN_MODE=1
METEOR_PER_LAYER_HOME_MODE=1
METEOR_PER_LAYER_HOME_MM=663.5
METEOR_PISET_HOME_AT_JOB_START=1
METEOR_INKCAR_CONSERVATIVE_TIMING=1
METEOR_PASS0/1/2 固定 XStart
```

UI 文案中的「运动快速分支」在代码里仅指 `IsInkCarFastOfficialQueuedMotionEnabled()`，且绑定精扫分支。

### 4.2 「快」只跳过扫程中的部分 Reach，省不掉 Home 停靠

`fastOfficialQueued` **仅**做这些事：

- Pass0 扫完不等 X 到扫程终点（`WaitInkCarXAxisAtScanEndpoint` 被绕过）。
- Pass1/2 不等 Y 到位、不等 per-pass swath 门控，直接异步发车。
- Pass1/2 的 `SignalPrintThreadPassReady` / `WaitPassSwathMeteorReady` 整段跳过。

**省不掉**的耗时大头：

```text
750 清洗站
  -> 663.5 mm 停稳（WaitInkCarXAxisStableForPrepAnchor，5~10 点采样）
  -> PiSetHome + StartJob 同步等待（最多 10 s）
  -> 525 mm 接近位
  -> Pass0/1/2 扫程
  -> Pass2 后回清洗站（稳妥模式 ReturnInkCarToCleanWaitStationAfterPass2）
```

### 4.3 修改前还有一个结构性问题（本轮已修）

在实现双分支之前，`AutoPrintThread5` Pass0 **无论按钮是否激活，都无条件走 663.5 + PiSetHome**。按钮只改 env，运动代码未按分支隔离。

---

## 5. 「不打印 → 回原点 → 直接下一层」的原因

三类机制叠加：

1. **数据/运动错位**：OfficialQueued 在 525 mm 侧预灌 3 swath → Pass0 可能出图 → Pass1/2 物理扫程无墨 → 运动线程认为本层扫程已执行 → 回 750 mm → 层循环照常推进。
2. **fastOfficialQueued 造成运动「假完成」**：Pass0 不等 X 到扫程终点就切 Y、进 Pass1。
3. **Pass0 在 663.5 mm 失败 → abort 但层循环不停**：`_meteorMotionAbortLayerK` + `EnsureInkCarAtCleanWaitStation`，Pass1/2 跳过，铺粉/层推进不一定停。

---

## 6. 双运动分支实现（代码）

### 6.1 设计目标

通过 `METEOR_AUTO_PRINT_MOTION_BRANCH` 切换三条路径，两按钮**互斥**，运行中禁止切换：

| 分支值 | 激活方式 | 运动特征 |
|--------|----------|----------|
| `physical_home_fast` | 「切到物理Home快速打印」 | 750→525 过 Home；无每层 663.5/PiSetHome；**BATCH=0 逐 pass 门控**；live AbsX |
| `precision_pisethome` | 「切到PiSetHome固定XStart」 | 663.5 Home + PiSetHome + OfficialQueued 预灌 + fastOfficialQueued |
| `default` | 两按钮均未激活 | 无 663.5 停靠；稳妥序（525 并行/停稳 + 逐 pass 门控） |

### 6.2 修改文件

#### `手动操作界面.cs`

- `RunAutoPrintThread5Pass0/1/2PhysicalHomeFast()`：快速分支完整 3-PASS 运动链。
- `MoveInkCarXToApproachAndWaitPass0SwathPhysicalFast()`：809→525 可与 StartJob 并行；**525 停稳后**再 Signal 放行 STARTSCAN。
- `WaitInkCarXNearScanEndpoint()` / `MeteorPhysicalHomeFastScanEndpointToleranceMm=2.5`：扫程终点容差（Pass0 回弹 1~2 mm）。
- `ClearMeteorImageXStartFixedProcessOverrides()`：进程内关闭用户级 `METEOR_IMAGE_XSTART_FIXED_*`。
- `AutoPrintThread5` Pass0/1/2 按分支 `break`；663.5 + PiSetHome **仅**在精扫分支执行。
- `ApplyMeteorPhysicalHomeFastPrintMode()` / `ApplyMeteorPiSetHomePhysicalHomeFixedXStartMode()` / 按钮互斥。

#### `手动操作界面.Designer.cs`

- PiSetHome 按钮下方新增「切到物理Home快速打印」（tabPage3）。

### 6.3 快速分支 env（当前最终版）

```text
METEOR_AUTO_PRINT_MOTION_BRANCH=physical_home_fast
METEOR_PISET_HOME_AT_JOB_START=0
METEOR_PER_LAYER_HOME_MODE=0
METEOR_OFFICIAL_QUEUED_SCAN_MODE=0
METEOR_INKCAR_CONSERVATIVE_TIMING=0
METEOR_BATCH_SWATH_MODE=0          ← 逐 pass 门控（Pass1 修复，非 BATCH 预灌）
METEOR_BATCH_POST_SEND_DEPART_DELAY_MS=0
METEOR_PASS0/1/2_*_XSTART_PX=0     ← 进程内关闭精扫 pass 固定 XStart
METEOR_IMAGE_XSTART_FIXED_FWD_PX=-1
METEOR_IMAGE_XSTART_FIXED_REV_PX=-1
METEOR_IMAGE_XSTART_FIXED_PX=-1     ← 关闭用户级 4110 等精扫标定值
```

**说明**：按钮写入 **进程级** env（`EnvironmentVariableTarget.Process`），Windows「用户环境变量」对话框里的值不会被按钮改掉；快速分支必须在进程内显式覆盖。

### 6.4 精扫分支 env

```text
METEOR_AUTO_PRINT_MOTION_BRANCH=precision_pisethome
（其余同原 PiSetHome 按钮：OfficialQueued=1, PER_LAYER_HOME=663.5, PiSetHome=1, PASS0/1/2 固定 XStart）
```

### 6.5 关键代码位置

| 功能 | 位置 |
|------|------|
| 快速 Pass0/1/2 | `手动操作界面.cs` — `RunAutoPrintThread5Pass*PhysicalHomeFast` |
| 525 门控 + swath 预载 | `MoveInkCarXToApproachAndWaitPass0SwathPhysicalFast` |
| 分支 env | `ApplyMeteorPhysicalHomeFastPrintMode` / `ApplyMeteorPiSetHomePhysicalHomeFixedXStartMode` |
| 精扫 Pass0 Home | `AutoPrintThread5` case 0 — `IsMeteorPrecisionPiSetHomeMotionBranch()` 块 |
| BATCH/门控 | `4-MeteorPrintEngine.cs` — `WaitPassSwathMeteorReady` / `FlushDeferredLegacyBatchSwaths` |
| Pass0 AbsX 锚点 | `4-MeteorPrintEngine.cs` — `LogDualCoordSnapshot("Pass0AtScanLowEnd", ...)` |

---

## 7. 切片与数据链（仍有效）

`5-SharpControl.cs`：

- `SimplifiedMeteorAutoFlowMode` 主路径：**SwathImageSplitter**（`nYJetOff=0`）。
- 保留 OfficialQueued、batch 门控、SPLIT-JOB-PER-PASS 等框架。

提醒：`nYJetOff` 必须为 0，否则不出图。

---

## 8. physical_home_fast 三轮实机 Bug 迭代（2026-07-09 下午）

### 8.1 Bug #1：Pass0 行程未完成、Y 轴乱飘（约 15:04）

**用户现象**：点击「物理 Home 快速打印」后 Pass0 扫程未完成，Y 轴乱飘。

**根因**：为省时去掉等停：

- Pass0/1/2 的 `BackToStation(..., waitStop=false)` 且无 `WaitInkCarX/YAxisReach`。
- Pass0 前 Y 准备也是 `waitStop=false`（116 mm 未到位就动 X）。
- Pass0 在 X 仍在 526 mm 时即记 `Pass0AtScanLowDepart`，随后发 Y 换带。

**日志特征**（15:04 段）：

```text
BackToStation ... WaitStopFlag=False
Pass0AtScanLowDepartPhysicalFast 时 googolXmm=526.x（未至 15mm）
Pass1 时 Y 在 102~104mm 漂移
```

**修复（第一轮）**：

- Pass0：525→15 后 `WaitInkCarXNearScanEndpoint(15mm)`；Y 换带 `WaitInkCarYAxisReach`。
- Pass1/2：各扫程终点 X 等待 + pass 间 Y 等停。
- Pass0 前 Y 准备：快速分支 `waitStop=true`。
- **保留**：525 并行 StartJob、无 663.5、BATCH=0。

---

### 8.2 Bug #2：Pass0 未打印、Pass1 卡住（约 15:18）

**用户现象**：Pass0 无墨；进入 Pass1 后线程卡住约 12 s 后 abort。

**根因 A — Pass0 时序仍错**：

- `SignalPrintThreadLayerPass0ReadyForMeteorSubmit` 在 **809→525 途中**即发出。
- STARTSCAN 在接近段发出（absX≈2085），真实 525→15 扫程时 swath 窗口已错过。

**根因 B — Pass1 容差过严**：

- Pass0 结束 X=13.124 mm（有效低位），Pass1 用 1 mm 容差等 15.000 mm → `delta=1.876` 超时 12 s → abort。

**修复（第二轮）**：

1. `MoveInkCarXToApproachAndWaitPass0SwathPhysicalFast` 改为：
   - 809→525 仅 `PreheatReady`（StartJob 可并行）；
   - **525 停稳**（`WaitInkCarXAxisReachWithProfileStopped`）后 `SignalPrintThreadLayerPass0ReadyForMeteorSubmit`；
   - `WaitPass0FirstSwathMeteorReady`（ENDDOC）后再启动 525→15 扫程。
2. 新增 `MeteorPhysicalHomeFastScanEndpointToleranceMm=2.5` / `WaitInkCarXNearScanEndpoint`；Pass1 入口不再因 13 mm vs 15 mm 卡死。

**Pass1 卡住日志**（15:18:52–15:19:03）：

```text
WaitInkCarXAxisReach: waiting, targetMm=15.000, currentMm=13.124, deltaMm=1.876
WaitInkCarXAxisReach: timeout ... toleranceMm=1.000
AutoPrintThread5[physical_home_fast]: Pass1 X not at scan low before REV submit; abort
```

---

### 8.3 Bug #3：3PASS 全无墨（约 15:30，日志 `F:/AppLog_Info_20260709_15.log`）

**用户现象**：机械流程跑完（Pass0/1/2 begin/end 均有），三层均无墨。

**根因 A — 用户级固定 XStart 在快速分支仍生效**：

```text
[MeteorXStart] FixedOverride env=METEOR_IMAGE_XSTART_FIXED_FWD_PX/REV_PX original=4110 forced=4110
```

- `4110` 为 PiSetHome 精扫标定值，依赖每层 PiSetHome 重置 AbsX。
- 快速分支 `piSetHomeAtSendStartJob=False`，525 mm 处 live AbsX≈**2392**，强制 4110 → 喷墨窗口与 encoder 完全错位。

**根因 B — Pass0 低位 AbsX 未写入引擎**：

- 快速分支使用 stage 名 `Pass0AtScanLowEndPhysicalFast`；
- 引擎 `LogDualCoordSnapshot` **仅**在 stage 等于 `Pass0AtScanLowEnd` 时设置 `_pass0ScanAbsXLowEndValid`；
- Pass1 无法走 `pass1_pass0MeasuredLowEndAbs_rev`，REV xStart 进一步偏差。

**15:30 日志关键时间线（运动已正常）**：

| 时间 | 事件 |
|------|------|
| 15:30:36 | 525 停稳 + STARTSCAN（仍 forced=4110） |
| 15:30:40 | Pass0AtScanLowEnd googolX≈17 mm, absX≈10377 |
| 15:30:41 | Pass1 begin, Pass1 REV swath xStart=9633 |
| 15:30:46 | Pass1AtScanHighEnd googolX≈522 mm |
| 15:30:47–51 | Pass2 525→15→525, EndJob OK |

**修复（第三轮）**：

1. `ClearMeteorImageXStartFixedProcessOverrides()`：进程内 `METEOR_IMAGE_XSTART_FIXED_* = -1`（负值关闭覆盖，见 `TryGetForcedImageXStartPixels`）。
2. `ApplyMeteorPhysicalHomeFastPrintMode` + **每层 Pass0 入口**再次调用，防止未重按按钮时用户 env 残留。
3. 快照 stage 改回引擎识别名：`Pass0AtApproachHold`、`Pass0AtScanLowEnd`（不再用 `*PhysicalFast` 后缀）。

---

## 9. 使用与验证

### 9.1 按钮使用

1. **快速打印**：点「切到物理Home快速打印」→ 按钮变蓝「物理Home快速打印已设」。
2. **精扫**：点「切到PiSetHome固定XStart」→ 按钮变绿。两按钮互斥。
3. 必须在**停止自动打印线程后**切换；对**下一次打印**生效。
4. **代码更新后建议重按一次快速按钮**，确保进程 env 含最新的 XStart 关闭项。

### 9.2 快速分支日志辨认

**应出现：**

```text
[MeteorMotionBranch] physical_home_fast Pass0/1/2 begin/end
motionBranch=physical_home_fast
batchSwathMode=False
WaitInkCarXAxisReachWithProfileStopped: ok, targetMm=525
Pass0AtScanLowEnd ... googolXmm≈15
[MeteorXStart] anchor=pass0_liveAbsX_fwd  imageCmdXStart≈2390（随 525 处 live AbsX）
pass1_pass0MeasuredLowEndAbs_rev（Pass1 REV）
```

**不应再出现：**

```text
FixedOverride ... forced=4110
Pass0AtMeteorHomeBeforePiSetHome
FlushSwathDone pass0/1/2（BATCH 预灌）
PassSwathWaitSkipped legacyBatchMode passIndex=1
Pass0AtScanLowEndPhysicalFast（应改为 Pass0AtScanLowEnd）
```

### 9.3 建议实验顺序

1. 重新编译 → 点「物理Home快速打印」→ 打 1 层。
2. 确认 Pass0/1/2 均有 `[MeteorXStart]` 且无 `FixedOverride forced=4110`。
3. 若 Pass0 有墨、Pass1 仍无：查 Pass1 是否出现 `pass1_pass0MeasuredLowEndAbs_rev`。
4. 精扫链 Pass1 修复（独立任务）：关闭 OfficialQueued 或 `METEOR_SPLIT_JOB_PER_PASS=1`，勿叠 fastOfficialQueued。

---

## 10. 当前关键结论

1. **PiSetHome 按钮 = 精扫校准**，不是快速打印；不能用于省层间时间。
2. **physical_home_fast = 无 PiSetHome + live AbsX + 逐 pass 门控**；不得沿用用户级 `METEOR_IMAGE_XSTART_FIXED_FWD_PX=4110`。
3. **等停不可省**：可省 663.5/PiSetHome/Stable 多点采样；**不可省**扫程 X 终点与 pass 间 Y 换带等待。
4. **Pass0 门控时序**：525 停稳 → Signal → swath ENDDOC → 再发 525→15 扫程；STARTSCAN 不得在 809→525 途中发出。
5. **引擎 stage 名**必须与 `4-MeteorPrintEngine.cs` 内字符串匹配，否则 AbsX 锚点链断裂。
6. `4-MeteorPrintEngine.cs` 在 gitignore 中，本地版本需与运动/数据 env 一致。

---

## 11. 待办（优先级，截至 2026-07-09）

| 优先级 | 事项 | 状态 |
|--------|------|------|
| P0 | 实机验证快速分支：无 `FixedOverride 4110`，Pass0/1/2 出墨 | 待用户复测 |
| P0 | 确认日志出现 `pass0_liveAbsX_fwd` / `pass1_pass0MeasuredLowEndAbs_rev` | 待用户复测 |
| P1 | 精扫链 Pass1：关 OfficialQueued 或 per-pass 门控，修 fastOfficialQueued | 未做 |
| P2 | PiSetHome Pass1 XStart 改为 2748+图宽 自动计算 | 未做 |
| P3 | PassPlatformYCrop + Y 映射 / pass 翻转表 | 长期 |
| P3 | 光栅尺装好后统一重标 Home、Xleft、PiSetHome 策略 | 长期 |

---

## 12. 对话时间线摘要

| 时间 | 用户反馈 | 处理 |
|------|----------|------|
| 上午 | Pass1 无墨、PiSetHome 不能省时、需独立快速分支 | 实现双分支 + BATCH=0 + 文档初版 |
| ~15:04 | Pass0 未完成、Y 乱飘 | 恢复 X/Y 等停；保留 525 并行与 BATCH=0 |
| ~15:18 | Pass0 无墨、Pass1 卡 12 s | 525 停稳后再 Signal；扫程容差 2.5 mm |
| ~15:30 | 3PASS 全无墨 | 关闭 IMAGE_XSTART_FIXED；修正 Pass0AtScanLowEnd stage 名 |
| ~15:40 | 要求写入对话记录 | 更新本文档 |

---

## 13. 相关文档与备份

- 前序记录：`meteor_debug_notes_20260611.md`
- 同 Job 窗口续查：`Backup/meteor_20260529_.../Meteor-3PASS-同Job窗口与REV续查记录-20260528.md`
- 旧快速链路参考：`4-MeteorPrintEngine -稳定打印版本层间细微偏差.cs`
- 旧丝滑运动参考：`Backup/meteor_20260529_batch-swath_deferred-endjob_before-startscan-gate-split_20260529_093810/手动操作界面.cs`
- 本轮实机日志：`F:/AppLog_Info_20260709_15.log`（15:04 / 15:18 / 15:30 三段）

---

## 14. 用户环境变量（Windows 用户级，仍可能存在）

以下值在「系统环境变量」中可能仍存在；**快速分支会在进程内覆盖**，精扫按钮会重新写入：

```text
METEOR_OFFICIAL_QUEUED_SCAN_MODE=1
METEOR_PISET_HOME_AT_JOB_START=1
METEOR_PER_LAYER_HOME_MM=663.5
METEOR_PASS0_FWD_XSTART_PX=2748
METEOR_PASS1_REV_XSTART_PX=10071（或 7085，以代码常量为准）
METEOR_PASS2_FWD_XSTART_PX=2760
METEOR_IMAGE_XSTART_FIXED_FWD_PX=4110   ← 快速分支须进程内置 -1 关闭
```
