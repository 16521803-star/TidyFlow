using System.Text.Json;
using TidyFlow.Core.Models;

namespace TidyFlow.Core.Safety;

/// <summary>
/// Executes validated OperationPlans and safely rolls back previous batches using cycle-safe two-phase moves.
/// </summary>
public sealed class BatchExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly string _historyDirectory;

    public BatchExecutor(string? historyDirectory = null)
    {
        _historyDirectory = string.IsNullOrWhiteSpace(historyDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TidyFlow",
                "history")
            : historyDirectory;
    }

    public BatchExecutionResult Execute(OperationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var batchId = Guid.NewGuid();
        var executable = plan.Operations.Where(o => o.WillModify).ToList();
        var skippedCount = plan.Operations.Count - executable.Count;

        if (executable.Count == 0)
        {
            return new BatchExecutionResult(batchId, 0, skippedCount, 0, Array.Empty<string>(), null);
        }

        var movePairs = executable
            .Select(o => new MovePair(o.SourcePath, o.DestinationPath))
            .ToList();

        var (appliedChanges, errors) = ExecuteMovePairs(movePairs);

        string? logPath = null;
        if (appliedChanges.Count > 0)
        {
            Directory.CreateDirectory(_historyDirectory);
            var transaction = new UndoTransaction(batchId, DateTimeOffset.Now, plan.Summary, appliedChanges);
            var fileName = $"{transaction.ExecutedAt:yyyyMMdd_HHmmss_fff}_{batchId:N}.json";
            logPath = Path.Combine(_historyDirectory, fileName);
            File.WriteAllText(logPath, JsonSerializer.Serialize(transaction, JsonOptions));
        }

        return new BatchExecutionResult(
            batchId,
            appliedChanges.Count,
            skippedCount,
            errors.Count,
            errors,
            logPath);
    }

    public IReadOnlyList<string> GetUndoLogFiles()
    {
        if (!Directory.Exists(_historyDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(_historyDirectory, "*.json")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
    }

    public UndoTransaction? PeekLatestUndoTransaction()
    {
        var latestLog = GetUndoLogFiles().FirstOrDefault();
        if (latestLog is null)
        {
            return null;
        }

        var json = File.ReadAllText(latestLog);
        return JsonSerializer.Deserialize<UndoTransaction>(json, JsonOptions);
    }

    public UndoResult UndoLatest()
    {
        var latestLog = GetUndoLogFiles().FirstOrDefault();
        if (latestLog is null)
        {
            return new UndoResult(Guid.Empty, 0, 0, new[] { "No undo history available." });
        }

        var json = File.ReadAllText(latestLog);
        var transaction = JsonSerializer.Deserialize<UndoTransaction>(json, JsonOptions);
        if (transaction is null)
        {
            return new UndoResult(Guid.Empty, 0, 1, new[] { "Failed to read undo transaction log." });
        }

        var reversePairs = transaction.Changes
            .Reverse()
            .Select(c => new MovePair(c.DestinationPath, c.SourcePath))
            .ToList();

        var (restoredChanges, errors) = ExecuteMovePairs(reversePairs);

        // Clean up empty directories that were created specifically by this batch.
        var candidateEmptyDirs = transaction.Changes
            .Where(c => c.CreatedDestinationDirectory)
            .Select(c => Path.GetDirectoryName(c.DestinationPath))
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(PathComparer);

        foreach (var dir in candidateEmptyDirs)
        {
            TryDeleteEmptyDirectory(dir!);
        }

        if (errors.Count == 0 && File.Exists(latestLog))
        {
            File.Delete(latestLog);
        }

        return new UndoResult(transaction.BatchId, restoredChanges.Count, errors.Count, errors);
    }

    private static (List<AppliedFileChange> Applied, List<string> Errors) ExecuteMovePairs(IReadOnlyList<MovePair> pairs)
    {
        var applied = new List<AppliedFileChange>(pairs.Count);
        var errors = new List<string>();

        var activeSources = new HashSet<string>(pairs.Select(p => p.SourcePath), PathComparer);
        var stagedMoves = new List<(string TempPath, MovePair Original, bool CreatedDestDir)>();
        var directMoves = new List<(MovePair Pair, bool CreatedDestDir)>();

        foreach (var pair in pairs)
        {
            if (!File.Exists(pair.SourcePath))
            {
                errors.Add($"Source file not found: {pair.SourcePath}");
                continue;
            }

            var destDir = Path.GetDirectoryName(pair.DestinationPath);
            var createdDir = false;
            if (!string.IsNullOrWhiteSpace(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
                createdDir = true;
            }

            // Stage via temporary path if destination is currently occupied by another moving source
            // or if this is a case-only rename on a case-insensitive filesystem (Windows).
            var isCaseOnlyRename = PathComparer.Equals(pair.SourcePath, pair.DestinationPath) &&
                                   !string.Equals(pair.SourcePath, pair.DestinationPath, StringComparison.Ordinal);
            var collidesWithMovingSource = activeSources.Contains(pair.DestinationPath);

            if (isCaseOnlyRename || collidesWithMovingSource)
            {
                var sourceDir = Path.GetDirectoryName(pair.SourcePath) ?? string.Empty;
                var tempPath = Path.Combine(sourceDir, $".tidyflow_tmp_{Guid.NewGuid():N}");
                try
                {
                    File.Move(pair.SourcePath, tempPath);
                    stagedMoves.Add((tempPath, pair, createdDir));
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to stage '{Path.GetFileName(pair.SourcePath)}': {ex.Message}");
                }
            }
            else
            {
                directMoves.Add((pair, createdDir));
            }
        }

        foreach (var (pair, createdDir) in directMoves)
        {
            try
            {
                if (File.Exists(pair.DestinationPath))
                {
                    errors.Add($"Destination already exists: {pair.DestinationPath}");
                    continue;
                }

                File.Move(pair.SourcePath, pair.DestinationPath);
                applied.Add(new AppliedFileChange(pair.SourcePath, pair.DestinationPath, createdDir));
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to move '{Path.GetFileName(pair.SourcePath)}': {ex.Message}");
            }
        }

        foreach (var (tempPath, pair, createdDir) in stagedMoves)
        {
            try
            {
                if (File.Exists(pair.DestinationPath))
                {
                    // Roll back staged file to its original path to keep disk consistent.
                    File.Move(tempPath, pair.SourcePath);
                    errors.Add($"Destination already exists: {pair.DestinationPath}");
                    continue;
                }

                File.Move(tempPath, pair.DestinationPath);
                applied.Add(new AppliedFileChange(pair.SourcePath, pair.DestinationPath, createdDir));
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to finalize '{Path.GetFileName(pair.SourcePath)}': {ex.Message}");
                TryRestoreTempFile(tempPath, pair.SourcePath);
            }
        }

        return (applied, errors);
    }

    private static void TryRestoreTempFile(string tempPath, string originalPath)
    {
        try
        {
            if (File.Exists(tempPath) && !File.Exists(originalPath))
            {
                File.Move(tempPath, originalPath);
            }
        }
        catch
        {
            // Best-effort recovery; error already recorded.
        }
    }

    private static void TryDeleteEmptyDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore locked folders.
        }
    }

    private sealed record MovePair(string SourcePath, string DestinationPath);
}
