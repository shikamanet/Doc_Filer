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

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    [ObservableProperty]
    private bool _isSelected;
}
