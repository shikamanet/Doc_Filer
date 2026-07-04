using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using RobustFiler.Models;
using RobustFiler.Services;

namespace RobustFiler.ViewModels;

public partial class FileNodeViewModel : ObservableObject
{
    private readonly IFileService _fileService;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private string _size = string.Empty;

    [ObservableProperty]
    private string _dateModified = string.Empty;

    public long RawSize { get; }
    public DateTime RawDateModified { get; }

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource? _fileIcon;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private ImageSource? _icon;

    [ObservableProperty]
    private bool _isLoaded;

    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE8A5";

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                if (value && !IsLoaded && IsDirectory)
                {
                    _ = LoadChildrenAsync();
                }
            }
        }
    }

    public ObservableCollection<FileNodeViewModel> Children { get; } = new();

    public FileNodeViewModel(IFileService fileService)
    {
        _fileService = fileService;
    }

    public FileNodeViewModel(FileItem item, IFileService fileService)
    {
        _fileService = fileService;
        Name = item.Name;
        FullPath = item.FullPath;
        IsDirectory = item.IsDirectory;
        RawSize = IsDirectory ? -1 : item.Size;
        RawDateModified = item.DateModified;
        Size = item.Size.ToString();
        DateModified = item.DateModified.ToString("g");

        _ = LoadIconAsync();

        if (IsDirectory)
        {
            // Dummy node for expander icon
            Children.Add(new FileNodeViewModel(_fileService) { Name = "Loading..." });
        }
    }

    private async Task LoadIconAsync()
    {
        FileIcon = await RobustFiler.Helpers.IconHelper.GetIconAsync(FullPath, IsDirectory);
    }

    public async Task LoadChildrenAsync()
    {
        if (IsLoaded || !IsDirectory) return;

        var items = await _fileService.GetFilesAsync(FullPath);

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue != null)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                Children.Clear();
                foreach (var item in items)
                {
                    Children.Add(new FileNodeViewModel(item, _fileService));
                }
                IsLoaded = true;
            });
        }
    }
}
