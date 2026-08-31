using System.Text.Json;
using System.Text.Json.Serialization;

namespace TortoiseGitLauncher;

internal static class ScriptRunnerStore
{
    public const int MaxRecentDirectoryCount = 20;

    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TortoiseGitLauncher",
        "script-runner.json");

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static ScriptRunnerSettings Load(out string? warningMessage)
    {
        warningMessage = null;
        if (!File.Exists(StoragePath))
        {
            return new ScriptRunnerSettings();
        }

        try
        {
            var json = File.ReadAllText(StoragePath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                warningMessage = "脚本执行配置格式无效，已使用默认配置。";
                return new ScriptRunnerSettings();
            }

            var settings = new ScriptRunnerSettings();
            if (document.RootElement.TryGetProperty(nameof(ScriptRunnerSettings.LastSelectedDirectoryPath), out var selectedElement) &&
                selectedElement.ValueKind == JsonValueKind.String)
            {
                settings.LastSelectedDirectoryPath = NormalizeOptionalDirectoryPath(selectedElement.GetString());
            }

            var skippedGroups = LoadDirectoryGroups(document.RootElement, settings);
            var skippedDirectories = LoadDirectories(document.RootElement, settings);
            var skippedScripts = LoadScripts(document.RootElement, settings);
            NormalizeDirectorySettings(settings);
            warningMessage = BuildSkippedEntriesWarning(
                skippedGroups,
                skippedDirectories,
                skippedScripts);
            return settings;
        }
        catch (Exception ex)
        {
            warningMessage = $"读取脚本执行配置失败，已使用默认配置。{Environment.NewLine}{ex.Message}";
            return new ScriptRunnerSettings();
        }
    }

    public static bool TrySave(ScriptRunnerSettings settings, out string? errorMessage)
    {
        var temporaryPath = StoragePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            settings.Version = ScriptRunnerSettings.CurrentVersion;
            NormalizeDirectorySettings(settings);
            NormalizeScriptOrder(settings.Scripts);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(false));
            File.Move(temporaryPath, StoragePath, overwrite: true);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
            }

            errorMessage = ex.Message;
            return false;
        }
    }

    private static int LoadDirectoryGroups(
        JsonElement rootElement,
        ScriptRunnerSettings settings)
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

    private static int LoadDirectories(JsonElement rootElement, ScriptRunnerSettings settings)
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
            if (sourceEntries.Count >= MaxRecentDirectoryCount)
            {
                break;
            }

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
        settings.DirectoryGroups ??= [];
        settings.RecentDirectories ??= [];

        var usedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedGroupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < settings.DirectoryGroups.Count; index++)
        {
            var group = settings.DirectoryGroups[index];
            group.Id = EnsureUniqueGuid(group.Id, usedGroupIds);
            group.Name = EnsureUniqueGroupName(group.Name, usedGroupNames);
            group.DisplayOrder = index;
        }

        var knownGroupIds = settings.DirectoryGroups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedEntries = new List<ExecutionDirectoryEntry>();
        foreach (var entry in settings.RecentDirectories)
        {
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
            entry.GroupId = !string.IsNullOrWhiteSpace(entry.GroupId) &&
                            knownGroupIds.Contains(entry.GroupId)
                ? entry.GroupId
                : null;
            entry.DisplayOrder = normalizedEntries.Count;
            normalizedEntries.Add(entry);
        }

        if (normalizedEntries.Count > MaxRecentDirectoryCount)
        {
            normalizedEntries.RemoveRange(
                MaxRecentDirectoryCount,
                normalizedEntries.Count - MaxRecentDirectoryCount);
        }

        settings.RecentDirectories = normalizedEntries;
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
