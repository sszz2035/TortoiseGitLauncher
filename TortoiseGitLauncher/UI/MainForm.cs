using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed partial class MainForm : Form
{
    private Label _selectedDirectoryValue = null!;
    private ComboBox _repoRootComboBox = null!;
    private Label _statusValue = null!;
    private readonly string _launchDirectory;
    private readonly List<RepositoryEntry> _repositoryEntries;
    private string _selectedDirectory;
    private RepositoryEntry? _selectedRepository;
    private bool _isUpdatingRepoComboBox;

    private static readonly CommandButton[] CommandButtons =
    [
        new("提交", "commit", CommandScope.WorkingDirectory),
        new("比较差异", "diff", CommandScope.WorkingDirectory),
        new("日志", "log", CommandScope.WorkingDirectory),
        new("同步", "sync", CommandScope.RepositoryRoot),
        new("拉取", "pull", CommandScope.RepositoryRoot),
        new("推送", "push", CommandScope.RepositoryRoot),
        new("切换/检出", "switch", CommandScope.RepositoryRoot),
        new("合并", "merge", CommandScope.RepositoryRoot),
        new("清理", "cleanup", CommandScope.RepositoryRoot),
        new("仓库状态", "repostatus", CommandScope.WorkingDirectory),
        new("还原", "revert", CommandScope.WorkingDirectory),
        new("解决冲突", "resolve", CommandScope.WorkingDirectory),
        new("获取", "fetch", CommandScope.RepositoryRoot),
        new("贮藏", "stashsave", CommandScope.RepositoryRoot),
        new("弹出贮藏", "stashpop", CommandScope.RepositoryRoot)
    ];

    private static readonly CommandSection[] CommandSections =
    [
        new("提交与更新", Color.FromArgb(59, 130, 246), ["提交", "比较差异", "拉取", "推送", "同步"]),
        new("管理工作区", Color.FromArgb(34, 197, 94), ["切换/检出", "合并", "清理", "仓库状态", "日志"]),
        new("处理更改", Color.FromArgb(168, 85, 247), ["还原", "解决冲突", "获取", "贮藏", "弹出贮藏"])
    ];

    public MainForm()
    {
        _launchDirectory = Environment.CurrentDirectory;
        _selectedDirectory = _launchDirectory;
        _repositoryEntries = RepositoryHistoryStore.Load();

        var detectedRepoRoot = GitPathHelper.FindRepositoryRoot(_launchDirectory);
        RepositoryEntry? initialSelection = null;
        var initialStatus = string.Empty;
        var initialStatusIsError = false;
        var saveLoadedEntries = false;

        if (detectedRepoRoot is not null)
        {
            initialSelection = FindRepositoryEntryByPath(detectedRepoRoot);
            if (initialSelection is null)
            {
                initialSelection = UpsertRepositoryEntry(detectedRepoRoot, displayName: null, moveToTop: true);
                saveLoadedEntries = true;
            }

            initialStatus = "已自动定位到当前仓库根目录。";
        }
        else if (_repositoryEntries.Count > 0)
        {
            initialSelection = _repositoryEntries[0];
            initialStatus = "已载入最近使用的仓库根目录。";
        }
        else
        {
            initialStatus = "当前目录未检测到 Git 仓库。请先选择一个仓库目录。";
            initialStatusIsError = true;
        }

        Text = "TortoiseGit 图形命令快捷面板";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1260, 820);
        Size = new Size(1460, 920);
        BackColor = Color.FromArgb(244, 247, 251);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var shellLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 228F));
        shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        shellLayout.Controls.Add(CreateSidebar(), 0, 0);
        shellLayout.Controls.Add(CreateRepositoryManagementPage(), 1, 0);
        Controls.Add(shellLayout);

        RefreshRepoRootComboBox(selectedRepoRootPath: null);

        if (initialSelection is not null)
        {
            SelectRepositoryEntry(initialSelection, initialStatus, moveToTop: false, saveImmediately: false);
            if (saveLoadedEntries)
            {
                RepositoryHistoryStore.Save(_repositoryEntries);
            }
        }
        else
        {
            ClearSelectedRepository(initialStatus, initialStatusIsError);
        }
    }

    private Control CreateSidebar()
    {
        var sidebarCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(229, 235, 243),
            CornerRadius = 22,
            Padding = new Padding(14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brandPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16),
            BackColor = Color.Transparent
        };
        brandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        brandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        brandPanel.Controls.Add(CreateIconBox(UiIconKind.Brand, Color.FromArgb(46, 118, 255), 42, Color.FromArgb(237, 244, 255)), 0, 0);
        brandPanel.Controls.Add(new Label
        {
            Text = "功能导航",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(10, 8, 0, 0)
        }, 1, 0);

        layout.Controls.Add(brandPanel, 0, 0);
        layout.Controls.Add(CreateSidebarButton("仓库管理", UiIconKind.SidebarRepo, isActive: true), 0, 1);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 2);
        layout.Controls.Add(new Label
        {
            Text = "后续新增功能时，可以继续往这里扩展更多页面。",
            AutoSize = true,
            MaximumSize = new Size(168, 0),
            ForeColor = Color.FromArgb(102, 110, 122),
            Margin = new Padding(4, 14, 4, 0)
        }, 0, 3);

        sidebarCard.Controls.Add(layout);
        return sidebarCard;
    }

    private Control CreateRepositoryManagementPage()
    {
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

        contentLayout.Controls.Add(CreatePageHeaderCard());
        contentLayout.Controls.Add(CreateRepositorySelectionCard());
        contentLayout.Controls.Add(CreateInfoBanner());

        foreach (var section in CommandSections)
        {
            contentLayout.Controls.Add(CreateCommandSectionCard(section));
        }

        contentLayout.Controls.Add(CreateFooterCard());
        contentLayout.Layout += (_, _) => SyncScrollPanelExtent(scrollPanel, contentLayout);
        scrollPanel.SizeChanged += (_, _) => SyncScrollPanelExtent(scrollPanel, contentLayout);
        SyncScrollPanelExtent(scrollPanel, contentLayout);
        scrollPanel.Controls.Add(contentLayout);
        return scrollPanel;
    }
    private static void SyncScrollPanelExtent(Panel scrollPanel, Control content)
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
