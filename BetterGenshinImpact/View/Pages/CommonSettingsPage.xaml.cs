using BetterGenshinImpact.ViewModel.Pages;
using System.Windows.Controls;
using System.Windows.Input;
using BetterGenshinImpact.GameTask.AutoFight.Config;
using System.Collections.Generic;
using System.Windows;
using Wpf.Ui.Violeta.Controls;
using BetterGenshinImpact.Core.Config;
using System;
using System.IO;
using System.Linq;
using BetterGenshinImpact.View.Windows;

namespace BetterGenshinImpact.View.Pages;

public partial class CommonSettingsPage : Page
{
    private CommonSettingsPageViewModel ViewModel { get; }
    
    private DefaultAutoFightConfig viewModelF { get; }

    public CommonSettingsPage(CommonSettingsPageViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;
        viewModelF = new DefaultAutoFightConfig();
        InitializeComponent();
    }
    
    private async void AddButtonClick(object sender, RoutedEventArgs e)
    {
        var json = File.ReadAllText(Global.Absolute(@"GameTask\AutoFight\Assets\combat_avatar.json"));
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<CombatAvatar>>(json) ??
                     throw new Exception("combat_avatar.json deserialize failed");
        var show = false;
        
        string maxId = config
            .Where(avatar => avatar.Id.StartsWith("3000")) // 假设ID是8位数，且3000开头的是3000xxxx
            .DefaultIfEmpty(new CombatAvatar { Id = "30000000" }) // 如果没有找到合适的ID，则使用默认值30000000
            .Max(avatar => avatar.Id);

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10, 0, 10, 10)
        };

        // 创建一个 Grid 来包含 Name 输入框及其标签
        var nameGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nameLabel = new TextBlock
        {
            Text = "名称:",
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var nameTextBox = new TextBox
        {
            Text = "自定义角色",
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        nameGrid.Children.Add(nameLabel);
        nameGrid.Children.Add(nameTextBox);
        Grid.SetColumn(nameLabel, 0);
        Grid.SetColumn(nameTextBox, 1);

        stackPanel.Children.Add(nameGrid);

        // 创建一个 Grid 来包含 NameEn 输入框及其标签
        var nameEnGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        nameEnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nameEnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nameEnLabel = new TextBlock
        {
            Text = "英文名称:",
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var nameEnTextBox = new TextBox
        {
            Text = "CustomAvatar",
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        nameEnGrid.Children.Add(nameEnLabel);
        nameEnGrid.Children.Add(nameEnTextBox);
        Grid.SetColumn(nameEnLabel, 0);
        Grid.SetColumn(nameEnTextBox, 1);

        stackPanel.Children.Add(nameEnGrid);

        // 创建一个 Grid 来包含 Weapon 输入框及其标签
        var weaponGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        weaponGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        weaponGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        var weaponLabel = new TextBlock
        {
            Text = "武器类型:",
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var weaponTextBox = new TextBox
        {
            Text = "13",
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        weaponTextBox.PreviewMouseLeftButtonDown += (sender, args) =>
        {
            if (!show)
            {
                show = true;
                MessageBox.Show("以下为各种武器的代码：\n单手剑：1\n法器：10 \n双手剑：11\n弓箭：12\n长柄武器：13");
            }
        };
        
        weaponGrid.Children.Add(weaponLabel);
        weaponGrid.Children.Add(weaponTextBox);
        Grid.SetColumn(weaponLabel, 0);
        Grid.SetColumn(weaponTextBox, 1);
        
        stackPanel.Children.Add(weaponGrid);

        // 创建一个 Grid 来包含 SkillCd 输入框及其标签
        var skillCdGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        skillCdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        skillCdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var skillCdLabel = new TextBlock
        {
            Text = "短E技能冷却时间:",
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var skillCdTextBox = new TextBox
        {
            Text = "16.0",
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        skillCdGrid.Children.Add(skillCdLabel);
        skillCdGrid.Children.Add(skillCdTextBox);
        Grid.SetColumn(skillCdLabel, 0);
        Grid.SetColumn(skillCdTextBox, 1);

        stackPanel.Children.Add(skillCdGrid);

        // 创建一个 Grid 来包含 SkillHoldCd 输入框及其标签
        var skillHoldCdGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        skillHoldCdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        skillHoldCdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var skillHoldCdLabel = new TextBlock
        {
            Text = "长E技能冷却时间:",
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var skillHoldCdTextBox = new TextBox
        {
            Text = "20.0",
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        skillHoldCdGrid.Children.Add(skillHoldCdLabel);
        skillHoldCdGrid.Children.Add(skillHoldCdTextBox);
        Grid.SetColumn(skillHoldCdLabel, 0);
        Grid.SetColumn(skillHoldCdTextBox, 1);

        stackPanel.Children.Add(skillHoldCdGrid);

        // 创建一个 Grid 来包含 BurstCd 输入框及其标签
        var burstCdGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        burstCdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        burstCdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var burstCdLabel = new TextBlock
        {
            Text = "Q技能冷却时间:",
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var burstCdTextBox = new TextBox
        {
            Text = "20.0",
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        burstCdGrid.Children.Add(burstCdLabel);
        burstCdGrid.Children.Add(burstCdTextBox);
        Grid.SetColumn(burstCdLabel, 0);
        Grid.SetColumn(burstCdTextBox, 1);

        stackPanel.Children.Add(burstCdGrid);

        // 创建一个 Grid 来包含 Alias 输入框及其标签
        var aliasGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        aliasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        aliasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var aliasLabel = new TextBlock
        {
            Text = "别名:",
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var aliasTextBox = new TextBox
        {
            Text = "自定义角色别名",
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        aliasGrid.Children.Add(aliasLabel);
        aliasGrid.Children.Add(aliasTextBox);
        Grid.SetColumn(aliasLabel, 0);
        Grid.SetColumn(aliasTextBox, 1);

        stackPanel.Children.Add(aliasGrid);
        // 创建一个滚动视图来包含 StackPanel
        var scrollViewer = new ScrollViewer
        {
            Content = stackPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        // 创建一个自定义的 MessageBox
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "添加自定义角色",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            CloseButtonText = "关闭",
            PrimaryButtonText = "确认",
            Owner = Application.Current.ShutdownMode == ShutdownMode.OnMainWindowClose ? null : Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.Width,
            MaxHeight = 470,
            MaxWidth = 300,
        };
        
        var result = await uiMessageBox.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            // 获取用户输入的值
            string name = nameTextBox.Text;
            string nameEn = nameEnTextBox.Text;
            string weapon = weaponTextBox.Text;//不设置武器没关系
            double skillCd = double.Parse(skillCdTextBox.Text);
            double skillHoldCd = double.Parse(skillHoldCdTextBox.Text);
            double burstCd = double.Parse(burstCdTextBox.Text);
            List<string> alias = new List<string> { aliasTextBox.Text };
            
            // 创建新的 CombatAvatar 对象
            CombatAvatar newAvatar = new CombatAvatar
            {
                Id = (int.Parse(maxId) + 1).ToString(),
                Name = name,
                NameEn = nameEn,
                Weapon = weapon,
                SkillCd = skillCd,
                SkillHoldCd = skillHoldCd,
                BurstCd = burstCd,
                Alias = alias
            };
            
            var nameList = config.Select(x => x.Name).ToList();
            var nameEnList = config.Select(x => x.NameEn).ToList();
            var aliasList = config.SelectMany(x => x.Alias).ToList();

            while (nameList.Contains(newAvatar.Name) || nameEnList.Contains(newAvatar.NameEn) || aliasList.Contains(newAvatar.Name))
            {
                Toast.Information("该名称或英文名称或别名已经存在，请重新输入！");
                return;
            }
            viewModelF.AddCombatAvatar(newAvatar);
            viewModelF.UpdateCombatAvatarList();
            Toast.Information("添加成功！");
        }
    }
    
    private void RemoveButtonClick(object sender, RoutedEventArgs e)
    {
        var json = File.ReadAllText(Global.Absolute(@"GameTask\AutoFight\Assets\combat_avatar.json"));
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<CombatAvatar>>(json) ??
                     throw new Exception("combat_avatar.json deserialize failed");
        var list = config.Where(x => x.Id.StartsWith("3000")).Select(x => x.Name).ToList();
        
        if (list.Count == 0)
        {
            Toast.Information("没有可删除的自定义角色！");
            return; 
        }
    
        var stackPanel = new StackPanel();
        var comboBox1 = new ComboBox { ItemsSource = list, SelectedItem = list?[0] };
        stackPanel.Children.Add(comboBox1);
    
        var result = PromptDialog.Prompt("请选择要删除的自定义角色？", "删除自定义角色", stackPanel, new Size(400, 200), null);
        if (comboBox1.SelectedItem != null && !string.IsNullOrEmpty(result) )
        {
            string taskToDelete = comboBox1.SelectedItem.ToString();
            var newList = config.Where(x => x.Name != taskToDelete).ToList();
            File.WriteAllText(Global.Absolute(@"GameTask\AutoFight\Assets\combat_avatar.json"), Newtonsoft.Json.JsonConvert.SerializeObject(newList, Newtonsoft.Json.Formatting.Indented));
            viewModelF.UpdateCombatAvatarList();
            Toast.Success("已删除自定义角色：" + taskToDelete);
        }
    }
}
