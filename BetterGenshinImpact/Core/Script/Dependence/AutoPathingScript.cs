using System;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.Common;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace BetterGenshinImpact.Core.Script.Dependence;

public class AutoPathingScript
{
    private object? _config = null;
    private string _rootPath;
    private readonly LimitedFile _autoPathingFile;

    public AutoPathingScript(string rootPath, object? config)
    {
        _config = config;
        _rootPath = rootPath;
        _autoPathingFile = new LimitedFile(Global.Absolute(@"User\AutoPathing"));
    }

    public async Task Run(string json, CancellationToken ct = default)
    {
        try
        {
            if (ct == default)
            {
                ct = CancellationContext.Instance.Cts.Token;
            }
            else
            {
                TaskControl.Logger.LogWarning("执行地图追踪传入Cts");
                ct = CancellationContext.Instance.Register(ct);
            }

            var task = PathingTask.BuildFromJson(json);
            var pathExecutor = new PathExecutor(ct);
            if (_config != null && _config is PathingPartyConfig patyConfig)
            {
                pathExecutor.PartyConfig = patyConfig;
            }

            await pathExecutor.Pathing(task);
        }
        catch (OperationCanceledException)
        {
            TaskControl.Logger.LogInformation("路径追踪任务被取消");
        }
        catch (ObjectDisposedException e)
        {
            TaskControl.Logger.LogError("访问已释放的对象: {Msg}", e.Message);
        }
        catch (Exception e)
        {
            TaskControl.Logger.LogDebug(e, "执行地图追踪时候发生错误");
            TaskControl.Logger.LogError("执行地图追踪时候发生错误: {Msg}", e.Message);
        }
    }

    public async Task RunFile(string path,CancellationToken ct = default)
    {
        try
        {
            var json = await new LimitedFile(_rootPath).ReadText(path);
            
            PathingConditionConfig.GetCountryName(path);
            
            await Run(json,ct);
        }
        catch (Exception e)
        {
            TaskControl.Logger.LogDebug(e,"读取文件时发生错误");
            TaskControl.Logger.LogError("读取文件时发生错误: {Msg}",e.Message);
        }
    }

    /// <summary>
    /// 从已订阅的内容中获取文件
    /// </summary>
    /// <param name="path">在 `\User\AutoPathing` 目录下获取文件</param>
    public async Task RunFileFromUser(string path,CancellationToken ct = default)
    {
        var json = await AutoPathingFile.ReadText(path);
        await Run(json);
    }
}