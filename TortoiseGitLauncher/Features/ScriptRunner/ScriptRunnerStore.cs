using System.Text.Json;

namespace TortoiseGitLauncher;

internal static class ScriptRunnerStore
{
    public const int MaxRecentDirectoryCount = 20;

    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TortoiseGitLauncher",
        "script-runner.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

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
                settings.LastSelectedDirectoryPath = NormalizeOptionalPath(selectedElement.GetString());
            }

            var skippedEntries = 0;
            if (document.RootElement.TryGetProperty(nameof(ScriptRunnerSettings.RecentDirectories), out var directoriesElement) &&
                directoriesElement.ValueKind == JsonValueKind.Array)
            {
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
            }

            if (skippedEntries > 0)
            {
                warningMessage = $"脚本执行配置中有 {skippedEntries} 个无效目录项，已跳过。";
            }

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

    private static string NormalizeOptionalPath(string? path)
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
}
