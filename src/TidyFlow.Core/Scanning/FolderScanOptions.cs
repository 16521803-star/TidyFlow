namespace TidyFlow.Core.Scanning;

public enum FileSortOrder
{
    NameAsc,
    NameDesc,
    ModifiedAsc,
    ModifiedDesc,
    SizeAsc,
    SizeDesc
}

public sealed record FolderScanOptions
{
    public bool IncludeSubdirectories { get; init; } = false;

    public bool IncludeHiddenOrSystem { get; init; } = false;

    public bool IgnoreActiveDownloads { get; init; } = true;

    public IReadOnlyCollection<string>? ExtensionFilter { get; init; }

    public FileSortOrder SortOrder { get; init; } = FileSortOrder.NameAsc;
}
