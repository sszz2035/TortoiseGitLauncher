using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            try
            {
                var crashLogPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
                File.WriteAllText(crashLogPath, ex.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch
            {
            }

            MessageBox.Show(
                ex.ToString(),
                "TortoiseGitLauncher 启动失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

internal sealed class MainForm : Form
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

    private Control CreatePageHeaderCard()
    {
        var card = CreateCardPanel(new Padding(22, 18, 22, 18));
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

        layout.Controls.Add(CreateIconBox(UiIconKind.Brand, Color.FromArgb(46, 118, 255), 52, Color.FromArgb(236, 244, 255)), 0, 0);

        var textLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(14, 2, 0, 0),
            BackColor = Color.Transparent
        };
        textLayout.Controls.Add(new Label
        {
            Text = "TortoiseGit 图形命令快捷面板",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 16.5F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 4)
        });
        textLayout.Controls.Add(new Label
        {
            Text = "仓库根目录可以从下拉框直接切换，列表支持自定义显示名、手动排序和删除条目。",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            ForeColor = Color.FromArgb(96, 104, 116)
        });

        layout.Controls.Add(textLayout, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateRepositorySelectionCard()
    {
        var card = CreateCardPanel(new Padding(20, 18, 20, 18));
        card.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titleRow.Controls.Add(CreateSmallIcon(UiIconKind.RepoFolder, Color.FromArgb(46, 118, 255), 18), 0, 0);
        titleRow.Controls.Add(new Label
        {
            Text = "仓库根目录",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(8, 0, 0, 0)
        }, 1, 0);
        var openRepositoryRootButton = CreateHeaderButton("打开根目录", Color.FromArgb(243, 247, 255));
        openRepositoryRootButton.MinimumSize = new Size(128, 34);
        openRepositoryRootButton.Padding = new Padding(9, 4, 9, 4);
        openRepositoryRootButton.Margin = new Padding(14, 0, 0, 0);
        openRepositoryRootButton.Image = IconFactory.Create(UiIconKind.RepoFolder, Color.FromArgb(59, 130, 246), 16);
        openRepositoryRootButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        openRepositoryRootButton.ImageAlign = ContentAlignment.MiddleLeft;
        openRepositoryRootButton.TextAlign = ContentAlignment.MiddleCenter;
        openRepositoryRootButton.Click += (_, _) => OpenRepositoryRootInExplorer();
        titleRow.Controls.Add(openRepositoryRootButton, 2, 0);

        layout.Controls.Add(titleRow, 0, 0);

        var selectionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        };
        selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _repoRootComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = false,
            MaxDropDownItems = 12,
            Margin = new Padding(0, 0, 12, 0),
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };
        _repoRootComboBox.SelectedIndexChanged += (_, _) => SelectRepoRootFromComboBox();
        selectionRow.Controls.Add(_repoRootComboBox, 0, 0);

        var browseButton = CreateHeaderButton("选择仓库目录", Color.FromArgb(243, 247, 255));
        browseButton.Image = IconFactory.Create(UiIconKind.ChooseFolder, Color.FromArgb(59, 130, 246), 16);
        browseButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        browseButton.ImageAlign = ContentAlignment.MiddleLeft;
        browseButton.TextAlign = ContentAlignment.MiddleCenter;
        browseButton.Margin = new Padding(0, 0, 10, 0);
        browseButton.Click += (_, _) => BrowseForRepositoryDirectory();
        selectionRow.Controls.Add(browseButton, 1, 0);

        var manageButton = CreateHeaderButton("管理仓库列表", Color.FromArgb(245, 248, 242));
        manageButton.Image = IconFactory.Create(UiIconKind.ManageList, Color.FromArgb(90, 99, 112), 16);
        manageButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        manageButton.ImageAlign = ContentAlignment.MiddleLeft;
        manageButton.TextAlign = ContentAlignment.MiddleCenter;
        manageButton.Click += (_, _) => OpenRepositoryManager();
        selectionRow.Controls.Add(manageButton, 2, 0);
        layout.Controls.Add(selectionRow, 0, 1);

        var currentPathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        currentPathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        currentPathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        currentPathRow.Controls.Add(new Label
        {
            Text = "当前目标目录：",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(74, 82, 94),
            Margin = new Padding(0, 2, 8, 0)
        }, 0, 0);

        _selectedDirectoryValue = new Label
        {
            Text = "未选择仓库根目录",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            ForeColor = Color.FromArgb(52, 60, 72)
        };
        currentPathRow.Controls.Add(_selectedDirectoryValue, 1, 0);
        layout.Controls.Add(currentPathRow, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private Control CreateInfoBanner()
    {
        var banner = new CardPanel
        {
            Dock = DockStyle.Top,
            FillColor = Color.FromArgb(240, 246, 255),
            BorderColor = Color.FromArgb(220, 232, 252),
            CornerRadius = 16,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 0, 0, 14)
        };

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.Controls.Add(CreateSmallIcon(UiIconKind.Info, Color.FromArgb(46, 118, 255), 16), 0, 0);
        row.Controls.Add(new Label
        {
            Text = "下拉框显示的是自定义仓库列表。蓝色按钮对应当前目标目录；绿色按钮使用选中仓库的根目录来执行。命令按钮都固定为统一尺寸，不再出现最后一排被拉宽。",
            AutoSize = true,
            MaximumSize = new Size(1040, 0),
            ForeColor = Color.FromArgb(72, 88, 115),
            Margin = new Padding(10, 0, 0, 0)
        }, 1, 0);
        banner.Controls.Add(row);
        return banner;
    }

    private Control CreateCommandSectionCard(CommandSection section)
    {
        var card = CreateCardPanel(new Padding(18, 16, 18, 16));
        card.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerRow.Controls.Add(new Panel
        {
            BackColor = section.AccentColor,
            Size = new Size(9, 9),
            Margin = new Padding(0, 6, 8, 0)
        }, 0, 0);
        headerRow.Controls.Add(new Label
        {
            Text = section.Title,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(39, 47, 58)
        }, 1, 0);
        layout.Controls.Add(headerRow, 0, 0);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            WrapContents = true,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        flow.Layout += (_, _) => UpdateFlowPanelHeight(flow);
        flow.SizeChanged += (_, _) => UpdateFlowPanelHeight(flow);

        foreach (var commandTitle in section.CommandTitles)
        {
            var command = CommandButtons.FirstOrDefault(button => button.Title == commandTitle);
            if (command is null)
            {
                continue;
            }

            flow.Controls.Add(CreateCommandButton(command));
        }

        UpdateFlowPanelHeight(flow);
        layout.Controls.Add(flow, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static void UpdateFlowPanelHeight(FlowLayoutPanel flow)
    {
        var availableWidth = flow.ClientSize.Width;
        if (availableWidth <= 0)
        {
            availableWidth = flow.Width;
        }

        if (availableWidth <= 0)
        {
            return;
        }

        var rowWidth = 0;
        var rowHeight = 0;
        var targetHeight = 0;

        foreach (Control control in flow.Controls)
        {
            if (!control.Visible)
            {
                continue;
            }

            var itemWidth = control.Width + control.Margin.Horizontal;
            var itemHeight = control.Height + control.Margin.Vertical;
            if (rowWidth > 0 && rowWidth + itemWidth > availableWidth)
            {
                targetHeight += rowHeight;
                rowWidth = 0;
                rowHeight = 0;
            }

            rowWidth += itemWidth;
            rowHeight = Math.Max(rowHeight, itemHeight);
        }

        targetHeight += rowHeight;
        if (targetHeight > 0 && flow.Height != targetHeight)
        {
            flow.Height = targetHeight;
        }
    }

    private Control CreateFooterCard()
    {
        var card = CreateCardPanel(new Padding(18, 14, 18, 14));
        card.Margin = new Padding(0, 0, 0, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var statusRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6)
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusRow.Controls.Add(CreateSmallIcon(UiIconKind.RepoFolder, Color.FromArgb(74, 168, 78), 16), 0, 0);

        _statusValue = new Label
        {
            Text = "就绪",
            AutoSize = true,
            MaximumSize = new Size(1080, 0),
            ForeColor = Color.FromArgb(20, 96, 28),
            Margin = new Padding(8, 0, 0, 0)
        };
        statusRow.Controls.Add(_statusValue, 1, 0);
        layout.Controls.Add(statusRow, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "仓库列表会保存在本机用户目录下。后续新增页面时，可以继续沿用当前左侧导航结构。",
            AutoSize = true,
            MaximumSize = new Size(1080, 0),
            ForeColor = Color.FromArgb(104, 111, 121)
        }, 0, 1);

        card.Controls.Add(layout);
        return card;
    }
    private static CardPanel CreateCardPanel(Padding padding) =>
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

    private static Button CreateHeaderButton(string text, Color backgroundColor)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(154, 38),
            Padding = new Padding(12, 6, 12, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = backgroundColor,
            UseCompatibleTextRendering = true,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point)
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(214, 223, 236);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static Button CreateSidebarButton(string text, UiIconKind iconKind, bool isActive)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 56,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
            Image = IconFactory.Create(iconKind, Color.FromArgb(31, 41, 55), 18),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            UseCompatibleTextRendering = true,
            BackColor = isActive ? Color.FromArgb(219, 233, 252) : Color.White,
            ForeColor = Color.FromArgb(25, 30, 40),
            Enabled = true,
            TabStop = false
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private Button CreateCommandButton(CommandButton command)
    {
        var isWorkingDirectoryCommand = command.Scope == CommandScope.WorkingDirectory;
        var button = new Button
        {
            Text = command.Title,
            Size = new Size(222, 54),
            Margin = new Padding(0, 0, 14, 12),
            Padding = new Padding(12, 8, 12, 8),
            Font = new Font("Microsoft YaHei UI", 10.3F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = isWorkingDirectoryCommand ? Color.FromArgb(240, 246, 255) : Color.FromArgb(241, 249, 239),
            ForeColor = Color.FromArgb(36, 44, 58),
            Image = IconFactory.Create(GetCommandIcon(command.Title), isWorkingDirectoryCommand ? Color.FromArgb(59, 130, 246) : Color.FromArgb(44, 137, 62), 18),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };

        button.FlatAppearance.BorderColor = isWorkingDirectoryCommand
            ? Color.FromArgb(207, 222, 246)
            : Color.FromArgb(207, 232, 202);
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => ExecuteTortoiseGitCommand(command);
        return button;
    }

    private static PictureBox CreateIconBox(UiIconKind kind, Color color, int boxSize, Color backgroundColor) =>
        new()
        {
            Size = new Size(boxSize, boxSize),
            Image = IconFactory.Create(kind, color, Math.Max(18, boxSize - 18)),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = backgroundColor,
            Margin = new Padding(0)
        };

    private static PictureBox CreateSmallIcon(UiIconKind kind, Color color, int size) =>
        new()
        {
            Size = new Size(size, size),
            Image = IconFactory.Create(kind, color, size),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

    private static UiIconKind GetCommandIcon(string commandTitle) => commandTitle switch
    {
        "提交" => UiIconKind.Commit,
        "比较差异" => UiIconKind.RepoStatus,
        "日志" => UiIconKind.RepoStatus,
        "同步" => UiIconKind.Sync,
        "拉取" => UiIconKind.Pull,
        "推送" => UiIconKind.Push,
        "切换/检出" => UiIconKind.Switch,
        "清理" => UiIconKind.Cleanup,
        "仓库状态" => UiIconKind.RepoStatus,
        "还原" => UiIconKind.Revert,
        "解决冲突" => UiIconKind.Resolve,
        "获取" => UiIconKind.Fetch,
        "贮藏" => UiIconKind.Stash,
        "弹出贮藏" => UiIconKind.StashPop,
        _ => UiIconKind.RepoFolder
    };

    private void BrowseForRepositoryDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择任意位于 Git 仓库中的目录，程序会自动定位到对应的仓库根目录",
            InitialDirectory = Directory.Exists(_selectedDirectory) ? _selectedDirectory : _launchDirectory,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || !Directory.Exists(dialog.SelectedPath))
        {
            return;
        }

        var repoRoot = GitPathHelper.FindRepositoryRoot(dialog.SelectedPath);
        if (repoRoot is null)
        {
            SetStatus("所选目录不在 Git 仓库中。", isError: true);
            MessageBox.Show(
                "所选目录不在 Git 仓库中，无法加入仓库列表。",
                "未检测到 Git 仓库",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var entry = UpsertRepositoryEntry(repoRoot, displayName: null, moveToTop: true);
        SelectRepositoryEntry(entry, "已切换仓库根目录。", moveToTop: false, saveImmediately: true);
    }

    private void OpenRepositoryRootInExplorer()
    {
        if (_selectedRepository is null)
        {
            SetStatus("当前未选择有效的仓库根目录。", isError: true);
            MessageBox.Show(
                "当前未选择有效的 Git 仓库根目录，请先从下拉框选择，或点“选择仓库目录”。",
                "无法打开",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var repoRootPath = _selectedRepository.RepoRootPath;
        if (!Directory.Exists(repoRootPath))
        {
            SetStatus("当前仓库根目录不存在或暂时不可访问。", isError: true);
            MessageBox.Show(
                "当前选中的仓库根目录不存在或暂时不可访问，请先检查路径，或在“管理仓库列表”里删除该项。",
                "路径不可用",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(repoRootPath);

        try
        {
            Process.Start(startInfo);
            SetStatus($"已在文件资源管理器中打开仓库根目录: {repoRootPath}", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"打开仓库根目录失败: {ex.Message}", isError: true);
            MessageBox.Show(
                $"无法在文件资源管理器中打开仓库根目录。\r\n\r\n{ex.Message}",
                "打开失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenRepositoryManager()
    {
        using var dialog = new RepositoryListManagerForm(_repositoryEntries, _selectedRepository?.RepoRootPath);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _repositoryEntries.Clear();
        _repositoryEntries.AddRange(dialog.GetEntries());
        RepositoryHistoryStore.Save(_repositoryEntries);

        var selectedPath = dialog.SelectedRepoRootPath;
        var selectedEntry = selectedPath is null ? null : FindRepositoryEntryByPath(selectedPath);
        if (selectedEntry is not null)
        {
            SelectRepositoryEntry(selectedEntry, "已更新仓库列表。", moveToTop: false, saveImmediately: false);
        }
        else if (_repositoryEntries.Count > 0)
        {
            SelectRepositoryEntry(_repositoryEntries[0], "已更新仓库列表。", moveToTop: false, saveImmediately: false);
        }
        else
        {
            ClearSelectedRepository("仓库列表已清空。请先重新添加一个仓库目录。", isError: true);
            RefreshRepoRootComboBox(selectedRepoRootPath: null);
        }
    }

    private void SelectRepoRootFromComboBox()
    {
        if (_isUpdatingRepoComboBox)
        {
            return;
        }

        if (_repoRootComboBox.SelectedItem is RepositoryEntry entry)
        {
            SelectRepositoryEntry(entry, "已从下拉框切换仓库根目录。", moveToTop: false, saveImmediately: false);
        }
    }

    private void SelectRepositoryEntry(RepositoryEntry entry, string statusMessage, bool moveToTop, bool saveImmediately)
    {
        if (moveToTop)
        {
            UpsertRepositoryEntry(entry.RepoRootPath, entry.DisplayName, moveToTop: true);
        }

        _selectedRepository = FindRepositoryEntryByPath(entry.RepoRootPath) ?? entry;
        _selectedDirectory = _selectedRepository.RepoRootPath;
        _selectedDirectoryValue.Text = _selectedDirectory;
        RefreshRepoRootComboBox(_selectedRepository.RepoRootPath);

        if (saveImmediately)
        {
            RepositoryHistoryStore.Save(_repositoryEntries);
        }

        SetStatus($"{statusMessage} 当前仓库根目录: {_selectedRepository.RepoRootPath}", isError: false);
    }
    private void ClearSelectedRepository(string statusMessage, bool isError)
    {
        _selectedRepository = null;
        _selectedDirectory = _launchDirectory;
        _selectedDirectoryValue.Text = "未选择仓库根目录";
        RefreshRepoRootComboBox(selectedRepoRootPath: null);
        SetStatus(statusMessage, isError);
    }

    private RepositoryEntry UpsertRepositoryEntry(string repoRootPath, string? displayName, bool moveToTop)
    {
        var normalizedPath = GitPathHelper.NormalizePath(repoRootPath);
        var existing = FindRepositoryEntryByPath(normalizedPath);

        if (existing is null)
        {
            existing = new RepositoryEntry
            {
                RepoRootPath = normalizedPath,
                DisplayName = displayName?.Trim() ?? string.Empty
            };

            if (moveToTop)
            {
                _repositoryEntries.Insert(0, existing);
            }
            else
            {
                _repositoryEntries.Add(existing);
            }
        }
        else
        {
            if (displayName is not null)
            {
                existing.DisplayName = displayName.Trim();
            }

            if (moveToTop)
            {
                _repositoryEntries.Remove(existing);
                _repositoryEntries.Insert(0, existing);
            }
        }

        if (_repositoryEntries.Count > 20)
        {
            _repositoryEntries.RemoveRange(20, _repositoryEntries.Count - 20);
        }

        return existing;
    }

    private RepositoryEntry? FindRepositoryEntryByPath(string repoRootPath)
    {
        var normalizedPath = GitPathHelper.NormalizePath(repoRootPath);
        return _repositoryEntries.FirstOrDefault(entry =>
            string.Equals(entry.RepoRootPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshRepoRootComboBox(string? selectedRepoRootPath)
    {
        _isUpdatingRepoComboBox = true;
        try
        {
            _repoRootComboBox.BeginUpdate();
            _repoRootComboBox.Items.Clear();

            foreach (var entry in _repositoryEntries)
            {
                _repoRootComboBox.Items.Add(entry);
            }

            if (selectedRepoRootPath is not null)
            {
                var normalizedPath = GitPathHelper.NormalizePath(selectedRepoRootPath);
                var selectedIndex = -1;

                for (var index = 0; index < _repoRootComboBox.Items.Count; index++)
                {
                    if (_repoRootComboBox.Items[index] is RepositoryEntry entry &&
                        string.Equals(entry.RepoRootPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }

                _repoRootComboBox.SelectedIndex = selectedIndex;
            }
            else
            {
                _repoRootComboBox.SelectedIndex = -1;
            }
        }
        finally
        {
            _repoRootComboBox.EndUpdate();
            _isUpdatingRepoComboBox = false;
        }
    }

    private void ExecuteTortoiseGitCommand(CommandButton command)
    {
        if (_selectedRepository is null)
        {
            SetStatus("当前未选择有效的仓库根目录。", isError: true);
            MessageBox.Show(
                "当前未选择有效的 Git 仓库根目录，请先从下拉框选择，或点“选择仓库目录”。",
                "无法执行",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!Directory.Exists(_selectedRepository.RepoRootPath))
        {
            SetStatus("当前仓库根目录不存在或暂时不可访问。", isError: true);
            MessageBox.Show(
                "当前选中的仓库根目录不存在或暂时不可访问，请先检查路径，或在“管理仓库列表”里删除该项。",
                "路径不可用",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!TortoiseGitLocator.TryFindExecutable(out var executablePath))
        {
            SetStatus("未找到 TortoiseGitProc.exe。", isError: true);
            MessageBox.Show(
                "没有找到 TortoiseGitProc.exe。\r\n请确认已经安装 TortoiseGit，或把它加入 PATH。",
                "未找到 TortoiseGit",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        var targetPath = command.Scope == CommandScope.RepositoryRoot
            ? _selectedRepository.RepoRootPath
            : _selectedDirectory;

        var arguments = $"/command:{command.Command} /path:\"{targetPath}\"";
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = targetPath,
            UseShellExecute = true
        };

        try
        {
            Process.Start(startInfo);
            SetStatus($"已打开 {command.Title}，目标路径: {targetPath}", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"启动失败: {ex.Message}", isError: true);
            MessageBox.Show(
                $"启动 TortoiseGit 命令失败。\r\n\r\n{ex.Message}",
                "启动失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        _statusValue.Text = message;
        _statusValue.ForeColor = isError
            ? Color.FromArgb(180, 45, 30)
            : Color.FromArgb(23, 112, 41);
    }
}

internal sealed record CommandSection(string Title, Color AccentColor, string[] CommandTitles);

internal sealed class CardPanel : Panel
{
    public Color FillColor = Color.White;

    public Color BorderColor = Color.FromArgb(229, 235, 243);

    public int CornerRadius = 16;

    public CardPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Margin = new Padding(0);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;

        using var path = CreateRoundedPath(rect, CornerRadius);
        using var brush = new SolidBrush(FillColor);
        using var pen = new Pen(BorderColor);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
        base.OnPaint(e);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal enum UiIconKind
{
    Brand,
    SidebarRepo,
    RepoFolder,
    ChooseFolder,
    ManageList,
    Info,
    Commit,
    Pull,
    Push,
    Sync,
    Switch,
    Cleanup,
    RepoStatus,
    Revert,
    Resolve,
    Fetch,
    Stash,
    StashPop
}

internal static class IconFactory
{
    public static Bitmap Create(UiIconKind kind, Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(color, Math.Max(1.6f, size / 10F))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(color);

        switch (kind)
        {
            case UiIconKind.Brand:
                DrawBrand(graphics, pen, brush, size);
                break;
            case UiIconKind.SidebarRepo:
            case UiIconKind.RepoFolder:
                DrawFolder(graphics, pen, brush, size);
                break;
            case UiIconKind.ChooseFolder:
                DrawFolder(graphics, pen, brush, size);
                DrawPlus(graphics, pen, size, size * 0.72F, size * 0.72F, size * 0.14F);
                break;
            case UiIconKind.ManageList:
                DrawList(graphics, pen, brush, size);
                break;
            case UiIconKind.Info:
                DrawInfo(graphics, pen, brush, size);
                break;
            case UiIconKind.Commit:
                DrawCommit(graphics, pen, brush, size);
                break;
            case UiIconKind.Pull:
            case UiIconKind.Fetch:
                DrawArrowTray(graphics, pen, brush, size, upward: false);
                break;
            case UiIconKind.Push:
            case UiIconKind.StashPop:
                DrawArrowTray(graphics, pen, brush, size, upward: true);
                break;
            case UiIconKind.Sync:
                DrawSync(graphics, pen, brush, size);
                break;
            case UiIconKind.Switch:
                DrawSwitch(graphics, pen, brush, size);
                break;
            case UiIconKind.Cleanup:
                DrawBroom(graphics, pen, brush, size);
                break;
            case UiIconKind.RepoStatus:
                DrawStatus(graphics, pen, brush, size);
                break;
            case UiIconKind.Revert:
                DrawRevert(graphics, pen, brush, size);
                break;
            case UiIconKind.Resolve:
                DrawResolve(graphics, pen, brush, size);
                break;
            case UiIconKind.Stash:
                DrawStash(graphics, pen, brush, size);
                break;
        }

        return bitmap;
    }
    private static void DrawBrand(Graphics g, Pen pen, Brush brush, int size)
    {
        var p1 = new PointF(size * 0.28F, size * 0.32F);
        var p2 = new PointF(size * 0.66F, size * 0.32F);
        var p3 = new PointF(size * 0.48F, size * 0.68F);
        g.DrawLine(pen, p1, p2);
        g.DrawLine(pen, p1, p3);
        g.DrawLine(pen, p2, p3);
        var node = size * 0.16F;
        g.FillEllipse(brush, p1.X - node / 2, p1.Y - node / 2, node, node);
        g.FillEllipse(brush, p2.X - node / 2, p2.Y - node / 2, node, node);
        g.FillEllipse(brush, p3.X - node / 2, p3.Y - node / 2, node, node);
    }

    private static void DrawFolder(Graphics g, Pen pen, Brush brush, int size)
    {
        var top = new RectangleF(size * 0.16F, size * 0.26F, size * 0.30F, size * 0.16F);
        var body = new RectangleF(size * 0.12F, size * 0.36F, size * 0.76F, size * 0.42F);
        g.DrawRectangle(pen, top.X, top.Y, top.Width, top.Height);
        g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
    }

    private static void DrawList(Graphics g, Pen pen, Brush brush, int size)
    {
        for (var index = 0; index < 3; index++)
        {
            var y = size * (0.28F + index * 0.22F);
            g.FillEllipse(brush, size * 0.14F, y, size * 0.10F, size * 0.10F);
            g.DrawLine(pen, size * 0.32F, y + size * 0.05F, size * 0.82F, y + size * 0.05F);
        }
    }

    private static void DrawInfo(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawEllipse(pen, size * 0.18F, size * 0.18F, size * 0.64F, size * 0.64F);
        g.FillEllipse(brush, size * 0.46F, size * 0.30F, size * 0.08F, size * 0.08F);
        g.DrawLine(pen, size * 0.50F, size * 0.42F, size * 0.50F, size * 0.66F);
    }

    private static void DrawCommit(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawRectangle(pen, size * 0.20F, size * 0.18F, size * 0.46F, size * 0.60F);
        g.DrawLine(pen, size * 0.54F, size * 0.18F, size * 0.74F, size * 0.38F);
        g.DrawLine(pen, size * 0.66F, size * 0.30F, size * 0.74F, size * 0.38F);
        g.DrawLine(pen, size * 0.74F, size * 0.38F, size * 0.66F, size * 0.46F);
        g.DrawLine(pen, size * 0.30F, size * 0.56F, size * 0.42F, size * 0.66F);
        g.DrawLine(pen, size * 0.42F, size * 0.66F, size * 0.68F, size * 0.36F);
    }

    private static void DrawArrowTray(Graphics g, Pen pen, Brush brush, int size, bool upward)
    {
        var trayY = size * 0.70F;
        g.DrawLine(pen, size * 0.22F, trayY, size * 0.78F, trayY);
        g.DrawLine(pen, size * 0.22F, trayY, size * 0.22F, size * 0.54F);
        g.DrawLine(pen, size * 0.78F, trayY, size * 0.78F, size * 0.54F);

        if (upward)
        {
            g.DrawLine(pen, size * 0.50F, size * 0.72F, size * 0.50F, size * 0.24F);
            g.DrawLine(pen, size * 0.50F, size * 0.24F, size * 0.36F, size * 0.38F);
            g.DrawLine(pen, size * 0.50F, size * 0.24F, size * 0.64F, size * 0.38F);
        }
        else
        {
            g.DrawLine(pen, size * 0.50F, size * 0.20F, size * 0.50F, size * 0.64F);
            g.DrawLine(pen, size * 0.50F, size * 0.64F, size * 0.36F, size * 0.50F);
            g.DrawLine(pen, size * 0.50F, size * 0.64F, size * 0.64F, size * 0.50F);
        }
    }

    private static void DrawSync(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawArc(pen, size * 0.18F, size * 0.22F, size * 0.42F, size * 0.42F, 30, 240);
        g.DrawLine(pen, size * 0.52F, size * 0.20F, size * 0.66F, size * 0.24F);
        g.DrawLine(pen, size * 0.52F, size * 0.20F, size * 0.58F, size * 0.34F);
        g.DrawArc(pen, size * 0.40F, size * 0.36F, size * 0.42F, size * 0.42F, 210, 240);
        g.DrawLine(pen, size * 0.48F, size * 0.80F, size * 0.34F, size * 0.76F);
        g.DrawLine(pen, size * 0.48F, size * 0.80F, size * 0.42F, size * 0.66F);
    }

    private static void DrawSwitch(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.30F, size * 0.22F, size * 0.30F, size * 0.72F);
        g.DrawLine(pen, size * 0.30F, size * 0.34F, size * 0.60F, size * 0.34F);
        g.DrawLine(pen, size * 0.30F, size * 0.60F, size * 0.60F, size * 0.72F);
        var node = size * 0.12F;
        g.FillEllipse(brush, size * 0.24F, size * 0.16F, node, node);
        g.FillEllipse(brush, size * 0.54F, size * 0.28F, node, node);
        g.FillEllipse(brush, size * 0.54F, size * 0.66F, node, node);
    }

    private static void DrawBroom(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.66F, size * 0.18F, size * 0.38F, size * 0.52F);
        g.DrawLine(pen, size * 0.34F, size * 0.56F, size * 0.56F, size * 0.78F);
        g.DrawLine(pen, size * 0.28F, size * 0.62F, size * 0.50F, size * 0.84F);
        g.DrawLine(pen, size * 0.40F, size * 0.50F, size * 0.62F, size * 0.72F);
    }

    private static void DrawStatus(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.22F, size * 0.28F, size * 0.78F, size * 0.28F);
        g.DrawLine(pen, size * 0.22F, size * 0.48F, size * 0.66F, size * 0.48F);
        g.DrawLine(pen, size * 0.22F, size * 0.68F, size * 0.58F, size * 0.68F);
    }

    private static void DrawRevert(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawArc(pen, size * 0.22F, size * 0.24F, size * 0.52F, size * 0.46F, 165, 210);
        g.DrawLine(pen, size * 0.28F, size * 0.30F, size * 0.18F, size * 0.44F);
        g.DrawLine(pen, size * 0.28F, size * 0.30F, size * 0.40F, size * 0.30F);
    }

    private static void DrawResolve(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawLine(pen, size * 0.24F, size * 0.24F, size * 0.76F, size * 0.76F);
        g.DrawLine(pen, size * 0.76F, size * 0.24F, size * 0.24F, size * 0.76F);
        g.FillEllipse(brush, size * 0.18F, size * 0.18F, size * 0.14F, size * 0.14F);
        g.FillEllipse(brush, size * 0.68F, size * 0.68F, size * 0.14F, size * 0.14F);
    }

    private static void DrawStash(Graphics g, Pen pen, Brush brush, int size)
    {
        g.DrawRectangle(pen, size * 0.20F, size * 0.34F, size * 0.60F, size * 0.36F);
        g.DrawLine(pen, size * 0.28F, size * 0.34F, size * 0.34F, size * 0.20F);
        g.DrawLine(pen, size * 0.72F, size * 0.34F, size * 0.66F, size * 0.20F);
        g.DrawLine(pen, size * 0.34F, size * 0.20F, size * 0.66F, size * 0.20F);
    }

    private static void DrawPlus(Graphics g, Pen pen, float size, float centerX, float centerY, float halfLength)
    {
        g.DrawLine(pen, centerX - halfLength, centerY, centerX + halfLength, centerY);
        g.DrawLine(pen, centerX, centerY - halfLength, centerX, centerY + halfLength);
    }
}
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

internal sealed class RepositoryEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string RepoRootPath { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? RepoRootPath
            : $"{DisplayName} [{RepoRootPath}]";

    public RepositoryEntry Clone() =>
        new()
        {
            DisplayName = DisplayName,
            RepoRootPath = RepoRootPath
        };

    public override string ToString() => DisplayLabel;
}

internal static class RepositoryHistoryStore
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TortoiseGitLauncher",
        "recent-repos.json");

    public static List<RepositoryEntry> Load()
    {
        try
        {
            if (!File.Exists(StoragePath))
            {
                return [];
            }

            var json = File.ReadAllText(StoragePath);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var entries = new List<RepositoryEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var path = element.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        entries.Add(new RepositoryEntry
                        {
                            RepoRootPath = GitPathHelper.NormalizePath(path)
                        });
                    }

                    continue;
                }

                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<RepositoryEntry>(element.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entry is null || string.IsNullOrWhiteSpace(entry.RepoRootPath))
                {
                    continue;
                }

                entry.RepoRootPath = GitPathHelper.NormalizePath(entry.RepoRootPath);
                entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
                entries.Add(entry);
            }

            var uniqueEntries = new List<RepositoryEntry>();
            foreach (var entry in entries)
            {
                if (uniqueEntries.Any(existing =>
                        string.Equals(existing.RepoRootPath, entry.RepoRootPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                uniqueEntries.Add(entry);
            }

            return uniqueEntries;
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IReadOnlyCollection<RepositoryEntry> repoEntries)
    {
        try
        {
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(repoEntries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var utf8NoBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(StoragePath, json, utf8NoBom);
        }
        catch
        {
            // 历史记录保存失败不影响主功能。
        }
    }
}

internal static class TortoiseGitLocator
{
    public static bool TryFindExecutable(out string executablePath)
    {
        var candidates = new List<string>();
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pathEntry in pathEntries)
        {
            candidates.Add(Path.Combine(pathEntry, "TortoiseGitProc.exe"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "TortoiseGit", "bin", "TortoiseGitProc.exe"));
        }

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            candidates.Add(Path.Combine(programFilesX86, "TortoiseGit", "bin", "TortoiseGitProc.exe"));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                executablePath = candidate;
                return true;
            }
        }

        executablePath = string.Empty;
        return false;
    }
}

internal static class GitPathHelper
{
    public static string? FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);

        while (current is not null)
        {
            var dotGitDirectory = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(dotGitDirectory) || File.Exists(dotGitDirectory))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    public static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

internal enum CommandScope
{
    WorkingDirectory,
    RepositoryRoot
}

internal sealed record CommandButton(string Title, string Command, CommandScope Scope);
