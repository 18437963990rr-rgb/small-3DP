# Meteor 3PASS 同 Job 窗口与 REV 续查记录（2026-05-28）

## 当前结论

本轮排查确认：当前软件不是一次性把 3 个 swath 全部无门控打包发送到 Meteor，而是一次 `PCMD_STARTJOB` 后，每个 pass 前单独发送该 pass 的 `PCMD_STARTSCAN + PCMD_IMAGE + PCMD_ENDDOC`，最后把 `PCMD_ENDJOB` 延后到 Pass2 物理扫程结束。

也就是说当前链路是：

1. Pass0：等待 Pass0 运动入口/扫描起点信号后，发送 `STARTSCAN FWD + IMAGE + ENDDOC`。
2. Pass1：等待 Pass1 入口信号后，发送 `STARTSCAN REV + IMAGE + ENDDOC`。
3. Pass2：等待 Pass2 入口信号后，发送 `STARTSCAN FWD + IMAGE + ENDDOC`。
4. Pass2 扫程结束后发送 `ENDJOB`。

因此“FWD 参数统一发、swath 数据分开发”的说法目前看不成立。方向参数不在 `IMAGE` 数据体内，而是在每条 swath 前的 `STARTSCAN` 命令内下发；`IMAGE` 命令只携带 plane、xLeft、yTop、width 和图像数据。

## 关键代码位置

- `1-3DP主控界面(人机交互模块)/5-SharpControl.cs`：`SimplifiedMeteorAutoFlowMode` 下负责拆 3 个 swath、设置 `SetPendingSwathPassIndex`、设置 `nPrtDir`，并在非 batch 模式下按 pass gate 等待后调用 `WriteImgLayerData`。
- `1-3DP主控界面(人机交互模块)/4-MeteorPrintEngine.cs`：`WriteImageLayer` 中负责计算 Xleft、组 `PCMD_STARTSCAN`、组 `PCMD_IMAGE`、发送 `PCMD_ENDDOC`，并记录 `[MeteorDirChain]`、`[MeteorXStart]`、`[MeteorXCoverage]` 等诊断。
- `1-3DP主控界面(人机交互模块)/手动操作界面.cs`：不是 swath 数据链本体，但负责 pass0/pass1/pass2 的本地运动时序、等待 swath ready、以及 `TriggerPassForPureMeteorSchedule` / ForcePD 的触发时机。

## 已验证事实

- Pass0 恢复出图后，说明基础图像数据、1bpp payload、`STARTJOB`、FWD `STARTSCAN`、`IMAGE` 发送链路可以工作。
- Pass1 日志中 `nPrtDir=0`、`actualStartScanDir=REV`，Meteor 日志也显示 `Rx:StartScan Pcc1 Rev`，说明 REV 至少已经进入 Meteor 命令链。
- Pass1 `IMAGE` 的 Xleft 曾测试过 `7323`、`11317`、`10567` 等值。`10567` 与 Meteor PD 事件 `AbsXCount=10564` 几乎重合，但仍未出图，说明问题不再只是“Xleft 离触发点太远”。
- Pass2 日志中可见启动/ForcePD 附近疑似导致 ABS 计数器重新归零或窗口被消费；这会影响后续 FWD 图像窗口与实际扫程的对应关系。
- `PCMD_IMAGE` 命令头中没有方向字段。方向由 swath 前的 `PCMD_STARTSCAN` 决定；HiPrint 参考实现也是每条 swath 前发 `STARTSCAN`，然后再发 `IMAGE/ENDDOC`。

## 本轮尝试

1. 将 Pass0 从固定 `xLeft=400` 改回使用实时/锚定 AbsX 体系后，Pass0 可以出图。
2. Pass1 先使用 `xLeft=7323`，观察到图形位置接近行程末端，且 Pass2 出现模糊/变形，怀疑 Pass1/Pass2 窗口或数据播放互相挤占。
3. Pass1 改为接近低端检测/Pass0 低端 AbsX 的 `xLeft=11317`，理论上与窗口更接近，但 Pass1 仍无图。
4. Pass1 再加入 `-800px` 偏移，得到 `xLeft=10567`，与 Meteor 日志 PD `AbsXCount=10564` 基本重合，仍无图。
5. 加入 `[MeteorDirChain]` 诊断，确认 Pass1 的 `STARTSCAN REV`、`IMAGE xStart`、命令头字段均实际写入。
6. 修正误导日志：`WriteImageLayer` 内首 swath STARTSCAN 后不再称 “PiSetHome at pause”，改为明确“不在这里调用 PiSetHome”。
7. 对 Pass2 的 ForcePD 时序做了调整：从“swath ready 后静止端 ForcePD，再启动 485->15”改为“先启动 485->15，再 ForcePD”，并增加 ForcePD 前后 PCC 状态日志。

## 关于尾部余量 / 扫程宽度

代码里确实存在一个“扫程宽度/尾部余量”的概念：`METEOR_SCAN_TRAVEL_MM` 默认 470mm，对应 400dpi 下约 `7402px`。这个值用于日志覆盖判断，也用于 batch/unified 分支里计算反向起点。

当前非 batch、按 pass 逐条发送模式下，Pass1 主要使用 Pass0 低端 AbsX 或 pass anchor 来计算 REV Xleft，并没有直接把 `xLeft + 图像宽 + scanTravelPx` 作为独立命令字段发给 Meteor。

但仍有一个重要风险：Meteor 内部可能按同一个 `STARTJOB/PrintLane` 的文档窗口、PD lockout 或 scan window 管理多个 `STARTSCAN/ENDDOC`。如果内部窗口按“xLeft + width + 余量”延伸，Pass1/Pass2 即使命令坐标看起来正确，也可能被认为仍处在前一 swath/前一方向的工作窗口内。

## 当前主要假设

优先级最高的假设是：同一个 `STARTJOB` 内连续发送 FWD、REV、FWD 三个 swath 时，Meteor 的播放窗口、PD lockout 或内部 scan window 没有像我们预期那样按 `ENDDOC` 完全释放，导致 Pass1/Pass2 虽然收到了命令和图像数据，但没有进入有效喷射窗口。

次级假设：

- Pass1 REV 需要自然 PD / ForcePD 的触发时机与 FWD 不同，目前 pass1 默认没有 ForcePD，可能 REV 窗口被错过。
- Pass2 ForcePD 在静止端触发时会消费窗口或导致 AbsX 归零，本轮已改为运动启动后触发，待下一轮日志验证。
- Xleft 仍可能需要微调，但 `xLeft=10567` 几乎贴近 PD 后仍无图，说明单纯坐标偏差已不是最强解释。

## 建议下一步

1. 用当前版本再打一轮，重点看新增的 `PureMeteorTriggerPass-BeforeForcePD-P2` 和 `AfterForcePD-P2`，确认 Pass2 ForcePD 是否仍导致 AbsX 归零或窗口异常。
2. 若 Pass1/Pass2 仍无图，建议做单变量实验：增加环境开关 `METEOR_SPLIT_JOB_PER_PASS=1`，让每个 pass 独立执行 `STARTJOB -> STARTSCAN -> IMAGE -> ENDDOC -> ENDJOB`。这能直接验证“同 Job 多 swath 窗口/lockout”是否为根因。
3. 如果独立 Job 后 Pass1 能出图，则再回头决定是保留 per-pass Job，还是继续研究同 Job 下 Meteor 需要的额外解锁/PD/窗口命令。
4. 如果独立 Job 仍不出图，再优先排 Pass1 的 REV 自然 PD/ForcePD 触发策略，而不是继续大幅改 Xleft。

## 2026-05-28 20:50 补充：HiPrint-style batch 试验开关

为了复刻 HiPrint “一次 STARTJOB 后连续灌入多个 swath”的方式，已保留现有逐 pass 门控逻辑，同时增加一个更接近 HiPrint 的 batch 试验组合：

- `METEOR_BATCH_SWATH_MODE=1`：Pass0 gate 放行后连续发送 3 条 swath，Pass1/Pass2 不再等待各自 pass gate。
- `METEOR_BATCH_ENDJOB_IMMEDIATE=1`：batch 连续发送完 3 条 swath 后立即 `ENDJOB`，不再延后到 Pass2 物理扫程结束。

该组合的目的不是直接定版，而是验证：如果一次性灌完整 job 后 Pass1/Pass2 恢复，则根因更可能在当前“同 Job 但按 pass gate 分开发送”引入的 Meteor 内部窗口、PD lockout 或播放时序；如果仍无图，再继续排 REV 触发和坐标窗口。

注意：实际联机测试发现，在当前本地 3PASS 运动架构下立即 `ENDJOB` 有安全风险。数据线程可能早于 Pass1/Pass2 物理扫程宣布 job 完成，导致层进度、Y 节拍或收尾动作提前。因此当前代码已将 `METEOR_BATCH_ENDJOB_IMMEDIATE=1` 钝化为安全延后：连续灌入 3 条 swath，但 `ENDJOB` 仍在 Pass2 物理扫程结束后执行。推荐下一轮只验证 `METEOR_BATCH_SWATH_MODE=1` 的连续灌 swath 效果。

## 备份

本轮开始前已备份当前关键代码到：

`backup/meteor_debug_20260528_204553`

包含：

- `4-MeteorPrintEngine.cs`
- `5-SharpControl.cs`
- `手动操作界面.cs`
