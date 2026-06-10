# Meteor 层间与 Pass 间套准 — 差补试验、Pass 内偏差与方案 A — 排查与解决记录（2026-06-06 ～ 2026-06-09）

> **文档版本：** 2026-06-09  
> **适用项目：** LaserAdd_3DP 主控 + Meteor PCC-E / Starfire  
> **涉及模块：** `4-MeteorPrintEngine.cs`、`5-SharpControl.cs`、`手动操作界面.cs`（`AutoPrintThread5`）  
> **前置阅读（按时间）：**  
> - [Meteor-层间套准-无PiSetHome与525光栅-解决记录-20260606.md](./Meteor-层间套准-无PiSetHome与525光栅-解决记录-20260606.md)（**上一版终点**：默认无 PiSetHome + 固定 Xleft4110 + 525 光栅散差分析）  
> - [Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md](./Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md)（高精度分支：750 + PiSetHome）  
> - [Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md](./Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md)（batch + 固定 Xleft 基线）  

---

## 1. 结论摘要

| 项 | 结论 |
|----|------|
| **相对 06-06 的新问题** | ① **P1 层间差补试打失败**（06-09）：层间偏差反而极大；② **同层 Pass 间 X 拼接偏差**（14:49 单层日志）：Pass0 准、Pass1 右偏约 3mm、Pass2 右偏约 4mm |
| **层间差补失败根因** | `4110` 标定在 **Flush 域**（`liveAbsX≈2091`）；旧差补在 **525 等停**采样（典型 ~1969/1932/2012），Flush 三层却共用 **2091** → 人为造成层间 `Xleft` 偏移（如 4147/4067） |
| **差补代码修正** | 差补 **默认关闭**；采样改 **Flush 时刻**；增加死区；从 `LogDualCoordSnapshot` 移除 525 差补采样 |
| **当前量产基线** | 固定 **4110 / 8462 / 4110**；默认 **无 PiSetHome**；层间差补默认关 ≈ 回到 06-06「好很多」命令侧行为 |
| **Pass 间偏差根因** | **非 Xleft 公式错误**；batch 在 Flush 锁死 `Xleft`，层内三次扫程后 **同固高 mm 上 PCC absX 不重复** |
| **层间 vs Pass 间量级** | 层间 ~1mm：三层共用同一 Flush 锚点；Pass 间 3–4mm：扫程中编码器漂移，Pass1/2 开扫时 absX 已变 |
| **硬件不变下的软件路线** | 层间：Flush 差补（修正后待再评估）/ P2 门控 / PiSetHome 分支；Pass 间：**方案 A**（per-pass 固定 X trim，已入代码，默认 0）及 B–E 备选 |

---

## 2. 接续背景：06-06 文档之后发生了什么

### 2.1 06-06 已确认的状态

[20260606 文档](./Meteor-层间套准-无PiSetHome与525光栅-解决记录-20260606.md) 终点：

- 量产倾向：**默认关闭 PiSetHome**、**Xleft 恢复 4110**，靠物理过 Home 压缩节拍。
- 大错位（10–20mm）已压下；与 Royal 比仍有 **~1–3mm 级**细微不齐。
- 日志主因：**525mm 固高重复到位，但 525 处 absX24 层间散差**（典型 1723–2088）；发图仍用固定 `liveAbsX≈2090 + Xleft=4110`（`UNCALIBRATED_LIVE`）。
- 待选增强：**P1 525/Flush 实测差补**、**P2 光栅对准门控**、**P3 恢复 PiSetHome**。

### 2.2 本阶段两条并行线索

```text
线索 A（层间）：按 06-06 P1 实现并试打「Flush/525 差补」→ 06-09 失败，回查采样域错误
线索 B（Pass 间）：单层精细日志（14:49）→ 发现层内 Pass0/1/2 同位置 absX 漂移，与层间问题独立
```

---

## 3. 问题一：06-09 层间差补试打失败

### 3.1 现象

- 启用层间 absX 差补后试打，**层间 X 偏差比差补前更严重**（量级可达数毫米以上）。
- Pass 间相对几何在差补开启时 **反而正常**（三层同向同偏移，拼接关系不变）。

### 3.2 日志与命令侧对比

差补开启时典型异常：

| 检查项 | 差补前（06-06 基线） | 差补失败时 |
|--------|----------------------|------------|
| Pass0 FWD `imageCmdXStart` | **4110**（各层相同） | **4147** 等（逐层变化） |
| Pass1 REV | **8462** | **8435** 等 |
| Pass2 FWD | **4110** | **4067** 等 |
| Flush `liveAbsX` | **2090/2091**（固定） | 仍 ~2091，但差补按错误采样计算 |

### 3.3 根因（采样域与标定域不一致）

| 域 | 典型 absX24 | 用途 |
|----|-------------|------|
| **525 等停**（`Pass0AtApproachHold`） | ~1969 / 1932 / 2012（层间有散差） | 旧差补 **错误地在此采样** |
| **Flush 发图绑定点** | **~2091**（batch unified 锁定） | **`4110` 标定域**；三层 swath 共用此基准 |

**逻辑错误：**

```text
标定：Xleft=4110 对应 Flush 时 liveAbsX≈2091
旧差补：用 525 处实测与首层 ref 做 delta，再叠加到 4110
结果：把「525 散差」误当成「Flush 域偏差」→ 层间 Xleft 被错误推拉
```

差补时 Pass 间仍正常，是因为 **三层 swath 在同一 Flush 基准上被同等偏移**，层内相对几何不变；层间却被人为拉散。

### 3.4 代码修正（`4-MeteorPrintEngine.cs`）

| 项 | 修正内容 |
|----|----------|
| 默认开关 | `METEOR_LAYER_ABSX_DELTA_COMP` **须显式 `=1` 才开启**；默认 **关** |
| 采样时刻 | `CaptureLayerAbsXDeltaCompAtFlush` — **仅在 Flush 完成时**捕获 absX，不再在 525 快照采样 |
| 死区 | `METEOR_LAYER_ABSX_DELTA_DEADZONE_PX`（默认 **30** count），抑制微小抖动 |
| 差补公式 | `fwdBase = 4110 + (首层 flush absX − 本层 flush absX)`，经 `TryGetLayerCompensatedFwdBasePx` 应用 |
| 清理 | 从 `LogDualCoordSnapshot` 移除 525 差补采样，避免双路径写入 |

### 3.5 当前对层间差补的态度

- **默认关闭**，量产行为 ≈ 06-06「好很多」固定 4110/8462/4110。
- 若再启用 P1，必须在 **Flush 域**验证：日志应出现 `[MeteorLayerAbsXDeltaComp]`，且 `imageCmdXStart` 变化与 **Flush absX 层间差**同向、同量级。
- **禁止**再用 525 等停 absX 直接驱动 `JobUnifiedAlignFwdBase`。

---

## 4. 问题二：同层 Pass 间 X 拼接偏差（14:49 单层日志）

### 4.1 现象（仅 X 向）

| Pass | `imageCmdXStart` | 开扫锚点 absX24 | 15mm 低端 absX24 | 相对 Pass0 |
|:----:|:----------------:|:---------------:|:----------------:|:----------:|
| 0 FWD | 4110 | **2091** | 10061 | 基准 |
| 1 REV | 8462 | **10119**（15mm 处） | — | 同位置 **+58 count ≈ 3mm** |
| 2 FWD | 4110 | **2094**（525mm 处） | 10078 | 低端 **+17 count**；整体右偏约 **4mm** |

肉眼：Pass0 准，Pass1 右偏约 3mm，Pass2 右偏约 4mm。

### 4.2 根因（非公式错误）

命令侧 **4110 / 8462 / 4110 正确**，与 06-01 batch unified 设计一致：

```text
Flush 时刻（pass0 扫程前）：
  JobUnifiedAlignFwdBase captured=4110, liveAbsX≈2091
  pass1 REV: batch_unifiedRev → 4110 + 4352 = 8462
  pass2 FWD: batch_unifiedFwd → 4110

Pass0 扫程中 pass1/2 在 pass0 运动时连续下发（batch legacy）：
  禁止用扫程中 live AbsX 做 pass1/2 Xleft（代码注释已说明）
  → 三层 Xleft 在 Flush 锁死

扫程进行中 PCC absX 随编码器漂移：
  Pass1 开扫时 absX 已 ≠ Pass0 开扫时
  → 同固高 mm 上 absX count 不重复 → 粉床 Pass 间错位
```

### 4.3 与层间问题的关系

| 维度 | 层间 ~1mm | Pass 间 3–4mm |
|------|-----------|---------------|
| **绑定点** | 每层一次 Flush，三层共用同一 `liveAbsX` | 同一 Flush 锚点，但 **扫程后** absX 已变 |
| **Xleft 命令** | 层间可相同仍偏 | 层内命令正确仍偏 |
| **差补开启时** | 层间更差（错误采样） | Pass 间 **相对正常**（三层同偏移） |

→ **层间与 Pass 间是两类问题**，需分别处理；不能指望只修层间差补同时修好 Pass 拼接。

---

## 5. 概念补充

### 5.1 三套数仍不能混用

| 读数 | 含义 |
|------|------|
| **固高 525mm / 15mm** | 运动卡定位；与 PCC absX **无固定换算** |
| **Flush liveAbsX ≈ 2091** | batch unified 发图绑定点（`4110` 标定域） |
| **525 等停 absX** | 过 Home 后光栅重复性样本；**≠ Flush 域** |
| **4110 / 8462** | `PCMD_IMAGE Xleft`；在 **某一 absX 基准** 下标定 |

### 5.2 batch legacy 层内发图时序（不变）

```text
750：Preheat → StartJob（默认不 PiSetHome）→ Queued×3
525：Flush（3× STARTSCAN+IMAGE+ENDDOC）→ 锁死 unified Xleft
525→15：Pass0 扫程（pass1/2 数据已在队列）
扫程中：Pass1 REV、Pass2 FWD 按队列消费，absX 持续变化
```

### 5.3 新光栅 + 750mm 映射的局限

- **750mm 单点映射**可改善层间（等停重复性优于 525），但 **不能自动解决 Pass 间**偏差。
- Pass 间需要在 **每个 pass 开扫前**按实测 absX 做补偿，或 per-pass 固定 trim（方案 A）。

---

## 6. 解决思路汇总（按问题分类）

### 6.1 层间套准（接续 06-06 P1–P4）

| 方案 | 思路 | 状态 |
|------|------|------|
| **P1 Flush 差补** | Flush 时实测 absX 与首层 ref 差，叠加到 4110 | 代码已修正采样域；**默认关**，待再评估 |
| **P2 525 光栅门控** | 525 等停后轮询 absX，进入 ±10～20 count 再 Flush | 未实现 |
| **P3 PiSetHome** | 750 或 525 置零 + 对应 Xleft（06-04 / 06-05 C） | 高精度分支，节拍最长 |
| **P4 日志增强** | Flush/525 实测写入 coverage，去掉 `UNCALIBRATED_LIVE` 歧义 | 部分已有双坐标快照 |

### 6.2 Pass 间套准（硬件不变，软件备选）

| 方案 | 思路 | 侵入性 | 状态 |
|------|------|--------|------|
| **A** | per-pass 固定 X trim（Pass1 REV / Pass2 FWD 微调 `imageCmdXStart`） | 低 | **已入代码**（`GetPassIntraLayerXTrimPx`，默认 0，仅 batch legacy） |
| **B** | 开扫前读 live absX，动态改 pass1/2 Xleft | 中 | 待评估（batch 预灌与扫程中 live 读数冲突需设计） |
| **C** | split-job-per-pass，每 pass 独立 Flush/锚点 | 高 | 已有 split 模式，节拍与队列行为不同 |
| **D** | 扫程中重新 unified 基准 | 高 | 与供应商 batch 语义冲突大 |
| **E** | 机械/光栅：Home@650、换光栅、750 映射 | 硬件 | 中长期 |

**当前优先：** 在 06-06 基线上用 **方案 A** 试打标定 Pass1/2 trim；层间仍保持差补默认关，避免 06-09 回归。

---

## 7. 当前软件基线（代码对齐）

### 7.1 `4-MeteorPrintEngine.cs`

| 项 | 当前默认 |
|----|----------|
| `IsPiSetHomeAtJobStartEnabled()` | **false** |
| `GetDefaultForcedFwdXStartPx()` / 固定 FWD | **4110** |
| `METEOR_LAYER_ABSX_DELTA_COMP` | **关**（须 `=1` 开启） |
| 差补采样 | **Flush**（`CaptureLayerAbsXDeltaCompAtFlush`） |
| Pass 间方案 A | `GetPassIntraLayerXTrimPx` + `WriteImageLayer` 内叠加；**默认 trim=0** |
| batch / Flush / 525 行程 | 与 06-01/06-06 相同，未改发送格式 |

### 7.2 时序（未改）

```text
层末 / 清洗后：墨车 750mm → Preheat → StartJob（默认不 PiSetHome）→ Queued×3
Sleep 1800ms → X 至 525mm → Flush → BatchPostSendDepartDelay → 525→15 扫程
```

---

## 8. 日志验收标准

### 8.1 层间基线通过（当前量产）

```text
PiSetHome skipped (default off)
JobUnifiedAlignFwdBase captured=4110 liveAbsX=2090/2091
imageCmdXStart=4110/8462/4110（各层相同，差补关）
PassScanOriginAnchor: ~2091 / ~10118 / ~2093（层间可比）
```

### 8.2 层间差补再试验（仅当显式开启 P1）

```text
[MeteorLayerAbsXDeltaComp] FirstLayerRefCaptured refAbsX24AtFlush=...
Flush 采样 absX 与 ref 差 → deltaCompPx 与层间粉床偏合同向
不应再出现「525 采样驱动、Flush liveAbsX 仍 2091」的矛盾组合
```

### 8.3 Pass 间（方案 A 试打）

```text
[MeteorPassXTrim] passIndex=1 ... before=8462 after=...
[MeteorPassXTrim] passIndex=2 ... before=4110 after=...
Pass0 无 trim 行；Pass1/2 低端 absX 与 Pass0 同位置差应缩小
```

### 8.4 失败回退

| 现象 | 动作 |
|------|------|
| 层间再度 >10mm 整体偏移 | 查首层 810/A 簇；临时 `METEOR_PISET_HOME_AT_JOB_START=1` 回退 06-04 |
| 差补后层间更差 | 确认 `METEOR_LAYER_ABSX_DELTA_COMP=0`；查是否误用 525 采样 |
| Pass 间仍 3–4mm | 在方案 A 试打基础上考虑 B 或硬件路线 |

---

## 9. 环境变量速查（本阶段新增/变更）

| 变量 | 默认 | 说明 |
|------|------|------|
| `METEOR_LAYER_ABSX_DELTA_COMP` | **关** | `=1` 开启 Flush 域层间差补 |
| `METEOR_LAYER_ABSX_DELTA_DEADZONE_PX` | 30 | 差补死区（count） |
| `METEOR_PASS_INTRA_X_TRIM` | 开 | `=0` 关闭方案 A |
| `METEOR_PASS1_X_TRIM_PX` / `METEOR_PASS2_X_TRIM_PX` | 0 | Pass1/2 trim（px）；另有 `_MM` 换算 |
| `METEOR_PISET_HOME_AT_JOB_START` | 关 | 见 06-06 |
| `METEOR_IMAGE_XSTART_FIXED_FWD_PX` | 4110 | 见 06-01 |

---

## 10. 相关源码索引

| 主题 | 文件 | 入口 |
|------|------|------|
| 层间 Flush 差补 | `4-MeteorPrintEngine.cs` | `IsLayerAbsXDeltaCompEnabled`, `CaptureLayerAbsXDeltaCompAtFlush`, `TryGetLayerCompensatedFwdBasePx` |
| batch unified Xleft | 同上 | `ResolveHiPrintCompatImageCmdXStart` → `batch_unifiedRev_fwdBasePlusWidth` / `batch_unifiedFwd_jobBase` |
| Pass 间方案 A | 同上 | `GetPassIntraLayerXTrimPx`, `WriteImageLayer`（`TryGetForcedImageXStartPixels` 之后） |
| 双坐标快照 | 同上 / `手动操作界面.cs` | `LogDualCoordSnapshot`, `Pass0AtApproachHold`, `PassScanOriginAnchor` |
| batch Flush | `4-MeteorPrintEngine.cs` / `5-SharpControl.cs` | `FlushDeferredLegacyBatchSwaths` |

---

## 11. 附录：本阶段已排除的假设

| 假设 | 结论 |
|------|------|
| 06-09 层间更差是机械问题 | **否**；差补采样域错误，命令侧 Xleft 被人为改乱 |
| Pass 间偏差是 8462 公式算错 | **否**；4110+4352=8462 正确 |
| 层间差补能同时修好 Pass 间 | **否**；差补三层同偏移，Pass 间相对几何不变 |
| 525 与 Flush absX 可互换 | **否**；标定域在 Flush ~2091，525 典型低 ~120 count |
| 仅改 750 映射即可修好 Pass 间 | **否**；需 per-pass 补偿或方案 A/B |

---

## 12. 与后续工作的衔接

1. **层间**：在修正后的 Flush 差补上小流量复测，或并行评估 P2 门控；大错位回退 06-04 PiSetHome 分支。  
2. **Pass 间**：方案 A 试打标定后记录 `[MeteorPassXTrim]` 与粉床测量；不足再评估方案 B。  
3. **机械**：Home@650、新光栅与 750 映射仍可作为中长期项，与软件 trim 正交。  
4. **文档链**：06-06 文档仍作无 PiSetHome 基线；06-04 作高精度分支；本文档作 **06-09 差补教训 + Pass 间分析 + 方案 A** 记录。

---

*最后更新：2026-06-09 — 接续 06-06；记录层间差补试打失败与 Flush 采样修正；单层 14:49 日志证实 Pass 间 absX 漂移；方案 A 已入代码，量产默认仍为固定 4110/8462/4110 + 差补关。*
