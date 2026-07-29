namespace TortoiseGitLauncher;

internal enum ScriptRunState
{
    Starting,
    Running,
    Succeeded,
    Failed,
    Stopped,
    StartFailed
}

internal enum ScriptOutputStream
{
    StandardOutput,
    StandardError
}

internal sealed record ScriptOutputLine(
    long Sequence,
    ScriptOutputStream Stream,
    string Text);

internal sealed record ScriptOutputSnapshot(
    IReadOnlyList<ScriptOutputLine> Lines,
    long Version,
    long DroppedLineCount);

internal sealed class ScriptRunInstance
{
    public const int MaxOutputLineCount = 10_000;

    private readonly Queue<ScriptOutputLine> _outputLines = new();
    private long _nextOutputSequence;
    private long _outputVersion;
    private long _droppedOutputLineCount;
    private readonly object _syncRoot = new();
    private ScriptRunState _state = ScriptRunState.Starting;
    private DateTimeOffset? _endedAt;
    private int? _exitCode;
    private int? _processId;
    private string _errorMessage = string.Empty;

    public ScriptRunInstance(ScriptConfiguration scriptSnapshot, string workingDirectory)
    {
        Id = Guid.NewGuid();
        ScriptSnapshot = scriptSnapshot.Clone();
        WorkingDirectory = workingDirectory;
        StartedAt = DateTimeOffset.Now;
    }

    public event EventHandler? StateChanged;

    public Guid Id { get; }

    public ScriptConfiguration ScriptSnapshot { get; }

    public string WorkingDirectory { get; }

    public DateTimeOffset StartedAt { get; }

    public ScriptRunState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    public DateTimeOffset? EndedAt
    {
        get
        {
            lock (_syncRoot)
            {
                return _endedAt;
            }
        }
    }

    public int? ExitCode
    {
        get
        {
            lock (_syncRoot)
            {
                return _exitCode;
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_syncRoot)
            {
                return _processId;
            }
        }
    }

    public string ErrorMessage
    {
        get
        {
            lock (_syncRoot)
            {
                return _errorMessage;
            }
        }
    }

    public bool IsRunning => State is ScriptRunState.Starting or ScriptRunState.Running;

    public long OutputVersion
    {
        get
        {
            lock (_syncRoot)
            {
                return _outputVersion;
            }
        }
    }

    public ScriptOutputSnapshot GetOutputSnapshot()
    {
        lock (_syncRoot)
        {
            return new ScriptOutputSnapshot(
                _outputLines.ToArray(),
                _outputVersion,
                _droppedOutputLineCount);
        }
    }

    internal void AppendOutput(ScriptOutputStream stream, string text)
    {
        lock (_syncRoot)
        {
            _nextOutputSequence++;
            _outputLines.Enqueue(new ScriptOutputLine(
                _nextOutputSequence,
                stream,
                text));

            while (_outputLines.Count > MaxOutputLineCount)
            {
                _outputLines.Dequeue();
                _droppedOutputLineCount++;
            }

            _outputVersion++;
        }
    }

    internal void MarkRunning(int processId)
    {
        var changed = false;
        lock (_syncRoot)
        {
            if (_state == ScriptRunState.Starting)
            {
                _processId = processId;
                _state = ScriptRunState.Running;
                changed = true;
            }
        }

        RaiseStateChanged(changed);
    }

    internal void MarkExited(int exitCode)
    {
        var changed = false;
        lock (_syncRoot)
        {
            if (_state is ScriptRunState.Starting or ScriptRunState.Running)
            {
                _exitCode = exitCode;
                _endedAt = DateTimeOffset.Now;
                _state = exitCode == 0
                    ? ScriptRunState.Succeeded
                    : ScriptRunState.Failed;
                changed = true;
            }
        }

        RaiseStateChanged(changed);
    }

    internal void MarkStartFailed(string errorMessage)
    {
        var changed = false;
        lock (_syncRoot)
        {
            if (_state == ScriptRunState.Starting)
            {
                _errorMessage = errorMessage;
                _endedAt = DateTimeOffset.Now;
                _state = ScriptRunState.StartFailed;
                changed = true;
            }
        }

        RaiseStateChanged(changed);
    }

    internal void MarkExecutionFailed(string errorMessage)
    {
        var changed = false;
        lock (_syncRoot)
        {
            if (_state is ScriptRunState.Starting or ScriptRunState.Running)
            {
                _errorMessage = errorMessage;
                _endedAt = DateTimeOffset.Now;
                _state = ScriptRunState.Failed;
                changed = true;
            }
        }

        RaiseStateChanged(changed);
    }

    internal void MarkStopped()
    {
        var changed = false;
        lock (_syncRoot)
        {
            if (_state is ScriptRunState.Starting or ScriptRunState.Running)
            {
                _endedAt = DateTimeOffset.Now;
                _state = ScriptRunState.Stopped;
                changed = true;
            }
        }

        RaiseStateChanged(changed);
    }

    public static string GetStateDisplayName(ScriptRunState state) =>
        state switch
        {
            ScriptRunState.Starting => "正在启动",
            ScriptRunState.Running => "运行中",
            ScriptRunState.Succeeded => "成功",
            ScriptRunState.Failed => "失败",
            ScriptRunState.Stopped => "已停止",
            ScriptRunState.StartFailed => "启动失败",
            _ => "未知"
        };

    private void RaiseStateChanged(bool changed)
    {
        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
