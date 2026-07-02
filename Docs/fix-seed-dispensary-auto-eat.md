# 化种匣被自动吃药误触发修复说明

## 问题现象

采集路线中装备并使用化种匣时，角色触发复苏/死亡恢复流程后，程序会误执行自动吃药的小道具快捷键。由于当前装备的小道具是化种匣而不是便携营养袋，后续流程会持续卡在化种匣相关状态，无法按预期恢复路线。

典型日志如下：

```text
[07:29:20.710] [WRN] BetterGenshinImpact.GameTask.Common.TaskControl
检测到复苏界面，吃药已超额(AutoEatCount=3)，前往七天神像

[07:30:08.397] [WRN] BetterGenshinImpact.GameTask.Common.TaskControl
自动吃药：尝试使用小道具恢复-n 0
```

## 根因分析

`PathingConditionConfig.AutoEatCount` 在地图追踪/战斗恢复链路中有两个含义：

- `0` 到 `2`：自动吃药已启用，并记录当前吃药/复活尝试次数。
- `3`：自动吃药关闭，或没有确认当前装备的是便携营养袋。

原逻辑在 `Avatar.ThrowWhenDefeated` 中把 `AutoEatCount >= 2` 统一视为“吃药已超额”，并在准备前往七天神像前将 `AutoEatCount` 重置为 `0`。当 `AutoEatCount` 本来是 `3` 时，这个重置会把“自动吃药禁用/未确认营养袋”的状态误改成“自动吃药启用”，导致后续恢复流程继续发送 `GIActions.QuickUseGadget`。

如果玩家当前装备的是化种匣，`QuickUseGadget` 就会触发化种匣，而不是使用便携营养袋恢复，从而造成采集恢复路径卡住。

## 修复内容

本次修复保留 `AutoEatCount=3` 的禁用语义，并让所有复苏/死亡兜底路径在发送小道具快捷键前都检查该状态。

### 1. 保留 `AutoEatCount=3` 禁用状态

文件：`BetterGenshinImpact/GameTask/AutoFight/Model/Avatar.cs`

复苏界面检测到 `AutoEatCount >= 2` 后仍会前往七天神像，但只有 `AutoEatCount < 3` 时才把计数重置为 `0`。

修复后语义：

- `AutoEatCount=2`：确实为吃药超额，前往七天神像前允许重置为 `0`，保持原有自动吃药重试逻辑。
- `AutoEatCount=3`：表示自动吃药关闭或未确认营养袋，前往七天神像，但保持 `3`，不重新开启小道具恢复。

### 2. 自动战斗切人兜底不再误按化种匣

文件：`BetterGenshinImpact/GameTask/AutoFight/Model/Avatar.cs`

切换角色失败并遇到复活弹窗时，旧逻辑会在药品不处于 CD 时直接发送 `QuickUseGadget`。修复后增加 `PathingConditionConfig.AutoEatCount < 3` 判断，只有自动吃药处于启用状态时才允许按小道具。

### 3. 地图追踪恢复路径增加小道具保护

文件：`BetterGenshinImpact/GameTask/AutoPathing/PathExecutor.cs`

以下场景原先会在确认复苏/死亡弹窗后直接发送 `QuickUseGadget`：

- 低血量/死亡恢复后尝试使用小道具。
- `SwitchAvatar` 切换角色失败后的死亡确认兜底。
- `SwitchAvatar2` 切换角色失败后的死亡确认兜底。

这些位置现在都增加了 `PathingConditionConfig.AutoEatCount < 3` 判断。确认弹窗仍会正常点击，前往七天神像恢复的主流程不变；只有“按当前小道具”这一步会尊重自动吃药禁用状态。

## 行为变化

修复后：

- 当未检测到便携营养袋或自动吃药被关闭时，复苏/死亡恢复不会再按当前小道具。
- 装备化种匣进行采集时，异常恢复流程不会再把化种匣当成便携营养袋使用。
- 自动吃药确实启用且 `AutoEatCount < 3` 时，原有吃药/复活尝试行为保持不变。
- 前往七天神像回血/复活的兜底逻辑保持不变。

## 验证

已执行主程序项目编译：

```powershell
dotnet build BetterGenshinImpact\BetterGenshinImpact.csproj -c Debug
```

结果：编译通过，`0` 个错误。构建过程中存在仓库既有警告，以及网络源无法读取 NuGet 漏洞数据的警告，不影响本次修复。

补充说明：`dotnet build BetterGenshinImpact.sln -c Debug` 当前会因为解决方案引用缺失的测试项目而失败：

- `Test\BetterGenshinImpact.Test\BetterGenshinImpact.Test.csproj`
- `Test\BgiCoordinatorServer.UnitTest\BgiCoordinatorServer.UnitTest.csproj`

该失败与本次修复无关。

