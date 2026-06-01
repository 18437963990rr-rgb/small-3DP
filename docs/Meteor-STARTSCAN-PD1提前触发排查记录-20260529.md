# Meteor STARTSCAN 后 PD1 提前触发排查记录（2026-05-29）

## 背景

3PASS 打印中，Pass0/Pass1/Pass2 图像数据已经能够按 `STARTJOB -> STARTSCAN -> IMAGE -> ENDDOC` 链路发送到 Meteor/PCC，但正式打印时 Pass1/Pass2 仍然无图。前期曾怀疑 `Xleft`、REV 坐标、`ENDJOB` 时机、同一 `STARTJOB` 多 swath 窗口复用等问题。

本轮排查的核心发现是：`STARTSCAN` 发出后，即使墨车没有运动，也会出现 `PD1 event`。这意味着扫描窗口可能在物理扫描开始前就被 Product Detect 消耗，导致后续 pass 窗口错位或无效。

## 已完成的软件调整

### 回归同一 STARTJOB batch 发送方向

参考 Meteor 扫描打印示例和 HiPrint 行为，当前主路径回到同一 `STARTJOB` 内发送多条 swath：

```text
PiSetHome/对齐 Home（按配置）
STARTJOB
  STARTSCAN FWD
  IMAGE
  ENDDOC
  STARTSCAN REV
  IMAGE
  ENDDOC
  STARTSCAN FWD
  IMAGE
  ENDDOC
ENDJOB
```

当前默认策略倾向：

- `METEOR_BATCH_SWATH_MODE=True`
- `METEOR_BATCH_STARTSCAN_GATE_SPLIT=False`
- `METEOR_SPLIT_JOB_PER_PASS=False`
- `ENDJOB` 默认延后到 Pass2 物理扫描结束后发送

### IMAGE 预打包

发现旧链路中 `STARTSCAN` 后才执行 `PackImageRowsToCommandBuffer`，导致 `STARTSCAN` 与 `IMAGE` 之间可能有数百毫秒窗口。已改为先在内存中打包完整 `PCMD_IMAGE`，然后连续发送：

```text
STARTSCAN
IMAGE
ENDDOC
```

该修改降低了 `STARTSCAN -> IMAGE` 间隔，但后续测试证明，PD1 的提前触发不是由 IMAGE 打包延迟单独造成。

### Pass0 等待全部 swath 入队

为模仿 HiPrint “先入队整层 swath，再开始运动”的行为，legacy batch 模式下，Pass0 物理扫描不再在首条 swath 后释放，而是等三条 swath 全部 `ENDDOC` 后才放行。

## 关键调试按钮

为隔离问题，在 `手动操作界面.cs` 添加了手动 Meteor 调试按钮：

- `Meteor调试 STARTJOB`
  - 只发送 `PCMD_STARTJOB`
  - 不运动，不发送 `STARTSCAN/IMAGE/ENDDOC`

- `Meteor调试 STARTSCAN FWD`
  - 在已有 `STARTJOB` 后只发送一条 `PCMD_STARTSCAN FWD`
  - 不运动，不发送 `IMAGE/ENDDOC`

- `Meteor调试 ENDJOB`
  - 尝试发送 `PCMD_ENDJOB` 清理 job

- `Meteor调试 空白IMAGE文档`
  - 放在手动界面第三个页签
  - 在内存中构造 `256x16` 全 0 的 1bpp 空白图
  - 发送完整合法文档：`STARTSCAN FWD -> IMAGE(空白) -> ENDDOC`
  - 不依赖模型窗口，不需要加载 CAD/空白模型

## 测试现象

### 单独 STARTJOB 不触发 PD

手动点击 `STARTJOB` 后，Meteor 日志只出现 PCC 初始化、`PdControlReg` 写入、FIFO datapath 启用等内容，没有 `PD1 event`。

结论：

- `STARTJOB` 本身不会触发 PD1。
- `PdControlReg` 写入本身也不会立即触发 PD1。

### 单独 STARTSCAN 后立即 PD1

在墨车不运动的情况下，点击 `STARTSCAN FWD` 后出现：

```text
Rx:StartScan Pcc1 Fwd Offset=0
--> PD1 event for PCC:1, AbsXCount:0, Delta:0, Len:0
```

结论：

- `STARTSCAN` 会使 PCC 进入扫描触发 armed 状态。
- armed 后，PD1 触发条件立即满足。
- 这不是 800us 延迟问题，而是静止状态下的提前触发。

### 空白 IMAGE 完整文档仍触发 PD1

为排除“只发 STARTSCAN 导致状态异常”，测试完整空白文档：

```text
STARTSCAN FWD
IMAGE(256x16 全空白)
ENDDOC
```

Meteor 日志：

```text
Rx:StartScan Pcc1 Fwd Offset=0
Rx:Image Plane=1 Xleft=8 Ytop=0 Width=256 Dwcount=128
Rx:EndDoc PccId=0 PrintLane=1
--> PD1 event for PCC:1, AbsXCount:0, Delta:0, Len:0
```

结论：

- PD1 提前触发不是因为缺少 `IMAGE/ENDDOC`。
- 即使文档命令序列合法，只要 `STARTSCAN` armed，仍会出现静止 PD1。
- 图像内容不是触发原因。

## ProductDetect 配置核对

当前 PCC-E Starfire 配置中 `[ProductDetect]` 只有：

```ini
[ProductDetect]
Xoffset   = 400
ActiveLow = 1
Filter    = 150
```

含义：

- `Xoffset`：触发后延迟多少 print clocks 开始打印，不决定是否触发。
- `ActiveLow`：PD 输入有效电平极性。
- `Filter`：输入滤波时间常数，单位 us，只能抑制毛刺，不能修复常态有效。

实测 `ActiveLow=0/1` 后，日志中的 `PdControlReg` 低位确实不同，一个为 `00`，一个为 `08`，说明配置修改已被当前 PCC 加载。

结论：

- 当前改的是正确配置文件。
- 问题不是“配置未生效”。
- 单纯翻转 `ActiveLow` 仍无法消除 `STARTSCAN -> PD1`。

## PL7 硬件输入排查

手册中 PL7 为：

```text
PL7 编码器跟触发信号输入（Transport）
```

相关针脚：

```text
1: 0V
2: 触发信号输入
3: 编码器 B+
4: 编码器 A+
5: 0V
6: 0V
7: 备用
8: 编码器 B-
9: 编码器 A-
10: PD/编码器可选 5V 供电（限流 500mA）
```

观察：

- PL7 接上时，墨车到 Meteor HOME 点，PD 灯会亮。
- PL7 拆下后，HOME 开关触发时 PD 灯不亮。
- 但 PL7 拆下后，点击 `STARTSCAN` 仍然出现 `PD1 event`。

解释：

- PL7-2 很可能确实是 Product Detect/触发输入链路的一部分。
- “拔掉 PL7 后仍触发”不能直接证明板卡坏，因为输入可能悬空。
- 更有效的测试不是拔掉，而是把 PL7-2 固定到明确的非触发电平。

建议判定：

- `ActiveLow=1` 时，低电平有效，空闲应为高电平。
- `ActiveLow=0` 时，高电平有效，空闲应为低电平。
- 需由电气侧按手册用安全方式给 PL7-2 明确上拉/下拉，不要随意把 24V 接到逻辑输入。

## 当前结论

目前最强结论：

```text
STARTJOB 不触发 PD1；
STARTSCAN 会 armed ProductDetect；
armed 后即使无运动、无模型、空白 IMAGE，也会出现 PD1 event；
因此正式打印中，Pass0 前的 STARTSCAN 很可能在物理扫描前消耗了 scan window。
```

这可以解释：

- Pass0/Pass1/Pass2 图像已发送，但后续 pass 不出图。
- Pass2 前看到的 AbsX/显示“置零”现象可能是 PD/scan 相对计数状态变化，而非软件主动 `PiSetHome`。
- 继续只调整 `Xleft`、REV 方向或 `ENDJOB` 时机，优先级应降低。

尚不能直接定性为板卡硬件损坏。更合理的排序是：

1. PD 输入空闲态/有效区/安装位置问题。
2. PL7-2 输入悬空或未被明确拉到非触发态。
3. HOME 点/等待位本身处于 PD 有效区。
4. PCC ProductDetect 输入电气类型与现场传感器输出不匹配。
5. 最后才考虑 PCC 输入通道异常或板卡问题。

## 下一步建议

### 1. 观察等待位 PD 灯状态

在不发 `STARTSCAN` 时，把墨车停在正式打印等待位，观察 PD 灯：

- 若等待位 PD 灯已亮：`STARTSCAN` 后立即 PD 是预期结果，需调整传感器位置/等待位/极性。
- 若等待位 PD 灯灭但 `STARTSCAN` 后立即 PD：继续查输入电平、悬空、滤波、PCC 输入状态。

### 2. 固定 PL7-2 到明确非触发电平

不要只拔掉 PL7。应在电气安全确认后：

- `ActiveLow=1`：将 PL7-2 固定到非触发高电平。
- `ActiveLow=0`：将 PL7-2 固定到非触发低电平。

然后测试：

```text
STARTJOB
空白IMAGE文档
观察是否仍有 PD1 event
ENDJOB/Abort 清理
```

判定：

- 不再 PD：根因在外部输入电平/悬空/传感器/接线。
- 仍 PD：继续怀疑 PCC 内部触发路径、输入通道异常、或其它 Transport/PD 接口。

### 3. 测量 PL7-2 电压

用万用表或示波器测 PL7-2 对 0V：

- 等待位
- HOME 点
- 传感器触发/未触发
- `STARTSCAN` 前后

记录电平是否与 `ActiveLow` 配置匹配。

### 4. 软件策略备选

如果硬件 PD 逻辑最终确认正常，但静止 armed 仍不可避免，则软件侧不应提前 batch 发送 `STARTSCAN`。可改为：

- 图像数据预处理/缓存可以提前完成；
- `STARTSCAN + IMAGE + ENDDOC` 在每个 pass 实际运动即将进入触发点前贴近发送；
- 避免 PCC 在静止等待阶段 armed 太久。

但这会偏离 HiPrint “提前入队多 swath”的方式，只有在硬件触发条件无法修复时才建议作为软件规避方案。

---

## 后续记录（2026-06-01）

在继续按供应商建议强化「原点灌满 3 swath → 延迟 → 再动」后，已通过 **Legacy batch defer + 固定 FWD Xleft=4110 + unified 基准捕获修复** 恢复 Pass2 出图。详见：

- [Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md](./Meteor-batch-legacy-固定Xleft4110-解决记录-20260601.md)
