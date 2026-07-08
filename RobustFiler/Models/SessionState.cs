using System.Collections.Generic;

namespace RobustFiler.Models;

public class TabState
{
    public string Header { get; set; } = string.Empty;
    public string PrimaryPath { get; set; } = string.Empty;
    public bool IsDualPane { get; set; }
    public string SecondaryPath { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public List<string> PrimaryExpandedPaths { get; set; } = new();
    public List<string> SecondaryExpandedPaths { get; set; } = new();
    public string PrimaryTreeRootPath { get; set; } = string.Empty;
    public string SecondaryTreeRootPath { get; set; } = string.Empty;
}

public class TabGroupState
{
    public string Name { get; set; } = "グループ1";
    public string ColorHex { get; set; } = "Transparent";
    public List<TabState> Tabs { get; set; } = new();
    public int SelectedTabIndex { get; set; }
    
    public List<TabState> SecondaryTabs { get; set; } = new();
    public int SecondarySelectedTabIndex { get; set; }
}

public class SessionState
{
    // Legacy support for migration
    public List<TabState>? Tabs { get; set; }
    public int SelectedTabIndex { get; set; }

    // New format
    public List<TabGroupState> Groups { get; set; } = new();
    public int SelectedGroupIndex { get; set; }
}
