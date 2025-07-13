using BetterGenshinImpact.ViewModel.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterGenshinImpact.View.Windows;
using Wpf.Ui.Violeta.Controls;
using System.Windows.Input; 
using System.Linq; 
using System;
using System.Collections.Generic;




namespace BetterGenshinImpact.View.Pages;

public partial class OneDragonFlowPage
{
    public OneDragonFlowViewModel ViewModel { get; set; }

    public OneDragonFlowPage(OneDragonFlowViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;
        InitializeComponent();
    }

    private void ConfigRow_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext != null)
        {
            var listView = FindParent<ListView>(fe);
            if (listView != null)
                listView.SelectedItem = fe.DataContext;
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && !(parent is T))
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }

    private bool IsValidBindingCode(string bindingCode)
    {
        return !string.IsNullOrEmpty(bindingCode) && bindingCode.Length == 2 &&
               char.IsLetterOrDigit(bindingCode[0]) && char.IsLetterOrDigit(bindingCode[1]);
    }

    private void GenshinUid_TextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox != null)
        {
            var uid = textBox.Text;
            if (string.IsNullOrEmpty(uid) || uid.Length != 9 || !int.TryParse(uid, out _))
            {
                Toast.Warning("无效UID，长度 " + uid.Length + " 位，请输入 9 位纯数字的UID");
            }
            else
            {
                var bindingCode = PromptDialog.Prompt(
                    $"请输入 {ViewModel.SelectedConfig?.Name} / UID {uid} 的绑定码：\n手机登录的账户：切换账号列表显示的后2位 " +
                    $"\n邮箱登录的账户：切换账号列表显示的前2位 ", "UID辅助切换绑定码设置", new Size(400, 250), "如不设置绑定码，则使用轮切方式切换账号");
                if (IsValidBindingCode(bindingCode) && ViewModel.SelectedConfig != null)
                {
                    ViewModel.SelectedConfig.AccountBindingCode = bindingCode;
                    FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), null);
                    Toast.Success($"UID: {uid} 已经绑定 {ViewModel.SelectedConfig.AccountBindingCode}");
                }
                else
                {
                    var textBox2 = sender as TextBox;
                    var someOtherControl = FindParent<FrameworkElement>(textBox2);
                    if (string.IsNullOrEmpty(bindingCode) || bindingCode == "如不设置绑定码，则使用轮切方式切换账号")
                    {
                        ViewModel.SelectedConfig.AccountBindingCode = string.Empty;
                        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), someOtherControl);
                        Toast.Warning("绑定码为空，将使用轮切方式切换账号");
                        return;
                    }

                    Toast.Warning("绑定码长度为 2 位，且只能包含字母或数字");
                    GenshinUid_TextChanged(sender, e);
                }
            }
        }
    }

    private void AccountBinding_Checked(object sender, RoutedEventArgs e)
    {
        var checkBox = sender as CheckBox;
        if (checkBox != null)
        {
            if (checkBox.IsChecked == true && ViewModel.SelectedConfig != null)
            {
                if (!string.IsNullOrEmpty(ViewModel.SelectedConfig.AccountBindingCode))
                {
                    Toast.Success(
                        $"UID: {ViewModel.SelectedConfig.GenshinUid} 绑定码 {ViewModel.SelectedConfig.AccountBindingCode}");
                    checkBox.IsChecked = true;
                }
                else
                {
                    var bindingCode = PromptDialog.Prompt(
                        $"请输入 {ViewModel.SelectedConfig?.Name} 的绑定码：\n手机登录的账户：切换账号列表显示的后2位 " +
                        $"\n邮箱登录的账户：切换账号列表显示的前2位 ", "UID辅助切换绑定码设置", new Size(400, 250), "如不设置绑定码，则使用轮切方式切换账号");
                    if (string.IsNullOrEmpty(bindingCode) || bindingCode == "如不设置绑定码，则使用轮切方式切换账号")
                    {
                        ViewModel.SelectedConfig.AccountBindingCode = string.Empty;
                        Toast.Warning("绑定码为空，将使用轮切方式切换账号");
                        return;
                    }

                    if (IsValidBindingCode(bindingCode) && ViewModel.SelectedConfig != null)
                    {
                        ViewModel.SelectedConfig.AccountBindingCode = bindingCode;
                        Toast.Success(
                            $"UID: {ViewModel.SelectedConfig.GenshinUid} 绑定码 {ViewModel.SelectedConfig.AccountBindingCode}");
                        checkBox.IsChecked = true;
                    }
                    else
                    {
                        Toast.Warning("绑定码长度为 2 位，且只能包含字母或数字");
                        checkBox.IsChecked = false;
                    }
                }
            }
        }
    }

    private void DomainComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
        {
            if (comboBox.SelectedItem is string selectedItem && string.IsNullOrEmpty(selectedItem))
            {
                return;
            }
            
            var list = ViewModel?.SelectedConfig?.CustomDomainList ?? new List<string>();
            string selectedItemContent = comboBox.SelectedItem.ToString() ?? string.Empty;
            
            if (selectedItemContent == "新建自定义任务")
            {
                Toast.Warning("11111");
                var newTaskWindow = PromptDialog.Prompt("请输入自定义任务名称：", "创建自定义任务", new Size(400, 200), "");
                //等待用户点击确定按钮
                if (!string.IsNullOrEmpty(newTaskWindow))
                {
                    if (ViewModel.SelectedConfig?.CustomDomainList.Contains(newTaskWindow) == true)
                    {
                        comboBox.SelectedItem = string.Empty;
                        Toast.Warning("已经存在同名的自定义任务");
                        return;
                    }
                    ViewModel.SelectedConfig?.CustomDomainList.AddRange(new[] { newTaskWindow });
                    ViewModel.SaveConfig(); 
                    ViewModel.InitializeDomainNameList(); 
                    comboBox.Items.Refresh(); 
                    comboBox.SelectedItem = newTaskWindow; 
                    return;
                }
                comboBox.SelectedItem = string.Empty;
            }
            else if (selectedItemContent == "删除自定义任务")
            {
                Toast.Warning("2222");
                if (list.Count == 0)
                {
                    comboBox.SelectedItem = string.Empty;
                    Toast.Warning("没有可删除的自定义任务");
                    return;
                }

                var stackPanel = new StackPanel();
                var comboBox1 = new ComboBox { ItemsSource = list, SelectedItem = list?[0] };
                stackPanel.Children.Add(comboBox1);

                var result = PromptDialog.Prompt("请选择要删除的自定义任务名称？", "删除自定义任务", stackPanel, new Size(400, 200), null);

                if (comboBox1.SelectedItem != null)
                {
                    string taskToDelete = comboBox1.SelectedItem.ToString();
                    ViewModel.SelectedConfig?.CustomDomainList.Remove(taskToDelete);
                    ViewModel.SaveConfig(); // 保存配置文件
                    ViewModel.InitializeDomainNameList(); // 初始化域名列表
                    comboBox.Items.Refresh(); // 刷新ComboBox项
                    Toast.Success("已删除自定义任务：" + taskToDelete);
                    comboBox.SelectedItem = string.Empty;
                }
                else
                {
                    comboBox.SelectedItem = string.Empty;
                    Toast.Warning("未选择要删除的自定义任务");
                }
            }
            else
            {
                Toast.Success(list.Any(x => x == selectedItemContent && !string.IsNullOrEmpty(x))
                    ? $"自动秘境：已选自定义配置组任务：{comboBox.SelectedItem}"
                    : $"自动秘境：已选秘境名称：{comboBox.SelectedItem}");
            }
        }
        else
        {
            Toast.Warning("未知错误");
        }
    }
}
