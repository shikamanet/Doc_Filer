using System;
using System.Threading.Tasks;

namespace RobustFiler.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string content);
    Task<string?> ShowInputDialogAsync(string title, string defaultText = "");
    Task<RobustFiler.Models.FavoriteItem?> ShowFavoriteSettingsDialogAsync(RobustFiler.Models.FavoriteItem currentItem);
    Task<string?> ShowCreateFolderDialogAsync();
    Task ShowErrorAsync(string title, Exception ex);
}
