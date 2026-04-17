using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.AutoFight.Model;
using BetterGenshinImpact.GameTask.AutoHoeing.Models;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static BetterGenshinImpact.GameTask.Common.TaskControl;

namespace BetterGenshinImpact.GameTask.AutoHoeing.Services;

/// <summary>
/// 路线执行引擎：调用PathExecutor执行地图追踪，并发运行拾取/异常检测/泥头车子任务
/// </summary>
public class RouteExecutionEngine
{
    private static readonly ILogger Logger = App.GetLogger<RouteExecutionEngine>();

    private readonly TemplatePickupService _pickupService;
    private readonly AnomalyDetector _anomalyDetector;
    private readonly DumperService _dumperService;
    private readonly BlacklistManager _blacklistManager;
    private readonly AutoHoeingConfig _config;
    private readonly PathingPartyConfig? _partyConfig;

    private volatile bool _running;

    public RouteExecutionEngine(
        TemplatePickupService pickupService,
        AnomalyDetector anomalyDetector,
        DumperService dumperService,
        BlacklistManager blacklistManager,
        AutoHoeingConfig config,
        PathingPartyConfig? partyConfig = null)
    {
        _pickupService = pickupService;
        _anomalyDetector = anomalyDetector;
        _dumperService = dumperService;
        _blacklistManager = blacklistManager;
        _config = config;
        _partyConfig = partyConfig;
    }

    /// <summary>
    /// 执行单条路线，并发启动所有子任务
    /// </summary>
    public async Task<RouteExecutionResult> ExecuteRoute(
        RouteInfo route, CancellationToken ct)
    {
        var result = new RouteExecutionResult();
        _running = true;
        _anomalyDetector.ShouldSwitchFurina = false;

        // 设置路线相关材料过滤
        if (_config.UseRouteRelatedMaterialsOnly)
            _pickupService.SetRouteRelatedMaterials(route.MonsterInfo, route.PickupHistory);
        else
            _pickupService.ResetAllEnabled();

        var sw = Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = cts.Token;

        bool IsRunning() => _running && !linkedCt.IsCancellationRequested;

        bool pathingFullyCompleted = false;

        // 主路线执行任务
        var pathingTask = Task.Run(async () =>
        {
            try
            {
                Logger.LogInformation("开始执行路线: {Name}", route.FileName);
                var task = PathingTask.BuildFromFilePath(route.FullPath);
                if (task != null)
                {
                    var executor = new PathExecutor(ct);
                    executor.PartyConfig = _partyConfig;
                    await executor.Pathing(task);
                    pathingFullyCompleted = executor.SuccessEnd;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("执行地图追踪出错: {Msg}", ex.Message);
            }
            finally
            {
                _running = false;
            }
        }, linkedCt);

        // 并发子任务列表
        var tasks = new List<Task> { pathingTask };

        // 模板匹配拾取
        if (_config.PickupMode.Contains("模板匹配"))
        {
            tasks.Add(Task.Run(() => _pickupService.RunPickupLoop(
                IsRunning, _blacklistManager.Blacklist,
                _config.PickupDelay, _config.RollingDelay,
                _config.ScrollCycle, _config.FindFInterval,
                linkedCt), linkedCt));
        }

        // 异常状态检测
        tasks.Add(Task.Run(() => _anomalyDetector.RunDetectionLoop(IsRunning, linkedCt), linkedCt));

        // 黑名单检测
        if (_config.PickupMode.Contains("模板匹配"))
        {
            tasks.Add(Task.Run(() => _blacklistManager.RunDetectionLoop(
                IsRunning, _pickupService.TargetItems.ToList(), linkedCt), linkedCt));
        }

        // 泥头车
        var dumperChars = ParseDumperCharacters(_config.DumperCharacters);
        if (dumperChars.Count > 0)
        {
            var pathingData = PathingTask.BuildFromFilePath(route.FullPath);
            if (pathingData != null)
            {
                CombatScenes? combatScenes = null;
                try
                {
                    using var region = CaptureToRectArea();
                    combatScenes = new CombatScenes().InitializeTeam(region);
                    if (!combatScenes.CheckTeamInitialized())
                    {
                        Logger.LogWarning("泥头车队伍识别失败，跳过泥头车功能");
                        combatScenes.Dispose();
                        combatScenes = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("泥头车CombatScenes初始化异常: {Msg}", ex.Message);
                    combatScenes?.Dispose();
                    combatScenes = null;
                }

                if (combatScenes != null)
                {
                    var cs = combatScenes;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _dumperService.RunDumperLoop(
                                pathingData.Positions, dumperChars, route.MapName,
                                cs, IsRunning, linkedCt);
                        }
                        finally
                        {
                            cs.Dispose();
                        }
                    }, linkedCt));
                }
            }
        }

        // 等待所有任务完成
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.LogDebug("并发任务异常: {Msg}", ex.Message);
        }

        sw.Stop();
        result.ActualDuration = sw.Elapsed.TotalSeconds;
        result.ShouldSwitchFurina = _anomalyDetector.ShouldSwitchFurina;
        result.Success = true;
        result.FullyCompleted = pathingFullyCompleted;

        return result;
    }

    private static List<int> ParseDumperCharacters(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new();
        return input.Split('，')
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
            .Where(n => n >= 1 && n <= 4)
            .ToList();
    }
}
