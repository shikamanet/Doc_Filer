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
        
        WeakReferenceMessenger.Default.Register<SessionChangedMessage>(this, (r, m) =>
        {
            _ = SaveSessionAsync();
        });

        _ = LoadFavoritesAsync();
        _ = LoadSessionAsync();
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

    partial void OnSelectedTabChanged(TabItemViewModel? value)
    {
        if (value != null)
        {
            ToggleDualPaneCommand.NotifyCanExecuteChanged();
            _ = SaveSessionAsync();
        }
    }

    private async Task LoadSessionAsync()
    {
        var session = await _fileService.LoadSessionAsync();
        if (session != null && session.Tabs.Count > 0)
        {
            foreach (var tabState in session.Tabs)
            {
                var primaryPane = new FilePaneViewModel(_fileService, _dialogService);
                if (!string.IsNullOrEmpty(tabState.PrimaryPath))
                {
                    _ = primaryPane.NavigateAsync(tabState.PrimaryPath);
                }

                var tab = new TabItemViewModel(primaryPane) { Header = tabState.Header };
                
                if (tabState.IsDualPane)
                {
                    var secondaryPane = new FilePaneViewModel(_fileService, _dialogService);
                    if (!string.IsNullOrEmpty(tabState.SecondaryPath))
                    {
                        _ = secondaryPane.NavigateAsync(tabState.SecondaryPath);
                    }
                    tab.ToggleDualPane(secondaryPane);
                }

                Tabs.Add(tab);
            }

            if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < Tabs.Count)
            {
                SelectedTab = Tabs[session.SelectedTabIndex];
            }
            else
            {
                SelectedTab = Tabs.FirstOrDefault();
            }
        }
        else
        {
            AddTab();
        }
    }

    private async Task SaveSessionAsync()
    {
        var session = new SessionState
        {
            SelectedTabIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0,
            Tabs = Tabs.Select(t => new TabState
            {
                Header = t.Header,
                IsDualPane = t.IsDualPane,
                PrimaryPath = t.PrimaryPane.CurrentPath,
                SecondaryPath = t.SecondaryPane?.CurrentPath ?? string.Empty
            }).ToList()
        };
        await _fileService.SaveSessionAsync(session);
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
        _ = SaveSessionAsync();
    }

    [RelayCommand]
    public void CloseTab(TabItemViewModel? tab)
    {
        if (tab != null && Tabs.Contains(tab))
        {
            Tabs.Remove(tab);
            tab.Dispose();
            if (Tabs.Count == 0)
            {
                AddTab();
            }
            _ = SaveSessionAsync();
        }
    }

    [RelayCommand]
    public void ToggleDualPane()
    {
        if (SelectedTab != null)
        {
            if (!SelectedTab.IsDualPane)
            {
                var newPane = new FilePaneViewModel(_fileService, _dialogService);
                SelectedTab.ToggleDualPane(newPane);
            }
            else
            {
                SelectedTab.ToggleDualPane(null);
            }
            _ = SaveSessionAsync();
        }
    }
}
