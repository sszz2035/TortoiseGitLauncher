using System.Text.Json;

namespace TortoiseGitLauncher;

internal static class RepositoryHistoryStore
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TortoiseGitLauncher",
        "recent-repos.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static List<RepositoryEntry> Load() =>
        LoadSettings(out _).Entries;

    public static RepositoryHistorySettings LoadSettings(out string? warningMessage)
    {
        warningMessage = null;
        if (!File.Exists(StoragePath))
        {
            return new RepositoryHistorySettings();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(StoragePath));
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var migratedSettings = new RepositoryHistorySettings();
                var migratedSkippedEntries = LoadEntries(document.RootElement, migratedSettings, allowLegacyStrings: true);
                NormalizeSettings(migratedSettings);
                warningMessage = BuildWarning(migratedSkippedEntries, migrated: true);
                return migratedSettings;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                warningMessage = "仓库历史配置格式无效，已使用默认配置。";
                return new RepositoryHistorySettings();
            }

            var settings = new RepositoryHistorySettings();
            var skippedGroups = LoadGroups(document.RootElement, settings);
            var skippedEntries = LoadEntries(
                document.RootElement,
                settings,
                allowLegacyStrings: false);
            NormalizeSettings(settings);
            warningMessage = BuildWarning(
                skippedGroups + skippedEntries,
                migrated: false);
            return settings;
        }
        catch (Exception ex)
        {
            warningMessage = $"读取仓库历史配置失败，已使用默认配置。{Environment.NewLine}{ex.Message}";
            return new RepositoryHistorySettings();
        }
    }

    public static void Save(IReadOnlyCollection<RepositoryEntry> repoEntries)
    {
        var settings = new RepositoryHistorySettings
        {
            Entries = repoEntries.Select(entry => entry.Clone()).ToList()
        };
        Save(settings);
    }

    public static void Save(RepositoryHistorySettings settings)
    {
        try
        {
            NormalizeSettings(settings);
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(StoragePath, json, new System.Text.UTF8Encoding(false));
        }
        catch
        {
        }
    }

    private static int LoadGroups(
        JsonElement rootElement,
        RepositoryHistorySettings settings)
    {
        if (!rootElement.TryGetProperty(nameof(RepositoryHistorySettings.Groups), out var groupsElement) ||
            groupsElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var skippedEntries = 0;
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceEntries = new List<(RepositoryGroup Group, int SourceIndex)>();
        var sourceIndex = 0;

        foreach (var element in groupsElement.EnumerateArray())
        {
            try
            {
                var group = JsonSerializer.Deserialize<RepositoryGroup>(
                    element.GetRawText(),
                    JsonOptions);
                if (group is null)
                {
                    skippedEntries++;
                    sourceIndex++;
                    continue;
                }

                group.Id = EnsureUniqueGuid(group.Id, usedIds);
                group.Name = string.IsNullOrWhiteSpace(group.Name)
                    ? "未命名组"
                    : group.Name.Trim();
                sourceEntries.Add((group, sourceIndex));
            }
            catch
            {
                skippedEntries++;
            }

            sourceIndex++;
        }

        foreach (var entry in sourceEntries
                     .OrderBy(item => item.Group.DisplayOrder)
                     .ThenBy(item => item.SourceIndex))
        {
            settings.Groups.Add(entry.Group);
        }

        return skippedEntries;
    }

    private static int LoadEntries(
        JsonElement entriesElement,
        RepositoryHistorySettings settings,
        bool allowLegacyStrings)
    {
        JsonElement arrayElement;
        if (allowLegacyStrings)
        {
            arrayElement = entriesElement;
        }
        else if (!entriesElement.TryGetProperty(nameof(RepositoryHistorySettings.Entries), out arrayElement) ||
                 arrayElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var skippedEntries = 0;
        var sourceEntries = new List<(RepositoryEntry Entry, int SourceIndex)>();
        var sourceIndex = 0;

        foreach (var element in arrayElement.EnumerateArray())
        {
            try
            {
                RepositoryEntry? entry;
                if (element.ValueKind == JsonValueKind.String)
                {
                    entry = new RepositoryEntry
                    {
                        RepoRootPath = element.GetString() ?? string.Empty
                    };
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    entry = JsonSerializer.Deserialize<RepositoryEntry>(
                        element.GetRawText(),
                        JsonOptions);
                }
                else
                {
                    entry = null;
                }

                if (entry is null || string.IsNullOrWhiteSpace(entry.RepoRootPath))
                {
                    skippedEntries++;
                    sourceIndex++;
                    continue;
                }

                entry.RepoRootPath = GitPathHelper.NormalizePath(entry.RepoRootPath);
                entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
                sourceEntries.Add((entry, sourceIndex));
            }
            catch
            {
                skippedEntries++;
            }

            sourceIndex++;
        }

        var knownGroupIds = settings.Groups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceEntry in sourceEntries
                     .OrderBy(item => item.Entry.DisplayOrder)
                     .ThenBy(item => item.SourceIndex))
        {
            var entry = sourceEntry.Entry;
            if (!uniquePaths.Add(entry.RepoRootPath))
            {
                skippedEntries++;
                continue;
            }

            entry.GroupId = !string.IsNullOrWhiteSpace(entry.GroupId) &&
                            knownGroupIds.Contains(entry.GroupId)
                ? entry.GroupId
                : null;
            settings.Entries.Add(entry);
        }

        return skippedEntries;
    }

    private static void NormalizeSettings(RepositoryHistorySettings settings)
    {
        settings.Version = RepositoryHistorySettings.CurrentVersion;
        settings.Groups ??= [];
        settings.Entries ??= [];

        var usedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedGroupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < settings.Groups.Count; index++)
        {
            var group = settings.Groups[index];
            group.Id = EnsureUniqueGuid(group.Id, usedGroupIds);
            group.Name = EnsureUniqueGroupName(group.Name, usedGroupNames);
            group.DisplayOrder = index;
        }

        var knownGroupIds = settings.Groups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedEntries = new List<RepositoryEntry>();
        var orderedSourceEntries = settings.Entries
            .Select((entry, sourceIndex) => (Entry: entry, SourceIndex: sourceIndex))
            .OrderBy(item => item.Entry?.DisplayOrder ?? int.MaxValue)
            .ThenBy(item => item.SourceIndex)
            .ToList();
        foreach (var sourceEntry in orderedSourceEntries)
        {
            var entry = sourceEntry.Entry;
            if (entry is null || string.IsNullOrWhiteSpace(entry.RepoRootPath))
            {
                continue;
            }

            try
            {
                entry.RepoRootPath = GitPathHelper.NormalizePath(entry.RepoRootPath);
            }
            catch
            {
                continue;
            }

            if (!uniquePaths.Add(entry.RepoRootPath))
            {
                continue;
            }

            entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
            entry.GroupId = !string.IsNullOrWhiteSpace(entry.GroupId) &&
                            knownGroupIds.Contains(entry.GroupId)
                ? entry.GroupId
                : null;
            entry.DisplayOrder = normalizedEntries.Count;
            normalizedEntries.Add(entry);
        }

        settings.Entries.Clear();
        settings.Entries.AddRange(normalizedEntries);
    }
    private static string EnsureUniqueGroupName(
        string? value,
        HashSet<string> usedNames)
    {
        var baseName = string.IsNullOrWhiteSpace(value)
            ? "未命名组"
            : value.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (!usedNames.Add(candidate))
        {
            candidate = $"{baseName} ({suffix})";
            suffix++;
        }

        return candidate;
    }
    private static string EnsureUniqueGuid(
        string? value,
        HashSet<string> usedIds)
    {
        var candidate = Guid.TryParse(value, out var parsed)
            ? parsed.ToString("D")
            : Guid.NewGuid().ToString("D");
        while (!usedIds.Add(candidate))
        {
            candidate = Guid.NewGuid().ToString("D");
        }

        return candidate;
    }

    private static string? BuildWarning(int skippedEntries, bool migrated)
    {
        if (migrated && skippedEntries == 0)
        {
            return "已将旧版仓库列表迁移为未分组配置。";
        }

        if (skippedEntries == 0)
        {
            return null;
        }

        var prefix = migrated
            ? "旧版仓库列表已迁移，"
            : "仓库历史配置中有";
        return $"{prefix}已跳过 {skippedEntries} 个无效项。";
    }
}