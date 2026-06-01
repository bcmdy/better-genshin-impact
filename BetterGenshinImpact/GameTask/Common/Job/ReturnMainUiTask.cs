using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.BgiVision;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using Vanara.PInvoke;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using BetterGenshinImpact.Core.Recognition;
using BetterGenshinImpact.Core.Recognition.OpenCv;
using BetterGenshinImpact.Core.Script.Dependence;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.Core.Simulator.Extensions;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Exception;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using BetterGenshinImpact.GameTask.AutoPathing.Model.Enum;
using BetterGenshinImpact.GameTask.AutoTrackPath.Model;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common.Exceptions;
using BetterGenshinImpact.GameTask.Common.Job;
using BetterGenshinImpact.GameTask.Common.Map.Maps;
using BetterGenshinImpact.GameTask.Common.Map.Maps.Base;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.GameTask.QuickTeleport.Assets;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Helpers.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BetterGenshinImpact.GameTask.Common.Job;

public class ReturnMainUiTask
{
    public string Name => "返回主界面";

    public async Task Start(CancellationToken ct)
    {
        Logger.LogWarning("0-696");
        if (Bv.IsInMainUi(CaptureToRectArea()))
        {
            return;
        }

        for (var i = 0; i < 8; i++)
        {
            Simulation.SendInput.Keyboard.KeyPress(User32.VK.VK_ESCAPE);
            await Delay(900, ct);

            var region = CaptureToRectArea();

            var exitDoor = region.Find(ElementAssets.Instance.BtnExitDoor.Value);
            if (exitDoor.IsExist())
            {
                exitDoor.Click();
                await Delay(5000, ct);
                region = CaptureToRectArea();
            }
            
            if (Bv.IsInMainUi(region))
            {
                region.Dispose();
                return;
            }
            else
            {
                region.Dispose();
            }
        }
        await Delay(500, ct);
        Simulation.SendInput.Keyboard.KeyPress(User32.VK.VK_RETURN);
        await Delay(500, ct);
        Simulation.SendInput.Keyboard.KeyPress(User32.VK.VK_ESCAPE);
    }
}
