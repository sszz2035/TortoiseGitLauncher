using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class ExplorerDirectoryTargetForm : Form
{
    private readonly ComboBox _pages;
    public string? SelectedPageId => (_pages.SelectedItem as DirectoryPage)?.Id;

    public ExplorerDirectoryTargetForm(string moduleName, string directoryPath,
        IEnumerable<DirectoryPage> pages, string selectedPageId)
    {
        Text = $"添加到{moduleName}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(500, 320);
        Size = new Size(620, 350);
        Font = new Font("Microsoft YaHei UI", 10F);
        MinimizeBox = false;
        MaximizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(18)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "待添加目录", AutoSize = true }, 0, 0);
        layout.Controls.Add(new TextBox
        {
            Text = directoryPath, ReadOnly = true, BackColor = Color.White,
            Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 12)
        }, 0, 1);
        layout.Controls.Add(new Label { Text = "选择目标目录页", AutoSize = true }, 0, 2);
        _pages = new ComboBox
        {
            Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 6, 0, 12)
        };
        foreach (var page in pages.OrderBy(page => page.DisplayOrder)) _pages.Items.Add(page);
        _pages.SelectedItem = _pages.Items.Cast<DirectoryPage>().FirstOrDefault(page =>
            string.Equals(page.Id, selectedPageId, StringComparison.OrdinalIgnoreCase));
        if (_pages.SelectedIndex < 0 && _pages.Items.Count > 0) _pages.SelectedIndex = 0;
        layout.Controls.Add(_pages, 0, 3);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 12, 0, 0)
        };
        var cancel = new Button
        {
            Text = "取消", AutoSize = true, MinimumSize = new Size(90, 34),
            DialogResult = DialogResult.Cancel
        };
        var confirm = new Button
        {
            Text = "添加", AutoSize = true, MinimumSize = new Size(90, 34),
            Enabled = _pages.Items.Count > 0
        };
        confirm.Click += (_, _) => { if (SelectedPageId is not null) DialogResult = DialogResult.OK; };
        actions.Controls.Add(cancel);
        actions.Controls.Add(confirm);
        layout.Controls.Add(actions, 0, 4);
        Controls.Add(layout);
        AcceptButton = confirm;
        CancelButton = cancel;
    }
}
