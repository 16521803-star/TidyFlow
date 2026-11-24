using TidyFlow.Core.Models;

namespace TidyFlow.Core.Scanning;

/// <summary>
/// Safely enumerates files in a directory while skipping active browser downloads and system locks.
/// </summary>
public sealed class FolderScanner
{
    private static readonly HashSet<string> ActiveDownloadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload",
        ".part",
        ".tmp",
        ".opdownload",
        ".download"
    };

    public IReadOnlyList<FileEntry> Scan(string directoryPath, FolderScanOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path must not be empty.", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        options ??= new FolderScanOptions();

        var normalizedFilter = NormalizeExtensions(options.ExtensionFilter);
        var searchOption = options.IncludeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            IgnoreInaccessible = true,
            AttributesToSkip = options.IncludeHiddenOrSystem
                ? FileAttributes.None
                : FileAttributes.Hidden | FileAttributes.System
        };

        var entries = new List<FileEntry>();
        var dirInfo = new DirectoryInfo(directoryPath);

        foreach (var file in dirInfo.EnumerateFiles("*", enumerationOptions))
        {
            if (options.IgnoreActiveDownloads && IsInProgressOrLockFile(file))
            {
                continue;
            }

            if (normalizedFilter is not null && !normalizedFilter.Contains(file.Extension))
            {
                continue;
            }

            entries.Add(FileEntry.FromFileInfo(file));
        }

        return SortEntries(entries, options.SortOrder);
    }

    private static bool IsInProgressOrLockFile(FileInfo file)
    {
        if (ActiveDownloadExtensions.Contains(file.Extension))
        {
            return true;
        }

        // Skip Office temporary lock files such as ~$Document.docx
        return file.Name.StartsWith("~$", StringComparison.Ordinal);
    }

    private static HashSet<string>? NormalizeExtensions(IReadOnlyCollection<string>? extensions)
    {
        if (extensions is null || extensions.Count == 0)
        {
            return null;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ext in extensions)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                continue;
            }

            var trimmed = ext.Trim();
            set.Add(trimmed.StartsWith('.') ? trimmed : $".{trimmed}");
        }

        return set.Count == 0 ? null : set;
    }

    private static IReadOnlyList<FileEntry> SortEntries(List<FileEntry> entries, FileSortOrder sortOrder)
    {
        return sortOrder switch
        {
            FileSortOrder.NameAsc => entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            FileSortOrder.NameDesc => entries.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            FileSortOrder.ModifiedAsc => entries.OrderBy(e => e.LastWriteTime).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            FileSortOrder.ModifiedDesc => entries.OrderByDescending(e => e.LastWriteTime).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            FileSortOrder.SizeAsc => entries.OrderBy(e => e.SizeBytes).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            FileSortOrder.SizeDesc => entries.OrderByDescending(e => e.SizeBytes).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => entries
        };
    }
}
