using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class DirectoryPageSelectorButton : Button
{
    private readonly string _title;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolTip _toolTip = new();

    public event Action<string>? PageSelected;
    public event EventHandler? ManagePagesRequested;

    public DirectoryPageSelectorButton(string title)
    {
        _title = title;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MaximumSize = new Size(460, 0);
        MinimumSize = new Size(180, 36);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.FromArgb(243, 247, 255);
        ForeColor = Color.FromArgb(35, 58, 92);
        Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
        Padding = new Padding(8, 4, 8, 4);
        Margin = new Padding(0);
        TextAlign = ContentAlignment.MiddleLeft;
        UseMnemonic = false;
        Cursor = Cursors.Hand;
        AccessibleName = title + "目录页切换";
        Click += (_, _) => _menu.Show(this, new Point(0, Height));
    }

    public void SetPages(IEnumerable<DirectoryPage> pages, string selectedId)
    {
        _menu.Items.Clear();
        foreach (var page in pages.OrderBy(page => page.DisplayOrder))
        {
            var id = page.Id;
            var item = new ToolStripMenuItem(page.Name)
            {
                Checked = string.Equals(id, selectedId, StringComparison.OrdinalIgnoreCase),
                ToolTipText = page.Name,
                AutoToolTip = true
            };
            item.Click += (_, _) => PageSelected?.Invoke(id);
            _menu.Items.Add(item);
            if (item.Checked)
            {
                var shortName = page.Name.Length > 18 ? page.Name[..18] + "…" : page.Name;
                Text = $"{_title} · {shortName}  ▾";
                _toolTip.SetToolTip(this, $"{_title} · {page.Name}（点击切换或管理目录页）");
            }
        }

        _menu.Items.Add(new ToolStripSeparator());
        var manage = new ToolStripMenuItem("管理目录页…");
        manage.Click += (_, _) => ManagePagesRequested?.Invoke(this, EventArgs.Empty);
        _menu.Items.Add(manage);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menu.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
