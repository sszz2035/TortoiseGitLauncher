using System.Text;
using System.Text.Json;

namespace TortoiseGitLauncher;

internal static class SafeConfigurationFile
{
    public static int ReadVersion(JsonElement root, int supportedVersion)
    {
        var version = 0;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Version", out var element))
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out version) || version < 0)
            {
                throw new InvalidDataException("配置版本号无效，已阻止覆盖原文件。");
            }
        }

        if (version > supportedVersion)
        {
            throw new NotSupportedException($"配置版本 {version} 高于本程序支持的 {supportedVersion}，已阻止覆盖原文件。");
        }

        return version;
    }

    public static string ReadString(JsonElement root, string propertyName, string fallback = "") =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? fallback
            : fallback;

    public static int ReadOrder(JsonElement root, int fallback) =>
        root.TryGetProperty("DisplayOrder", out var element) && element.ValueKind == JsonValueKind.Number &&
        element.TryGetInt32(out var order) ? order : fallback;

    public static void Write(string path, string json, int supportedVersion)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (File.Exists(path))
            {
                // Recheck at save time, including when a newer program changed the file.
                using var original = JsonDocument.Parse(File.ReadAllText(path));
                var version = ReadVersion(original.RootElement, supportedVersion);
                if (version < supportedVersion)
                {
                    var migrationBackup = path + $".v{version}.bak";
                    if (!File.Exists(migrationBackup))
                    {
                        File.Copy(path, migrationBackup, overwrite: false);
                    }
                }
            }

            var bytes = new UTF8Encoding(false, true).GetBytes(json);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, path + ".bak");
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
