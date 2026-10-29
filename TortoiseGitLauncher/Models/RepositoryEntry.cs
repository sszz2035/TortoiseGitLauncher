using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal sealed class RepositoryHistorySettings
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public List<RepositoryDirectoryPage> Pages { get; set; } = [new()];
    public string LastSelectedPageId { get; set; } = string.Empty;

    [JsonIgnore]
    public string? SaveBlockedReason { get; set; }

    [JsonIgnore]
    public bool AllowInitialDirectoryFallback { get; set; } = true;

    [JsonIgnore]
    public RepositoryDirectoryPage ActivePage => DirectoryPageCollection.GetActive(Pages, LastSelectedPageId);

    // Compatibility views for the current UI. Only Pages is serialized.
    [JsonIgnore]
    public List<RepositoryGroup> Groups { get => ActivePage.Groups; set => ActivePage.Groups = value; }

    [JsonIgnore]
    public List<RepositoryEntry> Entries { get => ActivePage.Entries; set => ActivePage.Entries = value; }

    public RepositoryHistorySettings Clone() => new()
    {
        Version = Version,
        Pages = Pages.Select(page => page.Clone()).ToList(),
        LastSelectedPageId = LastSelectedPageId,
        SaveBlockedReason = SaveBlockedReason,
        AllowInitialDirectoryFallback = AllowInitialDirectoryFallback
    };
}

internal sealed class RepositoryDirectoryPage : DirectoryPage
{
    public RepositoryDirectoryPage() { }

    public List<RepositoryGroup> Groups { get; set; } = [];

    public List<RepositoryEntry> Entries { get; set; } = [];

    public RepositoryDirectoryPage Clone() => new()
    {
        Id = Id, Name = Name, DisplayOrder = DisplayOrder,
        LastSelectedDirectoryPath = LastSelectedDirectoryPath,
        Groups = Groups.Select(group => group.Clone()).ToList(),
        Entries = Entries.Select(entry => entry.Clone()).ToList()
    };
}

internal sealed class RepositoryGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsExpanded { get; set; } = true;

    public RepositoryGroup Clone() =>
        new()
        {
            Id = Id,
            Name = Name,
            DisplayOrder = DisplayOrder,
            IsExpanded = IsExpanded
        };

    public override string ToString() => Name;
}

internal sealed class RepositoryEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string RepoRootPath { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string? GroupId { get; set; }

    public int DisplayOrder { get; set; }

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? RepoRootPath
            : $"{DisplayName} [{RepoRootPath}]";

    public RepositoryEntry Clone() =>
        new()
        {
            DisplayName = DisplayName,
            RepoRootPath = RepoRootPath,
            Notes = Notes,
            GroupId = GroupId,
            DisplayOrder = DisplayOrder
        };

    public override string ToString() => DisplayLabel;
}
internal sealed class RepositoryGroupComboItem(RepositoryGroup group)
{
    public RepositoryGroup Group { get; } = group;

    public override string ToString() =>
        $"{(Group.IsExpanded ? "▼" : "▶")} {Group.Name}";
}

internal sealed class RepositoryEntryComboItem(RepositoryEntry entry, bool indented)
{
    public RepositoryEntry Entry { get; } = entry;

    public bool Indented { get; } = indented;

    public override string ToString() =>
        Indented
            ? $"    {Entry.DisplayLabel}"
            : Entry.DisplayLabel;
}
