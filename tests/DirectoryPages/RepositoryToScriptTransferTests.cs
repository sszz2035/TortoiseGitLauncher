using TortoiseGitLauncher;

internal static class RepositoryToScriptTransferTests
{
    public static void Run(Action<bool, string> check, string root)
    {
        string PathFor(string name) => Path.Combine(root, "cross-module", name);
        var group = new RepositoryGroup { Name = "开发组", DisplayOrder = 3, IsExpanded = false };
        var first = new RepositoryEntry
        {
            RepoRootPath = PathFor("missing-first"), DisplayName = "第一个", Notes = "备注\n第一行 ",
            GroupId = group.Id, DisplayOrder = 1
        };
        var second = new RepositoryEntry
        {
            RepoRootPath = PathFor("missing-second"), DisplayName = "第二个", Notes = "第二个备注",
            GroupId = group.Id, DisplayOrder = 0
        };
        var original = new RepositoryDirectoryPage { Entries = [first, second], Groups = [group] };
        var source = original.Clone();
        var existingGroup = new ExecutionDirectoryGroup { Name = "开发组", DisplayOrder = 10 };
        var duplicate = new ExecutionDirectoryEntry
        {
            DirectoryPath = first.RepoRootPath.ToUpperInvariant() + Path.DirectorySeparatorChar,
            DisplayName = "目标名称", Notes = "目标备注", GroupId = existingGroup.Id, DisplayOrder = 4
        };
        var target = new ExecutionDirectoryPage { DirectoryGroups = [existingGroup], RecentDirectories = [duplicate] };
        var result = RepositoryToScriptTransfer.Transfer(source.Entries, source.Groups,
            target.RecentDirectories, target.DirectoryGroups, [], source.Groups.ToList(), move: true);
        check(result.AddedDirectories == 1 && result.AddedGroups == 1 && result.SkippedPaths.Count == 1 &&
              source.Entries.Count == 1 && source.Groups.Count == 1 && original.Entries.Count == 2,
            "cross-module partial group move retains duplicate source and isolates live pages");
        var added = target.RecentDirectories[1];
        var addedGroup = target.DirectoryGroups[1];
        check(added.DirectoryPath == second.RepoRootPath && added.DisplayName == second.DisplayName &&
              added.Notes == second.Notes && added.GroupId == addedGroup.Id && added.DisplayOrder == 0 &&
              addedGroup.Id != group.Id && addedGroup.Name == "开发组 (2)" && !addedGroup.IsExpanded &&
              addedGroup.DisplayOrder == 11,
            "cross-module mapping retains notes, names, missing paths, collapsed state and order");
        check(duplicate.DisplayName == "目标名称" && duplicate.Notes == "目标备注" && duplicate.GroupId == existingGroup.Id,
            "cross-module duplicate preserves target metadata");
        var allDuplicates = RepositoryToScriptTransfer.Transfer(source.Entries, source.Groups,
            target.RecentDirectories, target.DirectoryGroups, [], source.Groups.ToList(), move: true);
        check(!allDuplicates.HasChanges && source.Entries.Count == 1 && source.Groups.Count == 1 && target.DirectoryGroups.Count == 2,
            "all duplicate cross-module groups do not add empty groups or remove source");

        var ordinaryTarget = new ExecutionDirectoryPage();
        var ordinarySource = original.Clone();
        RepositoryToScriptTransfer.Transfer(ordinarySource.Entries, ordinarySource.Groups,
            ordinaryTarget.RecentDirectories, ordinaryTarget.DirectoryGroups,
            [ordinarySource.Entries[1], ordinarySource.Entries[0]], [], move: false);
        ordinaryTarget.RecentDirectories[0].Notes = "副本备注";
        check(ordinaryTarget.RecentDirectories[0].DirectoryPath == second.RepoRootPath &&
              ordinaryTarget.RecentDirectories.All(entry => entry.GroupId is null) &&
              ordinarySource.Entries[1].Notes == second.Notes && ordinarySource.Entries.Count == 2,
            "ordinary cross-module copy uses selection order, ungrouped area and independent metadata");
        var wholeSource = original.Clone();
        var wholeTarget = new ExecutionDirectoryPage();
        RepositoryToScriptTransfer.Transfer(wholeSource.Entries, wholeSource.Groups,
            wholeTarget.RecentDirectories, wholeTarget.DirectoryGroups, [], wholeSource.Groups.ToList(), move: true);
        check(wholeSource.Entries.Count == 0 && wholeSource.Groups.Count == 0 &&
              wholeTarget.RecentDirectories.Select(entry => entry.DirectoryPath).SequenceEqual([second.RepoRootPath, first.RepoRootPath]),
            "full cross-module group move removes source group and preserves child order");
        var empty = new RepositoryGroup { Name = "空组" };
        wholeSource.Groups.Add(empty);
        RepositoryToScriptTransfer.Transfer(wholeSource.Entries, wholeSource.Groups,
            wholeTarget.RecentDirectories, wholeTarget.DirectoryGroups, [], [empty], move: true);
        check(wholeSource.Groups.Count == 0 && wholeTarget.DirectoryGroups.Count == 2,
            "explicit empty group can move across modules");

        var repoPath = Path.Combine(root, "cross-repositories.json");
        var scriptPath = Path.Combine(root, "cross-scripts.json");
        var repos = new RepositoryHistorySettings { Pages = [original.Clone()] };
        var scripts = new ScriptRunnerSettings
        {
            Pages = [new ExecutionDirectoryPage { Name = "运行" }, new ExecutionDirectoryPage { Name = "其他" }],
            Scripts = [new ScriptConfiguration { Name = "全局脚本", ScriptPath = PathFor("run.py"), Arguments = "--test" }]
        };
        scripts.LastSelectedPageId = scripts.Pages[1].Id;
        check(RepositoryHistoryStore.TrySaveTo(repoPath, repos, out _) && ScriptRunnerStore.TrySaveTo(scriptPath, scripts, out _),
            "cross-module fixture saved");
        var repoDraft = repos.Clone();
        var scriptDraft = scripts.Clone();
        RepositoryToScriptTransfer.Transfer(repoDraft.Entries, repoDraft.Groups,
            scriptDraft.Pages[0].RecentDirectories, scriptDraft.Pages[0].DirectoryGroups,
            [], repoDraft.Groups.ToList(), move: true);
        check(RepositoryHistoryStore.LoadSettingsFrom(repoPath, out _).Entries.Count == 2 &&
              ScriptRunnerStore.LoadFrom(scriptPath, out _).Pages[0].RecentDirectories.Count == 0,
            "cross-module draft or cancel performs no disk writes");

        var save = new DirectoryTransferSave();
        var calls = new List<string>();
        string? SaveTarget()
        {
            calls.Add("target");
            return ScriptRunnerStore.TrySaveTo(scriptPath, scriptDraft, out var targetError) ? null : targetError;
        }
        string? SaveSource()
        {
            calls.Add("source");
            return RepositoryHistoryStore.TrySaveTo(repoPath, repoDraft, out var sourceError) ? null : sourceError;
        }
        var error = save.Save(() => "模拟目标写入失败", SaveSource);
        check(error is not null && !save.TargetSaved && calls.Count == 0 &&
              RepositoryHistoryStore.LoadSettingsFrom(repoPath, out _).Entries.Count == 2,
            "target failure never invokes source save or removes persisted source");
        error = save.Save(SaveTarget, () => { calls.Add("source-failed"); return "模拟源写入失败"; });
        var afterPartial = ScriptRunnerStore.LoadFrom(scriptPath, out _);
        check(error is not null && save.TargetSaved && !save.Completed &&
              calls.SequenceEqual(["target", "source-failed"]) &&
              RepositoryHistoryStore.LoadSettingsFrom(repoPath, out _).Entries.Count == 2 &&
              afterPartial.Pages[0].RecentDirectories.Count == 2 && afterPartial.Pages[0].DirectoryGroups.Count == 1,
            "partial commit saves target first and leaves original source recoverable on disk");
        error = save.Save(() => throw new InvalidOperationException("target must not be rewritten"), SaveSource);
        check(error is null && save.Completed && calls.SequenceEqual(["target", "source-failed", "source"]) &&
              RepositoryHistoryStore.LoadSettingsFrom(repoPath, out _).Entries.Count == 0 &&
              ScriptRunnerStore.LoadFrom(scriptPath, out _).Pages[0].DirectoryGroups.Count == 1,
            "retry completes source only, without duplicate target groups");
        check(save.Save(() => throw new InvalidOperationException(), () => throw new InvalidOperationException()) is null,
            "completed cross-module save is idempotent");
        var reloaded = ScriptRunnerStore.LoadFrom(scriptPath, out _);
        check(reloaded.LastSelectedPageId == scripts.LastSelectedPageId && reloaded.Pages[1].RecentDirectories.Count == 0 &&
              reloaded.Scripts.Count == 1 && reloaded.Scripts[0].Arguments == "--test" &&
              reloaded.Pages[0].RecentDirectories.Any(entry => entry.Notes == first.Notes),
            "save and reload retain current page, global scripts, untouched pages and transferred notes");
        check(File.Exists(repoPath + ".bak") && File.Exists(scriptPath + ".bak"),
            "cross-module saves retain both configuration backups");
    }
}
