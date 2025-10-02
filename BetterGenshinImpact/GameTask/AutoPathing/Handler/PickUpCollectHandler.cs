using System;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using Microsoft.Extensions.Logging;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using BetterGenshinImpact.GameTask.AutoFight.Model;
using System.Diagnostics;
using BetterGenshinImpact.GameTask.AutoFight.Script;
using System.Linq;


namespace BetterGenshinImpact.GameTask.AutoPathing.Handler;

    /// <summary>
/// 使用万叶或琴团通过战技吸取拾取物品，优先万叶，如果没有万叶则使用琴团
/// </summary>
public class PickUpCollectHandler : IActionHandler
{
    
    private readonly string[] _pickUoActions =
    [
        "枫原万叶 attack(0.08),keydown(E),wait(0.7),keyup(E),attack(0.2),wait(0.5)",
        "琴 attack(0.08),keydown(E),wait(0.4),moveby(1000,0),wait(0.2),moveby(1000,0),wait(0.2),moveby(1000,0),wait(0.2),moveby(1000,-3500),wait(1.8),keyup(E),wait(0.3),click(middle)",
    ];
    
    public async Task RunAsync(CancellationToken ct, WaypointForTrack? waypointForTrack = null, object? config = null)
    {
        Logger.LogInformation("执行 {Nhd} 动作", "聚集材料");

        var combatScenes = await RunnerContext.Instance.GetCombatScenes(ct);
        if (combatScenes == null)
        {
            Logger.LogError("队伍识别未初始化成功！");
            return;
        }
        
        Avatar? picker = null;
        if (waypointForTrack != null
            && !string.IsNullOrEmpty(waypointForTrack.ActionParams))
        {
            picker = combatScenes.SelectAvatar(waypointForTrack.ActionParams);
        }

        if (picker is not null)
        {
            picker.TrySwitch();
            await picker.WaitSkillCd(ct);//等待技能冷却
        }
        else
        {
            Logger.LogWarning("未找到ActionParams中角色{t},使用默认配置",waypointForTrack?.ActionParams);
        }
        
        PickUpMaterial(combatScenes,picker?.Name);
    }
    
   private void PickUpMaterial(CombatScenes combatScenes, string? pickerName = null)
    {
        try
        {
            bool foundAvatar = false;
            string[] actionsToUse = _pickUoActions;

            // 如果pickerName不为null，则使用pickerName对应的值
            if (pickerName != null)
            {
                actionsToUse = _pickUoActions.Where(action => 
                    action.StartsWith(pickerName + " ", StringComparison.OrdinalIgnoreCase)).ToArray();
                
                if (actionsToUse.Length == 0)
                {
                    Logger.LogError($"未找到对应的角色: {pickerName}");
                    return;
                }
            }

            foreach (var miningActionStr in actionsToUse)
            {
                var pickUpAction = CombatScriptParser.ParseContext(miningActionStr);
                foreach (var command in pickUpAction.CombatCommands)
                {
                    var avatar = combatScenes.SelectAvatar(command.Name);
                    if (avatar != null)
                    {
                        command.Execute(combatScenes);
                        foundAvatar = true;
                    }
                }
                if (foundAvatar)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // 处理异常
            Console.WriteLine($"PickUpCollectHandler 异常: {ex.Message}");
        }
    }

    
}
