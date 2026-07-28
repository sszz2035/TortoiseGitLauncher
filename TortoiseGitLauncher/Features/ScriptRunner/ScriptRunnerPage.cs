using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class ScriptRunnerPage : UserControl
{
    public ScriptRunnerPage()
    {
        BackColor = Color.Transparent;
        Margin = new Padding(0);

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent
        };

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        contentLayout.Controls.Add(CreateHeaderCard());
        contentLayout.Controls.Add(CreateSectionCard(
            "执行目录",
            UiIconKind.RepoFolder,
            Color.FromArgb(59, 130, 246),
            "尚未选择执行目录",
            58));
        contentLayout.Controls.Add(CreateSectionCard(
            "脚本按钮",
            UiIconKind.ScriptRunner,
            Color.FromArgb(34, 197, 94),
            "尚未配置脚本",
            92));
        contentLayout.Controls.Add(CreateSectionCard(
            "运行实例与输出",
            UiIconKind.RepoStatus,
            Color.FromArgb(168, 85, 247),
            "暂无运行实例",
            220));

        contentLayout.Layout += (_, _) => SyncScrollExtent(scrollPanel, contentLayout);
        scrollPanel.SizeChanged += (_, _) => SyncScrollExtent(scrollPanel, contentLayout);
        scrollPanel.Controls.Add(contentLayout);
        Controls.Add(scrollPanel);
    }

    private static Control CreateHeaderCard()
    {
        var card = CreateCard(new Padding(22, 18, 22, 18));
        card.Margin = new Padding(0, 0, 0, 16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        layout.Controls.Add(new PictureBox
        {
            Size = new Size(52, 52),
            Image = IconFactory.Create(UiIconKind.ScriptRunner, Color.FromArgb(46, 118, 255), 30),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.FromArgb(236, 244, 255),
            Margin = new Padding(0)
        }, 0, 0);

        var textLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(14, 2, 0, 0),
            BackColor = Color.Transparent
        };
        textLayout.Controls.Add(new Label
        {
            Text = "脚本执行",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 16.5F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 4)
        });
        textLayout.Controls.Add(new Label
        {
            Text = "在选定目录中运行自定义脚本，并集中查看运行状态和输出。",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            ForeColor = Color.FromArgb(100, 108, 120),
            Margin = new Padding(0)
        });
        layout.Controls.Add(textLayout, 1, 0);

        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateSectionCard(
        string title,
        UiIconKind iconKind,
        Color accentColor,
        string emptyText,
        int contentHeight)
    {
        var card = CreateCard(new Padding(20, 18, 20, 18));
        card.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };

        var titleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titleRow.Controls.Add(new PictureBox
        {
            Size = new Size(18, 18),
            Image = IconFactory.Create(iconKind, accentColor, 18),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        }, 0, 0);
        titleRow.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(8, 0, 0, 0)
        }, 1, 0);
        layout.Controls.Add(titleRow, 0, 0);

        var emptyState = new Panel
        {
            Dock = DockStyle.Top,
            Height = contentHeight,
            BackColor = Color.FromArgb(248, 250, 253),
            Margin = new Padding(0)
        };
        emptyState.Controls.Add(new Label
        {
            Text = emptyText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(112, 120, 132)
        });
        layout.Controls.Add(emptyState, 0, 1);

        card.Controls.Add(layout);
        return card;
    }

    private static CardPanel CreateCard(Padding padding) =>
        new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(229, 235, 243),
            CornerRadius = 18,
            Padding = padding
        };

    private static void SyncScrollExtent(Panel scrollPanel, Control content)
    {
        content.PerformLayout();
        var contentHeight = content.GetPreferredSize(new Size(scrollPanel.ClientSize.Width, 0)).Height;
        if (contentHeight <= 0)
        {
            contentHeight = content.Bottom;
        }

        scrollPanel.AutoScrollMinSize = new Size(0, contentHeight);
        var maxScrollY = Math.Max(0, contentHeight - scrollPanel.ClientSize.Height);
        var currentScrollY = Math.Max(0, -scrollPanel.AutoScrollPosition.Y);
        if (currentScrollY > maxScrollY)
        {
            scrollPanel.AutoScrollPosition = new Point(0, maxScrollY);
        }
    }
}
