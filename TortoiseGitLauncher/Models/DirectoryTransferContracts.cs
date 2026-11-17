namespace TortoiseGitLauncher;

// Explicit path views keep transfer behavior shared without changing the JSON schema.
internal interface IDirectoryTransferEntry<TEntry>
{
    string TransferPath { get; }
    string? GroupId { get; set; }
    int DisplayOrder { get; set; }
    TEntry Clone();
}

internal interface IDirectoryCollectionEntry<TEntry> : IDirectoryTransferEntry<TEntry>
{
    string DisplayName { get; set; }
    string Notes { get; set; }
}

internal interface IDirectoryTransferGroup<TGroup>
{
    string Id { get; set; }
    string Name { get; set; }
    int DisplayOrder { get; set; }
    TGroup Clone();
}
