using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal sealed class ScriptRunnerSettings
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public List<ExecutionDirectoryGroup> DirectoryGroups { get; set; } = [];

    public List<ExecutionDirectoryEntry> RecentDirectories { get; set; } = [];

    public string LastSelectedDirectoryPath { get; set; } = string.Empty;

    public List<ScriptConfiguration> Scripts { get; set; } = [];
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
            GroupId = GroupId,
            DisplayOrder = DisplayOrder
        };

    public override string ToString() => DisplayLabel;
}