using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class ExecutionDirectoryManagerForm : Form
{
    private readonly List<ExecutionDirectoryEntry> _entries;
    private readonly List<ExecutionDirectoryGroup> _groups;
    private readonly ListBox _entriesListBox;
    private readonly TextBox _displayNameTextBox;
    private readonly TextBox _groupNameTextBox;
    private readonly TextBox _pathTextBox;
    private readonly Label _pathStatusLabel;
    private readonly Label _summaryLabel;
    private bool _isRefreshing;
    private bool _isUpdatingSelection;
    private Point _dragStartPoint;
    private List<ManagerRow>? _dragCandidateRows;

    public string? SelectedDirectoryPath { get; private set; }

    public ExecutionDirectoryManagerForm(
        IEnumerable<ExecutionDirectoryEntry> entries,
        IEnumerable<ExecutionDirectoryGroup> groups,
        string? selectedDirectoryPath)
    {
        _entries = entries.Select(entry => entry.Clone()).ToList();
        _groups = groups.Select(group => group.Clone()).ToList();
        SelectedDirectoryPath = selectedDirectoryPath;

        Text = "管理执行目录";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 600);
        Size = new Size(1040, 700);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var rootLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Controls.Add(new Label
        {
            Text = "可创建分组并整理执行目录。先按 Ctrl 或 Shift 选中普通项，再点击“新建分组”可自动归入新组。",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);

        var contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));

        var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0, 0, 12, 0) };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _summaryLabel = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        leftPanel.Controls.Add(_summaryLabel, 0, 0);

        _entriesListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            SelectionMode = SelectionMode.MultiExtended,
            Margin = new Padding(0, 0, 0, 10)
        };
        _entriesListBox.SelectedIndexChanged += (_, _) => EnforceSelectionType();
        _entriesListBox.SelectedIndexChanged += (_, _) => UpdateEditorFromSelection();
        _entriesListBox.MouseDown += (_, eventArgs) =>
        {
            _dragStartPoint = eventArgs.Location;
            var index = _entriesListBox.IndexFromPoint(eventArgs.Location);
            _dragCandidateRows = index >= 0 && _entriesListBox.SelectedIndices.Contains(index)
                ? _entriesListBox.SelectedItems.Cast<ManagerRow>().ToList()
                : null;
        };
        _entriesListBox.MouseMove += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Left ||
                (Math.Abs(eventArgs.X - _dragStartPoint.X) < SystemInformation.DragSize.Width / 2 &&
                 Math.Abs(eventArgs.Y - _dragStartPoint.Y) < SystemInformation.DragSize.Height / 2))
            {
                return;
            }

            var dragIndex = _entriesListBox.IndexFromPoint(eventArgs.Location);
            if (dragIndex < 0)
            {
                return;
            }

            if (_dragCandidateRows is { Count: > 0 })
            {
                var candidateRows = _dragCandidateRows;
                _dragCandidateRows = null;
                _entriesListBox.ClearSelected();
                foreach (var candidateRow in candidateRows)
                {
                    var candidateIndex = _entriesListBox.Items.IndexOf(candidateRow);
                    if (candidateIndex >= 0) _entriesListBox.SetSelected(candidateIndex, true);
                }
            }

            BeginDrag(_entriesListBox.Items[dragIndex]);
        };        _entriesListBox.DragEnter += (_, eventArgs) => UpdateDragEffect(eventArgs);
        _entriesListBox.DragOver += (_, eventArgs) => UpdateDragEffect(eventArgs);
        _entriesListBox.DragDrop += (_, eventArgs) => CompleteDrag(eventArgs);
        _entriesListBox.DoubleClick += (_, _) => ToggleSelectedGroup();
        leftPanel.Controls.Add(_entriesListBox, 0, 1);

        var groupButtonsPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        var createGroupButton = CreateDialogButton("新建分组", Color.FromArgb(242, 246, 240));
        createGroupButton.Click += (_, _) => CreateGroup();
        groupButtonsPanel.Controls.Add(createGroupButton);
        leftPanel.Controls.Add(groupButtonsPanel, 0, 2);

        var operationPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
        var moveUpButton = CreateDialogButton("上移", Color.FromArgb(233, 242, 255));
        moveUpButton.Click += (_, _) => MoveSelectedItems(-1);
        operationPanel.Controls.Add(moveUpButton);
        var moveDownButton = CreateDialogButton("下移", Color.FromArgb(233, 242, 255));
        moveDownButton.Click += (_, _) => MoveSelectedItems(1);
        operationPanel.Controls.Add(moveDownButton);
        var deleteButton = CreateDialogButton("删除选中", Color.FromArgb(249, 237, 235));
        deleteButton.Click += (_, _) => DeleteSelectedItems();
        operationPanel.Controls.Add(deleteButton);
        leftPanel.Controls.Add(operationPanel, 0, 3);
        contentLayout.Controls.Add(leftPanel, 0, 0);

        var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Margin = new Padding(12, 0, 0, 0) };
        for (var index = 0; index < 8; index++) rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rightPanel.Controls.Add(new Label { Text = "目录显示名称", AutoSize = true, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) }, 0, 0);
        _displayNameTextBox = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        rightPanel.Controls.Add(_displayNameTextBox, 0, 1);
        var applyNameButton = CreateDialogButton("应用名称修改", Color.FromArgb(242, 246, 240));
        applyNameButton.Click += (_, _) => ApplyDisplayNameChanges();
        rightPanel.Controls.Add(applyNameButton, 0, 2);
        rightPanel.Controls.Add(new Label { Text = "分组名称", AutoSize = true, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), Margin = new Padding(0, 14, 0, 6) }, 0, 3);
        _groupNameTextBox = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        rightPanel.Controls.Add(_groupNameTextBox, 0, 4);
        var applyGroupButton = CreateDialogButton("应用分组名称", Color.FromArgb(242, 246, 240));
        applyGroupButton.Click += (_, _) => RenameSelectedGroup();
        rightPanel.Controls.Add(applyGroupButton, 0, 5);
        rightPanel.Controls.Add(new Label { Text = "实际路径", AutoSize = true, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), Margin = new Padding(0, 14, 0, 6) }, 0, 6);
        _pathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
        rightPanel.Controls.Add(_pathTextBox, 0, 7);
        _pathStatusLabel = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        rightPanel.Controls.Add(_pathStatusLabel, 0, 8);
        contentLayout.Controls.Add(rightPanel, 1, 0);
        rootLayout.Controls.Add(contentLayout, 0, 1);

        var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        var cancelButton = CreateDialogButton("取消", Color.FromArgb(245, 245, 245));
        cancelButton.DialogResult = DialogResult.Cancel;
        actionPanel.Controls.Add(cancelButton);
        var saveButton = CreateDialogButton("保存修改", Color.FromArgb(232, 241, 255));
        saveButton.Margin = new Padding(10, 0, 0, 0);
        saveButton.Click += (_, _) => SaveAndClose();
        actionPanel.Controls.Add(saveButton);
        rootLayout.Controls.Add(actionPanel, 0, 2);
        Controls.Add(rootLayout);
        CancelButton = cancelButton;
        RefreshEntriesList(selectedDirectoryPath);
    }

    public List<ExecutionDirectoryEntry> GetEntries() => _entries.Select(entry => entry.Clone()).ToList();
    public List<ExecutionDirectoryGroup> GetGroups() => _groups.Select(group => group.Clone()).ToList();

    private static Button CreateDialogButton(string text, Color backgroundColor)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(110, 36), Padding = new Padding(10, 5, 10, 5), FlatStyle = FlatStyle.Flat, BackColor = backgroundColor, UseCompatibleTextRendering = true };
        button.FlatAppearance.BorderColor = Color.FromArgb(190, 202, 215);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private void RefreshEntriesList(string? selectedDirectoryPath, IEnumerable<string>? selectedGroupIds = null)
    {
        var selectedPaths = selectedDirectoryPath is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(new[] { ScriptRunnerPathHelper.NormalizeDirectoryPath(selectedDirectoryPath) }, StringComparer.OrdinalIgnoreCase);
        var selectedGroups = (selectedGroupIds ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _isRefreshing = true;
        _entriesListBox.BeginUpdate();
        _entriesListBox.Items.Clear();
        foreach (var group in _groups.OrderBy(group => group.DisplayOrder))
        {
            _entriesListBox.Items.Add(new ManagerRow(group));
            if (group.IsExpanded)
            {
                foreach (var entry in _entries.Where(entry => string.Equals(entry.GroupId, group.Id, StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.DisplayOrder))
                    _entriesListBox.Items.Add(new ManagerRow(entry, true));
            }
        }
        foreach (var entry in _entries.Where(entry => string.IsNullOrWhiteSpace(entry.GroupId)).OrderBy(entry => entry.DisplayOrder))
            _entriesListBox.Items.Add(new ManagerRow(entry, false));
        _entriesListBox.EndUpdate();
        _summaryLabel.Text = $"共 {_entries.Count} 个执行目录，{_groups.Count} 个分组";
        for (var index = 0; index < _entriesListBox.Items.Count; index++)
        {
            if (_entriesListBox.Items[index] is not ManagerRow row) continue;
            if ((row.Entry is not null && selectedPaths.Contains(row.Entry.DirectoryPath)) || (row.Group is not null && selectedGroups.Contains(row.Group.Id))) _entriesListBox.SetSelected(index, true);
        }
        if (_entriesListBox.SelectedIndices.Count == 0 && _entriesListBox.Items.Count > 0 && selectedDirectoryPath is null) _entriesListBox.SetSelected(0, true);
        _isRefreshing = false;
        UpdateEditorFromSelection();
    }

    private void BeginDrag(object? item)
    {
        if (item is not ManagerRow row) return;
        if (!_entriesListBox.SelectedItems.Contains(row))
        {
            _entriesListBox.ClearSelected();
            var index = _entriesListBox.Items.IndexOf(row);
            if (index >= 0) _entriesListBox.SetSelected(index, true);
        }
        var rows = _entriesListBox.SelectedItems.Cast<ManagerRow>().ToList();
        if (rows.Count > 0) _entriesListBox.DoDragDrop(new DirectoryDragPayload(rows), DragDropEffects.Move);
    }

    private void UpdateDragEffect(DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetData(typeof(DirectoryDragPayload)) is not DirectoryDragPayload payload)
        {
            eventArgs.Effect = DragDropEffects.None;
            return;
        }

        var point = _entriesListBox.PointToClient(new Point(eventArgs.X, eventArgs.Y));
        var index = _entriesListBox.IndexFromPoint(point);
        var targetRow = index >= 0 ? _entriesListBox.Items[index] as ManagerRow : null;
        eventArgs.Effect = payload.Groups.Count > 0
            ? targetRow?.Group is not null && !payload.Groups.Contains(targetRow.Group)
                ? DragDropEffects.Move
                : DragDropEffects.None
            : payload.Entries.Count > 0
                ? DragDropEffects.Move
                : DragDropEffects.None;
    }

    private void CompleteDrag(DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetData(typeof(DirectoryDragPayload)) is not DirectoryDragPayload payload) return;
        var point = _entriesListBox.PointToClient(new Point(eventArgs.X, eventArgs.Y));
        var index = _entriesListBox.IndexFromPoint(point);
        var targetRow = index >= 0 ? _entriesListBox.Items[index] as ManagerRow : null;
        if (payload.Groups.Count > 0)
        {
            if (targetRow?.Group is not null) MoveGroupsByDrag(payload.Groups, targetRow.Group);
            return;
        }
        if (payload.Entries.Count == 0) return;
        var targetGroupId = targetRow?.Group?.Id ?? targetRow?.Entry?.GroupId;
        foreach (var entry in payload.Entries) entry.GroupId = targetGroupId;
        NormalizeOrders();
        RefreshEntriesList(payload.Entries.FirstOrDefault()?.DirectoryPath);
    }
    private void MoveGroupsByDrag(IReadOnlyList<ExecutionDirectoryGroup> selectedGroups, ExecutionDirectoryGroup targetGroup)
    {
        if (selectedGroups.Contains(targetGroup)) return;
        var movingGroups = _groups.Where(selectedGroups.Contains).ToList();
        if (movingGroups.Count == 0) return;
        _groups.RemoveAll(selectedGroups.Contains);
        var targetIndex = _groups.IndexOf(targetGroup);
        if (targetIndex < 0) return;
        _groups.InsertRange(targetIndex, movingGroups);
        NormalizeOrders();
        RefreshEntriesList(null, movingGroups.Select(group => group.Id));
    }
    private void EnforceSelectionType()
    {
        if (_isRefreshing || _isUpdatingSelection || _entriesListBox.SelectedIndices.Count < 2) return;
        var rows = _entriesListBox.SelectedItems.Cast<ManagerRow>().ToList();
        if (!rows.Any(row => row.Group is not null) || !rows.Any(row => row.Entry is not null)) return;
        _isUpdatingSelection = true;
        try
        {
            var keepIndex = _entriesListBox.SelectedIndices.Cast<int>().Last();
            _entriesListBox.ClearSelected();
            _entriesListBox.SetSelected(keepIndex, true);
        }
        finally { _isUpdatingSelection = false; }
    }

    private void UpdateEditorFromSelection()
    {
        var rows = _entriesListBox.SelectedItems.Cast<ManagerRow>().ToList();
        var entry = rows.Count == 1 ? rows[0].Entry : null;
        var group = rows.Count == 1 ? rows[0].Group : null;
        _displayNameTextBox.Text = entry?.DisplayName ?? string.Empty;
        _pathTextBox.Text = entry?.DirectoryPath ?? string.Empty;
        _groupNameTextBox.Text = group?.Name ?? string.Empty;
        if (entry is null) { _pathStatusLabel.Text = string.Empty; return; }
        var available = Directory.Exists(entry.DirectoryPath);
        _pathStatusLabel.Text = available ? "目录可用" : "目录不存在或暂时不可访问";
        _pathStatusLabel.ForeColor = available ? Color.FromArgb(23, 112, 41) : Color.FromArgb(180, 45, 30);
    }

    private void ApplyDisplayNameChanges()
    {
        var row = GetSingleSelectedRow();
        if (row?.Entry is null) return;
        row.Entry.DisplayName = _displayNameTextBox.Text.Trim();
        RefreshEntriesList(row.Entry.DirectoryPath);
    }

    private void CreateGroup()
    {
        var baseName = "新分组";
        var name = baseName;
        var suffix = 2;
        while (_groups.Any(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase))) name = $"{baseName} ({suffix++})";
        var selectedEntries = GetSelectedEntries();
        var newGroup = new ExecutionDirectoryGroup { Name = name, DisplayOrder = _groups.Count, IsExpanded = true };
        _groups.Add(newGroup);
        foreach (var entry in selectedEntries) entry.GroupId = newGroup.Id;
        RefreshEntriesList(selectedEntries.FirstOrDefault()?.DirectoryPath, new[] { newGroup.Id });
    }
    private void RenameSelectedGroup()
    {
        var row = GetSingleSelectedRow();
        if (row?.Group is null) return;
        var name = _groupNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_groups.Any(group => !ReferenceEquals(group, row.Group) && string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "分组名称不能重复。", "无法重命名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        row.Group.Name = name;
        RefreshEntriesList(null, new[] { row.Group.Id });
    }

    private void ToggleSelectedGroup()
    {
        var row = GetSingleSelectedRow();
        if (row?.Group is null) return;
        row.Group.IsExpanded = !row.Group.IsExpanded;
        RefreshEntriesList(null, new[] { row.Group.Id });
    }

    private void MoveSelectedItems(int offset)
    {
        var rows = _entriesListBox.SelectedItems.Cast<ManagerRow>().ToList();
        var groups = rows.Where(row => row.Group is not null).Select(row => row.Group!).ToList();
        if (groups.Count > 0)
        {
            var indexes = groups.Select(group => _groups.IndexOf(group)).OrderBy(index => index).ToList();
            if (offset < 0) { if (indexes[0] == 0) return; foreach (var index in indexes) (_groups[index - 1], _groups[index]) = (_groups[index], _groups[index - 1]); }
            else { if (indexes[^1] >= _groups.Count - 1) return; for (var index = indexes.Count - 1; index >= 0; index--) { var groupIndex = indexes[index]; (_groups[groupIndex + 1], _groups[groupIndex]) = (_groups[groupIndex], _groups[groupIndex + 1]); } }
            NormalizeOrders();
            RefreshEntriesList(null, groups.Select(group => group.Id));
            return;
        }
        var entries = GetSelectedEntries();
        if (entries.Count == 0) return;
        var ordered = _entries.OrderBy(entry => entry.DisplayOrder).ToList();
        var indexesEntries = entries.Select(entry => ordered.IndexOf(entry)).OrderBy(index => index).ToList();
        if (offset < 0) { if (indexesEntries[0] == 0) return; foreach (var index in indexesEntries) (ordered[index - 1], ordered[index]) = (ordered[index], ordered[index - 1]); }
        else { if (indexesEntries[^1] >= ordered.Count - 1) return; for (var index = indexesEntries.Count - 1; index >= 0; index--) { var entryIndex = indexesEntries[index]; (ordered[entryIndex + 1], ordered[entryIndex]) = (ordered[entryIndex], ordered[entryIndex + 1]); } }
        for (var index = 0; index < ordered.Count; index++) ordered[index].DisplayOrder = index;
        RefreshEntriesList(entries.FirstOrDefault()?.DirectoryPath);
    }

    private void DeleteSelectedItems()
    {
        var rows = _entriesListBox.SelectedItems.Cast<ManagerRow>().ToList();
        if (rows.Count == 0) return;
        var groups = rows.Where(row => row.Group is not null).Select(row => row.Group!).ToList();
        var entries = rows.Where(row => row.Entry is not null).Select(row => row.Entry!).ToList();
        if (groups.Count > 0)
        {
            var message = groups.Count == 1 ? "删除分组将同时删除组内所有执行目录，确定继续吗？" : $"删除 {groups.Count} 个分组将同时删除组内所有执行目录，确定继续吗？";
            if (MessageBox.Show(this, message, "确认删除分组", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            foreach (var group in groups) { _groups.Remove(group); _entries.RemoveAll(entry => string.Equals(entry.GroupId, group.Id, StringComparison.OrdinalIgnoreCase)); }
            NormalizeOrders();
            RefreshEntriesList(null);
            return;
        }
        if (MessageBox.Show(this, $"确定删除选中的 {entries.Count} 个执行目录吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var entry in entries) _entries.Remove(entry);
        NormalizeOrders();
        RefreshEntriesList(null);
    }

    private void NormalizeOrders()
    {
        for (var index = 0; index < _groups.Count; index++) _groups[index].DisplayOrder = index;
        var ordered = _entries.OrderBy(entry => entry.DisplayOrder).ToList();
        for (var index = 0; index < ordered.Count; index++) ordered[index].DisplayOrder = index;
    }

    private ManagerRow? GetSingleSelectedRow() => _entriesListBox.SelectedItems.Count == 1 ? _entriesListBox.SelectedItem as ManagerRow : null;
    private List<ExecutionDirectoryEntry> GetSelectedEntries() => _entriesListBox.SelectedItems.Cast<ManagerRow>().Where(row => row.Entry is not null).Select(row => row.Entry!).ToList();

    private void SaveAndClose()
    {
        ApplyDisplayNameChanges();
        NormalizeOrders();
        SelectedDirectoryPath = _entriesListBox.SelectedItems.Cast<ManagerRow>().Select(row => row.Entry?.DirectoryPath).FirstOrDefault(path => path is not null) ?? _entries.FirstOrDefault()?.DirectoryPath;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed class DirectoryDragPayload
    {
        public DirectoryDragPayload(IReadOnlyList<ManagerRow> rows)
        {
            Entries = rows.Where(row => row.Entry is not null).Select(row => row.Entry!).ToList();
            Groups = rows.Where(row => row.Group is not null).Select(row => row.Group!).ToList();
        }
        public IReadOnlyList<ExecutionDirectoryEntry> Entries { get; }
        public IReadOnlyList<ExecutionDirectoryGroup> Groups { get; }
    }
    private sealed class ManagerRow
    {
        public ExecutionDirectoryGroup? Group { get; }
        public ExecutionDirectoryEntry? Entry { get; }
        private readonly bool _indented;
        public ManagerRow(ExecutionDirectoryGroup group) => Group = group;
        public ManagerRow(ExecutionDirectoryEntry entry, bool indented) { Entry = entry; _indented = indented; }
        public override string ToString() => Group is not null ? $"{(Group.IsExpanded ? "▼" : "▶")} {Group.Name}" : (_indented ? $"    {Entry!.DisplayLabel}" : Entry!.DisplayLabel);
    }

    private sealed class GroupChoice(string? id, string name)
    {
        public string? Id { get; } = id;
        public override string ToString() => name;
    }
}