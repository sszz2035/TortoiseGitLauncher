using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed partial class ScriptRunnerPage
{
    private void SwitchExecutionDirectoryPage(string id)
    {
        if (string.Equals(id, _settings.ActivePage.Id, StringComparison.OrdinalIgnoreCase)) return;
        var error = ApplyExecutionDirectoryPages(_settings.Pages, id);
        if (error is not null)
        {
            SetDirectoryStatus($"切换目录页失败：{error}", isError: true);
            MessageBox.Show(this, error, "无法保存目录页", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenExecutionPageManager()
    {
        using var dialog = new DirectoryPageManagerForm<ExecutionDirectoryPage>(
            "脚本执行", _settings.Pages, _settings.ActivePage.Id,
            page => page.Clone(), ApplyExecutionDirectoryPages);
        dialog.ShowDialog(this);
    }

    private string? ApplyExecutionDirectoryPages(List<ExecutionDirectoryPage> pages, string selectedId)
    {
        var candidate = _settings.Clone();
        candidate.Pages = pages.Select(page => page.Clone()).ToList();
        candidate.LastSelectedPageId = selectedId;
        candidate.AllowInitialDirectoryFallback = false;
        var selected = DirectoryPageSelection.Restore(candidate.LastSelectedDirectoryPath,
            candidate.RecentDirectories, entry => entry.DirectoryPath);
        candidate.LastSelectedDirectoryPath = selected?.DirectoryPath ?? string.Empty;
        if (!ScriptRunnerStore.TrySave(candidate, out var errorMessage)) return errorMessage ?? "保存失败。";

        _settings.Pages = candidate.Pages;
        _settings.LastSelectedPageId = candidate.LastSelectedPageId;
        _settings.AllowInitialDirectoryFallback = false;
        _directoryPageSelector.SetPages(_settings.Pages, _settings.ActivePage.Id);
        // Keep the current UserControl and runner: instances retain their launch-time snapshots.
        InitializeDirectorySelection(initialRepositoryRootPath: null, persistSelection: false);
        return null;
    }
}
