using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class ScriptRunnerPage : UserControl
{
    private readonly string _launchDirectory;
    private readonly ScriptRunnerSettings _settings;
    private ComboBox _directoryComboBox = null!;
    private Label _selectedDirectoryValue = null!;
    private Label _directoryStatusLabel = null!;
    private ExecutionDirectoryEntry? _selectedDirectory;
    private bool _isUpdatingDirectoryComboBox;
    private Label _scriptConfigurationSummaryLabel = null!;
    private Label _scriptConfigurationStatusLabel = null!;
    private FlowLayoutPanel _scriptButtonsPanel = null!;
    private readonly ToolTip _scriptButtonToolTip = new()
    {
        AutoPopDelay = 12000,
        InitialDelay = 350,
        ReshowDelay = 150,
        ShowAlways = true
    };

    public ScriptRunnerPage(string launchDirectory, string? initialRepositoryRootPath)
    {
        _launchDirectory = ScriptRunnerPathHelper.NormalizeDirectoryPath(launchDirectory);
        _settings = ScriptRunnerStore.Load(out var loadWarning);

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
        contentLayout.Controls.Add(CreateDirectoryCard());
        contentLayout.Controls.Add(CreateScriptConfigurationCard());
        contentLayout.Controls.Add(CreatePlaceholderSectionCard(
            "运行实例与输出",
            UiIconKind.RepoStatus,
            Color.FromArgb(168, 85, 247),
            "暂无运行实例",
            220));

        contentLayout.Layout += (_, _) => SyncScrollExtent(scrollPanel, contentLayout);
        scrollPanel.SizeChanged += (_, _) => SyncScrollExtent(scrollPanel, contentLayout);
        scrollPanel.Controls.Add(contentLayout);
        Controls.Add(scrollPanel);

        InitializeDirectorySelection(initialRepositoryRootPath, persistSelection: loadWarning is null);
        if (!string.IsNullOrWhiteSpace(loadWarning))
        {
            SetDirectoryStatus(loadWarning, isError: true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scriptButtonToolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private Control CreateDirectoryCard()
    {
        var card = CreateCard(new Padding(20, 18, 20, 18));
        card.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        layout.Controls.Add(CreateSectionTitle(
            "执行目录",
            UiIconKind.RepoFolder,
            Color.FromArgb(59, 130, 246)), 0, 0);

        var selectionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        };
        selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _directoryComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = false,
            MaxDropDownItems = 12,
            FormattingEnabled = true,
            Margin = new Padding(0, 0, 12, 0),
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };
        _directoryComboBox.Format += (_, eventArgs) =>
        {
            if (eventArgs.ListItem is ExecutionDirectoryEntry entry)
            {
                eventArgs.Value = Directory.Exists(entry.DirectoryPath)
                    ? entry.DisplayLabel
                    : $"{entry.DisplayLabel}（不可用）";
            }
        };
        _directoryComboBox.SelectedIndexChanged += (_, _) => SelectDirectoryFromComboBox();
        selectionRow.Controls.Add(_directoryComboBox, 0, 0);

        var browseButton = CreateActionButton(
            "选择执行目录",
            UiIconKind.ChooseFolder,
            Color.FromArgb(59, 130, 246),
            Color.FromArgb(243, 247, 255));
        browseButton.Margin = new Padding(0, 0, 10, 0);
        browseButton.Click += (_, _) => BrowseForExecutionDirectory();
        selectionRow.Controls.Add(browseButton, 1, 0);

        var manageButton = CreateActionButton(
            "管理目录列表",
            UiIconKind.ManageList,
            Color.FromArgb(90, 99, 112),
            Color.FromArgb(245, 248, 242));
        manageButton.Click += (_, _) => OpenDirectoryManager();
        selectionRow.Controls.Add(manageButton, 2, 0);
        layout.Controls.Add(selectionRow, 0, 1);

        var currentPathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            BackColor = Color.Transparent
        };
        currentPathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        currentPathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        currentPathRow.Controls.Add(new Label
        {
            Text = "当前执行目录：",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(74, 82, 94),
            Margin = new Padding(0, 2, 8, 0)
        }, 0, 0);

        _selectedDirectoryValue = new Label
        {
            Text = "未选择执行目录",
            AutoSize = true,
            MaximumSize = new Size(980, 0),
            ForeColor = Color.FromArgb(52, 60, 72)
        };
        currentPathRow.Controls.Add(_selectedDirectoryValue, 1, 0);
        layout.Controls.Add(currentPathRow, 0, 2);

        _directoryStatusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1080, 0),
            Margin = new Padding(0, 10, 0, 0)
        };
        layout.Controls.Add(_directoryStatusLabel, 0, 3);

        card.Controls.Add(layout);
        return card;
    }

    private Control CreateScriptConfigurationCard()
    {
        var card = CreateCard(new Padding(20, 18, 20, 18));
        card.Margin = new Padding(0, 0, 0, 14);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent
        };
        layout.Controls.Add(CreateSectionTitle(
            "脚本按钮",
            UiIconKind.ScriptRunner,
            Color.FromArgb(34, 137, 74)), 0, 0);

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var createButton = CreateActionButton(
            "新建脚本",
            UiIconKind.ScriptRunner,
            Color.FromArgb(34, 137, 74),
            Color.FromArgb(237, 247, 240));
        createButton.Margin = new Padding(0, 0, 10, 0);
        createButton.Click += (_, _) => CreateScriptConfiguration();
        actionRow.Controls.Add(createButton);

        var manageButton = CreateActionButton(
            "管理脚本",
            UiIconKind.ManageList,
            Color.FromArgb(90, 99, 112),
            Color.FromArgb(245, 248, 242));
        manageButton.Click += (_, _) => OpenScriptConfigurationManager();
        actionRow.Controls.Add(manageButton);
        layout.Controls.Add(actionRow, 0, 1);

        _scriptConfigurationSummaryLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(82, 91, 104),
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(_scriptConfigurationSummaryLabel, 0, 2);

        _scriptButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 76,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _scriptButtonsPanel.Layout += (_, _) => UpdateScriptButtonPanelHeight();
        _scriptButtonsPanel.SizeChanged += (_, _) => UpdateScriptButtonPanelHeight();
        layout.Controls.Add(_scriptButtonsPanel, 0, 3);

        _scriptConfigurationStatusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1080, 0),
            Margin = new Padding(0, 4, 0, 0)
        };
        layout.Controls.Add(_scriptConfigurationStatusLabel, 0, 4);

        card.Controls.Add(layout);
        RefreshScriptConfigurationSummary();
        return card;
    }

    private void CreateScriptConfiguration()
    {
        using var dialog = new ScriptConfigurationForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ResultConfiguration is null)
        {
            return;
        }

        var configuration = dialog.ResultConfiguration;
        configuration.DisplayOrder = _settings.Scripts.Count;
        _settings.Scripts.Add(configuration);
        if (!PersistSettings(_scriptConfigurationStatusLabel))
        {
            _settings.Scripts.Remove(configuration);
            RefreshScriptConfigurationSummary();
            return;
        }

        RefreshScriptConfigurationSummary();
        SetScriptConfigurationStatus($"已创建脚本配置“{configuration.Name}”。", isError: false);
    }

    private void OpenScriptConfigurationManager()
    {
        using var dialog = new ScriptConfigurationManagerForm(_settings.Scripts);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var previousConfigurations = _settings.Scripts
            .Select(configuration => configuration.Clone())
            .ToList();
        _settings.Scripts.Clear();
        _settings.Scripts.AddRange(dialog.GetConfigurations());

        if (!PersistSettings(_scriptConfigurationStatusLabel))
        {
            _settings.Scripts.Clear();
            _settings.Scripts.AddRange(previousConfigurations);
            RefreshScriptConfigurationSummary();
            return;
        }

        RefreshScriptConfigurationSummary();
        SetScriptConfigurationStatus("已保存脚本配置修改。", isError: false);
    }

    private void RefreshScriptConfigurationSummary()
    {
        if (_scriptConfigurationSummaryLabel is null || _scriptButtonsPanel is null)
        {
            return;
        }

        _scriptButtonsPanel.SuspendLayout();
        try
        {
            foreach (Control existingControl in _scriptButtonsPanel.Controls.Cast<Control>().ToArray())
            {
                if (existingControl is Button { Image: not null } existingButton)
                {
                    existingButton.Image.Dispose();
                    existingButton.Image = null;
                }

                existingControl.Dispose();
            }

            _scriptButtonsPanel.Controls.Clear();
            if (_settings.Scripts.Count == 0)
            {
                _scriptConfigurationSummaryLabel.Text = "未配置脚本";
                _scriptButtonsPanel.Controls.Add(new Label
                {
                    Text = "尚未配置脚本",
                    AutoSize = false,
                    Size = new Size(260, 64),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(112, 120, 132),
                    BackColor = Color.FromArgb(248, 250, 253),
                    Margin = new Padding(0, 0, 14, 12)
                });
                return;
            }

            var invalidPathCount = _settings.Scripts.Count(configuration =>
                !File.Exists(configuration.ScriptPath));
            _scriptConfigurationSummaryLabel.Text = invalidPathCount == 0
                ? $"已配置 {_settings.Scripts.Count} 个脚本"
                : $"已配置 {_settings.Scripts.Count} 个脚本，其中 {invalidPathCount} 个路径无效";

            foreach (var configuration in _settings.Scripts.OrderBy(item => item.DisplayOrder))
            {
                _scriptButtonsPanel.Controls.Add(CreateScriptButton(configuration));
            }
        }
        finally
        {
            _scriptButtonsPanel.ResumeLayout(performLayout: true);
            UpdateScriptButtonPanelHeight();
        }
    }

    private Button CreateScriptButton(ScriptConfiguration configuration)
    {
        var scriptPathAvailable = File.Exists(configuration.ScriptPath);
        var executionDirectoryAvailable = _selectedDirectory is not null &&
                                          Directory.Exists(_selectedDirectory.DirectoryPath);
        var isAvailable = scriptPathAvailable && executionDirectoryAvailable;
        var button = new Button
        {
            Text = scriptPathAvailable
                ? configuration.Name
                : $"{configuration.Name}{Environment.NewLine}路径无效",
            Size = new Size(222, 64),
            Margin = new Padding(0, 0, 14, 12),
            Padding = new Padding(12, 8, 12, 8),
            Font = new Font("Microsoft YaHei UI", 10.3F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            UseCompatibleTextRendering = true,
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = scriptPathAvailable
                ? Color.FromArgb(241, 249, 239)
                : Color.FromArgb(251, 241, 239),
            ForeColor = scriptPathAvailable
                ? Color.FromArgb(36, 44, 58)
                : Color.FromArgb(162, 55, 42),
            Image = IconFactory.Create(
                UiIconKind.ScriptRunner,
                scriptPathAvailable
                    ? Color.FromArgb(44, 137, 62)
                    : Color.FromArgb(174, 82, 68),
                18),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Enabled = isAvailable,
            Tag = configuration
        };

        button.FlatAppearance.BorderColor = scriptPathAvailable
            ? Color.FromArgb(207, 232, 202)
            : Color.FromArgb(235, 205, 199);
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => HandleScriptButtonClick(configuration);

        var tooltipLines = new List<string>
        {
            configuration.Name,
            configuration.ScriptPath,
            ScriptConfigurationPathHelper.GetTypeDisplayName(configuration.ScriptType)
        };
        if (!string.IsNullOrWhiteSpace(configuration.Arguments))
        {
            tooltipLines.Add($"参数：{configuration.Arguments}");
        }

        if (!scriptPathAvailable)
        {
            tooltipLines.Add("脚本路径无效");
        }
        else if (!executionDirectoryAvailable)
        {
            tooltipLines.Add("当前执行目录不可用");
        }

        _scriptButtonToolTip.SetToolTip(button, string.Join(Environment.NewLine, tooltipLines));
        return button;
    }

    private void HandleScriptButtonClick(ScriptConfiguration configuration)
    {
        if (!File.Exists(configuration.ScriptPath))
        {
            RefreshScriptConfigurationSummary();
            SetScriptConfigurationStatus(
                $"脚本“{configuration.Name}”的路径不存在或暂时不可访问。",
                isError: true);
            return;
        }

        if (_selectedDirectory is null || !Directory.Exists(_selectedDirectory.DirectoryPath))
        {
            RefreshScriptConfigurationSummary();
            SetScriptConfigurationStatus("当前执行目录不可用，请先选择有效目录。", isError: true);
            return;
        }

        SetScriptConfigurationStatus(
            $"已选择脚本“{configuration.Name}”，执行目录：{_selectedDirectory.DirectoryPath}",
            isError: false);
    }

    private void UpdateScriptButtonPanelHeight()
    {
        if (_scriptButtonsPanel is null)
        {
            return;
        }

        var availableWidth = _scriptButtonsPanel.ClientSize.Width;
        if (availableWidth <= 0)
        {
            availableWidth = _scriptButtonsPanel.Width;
        }

        if (availableWidth <= 0)
        {
            return;
        }

        var rowWidth = 0;
        var rowHeight = 0;
        var targetHeight = 0;
        foreach (Control control in _scriptButtonsPanel.Controls)
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
        targetHeight = Math.Max(76, targetHeight);
        if (_scriptButtonsPanel.Height != targetHeight)
        {
            _scriptButtonsPanel.Height = targetHeight;
        }
    }

    private void SetScriptConfigurationStatus(string message, bool isError)
    {
        _scriptConfigurationStatusLabel.Text = message;
        _scriptConfigurationStatusLabel.ForeColor = isError
            ? Color.FromArgb(180, 45, 30)
            : Color.FromArgb(23, 112, 41);
    }
    private void InitializeDirectorySelection(string? initialRepositoryRootPath, bool persistSelection)
    {
        ExecutionDirectoryEntry? entry = null;
        var statusMessage = string.Empty;

        if (TryGetAvailablePath(_settings.LastSelectedDirectoryPath, out var lastSelectedPath))
        {
            entry = UpsertDirectory(lastSelectedPath, displayName: null, moveToTop: false);
            statusMessage = "已恢复上次使用的执行目录。";
        }
        else if (TryGetAvailablePath(initialRepositoryRootPath, out var repositoryRootPath))
        {
            entry = UpsertDirectory(repositoryRootPath, displayName: null, moveToTop: true);
            statusMessage = "已使用当前仓库根目录作为初始执行目录。";
        }
        else if (TryGetAvailablePath(_launchDirectory, out var launchDirectory))
        {
            entry = UpsertDirectory(launchDirectory, displayName: null, moveToTop: true);
            statusMessage = "已使用程序启动目录作为初始执行目录。";
        }
        else
        {
            entry = _settings.RecentDirectories.FirstOrDefault(item => Directory.Exists(item.DirectoryPath));
            statusMessage = entry is null
                ? "没有可用的执行目录，请先选择一个目录。"
                : "已选择最近目录列表中的可用目录。";
        }

        if (entry is null)
        {
            ClearSelectedDirectory(statusMessage, isError: true, saveImmediately: persistSelection);
            return;
        }

        SelectDirectoryEntry(
            entry,
            statusMessage,
            moveToTop: false,
            saveImmediately: persistSelection);
    }

    private void BrowseForExecutionDirectory()
    {
        var initialDirectory = _selectedDirectory is not null &&
                               Directory.Exists(_selectedDirectory.DirectoryPath)
            ? _selectedDirectory.DirectoryPath
            : _launchDirectory;

        using var dialog = new FolderBrowserDialog
        {
            Description = "选择脚本运行时使用的工作目录",
            InitialDirectory = initialDirectory,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || !Directory.Exists(dialog.SelectedPath))
        {
            return;
        }

        var entry = UpsertDirectory(dialog.SelectedPath, displayName: null, moveToTop: true);
        SelectDirectoryEntry(
            entry,
            "已切换执行目录。",
            moveToTop: false,
            saveImmediately: true);
    }

    private void OpenDirectoryManager()
    {
        using var dialog = new ExecutionDirectoryManagerForm(
            _settings.RecentDirectories,
            _selectedDirectory?.DirectoryPath);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.RecentDirectories.Clear();
        _settings.RecentDirectories.AddRange(dialog.GetEntries());

        var selectedEntry = string.IsNullOrWhiteSpace(dialog.SelectedDirectoryPath)
            ? null
            : FindDirectoryEntry(dialog.SelectedDirectoryPath);
        if (selectedEntry is not null)
        {
            SelectDirectoryEntry(
                selectedEntry,
                "已更新执行目录列表。",
                moveToTop: false,
                saveImmediately: true);
        }
        else if (_settings.RecentDirectories.Count > 0)
        {
            SelectDirectoryEntry(
                _settings.RecentDirectories[0],
                "已更新执行目录列表。",
                moveToTop: false,
                saveImmediately: true);
        }
        else
        {
            ClearSelectedDirectory(
                "执行目录列表已清空，请重新选择目录。",
                isError: true,
                saveImmediately: true);
        }
    }

    private void SelectDirectoryFromComboBox()
    {
        if (_isUpdatingDirectoryComboBox)
        {
            return;
        }

        if (_directoryComboBox.SelectedItem is ExecutionDirectoryEntry entry)
        {
            SelectDirectoryEntry(
                entry,
                Directory.Exists(entry.DirectoryPath)
                    ? "已从下拉框切换执行目录。"
                    : "所选执行目录不存在或暂时不可访问。",
                moveToTop: false,
                saveImmediately: true);
        }
    }

    private void SelectDirectoryEntry(
        ExecutionDirectoryEntry entry,
        string statusMessage,
        bool moveToTop,
        bool saveImmediately)
    {
        if (moveToTop)
        {
            UpsertDirectory(entry.DirectoryPath, entry.DisplayName, moveToTop: true);
        }

        _selectedDirectory = FindDirectoryEntry(entry.DirectoryPath) ?? entry;
        _settings.LastSelectedDirectoryPath = _selectedDirectory.DirectoryPath;
        _selectedDirectoryValue.Text = _selectedDirectory.DirectoryPath;
        RefreshDirectoryComboBox(_selectedDirectory.DirectoryPath);
        RefreshScriptConfigurationSummary();

        if (saveImmediately && !PersistSettings())
        {
            return;
        }

        SetDirectoryStatus(
            statusMessage,
            isError: !Directory.Exists(_selectedDirectory.DirectoryPath));
    }

    private void ClearSelectedDirectory(
        string statusMessage,
        bool isError,
        bool saveImmediately)
    {
        _selectedDirectory = null;
        _settings.LastSelectedDirectoryPath = string.Empty;
        _selectedDirectoryValue.Text = "未选择执行目录";
        RefreshDirectoryComboBox(selectedDirectoryPath: null);
        RefreshScriptConfigurationSummary();

        if (saveImmediately && !PersistSettings())
        {
            return;
        }

        SetDirectoryStatus(statusMessage, isError);
    }

    private ExecutionDirectoryEntry UpsertDirectory(
        string directoryPath,
        string? displayName,
        bool moveToTop)
    {
        var normalizedPath = ScriptRunnerPathHelper.NormalizeDirectoryPath(directoryPath);
        var existing = FindDirectoryEntry(normalizedPath);
        if (existing is null)
        {
            existing = new ExecutionDirectoryEntry
            {
                DirectoryPath = normalizedPath,
                DisplayName = displayName?.Trim() ?? string.Empty
            };

            if (moveToTop)
            {
                _settings.RecentDirectories.Insert(0, existing);
            }
            else
            {
                _settings.RecentDirectories.Add(existing);
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
                _settings.RecentDirectories.Remove(existing);
                _settings.RecentDirectories.Insert(0, existing);
            }
        }

        if (_settings.RecentDirectories.Count > ScriptRunnerStore.MaxRecentDirectoryCount)
        {
            _settings.RecentDirectories.RemoveRange(
                ScriptRunnerStore.MaxRecentDirectoryCount,
                _settings.RecentDirectories.Count - ScriptRunnerStore.MaxRecentDirectoryCount);
        }

        return existing;
    }

    private ExecutionDirectoryEntry? FindDirectoryEntry(string directoryPath)
    {
        try
        {
            var normalizedPath = ScriptRunnerPathHelper.NormalizeDirectoryPath(directoryPath);
            return _settings.RecentDirectories.FirstOrDefault(entry =>
                string.Equals(
                    entry.DirectoryPath,
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private void RefreshDirectoryComboBox(string? selectedDirectoryPath)
    {
        _isUpdatingDirectoryComboBox = true;
        try
        {
            _directoryComboBox.BeginUpdate();
            _directoryComboBox.Items.Clear();
            foreach (var entry in _settings.RecentDirectories)
            {
                _directoryComboBox.Items.Add(entry);
            }

            var selectedIndex = -1;
            if (!string.IsNullOrWhiteSpace(selectedDirectoryPath))
            {
                for (var index = 0; index < _directoryComboBox.Items.Count; index++)
                {
                    if (_directoryComboBox.Items[index] is ExecutionDirectoryEntry entry &&
                        string.Equals(
                            entry.DirectoryPath,
                            selectedDirectoryPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            _directoryComboBox.SelectedIndex = selectedIndex;
        }
        finally
        {
            _directoryComboBox.EndUpdate();
            _isUpdatingDirectoryComboBox = false;
        }
    }

    private bool PersistSettings(Label? statusLabel = null)
    {
        if (ScriptRunnerStore.TrySave(_settings, out var errorMessage))
        {
            return true;
        }

        var statusMessage = $"保存脚本执行配置失败：{errorMessage}";
        if (statusLabel is null)
        {
            SetDirectoryStatus(statusMessage, isError: true);
        }
        else
        {
            statusLabel.Text = statusMessage;
            statusLabel.ForeColor = Color.FromArgb(180, 45, 30);
        }

        MessageBox.Show(
            this,
            $"无法保存脚本执行配置。{Environment.NewLine}{Environment.NewLine}{errorMessage}",
            "保存失败",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return false;
    }

    private void SetDirectoryStatus(string message, bool isError)
    {
        _directoryStatusLabel.Text = message;
        _directoryStatusLabel.ForeColor = isError
            ? Color.FromArgb(180, 45, 30)
            : Color.FromArgb(23, 112, 41);
    }

    private static bool TryGetAvailablePath(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalizedPath = ScriptRunnerPathHelper.NormalizeDirectoryPath(path);
            return Directory.Exists(normalizedPath);
        }
        catch
        {
            normalizedPath = string.Empty;
            return false;
        }
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

    private static Control CreatePlaceholderSectionCard(
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
        layout.Controls.Add(CreateSectionTitle(title, iconKind, accentColor), 0, 0);

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

    private static Control CreateSectionTitle(
        string title,
        UiIconKind iconKind,
        Color accentColor)
    {
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
        return titleRow;
    }

    private static Button CreateActionButton(
        string text,
        UiIconKind iconKind,
        Color iconColor,
        Color backgroundColor)
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
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            Image = IconFactory.Create(iconKind, iconColor, 16),
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleCenter
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(214, 223, 236);
        button.FlatAppearance.BorderSize = 1;
        return button;
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
