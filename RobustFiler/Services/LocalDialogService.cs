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

    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var root = GetXamlRoot();
        if (root == null) return false;

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
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
}
