# Meteor Pass1/Pass2 恢复排查与解决记录

> 文档版本：**2026-05-27（续）**  
> 适用项目：LaserAdd_3DP 主控 + Meteor PCC-E / Starfire (SG1024 双 HDC)  
> 涉及模块：`4-MeteorPrintEngine.cs`、`5-SharpControl.cs`、`手动操作界面.cs`  
> 前置阅读：[Meteor-Pass0打印排查与解决记录.md](./Meteor-Pass0打印排查与解决记录.md)

---

## 1. 背景与目标

Pass0 在合适 cfg + live AbsX xStart 条件下已能 **清晰出图**。后续目标是恢复 **Pass1/Pass2** 同址 3PASS 打印，并理解历史上「batch 一次发完 3 strip」与当前「逐 pass 门控」两种模式的差异。

**Pass1/Pass2 恢复过程中的主要现象：**

| 现象 | 描述 |
|------|------|
| Pass0 有图，Pass1/2 无图 | 数据已进 PCC 队列，PD 可能触发但 substrate 不可见 |
| Pass2 图形在右下角 | xStart 随 Pass0 扫程中 live AbsX 漂移 |
| batch 后 Pass1 PD 在 ~9392 | 与 Pass1 物理扫程起点（15mm 端 ~11462）不匹配 |
| 圆缺口朝向反 | 预期缺口朝下，打印朝上（Y 向行序问题） |
| Pass2 回程模糊喷墨 / 撞限位 | EndJob 时序错误 + 运动指令叠加 |
| 条带工具 HDC2 有完整圆 | 数据链正常，问题在播放对齐而非缺图 |

---

## 2. 现场 cfg 备忘（分阶段，以当前机台实测为准）

路径（默认）：

```
C:\Users\Public\Documents\Meteor\Config\PccE\DefaultStarfire_PccE.cfg
```

### 2.1 早期金标准（2026-05-27 上午，batch 调试期）

| 项 | 值 | 说明 |
|----|-----|------|
| `RightToLeft` | **1** | 曾验证清晰出图 |
| `Orientations` | **0, 0** | 两 HDC 朝向一致 |
| `Yoffsets` | **0, 1024** | 双 HDC 叠拼步距 |
| `Plane1` | **1:1, 1:2** | HDC1 + HDC2 同 plane |
| `Scanning` | 1 | 扫描模式 |

### 2.2 当前现场金标准（2026-05-27 下午，逐 pass 调试期）

| 项 | 值 | 说明 |
|----|-----|------|
| `Orientations` | **1, 1** | Pass0 图形/方向正常；与 `RL=1` 同开曾发糊，勿同时启用 |
| `RightToLeft` | **0** | 当前清晰组合 |
| `Yoffsets` | **0, 1024** | 双 HDC |
| `Plane1` | **1:1, 1:2** | 双头同 plane |
| `Scanning` | 1 | — |

**双头认知：**

- PrintEngine 对每条 IMAGE 同时翻译 **HDC1 + HDC2**（日志：`PCC1 HDC1/HDC2 image translation`）。
- 条带检测工具中 **HDC1、HDC2 均有图形** → 切片/下发/双头映射数据层正常。
- Pass0 现场常只见「半圆」或弧段：因 Y 向 0/1024 各负责 ~1024 行，**合起来才是整圆**。
- 2 号头不喷时优先查 **Yoffsets / Plane1 / cfg**，非数据缺失。

---

## 3. 两种发送模式对比

### 3.1 历史 batch 模式（用户印象「一起发送能喷 Pass1/2」）

参考：`5-SharpControl - 副本.cs`、git `db28b31`（「打印成功」）。

| 项目 | batch 模式 |
|------|------------|
| 门控 | 仅 **Pass0 门控**，Pass1/2 **不等待**打印线程 Signal |
| 发送 | Pass0 放行后 **连续发 3 strip + EndJob**（约 0.6~1.2s 内） |
| 扫程门控 | `SuppressScanMotionGateForNextWrite`；`WaitPassSwathMeteorReady` 可短路 |
| 风险 | 3 doc 在 Pass0 物理扫程（~5s）完成前进队列；Pass1/2 xStart 不能用扫程中 live AbsX |

### 3.2 当前逐 pass 门控模式（Pass1/2 主调试路径）

| 项目 | 逐 pass 门控 |
|------|--------------|
| 门控 | Pass0/1/2 各 `WaitPassGateBeforeMeteorSubmit` |
| 发送 | 打印线程 **扫程中 Signal** 后，数据线程才发对应 strip |
| 优点 | 与物理 pass 时间对齐；Pass0 live AbsX 准确 |
| 要求 | **禁止在墨车静止端发 STARTSCAN**（Pass1 REV @15mm、Pass2 FWD @485mm 均如此） |

### 3.3 软件开关（2026-05-27 最新）

| 变量 / API | 当前默认 | 作用 |
|------------|----------|------|
| `METEOR_BATCH_SWATH_MODE` | **未设 = false（逐 pass）** | `=1` 启用 batch 预灌 |
| `IsBatchSwathModeEnabled()` | 代码 `return false` | 启动日志应见 `batchSwathMode=False` |
| `DescribeBatchSwathModeConfig()` | 诊断 API | 例：`METEOR_BATCH_SWATH_MODE=unset => batchSwathMode=False` |

> **历史踩坑：** 曾出现 env 未设但日志仍 `batchSwathMode=True`（旧默认）；已改为 **unset = 逐 pass**。

---

## 4. Pass1/Pass2 不出图：根因与修复

### 4.1 batch 下 Pass1/2 xStart 随 Pass0 扫程漂移（Pass2 右下角）

**现象（11:40 日志）：** Pass1/2 在 Pass0 扫程 0.6s 内连发，AbsX 从 ~4141 漂到 ~4564 → Pass2 图形落右下角。

**修复：** batch + pass>0 锁定 `_jobUnifiedImageXStartAbsX`（Pass0 首条 fwdBase）。

```
anchor=batch_unifiedFwd_jobBase           → Pass2 FWD
anchor=batch_pass1Rev_scanHighFromFwdBase → Pass1 REV（见 4.2）
```

### 4.2 Pass1 REV 锚点：485 端公式 vs 15mm 端物理扫程

**Pass 交替扫向：**

| pass | nPrtDir | STARTSCAN | 物理 X 扫程 |
|------|---------|-----------|-------------|
| 0 | 1 | FWD | 485mm → 15mm |
| 1 | 0 | REV | **15mm → 485mm** |
| 2 | 1 | FWD | 485mm → 15mm |

**错误：** batch Pass1 用 `fwdBase + width` ≈ **9391**（485 端 REV 公式）→ PD @ 9392，substrate 不可见。

**batch 修复：** `anchor=batch_pass1Rev_scanHighFromFwdBase` → `fwdBase + scanTravelWidthPx ≈ 11513`。

**逐 pass 修复（2026-05-27 新增）：** 用 Pass0 扫到 15mm 端的 **实测 AbsX**（非公式推算）：

```
anchor=pass1_pass0MeasuredLowEndAbs_rev
来源：LogDualCoordSnapshot("Pass0AtScanLowEnd") → _pass0ScanAbsXAtLowEnd
典型值：AbsX ≈ 11536（Pass0 低端实测，勿用 11709 等偏高估算）
```

### 4.3 静止下发 STARTSCAN 导致不喷（逐 pass 核心问题）

Meteor 播放窗口依赖 **扫程中编码器位置**。Pass0 已验证「先动再 Signal」有效；Pass1/2 必须对称：

| Pass | 正确顺序 | 错误顺序（不喷） |
|------|----------|------------------|
| Pass0 | 485→15 异步扫程 → Signal pass0 | 485 静止 Signal |
| Pass1 | 15→485 异步扫程 → Signal pass1 | 15mm 静止 Signal（REV 不喷） |
| Pass2 | 485→15 异步扫程 → Signal pass2 | **485mm 静止 Signal（FWD 不喷）** ← 2026-05-27 复现 |

**Pass2 错误流程（已修复前）：**

```
Signal pass2 @485 静止 → WaitPassSwathMeteorReady → 再启动 485→15 扫程
```

**Pass2 正确流程（当前代码）：**

```
485→15 异步扫程 → Signal pass2 → WaitInkCarXAxisReach(15mm)
→ 15→485 同步收口(waitStop=true) → TryCompleteDeferredEndJob @485mm
```

### 4.4 EndJob 过早导致 Pass1/2 不喷 / Pass2 回程异常

**现象（16:02 逐 pass 日志）：**

- 数据线程 3 strip 发完后 `MarkDeferredEndJob`，间隔 ~6s ✓
- 但 `TryCompleteDeferredEndJob` 曾在 **Pass2 到 15mm 端**即调用
- Pass2 物理扫程 485→15 需 ~5s；**EndJob 比 Pass2 播放早 ~5s** → Pass1/2 doc 可能被截断
- EndJob @15mm 后立即 **15→485 收口**，与 Meteor 状态重置叠加 → **PosLimit 撞限位**

**修复（2026-05-27）：**

| 阶段 | 行为 |
|------|------|
| 数据线程 | 3 strip 发完后 `MarkDeferredEndJobAfterPassSwaths`，**不**立刻 `SendEndJob` |
| 打印线程 Pass2 | **收口到 485mm 完成后** `TryCompleteDeferredEndJob("Pass2AfterScanHighEnd")` |
| 之后 | 再回清洗站 750mm（`INKCAR_CLEAN_STATION_X`） |

**勿再同时启用：**

- Pass2 异步扫程 + 数据线程立即 EndJob + 回程 Wait（旧组合，曾撞限位）
- EndJob @15mm + 同步 15→485 收口（状态冲突）

### 4.5 LogDualCoordSnapshot 误覆盖 Pass0 锚点（2026-05-27 修复）

**问题：** `stage.IndexOf("ScanLowEnd")` 过于宽泛，`Pass2AtScanLowEnd` 会覆盖 `_pass0ScanAbsXAtLowEnd`，导致 Pass1 锚点漂移。

**修复：** 仅 `Pass0AtScanLowEnd` 写入 Pass0 低端实测 AbsX。

---

## 5. 双头与条带检测结论

| 观察 | 结论 |
|------|------|
| 条带工具 HDC1 见半圆/弧段 | Y 向半幅，符合 Yoffsets 0/1024 叠拼 |
| 条带工具 HDC2 见完整圆 | batch 3 doc 均进队列；非「2 号头无数据」 |
| Meteor 日志 HDC1/HDC2 translation | 双头均有 image translation |
| Pass1/2 无可见图 | **播放 xStart / PD 窗口 / EndJob 时序**，非切片缺失 |

---

## 6. Pass0 圆缺口朝向（Y 向行序）

**现象：** 圆缺口应 **朝下**，打印 **朝上** → Y 向上下镜像。

**修正方式（二选一，勿同时开）：**

| 方式 | 做法 |
|------|------|
| **软件** | `METEOR_IMAGE_FLIP_Y=1` 或 `METEOR_IMAGE_FLIP_X` / `METEOR_IMAGE_ROTATE_180`（按现场择一） |
| **cfg** | `Orientations = 1, 1`（两 HDC 一致） |

**注意：** `Orientations=1` + `RightToLeft=1` 同开曾发糊；当前推荐 `Orientations=1, RL=0`。

---

## 7. 软件改动摘要（截至 2026-05-27）

| 文件 | 改动 |
|------|------|
| `4-MeteorPrintEngine.cs` | batch pass>0 xStart 锁定；Pass1 REV `scanHighFromFwdBase`（batch）；`pass1_pass0MeasuredLowEndAbs_rev`（逐 pass）；`IsBatchSwathModeEnabled()` 默认 false；`MarkDeferredEndJob` / `TryCompleteDeferredEndJob`；`LogDualCoordSnapshot` 精确匹配 Pass0；`LogScanPassMotionCheck` |
| `5-SharpControl.cs` | batch：Pass0 门控后连发 3 strip；SimplifiedMeteorAutoFlowMode 发完后 `MarkDeferredEndJob` 而非立即 EndJob |
| `手动操作界面.cs` | Pass1：先异步扫程再 Signal；Pass2：同上 + EndJob 延后到 485 收口后；去掉 Pass2 扫程前 `WaitPassSwathMeteorReady` |

**Pass0 稳定核心（未改原则）：**

- Pass0 **FWD**（`nPrtDir=1`）
- HiPrint compat + live AbsX @ Pass0 放行时刻
- Preheat 门控 → StartJob → Pass0 扫程 Signal → 首条 swath

---

## 8. 3PASS 门控时序（当前设计）

### 8.1 数据线程（`RenderToWic` / `SimplifiedMeteorAutoFlowMode`）

```
StartJob
→ Wait Pass0 gate → 发 strip0
→ Wait Pass1 gate → 发 strip1
→ Wait Pass2 gate → 发 strip2
→ MarkDeferredEndJob（不 SendEndJob）
```

### 8.2 打印线程（`AutoPrintThread5`）

```
Pass0: 485 预热 → StartJob 延迟 → 485→15 异步 → Signal pass0 → 到 15mm
Pass1: 15→485 异步 → Signal pass1 → 到 485mm
Pass2: 485→15 异步 → Signal pass2 → 到 15mm
     → 15→485 同步收口 → TryCompleteDeferredEndJob @485
     → 回清洗站 750mm
```

### 8.3 时序简图（逐 pass，当前主路径）

```
打印线程                          数据线程
    │                                 │
    ├─ Pass0: 485→15 扫程             │
    ├─ Signal Pass0 ─────────────────►│ Wait Pass0 → strip0 FWD
    ├─ 到 15mm（记录 Pass0AtScanLowEnd）│
    │                                 │
    ├─ Pass1: 15→485 扫程             │
    ├─ Signal Pass1 ─────────────────►│ Wait Pass1 → strip1 REV
    │   （xStart=pass0实测低端AbsX）    │
    ├─ 到 485mm                       │
    │                                 │
    ├─ Pass2: 485→15 扫程             │
    ├─ Signal Pass2 ─────────────────►│ Wait Pass2 → strip2 FWD
    ├─ 到 15mm                        │
    ├─ 15→485 收口（同步等停）         │
    ├─ TryCompleteDeferredEndJob ────►│ PCMD_ENDJOB
    └─ 750 清洗站                     │
```

---

## 9. 环境变量备忘

| 变量 | 建议 | 作用 |
|------|------|------|
| `METEOR_BATCH_SWATH_MODE` | **未设**（逐 pass）或 `=1` 对比 batch | batch vs 逐 pass |
| `METEOR_IMAGE_FLIP_Y` | 缺口反时设 `1` | Y 向行序翻转 |
| `METEOR_PASS0_FLIP_STARTSCAN` | 未设或 `0` | Pass0 保持 FWD |
| `METEOR_GATE_SENDSTARTJOB_ON_PASS0` | 自动流程已配置 | Pass0 门控 |
| cfg `Orientations` / `RightToLeft` | **1,1 + RL=0**（当前） | 勿 RL=1 与 Orientations=1 同开 |

---

## 10. 推荐测试顺序

```
1. cfg 固定：Orientations=1,1, RL=0, Yoffsets=0,1024, Plane1=1:1,1:2
2. 确认启动日志：METEOR_BATCH_SWATH_MODE=unset => batchSwathMode=False
3. Pass0 单 pass 确认清晰 + 缺口朝向
4. 3PASS 全跑：查 Pass0/1/2 Signal 均在扫程中（非静止端）
5. 查 DeferredEndJobCompleted stage=Pass2AfterScanHighEnd（非 Pass2AtScanLowEnd）
6. 查 Pass1 anchor=pass1_pass0MeasuredLowEndAbs_rev，AbsX≈11536
7. 查 PosLimit=False；Pass1/2 PD AbsX 在各自扫程窗口内
8. 勿同时改：batch + 打印线程立即 EndJob + Pass2 异步回程
```

---

## 11. 日志诊断表

| 关键字 | 正常（逐 pass 修复后） | 异常暗示 |
|--------|------------------------|----------|
| `batchSwathMode=False` | 逐 pass 生效 | `True` 且未设 env → 旧二进制 |
| `DeferredEndJobPending` | 3 strip 发完标记 | 无 → 仍立即 EndJob |
| `DeferredEndJobCompleted stage=Pass2AfterScanHighEnd` | EndJob 在 485 收口后 | `Pass2AtScanLowEnd` → 过早 EndJob |
| `Pass2ScanStart note=asyncScanThenSignal_likePass1` | Pass2 先动再 Signal | `pass2-beforeScan15` → 静止下发 |
| `anchor=pass1_pass0MeasuredLowEndAbs_rev` | Pass1 用 Pass0 实测 | ~9391 或 `batch_unifiedRev` → 锚点错 |
| `Pass1 PD AbsX` | ~11400–11550 | ~9392 → REV 锚点仍错 |
| `Pass0AtScanLowEnd absX24` | ~11536 | 缺失 → Pass1 锚点 fallback |
| `PosLimit=True` | — | EndJob/收口/清洗站运动冲突 |
| `EndJob` 时间 vs Pass2 StartScan | EndJob 晚于 Pass2 扫程+播放 ~5s | 过早 → Pass1/2 不喷 |

---

## 12. 与 Pass0 文档的关系

| 主题 | Pass0 文档 | 本文档 |
|------|------------|--------|
| xStart / DualCoord | 详述 | 继承；补充 batch 锁定 + Pass1 实测锚点 |
| RightToLeft | 曾记 RL=1 清晰 | **分阶段：batch 期 RL=1；当前 RL=0+Orientations=1** |
| 门控时序 | Pass0 门控 | 逐 pass 三门控 + 延后 EndJob |
| 双头 cfg | 未详述 | Yoffsets 1024、Plane1 1:1,1:2 |
| Y 行序 | 未涉及 | FLIP_Y / Orientations |
| EndJob | 外层统一结束 | **延后至 Pass2 485 收口** |

---

## 13. 问题演进时间线（2026-05-27）

| 时段 | 模式 | 现象 | 根因 | 处置 |
|------|------|------|------|------|
| 上午 | batch | Pass2 右下角 | live AbsX 漂移 | batch pass>0 xStart 锁定 |
| 中午 | batch | Pass1 PD@9392 无图 | REV 锚点按 485 端公式 | `scanHighFromFwdBase` |
| 下午 | 逐 pass | Pass1/2 仍不喷；Pass2 撞限位 | EndJob@15mm + Pass2 静止 Signal | 延后 EndJob；Pass2 先扫再 Signal |
| 下午 | 逐 pass | Pass1 xStart 偏高 ~173px | 公式 vs 实测 | `pass0MeasuredLowEndAbs_rev` |
| 下午 | cfg | 缺口/方向 | Orientations/RL 组合 | Orientations=1, RL=0 |

---

## 14. 待验证 / 未关闭项

- [ ] **重新编译后**复测 3PASS：Pass1/2 substrate 是否出图  
- [ ] 日志确认 `DeferredEndJobCompleted stage=Pass2AfterScanHighEnd` 且 `PosLimit=False`  
- [ ] Pass1 日志出现 `anchor=pass1_pass0MeasuredLowEndAbs_rev`，PD AbsX ~115xx  
- [ ] Pass2 扫程中有 PD（485→15 FWD 窗口）  
- [ ] 若仍不喷：评估 **每 pass 独立 JOB**（StartJob→1 strip→EndJob）是否为 Meteor 推荐模型  
- [ ] batch 模式 + 已修复 xStart 是否可作为 fallback 对比  
- [ ] 是否将 `METEOR_BATCH_SWATH_MODE` / cfg 默认值写入现场启动脚本  

---

## 15. 附录 A：batch 时序简图（Pass0 扫程内预灌）

```
打印线程                          数据线程
    │                                 │
    ├─ 485 等停 / Preheat             │
    ├─ STARTJOB ─────────────────────►│ SendStartJob
    ├─ 485→15 扫程启动                │
    ├─ Signal Pass0 ─────────────────►│ Wait Pass0 放行
    │   （扫程进行中 AbsX 递增）       ├─ strip0 FWD  xStart≈fwdBase
    │                                 ├─ strip1 REV  xStart≈fwdBase+scanTravel
    │                                 ├─ strip2 FWD  xStart≈fwdBase
    │                                 └─ EndJob  （约 0.6~1.2s 内完成）
    ├─ 扫程到 15mm（AbsX≈11462）       │
    ├─ Y 步距 → Pass1 扫程 15→485     │  （doc 已在队列，靠 PD 播放）
    └─ Pass2 …                        │
```

**要点：** 预灌时 Pass1/2 的 xStart **不能**用发图时刻 live AbsX，必须用 **Pass0 fwdBase 推导的固定锚点**。

---

## 16. 附录 B：危险组合清单（勿同时启用）

| 组合 | 后果 |
|------|------|
| batch 预灌 + Pass0 扫程未完成 | Pass1/2 STARTSCAN 时序错误 |
| Pass1/2 静止端 Signal | REV/FWD 均可能不喷 |
| EndJob @15mm + 15→485 收口 | 撞 PosLimit / 状态冲突 |
| Pass2 异步扫程 + 数据线程立即 EndJob | Pass2 不喷 + 回程异常 |
| `Orientations=1` + `RightToLeft=1` | 发糊 |
| `LogDualCoordSnapshot` 宽泛 ScanLowEnd 匹配 | Pass1 锚点被 Pass2 覆盖（已修） |

---

*最后更新：2026-05-27 — 合并 Pass1/Pass2 历史排查与逐 pass + 延后 EndJob + Pass2 扫程顺序修复。*

**续篇（2026-06-01）：** 当前现场闭环为 Legacy batch + 固定 Xleft 4110，见 [Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md](./Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md)。
