namespace TortoiseGitLauncher;

internal abstract class DirectoryPage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Name { get; set; } = "默认页";
    public int DisplayOrder { get; set; }

    // Selection is page-local, never keyed by a path across the whole module.
    public string LastSelectedDirectoryPath { get; set; } = string.Empty;

    public override string ToString() => Name;
}

internal static class DirectoryPageCollection
{
    public static TPage GetActive<TPage>(List<TPage> pages, string? selectedId)
        where TPage : DirectoryPage, new()
    {
        if (pages.Count == 0)
        {
            pages.Add(new TPage());
        }

        return pages.FirstOrDefault(page => string.Equals(page.Id, selectedId, StringComparison.OrdinalIgnoreCase))
               ?? pages[0];
    }

    public static string Normalize<TPage>(List<TPage> pages, string? selectedId)
        where TPage : DirectoryPage, new()
    {
        var ordered = pages.Where(page => page is not null).OrderBy(page => page.DisplayOrder).ToList();
        pages.Clear();
        pages.AddRange(ordered);
        if (pages.Count == 0)
        {
            pages.Add(new TPage());
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            if (string.IsNullOrWhiteSpace(page.Id) || !ids.Add(page.Id))
            {
                do { page.Id = Guid.NewGuid().ToString("D"); } while (!ids.Add(page.Id));
            }

            var name = string.IsNullOrWhiteSpace(page.Name) ? "未命名页" : page.Name.Trim();
            var candidate = name;
            for (var suffix = 2; !names.Add(candidate); suffix++)
            {
                candidate = $"{name} ({suffix})";
            }

            page.Name = candidate;
            page.DisplayOrder = index;
            page.LastSelectedDirectoryPath ??= string.Empty;
        }

        return GetActive(pages, selectedId).Id;
    }
}
