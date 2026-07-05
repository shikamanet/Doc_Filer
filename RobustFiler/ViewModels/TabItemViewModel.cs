using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace RobustFiler.ViewModels;

public partial class TabItemViewModel : ObservableObject, IDisposable
{
    public FilePaneViewModel PrimaryPane { get; }



    [ObservableProperty]
    private string _header = "新しいタブ";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClosable))]
    [NotifyPropertyChangedFor(nameof(IsCloseButtonVisible))]
    private bool _isLocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCloseButtonVisible))]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCloseButtonVisible))]
    private bool _isHovered;

    public bool IsClosable => !IsLocked;

    public bool IsCloseButtonVisible => IsClosable && IsHovered;

    public TabItemViewModel(FilePaneViewModel primaryPane)
    {
        PrimaryPane = primaryPane;
        PrimaryPane.PropertyChanged += PrimaryPane_PropertyChanged;
        PrimaryPane.NavigationInterceptor = OnPaneNavigating;
        UpdateHeader();
    }

    private bool OnPaneNavigating(string targetPath)
    {
        if (IsLocked)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RobustFiler.Messages.OpenNewTabMessage(targetPath));
            return true;
        }
        return false;
    }

    private void PrimaryPane_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilePaneViewModel.CurrentPath))
        {
            UpdateHeader();
        }
    }

    private void UpdateHeader()
    {
        var path = PrimaryPane.CurrentPath;
        if (string.IsNullOrEmpty(path))
        {
            Header = "PC";
        }
        else
        {
            var trimmedPath = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            Header = System.IO.Path.GetFileName(trimmedPath);
            if (string.IsNullOrEmpty(Header))
            {
                Header = path; // Drive root e.g. "C:\"
            }
        }
    }



    public void Dispose()
    {
        PrimaryPane.PropertyChanged -= PrimaryPane_PropertyChanged;
        PrimaryPane.Dispose();
    }
}
