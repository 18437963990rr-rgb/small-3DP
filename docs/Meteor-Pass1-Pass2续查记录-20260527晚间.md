# Meteor Pass1/Pass2 续查记录（2026-05-27 晚间）

> 适用项目：LaserAdd_3DP 主控 + Meteor PCC-E / Starfire  
> 关联文档：`Meteor-Pass0打印排查与解决记录.md`、`Meteor-Pass1-Pass2恢复排查与解决记录.md`

---

## 1. 本轮续查目标

在 `Pass0` 已基本稳定的前提下，继续收敛下面两个问题：

1. `Pass1` 为什么“命令下发成功、也有 PD，但仍然看不到喷墨”
2. `Pass2` 为什么“命令下发成功，但多轮日志里没有新的 PD”

本轮不再泛化讨论 `Pass0`、`cfg` 或成型缸，而是只盯：

- `Pass1 REV` 的 `xStart` 是否仍偏移
- `Pass2 FWD after REV` 是否真正重新进入 PD / 播放窗口

---

## 2. 为提高判读效率做的日志收敛

为了避免现场日志被过程性输出淹没，本轮先做了日志精简。

### 2.1 删除或压缩的日志

- `Pass0Entered`
- `Pass0AfterFlashOff`
- `Pass0AtApproachHold485`
- `Pass0StartJobReadyDelayBegin/End`
- `FirstScanAccelStart`
- `Pass0ScanMotionIssued`
- `WaitBegin / WaitReleased`
- `Pass0FirstSwathWaitBegin/End`
- `PassSwathWaitBegin/End`
- `MeteorScanMotionGate` 中的 `Poll`
- 每秒刷新的 `MainAxis8Monitor`

### 2.2 保留的关键日志

- `Pass0AtApproachHold`
- `Pass0AtScanLowEnd`
- `Pass1AtScanHighEnd`
- `Pass2AtScanLowEnd`
- `Pass2AfterScanHighEnd`
- `DeferredEndJobPending`
- `DeferredEndJobCompleted`
- `PassSwathWaitTimeout`
- `WaitTimeout`
- `anchor=...`

### 2.3 一个重要教训

最初为了降噪，一度把下面两处调用删掉了：

```csharp
MeteorPrintEngine.LogScanPassMotionCheck("Pass1AfterScan", GetCurrentPos(1));
MeteorPrintEngine.LogScanPassMotionCheck("Pass2AfterScan", GetCurrentPos(1));
```

后来确认这不是单纯的日志输出，它还负责写入：

- `_passScanEndAbsX[1]`
- `_passScanEndAbsX[2]`

删除后直接导致：

- `pass1EndAbsX=n/a`
- `Pass2` 退回 `anchor=passN_liveAbsX_fwd`

最终处理方式是：

- 恢复调用
- 只把成功日志静默化
- 保留内部状态写入

---

## 3. 续查过程中的关键判断变化

### 3.1 从“统一时序问题”收敛为“Pass1/Pass2 两类不同问题”

随着日志简化和 `Pass1AfterScan/Pass2AfterScan` 状态恢复，问题逐渐收敛成两类：

1. `Pass1`：有 `StartScan`、有 `Image`、也有 `PD`，但 `REV xStart` 偏右
2. `Pass2`：有 `StartScan`、有 `Image`，但多轮日志都没有新的 `PD`

也就是说，当前已不再是“整个 3PASS 的发送时序全错了”，而是：

- `Pass1` 更像窗口对齐问题
- `Pass2` 更像播放窗口没有重新建立

---

## 4. 关键日志与对应结论

### 4.1 2026-05-27 18:01 这一轮

#### Pass1

- `Rx:StartScan Pcc1 Rev`
- `Rx:Image Plane=1 Xleft=11670`
- `PD1 event AbsXCount=10370`

结论：

- `Pass1` 不是没触发
- 而是 `REV xStart=11670` 相对 `PD=10370` 偏右约 `1300 px`

#### Pass2

- `Rx:StartScan Pcc1 Fwd`
- `Rx:Image Plane=1 Xleft=4321`
- 后面没有新的 `PD`

结论：

- `Pass2` 已发命令，但没有重新触发 PD / 播放窗

---

### 4.2 2026-05-27 19:43 这一轮

针对上一轮问题，做了两件事：

1. 让 `Pass2` 优先使用 `Pass1` 扫程结束锚点
2. 曾尝试在 `Pass2` 前加入 `ForcePD`

#### Pass1

- `Rx:Image Plane=1 Xleft=11333`
- `PD1 event AbsXCount=10163`

结论：

- `Pass1` 偏移从约 `1300 px` 改善到约 `1170 px`
- 修正有效，但不够

#### Pass2

日志中出现：

- `SIG_FORCEPD`
- 随后 `liveAbsX24=26`
- 但 `imageCmdXStart=4362`

结论：

- `ForcePD` 把当前 `AbsX` 参考系拉到了接近 `0`
- 但 `xStart` 还在沿用旧锚点 `4362`
- 造成参考系脱节

因此当时的判断是：

- `ForcePD` 思路不一定错
- 但接入时机错了，不能在扫程前直接粗暴触发

---

### 4.3 2026-05-27 20:04 这一轮

本轮调整后，日志变成：

#### Pass0

- `Rx:Image Xleft=4212`
- `PD1 event AbsXCount=4267`

说明：

- `Pass0` 的 `FWD xStart` 与 `PD` 基本对齐
- `Pass0` 仍是当前最稳定的基准

#### Pass1

- `Rx:Image Xleft=10123`
- `PD1 event AbsXCount=9294`

说明：

- 相比上一轮，偏差又缩小
- 但仍有约 `829 px` 的右偏

结论：

- `Pass1 REV` 的临时左移修正方向正确
- 只是幅度还没完全收够

#### Pass2

- `Rx:StartScan Pcc1 Fwd`
- `Rx:Image Plane=1 Xleft=4390`
- 直到 `Rx:EndJob` 前仍无新的 `PD1 event`

同时软件日志显示：

- `anchor=pass2_pass1MeasuredHighEndAbs_fwd`
- `resolvedBase=4390`

说明：

- `Pass2` 已经不再退回单纯 `liveAbsX`
- 但即使锚点用了 `Pass1` 结束值，仍没有重新建立 PD

结论：

- `Pass2` 当前主症结仍然是“没重新拿到 PD”
- 不再是单纯的 `xStart` 算错

---

## 5. 本轮已落地的代码改动

### 5.1 恢复 Pass1/Pass2 扫程结束状态写入

在 `手动操作界面.cs` 中恢复：

```csharp
MeteorPrintEngine.LogScanPassMotionCheck("Pass1AfterScan", GetCurrentPos(1));
MeteorPrintEngine.LogScanPassMotionCheck("Pass2AfterScan", GetCurrentPos(1));
```

目的：

- 重新写回 `_passScanEndAbsX`
- 让 `Pass2` 能使用 `Pass1` 扫程结束实测值

### 5.2 Pass2 FWD 锚点改为优先使用 Pass1 结束实测值

在 `4-MeteorPrintEngine.cs` 的 `ResolveHiPrintCompatImageCmdXStart(...)` 中增加逻辑：

- `pendingPass >= 2`
- `startScanDir == SD_FWD`
- `!IsBatchSwathModeEnabled()`

优先使用：

- `_passScanEndAbsX[1]`

对应日志锚点：

```text
anchor=pass2_pass1MeasuredHighEndAbs_fwd
```

### 5.3 Pass1 REV 增加可调临时修正

新增：

- `METEOR_PASS1_REV_EXTRA_OFFSET_PX`
- `METEOR_PASS1_REV_EXTRA_OFFSET_MM`

默认值演进：

- 初始：`-1170 px`
- 晚间继续收敛：`-2000 px`

目的：

- 让 `Pass1 REV` 的 `xStart` 继续向现场 `PD` 靠拢

### 5.4 移除“扫程前 ForcePD”的错误接法

曾经尝试在 `Pass2` 启动前直接 `ForcePD`，结果造成：

- `liveAbsX24` 接近 `0`
- `imageCmdXStart` 仍沿用旧锚点

因此撤销了这种接法。

### 5.5 将 Pass2 触发改到 swath-ready 之后

当前 `Pass2` 改为：

```text
SignalPrintThreadPassReadyForMeteorSubmit(pass2)
-> WaitPassSwathMeteorReady(2)
-> TriggerPassForPureMeteorSchedule(...)
-> 再启动 485->15 扫程
```

设计意图：

- 先确保 `Pass2 swath` 已 `ENDDOC` 进入 PCC
- 再在高端触发 `ForcePD`
- 最后启动物理 X 扫程

这比早前“扫描前直接 ForcePD”更符合注释本意，也更接近 Meteor 对播放窗的预期时机。

---

## 6. 自动打印主路径与 Y 步距复核

本轮顺带确认了当前主路径不是 `AutoPrintThread2/3`，而是：

- `EquipmentMotionLogic3(Command=6)`
- `AutoPrintThread5(...)`

其中 3PASS 的 `Y` 步距常量为：

```csharp
private const double InkCarPassPitchYMm = 64.96 * 2.0;
```

即：

`InkCarPassPitchYMm = 129.92 mm`

当前 `AutoPrintThread5` 中的目标位关系为：

- 首条带基准：`passStartBaseY = 55.0 mm`
- `Pass0 -> Pass1`：`55.0 + 129.92 = 184.92 mm`
- `Pass1 -> Pass2`：`55.0 + 2 * 129.92 = 314.84 mm`

本轮日志里的实际到位值与此一致，说明：

- 当前 `Y` 条带步进本身与代码设定一致
- 现阶段更优先的问题仍在 `Pass1/Pass2` 的播放窗，而不是 `Y` 步距本身

---

## 7. 当前阶段结论

截至 2026-05-27 晚间，本轮排查可以得出以下结论：

1. `Pass0` 已是稳定基准，`FWD xStart` 与 `PD` 基本对齐
2. `Pass1` 不是没触发，而是 `REV xStart` 仍偏右，只是比前几轮更接近
3. `Pass2` 的主问题不是空图，也不是 `EndJob` 过早，而是“没有重新拿到新的 PD”
4. `EndJob` 延后到 `Pass2AfterScanHighEnd` 的思路是对的，已不再是首要矛盾
5. 现阶段最需要继续观察的是：
   - `Pass1 IMAGE Xleft` 与 `PD1 AbsXCount` 的差值是否继续收敛
   - `Pass2 StartScan/Image` 之后是否终于重新出现新的 `PD1 event`

---

## 8. 后续建议

下一轮现场若继续验证，建议仍只抓最小关键信息：

- `Pass1`：
  - `Rx:StartScan`
  - `Rx:Image Xleft=...`
  - `PD1 event AbsXCount=...`
- `Pass2`：
  - `Rx:StartScan`
  - `Rx:Image Xleft=...`
  - 是否出现新的 `PD1 event`
- 软件日志：
  - `anchor=...`
  - `Pass1AtScanHighEnd`
  - `Pass2AtScanLowEnd`
  - `DeferredEndJobCompleted`

如果后续在“`Pass2 swath-ready -> ForcePD -> scan`”这套顺序下，`Pass2` 仍长期无新 `PD`，则需要正式评估把 3PASS 临时切到：

```text
每 pass 独立 JOB
StartJob -> 1 strip -> EndJob
```

作为最后的 Meteor 播放窗隔离方案。

