using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RobustFiler.ViewModels;

public partial class TabGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "新しいグループ";

    [ObservableProperty]
    private string _colorHex = "Transparent";

    public ObservableCollection<TabItemViewModel> Tabs { get; } = new();

    private TabItemViewModel? _selectedTab;
    public TabItemViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab != null)
                _selectedTab.IsSelected = false;
            
            if (SetProperty(ref _selectedTab, value))
            {
                if (_selectedTab != null)
                    _selectedTab.IsSelected = true;
            }
        }
    }

    public ObservableCollection<TabItemViewModel> SecondaryTabs { get; } = new();

    private TabItemViewModel? _secondarySelectedTab;
    public TabItemViewModel? SecondarySelectedTab
    {
        get => _secondarySelectedTab;
        set
        {
            if (_secondarySelectedTab != null)
                _secondarySelectedTab.IsSelected = false;
            
            if (SetProperty(ref _secondarySelectedTab, value))
            {
                if (_secondarySelectedTab != null)
                    _secondarySelectedTab.IsSelected = true;
            }
        }
    }

    [ObservableProperty]
    private bool _isSelected;
}
