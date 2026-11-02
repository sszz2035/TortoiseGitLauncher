using System.Text.Json;
using TortoiseGitLauncher;

// Run only when a build/test run is authorized. Never touches real user settings.
var root = Path.Combine(Path.GetTempPath(), "TortoiseGitLauncher-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    passed++;
    Console.WriteLine("PASS " + name);
}

var groupId = Guid.NewGuid().ToString("D");
var paths = Enumerable.Range(0, 32).Select(index => Path.Combine(root, "repo-" + index)).ToArray();
var legacyRepo = JsonSerializer.Serialize(new
{
    Version = 2,
    Groups = new[] { new { Id = groupId, Name = "开发组", DisplayOrder = 0, IsExpanded = false } },
    Entries = paths.Select((path, index) => new
    {
        RepoRootPath = path, DisplayName = "仓库 " + index, Notes = "备注\r\n" + index,
        GroupId = groupId, DisplayOrder = index
    })
});
var repos = RepositoryHistoryStore.Parse(legacyRepo, out _);
Check(repos.SaveBlockedReason is null && repos.Pages.Count == 1 && repos.ActivePage.Name == "默认页", "repository default-page migration");
Check(repos.Entries.Count == 32 && repos.Entries[31].Notes == "备注\r\n31" &&
      repos.Entries[0].GroupId == groupId && !repos.Groups[0].IsExpanded, "repository migration preserves all entries and metadata");

var secondRepoPage = repos.ActivePage.Clone();
secondRepoPage.Id = Guid.NewGuid().ToString("D");
secondRepoPage.Name = "另一页";
secondRepoPage.DisplayOrder = 1;
secondRepoPage.Entries[0].Notes = "另一份备注";
repos.Pages.Add(secondRepoPage);
repos.LastSelectedPageId = secondRepoPage.Id;
Check(repos.Pages[0].Entries[0].Notes != repos.Pages[1].Entries[0].Notes, "repository page deep clone isolation");
var repoPath = Path.Combine(root, "recent-repos.json");
File.WriteAllText(repoPath, legacyRepo);
Check(RepositoryHistoryStore.TrySaveTo(repoPath, repos, out _), "repository atomic migration save");
Check(File.ReadAllText(repoPath + ".v2.bak") == legacyRepo && File.ReadAllText(repoPath + ".bak") == legacyRepo, "repository migration and previous-save backups");
var reloadedRepos = RepositoryHistoryStore.LoadSettingsFrom(repoPath, out _);
Check(reloadedRepos.Pages.Count == 2 && reloadedRepos.Pages.All(page => page.Entries.Count == 32) &&
      reloadedRepos.ActivePage.Id == secondRepoPage.Id, "repository roundtrip keeps duplicate paths across pages and active page");
using (var serialized = JsonDocument.Parse(File.ReadAllText(repoPath)))
{
    Check(!serialized.RootElement.TryGetProperty("Entries", out _) &&
          !serialized.RootElement.TryGetProperty("ActivePage", out _), "repository compatibility properties not serialized");
}
var arrayRepos = RepositoryHistoryStore.Parse(JsonSerializer.Serialize(paths), out _);
Check(arrayRepos.Entries.Count == 32 && arrayRepos.Groups.Count == 0, "legacy repository string array");

var mixedOrder = JsonSerializer.Serialize(new
{
    Version = 2,
    Groups = new[] { new { Id = groupId, Name = "后面的组", DisplayOrder = 8 } },
    Entries = new[] { new { RepoRootPath = paths[0], DisplayOrder = 4 } }
});
var mixedOrderSettings = RepositoryHistoryStore.Parse(mixedOrder, out _);
Check(mixedOrderSettings.Groups[0].DisplayOrder == 8 && mixedOrderSettings.Entries[0].DisplayOrder == 4,
    "migration preserves mixed group and ungrouped ordering");

var scriptPath = Path.Combine(root, "test.py");
var legacyScripts = JsonSerializer.Serialize(new
{
    Version = 3,
    LastSelectedDirectoryPath = paths[5],
    DirectoryGroups = new[] { new { Id = groupId, Name = "执行组", DisplayOrder = 0, IsExpanded = true } },
    RecentDirectories = paths.Select((path, index) => new
    {
        DirectoryPath = path, DisplayName = "目录 " + index, GroupId = groupId, DisplayOrder = index
    }),
    Scripts = new[] { new { Id = Guid.NewGuid().ToString("D"), Name = "脚本", ScriptPath = scriptPath, Arguments = "--test" } }
});
var scripts = ScriptRunnerStore.Parse(legacyScripts, out _);
Check(scripts.SaveBlockedReason is null && scripts.RecentDirectories.Count == 32 &&
      scripts.LastSelectedDirectoryPath == paths[5], "execution migration has no 20-item truncation and keeps selection");
Check(scripts.Scripts.Count == 1 && scripts.Scripts[0].Arguments == "--test", "scripts stay global during migration");
var secondExecutionPage = scripts.ActivePage.Clone();
secondExecutionPage.Id = Guid.NewGuid().ToString("D");
secondExecutionPage.Name = "第二页";
secondExecutionPage.DisplayOrder = 1;
secondExecutionPage.RecentDirectories[0].Notes = "跨模块备注";
scripts.Pages.Add(secondExecutionPage);
scripts.LastSelectedPageId = secondExecutionPage.Id;
var executionPath = Path.Combine(root, "script-runner.json");
File.WriteAllText(executionPath, legacyScripts);
Check(ScriptRunnerStore.TrySaveTo(executionPath, scripts, out _), "execution atomic migration save");
var reloadedScripts = ScriptRunnerStore.LoadFrom(executionPath, out _);
Check(reloadedScripts.Pages.Count == 2 && reloadedScripts.Pages.All(page => page.RecentDirectories.Count == 32) &&
      reloadedScripts.Scripts.Count == 1 && reloadedScripts.ActivePage.RecentDirectories[0].Notes == "跨模块备注",
      "execution roundtrip keeps pages and notes, global scripts not duplicated");
Check(scripts.Pages[0].RecentDirectories[0].Notes == string.Empty, "execution page deep clone isolation");
Check(File.ReadAllText(executionPath + ".v3.bak") == legacyScripts, "execution migration backup");

// A future-version file must be preserved, including if it appeared after loading.
var future = "{\"Version\":999,\"UnknownImportantData\":true}";
var futureSettings = RepositoryHistoryStore.Parse(future, out _);
Check(futureSettings.SaveBlockedReason is not null, "future repository version blocks saving");
File.WriteAllText(repoPath, future);
Check(!RepositoryHistoryStore.TrySaveTo(repoPath, repos, out _) && File.ReadAllText(repoPath) == future,
    "save-time future-version guard preserves original bytes");
Check(ScriptRunnerStore.Parse(future, out _).SaveBlockedReason is not null, "future execution version blocks saving");
Check(RepositoryHistoryStore.Parse("{", out _).SaveBlockedReason is not null &&
      ScriptRunnerStore.Parse("{", out _).SaveBlockedReason is not null, "malformed configurations protected");

var mixedPages = JsonSerializer.Serialize(new
{
    Version = 3, Pages = new object[] { 42, repos.Pages[0] }
});
var recovered = RepositoryHistoryStore.Parse(mixedPages, out var warning);
Check(recovered.Pages.Count == 1 && recovered.Entries.Count == 32 && !string.IsNullOrWhiteSpace(warning), "bad page does not clear good page");
var missingPages = RepositoryHistoryStore.Parse("{\"Version\":3}", out _);
Check(missingPages.SaveBlockedReason is not null, "new schema missing pages cannot be overwritten");
var bytes = File.ReadAllBytes(executionPath);
Check(!(bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191), "saved JSON UTF-8 without BOM");
// UI selection policy must not resurrect an explicitly removed configuration item.
var usablePath = Path.Combine(root, "available");
var removedPath = Path.Combine(root, "removed-but-still-on-disk");
Directory.CreateDirectory(usablePath);
Directory.CreateDirectory(removedPath);
var usable = new RepositoryEntry { RepoRootPath = usablePath };
var unavailable = new RepositoryEntry { RepoRootPath = Path.Combine(root, "missing") };
Check(DirectoryPageSelection.Restore(removedPath, new[] { unavailable, usable }, entry => entry.RepoRootPath) == usable,
    "removed remembered path falls back inside current page without upsert");
Check(DirectoryPageSelection.Restore(removedPath, Array.Empty<RepositoryEntry>(), entry => entry.RepoRootPath) is null,
    "empty page stays empty even when remembered directory exists");
Check(DirectoryPageSelection.Restore(unavailable.RepoRootPath, new[] { unavailable }, entry => entry.RepoRootPath) is null,
    "unavailable directory has no active selection but configuration is retained");
Check(repos.AllowInitialDirectoryFallback && scripts.AllowInitialDirectoryFallback &&
      !reloadedRepos.AllowInitialDirectoryFallback && !reloadedScripts.AllowInitialDirectoryFallback,
    "startup fallback only applies to first initialization or legacy migration");
Check(!reloadedRepos.Clone().AllowInitialDirectoryFallback && !reloadedScripts.Clone().AllowInitialDirectoryFallback,
    "working copies preserve startup fallback policy");
var isolated = reloadedRepos.Clone();
isolated.Pages[0].Name = "编辑副本";
isolated.Pages[0].Entries.Clear();
Check(reloadedRepos.Pages[0].Name != "编辑副本" && reloadedRepos.Pages[0].Entries.Count == 32,
    "cancelled page manager edits cannot mutate original page contents");
var executionCopy = reloadedScripts.Clone();
executionCopy.Pages.Clear();
executionCopy.Pages.Add(new ExecutionDirectoryPage { Name = "空页" });
executionCopy.LastSelectedPageId = executionCopy.Pages[0].Id;
Check(executionCopy.RecentDirectories.Count == 0 && executionCopy.Scripts.Count == 1 &&
      reloadedScripts.Pages.Count == 2, "new execution page empty, global scripts and original pages preserved");
DirectoryPageTransferTests.Run(Check, root);
Console.WriteLine($"{passed} checks passed. Fixtures: {root}");
