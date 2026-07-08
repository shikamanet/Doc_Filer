using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using RobustFiler.ViewModels;
using RobustFiler.Services;

namespace RobustFiler.Controls;

public sealed partial class FilePaneControl : UserControl
{
    public FilePaneViewModel ViewModel => (FilePaneViewModel)DataContext;

    public FilePaneControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(Microsoft.UI.Xaml.FrameworkElement sender, Microsoft.UI.Xaml.DataContextChangedEventArgs args)
    {
        Bindings.Update();
        if (ViewModel != null)
        {
            ViewModel.NodeSelectedRequest -= ViewModel_NodeSelectedRequest;
            ViewModel.NodeSelectedRequest += ViewModel_NodeSelectedRequest;
        }
    }

    private async void ViewModel_NodeSelectedRequest(object? sender, (FileNodeViewModel Node, bool BringToTop) args)
    {
        if (FolderTree != null)
        {
            FolderTree.SelectedItem = args.Node;

            // UIがツリーの展開とノードの生成を完了するまで少し待機する
            await System.Threading.Tasks.Task.Delay(100);

            var container = FolderTree.ContainerFromItem(args.Node) as Microsoft.UI.Xaml.UIElement;
            if (container != null)
            {
                var options = new Microsoft.UI.Xaml.BringIntoViewOptions() { AnimationDesired = true };
                if (args.BringToTop)
                {
                    options.VerticalAlignmentRatio = 0.0;
                }
                container.StartBringIntoView(options);
            }
        }
    }

    private void TreeView_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is FileNodeViewModel node)
        {
            if (node.IsDirectory)
            {
                _ = ViewModel.NavigateCommand.ExecuteAsync(node.FullPath);
            }
        }
    }

    private void FolderTree_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (FolderTree.SelectedItem is FileNodeViewModel node)
        {
            _ = ViewModel.OpenNodeWithSourceAsync(node, NavigationSource.TreeView);
        }
    }

    private void BreadcrumbBar_ItemClicked(Microsoft.UI.Xaml.Controls.BreadcrumbBar sender, Microsoft.UI.Xaml.Controls.BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is BreadcrumbItem item)
        {
            _ = ViewModel.NavigateToPathAsync(item.Path, bringToTop: true, source: NavigationSource.Breadcrumb);
        }
    }

    private void AddressBarBorder_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        // パンくずリスト内のアイテム（フォルダ名）をクリックした場合はTextBoxに切り替えない
        if (e.OriginalSource is Microsoft.UI.Xaml.FrameworkElement fe)
        {
            if (fe.DataContext is RobustFiler.ViewModels.BreadcrumbItem || fe is Microsoft.UI.Xaml.Controls.BreadcrumbBar)
            {
                return;
            }
            
            // VisualTree を辿って BreadcrumbBarItem がクリックされたか確認する（安全のため）
            var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(fe);
            while (parent != null)
            {
                if (parent is Microsoft.UI.Xaml.Controls.BreadcrumbBarItem)
                {
                    return;
                }
                parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
            }
        }

        Breadcrumb.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        AddressTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        AddressTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        AddressTextBox.SelectAll();
        e.Handled = true;
    }

    private void AddressTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _ = ViewModel.NavigateToPathAsync(AddressTextBox.Text, bringToTop: true, source: NavigationSource.AddressBar);
            AddressTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            Breadcrumb.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            AddressTextBox.Text = ViewModel.CurrentPath;
            AddressTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            Breadcrumb.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            e.Handled = true;
        }
    }

    private void AddressTextBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AddressTextBox.Text = ViewModel.CurrentPath;
        AddressTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        Breadcrumb.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void DataGrid_Sorting(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridColumnEventArgs e)
    {
        var tag = e.Column.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        bool ascending = true;
        if (e.Column.SortDirection == null || e.Column.SortDirection == CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Descending)
        {
            ascending = true;
            e.Column.SortDirection = CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Ascending;
        }
        else
        {
            ascending = false;
            e.Column.SortDirection = CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Descending;
        }

        foreach (var column in ((CommunityToolkit.WinUI.UI.Controls.DataGrid)sender).Columns)
        {
            if (column != e.Column) column.SortDirection = null;
        }

        ViewModel.SortItems(tag, ascending);
    }

    private void DataGrid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is CommunityToolkit.WinUI.UI.Controls.DataGrid dataGrid && dataGrid.SelectedItem is FileNodeViewModel node)
        {
            _ = ViewModel.OpenNodeWithSourceAsync(node, NavigationSource.DataGrid);
        }
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (FileNodeViewModel removed in e.RemovedItems)
        {
            ViewModel.SelectedItems.Remove(removed);
        }
        foreach (FileNodeViewModel added in e.AddedItems)
        {
            ViewModel.SelectedItems.Add(added);
        }
    }

    private void DataGrid_LoadingRow(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowEventArgs e)
    {
        e.Row.CanDrag = true;
        e.Row.DragStarting += DataGridRow_DragStarting;
        e.Row.PointerEntered += DataGridRow_PointerEntered;
        e.Row.PointerExited += DataGridRow_PointerExited;
    }

    private void DataGrid_UnloadingRow(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowEventArgs e)
    {
        e.Row.DragStarting -= DataGridRow_DragStarting;
        e.Row.PointerEntered -= DataGridRow_PointerEntered;
        e.Row.PointerExited -= DataGridRow_PointerExited;
    }

    private void DataGridRow_DragStarting(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs args)
    {
        var paths = ViewModel.SelectedItems.Select(x => x.FullPath).ToList();
        if (paths.Count > 0)
        {
            args.Data.SetText(string.Join("\n", paths));
            args.AllowedOperations = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy | Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void DataGrid_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (e.Modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Control))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "コピー";
        }
        else
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.DragUIOverride.Caption = "移動";
        }
    }

    private async void DataGrid_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        await HandleDropAsync(e, ViewModel.CurrentPath);
    }

    private void TreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        var paths = args.Items.OfType<FileNodeViewModel>().Select(x => x.FullPath).ToList();
        if (paths.Count > 0)
        {
            args.Data.SetText("INTERNAL_TREE|" + string.Join("\n", paths));
            args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy | Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void FolderTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Item is FileNodeViewModel node)
        {
            node.IsExpanded = true;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RobustFiler.Messages.SessionChangedMessage());
        }
    }

    private void FolderTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (args.Item is FileNodeViewModel node)
        {
            node.IsExpanded = false;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RobustFiler.Messages.SessionChangedMessage());
        }
    }

    private async void FolderTree_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.None) return;

        var draggedNodes = args.Items.OfType<FileNodeViewModel>().ToList();
        if (draggedNodes.Count == 0) return;

        var newParentNode = args.NewParentItem as FileNodeViewModel;
        string targetPath = newParentNode?.FullPath ?? ViewModel.CurrentPath;

        if (System.IO.File.Exists(targetPath))
        {
            targetPath = System.IO.Path.GetDirectoryName(targetPath) ?? targetPath;
        }

        var service = ((App)Microsoft.UI.Xaml.Application.Current).Services?.GetService<Services.IFileService>();
        if (service != null && ViewModel != null)
        {
            var sourcePaths = draggedNodes.Select(x => x.FullPath).ToList();
            try
            {
                if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy)
                {
                    await service.CopyFilesAsync(sourcePaths, targetPath);
                }
                else if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
                {
                    await service.MoveFilesAsync(sourcePaths, targetPath);
                }
            }
            catch (Exception ex)
            {
                var dialogService = ((App)Microsoft.UI.Xaml.Application.Current).Services?.GetService<Services.IDialogService>();
                if (dialogService != null)
                {
                    await dialogService.ShowErrorAsync("ファイル操作エラー", ex);
                }
            }
            finally
            {
                var sourceDirs = sourcePaths.Select(System.IO.Path.GetDirectoryName).Where(x => x != null).Cast<string>().Distinct();
                var affected = sourceDirs.Concat(new[] { targetPath }).ToArray();
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RobustFiler.Messages.FileSystemChangedMessage(affected));
            }
        }
    }

    private void TreeView_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        // For dragging from OUTSIDE into the TreeView
        if (e.Modifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Control))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "コピー";
        }
        else
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.DragUIOverride.Caption = "移動";
        }
    }

    private FileNodeViewModel? GetNodeFromElement(Microsoft.UI.Xaml.DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Microsoft.UI.Xaml.FrameworkElement fe && fe.DataContext is FileNodeViewModel node)
            {
                return node;
            }
            element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private async void TreeView_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        // For external drops (e.g. from Explorer to TreeView)
        // Internal drops will be handled by TreeView automatically and trigger DragItemsCompleted
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            try
            {
                var text = await e.DataView.GetTextAsync();
                if (text.StartsWith("INTERNAL_TREE|"))
                {
                    // Ignore internal drag drops since DragItemsCompleted will handle them
                    return;
                }
            }
            catch { }
        }

        string targetPath = ViewModel.CurrentPath;
        var node = GetNodeFromElement(e.OriginalSource as Microsoft.UI.Xaml.DependencyObject);
        if (node != null)
        {
            targetPath = node.FullPath;
        }

        if (System.IO.File.Exists(targetPath))
        {
            targetPath = System.IO.Path.GetDirectoryName(targetPath) ?? targetPath;
        }

        await HandleDropAsync(e, targetPath);
    }

    private async Task HandleDropAsync(Microsoft.UI.Xaml.DragEventArgs e, string targetPath)
    {
        var paths = new List<string>();
        try
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    paths.Add(item.Path);
                }
            }
            else if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    if (text.StartsWith("COPY|") || text.StartsWith("MOVE|"))
                    {
                        text = text.Substring(5);
                    }
                    paths.AddRange(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                }
            }

            if (paths.Count > 0)
            {
                var service = ((App)Microsoft.UI.Xaml.Application.Current).Services?.GetService<IFileService>();
                if (service != null && ViewModel != null)
                {
                    if (e.AcceptedOperation == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy)
                    {
                        await service.CopyFilesAsync(paths, targetPath);
                    }
                    else if (e.AcceptedOperation == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
                    {
                        await service.MoveFilesAsync(paths, targetPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var dialogService = ((App)Microsoft.UI.Xaml.Application.Current).Services?.GetService<Services.IDialogService>();
            if (dialogService != null)
            {
                await dialogService.ShowErrorAsync("ファイル操作エラー", ex);
            }
        }
        finally
        {
            if (paths.Count > 0)
            {
                var sourceDirs = paths.Select(System.IO.Path.GetDirectoryName).Where(x => x != null).Cast<string>().Distinct();
                var affected = sourceDirs.Concat(new[] { targetPath }).ToArray();
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RobustFiler.Messages.FileSystemChangedMessage(affected));
            }
        }
    }

    private void ColumnMenu_Opening(object sender, object e)
    {
        if (sender is MenuFlyout flyout)
        {
            foreach (var item in flyout.Items)
            {
                if (item is ToggleMenuFlyoutItem toggleItem && toggleItem.Tag is string tag)
                {
                    var column = FileListGrid.Columns.FirstOrDefault(c => c.Tag?.ToString() == tag);
                    if (column != null)
                    {
                        toggleItem.IsChecked = column.Visibility == Microsoft.UI.Xaml.Visibility.Visible;
                    }
                }
            }
        }
    }

    private void ColumnMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem toggleItem && toggleItem.Tag is string tag)
        {
            var column = FileListGrid.Columns.FirstOrDefault(c => c.Tag?.ToString() == tag);
            if (column != null)
            {
                column.Visibility = toggleItem.IsChecked ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }
    }

    private void TreeItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.StackPanel panel)
        {
            panel.Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["SystemControlHighlightListLowBrush"];
        }
    }

    private void TreeItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.StackPanel panel)
        {
            panel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void DataGridRow_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is CommunityToolkit.WinUI.UI.Controls.DataGridRow row)
        {
            var grid = FileListGrid;
            if (grid != null && !grid.SelectedItems.Contains(row.DataContext))
            {
                row.Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["SystemControlHighlightListLowBrush"];
            }
        }
    }

    private void DataGridRow_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is CommunityToolkit.WinUI.UI.Controls.DataGridRow row)
        {
            row.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void EnsureNodeSelected(object sender)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement element && element.DataContext is FileNodeViewModel node)
        {
            if (!ViewModel.SelectedItems.Contains(node))
            {
                FileListGrid.SelectedItem = node;
            }
        }
    }

    private void MenuFlyoutItem_Copy_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EnsureNodeSelected(sender);
        ViewModel.CopyFilesCommand.Execute(null);
    }

    private void MenuFlyoutItem_Cut_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EnsureNodeSelected(sender);
        ViewModel.CutFilesCommand.Execute(null);
    }

    private void MenuFlyoutItem_AddFavorite_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EnsureNodeSelected(sender);
        if (sender is MenuFlyoutItem item && item.DataContext is FileNodeViewModel node)
        {
            ViewModel.AddToFavoritesCommand.Execute(node);
        }
        else
        {
            ViewModel.AddToFavoritesCommand.Execute(null);
        }
    }

    private void MenuFlyoutItem_Rename_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EnsureNodeSelected(sender);
        ViewModel.RenameCommand.Execute(null);
    }

    private void MenuFlyoutItem_Delete_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EnsureNodeSelected(sender);
        ViewModel.DeleteCommand.Execute(null);
    }
}
