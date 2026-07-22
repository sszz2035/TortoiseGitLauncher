using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class RepositoryListManagerForm : Form
{
    private readonly List<RepositoryEntry> _entries;
    private readonly ListBox _entriesListBox;
    private readonly TextBox _displayNameTextBox;
    private readonly TextBox _pathTextBox;
    private readonly Label _summaryLabel;

    public string? SelectedRepoRootPath { get; private set; }

    public RepositoryListManagerForm(IEnumerable<RepositoryEntry> entries, string? selectedRepoRootPath)
    {
        _entries = entries.Select(entry => entry.Clone()).ToList();
        SelectedRepoRootPath = selectedRepoRootPath;

        Text = "管理仓库列表";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 520);
        Size = new Size(860, 580);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Controls.Add(new Label
        {
            Text = "可以在这里修改下拉框显示名、调整顺序，或删除不需要的仓库项。重命名只影响显示，不会改动原始路径。",
            AutoSize = true,
            MaximumSize = new Size(800, 0),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 12, 0)
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _summaryLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        leftPanel.Controls.Add(_summaryLabel, 0, 0);

        _entriesListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        _entriesListBox.SelectedIndexChanged += (_, _) => UpdateEditorFromSelection();
        leftPanel.Controls.Add(_entriesListBox, 0, 1);

        var orderButtonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0)
        };

        var moveUpButton = CreateDialogButton("上移", Color.FromArgb(233, 242, 255));
        moveUpButton.Margin = new Padding(0, 0, 10, 0);
        moveUpButton.Click += (_, _) => MoveSelectedEntry(-1);
        orderButtonsPanel.Controls.Add(moveUpButton);

        var moveDownButton = CreateDialogButton("下移", Color.FromArgb(233, 242, 255));
        moveDownButton.Margin = new Padding(0, 0, 10, 0);
        moveDownButton.Click += (_, _) => MoveSelectedEntry(1);
        orderButtonsPanel.Controls.Add(moveDownButton);

        var removeButton = CreateDialogButton("删除该项", Color.FromArgb(249, 237, 235));
        removeButton.Click += (_, _) => RemoveSelectedEntry();
        orderButtonsPanel.Controls.Add(removeButton);

        leftPanel.Controls.Add(orderButtonsPanel, 0, 2);
        contentLayout.Controls.Add(leftPanel, 0, 0);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Margin = new Padding(12, 0, 0, 0)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        rightPanel.Controls.Add(new Label
        {
            Text = "显示名称",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 0);

        _displayNameTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8)
        };
        rightPanel.Controls.Add(_displayNameTextBox, 0, 1);

        var applyNameButton = CreateDialogButton("应用名称修改", Color.FromArgb(242, 246, 240));
        applyNameButton.Click += (_, _) => ApplyDisplayNameChanges();
        rightPanel.Controls.Add(applyNameButton, 0, 2);

        rightPanel.Controls.Add(new Label
        {
            Text = "实际路径",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 14, 0, 6)
        }, 0, 3);

        _pathTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 8)
        };
        rightPanel.Controls.Add(_pathTextBox, 0, 4);

        rightPanel.Controls.Add(new Label
        {
            Text = "显示名称留空时，下拉框会直接显示仓库路径。",
            AutoSize = true,
            ForeColor = Color.FromArgb(90, 90, 90),
            MaximumSize = new Size(420, 0)
        }, 0, 5);

        contentLayout.Controls.Add(rightPanel, 1, 0);
        rootLayout.Controls.Add(contentLayout, 0, 1);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0)
        };

        var cancelButton = CreateDialogButton("取消", Color.FromArgb(245, 245, 245));
        cancelButton.Click += (_, _) => Close();
        actionPanel.Controls.Add(cancelButton);

        var saveButton = CreateDialogButton("保存修改", Color.FromArgb(232, 241, 255));
        saveButton.Margin = new Padding(10, 0, 0, 0);
        saveButton.Click += (_, _) => SaveAndClose();
        actionPanel.Controls.Add(saveButton);

        rootLayout.Controls.Add(actionPanel, 0, 2);
        Controls.Add(rootLayout);

        RefreshEntriesList(selectedRepoRootPath);
    }

    public List<RepositoryEntry> GetEntries() =>
        _entries.Select(entry => entry.Clone()).ToList();

    private static Button CreateDialogButton(string text, Color backgroundColor)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(110, 38),
            Padding = new Padding(12, 6, 12, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = backgroundColor,
            UseCompatibleTextRendering = true
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(190, 202, 215);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private void RefreshEntriesList(string? selectedRepoRootPath)
    {
        _entriesListBox.BeginUpdate();
        _entriesListBox.Items.Clear();

        foreach (var entry in _entries)
        {
            _entriesListBox.Items.Add(entry);
        }

        _entriesListBox.EndUpdate();
        _summaryLabel.Text = $"共 {_entries.Count} 个仓库项";

        if (_entries.Count == 0)
        {
            _entriesListBox.SelectedIndex = -1;
            UpdateEditorFromSelection();
            return;
        }

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedRepoRootPath))
        {
            var normalizedPath = GitPathHelper.NormalizePath(selectedRepoRootPath);
            for (var index = 0; index < _entries.Count; index++)
            {
                if (string.Equals(_entries[index].RepoRootPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        _entriesListBox.SelectedIndex = selectedIndex;
    }

    private void UpdateEditorFromSelection()
    {
        if (_entriesListBox.SelectedItem is not RepositoryEntry entry)
        {
            _displayNameTextBox.Text = string.Empty;
            _pathTextBox.Text = string.Empty;
            return;
        }

        _displayNameTextBox.Text = entry.DisplayName;
        _pathTextBox.Text = entry.RepoRootPath;
    }

    private void ApplyDisplayNameChanges()
    {
        if (_entriesListBox.SelectedItem is not RepositoryEntry entry)
        {
            return;
        }

        var updatedName = _displayNameTextBox.Text.Trim();
        entry.DisplayName = updatedName;
        var selectedPath = entry.RepoRootPath;
        RefreshEntriesList(selectedPath);
    }
    private void MoveSelectedEntry(int offset)
    {
        if (_entriesListBox.SelectedItem is not RepositoryEntry entry)
        {
            return;
        }

        var currentIndex = _entries.IndexOf(entry);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _entries.Count)
        {
            return;
        }

        _entries.RemoveAt(currentIndex);
        _entries.Insert(targetIndex, entry);
        RefreshEntriesList(entry.RepoRootPath);
    }

    private void RemoveSelectedEntry()
    {
        if (_entriesListBox.SelectedItem is not RepositoryEntry entry)
        {
            return;
        }

        var removeIndex = _entries.IndexOf(entry);
        if (removeIndex < 0)
        {
            return;
        }

        _entries.RemoveAt(removeIndex);
        var nextSelectedPath = removeIndex < _entries.Count
            ? _entries[removeIndex].RepoRootPath
            : _entries.LastOrDefault()?.RepoRootPath;
        RefreshEntriesList(nextSelectedPath);
    }

    private void SaveAndClose()
    {
        ApplyDisplayNameChanges();
        SelectedRepoRootPath = _entriesListBox.SelectedItem is RepositoryEntry entry
            ? entry.RepoRootPath
            : _entries.FirstOrDefault()?.RepoRootPath;

        DialogResult = DialogResult.OK;
        Close();
    }
}
