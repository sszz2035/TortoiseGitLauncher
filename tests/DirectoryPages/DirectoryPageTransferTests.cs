using System.Text.Json;
using TortoiseGitLauncher;

internal static class DirectoryPageTransferTests
{
    public static void Run(Action<bool, string> check, string root)
    {
        string PathFor(string name) => Path.Combine(root, "transfer", name);
        RepositoryEntry Entry(string name, int order, string? groupId = null) => new()
        {
            RepoRootPath = PathFor(name), DisplayName = name, Notes = "备注 " + name,
            GroupId = groupId, DisplayOrder = order
        };

        var group = new RepositoryGroup { Name = "开发组", DisplayOrder = 7, IsExpanded = false };
        var source = new RepositoryDirectoryPage
        {
            Groups = [group], Entries = [Entry("first", 3, group.Id), Entry("second", 1, group.Id)]
        };
        var targetGroup = new RepositoryGroup { Name = "目标组", DisplayOrder = 40 };
        var target = new RepositoryDirectoryPage
        {
            Groups = [targetGroup], Entries = [Entry("existing", 60), Entry("child", 99, targetGroup.Id)]
        };
        var copy = DirectoryPageTransfer.Transfer(source.Entries, source.Groups, target.Entries, target.Groups,
            new[] { source.Entries[1], source.Entries[0] }, Array.Empty<RepositoryGroup>(), move: false);
        check(copy.AddedDirectories == 2 && source.Entries.Count == 2 && target.Entries.Count == 4,
            "ordinary copy preserves source and adds selected entries");
        check(target.Entries[2].RepoRootPath == PathFor("second") && target.Entries[2].GroupId is null &&
              target.Entries[2].DisplayOrder == 61 && target.Entries[3].DisplayOrder == 62 &&
              target.Groups[0].DisplayOrder == 40 && target.Entries[0].DisplayOrder == 60,
            "ordinary copy follows visible selection order and appends after mixed target rows");
        target.Entries[2].Notes = "目标独立备注";
        check(source.Entries[1].Notes == "备注 second", "copied metadata is independent of source");
        check(!JsonSerializer.Serialize(target).Contains("TransferPath"), "transfer contracts do not add serialized fields");

        // Target-wide deduplication includes grouped entries, case, trailing slash and dot segments.
        var same = source.Entries[0].Clone();
        same.RepoRootPath = Path.Combine(PathFor("first"), "..", "FIRST") + Path.DirectorySeparatorChar;
        same.GroupId = targetGroup.Id;
        same.DisplayName = "目标名称";
        same.Notes = "目标备注";
        var duplicateTarget = new RepositoryDirectoryPage { Groups = [targetGroup.Clone()], Entries = [same] };
        var partial = DirectoryPageTransfer.Transfer(source.Entries, source.Groups,
            duplicateTarget.Entries, duplicateTarget.Groups, source.Entries.ToList(), Array.Empty<RepositoryGroup>(), move: true);
        check(partial.AddedDirectories == 1 && partial.SkippedPaths.Count == 1 &&
              source.Entries.Count == 1 && source.Entries[0].RepoRootPath == PathFor("first"),
            "ordinary move retains normalized duplicate paths at source");
        check(same.DisplayName == "目标名称" && same.Notes == "目标备注" && same.GroupId == targetGroup.Id,
            "duplicate target metadata and grouping are never overwritten");

        var transferGroup = new RepositoryGroup { Name = "开发组", DisplayOrder = 3, IsExpanded = false };
        var emptyGroup = new RepositoryGroup { Name = "空组", DisplayOrder = 4 };
        var groupedSource = new RepositoryDirectoryPage
        {
            Groups = [transferGroup, emptyGroup],
            Entries = [Entry("duplicate", 0, transferGroup.Id), Entry("move-last", 9, transferGroup.Id), Entry("move-first", 4, transferGroup.Id)]
        };
        var existingGroup = new RepositoryGroup { Name = "开发组", DisplayOrder = 20 };
        var groupedTarget = new RepositoryDirectoryPage
        {
            Groups = [existingGroup, new RepositoryGroup { Name = "开发组 (2)", DisplayOrder = 30 }],
            Entries = [Entry("duplicate", 0, existingGroup.Id), Entry("ungrouped-last", 50)]
        };
        var groupsMoved = DirectoryPageTransfer.Transfer(groupedSource.Entries, groupedSource.Groups,
            groupedTarget.Entries, groupedTarget.Groups, Array.Empty<RepositoryEntry>(), new[] { transferGroup, emptyGroup }, move: true);
        var newGroup = groupedTarget.Groups[2];
        check(groupsMoved.AddedDirectories == 2 && groupsMoved.AddedGroups == 2 && groupsMoved.RemovedGroups == 1 &&
              groupedSource.Groups.Count == 1 && groupedSource.Entries.Single().RepoRootPath == PathFor("duplicate"),
            "partial whole-group move retains duplicate child and source group, moves empty group");
        check(newGroup.Name == "开发组 (3)" && newGroup.Id != transferGroup.Id && !newGroup.IsExpanded &&
              newGroup.DisplayOrder == 51 && groupedTarget.Groups[3].DisplayOrder == 52,
            "whole-group transfer allocates fresh IDs, unique names and top-level order, preserves collapse");
        var newChildren = groupedTarget.Entries.Where(entry => entry.GroupId == newGroup.Id).OrderBy(entry => entry.DisplayOrder).ToList();
        check(newChildren.Select(entry => entry.RepoRootPath).SequenceEqual(new[] { PathFor("move-first"), PathFor("move-last") }) &&
              newChildren[0].Notes == "备注 move-first", "collapsed group includes all children in source order with notes");
        var allDuplicate = DirectoryPageTransfer.Transfer(groupedSource.Entries, groupedSource.Groups,
            groupedTarget.Entries, groupedTarget.Groups, Array.Empty<RepositoryEntry>(), groupedSource.Groups.ToList(), move: true);
        check(!allDuplicate.HasChanges && allDuplicate.SkippedGroups == 1 && allDuplicate.SkippedPaths.Count == 1 &&
              groupedSource.Groups.Count == 1 && groupedTarget.Groups.Count == 4,
            "all-duplicate group move changes neither page and creates no empty target group");

        var fullGroup = new RepositoryGroup { Name = "全量组" };
        var fullSource = new RepositoryDirectoryPage { Groups = [fullGroup], Entries = [Entry("full", 0, fullGroup.Id)] };
        var fullTarget = new RepositoryDirectoryPage();
        var full = DirectoryPageTransfer.Transfer(fullSource.Entries, fullSource.Groups, fullTarget.Entries, fullTarget.Groups,
            Array.Empty<RepositoryEntry>(), new[] { fullGroup }, move: true);
        check(full.RemovedGroups == 1 && fullSource.Entries.Count == 0 && fullSource.Groups.Count == 0,
            "fully moved group is removed from source");
        var emptySource = new RepositoryDirectoryPage { Groups = [new RepositoryGroup { Name = "空组" }] };
        var emptyTarget = new RepositoryDirectoryPage();
        var emptyCopy = DirectoryPageTransfer.Transfer(emptySource.Entries, emptySource.Groups, emptyTarget.Entries, emptyTarget.Groups,
            Array.Empty<RepositoryEntry>(), emptySource.Groups.ToList(), move: false);
        check(emptyCopy.HasChanges && emptyCopy.AddedDirectories == 0 && emptyCopy.AddedGroups == 1 && emptySource.Groups.Count == 1,
            "explicit empty-group copy succeeds without removing source");

        // The form owns these deep page copies. Cancel must discard both source and target edits.
        var original = new RepositoryHistorySettings
        {
            Pages = [new RepositoryDirectoryPage { Entries = [Entry("transaction", 0)] }, new RepositoryDirectoryPage { Name = "目标" }]
        };
        var draft = original.Clone();
        DirectoryPageTransfer.Transfer(draft.Pages[0].Entries, draft.Pages[0].Groups, draft.Pages[1].Entries, draft.Pages[1].Groups,
            draft.Pages[0].Entries.ToList(), Array.Empty<RepositoryGroup>(), move: true);
        check(original.Pages[0].Entries.Count == 1 && original.Pages[1].Entries.Count == 0 && draft.Pages[0].Entries.Count == 0,
            "cancelled transfer drafts never mutate live source or target pages");
        var fixture = Path.Combine(root, "transfer-pages.json");
        check(RepositoryHistoryStore.TrySaveTo(fixture, draft, out _), "save source and target pages together");
        var restored = RepositoryHistoryStore.LoadSettingsFrom(fixture, out _);
        check(restored.Pages[0].Entries.Count == 0 && restored.Pages[1].Entries.Single().Notes == "备注 transaction",
            "saved transfer survives restart with source removal and target metadata");
        var blocker = Path.Combine(root, "transfer-save-blocker");
        File.WriteAllText(blocker, "not a directory");
        check(!RepositoryHistoryStore.TrySaveTo(Path.Combine(blocker, "settings.json"), draft.Clone(), out _) &&
              original.Pages[0].Entries.Count == 1 && draft.Pages[1].Entries.Count == 1,
            "failed save preserves live pages and transfer draft for retry");

        var executionGroup = new ExecutionDirectoryGroup { Name = "执行组", IsExpanded = false };
        var executionSource = new ExecutionDirectoryPage
        {
            DirectoryGroups = [executionGroup],
            RecentDirectories = [new ExecutionDirectoryEntry { DirectoryPath = PathFor("missing-execution"), DisplayName = "执行项", Notes = "承接备注", GroupId = executionGroup.Id }]
        };
        var executionTarget = new ExecutionDirectoryPage();
        var execution = DirectoryPageTransfer.Transfer(executionSource.RecentDirectories, executionSource.DirectoryGroups,
            executionTarget.RecentDirectories, executionTarget.DirectoryGroups, Array.Empty<ExecutionDirectoryEntry>(), new[] { executionGroup }, move: true);
        check(execution.AddedDirectories == 1 && executionSource.DirectoryGroups.Count == 0 &&
              executionTarget.RecentDirectories[0].Notes == "承接备注" && !executionTarget.DirectoryGroups[0].IsExpanded &&
              !Directory.Exists(executionTarget.RecentDirectories[0].DirectoryPath),
            "execution pages use identical group rules, retain notes and allow unavailable paths");
        var executionSettings = new ScriptRunnerSettings
        {
            Pages = [executionSource, executionTarget], Scripts = [new ScriptConfiguration { Name = "全局", ScriptPath = Path.Combine(root, "test.py") }]
        };
        var executionFixture = Path.Combine(root, "transfer-execution.json");
        check(ScriptRunnerStore.TrySaveTo(executionFixture, executionSettings, out _) &&
              ScriptRunnerStore.LoadFrom(executionFixture, out _).Scripts.Count == 1,
            "execution transfer persists both pages while global scripts remain shared");

        var invalidSource = new RepositoryDirectoryPage { Entries = [Entry("safe", 0)] };
        var invalidTarget = new RepositoryDirectoryPage { Entries = [Entry("orphan", 0, "missing-group")] };
        var rejected = false;
        try
        {
            DirectoryPageTransfer.Transfer(invalidSource.Entries, invalidSource.Groups, invalidTarget.Entries, invalidTarget.Groups,
                invalidSource.Entries.ToList(), Array.Empty<RepositoryGroup>(), move: true);
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected && invalidSource.Entries.Count == 1 && invalidTarget.Entries.Count == 1,
            "invalid target membership is rejected before either page changes");
        check(DirectoryPageTransfer.Validate(new[] { Entry("same", 0), Entry("same", 1) }, Array.Empty<RepositoryGroup>()) is not null,
            "save validation rejects duplicate paths instead of silently dropping entries");
        rejected = false;
        try
        {
            DirectoryPageTransfer.Transfer(invalidSource.Entries, invalidSource.Groups, invalidSource.Entries, invalidSource.Groups,
                invalidSource.Entries.ToList(), Array.Empty<RepositoryGroup>(), move: true);
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected && invalidSource.Entries.Count == 1, "same-page transfer is rejected without data loss");
    }
}
