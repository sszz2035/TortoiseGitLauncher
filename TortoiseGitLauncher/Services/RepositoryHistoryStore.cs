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

    public static RepositoryHistorySettings LoadSettings(out string? warningMessage) =>
        LoadSettingsFrom(StoragePath, out warningMessage);

    internal static RepositoryHistorySettings LoadSettingsFrom(string path, out string? warningMessage)
    {
        warningMessage = null;
        if (!File.Exists(path))
        {
            return new RepositoryHistorySettings();
        }

        try
        {
            return Parse(File.ReadAllText(path), out warningMessage);
        }
        catch (Exception ex)
        {
            warningMessage = $"读取仓库历史配置失败，已阻止保存以保护原文件。{Environment.NewLine}{ex.Message}";
            return new RepositoryHistorySettings { SaveBlockedReason = warningMessage };
        }
    }

    internal static RepositoryHistorySettings Parse(string json, out string? warningMessage)
    {
        warningMessage = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = SafeConfigurationFile.ReadVersion(root, RepositoryHistorySettings.CurrentVersion);
            var settings = new RepositoryHistorySettings();
            var skipped = 0;
            var migrated = false;
            if (root.ValueKind == JsonValueKind.Array)
            {
                skipped = LoadEntries(root, settings.ActivePage, allowLegacyStrings: true);
                migrated = true;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty(nameof(RepositoryHistorySettings.Pages), out var pages))
                {
                    settings.AllowInitialDirectoryFallback = false;
                    if (pages.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException("目录页集合格式无效。");
                    settings.Pages.Clear();
                    var sourceIndex = 0;
                    foreach (var element in pages.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.Object ||
                            !element.TryGetProperty("Groups", out var groups) || groups.ValueKind != JsonValueKind.Array ||
                            !element.TryGetProperty("Entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                        {
                            skipped++;
                            sourceIndex++;
                            continue;
                        }

                        var page = new RepositoryDirectoryPage
                        {
                            Id = SafeConfigurationFile.ReadString(element, "Id", Guid.NewGuid().ToString("D")),
                            Name = SafeConfigurationFile.ReadString(element, "Name", "未命名页"),
                            DisplayOrder = SafeConfigurationFile.ReadOrder(element, sourceIndex++),
                            LastSelectedDirectoryPath = SafeConfigurationFile.ReadString(element, "LastSelectedDirectoryPath")
                        };
                        skipped += LoadGroups(element, page);
                        skipped += LoadEntries(element, page, allowLegacyStrings: false);
                        settings.Pages.Add(page);
                    }

                    if (settings.Pages.Count == 0)
                        throw new InvalidDataException("没有可恢复的目录页，已保护原文件。");
                    settings.LastSelectedPageId = SafeConfigurationFile.ReadString(root, "LastSelectedPageId");
                }
                else
                {
                    if (version >= RepositoryHistorySettings.CurrentVersion)
                        throw new InvalidDataException("新版配置缺少目录页集合。");
                    skipped = LoadGroups(root, settings.ActivePage) +
                              LoadEntries(root, settings.ActivePage, allowLegacyStrings: false);
                    migrated = true;
                }
            }
            else
            {
                throw new InvalidDataException("仓库历史配置格式无效。");
            }

            NormalizeSettings(settings);
            warningMessage = BuildWarning(skipped, migrated);
            return settings;
        }
        catch (Exception ex)
        {
            warningMessage = $"读取仓库历史配置失败，已阻止保存以保护原文件。{Environment.NewLine}{ex.Message}";
            return new RepositoryHistorySettings { SaveBlockedReason = warningMessage };
        }
    }

    public static void Save(RepositoryHistorySettings settings) => TrySave(settings, out _);

    public static bool TrySave(RepositoryHistorySettings settings, out string? errorMessage) =>
        TrySaveTo(StoragePath, settings, out errorMessage);

    internal static bool TrySaveTo(string path, RepositoryHistorySettings settings, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            if (settings.SaveBlockedReason is not null)
                throw new InvalidOperationException(settings.SaveBlockedReason);
            NormalizeSettings(settings);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            SafeConfigurationFile.Write(path, json, RepositoryHistorySettings.CurrentVersion);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static int LoadGroups(
        JsonElement rootElement,
        RepositoryDirectoryPage settings)
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
        RepositoryDirectoryPage settings,
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
                entry.Notes ??= string.Empty;
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
        settings.Pages ??= [];
        settings.LastSelectedPageId = DirectoryPageCollection.Normalize(settings.Pages, settings.LastSelectedPageId);
        foreach (var page in settings.Pages)
        {
            NormalizePage(page);
        }
    }

    private static void NormalizePage(RepositoryDirectoryPage settings)
    {
        settings.Groups ??= [];
        settings.Entries ??= [];

        var usedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedGroupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < settings.Groups.Count; index++)
        {
            var group = settings.Groups[index];
            group.Id = EnsureUniqueGuid(group.Id, usedGroupIds);
            group.Name = EnsureUniqueGroupName(group.Name, usedGroupNames);
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
            entry.Notes ??= string.Empty;
            entry.GroupId = !string.IsNullOrWhiteSpace(entry.GroupId) &&
                            knownGroupIds.Contains(entry.GroupId)
                ? entry.GroupId
                : null;
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
            return "已将旧版仓库列表完整迁移到默认页，原分组和条目保持不变。";
        }

        if (skippedEntries == 0)
        {
            return null;
        }

        var prefix = migrated
            ? "旧版仓库列表已迁移到默认页，"
            : "仓库历史配置中有";
        return $"{prefix}已跳过 {skippedEntries} 个无效项。";
    }
}
