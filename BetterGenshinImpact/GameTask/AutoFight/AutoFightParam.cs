using BetterGenshinImpact.GameTask.Model;
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
using BetterGenshinImpact.Core.Recognition.OCR;
using BetterGenshinImpact.Core.Recognition.OpenCv;


namespace BetterGenshinImpact.GameTask.AutoFight;

public class AutoFightParam : BaseTaskParam
{
    public  class FightFinishDetectConfig 
    {
        public string BattleEndProgressBarColor { get; set; }= "";

        public string BattleEndProgressBarColorTolerance { get; set; }= "";
        public bool FastCheckEnabled = false;
        public string FastCheckParams = "";
        public string CheckEndDelay = "";
        public string BeforeDetectDelay = "";
        public bool RotateFindEnemyEnabled = false;
    }
    
    public AutoFightParam(string path, AutoFightConfig autoFightConfig)
    {
        CombatStrategyPath = path;
        Timeout = autoFightConfig.Timeout;
        FightFinishDetectEnabled = autoFightConfig.FightFinishDetectEnabled;
        PickDropsAfterFightEnabled = autoFightConfig.PickDropsAfterFightEnabled;
        PickDropsAfterFightSeconds = autoFightConfig.PickDropsAfterFightSeconds;
        KazuhaPickupEnabled = autoFightConfig.KazuhaPickupEnabled;
        ActionSchedulerByCd = autoFightConfig.ActionSchedulerByCd;
       
        FinishDetectConfig.FastCheckEnabled = autoFightConfig.FinishDetectConfig.FastCheckEnabled;
        FinishDetectConfig.FastCheckParams = autoFightConfig.FinishDetectConfig.FastCheckParams;
        FinishDetectConfig.CheckEndDelay = autoFightConfig.FinishDetectConfig.CheckEndDelay;
        FinishDetectConfig.BeforeDetectDelay = autoFightConfig.FinishDetectConfig.BeforeDetectDelay;
        FinishDetectConfig.RotateFindEnemyEnabled = autoFightConfig.FinishDetectConfig.RotateFindEnemyEnabled;
        
        
        KazuhaPartyName = autoFightConfig.KazuhaPartyName;
        OnlyPickEliteDropsMode = autoFightConfig.OnlyPickEliteDropsMode;
        BattleThresholdForLoot = autoFightConfig.BattleThresholdForLoot ?? BattleThresholdForLoot;
        //下面参数固定，只取自动战斗里面的
        FinishDetectConfig.BattleEndProgressBarColor = TaskContext.Instance().Config.AutoFightConfig.FinishDetectConfig.BattleEndProgressBarColor;
        FinishDetectConfig.BattleEndProgressBarColorTolerance = TaskContext.Instance().Config.AutoFightConfig.FinishDetectConfig.BattleEndProgressBarColorTolerance;
        
        GuardianAvatar = autoFightConfig.GuardianAvatar;
        GuardianCombatSkip = autoFightConfig.GuardianCombatSkip;
        SkipModel = autoFightConfig.SkipModel;
        GuardianAvatarHold = autoFightConfig.GuardianAvatarHold;
        
        CountryName = autoFightConfig.CountryName;
        
        BurstEnabled = autoFightConfig.BurstEnabled;
        ExpKazuhaPickup = autoFightConfig.ExpKazuhaPickup;
        IsFirstCheck = autoFightConfig.FinishDetectConfig.IsFirstCheck;
        RotaryFactor = autoFightConfig.FinishDetectConfig.RotaryFactor;
        SwimmingEnabled = autoFightConfig.SwimmingEnabled;
        TakeMedicineEnabled = autoFightConfig.TakeMedicineEnabled;
        MedicineInterval = autoFightConfig.MedicineInterval;
        CheckInterval = autoFightConfig.CheckInterval;
        RecoverMaxCount = autoFightConfig.RecoverMaxCount;
        EndBloodCheackEnabled = autoFightConfig.EndBloodCheackEnabled;
    }

    public FightFinishDetectConfig FinishDetectConfig { get; set; } = new();

    public string CombatStrategyPath { get; set; }

    public bool FightFinishDetectEnabled { get; set; } = false;
    public bool PickDropsAfterFightEnabled { get; set; } = false;
    public int PickDropsAfterFightSeconds { get; set; } = 15;
    public int BattleThresholdForLoot { get; set; } = -1;
    public int Timeout { get; set; } = 120;

    public bool KazuhaPickupEnabled = true;
    public string ActionSchedulerByCd = "";
    public string KazuhaPartyName;
    public string OnlyPickEliteDropsMode="";
    public string GuardianAvatar { get; set; } = " ";
    public bool GuardianCombatSkip { get; set; } = false;
    public bool SkipModel = false;
    public bool GuardianAvatarHold = false;
    public string?[] CountryName = ["自动"];
    public bool BurstEnabled { get; set; } = false;
    public bool ExpKazuhaPickup  { get; set; } = false;
    public bool IsFirstCheck { get; set; } = true;
    
    public int RotaryFactor { get; set; } = 10;
    
    public static bool SwimmingEnabled  { get; set; } = false;
    
    public bool TakeMedicineEnabled { get; set; } = false;
    
    public int MedicineInterval { get; set; } = 1500;
    
    public int CheckInterval { get; set; } =  200;
    
    public int RecoverMaxCount { get; set; } =  5;
    
    public bool EndBloodCheackEnabled { get; set; } = false;
}