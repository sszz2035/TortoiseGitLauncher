namespace TortoiseGitLauncher;

internal static class DirectoryPageSelection
{
    // Never upsert a remembered path: it may have been explicitly removed by the user.
    public static TEntry? Restore<TEntry>(string? rememberedPath, IEnumerable<TEntry> entries,
        Func<TEntry, string> getPath) where TEntry : class
    {
        var available = entries.Where(entry => Directory.Exists(getPath(entry))).ToList();
        return available.FirstOrDefault(entry => string.Equals(
                   getPath(entry), rememberedPath, StringComparison.OrdinalIgnoreCase))
               ?? available.FirstOrDefault();
    }
}
