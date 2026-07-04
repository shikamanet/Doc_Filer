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

    public ObservableCollection<TabItemViewModel> Tabs => SelectedTabGroup?.Tabs ?? new ObservableCollection<TabItemViewModel>();
    public ObservableCollection<FavoriteItem> Favorites { get; } = new();
    public ObservableCollection<TabGroupViewModel> TabGroups { get; } = new();

    private TabGroupViewModel? _selectedTabGroup;
    public TabGroupViewModel? SelectedTabGroup
    {
        get => _selectedTabGroup;
        set
        {
            if (SetProperty(ref _selectedTabGroup, value))
            {
                foreach (var group in TabGroups)
                {
                    group.IsSelected = (group == _selectedTabGroup);
                }
                OnPropertyChanged(nameof(Tabs));
                OnPropertyChanged(nameof(SelectedTab));
                _ = SaveSessionAsync();
            }
        }
    }

    public TabItemViewModel? SelectedTab
    {
        get => SelectedTabGroup?.SelectedTab;
        set
        {
            if (SelectedTabGroup != null)
            {
                if (SelectedTabGroup.SelectedTab != value)
                {
                    SelectedTabGroup.SelectedTab = value;
                    OnPropertyChanged();
                    OnSelectedTabChanged(value);
                }
            }
        }
    }

    [ObservableProperty]
    private FavoriteItem? _selectedFavorite;

    public MainWindowViewModel(IFileService fileService, IDialogService dialogService)
    {
        _fileService = fileService;
        _dialogService = dialogService;

        WeakReferenceMessenger.Default.Register<AddFavoriteMessage>(this, (r, m) =>
        {
            AddFavorite(m.Value);
        });
        
        WeakReferenceMessenger.Default.Register<SessionChangedMessage>(this, (r, m) =>
        {
            _ = SaveSessionAsync();
        });

        WeakReferenceMessenger.Default.Register<OpenNewTabMessage>(this, (r, m) =>
        {
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() ?? App.Current.MainWindow?.DispatcherQueue;
            dispatcherQueue?.TryEnqueue(() =>
            {
                if (SelectedTabGroup == null) return;
                var primaryPane = new FilePaneViewModel(_fileService, _dialogService);
                _ = primaryPane.NavigateAsync(m.Path);
                var tab = new TabItemViewModel(primaryPane);
                SelectedTabGroup.Tabs.Add(tab);
                SelectedTab = tab;
                _ = SaveSessionAsync();
            });
        });

        _ = LoadFavoritesAsync();
        _ = LoadSessionAsync();
    }

    private async Task LoadFavoritesAsync()
    {
        var favs = await _fileService.LoadFavoritesAsync();
        Favorites.Clear();
        foreach (var item in favs)
        {
            await LoadIconsRecursiveAsync(item);
            Favorites.Add(item);
        }
    }

    private async Task LoadIconsRecursiveAsync(FavoriteItem item)
    {
        if (!item.IsFolder)
        {
            await item.LoadIconAsync();
        }
        foreach (var child in item.Children)
        {
            await LoadIconsRecursiveAsync(child);
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
            RemoveFavoriteRecursive(Favorites, item);
            _ = SaveFavoritesAsync();
        }
    }

    private bool RemoveFavoriteRecursive(System.Collections.Generic.IList<FavoriteItem> list, FavoriteItem target)
    {
        if (list.Contains(target))
        {
            list.Remove(target);
            return true;
        }
        foreach (var node in list)
        {
            if (node.IsFolder && RemoveFavoriteRecursive(node.Children, target))
            {
                return true;
            }
        }
        return false;
    }

    [RelayCommand]
    public async Task AddFavoriteFolderAsync()
    {
        var folderName = await _dialogService.ShowCreateFolderDialogAsync();
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            var folder = new FavoriteItem
            {
                Name = folderName,
                IsFolder = true
            };
            Favorites.Add(folder);
            _ = SaveFavoritesAsync();
        }
    }

    [RelayCommand]
    public async Task EditFavoriteAsync(FavoriteItem? item)
    {
        if (item != null)
        {
            var result = await _dialogService.ShowFavoriteSettingsDialogAsync(item);
            if (result != null)
            {
                item.Name = result.Name;
                item.Path = result.Path;
                item.Arguments = result.Arguments;
                
                if (!item.IsFolder && !string.IsNullOrEmpty(item.Path))
                {
                    item.Icon = await RobustFiler.Helpers.IconHelper.GetIconAsync(item.Path, isDirectory: System.IO.Directory.Exists(item.Path));
                }
                _ = SaveFavoritesAsync();
            }
        }
    }

    [RelayCommand]
    private async Task SaveFavoritesAsync()
    {
        await _fileService.SaveFavoritesAsync(Favorites);
    }

    private void OnSelectedTabChanged(TabItemViewModel? value)
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
        if (session != null)
        {
            if (session.Groups != null && session.Groups.Count > 0)
            {
                foreach (var groupState in session.Groups)
                {
                    var groupVm = new TabGroupViewModel
                    {
                        Name = groupState.Name,
                        ColorHex = groupState.ColorHex
                    };
                    
                    foreach (var tabState in groupState.Tabs)
                    {
                        var tab = CreateTabFromState(tabState);
                        groupVm.Tabs.Add(tab);
                    }
                    if (groupState.SelectedTabIndex >= 0 && groupState.SelectedTabIndex < groupVm.Tabs.Count)
                    {
                        groupVm.SelectedTab = groupVm.Tabs[groupState.SelectedTabIndex];
                    }
                    else
                    {
                        groupVm.SelectedTab = groupVm.Tabs.FirstOrDefault();
                    }
                    TabGroups.Add(groupVm);
                }
                
                if (session.SelectedGroupIndex >= 0 && session.SelectedGroupIndex < TabGroups.Count)
                {
                    SelectedTabGroup = TabGroups[session.SelectedGroupIndex];
                }
                else
                {
                    SelectedTabGroup = TabGroups.FirstOrDefault();
                }
            }
            else if (session.Tabs != null && session.Tabs.Count > 0)
            {
                // Legacy support for older session formats
                var defaultGroup = new TabGroupViewModel { Name = "デフォルト" };
                foreach (var tabState in session.Tabs)
                {
                    var tab = CreateTabFromState(tabState);
                    defaultGroup.Tabs.Add(tab);
                }
                if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < defaultGroup.Tabs.Count)
                {
                    defaultGroup.SelectedTab = defaultGroup.Tabs[session.SelectedTabIndex];
                }
                else
                {
                    defaultGroup.SelectedTab = defaultGroup.Tabs.FirstOrDefault();
                }
                TabGroups.Add(defaultGroup);
                SelectedTabGroup = defaultGroup;
            }
        }

        if (TabGroups.Count == 0)
        {
            AddTabGroup();
        }
    }

    private TabItemViewModel CreateTabFromState(TabState tabState)
    {
        var primaryPane = new FilePaneViewModel(_fileService, _dialogService);
        if (!string.IsNullOrEmpty(tabState.PrimaryPath))
        {
            _ = primaryPane.NavigateAsync(tabState.PrimaryPath);
        }

        var tab = new TabItemViewModel(primaryPane) { Header = tabState.Header, IsLocked = tabState.IsLocked };
        
        if (tabState.IsDualPane)
        {
            var secondaryPane = new FilePaneViewModel(_fileService, _dialogService);
            if (!string.IsNullOrEmpty(tabState.SecondaryPath))
            {
                _ = secondaryPane.NavigateAsync(tabState.SecondaryPath);
            }
            tab.ToggleDualPane(secondaryPane);
        }
        return tab;
    }

    private async Task SaveSessionAsync()
    {
        var session = new SessionState
        {
            SelectedGroupIndex = SelectedTabGroup != null ? TabGroups.IndexOf(SelectedTabGroup) : 0,
            Groups = TabGroups.Select(g => new TabGroupState
            {
                Name = g.Name,
                ColorHex = g.ColorHex,
                SelectedTabIndex = g.SelectedTab != null ? g.Tabs.IndexOf(g.SelectedTab) : 0,
                Tabs = g.Tabs.Select(t => new TabState
                {
                    Header = t.Header,
                    IsDualPane = t.IsDualPane,
                    PrimaryPath = t.PrimaryPane.CurrentPath,
                    SecondaryPath = t.SecondaryPane?.CurrentPath ?? string.Empty,
                    IsLocked = t.IsLocked
                }).ToList()
            }).ToList()
        };
        await _fileService.SaveSessionAsync(session);
    }

    partial void OnSelectedFavoriteChanged(FavoriteItem? value)
    {
        if (value != null && SelectedTab?.PrimaryPane != null)
        {
            if (value.IsFolder)
            {
                // Toggle expansion or do nothing
                value.IsExpanded = !value.IsExpanded;
            }
            else if (System.IO.Directory.Exists(value.Path))
            {
                _ = SelectedTab.PrimaryPane.NavigateToPathAsync(value.Path, bringToTop: true);
            }
            else if (System.IO.File.Exists(value.Path))
            {
                _ = _fileService.OpenFileAsync(value.Path, value.Arguments ?? "");
            }
            else
            {
                _ = _dialogService.ShowErrorAsync("エラー", new System.Exception("指定されたパスが見つかりません。移動または削除された可能性があります。"));
            }
            
            // Reset selection so the user can click it again
            SelectedFavorite = null; 
        }
    }

    [RelayCommand]
    public void AddTabGroup()
    {
        var newGroup = new TabGroupViewModel { Name = $"グループ {TabGroups.Count + 1}" };
        var primaryPane = new FilePaneViewModel(_fileService, _dialogService);
        var tab = new TabItemViewModel(primaryPane);
        newGroup.Tabs.Add(tab);
        newGroup.SelectedTab = tab;
        
        TabGroups.Add(newGroup);
        SelectedTabGroup = newGroup;
    }

    [RelayCommand]
    public void RemoveTabGroup(TabGroupViewModel? group)
    {
        if (group != null && TabGroups.Contains(group))
        {
            TabGroups.Remove(group);
            if (TabGroups.Count == 0)
            {
                AddTabGroup();
            }
            else if (SelectedTabGroup == null)
            {
                SelectedTabGroup = TabGroups.FirstOrDefault();
            }
            _ = SaveSessionAsync();
        }
    }

    [RelayCommand]
    public async Task RenameTabGroup(TabGroupViewModel? group)
    {
        if (group != null)
        {
            var newName = await _dialogService.ShowInputDialogAsync("グループ名の変更", group.Name);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                group.Name = newName;
                _ = SaveSessionAsync();
            }
        }
    }

    [RelayCommand]
    public void ChangeTabGroupColor(TabGroupViewModel? group)
    {
        // This is a placeholder for standard relay command if needed from UI.
        // The actual color change can be done directly by passing parameter or through UI flyouts.
    }

    public void SetTabGroupColor(TabGroupViewModel group, string colorHex)
    {
        if (group != null)
        {
            group.ColorHex = colorHex;
            _ = SaveSessionAsync();
        }
    }

    [RelayCommand]
    public void AddTab()
    {
        if (SelectedTabGroup == null) return;
        var primaryPane = new FilePaneViewModel(_fileService, _dialogService);
        var tab = new TabItemViewModel(primaryPane);
        SelectedTabGroup.Tabs.Add(tab);
        SelectedTabGroup.SelectedTab = tab;
        SelectedTab = tab; // This will trigger OnSelectedTabChanged and SaveSession
    }

    [RelayCommand]
    public void CloseTab(TabItemViewModel? tab)
    {
        if (SelectedTabGroup == null) return;
        if (tab != null && SelectedTabGroup.Tabs.Contains(tab) && !tab.IsLocked)
        {
            SelectedTabGroup.Tabs.Remove(tab);
            tab.Dispose();
            if (SelectedTabGroup.Tabs.Count == 0)
            {
                AddTab();
            }
            _ = SaveSessionAsync();
        }
    }

    [RelayCommand]
    public void CloseOtherTabs(TabItemViewModel? tab)
    {
        if (SelectedTabGroup == null || tab == null) return;
        var tabsToRemove = SelectedTabGroup.Tabs.Where(t => t != tab && !t.IsLocked).ToList();
        foreach (var t in tabsToRemove)
        {
            SelectedTabGroup.Tabs.Remove(t);
            t.Dispose();
        }
        _ = SaveSessionAsync();
    }

    [RelayCommand]
    public void CloseTabsToRight(TabItemViewModel? tab)
    {
        if (SelectedTabGroup == null || tab == null) return;
        int index = SelectedTabGroup.Tabs.IndexOf(tab);
        if (index >= 0)
        {
            var tabsToRemove = SelectedTabGroup.Tabs.Skip(index + 1).Where(t => !t.IsLocked).ToList();
            foreach (var t in tabsToRemove)
            {
                SelectedTabGroup.Tabs.Remove(t);
                t.Dispose();
            }
            _ = SaveSessionAsync();
        }
    }

    [RelayCommand]
    public void CloseAllTabs()
    {
        if (SelectedTabGroup == null) return;
        var tabsToRemove = SelectedTabGroup.Tabs.Where(t => !t.IsLocked).ToList();
        foreach (var t in tabsToRemove)
        {
            SelectedTabGroup.Tabs.Remove(t);
            t.Dispose();
        }
        if (SelectedTabGroup.Tabs.Count == 0)
        {
            AddTab();
        }
        _ = SaveSessionAsync();
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
