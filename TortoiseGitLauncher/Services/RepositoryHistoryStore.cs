using System.Text.Json;

namespace TortoiseGitLauncher;

internal static class RepositoryHistoryStore
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TortoiseGitLauncher",
        "recent-repos.json");

    public static List<RepositoryEntry> Load()
    {
        try
        {
            if (!File.Exists(StoragePath))
            {
                return [];
            }

            var json = File.ReadAllText(StoragePath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var entries = new List<RepositoryEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var path = element.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        entries.Add(new RepositoryEntry
                        {
                            RepoRootPath = GitPathHelper.NormalizePath(path)
                        });
                    }

                    continue;
                }

                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<RepositoryEntry>(element.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entry is null || string.IsNullOrWhiteSpace(entry.RepoRootPath))
                {
                    continue;
                }

                entry.RepoRootPath = GitPathHelper.NormalizePath(entry.RepoRootPath);
                entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
                entries.Add(entry);
            }

            var uniqueEntries = new List<RepositoryEntry>();
            foreach (var entry in entries)
            {
                if (uniqueEntries.Any(existing =>
                        string.Equals(existing.RepoRootPath, entry.RepoRootPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                uniqueEntries.Add(entry);
            }

            return uniqueEntries;
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IReadOnlyCollection<RepositoryEntry> repoEntries)
    {
        try
        {
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(repoEntries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var utf8NoBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(StoragePath, json, utf8NoBom);
        }
        catch
        {
            // 历史记录保存失败不影响主功能。
        }
    }
}
