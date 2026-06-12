# Meteor 扫描打印调试记录 - 2026-06-11

本文档整理本次对话中的问题、日志判断、代码修改和后续调试建议。它不是逐字稿，而是面向后续继续调试的工程记录。

## 1. 初始问题

当前扫描打印存在两类主要偏移：

1. 完全交由 Home / PiSetHome 重置 AbsXCount 时，多层打印偏差较小，通常在 1 mm 内，但各 pass 间拼接出现厘米级误差。
2. 使用 Xleft 差补后，pass 间误差缓解，但多层间误差增大到厘米级。

用户补充：HiPrint 软件似乎也是简单定位，虽然也有拼接误差和层间错位，但没有达到厘米级。

## 2. 配置讨论

曾讨论以下配置：

```text
PiSetHome=开
METEOR_LAYER_ABSX_DELTA_COMP=0
METEOR_PASS_LIVE_ABSX_COMP=0
```

这些属于运行时开关，按当前项目实现主要通过系统/进程环境变量读取。建议测试时在启动软件前设置，避免运行中环境变量未被进程刷新。

当前判断：

- `PiSetHome` 应在稳定物理 Home / reference 点执行，而不是在 525 mm 扫程起点执行。
- 实测从清洗/暂停位去 525 mm 时，在约 658 mm 处负方向撞 Home，因此 658 mm 更适合作为 PiSetHome 点。
- 不建议在运动中触发 PiSetHome；应在 658 mm 停稳、确认 Home active 后执行 PiSetHome + PCMD_STARTJOB。

## 3. 13 点日志结论

参考日志：

- `F:/AppLog_Info_20260611_13.log`
- `F:/新建文本文档 (3).txt`

当时主要错误：

- 第一层 `PCMD_STARTJOB r=0` 成功。
- 后续层 `PCMD_STARTJOB busy(r=14)` 多次重试后仍失败。
- 失败后软件仍继续发送 swath，导致 Meteor 报：
  - `Command Sequence Error in StartFifoDoc. StartDoc ignored`
  - `Command Sequence Error. Image data ignored`

直接原因：

- `SendStartJob()` 已返回 false，但主 3PASS 提交流程没有检查返回值，继续发图。

对应修复：

- 在 `5-SharpControl.cs` 的 SimplifiedMeteorAutoFlowMode 主路径中检查 `SendStartJob()` 返回值。
- 若 StartJob 失败，立即停止发送 swath，避免 Sequence Error。

## 4. 658 mm PiSetHome 时序调整

修改目标：

```text
清洗/暂停位 -> 移动到 658 mm Home/preheat 点
停稳 -> PiSetHome -> PCMD_STARTJOB
StartJob 成功 -> 移动到 525 mm
发 swath -> 开扫
```

相关修改：

- `4-MeteorPrintEngine.cs`
  - 新增 pass0 preheat-startjob 完成事件。
  - `SendStartJob()` 成功或失败都会通知运动线程。
  - 新增 `WaitPass0PreheatStartJobDoneIfEnabled()`。

- `手动操作界面.cs`
  - Pass0 先在 750 mm 记录快照。
  - 再移动到 658 mm，刷新光栅快照。
  - 在 658 mm 发 `SignalPrintThreadLayerPass0PreheatReady()`。
  - 等待 PiSetHome + STARTJOB 完成后，才去 525 mm。

## 5. 15 点日志结论

参考日志：

- `F:/AppLog_Info_20260611_15.log`

现象：

1. 从 `X=810,Y=50` 到 `X=750,Y=245` 时，出现先 X 后 Y，不像以前同时运动。
2. 第二层移动到 525 mm 后，X 停止，约 10 秒后突然 Y 向运动到 pass1/pass2 附近坐标。

判断：

- `X=810,Y=50 -> X=750,Y=245` 顺序运动来自 `EnsureInkCarAtCleanWaitStation()`，该函数当时写成 X 等停后再 Y 等停。
- 第二层 525 mm 停 10 秒是 `Pass0FirstSwathWaitTimeout`。
- Pass0 超时后只是 `return` 退出当前 `AutoPrintThread5(PassIndex=0)`，外层调度继续调用 Pass1/Pass2，所以出现后续 Y 运动。
- 第二层没有触发 658 mm 的 `PreheatReady / STARTJOB`，原因是第一层 EndJob 后 `ResetPass0GateAfterMeteorJobEnd()` 把 `_pass0GateEnabledOverride` 清掉。

对应修复：

- 正常 EndJob 后保留自动打印 gate override。
- StopJob / PiAbort 时才清除 gate override。
- Pass0 等不到 swath 或 StartJob/PiSetHome 失败时，设置本层 Meteor motion abort 标志。
- 同层后续 Pass1/Pass2 入口检测 abort 标志后直接跳过，避免继续 Y 换带。
- `EnsureInkCarAtCleanWaitStation()` 改回 X/Y 并发下发不等停，再统一等待到位。

## 6. 16 点日志结论

参考日志：

- `F:/AppLog_Info_20260611_16.log`
- `F:/新建文本文档 (3).txt`

### 6.1 第二/三层是否喷墨

从软件链路看，第二/三层不是没发图。

三层均有完整链路：

- `PCMD_STARTJOB r=0`
- `FlushSwathDone passIndex=0`
- `FlushSwathDone passIndex=1`
- `FlushSwathDone passIndex=2`
- `Pass0FirstSwathWaitEnd released=true`

低层 Meteor 记录也有三组：

```text
Rx:StartJob
Rx:StartScan Fwd / Rev / Fwd
Rx:Image Xleft=4110 / 8462 / 4110
```

因此“层间偏移缓解”不是因为第二第三层没有发图。物理喷嘴实际出墨日志无法百分百证明，但软件和 Meteor 命令链路完整。

### 6.2 pass 间仍有偏移

当前仍处于 `BATCH-LEGACY` 主路径：

```text
METEOR_BATCH_SWATH_MODE=unset => batchSwathMode=True
batchStartScanGateSplit=False
passLiveAbsXComp=False
```

这意味着 3 个 pass 的 swath 在 Pass0 前一次性下发。Pass1/Pass2 后续只走运动，不在各自真实开扫点重新计算 Xleft。

实际 Xleft 固定为：

```text
Pass0: 4110
Pass1: 8462
Pass2: 4110
```

日志中 `MeteorPassLowAlign` 显示 Pass0/Pass2 低端差值大约：

```text
2.10 mm
2.67 mm
0.32 mm
```

这与肉眼可见 pass 间偏移吻合。

当前判断：pass 间偏移的主要软件原因是 batch legacy 提前发图，Pass1/2 未在实际 gate / 开扫点重新绑定 Xleft。

### 6.3 运动不如以前丝滑

当前运动链路中人为加入了 658 mm 停靠点：

```text
Y 先不等停去 55
X 等停到 658
658 mm 执行 PiSetHome + STARTJOB
X 等停到 525
发 swath 后开扫
```

用户观察到“同时 X/Y 运动约 0.5s，随后明显卡顿等停，再切换成不等停运动”，主要来自：

- 658 mm 必须停稳做 PiSetHome + STARTJOB。
- 658 -> 525 当前也是等停移动。

这解决了跨层 gate / StartJob 问题，但牺牲了原来连续去 525 mm 的丝滑度。

## 7. 已做代码修改摘要

### `4-MeteorPrintEngine.cs`

- 增加 pass0 preheat STARTJOB 完成事件。
- `SendStartJob()` 的成功/失败都会释放等待。
- 正常 EndJob 后不清除自动打印 gate override。
- StopJob / PiAbort 后清除 gate override。
- gate reset 日志增加 `clearAutoPrintOverride` 字段。

### `5-SharpControl.cs`

- SimplifiedMeteorAutoFlowMode 主路径检查 `SendStartJob()` 返回值。
- StartJob 失败时停止 swath 发送，避免 Meteor command sequence error。

### `手动操作界面.cs`

- Pass0 在 658 mm Home/preheat 点执行 PiSetHome + STARTJOB 后，再去 525 mm。
- 增加本层 Meteor motion abort 标志，Pass0 失败后同层后续 pass 直接跳过。
- Pass0/1/2 swath 等待失败时写入 abort 标志。
- `EnsureInkCarAtCleanWaitStation()` 改为 X/Y 先同时下发不等停，再分别等待到位。

## 8. 当前建议

### 是否继续微调，还是等光栅尺

建议：

```text
不要继续深挖补偿参数微调；
等光栅尺装上后再做最终定位和补偿标定；
这几天只继续做软件链路稳定和诊断性调试。
```

原因：

- 光栅尺会改变固高定位基线。
- 当前调出来的 Xleft、home 点补偿、pass 补偿很可能需要重做。
- pass 间偏移仍有明显软件时序因素，不应只靠当前硬件状态下的补偿硬调。

### 装光栅尺前可继续做的事

1. 保持 StartJob 失败停发、gate 跨层保持、pass abort 等确定性修复。
2. 做少量诊断实验，验证 pass 间偏移是否来自 batch legacy 提前发图。
3. 不建议继续强行调 Xleft 补偿到极限。
4. 运动丝滑度可先记录，不急于在装光栅尺前追到最终状态。

## 9. 后续可选调试方向

### 方向 A：保留 batch legacy，继续用固定 Xleft

优点：

- 简单。
- 层间当前较稳定。

缺点：

- Pass1/2 不在实际开扫点绑定 Xleft。
- pass 间偏移可能一直存在。

### 方向 B：按 pass gate 下发 swath / STARTSCAN

优点：

- Pass1/2 可在真实开扫点重新绑定 Xleft。
- 更适合解决 pass 间拼接偏移。

缺点：

- 时序复杂度提高。
- 需要重新验证 Meteor command sequence 和运动同步。

### 方向 C：等待光栅尺后重新标定

建议作为最终路径。

装上光栅尺后应重新验证：

- 658 mm Home/preheat 点重复性。
- 525 mm approach 点重复性。
- Pass0/1/2 实际开扫点 AbsX。
- Xleft 标定值。
- 是否仍需要 PiSetHome 每层执行。

## 10. 当前关键结论

1. 第二/三层从软件和 Meteor 命令链路看已经发图成功。
2. 层间偏移缓解不是因为没喷。
3. pass 间偏移仍主要来自 `BATCH-LEGACY` 提前发图和固定 Xleft。
4. 运动卡顿主要来自新增的 658 mm 停靠 PiSetHome/STARTJOB 时序。
5. 光栅尺装上前不建议继续大规模微调补偿，建议只做链路稳定和诊断。
