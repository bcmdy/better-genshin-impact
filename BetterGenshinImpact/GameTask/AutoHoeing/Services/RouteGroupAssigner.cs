using BetterGenshinImpact.GameTask.AutoHoeing.Models;
using BetterGenshinImpact.GameTask.AutoHoeing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterGenshinImpact.GameTask.AutoHoeing.Services;

/// <summary>
/// 路线分组分配器：将已选路线按标签分配到10个路径组
/// </summary>
public static class RouteGroupAssigner
{
    private static readonly ILogger Logger = App.GetLogger<AutoHoeingTask>();

    public static void AssignGroups(List<RouteInfo> routes, List<List<string>> groupTags)
    {
        foreach (var route in routes)
        {
            if (!route.Selected) continue;

            route.Group = 0;

            // 不含路径组一任何标签 → 分到组1
            if (!groupTags[0].Any(tag => route.Tags.Contains(tag)))
            {
                route.Group = 1;
            }
            else
            {
                // 按组2-10顺序匹配
                for (int i = 1; i <= 9; i++)
                {
                    if (i < groupTags.Count && groupTags[i].Any(tag => route.Tags.Contains(tag)))
                    {
                        route.Group = i + 1;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 输出分组汇总信息（调试模式）
    /// </summary>
    public static void PrintGroupSummary(List<RouteInfo> routes)
    {
        var groupNames = new[]
        {
            "路径组一", "路径组二", "路径组三", "路径组四", "路径组五",
            "路径组六", "路径组七", "路径组八", "路径组九", "路径组十"
        };

        for (int g = 1; g <= 10; g++)
        {
            var groupRoutes = routes.Where(p => p.Group == g && p.Selected).ToList();
            if (groupRoutes.Count == 0) continue;

            var elites = groupRoutes.Sum(p => p.EliteMonsterCount);
            var monsters = groupRoutes.Sum(p => p.NormalMonsterCount);
            var gain = groupRoutes.Sum(p => p.EliteMoraGain + p.NormalMoraGain);
            var time = groupRoutes.Sum(p => p.AdjustedTime);

            var ts = TimeSpan.FromSeconds(time);
            Logger.LogInformation("{Group}: {Count}条路线, 精英{E}, 小怪{M}, 收益{G}摩拉, 预计{H}时{Min}分{S}秒",
                groupNames[g - 1], groupRoutes.Count, elites, monsters,
                gain.ToString("F0"), (int)ts.TotalHours, ts.Minutes, ts.Seconds);
        }
    }
}
