using Microsoft.Win32;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TortoiseGitLauncher;

internal enum ExplorerAddTarget
{
    Repository,
    ExecutionDirectory
}

internal sealed record ExplorerAddRequest(ExplorerAddTarget Target, string Path)
{
    public static bool TryParse(string[] args, out ExplorerAddRequest? request)
    {
        request = null;
        if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]) || !System.IO.Path.IsPathFullyQualified(args[1])) return false;
        var target = args[0] switch
        {
            "--add-to-repositories" => ExplorerAddTarget.Repository,
            "--add-to-script-directories" => ExplorerAddTarget.ExecutionDirectory,
            _ => (ExplorerAddTarget?)null
        };
        if (target is null) return false;
        try
        {
            var path = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(args[1]));
            request = new ExplorerAddRequest(target.Value, path);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (IOException) { return false; }
    }

    public string[] ToArguments() =>
    [
        Target == ExplorerAddTarget.Repository ? "--add-to-repositories" : "--add-to-script-directories",
        Path
    ];
}

internal static class ExplorerContextMenu
{
    private const string AppKeyName = "TortoiseGitLauncher.DirectoryActions";
    private const string PreferencesPath = "Software\\TortoiseGitLauncher";
    private const string DisabledValue = "ExplorerContextMenuDisabled";

    public static bool IsDisabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(PreferencesPath);
            return Convert.ToInt32(key?.GetValue(DisabledValue, 0)) == 1;
        }
    }

    public static void Register()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            throw new InvalidOperationException("无法确定当前软件可执行文件路径。");

        try
        {
            RegisterMenu("Directory", "%1", executable);
            RegisterMenu("Directory\\Background", "%V", executable);
        }
        catch
        {
            RemoveMenuEntries();
            throw;
        }
        using (var preferences = Registry.CurrentUser.CreateSubKey(PreferencesPath, writable: true))
            preferences?.DeleteValue(DisabledValue, throwOnMissingValue: false);
        NotifyShell();
    }

    public static void Unregister()
    {
        RemoveMenuEntries();
        using (var preferences = Registry.CurrentUser.CreateSubKey(PreferencesPath, writable: true))
            preferences?.SetValue(DisabledValue, 1, RegistryValueKind.DWord);
        NotifyShell();
    }

    private static void RemoveMenuEntries()
    {
        RemoveMenu("Directory");
        RemoveMenu("Directory\\Background");
    }

    private static void RegisterMenu(string scope, string pathPlaceholder, string executable)
    {
        var basePath = $"Software\\Classes\\{scope}\\shell\\{AppKeyName}";
        using var root = Registry.CurrentUser.CreateSubKey(basePath, writable: true)
            ?? throw new InvalidOperationException("无法创建资源管理器右键菜单注册项。");
        root.SetValue("MUIVerb", "添加到 TortoiseGitLauncher");
        root.SetValue("SubCommands", string.Empty);
        root.SetValue("Icon", Quote(executable));
        root.SetValue("MultiSelectModel", "Single");
        root.SetValue("Position", "Top");

        using var repository = root.CreateSubKey("shell\\AddRepository", writable: true)
            ?? throw new InvalidOperationException("无法创建“添加到仓库管理”菜单项。");
        repository.SetValue("MUIVerb", "添加到仓库管理");
        repository.SetValue("Icon", Quote(executable));
        SetCommand(repository, executable, "--add-to-repositories", pathPlaceholder);

        using var script = root.CreateSubKey("shell\\AddScriptDirectory", writable: true)
            ?? throw new InvalidOperationException("无法创建“添加到脚本执行”菜单项。");
        script.SetValue("MUIVerb", "添加到脚本执行");
        script.SetValue("Icon", Quote(executable));
        SetCommand(script, executable, "--add-to-script-directories", pathPlaceholder);
    }

    private static void SetCommand(RegistryKey verb, string executable, string argument, string placeholder)
    {
        using var command = verb.CreateSubKey("command", writable: true)
            ?? throw new InvalidOperationException("无法创建右键菜单启动命令。");
        // Appending \. keeps a drive-root trailing slash from escaping the closing quote.
        command.SetValue(string.Empty, $"{Quote(executable)} {argument} \"{placeholder}\\.\"");
    }

    private static void RemoveMenu(string scope)
    {
        using var parent = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{scope}\\shell", writable: true);
        parent?.DeleteSubKeyTree(AppKeyName, throwOnMissingSubKey: false);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static void NotifyShell() => ShellNotifyChange(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero);

    [System.Runtime.InteropServices.DllImport("shell32.dll", EntryPoint = "SHChangeNotify")]
    private static extern void ShellNotifyChange(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}

internal sealed class SingleInstanceBroker : IDisposable
{
    private static readonly string Identity = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{Environment.UserDomainName}\\{Environment.UserName}-{System.Diagnostics.Process.GetCurrentProcess().SessionId}")))[..16];
    public static string MutexName => $"Local\\TortoiseGitLauncher-{Identity}";
    private static string PipeName => $"TortoiseGitLauncher-{Identity}";

    private readonly CancellationTokenSource _cancellation = new();
    private Task? _serverTask;
    public event Action<string[]>? RequestReceived;

    public static bool TryForward(string[] args)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None);
                pipe.Connect(250);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true);
                writer.WriteLine(JsonSerializer.Serialize(args));
                writer.Flush();
                return true;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        return false;
    }

    public void Start() => _serverTask = Task.Run(ServeAsync);

    private async Task ServeAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);
                using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024, leaveOpen: true);
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
                readTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                var line = await reader.ReadLineAsync(readTimeout.Token).ConfigureAwait(false);
                if (line is null || line.Length > 32768) continue;
                var args = JsonSerializer.Deserialize<string[]>(line);
                if (args is not null && (args.Length == 0 || ExplorerAddRequest.TryParse(args, out _)))
                    RequestReceived?.Invoke(args);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // A connected client must not block all later requests indefinitely.
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (_serverTask is null) _cancellation.Dispose();
        else _ = _serverTask.ContinueWith(_ => _cancellation.Dispose(), TaskScheduler.Default);
    }
}
