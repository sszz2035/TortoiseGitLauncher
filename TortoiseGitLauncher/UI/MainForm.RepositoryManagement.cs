using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed partial class MainForm : Form
{
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
                eventArgs.Value = entryItem.ToString();
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
        using var dialog = new RepositoryListManagerForm(_repositoryEntries, _repositorySettings.Groups, _selectedRepository?.RepoRootPath);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _repositoryEntries.Clear();
        _repositoryEntries.AddRange(dialog.GetEntries());
        _repositorySettings.Groups.Clear();
        _repositorySettings.Groups.AddRange(dialog.GetGroups());
        RepositoryHistoryStore.Save(_repositorySettings);

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

        if (_repoRootComboBox.SelectedItem is RepositoryGroupComboItem groupItem)
        {
            groupItem.Group.IsExpanded = !groupItem.Group.IsExpanded;
            RepositoryHistoryStore.Save(_repositorySettings);
            var selectedPath = _selectedRepository?.RepoRootPath;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Disposing)
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
            RepositoryHistoryStore.Save(_repositorySettings);
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

    private void SetStatus(string message, bool isError)
    {
        _statusValue.Text = message;
        _statusValue.ForeColor = isError
            ? Color.FromArgb(180, 45, 30)
            : Color.FromArgb(23, 112, 41);
    }
}
