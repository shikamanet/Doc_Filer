using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using RobustFiler.ViewModels;

namespace RobustFiler;

public sealed partial class MainPage : Page
{
    public MainWindowViewModel ViewModel { get; }

    private GridLength _favoritesRowHeight = new GridLength(1, GridUnitType.Star);
    private GridLength _groupsRowHeight = new GridLength(1, GridUnitType.Star);

    public MainPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<MainWindowViewModel>();
        InitializeComponent();
    }

    private void PrimaryTabView_AddTabButtonClick(TabView sender, object args)
    {
        ViewModel.AddTabToPane(isSecondary: false);
    }

    private void SecondaryTabView_AddTabButtonClick(TabView sender, object args)
    {
        ViewModel.AddTabToPane(isSecondary: true);
    }

    private void PrimaryTabView_GotFocus(object sender, RoutedEventArgs e)
    {
        ViewModel.IsSecondaryActive = false;
    }

    private void SecondaryTabView_GotFocus(object sender, RoutedEventArgs e)
    {
        ViewModel.IsSecondaryActive = true;
    }

    private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is TabItemViewModel tab)
        {
            ViewModel.CloseTabCommand.Execute(tab);
        }
    }

    private void TabViewItemGrid_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TabItemViewModel tab)
        {
            tab.IsHovered = true;
        }
    }

    private void TabViewItemGrid_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TabItemViewModel tab)
        {
            tab.IsHovered = false;
        }
    }

    private void CustomCloseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TabItemViewModel tab)
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

    private void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is TabView tabView && tabView.DataContext is TabGroupViewModel group)
        {
            if (group == ViewModel.SelectedTabGroup)
            {
                ViewModel.SelectedTab = group.SelectedTab;
            }
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
        if (sender is MenuFlyoutItem item && item.DataContext is TabItemViewModel tab)
        {
            ViewModel.CloseAllTabsCommand.Execute(tab);
        }
    }

    private void TabView_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        args.Data.Properties.Add("TabItem", args.Tab);
        args.Data.Properties.Add("SourceTabView", sender);
    }

    private void TabView_TabStripDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey("TabItem"))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void TabView_TabStripDrop(object sender, DragEventArgs e)
    {
        if (sender is TabView targetTabView && e.DataView.Properties.TryGetValue("TabItem", out var tabObj) && tabObj is TabViewItem tabItem && tabItem.DataContext is TabItemViewModel tabVM)
        {
            if (e.DataView.Properties.TryGetValue("SourceTabView", out var sourceObj) && sourceObj is TabView sourceTabView)
            {
                if (sourceTabView != targetTabView)
                {
                    // Moving between primary and secondary (or vice versa)
                    var isSourceSecondary = ViewModel.SelectedTabGroup?.SecondaryTabs.Contains(tabVM) == true;
                    var isTargetSecondary = targetTabView.TabItemsSource == ViewModel.SelectedTabGroup?.SecondaryTabs;

                    if (isSourceSecondary != isTargetSecondary && ViewModel.SelectedTabGroup != null)
                    {
                        var sourceList = isSourceSecondary ? ViewModel.SelectedTabGroup.SecondaryTabs : ViewModel.SelectedTabGroup.Tabs;
                        var targetList = isTargetSecondary ? ViewModel.SelectedTabGroup.SecondaryTabs : ViewModel.SelectedTabGroup.Tabs;

                        sourceList.Remove(tabVM);
                        
                        // Handle insert index
                        int insertIndex = targetList.Count;
                        // For a real implementation, you'd calculate the drop index based on e.GetPosition(targetTabView).
                        // Since TabView doesn't easily expose the drop index, we just append.
                        
                        targetList.Add(tabVM);

                        if (isTargetSecondary)
                            ViewModel.SelectedTabGroup.SecondarySelectedTab = tabVM;
                        else
                            ViewModel.SelectedTabGroup.SelectedTab = tabVM;

                        // Ensure we don't leave empty panes
                        if (sourceList.Count == 0)
                        {
                            ViewModel.AddTabToPane(isSourceSecondary);
                        }
                    }
                }
            }
        }
    }

    private void MenuFlyoutItem_RenameGroup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is TabGroupViewModel group)
        {
            ViewModel.RenameTabGroupCommand.Execute(group);
        }
    }

    private void MenuFlyoutItem_RemoveGroup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is TabGroupViewModel group)
        {
            ViewModel.RemoveTabGroupCommand.Execute(group);
        }
    }

    private void MenuFlyoutItem_ChangeColor_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is TabGroupViewModel group && item.Tag is string colorHex)
        {
            ViewModel.SetTabGroupColor(group, colorHex);
        }
    }

    private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is RobustFiler.Models.FavoriteItem item)
        {
            if (!item.IsFolder)
            {
                ViewModel.SelectedFavorite = item;
            }
        }
    }

    private void TreeView_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        _ = ViewModel.SaveFavoritesCommand.ExecuteAsync(null);
    }

    private void MenuFlyoutItem_EditFavorite_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is RobustFiler.Models.FavoriteItem fav)
        {
            ViewModel.EditFavoriteCommand.Execute(fav);
        }
    }

    private void MenuFlyoutItem_RemoveFavorite_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is RobustFiler.Models.FavoriteItem fav)
        {
            ViewModel.RemoveFavoriteCommand.Execute(fav);
        }
    }

    private void FavoritesHeader_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (FavoritesTreeView.Visibility == Visibility.Visible)
        {
            _favoritesRowHeight = FavoritesRow.Height;
            FavoritesTreeView.Visibility = Visibility.Collapsed;
            FavoritesChevron.Glyph = "\uE76C"; // ChevronRight
            FavoritesRow.Height = GridLength.Auto;
        }
        else
        {
            FavoritesTreeView.Visibility = Visibility.Visible;
            FavoritesChevron.Glyph = "\uE70D"; // ChevronDown
            FavoritesRow.Height = _favoritesRowHeight;
        }
    }

    private void GroupsHeader_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (GroupsListView.Visibility == Visibility.Visible)
        {
            _groupsRowHeight = GroupsRow.Height;
            GroupsListView.Visibility = Visibility.Collapsed;
            GroupsChevron.Glyph = "\uE76C"; // ChevronRight
            GroupsRow.Height = GridLength.Auto;
        }
        else
        {
            GroupsListView.Visibility = Visibility.Visible;
            GroupsChevron.Glyph = "\uE70D"; // ChevronDown
            GroupsRow.Height = _groupsRowHeight;
        }
    }
}
