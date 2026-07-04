using System.Collections.Generic;

namespace RobustFiler.Models;

public class TabState
{
    public string Header { get; set; } = string.Empty;
    public string PrimaryPath { get; set; } = string.Empty;
    public bool IsDualPane { get; set; }
    public string SecondaryPath { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
}

public class SessionState
{
    public List<TabState> Tabs { get; set; } = new();
    public int SelectedTabIndex { get; set; }
}
