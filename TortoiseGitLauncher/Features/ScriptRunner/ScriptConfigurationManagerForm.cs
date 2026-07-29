using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class ScriptConfigurationManagerForm : Form
{
    private readonly List<ScriptConfiguration> _configurations;
    private readonly ListBox _configurationListBox;
    private readonly Label _summaryLabel;
    private readonly Label _nameValueLabel;
    private readonly TextBox _pathValueTextBox;
    private readonly Label _typeValueLabel;
    private readonly TextBox _argumentsValueTextBox;
    private readonly Label _iconValueLabel;
    private readonly Label _pathStatusLabel;

    public ScriptConfigurationManagerForm(IEnumerable<ScriptConfiguration> configurations)
    {
        _configurations = configurations.Select(configuration => configuration.Clone()).ToList();
        NormalizeDisplayOrder();

        Text = "管理脚本";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 570);
        Size = new Size(980, 650);
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
            Text = "新建或编辑脚本配置，并调整脚本按钮的显示顺序。路径失效的配置会继续保留。",
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));

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

        _configurationListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            FormattingEnabled = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        _configurationListBox.Format += (_, eventArgs) =>
        {
            if (eventArgs.ListItem is ScriptConfiguration configuration)
            {
                var extension = Path.GetExtension(configuration.ScriptPath).ToLowerInvariant();
                eventArgs.Value = File.Exists(configuration.ScriptPath)
                    ? $"{configuration.Name} ({extension})"
                    : $"{configuration.Name} ({extension})（路径无效）";
            }
        };
        _configurationListBox.SelectedIndexChanged += (_, _) => UpdateDetailsFromSelection();
        _configurationListBox.DoubleClick += (_, _) => EditSelectedConfiguration();
        leftPanel.Controls.Add(_configurationListBox, 0, 1);

        var listActions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0)
        };

        var newButton = CreateDialogButton("新建", Color.FromArgb(237, 247, 240));
        newButton.Margin = new Padding(0, 0, 8, 8);
        newButton.Click += (_, _) => CreateConfiguration();
        listActions.Controls.Add(newButton);

        var editButton = CreateDialogButton("编辑", Color.FromArgb(233, 242, 255));
        editButton.Margin = new Padding(0, 0, 8, 8);
        editButton.Click += (_, _) => EditSelectedConfiguration();
        listActions.Controls.Add(editButton);

        var moveUpButton = CreateDialogButton("上移", Color.FromArgb(245, 245, 245));
        moveUpButton.Margin = new Padding(0, 0, 8, 8);
        moveUpButton.Click += (_, _) => MoveSelectedConfiguration(-1);
        listActions.Controls.Add(moveUpButton);

        var moveDownButton = CreateDialogButton("下移", Color.FromArgb(245, 245, 245));
        moveDownButton.Margin = new Padding(0, 0, 8, 8);
        moveDownButton.Click += (_, _) => MoveSelectedConfiguration(1);
        listActions.Controls.Add(moveDownButton);

        var deleteButton = CreateDialogButton("删除", Color.FromArgb(249, 237, 235));
        deleteButton.Margin = new Padding(0, 0, 0, 8);
        deleteButton.Click += (_, _) => DeleteSelectedConfiguration();
        listActions.Controls.Add(deleteButton);

        leftPanel.Controls.Add(listActions, 0, 2);
        contentLayout.Controls.Add(leftPanel, 0, 0);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 12,
            Margin = new Padding(12, 0, 0, 0)
        };

        rightPanel.Controls.Add(CreateFieldLabel("脚本名称"), 0, 0);
        _nameValueLabel = CreateValueLabel();
        rightPanel.Controls.Add(_nameValueLabel, 0, 1);

        rightPanel.Controls.Add(CreateFieldLabel("脚本路径", new Padding(0, 14, 0, 6)), 0, 2);
        _pathValueTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            ReadOnly = true,
            Multiline = true,
            Height = 66,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 6)
        };
        rightPanel.Controls.Add(_pathValueTextBox, 0, 3);

        _pathStatusLabel = CreateValueLabel();
        rightPanel.Controls.Add(_pathStatusLabel, 0, 4);

        rightPanel.Controls.Add(CreateFieldLabel("脚本类型", new Padding(0, 14, 0, 6)), 0, 5);
        _typeValueLabel = CreateValueLabel();
        rightPanel.Controls.Add(_typeValueLabel, 0, 6);

        rightPanel.Controls.Add(CreateFieldLabel("命令行参数", new Padding(0, 14, 0, 6)), 0, 7);
        _argumentsValueTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            ReadOnly = true,
            Multiline = true,
            Height = 58,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 6)
        };
        rightPanel.Controls.Add(_argumentsValueTextBox, 0, 8);

        rightPanel.Controls.Add(CreateFieldLabel("内置图标", new Padding(0, 14, 0, 6)), 0, 9);
        _iconValueLabel = CreateValueLabel();
        rightPanel.Controls.Add(_iconValueLabel, 0, 10);

        rightPanel.Controls.Add(new Label
        {
            Text = "双击左侧脚本可以直接编辑。删除操作会在确认后从配置中移除该脚本。",
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = Color.FromArgb(96, 104, 116),
            Margin = new Padding(0, 18, 0, 0)
        }, 0, 11);

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
        cancelButton.DialogResult = DialogResult.Cancel;
        actionPanel.Controls.Add(cancelButton);

        var saveButton = CreateDialogButton("保存修改", Color.FromArgb(232, 241, 255));
        saveButton.Margin = new Padding(10, 0, 0, 0);
        saveButton.Click += (_, _) => SaveAndClose();
        actionPanel.Controls.Add(saveButton);

        rootLayout.Controls.Add(actionPanel, 0, 2);
        Controls.Add(rootLayout);
        CancelButton = cancelButton;

        RefreshConfigurationList(_configurations.FirstOrDefault()?.Id);
    }

    public List<ScriptConfiguration> GetConfigurations()
    {
        NormalizeDisplayOrder();
        return _configurations.Select(configuration => configuration.Clone()).ToList();
    }

    private void CreateConfiguration()
    {
        using var dialog = new ScriptConfigurationForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ResultConfiguration is null)
        {
            return;
        }

        var configuration = dialog.ResultConfiguration;
        configuration.DisplayOrder = _configurations.Count;
        _configurations.Add(configuration);
        RefreshConfigurationList(configuration.Id);
    }

    private void EditSelectedConfiguration()
    {
        if (_configurationListBox.SelectedItem is not ScriptConfiguration selected)
        {
            return;
        }

        using var dialog = new ScriptConfigurationForm(selected);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ResultConfiguration is null)
        {
            return;
        }

        var index = _configurations.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        var updated = dialog.ResultConfiguration;
        updated.DisplayOrder = index;
        _configurations[index] = updated;
        RefreshConfigurationList(updated.Id);
    }

    private void MoveSelectedConfiguration(int offset)
    {
        if (_configurationListBox.SelectedItem is not ScriptConfiguration selected)
        {
            return;
        }

        var currentIndex = _configurations.IndexOf(selected);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _configurations.Count)
        {
            return;
        }

        _configurations.RemoveAt(currentIndex);
        _configurations.Insert(targetIndex, selected);
        NormalizeDisplayOrder();
        RefreshConfigurationList(selected.Id);
    }

    private void DeleteSelectedConfiguration()
    {
        if (_configurationListBox.SelectedItem is not ScriptConfiguration selected)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"确定删除脚本配置“{selected.Name}”吗？",
            "确认删除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes)
        {
            return;
        }

        var removeIndex = _configurations.IndexOf(selected);
        if (removeIndex < 0)
        {
            return;
        }

        _configurations.RemoveAt(removeIndex);
        NormalizeDisplayOrder();
        var nextSelectedId = removeIndex < _configurations.Count
            ? _configurations[removeIndex].Id
            : _configurations.LastOrDefault()?.Id;
        RefreshConfigurationList(nextSelectedId);
    }

    private void RefreshConfigurationList(string? selectedId)
    {
        _configurationListBox.BeginUpdate();
        _configurationListBox.Items.Clear();
        foreach (var configuration in _configurations)
        {
            _configurationListBox.Items.Add(configuration);
        }
        _configurationListBox.EndUpdate();

        _summaryLabel.Text = $"共 {_configurations.Count} 个脚本配置";
        if (_configurations.Count == 0)
        {
            _configurationListBox.SelectedIndex = -1;
            UpdateDetailsFromSelection();
            return;
        }

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            for (var index = 0; index < _configurations.Count; index++)
            {
                if (string.Equals(_configurations[index].Id, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        _configurationListBox.SelectedIndex = selectedIndex;
    }

    private void UpdateDetailsFromSelection()
    {
        if (_configurationListBox.SelectedItem is not ScriptConfiguration configuration)
        {
            _nameValueLabel.Text = "未选择脚本";
            _pathValueTextBox.Text = string.Empty;
            _pathStatusLabel.Text = string.Empty;
            _typeValueLabel.Text = string.Empty;
            _argumentsValueTextBox.Text = string.Empty;
            _iconValueLabel.Text = string.Empty;
            return;
        }

        _nameValueLabel.Text = configuration.Name;
        _pathValueTextBox.Text = configuration.ScriptPath;
        var pathAvailable = File.Exists(configuration.ScriptPath);
        _pathStatusLabel.Text = pathAvailable ? "脚本文件可用" : "脚本文件不存在或暂时不可访问";
        _pathStatusLabel.ForeColor = pathAvailable
            ? Color.FromArgb(23, 112, 41)
            : Color.FromArgb(180, 45, 30);
        _typeValueLabel.Text = ScriptConfigurationPathHelper.GetTypeDisplayName(configuration.ScriptType);
        _argumentsValueTextBox.Text = configuration.Arguments;
        _iconValueLabel.Text = ScriptIconCatalog.Options.First(option =>
            string.Equals(
                option.Key,
                ScriptIconCatalog.NormalizeKey(configuration.IconKey),
                StringComparison.Ordinal)).DisplayName;
    }

    private void SaveAndClose()
    {
        NormalizeDisplayOrder();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void NormalizeDisplayOrder()
    {
        for (var index = 0; index < _configurations.Count; index++)
        {
            _configurations[index].DisplayOrder = index;
        }
    }

    private static Label CreateFieldLabel(string text, Padding? margin = null) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = margin ?? new Padding(0, 0, 0, 6)
        };

    private static Label CreateValueLabel() =>
        new()
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = Color.FromArgb(52, 60, 72),
            Margin = new Padding(0)
        };

    private static Button CreateDialogButton(string text, Color backgroundColor)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(90, 38),
            Padding = new Padding(12, 6, 12, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = backgroundColor,
            UseCompatibleTextRendering = true
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(190, 202, 215);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }
}
