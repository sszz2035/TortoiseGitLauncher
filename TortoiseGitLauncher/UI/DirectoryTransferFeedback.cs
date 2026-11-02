using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal static class DirectoryTransferFeedback
{
    public static void Show(IWin32Window owner, DirectoryTransferResult result, bool move, string targetName)
    {
        var action = move ? "移动" : "复制";
        var message = result.HasChanges
            ? $"已在编辑副本中{action}到“{targetName}”：新增目录 {result.AddedDirectories} 项，新增分组 {result.AddedGroups} 个。"
            : $"未{action}任何项。";
        if (move) message += $"\n源页移除分组 {result.RemovedGroups} 个；重复项保留在源页。";
        message += $"\n跳过重复目录 {result.SkippedPaths.Count} 项；全部重复而跳过的分组 {result.SkippedGroups} 个。";
        if (result.RenamedGroups.Count > 0) message += "\n分组自动改名：\n" + string.Join("\n", result.RenamedGroups);
        message += "\n\n点击“保存修改”统一应用；取消则全部放弃。";
        if (result.SkippedPaths.Count == 0)
        {
            MessageBox.Show(owner, message, $"{action}结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var details = new Form
        {
            Text = $"{action}结果", StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new System.Drawing.Size(560, 400), Size = new System.Drawing.Size(700, 520),
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10F), MinimizeBox = false, MaximizeBox = false
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = message + "\n\n跳过的目录：", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) }, 0, 0);
        layout.Controls.Add(new TextBox
        {
            Text = string.Join(Environment.NewLine, result.SkippedPaths), Multiline = true, ReadOnly = true,
            BackColor = System.Drawing.Color.White, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill
        }, 0, 1);
        var close = new Button { Text = "确定", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0), DialogResult = DialogResult.OK };
        layout.Controls.Add(close, 0, 2);
        details.Controls.Add(layout);
        details.AcceptButton = close;
        details.CancelButton = close;
        details.ShowDialog(owner);
    }
}
