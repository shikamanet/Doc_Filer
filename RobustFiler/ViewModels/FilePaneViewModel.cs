using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using RobustFiler.Models;
using RobustFiler.Services;

namespace RobustFiler.ViewModels;

public partial class FilePaneViewModel : ObservableObject, IDisposable
{
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();
    private bool _isNavigating;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    public ObservableCollection<FileNodeViewModel> RootDrives { get; } = new();
    public ObservableCollection<FileNodeViewModel> CurrentFolderItems { get; } = new();

    public FilePaneViewModel(IFileService fileService, IDialogService dialogService)
    {
        _fileService = fileService;
        _dialogService = dialogService;
        _fileService.DirectoryChanged += OnDirectoryChanged;
        InitializeDrives();
    }

    private void OnDirectoryChanged(object? sender, string path)
    {
        if (CurrentPath == path)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? App.Current.MainWindow?.DispatcherQueue;
            dispatcherQueue?.TryEnqueue(async () =>
            {
                await LoadFolderContentCoreAsync(path);
            });
        }
    }

    private void InitializeDrives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                var item = new FileItem
                {
                    Name = drive.Name,
                    FullPath = drive.Name,
                    IsDirectory = true,
                    Size = drive.TotalSize,
                    DateModified = DateTime.MinValue
                };
                RootDrives.Add(new FileNodeViewModel(item, _fileService));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        if (CanGoBack())
        {
            _forwardHistory.Push(CurrentPath);
            var prev = _backHistory.Pop();
            _ = NavigateInternalAsync(prev, isHistoryNavigation: true);
        }
    }

    private bool CanGoBack() => _backHistory.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward()
    {
        if (CanGoForward())
        {
            _backHistory.Push(CurrentPath);
            var next = _forwardHistory.Pop();
            _ = NavigateInternalAsync(next, isHistoryNavigation: true);
        }
    }

    private bool CanGoForward() => _forwardHistory.Count > 0;

    [RelayCommand]
    public async Task NavigateAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || CurrentPath == path) return;

        if (!string.IsNullOrEmpty(CurrentPath))
        {
            _backHistory.Push(CurrentPath);
            _forwardHistory.Clear();
        }

        await NavigateInternalAsync(path, isHistoryNavigation: false);
    }

    private async Task NavigateInternalAsync(string path, bool isHistoryNavigation)
    {
        if (_isNavigating) return;
        _isNavigating = true;

        try
        {
            if (Directory.Exists(path))
            {
                CurrentPath = path;
                await LoadFolderContentCoreAsync(path);
            }
            else
            {
                await _dialogService.ShowErrorAsync("エラー", new Exception("指定されたパスが見つかりません。"));
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("アクセスエラー", ex);
        }
        finally
        {
            _isNavigating = false;
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task LoadFolderContentCoreAsync(string path)
    {
        _fileService.StartWatch(path);
        var items = await _fileService.GetFilesAsync(path);

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? App.Current.MainWindow?.DispatcherQueue;
        if (dispatcherQueue != null)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                CurrentFolderItems.Clear();
                foreach (var item in items)
                {
                    CurrentFolderItems.Add(new FileNodeViewModel(item, _fileService));
                }
            });
        }
    }

    [RelayCommand]
    public async Task CreateFolderAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        var folderName = await _dialogService.ShowInputDialogAsync("新しいフォルダー", "新しいフォルダー");
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            bool success = await _fileService.CreateFolderAsync(CurrentPath, folderName);
            if (!success)
            {
                await _dialogService.ShowErrorAsync("エラー", new Exception("フォルダーを作成できませんでした。"));
            }
        }
    }

    [RelayCommand]
    public async Task RenameAsync(FileNodeViewModel? node)
    {
        if (node == null) return;
        var newName = await _dialogService.ShowInputDialogAsync("名前の変更", node.Name);
        if (!string.IsNullOrWhiteSpace(newName) && newName != node.Name)
        {
            if (!await _fileService.RenameAsync(node.FullPath, newName))
            {
                await _dialogService.ShowErrorAsync("エラー", new Exception("名前を変更できませんでした。"));
            }
        }
    }

    [RelayCommand]
    public async Task DeleteAsync(FileNodeViewModel? node)
    {
        if (node == null) return;
        if (await _dialogService.ShowConfirmationAsync("削除の確認", $"'{node.Name}' をごみ箱に移動しますか？"))
        {
            if (await _fileService.DeleteToRecycleBinAsync(node.FullPath))
            {
                CurrentFolderItems.Remove(node);
            }
            else
            {
                await _dialogService.ShowErrorAsync("エラー", new Exception("削除できませんでした。権限またはロックを確認してください。"));
            }
        }
    }

    public void SortItems(string columnTag, bool ascending)
    {
        var sorted = CurrentFolderItems.ToList();
        switch (columnTag)
        {
            case "Name":
                sorted = ascending ? sorted.OrderBy(x => x.IsDirectory ? 0 : 1).ThenBy(x => x.Name).ToList() : sorted.OrderBy(x => x.IsDirectory ? 0 : 1).ThenByDescending(x => x.Name).ToList();
                break;
            case "DateModified":
                sorted = ascending ? sorted.OrderBy(x => x.RawDateModified).ToList() : sorted.OrderByDescending(x => x.RawDateModified).ToList();
                break;
            case "Size":
                sorted = ascending ? sorted.OrderBy(x => x.RawSize).ToList() : sorted.OrderByDescending(x => x.RawSize).ToList();
                break;
        }

        CurrentFolderItems.Clear();
        foreach (var item in sorted)
        {
            CurrentFolderItems.Add(item);
        }
    }

    public void Dispose()
    {
        _fileService.DirectoryChanged -= OnDirectoryChanged;
        _fileService.StopWatch();
    }
}
