using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using RobustFiler.ViewModels;

namespace RobustFiler;

public sealed partial class MainPage : Page
{
    public MainWindowViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<MainWindowViewModel>();
        InitializeComponent();
    }

    private void TabView_AddTabButtonClick(TabView sender, object args)
    {
        ViewModel.AddTabCommand.Execute(null);
    }

    private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is TabItemViewModel tab)
        {
            ViewModel.CloseTabCommand.Execute(tab);
        }
    }

    private void TabViewItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TabItemViewModel tab)
        {
            tab.IsLocked = !tab.IsLocked;
            e.Handled = true;
        }
    }

    private void CloseTabMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is TabItemViewModel tab)
        {
            ViewModel.CloseTabCommand.Execute(tab);
        }
    }

    private void CloseOtherTabsMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is TabItemViewModel tab)
        {
            ViewModel.CloseOtherTabsCommand.Execute(tab);
        }
    }

    private void CloseTabsToRightMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is TabItemViewModel tab)
        {
            ViewModel.CloseTabsToRightCommand.Execute(tab);
        }
    }

    private void CloseAllTabsMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.CloseAllTabsCommand.Execute(null);
    }
}
