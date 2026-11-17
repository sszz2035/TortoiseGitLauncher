using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class ScriptRunSessionsControl : UserControl
{
    private readonly Action<string, bool> _notifyStatus;
    private readonly ScriptProcessRunner _scriptProcessRunner = new();
    private readonly List<ScriptRunInstance> _runInstances = [];
    private Label _runInstancesSummaryLabel = null!;
    private Button _clearCompletedRunInstancesButton = null!;
    private TabControl _runInstancesTabControl = null!;
    private readonly Dictionary<Guid, RunInstanceTabView> _runInstanceTabViews = [];
    private readonly System.Windows.Forms.Timer _runUiRefreshTimer = new() { Interval = 250 };
    private Guid? _renderedOutputInstanceId;
    private long _renderedOutputVersion = -1;
    private long _renderedOutputFirstSequence;
    private long _renderedOutputLastSequence;

    internal ScriptRunSessionsControl(Action<string, bool> notifyStatus)
    {
        _notifyStatus = notifyStatus;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top;
        BackColor = Color.Transparent;
        Margin = new Padding(0);
        Controls.Add(CreateRunInstancesCard());
        _runUiRefreshTimer.Tick += (_, _) => RefreshSelectedRunInstanceOutput(force: false);
        _runUiRefreshTimer.Start();
    }

    internal int RunningInstanceCount => _runInstances.Count(instance => instance.IsRunning);

    internal ScriptRunInstance Start(ScriptConfiguration configuration, string workingDirectory)
    {
        var instance = _scriptProcessRunner.Start(configuration, workingDirectory);
        _runInstances.Insert(0, instance);
        instance.StateChanged += OnRunInstanceStateChanged;
        RefreshRunInstanceTabs(instance.Id);
        return instance;
    }

    internal void StopAllRunningInstances() => _scriptProcessRunner.StopAll();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var instance in _runInstances)
            {
                instance.StateChanged -= OnRunInstanceStateChanged;
            }

            foreach (var view in _runInstanceTabViews.Values)
            {
                DisposeRunInstanceButtonImages(view);
            }

            _clearCompletedRunInstancesButton.Image?.Dispose();
            _clearCompletedRunInstancesButton.Image = null;
            _runInstanceTabViews.Clear();
            _runUiRefreshTimer.Stop();
            _runUiRefreshTimer.Dispose();
            _scriptProcessRunner.Dispose();
        }

        base.Dispose(disposing);
    }
    private Control CreateRunInstancesCard()
    {
        var card = ScriptRunnerPage.CreateCard(new Padding(20, 18, 20, 18));
        card.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        layout.Controls.Add(ScriptRunnerPage.CreateSectionTitle(
            "运行实例与输出",
            UiIconKind.RepoStatus,
            Color.FromArgb(168, 85, 247)), 0, 0);

        var instancesToolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        instancesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        instancesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _runInstancesSummaryLabel = new Label
        {
            Text = "暂无运行实例",
            AutoSize = true,
            ForeColor = Color.FromArgb(82, 91, 104),
            Margin = new Padding(0, 7, 12, 0)
        };
        instancesToolbar.Controls.Add(_runInstancesSummaryLabel, 0, 0);

        _clearCompletedRunInstancesButton = CreateRunInstanceCommandButton(
            "清除已结束",
            UiIconKind.Cleanup,
            Color.FromArgb(88, 96, 108));
        _clearCompletedRunInstancesButton.Click += (_, _) =>
            ClearCompletedRunInstances();
        instancesToolbar.Controls.Add(_clearCompletedRunInstancesButton, 1, 0);
        layout.Controls.Add(instancesToolbar, 0, 1);

        _runInstancesTabControl = new TabControl
        {
            Dock = DockStyle.Top,
            Height = 360,
            Alignment = TabAlignment.Bottom,
            HotTrack = true,
            Multiline = false,
            ShowToolTips = true,
            SizeMode = TabSizeMode.Normal,
            Padding = new Point(12, 5),
            Margin = new Padding(0)
        };
        _runInstancesTabControl.SelectedIndexChanged += (_, _) =>
        {
            ResetRenderedOutputState();
            RefreshSelectedRunInstanceOutput(force: true);
        };
        layout.Controls.Add(_runInstancesTabControl, 0, 2);

        card.Controls.Add(layout);
        RefreshRunInstanceTabs();
        RefreshSelectedRunInstanceOutput(force: true);
        return card;
    }

    private void RefreshRunInstanceTabs(Guid? selectedInstanceId = null)
    {
        if (IsDisposed || Disposing ||
            _runInstancesSummaryLabel is null || _runInstancesTabControl is null)
        {
            return;
        }

        var previousId =
            _runInstancesTabControl.SelectedTab?.Tag is ScriptRunInstance selected
                ? selected.Id
                : (Guid?)null;

        for (var index = 0; index < _runInstances.Count; index++)
        {
            var instance = _runInstances[index];
            if (!_runInstanceTabViews.TryGetValue(instance.Id, out var view))
            {
                view = CreateRunInstanceTabView(instance);
                _runInstanceTabViews.Add(instance.Id, view);
                _runInstancesTabControl.TabPages.Insert(
                    Math.Min(index, _runInstancesTabControl.TabPages.Count),
                    view.TabPage);
            }

            UpdateRunInstanceTab(instance, view);
        }

        var runningCount = _runInstances.Count(instance => instance.IsRunning);
        _runInstancesSummaryLabel.Text = _runInstances.Count == 0
            ? "暂无运行实例"
            : $"共 {_runInstances.Count} 个实例，{runningCount} 个运行中";
        _clearCompletedRunInstancesButton.Enabled =
            _runInstances.Any(instance => !instance.IsRunning);

        var targetId = selectedInstanceId ?? previousId;
        if (targetId.HasValue &&
            _runInstanceTabViews.TryGetValue(targetId.Value, out var targetView))
        {
            _runInstancesTabControl.SelectedTab = targetView.TabPage;
        }
        else if (_runInstancesTabControl.SelectedTab is null &&
                 _runInstancesTabControl.TabPages.Count > 0)
        {
            _runInstancesTabControl.SelectedIndex = 0;
        }
    }

    private RunInstanceTabView CreateRunInstanceTabView(ScriptRunInstance instance)
    {
        var tabPage = new TabPage
        {
            Tag = instance,
            BackColor = Color.White,
            UseVisualStyleBackColor = false,
            Padding = new Padding(10)
        };
        var pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = new Padding(0)
        };
        pageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var detailsLabel = new Label
        {
            Text = "正在读取运行实例信息",
            AutoSize = true,
            MaximumSize = new Size(1080, 0),
            ForeColor = Color.FromArgb(82, 91, 104),
            Margin = new Padding(0, 3, 12, 0)
        };
        detailsLayout.Controls.Add(detailsLabel, 0, 0);

        var stopButton = new Button
        {
            Text = "终止",
            Width = 104,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 245, 245),
            ForeColor = Color.FromArgb(174, 38, 38),
            Font = new Font(
                "Microsoft YaHei UI",
                9F,
                FontStyle.Regular,
                GraphicsUnit.Point),
            Image = IconFactory.Create(
                UiIconKind.Stop,
                Color.FromArgb(185, 40, 40),
                15),
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = true,
            Margin = new Padding(0)
        };
        stopButton.FlatAppearance.BorderColor = Color.FromArgb(239, 190, 190);
        stopButton.Click += (_, _) => StopRunInstance(instance);
        detailsLayout.Controls.Add(stopButton, 1, 0);

        var removeButton = CreateRunInstanceCommandButton(
            "移除",
            UiIconKind.Resolve,
            Color.FromArgb(82, 91, 104));
        removeButton.Width = 92;
        removeButton.Margin = new Padding(8, 0, 0, 0);
        removeButton.Click += (_, _) => RemoveRunInstance(instance);
        detailsLayout.Controls.Add(removeButton, 2, 0);
        pageLayout.Controls.Add(detailsLayout, 0, 0);

        var outputTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(35, 42, 52),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0)
        };
        pageLayout.Controls.Add(outputTextBox, 0, 1);
        tabPage.Controls.Add(pageLayout);

        return new RunInstanceTabView(
            tabPage,
            detailsLabel,
            stopButton,
            removeButton,
            outputTextBox);
    }

    private static void UpdateRunInstanceTab(
        ScriptRunInstance instance,
        RunInstanceTabView view)
    {
        view.TabPage.Text = FormatRunInstanceTab(instance);
        view.TabPage.ToolTipText = FormatRunInstance(instance);
        view.StopButton.Enabled = instance.CanStop;
        view.StopButton.Text = instance.State == ScriptRunState.Stopping
            ? "正在终止"
            : "终止";
        view.RemoveButton.Enabled = !instance.IsRunning;
    }

    private void RefreshSelectedRunInstanceOutput(bool force)
    {
        if (IsDisposed || Disposing ||
            !TryGetSelectedRunInstanceView(out var instance, out var view))
        {
            ResetRenderedOutputState();
            return;
        }

        UpdateSelectedRunInstanceDetails(instance, view);
        var outputVersion = instance.OutputVersion;
        if (!force &&
            _renderedOutputInstanceId == instance.Id &&
            _renderedOutputVersion == outputVersion)
        {
            return;
        }

        var outputTextBox = view.OutputTextBox;
        var snapshot = instance.GetOutputSnapshot();
        var lines = snapshot.Lines;
        var firstSequence = lines.Count > 0 ? lines[0].Sequence : 0;
        var selectedInstanceChanged = _renderedOutputInstanceId != instance.Id;
        var bufferWasTrimmed =
            _renderedOutputFirstSequence > 0 &&
            firstSequence > _renderedOutputFirstSequence;
        var rebuildOutput = force || selectedInstanceChanged || bufferWasTrimmed;

        var shouldScrollToEnd = force ||
                                outputTextBox.TextLength == 0 ||
                                outputTextBox.SelectionStart >=
                                Math.Max(0, outputTextBox.TextLength - 1);
        var previousSelectionStart = outputTextBox.SelectionStart;
        var previousSelectionLength = outputTextBox.SelectionLength;

        if (lines.Count == 0)
        {
            if (rebuildOutput || outputTextBox.TextLength > 0)
            {
                outputTextBox.Clear();
            }

            _renderedOutputFirstSequence = 0;
            _renderedOutputLastSequence = 0;
        }
        else if (rebuildOutput)
        {
            outputTextBox.Text = BuildOutputText(snapshot);
            _renderedOutputFirstSequence = firstSequence;
            _renderedOutputLastSequence = lines[^1].Sequence;
        }
        else
        {
            var appendedLines = lines
                .Where(line => line.Sequence > _renderedOutputLastSequence)
                .ToArray();
            if (appendedLines.Length > 0)
            {
                outputTextBox.AppendText(BuildOutputText(appendedLines));
                _renderedOutputLastSequence = appendedLines[^1].Sequence;
            }

            if (_renderedOutputFirstSequence == 0)
            {
                _renderedOutputFirstSequence = firstSequence;
            }
        }

        _renderedOutputInstanceId = instance.Id;
        _renderedOutputVersion = snapshot.Version;
        if (shouldScrollToEnd)
        {
            outputTextBox.SelectionStart = outputTextBox.TextLength;
            outputTextBox.SelectionLength = 0;
            outputTextBox.ScrollToCaret();
        }
        else
        {
            outputTextBox.SelectionStart = Math.Min(
                previousSelectionStart,
                outputTextBox.TextLength);
            outputTextBox.SelectionLength = Math.Min(
                previousSelectionLength,
                outputTextBox.TextLength - outputTextBox.SelectionStart);
        }
    }

    private static void UpdateSelectedRunInstanceDetails(
        ScriptRunInstance instance,
        RunInstanceTabView view)
    {
        var state = instance.State;
        var endedAt = instance.EndedAt;
        var elapsed = (endedAt ?? DateTimeOffset.Now) - instance.StartedAt;
        var endText = endedAt.HasValue
            ? endedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : "运行中";
        var exitCodeText = instance.ExitCode.HasValue
            ? instance.ExitCode.Value.ToString()
            : "--";

        var details = new StringBuilder()
            .Append(instance.ScriptSnapshot.Name)
            .Append("    状态：")
            .Append(ScriptRunInstance.GetStateDisplayName(state))
            .Append("    开始：")
            .Append(instance.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append("    结束：")
            .Append(endText)
            .Append("    用时：")
            .Append(FormatDuration(elapsed))
            .Append("    退出码：")
            .Append(exitCodeText)
            .AppendLine()
            .Append("执行目录：")
            .Append(instance.WorkingDirectory);

        if (!string.IsNullOrWhiteSpace(instance.ErrorMessage))
        {
            details
                .AppendLine()
                .Append("错误：")
                .Append(instance.ErrorMessage);
        }

        view.DetailsLabel.Text = details.ToString();
        view.DetailsLabel.ForeColor = state switch
        {
            ScriptRunState.Running or
            ScriptRunState.Starting or
            ScriptRunState.Stopping => Color.FromArgb(92, 71, 165),
            ScriptRunState.Succeeded => Color.FromArgb(23, 112, 41),
            ScriptRunState.Failed or ScriptRunState.StartFailed => Color.FromArgb(180, 45, 30),
            ScriptRunState.Stopped => Color.FromArgb(120, 88, 30),
            _ => Color.FromArgb(82, 91, 104)
        };
        view.StopButton.Enabled = instance.CanStop;
        view.StopButton.Text = state == ScriptRunState.Stopping
            ? "正在终止"
            : "终止";
        view.RemoveButton.Enabled = !instance.IsRunning;
    }

    private void StopRunInstance(ScriptRunInstance instance)
    {
        if (!instance.CanStop)
        {
            return;
        }

        if (!_scriptProcessRunner.TryStop(instance.Id, out var errorMessage))
        {
            MessageBox.Show(
                this,
                errorMessage,
                "无法终止脚本",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            RefreshRunInstanceUi();
            return;
        }

        NotifyStatus(
            $"正在终止脚本“{instance.ScriptSnapshot.Name}”及其子进程。",
            isError: false);
        RefreshRunInstanceUi();
    }

    private void RemoveRunInstance(ScriptRunInstance instance)
    {
        if (instance.IsRunning)
        {
            return;
        }

        RemoveRunInstanceCore(instance);
        ResetRenderedOutputState();
        RefreshRunInstanceUi();
        NotifyStatus(
            $"已移除运行实例“{instance.ScriptSnapshot.Name}”。",
            isError: false);
    }

    private void ClearCompletedRunInstances()
    {
        var completedInstances = _runInstances
            .Where(instance => !instance.IsRunning)
            .ToArray();
        if (completedInstances.Length == 0)
        {
            return;
        }

        foreach (var instance in completedInstances)
        {
            RemoveRunInstanceCore(instance);
        }

        ResetRenderedOutputState();
        RefreshRunInstanceUi();
        NotifyStatus(
            $"已清除 {completedInstances.Length} 个已结束运行实例。",
            isError: false);
    }

    private void RemoveRunInstanceCore(ScriptRunInstance instance)
    {
        instance.StateChanged -= OnRunInstanceStateChanged;
        _runInstances.Remove(instance);
        if (!_runInstanceTabViews.Remove(instance.Id, out var view))
        {
            return;
        }

        _runInstancesTabControl.TabPages.Remove(view.TabPage);
        DisposeRunInstanceButtonImages(view);
        view.TabPage.Dispose();
    }

    private static void DisposeRunInstanceButtonImages(RunInstanceTabView view)
    {
        view.StopButton.Image?.Dispose();
        view.StopButton.Image = null;
        view.RemoveButton.Image?.Dispose();
        view.RemoveButton.Image = null;
    }

    private bool TryGetSelectedRunInstanceView(
        out ScriptRunInstance instance,
        out RunInstanceTabView view)
    {
        instance = null!;
        view = null!;
        if (_runInstancesTabControl.SelectedTab?.Tag is not ScriptRunInstance selected ||
            !_runInstanceTabViews.TryGetValue(selected.Id, out var selectedView))
        {
            return false;
        }

        instance = selected;
        view = selectedView;
        return true;
    }

    private void OnRunInstanceStateChanged(object? sender, EventArgs eventArgs)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke((Action)RefreshRunInstanceUi);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        RefreshRunInstanceUi();
    }

    private void RefreshRunInstanceUi()
    {
        RefreshRunInstanceTabs();
        RefreshSelectedRunInstanceOutput(force: false);
    }

    private void ResetRenderedOutputState()
    {
        _renderedOutputInstanceId = null;
        _renderedOutputVersion = -1;
        _renderedOutputFirstSequence = 0;
        _renderedOutputLastSequence = 0;
    }

    private static string BuildOutputText(ScriptOutputSnapshot snapshot)
    {
        var builder = new StringBuilder();
        if (snapshot.DroppedLineCount > 0)
        {
            builder
                .Append("[已省略较早的 ")
                .Append(snapshot.DroppedLineCount)
                .AppendLine(" 行输出]");
        }

        AppendOutputLines(builder, snapshot.Lines);
        return builder.ToString();
    }

    private static string BuildOutputText(IEnumerable<ScriptOutputLine> lines)
    {
        var builder = new StringBuilder();
        AppendOutputLines(builder, lines);
        return builder.ToString();
    }

    private static void AppendOutputLines(
        StringBuilder builder,
        IEnumerable<ScriptOutputLine> lines)
    {
        foreach (var line in lines)
        {
            if (line.Stream == ScriptOutputStream.StandardError)
            {
                builder.Append("[stderr] ");
            }

            builder.AppendLine(line.Text);
        }
    }

    private static string FormatRunInstance(ScriptRunInstance instance)
    {
        var stateText = ScriptRunInstance.GetStateDisplayName(instance.State);
        var processText = instance.State switch
        {
            ScriptRunState.Running or ScriptRunState.Stopping
                when instance.ProcessId.HasValue => $"PID {instance.ProcessId.Value}",
            ScriptRunState.Succeeded or
            ScriptRunState.Failed or
            ScriptRunState.Stopped
                when instance.ExitCode.HasValue => $"退出码 {instance.ExitCode.Value}",
            ScriptRunState.StartFailed when !string.IsNullOrWhiteSpace(instance.ErrorMessage) =>
                instance.ErrorMessage,
            _ => string.Empty
        };
        var stateDetail = string.IsNullOrWhiteSpace(processText)
            ? stateText
            : $"{stateText}，{processText}";

        return $"{instance.StartedAt:HH:mm:ss}  {instance.ScriptSnapshot.Name}  {stateDetail}  [{instance.WorkingDirectory}]";
    }

    private static string FormatRunInstanceTab(ScriptRunInstance instance) =>
        $"{instance.ScriptSnapshot.Name} · " +
        $"{ScriptRunInstance.GetStateDisplayName(instance.State)} · " +
        $"{instance.StartedAt:HH:mm:ss}";

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static Button CreateRunInstanceCommandButton(
        string text,
        UiIconKind iconKind,
        Color iconColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 126,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(64, 72, 84),
            Font = new Font(
                "Microsoft YaHei UI",
                9F,
                FontStyle.Regular,
                GraphicsUnit.Point),
            Image = IconFactory.Create(iconKind, iconColor, 15),
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = true,
            Margin = new Padding(0)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(214, 223, 236);
        return button;
    }

    private sealed record RunInstanceTabView(
        TabPage TabPage,
        Label DetailsLabel,
        Button StopButton,
        Button RemoveButton,
        RichTextBox OutputTextBox);

    private void NotifyStatus(string message, bool isError) => _notifyStatus(message, isError);
}