using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal sealed class RepositoryHistorySettings
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public List<RepositoryGroup> Groups { get; set; } = [];

    public List<RepositoryEntry> Entries { get; set; } = [];
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