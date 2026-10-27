using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TortoiseGitLauncher;

internal static class RepositoryCommitReader
{
    public static async Task<string> ReadUpdatedTimeAsync(string repositoryRoot)
    {
        try
        {
            if (!Directory.Exists(repositoryRoot))
            {
                return "仓库目录不存在或不可访问";
            }

            var gitPath = Path.Combine(repositoryRoot, ".git");
            if (!Directory.Exists(gitPath) && !File.Exists(gitPath))
            {
                return "未检测到 Git 仓库";
            }

            var executable = FindGitExecutable();
            if (executable is null)
            {
                return "未找到 Git，无法读取提交时间";
            }

            // Resolve HEAD first so the timestamp remains tied to this exact commit,
            // even if another tool switches the branch while the query is running.
            var head = await QueryAsync(executable, repositoryRoot, "rev-parse", "--verify", "--quiet", "HEAD");
            if (head.ExitCode == 1)
            {
                return "暂无提交";
            }

            var commit = head.Output.Trim();
            if (head.ExitCode != 0 || (commit.Length != 40 && commit.Length != 64) ||
                !commit.All(Uri.IsHexDigit))
            {
                return "无法读取最新提交时间";
            }

            var result = await QueryAsync(executable, repositoryRoot, "show", "--no-patch", "--format=%cI", commit, "--");
            return result.ExitCode == 0 && DateTimeOffset.TryParse(
                result.Output.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
                ? timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "无法读取最新提交时间";
        }
        catch (OperationCanceledException)
        {
            return "读取提交时间超时";
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or
                                   ArgumentException or InvalidOperationException or NotSupportedException or
                                   System.Security.SecurityException)
        {
            return "无法读取最新提交时间";
        }
    }

    private static async Task<(int ExitCode, string Output)> QueryAsync(
        string executable, string repositoryRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("--no-pager");
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryRoot);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // These are read-only queries. Never prompt, refresh the index or acquire optional locks.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            return (process.ExitCode, await output.ConfigureAwait(false));
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
    }

    private static string? FindGitExecutable()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Trim('"'))
            .ToList();
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                directories.Add(Path.Combine(root, "Git", "cmd"));
                directories.Add(Path.Combine(root, "Git", "bin"));
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            directories.Add(Path.Combine(localAppData, "Programs", "Git", "cmd"));
        }

        return directories.Select(directory => Path.Combine(directory, "git.exe")).FirstOrDefault(File.Exists);
    }
}
