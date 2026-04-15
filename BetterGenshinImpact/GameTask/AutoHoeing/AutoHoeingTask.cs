using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.AutoHoeing.Models;
using BetterGenshinImpact.GameTask.AutoHoeing.Services;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.Job;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static BetterGenshinImpact.GameTask.Common.TaskControl;

namespace BetterGenshinImpact.GameTask.AutoHoeing;

/// <summary>
/// 锄地一条龙 - BetterGI原生C#独立任务
/// 将JS脚本"锄地一条龙"的全部功能转写为原生任务
/// </summary>
public class AutoHoeingTask : ISoloTask
{
    public string Name => "锄地一条龙";

    private readonly ILogger<AutoHoeingTask> _logger = App.GetLogger<AutoHoeingTask>();
    private AutoHoeingConfig _config = null!;
    private CancellationToken _ct;

    /// <summary>
    /// 配置组传入的地图追踪配置（含队伍、战斗策略等），为null时使用默认配置
    /// </summary>
    private readonly PathingPartyConfig? _partyConfig;

    /// <summary>
    /// 配置组传入的独立任务配置覆盖，为null时使用全局AutoHoeingConfig
    /// </summary>
    private readonly Dictionary<string, object?>? _settingsOverride;

    // 数据目录（路线文件、怪物信息、运行记录等）
    private string _dataDir = "";

    // 服务
    private readonly MonsterInfoRepository _monsterRepo = new();
    private readonly CdManager _cdManager = new();
    private readonly RouteSelector _routeSelector = new();
    private readonly TimeRestrictionChecker _timeChecker = new();
    private readonly TemplatePickupService _pickupService = new();
    private readonly AnomalyDetector _anomalyDetector = new();
    private readonly DumperService _dumperService = new();
    private readonly BlacklistManager _blacklistManager = new();
    private readonly CookingService _cookingService = new();
    private RouteExecutionEngine? _executionEngine;
    private bool _shouldSwitchFurina;

    public AutoHoeingTask(PathingPartyConfig? partyConfig = null, Dictionary<string, object?>? settings = null)
    {
        _partyConfig = partyConfig;
        _settingsOverride = settings;
    }

    public async Task Start(CancellationToken ct)
    {
        _ct = ct;
        _config = TaskContext.Instance().Config.AutoHoeingConfig;

        // 如果有配置组传入的覆盖配置，应用到_config的副本
        if (_settingsOverride != null && _settingsOverride.Count > 0)
        {
            ApplySettingsOverride();
        }

        // 数据目录：使用JS脚本原有的pathing目录
        _dataDir = Path.Combine(
            AppContext.BaseDirectory,
            "User", "JsScript", "AutoHoeingOneDragon");

        // 检查JS脚本资源是否存在
        if (!Directory.Exists(_dataDir) || !Directory.Exists(Path.Combine(_dataDir, "pathing")))
        {
            _logger.LogError("锄地一条龙资源目录不存在: {Dir}", _dataDir);
            _logger.LogError("请先在「脚本仓库」中订阅并下载「AutoHoeingOneDragon」JS脚本，独立任务依赖该脚本的路线和资源文件");

            // 在UI线程弹窗提示
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Wpf.Ui.Violeta.Controls.Toast.Warning(
                    "锄地一条龙资源未找到，请先在「脚本仓库」中订阅并下载「AutoHoeingOneDragon」脚本");
            });
            return;
        }

        _logger.LogInformation("锄地一条龙任务启动，数据目录: {Dir}", _dataDir);

        try
        {
            await RunTask();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("锄地一条龙任务被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "锄地一条龙任务异常终止");
        }
    }

    private async Task RunTask()
    {
        // 1. 加载配置
        var accountName = string.IsNullOrEmpty(_config.AccountName) ? "默认账户" : _config.AccountName;
        LoadGroupSettings(accountName);

        // 2. 解析时间限制
        _timeChecker.ParseRestrictions(_config.NoRunPeriod);

        // 3. 加载怪物信息
        var monsterInfoPath = Path.Combine(_dataDir, "assets", "monsterInfo.json");
        _monsterRepo.Load(monsterInfoPath);

        // 3.5 初始化并发服务
        var assetsDir = Path.Combine(_dataDir, "assets");
        _pickupService.LoadTemplates(assetsDir, _config.PickupMode);
        _anomalyDetector.LoadTemplates(assetsDir);
        _blacklistManager.Load(_dataDir, accountName);
        _blacklistManager.LoadItemFullTemplate(assetsDir);
        _cookingService.LoadTemplates(assetsDir);
        _executionEngine = new RouteExecutionEngine(
            _pickupService, _anomalyDetector, _dumperService, _blacklistManager, _config, _partyConfig);

        // 4. 加载CD记录
        _cdManager.Load(_dataDir, accountName);

        // 5. 构建分组标签
        var groupTags = BuildGroupTags();

        // 6. 根据操作模式执行
        var operationMode = _config.OperationMode;

        if (operationMode == "启用仅指定怪物模式")
        {
            await RunTargetMonsterMode(accountName, groupTags);
        }
        else
        {
            // 预处理路线
            var pathingDir = Path.Combine(_dataDir, "pathing");
            var routes = RouteInfoLoader.LoadRoutes(
                pathingDir, _monsterRepo, _config.IgnoreRate, groupTags[0]);

            // 初始化CD和运行记录
            foreach (var route in routes)
                _cdManager.InitializeRoute(route);

            // 自我优化
            SelfOptimizer.Apply(routes, _config.DisableSelfOptimization, _config.CuriosityFactor);

            // 标记过滤
            var priorityTags = ParseChineseTags(_config.PriorityTags);
            var excludeTags = ParseChineseTags(_config.ExcludeTags);
            if (!_config.PickupMode.Contains("模板匹配") && !excludeTags.Contains("沙暴"))
                excludeTags.Add("沙暴");

            RouteMarker.MarkRoutes(routes, groupTags, priorityTags, excludeTags);

            // 路线选择优化
            var targetElite = Math.Max(0, _config.TargetEliteNum) + 5;
            var targetMonster = Math.Max(0, _config.TargetMonsterNum) + 25;
            _routeSelector.SelectOptimalRoutes(
                routes, _config.EfficiencyIndex, targetElite, targetMonster, _config.SortMode);

            // 分组分配
            RouteGroupAssigner.AssignGroups(routes, groupTags);

            if (operationMode == "调试路线分配")
            {
                RouteGroupAssigner.PrintGroupSummary(routes, _config, _dataDir);
                _cdManager.UpdateAllRecords(routes);
            }
            else if (operationMode == "运行锄地路线")
            {
                // 队伍校验
                ValidateTeam();

                _logger.LogInformation("开始运行锄地路线");
                _cdManager.UpdateAllRecords(routes);
                await ProcessRoutesByGroup(routes, accountName);
            }
            else // 强制刷新所有运行记录
            {
                _logger.LogInformation("强制刷新所有运行记录");
                _cdManager.ClearAll();
                // 同时清除内存中路线对象上的CD和运行记录
                foreach (var route in routes)
                {
                    route.CdTime = DateTime.MinValue;
                    route.Records.Clear();
                }
                _cdManager.UpdateAllRecords(routes);
            }
        }
    }

    private async Task RunTargetMonsterMode(string accountName, List<List<string>> groupTags)
    {
        var targetMonsters = ParseChineseTags(_config.TargetMonsters);
        if (targetMonsters.Count == 0)
        {
            _logger.LogError("目标怪物为空，请检查配置");
            return;
        }

        _logger.LogInformation("目标怪物模式：{Monsters}", string.Join("、", targetMonsters));

        var pathingDir = Path.Combine(_dataDir, "pathing");
        var fakeGroupTags = Enumerable.Range(0, 10).Select(_ => new List<string>()).ToList();
        var routes = RouteInfoLoader.LoadRoutes(pathingDir, _monsterRepo, _config.IgnoreRate, fakeGroupTags[0]);

        foreach (var route in routes)
            _cdManager.InitializeRoute(route);

        // 逐路线匹配
        foreach (var route in routes)
        {
            var textToSearch = (route.FullPath ?? "") + " " +
                string.Join(" ", route.MonsterInfo.Keys);
            route.Selected = targetMonsters.Any(m => textToSearch.Contains(m));
            route.Group = route.Selected ? 1 : 0;
        }

        var selectedCount = routes.Count(p => p.Selected);
        _logger.LogInformation("目标怪物模式：共找到 {Count} 条相关路线", selectedCount);

        _cdManager.UpdateAllRecords(routes);
        await ProcessRoutesByGroup(routes, accountName);
    }

    private async Task ProcessRoutesByGroup(List<RouteInfo> routes, string accountName)
    {
        var targetGroup = _config.GroupIndex;
        var groupRoutes = routes.Where(r => r.Group == targetGroup && r.Selected).ToList();

        // 计算组内总计信息
        var totalElites = groupRoutes.Sum(r => r.EliteMonsterCount);
        var totalMonsters = groupRoutes.Sum(r => r.NormalMonsterCount);
        var totalGain = groupRoutes.Sum(r => r.EliteMoraGain + r.NormalMoraGain);
        var totalEstimatedTime = groupRoutes.Sum(r => r.AdjustedTime);

        var tsTotal = TimeSpan.FromSeconds(totalEstimatedTime);
        _logger.LogInformation("当前组 路径组{G} 共 {Count} 条路线，精英{E}，小怪{M}，预计用时 {H}时{Min}分{S}秒",
            targetGroup, groupRoutes.Count, totalElites, totalMonsters,
            (int)tsTotal.TotalHours, tsTotal.Minutes, tsTotal.Seconds);

        // 切换队伍
        if (!string.IsNullOrEmpty(_config.PartyName))
        {
            _logger.LogInformation("切换至配置队伍: {Name}", _config.PartyName);
            var switchSuccess = await new SwitchPartyTask().Start(_config.PartyName, _ct);
            if (!switchSuccess)
            {
                _logger.LogWarning("切换队伍失败: {Name}，继续执行", _config.PartyName);
            }
            await Delay(500, _ct);
        }

        int count = 0;
        var groupStartTime = DateTime.Now;
        double remainingEstimatedTime = totalEstimatedTime;
        double skippedTime = 0;

        foreach (var route in groupRoutes)
        {
            _ct.ThrowIfCancellationRequested();
            count++;

            // 时间限制检查
            if (_timeChecker.IsInRestrictedPeriod() || _timeChecker.IsApproachingRestriction())
            {
                _logger.LogWarning("接近或处于限制时间，停止执行");
                break;
            }

            // CD检查
            if (_cdManager.IsOnCooldown(route))
            {
                _logger.LogInformation("路线 {Name} 未刷新，跳过", route.FileName);
                skippedTime += route.AdjustedTime;
                remainingEstimatedTime -= route.AdjustedTime;
                continue;
            }

            _logger.LogInformation("开始处理第 {G} 组第 {N}/{T} 个: {Name}",
                targetGroup, count, groupRoutes.Count, route.FileName);

            // 白芙切换
            if (_shouldSwitchFurina)
            {
                _logger.LogInformation("上条路线检测到白芙，执行强制黑芙切换");
                _shouldSwitchFurina = false;
                var switchPath = Path.Combine(_dataDir, "assets", "强制黑芙.json");
                if (File.Exists(switchPath))
                {
                    var switchTask = PathingTask.BuildFromFilePath(switchPath);
                    if (switchTask != null)
                    {
                        var executor = new PathExecutor(_ct);
                        executor.PartyConfig = _partyConfig;
                        await executor.Pathing(switchTask);
                    }
                }
            }

            // 料理buff
            await _cookingService.TryUseCooking(_config.CookingNames, _ct);

            var sw = Stopwatch.StartNew();

            try
            {
                if (_executionEngine != null)
                {
                    var execResult = await _executionEngine.ExecuteRoute(route, _ct);
                    _shouldSwitchFurina = execResult.ShouldSwitchFurina;
                    sw.Stop();
                    var duration = execResult.ActualDuration;

                    // 更新剩余时间
                    remainingEstimatedTime -= route.AdjustedTime;

                    if (execResult.FullyCompleted)
                    {
                        _cdManager.RecordCompletion(route, duration);
                        _cdManager.UpdateAllRecords(routes);
                    }
                    else
                    {
                        _logger.LogWarning("路线 {Name} 未完整执行（中断/异常），不记录CD", route.FileName);
                    }

                    // 计算预计剩余时间
                    var actualUsedTime = (DateTime.Now - groupStartTime).TotalSeconds;
                    var consumedEstimated = totalEstimatedTime - remainingEstimatedTime - skippedTime;
                    var predictRemaining = consumedEstimated > 0
                        ? remainingEstimatedTime * actualUsedTime / consumedEstimated
                        : remainingEstimatedTime;
                    var tsRemain = TimeSpan.FromSeconds(Math.Max(0, predictRemaining));

                    _logger.LogInformation(
                        "当前进度：第 {G} 组第 {N}/{T} 个  {Name}已完成，该组预计剩余: {H} 时 {Min} 分 {S} 秒",
                        targetGroup, count, groupRoutes.Count, route.FileName,
                        (int)tsRemain.TotalHours, tsRemain.Minutes, tsRemain.Seconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("执行路线 {Name} 出错: {Msg}", route.FileName, ex.Message);
            }
        }
    }

    private void ValidateTeam()
    {
        if (_config.SkipValidation)
        {
            _logger.LogWarning("已跳过校验阶段");
            return;
        }

        // 基本校验（窗口分辨率等）
        var gameInfo = TaskContext.Instance().SystemInfo;
        if (gameInfo.CaptureAreaRect.Width != 1920 || gameInfo.CaptureAreaRect.Height != 1080)
        {
            _logger.LogWarning("游戏窗口非 1920×1080，可能导致图像识别失败");
        }
    }

    private void LoadGroupSettings(string accountName)
    {
        if (_config.GroupIndex == 1)
        {
            // 路径组一：保存配置
            var settings = new AccountGroupSettings
            {
                TagsForGroup1 = _config.TagsForGroup1,
                TagsForGroup2 = _config.TagsForGroup2,
                TagsForGroup3 = _config.TagsForGroup3,
                TagsForGroup4 = _config.TagsForGroup4,
                TagsForGroup5 = _config.TagsForGroup5,
                TagsForGroup6 = _config.TagsForGroup6,
                TagsForGroup7 = _config.TagsForGroup7,
                TagsForGroup8 = _config.TagsForGroup8,
                TagsForGroup9 = _config.TagsForGroup9,
                TagsForGroup10 = _config.TagsForGroup10,
                DisableSelfOptimization = _config.DisableSelfOptimization,
                EfficiencyIndex = _config.EfficiencyIndex,
                CuriosityFactor = _config.CuriosityFactor.ToString(),
                IgnoreRate = _config.IgnoreRate,
                TargetEliteNum = _config.TargetEliteNum,
                TargetMonsterNum = _config.TargetMonsterNum,
                PriorityTags = _config.PriorityTags,
                ExcludeTags = _config.ExcludeTags
            };
            var filePath = Path.Combine(_dataDir, "settings", $"{accountName}.json");
            settings.SaveToFile(filePath);
        }
        else
        {
            // 非路径组一：加载配置
            var filePath = Path.Combine(_dataDir, "settings", $"{accountName}.json");
            var settings = AccountGroupSettings.LoadFromFile(filePath);
            if (settings != null)
            {
                _config.TagsForGroup1 = settings.TagsForGroup1;
                _config.TagsForGroup2 = settings.TagsForGroup2;
                _config.TagsForGroup3 = settings.TagsForGroup3;
                _config.TagsForGroup4 = settings.TagsForGroup4;
                _config.TagsForGroup5 = settings.TagsForGroup5;
                _config.TagsForGroup6 = settings.TagsForGroup6;
                _config.TagsForGroup7 = settings.TagsForGroup7;
                _config.TagsForGroup8 = settings.TagsForGroup8;
                _config.TagsForGroup9 = settings.TagsForGroup9;
                _config.TagsForGroup10 = settings.TagsForGroup10;
                _config.EfficiencyIndex = settings.EfficiencyIndex;
                _config.IgnoreRate = settings.IgnoreRate;
                _config.TargetEliteNum = settings.TargetEliteNum;
                _config.TargetMonsterNum = settings.TargetMonsterNum;
                _config.PriorityTags = settings.PriorityTags;
                _config.ExcludeTags = settings.ExcludeTags;
            }
            else
            {
                _logger.LogError("配置文件不存在，请先在路径组一运行一次");
            }
        }
    }

    private List<List<string>> BuildGroupTags()
    {
        var groupSettings = new[]
        {
            _config.TagsForGroup1, _config.TagsForGroup2, _config.TagsForGroup3,
            _config.TagsForGroup4, _config.TagsForGroup5, _config.TagsForGroup6,
            _config.TagsForGroup7, _config.TagsForGroup8, _config.TagsForGroup9,
            _config.TagsForGroup10
        };

        var groupTags = groupSettings
            .Select(s => ParseChineseTags(s))
            .ToList();

        // 第0组 = 所有组标签的并集去重
        groupTags[0] = groupTags.SelectMany(t => t).Distinct().ToList();

        return groupTags;
    }

    private static List<string> ParseChineseTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new();
        return input.Split('，')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    /// <summary>
    /// 将配置组传入的覆盖值应用到当前配置
    /// </summary>
    private void ApplySettingsOverride()
    {
        if (_settingsOverride == null) return;

        T Get<T>(string key, T fallback)
        {
            if (_settingsOverride.TryGetValue(key, out var val) && val != null)
            {
                try { return (T)Convert.ChangeType(val, typeof(T)); }
                catch { return fallback; }
            }
            return fallback;
        }

        // groupIndex: 下拉框值是"路径组一"~"路径组十"，需要转换为数字1-10
        var groupIndexStr = Get("groupIndex", "");
        if (!string.IsNullOrEmpty(groupIndexStr))
        {
            var groupMap = new Dictionary<string, int>
            {
                ["路径组一"] = 1, ["路径组二"] = 2, ["路径组三"] = 3, ["路径组四"] = 4, ["路径组五"] = 5,
                ["路径组六"] = 6, ["路径组七"] = 7, ["路径组八"] = 8, ["路径组九"] = 9, ["路径组十"] = 10
            };
            if (groupMap.TryGetValue(groupIndexStr, out var idx))
                _config.GroupIndex = idx;
        }

        _config.OperationMode = Get("operationMode", _config.OperationMode);
        _config.PartyName = Get("partyName", _config.PartyName);
        _config.SortMode = Get("sortMode", _config.SortMode);
        _config.PickupMode = Get("pickupMode", _config.PickupMode);
        _config.UseRouteRelatedMaterialsOnly = Get("useRouteRelatedMaterialsOnly", _config.UseRouteRelatedMaterialsOnly);
        _config.DisableSecondaryValidation = Get("disableSecondaryValidation", _config.DisableSecondaryValidation);
        _config.DumperCharacters = Get("dumperCharacters", _config.DumperCharacters);
        _config.CookingNames = Get("cookingNames", _config.CookingNames);
        _config.NoRunPeriod = Get("noRunPeriod", _config.NoRunPeriod);
        _config.FindFInterval = Get("findFInterval", _config.FindFInterval);
        _config.PickupDelay = Get("pickupDelay", _config.PickupDelay);
        _config.RollingDelay = Get("rollingDelay", _config.RollingDelay);
        _config.ScrollCycle = Get("scrollCycle", _config.ScrollCycle);
        _config.LogMonsterCount = Get("logMonsterCount", _config.LogMonsterCount);
        _config.DisableAsync = Get("disableAsync", _config.DisableAsync);
        _config.EnableCoordinateCheck = Get("enableCoordinateCheck", _config.EnableCoordinateCheck);
        _config.SkipValidation = Get("skipValidation", _config.SkipValidation);
        _config.AccountName = Get("accountName", _config.AccountName);
        _config.TagsForGroup1 = Get("tagsForGroup1", _config.TagsForGroup1);
        _config.TagsForGroup2 = Get("tagsForGroup2", _config.TagsForGroup2);
        _config.TagsForGroup3 = Get("tagsForGroup3", _config.TagsForGroup3);
        _config.TagsForGroup4 = Get("tagsForGroup4", _config.TagsForGroup4);
        _config.TagsForGroup5 = Get("tagsForGroup5", _config.TagsForGroup5);
        _config.TagsForGroup6 = Get("tagsForGroup6", _config.TagsForGroup6);
        _config.TagsForGroup7 = Get("tagsForGroup7", _config.TagsForGroup7);
        _config.TagsForGroup8 = Get("tagsForGroup8", _config.TagsForGroup8);
        _config.TagsForGroup9 = Get("tagsForGroup9", _config.TagsForGroup9);
        _config.TagsForGroup10 = Get("tagsForGroup10", _config.TagsForGroup10);
        _config.DisableSelfOptimization = Get("disableSelfOptimization", _config.DisableSelfOptimization);
        _config.EfficiencyIndex = Get("efficiencyIndex", _config.EfficiencyIndex);
        _config.CuriosityFactor = Get("curiosityFactor", _config.CuriosityFactor);
        _config.IgnoreRate = Get("ignoreRate", _config.IgnoreRate);
        _config.TargetEliteNum = Get("targetEliteNum", _config.TargetEliteNum);
        _config.TargetMonsterNum = Get("targetMonsterNum", _config.TargetMonsterNum);
        _config.PriorityTags = Get("priorityTags", _config.PriorityTags);
        _config.ExcludeTags = Get("excludeTags", _config.ExcludeTags);
        _config.TargetMonsters = Get("targetMonsters", _config.TargetMonsters);
    }

    /// <summary>
    /// 获取可配置参数定义（供UI编辑使用），顺序和说明与JS settings.json一致
    /// </summary>
    public static List<SoloTaskSettingItem> GetSettingDefinitions()
    {
        var config = TaskContext.Instance().Config.AutoHoeingConfig;
        // groupIndex: 数字转为下拉框选项
        var groupNames = new[] { "路径组一","路径组二","路径组三","路径组四","路径组五","路径组六","路径组七","路径组八","路径组九","路径组十" };
        var currentGroup = config.GroupIndex >= 1 && config.GroupIndex <= 10 ? groupNames[config.GroupIndex - 1] : "路径组一";

        return new List<SoloTaskSettingItem>
        {
            // ===== 第一部分：路径组执行配置 =====
            new() { Name = "operationMode", Label = "执行模式", Type = "select", DefaultValue = config.OperationMode,
                Options = new() { "运行锄地路线", "调试路线分配", "强制刷新所有运行记录", "启用仅指定怪物模式" } },
            new() { Name = "groupIndex", Label = "选择执行第几个路径组", Type = "select", DefaultValue = currentGroup,
                Options = new(groupNames) },
            new() { Name = "partyName", Label = "本路径组使用配队名称\n【注意】请只在这里填写要使用的配队，配置组中配队项留空", Type = "text", DefaultValue = config.PartyName },
            new() { Name = "sortMode", Label = "组内路线排序模式", Type = "select", DefaultValue = config.SortMode,
                Options = new() { "原文件顺序", "效率降序", "高收益优先" } },
            new() { Name = "pickupMode", Label = "拾取模式\n【注意】bgi原版拾取性能开销大，准确低，尽量不要使用", Type = "select", DefaultValue = config.PickupMode,
                Options = new() { "模板匹配拾取狗粮和怪物材料", "模板匹配仅拾取狗粮", "BGI原版拾取", "不拾取" } },
            new() { Name = "useRouteRelatedMaterialsOnly", Label = "只使用路线相关怪物材料进行识别，提高性能\n仅在选择模板匹配拾取狗粮和怪物材料时生效\n推荐先不勾选运行一段时间获取历史数据后勾选", Type = "bool", DefaultValue = config.UseRouteRelatedMaterialsOnly },
            new() { Name = "disableSecondaryValidation", Label = "禁用识别到物品后的二次校验，可能增加误捡概率", Type = "bool", DefaultValue = config.DisableSecondaryValidation },
            new() { Name = "dumperCharacters", Label = "泥头车模式，将在接近战斗点前提前释放部分角色E技能\n需要启用时填写角色在队伍中的编号，多个用中文逗号分隔\n【注意】精英路线启用泥头车将有可能导致狗粮损失", Type = "text", DefaultValue = config.DumperCharacters },
            new() { Name = "cookingNames", Label = "使用料理名称，将在路线之间尝试使用对应名称的料理\n多个料理名称之间使用中文逗号分隔，使用间隔为300秒", Type = "text", DefaultValue = config.CookingNames },
            new() { Name = "noRunPeriod", Label = "不运行时段\n示例：单个小时：8  连续区间：8-11 或 23:11-23:55\n多项用中文逗号分隔，留空=全天可运行", Type = "text", DefaultValue = config.NoRunPeriod },
            new() { Name = "findFInterval", Label = "识别间隔(ms)，两次检测F图标之间等待时间，建议10-200", Type = "number", DefaultValue = config.FindFInterval },
            new() { Name = "pickupDelay", Label = "拾取后延时(ms)，连续拾取相同物品时建议调大，建议32-200", Type = "number", DefaultValue = config.PickupDelay },
            new() { Name = "rollingDelay", Label = "滚动后延时(ms)，拾取错误时建议调大，建议16-100", Type = "number", DefaultValue = config.RollingDelay },
            new() { Name = "scrollCycle", Label = "单次滚动周期(ms)，上下滚动不全时建议调大，建议800-2000", Type = "number", DefaultValue = config.ScrollCycle },
            new() { Name = "logMonsterCount", Label = "运行路线时输出交互或拾取精英和小怪数量，便于在日志分析中比对", Type = "bool", DefaultValue = config.LogMonsterCount },
            new() { Name = "disableAsync", Label = "禁用异步操作，设备性能过低换队受影响时选择性勾选", Type = "bool", DefaultValue = config.DisableAsync },
            new() { Name = "enableCoordinateCheck", Label = "路线结尾时进行坐标检查\n用于在路线出现卡死等放弃时不记录CD信息", Type = "bool", DefaultValue = config.EnableCoordinateCheck },
            new() { Name = "skipValidation", Label = "跳过校验阶段\n确认跳过校验阶段，任何包括但不限于漏怪、卡死、不拾取等问题均由自己配置引起", Type = "bool", DefaultValue = config.SkipValidation },

            // ===== 第二部分：路线选择与分组配置 =====
            new() { Name = "accountName", Label = "账户名称\n用于多用户运行时区分不同账户的记录，单用户请勿修改", Type = "text", DefaultValue = config.AccountName },
            new() { Name = "tagsForGroup1", Label = "路径组一要【排除】的标签\n允许使用的标签：水免，次数盾，高危，传奇，蕈兽，小怪，沙暴，狭窄地形，环境伤害\n多个标签使用中文逗号分隔", Type = "text", DefaultValue = config.TagsForGroup1 },
            new() { Name = "tagsForGroup2", Label = "路径组二要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup2 },
            new() { Name = "tagsForGroup3", Label = "路径组三要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup3 },
            new() { Name = "tagsForGroup4", Label = "路径组四要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup4 },
            new() { Name = "disableSelfOptimization", Label = "禁用根据运行记录优化路线选择的功能\n完全使用路线原有信息", Type = "bool", DefaultValue = config.DisableSelfOptimization },
            new() { Name = "efficiencyIndex", Label = "摩拉/耗时权衡因数，填0及以上的数字\n越大越倾向于花费较多时间提高总收益\n含义为愿意为1摩拉多花多少秒", Type = "number", DefaultValue = config.EfficiencyIndex },
            new() { Name = "curiosityFactor", Label = "好奇系数，缺少记录的路线预期用时将被削减对应比例\n填0-1之间的数", Type = "number", DefaultValue = config.CuriosityFactor },
            new() { Name = "ignoreRate", Label = "小怪数量/精英数量大于该值的路线将被视为纯小怪路线\n忽略其中包含的精英", Type = "number", DefaultValue = config.IgnoreRate },
            new() { Name = "targetEliteNum", Label = "目标精英数量", Type = "number", DefaultValue = config.TargetEliteNum },
            new() { Name = "targetMonsterNum", Label = "目标小怪数量", Type = "number", DefaultValue = config.TargetMonsterNum },
            new() { Name = "priorityTags", Label = "优先关键词，含关键词的路线会被视为最高效率\n不同关键词使用中文逗号分隔\n仅优先选择，不影响路线排序", Type = "text", DefaultValue = config.PriorityTags },
            new() { Name = "excludeTags", Label = "排除关键词，含关键词的路线会被完全排除\n不同关键词使用中文逗号分隔", Type = "text", DefaultValue = config.ExcludeTags },
            new() { Name = "tagsForGroup5", Label = "路径组五要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup5 },
            new() { Name = "tagsForGroup6", Label = "路径组六要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup6 },
            new() { Name = "tagsForGroup7", Label = "路径组七要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup7 },
            new() { Name = "tagsForGroup8", Label = "路径组八要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup8 },
            new() { Name = "tagsForGroup9", Label = "路径组九要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup9 },
            new() { Name = "tagsForGroup10", Label = "路径组十要【选择】的标签", Type = "text", DefaultValue = config.TagsForGroup10 },

            // ===== 第三部分：仅指定怪物模式 =====
            new() { Name = "targetMonsters", Label = "目标怪物\n建议按照怪物图鉴中的名字填写，有多个目标时使用中文逗号分隔", Type = "text", DefaultValue = config.TargetMonsters },
        };
    }
}
