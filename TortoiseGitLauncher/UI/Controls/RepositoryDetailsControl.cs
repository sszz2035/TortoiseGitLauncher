using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class RepositoryDetailsControl : TableLayoutPanel
{
    private readonly Label _directoryValue;
    private readonly Label _branchValue;
    private readonly Label _updatedTimeValue;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 2000 };
    private readonly TextBox _notesTextBox;
    private string? _repositoryRoot;
    private bool _isUpdating;
    private bool _refreshInProgress;
    private int _refreshVersion;

    public event EventHandler? NotesChanged;
    public event EventHandler? NotesEditCompleted;

    public string Notes => _notesTextBox.Text;

    public RepositoryDetailsControl()
    {
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        ColumnCount = 2;
        RowCount = 4;
        Margin = new Padding(0);
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var index = 0; index < RowCount; index++)
        {
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        _directoryValue = CreateValueLabel("未选择仓库根目录");
        _branchValue = CreateValueLabel("未选择仓库");
        _updatedTimeValue = CreateValueLabel("未选择仓库");
        _refreshTimer.Tick += (_, _) => _ = RefreshBranchAsync();
        _notesTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 62,
            Margin = new Padding(3, 4, 0, 0),
            BackColor = Color.White,
            ReadOnly = true
        };
        _notesTextBox.TextChanged += (_, _) =>
        {
            if (!_isUpdating)
            {
                NotesChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        _notesTextBox.Leave += (_, _) => NotesEditCompleted?.Invoke(this, EventArgs.Empty);

        AddRow("当前目标目录：", _directoryValue, 0);
        AddRow("当前分支：", _branchValue, 1);
        AddRow("最新更新时间：", _updatedTimeValue, 2);
        AddRow("备注：", _notesTextBox, 3);
        Layout += (_, _) =>
        {
            var widths = GetColumnWidths();
            var availableWidth = ClientSize.Width - widths[0] - _directoryValue.Margin.Horizontal;
            if (availableWidth > 0)
            {
                var maximumSize = new Size(availableWidth, 0);
                if (_directoryValue.MaximumSize != maximumSize)
                {
                    _directoryValue.MaximumSize = maximumSize;
                    _branchValue.MaximumSize = maximumSize;
                    _updatedTimeValue.MaximumSize = maximumSize;
                }
            }
        };
    }

    public void SetRepository(string? repositoryRoot, string? notes)
    {
        _refreshVersion++;
        _repositoryRoot = repositoryRoot;
        _directoryValue.Text = repositoryRoot ?? "未选择仓库根目录";
        _branchValue.Text = repositoryRoot is null ? "未选择仓库" : "读取中…";
        _updatedTimeValue.Text = repositoryRoot is null ? "未选择仓库" : "读取中…";
        UpdateRefreshTimer();
        _isUpdating = true;
        try
        {
            _notesTextBox.ReadOnly = repositoryRoot is null;
            _notesTextBox.Text = notes ?? string.Empty;
        }
        finally
        {
            _isUpdating = false;
        }

        _ = RefreshBranchAsync();
    }

    public async Task RefreshBranchAsync()
    {
        if (!IsHandleCreated || IsDisposed || Disposing || _repositoryRoot is null || _refreshInProgress)
        {
            return;
        }

        var repositoryRoot = _repositoryRoot;
        var version = _refreshVersion;
        _refreshInProgress = true;
        try
        {
            var branch = await Task.Run(() => RepositoryBranchReader.Read(repositoryRoot));
            var updatedTime = await Task.Run(() => RepositoryCommitReader.ReadUpdatedTimeAsync(repositoryRoot));
            var latestBranch = await Task.Run(() => RepositoryBranchReader.Read(repositoryRoot));
            if (!IsDisposed && !Disposing && IsHandleCreated && version == _refreshVersion)
            {
                // A concurrent checkout can invalidate the pair. Retry on the next poll.
                if (!string.Equals(branch, latestBranch, StringComparison.Ordinal))
                {
                    _branchValue.Text = latestBranch;
                    _updatedTimeValue.Text = "读取中…";
                    return;
                }

                _branchValue.Text = branch;
                _updatedTimeValue.Text = updatedTime;
            }
        }
        finally
        {
            _refreshInProgress = false;
            if (!IsDisposed && !Disposing && IsHandleCreated && version != _refreshVersion)
            {
                _ = RefreshBranchAsync();
            }
        }
    }

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        UpdateRefreshTimer();
        _ = RefreshBranchAsync();
    }

    protected override void OnVisibleChanged(EventArgs eventArgs)
    {
        base.OnVisibleChanged(eventArgs);
        UpdateRefreshTimer();
    }

    protected override void OnHandleDestroyed(EventArgs eventArgs)
    {
        _refreshTimer.Stop();
        _refreshVersion++;
        base.OnHandleDestroyed(eventArgs);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateRefreshTimer()
    {
        if (!IsDisposed && !Disposing)
        {
            _refreshTimer.Enabled = IsHandleCreated && Visible && _repositoryRoot is not null;
        }
    }

    private void AddRow(string title, Control value, int row)
    {
        Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(74, 82, 94),
            Margin = new Padding(0, 6, 8, 0)
        }, 0, row);
        Controls.Add(value, 1, row);
    }

    private static Label CreateValueLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        AutoSize = true,
        MaximumSize = new Size(980, 0),
        Margin = new Padding(3, 6, 0, 4),
        ForeColor = Color.FromArgb(52, 60, 72)
    };
}
