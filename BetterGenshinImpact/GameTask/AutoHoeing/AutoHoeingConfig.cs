using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace BetterGenshinImpact.GameTask.AutoHoeing;

/// <summary>
/// 锄地一条龙配置类
/// </summary>
[Serializable]
public partial class AutoHoeingConfig : ObservableObject
{
    // ========== 第一部分：执行配置 ==========

    /// <summary>
    /// 执行模式：运行锄地路线、调试路线分配、强制刷新所有运行记录、启用仅指定怪物模式
    /// </summary>
    [ObservableProperty]
    private string _operationMode = "运行锄地路线";

    /// <summary>
    /// 选择执行第几个路径组（1-10）
    /// </summary>
    [ObservableProperty]
    private int _groupIndex = 1;

    /// <summary>
    /// 本路径组使用配队名称
    /// </summary>
    [ObservableProperty]
    private string _partyName = "";

    /// <summary>
    /// 组内路线排序模式：原文件顺序、效率降序、高收益优先
    /// </summary>
    [ObservableProperty]
    private string _sortMode = "高收益优先";

    /// <summary>
    /// 拾取模式（默认改为「模板匹配仅拾取狗粮」以减少新用户首跑的拾取干扰）
    /// </summary>
    [ObservableProperty]
    private string _pickupMode = "模板匹配仅拾取狗粮";

    /// <summary>
    /// 仅使用路线相关怪物材料进行识别
    /// </summary>
    [ObservableProperty]
    private bool _useRouteRelatedMaterialsOnly;

    /// <summary>
    /// 禁用识别到物品后的二次校验
    /// </summary>
    [ObservableProperty]
    private bool _disableSecondaryValidation;

    /// <summary>
    /// 泥头车角色编号（中文逗号分隔，如"1，3"）
    /// </summary>
    [ObservableProperty]
    private string _dumperCharacters = "";

    /// <summary>
    /// 使用料理名称（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _cookingNames = "";

    /// <summary>
    /// 不运行时段
    /// </summary>
    [ObservableProperty]
    private string _noRunPeriod = "";

    /// <summary>
    /// 识别间隔(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _findFInterval = 100;

    /// <summary>
    /// 拾取后延时(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _pickupDelay = 50;

    /// <summary>
    /// 滚动后延时(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _rollingDelay = 32;

    /// <summary>
    /// 单次滚动周期(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _scrollCycle = 1000;

    /// <summary>
    /// 运行路线时输出怪物数量日志
    /// </summary>
    [ObservableProperty]
    private bool _logMonsterCount;

    /// <summary>
    /// 禁用异步操作
    /// </summary>
    [ObservableProperty]
    private bool _disableAsync;

    /// <summary>
    /// 路线结尾时进行坐标检查
    /// </summary>
    [ObservableProperty]
    private bool _enableCoordinateCheck;

    /// <summary>
    /// 跳过校验阶段
    /// </summary>
    [ObservableProperty]
    private bool _skipValidation;

    // ========== 第二部分：路线选择与分组配置 ==========

    /// <summary>
    /// 账户名称
    /// </summary>
    [ObservableProperty]
    private string _accountName = "默认账户";

    /// <summary>
    /// 路径组一要排除的标签
    /// </summary>
    [ObservableProperty]
    private string _tagsForGroup1 = "蕈兽，传奇，狭窄地形";

    [ObservableProperty] private string _tagsForGroup2 = "";
    [ObservableProperty] private string _tagsForGroup3 = "";
    [ObservableProperty] private string _tagsForGroup4 = "";
    [ObservableProperty] private string _tagsForGroup5 = "";
    [ObservableProperty] private string _tagsForGroup6 = "";
    [ObservableProperty] private string _tagsForGroup7 = "";
    [ObservableProperty] private string _tagsForGroup8 = "";
    [ObservableProperty] private string _tagsForGroup9 = "";
    [ObservableProperty] private string _tagsForGroup10 = "";

    /// <summary>
    /// 禁用根据运行记录优化路线选择
    /// </summary>
    [ObservableProperty]
    private bool _disableSelfOptimization;

    /// <summary>
    /// 摩拉/耗时权衡因数
    /// </summary>
    [ObservableProperty]
    private double _efficiencyIndex = 0.25;

    /// <summary>
    /// 好奇系数（0-1）
    /// </summary>
    [ObservableProperty]
    private double _curiosityFactor;

    /// <summary>
    /// 小怪/精英忽略比例
    /// </summary>
    [ObservableProperty]
    private int _ignoreRate = 100;

    /// <summary>
    /// 目标精英数量
    /// </summary>
    [ObservableProperty]
    private int _targetEliteNum = 400;

    /// <summary>
    /// 目标小怪数量
    /// </summary>
    [ObservableProperty]
    private int _targetMonsterNum = 2000;

    /// <summary>
    /// 优先关键词（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _priorityTags = "";

    /// <summary>
    /// 排除关键词（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _excludeTags = "";

    // ========== 第三部分：仅指定怪物模式 ==========

    /// <summary>
    /// 目标怪物（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _targetMonsters = "";

    // ========== 第四部分：联机配置 ==========

    /// <summary>
    /// 启用联机模式
    /// </summary>
    [ObservableProperty]
    private bool _multiplayerEnabled = false;

    /// <summary>
    /// 联机队伍名称（为空则不切换）
    /// </summary>
    [ObservableProperty]
    private string _multiplayerPartyName = "";

    /// <summary>
    /// 联机起始角色名称（为空则不切换）
    /// </summary>
    [ObservableProperty]
    private string _multiplayerStartAvatarName = "";

    /// <summary>
    /// 协调服务器地址
    /// </summary>
    [ObservableProperty]
    private string _coordinatorServerUrl = "https://bgi-sync.example.com";

    /// <summary>
    /// 当前房间码（运行时状态，不持久化）
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string CurrentRoomCode { get; set; } = "";

    /// <summary>
    /// 集合点等待超时（秒），默认 60
    /// </summary>
    [ObservableProperty]
    private int _syncTimeoutSeconds = 60;

    /// <summary>
    /// 最低开始人数，低于此人数时集合点直接放行（不等待），默认0自动等齐所有人，设为1可单人调试
    /// </summary>
    [ObservableProperty]
    private int _minPlayersToSync = 0;

    /// <summary>
    /// 从第几条路线开始执行（1-based，0表示从头开始），用于调试续点
    /// </summary>
    [ObservableProperty]
    private int _startRouteIndex = 0;

    /// <summary>
    /// 玩家名称，联机时显示给其他玩家
    /// </summary>
    [ObservableProperty]
    private string _playerName = "";

    /// <summary>
    /// 玩家 UID，用于进入世界和多世界切换
    /// </summary>
    [ObservableProperty]
    private string _playerUid = "";

    /// <summary>
    /// 调试模式：跳过路线一致性验证，方便单人调试
    /// </summary>
    [ObservableProperty]
    private bool _debugMode = false;

    /// <summary>
    /// 使用固定调试线路：启用后从指定目录按文件名顺序加载路线，跳过正常路线选择逻辑
    /// </summary>
    [ObservableProperty]
    private bool _useFixedDebugRoutes = false;

    /// <summary>
    /// 固定调试线路目录路径，默认为内置 DebugRoutes 目录，可自定义
    /// </summary>
    [ObservableProperty]
    private string _fixedDebugRoutePath = "";

    /// <summary>
    /// 选中的内置线路文件夹名称（为空表示未选择）
    /// </summary>
    [ObservableProperty]
    private string _selectedBuiltinRoute = "";

    /// <summary>
    /// 集合点与战斗点的最小距离阈值，小于此距离的点不作为集合点，默认30
    /// </summary>
    [ObservableProperty]
    private double _syncPointMinDistance = 30.0;

    /// <summary>
    /// 联机模式战斗超时时间（秒），由房主设定并同步给所有成员，覆盖各自的自动战斗超时配置。默认 120
    /// </summary>
    [ObservableProperty]
    private int _fightTimeoutSeconds = 120;

    /// <summary>
    /// 战斗额外等待时间（秒），同步点超时后为 Fighting 成员额外等待，默认 60
    /// </summary>
    [ObservableProperty]
    private int _fightExtraWaitSeconds = 60;

    /// <summary>
    /// 重新加入最大等待时间（秒），同步点超时后为 Rejoining/Reviving 成员额外等待，默认 300
    /// </summary>
    [ObservableProperty]
    private int _rejoinMaxWaitSeconds = 300;

    /// <summary>
    /// 最大连续跳过路线次数，达到上限后退出联机锄地，默认 3
    /// </summary>
    [ObservableProperty]
    private int _maxConsecutiveSkips = 3;

    /// <summary>
    /// 最大连续同步超时次数，达到上限后退出联机锄地，默认 3
    /// </summary>
    [ObservableProperty]
    private int _maxConsecutiveTimeouts = 3;

    /// <summary>
    /// 最大路线滞后容忍数量，超过此数量的成员被视为落后过多，默认 2
    /// </summary>
    [ObservableProperty]
    private int? _maxRouteLag = 2;

    // === 集体卡死监测（multiplayer-mutual-wait-collective-skip spec / OQ-1~OQ-8 全部默认值）===
    /// <summary>启用集体卡死监测，默认 true。关闭后服务端不创建 MutualWaitMonitor，行为退化到 60s 超时</summary>
    [ObservableProperty]
    private bool _enableMutualWaitCollectiveSkip = true;
    /// <summary>触发阈值比例：totalWaiters ≥ ⌈online * MutualWaitMinWaitersRatio⌉ 才进入稳定计时，OQ-3 默认 0.5</summary>
    [ObservableProperty]
    private double _mutualWaitMinWaitersRatio = 0.5;
    /// <summary>ArrivalSets 快照保持稳定 N 秒后触发协同跳段，默认 30 秒（保守起步）</summary>
    [ObservableProperty]
    private int _mutualWaitStableSeconds = 30;
    /// <summary>连续触发协同跳段上限，达到后走 OnConsecutiveSyncTimeoutExceeded 类型路径协调停止，默认 3</summary>
    [ObservableProperty]
    private int _maxConsecutiveCollectiveSkips = 3;

    // === 快速同步点抢报（multiplayer-fast-sync-host-controlled spec, host-controlled 三处对称）===
    /// <summary>
    /// 启用快速同步点抢报（主开关）。默认 false。
    /// 关闭时所有抢报路径短路，行为退化为现有"严格到达后上报"。
    /// 详见 .kiro/specs/multiplayer-fast-sync-host-controlled/。
    /// </summary>
    [ObservableProperty]
    private bool _fastSyncPointEnabled = false;

    /// <summary>
    /// 路径同步点抢报距离阈值（米，原神坐标系）。范围 [5.0, 30.0]，默认 10.0。
    /// 距 waypoint 距离 ≤ 阈值时触发 OR 门抢报；持久化加载时由
    /// FastSyncDecisions.ClampPathingDistance 兜底 clamp。
    /// </summary>
    [ObservableProperty]
    private double _fastSyncPathingDistance = 10.0;

    /// <summary>
    /// 传送 loading 命中后到抢报上报之间的延迟毫秒数。范围 [0, 3000]，默认 0。
    /// 高网络延迟环境可上调。持久化加载时由 FastSyncDecisions.ClampTeleportDelay 兜底 clamp。
    /// </summary>
    [ObservableProperty]
    private int _fastSyncTeleportLoadingDelayMs = 0;

    /// <summary>
    /// 启用万叶聚物同步流程。默认 false，用户需显式打勾才启用，避免无万叶队伍走无效流程。
    /// 替代旧的 KazuhaPlayerIndex 字段（kazuha-player-auto-detection：从"按索引指定"改为"运行时声明"）。
    /// 启用判定：EnableKazuhaSync ∧ isConnected。
    /// </summary>
    [ObservableProperty]
    private bool _enableKazuhaSync = false;

    /// <summary>
    /// 万叶聚物完成后非万叶玩家原地再停留的秒数（让吸过来的物品被己方拾取），范围 [0, 30]，默认 1
    /// </summary>
    [ObservableProperty]
    private int _kazuhaSyncWaitSeconds = 1;

    /// <summary>
    /// 万叶聚物同步流程总超时秒数（在战斗点等待 + 聚物动作的总预算），范围 [5, 120]，默认 20
    /// </summary>
    [ObservableProperty]
    private int _kazuhaSyncTimeoutSeconds = 20;

    /// <summary>
    /// 万叶玩家等待 E 技 CD 的最长上限秒数（超时直接尝试释放，由 OCR + 视觉双判决定成败），范围 [3, 10]，默认 5。需保证小于 KazuhaSyncTimeoutSeconds
    /// </summary>
    [ObservableProperty]
    private int _kazuhaWaitSkillCdSeconds = 5;

    /// <summary>
    /// 拾取前精接近步数（联机万叶聚物）：
    /// 非万叶玩家在战后回点完成第一段粗接近后，再做"二段精接近"到万叶上报的聚物点 / 兜底接近战斗点。
    /// 步数越大上限耗时越长（每步 ~80ms），范围 [1, 30]，默认 6（约 0.5s 上限）。
    /// 仅联机万叶聚物分支生效，单机零回归（multiplayer-kazuha-collect-point-broadcast）。
    /// </summary>
    [ObservableProperty]
    private int _kazuhaSecondApproachMaxSteps = 6;

    /// <summary>
    /// 房间白名单，逗号分隔的玩家名称
    /// </summary>
    [ObservableProperty]
    private string _roomWhitelist = "";

    /// <summary>
    /// 房间期望人数（2-4），用于判断人齐条件
    /// </summary>
    [ObservableProperty]
    private int _expectedPlayerCount = 4;

    /// <summary>
    /// 组队等待超时（秒），超时后停止联机锄地
    /// </summary>
    [ObservableProperty]
    private int _partyTimeoutSeconds = 600;

    /// <summary>
    /// 组队超时动作：0=结束任务，1=现有人数锄地
    /// </summary>
    [ObservableProperty]
    private int _partyTimeoutAction = 0;

    // ========== 第五部分：联机角色配置（配置组专用） ==========

    /// <summary>
    /// 联机角色：host=房主，member=成员
    /// </summary>
    [ObservableProperty]
    private string _multiplayerRole = "host";

    /// <summary>
    /// 成员加入方式：byHostName=指定玩家名称，random=随机加入现有房间
    /// </summary>
    [ObservableProperty]
    private string _memberJoinMode = "random";

    /// <summary>
    /// 成员加入时指定的房主玩家名称
    /// </summary>
    [ObservableProperty]
    private string _targetHostName = "";

    // ========== 第六部分：多世界连续锄地配置 ==========

    /// <summary>
    /// 启用多世界连续锄地（房主设定，完成一个世界后轮换到下一个玩家的世界）
    /// </summary>
    [ObservableProperty]
    private bool _multiWorldEnabled = false;

    /// <summary>
    /// 多世界锄地轮数（1-4），由房主设定，按加入顺序依次成为房主
    /// </summary>
    [ObservableProperty]
    private int _multiWorldCount = 2;

    // === 反复复苏双层兜底（multi-revival-rapid-recurrence-fallback spec, OQ-1 / OQ-2 默认值）===

    /// <summary>
    /// 反复复苏滑动窗口（秒）：在此窗口内累计复苏次数达 RapidRevivalThreshold 即触发"跳本段 + 神像回血"。
    /// 默认 60 秒，覆盖实测日志最坏间隔（29s）。范围 [10, 300]。
    /// </summary>
    [ObservableProperty]
    private int _rapidRevivalWindowSeconds = 60;

    /// <summary>
    /// 反复复苏触发阈值（次）：滑动窗口内累计达此次数即触发"跳本段 + 神像回血"。
    /// 默认 2 次（即"窗口内出现第 2 次复苏立即升级"）。范围 [2, 10]。
    /// </summary>
    [ObservableProperty]
    private int _rapidRevivalThreshold = 2;

    /// <summary>
    /// 单条路线复苏次数上限：路线累计达此次数即触发"跳整路线 + 神像回血"（防死循环）。
    /// 默认 3 次，与 MaxConsecutiveSkips 保持一致。范围 [2, 10]。
    /// </summary>
    [ObservableProperty]
    private int _routeRevivalCap = 3;

    // === 路线变体偏好（route-variant-sync-by-logical-id spec / R13）===

    /// <summary>
    /// 路线变体偏好字典：key = LogicalRouteId, value = 玩家选中的变体文件名。
    /// 全局存储（落盘到 User/config.json），不进 RoomConfig（不同步给其他玩家——
    /// 不同玩家独立选自己跑哪个变体是核心价值）。
    /// 默认空字典；老用户升级后所有路线走"无偏好"分支，行为完全不变。
    ///
    /// ⚠️ 直接 mutate 字典（如 VariantPreferences[k] = v）不会触发 PropertyChanged，
    ///    持久化会丢失。请使用 SetVariantPreference / RemoveVariantPreference 方法。
    /// </summary>
    [ObservableProperty]
    private Dictionary<string, string> _variantPreferences = new();

    /// <summary>
    /// 设置某 LogicalRouteId 的变体偏好（R13.5）。
    /// 类内显式 OnPropertyChanged，避开 [ObservableProperty] 字典 mutate 不触发的陷阱。
    /// </summary>
    public void SetVariantPreference(string logicalRouteId, string fileName)
    {
        if (string.IsNullOrEmpty(logicalRouteId)) return;
        _variantPreferences[logicalRouteId] = fileName ?? string.Empty;
        OnPropertyChanged(nameof(VariantPreferences));
    }

    /// <summary>
    /// 移除某 LogicalRouteId 的变体偏好。
    /// </summary>
    public void RemoveVariantPreference(string logicalRouteId)
    {
        if (string.IsNullOrEmpty(logicalRouteId)) return;
        if (_variantPreferences.Remove(logicalRouteId))
            OnPropertyChanged(nameof(VariantPreferences));
    }
}
