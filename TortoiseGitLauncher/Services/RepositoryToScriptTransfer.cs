namespace TortoiseGitLauncher;

// Map configuration only. Paths need not exist or point to Git repositories.
internal static class RepositoryToScriptTransfer
{
    public static DirectoryTransferResult Transfer(
        List<RepositoryEntry> sourceEntries, List<RepositoryGroup> sourceGroups,
        List<ExecutionDirectoryEntry> targetEntries, List<ExecutionDirectoryGroup> targetGroups,
        IReadOnlyList<RepositoryEntry> selectedEntries, IReadOnlyList<RepositoryGroup> selectedGroups, bool move)
    {
        if (selectedEntries.Any(entry => !sourceEntries.Contains(entry)) ||
            selectedGroups.Any(group => !sourceGroups.Contains(group)))
            throw new ArgumentException("选中的内容已不在源目录页中，请重新选择。");
        if (DirectoryPageTransfer.Validate(sourceEntries, sourceGroups) is { } error)
            throw new ArgumentException($"源目录页：{error}");

        var entries = sourceEntries.ToDictionary(entry => entry, entry => new ExecutionDirectoryEntry
        {
            DirectoryPath = entry.RepoRootPath, DisplayName = entry.DisplayName, Notes = entry.Notes,
            GroupId = entry.GroupId, DisplayOrder = entry.DisplayOrder
        });
        var groups = sourceGroups.ToDictionary(group => group, group => new ExecutionDirectoryGroup
        {
            Id = group.Id, Name = group.Name, DisplayOrder = group.DisplayOrder, IsExpanded = group.IsExpanded
        });
        var mappedEntries = entries.Values.ToList();
        var mappedGroups = groups.Values.ToList();
        var result = DirectoryPageTransfer.Transfer(mappedEntries, mappedGroups, targetEntries, targetGroups,
            selectedEntries.Select(entry => entries[entry]).ToList(),
            selectedGroups.Select(group => groups[group]).ToList(), move);
        if (move)
        {
            var retainedEntries = mappedEntries.ToHashSet();
            var retainedGroups = mappedGroups.ToHashSet();
            sourceEntries.RemoveAll(entry => !retainedEntries.Contains(entries[entry]));
            sourceGroups.RemoveAll(group => !retainedGroups.Contains(groups[group]));
        }
        return result;
    }
}
