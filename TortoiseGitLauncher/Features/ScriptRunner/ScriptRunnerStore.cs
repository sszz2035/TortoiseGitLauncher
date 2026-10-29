using System.Text.Json;
using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal static class ScriptRunnerStore
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TortoiseGitLauncher",
        "script-runner.json");

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static ScriptRunnerSettings Load(out string? warningMessage) => LoadFrom(StoragePath, out warningMessage);

    internal static ScriptRunnerSettings LoadFrom(string path, out string? warningMessage)
    {
        warningMessage = null;
        if (!File.Exists(path))
        {
            return new ScriptRunnerSettings();
        }

        try
        {
            return Parse(File.ReadAllText(path), out warningMessage);
        }
        catch (Exception ex)
        {
            warningMessage = $"读取脚本执行配置失败，已阻止保存以保护原文件。{Environment.NewLine}{ex.Message}";
            return new ScriptRunnerSettings { SaveBlockedReason = warningMessage };
        }
    }

    internal static ScriptRunnerSettings Parse(string json, out string? warningMessage)
    {
        warningMessage = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("脚本执行配置格式无效。");
            var version = SafeConfigurationFile.ReadVersion(root, ScriptRunnerSettings.CurrentVersion);
            var settings = new ScriptRunnerSettings();
            var skippedGroups = 0;
            var skippedDirectories = 0;
            var migrated = false;
            if (root.TryGetProperty(nameof(ScriptRunnerSettings.Pages), out var pages))
            {
                settings.AllowInitialDirectoryFallback = false;
                if (pages.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("目录页集合格式无效。");
                settings.Pages.Clear();
                var sourceIndex = 0;
                foreach (var element in pages.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object ||
                        !element.TryGetProperty("DirectoryGroups", out var groups) || groups.ValueKind != JsonValueKind.Array ||
                        !element.TryGetProperty("RecentDirectories", out var directories) || directories.ValueKind != JsonValueKind.Array)
                    {
                        skippedDirectories++;
                        sourceIndex++;
                        continue;
                    }

                    var page = new ExecutionDirectoryPage
                    {
                        Id = SafeConfigurationFile.ReadString(element, "Id", Guid.NewGuid().ToString("D")),
                        Name = SafeConfigurationFile.ReadString(element, "Name", "未命名页"),
                        DisplayOrder = SafeConfigurationFile.ReadOrder(element, sourceIndex++),
                        LastSelectedDirectoryPath = NormalizeOptionalDirectoryPath(
                            SafeConfigurationFile.ReadString(element, "LastSelectedDirectoryPath"))
                    };
                    skippedGroups += LoadDirectoryGroups(element, page);
                    skippedDirectories += LoadDirectories(element, page);
                    settings.Pages.Add(page);
                }

                if (settings.Pages.Count == 0)
                    throw new InvalidDataException("没有可恢复的目录页，已保护原文件。");
                settings.LastSelectedPageId = SafeConfigurationFile.ReadString(root, "LastSelectedPageId");
            }
            else
            {
                if (version >= ScriptRunnerSettings.CurrentVersion)
                    throw new InvalidDataException("新版配置缺少目录页集合。");
                settings.LastSelectedDirectoryPath = NormalizeOptionalDirectoryPath(
                    SafeConfigurationFile.ReadString(root, "LastSelectedDirectoryPath"));
                skippedGroups = LoadDirectoryGroups(root, settings.ActivePage);
                skippedDirectories = LoadDirectories(root, settings.ActivePage);
                migrated = true;
            }

            var skippedScripts = LoadScripts(root, settings);
            NormalizeDirectorySettings(settings);
            warningMessage = BuildSkippedEntriesWarning(skippedGroups, skippedDirectories, skippedScripts);
            if (migrated)
                warningMessage = "已将旧版执行目录完整迁移到默认页，脚本仍全局共享。" + warningMessage;
            return settings;
        }
        catch (Exception ex)
        {
            warningMessage = $"读取脚本执行配置失败，已阻止保存以保护原文件。{Environment.NewLine}{ex.Message}";
            return new ScriptRunnerSettings { SaveBlockedReason = warningMessage };
        }
    }

    public static bool TrySave(ScriptRunnerSettings settings, out string? errorMessage) =>
        TrySaveTo(StoragePath, settings, out errorMessage);

    internal static bool TrySaveTo(string path, ScriptRunnerSettings settings, out string? errorMessage)
    {
        try
        {
            if (settings.SaveBlockedReason is not null)
                throw new InvalidOperationException(settings.SaveBlockedReason);
            NormalizeDirectorySettings(settings);
            NormalizeScriptOrder(settings.Scripts);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            SafeConfigurationFile.Write(path, json, ScriptRunnerSettings.CurrentVersion);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static int LoadDirectoryGroups(
        JsonElement rootElement,
        ExecutionDirectoryPage settings)
    {
        if (!rootElement.TryGetProperty(nameof(ScriptRunnerSettings.DirectoryGroups), out var groupsElement) ||
            groupsElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var skippedEntries = 0;
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceEntries = new List<(ExecutionDirectoryGroup Group, int SourceIndex)>();
        var sourceIndex = 0;
        foreach (var element in groupsElement.EnumerateArray())
        {
            try
            {
                var group = JsonSerializer.Deserialize<ExecutionDirectoryGroup>(
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
                if (!element.TryGetProperty(nameof(ExecutionDirectoryGroup.DisplayOrder), out _))
                {
                    group.DisplayOrder = sourceIndex;
                }

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
            settings.DirectoryGroups.Add(entry.Group);
        }

        return skippedEntries;
    }

    private static int LoadDirectories(JsonElement rootElement, ExecutionDirectoryPage settings)
    {
        var skippedEntries = 0;
        if (!rootElement.TryGetProperty(nameof(ScriptRunnerSettings.RecentDirectories), out var directoriesElement) ||
            directoriesElement.ValueKind != JsonValueKind.Array)
        {
            return skippedEntries;
        }

        var sourceEntries = new List<(ExecutionDirectoryEntry Entry, int SourceIndex)>();
        var sourceIndex = 0;
        foreach (var element in directoriesElement.EnumerateArray())
        {
            try
            {
                var entry = JsonSerializer.Deserialize<ExecutionDirectoryEntry>(
                    element.GetRawText(),
                    JsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.DirectoryPath))
                {
                    skippedEntries++;
                    sourceIndex++;
                    continue;
                }

                entry.DirectoryPath = ScriptRunnerPathHelper.NormalizeDirectoryPath(entry.DirectoryPath);
                entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
                entry.Notes ??= string.Empty;
                if (!element.TryGetProperty(nameof(ExecutionDirectoryEntry.DisplayOrder), out _))
                {
                    entry.DisplayOrder = sourceIndex;
                }

                sourceEntries.Add((entry, sourceIndex));
            }
            catch
            {
                skippedEntries++;
            }

            sourceIndex++;
        }

        var knownGroupIds = settings.DirectoryGroups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceEntry in sourceEntries
                     .OrderBy(item => item.Entry.DisplayOrder)
                     .ThenBy(item => item.SourceIndex))
        {
            var entry = sourceEntry.Entry;
            if (!uniquePaths.Add(entry.DirectoryPath))
            {
                skippedEntries++;
                continue;
            }

            entry.GroupId = !string.IsNullOrWhiteSpace(entry.GroupId) &&
                            knownGroupIds.Contains(entry.GroupId)
                ? entry.GroupId
                : null;
            settings.RecentDirectories.Add(entry);
        }

        return skippedEntries;
    }
    private static int LoadScripts(JsonElement rootElement, ScriptRunnerSettings settings)
    {
        if (!rootElement.TryGetProperty(nameof(ScriptRunnerSettings.Scripts), out var scriptsElement) ||
            scriptsElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var skippedEntries = 0;
        var loadedEntries = new List<(ScriptConfiguration Configuration, int SourceIndex)>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceIndex = 0;

        foreach (var element in scriptsElement.EnumerateArray())
        {
            try
            {
                var configuration = JsonSerializer.Deserialize<ScriptConfiguration>(element.GetRawText(), JsonOptions);
                if (configuration is null ||
                    string.IsNullOrWhiteSpace(configuration.ScriptPath) ||
                    !Path.IsPathFullyQualified(configuration.ScriptPath))
                {
                    skippedEntries++;
                    sourceIndex++;
                    continue;
                }

                configuration.ScriptPath = Path.GetFullPath(configuration.ScriptPath);
                if (!ScriptConfigurationPathHelper.TryGetScriptType(
                        configuration.ScriptPath,
                        out var scriptType))
                {
                    skippedEntries++;
                    sourceIndex++;
                    continue;
                }

                configuration.ScriptType = scriptType;
                configuration.Name = string.IsNullOrWhiteSpace(configuration.Name)
                    ? Path.GetFileNameWithoutExtension(configuration.ScriptPath)
                    : configuration.Name.Trim();
                configuration.Arguments ??= string.Empty;
                configuration.IconKey = ScriptIconCatalog.NormalizeKey(configuration.IconKey);

                if (!Guid.TryParse(configuration.Id, out _) || !usedIds.Add(configuration.Id))
                {
                    configuration.Id = Guid.NewGuid().ToString("D");
                    usedIds.Add(configuration.Id);
                }

                loadedEntries.Add((configuration, sourceIndex));
            }
            catch
            {
                skippedEntries++;
            }

            sourceIndex++;
        }

        foreach (var entry in loadedEntries
                     .OrderBy(item => item.Configuration.DisplayOrder)
                     .ThenBy(item => item.SourceIndex))
        {
            settings.Scripts.Add(entry.Configuration);
        }

        NormalizeScriptOrder(settings.Scripts);
        return skippedEntries;
    }

    private static void NormalizeDirectorySettings(ScriptRunnerSettings settings)
    {
        settings.Version = ScriptRunnerSettings.CurrentVersion;
        settings.Pages ??= [];
        settings.Scripts ??= [];
        settings.LastSelectedPageId = DirectoryPageCollection.Normalize(settings.Pages, settings.LastSelectedPageId);
        foreach (var page in settings.Pages)
        {
            NormalizeDirectoryPage(page);
        }
    }

    private static void NormalizeDirectoryPage(ExecutionDirectoryPage settings)
    {
        settings.DirectoryGroups ??= [];
        settings.RecentDirectories ??= [];

        var usedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedGroupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < settings.DirectoryGroups.Count; index++)
        {
            var group = settings.DirectoryGroups[index];
            group.Id = EnsureUniqueGuid(group.Id, usedGroupIds);
            group.Name = EnsureUniqueGroupName(group.Name, usedGroupNames);
        }

        var knownGroupIds = settings.DirectoryGroups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedEntries = new List<ExecutionDirectoryEntry>();
        var orderedSourceEntries = settings.RecentDirectories
            .Select((entry, sourceIndex) => (Entry: entry, SourceIndex: sourceIndex))
            .OrderBy(item => item.Entry?.DisplayOrder ?? int.MaxValue)
            .ThenBy(item => item.SourceIndex)
            .ToList();
        foreach (var sourceEntry in orderedSourceEntries)
        {
            var entry = sourceEntry.Entry;
            if (entry is null || string.IsNullOrWhiteSpace(entry.DirectoryPath))
            {
                continue;
            }

            try
            {
                entry.DirectoryPath = ScriptRunnerPathHelper.NormalizeDirectoryPath(entry.DirectoryPath);
            }
            catch
            {
                continue;
            }

            if (!uniquePaths.Add(entry.DirectoryPath))
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

        settings.RecentDirectories.Clear();
        settings.RecentDirectories.AddRange(normalizedEntries);
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
    private static void NormalizeScriptOrder(IList<ScriptConfiguration> scripts)
    {
        for (var index = 0; index < scripts.Count; index++)
        {
            scripts[index].DisplayOrder = index;
            scripts[index].IconKey = ScriptIconCatalog.NormalizeKey(scripts[index].IconKey);
        }
    }

    private static string? BuildSkippedEntriesWarning(
        int skippedGroups,
        int skippedDirectories,
        int skippedScripts)
    {
        var warnings = new List<string>();
        if (skippedGroups > 0)
        {
            warnings.Add($"{skippedGroups} 个无效目录组");
        }

        if (skippedDirectories > 0)
        {
            warnings.Add($"{skippedDirectories} 个无效目录项");
        }

        if (skippedScripts > 0)
        {
            warnings.Add($"{skippedScripts} 个无效脚本项");
        }

        return warnings.Count == 0
            ? null
            : $"脚本执行配置中有 {string.Join("、", warnings)}，已跳过。";
    }

    private static string NormalizeOptionalDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return ScriptRunnerPathHelper.NormalizeDirectoryPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
