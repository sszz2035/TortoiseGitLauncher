using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed partial class MainForm : Form
{
    private Panel _pageHost = null!;
    private Button _repositoryNavigationButton = null!;
    private Button _scriptRunnerNavigationButton = null!;
    private RepositoryManagementPage _repositoryManagementPage = null!;
    private ScriptRunnerPage _scriptRunnerPage = null!;

    private LinkLabel _explorerMenuLink = null!;
    private readonly ContextMenuStrip _explorerMenu = new();
    private readonly Queue<ExplorerAddRequest> _explorerRequests = new();
    private readonly System.Windows.Forms.Timer _explorerRequestTimer = new() { Interval = 250 };
    private bool _handlingExplorerRequest;
    private readonly string _launchDirectory;

    public MainForm(bool suppressInitialDirectoryFallback = false)
    {
        string? contextMenuWarning = null;
        try { if (!ExplorerContextMenu.IsDisabled) ExplorerContextMenu.Register(); }
        catch (Exception ex) { contextMenuWarning = $"资源管理器右键菜单注册失败：{ex.Message}"; }

        _launchDirectory = Environment.CurrentDirectory;
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
        shellLayout.Controls.Add(CreatePageHost(suppressInitialDirectoryFallback), 1, 0);
        Controls.Add(shellLayout);

        Activated += (_, _) => _repositoryManagementPage.RefreshOnActivate();
        _explorerRequestTimer.Tick += (_, _) => ProcessNextExplorerRequest();
        FormClosed += (_, _) =>
        {
            _explorerRequestTimer.Dispose();
            _explorerMenu.Dispose();
        };
        if (contextMenuWarning is not null) _repositoryManagementPage.SetStatus(contextMenuWarning, isError: true);
    }
    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        if (!_repositoryManagementPage.SaveRepositoryNotes())
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
            if (!_repositoryManagementPage.SaveRepositoryNotes()) return;
            ShowPage(_repositoryManagementPage);
            _repositoryManagementPage.AddRepositoryFromExplorer(request.Path);
            return;
        }

        ShowPage(_scriptRunnerPage);
        if (!_scriptRunnerPage.AddDirectoryFromExplorer(request.Path, out var message))
            MessageBox.Show(this, message, "无法添加执行目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private Control CreatePageHost(bool suppressInitialDirectoryFallback)
    {
        _pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        _repositoryManagementPage = new RepositoryManagementPage(_launchDirectory, suppressInitialDirectoryFallback)
        {
            Dock = DockStyle.Fill
        };
        _scriptRunnerPage = new ScriptRunnerPage(
            _launchDirectory, _repositoryManagementPage.InitialRepositoryRootPath, suppressInitialDirectoryFallback)
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        _repositoryManagementPage.AttachScriptRunnerPage(_scriptRunnerPage);
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
            _repositoryManagementPage.RefreshOnActivate();
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

    internal static PictureBox CreateIconBox(UiIconKind kind, Color color, int boxSize, Color backgroundColor) =>
        new()
        {
            Size = new Size(boxSize, boxSize),
            Image = IconFactory.Create(kind, color, Math.Max(18, boxSize - 18)),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = backgroundColor,
            Margin = new Padding(0)
        };

}
