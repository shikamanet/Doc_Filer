using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace RobustFiler.Services;

public class LocalDialogService : IDialogService
{
    private Microsoft.UI.Xaml.XamlRoot? GetXamlRoot()
    {
        return App.Current.MainWindow?.Content?.XamlRoot;
    }

    public async Task<bool> ShowConfirmationAsync(string title, string content)
    {
        var root = GetXamlRoot();
        if (root == null) return false;

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            XamlRoot = root
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<string?> ShowInputDialogAsync(string title, string defaultText = "")
    {
        var root = GetXamlRoot();
        if (root == null) return null;

        var textBox = new TextBox
        {
            Text = defaultText,
            Width = 300,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            XamlRoot = root
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    public async Task ShowErrorAsync(string title, Exception ex)
    {
        var root = GetXamlRoot();
        if (root == null) return;

        var dialog = new ContentDialog
        {
            Title = title,
            Content = ex.Message,
            CloseButtonText = "OK",
            XamlRoot = root
        };

        await dialog.ShowAsync();
    }

    public async Task<string?> ShowCreateFolderDialogAsync()
    {
        return await ShowInputDialogAsync("フォルダの作成", "新しいフォルダ");
    }

    public async Task<RobustFiler.Models.FavoriteItem?> ShowFavoriteSettingsDialogAsync(RobustFiler.Models.FavoriteItem currentItem)
    {
        var root = GetXamlRoot();
        if (root == null) return null;

        var panel = new StackPanel { Spacing = 12, Width = 400 };

        var nameBox = new TextBox { Header = "名前", Text = currentItem.Name, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        var pathBox = new TextBox { Header = "パス", Text = currentItem.Path, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        var argsBox = new TextBox { Header = "パラメータ（引数）", Text = currentItem.Arguments, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };

        panel.Children.Add(nameBox);
        
        if (!currentItem.IsFolder)
        {
            panel.Children.Add(pathBox);
            panel.Children.Add(argsBox);
        }

        var dialog = new ContentDialog
        {
            Title = currentItem.IsFolder ? "フォルダの設定" : "お気に入りの設定",
            Content = panel,
            PrimaryButtonText = "保存",
            CloseButtonText = "キャンセル",
            XamlRoot = root
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return new RobustFiler.Models.FavoriteItem
            {
                Name = nameBox.Text,
                Path = currentItem.IsFolder ? string.Empty : pathBox.Text,
                Arguments = currentItem.IsFolder ? string.Empty : argsBox.Text,
                IsFolder = currentItem.IsFolder,
                Icon = currentItem.Icon
            };
        }
        return null;
    }
}
