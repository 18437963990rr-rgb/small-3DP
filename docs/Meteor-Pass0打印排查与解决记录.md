# Meteor Pass0 自动打印排查与解决记录

> 文档版本：2026-05-26  
> 适用项目：LaserAdd_3DP 主控 + Meteor PCC-E / Starfire (SG1024)  
> 涉及模块：`4-MeteorPrintEngine.cs`、`5-SharpControl.cs`、`手动操作界面.cs`

---

## 1. 问题概述

自动打印 pass0 阶段出现多种异常，与 HiPrint 上位机、Beckhoff 下位机对照排查后，现象包括：

| 现象 | 描述 |
|------|------|
| 仅某一 pass 能打出 | 常见为 pass1 有墨、pass0 无墨或错位 |
| 图形模糊 / 拖影 | 能喷但沿扫程方向发糊 |
| pass 图形错位 | pass0 图形出现在 pass1/2 位置 |
| 完全不出图 | xStart 超出实测 AbsX 扫程 |
| 方向反了 | 左右镜像，与预期扫向不符 |

**最终结论：** 问题来自 **多层根因叠加**，需同时处理软件命令层（xStart / 门控 / 扫向）与 Meteor cfg 层（`RightToLeft` 扫向约定）。单独改软件或单独改 cfg 均无法一次性得到「清晰、位置正确」的 pass0 打印。

---

## 2. 核心架构认知（极重要）

### 2.1 两套 X 坐标系，无自动同步

| 坐标系 | 来源 | 用途 |
|--------|------|------|
| **固高 X（光栅）** | 运动控制编码器，单位 mm | 墨车 485mm ↔ 15mm 定位 |
| **Meteor AbsXCount** | PCC 喷墨编码器，400 DPI print clock | `PCMD_IMAGE` 的 xStart、STARTSCAN 对齐 |

**关键事实（现场标定）：**

- 固高扫程：**485mm → 15mm**（机械从高到低）
- AbsXCount：**单调递增**（例：485mm 处 ≈ 4310 px，15mm 处 ≈ 11618 px，跨度 ≈ 7308 px）
- **不可用** `固高 mm × 400 DPI` 换算成 AbsX 设 xStart

### 2.2 两套「pass」概念勿混用

| 名称 | 含义 |
|------|------|
| `stripIndex` 0/1/2 | `SwathImageSplitter` 按 Y 切 ~2048 行条带，一次 STARTJOB 内 batch 发送 |
| `nPassID` 0/1/2 | 墨车物理 3 PASS，`AutoPrintThread5` Command=6 |

数据线程的 strip 与打印线程的物理 pass 若不同步，会导致 pass 错位。

### 2.3 问题分层模型

```
┌─────────────────────────────────────────────────────────┐
│  Layer A：Meteor cfg（PrintEngine 与编码器扫向约定）      │
│  → 能喷但发糊：RightToLeft 与现场不一致                   │
├─────────────────────────────────────────────────────────┤
│  Layer B：软件命令（STARTSCAN 方向 + xStart 左/右缘）     │
│  → 不出图 / 左右反：FWD/REV 与 live AbsX 配对错误         │
├─────────────────────────────────────────────────────────┤
│  Layer C：时序门控（485 等停 → STARTJOB → 485→15 扫描）   │
│  → pass 错位 / 过早 ENDJOB：打印线程与数据线程不同步       │
└─────────────────────────────────────────────────────────┘
```

---

## 3. 排查时间线与主要尝试

### 3.1 第一轮：xStart 与 STARTSCAN 方向配对

**做法：** HiPrint 兼容模式下，按 `startScanDir` 选择 xStart：FWD 用 `xLeft`，REV 用 `xLeft + width`。

**日志表现：** `actualScanDir=REV`，`IMAGE xStart=10163`，`revDeltaApplied=7328`。

**结果：** **完全不出图** — xStart 10163 超出实测 AbsX 扫程（现场高端约 11618，且发图时刻 AbsX 仍在 ~4317 附近）。

**教训：** 不能用 job 基址（如 2835）+ 图宽推算 REV 右缘，必须用 **live AbsX 或 DualCoord 实测区间**。

---

### 3.2 第二轮：扫程高端锚点 + ENDJOB + STARTJOB 延迟

**做法：**

- pass0 REV 时尝试用 `485mm × DPI` 换算 `scanHighPx`（**错误**，仍假设 mm ≡ AbsX）
- `RenderToWic` 条带发完后增加 `SendEndJob()`
- HiPrint 兼容默认不再跳过 STARTJOB 后 ~1400ms 等待

**结果：** 仍基于错误坐标假设，未根本解决。

---

### 3.3 第三轮：双坐标系方案（软件侧）

**做法：**

1. pass0 **默认关闭** STARTSCAN 翻转（`METEOR_PASS0_FLIP_STARTSCAN=1` 可恢复旧行为）
2. pass0 xStart **优先 live AbsX**（`METEOR_IMAGE_XSTART_FROM_LIVE_ABS=0` 可关闭）
3. 新增 `[MeteorDualCoord]`：打印线程在 485 等停、15 到位记录固高 mm 与 AbsX 对照
4. 重写 `ResolveHiPrintCompatImageCmdXStart`：不再用 `MmToAbsXPixels(485/15)` 设 xStart
5. `[MeteorXCoverage]` 改用实测 AbsX 区间

**典型成功日志（pass0 能喷）：**

```
[MeteorDualCoord] stage=Pass0AtApproachHold googolXmm=485.000 absX24=4310
[MeteorXStart] HiPrintCompat ... actualScanDir=FWD pass0DirFlip=False
  liveAbsX24=4317 anchor=pass0_liveAbsX_fwd imageCmdXStart=4317
[MeteorXCoverage] encRange=[4317,11645] status=UNCALIBRATED_LIVE
[MeteorDualCoord] stage=Pass0AtScanLowEnd googolXmm=15.754 absX24=11618
```

**结果：** **pass0 能喷墨**；图形方向可能反；**仍发糊**（见 3.5）。

---

### 3.4 第四轮：REV 与 xStart 右缘配对（软件）

**问题：** FWD 用 `liveAbsX` 作左缘正确；REV 若仍用左缘会导致不出图或错位。

**修复：** REV 扫向时 `imageCmdXStart = liveAbsX + printWidthPx`（图右缘）。

**配合环境变量：** `METEOR_PASS0_FLIP_STARTSCAN=1` → `actualScanDir=REV`，`imageCmdXStart≈11645`。

**结果：** 方向问题可通过软件 REV + 右缘 xStart 改善；**发糊仍未解决**。

---

### 3.5 第五轮：Meteor cfg `RightToLeft`（最终清晰打印）

**现象：** 软件配置改好后 **能喷、图形形状/方向大致正常，但仍发糊**。

**根因（现场验证）：** Meteor cfg 中 `[System] RightToLeft` 与现场编码器/机械扫向约定不一致。

**解决：** 将 `DefaultStarfire_PccE.cfg` 中：

```ini
RightToLeft = 0   ; 原值
```

改为：

```ini
RightToLeft = 1   ; 现场验证值，打印清晰
```

**说明：**

- `RightToLeft` **不是**简单镜像位图，而是 Scanning 模式下 PrintEngine 如何解释 **编码器递增 vs 平台扫向**，影响 print clock 与喷嘴触发对齐。
- 与 `[Encoder] Invert` 不同：`Invert` 反转编码器极性；`RightToLeft` 反转 **传送/扫描方向约定**（全局）。
- 软件 AppLog 中 xStart、DualCoord 均正常时仍可能发糊 — **AppLog 无法直接暴露 cfg 扫向错误**。

**最终结果：** pass0 **打印正常、图形清晰**。

---

## 4. 现场标准配置清单

### 4.1 Meteor cfg（金标准备份）

路径（默认）：

```
C:\Users\Public\Documents\Meteor\Config\PccE\DefaultStarfire_PccE.cfg
```

**必须核对：**

| 项 | 现场验证值 | 说明 |
|----|------------|------|
| `RightToLeft` | **1** | 扫向约定；错则发糊 |
| `Scanning` | 1 | 扫描模式 |
| `Encoder.Invert` | 0 | 勿随意改 |
| `Multiplier/Divider` | 2 / 127 | 400 DPI |
| `XDpi` | 400 | 与软件 RenderDpiX 一致 |

**日志：** `LogToDisk=1`，`LogCommands=1`，`PrintEngine.Log`（常见路径 `D:\SimFiles`）。

### 4.2 软件环境变量备忘

| 变量 | 默认 / 建议 | 作用 |
|------|-------------|------|
| `METEOR_PASS0_FLIP_STARTSCAN` | 按 pass0 实测；REV 时需配合右缘 xStart | pass0 STARTSCAN 是否翻转 |
| `METEOR_IMAGE_XSTART_FROM_LIVE_ABS` | 默认 true | xStart 用发图时刻 PCC live AbsX |
| `METEOR_HIPRINT_CHAIN_COMPAT` | 默认 true | HiPrint 兼容 xStart 规则 |
| `METEOR_HIPRINT_SKIP_STARTJOB_DELAY` | 设为 1 可跳过 STARTJOB 后等待 | 预热时序 |
| `METEOR_GATE_SENDSTARTJOB_ON_PASS0` | 自动打印流程配置 | pass0 门控 |

### 4.3 软件参数

- **`m_dXJetOff`（g_RYSYSParam）**：双向 X 微调，对应 `CCP_BIDI_XADJUST`；用于 pass 间轻微横移，**不能替代** cfg `RightToLeft`。
- **pass0 门控时序：** 485 等停 → PreheatReady → STARTJOB → 延迟 ~1800ms → 485→15 扫描 → 放行 Meteor 发 swath。

---

## 5. 关键日志标签说明

| 标签 | 含义 | 正常 pass0 参考 |
|------|------|-----------------|
| `[MeteorDualCoord]` | 固高 mm vs AbsX24 对照 | 485→4310，15→11618 |
| `[MeteorXStart] PassScanOriginAnchor` | 打印线程放行时 AbsX 锚点 | ≈4317 |
| `[MeteorXStart] HiPrintCompat` | 发图 xStart / 扫向 / anchor | 见实测 |
| `[MeteorXCoverage]` | encRange 是否在扫程内 | status=OK 或 UNCALIBRATED_LIVE |
| `WriteImageLayer: sending STARTSCAN` | actualScanDir | FWD 或 REV（与 flip 一致） |
| `WriteImageLayer: IMAGE xStart=` | 最终下发 xStart | FWD≈4317，REV≈11645 |

**说明：** 发图时刻通常只有 ApproachHold 的 DualCoord，故首轮 `measuredAbsXRange=[0,0]` 属正常；ScanLowEnd 在扫程结束后才写入。

---

## 6. 故障速查表

| 现象 | 优先检查 |
|------|----------|
| 完全不出图 | xStart 是否超出 AbsX 扫程；是否误用 mm×DPI 换算 AbsX |
| 能喷但发糊 | **cfg `RightToLeft` 是否为 1**；Encoder.Invert 是否被改 |
| 左右镜像 | STARTSCAN FWD/REV + xStart 左/右缘是否配对；`METEOR_PASS0_FLIP_STARTSCAN` |
| pass0 图到 pass1 | stripIndex 与 nPassID 混用；batch 发多 strip；门控时序 |
| 只喷一段 | ENDJOB 是否过早（扫程未完成）；扫程宽度 vs 图宽 |
| pass 间横移 | `m_dXJetOff` / BidiXAdjust |

---

## 7. 主要代码改动摘要

| 文件 | 改动要点 |
|------|----------|
| `4-MeteorPrintEngine.cs` | DualCoord 标定；live AbsX xStart；REV 右缘 xStart；Pass0 门控；HiPrint compat；MeteorXCoverage |
| `5-SharpControl.cs` | `RenderToWic` 条带后 `SendEndJob()` |
| `手动操作界面.cs` | pass0 在 485/15 处 `LogDualCoordSnapshot` |

---

## 8. 经验总结

1. **固高 mm 与 Meteor AbsX 是两套坐标系**，必须 DualCoord 标定，不能混算。
2. **能喷 ≠ 配置正确**：xStart 对了仍可能因 `RightToLeft` 错误而发糊。
3. **cfg 变更需纳入版本管理**：重装 Meteor / 换 cfg 备份时，首先核对 `RightToLeft=1`。
4. **HiPrint 与自研软件差异**往往在 PrintEngine 扫向约定 + 发图时序，而非单纯 DPI 或图宽。
5. 后续 pass1/2 验证时，在 pass0 稳定基础上再调 `nPrtDir` 交替扫与 `m_dXJetOff`。

---

## 9. 附录：Pass0 时序简图

```
打印线程                          数据线程
    │                                │
    ├─ 到 485mm 等停                  │
    ├─ DualCoord(ApproachHold)       │
    ├─ PreheatReady ────────────────►│ Wait → StartJob → SendStartJob
    ├─ sleep(1800ms)                 │
    ├─ 发出 485→15 扫描               │
    ├─ SignalPass0Ready ────────────►│ Wait → WriteImageLayer (STARTSCAN+IMAGE)
    ├─ WaitInkCarXAxisReach(15)      │ (可能已 SendEndJob)
    └─ DualCoord(ScanLowEnd)         │
```

---

## 10. 文档维护

- 换喷头 / 改 Encoder 传动 / 重装 Meteor 后，请复测 DualCoord 并更新本文 §2.1 数值。
- 若 pass1/2 有独立结论，可追加章节「3PASS 联调记录」。

---

*本文档由 2026-05 自动打印 Meteor pass0 联调过程整理，供后续维护与换机参考。*
