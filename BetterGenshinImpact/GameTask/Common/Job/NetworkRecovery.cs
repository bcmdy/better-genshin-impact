using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using Vanara.PInvoke;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using System;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Exception;
using BetterGenshinImpact.GameTask.AutoWood.Assets;
using BetterGenshinImpact.GameTask.AutoWood.Utils;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Model.Area;
using Microsoft.Extensions.Logging;
using Vanara.PInvoke;
using static BetterGenshinImpact.GameTask.Common.TaskControl;

using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Recognition.OCR;
using BetterGenshinImpact.Core.Recognition.ONNX;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.Core.Simulator.Extensions;
using BetterGenshinImpact.GameTask.AutoFight.Assets;
using BetterGenshinImpact.GameTask.AutoFight.Model;
using BetterGenshinImpact.GameTask.AutoFight.Script;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Exception;
using BetterGenshinImpact.GameTask.AutoPick.Assets;
using BetterGenshinImpact.GameTask.Common.Map;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Service.Notification;
using BetterGenshinImpact.View.Drawable;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Recognition;
using BetterGenshinImpact.GameTask.AutoTrackPath;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common.Job;
using BetterGenshinImpact.Service.Notification.Model.Enum;
using Vanara.PInvoke;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text.RegularExpressions;
using BetterGenshinImpact.GameTask.AutoArtifactSalvage;
using System.Collections.ObjectModel;
using BetterGenshinImpact.Core.Script.Dependence;
using BetterGenshinImpact.GameTask.AutoDomain.Model;
using BetterGenshinImpact.GameTask.Common;
using Compunet.YoloSharp;
using Microsoft.Extensions.DependencyInjection;

namespace BetterGenshinImpact.GameTask.Common.Job;

public class NetworkRecovery
{
    public string Name => "断网恢复";


    private readonly BgiYoloPredictor _predictor;

    private readonly CombatScriptBag _combatScriptBag;

    private CancellationToken _ct;

    private ObservableCollection<OneDragonFlowConfig> ConfigList = [];
    
    private readonly ReturnMainUiTask _returnMainUiTask = new();
    
    private static RecognitionObject GetConfirmRa(bool isOcrMatch = false,params string[] targetText)
    {
        var screenArea = CaptureToRectArea();
        var x = (int)(screenArea.Width * 0.3);
        var y = (int)(screenArea.Height * 0.3);
        var width = (int)(screenArea.Width * 0.5);
        var height = (int)(screenArea.Height * 0.5);
        
        return isOcrMatch ? RecognitionObject.OcrMatch(x, y, width, height, targetText) : 
            RecognitionObject.Ocr(x, y, width, height);
    }
    
    public static async Task Start(CancellationToken ct)
    {
        var fightAssets = AutoFightAssets.Instance;
        
        await NewRetry.WaitForElementDisappear(
            GetConfirmRa(true,"连接超时","连接已断开","网络错误","无法登录服务器","提示","通知"),
            screen => { 
                var confirm =
                    screen.FindMulti(GetConfirmRa());
                var confirmDone = confirm.LastOrDefault(t =>
                    Regex.IsMatch(t.Text, "确认"));
                if (confirmDone != null)
                {
                    confirmDone.Click();
                    confirmDone.Dispose();
                }
            },
            ct,
            10,
            1000
        );
        
        //等待3秒
        await Task.Delay(3000);
        
        await NewRetry.WaitForElementDisappear(
            GetConfirmRa(true,"连接超时","连接已断开","网络错误","无法登录服务器","提示","通知"),
            screen => { 
                var confirm =
                    screen.FindMulti(GetConfirmRa());
                var confirmDone = confirm.LastOrDefault(t =>
                    Regex.IsMatch(t.Text, "确认"));
                if (confirmDone != null)
                {
                    confirmDone.Click();
                    confirmDone.Dispose();
                }
            },
            ct,
            5,
            1000
        );
        
        await NewRetry.WaitForElementAppear(
            ElementAssets.Instance.PaimonMenuRo,
            ()  => {  
                using var ra = CaptureToRectArea();
                //Enter出现
                var enter =
                    ra.FindMulti(GetConfirmRa());
                var enterDone = enter.LastOrDefault(t =>
                    Regex.IsMatch(t.Text, "确认"));
                if (enterDone != null)
                {
                    Logger.LogWarning("确认");
                    enterDone.Click();
                    enterDone.Dispose();
                }
                if (ra.Find(ElementAssets.Instance.PaimonMenuRo).IsEmpty())
                {
                    GameCaptureRegion.GameRegion1080PPosClick(1100, 755); 
                    GameCaptureRegion.GameRegion1080PPosClick(1200, 630); 
                }
            },
            ct,
            60,
            1000
        );
        
        await new ReturnMainUiTask().Start(ct);
        
    }
}
