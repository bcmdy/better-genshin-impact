using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Model;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using Microsoft.Extensions.Logging;
using static BetterGenshinImpact.GameTask.Common.TaskControl;

namespace BetterGenshinImpact.GameTask.AutoPathing.Handler;

/// <summary>
/// 元素采集
/// </summary>
public class ElementalCollectHandler(ElementalType elementalType) : IActionHandler
{
    public async Task RunAsync(CancellationToken ct, WaypointForTrack? waypointForTrack = null, object? config = null)
    {
        var combatScenes = await RunnerContext.Instance.GetCombatScenes(ct);
        if (combatScenes == null)
        {
            Logger.LogError("队伍识别未初始化成功！");
            return;
        }

        // 筛选出对应元素的角色列表
        var elementalCollectAvatars = ElementalCollectAvatarConfigs.Lists.Where(x => x.ElementalType == elementalType).ToList();
        // 循环遍历角色列表
        foreach (var combatScenesAvatar in combatScenes.GetAvatars())
        {
            // 判断是否为对应元素的角色
            var elementalCollectAvatar = elementalCollectAvatars.FirstOrDefault(x => x.Name == combatScenesAvatar.Name);
            if (elementalCollectAvatar == null)
            {
                continue;
            }

            // 切人
            if (combatScenesAvatar.TrySwitch())
            {
                if (elementalCollectAvatar.NormalAttack)
                {
                    combatScenesAvatar.Attack(100);
                }
                else if (elementalCollectAvatar.ElementalSkill)
                {

                    await combatScenesAvatar.WaitSkillCd(ct);
                    combatScenesAvatar.UseSkill();
                }
            }
            else
            {
                Logger.LogError("切人失败,无法进行{Element}元素采集", elementalType.ToChinese());
            }

            break;
        }
    }
}

public class ElementalCollectAvatar(string name, ElementalType elementalType, bool normalAttack, bool elementalSkill)
{
    public string Name { get; set; } = name;
    public ElementalType ElementalType { get; set; } = elementalType;
    public bool NormalAttack { get; set; } = normalAttack;

    public bool ElementalSkill { get; set; } = elementalSkill;

    // public CombatAvatar Info => DefaultAutoFightConfig.CombatAvatarMap[Name];

    public DateTime LastUseSkillTime { get; set; } = DateTime.MinValue;
}

public class ElementalCollectAvatarConfigs
{
    public static List<ElementalCollectAvatar> Lists { get; set; } =
    [
        // 全
        new ElementalCollectAvatar("天理", ElementalType.Omni, true, true),
        // 水
        new ElementalCollectAvatar("芭芭拉", ElementalType.Hydro, true, true),
        new ElementalCollectAvatar("莫娜", ElementalType.Hydro, true, false),
        new ElementalCollectAvatar("珊瑚宫心海", ElementalType.Hydro, true, true),
        new ElementalCollectAvatar("玛拉妮", ElementalType.Hydro, true, false),
        new ElementalCollectAvatar("那维莱特", ElementalType.Hydro, true, true),
        new ElementalCollectAvatar("芙宁娜", ElementalType.Hydro, true, false),
        new ElementalCollectAvatar("妮露", ElementalType.Hydro, false, true),
        new ElementalCollectAvatar("坎蒂斯", ElementalType.Hydro, false, true),
        new ElementalCollectAvatar("行秋", ElementalType.Hydro, false, true),
        new ElementalCollectAvatar("神里绫人", ElementalType.Hydro, false, true),
        new ElementalCollectAvatar("塔利雅", ElementalType.Hydro, false, true),
        new ElementalCollectAvatar("希格雯", ElementalType.Hydro, false, true),
        new ElementalCollectAvatar("夜兰", ElementalType.Hydro, false, false),
        new ElementalCollectAvatar("达达利亚", ElementalType.Hydro, false, false),
        // 雷
        new ElementalCollectAvatar("丽莎", ElementalType.Electro, true, true),
        new ElementalCollectAvatar("八重神子", ElementalType.Electro, true, false),
        new ElementalCollectAvatar("瓦雷莎", ElementalType.Electro, true, false),
        new ElementalCollectAvatar("雷电将军", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("久岐忍", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("北斗", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("菲谢尔", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("雷泽", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("伊涅芙", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("伊安珊", ElementalType.Electro, false, false),
        new ElementalCollectAvatar("欧洛伦", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("克洛琳德", ElementalType.Electro, false, false),
        new ElementalCollectAvatar("赛索斯", ElementalType.Electro, false, false),
        new ElementalCollectAvatar("赛诺", ElementalType.Electro, false, false),
        new ElementalCollectAvatar("多莉", ElementalType.Electro, false, true),
        new ElementalCollectAvatar("九条裟罗", ElementalType.Electro, false, false),
        new ElementalCollectAvatar("刻晴", ElementalType.Electro, false, false),
        // 风
        new ElementalCollectAvatar("砂糖", ElementalType.Anemo, true, true),
        new ElementalCollectAvatar("鹿野院平藏", ElementalType.Anemo, true, true),
        new ElementalCollectAvatar("流浪者", ElementalType.Anemo, true, false),
        new ElementalCollectAvatar("闲云", ElementalType.Anemo, true, false),
        new ElementalCollectAvatar("蓝砚", ElementalType.Anemo, true, false),
        new ElementalCollectAvatar("枫原万叶", ElementalType.Anemo, false, true),
        new ElementalCollectAvatar("珐露珊", ElementalType.Anemo, false, true),
        new ElementalCollectAvatar("琳妮特", ElementalType.Anemo, false, true),
        new ElementalCollectAvatar("温迪", ElementalType.Anemo, false, true),
        new ElementalCollectAvatar("琴", ElementalType.Anemo, false, true),
        new ElementalCollectAvatar("早柚", ElementalType.Anemo, false, true),
        new ElementalCollectAvatar("伊法", ElementalType.Anemo, true, false),
        new ElementalCollectAvatar("梦见月瑞希", ElementalType.Anemo, true, false),
        new ElementalCollectAvatar("恰斯卡", ElementalType.Anemo, false, false),
        new ElementalCollectAvatar("魈", ElementalType.Anemo, false, false),
        // 火
        new ElementalCollectAvatar("烟绯", ElementalType.Pyro, true, true),
        new ElementalCollectAvatar("迪卢克", ElementalType.Pyro, false,true),
        new ElementalCollectAvatar("可莉", ElementalType.Pyro, true, true),
        new ElementalCollectAvatar("班尼特", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("香菱", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("托马", ElementalType.Pyro,false, true),
        new ElementalCollectAvatar("胡桃", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("迪希雅", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("夏沃蕾", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("辛焱", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("林尼", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("宵宫", ElementalType.Pyro, false, true),
        new ElementalCollectAvatar("玛薇卡", ElementalType.Pyro, false, false),
        new ElementalCollectAvatar("阿蕾奇诺", ElementalType.Pyro, false, false),
        new ElementalCollectAvatar("嘉明", ElementalType.Pyro, false, false),
        new ElementalCollectAvatar("安柏", ElementalType.Pyro, false, false),
        // 草
        new ElementalCollectAvatar("基尼奇", ElementalType.Dendro, false, false),
        new ElementalCollectAvatar("艾梅莉埃", ElementalType.Dendro, false, true),
        new ElementalCollectAvatar("绮良良", ElementalType.Dendro, false, true),
        new ElementalCollectAvatar("白术", ElementalType.Dendro, true, true),
        new ElementalCollectAvatar("卡维", ElementalType.Dendro, false, true),
        new ElementalCollectAvatar("艾尔海森", ElementalType.Dendro, false, false),
        new ElementalCollectAvatar("瑶瑶", ElementalType.Dendro, false, false),
        new ElementalCollectAvatar("纳西妲", ElementalType.Dendro, true, true),
        new ElementalCollectAvatar("提纳里", ElementalType.Dendro, false, true),
        new ElementalCollectAvatar("柯莱", ElementalType.Dendro, false, true),
        // 岩
        new ElementalCollectAvatar("希诺宁", ElementalType.Geo, false, false),
        new ElementalCollectAvatar("卡齐娜", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("千织", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("钟离", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("娜维娅", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("云堇", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("荒泷一斗", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("五郎", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("阿贝多", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("诺艾尔", ElementalType.Geo, false, true),
        new ElementalCollectAvatar("凝光", ElementalType.Geo, true, true),
        // 冰
        new ElementalCollectAvatar("茜特菈莉", ElementalType.Cryo, true, true),
        new ElementalCollectAvatar("丝柯克", ElementalType.Cryo, false, false),
        new ElementalCollectAvatar("爱可菲", ElementalType.Cryo, false, true),
        new ElementalCollectAvatar("夏洛蒂", ElementalType.Cryo, true, true),
        new ElementalCollectAvatar("莱欧斯利", ElementalType.Cryo, true, false),
        new ElementalCollectAvatar("菲米尼", ElementalType.Cryo, false, true),
        new ElementalCollectAvatar("米卡", ElementalType.Cryo, false, true),
        new ElementalCollectAvatar("莱依拉", ElementalType.Cryo, false, true),
        new ElementalCollectAvatar("申鹤", ElementalType.Cryo, false, false),
        new ElementalCollectAvatar("埃洛伊", ElementalType.Cryo, false, false),
        new ElementalCollectAvatar("神里绫华", ElementalType.Cryo, false, true),
        new ElementalCollectAvatar("优菈", ElementalType.Cryo, false, false),
        new ElementalCollectAvatar("罗莎莉亚", ElementalType.Cryo, false, true),
        new ElementalCollectAvatar("甘雨", ElementalType.Cryo, false, false),
        new ElementalCollectAvatar("迪奥娜", ElementalType.Cryo, false, false),
        new ElementalCollectAvatar("七七", ElementalType.Cryo, false, true),
        new ElementalCollectAvatar("重云", ElementalType.Cryo, false, false),
        new ElementalCollectAvatar("凯亚", ElementalType.Cryo, false, true),
    ];

    public static ElementalCollectAvatar? Get(string name, ElementalType type) => Lists.FirstOrDefault(x => x.Name == name && x.ElementalType == type);

    public static List<string> GetAvatarNameList(ElementalType type) => Lists.Where(x => x.ElementalType == type).Select(x => x.Name).ToList();
}
