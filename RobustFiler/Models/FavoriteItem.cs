using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System.Threading.Tasks;

namespace RobustFiler.Models;

public partial class FavoriteItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [JsonIgnore]
    [ObservableProperty]
    private ImageSource? _icon;

    public FavoriteItem()
    {
    }

    public async Task LoadIconAsync()
    {
        if (Icon == null && !string.IsNullOrEmpty(Path))
        {
            Icon = await RobustFiler.Helpers.IconHelper.GetIconAsync(Path, isDirectory: true);
        }
    }
}
