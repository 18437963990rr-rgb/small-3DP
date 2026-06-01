# Meteor 3PASS split-job-per-pass 诊断记录（2026-05-29）

## 背景

10:14 的 gate-split 测试确认：Pass1/Pass2 的 `STARTSCAN+IMAGE+ENDDOC` 已经不再提前发送，而是绑定各自 pass gate 后发送。Pass1 的 REV 命令、`IMAGE xStart=11515`、自然 PD `AbsXCount=10564` 均落在理论窗口内；Pass2 的 ForcePD 前后 PCC AbsX 也未在触发瞬间清零。

因此当前最强假设从“STARTSCAN 提前”转为：同一 `STARTJOB` 内 Pass0 后，Meteor 可能未重新触发，或未释放内部 scan window / PD lockout。

## 本次改动

新增实验开关：

- `METEOR_SPLIT_JOB_PER_PASS=1`

开启条件：

- `METEOR_BATCH_SWATH_MODE=1`
- `METEOR_BATCH_STARTSCAN_GATE_SPLIT=1` 或不设置
- `METEOR_SPLIT_JOB_PER_PASS=1`

开启后的行为：

1. 仍先在软件内缓存 3 个 strip。
2. 每个 pass 到自身 gate 后，单独发送 `STARTJOB -> STARTSCAN -> IMAGE -> ENDDOC`。
3. 该 pass 的 X 扫程物理结束后，再由运动线程发送对应 `ENDJOB`。
4. Pass0/Pass1/Pass2 之间的 Y 运动仍在各自 pass 扫程结束后执行，避免数据线程提前结束 job 导致收口或进度提前。

## 建议测试环境

```powershell
$env:METEOR_BATCH_SWATH_MODE='1'
$env:METEOR_BATCH_STARTSCAN_GATE_SPLIT='1'
$env:METEOR_BATCH_ENDJOB_IMMEDIATE='0'
$env:METEOR_SPLIT_JOB_PER_PASS='1'
```

## 判定逻辑

- 如果 Pass1/Pass2 恢复出图，根因基本锁定为同一 `STARTJOB` 多 swath 的 Meteor 内部窗口/PD lockout 复用问题。
- 如果仍只有 Pass0 出图，则继续排 Pass1 REV 的触发策略、PCC lane/window 状态，Xleft 本身的优先级降低。

## 代码位置

- `1-3DP主控界面(人机交互模块)/4-MeteorPrintEngine.cs`：新增 `IsSplitJobPerPassEnabled()`，并将配置写入 `DescribeBatchSwathModeConfig()` 日志。
- `1-3DP主控界面(人机交互模块)/5-SharpControl.cs`：在 BATCH-GATE-SPLIT 缓存发送循环内，按 pass 单独 `StartJob/SendStartJob`，swath 完成后标记延后 EndJob。
- `1-3DP主控界面(人机交互模块)/手动操作界面.cs`：Pass0/Pass1 扫程结束后，在 split-job 模式下补 `TryCompleteDeferredEndJob()`；Pass2 原有收口后 EndJob 路径保留。
