using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.Common.Map;
using BetterGenshinImpact.GameTask.Model.Area;
using System;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.GameTask.Common;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using Logger = Serilog.Core.Logger;

namespace BetterGenshinImpact.GameTask.AutoPathing;

public class CameraRotateTask(CancellationToken ct)
{
    private readonly double _dpi = TaskContext.Instance().DpiScale;

    /// <summary>
    /// 向目标角度旋转
    /// </summary>
    /// <param name="targetOrientation"></param>
    /// <param name="imageRegion"></param>
    /// <returns></returns>
    public float RotateToApproach(float targetOrientation, ImageRegion imageRegion)
    {
        var cao = CameraOrientation.Compute(imageRegion.SrcMat);
        var diff = (cao - targetOrientation + 180) % 360 - 180;
        diff += diff < -180 ? 360 : 0;
        if (diff == 0)
        {
            return diff;
        }

        // 平滑的旋转视角
        // todo dpi 和分辨率都会影响转动速度
        
        double controlRatio = 1;
        if (Math.Abs(diff) > 90)
        {
            controlRatio = 4;
        }
        else if (Math.Abs(diff) > 30)
        {
            controlRatio = 3;
        }
        else if (Math.Abs(diff) > 5)
        {
            controlRatio = 2;
        }

        // TaskControl.Logger.LogWarning("转动视角，当前角度-{cao}，目标角度-{targetOrientation}，误差-{diff}，控制比例-{controlRatio}",cao,targetOrientation,diff,controlRatio);
        Simulation.SendInput.Mouse.MoveMouseBy((int)Math.Round(-controlRatio * diff * _dpi), 0);
        return diff;
    }

    private static volatile object _zLock = new object(); 
    /// <summary>
    /// 转动视角到目标角度
    /// </summary>
    /// <param name="targetOrientation">目标角度</param>
    /// <param name="maxDiff">最大误差</param>
    /// <param name="maxTryTimes">最大尝试次数（超时时间）</param>
    /// <returns></returns>
    public async Task<bool> WaitUntilRotatedTo(int targetOrientation, int maxDiff, int maxTryTimes = 50)
    {
        // TaskControl.Logger.LogWarning("开始转动视角到目标角度-{targetOrientation}，最大误差-{maxDiff}，最大尝试次数-{maxTryTimes}",
            // targetOrientation, maxDiff, maxTryTimes);
        bool isSuccessful = false;
        int count = 0;
        while (!ct.IsCancellationRequested)
        {
            var screen = CaptureToRectArea();
            float aa = 0;
            if (Monitor.TryEnter(_zLock))
            {
                try
                {
                    aa = Math.Abs(RotateToApproach(targetOrientation, screen));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    Monitor.Exit(_zLock);
                }
            }
            
            if (aa < maxDiff + count / 2)
            {
                isSuccessful = true;
                break;
            }

            if (count > maxTryTimes)
            {
                //aa为正bb=1，aa为负数bb=-1
                var bb = aa > 0 ? 1 : -1;
                Simulation.SendInput.Mouse.MoveMouseBy((int)Math.Round(bb * _dpi * 1000), 0);
                //按鼠标中键
                Simulation.SendInput.Mouse.MiddleButtonClick();
                TaskControl.Logger.LogWarning("视角转动到目标角度超时，停止转动- {t}", bb);
                break;
            }

            // TaskControl.Logger.LogWarning("转动视角到目标角度中，当前角度误差-{aa}，尝试次数-{count}", aa, count);
            await Delay(50, ct);
            count++;
        }

        return isSuccessful;  
    }
}
