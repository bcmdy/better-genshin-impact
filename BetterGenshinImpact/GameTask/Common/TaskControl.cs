using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Exception;
using BetterGenshinImpact.GameTask.Model.Area;
using Fischless.GameCapture;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Vanara.PInvoke;
using System.Net.NetworkInformation;
using BetterGenshinImpact.GameTask.Common.Job;
using System.Threading;
using System.Threading.Tasks;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.GameTask.AutoFight.Assets;
using System.Linq;
using BetterGenshinImpact.Core.Recognition;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using BetterGenshinImpact.GameTask.Common;

namespace BetterGenshinImpact.GameTask.Common;

public class TaskControl
{
    public static ILogger Logger { get; } = App.GetLogger<TaskControl>();

    public static readonly SemaphoreSlim TaskSemaphore = new(1, 1);
    
    private static DateTime _lastCheckTime = DateTime.MinValue;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(TaskContext.Instance().Config.OtherConfig.NetworkDetectionInterval);
    private static readonly TimeSpan CheckIntervalWin = TimeSpan.FromSeconds(30);
    private static readonly Ping PingSender = new Ping();
    private static readonly bool NetworkDetectionConfig = TaskContext.Instance().Config.OtherConfig.NetworkDetectionConfig;
    private static int _networkFailureCount = 0;
    
    private static RecognitionObject GetConfirmRa(bool isOcrMatch = false,params string[] targetText)
    {
        var screenArea = CaptureToRectArea();
        var x = (int)(screenArea.Width * 0.3);
        var y = (int)(screenArea.Height * 0.1);
        var width = (int)(screenArea.Width * 0.65);
        var height = (int)(screenArea.Height * 0.85);
        
        return isOcrMatch ? RecognitionObject.OcrMatch(x, y, width, height, targetText) : 
            RecognitionObject.Ocr(x, y, width, height);
    }
    
    public static bool IsSuspendedByNetwork { get; set; } = false;
    
    public static bool IsSuspendedByWindow { get; set; } = false;

    private static Task CheckNetworkStatusAsync()
    {
        if (DateTime.UtcNow - _lastCheckTime < CheckInterval)
        {
            if (DateTime.UtcNow - _lastCheckTime > CheckIntervalWin)
            { 
                using var qq = CaptureToRectArea();
                var okRa = qq.Find(AutoFightAssets.Instance.ConfirmRaZ);
                {
                    if (okRa.IsExist())
                    {
                        Logger.LogWarning("弹窗状态:{0}",okRa.IsExist());
                        IsSuspendedByWindow = true;
                    }
                }
            }
            else
            {
                return Task.CompletedTask;
            }
        }
        
        _lastCheckTime = DateTime.UtcNow;

        var isSuspend = false; 
        try
        {
            var reply = PingSender.Send(TaskContext.Instance().Config.OtherConfig.NetworkDetectionUrl);
            isSuspend = reply.Status != IPStatus.Success;
            if (IsSuspendedByNetwork || IsSuspendedByWindow)
            {
                Logger.LogWarning(IsSuspendedByWindow ? "窗口弹窗状态恢复中..." : "网络恢复中...");
                if (NetworkRecovery.Start(CancellationToken.None).Wait(10000))
                {
                    isSuspend = false;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "网络状态检查：错误");
            isSuspend = true;
        }
        finally
        {
            if (isSuspend)
            {
                _networkFailureCount++;
                if (_networkFailureCount >= 3)
                {
                    try
                    {
                        var reply2 = PingSender.Send("www.qq.com");
                        if (reply2.Status != IPStatus.Success)
                        {
                            IsSuspendedByNetwork = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "网络状态检查：错误");
                        IsSuspendedByNetwork = true;
                    }
                }
            }
            else
            {
                _networkFailureCount = 0;
                IsSuspendedByNetwork = false;
                // var now = DateTime.UtcNow; // 声明并初始化 now 变量
                //
                // var targetStartTime = new DateTime(now.Year, now.Month, now.Day, 3, 59, 0); // 设置为当天的凌晨3点59分
                // var targetEndTime = new DateTime(now.Year, now.Month, now.Day, 4, 0, 0); // 设置为当天的凌晨4点
                //
                // if (now - _startTime > TimeSpan.FromDays(1) || (now >= targetStartTime && now < targetEndTime))
                // {
                //     throw new RetryException("超过1天未启动游戏，尝试重启游戏");
                // }
            }
        }
        return Task.CompletedTask;
    }

    public static void CheckAndSleep(int millisecondsTimeout)
    {
        TrySuspend();
        CheckAndActivateGameWindow();
        Thread.Sleep(millisecondsTimeout);
    }

    public static void Sleep(int millisecondsTimeout)
    {
        NewRetry.Do(() =>
        {
            TrySuspend();
            CheckAndActivateGameWindow();
        }, TimeSpan.FromSeconds(1), 100);
        Thread.Sleep(millisecondsTimeout);
    }

    private static bool IsKeyPressed(User32.VK key)
    {
        // 获取按键状态
        var state = User32.GetAsyncKeyState((int)key);

        // 检查高位是否为 1（表示按键被按下）
        return (state & 0x8000) != 0;
    }

    public static void TrySuspend()
    {
        if (NetworkDetectionConfig)Task.Run(CheckNetworkStatusAsync);
        var first = true;
        //此处为了记录最开始的暂停状态
        var isSuspend = RunnerContext.Instance.IsSuspend || IsSuspendedByNetwork;
        while (RunnerContext.Instance.IsSuspend || IsSuspendedByNetwork)
        {
            if (RunnerContext.Instance.IsSuspend) IsSuspendedByNetwork = false; NetworkRecovery.RecoveryNetworkDone = true;
            if (first)
            {
                RunnerContext.Instance.StopAutoPick();
                //使快捷键本身释放
                Thread.Sleep(300);
                foreach (User32.VK key in Enum.GetValues(typeof(User32.VK)))
                {
                    // 检查键是否被按下
                    if (IsKeyPressed(key)) // 强制转换 VK 枚举为 int
                    {
                        Logger.LogWarning($"解除{key}的按下状态.");
                        Simulation.SendInput.Keyboard.KeyUp(key);
                    }
                }

                Logger.LogWarning(IsSuspendedByNetwork ? "网络检测失败触发暂停，等待解除" : "快捷键触发暂停，等待解除");
                foreach (var item in RunnerContext.Instance.SuspendableDictionary)
                {
                    item.Value.Suspend();
                }

                first = false;
            }

            if (IsSuspendedByNetwork)
            {
                CheckNetworkStatusAsync().Wait(1000, CancellationToken.None);
            }

            Thread.Sleep(1000);
        }

        //从暂停中解除
        if (isSuspend)
        {
            Logger.LogWarning("暂停已经解除");
            RunnerContext.Instance.ResumeAutoPick();
            foreach (var item in RunnerContext.Instance.SuspendableDictionary)
            {
                item.Value.Resume();
            }
        }
    }

    private static void CheckAndActivateGameWindow()
    {
        if (IsSuspendedByNetwork)
        {
            Logger.LogInformation("网络恢复中，暂停尝试恢复窗口");
            return;
        }
        
        if (!TaskContext.Instance().Config.OtherConfig.RestoreFocusOnLostEnabled)
        {
            if (!SystemControl.IsGenshinImpactActiveByProcess())
            {
                var name = SystemControl.GetActiveByProcess();
                Logger.LogWarning($"当前获取焦点的窗口为: {name}，不是原神，暂停");
                throw new RetryException("当前获取焦点的窗口不是原神");
            }
        }

        var count = 0;
        //未激活则尝试恢复窗口
        while (!SystemControl.IsGenshinImpactActiveByProcess())
        {
            if (count >= 10 && count % 10 == 0)
            {
                Logger.LogInformation("多次尝试未恢复，尝试最小化后激活窗口！");
                SystemControl.MinimizeAndActivateWindow(TaskContext.Instance().GameHandle);
            }
            else
            {
                Logger.LogInformation("当前获取焦点的窗口不是原神，尝试恢复窗口");
                SystemControl.FocusWindow(TaskContext.Instance().GameHandle);
            }

            count++;
            Thread.Sleep(1000);
        }
    }

    public static void Sleep(int millisecondsTimeout, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            throw new NormalEndException("取消自动任务");
        }

        if (millisecondsTimeout <= 0)
        {
            return;
        }

        NewRetry.Do(() =>
        {
            if (ct.IsCancellationRequested)
            {
                throw new NormalEndException("取消自动任务");
            }
            TrySuspend();
            CheckAndActivateGameWindow();
        }, TimeSpan.FromSeconds(1), 100);
        Thread.Sleep(millisecondsTimeout);
        if (ct.IsCancellationRequested)
        {
            throw new NormalEndException("取消自动任务");
        }
    }

    public static async Task Delay(int millisecondsTimeout, CancellationToken ct)
    {
        if (ct is { IsCancellationRequested: true })
        {
            throw new NormalEndException("取消自动任务");
        }

        if (millisecondsTimeout <= 0)
        {
            return;
        }

        NewRetry.Do(() =>
        {
            if (ct is { IsCancellationRequested: true })
            {
                throw new NormalEndException("取消自动任务");
            }
            TrySuspend();
            CheckAndActivateGameWindow();
        }, TimeSpan.FromSeconds(1), 100);
        await Task.Delay(millisecondsTimeout, ct);
        if (ct is { IsCancellationRequested: true })
        {
            throw new NormalEndException("取消自动任务");
        }
    }

    public static Mat CaptureGameImage(IGameCapture? gameCapture)
    {
        var image = gameCapture?.Capture();
        if (image == null)
        {
            Logger.LogWarning("截图失败!");
            // 重试3次
            for (var i = 0; i < 3; i++)
            {
                image = gameCapture?.Capture();
                if (image != null)
                {
                    return image;
                }

                Sleep(30);
            }

            throw new Exception("尝试多次后,截图失败!");
        }
        else
        {
            return image;
        }
    }

    public static Mat? CaptureGameImageNoRetry(IGameCapture? gameCapture)
    {
        return gameCapture?.Capture();
    }

    /// <summary>
    /// 自动判断当前运行上下文中截图方式，并选择合适的截图方式返回
    /// </summary>
    /// <returns></returns>
    public static ImageRegion CaptureToRectArea(bool forceNew = false)
    {
        var image = CaptureGameImage(TaskTriggerDispatcher.GlobalGameCapture);
        var content = new CaptureContent(image, 0, 0);
        return content.CaptureRectArea;
    }
}