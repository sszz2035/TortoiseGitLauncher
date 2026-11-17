namespace TortoiseGitLauncher;

internal static class DirectoryCollectionNormalizer
{
    public static string EnsureUniqueId(string? value, HashSet<string> usedIds)
    {
        var candidate = Guid.TryParse(value, out var parsed)
            ? parsed.ToString("D")
            : Guid.NewGuid().ToString("D");
        while (!usedIds.Add(candidate))
        {
            candidate = Guid.NewGuid().ToString("D");
        }

        return candidate;
    }

    public static string EnsureUniqueGroupName(string? value, HashSet<string> usedNames)
    {
        var baseName = string.IsNullOrWhiteSpace(value) ? "未命名组" : value.Trim();
        var candidate = baseName;
        for (var suffix = 2; !usedNames.Add(candidate); suffix++)
        {
            candidate = $"{baseName} ({suffix})";
        }

        return candidate;
    }

    public static void NormalizeGroups<TGroup>(IList<TGroup> groups)
        where TGroup : IDirectoryTransferGroup<TGroup>
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            group.Id = EnsureUniqueId(group.Id, usedIds);
            group.Name = EnsureUniqueGroupName(group.Name, usedNames);
        }
    }

    public static void NormalizeEntries<TEntry, TGroup>(
        IList<TEntry> entries,
        IEnumerable<TGroup> groups,
        Func<TEntry, string> getPath,
        Action<TEntry, string> setPath,
        Func<string, string> normalizePath)
        where TEntry : IDirectoryCollectionEntry<TEntry>
        where TGroup : IDirectoryTransferGroup<TGroup>
    {
        var knownGroupIds = groups.Select(group => group.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedEntries = entries
            .Select((entry, sourceIndex) => (Entry: entry, SourceIndex: sourceIndex))
            .OrderBy(item => item.Entry?.DisplayOrder ?? int.MaxValue)
            .ThenBy(item => item.SourceIndex)
            .Select(item => item.Entry)
            .Where(entry => entry is not null)
            .ToList();
        var retainedEntries = new List<TEntry>(normalizedEntries.Count);

        foreach (var entry in normalizedEntries)
        {
            var path = getPath(entry);
            if (string.IsNullOrWhiteSpace(path)) continue;

            try
            {
                path = normalizePath(path);
            }
            catch
            {
                continue;
            }

            if (!uniquePaths.Add(path)) continue;

            setPath(entry, path);
            entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
            entry.Notes ??= string.Empty;
            entry.GroupId = !string.IsNullOrWhiteSpace(entry.GroupId) && knownGroupIds.Contains(entry.GroupId)
                ? entry.GroupId
                : null;
            retainedEntries.Add(entry);
        }

        entries.Clear();
        foreach (var entry in retainedEntries) entries.Add(entry);
    }
}