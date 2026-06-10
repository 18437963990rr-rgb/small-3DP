# Meteor 层间与 Pass 间套准 — 讨论思路梳理（至 2026-06-10）

> **文档版本：** 2026-06-10  
> **文档性质：** 讨论梳理 / 决策备忘（非单次试打结案报告）  
> **适用项目：** LaserAdd_3DP 主控 + Meteor PCC-E / Starfire  
> **涉及模块：** `4-MeteorPrintEngine.cs`、`5-SharpControl.cs`、`手动操作界面.cs`  
> **前置阅读（按技术细节深度）：**  
> - [Meteor-层间与Pass间套准-差补试验与方案A-解决记录-20260609.md](./Meteor-层间与Pass间套准-差补试验与方案A-解决记录-20260609.md)（06-09 试打与根因记录）  
> - [Meteor-层间套准-无PiSetHome与525光栅-解决记录-20260606.md](./Meteor-层间套准-无PiSetHome与525光栅-解决记录-20260606.md)（06-06 量产基线）  

---

## 1. 写这份文档的目的

近期对话围绕同一现象反复展开，但容易混谈。本文把**已形成的判断、尚未证实的假设、下一步分叉**分开记录，便于：

- 区分 **层间套准** 与 **Pass 间拼接** 两条线，不再用一套 Xleft 策略「包打天下」；
- 理解当前代码（8462/4110、gate-split、live comp）**在解决什么、没解决什么**；
- 评估 **过几天加装光栅尺** 后，哪些工作可保留、哪些只是过渡；
- 决定下一枪试打应验证**哪条假设**，而不是继续拧常数。

---

## 2. 当前共识（一句话 + 一张表）

**一句话：** 层间与 Pass 间是**两类独立问题**；当前量产路径「层间交给物理 Home + Flush 金样 Xleft」会把 Pass 间一直存在的 absX 漂移**暴露出来**；在 PCC 光栅域与 Xleft 标定域未统一前，任何「只改 Xleft 数值」的方案都只能是**近似或过渡**。

| 维度 | 层间 | Pass 间（同层 P0/P1/P2） |
|------|------|--------------------------|
| **关心什么** | 第 N 层与第 N+1 层图形是否上下对齐 | 同层三条 swath 在 X 向是否拼严 |
| **典型量级（近期）** | ~1–3mm（Home 后） | 数 mm～厘米级（视策略与测法） |
| **绑定点** | 每层 STARTJOB / Flush 时刻的 PCC 状态 | 每个 pass **开扫前**的 PCC absX |
| **当前默认策略** | 物理过 Home；层间 absX 差补**默认关** | 金样 **4110 / 8462 / 4110** + gate-split 下发 |
| **曾出现的「反常」** | 层间差补开错采样域 → 层间**更差** | 此时 Pass 间**相对正常**（三层被同等偏移） |

---

## 3. 现场观察与试打结论（时间线）

```text
06-06  量产基线：无 PiSetHome + 固定 Xleft4110；层间 ~1–3mm；525 处 absX 层间散差大
06-09  层间差补试打失败：525 采样 ≠ Flush 标定域 → 层间恶化；Pass 间反而显得「能拼」
06-09  单层日志：Pass0 准、Pass1 右偏 ~3mm、Pass2 右偏 ~4mm（命令侧 4110/8462/4110 仍「正确」）
06-09  方案 A（env 固定 per-pass X trim）→ 已实现后又删除（不能跨次复现、与域问题纠缠）
06-09  方案 B（开扫 gate 动态 live absX comp）→ 进行中
06-09  17:35  方案 B 首打：Pass1 comp=-405 → 严重右偏（公式全量套用 pass0Low 波动）
06-09  17:35  修正 overlapNominal + cap 后：Pass1 comp=-68；肉眼 Pass0/Pass2「似对齐」
06-09  17:47  卡尺复测：Pass0/Pass2 仍有数 mm；Pass1 有所收敛仍偏大
```

### 3.1 17:47 试打关键数字（验收锚点）

| 观测点 | absX24 | 说明 |
|--------|--------|------|
| Pass0 @15mm 低端 | **10005** | `pass0Low`，Pass1 comp 基准 |
| Pass1 开扫锚点 @15mm | **10119** | raw overlap = +114 |
| Pass2 @15mm 低端 | **10093** | 相对 Pass0：**+88 count ≈ 5.6mm** |
| Pass1 下发 Xleft | **8394** | 由 8462 + comp **-68** |
| Pass2 下发 Xleft | **4110** | comp **= 0**（旧公式 -4，死区 30 吃掉） |

**与卡尺一致：** Pass0/Pass2 数 mm 误差 ↔ 日志低端差 +88；肉眼「对齐」不可靠。  
**Pass1 厘米级若成立：** 命令只动了 ~4.3mm，说明**主因可能不在** `anchor1 - pass0Low` 这一段，或另有 Y/PD/时序因素。

---

## 4. 三套「数」不要混用（讨论反复出现的根因）

| 读数 / 域 | 典型量级 | 含义 |
|-----------|----------|------|
| **固高 mm**（525 / 15 / 750） | 机械定位 | 运动卡坐标；与 PCC absX **无固定换算** |
| **Flush / 525 发图域 absX** | ~**2090** | `4110` / `8462` 金样标定所绑定的 PCC 域 |
| **15mm 换向 / 扫程端 absX** | ~**10000+** | `pass0Low`、Pass1 开扫锚点等实测 |

```text
金样 Xleft（4110/8462）标定在 Flush 域（absX≈2090）
锚点采样在 15mm 域（absX≈10000）
→ 二者之间没有已在代码中证明的线性「count 差 = Xleft 该动多少 px」
```

这也是「自动按锚点算」仍可能大偏差的**结构性原因**，不只是参数没调好。

---

## 5. 方案演进与各自定位

### 5.1 层间：Flush 域差补（P1）

- **意图：** 每层 Flush 时测 absX，相对首层 ref 微调 `4110`。  
- **06-09 失败：** 曾在 **525 等停**采样，却去改 **Flush 域**金样 → 层间被拉散。  
- **代码现状：** 采样已改 Flush；**默认仍关闭**（`METEOR_LAYER_ABSX_DELTA_COMP` 须显式 `=1`）。  
- **与 Pass 间关系：** 差补错域时 Pass 间「相对正常」，说明三层**同向同偏移**时拼接关系不变——再次证明两类问题要分开看。

### 5.2 Pass 间：方案 A（env 固定 trim）— 已废弃

- 手工设 `METEOR_PASS_X_TRIM_*` 等，每次打印同一常数。  
- **问题：** pass0Low 层间波动大（9713 / 10005 / 10014）；固定 trim **不能跟跑**；与 Flush 锁死 8462 叠加后难解释。  
- **结论：** 与「按光栅自动算」相比，只是**谁填常数**的差别，未解决域不一致。

### 5.3 Pass 间：方案 B（live absX comp）— 当前代码默认开启

**机制：**

```text
Xleft_最终 = 金样底座（4110 / 8462 / 4110）+ TryComputePassLiveAbsXCompPx()
```

- Pass0：Flush 预灌，底座不动；comp 不作用于 Pass0。  
- Pass1 REV：`comp = -( (anchor1 - pass0Low) - overlapNominal )`，有 cap / 死区。  
- Pass2 FWD（已改）：`predictedLowExcess = (anchor2-anchor0) + (anchor1-pass0Low) - overlapNominal`，再取负 comp。  
- **gate-split：** Pass0 Flush 后，Pass1/2 在各自机械 gate 再 `WriteImageLayer`。

**环境变量（调参用，非终态）：**

| 变量 | 默认 | 作用 |
|------|------|------|
| `METEOR_PASS_LIVE_ABSX_COMP` | 开 | 总开关；`=0` 关闭 |
| `METEOR_PASS1_LIVE_ABS_OVERLAP_NOMINAL_PX` | 46 | Pass1 名义重叠，从 raw 中扣除 |
| `METEOR_PASS_LIVE_ABSX_COMP_MAX_PX` | 100 | 单次 comp 上限 |
| `METEOR_PASS_LIVE_ABSX_COMP_DEADZONE_PX` | 30 | 小于此不补偿 |

**06-10 代码小改（相对 17:47 试打）：** Pass2 改用 `predicted15mmLowExcess`；gate-split 时关闭 `useUnifiedRevXStart` 与 comp 双路径；增加 `[MeteorPassLowAlign]` 日志。

### 5.4 方案 B 与 env trim 的异同（讨论结论）

| | env 固定 trim | 方案 B live comp |
|--|---------------|------------------|
| **执行手段** | 改 `imageCmdXStart` | 相同 |
| **底座** | 4110 / 8462 / 4110 | 相同 |
| **补偿从哪来** | 人填常数 | 开扫 gate 读锚点公式算 |
| **能否跟 pass0Low 波动** | 不能 | 能，但只动「小 trim」 |
| **本质** | 经验旋钮 | **仍是**金样上的经验旋钮，不是锚点重算底座 |

---

## 6. 为何「每次按光栅锚点自动算」上次仍有较大误差

讨论形成的**五条原因**（可单独或叠加）：

1. **Pass2 上次 comp=0**  
   旧公式 `anchor0-anchor2=-4` < 死区 30 → 卡尺数 mm 必然还在。

2. **Pass1 只补偿 ~4mm**  
   若偏差达厘米级，公式覆盖的物理量不够；需怀疑 Y 条带、REV 喷窗、开扫时序等。

3. **锚点域 ≠ Xleft 标定域**  
   在 ~10000 段读锚点差，去改 Flush 域标定的 8462/4110，缺少严格换算。

4. **故意只补一部分**  
   overlapNominal、deadzone、compMax 都会削掉可用补偿量。

5. **更准的代码路径在 batch 下被禁用**  
   `ResolveHiPrintCompatImageCmdXStart` 中已有：
   - Pass1 REV：`pass0MeasuredLowEnd + offset`  
   - Pass2 FWD：`pass1MeasuredHighEnd`  
   但条件为 `!IsBatchSwathModeEnabled() || IsSplitJobPerPassEnabled()`，**当前 batch + gate-split 仍走 unified 8462/4110**，锚点只用于 comp，**未用于替换底座**。

```text
                    ┌─────────────────────────────────────┐
  光栅锚点（15mm）   │ pass0Low、anchor1/2 …（每次在变）     │
                    └──────────────┬──────────────────────┘
                                   │ 公式 → 仅 compPx（有 cap）
                                   ▼
                    ┌─────────────────────────────────────┐
  Xleft 底座        │ 4110 / 8462 / 4110（Flush 金样）      │  ← 基本固定
                    └─────────────────────────────────────┘
```

---

## 7. 待验证方向：gate 锚点定 Xleft（讨论提议，尚未实现为开关）

### 7.1 目的（不是再拧 trim）

做一次 **A/B 对照试打**，回答：

> Pass 间偏差，主要是因为 **8462/4110 底座域错了**，还是因为 **在 8462 上叠 trim 这种机制本身不对**？

| 组别 | 策略 | 状态 |
|------|------|------|
| **A** | 8462/4110 + live comp | 已试；Pass2 mm 级；Pass1 仍大 |
| **B** | gate-split 时启用 `pass0MeasuredLowEnd` / `pass1HighEnd` **直接定 Xleft**，**关闭** 8462 叠 comp | **未做**（建议做成 env 诊断开关） |

### 7.2 B 组若成功 / 失败分别说明什么

| 结果 | 解读 |
|------|------|
| B 明显好于 A | 根因偏「底座域」；后续应沿锚点定 Xleft 收敛，comp 可降级 |
| B 与 A 差不多 | 少在 Xleft 上耗精力；转查 Y、PD、开扫与运动同步 |
| B 更差 | 锚点直接当 Xleft 的换算或 offset 仍错，需标定表 |

**建议实现形式：** `METEOR_PASS_GATE_ANCHOR_XSTART=1` 诊断模式，**默认仍 A**，便于同机对比、日志同口径。

---

## 8. 战略层讨论：层间只交 Home，Pass 拼接又坏

### 8.1 已形成的理解

- **不太管层间、只盯单层 Pass 拼接** → 历史上**能做好**（同层相对几何可控）。  
- **层间交给物理 Home、要逐层对齐** → **Pass 间问题重新变大**（数 mm～厘米）。  
- **层间差补采样错域时** → 层间恶化，Pass 间**相对**正常（三层同偏移）。

这不是「层间策略必然毁掉 Pass」，而是：**当前选用的层间路径（Home + 无 Flush 差补 + Flush 金样 Xleft）与 Pass 开扫时刻的 absX 状态不匹配**。

### 8.2 物理 Home 改善了什么、没改善什么

| | 物理 Home |
|--|-----------|
| **改善** | 每层 JOB 起点机械/光栅状态更可重复 → **层间** |
| **不自动改善** | 同层内 Pass0→Pass1→Pass2 扫程中 PCC absX 漂移 → **Pass 间** |

---

## 9. 加装光栅尺后，方案会不会又要推倒重来？

**判断：会演进，不必从零推翻。**

### 9.1 相对稳定的部分（问题结构与方向）

1. **层间 vs Pass 间仍须分开设计**——光栅尺让读数更准，不消除「三次扫程开扫时刻不一致」。  
2. **按 pass gate 再发 swath** 仍合理——有独立尺后更应「到点、读数、再发图」。  
3. **双坐标日志、锚点快照** 可延续，作为标定与验收基础设施。

### 9.2 可能变化的部分（实现与默认分支）

| 项目 | 现在 | 有光栅尺后（预期） |
|------|------|---------------------|
| **层间** | 主要靠 Home | **P2 门控**（525/750 等 absX 进窗再 Flush/StartJob）+ 正确域差补 |
| **固高 ↔ PCC** | 无稳定换算 | **标定表 / 仿射**；散差减小 |
| **Pass comp** | 经验公式 + cap | 可能**趋近 0** 或仅保留名义重叠 |
| **Xleft 默认** | 金样 + comp | 更可能 **gate 锚点定底座**；金样作回退 |
| **PiSetHome** | 默认关 | 可能「少做或换点做」，不必回到全量 Home 分支 |

### 9.3 分阶段路线图（讨论归纳）

```text
L0（短期，无新尺）
  验证：Pass 间主因是「Xleft 底座域」还是「Y/PD/时序」
  手段：METEOR_PASS_GATE_ANCHOR_XSTART=1 的 A/B 试打
  避免：把 comp 焊成量产终态

L1（加尺前后）
  层间：P2 光栅门控 + Flush 域差补（采样域正确）
  Pass 间：每 pass gate 发图 + 实测 absX 定 Xleft（非仅 trim 8462）

L2（有尺稳定后）
  固高↔PCC 标定；comp 收敛；Home/PiSetHome 作兜底
```

**结论：** 现在做的 gate、锚点、日志是在为 L1/L2 **打地基**；**8462 + 经验 comp** 最可能是**过渡层**，加尺后默认策略大概率调整，但不必推翻整套 Meteor 流程。

---

## 10. 代码现状快照（截至 06-10 讨论）

| 项 | 状态 |
|----|------|
| 层间 Flush 差补 | 已实现，**默认关** |
| 方案 A env trim | **已删除** |
| 方案 B live comp | **默认开**；Pass2 用 `predicted15mmLowExcess` |
| gate-split 发 Pass1/2 | `ShouldUsePassLiveAbsXCompBatchGateSplit()` |
| `pass0MeasuredLowEnd` 定 Xleft | 代码存在，**batch 模式下未走** |
| `METEOR_PASS_GATE_ANCHOR_XSTART` | **未实现**（讨论建议） |
| `[MeteorPassLowAlign]` | Pass2 @15mm 打 pass0Low/pass2Low 差 |

---

## 11. 待办与决策点（整理思路用）

按优先级排列，**不要求一次做完**：

| 优先级 | 事项 | 类型 |
|--------|------|------|
| P0 | 实现 `METEOR_PASS_GATE_ANCHOR_XSTART=1` 诊断开关，做 A/B 试打 | 验证假设 |
| P0 | 试打验收日志：`[MeteorPassLiveAbsXComp]`、`[MeteorPassLowAlign]`、三层 `imageCmdXStart` | 验收 |
| P1 | Pass1 仍大偏时：关 comp 单测 Pass1；查 Y 条带 / REV 窗口 / 开扫时序 | 分叉排查 |
| P1 | 层间：是否在加尺前再评估 **Flush 域**差补（非 525 采样） | 层间线 |
| P2 | 加尺后 P2 门控与标定表设计 | 硬件到位后 |
| — | 勿做：用 525 采样驱动层间 Xleft；勿把 comp 当无需再改的终态 | 红线 |

---

## 12. 开放问题（刻意保留，避免过早定论）

1. Pass1 **厘米级**偏差中，Xleft 与 Y/PD/时序各占多少？  
2. `overlapNominal=46` 是否有机台实测依据，还是来自单次经验？  
3. batch 下禁用 `pass0MeasuredLowEnd` 是历史刻意（防扫程中 live 漂移），gate-split 后是否应**有条件放开**？  
4. 加光栅尺后，层间是否仍「只交 Home」，还是 **Home + 门控** 成标配？  
5. 金样 4110/8462 在未来是**主路径**还是**标定回退值**？

---

## 13. 相关文档索引

| 文档 | 内容 |
|------|------|
| [20260609 差补与方案 A 记录](./Meteor-层间与Pass间套准-差补试验与方案A-解决记录-20260609.md) | 试打数据、根因、代码修正 |
| [20260606 无 PiSetHome 基线](./Meteor-层间套准-无PiSetHome与525光栅-解决记录-20260606.md) | 525 散差、固定 4110 |
| [20260604 PiSetHome 750](./Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md) | 高精度分支 |
| [20260601 batch legacy](./Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md) | 4110/8462 设计来源 |

---

## 14. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-06-10 | 初稿：汇总 06-09～06-10 对话中的问题二分、方案 B 局限、gate 锚点 A/B 提议、光栅尺演进判断 |
