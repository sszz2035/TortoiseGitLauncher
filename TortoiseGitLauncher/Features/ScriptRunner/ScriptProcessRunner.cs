using System.Diagnostics;

namespace TortoiseGitLauncher;

internal sealed class ScriptProcessRunner : IDisposable
{
    private const string CmdScriptPathEnvironmentVariable = "TGL_SCRIPT_PATH";

    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ActiveProcess> _activeProcesses = [];
    private bool _disposed;

    public ScriptRunInstance Start(
        ScriptConfiguration configuration,
        string workingDirectory)
    {
        var snapshot = configuration.Clone();
        var instance = new ScriptRunInstance(snapshot, workingDirectory);
        Process? process = null;
        var processStarted = false;

        try
        {
            ThrowIfDisposed();

            if (!File.Exists(snapshot.ScriptPath))
            {
                throw new FileNotFoundException("脚本文件不存在或暂时不可访问。", snapshot.ScriptPath);
            }

            if (!Directory.Exists(workingDirectory))
            {
                throw new DirectoryNotFoundException($"执行目录不存在或暂时不可访问：{workingDirectory}");
            }

            if (!ScriptConfigurationPathHelper.TryGetScriptType(
                    snapshot.ScriptPath,
                    out var scriptType))
            {
                throw new NotSupportedException("只支持 .py、.bat、.cmd 和 .ps1 脚本。");
            }

            snapshot.ScriptType = scriptType;
            instance.ScriptSnapshot.ScriptType = scriptType;
            var createdProcess = new Process
            {
                StartInfo = CreateStartInfo(snapshot, workingDirectory)
            };
            process = createdProcess;
            createdProcess.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    instance.AppendOutput(
                        ScriptOutputStream.StandardOutput,
                        eventArgs.Data);
                }
            };
            createdProcess.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    instance.AppendOutput(
                        ScriptOutputStream.StandardError,
                        eventArgs.Data);
                }
            };
            createdProcess.Exited += (_, _) => CompleteProcess(instance.Id, createdProcess);

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ScriptProcessRunner));
                }

                _activeProcesses.Add(
                    instance.Id,
                    new ActiveProcess(instance, createdProcess));
            }

            if (!createdProcess.Start())
            {
                throw new InvalidOperationException("系统未能创建脚本进程。");
            }

            processStarted = true;
            instance.MarkRunning(createdProcess.Id);
            createdProcess.BeginOutputReadLine();
            createdProcess.BeginErrorReadLine();
            createdProcess.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            instance.AppendOutput(
                ScriptOutputStream.StandardError,
                ex.Message);
            RemoveActiveProcess(instance.Id, process);
            if (processStarted && process is not null)
            {
                TryKillProcessTree(process);
            }

            process?.Dispose();
            if (instance.State == ScriptRunState.Starting)
            {
                instance.MarkStartFailed(ex.Message);
            }
            else
            {
                instance.MarkExecutionFailed(ex.Message);
            }
        }

        return instance;
    }

    public void Dispose()
    {
        List<ActiveProcess> activeProcesses;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            activeProcesses = _activeProcesses.Values.ToList();
            _activeProcesses.Clear();
        }

        foreach (var activeProcess in activeProcesses)
        {
            activeProcess.Instance.MarkStopped();
            TryKillProcessTree(activeProcess.Process);
            activeProcess.Process.Dispose();
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        ScriptConfiguration configuration,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        switch (configuration.ScriptType)
        {
            case ScriptFileType.Python:
                startInfo.FileName = "python";
                startInfo.Arguments = AppendRawArguments(
                    QuoteProcessArgument(configuration.ScriptPath),
                    configuration.Arguments);
                break;

            case ScriptFileType.Batch:
            case ScriptFileType.Command:
                startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                startInfo.Environment[CmdScriptPathEnvironmentVariable] = configuration.ScriptPath;
                var command = $"\"%{CmdScriptPathEnvironmentVariable}%\"";
                command = AppendRawArguments(command, configuration.Arguments);
                startInfo.Arguments = $"/d /s /v:off /c \"{command}\"";
                break;

            case ScriptFileType.PowerShell:
                startInfo.FileName = Path.Combine(
                    Environment.SystemDirectory,
                    "WindowsPowerShell",
                    "v1.0",
                    "powershell.exe");
                startInfo.Arguments = AppendRawArguments(
                    $"-NoLogo -File {QuoteProcessArgument(configuration.ScriptPath)}",
                    configuration.Arguments);
                break;

            default:
                throw new NotSupportedException($"不支持的脚本类型：{configuration.ScriptType}");
        }

        return startInfo;
    }

    private void CompleteProcess(Guid instanceId, Process process)
    {
        var activeProcess = RemoveActiveProcess(instanceId, process);
        if (activeProcess is null)
        {
            return;
        }

        try
        {
            process.WaitForExit();
            activeProcess.Instance.MarkExited(process.ExitCode);
        }
        catch (Exception ex)
        {
            activeProcess.Instance.AppendOutput(
                ScriptOutputStream.StandardError,
                ex.Message);
            activeProcess.Instance.MarkExecutionFailed(ex.Message);
        }
        finally
        {
            process.Dispose();
        }
    }

    private ActiveProcess? RemoveActiveProcess(Guid instanceId, Process? expectedProcess)
    {
        lock (_syncRoot)
        {
            if (!_activeProcesses.TryGetValue(instanceId, out var activeProcess) ||
                (expectedProcess is not null &&
                 !ReferenceEquals(activeProcess.Process, expectedProcess)))
            {
                return null;
            }

            _activeProcesses.Remove(instanceId);
            return activeProcess;
        }
    }

    private void ThrowIfDisposed()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    private static string AppendRawArguments(string command, string? arguments) =>
        string.IsNullOrEmpty(arguments)
            ? command
            : $"{command} {arguments}";

    private static string QuoteProcessArgument(string value) =>
        $"\"{value.Replace("\"", "\\\"")}\"";

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
        }
        catch
        {
        }
    }

    private sealed record ActiveProcess(
        ScriptRunInstance Instance,
        Process Process);
}
