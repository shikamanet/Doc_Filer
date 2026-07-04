using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using RobustFiler.ViewModels;
using RobustFiler.Services;

namespace RobustFiler.Controls;

public sealed partial class FilePaneControl : UserControl
{
    public FilePaneViewModel ViewModel => (FilePaneViewModel)DataContext;

    public FilePaneControl()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is FileNodeViewModel node && node.IsDirectory)
        {
            _ = ViewModel.NavigateCommand.ExecuteAsync(node.FullPath);
        }
    }

    private void BreadcrumbBar_ItemClicked(Microsoft.UI.Xaml.Controls.BreadcrumbBar sender, Microsoft.UI.Xaml.Controls.BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is BreadcrumbItem item)
        {
            _ = ViewModel.NavigateCommand.ExecuteAsync(item.Path);
        }
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
            _ = ViewModel.OpenNodeCommand.ExecuteAsync(node);
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
    }

    private void DataGrid_UnloadingRow(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowEventArgs e)
    {
        e.Row.DragStarting -= DataGridRow_DragStarting;
    }

    private void DataGridRow_DragStarting(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs args)
    {
        var paths = ViewModel.SelectedItems.Select(x => x.FullPath).ToList();
        if (paths.Count > 0)
        {
            args.Data.SetText("COPY|" + string.Join("\n", paths));
        }
    }

    private void DataGrid_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy | Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private async void DataGrid_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            try
            {
                var text = await e.DataView.GetTextAsync();
                if (string.IsNullOrEmpty(text)) return;
                var parts = text.Split('|', 2);
                if (parts.Length != 2) return;
                var op = parts[0];
                var paths = parts[1].Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var service = ((App)Microsoft.UI.Xaml.Application.Current).Services?.GetService<IFileService>();
                if (service != null && ViewModel != null)
                {
                    if (op == "COPY")
                    {
                        await service.CopyFilesAsync(paths, ViewModel.CurrentPath);
                    }
                    else if (op == "MOVE")
                    {
                        await service.MoveFilesAsync(paths, ViewModel.CurrentPath);
                    }
                }
            }
            catch
            {
                // Ignore drop errors
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
}
