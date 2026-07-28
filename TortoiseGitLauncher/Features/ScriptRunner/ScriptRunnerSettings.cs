using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal sealed class ScriptRunnerSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<ExecutionDirectoryEntry> RecentDirectories { get; set; } = [];

    public string LastSelectedDirectoryPath { get; set; } = string.Empty;
}

internal sealed class ExecutionDirectoryEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string DirectoryPath { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? DirectoryPath
            : $"{DisplayName} [{DirectoryPath}]";

    public ExecutionDirectoryEntry Clone() =>
        new()
        {
            DisplayName = DisplayName,
            DirectoryPath = DirectoryPath
        };

    public override string ToString() => DisplayLabel;
}
