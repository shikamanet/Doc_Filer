using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using RobustFiler.Models;
using RobustFiler.Services;

namespace RobustFiler.ViewModels;

public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public partial class FilePaneViewModel : ObservableObject, IDisposable
{
    public event EventHandler<(FileNodeViewModel Node, bool BringToTop)>? NodeSelectedRequest;
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();
    private bool _isNavigating;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    public ObservableCollection<FileNodeViewModel> RootDrives { get; } = new();
    
    [ObservableProperty]
    private ObservableCollection<FileNodeViewModel> _currentFolderItems = new();
    
    public ObservableCollection<FileNodeViewModel> SelectedItems { get; } = new();
    public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = new();

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
        await NavigateInternalAsync(path, false, false);
    }

    public async Task NavigateToPathAsync(string path, bool bringToTop)
    {
        if (string.IsNullOrWhiteSpace(path) || CurrentPath == path) return;
        await NavigateInternalAsync(path, false, bringToTop);
    }

    public Func<string, bool>? NavigationInterceptor { get; set; }

    private async Task NavigateInternalAsync(string path, bool isHistoryNavigation, bool bringToTop = false)
    {
        if (_isNavigating) return;
        if (NavigationInterceptor?.Invoke(path) == true) return;
        _isNavigating = true;

        try
        {
            if (Directory.Exists(path))
            {
                if (!isHistoryNavigation && !string.IsNullOrEmpty(CurrentPath))
                {
                    _backHistory.Push(CurrentPath);
                    _forwardHistory.Clear();
                }

                CurrentPath = path;
                UpdateBreadcrumbs(path);
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
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RobustFiler.Messages.SessionChangedMessage());
            
            if (!isHistoryNavigation && Directory.Exists(path))
            {
                _ = ExpandToPathAsync(path, bringToTop);
            }
        }
    }

    private async Task ExpandToPathAsync(string targetPath, bool bringToTop)
    {
        var node = await FindAndExpandNodeAsync(RootDrives, targetPath);
        if (node != null)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? App.Current.MainWindow?.DispatcherQueue;
            dispatcherQueue?.TryEnqueue(() =>
            {
                NodeSelectedRequest?.Invoke(this, (node, bringToTop));
            });
        }
    }

    private async Task<FileNodeViewModel?> FindAndExpandNodeAsync(IEnumerable<FileNodeViewModel> nodes, string targetPath)
    {
        string targetNormalized = targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            string nodeNormalized = node.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (targetNormalized.StartsWith(nodeNormalized, StringComparison.OrdinalIgnoreCase) && node.IsDirectory)
            {
                if (!node.IsLoaded)
                {
                    await node.LoadChildrenAsync();
                }
                
                node.IsExpanded = true;
                
                return await FindAndExpandNodeAsync(node.Children, targetPath);
            }
        }
        return null;
    }

    private void UpdateBreadcrumbs(string path)
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? App.Current.MainWindow?.DispatcherQueue;
        dispatcherQueue?.TryEnqueue(() =>
        {
            BreadcrumbItems.Clear();
            if (string.IsNullOrEmpty(path)) return;

            var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            string current = "";
            for (int i = 0; i < parts.Length; i++)
            {
                current += parts[i] + Path.DirectorySeparatorChar;
                BreadcrumbItems.Add(new BreadcrumbItem { Name = parts[i], Path = current });
            }
        });
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
                SelectedItems.Clear();
                var newItems = new ObservableCollection<FileNodeViewModel>();
                foreach (var item in items)
                {
                    newItems.Add(new FileNodeViewModel(item, _fileService));
                }
                CurrentFolderItems = newItems;
            });
        }
    }

    [RelayCommand]
    public async Task OpenNodeAsync(FileNodeViewModel? node)
    {
        if (node == null) return;
        if (node.IsDirectory)
        {
            await NavigateAsync(node.FullPath);
        }
        else if (node.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var target = _fileService.ResolveShortcut(node.FullPath);
            if (!string.IsNullOrEmpty(target))
            {
                if (Directory.Exists(target))
                {
                    await NavigateAsync(target);
                }
                else
                {
                    await _fileService.OpenFileAsync(target);
                }
            }
            else
            {
                await _fileService.OpenFileAsync(node.FullPath);
            }
        }
        else
        {
            await _fileService.OpenFileAsync(node.FullPath);
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
    public async Task RenameAsync()
    {
        var node = SelectedItems.FirstOrDefault();
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
    public async Task DeleteAsync()
    {
        if (SelectedItems.Count == 0) return;
        if (await _dialogService.ShowConfirmationAsync("削除の確認", $"{SelectedItems.Count} 個の項目をごみ箱に移動しますか？"))
        {
            var nodesToDelete = SelectedItems.ToList();
            foreach (var node in nodesToDelete)
            {
                if (await _fileService.DeleteToRecycleBinAsync(node.FullPath))
                {
                    CurrentFolderItems.Remove(node);
                }
                else
                {
                    await _dialogService.ShowErrorAsync("エラー", new Exception($"'{node.Name}' を削除できませんでした。"));
                }
            }
            SelectedItems.Clear();
        }
    }

    [RelayCommand]
    public void CopyFiles()
    {
        if (SelectedItems.Count == 0) return;
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        string paths = string.Join("\n", SelectedItems.Select(x => x.FullPath));
        dataPackage.SetText("COPY|" + paths);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    public void CutFiles()
    {
        if (SelectedItems.Count == 0) return;
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        string paths = string.Join("\n", SelectedItems.Select(x => x.FullPath));
        dataPackage.SetText("MOVE|" + paths);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    public async Task PasteFilesAsync()
    {
        var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            try
            {
                var text = await dataPackageView.GetTextAsync();
                if (string.IsNullOrEmpty(text)) return;
                var parts = text.Split('|', 2);
                if (parts.Length != 2) return;
                var op = parts[0];
                var paths = parts[1].Split('\n', StringSplitOptions.RemoveEmptyEntries);

                if (op == "COPY")
                {
                    await _fileService.CopyFilesAsync(paths, CurrentPath);
                }
                else if (op == "MOVE")
                {
                    await _fileService.MoveFilesAsync(paths, CurrentPath);
                    Windows.ApplicationModel.DataTransfer.Clipboard.Clear();
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("エラー", ex);
            }
        }
    }

    [RelayCommand]
    public void AddToFavorites(FileNodeViewModel? node)
    {
        var targetNode = node ?? SelectedItems.FirstOrDefault();
        if (targetNode == null) return;

        var favorite = new FavoriteItem
        {
            Name = targetNode.Name,
            Path = targetNode.FullPath
        };
        _ = favorite.LoadIconAsync();
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RobustFiler.Messages.AddFavoriteMessage(favorite));
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
                sorted = ascending ? sorted.OrderBy(x => x.IsDirectory ? 0 : 1).ThenBy(x => x.RawDateModified).ToList() : sorted.OrderBy(x => x.IsDirectory ? 0 : 1).ThenByDescending(x => x.RawDateModified).ToList();
                break;
            case "Size":
                sorted = ascending ? sorted.OrderBy(x => x.IsDirectory ? 0 : 1).ThenBy(x => x.RawSize).ToList() : sorted.OrderBy(x => x.IsDirectory ? 0 : 1).ThenByDescending(x => x.RawSize).ToList();
                break;
        }

        var newItems = new ObservableCollection<FileNodeViewModel>();
        foreach (var item in sorted)
        {
            newItems.Add(item);
        }
        CurrentFolderItems = newItems;
    }

    public void Dispose()
    {
        _fileService.DirectoryChanged -= OnDirectoryChanged;
        _fileService.StopWatch();
    }
}
