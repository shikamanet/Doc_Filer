using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RobustFiler.ViewModels;

public partial class TabItemViewModel : ObservableObject, IDisposable
{
    public FilePaneViewModel PrimaryPane { get; }

    [ObservableProperty]
    private FilePaneViewModel? _secondaryPane;

    [ObservableProperty]
    private bool _isDualPane;

    [ObservableProperty]
    private string _header = "新しいタブ";

    public TabItemViewModel(FilePaneViewModel primaryPane)
    {
        PrimaryPane = primaryPane;
        PrimaryPane.PropertyChanged += PrimaryPane_PropertyChanged;
        UpdateHeader();
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
            Header = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(Header))
            {
                Header = path; // Drive root e.g. "C:\"
            }
        }
    }

    public void ToggleDualPane(FilePaneViewModel? newSecondaryPane)
    {
        if (IsDualPane)
        {
            SecondaryPane?.Dispose();
            SecondaryPane = null;
            IsDualPane = false;
        }
        else
        {
            SecondaryPane = newSecondaryPane;
            if (SecondaryPane != null)
            {
                _ = SecondaryPane.NavigateAsync(PrimaryPane.CurrentPath);
            }
            IsDualPane = true;
        }
    }

    public void Dispose()
    {
        PrimaryPane.PropertyChanged -= PrimaryPane_PropertyChanged;
        PrimaryPane.Dispose();
        SecondaryPane?.Dispose();
    }
}
