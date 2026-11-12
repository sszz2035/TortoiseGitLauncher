using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed partial class MainForm : Form
{
    private Panel _pageHost = null!;
    private Button _repositoryNavigationButton = null!;
    private Button _scriptRunnerNavigationButton = null!;
    private Control _repositoryManagementPage = null!;
    private ScriptRunnerPage _scriptRunnerPage = null!;

    private RepositoryDetailsControl _repositoryDetails = null!;
    private readonly System.Windows.Forms.Timer _repositoryNotesSaveTimer = new() { Interval = 500 };
    private bool _repositoryNotesDirty;
    private ComboBox _repoRootComboBox = null!;
    private DirectoryPageSelectorButton _repositoryPageSelector = null!;
    private Button _openRepositoryRootButton = null!;
    private readonly List<Button> _repositoryCommandButtons = [];
    private Label _statusValue = null!;
    private LinkLabel _explorerMenuLink = null!;
    private readonly ContextMenuStrip _explorerMenu = new();
    private readonly Queue<ExplorerAddRequest> _explorerRequests = new();
    private readonly System.Windows.Forms.Timer _explorerRequestTimer = new() { Interval = 250 };
    private bool _handlingExplorerRequest;
    private readonly string _launchDirectory;
    private readonly RepositoryHistorySettings _repositorySettings;
    private List<RepositoryEntry> _repositoryEntries => _repositorySettings.Entries;
    private string _selectedDirectory;
    private RepositoryEntry? _selectedRepository;
    private bool _isUpdatingRepoComboBox;

    private static readonly CommandButton[] CommandButtons =
    [
        new("提交", "commit", CommandScope.WorkingDirectory),
        new("比较差异", "showcompare", CommandScope.WorkingDirectory),
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

    public MainForm(bool suppressInitialDirectoryFallback = false)
    {
        string? contextMenuWarning = null;
        try { if (!ExplorerContextMenu.IsDisabled) ExplorerContextMenu.Register(); }
        catch (Exception ex) { contextMenuWarning = $"资源管理器右键菜单注册失败：{ex.Message}"; }

        _launchDirectory = Environment.CurrentDirectory;
        _selectedDirectory = _launchDirectory;
        _repositorySettings = RepositoryHistoryStore.LoadSettings(out var repositoryLoadWarning);

        var detectedRepoRoot = GitPathHelper.FindRepositoryRoot(_launchDirectory);
        RepositoryEntry? initialSelection = null;
        var initialStatus = string.Empty;
        var initialStatusIsError = false;
        var saveLoadedEntries = false;

        if (!suppressInitialDirectoryFallback && _repositorySettings.AllowInitialDirectoryFallback &&
            _repositorySettings.SaveBlockedReason is null && detectedRepoRoot is not null)
        {
            initialSelection = FindRepositoryEntryByPath(detectedRepoRoot);
            if (initialSelection is null)
            {
                initialSelection = UpsertRepositoryEntry(detectedRepoRoot, displayName: null, moveToTop: true);
                saveLoadedEntries = true;
            }

            initialStatus = string.IsNullOrWhiteSpace(repositoryLoadWarning)
                ? "已自动定位到当前仓库根目录。"
                : repositoryLoadWarning;
        }
        else if (DirectoryPageSelection.Restore(_repositorySettings.ActivePage.LastSelectedDirectoryPath,
                     _repositoryEntries, entry => entry.RepoRootPath) is { } rememberedEntry)
        {
            initialSelection = rememberedEntry;
            initialStatus = string.IsNullOrWhiteSpace(repositoryLoadWarning)
                ? "已载入最近使用的仓库根目录。"
                : repositoryLoadWarning;
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
        shellLayout.Controls.Add(CreatePageHost(initialSelection?.RepoRootPath, suppressInitialDirectoryFallback), 1, 0);
        Controls.Add(shellLayout);

        _repositoryNotesSaveTimer.Tick += (_, _) => SaveRepositoryNotes();
        Activated += (_, _) => _ = _repositoryDetails.RefreshBranchAsync();
        _explorerRequestTimer.Tick += (_, _) => ProcessNextExplorerRequest();
        FormClosed += (_, _) =>
        {
            _repositoryNotesSaveTimer.Dispose();
            _explorerRequestTimer.Dispose();
            _explorerMenu.Dispose();
        };

        RefreshRepoRootComboBox(selectedRepoRootPath: null);

        if (initialSelection is not null)
        {
            SelectRepositoryEntry(initialSelection, initialStatus, moveToTop: false,
                saveImmediately: saveLoadedEntries || _repositorySettings.SaveBlockedReason is null);
        }
        else
        {
            ClearSelectedRepository(initialStatus, initialStatusIsError);
        }

        if (contextMenuWarning is not null) SetStatus(contextMenuWarning, isError: true);
    }

    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        if (!SaveRepositoryNotes())
        {
            eventArgs.Cancel = true;
            MessageBox.Show(this, "仓库备注保存失败，请检查配置目录的写入权限后重试。", "无法保存备注",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var runningCount = _scriptRunnerPage.RunningInstanceCount;
        if (eventArgs.CloseReason == CloseReason.UserClosing &&
            runningCount > 0)
        {
            var result = MessageBox.Show(
                this,
                $"仍有 {runningCount} 个脚本实例正在运行。" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "确认退出后，这些实例及其子进程将被终止。",
                "确认退出",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                eventArgs.Cancel = true;
                return;
            }

            _scriptRunnerPage.StopAllRunningInstances();
        }

        base.OnFormClosing(eventArgs);
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
            RowCount = 6,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
        _repositoryNavigationButton = CreateSidebarButton("仓库管理", UiIconKind.SidebarRepo, isActive: true);
        _scriptRunnerNavigationButton = CreateSidebarButton("脚本执行", UiIconKind.ScriptRunner, isActive: false);
        _repositoryNavigationButton.Click += (_, _) => ShowPage(_repositoryManagementPage);
        _scriptRunnerNavigationButton.Click += (_, _) => ShowPage(_scriptRunnerPage);
        layout.Controls.Add(_repositoryNavigationButton, 0, 1);
        layout.Controls.Add(_scriptRunnerNavigationButton, 0, 2);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 3);
        _explorerMenuLink = new LinkLabel
        {
            Text = "管理资源管理器右键菜单…",
            AutoSize = true,
            MaximumSize = new Size(168, 0),
            Margin = new Padding(4, 12, 4, 0)
        };
        _explorerMenuLink.LinkClicked += (_, _) => ShowExplorerMenuOptions();
        layout.Controls.Add(_explorerMenuLink, 0, 4);
        layout.Controls.Add(new Label
        {
            Text = "仓库操作与自定义脚本集中管理。",
            AutoSize = true,
            MaximumSize = new Size(168, 0),
            ForeColor = Color.FromArgb(102, 110, 122),
            Margin = new Padding(4, 14, 4, 0)
        }, 0, 5);

        sidebarCard.Controls.Add(layout);
        return sidebarCard;
    }

    private void ShowExplorerMenuOptions()
    {
        var menu = _explorerMenu;
        if (menu.Items.Count > 0)
        {
            menu.Show(_explorerMenuLink, new Point(0, _explorerMenuLink.Height));
            return;
        }
        menu.Items.Add("启用/更新右键菜单", null, (_, _) =>
        {
            try
            {
                ExplorerContextMenu.Register();
                MessageBox.Show(this, "文件夹右键菜单已启用。", "资源管理器右键菜单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "启用失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        menu.Items.Add("移除右键菜单", null, (_, _) =>
        {
            try
            {
                ExplorerContextMenu.Unregister();
                MessageBox.Show(this, "文件夹右键菜单已移除。", "资源管理器右键菜单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "移除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        menu.Show(_explorerMenuLink, new Point(0, _explorerMenuLink.Height));
    }

    internal void HandleExplorerRequest(string[] args)
    {
        if (IsDisposed || Disposing) return;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        if (Enabled) Activate();
        if (args.Length == 0) return;
        if (!ExplorerAddRequest.TryParse(args, out var request) || request is null) return;
        _explorerRequests.Enqueue(request);
        _explorerRequestTimer.Start();
    }

    private void ProcessNextExplorerRequest()
    {
        // List managers own editing snapshots; importing must wait until their dialogs close.
        if (!Enabled || _handlingExplorerRequest || IsDisposed || Disposing) return;
        if (!_explorerRequests.TryDequeue(out var request))
        {
            _explorerRequestTimer.Stop();
            return;
        }
        _handlingExplorerRequest = true;
        try
        {
            ProcessExplorerRequest(request);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法添加目录：{ex.Message}", "添加失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _handlingExplorerRequest = false;
            if (_explorerRequests.Count == 0) _explorerRequestTimer.Stop();
        }
    }

    private void ProcessExplorerRequest(ExplorerAddRequest request)
    {
        if (!Directory.Exists(request.Path))
        {
            MessageBox.Show(this, $"目录不存在或不可访问：{request.Path}", "无法添加目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (request.Target == ExplorerAddTarget.Repository)
        {
            AddRepositoryFromExplorer(request.Path);
            return;
        }

        ShowPage(_scriptRunnerPage);
        if (!_scriptRunnerPage.AddDirectoryFromExplorer(request.Path, out var message))
            MessageBox.Show(this, message, "无法添加执行目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void AddRepositoryFromExplorer(string path)
    {
        if (!SaveRepositoryNotes()) return;
        var repositoryRoot = GitPathHelper.FindRepositoryRoot(path);
        if (repositoryRoot is null)
        {
            ShowPage(_repositoryManagementPage);
            MessageBox.Show(this, "所选目录不属于 Git 仓库，无法添加到仓库管理。", "不是 Git 仓库",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ShowPage(_repositoryManagementPage);
        using var dialog = new ExplorerDirectoryTargetForm("仓库管理", repositoryRoot,
            _repositorySettings.Pages, _repositorySettings.ActivePage.Id);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedPageId is null) return;
        if (!SaveRepositoryNotes()) return;

        var normalizedPath = GitPathHelper.NormalizePath(repositoryRoot);
        var pages = _repositorySettings.Pages.Select(page => page.Clone()).ToList();
        var page = pages.Single(page => page.Id == dialog.SelectedPageId);
        var entry = page.Entries.FirstOrDefault(item => string.Equals(
            item.RepoRootPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new RepositoryEntry
            {
                RepoRootPath = normalizedPath,
                DisplayOrder = page.Groups.Select(group => group.DisplayOrder)
                    .Concat(page.Entries.Where(item => string.IsNullOrWhiteSpace(item.GroupId))
                        .Select(item => item.DisplayOrder)).DefaultIfEmpty(-1).Max() + 1
            };
            page.Entries.Add(entry);
        }
        page.LastSelectedDirectoryPath = entry.RepoRootPath;
        var error = ApplyRepositoryDirectoryPages(pages, page.Id);
        if (error is null)
        {
            SetStatus("已从资源管理器添加并选中仓库。", isError: false);
            return;
        }
        MessageBox.Show(this, $"保存仓库配置失败，原配置未更改。{Environment.NewLine}{error}", "添加失败",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private Control CreatePageHost(string? initialRepositoryRootPath, bool suppressInitialDirectoryFallback)
    {
        _pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        _repositoryManagementPage = CreateRepositoryManagementPage();
        _repositoryManagementPage.Dock = DockStyle.Fill;
        _scriptRunnerPage = new ScriptRunnerPage(_launchDirectory, initialRepositoryRootPath, suppressInitialDirectoryFallback)
        {
            Dock = DockStyle.Fill,
            Visible = false
        };

        _pageHost.Controls.Add(_scriptRunnerPage);
        _pageHost.Controls.Add(_repositoryManagementPage);
        return _pageHost;
    }

    private void ShowPage(Control page)
    {
        foreach (Control pageControl in _pageHost.Controls)
        {
            pageControl.Visible = ReferenceEquals(pageControl, page);
        }

        page.BringToFront();
        if (ReferenceEquals(page, _repositoryManagementPage))
        {
            _ = _repositoryDetails.RefreshBranchAsync();
        }
        SetSidebarButtonActive(_repositoryNavigationButton, ReferenceEquals(page, _repositoryManagementPage));
        SetSidebarButtonActive(_scriptRunnerNavigationButton, ReferenceEquals(page, _scriptRunnerPage));
    }

    private static void SetSidebarButtonActive(Button button, bool isActive)
    {
        button.BackColor = isActive ? Color.FromArgb(219, 233, 252) : Color.White;
        button.ForeColor = Color.FromArgb(25, 30, 40);
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

    private static PictureBox CreateIconBox(UiIconKind kind, Color color, int boxSize, Color backgroundColor) =>
        new()
        {
            Size = new Size(boxSize, boxSize),
            Image = IconFactory.Create(kind, color, Math.Max(18, boxSize - 18)),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = backgroundColor,
            Margin = new Padding(0)
        };

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
