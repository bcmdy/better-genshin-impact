﻿using BetterGenshinImpact.ViewModel.Pages;
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
    
    private DefaultAutoFightConfig ViewModelDefaultAuto { get; }

    public CommonSettingsPage(CommonSettingsPageViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;
        ViewModelDefaultAuto = new DefaultAutoFightConfig();
        InitializeComponent();
    }
    
    private void PopupCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Popup.IsOpen = false;
    }
    
    private async void AddButtonClick(object sender, RoutedEventArgs e)
    {
        if (new[] { NameTextBox.Text, NameEnTextBox.Text, WeaponTextBox.Text, AliasTextBox.Text, SkillCdTextBox.Text, 
                SkillHoldCdTextBox.Text, BurstCdTextBox.Text }.Any(string.IsNullOrEmpty))
        {
            Toast.Warning("请填写所有项！");
            return;
        }

        var validWeaponTypes = new HashSet<string> { "1", "10", "11", "12", "13" };
        if (!validWeaponTypes.Contains(WeaponTextBox.Text))
        {
            Toast.Warning("请选择正确的武器类型:\n单手剑：1\n法器：10 \n双手剑：11\n弓箭：12\n长柄武器：13");
            return;
        }
        
        var json = File.ReadAllText(Global.Absolute(@"GameTask\AutoFight\Assets\combat_avatar.json"));
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<CombatAvatar>>(json) ??
                     throw new Exception("combat_avatar.json deserialize failed");
        
        var maxId = config
            .Where(avatar => avatar.Id.StartsWith("3000")) 
            .DefaultIfEmpty(new CombatAvatar { Id = "30000000" }) 
            .Max(avatar => avatar.Id);
            
        var newAvatar = new CombatAvatar
        {
            Id = (int.Parse(maxId) + 1).ToString(),
            Name = NameTextBox.Text,
            NameEn = NameEnTextBox.Text,
            Weapon = WeaponTextBox.Text,
            SkillCd = double.Parse(SkillCdTextBox.Text),
            SkillHoldCd = double.Parse(SkillHoldCdTextBox.Text),
            BurstCd = double.Parse(BurstCdTextBox.Text),
            Alias = new List<string> { AliasTextBox.Text }
        };
        
        var nameList = config.Select(x => x.Name).ToList();
        var nameEnList = config.Select(x => x.NameEn).ToList();
        var aliasList = config.SelectMany(x => x.Alias).ToList();

        while (nameList.Contains(newAvatar.Name) || nameEnList.Contains(newAvatar.NameEn) || aliasList.Contains(newAvatar.Name))
        {
            Toast.Information("该名称或英文名称或别名已经存在，请重新输入！");
            return;
        }
        
        ViewModelDefaultAuto.AddCombatAvatar(newAvatar);
        ViewModelDefaultAuto.UpdateCombatAvatarList();
        Toast.Success($"自定义角色 {NameTextBox.Text} 添加成功！");
        Popup.IsOpen = false;
    }
    
    private void RemoveButtonClick(object sender, RoutedEventArgs e)
    {
        var json = File.ReadAllText(Global.Absolute(@"GameTask\AutoFight\Assets\combat_avatar.json"));
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<CombatAvatar>>(json) ??
                     throw new Exception("combat_avatar.json deserialize failed");
        var list = config.Where(x => x.Id.StartsWith("3000")).Select(x => x.Name).ToList();
        
        if (list.Count == 0)
        {
            Toast.Warning("没有可删除的自定义角色！");
            return; 
        }
    
        var stackPanel = new StackPanel();
        var comboBox1 = new ComboBox { ItemsSource = list, SelectedItem = list?[0] };
        stackPanel.Children.Add(comboBox1);
    
        var result = PromptDialog.Prompt("请选择要删除的自定义角色？", "删除自定义角色", stackPanel, new Size(400, 200), null);
        if (comboBox1.SelectedItem != null && !string.IsNullOrEmpty(result) )
        {
            var taskToDelete = comboBox1.SelectedItem.ToString();
            var newList = config.Where(x => x.Name != taskToDelete).ToList();
            File.WriteAllText(Global.Absolute(@"GameTask\AutoFight\Assets\combat_avatar.json"), Newtonsoft.Json.JsonConvert.SerializeObject(newList, Newtonsoft.Json.Formatting.Indented));
            
            ViewModelDefaultAuto.UpdateCombatAvatarList();
            Toast.Success("已删除自定义角色：" + taskToDelete);
        }
    }
   
}
