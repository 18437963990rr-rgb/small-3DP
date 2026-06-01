# Meteor Home / Xleft / REV 续查记录（2026-05-28）

> 适用项目：LaserAdd_3DP 主控 + Meteor PCC-E / Starfire  
> 关联文档：`Meteor-Pass0打印排查与解决记录.md`、`Meteor-Pass1-Pass2恢复排查与解决记录.md`、`Meteor-Pass1-Pass2续查记录-20260527晚间.md`

## 1. 本轮排查起点

上一轮排查结束后，`Pass1/Pass2` 仍存在不稳定或不喷墨现象：

- `Pass0` 已能稳定出图。
- `Pass1` 多次表现为命令下发成功、甚至有 `PD1 event`，但无可见图形。
- `Pass2` 曾多次表现为命令下发成功，但缺少新的 `PD1 event`。
- 逐 pass 门控、延后 `EndJob`、Pass2 swath-ready 后触发等时序改动后，问题仍未彻底收敛。

因此，本轮重点从“是否已把图像数据送入 Meteor”转向：

- Meteor 自带测试软件与本软件的 `PCMD_IMAGE Xleft` 是否一致。
- `REV` 扫描方向是否真正进入 Meteor `STARTSCAN`。
- `PiSetHome` / 机械原点 / 图像 `Xleft` 三者是否在同一参考系下。

## 2. Meteor 自带测试软件单 pass 对照

使用 Meteor 自带打印软件打印单 pass 图形，日志中关键现象：

```text
Rx:StartScan Pcc1 Fwd Offset=0
Rx:Image Plane=1 Xleft=400 Ytop=0 Width=12001 Dwcount=873824
...
PCC1 Print: ... 00000190 00003081
PD1 event ... AbsXCount:0, Delta:0, Len:0
```

重要观察：

- Meteor 自带软件的 `PCMD_IMAGE Xleft` 为固定值 `400`，最终底层命令中也对应 `0x190`。
- 该 `Xleft=400` 不来自现场实时 `AbsXCount`。
- 这与本软件之前 `Xleft≈4111/4390/9358/...` 的动态计算方式差异很大。

阶段性判断：

- 本软件长期用 live AbsX / pass anchor / 实测端点推导 `Xleft`，可能把图像窗口推到了错误参考系。
- `Xleft` 不应简单等同于当前机械位置或当前 PCC AbsX。

## 3. 固定 Xleft=400 实验

为验证 `Xleft` 是否是核心变量，本软件加入临时实验覆盖：

```text
METEOR_IMAGE_XSTART_FIXED_PX
```

当前行为：

- 默认强制所有 pass 的 `PCMD_IMAGE Xleft=400`。
- 设 `METEOR_IMAGE_XSTART_FIXED_PX=-1` 可关闭覆盖。
- 软件日志会输出：

```text
[MeteorXStart] FixedOverride ... original=... forced=400 ...
```

测试结果：

- 修改后本软件变成 `Pass2` 才出图。
- 说明“所有 pass 都固定 400”并不是最终正确方案。
- 但它证明了 `Xleft` 对出图 pass 有决定性影响，值得继续围绕坐标参考系排查。

后续推论：

- `FWD Xleft=400` 可能合理。
- `REV` 不应也直接使用 `400`。
- 按 HiPrint 代码习惯，`REV Xleft` 更可能应为图像右边缘：

```text
REV Xleft = FWD Xleft + Width
```

若宽度按当前 7328 px，则：

```text
REV Xleft ≈ 400 + 7328 = 7728
```

## 4. Meteor 测试软件三次打印对照

用户使用三张 `7323 x 2048 px` 图形，在 Meteor 测试软件中按接近当前墨车行程进行三次打印。

用户现场观察：

- `Pass0` 有图。
- `Pass1` 无图。
- `Pass2` 有图。

Meteor 日志中的关键异常：

```text
Rx:StartScan Pcc1 Fwd Offset=0
Rx:Image Plane=1 Xleft=800 ...

Rx:StartScan Pcc1 Fwd Offset=0
Rx:Image Plane=1 Xleft=800 ...

Rx:StartScan Pcc1 Fwd Offset=0
Rx:Image Plane=1 Xleft=800 ...
```

即使现场确认在第二次反向回程前点击了 Meteor 测试软件的 `Rev` 按钮，日志仍显示三次全部为 `Fwd`。

阶段性判断：

- 这是本轮最硬的异常之一。
- 至少从 Meteor PrintEngine 日志层看，测试软件实际下发给 Meteor 的 `PCMD_STARTSCAN` 方向没有变成 `REV`。
- 如果第二趟物理车确实是反向运动，但 Meteor 文档仍按 `FWD` 解释，则 `Pass1` 不出图是合理结果。

可能原因：

- 测试软件的 `Rev` 按钮只改变 UI / 运动 / 预览，不改变 `PCMD_STARTSCAN`。
- 当前配置或 JobType 下，测试软件固定按 FWD 发送。
- `Rev` 需要配合其它 bidi / scan setting 才实际生效。
- 文档已预载，点击 `Rev` 时命令队列已生成。

## 5. 行程宽度与 swath 宽度疑点

当前 7328 px 在 400 DPI 下对应宽度约：

```text
7328 / 400 * 25.4 ≈ 465.3 mm
```

若机械扫描行程为约 470 mm，则理论余量只有约：

```text
470 - 465.3 ≈ 4.7 mm
```

这使得以下因素都可能导致反向 pass 落在有效窗口外：

- head `Rev X-offset`
- `BIDI_XADJUST`
- `Xleft` 右缘/左缘理解错误
- 触发点提前/滞后
- `PiSetHome` 参考点与机械原点不一致

阶段性判断：

- 行程宽度小于或接近 swath 宽度，确实可能影响反向。
- 但测试软件日志三次都是 `FWD`，比单纯行程不足更优先排查。

## 6. HiPrint 的 PiSetHome 线索

查看 `E:\HiPrint` 后发现，HiPrint 在每张图正式开始打印前会调用 Meteor 的 `PiSetHome()`。

发送顺序为：

```text
PiSetHome()
-> CCP_BIDI_XADJUST
-> PCMD_STARTJOB
-> loop:
   PCMD_STARTSCAN
   PCMD_IMAGE
   PCMD_ENDDOC
-> PCMD_ENDJOB
```

代码位置：

- `E:\HiPrint\HiPrint\src\core\printEngine\PrintJobManagerThread.cpp`
- `meteorApi->setHome()` 在 `PCMD_STARTJOB` 前。
- `MeteorApi::setHome()` 底层直接调用 `PiSetHome()`。

与本软件差异：

- 本软件当前默认 `METEOR_PISET_HOME_AT_JOB_START` 关闭。
- 日志会出现：

```text
Meteor SendStartJob: PiSetHome skipped (METEOR_PISET_HOME_AT_JOB_START disabled)
```

阶段性判断：

- HiPrint 把 `PiSetHome` 作为每个打印 Job 前的固定步骤。
- 本软件当前未完全复刻该习惯。
- 若 `PiSetHome` 参考点不一致，会直接影响 Meteor X=0 与 `PCMD_IMAGE Xleft` 的关系。

## 7. 多轴515 PLC 运动规律参考

`E:\多轴515rar` 的 TwinCAT PLC 中，自动扫描 `M_Scan` 大致运动流程：

```text
触发扫描
-> Y 相对移动 700
-> X 相对移动 200 * flag，到打印初始点
-> X 相对移动 900 * flag，执行一次扫描
-> Y 相对移动 64.96，进入下一条
-> X 反向移动 900 * flag
-> 循环
-> X/Y 绝对回 0
```

其中：

- `64.96` 是单喷头参数。
- 当前双喷头项目使用 `129.92 mm`，该差异已解释，不作为本轮主要疑点。

关键理解：

- PLC 的机械原点与 Meteor 的 `PiSetHome` 原点不是天然同一个概念。
- PLC 最后 `X/Y` 回 `0` 是机械坐标归位。
- `PiSetHome` 是 Meteor/PCC 内部 X 计数参考归零。
- 两者必须在同一个物理位置建立关联，否则 `Xleft` 会看似合理但实际窗口错位。

## 8. 当前坐标模型

本轮形成的工作模型：

```text
机械原点 / 暂停位
    -> 调 PiSetHome
    -> Meteor X = 0

机械移动到打印起点
    -> 例如 X +200mm / +220mm / XOffset

PCMD_IMAGE Xleft
    -> 图像相对 Meteor X=0 的喷印起点

扫描行程
    -> 必须覆盖 Xleft 到 Xleft + swathWidth
    -> REV 时通常应使用图像右边缘作为 Xleft
```

容易混淆的点：

- `PiSetHome` 不是简单等价于机械 `0mm`，除非调用它时墨车确实停在机械原点。
- 机械打印起点 `200*flag` 或 `220*flag` 更像从机械原点到扫描起点的偏移。
- `PCMD_IMAGE Xleft` 不应直接用当前实时 `AbsXCount`，也不一定等于机械偏移量。

## 9. 本轮代码实验状态

截至本文档记录时，本软件中存在以下临时实验逻辑：

```text
METEOR_IMAGE_XSTART_FIXED_PX
```

默认：

```text
Xleft = 400
```

关闭：

```text
METEOR_IMAGE_XSTART_FIXED_PX=-1
```

此实验只用于验证 `Xleft` 参考系，不应视为最终方案。

## 10. 当前结论

1. `Xleft` 是关键变量，不能再只围绕 ForcePD delay 微调。
2. Meteor 测试软件三次打印全部显示 `FWD`，即使用户点击了 `Rev`，这是必须追究的异常。
3. `Pass1` 不出图可能与 `REV STARTSCAN` 未真正生效、或 `REV Xleft` 使用左缘而非右缘有关。
4. HiPrint 每次 `PCMD_STARTJOB` 前调用 `PiSetHome()`，本软件当前默认跳过，该差异需要做 A/B 验证。
5. 机械原点、Meteor Home、图像 Xleft 是三套概念，后续必须建立明确映射。
6. 扫描行程 470mm 对 7328px/400DPI 的 swath 余量很小，后续测试建议适当放宽行程，以排除窗口边界问题。

## 11. 建议下一轮 A/B 测试

### A. 验证 Meteor 测试软件 Rev 是否真正下发

最小测试：

```text
只打一张图
点 Fwd -> 看 Rx:StartScan 是否 Fwd
点 Rev -> 看 Rx:StartScan 是否 Rev
```

若 `Rev` 仍显示 `Fwd`：

- 先查 Meteor 测试软件配置或按钮含义。
- 不再用该测试软件的 `Rev` 作为反向打印依据。

### B. 验证 HiPrint 式 PiSetHome

前提：

- 墨车停在确定的 Home / 暂停位。

设置：

```text
METEOR_PISET_HOME_AT_JOB_START=1
```

观察：

- `SendStartJob-BeforePiSetHome`
- `SendStartJob-AfterPiSetHome`
- `AbsXCount` 是否稳定接近预期参考值
- 后续 `Rx:Image Xleft` 与出图 pass 的关系

### C. 验证 FWD/REV 分别使用左缘/右缘

建议实验：

```text
FWD Xleft = 400
REV Xleft = 400 + printWidthPx
```

当前 7328 px 宽度下：

```text
REV Xleft ≈ 7728
```

该实验比“所有 pass 都固定 400”更接近 HiPrint 的 `createImageCommand` 逻辑。

### D. 放宽 X 扫描行程

若机械允许，临时把扫描行程从约 `470mm` 放宽到更大值，至少保证：

```text
scanTravel > swathWidth + rev/fwd offset + 触发余量
```

用于排除反向窗口刚好扫不到图像范围的问题。

## 12. 后续文档关注点

下次记录应重点捕捉：

- `PiSetHome` 前后 `AbsXCount`
- `Rx:StartScan` 实际方向
- `Rx:Image Xleft`
- `PCC1 Print: ... 12345673 ...` 中最终 `Xleft/Width`
- `PD1 event AbsXCount/Delta/Len`
- 实际哪一 pass 出图

尤其是：

```text
界面选择 REV，但日志是否真的 Rx:StartScan Rev
```

这是当前最优先要澄清的分叉点。

