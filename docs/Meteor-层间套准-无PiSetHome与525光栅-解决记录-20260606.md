# Meteor 层间套准 — 无 PiSetHome / 525 光栅散差 — 排查与方案记录（2026-06-06）

> **文档版本：** 2026-06-06  
> **适用项目：** LaserAdd_3DP 主控 + Meteor PCC-E / Starfire  
> **涉及模块：** `4-MeteorPrintEngine.cs`、`5-SharpControl.cs`、`手动操作界面.cs`（`AutoPrintThread5`）  
> **前置阅读（按时间）：**  
> - [Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md](./Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md)（**上一版闭环**：750 等待位 + 默认 PiSetHome）  
> - [Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md](./Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md)（batch + 固定 Xleft 基线）  

---

## 1. 结论摘要

| 项 | 结论 |
|----|------|
| **相对 06-04 的变化** | 量产倾向改为 **默认关闭 PiSetHome**、**Xleft 恢复 4110**，靠 **物理过 Home** 定位以 **压缩节拍** |
| **06-06 现场** | 错层较早期 **10–20mm 大错位明显好转**；与 **Royal 控制系统** 比，X 方向仍有 **细微不齐** |
| **日志主因** | 固高 **525mm 重复到位**，但 PCC **525 处 absX24 层间散差**（典型 **~1723–2088**，极差可达 **~365 count**）；发图仍用固定 **liveAbsX≈2090 + Xleft=4110**（`UNCALIBRATED_LIVE`） |
| **关键认知** | **525mm（固高）≠ 固定 AbsX count**；两套编码器无硬同步；PiSetHome 置零点在 **监视窗实测为 525 等停位**，非 API 即时读数 |
| **当前代码基线** | `IsPiSetHomeAtJobStartEnabled()` **默认 false**；`GetDefaultForcedFwdXStartPx()` **固定 4110**；Preheat→StartJob→Flush 时序 **未改** |
| **待选增强** | ① Flush 用 525 实测差补；② 525 光栅对准门控（±10 count）；③ 恢复 PiSetHome（精度换节拍） |

---

## 2. 接续背景：06-04 之后为何改方向

### 2.1 06-04 已验证方案（文档终点）

[20260604 文档](./Meteor-层间图形错位-PiSetHome750等待位-解决记录-20260604.md) 结论：

- **750mm** 清洗/待机位发 `PreheatReady` → `SendStartJob` 前 **PiSetHome（默认开启）** → 再运动至 **525mm** 灌 swath / 开扫。
- 配合 **batch legacy + 固定 Xleft=4110**，现场复测 **层间图形无错位**。

### 2.2 量产新约束（06-05 讨论）

| 约束 | 说明 |
|------|------|
| **节拍优先** | 每层 `PiSetHome` + `Sleep 1800ms` 等缓冲拉长周期，希望压缩打印时长 |
| **物理 Home 可信** | 750 等待位与清洗完成位统一；Home 传感器有效；认为 **靠过 Home 定位** 可替代软件置零 |
| **保留 batch** | 数据发送格式、三条 swath 预灌 Flush、Pass0 门控 **不变** |

### 2.3 方案演进时间线（06-05 代码试验）

| 阶段 | 备份标签 | 思路 |
|------|----------|------|
| A | `pisethome-wait750`（06-04） | 750 Preheat + 默认 PiSetHome |
| B | `flush525-after-home-cross` | 调整 Flush 与 **750→525 过 Home** 的时序关系 |
| C | `xleft525-home-delta` | **525 处 PiSetHome 置零**后，Xleft 从 4110 扣 **225mm≈3543px** → **567**（400dpi：`225×400/25.4≈3543`） |
| D | **`no-pisethome-xleft4110`**（**当前倾向**） | **取消 PiSetHome**；**Xleft 恢复 4110**；日志 `PiSetHome skipped (default off)` |

> **说明：** 阶段 C 与监视窗「525 处 AbsX 置零、567 可正常打」一致；阶段 D 为节拍妥协，接受 525 处 AbsX **不置零**（典型 **~1900–2050**）。

---

## 3. 概念澄清（本阶段新增）

### 3.1 固高 525mm 与 AbsX count **不能固定换算**

| 坐标 | 含义 |
|------|------|
| **固高 525mm** | `INKCAR_SCAN_APPROACH_HIGH_MM` 打印前等停高端；`googolXmm=525.000` |
| **PCC absX24** | Meteor 光栅/喷墨域计数；`Xleft` 落在此域 |
| **关系** | 代码中 **无** `AbsX = f(525mm)`；仅能 **实测** 或做 **段内统计** |

**当前配置（无 PiSetHome）525mm 处实测：**

| 场景 | absX24 典型值 |
|------|----------------|
| 06-06 短打（10:57，首层清洗） | **2023 / 1915 / 1883** |
| 06-06 长打（11.log，L4–L59） | **1723–2088**（std≈105 count） |
| Flush 发图基准 | **恒 2090**（`JobUnifiedAlignFwdBase`） |
| 开 PiSetHome@525（06-05 验证） | 监视窗 **≈0–1** |

**仅用于喷墨域毫米解释（400dpi，非固高 mm）：**

```text
mm_print = absX24 × 25.4 / 400
2090 → ≈132.7mm    4110 → ≈261.0mm（Xleft 标定域）
```

### 3.2 三套数不要混用

| 读数 | 含义 |
|------|------|
| **750mm** | 固高清洗/待机位（`INKCAR_CLEAN_STATION_X`） |
| **810mm 一带** | X 正限位回零后参考位语义（历史冷启动曾出现 `googol=810, absX=0`） |
| **-69 等** | 界面显示系；**不等于** PCC `AbsXCount` |
| **4110 / 2090 / 2835** | 发图参数（固定 Xleft / Flush live / jobNprt 换算），**不是** 525mm 的 AbsX |

### 3.3 PiSetHome 置零时机（现场与日志差异）

| 观测 | 说明 |
|------|------|
| API 调用后 **立即读数** `deltaAbsX24=0` | 可能 **过早**，不代表未置零 |
| **监视窗** 在 **525mm** 等停位 | 用户确认 **AbsX/Encoder 确实置零**；支持 Xleft=567 方案 |
| **差补应做在「真置零点」** | 若选 PiSetHome 路线，宜在置零后 **轮询 50–200ms** 再记日志（**尚未实现**） |

### 3.4 `UNCALIBRATED_LIVE` 含义

Flush 时 `[MeteorXCoverage] … status=UNCALIBRATED_LIVE`、`measuredAbsXRange=[0,0]`：

- 表示 **未把 525 实测 AbsX** 纳入 `xStart` / `scanRangePx` 计算；
- 各层 `scanRangePx` 恒为 **[2090, 9492]**，`imageCmdXStart` 恒为 **4110/8462/4110**。

---

## 4. 根因分析（06-06 日志）

### 4.1 命令侧：无异常

各层一致：

```text
PiSetHome skipped (default off)
JobUnifiedAlignFwdBase captured=4110 liveAbsX=2090
imageCmdXStart=4110/8462/4110
PassScanOriginAnchor: 2090 / 10118 / 2093
```

→ **不是「第二层未发图」或 Xleft 逐层算错**。

### 4.2 物理侧：750 稳、525 抖

**06-06 长打（`AppLog_Info_20260606_11.log`，57 层统计）：**

| 工位 | absX24 极差 | 固高 mm |
|------|-------------|---------|
| **750 等待位** | **~15 count**（约 -1647～-1662） | ~750 |
| **525 等停位** | **~365 count**（1723～2088） | 恒 525.000 |
| **Pass2 扫完低端** | **~419 count**（9676～10095） | ~15 |

**相对固定发图基准 2090 的 525 偏差：** 约 **-367～-2 count**（std≈**105 count**）。

按现场标定 **~50 count ≈ 3mm** 估算：相邻层 525 差 **30–100 count** → 肉眼 **~2–6mm** 细微不齐，与 Royal「几乎重合」有差距。

### 4.3 首层清洗的作用（06-06 对比）

| 运行 | L1 @750 | L1 @525 | 说明 |
|------|---------|---------|------|
| **10:20 无清洗** | **810mm, absX=0**（A 簇） | 1883 | 冷启动异常 |
| **10:31 / 10:57 有清洗** | **750mm, absX≈-1659**（B 簇） | 1795–2023 | 与 L2/L3 同簇 |

首层清洗 **消除 A/B 两簇跳变（~943 count）**，是大错位好转的重要原因；**剩余**为回程重复过 Home 垫片带来的 **525 散差**。

### 4.4 与 Royal 差距（归纳）

Royal 侧推测（待对照 Royal 日志）：

1. 每层 STARTJOB 前 **PiSetHome**，Flush 时 liveAbsX **≈0**；或  
2. **按实测 AbsX 动态调 xStart**；或  
3. 等停位 **光栅对准门控** 后再发图。

本系统路线 D：**物理 Home + 固定 4110**，大错位已压下，**精细套准**仍受 525 散差限制。

---

## 5. 当前软件方案（代码基线）

### 5.1 `4-MeteorPrintEngine.cs`（对齐 `no-pisethome-xleft4110` 备份）

| 项 | 当前默认 |
|----|----------|
| `IsPiSetHomeAtJobStartEnabled()` | **false**（仅 `METEOR_PISET_HOME_AT_JOB_START=1` 开启） |
| `GetDefaultForcedFwdXStartPx()` | **4110**（`METEOR_IMAGE_XSTART_LEGACY_FWD_AT_750_PX` 可覆盖） |
| `SendStartJob` | 跳过 PiSetHome 时打日志：`PiSetHome skipped (default off; physical home only; …)` |
| `METEOR_MIN_MS_AFTER_STARTJOB_FOR_SETHOME` | 仍保留（默认 **1400ms**，服务 HALT/首条 STARTSCAN，**非 PiSetHome 专用**） |
| batch / unified / 525 行程 | **与 06-01 相同**，未改发送格式 |

### 5.2 时序（与 06-04 相同骨架，PiSetHome 默认关）

```text
层末 / 清洗后：墨车 750mm
    ↓
Pass0AtCleanStationWait（双坐标快照）
SignalPrintThreadLayerPass0PreheatReady
    ↓  数据线程：WaitPreheat → StartJob → SendStartJob（默认不 PiSetHome）→ Queued×3
Sleep 1800ms
    ↓
X 至 525mm → Pass0AtApproachHold（双坐标快照）
SignalPrintThreadLayerPass0ReadyForMeteorSubmit → Flush → BatchPostSendDepartDelay → 525→15 扫程
```

**未改：** Preheat 在 **750**、Flush 在 **525 就绪后**、三层 batch 一次灌满。

### 5.3 代码备份清单

| 文件 | 说明 |
|------|------|
| `Backup/4-MeteorPrintEngine.cs.bak-20260604_114433-pisethome-wait750` | 06-04 闭环（PiSetHome 默认开） |
| `Backup/4-MeteorPrintEngine.cs.bak-20260605_140741-flush525-after-home-cross` | Flush 与过 Home 时序试验 |
| `Backup/4-MeteorPrintEngine.cs.bak-20260605_154149-xleft525-home-delta` | 525 置零 + Xleft=567 试验 |
| `Backup/4-MeteorPrintEngine.cs.bak-20260605_173632-no-pisethome-xleft4110` | **当前量产倾向** |

---

## 6. 现场验证记录（2026-06-06）

### 6.1 日志文件

| 文件 | 时段 | 内容 |
|------|------|------|
| `F:\AppLog_Info_20260606_10.log` | 10:20 / 10:31 / **10:57** | 无清洗、清洗+直打、**首层清洗 3 层**（「好很多」短打） |
| `F:\AppLog_Info_20260606_11.log` | 11:00–11:50 | **长打 L4–L59**，细微不齐分析样本 |

### 6.2 10:57 三层（代表「好很多」）

| 层 | 525 absX24 | Δ vs 2090 | Pass2 末端 absX24 |
|:---:|:---:|:---:|:---:|
| L1 | 2023 | -67 | 9735 |
| L2 | 1915 | -175 | 10042 |
| L3 | 1883 | -207 | 10014 |

525 极差 **140 count**；命令侧 liveAbsX **2090/2091**。

### 6.3 机械规划（讨论中，代码未动）

- Home 传感器拟迁移至 **~650mm** 工位，使 **750→525** 段过 Home 行为更可控；需与 **Xleft / 门控** 一并重标。

---

## 7. 待选增强方案（按侵入性排序）

### 7.1 P1：525 实测差补（推荐优先评估，**不加 PiSetHome**）

**思路：** Flush 时以 `Pass0AtApproachHold` 的 **实测 absX24** 作为 `JobUnifiedAlignFwdBase` / `liveAbsX`，替代固定 **2090**；或在固定 4110 上叠加 `(absX_measured - absX_ref)`。

**优点：** 不改机械、不显著加长节拍。  
**风险：** 需验证 batch 三层 unified 基准一致性；Pass1/2 仍走 unified 偏移。

### 7.2 P2：525 光栅对准门控

**思路：** 525 等停后 **轮询 PCC AbsX**，进入 **±10～20 count** 窗口再 `SignalPass0Ready` / Flush。

**优点：** 减少「固高已到、光栅未到」就发图。  
**缺点：** 极端散差时可能增加等待时间；需定义超时与 fallback。

### 7.3 P3：恢复 PiSetHome（06-04 / 06-05 C 路线）

| 变体 | 置零位置 | Xleft |
|------|----------|-------|
| 06-04 | **750mm**（SendStartJob 前） | **4110** |
| 06-05 C | **525mm**（监视窗置零） | **567**（4110−3543） |

**优点：** 层间 liveAbsX **≈0–1**，最接近 Royal。  
**缺点：** 节拍最长；需保留 1800ms 等缓冲。

### 7.4 P4：日志增强（低风险，建议并行）

- PiSetHome 后 **50–200ms 轮询** `AfterPiSetHome` AbsX，与监视窗对照；
- `[MeteorXCoverage]` 写入 **525 实测** 到 `measuredAbsXRange`，去掉 `UNCALIBRATED_LIVE` 歧义。

---

## 8. 日志验收标准（当前基线）

### 8.1 通过（大错位已消除）

```text
Pass0AtCleanStationWait … googolXmm≈750 … absX24≈-165x（B 簇）
PiSetHome skipped (default off …)
JobUnifiedAlignFwdBase captured=4110 liveAbsX=2090
imageCmdXStart=4110/8462/4110（各层相同）
```

### 8.2 细微不齐判据（相对 Royal）

| 检查项 | 日志线索 | 粉床 |
|--------|----------|------|
| 525 层间散差 | `Pass0AtApproachHold absX24` 相邻层差 **>30–50 count** | X 向 **1–3mm** 级不齐 |
| 发图未跟实测 | 525=2023 但 `liveAbsX=2090` | 与 Δ(2090−实测) 同向偏差 |
| 750 vs 525 | 750 极差 <20，525 极差 >100 | 伺服重复、光栅不重复 |

### 8.3 失败回退

若层间再次出现 **>10mm** 整体偏移：

1. 查首层是否落在 **810/A 簇**（无清洗冷启动）；  
2. 临时 `METEOR_PISET_HOME_AT_JOB_START=1` 回退 06-04；  
3. 或启用 P1 差补后复测。

---

## 9. 环境变量速查（相对 06-04 更新）

| 变量 | 06-04 默认 | **当前默认** | 说明 |
|------|------------|--------------|------|
| `METEOR_PISET_HOME_AT_JOB_START` | **开启** | **关闭**（须显式 `=1` 才开） | 节拍 vs 精度权衡 |
| `METEOR_IMAGE_XSTART_FIXED_FWD_PX` | 4110 | 4110 | 见 06-01 |
| `METEOR_IMAGE_XSTART_LEGACY_FWD_AT_750_PX` | — | 可覆盖 4110 | 06-05 引入 |
| `METEOR_BATCH_SWATH_MODE` | true | true | 不变 |
| `METEOR_BATCH_POST_SEND_DEPART_DELAY_MS` | 1000 | 1000 | 不变 |

---

## 10. 相关源码索引

| 主题 | 文件 | 入口 |
|------|------|------|
| PiSetHome 开关 | `4-MeteorPrintEngine.cs` | `IsPiSetHomeAtJobStartEnabled`, `SendStartJob` |
| 固定 Xleft 4110 | 同上 | `GetDefaultForcedFwdXStartPx`, `TryGetForcedImageXStartPixels`, `pass0_fixedFwdPx` |
| 双坐标快照 | 同上 / `手动操作界面.cs` | `LogDualCoordSnapshot`, `Pass0AtCleanStationWait`, `Pass0AtApproachHold` |
| Coverage 校准状态 | 同上 | `[MeteorXCoverage]`, `UNCALIBRATED_LIVE` |
| 750 Preheat 时序 | `手动操作界面.cs` | `AutoPrintThread5` → `case 0` |
| batch Flush | `4-MeteorPrintEngine.cs` / `5-SharpControl.cs` | `FlushDeferredLegacyBatchSwaths` |

---

## 11. 附录：本阶段已排除的假设

| 假设 | 结论 |
|------|------|
| 板卡「算错」开窗 | 板卡按 AbsX+Xleft 自洽；问题在 **基准未层间对齐** |
| 固定 525mm ⇒ 固定 AbsX | **不成立**；实测散差是细微不齐主因 |
| 仅靠固高伺服精度 | 750 固高极稳、525 光栅散差大 → **光栅/Home 重复性** 为主 |
| 发图太晚导致不齐 | 与供应商队列规则无关；`DocsQueued` 正常 |
| 必须改 batch 格式 | 当前 **Pre heat→Flush→扫程** 可保留，改 **基准源** 即可试 P1 |

---

## 12. 与后续工作的衔接

1. **对照 Royal 日志**：确认 Royal 在等停位 **AbsX 典型值**（是否 ≈0）。  
2. **择一增强**：P1 差补 / P2 门控 / P3 PiSetHome，按节拍与精度需求选型。  
3. **机械**：Home 传感器 **~650mm** 迁移后，需重测 525 absX 分布并重标 Xleft。  
4. **文档链**：若 P1/P3 闭环，可另起 `2026060x` 补充文档；06-04 文档仍作 **高精度分支** 参考。

---

*最后更新：2026-06-06 — 自 06-04「750+PiSetHome」之后，量产改为默认无 PiSetHome + Xleft4110；06-06 日志证实 525 光栅散差与 Royal 细微差距；待选 P1–P4 增强方案。*
