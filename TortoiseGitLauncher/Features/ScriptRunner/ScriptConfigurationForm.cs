using System.Drawing;
using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed class ScriptConfigurationForm : Form
{
    private readonly ScriptConfiguration? _sourceConfiguration;
    private readonly TextBox _nameTextBox;
    private readonly TextBox _pathTextBox;
    private readonly Label _typeValueLabel;
    private readonly TextBox _argumentsTextBox;
    private readonly ComboBox _iconComboBox;
    private bool _isUpdatingName;
    private string _lastAutomaticName = string.Empty;

    public ScriptConfiguration? ResultConfiguration { get; private set; }

    public ScriptConfigurationForm(ScriptConfiguration? configuration = null)
    {
        _sourceConfiguration = configuration?.Clone();

        Text = configuration is null ? "新建脚本" : "编辑脚本";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 550);
        Size = new Size(780, 590);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        rootLayout.Controls.Add(new Label
        {
            Text = configuration is null
                ? "配置脚本文件、显示名称、命令行参数和内置图标。"
                : "修改脚本配置。已经启动的运行实例不会受后续修改影响。",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            ForeColor = Color.FromArgb(74, 82, 94),
            Margin = new Padding(0, 0, 0, 16)
        }, 0, 0);

        var editorLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 11,
            Margin = new Padding(0)
        };

        editorLayout.Controls.Add(CreateFieldLabel("脚本路径"), 0, 0);

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 6)
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _pathTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0)
        };
        _pathTextBox.TextChanged += (_, _) => UpdateDerivedFieldsFromPath();
        pathRow.Controls.Add(_pathTextBox, 0, 0);

        var browseButton = CreateDialogButton("浏览...", Color.FromArgb(233, 242, 255));
        browseButton.MinimumSize = new Size(96, 34);
        browseButton.Click += (_, _) => BrowseForScript();
        pathRow.Controls.Add(browseButton, 1, 0);
        editorLayout.Controls.Add(pathRow, 0, 1);

        editorLayout.Controls.Add(new Label
        {
            Text = "支持 .py、.bat、.cmd 和 .ps1；保存时文件必须存在，并记录绝对路径。",
            AutoSize = true,
            ForeColor = Color.FromArgb(96, 104, 116),
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 2);

        editorLayout.Controls.Add(CreateFieldLabel("脚本名称"), 0, 3);
        _nameTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 14)
        };
        editorLayout.Controls.Add(_nameTextBox, 0, 4);

        editorLayout.Controls.Add(CreateFieldLabel("脚本类型"), 0, 5);
        _typeValueLabel = new Label
        {
            Text = "请先选择脚本文件",
            AutoSize = true,
            ForeColor = Color.FromArgb(74, 82, 94),
            Margin = new Padding(0, 0, 0, 14)
        };
        editorLayout.Controls.Add(_typeValueLabel, 0, 6);

        editorLayout.Controls.Add(CreateFieldLabel("命令行参数（可选）"), 0, 7);
        _argumentsTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 14)
        };
        editorLayout.Controls.Add(_argumentsTextBox, 0, 8);

        editorLayout.Controls.Add(CreateFieldLabel("脚本图标"), 0, 9);
        var iconRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        iconRow.Controls.Add(new PictureBox
        {
            Size = new Size(34, 34),
            Image = IconFactory.Create(UiIconKind.ScriptRunner, Color.FromArgb(34, 137, 74), 22),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.FromArgb(237, 247, 240),
            Margin = new Padding(0, 0, 10, 0)
        }, 0, 0);

        _iconComboBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0)
        };
        foreach (var iconOption in ScriptIconCatalog.Options)
        {
            _iconComboBox.Items.Add(iconOption);
        }

        iconRow.Controls.Add(_iconComboBox, 1, 0);
        editorLayout.Controls.Add(iconRow, 0, 10);
        rootLayout.Controls.Add(editorLayout, 0, 1);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0)
        };

        var cancelButton = CreateDialogButton("取消", Color.FromArgb(245, 245, 245));
        cancelButton.DialogResult = DialogResult.Cancel;
        actionPanel.Controls.Add(cancelButton);

        var saveButton = CreateDialogButton("保存", Color.FromArgb(232, 241, 255));
        saveButton.Margin = new Padding(10, 0, 0, 0);
        saveButton.Click += (_, _) => SaveConfiguration();
        actionPanel.Controls.Add(saveButton);

        rootLayout.Controls.Add(actionPanel, 0, 2);
        Controls.Add(rootLayout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        LoadConfiguration(configuration);
    }

    private void LoadConfiguration(ScriptConfiguration? configuration)
    {
        if (configuration is null)
        {
            if (_iconComboBox.Items.Count > 0)
            {
                _iconComboBox.SelectedIndex = 0;
            }

            return;
        }

        _pathTextBox.Text = configuration.ScriptPath;
        _isUpdatingName = true;
        _nameTextBox.Text = configuration.Name;
        _isUpdatingName = false;
        _argumentsTextBox.Text = configuration.Arguments;

        var iconIndex = ScriptIconCatalog.Options
            .Select((option, index) => new { option, index })
            .FirstOrDefault(item => string.Equals(
                item.option.Key,
                ScriptIconCatalog.NormalizeKey(configuration.IconKey),
                StringComparison.Ordinal))
            ?.index ?? 0;
        _iconComboBox.SelectedIndex = iconIndex;
    }

    private void BrowseForScript()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择脚本文件",
            Filter = "支持的脚本 (*.py;*.bat;*.cmd;*.ps1)|*.py;*.bat;*.cmd;*.ps1|Python 脚本 (*.py)|*.py|批处理脚本 (*.bat;*.cmd)|*.bat;*.cmd|PowerShell 脚本 (*.ps1)|*.ps1|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        var currentPath = _pathTextBox.Text.Trim();
        if (Path.IsPathFullyQualified(currentPath))
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathTextBox.Text = dialog.FileName;
        }
    }

    private void UpdateDerivedFieldsFromPath()
    {
        var path = _pathTextBox.Text.Trim();
        if (ScriptConfigurationPathHelper.TryGetScriptType(path, out var scriptType))
        {
            _typeValueLabel.Text = ScriptConfigurationPathHelper.GetTypeDisplayName(scriptType);
            _typeValueLabel.ForeColor = Color.FromArgb(23, 112, 41);
        }
        else
        {
            _typeValueLabel.Text = string.IsNullOrWhiteSpace(path)
                ? "请先选择脚本文件"
                : "不支持此脚本类型";
            _typeValueLabel.ForeColor = string.IsNullOrWhiteSpace(path)
                ? Color.FromArgb(74, 82, 94)
                : Color.FromArgb(180, 45, 30);
        }

        if (_sourceConfiguration is not null)
        {
            return;
        }

        var automaticName = string.Empty;
        try
        {
            automaticName = Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
        }

        if (_isUpdatingName ||
            (!string.IsNullOrWhiteSpace(_nameTextBox.Text) &&
             !string.Equals(_nameTextBox.Text, _lastAutomaticName, StringComparison.Ordinal)))
        {
            return;
        }

        _isUpdatingName = true;
        _nameTextBox.Text = automaticName;
        _lastAutomaticName = automaticName;
        _isUpdatingName = false;
    }

    private void SaveConfiguration()
    {
        var enteredPath = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(enteredPath))
        {
            ShowValidationMessage("请选择或输入脚本路径。");
            _pathTextBox.Focus();
            return;
        }

        if (!Path.IsPathFullyQualified(enteredPath))
        {
            ShowValidationMessage("脚本路径必须是绝对路径。");
            _pathTextBox.Focus();
            return;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(enteredPath);
        }
        catch (Exception ex)
        {
            ShowValidationMessage($"脚本路径无效。{Environment.NewLine}{ex.Message}");
            _pathTextBox.Focus();
            return;
        }

        if (!ScriptConfigurationPathHelper.TryGetScriptType(normalizedPath, out var scriptType))
        {
            ShowValidationMessage("只支持 .py、.bat、.cmd 和 .ps1 脚本。");
            _pathTextBox.Focus();
            return;
        }

        if (!File.Exists(normalizedPath))
        {
            ShowValidationMessage("脚本文件不存在或暂时不可访问。");
            _pathTextBox.Focus();
            return;
        }

        var name = _nameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileNameWithoutExtension(normalizedPath);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowValidationMessage("请输入脚本名称。");
            _nameTextBox.Focus();
            return;
        }

        var iconKey = _iconComboBox.SelectedItem is ScriptIconOption iconOption
            ? iconOption.Key
            : ScriptIconCatalog.DefaultIconKey;

        ResultConfiguration = new ScriptConfiguration
        {
            Id = _sourceConfiguration?.Id ?? Guid.NewGuid().ToString("D"),
            Name = name,
            ScriptPath = normalizedPath,
            ScriptType = scriptType,
            Arguments = _argumentsTextBox.Text,
            IconKey = ScriptIconCatalog.NormalizeKey(iconKey),
            DisplayOrder = _sourceConfiguration?.DisplayOrder ?? 0
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowValidationMessage(string message)
    {
        MessageBox.Show(
            this,
            message,
            "无法保存脚本配置",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private static Label CreateFieldLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 6)
        };

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
}
