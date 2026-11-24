using System.Collections.Concurrent;
using TidyFlow.Core.Models;
using TidyFlow.Core.Organizing;
using TidyFlow.Core.Safety;

namespace TidyFlow.Core.Watching;

public sealed record WatchOrganizeEventArgs(
    int MovedCount,
    IReadOnlyList<string> MovedDescriptions,
    string? ErrorMessage = null);

/// <summary>
/// Watches a directory for newly completed files and automatically organizes them using OrganizeOptions + BatchExecutor.
/// </summary>
public sealed class FolderWatchService : IDisposable
{
    private static readonly HashSet<string> IgnoredTempExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload",
        ".part",
        ".tmp",
        ".opdownload",
        ".download"
    };

    private readonly FolderOrganizer _organizer = new();
    private readonly BatchExecutor _executor;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private string _watchDirectory = string.Empty;
    private OrganizeOptions _organizeOptions = new();
    private bool _disposed;

    public FolderWatchService(BatchExecutor? executor = null)
    {
        _executor = executor ?? new BatchExecutor();
    }

    public event EventHandler<WatchOrganizeEventArgs>? BatchProcessed;

    public bool IsWatching { get; private set; }

    public string WatchDirectory => _watchDirectory;

    public void Start(string directoryPath, OrganizeOptions options)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Watch directory not found: {directoryPath}");
        }

        ArgumentNullException.ThrowIfNull(options);

        Stop();

        _watchDirectory = directoryPath;
        _organizeOptions = options;
        _pendingPaths.Clear();

        _watcher = new FileSystemWatcher(directoryPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileActivity;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Changed += OnFileActivity;

        _debounceTimer = new Timer(_ => ProcessPending(), null, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(1.5));
        IsWatching = true;
    }

    public void Stop()
    {
        lock (_sync)
        {
            IsWatching = false;
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileActivity;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Changed -= OnFileActivity;
                _watcher.Dispose();
                _watcher = null;
            }

            _pendingPaths.Clear();
        }
    }

    private void OnFileActivity(object sender, FileSystemEventArgs e)
        => EnqueueIfEligible(e.FullPath);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
        => EnqueueIfEligible(e.FullPath);

    private void EnqueueIfEligible(string fullPath)
    {
        if (!IsWatching || string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        var ext = Path.GetExtension(fullPath);
        var name = Path.GetFileName(fullPath);
        if (IgnoredTempExtensions.Contains(ext) || name.StartsWith("~$", StringComparison.Ordinal))
        {
            return;
        }

        _pendingPaths[fullPath] = DateTimeOffset.Now;
    }

    private void ProcessPending()
    {
        if (!Monitor.TryEnter(_sync))
        {
            return;
        }

        try
        {
            if (!IsWatching || _pendingPaths.IsEmpty)
            {
                return;
            }

            var cutoff = DateTimeOffset.Now.AddMilliseconds(-900);
            var readyEntries = new List<FileEntry>();

            foreach (var kvp in _pendingPaths.ToArray())
            {
                if (kvp.Value > cutoff)
                {
                    continue;
                }

                var path = kvp.Key;
                if (!File.Exists(path))
                {
                    _pendingPaths.TryRemove(path, out _);
                    continue;
                }

                if (!IsFileUnlocked(path))
                {
                    continue;
                }

                _pendingPaths.TryRemove(path, out _);
                var info = new FileInfo(path);
                if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    continue;
                }

                readyEntries.Add(FileEntry.FromFileInfo(info));
            }

            if (readyEntries.Count == 0)
            {
                return;
            }

            var plan = _organizer.BuildPlan(_watchDirectory, readyEntries, _organizeOptions);
            if (plan.ReadyCount == 0)
            {
                return;
            }

            var result = _executor.Execute(plan);
            var descriptions = plan.Operations
                .Where(o => o.WillModify)
                .Select(o => $"{o.SourceName} -> {Path.GetRelativePath(_watchDirectory, o.DestinationPath)}")
                .ToList();

            var error = result.Errors.Count > 0 ? string.Join("; ", result.Errors) : null;
            BatchProcessed?.Invoke(this, new WatchOrganizeEventArgs(result.AppliedCount, descriptions, error));
        }
        catch (Exception ex)
        {
            BatchProcessed?.Invoke(this, new WatchOrganizeEventArgs(0, Array.Empty<string>(), ex.Message));
        }
        finally
        {
            Monitor.Exit(_sync);
        }
    }

    private static bool IsFileUnlocked(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }
}
