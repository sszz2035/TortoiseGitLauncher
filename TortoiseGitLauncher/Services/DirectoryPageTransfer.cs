namespace TortoiseGitLauncher;

internal sealed class DirectoryTransferResult
{
    public int AddedDirectories { get; set; }
    public int AddedGroups { get; set; }
    public int RemovedGroups { get; set; }
    public int SkippedGroups { get; set; }
    public List<string> SkippedPaths { get; } = [];
    public List<string> RenamedGroups { get; } = [];
    public bool HasChanges => AddedDirectories > 0 || AddedGroups > 0;
}

internal static class DirectoryPageTransfer
{
    public static string NormalizePath(string path)
    {
        // Normalize for comparison only; transferred entries retain their stored absolute path.
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("目录路径必须是绝对路径。");
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string? Validate<TEntry, TGroup>(IReadOnlyList<TEntry> entries, IReadOnlyList<TGroup> groups)
        where TEntry : class, IDirectoryTransferEntry<TEntry>
        where TGroup : class, IDirectoryTransferGroup<TGroup>
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id)) return "分组标识为空或重复。";
            if (string.IsNullOrWhiteSpace(group.Name)) return "分组名称不能为空。";
            if (!names.Add(group.Name.Trim())) return "分组名称不能重复。";
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            try
            {
                if (!paths.Add(NormalizePath(entry.TransferPath))) return $"目录路径重复：{entry.TransferPath}";
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return $"目录路径无效：{entry.TransferPath}\n{ex.Message}";
            }
            if (!string.IsNullOrWhiteSpace(entry.GroupId) && !ids.Contains(entry.GroupId))
                return $"目录所属分组不存在：{entry.TransferPath}";
        }
        return null;
    }

    // Work only on the manager's cloned pages. Prepare every addition before changing either side.
    public static DirectoryTransferResult Transfer<TEntry, TGroup>(
        List<TEntry> sourceEntries, List<TGroup> sourceGroups,
        List<TEntry> targetEntries, List<TGroup> targetGroups,
        IReadOnlyList<TEntry> selectedEntries, IReadOnlyList<TGroup> selectedGroups, bool move)
        where TEntry : class, IDirectoryTransferEntry<TEntry>
        where TGroup : class, IDirectoryTransferGroup<TGroup>
    {
        if (ReferenceEquals(sourceEntries, targetEntries) || ReferenceEquals(sourceGroups, targetGroups))
            throw new ArgumentException("不能转移到当前目录页。");
        if (selectedEntries.Count > 0 && selectedGroups.Count > 0)
            throw new ArgumentException("请分别选择目录项或分组。");
        if (selectedEntries.Any(entry => !sourceEntries.Contains(entry)) ||
            selectedGroups.Any(group => !sourceGroups.Contains(group)))
            throw new ArgumentException("选中的内容已不在源目录页中，请重新选择。");
        if (Validate(sourceEntries, sourceGroups) is { } sourceError)
            throw new ArgumentException($"源目录页：{sourceError}");
        if (Validate(targetEntries, targetGroups) is { } targetError)
            throw new ArgumentException($"目标目录页：{targetError}");

        var result = new DirectoryTransferResult();
        var paths = targetEntries.Select(entry => NormalizePath(entry.TransferPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = targetGroups.Select(group => group.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groupIds = targetGroups.Concat(sourceGroups).Select(group => group.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextOrder = targetGroups.Select(group => group.DisplayOrder)
            .Concat(targetEntries.Where(entry => string.IsNullOrWhiteSpace(entry.GroupId)).Select(entry => entry.DisplayOrder))
            .DefaultIfEmpty(-1).Max() + 1;
        var additions = new List<TEntry>();
        var newGroups = new List<TGroup>();
        var movedEntries = new HashSet<TEntry>();
        var movedGroups = new HashSet<TGroup>();

        bool AddEntry(TEntry entry, string? groupId, int order)
        {
            if (!paths.Add(NormalizePath(entry.TransferPath)))
            {
                result.SkippedPaths.Add(entry.TransferPath);
                return false;
            }
            var copy = entry.Clone();
            copy.GroupId = groupId;
            copy.DisplayOrder = order;
            additions.Add(copy);
            movedEntries.Add(entry);
            return true;
        }

        foreach (var entry in selectedEntries.Distinct())
        {
            if (AddEntry(entry, null, nextOrder)) nextOrder++;
        }

        foreach (var group in selectedGroups.Distinct())
        {
            var children = sourceEntries.Where(entry => string.Equals(entry.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.DisplayOrder).ToList();
            var newGroup = group.Clone();
            do { newGroup.Id = Guid.NewGuid().ToString("D"); } while (!groupIds.Add(newGroup.Id));
            var before = additions.Count;
            foreach (var child in children) AddEntry(child, newGroup.Id, additions.Count - before);
            if (children.Count > 0 && additions.Count == before)
            {
                result.SkippedGroups++;
                continue;
            }

            var baseName = group.Name.Trim();
            var name = baseName;
            for (var suffix = 2; !names.Add(name); suffix++) name = $"{baseName} ({suffix})";
            newGroup.Name = name;
            newGroup.DisplayOrder = nextOrder++;
            if (name != baseName) result.RenamedGroups.Add($"{baseName} → {name}");
            newGroups.Add(newGroup);
            if (children.All(movedEntries.Contains)) movedGroups.Add(group);
        }

        targetEntries.AddRange(additions);
        targetGroups.AddRange(newGroups);
        if (move)
        {
            sourceEntries.RemoveAll(movedEntries.Contains);
            sourceGroups.RemoveAll(movedGroups.Contains);
            result.RemovedGroups = movedGroups.Count;
        }
        result.AddedDirectories = additions.Count;
        result.AddedGroups = newGroups.Count;
        return result;
    }
}
