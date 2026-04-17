using System;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common.Map.Maps;
using BetterGenshinImpact.GameTask.Common.Map.Maps.Base;
using BetterGenshinImpact.GameTask.Model.Area;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using OpenCvSharp;
using System.Threading;

namespace BetterGenshinImpact.GameTask.AutoPathing;

public class NavigationInstance
{
    private float _prevX = -1;
    private float _prevY = -1;
    private DateTime _captureTime = DateTime.MinValue;
    private int _consecutiveFailCount = 0;
    private const int GlobalMatchFallbackThreshold = 2; // 连续失败2次后触发全局匹配
    
    public void Reset()
    {
        (_prevX, _prevY) = (-1, -1);
        _consecutiveFailCount = 0;
    }
    
    public void SetPrevPosition(float x, float y)
    {
        (_prevX, _prevY) = (x, y);
        // 不重置 _consecutiveFailCount，因为 SetPrevPosition 是外部设置参考点，不代表匹配成功
    }

    private static readonly object GetPositionLock = new object(); 
    public Point2f GetPosition(ImageRegion imageRegion, string mapName, string mapMatchMethod)
    {
        if (Monitor.TryEnter(GetPositionLock, 100))
        {
            try
            {
                var colorMat = new Mat(imageRegion.SrcMat, MapAssets.Instance.MimiMapRect);
                var captureTime = DateTime.UtcNow;
                var p = MapManager.GetMap(mapName, mapMatchMethod).GetMiniMapPosition(colorMat, _prevX, _prevY);
                
                // 局部匹配失败且有prevPos时，尝试全局匹配回退
                if (p == default && _prevX > 0 && _prevY > 0)
                {
                    _consecutiveFailCount++;
                    if (_consecutiveFailCount >= GlobalMatchFallbackThreshold)
                    {
                        var savedPrevX = _prevX;
                        var savedPrevY = _prevY;
                        (_prevX, _prevY) = (-1, -1); // 临时重置触发全局匹配
                        p = MapManager.GetMap(mapName, mapMatchMethod).GetMiniMapPosition(colorMat, _prevX, _prevY);
                        if (p == default)
                        {
                            (_prevX, _prevY) = (savedPrevX, savedPrevY);
                        }
                        else
                        {
                            _consecutiveFailCount = 0;
                        }
                    }
                    else
                    {
                        // 局部匹配失败，等待累积到阈值后触发全局匹配
                    }
                }
                
                if (p != default && captureTime > _captureTime)
                {
                    (_prevX, _prevY) = (p.X, p.Y);
                    _captureTime = captureTime;
                    _consecutiveFailCount = 0;
                }

                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<object>(typeof(Navigation),
                    "SendCurrentPosition", new object(), p));
                return p;
            }
            catch (Exception ex)
            {
                // 获取位置失败，返回上次的位置（仅当上次位置有效时）
                if (_prevX > 0 && _prevY > 0)
                {
                    return new Point2f(_prevX, _prevY);
                }
                return default;
            }
            finally
            {
                Monitor.Exit(GetPositionLock);
            }
        }
        
        // 锁获取超时，返回上次的位置（仅当上次位置有效时）
        if (_prevX > 0 && _prevY > 0)
        {
            return new Point2f(_prevX, _prevY);
        }
        return default;
        
    }

    /// <summary>
    /// 稳定获取当前位置坐标，优先使用全地图匹配，适用于不需要高效率但需要高稳定性的场景
    /// </summary>
    /// <param name="imageRegion">图像区域</param>
    /// <param name="mapName">地图名字</param>
    /// <param name="mapMatchMethod">地图匹配方式</param>
    /// <returns>当前位置坐标</returns>
    public Point2f GetPositionStable(ImageRegion imageRegion, string mapName, string mapMatchMethod)
    {
        var colorMat = new Mat(imageRegion.SrcMat, MapAssets.Instance.MimiMapRect);
        var captureTime = DateTime.UtcNow;

        // 先尝试使用局部匹配
        var sceneMap = MapManager.GetMap(mapName, mapMatchMethod);
        //提高局部匹配的阈值，以解决在沙漠录制点位时，移动过远不会触发全局匹配的情况
        var p = (sceneMap as SceneBaseMapByTemplateMatch)?.GetMiniMapPosition(colorMat, _prevX, _prevY, 0)
                ?? sceneMap.GetMiniMapPosition(colorMat, _prevX, _prevY);

        // 如果局部匹配失败或者点位跳跃过大，再尝试全地图匹配
        if (p == default || (_prevX > 0 && _prevY >0 && p.DistanceTo(new Point2f(_prevX,_prevY)) > 150))
        {
            Reset();
            p = MapManager.GetMap(mapName, mapMatchMethod).GetMiniMapPosition(colorMat, _prevX, _prevY);
        }
        if (p != default && captureTime > _captureTime)
        {
            (_prevX, _prevY) = (p.X, p.Y);
            _captureTime = captureTime;
        }

        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<object>(typeof(Navigation),
            "SendCurrentPosition", new object(), p));
        return p;
    }

    public Point2f GetPositionStableByCache(ImageRegion imageRegion, string mapName, string mapMatchingMethod, int cacheTimeMs = 900)
    {
        var captureTime = DateTime.UtcNow;
        if (captureTime - _captureTime < TimeSpan.FromMilliseconds(cacheTimeMs) && _prevX > 0 && _prevY > 0)
        {
            return new Point2f(_prevX, _prevY);
        }

        return GetPositionStable(imageRegion, mapName, mapMatchingMethod);
    }
}