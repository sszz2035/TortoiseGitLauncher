namespace TortoiseGitLauncher;

internal enum ScriptFileType
{
    Python,
    Batch,
    Command,
    PowerShell
}

internal sealed class ScriptConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public string Name { get; set; } = string.Empty;

    public string ScriptPath { get; set; } = string.Empty;

    public ScriptFileType ScriptType { get; set; }

    public string Arguments { get; set; } = string.Empty;

    public string IconKey { get; set; } = ScriptIconCatalog.DefaultIconKey;

    public int DisplayOrder { get; set; }

    public ScriptConfiguration Clone() =>
        new()
        {
            Id = Id,
            Name = Name,
            ScriptPath = ScriptPath,
            ScriptType = ScriptType,
            Arguments = Arguments,
            IconKey = IconKey,
            DisplayOrder = DisplayOrder
        };

    public override string ToString() => Name;
}

internal sealed record ScriptIconOption(string Key, string DisplayName)
{
    public override string ToString() => DisplayName;
}

internal static class ScriptIconCatalog
{
    public const string DefaultIconKey = "default-script";

    public static IReadOnlyList<ScriptIconOption> Options { get; } =
    [
        new(DefaultIconKey, "默认脚本图标")
    ];

    public static string NormalizeKey(string? iconKey) =>
        Options.Any(option => string.Equals(option.Key, iconKey, StringComparison.Ordinal))
            ? iconKey!
            : DefaultIconKey;
}

internal static class ScriptConfigurationPathHelper
{
    public static bool TryGetScriptType(string? scriptPath, out ScriptFileType scriptType)
    {
        scriptType = default;
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return false;
        }

        switch (Path.GetExtension(scriptPath).ToLowerInvariant())
        {
            case ".py":
                scriptType = ScriptFileType.Python;
                return true;
            case ".bat":
                scriptType = ScriptFileType.Batch;
                return true;
            case ".cmd":
                scriptType = ScriptFileType.Command;
                return true;
            case ".ps1":
                scriptType = ScriptFileType.PowerShell;
                return true;
            default:
                return false;
        }
    }

    public static string GetTypeDisplayName(ScriptFileType scriptType) =>
        scriptType switch
        {
            ScriptFileType.Python => "Python (.py)",
            ScriptFileType.Batch => "批处理 (.bat)",
            ScriptFileType.Command => "命令脚本 (.cmd)",
            ScriptFileType.PowerShell => "PowerShell (.ps1)",
            _ => "未知类型"
        };
}
