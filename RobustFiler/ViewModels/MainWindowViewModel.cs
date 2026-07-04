using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobustFiler.Services;

namespace RobustFiler.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<TabItemViewModel> Tabs { get; } = new();

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    public MainWindowViewModel(IFileService fileService, IDialogService dialogService)
    {
        _fileService = fileService;
        _dialogService = dialogService;
        AddTab();
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
