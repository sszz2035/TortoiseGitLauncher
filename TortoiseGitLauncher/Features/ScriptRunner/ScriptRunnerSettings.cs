using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal sealed class ScriptRunnerSettings
{
    public const int CurrentVersion = 4;

    public int Version { get; set; } = CurrentVersion;

    public List<ExecutionDirectoryPage> Pages { get; set; } = [new()];
    public string LastSelectedPageId { get; set; } = string.Empty;

    [JsonIgnore]
    public string? SaveBlockedReason { get; set; }

    [JsonIgnore]
    public ExecutionDirectoryPage ActivePage => DirectoryPageCollection.GetActive(Pages, LastSelectedPageId);

    [JsonIgnore]
    public List<ExecutionDirectoryGroup> DirectoryGroups { get => ActivePage.DirectoryGroups; set => ActivePage.DirectoryGroups = value; }

    [JsonIgnore]
    public List<ExecutionDirectoryEntry> RecentDirectories { get => ActivePage.RecentDirectories; set => ActivePage.RecentDirectories = value; }

    [JsonIgnore]
    public string LastSelectedDirectoryPath
    {
        get => ActivePage.LastSelectedDirectoryPath;
        set => ActivePage.LastSelectedDirectoryPath = value;
    }

    public List<ScriptConfiguration> Scripts { get; set; } = [];

    public ScriptRunnerSettings Clone() => new()
    {
        Version = Version,
        Pages = Pages.Select(page => page.Clone()).ToList(),
        LastSelectedPageId = LastSelectedPageId,
        Scripts = Scripts.Select(script => script.Clone()).ToList(),
        SaveBlockedReason = SaveBlockedReason
    };
}

internal sealed class ExecutionDirectoryPage : DirectoryPage
{
    public ExecutionDirectoryPage() { }

    public List<ExecutionDirectoryGroup> DirectoryGroups { get; set; } = [];

    public List<ExecutionDirectoryEntry> RecentDirectories { get; set; } = [];

    public ExecutionDirectoryPage Clone() => new()
    {
        Id = Id, Name = Name, DisplayOrder = DisplayOrder,
        LastSelectedDirectoryPath = LastSelectedDirectoryPath,
        DirectoryGroups = DirectoryGroups.Select(group => group.Clone()).ToList(),
        RecentDirectories = RecentDirectories.Select(entry => entry.Clone()).ToList()
    };
}

internal sealed class ExecutionDirectoryGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsExpanded { get; set; } = true;

    public ExecutionDirectoryGroup Clone() =>
        new()
        {
            Id = Id,
            Name = Name,
            DisplayOrder = DisplayOrder,
            IsExpanded = IsExpanded
        };

    public override string ToString() => Name;
}

internal sealed class ExecutionDirectoryEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string DirectoryPath { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string? GroupId { get; set; }

    public int DisplayOrder { get; set; }

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? DirectoryPath
            : $"{DisplayName} [{DirectoryPath}]";

    public ExecutionDirectoryEntry Clone() =>
        new()
        {
            DisplayName = DisplayName,
            DirectoryPath = DirectoryPath,
            Notes = Notes,
            GroupId = GroupId,
            DisplayOrder = DisplayOrder
        };

    public override string ToString() => DisplayLabel;
}
internal sealed class ExecutionDirectoryGroupComboItem(ExecutionDirectoryGroup group)
{
    public ExecutionDirectoryGroup Group { get; } = group;

    public override string ToString() =>
        $"{(Group.IsExpanded ? "▼" : "▶")} {Group.Name}";
}

internal sealed class ExecutionDirectoryEntryComboItem(ExecutionDirectoryEntry entry, bool indented)
{
    public ExecutionDirectoryEntry Entry { get; } = entry;

    public bool Indented { get; } = indented;

    public override string ToString() =>
        Indented
            ? $"    {Entry.DisplayLabel}"
            : Entry.DisplayLabel;
}
