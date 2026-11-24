using TidyFlow.Core.Models;
using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Cleaning;

public sealed record FolderSizeSummary(
    string FolderPath,
    string Name,
    long TotalBytes,
    int FileCount);

public sealed record EmptyFolderEntry(
    string FullPath,
    string RelativePath);

public sealed record SpaceScanResult(
    long TotalScannedBytes,
    int TotalFileCount,
    IReadOnlyList<FileEntry> LargestFiles,
    IReadOnlyList<FolderSizeSummary> TopSubfolders,
    IReadOnlyList<EmptyFolderEntry> EmptyFolders);

/// <summary>
/// Audits disk usage inside a directory to locate the heaviest files, largest subfolders, and empty folders.
/// </summary>
public sealed class SpaceCleaner
{
    private readonly FolderScanner _scanner = new();

    public SpaceScanResult Scan(
        string rootDirectory,
        int topFilesCount = 100,
        long minimumFileSizeBytes = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory must not be empty.", nameof(rootDirectory));
        }

        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {rootDirectory}");
        }

        var files = _scanner.Scan(rootDirectory, new FolderScanOptions
        {
            IncludeSubdirectories = true,
            IgnoreActiveDownloads = false,
            IncludeHiddenOrSystem = false,
            SortOrder = FileSortOrder.SizeDesc
        });

        var totalBytes = files.Sum(f => f.SizeBytes);

        var largestFiles = files
            .Where(f => f.SizeBytes >= minimumFileSizeBytes)
            .Take(Math.Max(1, topFilesCount))
            .ToList();

        var rootInfo = new DirectoryInfo(rootDirectory);
        var normalizedRoot = Path.GetFullPath(rootInfo.FullName)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var subfolderTotals = new Dictionary<string, (long Bytes, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rel = Path.GetRelativePath(normalizedRoot, file.FullPath);
            var sepIndex = rel.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            var bucketKey = sepIndex > 0 ? rel[..sepIndex] : "(Root Files)";

            if (subfolderTotals.TryGetValue(bucketKey, out var current))
            {
                subfolderTotals[bucketKey] = (current.Bytes + file.SizeBytes, current.Count + 1);
            }
            else
            {
                subfolderTotals[bucketKey] = (file.SizeBytes, 1);
            }
        }

        var topSubfolders = subfolderTotals
            .Select(kvp => new FolderSizeSummary(
                kvp.Key == "(Root Files)" ? normalizedRoot : Path.Combine(normalizedRoot, kvp.Key),
                kvp.Key,
                kvp.Value.Bytes,
                kvp.Value.Count))
            .OrderByDescending(s => s.TotalBytes)
            .ToList();

        var emptyFolders = FindEmptyDirectories(normalizedRoot, cancellationToken);

        return new SpaceScanResult(
            totalBytes,
            files.Count,
            largestFiles,
            topSubfolders,
            emptyFolders);
    }

    public static int DeleteEmptyFolders(IEnumerable<string> folderPaths)
    {
        var deleted = 0;

        // Delete deepest folders first so nested empty trees collapse cleanly.
        var ordered = folderPaths
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(p => p.Length);

        foreach (var path in ordered)
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(path).Any())
                {
                    Directory.Delete(path, recursive: false);
                    deleted++;
                }
            }
            catch
            {
                // Skip locked or in-use folders safely.
            }
        }

        return deleted;
    }

    private static IReadOnlyList<EmptyFolderEntry> FindEmptyDirectories(string rootPath, CancellationToken cancellationToken)
    {
        var results = new List<EmptyFolderEntry>();

        try
        {
            var subdirs = Directory.EnumerateDirectories(rootPath, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            })
            .OrderByDescending(p => p.Length);

            foreach (var dir in subdirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        results.Add(new EmptyFolderEntry(dir, Path.GetRelativePath(rootPath, dir)));
                    }
                }
                catch
                {
                    // Skip inaccessible folders.
                }
            }
        }
        catch
        {
            // Skip inaccessible root enumeration errors.
        }

        return results;
    }
}
