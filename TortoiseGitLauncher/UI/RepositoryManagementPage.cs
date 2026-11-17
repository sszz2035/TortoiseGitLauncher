using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class RepositoryManagementPage : UserControl
{
    private readonly string _launchDirectory;
    private readonly RepositoryHistorySettings _repositorySettings;
    private List<RepositoryEntry> _repositoryEntries => _repositorySettings.Entries;
    private string _selectedDirectory;
    private RepositoryEntry? _selectedRepository;
    private bool _isUpdatingRepoComboBox;
    private RepositoryDetailsControl _repositoryDetails = null!;
    private readonly System.Windows.Forms.Timer _repositoryNotesSaveTimer = new() { Interval = 500 };
    private bool _repositoryNotesDirty;
    private ComboBox _repoRootComboBox = null!;
    private DirectoryPageSelectorButton _repositoryPageSelector = null!;
    private Button _openRepositoryRootButton = null!;
    private readonly List<Button> _repositoryCommandButtons = [];
    private Label _statusValue = null!;
    private ScriptRunnerPage _scriptRunnerPage = null!;
    internal void AttachScriptRunnerPage(ScriptRunnerPage page) => _scriptRunnerPage = page;
    internal string? InitialRepositoryRootPath => _selectedRepository?.RepoRootPath;

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

    internal RepositoryManagementPage(string launchDirectory, bool suppressInitialDirectoryFallback)
    {
        _launchDirectory = launchDirectory;
        _selectedDirectory = launchDirectory;
        _repositorySettings = RepositoryHistoryStore.LoadSettings(out var repositoryLoadWarning);
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Controls.Add(CreateRepositoryManagementPage());
        _repositoryNotesSaveTimer.Tick += (_, _) => SaveRepositoryNotes();

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
        if (!string.IsNullOrWhiteSpace(repositoryLoadWarning)) SetStatus(repositoryLoadWarning, isError: true);
    }

    internal void RefreshOnActivate() => _ = _repositoryDetails.RefreshBranchAsync();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _repositoryNotesSaveTimer.Dispose();
        base.Dispose(disposing);
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

        layout.Controls.Add(MainForm.CreateIconBox(UiIconKind.Brand, Color.FromArgb(46, 118, 255), 52, Color.FromArgb(236, 244, 255)), 0, 0);

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
        _repositoryPageSelector = new DirectoryPageSelectorButton("仓库根目录");
        _repositoryPageSelector.SetPages(_repositorySettings.Pages, _repositorySettings.ActivePage.Id);
        _repositoryPageSelector.PageSelected += SwitchRepositoryDirectoryPage;
        _repositoryPageSelector.ManagePagesRequested += (_, _) => OpenRepositoryPageManager();
        titleRow.Controls.Add(_repositoryPageSelector, 1, 0);
        var openRepositoryRootButton = CreateHeaderButton("打开根目录", Color.FromArgb(243, 247, 255));
        openRepositoryRootButton.MinimumSize = new Size(128, 34);
        openRepositoryRootButton.Padding = new Padding(9, 4, 9, 4);
        openRepositoryRootButton.Margin = new Padding(14, 0, 0, 0);
        openRepositoryRootButton.Image = IconFactory.Create(UiIconKind.RepoFolder, Color.FromArgb(59, 130, 246), 16);
        openRepositoryRootButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        openRepositoryRootButton.ImageAlign = ContentAlignment.MiddleLeft;
        openRepositoryRootButton.TextAlign = ContentAlignment.MiddleCenter;
        openRepositoryRootButton.Click += (_, _) => OpenRepositoryRootInExplorer();
        _openRepositoryRootButton = openRepositoryRootButton;
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
            FormattingEnabled = true,
            DropDownWidth = 720,
            Margin = new Padding(0, 0, 12, 0),
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };
        _repoRootComboBox.Format += (_, eventArgs) =>
        {
            if (eventArgs.ListItem is RepositoryGroupComboItem groupItem)
            {
                eventArgs.Value = groupItem.ToString();
            }
            else if (eventArgs.ListItem is RepositoryEntryComboItem entryItem)
            {
                var label = entryItem.ToString();
                eventArgs.Value = Directory.Exists(entryItem.Entry.RepoRootPath) ? label : $"{label}（不可用）";
            }
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

        _repositoryDetails = new RepositoryDetailsControl();
        _repositoryDetails.NotesChanged += (_, _) =>
        {
            if (_selectedRepository is null)
            {
                return;
            }

            _selectedRepository.Notes = _repositoryDetails.Notes;
            _repositoryNotesDirty = true;
            _repositoryNotesSaveTimer.Stop();
            _repositoryNotesSaveTimer.Start();
        };
        _repositoryDetails.NotesEditCompleted += (_, _) => SaveRepositoryNotes();
        layout.Controls.Add(_repositoryDetails, 0, 2);

        card.Controls.Add(layout);
        return card;
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
        _repositoryCommandButtons.Add(button);
        return button;
    }

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
        if (!SaveRepositoryNotes())
        {
            return;
        }

        using var dialog = new RepositoryListManagerForm(_repositoryEntries, _repositorySettings.Groups,
            _selectedRepository?.RepoRootPath, editor =>
            {
                var pages = editor.GetPages();
                var page = DirectoryPageCollection.GetActive(pages, _repositorySettings.LastSelectedPageId);
                page.Entries = editor.GetEntries();
                page.Groups = editor.GetGroups();
                page.LastSelectedDirectoryPath = editor.SelectedRepoRootPath ?? string.Empty;
                if (!editor.HasExecutionChanges) return ApplyRepositoryDirectoryPages(pages, page.Id);
                return editor.TransferSave.Save(
                    () => _scriptRunnerPage.ApplyTransferredDirectoryPages(editor.GetExecutionPages()),
                    () => ApplyRepositoryDirectoryPages(pages, page.Id));
            }, _repositorySettings.Pages, _repositorySettings.ActivePage.Id, _scriptRunnerPage.CloneDirectoryPages());
        dialog.ShowDialog(this);
    }

    private void SelectRepoRootFromComboBox()
    {
        if (_isUpdatingRepoComboBox)
        {
            return;
        }

        if (_repoRootComboBox.SelectedItem is RepositoryGroupComboItem groupItem)
        {
            groupItem.Group.IsExpanded = !groupItem.Group.IsExpanded;
            var saved = RepositoryHistoryStore.TrySave(_repositorySettings, out var errorMessage);
            if (!saved)
            {
                groupItem.Group.IsExpanded = !groupItem.Group.IsExpanded;
                SetStatus($"保存分组展开状态失败：{errorMessage}", isError: true);
            }
            var selectedPath = _selectedRepository?.RepoRootPath;
            var pageId = _repositorySettings.ActivePage.Id;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Disposing || _repositorySettings.ActivePage.Id != pageId)
                {
                    return;
                }

                RefreshRepoRootComboBox(selectedPath);
                _repoRootComboBox.DroppedDown = true;
            }));
            return;
        }

        if (TryGetRepositoryComboEntry(_repoRootComboBox.SelectedItem, out var entry))
        {
            SelectRepositoryEntry(entry, "已从下拉框切换仓库根目录。", moveToTop: false, saveImmediately: true);
        }
    }
    private void SelectRepositoryEntry(RepositoryEntry entry, string statusMessage, bool moveToTop, bool saveImmediately)
    {
        if (!SaveRepositoryNotes())
        {
            RefreshRepoRootComboBox(_selectedRepository?.RepoRootPath);
            return;
        }

        if (moveToTop)
        {
            UpsertRepositoryEntry(entry.RepoRootPath, entry.DisplayName, moveToTop: true);
        }

        _selectedRepository = FindRepositoryEntryByPath(entry.RepoRootPath) ?? entry;
        _selectedDirectory = _selectedRepository.RepoRootPath;
        _repositorySettings.ActivePage.LastSelectedDirectoryPath = _selectedDirectory;
        _repositoryDetails.SetRepository(_selectedDirectory, _selectedRepository.Notes);
        RefreshRepoRootComboBox(_selectedRepository.RepoRootPath);
        UpdateRepositoryActionState();

        if (saveImmediately && !RepositoryHistoryStore.TrySave(_repositorySettings, out var errorMessage))
        {
            SetStatus($"保存当前仓库选择失败：{errorMessage}", isError: true);
            return;
        }

        SetStatus($"{statusMessage} 当前仓库根目录: {_selectedRepository.RepoRootPath}", isError: false);
    }
    private void ClearSelectedRepository(string statusMessage, bool isError)
    {
        _selectedRepository = null;
        _selectedDirectory = string.Empty;
        _repositorySettings.ActivePage.LastSelectedDirectoryPath = string.Empty;
        _repositoryDetails.SetRepository(null, null);
        RefreshRepoRootComboBox(selectedRepoRootPath: null);
        UpdateRepositoryActionState();
        SetStatus(statusMessage, isError);
    }

    private void UpdateRepositoryActionState()
    {
        var available = _selectedRepository is not null && Directory.Exists(_selectedRepository.RepoRootPath);
        _openRepositoryRootButton.Enabled = available;
        foreach (var button in _repositoryCommandButtons) button.Enabled = available;
    }

    internal bool SaveRepositoryNotes()
    {
        _repositoryNotesSaveTimer.Stop();
        if (!_repositoryNotesDirty)
        {
            return true;
        }

        if (!RepositoryHistoryStore.TrySave(_repositorySettings, out var errorMessage))
        {
            SetStatus($"仓库备注保存失败：{errorMessage}", isError: true);
            return false;
        }

        _repositoryNotesDirty = false;
        return true;
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

            var groupedEntries = _repositoryEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.GroupId))
                .GroupBy(entry => entry.GroupId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group
                    .OrderBy(entry => entry.DisplayOrder)
                    .ToList(), StringComparer.OrdinalIgnoreCase);
            var topLevelItems = new List<(int Order, int Kind, object Item)>();
            foreach (var group in _repositorySettings.Groups
                         .OrderBy(group => group.DisplayOrder))
            {
                topLevelItems.Add((
                    group.DisplayOrder,
                    0,
                    new RepositoryGroupComboItem(group)));
            }

            foreach (var entry in _repositoryEntries
                         .Where(entry => string.IsNullOrWhiteSpace(entry.GroupId))
                         .OrderBy(entry => entry.DisplayOrder))
            {
                topLevelItems.Add((
                    entry.DisplayOrder,
                    1,
                    new RepositoryEntryComboItem(entry, indented: false)));
            }

            foreach (var item in topLevelItems
                         .OrderBy(item => item.Order)
                         .ThenBy(item => item.Kind))
            {
                _repoRootComboBox.Items.Add(item.Item);
                if (item.Item is RepositoryGroupComboItem groupItem &&
                    groupItem.Group.IsExpanded &&
                    groupedEntries.TryGetValue(groupItem.Group.Id, out var entries))
                {
                    foreach (var entry in entries)
                    {
                        _repoRootComboBox.Items.Add(
                            new RepositoryEntryComboItem(entry, indented: true));
                    }
                }
            }

            if (selectedRepoRootPath is not null)
            {
                var normalizedPath = GitPathHelper.NormalizePath(selectedRepoRootPath);
                var selectedIndex = -1;

                for (var index = 0; index < _repoRootComboBox.Items.Count; index++)
                {
                    if (TryGetRepositoryComboEntry(
                            _repoRootComboBox.Items[index],
                            out var entry) &&
                        string.Equals(
                            entry.RepoRootPath,
                            normalizedPath,
                            StringComparison.OrdinalIgnoreCase))
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

    private static bool TryGetRepositoryComboEntry(
        object? item,
        out RepositoryEntry entry)
    {
        if (item is RepositoryEntryComboItem entryItem)
        {
            entry = entryItem.Entry;
            return true;
        }

        if (item is RepositoryEntry directEntry)
        {
            entry = directEntry;
            return true;
        }

        entry = null!;
        return false;
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

    internal void SetStatus(string message, bool isError)
    {
        _statusValue.Text = message;
        _statusValue.ForeColor = isError
            ? Color.FromArgb(180, 45, 30)
            : Color.FromArgb(23, 112, 41);
    }

    private void SwitchRepositoryDirectoryPage(string id)
    {
        if (string.Equals(id, _repositorySettings.ActivePage.Id, StringComparison.OrdinalIgnoreCase) ||
            !SaveRepositoryNotes()) return;

        var error = ApplyRepositoryDirectoryPages(_repositorySettings.Pages, id);
        if (error is not null)
        {
            SetStatus($"切换目录页失败：{error}", isError: true);
            MessageBox.Show(this, error, "无法保存目录页", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenRepositoryPageManager()
    {
        if (!SaveRepositoryNotes()) return;
        using var dialog = new DirectoryPageManagerForm<RepositoryDirectoryPage>(
            "仓库管理", _repositorySettings.Pages, _repositorySettings.ActivePage.Id,
            page => page.Clone(), ApplyRepositoryDirectoryPages);
        dialog.ShowDialog(this);
    }

    // Save a candidate first, so failed writes cannot replace the active page or its notes.
    private string? ApplyRepositoryDirectoryPages(List<RepositoryDirectoryPage> pages, string selectedId)
    {
        var candidate = _repositorySettings.Clone();
        candidate.Pages = pages.Select(page => page.Clone()).ToList();
        candidate.LastSelectedPageId = selectedId;
        candidate.AllowInitialDirectoryFallback = false;
        var selected = DirectoryPageSelection.Restore(candidate.ActivePage.LastSelectedDirectoryPath,
            candidate.Entries, entry => entry.RepoRootPath);
        candidate.ActivePage.LastSelectedDirectoryPath = selected?.RepoRootPath ?? string.Empty;
        if (!RepositoryHistoryStore.TrySave(candidate, out var errorMessage)) return errorMessage ?? "保存失败。";

        _repositorySettings.Pages = candidate.Pages;
        _repositorySettings.LastSelectedPageId = candidate.LastSelectedPageId;
        _repositorySettings.AllowInitialDirectoryFallback = false;
        RestoreRepositoryDirectoryPage();
        return null;
    }

    private void RestoreRepositoryDirectoryPage()
    {
        _repositoryPageSelector.SetPages(_repositorySettings.Pages, _repositorySettings.ActivePage.Id);
        var selected = DirectoryPageSelection.Restore(_repositorySettings.ActivePage.LastSelectedDirectoryPath,
            _repositoryEntries, entry => entry.RepoRootPath);
        if (selected is null)
        {
            ClearSelectedRepository($"目录页“{_repositorySettings.ActivePage.Name}”没有可用仓库，请选择仓库目录。", isError: true);
        }
        else
        {
            SelectRepositoryEntry(selected, $"已切换到目录页“{_repositorySettings.ActivePage.Name}”。",
                moveToTop: false, saveImmediately: false);
        }
    }
    internal void AddRepositoryFromExplorer(string path)
    {
        if (!SaveRepositoryNotes()) return;
        var repositoryRoot = GitPathHelper.FindRepositoryRoot(path);
        if (repositoryRoot is null)
        {
            MessageBox.Show(this, "所选目录不属于 Git 仓库，无法添加到仓库管理。", "不是 Git 仓库",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
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
    private static void SyncScrollPanelExtent(Panel scrollPanel, Control content)
    {
        content.PerformLayout();
        var contentHeight = content.GetPreferredSize(new Size(scrollPanel.ClientSize.Width, 0)).Height;
        if (contentHeight <= 0) contentHeight = content.Bottom;
        scrollPanel.AutoScrollMinSize = new Size(0, contentHeight);
        var maxScrollY = Math.Max(0, contentHeight - scrollPanel.ClientSize.Height);
        var currentScrollY = Math.Max(0, -scrollPanel.AutoScrollPosition.Y);
        if (currentScrollY > maxScrollY) scrollPanel.AutoScrollPosition = new Point(0, maxScrollY);
    }
}
