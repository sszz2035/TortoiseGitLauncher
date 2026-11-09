using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

// This modal editor owns deep clones. A failed save leaves them open for retry.
internal sealed class DirectoryPageManagerForm<TPage> : Form where TPage : DirectoryPage, new()
{
    private readonly List<TPage> _pages;
    private readonly Func<List<TPage>, string, string?> _save;
    private readonly ListBox _list;
    private readonly TextBox _name;
    private readonly Label _error;
    private readonly Button _delete;
    private readonly Button _up;
    private readonly Button _down;
    private bool _updating;

    public DirectoryPageManagerForm(string moduleName, IEnumerable<TPage> pages, string selectedId,
        Func<TPage, TPage> clone, Func<List<TPage>, string, string?> save)
    {
        _pages = pages.Select(clone).ToList();
        _save = save;
        Text = moduleName + " · 管理目录页";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 410);
        Size = new Size(740, 490);
        Font = new Font("Microsoft YaHei UI", 10F);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4,
            Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var hint = new Label
        {
            Text = "每页拥有独立目录列表；保存修改后应用，取消则放弃本次调整。",
            AutoSize = true, MaximumSize = new Size(660, 0), Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(hint, 0, 0);
        root.SetColumnSpan(hint, 2);
        _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        _list.SelectedIndexChanged += (_, _) => UpdateEditor();
        root.Controls.Add(_list, 0, 1);
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(16, 0, 0, 0)
        };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.Controls.Add(new Label { Text = "目录页名称", AutoSize = true }, 0, 0);
        _name = new TextBox { Dock = DockStyle.Top, BackColor = Color.White };
        _name.TextChanged += (_, _) =>
        {
            if (_updating || _list.SelectedItem is not TPage page) return;
            page.Name = _name.Text.Trim();
            var index = _list.SelectedIndex;
            _updating = true;
            try { _list.Items[index] = page; }
            finally { _updating = false; }
        };
        editor.Controls.Add(_name, 0, 1);
        root.Controls.Add(editor, 1, 1);
        var operations = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
        operations.Controls.Add(MakeButton("新建页", (_, _) =>
        {
            var name = "新目录页";
            for (var suffix = 2; _pages.Any(page => string.Equals(page.Name, name, StringComparison.OrdinalIgnoreCase)); suffix++)
                name = $"新目录页 ({suffix})";
            var page = new TPage { Name = name, DisplayOrder = _pages.Count };
            _pages.Add(page);
            RefreshList(page.Id);
            _name.Focus();
            _name.SelectAll();
        }));
        _up = MakeButton("上移", (_, _) => MoveSelectedPage(-1));
        _down = MakeButton("下移", (_, _) => MoveSelectedPage(1));
        _delete = MakeButton("删除页", (_, _) => DeleteSelected());
        operations.Controls.AddRange([_up, _down, _delete]);
        root.Controls.Add(operations, 0, 2);
        root.SetColumnSpan(operations, 2);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _error = new Label { AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = Color.Firebrick };
        footer.Controls.Add(_error, 0, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        actions.Controls.Add(MakeButton("保存修改", (_, _) => SaveAndClose()));
        var cancel = MakeButton("取消", (_, _) => { });
        cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.Add(cancel);
        footer.Controls.Add(actions, 1, 0);
        root.Controls.Add(footer, 0, 3);
        root.SetColumnSpan(footer, 2);
        Controls.Add(root);
        CancelButton = cancel;
        RefreshList(selectedId);
    }

    private void RefreshList(string id)
    {
        _updating = true;
        try
        {
            _list.Items.Clear();
            _list.Items.AddRange(_pages.Cast<object>().ToArray());
            _list.SelectedIndex = Math.Max(0, _pages.FindIndex(page => page.Id == id));
        }
        finally { _updating = false; }
        UpdateEditor();
    }

    private void UpdateEditor()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            _name.ReadOnly = _list.SelectedItem is not TPage;
            _name.Text = (_list.SelectedItem as TPage)?.Name ?? string.Empty;
            _delete.Enabled = _pages.Count > 1;
            _up.Enabled = _list.SelectedIndex > 0;
            _down.Enabled = _list.SelectedIndex >= 0 && _list.SelectedIndex < _pages.Count - 1;
        }
        finally { _updating = false; }
    }

    private void MoveSelectedPage(int offset)
    {
        var index = _list.SelectedIndex;
        var target = index + offset;
        if (index < 0 || target < 0 || target >= _pages.Count) return;
        var id = _pages[index].Id;
        (_pages[index], _pages[target]) = (_pages[target], _pages[index]);
        RefreshList(id);
    }

    private void DeleteSelected()
    {
        if (_pages.Count <= 1 || _list.SelectedItem is not TPage page) return;
        if (MessageBox.Show(this, $"确定删除目录页“{page.Name}”及其中所有分组和目录配置吗？\n不会删除磁盘上的真实目录，也不会影响其他页。",
                "确认删除目录页", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var index = _list.SelectedIndex;
        _pages.RemoveAt(index);
        RefreshList(_pages[Math.Min(index, _pages.Count - 1)].Id);
    }

    private void SaveAndClose()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in _pages)
        {
            if (string.IsNullOrWhiteSpace(page.Name) || !names.Add(page.Name.Trim()))
            {
                _error.Text = "目录页名称不能为空，且同一模块内不能重复。";
                RefreshList(page.Id);
                _name.Focus();
                return;
            }
        }

        for (var index = 0; index < _pages.Count; index++) _pages[index].DisplayOrder = index;
        var selectedId = (_list.SelectedItem as TPage)?.Id ?? _pages[0].Id;
        var error = _save(_pages, selectedId);
        if (error is not null)
        {
            _error.Text = "保存失败：" + error;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button MakeButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text, AutoSize = true, MinimumSize = new Size(90, 34),
            BackColor = Color.FromArgb(240, 246, 255), FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 8, 8, 4)
        };
        button.Click += click;
        return button;
    }
}
