using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace BetterGenshinImpact.GameTask.AutoHoeing;

/// <summary>
/// 锄地一条龙配置类
/// </summary>
[Serializable]
public partial class AutoHoeingConfig : ObservableObject
{
    // ========== 第一部分：执行配置 ==========

    /// <summary>
    /// 执行模式：运行锄地路线、调试路线分配、强制刷新所有运行记录、启用仅指定怪物模式
    /// </summary>
    [ObservableProperty]
    private string _operationMode = "运行锄地路线";

    /// <summary>
    /// 选择执行第几个路径组（1-10）
    /// </summary>
    [ObservableProperty]
    private int _groupIndex = 1;

    /// <summary>
    /// 本路径组使用配队名称
    /// </summary>
    [ObservableProperty]
    private string _partyName = "";

    /// <summary>
    /// 组内路线排序模式：原文件顺序、效率降序、高收益优先
    /// </summary>
    [ObservableProperty]
    private string _sortMode = "高收益优先";

    /// <summary>
    /// 拾取模式
    /// </summary>
    [ObservableProperty]
    private string _pickupMode = "模板匹配拾取狗粮和怪物材料";

    /// <summary>
    /// 仅使用路线相关怪物材料进行识别
    /// </summary>
    [ObservableProperty]
    private bool _useRouteRelatedMaterialsOnly;

    /// <summary>
    /// 禁用识别到物品后的二次校验
    /// </summary>
    [ObservableProperty]
    private bool _disableSecondaryValidation;

    /// <summary>
    /// 泥头车角色编号（中文逗号分隔，如"1，3"）
    /// </summary>
    [ObservableProperty]
    private string _dumperCharacters = "";

    /// <summary>
    /// 使用料理名称（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _cookingNames = "";

    /// <summary>
    /// 不运行时段
    /// </summary>
    [ObservableProperty]
    private string _noRunPeriod = "";

    /// <summary>
    /// 识别间隔(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _findFInterval = 100;

    /// <summary>
    /// 拾取后延时(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _pickupDelay = 50;

    /// <summary>
    /// 滚动后延时(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _rollingDelay = 32;

    /// <summary>
    /// 单次滚动周期(毫秒)
    /// </summary>
    [ObservableProperty]
    private int _scrollCycle = 1000;

    /// <summary>
    /// 运行路线时输出怪物数量日志
    /// </summary>
    [ObservableProperty]
    private bool _logMonsterCount;

    /// <summary>
    /// 禁用异步操作
    /// </summary>
    [ObservableProperty]
    private bool _disableAsync;

    /// <summary>
    /// 路线结尾时进行坐标检查
    /// </summary>
    [ObservableProperty]
    private bool _enableCoordinateCheck;

    /// <summary>
    /// 跳过校验阶段
    /// </summary>
    [ObservableProperty]
    private bool _skipValidation;

    // ========== 第二部分：路线选择与分组配置 ==========

    /// <summary>
    /// 账户名称
    /// </summary>
    [ObservableProperty]
    private string _accountName = "默认账户";

    /// <summary>
    /// 路径组一要排除的标签
    /// </summary>
    [ObservableProperty]
    private string _tagsForGroup1 = "蕈兽，传奇，狭窄地形";

    [ObservableProperty] private string _tagsForGroup2 = "";
    [ObservableProperty] private string _tagsForGroup3 = "";
    [ObservableProperty] private string _tagsForGroup4 = "";
    [ObservableProperty] private string _tagsForGroup5 = "";
    [ObservableProperty] private string _tagsForGroup6 = "";
    [ObservableProperty] private string _tagsForGroup7 = "";
    [ObservableProperty] private string _tagsForGroup8 = "";
    [ObservableProperty] private string _tagsForGroup9 = "";
    [ObservableProperty] private string _tagsForGroup10 = "";

    /// <summary>
    /// 禁用根据运行记录优化路线选择
    /// </summary>
    [ObservableProperty]
    private bool _disableSelfOptimization;

    /// <summary>
    /// 摩拉/耗时权衡因数
    /// </summary>
    [ObservableProperty]
    private double _efficiencyIndex = 0.25;

    /// <summary>
    /// 好奇系数（0-1）
    /// </summary>
    [ObservableProperty]
    private double _curiosityFactor;

    /// <summary>
    /// 小怪/精英忽略比例
    /// </summary>
    [ObservableProperty]
    private int _ignoreRate = 100;

    /// <summary>
    /// 目标精英数量
    /// </summary>
    [ObservableProperty]
    private int _targetEliteNum = 400;

    /// <summary>
    /// 目标小怪数量
    /// </summary>
    [ObservableProperty]
    private int _targetMonsterNum = 2000;

    /// <summary>
    /// 优先关键词（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _priorityTags = "";

    /// <summary>
    /// 排除关键词（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _excludeTags = "";

    // ========== 第三部分：仅指定怪物模式 ==========

    /// <summary>
    /// 目标怪物（中文逗号分隔）
    /// </summary>
    [ObservableProperty]
    private string _targetMonsters = "";
}
