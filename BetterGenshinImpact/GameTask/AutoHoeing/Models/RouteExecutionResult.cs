namespace BetterGenshinImpact.GameTask.AutoHoeing.Models;

/// <summary>
/// 单条路线执行结果
/// </summary>
public class RouteExecutionResult
{
    public double ActualDuration { get; set; }
    public bool ShouldSwitchFurina { get; set; }
    public bool Success { get; set; }

    /// <summary>
    /// 路线是否完整执行到最后一个节点（用于判断是否记录CD和运行时长）
    /// </summary>
    public bool FullyCompleted { get; set; }
}
