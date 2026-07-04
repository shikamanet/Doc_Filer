using Microsoft.UI.Xaml.Controls;
using RobustFiler.ViewModels;

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

    private void AddressBar_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            if (ViewModel.NavigateCommand.CanExecute(textBox.Text))
            {
                ViewModel.NavigateCommand.Execute(textBox.Text);
            }
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
}
