using BetterGenshinImpact.ViewModel.Pages;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace BetterGenshinImpact.View.Pages;

public partial class TaskSettingsPage : Page
{
    private TaskSettingsPageViewModel ViewModel { get; }

    public TaskSettingsPage(TaskSettingsPageViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;
        InitializeComponent();
    }
    
    private void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && ViewModel?.Config != null)
        {
            var cfg = ViewModel.Config.AutoFightConfig.FinishDetectConfig;
            if (tb.IsChecked == true)
            {
                cfg.PaimonEndModel = cfg.EndModel;
            }
            else
            {
                cfg.PaimonEndModel = false;
            }
        }
    }
}
