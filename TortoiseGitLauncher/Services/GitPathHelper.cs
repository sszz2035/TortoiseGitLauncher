namespace TortoiseGitLauncher;

internal static class GitPathHelper
{
    public static string? FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);

        while (current is not null)
        {
            var dotGitDirectory = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(dotGitDirectory) || File.Exists(dotGitDirectory))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    public static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
