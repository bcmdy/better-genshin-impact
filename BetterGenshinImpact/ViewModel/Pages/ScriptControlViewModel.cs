using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Script;
using BetterGenshinImpact.Core.Script.Group;
using BetterGenshinImpact.Core.Script.Project;
using BetterGenshinImpact.Core.Script.Utils;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using BetterGenshinImpact.GameTask.LogParse;
using BetterGenshinImpact.GameTask.TaskProgress;
using BetterGenshinImpact.Helpers.Ui;
using BetterGenshinImpact.Model;
using BetterGenshinImpact.Service.Interface;
using BetterGenshinImpact.View.Controls.Webview;
using BetterGenshinImpact.View.Pages.View;
using BetterGenshinImpact.View.Behavior;
using BetterGenshinImpact.View.Windows;
using BetterGenshinImpact.View.Windows.Editable;
using BetterGenshinImpact.ViewModel.Pages.View;
using BetterGenshinImpact.ViewModel.Windows.Editable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Violeta.Controls;
using Button = Wpf.Ui.Controls.Button;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using StackPanel = Wpf.Ui.Controls.StackPanel;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;
using  Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BetterGenshinImpact.ViewModel.Pages;

public partial class ScriptControlViewModel : ViewModel
{
    private readonly ISnackbarService _snackbarService;

    private readonly ILogger<ScriptControlViewModel> _logger = App.GetLogger<ScriptControlViewModel>();

    private readonly IScriptService _scriptService;
    
    [ObservableProperty] private Boolean _isInsetMode = false;
    
    [ObservableProperty] private OneDragonFlowViewModel? _viewModel;

    /// <summary>
    /// 锄地一条龙是否已解锁（用于 UI 绑定，响应解锁事件实时刷新）
    /// </summary>
    [ObservableProperty]
    private bool _isHoeingUnlocked = TaskSettingsPageViewModel.AutoHoeingUnlocked;
    
    public static OtherConfig OtherConfig { get; set; } = TaskContext.Instance().Config.OtherConfig;

    public static class AppPaths
    {
        public const string JsScripts = @"User\JsScript";
        public const string MapTracks = @"User\AutoPathing";
        public const string MouseScripts = @"User\KeyMouseScript";
    }
    
    private readonly List<String> _scriptGroupsDefault = new List<string> { "领取邮件","合成树脂","自动秘境","领取每日奖励","领取尘歌壶奖励" };

    /// <summary>
    /// 配置组配置
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ScriptGroup> _scriptGroups = [];

    /// <summary>
    /// 当前选中的配置组
    /// </summary>
    [ObservableProperty]
    private ScriptGroup? _selectedScriptGroup = null;

    public readonly string ScriptGroupPath = Global.Absolute(@"User\ScriptGroup");
    public readonly string LogPath = Global.Absolute(@"log");


    public override void OnNavigatedTo()
    {
        ReadScriptGroup();
    }

    public ScriptControlViewModel(ISnackbarService snackbarService, IScriptService scriptService)
    {
        _snackbarService = snackbarService;
        _scriptService = scriptService;
        ScriptGroups.CollectionChanged += ScriptGroupsCollectionChanged;
        WeakReferenceMessenger.Default.Register<PropertyChangedMessage<object>>(this, (_, msg) =>
        {
            if (msg.PropertyName == "AutoHoeingUnlocked")
                IsHoeingUnlocked = TaskSettingsPageViewModel.AutoHoeingUnlocked;
        });
    }
    
    public ScriptControlViewModel(ISnackbarService snackbarService, IScriptService scriptService,
        ObservableCollection<ScriptGroup> scriptGroups,ScriptGroup? selectedScriptGroup,bool isInsetMode)
    {
        _snackbarService = snackbarService;
        _scriptService = scriptService;
        ScriptGroups = scriptGroups;
        SelectedScriptGroup = selectedScriptGroup;
        _isInsetMode = isInsetMode;
        ScriptGroups.CollectionChanged += ScriptGroupsCollectionChanged;
        WeakReferenceMessenger.Default.Register<PropertyChangedMessage<object>>(this, (_, msg) =>
        {
            if (msg.PropertyName == "AutoHoeingUnlocked")
                IsHoeingUnlocked = TaskSettingsPageViewModel.AutoHoeingUnlocked;
        });
    }
    
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddTransient<ScriptControlViewModel>();
    }

    [RelayCommand]
    private void OnAddScriptGroup()
    {
        // 创建一个TextBox并设置自动聚焦
        var textBox = new System.Windows.Controls.TextBox()
        {
            VerticalAlignment = VerticalAlignment.Top
        };
        textBox.Loaded += (sender, e) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };
        var str = PromptDialog.Prompt("请输入配置组名称", "新增配置组", textBox);
        if (!string.IsNullOrEmpty(str))
        {
            // 检查是否已存在
            if (ScriptGroups.Any(x => x.Name == str))
            {
                _snackbarService.Show(
                    "配置组已存在",
                    $"配置组 {str} 已经存在，请勿重复添加",
                    ControlAppearance.Caution,
                    null,
                    TimeSpan.FromSeconds(2)
                );
            }
            else
            {
                ScriptGroups.Add(new ScriptGroup { Name = str });
            }
        }
    }

    [RelayCommand]
    private void ClearTasks()
    {
        // 确认？
        var result = ThemedMessageBox.Question("是否清空所有任务？", "清空任务", MessageBoxButton.YesNo, System.Windows.MessageBoxResult.No);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        if (SelectedScriptGroup == null)
        {
            return;
        }

        SelectedScriptGroup.Projects.Clear();
        WriteScriptGroup(SelectedScriptGroup);
    }

    [RelayCommand]
    private async Task OpenLogParse()
    {
        if (SelectedScriptGroup == null)
        {
            return;
        }

        GameInfo? gameInfo = null;
        var config = LogParse.LoadConfig();

        OtherConfig.Miyoushe mcfg = TaskContext.Instance().Config.OtherConfig.MiyousheConfig;
        if (mcfg.LogSyncCookie && !string.IsNullOrEmpty(mcfg.Cookie))
        {
            config.Cookie = mcfg.Cookie;
        }

        if (!string.IsNullOrEmpty(config.Cookie))
        {
            config.CookieDictionary.TryGetValue(config.Cookie, out gameInfo);
        }



        LogParseConfig.ScriptGroupLogParseConfig? sgpc;
        if (!config.ScriptGroupLogDictionary.TryGetValue(SelectedScriptGroup.Name, out sgpc))
        {
            sgpc = new LogParseConfig.ScriptGroupLogParseConfig();
        }


        // 创建 StackPanel
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10)
        };

        // 创建 ComboBox
        var rangeComboBox = new ComboBox
        {
            Width = 200,
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };
        var rangeComboBoxItems = new List<object>
        {
            new { Text = "当前配置组", Value = "CurrentConfig" },
            new { Text = "所有", Value = "All" }
        };
        rangeComboBox.DisplayMemberPath = "Text"; // 显示的文本
        rangeComboBox.SelectedValuePath = "Value"; // 绑定的值
        rangeComboBox.ItemsSource = rangeComboBoxItems;
        rangeComboBox.SelectedIndex = 0; // 默认选中第一个项
        stackPanel.Children.Add(rangeComboBox);


        var dayRangeComboBox = new ComboBox
        {
            Width = 200,
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };
        // 定义范围选项数据
        var dayRangeComboBoxItems = new List<object>
        {
            new { Text = "1天" , Value = "1" },
            new { Text = "3天", Value = "3" },
            new { Text = "7天", Value = "7" },
            new { Text = "15天", Value = "15" },
            new { Text = "31天", Value = "31" },
            new { Text = "61天", Value = "61" },
            new { Text = "92天", Value = "92" },
            new { Text = "所有", Value = "All" }
        };
        dayRangeComboBox.ItemsSource = dayRangeComboBoxItems;
        dayRangeComboBox.DisplayMemberPath = "Text"; // 显示的文本
        dayRangeComboBox.SelectedValuePath = "Value"; // 绑定的值
        dayRangeComboBox.SelectedIndex = 0;
        stackPanel.Children.Add(dayRangeComboBox);

        CheckBox mergerStatsSwitch = new CheckBox
        {
            Content = "合并相邻同名配置组",
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(mergerStatsSwitch);

        // 开关控件：ToggleButton 或 CheckBox
        CheckBox faultStatsSwitch = new CheckBox
        {
            Content = "异常情况统计",
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(faultStatsSwitch);

        // 开关控件：ToggleButton 或 CheckBox
        CheckBox hoeingStatsSwitch = new CheckBox
        {
            Content = "统计锄地摩拉怪物数",
            VerticalAlignment = VerticalAlignment.Center
        };

        CheckBox GenerateFarmingPlanData = new CheckBox
        {
            Content = "生成锄地规划数据",
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(GenerateFarmingPlanData);

        //firstRow.Children.Add(toggleSwitch);

        // 将第一行添加到 StackPanel
        stackPanel.Children.Add(hoeingStatsSwitch);

        // 第二行：文本框和“？”按钮
        StackPanel secondRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // 文本框
        TextBox cookieTextBox = new TextBox
        {
            Width = 200,
            Margin = new Thickness(0, 0, 10, 0)
        };
        secondRow.Children.Add(cookieTextBox);

        // “？”按钮
        Button questionButton = new Button
        {
            Content = "?",
            Width = 30,
            Height = 30
        };

        secondRow.Children.Add(questionButton);

        StackPanel threeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // 创建一个 TextBlock
        TextBlock hoeingDelayBlock = new TextBlock
        {
            Text = "锄地延时(秒)：",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16,
            Margin = new Thickness(0, 0, 10, 0)
        };


        TextBox hoeingDelayTextBox = new TextBox
        {
            Width = 100,
            FontSize = 16,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        threeRow.Children.Add(hoeingDelayBlock);
        threeRow.Children.Add(hoeingDelayTextBox);


        // 将第二行添加到 StackPanel
        stackPanel.Children.Add(secondRow);
        stackPanel.Children.Add(threeRow);
        //PrimaryButtonText
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "日志分析",
            Content = stackPanel,
            CloseButtonText = "取消",
            PrimaryButtonText = "确定",
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        uiMessageBox.SourceInitialized += (s, e) => WindowHelper.TryApplySystemBackdrop(uiMessageBox);

        void OnQuestionButtonOnClick(object sender, RoutedEventArgs args)
        {
            WebpageWindow cookieWin = new()
            {
                Title = "日志分析",
                Width = 800,
                Height = 600,
                Owner = uiMessageBox,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            cookieWin.NavigateToHtml(TravelsDiaryDetailManager.generHtmlMessage());
            cookieWin.Show();
        }

        questionButton.Click += OnQuestionButtonOnClick;

        //对象赋值
        rangeComboBox.SelectedValue = sgpc.RangeValue;
        dayRangeComboBox.SelectedValue = sgpc.DayRangeValue;
        cookieTextBox.Text = config.Cookie;
        hoeingStatsSwitch.IsChecked = sgpc.HoeingStatsSwitch;
        GenerateFarmingPlanData.IsChecked = sgpc.GenerateFarmingPlanData;
        faultStatsSwitch.IsChecked = sgpc.FaultStatsSwitch;
        mergerStatsSwitch.IsChecked = sgpc.MergerStatsSwitch;

        hoeingDelayTextBox.Text = sgpc.HoeingDelay;

        MessageBoxResult result = await uiMessageBox.ShowDialogAsync();


        if (result == MessageBoxResult.Primary)
        {
            string rangeValue = ((dynamic)rangeComboBox.SelectedItem).Value;
            string dayRangeValue = ((dynamic)dayRangeComboBox.SelectedItem).Value;
            string cookieValue = cookieTextBox.Text;

            //保存配置文件
            sgpc.DayRangeValue = dayRangeValue;
            sgpc.RangeValue = rangeValue;
            sgpc.HoeingStatsSwitch = hoeingStatsSwitch.IsChecked ?? false;
            sgpc.GenerateFarmingPlanData = GenerateFarmingPlanData.IsChecked ?? false;
            sgpc.FaultStatsSwitch = faultStatsSwitch.IsChecked ?? false;
            sgpc.MergerStatsSwitch = mergerStatsSwitch.IsChecked ?? false;
            sgpc.HoeingDelay = hoeingDelayTextBox.Text;

            config.Cookie = cookieValue;
            config.ScriptGroupLogDictionary[SelectedScriptGroup.Name] = sgpc;

            if (mcfg.LogSyncCookie && !string.IsNullOrEmpty(cookieValue))
            {
                mcfg.Cookie = cookieValue;
            }

            LogParse.WriteConfigFile(config);


            WebpageWindow win = new()
            {
                Title = "日志分析",
                Width = 800,
                Height = 600,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            void OnHtmlGenerationStatusChanged(string status)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Toast.Information(status, time: 5000);
                });
            }

            LogParse.HtmlGenerationStatusChanged += OnHtmlGenerationStatusChanged;
            Toast.Information("正在准备数据...");
            List<(string FileName, string Date)> fs = LogParse.GetLogFiles(LogPath);
            if (dayRangeValue != "All")
            {
                int n = int.Parse(dayRangeValue);
                if (n < fs.Count)
                {
                    fs = fs.GetRange(fs.Count - n, n);
                }
            }


            //最终确定是否打开锄地开关
            bool hoeingStats = false;

            if ((hoeingStatsSwitch.IsChecked ?? false) && string.IsNullOrEmpty(cookieValue))
            {
                Toast.Warning("未填写cookie，此次将不启用锄地统计！");
            }

            //真正存储的gameinfo
            GameInfo? realGameInfo = gameInfo;
            //统计锄地开关打开，并且不为cookie不为空
            if ((hoeingStatsSwitch.IsChecked ?? false) && !string.IsNullOrEmpty(cookieValue))
            {
                try
                {
                    Toast.Information("正在从米游社获取旅行札记数据，请耐心等待！");
                    gameInfo = await TravelsDiaryDetailManager.UpdateTravelsDiaryDetailManager(cookieValue);
                    Toast.Success($"米游社数据获取成功，开始进行解析，请耐心等待！");
                }
                catch (Exception)
                {
                    if (realGameInfo != null)
                    {
                        Toast.Warning("访问米游社接口异常，此次将锄地统计将不更新最新数据！");
                    }
                    else
                    {
                        Toast.Warning("访问米游社接口异常，此次将不启用锄地统计！");
                    }
                }
            }

            if (gameInfo != null)
            {
                realGameInfo = gameInfo;

                config.CookieDictionary[cookieValue] = realGameInfo;
                LogParse.WriteConfigFile(config);
            }

            if ((hoeingStatsSwitch.IsChecked ?? false) && realGameInfo != null)
            {
                hoeingStats = true;
            }

            var configGroupEntities = LogParse.ParseFile(fs);
            if (rangeValue == "CurrentConfig")
            {
                //Toast.Success(_selectedScriptGroup.Name);
                configGroupEntities = configGroupEntities.Where(item => SelectedScriptGroup.Name == item.Name).ToList();
            }

            if (configGroupEntities.Count == 0)
            {
                Toast.Warning("未解析出日志记录！");
                LogParse.HtmlGenerationStatusChanged -= OnHtmlGenerationStatusChanged;
            }
            else
            {
                configGroupEntities.Reverse();
                try
                {
                    // 生成HTML并加载
                    win.NavigateToHtml(LogParse.GenerHtmlByConfigGroupEntity(configGroupEntities,
                    hoeingStats ? realGameInfo : null, sgpc));
                    win.ShowDialog();
                    // 取消订阅事件
                    LogParse.HtmlGenerationStatusChanged -= OnHtmlGenerationStatusChanged;

                }
                catch (Exception ex)
                {
                    LogParse.HtmlGenerationStatusChanged -= OnHtmlGenerationStatusChanged;
                    Toast.Error($"生成日志分析时出错: {ex.Message}");
                }
            }
        }
    }

    static string[] GetJsonFiles(string folderPath)
    {
        // 检查文件夹是否存在
        if (!Directory.Exists(folderPath))
        {
            return new string[0];
        }

        // 获取所有 .json 文件
        return Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
    }

    [RelayCommand]
    public void OnOpenLocalScriptRepo()
    {
        TaskContext.Instance().Config.ScriptConfig.ScriptRepoHintDotVisible = false;
        ScriptRepoUpdater.Instance.OpenScriptRepoWindow();
    }
    
    [RelayCommand]
    private void OnOpenScriptsFolder(string directoryType)
    {
        
        string path = directoryType switch
        {
            "JS" => AppPaths.JsScripts,
            "DT" => AppPaths.MapTracks,
            "KM" => AppPaths.MouseScripts,
            _ => AppPaths.JsScripts,
        };
    
        Process.Start("explorer.exe", path);
    }

    [RelayCommand]
    private void UpdateTasks()
    {
        List<ScriptGroupProject> projects = new();
        List<ScriptGroupProject> oldProjects = new();
        oldProjects.AddRange(SelectedScriptGroup?.Projects ?? []);
        var oldcount = oldProjects.Count;
        List<string> folderNames = new();
        foreach (var project in oldProjects)
        {
            if (project.Type == "Pathing")
            {
                if (!folderNames.Contains(project.FolderName))
                {
                    folderNames.Add(project.FolderName);
                    //根据文件夹更新
                    var dirPath = $@"{MapPathingViewModel.PathJsonPath}\{project.FolderName}";
                    foreach (var jsonFile in GetJsonFiles(dirPath))
                    {
                        var fileInfo = new FileInfo(jsonFile);
                        var oldProject = oldProjects.FirstOrDefault(item => item.Name == fileInfo.Name);
                        if (oldProject == null)
                        {
                            projects.Add(ScriptGroupProject.BuildPathingProject(fileInfo.Name, project.FolderName));
                        }
                        else
                        {
                            projects.Add(oldProject);
                        }
                    }
                }
            }
            else
            {
                projects.Add(project);
            }
        }

        SelectedScriptGroup?.Projects.Clear();
        foreach (var scriptGroupProject in projects)
        {
            SelectedScriptGroup?.AddProject(scriptGroupProject);
        }

        Toast.Success($"增加了{projects.Count - oldcount}个地图追踪任务");
        if (SelectedScriptGroup != null) WriteScriptGroup(SelectedScriptGroup);
    }

    [RelayCommand]
    private void ReverseTaskOrder()
    {
        List<ScriptGroupProject> projects = new();
        projects.AddRange(SelectedScriptGroup?.Projects.Reverse() ?? []);
        SelectedScriptGroup?.Projects.Clear();
        projects.ForEach(item => SelectedScriptGroup?.Projects.Add(item));
        if (SelectedScriptGroup != null) WriteScriptGroup(SelectedScriptGroup);
    }
    [RelayCommand]
    private void ExportMergerJsons()
    {
        int count = 0;
        var pathDir = Path.Combine(LogPath, "exportMergerJson", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), "AutoPathing");
        foreach (var scriptGroupProject in SelectedScriptGroup?.Projects ?? [])
        {
            if (scriptGroupProject.Type == "Pathing")
            {
                var mergerJson = JsonMerger.getMergePathingJson(Path.Combine(MapPathingViewModel.PathJsonPath,
                    scriptGroupProject.FolderName, scriptGroupProject.Name));
                string fullPath = Path.Combine(pathDir, scriptGroupProject.FolderName, scriptGroupProject.Name);
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(fullPath, mergerJson);
                count++;
            }
        }
        if (count > 0)
        {
            Process.Start("explorer.exe", pathDir);
        }
    }


    [RelayCommand]
    public void AddScriptGroupNextFlag(ScriptGroup? item)
    {
        foreach (var scriptGroup in ScriptGroups)
        {
            scriptGroup.NextFlag = false;
        }

        if (item != null)
        {
            item.NextFlag = true;
            TaskContext.Instance().Config.NextScriptGroupName = item.Name;
        }
    }

    [RelayCommand]
    public void OnCopyScriptGroup(ScriptGroup? item)
    {
        if (item == null)
        {
            return;
        }

        var textBox = new System.Windows.Controls.TextBox()
        {
            VerticalAlignment = VerticalAlignment.Top
        };
        textBox.Loaded += (sender, e) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        var str = PromptDialog.Prompt("请输入配置组名称", "复制配置组", textBox, item.Name);
        if (!string.IsNullOrEmpty(str))
        {
            // 检查是否已存在
            if (ScriptGroups.Any(x => x.Name == str))
            {
                _snackbarService.Show(
                    "配置组已存在",
                    $"配置组 {str} 已经存在，复制失败",
                    ControlAppearance.Caution,
                    null,
                    TimeSpan.FromSeconds(2)
                );
            }
            else
            {
                var newScriptGroup = JsonSerializer.Deserialize<ScriptGroup>(JsonSerializer.Serialize(item));
                if (newScriptGroup != null)
                {
                    newScriptGroup.Name = str;
                    ScriptGroup.ResetGroupInfo(newScriptGroup);
                    foreach (var project in newScriptGroup.Projects)
                    {
                        try
                        {
                            project.BuildScriptProjectRelation();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    ScriptGroups.Add(newScriptGroup);
                }

                //WriteScriptGroup(newScriptGroup);
            }
        }
    }

    [RelayCommand]
    public void OnRenameScriptGroup(ScriptGroup? item)
    {
        if (item == null)
        {
            return;
        }

        var textBox = new System.Windows.Controls.TextBox()
        {
            VerticalAlignment = VerticalAlignment.Top
        };
        textBox.Loaded += (sender, e) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        var str = PromptDialog.Prompt("请输入配置组名称", "重命名配置组", textBox, item.Name);
        if (!string.IsNullOrEmpty(str))
        {
            if (item.Name == str)
            {
                Toast.Warning("新名称与旧名称相同");
                return;
            }

            // 检查是否已存在
            if (ScriptGroups.Any(x => x.Name == str))
            {
                _snackbarService.Show(
                    "配置组已存在",
                    $"配置组 {str} 已经存在，重命名失败",
                    ControlAppearance.Caution,
                    null,
                    TimeSpan.FromSeconds(2)
                );
            }
            else
            {
                var result = MessageBox.Show("所有名为 < " + item.Name + " > 的自定义秘境任务和配置单中的任务将重命名为 < " + str + " > ，是否继续？", 
                    "重名配置组关联修改", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }
                
                var ViewModel = new OneDragonFlowViewModel();
                ViewModel.InitConfigList();
                var configList = ViewModel.ConfigList;
                
               // 读取ConfigList中所有的配置单，检查每个配置单中的TaskEnabledList，如果含有和item.Name相同的配置组，则把这个TaskEnabledList中的配置组改为str
                foreach (var config in configList)
                {
                    var oldName = item.Name;
                    
                    if (config.CustomDomainList.Any(task => task == item.Name)) 
                    {   
                        for (int i = 0; i < config.CustomDomainList.Count; i++)
                        {
                            if (config.CustomDomainList[i] == oldName)
                            {
                                config.CustomDomainList[i] = str;
                            }
                        }
                        ViewModel.WriteConfig(config);
                    }
                    
                    // 使用反射检查和修改所有以 "DomainName" 结尾的属性
                    var properties = config.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(prop => prop.Name.EndsWith("DomainName") && prop.PropertyType == typeof(string));
                    
                    foreach (var prop in properties)
                    {
                        if (prop.GetValue(config) as string == oldName)
                        {
                            prop.SetValue(config, str);
                        }
                        ViewModel.WriteConfig(config);
                    }
                    
                    if (config.TaskEnabledList.Any(task => task.Value.Item2 == item.Name))
                    {
                        foreach (var task in config.TaskEnabledList)
                        {
                            if (task.Value.Item2 == oldName)
                            {
                                config.TaskEnabledList[task.Key] = (task.Value.Item1, str);
                            }
                        }
                        ViewModel.WriteConfig(config);
                    }
                }
                
                File.Move(Path.Combine(ScriptGroupPath, $"{item.Name}.json"), Path.Combine(ScriptGroupPath, $"{str}.json"));
                item.Name = str;
                if (item.NextFlag)
                {
                    TaskContext.Instance().Config.NextScriptGroupName = item.Name;
                }
                WriteScriptGroup(item);
            }
        }
    }

    [RelayCommand]
    public void OnDeleteScriptGroup(ScriptGroup? item)
    {
        if (item == null)
        {
            return;
        }
        
        //弹窗提示"配置单中的所有同名配置组将被删除，是否继续？，取消退出，确认执行删除操作"
        var result = MessageBox.Show("所有名为 < " + item.Name + " > 的自定义秘境任务和配置单的任务将被删除？", 
            "删除配置组关联修改", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }
        
        try
        {
            var ViewModel = new OneDragonFlowViewModel();
            ViewModel.InitConfigList();
            var configList = ViewModel.ConfigList;
            // 读取ConfigList中所有的配置单，检查每个配置单中的TaskEnabledList，如果含有和item.Name相同的配置组，则把这个TaskEnabledList中的配置组删除
            foreach (var config in configList)
            {
                var oldName = item.Name;
                // 删除 CustomDomainList 中的元素
                if (config.CustomDomainList.Any(task => task == oldName))
                {
                    for (int i = 0; i < config.CustomDomainList.Count; i++)
                    {
                        if (config.CustomDomainList[i] == oldName)
                        {
                            config.CustomDomainList.RemoveAt(i);
                            i--; // 调整索引以避免跳过元素
                        }
                    }
                    ViewModel.WriteConfig(config);
                }

                // 使用反射检查和修改所有以 "DomainName" 结尾的属性（删除）
                var propertiesToDelete = config.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(prop => prop.Name.EndsWith("DomainName") && prop.PropertyType == typeof(string));

                // 删除 DomainName 中的属性
                foreach (var prop in propertiesToDelete)
                {
                    if (prop.GetValue(config) as string == oldName)
                    {
                        prop.SetValue(config, string.Empty); 
                    }
                    ViewModel.WriteConfig(config);
                }
                
                if (config.TaskEnabledList.Any(task => task.Value.Item2 == item.Name))
                {
                    foreach (var task in config.TaskEnabledList)
                    {
                        if (task.Value.Item2 == oldName)
                        {
                            config.TaskEnabledList.Remove(task.Key);
                        }
                    }
                    ViewModel.WriteConfig(config);
                }
            }
            
            ScriptGroups.Remove(item);
            File.Delete(Path.Combine(ScriptGroupPath, $"{item.Name}.json"));
            _snackbarService.Show(
                "配置组删除成功",
                $"配置组 {item.Name} 已经被删除",
                ControlAppearance.Success,
                null,
                TimeSpan.FromSeconds(2)
            );
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "删除配置组配置时失败");
            _snackbarService.Show(
                "删除配置组配置失败",
                $"配置组 {item.Name} 删除失败！",
                ControlAppearance.Danger,
                null,
                TimeSpan.FromSeconds(3)
            );
        }
        if (ScriptGroups.Count() != 0)//如果删除的是当前选中的配置组，则清空选中项
        {
            SelectedScriptGroup = ScriptGroups.First();
        }else
        {
            SelectedScriptGroup = null;
        }
    }

    [RelayCommand]
    private void OnAddJsScript()
    {
        var list = LoadAllJsScriptProjects();
        var stackPanel = CreateJsScriptSelectionPanel(list, typeof(CheckBox));

        var result = PromptDialog.Prompt("请选择需要添加的JS脚本", "请选择需要添加的JS脚本", stackPanel, new Size(500, 600));
        if (!string.IsNullOrEmpty(result))
        {
            AddSelectedJsScripts((StackPanel)stackPanel.Content);
        }
    }

    internal static ScrollViewer CreateJsScriptSelectionPanel(List<ScriptProject> list, Type selectType)
    {
        var stackPanel = new StackPanel();

        var filterTextBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 10),
            PlaceholderText = "输入搜索条件...",
        };
        filterTextBox.TextChanged += delegate { ApplyJsScriptFilter(stackPanel, list, filterTextBox.Text, selectType); };
        stackPanel.Children.Add(filterTextBox);

        AddJsScriptsToPanel(stackPanel, list, filterTextBox.Text, selectType);

        var scrollViewer = new ScrollViewer
        {
            Content = stackPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 435 // 固定高度
        };

        return scrollViewer;
    }

    private static void ApplyJsScriptFilter(StackPanel parentPanel, List<ScriptProject> scripts, string filter, Type selectType)
    {
        if (parentPanel.Children.Count > 0)
        {
            List<UIElement> removeElements = new List<UIElement>();
            foreach (UIElement parentPanelChild in parentPanel.Children)
            {
                if (parentPanelChild is FrameworkElement frameworkElement && frameworkElement.Name.StartsWith("dynamic_"))
                {
                    removeElements.Add(frameworkElement);
                }
            }

            removeElements.ForEach(parentPanel.Children.Remove);
        }

        AddJsScriptsToPanel(parentPanel, scripts, filter, selectType);
    }

    private static void AddJsScriptsToPanel(StackPanel parentPanel, List<ScriptProject> scripts, string filter, Type selectType)
    {
        foreach (var script in scripts)
        {
            var displayText = script.FolderName + " - " + script.Manifest.Name;

            if (!string.IsNullOrEmpty(filter) &&
                !displayText.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !script.FolderName.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !script.Manifest.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (selectType == typeof(CheckBox))
            {
                var checkBox = new CheckBox
                {
                    Content = displayText,
                    Tag = script.FolderName,
                    Margin = new Thickness(0, 2, 0, 2),
                    Name = "dynamic_" + Guid.NewGuid().ToString().Replace("-", "_")
                };
                parentPanel.Children.Add(checkBox);
            }
            else if (selectType == typeof(RadioButton))
            {
                var radioButton = new RadioButton
                {
                    Content = displayText,
                    Tag = script.FolderName,
                    Margin = new Thickness(0, 2, 0, 2),
                    Name = "dynamic_" + Guid.NewGuid().ToString().Replace("-", "_"),
                    GroupName = "JsScriptsRadioButtonGroup"
                };
                parentPanel.Children.Add(radioButton);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void AddSelectedJsScripts(StackPanel stackPanel)
    {
        foreach (var child in stackPanel.Children)
        {
            if (child is CheckBox { IsChecked: true } checkBox && checkBox.Tag is string folderName)
            {
                SelectedScriptGroup?.AddProject(new ScriptGroupProject(new ScriptProject(folderName)));
            }
        }
    }

    [RelayCommand]
    private void OnAddKmScript()
    {
        var list = LoadAllKmScripts();
        var combobox = new ComboBox
        {
            VerticalAlignment = VerticalAlignment.Top
        };

        foreach (var fileInfo in list)
        {
            combobox.Items.Add(fileInfo.Name);
        }

        var str = PromptDialog.Prompt("请选择需要添加的键鼠脚本", "请选择需要添加的键鼠脚本", combobox);
        if (!string.IsNullOrEmpty(str))
        {
            SelectedScriptGroup?.AddProject(ScriptGroupProject.BuildKeyMouseProject(str));
        }
    }

    [RelayCommand]
    private void OnAddShell()
    {
        var str = PromptDialog.Prompt("执行 shell 操作存在极大风险！请勿输入你看不懂的指令！以免引发安全隐患并损坏系统！\n执行 shell 的时候，游戏可能会失去焦点", "请输入需要执行的shell");
        if (!string.IsNullOrEmpty(str))
        {
            SelectedScriptGroup?.AddProject(ScriptGroupProject.BuildShellProject(str));
        }
    }

    [RelayCommand]
    private void OnAddSoloTask()
    {
        if (!TaskSettingsPageViewModel.AutoHoeingUnlocked)
        {
            Toast.Warning("独立任务未解锁，请先在独立任务设置页面解锁");
            return;
        }
        var tasks = GameTask.SoloTaskRegistry.AvailableTasks;
        var combobox = new System.Windows.Controls.ComboBox
        {
            ItemsSource = tasks,
            SelectedIndex = 0
        };
        var str = PromptDialog.Prompt("请选择需要添加的独立任务", "请选择需要添加的独立任务", combobox);
        if (!string.IsNullOrEmpty(str))
        {
            SelectedScriptGroup?.AddProject(ScriptGroupProject.BuildSoloTaskProject(str));
        }
    }

    [RelayCommand]
    private async Task OnAddPathing()
    {
        try
        {
            // 在后台线程中加载数据
            var root = await Task.Run(() => FileTreeNodeHelper.LoadDirectory<PathingTask>(MapPathingViewModel.PathJsonPath));

            // 异步创建选择面板
            var stackPanel = await CreatePathingScriptSelectionPanelAsync(root.Children);

            // 显示选择对话框
            var result = PromptDialog.Prompt(
                "请选择需要添加的地图追踪任务",
                "请选择需要添加的地图追踪任务",
                stackPanel,
                new Size(600, 720),
                new PromptDialogConfig
                {
                    DisableAutoTranslate = true
                });

            if (!string.IsNullOrEmpty(result))
            {
                AddSelectedPathingScripts((StackPanel)stackPanel.Content);
            }
        }
        catch (Exception ex)
        {
            Toast.Error($"加载地图追踪任务失败: {ex.Message}");
            _logger.LogError(ex, "加载地图追踪任务时发生错误");
        }
    }

    // 添加防抖计时器字段
    private DispatcherTimer? _debounceTimer;
    private const int DebounceDelayMs = 300;

    // 存储路径与UI元素的映射
    private readonly Dictionary<string, FrameworkElement> _nodeUIElements = [];

    /// <summary>
    /// 异步创建地图追踪任务选择面板
    /// </summary>
    private async Task<ScrollViewer> CreatePathingScriptSelectionPanelAsync(IEnumerable<FileTreeNode<PathingTask>> list)
    {
        var stackPanel = new StackPanel();
        CheckBox excludeCheckBox = new()
        {
            Content = "排除已选择过的目录",
            VerticalAlignment = VerticalAlignment.Center,
        };
        CheckBox deepCheckBox = new()
        {
            Content = "深度搜索",
            VerticalAlignment = VerticalAlignment.Center,
        };
        TextBox filterTextBox = new()
        {
            Margin = new Thickness(0, 0, 0, 10),
            PlaceholderText = "输入筛选条件...",
        };

        // 初始化防抖计时器
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DebounceDelayMs)
        };

        excludeCheckBox.Click += delegate
        {
            _ = ApplyFilterToExistingNodesAsync(list, filterTextBox.Text, excludeCheckBox.IsChecked, deepCheckBox.IsChecked);
        };
        deepCheckBox.Click += delegate
        {
            _ = ApplyFilterToExistingNodesAsync(list, filterTextBox.Text, excludeCheckBox.IsChecked, deepCheckBox.IsChecked);
        };
        filterTextBox.TextChanged += delegate
        {
            _debounceTimer.Stop();

            // 设置计时器回调
            _debounceTimer.Tick -= OnDebounceTimerTick;
            _debounceTimer.Tick += OnDebounceTimerTick;

            _debounceTimer.Start();
            void OnDebounceTimerTick(object? sender, EventArgs e)
            {
                _debounceTimer.Stop();
                _debounceTimer.Tick -= OnDebounceTimerTick;
                _ = ApplyFilterToExistingNodesAsync(list, filterTextBox.Text, excludeCheckBox.IsChecked, deepCheckBox.IsChecked);
            }
        };

        stackPanel.Children.Add(excludeCheckBox);
        stackPanel.Children.Add(deepCheckBox);
        stackPanel.Children.Add(filterTextBox);

        // 异步构建UI树
        await BuildCompleteUITreeAsync(stackPanel, list, 0);

        filterTextBox.Focus();

        var scrollViewer = new ScrollViewer
        {
            Content = stackPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        return scrollViewer;
    }

    /// <summary>
    /// 异步构建完整的UI树
    /// </summary>
    /// <param name="parentPanel">构建内容的父容器</param>
    /// <param name="nodes">要构建的节点集合</param>
    /// <param name="depth">构建的深度</param>
    private async Task BuildCompleteUITreeAsync(StackPanel parentPanel, IEnumerable<FileTreeNode<PathingTask>>? nodes, int depth)
    {
        if (nodes == null)
            return;

        var nodeList = nodes.ToList();

        for (int i = 0; i < nodeList.Count; i += 1)
        {
            var batch = nodeList.Skip(i).Take(1);

            // 在UI线程中创建UI元素并添加到面板
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                foreach (var node in batch)
                {
                    var element = CreateUIElementForNode(node, depth);
                    parentPanel.Children.Add(element);

                    // 如果是目录且有子节点，递归构建子节点
                    if (node.IsDirectory && node.Children?.Any() == true && element is Expander expander)
                    {
                        if (expander.Content is StackPanel childPanel)
                        {
                            await BuildCompleteUITreeAsync(childPanel, node.Children, depth + 1);
                        }
                    }
                }
            }, DispatcherPriority.Background);

            // 让出控制权，避免长时间阻塞UI线程
            await Task.Delay(1);
        }
    }
    
    /// <summary>
    /// 为单个节点创建UI元素
    /// </summary>
    /// <param name="node">文件树节点</param>
    /// <param name="depth">节点深度</param>
    /// <returns>创建的UI元素</returns>
    private FrameworkElement CreateUIElementForNode(FileTreeNode<PathingTask> node, int depth)
    {
        var checkBox = new CheckBox
        {
            Content = node.FileName,
            Tag = node.FilePath,
            Margin = new Thickness(depth * 30, 0, 0, 0),
            Name = "dynamic_" + Guid.NewGuid().ToString().Replace("-", "_")
        };

        // 存储路径与UI元素的映射
        if (!string.IsNullOrEmpty(node.FilePath))
            _nodeUIElements[node.FilePath] = checkBox;

        if (node.IsDirectory)
        {
            // 如果父节点没有任何子内容，则不可勾选
            if (node.Children == null || node.Children.Count == 0)
                checkBox.IsEnabled = false;

            var childPanel = new StackPanel();
            checkBox.IsThreeState = true;
            var expander = new Expander
            {
                Header = checkBox,
                Content = childPanel,
                IsExpanded = false,
                Name = "dynamic_" + Guid.NewGuid().ToString().Replace("-", "_"),
                Visibility = Visibility.Visible
            };

            // 存储路径与UI元素的映射
            if (!string.IsNullOrEmpty(node.FilePath))
            {
                _nodeUIElements[node.FilePath + "_expander"] = expander;
            }

            // 修改事件处理：用户点击时只在全选和全不选之间切换
            checkBox.Click += (s, e) => HandleDirectoryCheckBoxClick(checkBox, childPanel);

            return expander;
        }
        else
        {
            // 为文件复选框添加状态改变事件，用于更新父级状态
            checkBox.Checked += (s, e) => UpdateParentCheckBoxState(checkBox);
            checkBox.Unchecked += (s, e) => UpdateParentCheckBoxState(checkBox);

            return checkBox;
        }
    }

    /// <summary>
    /// 异步应用筛选到已存在的节点
    /// <param name="nodes">要筛选的节点集合</param>
    /// <param name="filter">用户输入的筛选关键词</param>
    /// <param name="excludeSelectedFolder">排除选择的目录</param>
    /// <param name="isDeepSearch">深度搜索功能</param>
    /// </summary>
    private async Task ApplyFilterToExistingNodesAsync(IEnumerable<FileTreeNode<PathingTask>> nodes, string filter, bool? excludeSelectedFolder = false, bool? isDeepSearch = false)
    {
        var filteredResult = await Task.Run(() =>
        {
            IEnumerable<FileTreeNode<PathingTask>> filteredNodes = nodes;

            // 如果启用排除已选择过的目录，先过滤掉这些目录
            if (excludeSelectedFolder ?? false)
            {
                List<string> skipFolderNames = SelectedScriptGroup?.Projects.ToList().Select(item => item.FolderName).Distinct().ToList() ?? [];
                string jsonString = JsonSerializer.Serialize(nodes);
                var copiedNodes = JsonSerializer.Deserialize<ObservableCollection<FileTreeNode<PathingTask>>>(jsonString);
                if (copiedNodes != null)
                {
                    copiedNodes = FileTreeNodeHelper.FilterTree(copiedNodes, skipFolderNames);
                    copiedNodes = FileTreeNodeHelper.FilterEmptyNodes(copiedNodes);
                    filteredNodes = copiedNodes;
                }
            }

            return filteredNodes;
        });

        // 在UI线程中更新可见性
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // 重置所有节点的可见性
            foreach (var element in _nodeUIElements.Values)
                element.Visibility = Visibility.Collapsed;

            UpdateNodesVisibility(filteredResult, filter, isDeepSearch);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 更新节点的可见性和展开状态
    /// </summary>
    /// <param name="nodes">要处理的文件树节点集合</param>
    /// <param name="filter">用户输入的筛选关键词</param>
    /// <param name="isDeepSearch">是否启用深度搜索</param>
    /// <param name="depth">当前节点在树中的深度级别</param>
    /// <param name="parentMatched">当前节点的父级是否已经匹配筛选条件</param>
    /// <param name="returnMatchStatus">是否返回匹配状态（用于子节点处理）</param>
    /// <returns>returnMatchStatus则返回是否包含匹配的节点</returns>
    private bool UpdateNodesVisibility(IEnumerable<FileTreeNode<PathingTask>> nodes, string filter, bool? isDeepSearch, int depth = 0, bool parentMatched = false, bool returnMatchStatus = false)
    {
        bool containsMatch = false;

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.FilePath))
                continue;

            bool nodeMatches = !string.IsNullOrEmpty(filter) && IsNodeMatched(node, filter);
            bool shouldShow = ShouldShowNode(node, filter, isDeepSearch, depth, parentMatched);

            // 更新节点可见性
            if (_nodeUIElements.TryGetValue(node.FilePath, out var element))
            {
                element.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                if (shouldShow && nodeMatches && returnMatchStatus)
                    containsMatch = true;
            }

            // 如果是目录节点，递归处理子节点并更新展开状态
            if (node.IsDirectory && _nodeUIElements.TryGetValue(node.FilePath + "_expander", out var expanderElement) && expanderElement is Expander expander)
            {
                if (shouldShow)
                {
                    // 递归处理子节点，传入returnMatchStatus = true来获取子节点匹配状态
                    bool childContainsMatch = UpdateNodesVisibility(node.Children, filter, isDeepSearch, depth + 1, nodeMatches || parentMatched, true);

                    // 如果子节点包含匹配且需要返回匹配状态，当前层级也标记为包含匹配
                    if (childContainsMatch && returnMatchStatus)
                        containsMatch = true;

                    expander.IsExpanded = ShouldExpandNode(filter, nodeMatches, parentMatched, childContainsMatch, depth, isDeepSearch, GetParentFolderName(node));
                    expander.Visibility = Visibility.Visible;
                }
                else
                {
                    expander.Visibility = Visibility.Collapsed;
                }
            }
        }

        return containsMatch;
    }

    /// <summary>
    /// 该节点是否应该显示
    /// </summary>
    /// <param name="node">要检查的节点</param>
    /// <param name="filter">筛选条件</param>
    /// <param name="isDeepSearch">是否启用深度搜索</param>
    /// <param name="currentDepth">当前深度</param>
    /// <param name="parentMatched">父节点是否已匹配</param>
    /// <returns>是否应该显示该节点</returns>
    private static bool ShouldShowNode(FileTreeNode<PathingTask> node, string filter, bool? isDeepSearch = false, int currentDepth = 0, bool parentMatched = false)
    {
        // 如果没有筛选条件，显示所有节点
        if (string.IsNullOrEmpty(filter))
            return true;

        // 如果该节点任意层级父节点已匹配，则忽略深度限制显示其全部子内容
        if (parentMatched)
            return true;

        bool currentNodeMatches = IsNodeMatched(node, filter);

        // 如果该节点匹配，显示该节点
        if (currentNodeMatches)
            return true;

        // 不超过允许深度的前提下，递归目录节点，逐一判断其所有子节点是否应该显示
        if (currentDepth >= GetMaxDepth(isDeepSearch, GetParentFolderName(node)))
            return false;

        if (node.IsDirectory && node.Children?.Any() == true)
        {
            foreach (var child in node.Children)
            {
                // 递归时，传递当前节点的匹配状态，任意当前深度的节点应该显示，则当前节点也应该显示
                if (ShouldShowNode(child, filter, isDeepSearch, currentDepth + 1, currentNodeMatches))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 该节点是否匹配
    /// </summary>
    /// <param name="node">要检查的节点</param>
    /// <param name="filter">筛选条件</param>
    /// <returns>是否匹配</returns>
    private static bool IsNodeMatched(FileTreeNode<PathingTask> node, string filter)
    {
        // 该节点名称是否匹配
        if (node.FileName?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // 往前追溯，该节点路径中是否至少有一段匹配
        if (!string.IsNullOrEmpty(node.FilePath))
        {
            var relativePath = Path.GetRelativePath(MapPathingViewModel.PathJsonPath, node.FilePath);
            var pathSegments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // 处理路径段匹配，对于文件名需要去除扩展名
            foreach (var segment in pathSegments)
            {
                // 如果这是最后一个段且不是目录，则去除扩展名后匹配
                var segmentToMatch = segment;
                if (segment == pathSegments.Last() && !node.IsDirectory)
                {
                    segmentToMatch = Path.GetFileNameWithoutExtension(segment);
                }

                if (segmentToMatch.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 该节点是否应该自动展开
    /// </summary>
    /// <param name="filter">筛选条件</param>
    /// <param name="currentNodeMatches">当前节点是否匹配</param>
    /// <param name="parentMatched">父节点是否已匹配</param>
    /// <param name="childContainsMatch">子树是否包含匹配</param>
    /// <param name="depth">当前深度</param>
    /// <param name="isDeepSearch">是否启用深度搜索</param>
    /// <param name="parentFolderName">父文件夹名称</param>
    /// <returns>是否应该展开</returns>
    private static bool ShouldExpandNode(string filter, bool currentNodeMatches, bool parentMatched, bool childContainsMatch, int depth, bool? isDeepSearch, string? parentFolderName)
    {
        // 如果没有筛选条件（输入框为空），所有节点都不展开
        if (string.IsNullOrEmpty(filter))
            return false;

        // 如果该节点的父节点已匹配，子目录不再展开，便于浏览
        if (parentMatched)
            return false;

        // 该节点的深度大于等于深度限制时，不再展开
        if (depth >= GetMaxDepth(isDeepSearch, parentFolderName))
            return false;

        // 该节点名称直接匹配，自动展开
        if (!string.IsNullOrEmpty(filter) && currentNodeMatches)
            return true;

        // 该节点的子树中存在至少一个匹配的节点，自动展开该节点以显示深层匹配节点
        if (childContainsMatch)
            return true;

        return false;
    }

    /// <summary>
    /// 获取该节点的父文件夹名称
    /// </summary>
    /// <param name="node">节点</param>
    /// <returns>父文件夹名称</returns>
    private static string? GetParentFolderName(FileTreeNode<PathingTask> node)
    {
        // 如果节点没有文件路径，返回 null
        if (string.IsNullOrEmpty(node.FilePath))
            return null;

        // 获取相对于 PathJsonPath 的路径
        var relativePath = Path.GetRelativePath(MapPathingViewModel.PathJsonPath, node.FilePath);
        var pathSegments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 返回第一级目录名称
        return pathSegments.Length > 0 ? pathSegments[0] : null;
    }

    /// <summary>
    /// 获取允许的最大深度
    /// </summary>
    /// <param name="isDeepSearch">是否启用深度搜索</param>
    /// <param name="parentFolderName">父文件夹文件名内容</param>
    /// <param name="currentNodeMatched">当前节点是否匹配</param>
    /// <param name="parentMatched">父节点是否已匹配</param>
    /// <returns>允许的深度</returns>
    private static int GetMaxDepth(bool? isDeepSearch, string? parentFolderName, bool currentNodeMatched = false, bool parentMatched = false)
    {
        // 如果开启深度搜索，允许全部子内容
        if (isDeepSearch == true)
            return int.MaxValue;

        // 如果当前节点匹配或父节点已匹配，允许全部子内容
        if (currentNodeMatched || parentMatched)
            return int.MaxValue;

        int defaultDepth = 1;

        // 特殊目录的深度扩展
        if (parentFolderName == "地方特产")
            return defaultDepth + 1;

        return defaultDepth;
    }

    /// <summary>
    /// 处理目录复选框点击事件
    /// </summary>
    /// <param name="checkBox">被点击的目录复选框</param>
    /// <param name="childPanel">子面板</param>
    private void HandleDirectoryCheckBoxClick(CheckBox checkBox, StackPanel childPanel)
    {
        var childCheckBoxes = GetAllChildCheckBoxes(childPanel);

        // 判断目标状态：如果所有子项都已选中，则全不选；否则全选
        bool allChildrenChecked = childCheckBoxes.Count > 0 && childCheckBoxes.All(cb => cb.IsChecked == true);
        bool targetState = !allChildrenChecked;

        checkBox.IsChecked = targetState;
        SetChildCheckBoxesState(childPanel, targetState);
        UpdateParentCheckBoxState(checkBox);
    }

    /// <summary>
    /// 递归获取面板中所有的子复选框
    /// </summary>
    /// <param name="panel">获取的面板</param>
    /// <returns>所有子复选框列表</returns>
    private List<CheckBox> GetAllChildCheckBoxes(StackPanel panel)
    {
        var checkBoxes = new List<CheckBox>();

        foreach (var child in panel.Children)
        {
            if (child is CheckBox checkBox)
            {
                checkBoxes.Add(checkBox);
            }
            else if (child is Expander expander)
            {
                if (expander.Header is CheckBox headerCheckBox)
                {
                    checkBoxes.Add(headerCheckBox);
                }

                if (expander.Content is StackPanel nestedPanel)
                {
                    checkBoxes.AddRange(GetAllChildCheckBoxes(nestedPanel));
                }
            }
        }

        return checkBoxes;
    }

    /// <summary>
    /// 更新父级复选框的三态状态
    /// </summary>
    /// <param name="changedCheckBox">状态发生改变的复选框</param>
    private void UpdateParentCheckBoxState(CheckBox changedCheckBox)
    {
        // 查找父级复选框
        var parentCheckBox = FindParentCheckBox(changedCheckBox);
        if (parentCheckBox == null)
            return;

        // 获取同级所有复选框
        var siblingCheckBoxes = GetSiblingCheckBoxes(changedCheckBox);

        // 计算状态
        int checkedCount = siblingCheckBoxes.Count(cb => cb.IsChecked == true);
        int uncheckedCount = siblingCheckBoxes.Count(cb => cb.IsChecked == false);
        int indeterminateCount = siblingCheckBoxes.Count(cb => cb.IsChecked == null);

        // 设置父级复选框状态
        if (checkedCount == siblingCheckBoxes.Count)
            parentCheckBox.IsChecked = true;
        else if (uncheckedCount == siblingCheckBoxes.Count)
            parentCheckBox.IsChecked = false;
        else
            parentCheckBox.IsChecked = null;

        // 递归更新上级父级
        UpdateParentCheckBoxState(parentCheckBox);
    }

    /// <summary>
    /// 查找指定复选框的父级复选框
    /// </summary>
    /// <param name="checkBox">当前复选框</param>
    /// <returns>父级复选框，如果没有则返回null</returns>
    private CheckBox? FindParentCheckBox(CheckBox checkBox)
    {
        var filePath = checkBox.Tag as string;
        if (string.IsNullOrEmpty(filePath))
            return null;

        // 获取父目录路径
        var parentPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(parentPath))
            return null;

        // 查找父级复选框
        if (_nodeUIElements.TryGetValue(parentPath, out var parentElement) && parentElement is CheckBox parentCheckBox)
        {
            return parentCheckBox;
        }

        return null;
    }

    /// <summary>
    /// 获取同级的所有复选框
    /// </summary>
    /// <param name="checkBox">当前复选框</param>
    /// <returns>同级复选框列表</returns>
    private static List<CheckBox> GetSiblingCheckBoxes(CheckBox checkBox)
    {
        // 先尝试获取逻辑父级
        var parent = LogicalTreeHelper.GetParent(checkBox) as FrameworkElement;

        // 如果逻辑父级不存在，尝试可视化父级
        parent ??= VisualTreeHelper.GetParent(checkBox) as FrameworkElement;

        // 如果当前元素是Expander的Header，获取Expander的父级
        if (parent is Expander expander)
        {
            parent = LogicalTreeHelper.GetParent(expander) as FrameworkElement ??
                     VisualTreeHelper.GetParent(expander) as FrameworkElement;
        }

        // 遍历同级元素
        var siblings = new List<CheckBox>();
        if (parent is StackPanel stackPanel)
        {
            foreach (var child in stackPanel.Children)
            {
                if (child is CheckBox siblingCheckBox)
                    siblings.Add(siblingCheckBox);
                else if (child is Expander childExpander && childExpander.Header is CheckBox expanderCheckBox)
                    siblings.Add(expanderCheckBox);
            }
        }

        return siblings;
    }

    /// <summary>
    /// 递归设置子复选框状态
    /// </summary>
    /// <param name="childStackPanel">子面板</param>
    /// <param name="state">目标状态</param>
    private static void SetChildCheckBoxesState(StackPanel childStackPanel, bool state)
    {
        foreach (var child in childStackPanel.Children)
        {
            if (child is CheckBox checkBox)
            {
                checkBox.IsChecked = state;
            }
            else if (child is Expander expander && expander.Content is StackPanel nestedStackPanel)
            {
                if (expander.Header is CheckBox headerCheckBox)
                {
                    headerCheckBox.IsChecked = state;
                }

                SetChildCheckBoxesState(nestedStackPanel, state);
            }
        }
    }

    private void AddSelectedPathingScripts(StackPanel stackPanel)
    {
        foreach (var child in stackPanel.Children)
        {
            if (child is CheckBox { IsChecked: true } checkBox && checkBox.Tag is string filePath)
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                {
                    var relativePath = Path.GetRelativePath(MapPathingViewModel.PathJsonPath, fileInfo.Directory!.FullName);
                    SelectedScriptGroup?.AddProject(ScriptGroupProject.BuildPathingProject(fileInfo.Name, relativePath));
                }
            }
            else if (child is Expander { Content: StackPanel nestedStackPanel })
            {
                AddSelectedPathingScripts(nestedStackPanel);
            }
        }
    }

    // private Dictionary<string, List<FileInfo>> LoadAllPathingScripts()
    // {
    //     var folder = Global.Absolute(@"User\AutoPathing");
    //     var directories = Directory.GetDirectories(folder);
    //     var result = new Dictionary<string, List<FileInfo>>();
    //
    //     foreach (var directory in directories)
    //     {
    //         var dirInfo = new DirectoryInfo(directory);
    //         var files = dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly).ToList();
    //         result.Add(dirInfo.Name, files);
    //     }
    //
    //     return result;
    // }

    internal static List<ScriptProject> LoadAllJsScriptProjects()
    {
        var path = Global.ScriptPath();
        Directory.CreateDirectory(path);
        // 获取所有脚本项目
        var projects = Directory.GetDirectories(path)
            .Select(x =>
            {
                try
                {
                    return new ScriptProject(Path.GetFileName(x));
                }
                catch (Exception e)
                {
                    Toast.Warning($"加载单个脚本失败：{e.Message}");
                    return null;
                }
            })
            .Where(x => x != null)
            .ToList();
        return projects;
    }

    private List<FileInfo> LoadAllKmScripts()
    {
        var folder = Global.Absolute(@"User\KeyMouseScript");
        Directory.CreateDirectory(folder);
        // 获取所有脚本项目
        var files = Directory.GetFiles(folder, "*.*",
            SearchOption.AllDirectories);

        return files.Select(file => new FileInfo(file)).ToList();
    }

    [RelayCommand]
    public void OnEditScriptCommon(ScriptGroupProject? item)
    {
        if (item == null)
        {
            return;
        }

        ShowEditWindow(item);

        // foreach (var group in ScriptGroups)
        // {
        //     WriteScriptGroup(group);
        // }
    }

    [RelayCommand]
    private void AddNextFlag(ScriptGroupProject? item)
    {
        if (item == null || SelectedScriptGroup == null)
        {
            return;
        }

        List<ValueTuple<string, int, string, string>> nextScheduledTask = TaskContext.Instance().Config.NextScheduledTask;
        var nst = nextScheduledTask.Find(item2 => item2.Item1 == SelectedScriptGroup?.Name);
        if (nst != default)
        {
            nextScheduledTask.Remove(nst);
        }

        nextScheduledTask.Add((SelectedScriptGroup?.Name ?? "", item.Index, item.FolderName, item.Name));
        foreach (var item1 in SelectedScriptGroup?.Projects ?? [])
        {
            item1.NextFlag = false;
        }

        item.NextFlag = true;
    }

    public static void ShowEditWindow(ScriptGroupProject project)
    {
        var viewModel = new ScriptGroupProjectEditorViewModel(project);
        var editor = new ScriptGroupProjectEditor(project)
        {
            DataContext = viewModel
        };
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "修改通用设置",
            Content = editor,
            CloseButtonText = "关闭",
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        uiMessageBox.ShowDialogAsync();
    }

    [RelayCommand]
    public void OnEditJsScriptSettings(ScriptGroupProject? item)
    {
        if (item == null)
        {
            return;
        }

        if (item.Project == null)
        {
            item.BuildScriptProjectRelation();
        }

        if (item.Project == null)
        {
            return;
        }

        if (item.Type == "Javascript")
        {
            if (item.JsScriptSettingsObject == null)
            {
                item.JsScriptSettingsObject = new ExpandoObject();
            }

            var ui = item.Project.LoadSettingUi(item.JsScriptSettingsObject);
            if (ui == null)
            {
                Toast.Warning("此脚本未提供自定义配置");
                return;
            }

            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "修改JS脚本自定义设置    ",
                Content = ui,
                CloseButtonText = "关闭",
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            AutoTranslateInterceptor.SetEnableAutoTranslate(uiMessageBox, false);
            uiMessageBox.ShowDialogAsync();

            // 由于 JsScriptSettingsObject 的存在，这里只能手动再次保存配置
            foreach (var group in ScriptGroups)
            {
                WriteScriptGroup(group);
            }
        }
        else
        {
            Toast.Warning("只有JS脚本才有自定义配置");
        }
    }

    [RelayCommand]
    public void OnEditSoloTaskSettings(ScriptGroupProject? item)
    {
        if (item == null || item.Type != "SoloTask") return;

        item.SoloTaskSettingsObject ??= new Dictionary<string, object?>();

        // 锄地一条龙：统一弹窗，顶部有单机/联机切换开关
        if (item.Name == "锄地一条龙")
        {
            ShowHoeingSettingsDialog(item);
            return;
        }

        var settingItems = GameTask.SoloTaskRegistry.GetSettingItems(item.Name);
        if (settingItems.Count == 0)
        {
            Toast.Warning("此独立任务没有可配置的参数");
            return;
        }

        var stackPanel = new StackPanel { Margin = new Thickness(10) };

        var controls = new Dictionary<string, FrameworkElement>();
        foreach (var setting in settingItems)
        {
            var label = new TextBlock
            {
                Text = setting.Label,
                Margin = new Thickness(0, 8, 0, 2),
                FontSize = 14
            };
            stackPanel.Children.Add(label);

            // 获取当前值：优先用覆盖值，否则用默认值
            object? currentValue = item.SoloTaskSettingsObject.TryGetValue(setting.Name, out var ov)
                ? ov : setting.DefaultValue;

            if (setting.Type == "select" && setting.Options != null)
            {
                var currentStr = currentValue?.ToString() ?? "";
                var combo = new System.Windows.Controls.ComboBox
                {
                    ItemsSource = setting.Options,
                    SelectedItem = currentStr,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stackPanel.Children.Add(combo);
                controls[setting.Name] = combo;
            }
            else if (setting.Type == "bool")
            {
                var check = new System.Windows.Controls.CheckBox
                {
                    IsChecked = currentValue is true or "True" or "true",
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stackPanel.Children.Add(check);
                controls[setting.Name] = check;
            }
            else if (setting.Type == "number")
            {
                var numberBox = new Wpf.Ui.Controls.NumberBox
                {
                    Minimum = 0,
                    Maximum = 999999,
                    SpinButtonPlacementMode = Wpf.Ui.Controls.NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                // 尝试从当前值设置（使用 DefaultValue 作为 fallback）
                double parsedValue;
                if (currentValue is double d2)
                    parsedValue = d2;
                else if (currentValue is int i2)
                    parsedValue = i2;
                else if (currentValue is long l2)
                    parsedValue = l2;
                else if (double.TryParse(currentValue?.ToString(), out var parsed2))
                    parsedValue = parsed2;
                else
                    parsedValue = Convert.ToDouble(setting.DefaultValue);
                numberBox.Value = parsedValue;

                stackPanel.Children.Add(numberBox);
                controls[setting.Name] = numberBox;
            }
            else
            {
                var textBox = new TextBox
                {
                    Text = currentValue?.ToString() ?? "",
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stackPanel.Children.Add(textBox);
                controls[setting.Name] = textBox;
            }
        }

        var dialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = $"修改独立任务配置 - {item.Name}",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 500
            },
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        dialog.ShowDialogAsync().ContinueWith(t =>
        {
            if (t.Result == MessageBoxResult.Primary)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var setting in settingItems)
                    {
                        if (!controls.TryGetValue(setting.Name, out var ctrl)) continue;

                        object? value = ctrl switch
                        {
                            System.Windows.Controls.ComboBox combo => combo.SelectedItem?.ToString(),
                            System.Windows.Controls.CheckBox check => check.IsChecked ?? false,
                            Wpf.Ui.Controls.NumberBox nb => setting.Type == "number" ? nb.Value : nb.Text,
                            TextBox tb => tb.Text,
                            _ => null
                        };

                        item.SoloTaskSettingsObject[setting.Name] = value;
                    }

                    foreach (var group in ScriptGroups)
                    {
                        WriteScriptGroup(group);
                    }
                    Toast.Success("独立任务配置已保存");
                });
            }
        });
    }

    private void ShowHoeingSettingsDialog(ScriptGroupProject item)
    {
        var settings = item.SoloTaskSettingsObject!;
        var globalCfg = TaskContext.Instance().Config.AutoHoeingConfig;

        string GetStr(string key, string fallback) =>
            settings.TryGetValue(key, out var v) ? v?.ToString() ?? fallback : fallback;
        bool GetBool(string key, bool fallback) =>
            settings.TryGetValue(key, out var v) ? v is true or "True" or "true" : fallback;
        int GetInt(string key, int fallback) =>
            settings.TryGetValue(key, out var v) && int.TryParse(v?.ToString(), out var n) ? n : fallback;

        // ===== 顶部：单机/联机切换 =====
        var mpEnabled = GetBool("multiplayerEnabled", globalCfg.MultiplayerEnabled);
        var modeToggle = new System.Windows.Controls.CheckBox
        {
            Content = "启用联机锄地模式",
            IsChecked = mpEnabled,
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 12)
        };

        // ===== 单机内容区（普通参数列表）=====
        var soloPanel = new System.Windows.Controls.StackPanel
        {
            Visibility = mpEnabled ? Visibility.Collapsed : Visibility.Visible
        };
        var settingItems = GameTask.SoloTaskRegistry.GetSettingItems(item.Name);
        var controls = new Dictionary<string, FrameworkElement>();
        foreach (var setting in settingItems)
        {
            soloPanel.Children.Add(new TextBlock { Text = setting.Label, Margin = new Thickness(0, 8, 0, 2), FontSize = 13 });
            object? currentValue = settings.TryGetValue(setting.Name, out var ov) ? ov : setting.DefaultValue;
            if (setting.Type == "select" && setting.Options != null)
            {
                var combo = new System.Windows.Controls.ComboBox
                {
                    ItemsSource = setting.Options,
                    SelectedItem = currentValue?.ToString() ?? "",
                    Margin = new Thickness(0, 0, 0, 4)
                };
                soloPanel.Children.Add(combo);
                controls[setting.Name] = combo;
            }
            else if (setting.Type == "bool")
            {
                var check = new System.Windows.Controls.CheckBox
                {
                    IsChecked = currentValue is true or "True" or "true",
                    Margin = new Thickness(0, 0, 0, 4)
                };
                soloPanel.Children.Add(check);
                controls[setting.Name] = check;
            }
            else
            {
                var tb = new TextBox { Text = currentValue?.ToString() ?? "", Margin = new Thickness(0, 0, 0, 4) };
                soloPanel.Children.Add(tb);
                controls[setting.Name] = tb;
            }
        }

        // ===== 联机内容区（声明在布局代码之前，由下方布局代码填充）=====
        var currentRole = GetStr("multiplayerRole", "host");
        var currentJoinMode = GetStr("memberJoinMode", "byHostName");

        var roleCombo = new System.Windows.Controls.ComboBox
        {
            ItemsSource = new[] { "房主（创建房间）", "成员（加入房间）" },
            SelectedIndex = currentRole == "member" ? 1 : 0,
            Margin = new Thickness(0, 0, 0, 0)
        };
        var joinModeCombo = new System.Windows.Controls.ComboBox
        {
            ItemsSource = new[] { "指定房主名称", "随机加入现有房间" },
            SelectedIndex = currentJoinMode == "random" ? 1 : 0,
            Margin = new Thickness(0, 0, 0, 0)
        };
        var targetHostBox = new TextBox { Text = GetStr("targetHostName", globalCfg.TargetHostName), PlaceholderText = "房主的玩家名称" };
        var serverUrlBox = new TextBox { Text = GetStr("coordinatorServerUrl", globalCfg.CoordinatorServerUrl), PlaceholderText = "协调服务器地址" };
        var playerNameBox = new TextBox { Text = GetStr("playerName", globalCfg.PlayerName), PlaceholderText = "玩家名称" };
        var playerUidBox = new TextBox { Text = GetStr("playerUid", globalCfg.PlayerUid), PlaceholderText = "玩家 UID" };

        // 房主专属
        var expectedCountBox = new TextBox { Text = GetInt("expectedPlayerCount", globalCfg.ExpectedPlayerCount).ToString(), PlaceholderText = "2-4" };
        var whitelistBox = new TextBox { Text = GetStr("roomWhitelist", globalCfg.RoomWhitelist), PlaceholderText = "逗号分隔，留空不限制" };
        var partyTimeoutBox = new TextBox { Text = GetInt("partyTimeoutSeconds", globalCfg.PartyTimeoutSeconds).ToString(), PlaceholderText = "秒" };
        var memberPartyTimeoutBox = new TextBox { Text = GetInt("partyTimeoutSeconds", globalCfg.PartyTimeoutSeconds).ToString(), PlaceholderText = "秒，默认300" };
        var timeoutActionCombo = new System.Windows.Controls.ComboBox { ItemsSource = new[] { "超时后结束任务", "超时后以现有人数开始" }, SelectedIndex = GetInt("partyTimeoutAction", globalCfg.PartyTimeoutAction) };
        var syncTimeoutBox = new TextBox { Text = GetInt("syncTimeoutSeconds", globalCfg.SyncTimeoutSeconds).ToString(), PlaceholderText = "秒" };
        var minPlayersBox = new TextBox { Text = GetInt("minPlayersToSync", globalCfg.MinPlayersToSync).ToString(), PlaceholderText = "0=等齐" };
        var syncPointMinDistBox = new TextBox { Text = GetStr("syncPointMinDistance", globalCfg.SyncPointMinDistance.ToString()), PlaceholderText = "默认30" };
        var startRouteIndexBox = new TextBox { Text = GetInt("startRouteIndex", globalCfg.StartRouteIndex).ToString(), PlaceholderText = "0=从头" };
        var enableKazuhaSyncCheck = new System.Windows.Controls.CheckBox { Content = "启用万叶聚物同步", IsChecked = GetBool("enableKazuhaSync", globalCfg.EnableKazuhaSync) };
        // multiplayer-hoeing-selectable-fight-strategy §C7: 固定策略开关（默认读 settings，缺省 globalCfg 当前值即默认 true）
        var useFixedFightStrategyCheck = new System.Windows.Controls.CheckBox { Content = "固定使用联机战斗策略(关闭即使用配置组中选择的策略)", IsChecked = GetBool("multiplayerUseFixedFightStrategy", globalCfg.MultiplayerUseFixedFightStrategy) };
        var fightTimeoutBox = new TextBox { Text = GetInt("fightTimeoutSeconds", globalCfg.FightTimeoutSeconds).ToString(), PlaceholderText = "秒，默认120" };

        // ===== 快速同步点抢报（multiplayer-fast-sync-host-controlled spec, host-controlled）=====
        var fastSyncEnabledCheck = new System.Windows.Controls.CheckBox { Content = "启用快速同步点抢报", IsChecked = GetBool("fastSyncPointEnabled", globalCfg.FastSyncPointEnabled) };
        var fastSyncPathingDistanceBox = new TextBox { Text = GetStr("fastSyncPathingDistance", globalCfg.FastSyncPathingDistance.ToString()), PlaceholderText = "米，5-30，默认10" };
        var fastSyncTeleportLoadingDelayBox = new TextBox { Text = GetInt("fastSyncTeleportLoadingDelayMs", globalCfg.FastSyncTeleportLoadingDelayMs).ToString(), PlaceholderText = "毫秒，0-3000，默认0" };
        // 联动：仅当主开关启用时两个数值框才有效
        void UpdateFastSyncEnabled()
        {
            var enabled = fastSyncEnabledCheck.IsChecked ?? false;
            fastSyncPathingDistanceBox.IsEnabled = enabled;
            fastSyncTeleportLoadingDelayBox.IsEnabled = enabled;
        }
        fastSyncEnabledCheck.Checked += (_, _) => UpdateFastSyncEnabled();
        fastSyncEnabledCheck.Unchecked += (_, _) => UpdateFastSyncEnabled();
        UpdateFastSyncEnabled();

        // ===== 万叶聚物同步配置（multiplayer-kazuha-collect-sync + kazuha-player-auto-detection）=====
        var kazuhaSyncWaitSecondsBox = new TextBox { Text = GetInt("kazuhaSyncWaitSeconds", globalCfg.KazuhaSyncWaitSeconds).ToString(), PlaceholderText = "秒，0-30，默认1" };
        var kazuhaSyncTimeoutSecondsBox = new TextBox { Text = GetInt("kazuhaSyncTimeoutSeconds", globalCfg.KazuhaSyncTimeoutSeconds).ToString(), PlaceholderText = "秒，5-120，默认20" };
        var kazuhaWaitSkillCdSecondsBox = new TextBox { Text = GetInt("kazuhaWaitSkillCdSeconds", globalCfg.KazuhaWaitSkillCdSeconds).ToString(), PlaceholderText = "秒，3-10，默认5" };
        var kazuhaSecondApproachMaxStepsBox = new TextBox { Text = GetInt("kazuhaSecondApproachMaxSteps", globalCfg.KazuhaSecondApproachMaxSteps).ToString(), PlaceholderText = "步，1-30，默认6" };

        // 联动启用：仅当勾选"启用万叶聚物同步"时三个输入框才有效
        // kazuha-player-auto-detection: 替换原"按 KazuhaPlayerIndex ∈ [1,4] 判定"，改为 EnableKazuhaSync 布尔门控
        void UpdateKazuhaSyncEnabled()
        {
            var enabled = enableKazuhaSyncCheck.IsChecked ?? false;
            kazuhaSyncWaitSecondsBox.IsEnabled = enabled;
            kazuhaSyncTimeoutSecondsBox.IsEnabled = enabled;
            kazuhaWaitSkillCdSecondsBox.IsEnabled = enabled;
            kazuhaSecondApproachMaxStepsBox.IsEnabled = enabled;
        }
        enableKazuhaSyncCheck.Checked += (_, _) => UpdateKazuhaSyncEnabled();
        enableKazuhaSyncCheck.Unchecked += (_, _) => UpdateKazuhaSyncEnabled();
        UpdateKazuhaSyncEnabled();
        var debugModeCheck = new System.Windows.Controls.CheckBox { Content = "调试模式（跳过路线一致性验证）", IsChecked = GetBool("debugMode", globalCfg.DebugMode) };
        // route-mode-dropdown: 用下拉框替换原 useFixedRoutesCheck 复选框（方案 A 布尔重映射）
        var initialUseFixed = GetBool("useFixedDebugRoutes", globalCfg.UseFixedDebugRoutes);
        var routeModeCombo = new System.Windows.Controls.ComboBox
        {
            ItemsSource = new[] { BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteModeDecisions.BuiltinOnline, BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteModeDecisions.SoloDebug },
            Margin = new Thickness(0, 0, 0, 4)
        };
        // ComboBox SelectedItem 时序：ItemsSource 赋值后再设 SelectedItem（bgi-config-and-mvvm §4.1），
        // 此处 ItemsSource 已先于 SelectedItem 赋值且为静态两项，直接赋值安全；用 Loaded 兜底再确认一次。
        var initialRouteMode = BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteModeDecisions.MapUseFixedToRouteMode(initialUseFixed);
        routeModeCombo.SelectedItem = initialRouteMode;
        routeModeCombo.Loaded += (_, _) =>
        {
            if (!Equals(routeModeCombo.SelectedItem, initialRouteMode))
                routeModeCombo.SelectedItem = initialRouteMode;
        };
        var fixedRoutePathBox = new TextBox { Text = GetStr("fixedDebugRoutePath", globalCfg.FixedDebugRoutePath), PlaceholderText = "调试线路目录（留空使用内置）" };

        // route-mode-dropdown: "执行线路"下拉框 = 单机配置 groupIndex（勘察点 5）。
        // 复用 SoloTaskRegistry 的 groupIndex 定义（Options「路径组一…路径组十」、DefaultValue=currentGroup），
        // 不在弹窗内重复硬编码 groupNames，避免与 AutoHoeingTask.GetSettingDefinitions 漂移。
        var groupIndexDef = settingItems.FirstOrDefault(s => s.Name == "groupIndex");
        var groupOptions = groupIndexDef?.Options ?? new System.Collections.Generic.List<string> { "路径组一" };
        var groupDefault = groupIndexDef?.DefaultValue?.ToString() ?? "路径组一";
        var initialGroup = GetStr("groupIndex", groupDefault);
        var groupIndexCombo = new System.Windows.Controls.ComboBox
        {
            ItemsSource = groupOptions,
            Margin = new Thickness(0, 0, 0, 4)
        };
        // SelectedItem 时序兜底：用 ResolveSelectedOrDefault 统一校验，saved 在选项内则用 saved，否则回退 groupDefault（bgi-config-and-mvvm §4.1）
        var resolvedGroup = BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteModeDecisions.ResolveSelectedOrDefault(groupOptions, initialGroup, groupDefault);
        groupIndexCombo.SelectedItem = resolvedGroup;
        groupIndexCombo.Loaded += (_, _) =>
        {
            if (!Equals(groupIndexCombo.SelectedItem, resolvedGroup))
                groupIndexCombo.SelectedItem = resolvedGroup;
        };
        var groupIndexField = MakeField("执行线路", groupIndexCombo, "选择单机锄地的第几个路径组，联机将用该路径组所选线路来跑");

        // route-mode-dropdown: 下拉框下方固定提示文案（Req 4.1 / 4.2，OQ-B 相对路径）
        var routeModeHint = new TextBlock
        {
            Text = "如果使用单机调试线路，关闭联机选项进行线路调试，建议使用联机内置线路，针对联机做了优化。\n"
                 + "提示：把你自己的线路文件夹拷贝到内置线路文件夹（安装目录下的 GameTask\\AutoHoeing\\Assets），即可作为内置线路被选择。",
            FontSize = 11,
            Foreground = SystemColors.GrayTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };

        // 多世界
        var multiWorldCheck = new System.Windows.Controls.CheckBox
        {
            Content = "启用多世界连续锄地",
            IsChecked = GetBool("multiWorldEnabled", globalCfg.MultiWorldEnabled),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var multiWorldCountBox = new TextBox
        {
            Text = GetInt("multiWorldCount", globalCfg.MultiWorldCount).ToString(),
            PlaceholderText = "轮数（1-4）",
            Width = 80,
            Margin = new Thickness(0, 0, 0, 0)
        };
        multiWorldCountBox.IsEnabled = multiWorldCheck.IsChecked ?? false;
        multiWorldCheck.Checked += (_, _) => multiWorldCountBox.IsEnabled = true;
        multiWorldCheck.Unchecked += (_, _) => multiWorldCountBox.IsEnabled = false;

        // ── 辅助函数 ──────────────────────────────────────────────────────────

        // 分组标题：文字 + 全宽分隔线
        System.Windows.Controls.DockPanel MakeGroupHeader(string title)
        {
            var dp = new System.Windows.Controls.DockPanel { Margin = new Thickness(0, 14, 0, 6), LastChildFill = true };
            var tb = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = SystemColors.ControlLightBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.DockPanel.SetDock(tb, System.Windows.Controls.Dock.Left);
            var line = new System.Windows.Shapes.Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromRgb(80, 80, 80)), VerticalAlignment = VerticalAlignment.Center };
            dp.Children.Add(tb);
            dp.Children.Add(line);
            return dp;
        }

        // 字段：标签在上，控件在下，控件填满宽度（用于大字段）
        System.Windows.Controls.StackPanel MakeField(string label, System.Windows.UIElement control, string? hint = null)
        {
            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = SystemColors.GrayTextBrush, Margin = new Thickness(0, 0, 0, 3) });
            if (hint != null)
                sp.Children.Add(new TextBlock { Text = hint, FontSize = 11, Foreground = SystemColors.GrayTextBrush, Margin = new Thickness(0, 0, 0, 2), TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(control);
            return sp;
        }

        // 小数字字段：标签在上，输入框固定宽度（用于数字类）
        System.Windows.Controls.StackPanel MakeSmallField(string label, System.Windows.UIElement control, double ctrlWidth)
        {
            if (control is System.Windows.FrameworkElement fe) { fe.Width = ctrlWidth; fe.HorizontalAlignment = HorizontalAlignment.Left; }
            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 16, 8) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = SystemColors.GrayTextBrush, Margin = new Thickness(0, 0, 0, 3) });
            sp.Children.Add(control);
            return sp;
        }

        // 一行多个小字段（WrapPanel 自动换行）
        System.Windows.Controls.WrapPanel MakeSmallRow(params System.Windows.UIElement[] fields)
        {
            var wp = new System.Windows.Controls.WrapPanel();
            foreach (var f in fields) wp.Children.Add(f);
            return wp;
        }

        // 期望人数（小）+ 白名单（大）：DockPanel，左侧固定，右侧填满
        System.Windows.Controls.DockPanel MakeCountAndWhitelist()
        {
            var dp = new System.Windows.Controls.DockPanel { Margin = new Thickness(0, 0, 0, 0), LastChildFill = true };
            var left = MakeSmallField("期望人数（2-4）", expectedCountBox, 50);
            left.Margin = new Thickness(0, 0, 16, 8);
            System.Windows.Controls.DockPanel.SetDock(left, System.Windows.Controls.Dock.Left);
            var right = MakeField("房间白名单（逗号分隔，留空不限）", whitelistBox);
            dp.Children.Add(left);
            dp.Children.Add(right);
            return dp;
        }

        // 设置控件填满宽度
        void Stretch(System.Windows.FrameworkElement el) { el.HorizontalAlignment = HorizontalAlignment.Stretch; el.Width = double.NaN; }

        Stretch(serverUrlBox);
        Stretch(whitelistBox);
        Stretch(fixedRoutePathBox);
        Stretch(targetHostBox);
        Stretch(roleCombo);

        // ── 联机面板布局 ──────────────────────────────────────────────────────

        var mpPanel = new System.Windows.Controls.StackPanel
        {
            Visibility = mpEnabled ? Visibility.Visible : Visibility.Collapsed
        };

        // ========== multiplayer-hoeing-fixed-fight-strategy §6 ==========
        // 联机战斗策略（固定文件）说明 + 打开按钮：与 TaskSettingsPage 设置页文案逐字符一致。
        // Click 委派给共享 helper（MultiplayerFightStrategyFileHelper.OpenForEdit），
        // 不在弹窗内复制"判定 → 创建 → Process.Start → 异常兜底"逻辑。
        {
            var fixedStrategyPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            fixedStrategyPanel.Children.Add(new TextBlock
            {
                Text = "联机战斗策略（固定文件）",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            fixedStrategyPanel.Children.Add(new TextBlock
            {
                Text = "联机锄地战斗策略已固定为 User\\AutoFight\\联机战斗策略.txt（文件不存在时回退至配置组中的选择）。点击下方按钮可编辑该文件。",
                FontSize = 12,
                Foreground = SystemColors.GrayTextBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });
            var openFixedStrategyBtn = new System.Windows.Controls.Button
            {
                Content = "打开联机战斗策略文件",
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 4, 12, 4)
            };
            openFixedStrategyBtn.Click += (_, _) =>
                BetterGenshinImpact.GameTask.AutoFight.MultiplayerFightStrategyFileHelper.OpenForEdit();
            fixedStrategyPanel.Children.Add(openFixedStrategyBtn);
            // multiplayer-hoeing-selectable-fight-strategy §C7: 开关 + 红字说明（OQ-B 两处都加 / OQ-D 措辞）
            useFixedFightStrategyCheck.Margin = new Thickness(0, 8, 0, 0);
            fixedStrategyPanel.Children.Add(useFixedFightStrategyCheck);
            fixedStrategyPanel.Children.Add(new TextBlock
            {
                Text = "联机战斗策略为针对联机优化过，大部分角色的普通战斗策略和联机可能不一样，可打开《联机战斗策略.txt》查看和编辑相关角色的策略。",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.DarkRed,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
            mpPanel.Children.Add(fixedStrategyPanel);
        }
        // ========== /multiplayer-hoeing-fixed-fight-strategy §6 ==========

        // 分组1：身份信息
        mpPanel.Children.Add(MakeGroupHeader("身份信息"));
        mpPanel.Children.Add(MakeField("服务器地址", serverUrlBox));
        // 玩家名称 + UID：固定宽度，同行
        var nameUidRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        nameUidRow.Children.Add(MakeSmallField("玩家名称(必填)", playerNameBox, 130));
        nameUidRow.Children.Add(MakeSmallField("玩家 UID(必填)", playerUidBox, 120));
        mpPanel.Children.Add(nameUidRow);
        mpPanel.Children.Add(MakeField("联机角色", roleCombo));

        // ===== 拾取配置 =====
        // 与 AutoHoeingTask.GetSettingDefinitions() 第 3027 行 pickupMode.Options 字面量逐字符一致
        // 4 个选项 + ApplySettingsOverride 第 2881 行消费的 "pickupMode" 键
        var pickupModeCombo = new System.Windows.Controls.ComboBox
        {
            ItemsSource = new[] { "模板匹配拾取狗粮和怪物材料", "模板匹配仅拾取狗粮", "BGI原版拾取", "不拾取" },
            SelectedItem = GetStr("pickupMode", globalCfg.PickupMode),
            Margin = new Thickness(0, 0, 0, 0)
        };
        Stretch(pickupModeCombo);

        mpPanel.Children.Add(MakeGroupHeader("拾取配置"));
        mpPanel.Children.Add(MakeField("拾取模式", pickupModeCombo, "推荐使用模板匹配拾取，BGI原版拾取性能开销大、准确度低"));

        // ===== 联机队伍和角色准备 =====
        var multiplayerPartyNameBox = new TextBox { Text = GetStr("multiplayerPartyName", globalCfg.MultiplayerPartyName), PlaceholderText = "留空则使用当前队伍" };
        var multiplayerStartAvatarNameBox = new TextBox { Text = GetStr("multiplayerStartAvatarName", globalCfg.MultiplayerStartAvatarName), PlaceholderText = "留空则使用当前角色，如：钟离、纳西妲" };
        Stretch(multiplayerPartyNameBox);
        Stretch(multiplayerStartAvatarNameBox);
        
        mpPanel.Children.Add(MakeGroupHeader("联机队伍和角色准备"));
        mpPanel.Children.Add(MakeField("联机队伍名称", multiplayerPartyNameBox, "联机前自动切换到指定队伍，留空则使用当前队伍"));
        mpPanel.Children.Add(MakeField("联机起始角色名称", multiplayerStartAvatarNameBox, "联机前自动切换到指定角色，留空则使用当前角色"));

        // 房主面板
        var hostPanel = new System.Windows.Controls.StackPanel();

        // 分组2：房间设置
        hostPanel.Children.Add(MakeGroupHeader("房间设置"));
        // 期望人数（小）单独一行，白名单单独一行
        hostPanel.Children.Add(MakeSmallRow(MakeSmallField("期望人数（2-4）", expectedCountBox, 50)));
        hostPanel.Children.Add(MakeField("房间白名单（逗号分隔，留空不限，房主自己也要加进去）", whitelistBox));
        // 组队超时（小）+ 超时动作（固定）同行
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("组队超时（秒）", partyTimeoutBox, 65),
            MakeSmallField("超时动作", timeoutActionCombo, 160)));

        // 多世界行
        var mwRow = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var mwTopRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        mwTopRow.Children.Add(multiWorldCheck);
        mwTopRow.Children.Add(new TextBlock { Text = "  轮数", FontSize = 12, Foreground = SystemColors.GrayTextBrush, VerticalAlignment = VerticalAlignment.Center });
        multiWorldCountBox.Width = 50; multiWorldCountBox.HorizontalAlignment = HorizontalAlignment.Left;
        mwTopRow.Children.Add(multiWorldCountBox);
        mwRow.Children.Add(mwTopRow);
        mwRow.Children.Add(new TextBlock { Text = "按加入顺序轮换房主，最多4轮", FontSize = 11, Foreground = SystemColors.GrayTextBrush, Margin = new Thickness(26, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
        hostPanel.Children.Add(mwRow);

        // 分组3：同步设置
        hostPanel.Children.Add(MakeGroupHeader("同步设置(以下所有配置将同步给所有成员，多轮次沿用同配置)"));
        // 第一行：集合点超时 + 最低同步人数
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("集合点超时（秒）", syncTimeoutBox, 65),
            MakeSmallField("最低同步人数（0=等齐）", minPlayersBox, 50)));
        // 第二行：集合点最小距离 + 起始路线（万叶聚物开关挪到下面"万叶聚物同步配置"组首行）
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("集合点最小距离", syncPointMinDistBox, 65),
            MakeSmallField("从第几条路线开始（0=从头）", startRouteIndexBox, 65)));

        // 万叶聚物同步配置（仅在启用万叶聚物同步时生效）
        // 启用开关与其他万叶配置放一组，用户调参时不需要在两组之间来回找
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("启用万叶聚物同步", enableKazuhaSyncCheck, 180)));
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("万叶聚物完成后停留（秒，0-30）", kazuhaSyncWaitSecondsBox, 70),
            MakeSmallField("聚物同步总超时（秒，5-120）", kazuhaSyncTimeoutSecondsBox, 70),
            MakeSmallField("万叶 E 技 CD 等待上限（秒，3-10）", kazuhaWaitSkillCdSecondsBox, 70)));
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("拾取前精接近步数（联机万叶聚物，1-30）", kazuhaSecondApproachMaxStepsBox, 70)));

        // 分组4：战斗配置
        hostPanel.Children.Add(MakeGroupHeader("战斗配置"));
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("战斗超时（秒）", fightTimeoutBox, 65)));

        // 分组：快速同步点抢报（multiplayer-fast-sync-host-controlled spec, host-controlled）
        hostPanel.Children.Add(MakeGroupHeader("快速同步点抢报"));
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("启用快速同步点抢报", fastSyncEnabledCheck, 280)));
        hostPanel.Children.Add(MakeSmallRow(
            MakeSmallField("路径抢报距离阈值（米，5-30）", fastSyncPathingDistanceBox, 70),
            MakeSmallField("传送 loading 抢报延迟（毫秒，0-3000）", fastSyncTeleportLoadingDelayBox, 70)));

        // 分组5：线路选项（Expander 折叠，默认收起）
        var debugInner = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        debugInner.Children.Add(debugModeCheck);
        debugInner.Children.Add(new System.Windows.Controls.StackPanel { Height = 4 });
        // route-mode-dropdown: 下拉框替换原 useFixedRoutesCheck（位置不变：debugModeCheck 之后）
        debugInner.Children.Add(routeModeCombo);
        debugInner.Children.Add(routeModeHint);
        debugInner.Children.Add(new System.Windows.Controls.StackPanel { Height = 4 });
        
        // 手动指定线路目录 - 只在「固定内置联机线路」模式下显示
        var manualRouteField = MakeField("手动指定线路目录", fixedRoutePathBox, "留空则使用下方按钮选择");
        debugInner.Children.Add(manualRouteField);
        // route-mode-dropdown: 执行线路（groupIndex）下拉，仅「单机调试线路」模式下显示
        debugInner.Children.Add(groupIndexField);

        // 添加内置线路选择按钮
        var routeScanner = new BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteDirectoryScanner();

        // import-local-route-folder C7: 变体偏好面板刷新委托（在 variantExpander.Expanded 定义处赋值）。
        // 导入成功后若面板已展开，调用它立即重建变体列表（拿到新导入文件夹的变体子目录）。
        Action? refreshVariantPanel = null;

        // 内置线路选择区域容器
        var builtinRouteContainer = new System.Windows.Controls.StackPanel();

        // import-local-route-folder C3: 从本地文件夹导入按钮（固定文案 OQ-1），恒为容器首节点，随容器显隐（Req 1.x）
        var importFolderBtn = new System.Windows.Controls.Button
        {
            Content = "从本地文件夹导入线路",
            Margin = new Thickness(0, 8, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // 打开内置线路目录（GameTask\AutoHoeing\Assets）按钮，放在导入按钮右侧
        var openAssetsDirBtn = new System.Windows.Controls.Button
        {
            Content = "打开内置线路目录",
            Margin = new Thickness(8, 8, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openAssetsDirBtn.Click += (s, e) =>
        {
            try
            {
                var assetsDir = System.IO.Path.Combine(Global.Absolute("GameTask"), "AutoHoeing", "Assets");
                System.IO.Directory.CreateDirectory(assetsDir);   // 目录不存在时创建（不删除任何内容）
                Process.Start(new ProcessStartInfo(assetsDir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // 打开目录失败不应让弹窗崩溃；记录并提示
                _logger.LogWarning(ex, "[内置线路] 打开内置线路目录失败");
                Toast.Warning("打开内置线路目录失败，请查看日志");
            }
        };

        // 导入按钮 + 打开目录按钮同行
        var importRow = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal
        };
        importRow.Children.Add(importFolderBtn);
        importRow.Children.Add(openAssetsDirBtn);

        // import-local-route-folder C2: 可重入构建函数，解决"扫描==0→导入后>0"——每次清空容器并按最新扫描结果重建
        void RebuildBuiltinButtons()
        {
            builtinRouteContainer.Children.Clear();
            builtinRouteContainer.Children.Add(importRow);   // 导入按钮 + 打开目录按钮恒在顶部

            var builtinFolders = routeScanner.ScanBuiltinRoutes();   // 每次重扫
            if (builtinFolders.Count > 0)
            {
                builtinRouteContainer.Children.Add(new TextBlock 
                { 
                    Text = "内置线路快速选择", 
                    FontSize = 12, 
                    Foreground = SystemColors.GrayTextBrush,
                    Margin = new Thickness(0, 8, 0, 4)
                });
                
                var buttonPanel = new System.Windows.Controls.WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                var selectedRoute = GetStr("selectedBuiltinRoute", globalCfg.SelectedBuiltinRoute);
                
                foreach (var folder in builtinFolders)
                {
                    var btn = new System.Windows.Controls.Button
                    {
                        Content = folder.FolderName,
                        Margin = new Thickness(0, 0, 8, 8),
                        Tag = folder.FolderName
                    };
                    
                    // 设置按钮样式 - 使用基本的 WPF 样式而不是 WPF UI 的 Appearance
                    if (folder.FolderName == selectedRoute)
                    {
                        btn.Background = SystemColors.HighlightBrush;
                        btn.Foreground = SystemColors.HighlightTextBrush;
                    }
                    else
                    {
                        btn.Background = SystemColors.ControlBrush;
                        btn.Foreground = SystemColors.ControlTextBrush;
                    }
                    
                    btn.Click += (s, e) =>
                    {
                        // 更新所有按钮样式
                        foreach (var child in buttonPanel.Children.OfType<System.Windows.Controls.Button>())
                        {
                            child.Background = SystemColors.ControlBrush;
                            child.Foreground = SystemColors.ControlTextBrush;
                        }
                        btn.Background = SystemColors.HighlightBrush;
                        btn.Foreground = SystemColors.HighlightTextBrush;
                        settings["selectedBuiltinRoute"] = btn.Tag.ToString();
                    };
                    
                    buttonPanel.Children.Add(btn);
                }
                
                builtinRouteContainer.Children.Add(buttonPanel);
                
                builtinRouteContainer.Children.Add(new TextBlock 
                { 
                    Text = "手动输入路径优先级高于按钮选择", 
                    FontSize = 11, 
                    Foreground = SystemColors.GrayTextBrush,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                
                // 更新按钮状态的方法
                void UpdateButtonStates()
                {
                    var useFixedRoutes = BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteModeDecisions.IsBuiltinOnline(routeModeCombo.SelectedItem?.ToString());
                    var hasManualPath = !string.IsNullOrWhiteSpace(fixedRoutePathBox.Text);
                    var buttonsEnabled = useFixedRoutes && !hasManualPath;
                    
                    // 可见性统一由 UpdateRouteModeVisibility 控制（§3 C5.3），本方法只管按钮启用/高亮
                    
                    // 控制按钮的启用状态
                    foreach (var btn in buttonPanel.Children.OfType<System.Windows.Controls.Button>())
                    {
                        btn.IsEnabled = buttonsEnabled;
                    }
                    
                    // 如果不满足条件，清除选择状态
                    if (!buttonsEnabled)
                    {
                        foreach (var btn in buttonPanel.Children.OfType<System.Windows.Controls.Button>())
                        {
                            btn.Background = SystemColors.ControlBrush;
                            btn.Foreground = SystemColors.ControlTextBrush;
                        }
                    }
                }
                
                // 监听线路模式下拉变化（route-mode-dropdown）
                routeModeCombo.SelectionChanged += (s, e) => UpdateButtonStates();
                
                // 监听手动路径输入变化
                fixedRoutePathBox.TextChanged += (s, e) => UpdateButtonStates();
                
                // 初始化状态
                UpdateButtonStates();
            }
        }
        RebuildBuiltinButtons();   // 首次构建

        // import-local-route-folder C3: 导入按钮点击 = 弹文件夹选择 → 校验 → 重名确认 → 拷贝 → 选中 → 重建 → 刷新变体 → Toast
        importFolderBtn.Click += (s, e) =>
        {
            try
            {
                var picker = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
                {
                    Description = "选择要导入的本地线路文件夹",
                    UseDescriptionForTitle = true
                };
                if (picker.ShowDialog() != true) return;   // 取消 = no-op（Req 2.2）
                var sourcePath = picker.SelectedPath;
                if (string.IsNullOrWhiteSpace(sourcePath)) return;

                // 校验 Valid_Route_Folder（Req 3.1 / 3.2 / 6.1）
                if (!BetterGenshinImpact.GameTask.AutoHoeing.Services.LocalRouteFolderImporter.IsValidRouteFolder(sourcePath))
                {
                    Toast.Warning("所选文件夹不含线路文件（*.json）");
                    return;
                }

                var assetsDir = System.IO.Path.Combine(Global.Absolute("GameTask"), "AutoHoeing", "Assets");
                var targetName = BetterGenshinImpact.GameTask.AutoHoeing.Services.LocalRouteFolderImporter.ResolveTargetName(sourcePath);
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    Toast.Warning("无法解析所选文件夹名称");
                    return;
                }
                var targetPath = System.IO.Path.Combine(assetsDir, targetName);

                // 源已在 Assets 内 → 跳过拷贝直接选中（Req 3.5）
                bool copied = false;
                if (!BetterGenshinImpact.GameTask.AutoHoeing.Services.LocalRouteFolderImporter.IsInsideAssets(sourcePath, assetsDir))
                {
                    // 重名确认（OQ-2 / Req 4.1）
                    if (BetterGenshinImpact.GameTask.AutoHoeing.Services.LocalRouteFolderImporter.NeedsOverwriteConfirm(System.IO.Directory.Exists(targetPath)))
                    {
                        var r = ThemedMessageBox.Question(
                            $"内置线路目录已存在同名文件夹「{targetName}」。\n是否覆盖其中的同名文件？（不会删除目标目录的其他文件）",
                            "导入线路 - 重名确认",
                            MessageBoxButton.YesNo,
                            System.Windows.MessageBoxResult.No);
                        if (r != System.Windows.MessageBoxResult.Yes) return;   // 否/取消 = 终止，不拷贝不改选中（Req 4 / 6.4）
                    }

                    try
                    {
                        BetterGenshinImpact.GameTask.AutoHoeing.Services.LocalRouteFolderImporter
                            .CopyDirectoryRecursive(sourcePath, targetPath, overwrite: true);
                        copied = true;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        // 无权限写入：记录并提示，不崩溃、不改选中（Req 6.3 / 6.4）
                        _logger.LogWarning(ex, "[导入线路] 无权限拷贝到内置线路目录: {Target}", targetPath);
                        Toast.Error("导入失败：无权限写入内置线路目录");
                        return;
                    }
                    catch (IOException ex)
                    {
                        // 拷贝 IO 错误：记录并提示，不崩溃、不改选中（Req 6.2 / 6.4）
                        _logger.LogWarning(ex, "[导入线路] 拷贝发生 IO 错误: {Target}", targetPath);
                        Toast.Error("导入失败：拷贝文件时发生 IO 错误");
                        return;
                    }
                }

                // 成功：写选中 → 重建按钮组（高亮新文件夹）（Req 5.1/5.2/5.3）
                settings["selectedBuiltinRoute"] = targetName;
                RebuildBuiltinButtons();

                // 刷新变体偏好面板（仅当已展开，OQ-5；IsExpanded 守卫在委托内部，避免前向引用 variantExpander）
                refreshVariantPanel?.Invoke();

                // 成功提示 + 联机一致性前提 A2（Req 7.1 / 7.2）
                Toast.Success(copied ? $"已导入线路「{targetName}」并设为当前内置线路" : $"已选中内置线路「{targetName}」");
                Toast.Information("提示：联机仅同步文件夹名并做 MD5 校验，不传输线路文件。请确保每位成员各自拥有同名、同内容的线路文件夹。", time: 6000);
            }
            catch (Exception ex)
            {
                // 兜底：任何未预期异常都不应让弹窗崩溃（Req 6.2/6.3/6.4）
                _logger.LogWarning(ex, "[导入线路] 导入流程发生未预期异常");
                Toast.Error("导入失败，请查看日志");
            }
        };

        debugInner.Children.Add(builtinRouteContainer);

        // route-mode-dropdown: 线路模式可见性统一控制（覆盖扫描==0 场景）
        void UpdateRouteModeVisibility()
        {
            var builtinOnline = BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteModeDecisions.IsBuiltinOnline(routeModeCombo.SelectedItem?.ToString());
            // 固定内置联机线路 → 显示手动框 + 内置按钮组，隐藏执行线路下拉
            manualRouteField.Visibility = builtinOnline ? Visibility.Visible : Visibility.Collapsed;
            builtinRouteContainer.Visibility = builtinOnline ? Visibility.Visible : Visibility.Collapsed;
            // 单机调试线路 → 显示执行线路（groupIndex）下拉，隐藏上面两块
            groupIndexField.Visibility = builtinOnline ? Visibility.Collapsed : Visibility.Visible;
        }
        routeModeCombo.SelectionChanged += (_, _) => UpdateRouteModeVisibility();
        UpdateRouteModeVisibility();

        var debugExpander = new System.Windows.Controls.Expander
        {
            Header = "线路选项",
            IsExpanded = GetBool("debugMode", false) || GetBool("useFixedDebugRoutes", false),
            Content = debugInner,
            Margin = new Thickness(0, 4, 0, 0)
        };
        hostPanel.Children.Add(debugExpander);

        // 成员面板
        var memberPanel = new System.Windows.Controls.StackPanel();
        memberPanel.Children.Add(MakeGroupHeader("加入设置"));
        memberPanel.Children.Add(MakeSmallRow(MakeSmallField("加入方式", joinModeCombo, 160)));
        memberPanel.Children.Add(MakeField("指定房主名称", targetHostBox, "仅「指定房主名称」模式下生效"));
        memberPanel.Children.Add(MakeSmallRow(MakeSmallField("等待超时（秒）", memberPartyTimeoutBox, 65)));

        void UpdateJoinModeVisibility() => targetHostBox.IsEnabled = joinModeCombo.SelectedIndex == 0;
        joinModeCombo.SelectionChanged += (_, _) => UpdateJoinModeVisibility();
        UpdateJoinModeVisibility();

        mpPanel.Children.Add(hostPanel);
        mpPanel.Children.Add(memberPanel);

        void UpdateRoleVisibility()
        {
            var isHost = roleCombo.SelectedIndex == 0;
            hostPanel.Visibility = isHost ? Visibility.Visible : Visibility.Collapsed;
            memberPanel.Visibility = isHost ? Visibility.Collapsed : Visibility.Visible;
        }
        roleCombo.SelectionChanged += (_, _) => UpdateRoleVisibility();
        UpdateRoleVisibility();

        // ===== 根面板：顶部开关 + 内容区切换 =====
        var rootPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
        rootPanel.Children.Add(modeToggle);
        rootPanel.Children.Add(soloPanel);
        rootPanel.Children.Add(mpPanel);

        // ===== route-variant-sync-by-logical-id spec / §15.8 / R15.6：线路变体偏好折叠面板 =====
        // 房主和成员都可见可编辑（不随角色切换隐藏），偏好（基名→变体文件夹名）存配置组
        // settings["variantPreferences"]，并镜像到全局 AutoHoeingConfig.VariantPreferences 作兜底。
        // 列表 + 点击弹窗选 A变体/B变体/C变体/D变体，解决 200+ 线路场景下下拉框过多的问题。
        var variantExpander = new System.Windows.Controls.Expander
        {
            IsExpanded = false,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        // 折叠标题栏：左侧标题文字 + 右侧两个按钮（使用教程 / 制作规则），点击打开对应说明文档
        {
            var headerGrid = new System.Windows.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var headerText = new TextBlock
            {
                Text = "线路变体偏好",
                VerticalAlignment = VerticalAlignment.Center,
            };
            System.Windows.Controls.Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(headerText);

            // 打开输出根目录下的某个 md 说明文档（点击不应连带触发 Expander 展开/收起）
            void OpenDoc(string fileName)
            {
                try
                {
                    var docPath = System.IO.Path.Combine(AppContext.BaseDirectory, fileName);
                    if (System.IO.File.Exists(docPath))
                    {
                        Process.Start(new ProcessStartInfo(docPath) { UseShellExecute = true });
                    }
                    else
                    {
                        Toast.Warning($"未找到《{fileName}》，请重新编译以生成该文件");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[变体偏好] 打开说明文档失败: {File}", fileName);
                    Toast.Warning("打开说明文档失败，请查看日志");
                }
            }

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(12, 0, 8, 0),
            };
            var tutorialBtn = new Button
            {
                Content = "使用教程",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0),
            };
            tutorialBtn.Click += (s, e) => { e.Handled = true; OpenDoc("联机锄地使用教程.md"); };
            btnPanel.Children.Add(tutorialBtn);

            var rulesBtn = new Button
            {
                Content = "制作规则",
                Padding = new Thickness(8, 2, 8, 2),
            };
            rulesBtn.Click += (s, e) => { e.Handled = true; OpenDoc("联机锄地变体线路制作规则.md"); };
            btnPanel.Children.Add(rulesBtn);

            System.Windows.Controls.Grid.SetColumn(btnPanel, 1);
            headerGrid.Children.Add(btnPanel);
            variantExpander.Header = headerGrid;
        }
        // 当前对话框会话内的变体偏好编辑缓冲（基名 → 变体文件夹名）。保存时写入 settings。
        var variantPrefBuffer = new Dictionary<string, string>(StringComparer.Ordinal);
        // 预填：先全局，再用配置组已存的覆盖
        {
            var gcfg = TaskContext.Instance().Config.AutoHoeingConfig;
            if (gcfg.VariantPreferences != null)
                foreach (var (k, v) in gcfg.VariantPreferences)
                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v)) variantPrefBuffer[k] = v;
            if (settings.TryGetValue("variantPreferences", out var existRaw) && existRaw != null)
            {
                try
                {
                    if (existRaw is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var p in je.EnumerateObject())
                            if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var v = p.Value.GetString();
                                if (!string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(v)) variantPrefBuffer[p.Name] = v!;
                            }
                    }
                    else if (existRaw is Dictionary<string, string> sd)
                    {
                        foreach (var (k, v) in sd)
                            if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v)) variantPrefBuffer[k] = v;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[变体偏好] 读取配置组已存偏好失败，仅用全局值预填");
                }
            }
        }
        // import-local-route-folder C7: 把变体面板重建逻辑抽成本地函数，供 Expanded 事件与导入后刷新复用（幂等，每次 forceRefresh 重扫）
        void BuildVariantPanelContent()
        {
            try
            {
                var globalCfg = TaskContext.Instance().Config.AutoHoeingConfig;
                var dirs = BetterGenshinImpact.GameTask.AutoHoeing.AutoHoeingTask
                    .ResolveAllHoeingRouteDirs(globalCfg);
                // 按"总文件夹"分组：每个总文件夹 → 其下存在的变体子文件夹集合（A→B→C→D）
                var topFolders = BetterGenshinImpact.GameTask.AutoHoeing.Multiplayer.RouteVariantScanner
                    .ScanTopFolders(dirs, forceRefresh: true);

                if (topFolders.Count == 0)
                {
                    variantExpander.Content = new TextBlock
                    {
                        Text = "未发现变体线路。请在总线路文件夹下建 A变体/B变体/C变体/D变体 子文件夹并放入对应 _a/_b 后缀的 JSON。",
                        Margin = new Thickness(8),
                        Foreground = SystemColors.GrayTextBrush,
                        TextWrapping = TextWrapping.Wrap
                    };
                    return;
                }

                var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(8) };
                foreach (var kv in topFolders.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    var topName = kv.Key;
                    var availableFolders = kv.Value;   // 已按 A→B→C→D 排序
                    if (availableFolders.Count == 0) continue;

                    var rowGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 6) };
                    // 左列放线路名（自适应，给个最小宽），右列按钮占满剩余空间（避免说明文字被截断）
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 90 });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var label = new TextBlock
                    {
                        Text = topName,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 12, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    System.Windows.Controls.Grid.SetColumn(label, 0);
                    rowGrid.Children.Add(label);

                    // 默认代表（A→B→C→D 第一个存在）
                    var repFolder = availableFolders[0];

                    // 预读各变体说明（变体说明.txt），用于弹窗 + 按钮展示（R15.11）
                    var folderDescs = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var vf in availableFolders)
                    {
                        var desc = BetterGenshinImpact.GameTask.AutoHoeing.Multiplayer.RouteVariantScanner
                            .ReadVariantDescription(dirs, topName, vf);
                        folderDescs[vf] = desc;
                    }
                    string DescSuffix(string vf)
                        => folderDescs.TryGetValue(vf, out var d) && !string.IsNullOrEmpty(d) ? $"（{d}）" : "";

                    string CurrentLabel()
                    {
                        if (variantPrefBuffer.TryGetValue(topName, out var f) && !string.IsNullOrEmpty(f))
                            return $"当前：{f}{DescSuffix(f)}";
                        return $"跟随默认（{repFolder}{DescSuffix(repFolder)}）";
                    }

                    var pickBtn = new Button
                    {
                        Content = CurrentLabel(),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                    };
                    System.Windows.Controls.Grid.SetColumn(pickBtn, 1);
                    rowGrid.Children.Add(pickBtn);

                    pickBtn.Click += async (_, _) =>
                    {
                        try
                        {
                            var optionPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(8) };
                            optionPanel.Children.Add(new TextBlock
                            {
                                Text = $"为「{topName}」选择变体（整个文件夹下所有线路都跑此变体）：",
                                Margin = new Thickness(0, 0, 0, 8),
                                TextWrapping = TextWrapping.Wrap
                            });
                            string? chosen = variantPrefBuffer.TryGetValue(topName, out var cur) ? cur : null;
                            var group = "variant_" + topName;

                            var rbDefault = new System.Windows.Controls.RadioButton
                            {
                                Content = $"跟随默认（{repFolder}{DescSuffix(repFolder)}）",
                                GroupName = group,
                                Margin = new Thickness(0, 2, 0, 2),
                                IsChecked = string.IsNullOrEmpty(chosen)
                            };
                            optionPanel.Children.Add(rbDefault);
                            var folderRadios = new List<System.Windows.Controls.RadioButton>();
                            foreach (var f in availableFolders)
                            {
                                var rb = new System.Windows.Controls.RadioButton
                                {
                                    Content = $"{f}{DescSuffix(f)}",
                                    GroupName = group,
                                    Tag = f,
                                    Margin = new Thickness(0, 2, 0, 2),
                                    IsChecked = string.Equals(chosen, f, StringComparison.Ordinal)
                                };
                                folderRadios.Add(rb);
                                optionPanel.Children.Add(rb);
                            }

                            var pickDialog = new Wpf.Ui.Controls.MessageBox
                            {
                                Title = $"选择变体 - {topName}",
                                Content = optionPanel,
                                PrimaryButtonText = "确定",
                                CloseButtonText = "取消",
                                Owner = Application.Current.MainWindow,
                                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            };
                            var r = await pickDialog.ShowDialogAsync();
                            if (r != MessageBoxResult.Primary) return;

                            var pickedFolder = folderRadios.FirstOrDefault(x => x.IsChecked == true)?.Tag as string;
                            if (string.IsNullOrEmpty(pickedFolder))
                                variantPrefBuffer.Remove(topName);   // 跟随默认 = 清除偏好
                            else
                                variantPrefBuffer[topName] = pickedFolder!;
                            pickBtn.Content = CurrentLabel();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[变体偏好] 选择变体弹窗异常");
                        }
                    };

                    stack.Children.Add(rowGrid);
                }
                variantExpander.Content = stack;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[变体偏好] 折叠面板加载失败");
                variantExpander.Content = new TextBlock
                {
                    Text = "加载变体列表失败，请查看日志",
                    Margin = new Thickness(8),
                    Foreground = SystemColors.GrayTextBrush
                };
            }
        }
        variantExpander.Expanded += (_, _) => BuildVariantPanelContent();
        // C7: 供导入成功后复用（OQ-5）。IsExpanded 守卫在委托内，避免导入按钮闭包前向引用 variantExpander。
        refreshVariantPanel = () => { if (variantExpander.IsExpanded) BuildVariantPanelContent(); };
        rootPanel.Children.Add(variantExpander);

        modeToggle.Checked += (_, _) =>
        {
            soloPanel.Visibility = Visibility.Collapsed;
            mpPanel.Visibility = Visibility.Visible;
        };
        modeToggle.Unchecked += (_, _) =>
        {
            soloPanel.Visibility = Visibility.Visible;
            mpPanel.Visibility = Visibility.Collapsed;
        };

        var dialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = "修改独立任务配置 - 锄地一条龙",
            Width = 520,
            Content = new ScrollViewer
            {
                Content = rootPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 600,
            },
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        dialog.ShowDialogAsync().ContinueWith(t =>
        {
            if (t.Result != MessageBoxResult.Primary) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var isMpMode = modeToggle.IsChecked ?? false;
                settings["multiplayerEnabled"] = isMpMode;

                if (isMpMode)
                {
                    // 保存联机配置
                    settings["multiplayerRole"] = roleCombo.SelectedIndex == 0 ? "host" : "member";
                    settings["memberJoinMode"] = joinModeCombo.SelectedIndex == 0 ? "byHostName" : "random";
                    settings["targetHostName"] = targetHostBox.Text;
                    settings["coordinatorServerUrl"] = serverUrlBox.Text;
                    settings["playerName"] = playerNameBox.Text;
                    settings["playerUid"] = playerUidBox.Text;
                    settings["multiplayerPartyName"] = multiplayerPartyNameBox.Text;
                    settings["multiplayerStartAvatarName"] = multiplayerStartAvatarNameBox.Text;
                    var isHostRole = roleCombo.SelectedIndex == 0;
                    var timeoutText = isHostRole ? partyTimeoutBox.Text : memberPartyTimeoutBox.Text;
                    if (int.TryParse(timeoutText, out var pt)) settings["partyTimeoutSeconds"] = pt;
                    settings["partyTimeoutAction"] = timeoutActionCombo.SelectedIndex;
                    if (int.TryParse(expectedCountBox.Text, out var ec)) settings["expectedPlayerCount"] = ec;
                    settings["roomWhitelist"] = whitelistBox.Text;
                    if (int.TryParse(syncTimeoutBox.Text, out var st)) settings["syncTimeoutSeconds"] = st;
                    if (int.TryParse(minPlayersBox.Text, out var mp)) settings["minPlayersToSync"] = mp;
                    if (double.TryParse(syncPointMinDistBox.Text, out var spd)) settings["syncPointMinDistance"] = spd;
                    if (int.TryParse(startRouteIndexBox.Text, out var sri)) settings["startRouteIndex"] = sri;
                    settings["enableKazuhaSync"] = enableKazuhaSyncCheck.IsChecked ?? false;
                    settings["multiplayerUseFixedFightStrategy"] = useFixedFightStrategyCheck.IsChecked ?? true;
                    if (int.TryParse(kazuhaSyncWaitSecondsBox.Text, out var ksw)) settings["kazuhaSyncWaitSeconds"] = ksw;
                    if (int.TryParse(kazuhaSyncTimeoutSecondsBox.Text, out var kst)) settings["kazuhaSyncTimeoutSeconds"] = kst;
                    if (int.TryParse(kazuhaWaitSkillCdSecondsBox.Text, out var kwc)) settings["kazuhaWaitSkillCdSeconds"] = kwc;
                    if (int.TryParse(kazuhaSecondApproachMaxStepsBox.Text, out var ksam)) settings["kazuhaSecondApproachMaxSteps"] = ksam;
                    if (int.TryParse(fightTimeoutBox.Text, out var fts)) settings["fightTimeoutSeconds"] = fts;
                    // === 快速同步点抢报（multiplayer-fast-sync-host-controlled spec FR4）===
                    settings["fastSyncPointEnabled"] = fastSyncEnabledCheck.IsChecked ?? false;
                    if (double.TryParse(fastSyncPathingDistanceBox.Text, out var fspd)) settings["fastSyncPathingDistance"] = fspd;
                    if (int.TryParse(fastSyncTeleportLoadingDelayBox.Text, out var fstd)) settings["fastSyncTeleportLoadingDelayMs"] = fstd;
                    settings["debugMode"] = debugModeCheck.IsChecked ?? false;
                    // route-mode-dropdown: 下拉选中项 → useFixedDebugRoutes 布尔（方案 A 重映射）
                    settings["useFixedDebugRoutes"] = BetterGenshinImpact.GameTask.AutoHoeing.Services.RouteModeDecisions.MapRouteModeToUseFixed(routeModeCombo.SelectedItem?.ToString());
                    settings["fixedDebugRoutePath"] = fixedRoutePathBox.Text;
                    // 保存选中的内置线路
                    if (settings.ContainsKey("selectedBuiltinRoute"))
                    {
                        // selectedBuiltinRoute 已在按钮点击事件中设置
                    }
                    else
                    {
                        settings["selectedBuiltinRoute"] = GetStr("selectedBuiltinRoute", globalCfg.SelectedBuiltinRoute);
                    }
                    settings["multiWorldEnabled"] = multiWorldCheck.IsChecked ?? false;
                    if (int.TryParse(multiWorldCountBox.Text, out var mwc)) settings["multiWorldCount"] = mwc;
                    // 拾取模式：与 AutoHoeingTask.ApplySettingsOverride 第 2881 行 Get("pickupMode", _config.PickupMode) 对齐
                    settings["pickupMode"] = pickupModeCombo.SelectedItem?.ToString();
                    // route-mode-dropdown: 始终写入执行线路（groupIndex）下拉的当前值（与 soloPanel 共用键，类比 pickupMode）。
                    // 运行时 ApplySettingsOverride 的 Get("groupIndex","") → groupMap 仅在 UseFixedDebugRoutes==false 分支影响选路；
                    // UseFixedDebugRoutes==true 时运行时走 LoadRoutesBasedOnConfig，不读 GroupIndex，写入无副作用。
                    settings["groupIndex"] = groupIndexCombo.SelectedItem?.ToString();
                }
                else
                {
                    // 保存单机配置，同时清除联机专属字段避免残留影响
                    foreach (var setting in settingItems)
                    {
                        if (!controls.TryGetValue(setting.Name, out var ctrl)) continue;
                        object? value = ctrl switch
                        {
                            System.Windows.Controls.ComboBox combo => combo.SelectedItem?.ToString(),
                            System.Windows.Controls.CheckBox check => check.IsChecked ?? false,
                            TextBox tb => setting.Type == "number"
                                ? double.TryParse(tb.Text, out var n) ? (object)n : tb.Text
                                : tb.Text,
                            _ => null
                        };
                        settings[setting.Name] = value;
                    }
                    // 清除联机专属字段，防止残留值在单机模式下被 ApplySettingsOverride 错误应用
                    // 注意：保留 "multiplayerRole" 和 "memberJoinMode"，避免单机保存破坏联机角色配置
                    foreach (var key in new[] {
                        "targetHostName", "coordinatorServerUrl",
                        "playerName", "playerUid", "multiplayerPartyName", "multiplayerStartAvatarName", 
                        "expectedPlayerCount", "roomWhitelist",
                        "partyTimeoutSeconds", "partyTimeoutAction", "syncTimeoutSeconds", "minPlayersToSync",
                        "syncPointMinDistance", "startRouteIndex", "enableKazuhaSync", "multiplayerUseFixedFightStrategy",
                        "kazuhaSyncWaitSeconds", "kazuhaSyncTimeoutSeconds", "kazuhaWaitSkillCdSeconds",
                        "fightTimeoutSeconds",
                        // === 快速同步点抢报（单机模式清除）===
                        "fastSyncPointEnabled", "fastSyncPathingDistance", "fastSyncTeleportLoadingDelayMs",
                        "debugMode", "useFixedDebugRoutes", "fixedDebugRoutePath", "selectedBuiltinRoute",
                        "multiWorldEnabled", "multiWorldCount"
                    })
                        settings.Remove(key);
                }

                // route-variant-sync-by-logical-id spec / §15.7 / R15.5：
                // 变体偏好（基名→变体文件夹名）写入配置组 settings，并镜像到全局作兜底。
                // 不随联机/单机分支清除（运行时仅联机生效，存着无害）。
                if (variantPrefBuffer.Count > 0)
                {
                    settings["variantPreferences"] = new Dictionary<string, string>(variantPrefBuffer, StringComparer.Ordinal);
                    var gcfg = TaskContext.Instance().Config.AutoHoeingConfig;
                    foreach (var (k, v) in variantPrefBuffer)
                        gcfg.SetVariantPreference(k, v);   // 镜像到全局兜底
                }
                else
                {
                    settings.Remove("variantPreferences");
                }

                foreach (var group in ScriptGroups)
                    WriteScriptGroup(group);
                Toast.Success("独立任务配置已保存");
            });
        });
    }
    [RelayCommand]
    public async void OnDeleteScriptByFolder(ScriptGroupProject? item)
    {
        if (item == null)
        {
            return;
        }

        if (SelectedScriptGroup != null)
        {
            var toBeDeletedProjects = SelectedScriptGroup.Projects
                .Where(item2 => item2.FolderName == item.FolderName)
                .ToList();

            foreach (var project in toBeDeletedProjects)
            {
                SelectedScriptGroup.Projects.Remove(project);
            }

            _snackbarService.Show(
                "脚本配置移除成功",
                $"已移除 {item.FolderName} 下的所有关联配置",
                ControlAppearance.Success,
                null,
                TimeSpan.FromSeconds(2)
            );
        }
    }

    [RelayCommand]
    public void OnDeleteScript(ScriptGroupProject? item)
    {
        if (item == null)
        {
            return;
        }

        SelectedScriptGroup?.Projects.Remove(item);
        _snackbarService.Show(
            "脚本配置移除成功",
            $"{item.Name} 的关联配置已经移除",
            ControlAppearance.Success,
            null,
            TimeSpan.FromSeconds(2)
        );
    }

    [RelayCommand]
    public void OnOpenScriptFolder(ScriptGroupProject? item)
    {
        if (item == null)
        {
            return;
        }

        try
        {
            string? path = null;
            switch (item.Type)
            {
                case "Javascript":
                    path = Path.Combine(Global.ScriptPath(), item.FolderName);
                    break;
                case "KeyMouse":
                    path = Global.Absolute(@"User\KeyMouseScript");
                    break;
                case "Pathing":
                    path = Path.Combine(MapPathingViewModel.PathJsonPath, item.FolderName);
                    break;
            }

            if (path != null && Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                _snackbarService.Show("打开失败", "目录不存在", ControlAppearance.Caution, null, TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "打开脚本目录失败");
            _snackbarService.Show("打开失败", e.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
        }
    }
    public void ScriptGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (ScriptGroup newItem in e.NewItems)
            {
                newItem.Projects.CollectionChanged += ScriptProjectsCollectionChanged;
                foreach (var project in newItem.Projects)
                {
                    project.PropertyChanged += ScriptProjectsPChanged;
                }
            }
        }

        if (e.OldItems != null)
        {
            foreach (ScriptGroup oldItem in e.OldItems)
            {
                foreach (var project in oldItem.Projects)
                {
                    project.PropertyChanged -= ScriptProjectsPChanged;
                }

                oldItem.Projects.CollectionChanged -= ScriptProjectsCollectionChanged;
            }
        }

        // 补充排序字段
        var i = 1;
        foreach (var group in ScriptGroups)
        {
            group.Index = i++;
        }

        // 保存配置组配置
        foreach (var group in ScriptGroups)
        {
            WriteScriptGroup(group);
        }
    }

    private void ScriptProjectsPChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var group in ScriptGroups)
        {
            WriteScriptGroup(group);
        }
    }

    private void ScriptProjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 补充排序字段
        if (SelectedScriptGroup is { Projects.Count: > 0 })
        {
            var i = 1;
            foreach (var project in SelectedScriptGroup.Projects)
            {
                project.Index = i++;
            }
        }

        // 保存配置组配置
        if (SelectedScriptGroup != null)
        {
            WriteScriptGroup(SelectedScriptGroup);
        }
    }


    private void WriteScriptGroup(ScriptGroup scriptGroup)
    {
        try
        {
            if (!Directory.Exists(ScriptGroupPath))
            {
                Directory.CreateDirectory(ScriptGroupPath);
            }

            var file = Path.Combine(ScriptGroupPath, $"{scriptGroup.Name}.json");
            File.WriteAllText(file, scriptGroup.ToJson());
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "保存配置组配置时失败");
            _snackbarService.Show(
                "保存配置组配置失败",
                $"{scriptGroup.Name} 保存失败！",
                ControlAppearance.Danger,
                null,
                TimeSpan.FromSeconds(3)
            );
        }
    }

    private static void SetTaskContextNextFlag(ScriptGroup group)
    {
        var nst = TaskContext.Instance().Config.NextScheduledTask.Find(item => item.Item1 == group.Name);
        foreach (var item in group.Projects)
        {
            item.NextFlag = false;
            if (nst != default)
            {
                if (nst.Item2 == item.Index && nst.Item3 == item.FolderName && nst.Item4 == item.Name)
                {
                    item.NextFlag = true;
                }
            }
        }
    }

    private void ReadScriptGroup()
    {
        try
        {
            if (!Directory.Exists(ScriptGroupPath))
            {
                Directory.CreateDirectory(ScriptGroupPath);
            }

            ScriptGroups.Clear();
            var files = Directory.GetFiles(ScriptGroupPath, "*.json");
            List<ScriptGroup> groups = [];
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var group = ScriptGroup.FromJson(json);
                    SetTaskContextNextFlag(group);
                    if (group.Name == TaskContext.Instance().Config.NextScriptGroupName)
                    {
                        group.NextFlag = true;
                    }
                    groups.Add(group);
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "读取单个配置组配置时失败");
                    _snackbarService.Show(
                        "读取配置组配置失败",
                        "读取配置组配置失败:" + e.Message,
                        ControlAppearance.Danger,
                        null,
                        TimeSpan.FromSeconds(3)
                    );
                }
            }

            // 按index排序
            groups.Sort((a, b) => a.Index.CompareTo(b.Index));
            foreach (var group in groups)
            {
                ScriptGroups.Add(group);
            }
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "读取配置组配置时失败");
            _snackbarService.Show(
                "读取配置组配置失败",
                "读取配置组配置失败！",
                ControlAppearance.Danger,
                null,
                TimeSpan.FromSeconds(3)
            );
        }
    }

    [RelayCommand]
    public void OnGoToScriptGroupUrl()
    {
        Process.Start(new ProcessStartInfo("https://www.bettergi.com/feats/autos/dispatcher.html") { UseShellExecute = true });
    }

    [RelayCommand]
    public void OnImportScriptGroup(string scriptGroupExample)
    {
        ScriptGroup group = new();
        if ("AutoCrystalflyExampleGroup" == scriptGroupExample)
        {
            group.Name = "晶蝶示例组";
            group.AddProject(new ScriptGroupProject(new ScriptProject("AutoCrystalfly")));
        }

        if (ScriptGroups.Any(x => x.Name == group.Name))
        {
            _snackbarService.Show(
                "配置组已存在",
                $"配置组 {group.Name} 已经存在，请勿重复添加",
                ControlAppearance.Caution,
                null,
                TimeSpan.FromSeconds(2)
            );
            return;
        }

        ScriptGroups.Add(group);
    }

    [RelayCommand]
    public async Task OnStartScriptGroupAsync()
    {
        if (SelectedScriptGroup == null)
        {
            _snackbarService.Show(
                "未选择配置组",
                "请先选择一个配置组",
                ControlAppearance.Caution,
                null,
                TimeSpan.FromSeconds(2)
            );
            return;
        }

        RunnerContext.Instance.Reset();

        TaskProgress taskProgress = new()
        {
            ScriptGroupNames = [SelectedScriptGroup.Name]
        };
        RunnerContext.Instance.taskProgress = taskProgress;
        taskProgress.CurrentScriptGroupName = SelectedScriptGroup.Name;
        TaskProgressManager.SaveTaskProgress(taskProgress);
        await _scriptService.RunMulti(GetNextProjects(SelectedScriptGroup), SelectedScriptGroup.Name, taskProgress);
    }

    [RelayCommand]
    public void OnOpenScriptGroupSettings()
    {
        if (SelectedScriptGroup == null)
        {
            return;
        }

        // var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        // {
        //     Content = new ScriptGroupConfigView(SelectedScriptGroup.Config),
        //     Title = "配置组设置"
        // };
        //
        // await uiMessageBox.ShowDialogAsync();

        var dialogWindow = new FluentWindow
        {
            Title = "配置组设置",
            Content = new ScriptGroupConfigView(new ScriptGroupConfigViewModel(TaskContext.Instance().Config, SelectedScriptGroup.Config)),
            Width = 800,
            Height = 600,
            MinWidth = 800,
            MaxWidth = 800,
            MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ExtendsContentIntoTitleBar = true,
            WindowBackdropType = WindowBackdropType.Auto,
        };
        dialogWindow.SourceInitialized += (s, e) => WindowHelper.TryApplySystemBackdrop(dialogWindow);

        // var dialogWindow = new WpfUiWindow(new ScriptGroupConfigView(SelectedScriptGroup.Config))
        // {
        //     Title = "配置组设置"
        // };

        // 显示对话框
        var result = dialogWindow.ShowDialog();

        // if (result == true)
        // {
        //     // 用户点击了确定或关闭
        // }

        WriteScriptGroup(SelectedScriptGroup);
    }

    public static List<ScriptGroup> GetNextScriptGroups(List<ScriptGroup> groups)
    {
        if (groups.Where(g => g.NextFlag).Count() > 0)
        {
            List<ScriptGroup> ng = new();
            bool start = false;
            foreach (var group in groups)
            {
                if (group.NextFlag)
                {
                    start = true;
                    group.NextFlag = false;
                    TaskContext.Instance().Config.NextScriptGroupName = String.Empty;
                }

                if (start)
                {
                    ng.Add(group);
                }
            }

            return ng;
        }

        return groups;
    }

    public static List<ScriptGroupProject> GetNextProjects(ScriptGroup group)
    {
        SetTaskContextNextFlag(group);
        List<ScriptGroupProject> ls = new List<ScriptGroupProject>();
        if (group.Projects.Where(g => g.NextFlag ?? false).Count() > 0)
        {
            bool start = false;
            foreach (var item in group.Projects)
            {
                if (item.NextFlag ?? false)
                {
                    start = true;
                }

                if (!start)
                {
                    item.SkipFlag = true;
                }
                ls.Add(item);
            }

            if (!start)
            {
                ls.AddRange(group.Projects);
            }

            //拿出来后清空，和置状态
            if (start)
            {
                List<ValueTuple<string, int, string, string>> nextScheduledTask = TaskContext.Instance().Config.NextScheduledTask;
                foreach (var item in nextScheduledTask)
                {
                    if (item.Item1 == group.Name)
                    {
                        nextScheduledTask.Remove(item);
                        break;
                    }
                }

                foreach (var item in group.Projects)
                {
                    item.NextFlag = false;
                }
            }

            return ls;
        }

        return group.Projects.Select(g => g).ToList();
    }

    [RelayCommand]
    public async Task OnContinueMultiScriptGroupAsync()
    {

        // 创建一个 StackPanel 来包含全选按钮和所有配置组的 CheckBox
        var stackPanel = new StackPanel();


        // 添加分割线
        var separator = new Separator
        {
            Margin = new Thickness(0, 4, 0, 4)
        };
        stackPanel.Children.Add(separator);

        List<TaskProgress> taskProgresses = TaskProgressManager.LoadAllTaskProgress();
        var checkBox = new ComboBox(); ;
        stackPanel.Children.Add(checkBox);
        ObservableCollection<KeyValuePair<string, string>> kvs = new ObservableCollection<KeyValuePair<string, string>>();
        foreach (var taskProgress in taskProgresses)
        {
            var name = taskProgress.Name + "_" + taskProgress.CurrentScriptGroupName + "_";
            if (taskProgress.Loop)
            {
                name += "循环(" + taskProgress.LoopCount + ")_";
            }
            if (taskProgress.CurrentScriptGroupProjectInfo != null)
            {
                name = name + taskProgress.CurrentScriptGroupProjectInfo.Index + "_" + taskProgress.CurrentScriptGroupProjectInfo.Name;
            }
            kvs.Add(new KeyValuePair<string, string>(taskProgress.Name, name));
        }

        checkBox.SelectedValuePath = "Key";
        checkBox.DisplayMemberPath = "Value";
        checkBox.ItemsSource = kvs;
        checkBox.SelectedIndex = 0;
        //SelectedValuePath="Key"
        // DisplayMemberPath="Value"
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "选择需要继续执行的进度记录",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 300 // 设置固定高度
                ,
                Width = 600
            },
            CloseButtonText = "关闭",
            PrimaryButtonText = "确认执行",
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var result = await uiMessageBox.ShowDialogAsync();
        if (result == MessageBoxResult.Primary)
        {

            /*var selectedGroups = checkBoxes
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => kv.Key)
                .ToList();*/
            Object val = checkBox.SelectedValue;
            if (val == null)
            {
                return;
            }
            await OnContinueTaskProgressAsync(Convert.ToString(val), taskProgresses);

        }
    }

    public async Task OnContinueTaskProgressAsync(string name, List<TaskProgress>? taskProgresses = null)
    {
        if (taskProgresses == null)
        {
            taskProgresses = TaskProgressManager.LoadAllTaskProgress();
        }
        TaskProgress? taskProgress = null;
        if (name == "latest")
        {
            if (taskProgresses.Count > 0)
            {
                taskProgress = taskProgresses[0];
            }
        }
        else
        {
            taskProgress = taskProgresses.FirstOrDefault(t => t.Name == name);
        }



        if (taskProgress != null)
        {
            //await StartGroups(selectedGroups);
            //taskProgress.Next
            var sg = ScriptGroups.ToList().Where(sg => taskProgress.ScriptGroupNames.Contains(sg.Name)).ToList();
            TaskProgressManager.GenerNextProjectInfo(taskProgress, sg);
            if (taskProgress.Next == null)
            {
                _logger.LogWarning("无法定位到下一个要执行的项目：next为空（" + taskProgress.Name + ")");
            }
            else
            {
                await StartGroups(sg, taskProgress);
            }

        }
        else
        {
            _logger.LogWarning("无法定位到下一个要执行的项目:taskProgress为空");
        }
    }

    public async Task OnStartMultiScriptTaskProgressAsync(params string[] names)
    {
        if (ScriptGroups.Count == 0)
        {
            ReadScriptGroup();
        }

        string taskProgressName;
        if (names == null || names.Length == 0)
        {
            taskProgressName = "latest";
        }
        else
        {
            taskProgressName = names[0];
        }

        await OnContinueTaskProgressAsync(taskProgressName);
    }

    [RelayCommand]
    public async Task OnStartMultiScriptGroupAsync()
    {
        // 创建一个 StackPanel 来包含全选按钮和所有配置组的 CheckBox
        var stackPanel = new StackPanel();
        var checkBoxes = new Dictionary<ScriptGroup, CheckBox>();


        var loopCheckBox = new CheckBox
        {
            Content = "循环",
        };


        // 创建全选按钮
        var selectAllCheckBox = new CheckBox
        {
            Content = "全选",
            IsThreeState = true
        };
        selectAllCheckBox.Checked += (s, e) =>
        {
            foreach (var checkBox in checkBoxes.Values)
            {
                checkBox.IsChecked = true;
            }
        };
        selectAllCheckBox.Unchecked += (s, e) =>
        {
            foreach (var checkBox in checkBoxes.Values)
            {
                checkBox.IsChecked = false;
            }
        };
        selectAllCheckBox.Indeterminate += (s, e) =>
        {
            if (checkBoxes.Values.All(cb => cb.IsChecked == true))
            {
                selectAllCheckBox.IsChecked = false;
            }
            else if (checkBoxes.Values.All(cb => cb.IsChecked == false))
            {
                selectAllCheckBox.IsChecked = true;
            }
        };

        stackPanel.Children.Add(loopCheckBox);
        stackPanel.Children.Add(selectAllCheckBox);

        // 添加分割线
        var separator = new Separator
        {
            Margin = new Thickness(0, 4, 0, 4)
        };
        stackPanel.Children.Add(separator);

        // 创建每个配置组的 CheckBox
        foreach (var scriptGroup in ScriptGroups)
        {
            if (scriptGroup.Config.PathingConfig.HideOnRepeat)
            {
                continue;
            }
            var checkBox = new CheckBox
            {
                Content = scriptGroup.Name,
                Tag = scriptGroup
            };
            checkBoxes[scriptGroup] = checkBox;
            stackPanel.Children.Add(checkBox);

            checkBox.Checked += (s, e) => UpdateSelectAllCheckBoxState();
            checkBox.Unchecked += (s, e) => UpdateSelectAllCheckBoxState();
        }

        void UpdateSelectAllCheckBoxState()
        {
            int checkedCount = checkBoxes.Values.Count(cb => cb.IsChecked == true);
            if (checkedCount == 0)
            {
                selectAllCheckBox.IsChecked = false;
            }
            else if (checkedCount == checkBoxes.Count)
            {
                selectAllCheckBox.IsChecked = true;
            }
            else
            {
                selectAllCheckBox.IsChecked = null;
            }
        }

        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "选择需要执行的配置组",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 300 // 设置固定高度
            },
            CloseButtonText = "关闭",
            PrimaryButtonText = "确认执行",
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var result = await uiMessageBox.ShowDialogAsync();
        if (result == MessageBoxResult.Primary)
        {
            var selectedGroups = checkBoxes
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => kv.Key)
                .ToList();

            if (selectedGroups.Count == 0)
            {
                _snackbarService.Show(
                    "未选择配置组",
                    "请至少选择一个配置组进行执行",
                    ControlAppearance.Caution,
                    null,
                    TimeSpan.FromSeconds(3)
                );
                return;
            }
            await StartGroups(selectedGroups, null, loopCheckBox.IsChecked ?? false);
        }
    }

    private void SelectAllCheckBox_Indeterminate(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    public async Task OnStartMultiScriptGroupWithNamesAsync(params string[] names)
    {
        if (ScriptGroups.Count == 0)
        {
            ReadScriptGroup();
        }
        List<ScriptGroup> scriptGroups = new List<ScriptGroup>();
        foreach (var name in names)
        {
            try
            {
                var group = ScriptGroups.First(x => x.Name == name);
                scriptGroups.Add(group);
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("传入的配置组名称不存在:{Name}", name);
            }
        }

        if (scriptGroups.Count > 0)
        {
            await StartGroups(scriptGroups);
        }
        else
        {
            _logger.LogWarning("需要执行的配置组为空");
        }
    }

    public async Task StartGroups(List<ScriptGroup> scriptGroups, TaskProgress? taskProgress = null, bool loop = false)
    {
        _logger.LogInformation("开始连续执行选中配置组:{Names}", string.Join(",", scriptGroups.Select(x => x.Name)));
        try
        {
            RunnerContext.Instance.IsContinuousRunGroup = true;
            if (taskProgress == null)
            {
                taskProgress = new()
                {
                    ScriptGroupNames = scriptGroups.Select(x => x.Name).ToList()
                    ,
                    Loop = loop
                };
            }

            RunnerContext.Instance.taskProgress = taskProgress;
            var sg = GetNextScriptGroups(scriptGroups);
            foreach (var scriptGroup in sg)
            {
                if (taskProgress.Next != null)
                {
                    if (scriptGroup.Name != taskProgress.Next.GroupName)
                    {
                        continue;
                    }
                }
                taskProgress.CurrentScriptGroupName = scriptGroup.Name;
                TaskProgressManager.SaveTaskProgress(taskProgress);
                await _scriptService.RunMulti(GetNextProjects(scriptGroup), scriptGroup.Name, taskProgress);
                await Task.Delay(2000);
            }

            taskProgress.LoopCount++;
            if (taskProgress is { Loop: true })
            {
                taskProgress.LastScriptGroupName = null;
                taskProgress.LastSuccessScriptGroupProjectInfo = null;
                taskProgress.Next = null;
                await StartGroups(scriptGroups, taskProgress);
            }
            else
            {
                //只有最后一次成功才算
                if (taskProgress.ConsecutiveFailureCount == 0)
                {
                    taskProgress.EndTime = DateTime.Now;
                    TaskProgressManager.SaveTaskProgress(taskProgress);
                }

            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
        }
        finally
        {
            RunnerContext.Instance.Reset();
        }


    }
}
