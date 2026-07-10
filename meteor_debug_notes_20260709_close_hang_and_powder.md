# Meteor 打印关闭卡死与落粉圈数调试记录 - 2026-07-09

本文档整理本轮对话中的问题、根因判断、代码修改、风险边界和后续建议。它不是逐字转录，而是面向后续继续调试的工程记录。

**最后更新**：2026-07-09

---

## 1. 本轮问题

用户本轮主要反馈三类问题：

1. `textBox25` 的落粉圈数是否支持小于一圈的转动，例如 `0.5` 圈。
2. 实际打印流程中，如果在打印中间层暂停，随后点击关闭打印，软件会卡在“正在关闭”状态，无法关闭。
3. 偶尔最小化软件后再恢复窗口，软件会出现未响应状态。

本轮还明确了一个维护约束：

- 老项目源码存在 GBK / ANSI / 历史混合编码风险。
- 只允许最小范围修改指定位置。
- 不允许通过重写整个文件的方式保存。
- 不允许顺手修复全文件注释、格式或编码。
- 如果发现乱码、编码不明或 diff 异常增大，必须立即停止并先汇报。

---

## 2. 落粉圈数 textBox25

### 2.1 结论

`textBox25` 绑定到 `PowderSupplyRotateNum`。

原逻辑中，`PowderSupplyRotateNum` 的 setter 会把 `value <= 0.5` 强制改成 `1`，因此 `0.5` 圈不能被真实保留。

### 2.2 已处理

已将限制条件从：

```csharp
if (value <= 0.5)
```

调整为：

```csharp
if (value <= 0)
```

效果：

- `0.5`、`0.8` 等正数小数圈数可以保留。
- `0` 或负数仍会被保护为默认值。

该修改发生在用户 git 备份之前，因此当前已进入用户的 git 基线。

---

## 3. 打印暂停后关闭卡在“正在关闭”

### 3.1 根因判断

排查到关闭自动打印线程路径时，原 `DeleteAutoPrintThread()` 使用：

```csharp
tempThread.Abort();
while (tempThread.ThreadState != ThreadState.Aborted)
{
    Thread.Sleep(100);
}
```

问题：

- 如果打印线程正处于非托管调用、运动等待、暂停等待或无法及时响应 `Abort()` 的状态，UI 线程会一直等待。
- 这会导致关闭打印时界面卡在“正在关闭”。

另外，`4-MeteorPrintEngine.cs` 中 `StopJob()` 原先使用 `lock (SyncRoot)`，如果打印线程持有该锁并处于暂停或阻塞状态，关闭路径也可能被同步锁拖死。

### 3.2 已处理：手动操作界面.cs

文件：

```text
1-3DP主控界面(人机交互模块)\手动操作界面.cs
```

修改位置：

```text
DeleteAutoPrintThread()
```

最小范围修改：

- 增加 `AutoPrintThreadAbortJoinTimeoutMs = 3000`。
- 避免线程自己删除自己时再次等待自己。
- 将无限等待 `ThreadState == Aborted` 改为 `Join(3000)`。
- 超时后写日志并返回，避免 UI 无限卡死。

当前 git diff 规模：

```text
1 file changed, 11 insertions(+), 2 deletions(-)
```

注意：

- Git 提示该文件下次被 Git 触碰时 `LF will be replaced by CRLF`。
- 当前 diff 已确认很小，没有再出现整文件重写。

### 3.3 已处理：4-MeteorPrintEngine.cs

文件：

```text
1-3DP主控界面(人机交互模块)\4-MeteorPrintEngine.cs
```

该文件被 git 忽略，因此修改不会显示在 `git diff` 中。

修改位置：

```text
StopJob()
```

最小范围修改：

- 将原 `lock (SyncRoot)` 改为 `Monitor.TryEnter(SyncRoot, 3000)`。
- 如果 3 秒内拿不到锁，则记录日志并返回 `false`。
- 使用 `finally` 调用 `Monitor.Exit(SyncRoot)`，确保成功进入锁后可释放。

当前可检索到的关键位置：

```text
4-MeteorPrintEngine.cs:4278  Monitor.TryEnter(SyncRoot, 3000)
4-MeteorPrintEngine.cs:4280  Meteor StopJob: SyncRoot busy for 3000ms; skip blocking UI close path
4-MeteorPrintEngine.cs:4335  Monitor.Exit(SyncRoot)
```

### 3.4 预期效果

本轮修复重点是“关闭打印路径不要无限阻塞 UI”：

- 自动打印线程无法及时 abort 时，最多等待约 3 秒。
- Meteor 停止任务拿不到内部锁时，最多等待约 3 秒。
- 因此暂停后点关闭打印，理论上不应再永久卡在“正在关闭”。

---

## 4. 窗口最小化后恢复未响应

### 4.1 当前判断

未发现该界面有直接的 `Resize`、`SizeChanged`、`WindowState` 等最小化/恢复事件处理逻辑，因此该问题不太像是窗口恢复事件本身造成。

更可能的原因是：

1. UI 线程已经被关闭打印路径里的无限等待卡住。
2. 后台线程存在高 CPU 空转等待，恢复窗口时 UI 消息处理被拖慢或表现为未响应。
3. 运动等待变量未刷新，导致某些流程进入死等。

### 4.2 已发现但暂未修改的空转等待

在 `手动操作界面.cs` 中发现若干空循环等待，例如：

```text
while (PosValue <= n_AdapativeDryMoveStopPosition - 5);
while (PosValue <= 620);
while (PosValue <= 620);
```

这类写法的问题：

- 没有 `Thread.Sleep()`，会高 CPU 空转。
- 如果 `PosValue` 不在循环内刷新，可能永远不退出。
- 对窗口最小化后恢复未响应有较高嫌疑。

### 4.3 本轮决定

用户已明确“暂时不碰这些”。

因此本轮未修改这些空转等待。

### 4.4 是否可以直接删除

不建议直接删除。

这些等待大概率承担“等运动到位后再继续下一步”的工艺时序作用。直接删除可能造成：

- 轴或铺粉车未到位，后续动作提前执行。
- 运动指令重叠。
- 打印层流程时序错乱。
- 铺粉异常、限位风险或打印失败。

后续若要修，建议保留等待语义，仅将空循环改为带刷新、`Thread.Sleep()`、超时和日志的等待。

---

## 5. 备份记录

用户提示 `4-MeteorPrintEngine.cs` 被 git 忽略，需要手动备份并注明日期。

已创建备份：

```text
Backup\manual_backup_20260709\4-MeteorPrintEngine_20260709_before_close_hang_fix.cs
```

该备份目录当前为未跟踪状态：

```text
?? Backup/manual_backup_20260709/
```

---

## 6. 编码与回退记录

本轮曾发生一次不符合维护约束的文件保存事故：

- 使用 PowerShell `Set-Content` 操作 `手动操作界面.cs`，导致 diff 异常放大，接近整文件重写。
- 随后已按用户要求回退到 git 版本。
- 之后重新按最小范围修改方式处理。

还发生过一次错误插入位置：

- 因为 ASCII 上下文不唯一，补丁曾插入到错误的 `if (tempThread != null)` 附近。
- 发现后立即停止汇报。
- 用户确认后再次回退到 git 版本，并重新定位到 `DeleteAutoPrintThread()` 后才修改。

当前状态：

- `手动操作界面.cs` 已恢复为小 diff。
- 未再进行整文件重写。
- 未修改全文件注释、格式或编码。

---

## 7. 当前工作区状态

当前已知状态：

```text
M  1-3DP主控界面(人机交互模块)\手动操作界面.cs
?? Backup\manual_backup_20260709\
```

另外：

```text
1-3DP主控界面(人机交互模块)\4-MeteorPrintEngine.cs
```

该文件被 git 忽略，但已有关闭卡死相关修改。

---

## 8. 验证情况

本轮未重新执行完整构建。

原因：

- 该老项目此前构建已暴露既有环境/工程问题，例如资源生成、缺失 obj/nuget props 等。
- 这些问题不属于本轮关闭卡死修复引入。
- 用户当前重点是最小范围修改与记录，不继续扩大操作面。

建议实机验证：

1. 自动打印到中间层。
2. 暂停。
3. 点击关闭打印。
4. 观察是否仍永久停留在“正在关闭”。
5. 查看日志中是否出现：

```text
DeleteAutoPrintThread timeout
Meteor StopJob: SyncRoot busy for 3000ms
```

如果出现这些日志，说明本轮保护逻辑生效，但底层打印线程或 Meteor 锁仍存在未及时退出的问题，需要继续沿对应日志追踪。

---

## 9. 后续建议

### 9.1 暂不处理空转等待

按用户当前决定，本轮不修改空转等待。

但如果后续最小化/恢复未响应仍复现，优先检查并最小化修改这些等待点。

### 9.2 推荐后续最小修法

不要删除等待，而是改成类似：

```csharp
while (PosValue <= 620)
{
    PosValue = GetCurrentPos(7);
    Thread.Sleep(10);
}
```

更稳妥版本应增加超时和日志，避免设备不到位时软件永久卡死。

### 9.3 继续保持源码修改纪律

后续再动老源码时建议：

- 修改前先看 `git diff --stat`。
- 修改后立即看 `git diff --stat` 和具体 hunk。
- 只用唯一上下文定位。
- 不使用会重写整文件的保存方式。
- 遇到乱码、编码不明或 diff 异常放大，立即停止。
