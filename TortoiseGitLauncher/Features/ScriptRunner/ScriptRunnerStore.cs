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

            var skippedDirectories = LoadDirectories(document.RootElement, settings);
            var skippedScripts = LoadScripts(document.RootElement, settings);
            warningMessage = BuildSkippedEntriesWarning(skippedDirectories, skippedScripts);
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

    private static int LoadDirectories(JsonElement rootElement, ScriptRunnerSettings settings)
    {
        var skippedEntries = 0;
        if (!rootElement.TryGetProperty(nameof(ScriptRunnerSettings.RecentDirectories), out var directoriesElement) ||
            directoriesElement.ValueKind != JsonValueKind.Array)
        {
            return skippedEntries;
        }

        foreach (var element in directoriesElement.EnumerateArray())
        {
            if (settings.RecentDirectories.Count >= MaxRecentDirectoryCount)
            {
                break;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ExecutionDirectoryEntry>(element.GetRawText(), JsonOptions);
                if (entry is null || string.IsNullOrWhiteSpace(entry.DirectoryPath))
                {
                    skippedEntries++;
                    continue;
                }

                entry.DirectoryPath = ScriptRunnerPathHelper.NormalizeDirectoryPath(entry.DirectoryPath);
                entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
                if (settings.RecentDirectories.Any(existing =>
                        string.Equals(existing.DirectoryPath, entry.DirectoryPath, StringComparison.OrdinalIgnoreCase)))
                {
                    skippedEntries++;
                    continue;
                }

                settings.RecentDirectories.Add(entry);
            }
            catch
            {
                skippedEntries++;
            }
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

    private static void NormalizeScriptOrder(IList<ScriptConfiguration> scripts)
    {
        for (var index = 0; index < scripts.Count; index++)
        {
            scripts[index].DisplayOrder = index;
            scripts[index].IconKey = ScriptIconCatalog.NormalizeKey(scripts[index].IconKey);
        }
    }

    private static string? BuildSkippedEntriesWarning(int skippedDirectories, int skippedScripts)
    {
        var warnings = new List<string>();
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
