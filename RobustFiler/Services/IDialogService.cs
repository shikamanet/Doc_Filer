using System;
using System.Threading.Tasks;

namespace RobustFiler.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task<string?> ShowInputDialogAsync(string title, string defaultText = "");
    Task ShowErrorAsync(string title, Exception ex);
}
