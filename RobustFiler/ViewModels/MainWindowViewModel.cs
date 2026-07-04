using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RobustFiler.Messages;
using RobustFiler.Models;
using RobustFiler.Services;

namespace RobustFiler.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<TabItemViewModel> Tabs { get; } = new();
    public ObservableCollection<FavoriteItem> Favorites { get; } = new();

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    [ObservableProperty]
    private FavoriteItem? _selectedFavorite;

    public MainWindowViewModel(IFileService fileService, IDialogService dialogService)
    {
        _fileService = fileService;
        _dialogService = dialogService;
        AddTab();

        WeakReferenceMessenger.Default.Register<AddFavoriteMessage>(this, (r, m) =>
        {
            AddFavorite(m.Value);
        });

        _ = LoadFavoritesAsync();
    }

    private async Task LoadFavoritesAsync()
    {
        var loaded = await _fileService.LoadFavoritesAsync();
        foreach (var item in loaded)
        {
            await item.LoadIconAsync();
            Favorites.Add(item);
        }
    }

    private void AddFavorite(FavoriteItem item)
    {
        if (Favorites.Any(f => f.Path == item.Path)) return;

        Favorites.Add(item);
        _ = SaveFavoritesAsync();
    }

    [RelayCommand]
    public void RemoveFavorite(FavoriteItem? item)
    {
        if (item != null)
        {
            Favorites.Remove(item);
            _ = SaveFavoritesAsync();
        }
    }

    private async Task SaveFavoritesAsync()
    {
        await _fileService.SaveFavoritesAsync(Favorites);
    }

    partial void OnSelectedFavoriteChanged(FavoriteItem? value)
    {
        if (value != null && SelectedTab?.PrimaryPane != null)
        {
            // Navigate the primary pane to the favorite path
            _ = SelectedTab.PrimaryPane.NavigateAsync(value.Path);
            
            // Reset selection so the user can click it again
            SelectedFavorite = null; 
        }
    }

    [RelayCommand]
    public void AddTab()
    {
        var primaryPane = new FilePaneViewModel(_fileService, _dialogService);
        var tab = new TabItemViewModel(primaryPane);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    [RelayCommand]
    public void CloseTab(TabItemViewModel? tab)
    {
        if (tab == null) return;
        Tabs.Remove(tab);
        tab.Dispose();
        
        if (Tabs.Count == 0)
        {
            AddTab();
        }
    }

    [RelayCommand]
    public void ToggleDualPane()
    {
        if (SelectedTab != null)
        {
            var newPane = new FilePaneViewModel(_fileService, _dialogService);
            SelectedTab.ToggleDualPane(newPane);
        }
    }
}
