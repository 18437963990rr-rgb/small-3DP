# Meteor 层间图形错位 — 750mm 等待位 PiSetHome 排查与解决记录（2026-06-04）

> **文档版本：** 2026-06-04  
> **适用项目：** LaserAdd_3DP 主控 + Meteor PCC-E / Starfire  
> **涉及模块：** `4-MeteorPrintEngine.cs`、`5-SharpControl.cs`、`手动操作界面.cs`（`AutoPrintThread5`）、`主界面.cs`（`DataTaskTHREAD` / `renderSubIndex`，见 §2.1）  
> **前置阅读：**  
> - [Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md](./Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md)  
> - [Meteor-Home-Xleft-REV续查记录-20260528.md](./Meteor-Home-Xleft-REV续查记录-20260528.md)  

---

## 1. 结论摘要

| 项 | 结论 |
|----|------|
| **现象** | 多层打印时，第 2 层相对第 1 层在粉床上 **整体左偏约 10–20mm**；曾伴「仅首层有图」 |
| **主因（层间左偏）** | 层与层 **PCC AbsX 基准不一致**（发图/Flush 时约 **3688 → 3482**，差 **~206 count ≈ 13mm@400dpi**），而软件仍用 **固定 FWD Xleft=4110**；机械上每层进站 **Home 传感器触发侧/垫片重复性**不同（清洗后从高端侧进站 vs 非清洗从 0 侧回程等） |
| **非主因** | Meteor **发图队列时机**（`DocsQueuedLane1`、扫程中续发 swath）；在 **525mm** 等停点 `PiSetHome`（当时未在真 Home 位） |
| **闭环措施** | 在 **750mm 清洗/待机等待位** 先发 `PreheatReady` → 数据线程 **`SendStartJob` 前 `PiSetHome`（默认开启）** → 再运动至 **525mm** 灌 swath / 开扫 |
| **现场验证（2026-06-04）** | 重新测试，**确认图形无错位** |

量产建议：保持 **batch legacy + 固定 4110**（见 06-01 文档），并保留本版的 **750mm 等待位 PiSetHome + Preheat 时序**。

---

## 2. 问题现象

### 2.1 层间 X 向整体左偏

- 第 0 层与第 1 层 **Pass0/1/2 的 Xleft 命令相同**（如 FWD **4110**、REV **8462**）。
- **Flush / STARTJOB** 时刻 PCC **AbsX 不同**（例：~**3688** vs ~**3482**）。
- 固定 `Xleft` 未随层间 AbsX 补偿 → 粉床图形相对上一层左移（目测 ~20mm，与编码器差量级一致）。

### 2.2 仅首层有图（并行问题，已单独修复）

- `SimplifiedMeteorAutoFlowMode` 下，`DataTaskTHREAD` 对 `j≥1` 曾用 `RenderToWic(..., i+1, ...)`，触发 `subindex>0` 跳过渲染。
- 修复：`renderSubIndex = SimplifiedMeteorAutoFlowMode ? i : (i+1)`（`主界面.cs` / `DataTaskTHREAD`）。
- 与层间左偏 **独立**；左偏闭环以 **PiSetHome@750** 为主。

---

## 3. 概念澄清

### 3.1 「Flush」指什么

在 **Legacy batch** 模式下，**Flush** = 将内存中 **Queued** 的多条 swath **一次性**下发给 PCC：

```text
WriteImageLayer ×3 → [MeteorBatchDefer] Queued passIndex=0/1/2
渲染结束 → SignalBatchSwathsMeteorReady → FlushDeferredLegacyBatchSwaths
         → 连续 3×（STARTSCAN + IMAGE + ENDDOC）→ FlushEnd
```

文档中「每层 Flush 时 AbsX」= **该层 3 条 swath 全部 FlushEnd 完成前后** 读到的 PCC `AbsXCount`，不是清洗动作。

### 3.2 三套坐标（再次强调）

| 概念 | 含义 |
|------|------|
| **固高 X mm**（525↔15、**750** 清洗站） | 运动卡；与 PCC **无硬同步** |
| **PCC AbsXCount** | 喷墨编码器域；`PCMD_IMAGE Xleft` 落在此域 |
| **固定 4110 px** | 在 **某一 AbsX 基准** 下标定的 FWD 开窗（06-01 文档） |

### 3.3 750mm 等待位与 Home

- 代码常量：`INKCAR_CLEAN_STATION_X = 750.0`（清洗/待机/层末回站）。
- 现场：**打印完成等待位 = 清洗完成位 ≈ 750mm**，**Home 传感器在此有效**（监视界面固高显示值可能与 PCC AbsX 不同，如约 **-69** 为显示系，验证以日志 **双坐标快照** 为准）。

---

## 4. 根因分析

### 4.1 为何「限位与扫程未改」仍会有层间 AbsX 差

- 机械 **525→15** 扫程可每层一致。
- **PCC AbsX** 记录的是喷墨编码器在 **Meteor 参考系**下的计数，层与层之间若 **未在同一 Home 位对齐**，仅靠固定 `Xleft` 无法保证粉床对齐。

### 4.2 机械/Home 触发不对称（现场归纳）

| 层间差异 | 说明 |
|----------|------|
| Pass0 / 周期清洗 | 墨车常从 **Home/高端侧（~810mm 限位回零语义）** 经清洗流程进入 **750mm** |
| 下一层无清洗 | 回程可能从 **0 侧** 逼近 Home，传感器 **垫片 10–20mm** 导致触发时刻不同 |
| 编码器后果 | 同标称 750mm 停靠，**AbsX 可差 ~200 count（≈10–15mm）** → 叠加固定 4110 → 目视左偏 |

### 4.3 与供应商说明的关系

供应商（摘要）：

- 发图：在到达该 swath **Xleft** 之前入队即可；`DocsQueuedLane1>1` 后可动，扫程中可续发。
- `PiSetHome`：**开机寻零一次**；**零点变化后再寻零**；**同一 STARTJOB 内 3 pass 不要反复 Home**。
- 扫描模式 **不必 ForcePD**（≈ PiSetHome）。

本现场属于：**层边界「等效零点」漂移**（Home 重复性），在 **750mm 真 Home 位** 每层 `STARTJOB` 前对齐，符合说明书「暂停/Home 位静止再 Home」，**不是**在 525mm 普通等停点乱 Home。

### 4.4 旧软件时序为何无效

改代码前：

```text
Pass0：先 BackToStation → 525mm
     → SignalPrintThreadLayerPass0PreheatReady（pass0-approach485）
     → 数据线程 SendStartJob（PiSetHome 默认关闭，且在 525 亦非 Home）
```

因此在 **525mm** 发 STARTJOB/Flush 时 AbsX 仍随层漂移；**不是**「发图太晚」类问题。

---

## 5. 解决方案

### 5.1 时序（当前量产主路径 `AutoPrintThread5` Pass0）

```text
层末 / 清洗后：墨车停在 750mm（INKCAR_CLEAN_STATION_X）
    ↓
Pass0 开始（Command=1, PassIndex=0）
    ↓
LogDualCoordSnapshot("Pass0AtCleanStationWait")
SignalPrintThreadLayerPass0PreheatReady(..., "pass0-wait750-cleanStation")
    ↓  数据线程：WaitPreheat → StartJob → SendStartJob（PiSetHome）→ Render/batch
Sleep 1800ms（保留原 STARTJOB 后缓冲，与 WriteImageLayer 内间隔配合）
    ↓
BackToStation：Y 准备 → X 至 InkCarScanApproachXMm（525mm）
LogDualCoordSnapshot("Pass0AtApproachHold")
SignalPrintThreadLayerPass0ReadyForMeteorSubmit
WaitPass0FirstSwathMeteorReady → X 扫程 525→15 …
```

**同一 STARTJOB 内 Pass1/2：** 不在 pass 之间再 `PiSetHome`。

### 5.2 `4-MeteorPrintEngine.cs`

| 改动 | 说明 |
|------|------|
| `IsPiSetHomeAtJobStartEnabled()` | **默认 true**（未设 env 即开启） |
| `SendStartJob` | `STARTJOB` 前 `TrySetHome()`；日志注明 expect **~750mm** wait/home |
| 关闭方式 | `METEOR_PISET_HOME_AT_JOB_START=0` / `false` / `off` |

### 5.3 备份

修改前备份：

`Backup/4-MeteorPrintEngine.cs.bak-20260604_114433-pisethome-wait750`

---

## 6. 日志验收标准

### 6.1 时序与位置

```text
[MeteorDualCoord] ... Pass0AtCleanStationWait ... googolXmm≈750
[MeteorScanGate] ... PreheatReady ... motionPath=pass0-wait750-cleanStation
Meteor SendStartJob: PiSetHome at wait/home station ...
SendStartJob-BeforePiSetHome / SendStartJob-AfterPiSetHome
... Pass0AtApproachHold ... googolXmm≈525
```

### 6.2 层间对齐

| 检查项 | 通过标准 |
|--------|----------|
| 两层 `AfterPiSetHome` AbsX | **接近**（差值远小于 ~200 count） |
| 粉床目视 | 第 2 层相对第 1 层 **无整体左/右移** |
| `PiSetHome skipped` | 不应出现（除非显式关闭 env） |

### 6.3 失败判据

- `AfterPiSetHome` 前后 AbsX **几乎不变** → 当时未在 Home 传感器上压合，查机械停靠/垫片。
- Preheat 仍在 `pass0-approach485` → 未合入本版 **750 等待位** 改动。

---

## 7. 环境变量速查

| 变量 | 默认（本版后） | 说明 |
|------|----------------|------|
| `METEOR_PISET_HOME_AT_JOB_START` | **开启**（未设 env） | `SendStartJob` 前 `PiSetHome` |
| `METEOR_PISET_HOME_AT_JOB_START=0` | 关闭 | 回退旧行为 |
| `METEOR_BATCH_SWATH_MODE` | true | 见 06-01 文档 |
| `METEOR_IMAGE_XSTART_FIXED_FWD_PX` | 4110（代码默认） | 见 06-01 文档 |

---

## 8. 与 06-01 文档的衔接

[Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md](./Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md) §8 曾列 **P2：PCC AbsX=0 发图原点 / PiSetHome 与 525mm 解耦**。

本阶段在 **750mm 等待位（与 525mm 解耦）** 每层 `STARTJOB` 前对齐 PCC，现场已验证 **层间无错位**。若恢复 **7323px** 全宽，仍需在相同 Home 时序下 **重标或复核 4110 / REV 右缘**。

---

## 9. 相关源码索引

| 主题 | 文件 | 入口 |
|------|------|------|
| PiSetHome @ STARTJOB | `4-MeteorPrintEngine.cs` | `IsPiSetHomeAtJobStartEnabled`, `SendStartJob` |
| Preheat / Pass0 门控 | 同上 | `SignalPrintThreadLayerPass0PreheatReady`, `WaitPass0PreheatGateBeforeStartJobIfEnabled` |
| 750→525 运动与信号 | `手动操作界面.cs` | `AutoPrintThread5` → `case 0` |
| batch Flush | `4-MeteorPrintEngine.cs` / `5-SharpControl.cs` | `FlushDeferredLegacyBatchSwaths`, `RenderToWic` |
| 多层 renderSubIndex | `主界面.cs` | `DataTaskTHREAD` → `renderSubIndex` |

---

## 10. 附录：排查中已排除的假设

| 假设 | 结论 |
|------|------|
| 发图太晚（未在 Xleft 前入队） | 供应商确认窗口规则；层间左偏与队列时机 **无关** |
| 每层 STARTJOB 在 525mm PiSetHome | 非 Home 位，无效；旧日志为 `PiSetHome skipped` |
| 仅靠减小 Xleft 到 200–400 | 需配合 cfg **Xoffset** 重标；不能单独消除层间 AbsX 差 |
| ForcePD 拉 Pass1/2 | 扫描模式不需要（供应商） |

---

*最后更新：2026-06-04 — 750mm 等待位 Preheat + 默认 `PiSetHome@SendStartJob` 后现场复测确认图形无错位。*
