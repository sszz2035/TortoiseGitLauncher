namespace TortoiseGitLauncher;

internal static class ScriptRunnerPathHelper
{
    public static string NormalizeDirectoryPath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
