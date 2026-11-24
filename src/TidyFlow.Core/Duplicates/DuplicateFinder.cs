using System.Security.Cryptography;
using TidyFlow.Core.Models;
using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Duplicates;

public sealed record DuplicateGroup(
    long FileSizeBytes,
    string Sha256Hash,
    IReadOnlyList<FileEntry> Files)
{
    public long WastedBytes => FileSizeBytes * Math.Max(0, Files.Count - 1);
}

public sealed record DuplicateScanResult(
    int TotalFilesScanned,
    IReadOnlyList<DuplicateGroup> Groups,
    long TotalWastedBytes);

/// <summary>
/// Finds identical files by grouping on exact byte size first, then verifying with a fast prefix + full SHA-256 hash.
/// </summary>
public sealed class DuplicateFinder
{
    private const int QuickSampleBytes = 4096;

    private readonly FolderScanner _scanner = new();

    public DuplicateScanResult Scan(
        string directoryPath,
        bool includeSubdirectories = true,
        bool includeEmptyFiles = false,
        CancellationToken cancellationToken = default)
    {
        var files = _scanner.Scan(directoryPath, new FolderScanOptions
        {
            IncludeSubdirectories = includeSubdirectories,
            IgnoreActiveDownloads = true,
            SortOrder = FileSortOrder.NameAsc
        });

        var sizeBuckets = files
            .Where(f => includeEmptyFiles || f.SizeBytes > 0)
            .GroupBy(f => f.SizeBytes)
            .Where(g => g.Count() > 1);

        var groups = new List<DuplicateGroup>();

        foreach (var sizeBucket in sizeBuckets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Pre-filter with first 4KB hash before reading entire large files.
            var prefixBuckets = new Dictionary<string, List<FileEntry>>(StringComparer.Ordinal);
            foreach (var entry in sizeBucket)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var quickHash = TryComputeHash(entry.FullPath, QuickSampleBytes);
                if (quickHash is null)
                {
                    continue;
                }

                if (!prefixBuckets.TryGetValue(quickHash, out var list))
                {
                    list = new List<FileEntry>();
                    prefixBuckets[quickHash] = list;
                }

                list.Add(entry);
            }

            foreach (var candidates in prefixBuckets.Values.Where(v => v.Count > 1))
            {
                var fullHashBuckets = new Dictionary<string, List<FileEntry>>(StringComparer.Ordinal);
                foreach (var entry in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fullHash = TryComputeHash(entry.FullPath, maxBytes: null);
                    if (fullHash is null)
                    {
                        continue;
                    }

                    if (!fullHashBuckets.TryGetValue(fullHash, out var list))
                    {
                        list = new List<FileEntry>();
                        fullHashBuckets[fullHash] = list;
                    }

                    list.Add(entry);
                }

                foreach (var (hash, dupes) in fullHashBuckets)
                {
                    if (dupes.Count > 1)
                    {
                        var ordered = dupes.OrderBy(d => d.LastWriteTime).ThenBy(d => d.FullPath, StringComparer.OrdinalIgnoreCase).ToList();
                        groups.Add(new DuplicateGroup(sizeBucket.Key, hash, ordered));
                    }
                }
            }
        }

        var sortedGroups = groups
            .OrderByDescending(g => g.WastedBytes)
            .ThenByDescending(g => g.Files.Count)
            .ToList();

        var wasted = sortedGroups.Sum(g => g.WastedBytes);
        return new DuplicateScanResult(files.Count, sortedGroups, wasted);
    }

    private static string? TryComputeHash(string filePath, int? maxBytes)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = SHA256.Create();

            if (!maxBytes.HasValue || stream.Length <= maxBytes.Value)
            {
                return Convert.ToHexString(sha.ComputeHash(stream));
            }

            var buffer = new byte[maxBytes.Value];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return Convert.ToHexString(sha.ComputeHash(buffer, 0, totalRead));
        }
        catch
        {
            return null;
        }
    }
}
