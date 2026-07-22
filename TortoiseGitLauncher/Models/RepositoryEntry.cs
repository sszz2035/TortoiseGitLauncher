using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal sealed class RepositoryEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string RepoRootPath { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? RepoRootPath
            : $"{DisplayName} [{RepoRootPath}]";

    public RepositoryEntry Clone() =>
        new()
        {
            DisplayName = DisplayName,
            RepoRootPath = RepoRootPath
        };

    public override string ToString() => DisplayLabel;
}
