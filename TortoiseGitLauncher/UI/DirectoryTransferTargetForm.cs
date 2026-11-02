using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class DirectoryTransferTargetForm : Form
{
    private readonly ComboBox _pages;
    public string? SelectedPageId => (_pages.SelectedItem as DirectoryPage)?.Id;

    public DirectoryTransferTargetForm(string moduleName, IEnumerable<DirectoryPage> pages, bool move)
    {
        Text = move ? "移动到其他目录页" : "复制到其他目录页";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(480, 260);
        Size = new Size(540, 280);
        Font = new Font("Microsoft YaHei UI", 10F);
        MinimizeBox = false;
        MaximizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = $"{moduleName} · 目标目录页", AutoSize = true, Margin = new Padding(0, 0, 0, 10) }, 0, 0);
        _pages = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 0, 12) };
        foreach (var page in pages.OrderBy(page => page.DisplayOrder)) _pages.Items.Add(page);
        if (_pages.Items.Count > 0) _pages.SelectedIndex = 0;
        layout.Controls.Add(_pages, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = "重复路径将跳过。点击管理窗口的“保存修改”后生效，取消则放弃本次修改。",
            AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0)
        }, 0, 2);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 12, 0, 0)
        };
        var cancel = new Button { Text = "取消", AutoSize = true, MinimumSize = new Size(90, 34), DialogResult = DialogResult.Cancel };
        var confirm = new Button { Text = move ? "移动" : "复制", AutoSize = true, MinimumSize = new Size(90, 34), Enabled = _pages.Items.Count > 0 };
        confirm.Click += (_, _) => { if (SelectedPageId is not null) DialogResult = DialogResult.OK; };
        actions.Controls.Add(cancel);
        actions.Controls.Add(confirm);
        layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);
        CancelButton = cancel;
        AcceptButton = confirm;
    }
}
