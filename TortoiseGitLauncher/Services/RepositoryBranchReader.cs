namespace TortoiseGitLauncher;

internal static class RepositoryBranchReader
{
    // HEAD belongs to the worktree, even when objects and refs are shared.
    public static string Read(string repositoryRoot)
    {
        try
        {
            if (!Directory.Exists(repositoryRoot))
            {
                return "仓库目录不存在或不可访问";
            }

            var gitDirectory = Path.Combine(repositoryRoot, ".git");
            if (File.Exists(gitDirectory))
            {
                var gitFile = File.ReadAllText(gitDirectory).Trim();
                const string prefix = "gitdir:";
                if (!gitFile.StartsWith(prefix, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(gitFile[prefix.Length..]))
                {
                    return "无法读取 Git 目录";
                }

                gitDirectory = Path.GetFullPath(gitFile[prefix.Length..].Trim(), repositoryRoot);
            }
            else if (!Directory.Exists(gitDirectory))
            {
                return "未检测到 Git 仓库";
            }

            var head = File.ReadAllText(Path.Combine(gitDirectory, "HEAD")).Trim();
            const string branchPrefix = "ref: refs/heads/";
            if (head.StartsWith(branchPrefix, StringComparison.Ordinal) &&
                head.Length > branchPrefix.Length)
            {
                // The name also remains available in a repository without its first commit.
                return head[branchPrefix.Length..];
            }

            if ((head.Length == 40 || head.Length == 64) && head.All(Uri.IsHexDigit))
            {
                return $"游离 HEAD · {head[..8]}";
            }

            return "无法识别 HEAD 状态";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return "无法读取当前分支";
        }
    }
}
