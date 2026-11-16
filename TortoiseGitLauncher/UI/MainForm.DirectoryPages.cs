using System.Windows.Forms;

namespace TortoiseGitLauncher;

internal sealed partial class MainForm
{
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
}
