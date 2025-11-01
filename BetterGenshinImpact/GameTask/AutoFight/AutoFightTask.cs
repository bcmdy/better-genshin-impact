using BetterGenshinImpact.Core.Recognition.ONNX;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.Core.Simulator.Extensions;
using BetterGenshinImpact.GameTask.AutoFight.Model;
using BetterGenshinImpact.GameTask.AutoFight.Script;
using BetterGenshinImpact.GameTask.Model.Area;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Recognition;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using BetterGenshinImpact.GameTask.Common.Job;
using OpenCvSharp;
using BetterGenshinImpact.Helpers;
using Vanara;
using Microsoft.Extensions.DependencyInjection;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.AutoFight.Assets;
using BetterGenshinImpact.View.Drawable;
using BetterGenshinImpact.Core.Recognition.OpenCv;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using Vanara.PInvoke;
using BetterGenshinImpact.GameTask.AutoPathing.Handler;
using BetterGenshinImpact.GameTask.AutoPick.Assets;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using System.Text.RegularExpressions;

namespace BetterGenshinImpact.GameTask.AutoFight;

public class AutoFightTask : ISoloTask
{
    public string Name => "自动战斗";

    private readonly AutoFightParam _taskParam;

    private readonly CombatScriptBag _combatScriptBag;
    
    private readonly CombatScriptBag _combatScriptBagSecond;

    private CancellationToken _ct;

    private readonly BgiYoloPredictor _predictor;

    private DateTime _lastFightFlagTime = DateTime.UtcNow; // 战斗标志最近一次出现的时间

    private readonly double _dpi = TaskContext.Instance().DpiScale;

    private static OtherConfig Config { get; set; } = TaskContext.Instance().Config.OtherConfig;
    
    private static AutoFightConfig FightConfig { get; set; } = TaskContext.Instance().Config.AutoFightConfig;
    
    public static bool FightStatusFlag = false;
    
    public static int SwitchTryCount = 0;
    
    public static volatile  bool FightEndFlag = false;
    
    private static volatile bool _isExperiencePickup = false;

    public static bool IsTpForRecover {get; set;} = false;

    // 战斗点位
    public static WaypointForTrack? FightWaypoint  {get; set;} = null;
    
    private static readonly object PickLock = new object(); 
    
    private class TaskFightFinishDetectConfig
    {
        public int DelayTime = 1500;
        public int DetectDelayTime = 450;
        public Dictionary<string, int> DelayTimes = new();
        public double CheckTime = 5;
        public List<string> CheckNames = new();
        public bool FastCheckEnabled;
        public bool RotateFindEnemyEnabled = false;

        public TaskFightFinishDetectConfig(AutoFightParam.FightFinishDetectConfig finishDetectConfig)
        {
            FastCheckEnabled = finishDetectConfig.FastCheckEnabled;
            ParseCheckTimeString(finishDetectConfig.FastCheckParams, out CheckTime, CheckNames);
            ParseFastCheckEndDelayString(finishDetectConfig.CheckEndDelay, out DelayTime, DelayTimes);
            BattleEndProgressBarColor =
                ParseStringToTuple(finishDetectConfig.BattleEndProgressBarColor, (95, 235, 255));
            BattleEndProgressBarColorTolerance =
                ParseSingleOrCommaSeparated(finishDetectConfig.BattleEndProgressBarColorTolerance, (6, 6, 6));
            DetectDelayTime =
                (int)((double.TryParse(finishDetectConfig.BeforeDetectDelay, out var result) ? result : 0.45) * 1000);
            RotateFindEnemyEnabled = finishDetectConfig.RotateFindEnemyEnabled;
        }

        public (int, int, int) BattleEndProgressBarColor { get; }
        public (int, int, int) BattleEndProgressBarColorTolerance { get; }

        public static void ParseCheckTimeString(
            string input,
            out double checkTime,
            List<string> names)
        {
            checkTime = 5;
            if (string.IsNullOrEmpty(input))
            {
                return; // 直接返回
            }

            var uniqueNames = new HashSet<string>(); // 用于临时去重的集合

            // 按分号分割字符串
            var segments = input.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                var trimmedSegment = segment.Trim();

                // 如果是纯数字部分
                if (double.TryParse(trimmedSegment, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double number))
                {
                    checkTime = number; // 更新 CheckTime
                }
                else if (!uniqueNames.Contains(trimmedSegment)) // 如果是非数字且不重复
                {
                    uniqueNames.Add(trimmedSegment); // 添加到集合
                }
            }

            names.AddRange(uniqueNames); // 将集合转换为列表
        }

        public static void ParseFastCheckEndDelayString(
            string input,
            out int delayTime,
            Dictionary<string, int> nameDelayMap)
        {
            delayTime = 1500;

            if (string.IsNullOrEmpty(input))
            {
                return; // 直接返回
            }

            // 分割字符串，以分号为分隔符
            var segments = input.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                var parts = segment.Split(',');

                // 如果是纯数字部分
                if (parts.Length == 1)
                {
                    if (double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                            out double number))
                    {
                        delayTime = (int)(number * 1000); // 更新 delayTime
                    }
                }
                // 如果是名字,数字格式
                else if (parts.Length == 2)
                {
                    string name = parts[0].Trim();
                    if (double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                            out double value))
                    {
                        nameDelayMap[name] = (int)(value * 1000); // 更新字典，取最后一个值
                    }
                }
                // 其他格式，跳过不处理
            }
        }


        static bool IsSingleNumber(string input, out int result)
        {
            return int.TryParse(input, out result);
        }

        static (int, int, int) ParseSingleOrCommaSeparated(string input, (int, int, int) defaultValue)
        {
            // 如果是单个数字
            if (IsSingleNumber(input, out var singleNumber))
            {
                return (singleNumber, singleNumber, singleNumber);
            }

            return ParseStringToTuple(input, defaultValue);
        }

        static (int, int, int) ParseStringToTuple(string input, (int, int, int) defaultValue)
        {
            // 尝试按逗号分割字符串
            var parts = input.Split(',');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out var num1) &&
                int.TryParse(parts[1], out var num2) &&
                int.TryParse(parts[2], out var num3))
            {
                return (num1, num2, num3);
            }

            // 如果解析失败，返回默认值
            return defaultValue;
        }
    }

    private TaskFightFinishDetectConfig _finishDetectConfig;

    public AutoFightTask(AutoFightParam taskParam)
    {
        _taskParam = taskParam;
        
        var combatScriptBagAll = CombatScriptParser.ReadAndParse(_taskParam.CombatStrategyPath);
        
        _combatScriptBagSecond= combatScriptBagAll;
        
        #region 指定国家战斗脚本解析

        var isAutoSelectTeam = FightConfig.StrategyName.Contains("根据队伍自动选择");
        
        var isSelectAuto = _taskParam.CountryName.Contains("自动");
        
        if (isAutoSelectTeam)
        {
            var countryNamesList = FightConfig.CountryNamesList;
            
            // 对combatScriptBagAll进行重新排序，把含国家脚步名称排后面
            combatScriptBagAll.CombatScripts = combatScriptBagAll.CombatScripts
                .OrderBy(script => countryNamesList.Any(country => script.Name.Contains(country)))
                .ThenBy(script => countryNamesList.FirstOrDefault(country => script.Name.Contains(country)) ?? "")
                .ToList();
            
            var filteredCombatScripts = combatScriptBagAll.CombatScripts
                .Where(script => 
                    _taskParam.CountryName.Length >= 2 
                        ? _taskParam.CountryName.All(country => country != null && script.Name.Contains(country))
                        : _taskParam.CountryName.Any(country => country != null && script.Name.Contains(country)))
                .ToList();
            
            if (filteredCombatScripts.Count == 0)
            {
                //可能在 _taskParam.CountryName.Length >= 2 可能是因为没有符合条件的脚本，尝试Any
                filteredCombatScripts = combatScriptBagAll.CombatScripts
                    .Where(script => _taskParam.CountryName.Any(country => country != "精英" && country != "小怪" && script.Name.Contains(country)))
                    .ToList();
                if (filteredCombatScripts.Count == 0)
                {
                    filteredCombatScripts = combatScriptBagAll.CombatScripts
                        .Where(script => _taskParam.CountryName.Any(country => country != null && script.Name.Contains(country)))
                        .ToList();
                }
            }
            
            // 如果没有找到对应国家的脚本，则使用所有脚本
            if (filteredCombatScripts.Count == 0 && isAutoSelectTeam && isSelectAuto)
            {
                TaskControl.Logger.LogWarning("没有找到符合 {CountryName} 的战斗脚本，将使用所有策略进行匹配", string.Join(", ", _taskParam.CountryName));
                filteredCombatScripts = combatScriptBagAll.CombatScripts;
            }
            
            var combatScriptBagByCountry = new CombatScriptBag(filteredCombatScripts.Count == 0 ?combatScriptBagAll.CombatScripts : filteredCombatScripts);
            
            _combatScriptBag = isSelectAuto || combatScriptBagAll.CombatScripts.Count <= 1 ? combatScriptBagAll : combatScriptBagByCountry;
            
        }
        #endregion

        else
        {
            _combatScriptBag = combatScriptBagAll;
        }

        if (_taskParam.FightFinishDetectEnabled)
        {
            _predictor = App.ServiceProvider.GetRequiredService<BgiOnnxFactory>().CreateYoloPredictor(BgiOnnxModel.BgiWorld);
        }

        _finishDetectConfig = new TaskFightFinishDetectConfig(_taskParam.FinishDetectConfig);
        
    }
    public CombatScenes GetCombatScenesWithRetry()
    {
        const int maxRetries = 5;
        var retryDelayMs = 1000; // 可选：重试间隔，单位毫秒

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var ra = CaptureToRectArea();
            var combatScenes = new CombatScenes().InitializeTeam(ra);
            if (combatScenes.CheckTeamInitialized())
            {
                return combatScenes;
            }
        
            if (attempt < maxRetries)
            {
                Thread.Sleep(retryDelayMs); // 可选：延迟再试
            }
            ra.Dispose();
        }

        if (!Config.CustomAvatarConfigOut.CustomAvatarEnabled) throw new Exception("识别队伍角色失败（已重试 5 次）");
        
        return new CombatScenes().InitializeTeamForced(Config.CustomAvatarConfigOut.CustomAvatarForceUseList);
    }
    // 方法1：判断是否是单个数字

    /*public int delayTime=1500;
    public Dictionary<string, int> delayTimes = new();
    public double checkTime = 5;
    public List<string> checkNames = new();*/
    public async Task Start(CancellationToken ct)
    {
        _ct = ct;

        LogScreenResolution();
        
        var combatScenes = GetCombatScenesWithRetry();
        
        if (_taskParam.AutoCombatEq && PathingConditionConfig.CombatScenesGoBackUp is not null && 
            PathingConditionConfig.CombatScenesGoBackUp.Avatars.Select(avatar => avatar.Name).ToArray()
                .SequenceEqual(combatScenes.Avatars.Select(a => a.Name).ToArray()))
        {
            Logger.LogInformation("自动战斗：继承地图追踪队伍Cd信息...");
            combatScenes = PathingConditionConfig.CombatScenesGoBackUp;
        }
        
        /*var combatScenes = new CombatScenes().InitializeTeam(CaptureToRectArea());
        if (!combatScenes.CheckTeamInitialized())
        {
            throw new Exception("识别队伍角色失败");
        }*/

        // var actionSchedulerByCd = ParseStringToDictionary(_taskParam.ActionSchedulerByCd);
    var combatCommands = _combatScriptBag.FindCombatScript(combatScenes.GetAvatars(),
        FightConfig.StrategyName.Contains("根据队伍自动选择")) ??
                         _combatScriptBagSecond.FindCombatScript(combatScenes.GetAvatars());
        
        var bandList = (_taskParam.AutoCombatEq && !string.IsNullOrWhiteSpace(_taskParam.UseEqList))
            ? _taskParam.UseEqList.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => s.Contains("C", StringComparison.OrdinalIgnoreCase)) // 检查是否包含'C'
                .Select(s => int.TryParse(s.TrimEnd('C'), out var n) ? n : 0) // 去掉'C'并尝试解析数字
                .Where(n => n >= 1 && n <= combatScenes.GetAvatars().Count) // 保证序号在队伍
                .ToList()
            : new List<int>(); // 如果没有指定AutoCombatEq，默认情况下bandList为空

        var bandAvatarsName = _taskParam.AutoCombatEq ? combatScenes.GetAvatars().Where(a => bandList.Contains(a.Index)).Select(a => a.Name).ToList() : new List<string>();
        // Logger.LogError("当前禁用角色：{CombatScriptName}", bandAvatarsName);
        
        // 命令用到的角色名 筛选交集
        var commandAvatarNames = combatCommands.Select(c => c.Name).Distinct()
            .Select(n => combatScenes.SelectAvatar(n)?.Name)
            .WhereNotNull().ToList();
        commandAvatarNames = commandAvatarNames.Except(bandAvatarsName).ToList();
        
        // 过滤不可执行的脚本，Task里并不支持"当前角色"。
        combatCommands = combatCommands 
            .Where(c => commandAvatarNames.Contains(c.Name))
            .ToList();
        
        if (commandAvatarNames.Count <= 0)
        {
            throw new Exception("没有可用战斗脚本");
        }

        // 新的取消token
        var cts2 = new CancellationTokenSource();
        ct.Register(cts2.Cancel);

        combatScenes.BeforeTask(cts2.Token);
        TimeSpan fightTimeout = TimeSpan.FromSeconds(_taskParam.Timeout); // 战斗超时时间
        Stopwatch timeoutStopwatch = Stopwatch.StartNew();

        Stopwatch checkFightFinishStopwatch = Stopwatch.StartNew();
        TimeSpan checkFightFinishTime = TimeSpan.FromSeconds(_finishDetectConfig.CheckTime); //检查战斗超时时间的超时时间


        //战斗前检查，可做成配置
        // if (await CheckFightFinish()) {
        //     return;
        // }
        // var FightEndFlag = false;
        FightEndFlag = false;
        SwitchTryCount = 0;
        var fightEndFlag = false;
        var timeOutFlag = false;
        string lastFightName = "";

        //统计切换人打架次数
        var countFight = 0;
        
        // 可以跳过的角色名,配置中有的和命令中有的取交
        var canBeSkippedAvatarNames = combatScenes.UpdateActionSchedulerByCd(_taskParam.ActionSchedulerByCd)
            .Where(s => commandAvatarNames.Contains(s)).WhereNotNull().ToList();
        
        //所有角色是否都可被跳过
        var allCanBeSkipped = commandAvatarNames.All(a => canBeSkippedAvatarNames.Contains(a));
        
        var delayTime = _finishDetectConfig.DelayTime;
        var detectDelayTime = _finishDetectConfig.DetectDelayTime;

        Avatar? guardianAvatar = null;
        if (!string.IsNullOrWhiteSpace(_taskParam.GuardianAvatar))
        {
            // Logger.LogInformation("盾奶优先功能角色预处理开始..{aq}-{aa}.",_taskParam.GuardianAvatar,combatScenes.GetAvatars().Count);
            if (int.Parse(_taskParam.GuardianAvatar) <= combatScenes.GetAvatars().Count) //确保序号在队伍内
            {
                guardianAvatar = combatScenes.SelectAvatar(int.Parse(_taskParam.GuardianAvatar));
            }
            else
            {
                Logger.LogWarning("盾奶优先功能角色预处理失败，请检查盾奶优先功能角色配置是否正确。");
                if (combatScenes.SelectAvatar(_taskParam.GuardianAvatar) is not null)
                {
                    guardianAvatar = combatScenes.SelectAvatar(int.Parse(_taskParam.GuardianAvatar));
                }
            }
        }

        AutoFightSeek.RotationCount= 0; // 重置旋转次数

        ImageRegion image = null;

        var useEqList = (_taskParam.AutoCombatEq && !string.IsNullOrWhiteSpace(_taskParam.UseEqList))
            ? _taskParam.UseEqList.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
                .Where(n => n >= 1 && n <= combatScenes.GetAvatars().Count) // 保证序号在队伍
                .ToList()
            : new List<int> { 1, 2, 3, 4 }
                .Where(n => n >= 1 && n <= combatScenes.GetAvatars().Count) // 添加此行以处理默认值
                .ToList();
        
        var useSkillList = new List<int>();
        var useSkillListWithH = new List<int>();
        var useSkillListWithF = 0;
        var useSkillListWithA = new Dictionary<int, int>();

        if (_taskParam.AutoCombatEq && !string.IsNullOrWhiteSpace(_taskParam.UseSkillList))
        {
            var skillParts = _taskParam.UseSkillList.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in skillParts)
            {
                var trimmedPart = part.Trim();
                // 使用正则表达式移除A及其后面的括号和内容
                var skillNumberStr = trimmedPart.Replace("H", "").Replace("F", "").Trim();
                var match = Regex.Match(skillNumberStr, @"(\d+)A(\(\d+\))?");
                skillNumberStr = System.Text.RegularExpressions.Regex.Replace(skillNumberStr, @"A\(\d+\)|A", "");
                
                if (match.Success)
                {
                    // 提取以A结尾的数字前面的数字
                    if (int.TryParse(match.Groups[1].Value, out int skillNumber2))
                    {
                        // 提取括号中的数字，如果存在的话
                        if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value.Trim('(', ')'), out int value))
                        {
                            useSkillListWithA.Add(skillNumber2, value);
                        }
                        else
                        {
                            useSkillListWithA.Add(skillNumber2,600);
                        }
                    }
                }
                
                var skillNumber = int.TryParse(skillNumberStr, out var n) ? n : 0;

                if (skillNumber >= 1 && skillNumber <= combatScenes.GetAvatars().Count) //保证序号在队伍
                {
                    useSkillList.Add(skillNumber); // 添加到全部技能列表

                    if (trimmedPart.Contains('H'))
                    {
                        useSkillListWithH.Add(skillNumber); // 添加到带H的技能列表
                    }
                    if (trimmedPart.Contains('F') && useSkillListWithF == 0) // 只记录第一个F
                    {
                        useSkillListWithF = skillNumber; // 记录第一个带F的技能序号
                    }
                }
            }
            foreach (var kvp in useSkillListWithA)
            {
                Logger.LogError($"{{ {kvp.Key}, {kvp.Value} }}");
            }
        }
        else
        {
            useSkillList = new List<int> { 1, 2, 3, 4 };
            useSkillListWithH = new List<int>();
            // useSkillListWithF = 0;
        }

        var predefinedlist = new List<string>() { "枫原万叶" ,"希诺宁"};
        
        // 战斗操作
        var fightTask = Task.Run(async () =>
        {
            #region 基于战斗检测经验值开关万叶拾取功能同步任务
            
            if (_taskParam.ExpKazuhaPickup) FindExp(cts2.Token);
            
            #endregion
            
            #region 自动吃药功能同步任务

            if (_taskParam.TakeMedicineEnabled)
            {
                TakeMedicine(cts2.Token);
            }
            else
            {
                IsTpForRecover = false;
                RecoverCount = 3;
            }
            
            #endregion
            
            try
            {
                FightStatusFlag = true;
                
                while (!cts2.Token.IsCancellationRequested)
                {
                   // 所有战斗角色都可以被取消
                    #region 本次战斗的跳过战斗判定

                    //如果所有角色都可以被跳过，且没有任何一个cd大于0的(技能都还没好)
                    //则强制等待，因为不等待的话什么都不能做，而且会造成刷屏
                    if (allCanBeSkipped)
                    {
                        //获取最低cd
                        var minCoolDown = commandAvatarNames.Select(a => combatScenes.SelectAvatar(a)).WhereNotNull()
                            .Select(a => a.GetSkillCdSeconds()).Min();
                        if (minCoolDown > 0)
                        {
                            TaskControl.Logger.LogInformation("队伍中所有角色的技能都在冷却中,等待{MinCoolDown}秒后继续。", Math.Round(minCoolDown, 2));
                            await Delay((int)Math.Ceiling(minCoolDown * 1000), ct);
                        }
                    }

                    var skipFightName = "";

                    #endregion
                    
                    for (var i = 0; i < combatCommands.Count; i++)
                    {
                        var command = combatCommands[i];
                        var lastCommand = i == 0 ? command : combatCommands[i - 1];
                        
                        #region 盾奶位技能优先和自动EQ功能
                        
                        var skipModel = guardianAvatar != null && lastFightName != command.Name;
                        
                        if (skipModel) {
                            
                            image = CaptureToRectArea();
                            
                            await AutoFightSkill.EnsureGuardianSkill(guardianAvatar,lastCommand,lastFightName,
                            _taskParam.GuardianAvatar,_taskParam.GuardianAvatarHold,5,ct,_taskParam.GuardianCombatSkip,_taskParam.BurstEnabled);
                            
                            if (_taskParam.AutoCombatEq && guardianAvatar.ManualSkillCd == 0 && !ct.IsCancellationRequested)
                            {
                                if (timeoutStopwatch.Elapsed > fightTimeout)
                                {
                                    fightEndFlag = true;
                                    timeOutFlag = true;
                                    break;
                                }

                                if(i>0)i--;
                                continue;     
                                
                            }

                            if (_taskParam.AutoCombatEq)
                            {
                                var useEq = new List<int>();
                                for (var h = 1; h <= combatScenes.GetAvatars().Count; h++)
                                {
                                    if (!combatScenes.SelectAvatar(h).IsActive(image))
                                    {
                                        continue;
                                    }

                                    try
                                    {
                                        useEq = await AutoFightSkill.AvatarQSkillAsync(image, useEqList, h);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("自动EQ战斗：角色 {name} 识别异常 {ex}", h, ex.Message);
                                        fightEndFlag = true;
                                    }
                                    
                                    break;
                                }
                                
                                if (useSkillListWithF>0 && combatScenes.SelectAvatar(useSkillListWithF).IsSkillReady()) //自定义序号首位先放E，只执行一次
                                {
                                    Logger.LogInformation("自动EQ战斗：执行序号 {name} 首E技能", useSkillListWithF);
                                    var avatarFirst = combatScenes.SelectAvatar(useSkillListWithF);
                                
                                    if (avatarFirst.TrySwitch(10) && !await AutoFightSkill.AvatarSkillAsync(Logger, avatarFirst, false, 1, ct))
                                    {
                                        avatarFirst.UseSkill(useSkillListWithH.Contains(useSkillListWithF),1); 
                                        var useA = useSkillListWithA.ContainsKey(useSkillListWithF) && useSkillListWithA[useSkillListWithF] > 0;
                                        if (useA)
                                        {
                                            Logger.LogInformation("自动EQ战斗：执行序号 {name} 角色首E技能后普攻 {time} ms", useSkillListWithF, useSkillListWithA[useSkillListWithF]);
                                            avatarFirst.Attack(useSkillListWithA[useSkillListWithF]);
                                        }
                                    }
                                    useSkillListWithF = 0;
                                }

                                if (useEq.Count > 0)
                                {
                                    foreach (var num in useEq) 
                                    {
                                        Logger.LogInformation("自动EQ战斗：使用序号 {name} 角色技能", num);
                                        var avatarQ = combatScenes.SelectAvatar(num);
                                        var useE = useSkillList.Contains(num);
                                        var avatarQHold = useSkillListWithH.Contains(num);
                                        var usePre = predefinedlist.Contains(avatarQ.Name);
                                        var useAContainsKey = useSkillListWithA.ContainsKey(num);
                                        var useA = (useAContainsKey && useSkillListWithA[num] > 0) || usePre;
                                        if (avatarQ.TrySwitch(15))
                                        {
                                            lastFightName = avatarQ.Name;
                                            countFight++;
                                            if (useE && !await AutoFightSkill.AvatarSkillAsync(Logger, avatarQ, false, 1, ct))
                                            {
                                                avatarQ.UseSkill(avatarQHold);
                                                if (useA)
                                                {
                                                    if (!useAContainsKey)
                                                    {
                                                        useSkillListWithA.Add(num,avatarQHold?700:600);
                                                    }
                                                    Logger.LogInformation("自动EQ战斗：执行序号 {name} 角色普攻 {time} ms", num, useSkillListWithA[num]);
                                                    avatarQ.Attack(useSkillListWithA[num]); 
                                                }
                                                
                                                var imageAfterUseSkill = CaptureToRectArea();
                                                var retry = 30;
                                                while (!await AutoFightSkill.AvatarSkillAsync(Logger, avatarQ, false, 1, ct,imageAfterUseSkill) && retry > 0)
                                                {
                                                    Simulation.SendInput.SimulateAction(GIActions.ElementalSkill);
                                                    Simulation.ReleaseAllKey();
                                                    //防止在纳塔飞天或爬墙
                                                    if (retry % 4 == 0)
                                                    {
                                                        Simulation.SendInput.SimulateAction(GIActions.NormalAttack);
                                                        Simulation.SendInput.SimulateAction(GIActions.Drop);
                                                    }
                                                    imageAfterUseSkill = CaptureToRectArea();
                                                    await Task.Delay(30, ct);
                                                    retry -= 1;
                                                }
                                                imageAfterUseSkill.Dispose();
                                                
                                                // if (retry > 0)
                                                // {
                                                //     avatarQ.LastSkillTime = DateTime.UtcNow;
                                                //     
                                                //     if (avatarQ.Name == "枫原万叶")
                                                //     {
                                                //         await Delay(100, ct);
                                                //         Simulation.SendInput.SimulateAction(GIActions.NormalAttack);
                                                //         await Delay(200, ct);
                                                //     }
                                                //     
                                                //     await Delay(99, ct);
                                                // }
                                            }
                                            
                                            fightEndFlag = await CheckFightFinish(0, detectDelayTime, ct,avatarQ);
                                            if (!fightEndFlag)
                                            { 
                                                var ms = 30;
                                                Simulation.SendInput.SimulateAction(GIActions.ElementalBurst);
                                                var imageAfterBurst = CaptureToRectArea();
                                                while (imageAfterBurst.Find(ElementAssets.Instance.PaimonMenuRo).IsExist() 
                                                       && !await AutoFightSkill.AvatarSkillAsync(Logger, avatarQ, true, 1, ct,imageAfterBurst,false) 
                                                       && ms > 0)
                                                {
                                                    Simulation.SendInput.SimulateAction(GIActions.ElementalBurst);
                                                    await Delay(50, ct);
                                                    imageAfterBurst = CaptureToRectArea();
                                                    ms -= 1;
                                                }
                                                Simulation.SendInput.SimulateAction(GIActions.ElementalBurst);
                                                imageAfterBurst.Dispose();
                                            }
                                            else
                                            {
                                                break;
                                            }
                                            if (guardianAvatar.IsSkillReady())
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                                useEq.Clear(); 
                                if (guardianAvatar.IsSkillReady() && !ct.IsCancellationRequested)
                                {
                                    if(i>0)i--;
                                    continue;
                                }
                            }
                            image.Dispose();
                        }
                        
                        if (fightEndFlag)break;
                        
                        var avatar = combatScenes.SelectAvatar(command.Name);
                        
                        #endregion
                        
                        #region 初始寻敌处理

                        if ( _finishDetectConfig.RotateFindEnemyEnabled && i == 0 && _taskParam.IsFirstCheck)
                        {
                            try
                            {
                                await AutoFightSeek.SeekAndFightAsync(TaskControl.Logger, detectDelayTime, delayTime,
                                    ct, true, _taskParam.RotaryFactor,avatar);
                            }
                            catch (Exception ex)
                            {
                                fightEndFlag = true;
                                Logger.LogError("初始寻敌异常 {ex}", ex.Message);
                            }
                        }
                        
                        #endregion
                        
                        if (avatar is null || (avatar.Name == guardianAvatar?.Name && (_taskParam.GuardianCombatSkip || _taskParam.BurstEnabled)))
                        {
                            Logger.LogDebug("跳过角色{command.Name} - {avatar.Name}", command.Name,avatar?.Name);
                            continue;
                        }
                        if (_taskParam.AutoCombatEq)avatar?.TrySwitch(10);
                        #region 每个命令的跳过战斗判定

                        // 判断是否满足跳过条件:
                        // 1.上一次成功执行命令的最后执行角色不是这次的执行角色
                        // 2.这次执行的角色包含在可跳过的角色列表中
                        if (!
                                //上次命令的执行角色和这次相同
                                (lastFightName == command.Name &&
                                 // 且未跳过(成功执行)了,则不进行跳过判定
                                 skipFightName == "")
                            &&
                            // 且这次执行的角色包含在可跳过的角色列表中
                            (allCanBeSkipped || canBeSkippedAvatarNames.Contains(command.Name))
                           )
                        {
                            var cd = avatar.GetSkillCdSeconds();
                            if (cd > 0)
                            {
                                // 如果上一次该角色已经被跳过，则不进行log输出，以免刷屏
                                if (skipFightName != command.Name)
                                {
                                    var manualSkillCd = avatar.ManualSkillCd;
                                    if (manualSkillCd > 0)
                                    {
                                        TaskControl.Logger.LogInformation("{commandName}cd冷却为{skillCd}秒,剩余{Cd}秒,跳过此次行动",
                                            command.Name,
                                            manualSkillCd, Math.Round(cd, 2));
                                    }
                                    else
                                    {
                                        TaskControl.Logger.LogInformation("{CommandName}cd冷却剩余{Cd}秒,跳过此次行动", command.Name,
                                            Math.Round(cd, 2));
                                    }
                                }

                                // 避免重复log提示
                                skipFightName = command.Name;
                                continue;
                            }

                            // 表示这次执行命令没有跳过
                            skipFightName = "";
                        }

                        #endregion
                        
                        if (timeoutStopwatch.Elapsed > fightTimeout || AutoFightSeek.RotationCount >= 6)
                        {
                            TaskControl.Logger.LogInformation(AutoFightSeek.RotationCount >= 6 ? "旋转次数达到上限，战斗结束" : "战斗超时结束");
                            fightEndFlag = true;
                            timeOutFlag = true;
                            break;
                        }

                        #region Q前寻敌处理
                        if (_finishDetectConfig.RotateFindEnemyEnabled && _taskParam.CheckBeforeBurst && (command.Method == Method.Burst || command.Args.Contains("q") || command.Args.Contains("Q")))
                        {
                            fightEndFlag = await CheckFightFinish(0, detectDelayTime, ct,avatar);
                        }
                        #endregion
                        
                        command.Execute(combatScenes, lastCommand);
                        //统计战斗人次
                        if (i == combatCommands.Count - 1 || command.Name != combatCommands[i + 1].Name)
                        {
                            countFight++;
                        }

                        lastFightName = command.Name;
                        if (!fightEndFlag && _taskParam is { FightFinishDetectEnabled: true })
                        {
                            //处于最后一个位置，或者当前执行人和下一个人名字不一样的情况，满足一定条件(开启快速检查，并且检查时间大于0或人名存在配置)检查战斗
                            if (i == combatCommands.Count - 1
                                || (
                                    _finishDetectConfig.FastCheckEnabled &&
                                    command.Name != combatCommands[i + 1].Name &&
                                    ((_finishDetectConfig.CheckTime > 0 &&
                                      checkFightFinishStopwatch.Elapsed > checkFightFinishTime)
                                     || _finishDetectConfig.CheckNames.Contains(command.Name))
                                ))
                            {
                                checkFightFinishStopwatch.Restart();
                               
                                if (_finishDetectConfig.DelayTimes.TryGetValue(command.Name, out var time))
                                {
                                    delayTime = time;
                                    TaskControl.Logger.LogInformation($"{command.Name}结束后，延时检查为{delayTime}毫秒");
                                }
                                else
                                {
                                    // Logger.LogInformation($"延时检查为{delayTime}毫秒");
                                }

                                fightEndFlag = await CheckFightFinish(delayTime, detectDelayTime,ct,avatar);
                            }
                        }

                        if (fightEndFlag)
                        {
                            break;
                        }
                    }


                    if (fightEndFlag)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
                throw;
            }
            finally
            {
                Simulation.ReleaseAllKey();
                FightStatusFlag = false;
                image?.Dispose();
                // if (_taskParam.TakeMedicineEnabled) RecoverCount = 0;
            }
        }, cts2.Token);

        await fightTask;

        if (_taskParam.KazuhaPickupEnabled && _taskParam.ExpKazuhaPickup && !_isExperiencePickup)
        {
            TaskControl.Logger.LogInformation("基于怪物经验判断：{text} 经验值显示","等待");
            await Delay(1000, ct);
        }
        FightEndFlag = true; 

        if ((_taskParam.BattleThresholdForLoot >= 2 && countFight < _taskParam.BattleThresholdForLoot) && (!_taskParam.ExpKazuhaPickup || !_isExperiencePickup))
        {
            TaskControl.Logger.LogInformation($"战斗人次（{countFight}）低于配置人次（{_taskParam.BattleThresholdForLoot}），跳过此次拾取！");
            
            if (_taskParam.EndBloodCheackEnabled)
            {
                //防止检测战斗结束时，派蒙头冠消失
                using var ra = CaptureToRectArea();
                var pixelValue = ra.SrcMat.At<Vec3b>(32, 67);
                ra.Dispose();
                // 检查每个通道的值是否在允许的范围内
                if (!(Math.Abs(pixelValue[0] - 143) <= 10 &&
                      Math.Abs(pixelValue[1] - 196) <= 10 &&
                      Math.Abs(pixelValue[2] - 233) <= 10))
                {
                    await Delay(1000, ct);
                }
            
                await EndBloodCheck(ct);
            }
            
            return;
        }
      
        if(_taskParam.KazuhaPickupEnabled && _taskParam.ExpKazuhaPickup) TaskControl.Logger.LogInformation("基于怪物经验判断：{text} 万叶拾取", _isExperiencePickup? "执行" : "不执行");
        
        if (_taskParam.KazuhaPickupEnabled && (!_taskParam.ExpKazuhaPickup || _isExperiencePickup))
        {
            // Logger.LogInformation("开始 _isExperiencePickup：{_isExperiencePickup}",_isExperiencePickup);
            // 队伍中存在万叶的时候使用一次长E
            var picker = combatScenes.SelectAvatar("枫原万叶") ?? combatScenes.SelectAvatar("琴");
            
            var oldPartyName = RunnerContext.Instance.PartyName;
            var switchPartyFlag = false;
            if (picker == null && !timeOutFlag &&!string.IsNullOrEmpty(_taskParam.KazuhaPartyName) && oldPartyName != _taskParam.KazuhaPartyName)
            {
                try
                {
                    TaskControl.Logger.LogInformation($"切换为拾取队伍：{_taskParam.KazuhaPartyName}");
                    var success = await new SwitchPartyTask().Start(_taskParam.KazuhaPartyName, ct);
                    if (success)
                    {
                        TaskControl.Logger.LogInformation($"成功切换队伍为{_taskParam.KazuhaPartyName}");
                        switchPartyFlag = true;
                        RunnerContext.Instance.PartyName = _taskParam.KazuhaPartyName;
                        RunnerContext.Instance.ClearCombatScenes();
                        var cs = await RunnerContext.Instance.GetCombatScenes(ct);
                        picker = cs.SelectAvatar("枫原万叶") ?? cs.SelectAvatar("琴");
                    }
                }
                catch (Exception e)
                {
                    TaskControl.Logger.LogInformation("切换队伍异常，跳过此步骤！");
                }

            }
            
            if (picker != null)
            {
                if (picker.Name == "枫原万叶")
                {
                    var time = TimeSpan.FromSeconds(picker.GetSkillCdSeconds());
                    using var fra = CaptureToRectArea();
                    if (!(lastFightName == picker.Name && time.TotalSeconds > 3))
                    {
                        TaskControl.Logger.LogInformation("使用 枫原万叶-长E 拾取掉落物");
                        await Delay(200, ct);
                        if (picker.TrySwitch(10))
                        {
                            await Delay(50, ct);
                            if (await AutoFightSkill.AvatarSkillAsync(Logger, picker, false, 1, ct))
                            {
                                await picker.WaitSkillCd(ct);
                            }
                            picker.UseSkill(true);
                            await Delay(50, ct);
                            Simulation.SendInput.SimulateAction(GIActions.NormalAttack);
                            await Delay(1500, ct);
                        }
                    }
                    else
                    {
                        TaskControl.Logger.LogInformation("距最近一次万叶出招，时间过短，跳过此次万叶拾取！");
                    }
                }
                else if (picker.Name == "琴")
                {
                    TaskControl.Logger.LogInformation("使用 琴-长E 拾取掉落物");
                    
                    var actionsToUse = PickUpCollectHandler.PickUpActions
                        .Where(action => action.StartsWith("琴-长E" + " ", StringComparison.OrdinalIgnoreCase))
                        .Select(action => action.Replace("琴-长E","琴", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    var find = _taskParam.QinDoublePickUp;
                    await Delay(150, ct);
                    if (picker.TrySwitch(10))
                    {
                        if (await AutoFightSkill.AvatarSkillAsync(Logger, picker, false, 1, ct))//有祭礼情况下可能CD已经好了
                        {
                            await picker.WaitSkillCd(ct);
                        }
                        foreach (var miningActionStr in actionsToUse)
                        {
                            var pickUpAction = CombatScriptParser.ParseContext(miningActionStr);

                            for (int i = 0; i < 2; i++)
                            {
                                foreach (var command in pickUpAction.CombatCommands)
                                {
                                    command.Execute(combatScenes);
                                    //异步执行，防止卡顿
                                    //异步执行，防止卡顿
                                    Task.Run(() =>
                                    {
                                        if (Monitor.TryEnter(PickLock))
                                        {
                                            try
                                            {
                                                if (find)
                                                {
                                                    using (var imagePick = CaptureToRectArea())
                                                    {
                                                        if (imagePick.Find(AutoPickAssets.Instance.PickRo).IsExist())
                                                        {
                                                            find = false;
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                Logger.LogError(e, "琴拾取物品异常");
                                                find = false;
                                            }
                                            finally
                                            {
                                                Monitor.Exit(PickLock);
                                            }
                                        }
                                        // 后面没代码了，不用写return？
                                    });
                                }

                                if (!find)
                                {
                                    break;
                                }

                                if (i == 0)
                                {
                                    Logger.LogInformation("自动拾取；尝试再次执行 琴-长E 拾取");
                                    await picker.WaitSkillCd(ct);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            
                            Simulation.ReleaseAllKey();
                        }
                    }
                }
            }
            //切换过队伍的，需要再切回来
            if (switchPartyFlag && !string.IsNullOrEmpty(oldPartyName))
            {
                try
                {
                    TaskControl.Logger.LogInformation($"切换为原队伍：{oldPartyName}");
                    var success = await new SwitchPartyTask().Start(oldPartyName, ct);
                    if (success)
                    {
                        TaskControl.Logger.LogInformation($"切换为原队伍{oldPartyName}");
                        switchPartyFlag = true;
                        RunnerContext.Instance.PartyName = oldPartyName;
                        RunnerContext.Instance.ClearCombatScenes();
                        await RunnerContext.Instance.GetCombatScenes(ct);
    
                    }
                }
                catch (Exception e)
                {
                    TaskControl.Logger.LogInformation("恢复原队伍失败，跳过此步骤！");
                }
                    
            }
        }
        
        if (_taskParam is { PickDropsAfterFightEnabled: true } )
        {
            // 执行自动拾取掉落物的功能
            await new ScanPickTask().Start(ct);
        }

        if (_taskParam.EndBloodCheackEnabled)
        {
            // if(!Bv.IsInBigMapUi(CaptureToRectArea()))
            //防止检测战斗结束时，派蒙头冠消失
            var pixelValue = CaptureToRectArea().SrcMat.At<Vec3b>(32, 67);
            // 检查每个通道的值是否在允许的范围内
            if (!(Math.Abs(pixelValue[0] - 143) <= 10 &&
                  Math.Abs(pixelValue[1] - 196) <= 10 &&
                  Math.Abs(pixelValue[2] - 233) <= 10))
            {
                await Delay(1000, ct);
            }
            
            await EndBloodCheck(ct);
        }
    }

    private void LogScreenResolution()
    {
        AssertUtils.CheckGameResolution("自动战斗");
    }

    static bool AreDifferencesWithinBounds((int, int, int) a, (int, int, int) b, (int, int, int) c)
    {
        // 计算每个位置的差值绝对值并进行比较
        return Math.Abs(a.Item1 - b.Item1) < c.Item1 &&
               Math.Abs(a.Item2 - b.Item2) < c.Item2 &&
               Math.Abs(a.Item3 - b.Item3) < c.Item3;
    }

    public async Task<bool> CheckFightFinish(int delayTime = 1500, int detectDelayTime = 450,CancellationToken ct = default,Avatar? avatar = null)
    {
        if (_finishDetectConfig.RotateFindEnemyEnabled)
        {
            bool? result = null;
            try
            {
                result = await AutoFightSeek.SeekAndFightAsync(TaskControl.Logger, detectDelayTime, delayTime, ct,false,_taskParam.RotaryFactor,avatar);
            }
            catch (Exception ex)
            {
                TaskControl.Logger.LogError(ex, "SeekAndFightAsync 方法发生异常");
                return true;
            }
            
            AutoFightSeek.RotationCount = (result == null) ? 
                AutoFightSeek.RotationCount + 1 :  0;
            
            if (result != null)
            {
                return result.Value;
            }
        }

        if (!_finishDetectConfig.RotateFindEnemyEnabled)await Delay(delayTime, _ct);
        
        TaskControl.Logger.LogInformation("打开编队界面检查战斗是否结束，延时{detectDelayTime}毫秒检查", detectDelayTime);
        // 最终方案确认战斗结束
        Simulation.SendInput.SimulateAction(GIActions.OpenPartySetupScreen);
        await Delay(detectDelayTime, _ct);
        
        using var ra = CaptureToRectArea();
        //判断整个界面是否有红色色块，如果有，则战继续，否则战斗结束
        // 只提取橙色
        
        var b3 = ra.SrcMat.At<Vec3b>(50, 790); //进度条颜色
        var whiteTile = ra.SrcMat.At<Vec3b>(50, 768); //白块
        ra.Dispose();
        Simulation.SendInput.SimulateAction(GIActions.Drop);
        if (IsWhite(whiteTile.Item2, whiteTile.Item1, whiteTile.Item0) &&
            IsYellow(b3.Item2, b3.Item1,
                b3.Item0) /* AreDifferencesWithinBounds(_finishDetectConfig.BattleEndProgressBarColor, (b3.Item0, b3.Item1, b3.Item2), _finishDetectConfig.BattleEndProgressBarColorTolerance)*/
           )
        {
            TaskControl.Logger.LogInformation("识别到战斗结束-j");
            //取消正在进行的换队
            Simulation.SendInput.SimulateAction(GIActions.OpenPartySetupScreen);
            return true;
        }

        TaskControl.Logger.LogInformation($"未识别到战斗结束yellow{b3.Item0},{b3.Item1},{b3.Item2}");
        TaskControl.Logger.LogInformation($"未识别到战斗结束white{whiteTile.Item0},{whiteTile.Item1},{whiteTile.Item2}");

        if (_finishDetectConfig.RotateFindEnemyEnabled)
        {
            Task.Run(() =>
            {
                Scalar bloodLower = new Scalar(255, 90, 90);
                MoveForwardTask.MoveForwardAsync(bloodLower, bloodLower, TaskControl.Logger, _ct);
            } ,_ct);
        }
        
        _lastFightFlagTime = DateTime.UtcNow;
        return false;
    }

    bool IsYellow(int r, int g, int b)
    {
        //Logger.LogInformation($"IsYellow({r},{g},{b})");
        // 黄色范围：R高，G高，B低
        return (r >= 200 && r <= 255) &&
               (g >= 200 && g <= 255) &&
               (b >= 0 && b <= 100);
    }

    bool IsWhite(int r, int g, int b)
    {
        //Logger.LogInformation($"IsWhite({r},{g},{b})");
        // 白色范围：R高，G高，B低
        return (r >= 240 && r <= 255) &&
               (g >= 240 && g <= 255) &&
               (b >= 240 && b <= 255);
    }
    
    //基于万叶经验值判断是否拾取
    private static Task FindExp(CancellationToken cts2)
    {
        var autoFightAssets = AutoFightAssets.Instance;

        try  
        {
            Task.Run(() =>
            {
                _isExperiencePickup = false;
                var expLogo = false;
                
                var experienceRas = new[]
                {
                   autoFightAssets.InitializeRecognitionObject(60), 
                   autoFightAssets.InitializeRecognitionObject(58), 
                   autoFightAssets.InitializeRecognitionObject(57),
                };
                
                while (!(_isExperiencePickup || FightEndFlag) && !cts2.IsCancellationRequested)
                {
                    try
                    {
                        cts2.ThrowIfCancellationRequested();

                        var result = NewRetry.WaitForAction(() =>
                        {
                            using (var ra = CaptureToRectArea())
                            {
                                _isExperiencePickup = experienceRas.Any(experienceRa => 
                                {
                                    var isExist = ra.Find(experienceRa);
                                    if (!isExist.IsExist())
                                    {
                                        return false;
                                    }
                
                                    var pixelValue1 = ra.SrcMat.At<Vec3b>(isExist.Y, isExist.X - 147); //经验值图标，在2K以上时匹配度0.6，这个经验值颜色尤为重要
                                    expLogo = pixelValue1[0] == 253 && pixelValue1[1] == 247 && pixelValue1[2] == 172;

                                    return expLogo;
                                });
                            }
                            return _isExperiencePickup;
                        }, cts2, 1, 100).Result;
                    }
                    catch (OperationCanceledException ex)
                    {
                        Console.WriteLine($"检测经验发生异常: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        // Console.WriteLine($"检测怪物经验发生异常: {ex.Message}");
                    }
                    
                    if (_isExperiencePickup) Logger.LogInformation("基于怪物经验判断：识别到 {text1} 经验值，{text2} 万叶拾取","精英","启用" );

                }
                
                cts2.ThrowIfCancellationRequested();
                
            }, cts2); 
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine($"检测经验发生异常: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"检测怪物经验发生异常: {ex.Message}");
        }
        finally
        {
            VisionContext.Instance().DrawContent.ClearAll();
        }
        
        return Task.CompletedTask;
    }
    
    public static int RecoverCount = 0; // 吃复活药次数
    
    private Task TakeMedicine(CancellationToken cts2,bool endBloodCheck = false)
    {
        RecoverCount = 0; // 吃复活药次数
        IsTpForRecover = true; //检查复活关闭
        var resurrectionCount = 0; // 吃复活药次数
        var tolerance = 10;// 定义容错范围
        var greenBlood = 0; // 绿血标记
        
        try
        {
            Task.Run(() =>
            {
                using (var ra = CaptureToRectArea())
                {
                    using var mRect = ra.DeriveCrop(1817, 781, 4, 14);
                    using var mask = OpenCvCommonHelper.Threshold(mRect.SrcMat,new Scalar(192, 233, 102), new Scalar(193, 233, 103));
                    using var labels = new Mat();
                    using var stats = new Mat();
                    using var centroids = new Mat();

                    var numLabels = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids,
                        connectivity: PixelConnectivity.Connectivity4, ltype: MatType.CV_32S);

                    // Logger.LogInformation("自动吃药：检测到{numLabels}", numLabels);
                    
                    if (!(numLabels > 1))//判断是否带营养袋，连通性检测药品上方的绿色块
                    {
                        RecoverCount = 3;
                        IsTpForRecover = false;
                        TaskControl.Logger.LogInformation("自动吃药：未发现营养袋，自动吃药关闭");
                        return;
                    }
                    else
                    {
                        if (!endBloodCheck)  TaskControl.Logger.LogInformation(
                            "自动吃药：检测间隔{checkInterval}，吃药间隔{medicineInterval}，吃药上限{recoverMaxCount}，结束吃药{endBloodCheck}",
                            _taskParam.CheckInterval, _taskParam.MedicineInterval, _taskParam.RecoverMaxCount, _taskParam.EndBloodCheackEnabled ? "开" : "关");
                    }
                }

                while (!FightEndFlag && !cts2.IsCancellationRequested)
                {
                    var gray = false;
                    var redBlood = false;
                    
                    try
                    {
                        cts2.ThrowIfCancellationRequested();

                        var cheack = NewRetry.WaitForAction(() =>
                        {
                            using (var ra = CaptureToRectArea())
                            {
                                var pixelValue = ra.SrcMat.At<Vec3b>(32, 67);//派蒙头冠颜色，比模板匹配快，在开大或其他页面不进行检查
                                var paiMon = (Math.Abs(pixelValue[0] - 143) <= tolerance &&
                                             Math.Abs(pixelValue[1] - 196) <= tolerance &&
                                             Math.Abs(pixelValue[2] - 233) <= tolerance);
                                if (!paiMon)
                                {
                                    //延时_taskParam.CheckInterval毫秒再次检查
                                    Sleep(_taskParam.CheckInterval-10, _ct);
                                    return true;
                                }

                                using var bloodtRect = ra.DeriveCrop(808, 1009, 3, 3);
                                using var mask = OpenCvCommonHelper.Threshold(bloodtRect.SrcMat, new Scalar(250, 90, 89),
                                    new Scalar(250, 91, 89));
                                using var labels = new Mat();
                                using var stats = new Mat();
                                using var centroids = new Mat();

                                var numLabels = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids,
                                    connectivity: PixelConnectivity.Connectivity4, ltype: MatType.CV_32S);

                                //死亡检查
                                for (int h = 0; h < 4; h++)
                                {
                                    using var croppedImage = ra.DeriveCrop(1810, 256 + 96 * h, 15, 1).SrcMat;
                                    
                                    var isGrayscale = true;
                                    for (int i = 0; i < croppedImage.Rows; i++)
                                    {
                                        for (int j = 0; j < croppedImage.Cols; j++)
                                        {
                                            Vec3b pixel = croppedImage.At<Vec3b>(i, j);
                                            if (pixel[0] != pixel[1] || pixel[1] != pixel[2]) //转为灰度后，是否和现在的灰度一样，即可判断死亡
                                            {
                                                isGrayscale = false;
                                                break;
                                            }
                                        }

                                        if (!isGrayscale)
                                        {
                                            break;
                                        }
                                    }

                                    if (isGrayscale)
                                    {
                                        gray = true;
                                    }
                                }

                                if (numLabels > 1)//红血检查
                                {
                                    pixelValue = ra.SrcMat.At<Vec3b>(785, 1818);
                                    if (pixelValue[0] == 255 && pixelValue[1] == 255 && pixelValue[2] == 255)
                                    {
                                        if (resurrectionCount >= 0)
                                        {
                                            Logger.LogInformation("自动吃药：检测到复活药，{text} 吃回复药", "不执行");
                                            resurrectionCount = -1;
                                        }
                                        redBlood = false;
                                    }
                                    else
                                    {
                                        redBlood = true;
                                    }
                                }
                                else
                                {  
                                    pixelValue = ra.SrcMat.At<Vec3b>(1010,814);//在丝血时，连通性和颜色判断都检测不到，直接检测是否为绿色累计3次
                                    if (!(Math.Abs(pixelValue[0] - 34) <= tolerance &&
                                         Math.Abs(pixelValue[1] - 215) <= tolerance &&
                                         Math.Abs(pixelValue[2] - 150) <= tolerance))
                                    { 
                                        greenBlood ++;
                                        if (greenBlood > 3 || endBloodCheck && greenBlood > 0)
                                        {
                                            pixelValue = ra.SrcMat.At<Vec3b>(785, 1818);
                                            if (pixelValue[0] == 255 && pixelValue[1] == 255 && pixelValue[2] == 255)
                                            {
                                                if (resurrectionCount >= 0)
                                                {
                                                    Logger.LogInformation("自动吃药：检测到复活药，{text} 吃回复药", "不执行");
                                                    resurrectionCount = -1;
                                                }
                                                redBlood = false;
                                            }
                                            else
                                            {
                                                redBlood = true;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        greenBlood = 0;
                                    }
                                    
                                }

                                return redBlood || gray;
                            }
                        }, cts2, 1, _taskParam.CheckInterval).Result;

                        if ((redBlood || gray) &&
                            (DateTime.UtcNow - PathingConditionConfig.LastEatTime).TotalMilliseconds > Math.Max(_taskParam.MedicineInterval, 1500))
                        {
                            var shouldRecover = (redBlood && resurrectionCount < _taskParam.RecoverMaxCount) ||
                                                 (gray && RecoverCount < 2);//判断吃药上限
                            if (shouldRecover)
                            {
                                Simulation.SendInput.SimulateAction(GIActions.QuickUseGadget); 
                                Simulation.ReleaseAllKey();
                                if (redBlood) resurrectionCount++;
                                if (gray) RecoverCount++;
                                TaskControl.Logger.LogInformation("自动吃药：{text} " + "使用小道具", redBlood ? "发现红血" : "发现角色死亡");
                                PathingConditionConfig.LastEatTime = DateTime.UtcNow;
                                redBlood = false;
                                gray = false;
                                if (endBloodCheck && (resurrectionCount >= 1 || RecoverCount >= 1)) return;//单次检测复用
                            }
                            else
                            {
                                resurrectionCount = _taskParam.RecoverMaxCount;
                                RecoverCount = 2;
                                TaskControl.Logger.LogInformation("自动吃药：{text}", "吃药数量超额退出！");
                                IsTpForRecover = false; // 吃完药品后，打开复活检测
                                return;
                            }
                        }

                        using (var bitmap = CaptureToRectArea())//复活界面检测，自动战斗期间，不进行BGI的复活检测，超出吃药上限后才会检测
                        {
                            if (Bv.IsInRevivePrompt(bitmap))
                            {
                                if (RecoverCount < 1)//只吃一次复活药
                                {
                                    PathingConditionConfig.LastEatTime = DateTime.UtcNow;
                                    RecoverCount++;
                                    var confirmRectArea = bitmap.Find(AutoFightAssets.Instance.ConfirmRa);
                                    if (!confirmRectArea.IsEmpty())
                                    {
                                        Simulation.ReleaseAllKey();
                                        confirmRectArea.Click();
                                        Delay(100, _ct).Wait();
                                        confirmRectArea.ClickTo(-100, 0);
                                        Delay(100, _ct).Wait();
                                        Simulation.SendInput.SimulateAction(GIActions.QuickUseGadget);
                                        continue;
                                    }
                                }
                                else if (RecoverCount < 2)
                                {
                                    Simulation.SendInput.Keyboard.KeyPress(User32.VK.VK_ESCAPE);  
                                    continue;
                                }
                            }
                        }

                        if (RecoverCount > 1 || FightEndFlag)
                        {
                            RecoverCount = 2;
                            
                            // using (var bitmap = CaptureToRectArea())
                            // {
                            //     var confirmRectArea = bitmap.Find(AutoFightAssets.Instance.ConfirmRa);
                            //     if (!confirmRectArea.IsEmpty())
                            //     {
                            //         Simulation.ReleaseAllKey();
                            //         confirmRectArea.Click();
                            //         confirmRectArea.ClickTo(-100, 0);
                            //         Simulation.SendInput.SimulateAction(GIActions.QuickUseGadget);
                            //         Delay(500, _ct).Wait();
                            //     }
                            // }
                            // // Logger.LogInformation("自动吃药：检测到复活界面33，{text} ", RecoverCount);
                            IsTpForRecover = false;
                        }

                    }
                    catch (OperationCanceledException ex)
                    {
                        Console.WriteLine($"自动吃药发生异常: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        // Console.WriteLine($"自动吃药发生异常: {ex.Message}");
                    }
                }

            }, cts2);
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine($"自动吃药发生异常: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"自动吃药发生异常: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    //定义按键，用于结束吃药的切换人
    private static readonly GIActions[] MemberActions = new GIActions[]
    {
        GIActions.SwitchMember1,
        GIActions.SwitchMember2,
        GIActions.SwitchMember3,
        GIActions.SwitchMember4
    };
    
    private async Task EndBloodCheck(CancellationToken ct)
    {
        IsTpForRecover = true; // 复活检测关闭
        var ms = 2500;  //检测区域是否有红血，没有发现红血，则退出
        var useMedicine = new List<int> { 1, 2, 3, 4 };
        var endBloodCheck = false;//血量复检标志位
        
        try
        { 
            await TakeMedicine(ct,true);//尝试吃药和复活角色
            
            while (ms > 0)
            {
               using (var ra = CaptureToRectArea())
               {
                    var pixelValue = ra.SrcMat.At<Vec3b>(785, 1818);//TakeMedicine后，不会有死亡角色，如出现死亡角色导致变复活药，说明死超过2位角色，也不用执行了
                    if (pixelValue[0] == 255 && pixelValue[1] == 255 && pixelValue[2] == 255)
                    {
                        Logger.LogInformation("自动结束吃药：检测到复活药，{text} 结束吃恢复药", "不执行");
                        return;
                    }
                    else
                    {
                        // 非复活药前提下再识别营养袋，优化效率
                        using var mRect = ra.DeriveCrop(1817, 781, 4, 14);
                        using var mask = OpenCvCommonHelper.Threshold(mRect.SrcMat,new Scalar(192, 233, 102), new Scalar(193, 233, 103));
                        using var labels = new Mat();
                        using var stats = new Mat();
                        using var centroids = new Mat();

                        var numLabels = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids,
                            connectivity: PixelConnectivity.Connectivity4, ltype: MatType.CV_32S);

                        // Logger.LogInformation("自动吃药：检测到{numLabels}", numLabels);
                    
                        if (!(numLabels > 1))//判断是否带营养袋，连通性检测药品上方的绿色块
                        {
                            Logger.LogInformation("自动结束吃药：{t} 营养袋，结束吃药关闭","未发现");
                            return;
                        }
                    }
                   
                    for (var h = 0; h < 4; h++)
                    {
                       using var bloodtRect = ra.DeriveCrop(1694, 281 + h * 96, 3, 10);
                       using var mask = OpenCvCommonHelper.Threshold(bloodtRect.SrcMat, new Scalar(150, 215, 34),new Scalar(161, 220, 60));
                       using var labels = new Mat();
                       using var stats = new Mat();
                       using var centroids = new Mat();

                       var numLabels = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids,
                           connectivity: PixelConnectivity.Connectivity4, ltype: MatType.CV_32S);//非出战角色，右侧头像血量检查

                       using var bloodtRect2 = ra.DeriveCrop(1859, 278+ h * 96, 3, 3);
                       using var mask2 = OpenCvCommonHelper.Threshold(bloodtRect2.SrcMat, new Scalar(255, 255, 255));
                       using var labels2 = new Mat();
                       using var stats2 = new Mat();
                       using var centroids2 = new Mat();

                       var numLabels2 = Cv2.ConnectedComponentsWithStats(mask2, labels2, stats2, centroids2,
                           connectivity: PixelConnectivity.Connectivity4, ltype: MatType.CV_32S);//出战代表没有死亡，如果红血，会在开头的TakeMedicine恢复

                       if (numLabels > 1 || !(numLabels2 > 1))
                       {
                           ms = 1;// 发现红血，退出
                           useMedicine.Remove(h+1);
                       }
                    }
               }

               if (useMedicine.Count > 0 && !endBloodCheck)//发现红血角色，可能因为游泳等误判，进行复检
               {
                   endBloodCheck = true;
                   TaskControl.Logger.LogInformation("自动结束吃药：检测到红血角色，{text} 结束吃药，进行复检", useMedicine);
                   ms = 100;// 设置100会再次检测
                   useMedicine = new List<int> { 1, 2, 3, 4 };
                   await Task.Delay(500, ct);
               }
               
               await Task.Delay(100, ct);
               ms -= 95;
            }

            using var swimming = CaptureToRectArea();
            if (useMedicine.Count > 0 && !Avatar.SwimmingConfirm(swimming))
            {
                //计算2上次吃药时间到现在是否超过2秒，未超过就等待
                if ((DateTime.UtcNow - PathingConditionConfig.LastEatTime).TotalMilliseconds < 1500)
                {
                    await Task.Delay(1500 - (int)(DateTime.UtcNow - PathingConditionConfig.LastEatTime).TotalMilliseconds, ct);
                }
                PathingConditionConfig.LastEatTime = DateTime.UtcNow;
                TaskControl.Logger.LogInformation("自动结束吃药：发现红血角色，执行吃药 {text} 编号", useMedicine);
                //通过编号切换角色补血,不进行确认是否吃上
                foreach (var num in useMedicine)
                {
                    Simulation.ReleaseAllKey();
                    await Task.Delay(700, ct);
                    Simulation.SendInput.SimulateAction(MemberActions[num-1]);
                    await Task.Delay(800, ct);
               
                    using (var bitmap = CaptureToRectArea())
                    {
                        if (Bv.IsInRevivePrompt(bitmap))//如果在复活界面，说明没复活药了
                        {
                            Simulation.SendInput.Keyboard.KeyPress(User32.VK.VK_ESCAPE);
                            await Task.Delay(500, ct);
                        }
                    }

                    Simulation.SendInput.SimulateAction(GIActions.QuickUseGadget);
                    await Task.Delay(700, ct);
                }
            }
            else
            {
                TaskControl.Logger.LogInformation("自动结束吃药：检测未发现红血角色，{text} 结束吃药","不执行");
            }
            IsTpForRecover = false; // 复活检测打开
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine($"战斗结束血量检测发生异常: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"战斗结束血量检测发生异常: {ex.Message}");
        }
    }

    static double FindMax(double[] numbers)
    {
        if (numbers == null || numbers.Length == 0)
        {
            throw new ArgumentException("The array is empty or null.");
        }

        double max = numbers[0] > 10000 ? 0 : numbers[0];
        foreach (var num in numbers)
        {
            var cpnum = numbers[0] > 10000 ? 0 : num;
            max = Math.Max(max, num);
        }

        return max;
    }

    [Obsolete]
    private static Dictionary<string, double> ParseStringToDictionary(string input, double defaultValue = -1)
    {
        var dictionary = new Dictionary<string, double>();

        if (string.IsNullOrEmpty(input))
        {
            return dictionary; // 返回空字典
        }

        string[] pairs = input.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split(',', StringSplitOptions.TrimEntries);

            if (parts.Length > 0)
            {
                string name = parts[0];
                double value = defaultValue;

                if (parts.Length > 1 && double.TryParse(parts[1], out var parsedValue))
                {
                    value = parsedValue;
                }

                dictionary[name] = value;
            }
        }

        return dictionary;
    }

    private bool HasFightFlagByYolo(ImageRegion imageRegion)
    {
        // if (RuntimeHelper.IsDebug)
        // {
        //     imageRegion.SrcMat.SaveImage(Global.Absolute(@"log\fight\" + $"{DateTime.UtcNow:yyyyMMdd_HHmmss_ffff}.png"));
        // }
        var dict = _predictor.Detect(imageRegion);
        return dict.ContainsKey("health_bar") || dict.ContainsKey("enemy_identify");
    }

    // 无用
    // [Obsolete]
    // private bool HasFightFlagByGadget(ImageRegion imageRegion)
    // {
    //     // 小道具位置 1920-133,800,60,50
    //     var gadgetMat = imageRegion.DeriveCrop(AutoFightAssets.Instance.GadgetRect).SrcMat;
    //     var list = ContoursHelper.FindSpecifyColorRects(gadgetMat, new Scalar(225, 220, 225), new Scalar(255, 255, 255));
    //     // 要大于 gadgetMat 的 1/2
    //     return list.Any(r => r.Width > gadgetMat.Width / 2 && r.Height > gadgetMat.Height / 2);
    // }
}
