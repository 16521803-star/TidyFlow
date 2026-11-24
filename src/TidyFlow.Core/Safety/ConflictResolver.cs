using TidyFlow.Core.Models;

namespace TidyFlow.Core.Safety;

public enum DuplicateNameBehavior
{
    AutoNumberSuffix,
    MarkError
}

public sealed record ProposedOperation(
    string SourcePath,
    string ProposedDestinationPath,
    OperationKind Kind,
    string? InitialNote = null,
    bool ForceSkip = false,
    bool ForceError = false);

/// <summary>
/// Resolves unchanged paths, batch-internal duplicates, and collisions with existing files on disk
/// so no file is ever silently overwritten.
/// </summary>
public sealed class ConflictResolver
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public OperationPlan Resolve(
        IReadOnlyList<ProposedOperation> proposals,
        string summary,
        DuplicateNameBehavior duplicateBehavior = DuplicateNameBehavior.AutoNumberSuffix)
    {
        ArgumentNullException.ThrowIfNull(proposals);

        // Sources that are genuinely moving out of their current path free up those paths for other files in the same batch.
        var vacatedSources = new HashSet<string>(PathComparer);
        foreach (var proposal in proposals)
        {
            if (proposal.ForceSkip || proposal.ForceError)
            {
                continue;
            }

            if (!string.Equals(proposal.SourcePath, proposal.ProposedDestinationPath, StringComparison.Ordinal))
            {
                vacatedSources.Add(proposal.SourcePath);
            }
        }

        var claimedDestinations = new HashSet<string>(PathComparer);
        var planned = new List<PlannedFileOperation>(proposals.Count);

        foreach (var proposal in proposals)
        {
            if (proposal.ForceError)
            {
                planned.Add(new PlannedFileOperation(
                    proposal.SourcePath,
                    proposal.ProposedDestinationPath,
                    proposal.Kind,
                    OperationStatus.Error,
                    proposal.InitialNote ?? "Invalid operation."));
                continue;
            }

            if (proposal.ForceSkip)
            {
                planned.Add(new PlannedFileOperation(
                    proposal.SourcePath,
                    proposal.SourcePath,
                    proposal.Kind,
                    OperationStatus.Skipped,
                    proposal.InitialNote ?? "Skipped."));
                continue;
            }

            // Detect exact no-op (note: case-only rename on Windows has StringComparison.Ordinal != OrdinalIgnoreCase, so allow it!)
            if (string.Equals(proposal.SourcePath, proposal.ProposedDestinationPath, StringComparison.Ordinal))
            {
                planned.Add(new PlannedFileOperation(
                    proposal.SourcePath,
                    proposal.ProposedDestinationPath,
                    proposal.Kind,
                    OperationStatus.Skipped,
                    proposal.InitialNote ?? "Already at target path."));
                continue;
            }

            var candidate = proposal.ProposedDestinationPath;
            var hasConflict = IsPathOccupied(candidate, proposal.SourcePath, claimedDestinations, vacatedSources);

            if (!hasConflict)
            {
                claimedDestinations.Add(candidate);
                planned.Add(new PlannedFileOperation(
                    proposal.SourcePath,
                    candidate,
                    proposal.Kind,
                    OperationStatus.Ready,
                    proposal.InitialNote));
                continue;
            }

            if (duplicateBehavior == DuplicateNameBehavior.MarkError)
            {
                planned.Add(new PlannedFileOperation(
                    proposal.SourcePath,
                    candidate,
                    proposal.Kind,
                    OperationStatus.Error,
                    $"Destination already exists or conflicts: {Path.GetFileName(candidate)}"));
                continue;
            }

            var resolved = FindUniquePath(candidate, proposal.SourcePath, claimedDestinations, vacatedSources);
            claimedDestinations.Add(resolved);
            planned.Add(new PlannedFileOperation(
                proposal.SourcePath,
                resolved,
                proposal.Kind,
                OperationStatus.ConflictResolved,
                $"Renamed to '{Path.GetFileName(resolved)}' to avoid conflict."));
        }

        return new OperationPlan(planned, DateTimeOffset.Now, summary);
    }

    private static bool IsPathOccupied(
        string candidatePath,
        string ownSourcePath,
        HashSet<string> claimedDestinations,
        HashSet<string> vacatedSources)
    {
        if (claimedDestinations.Contains(candidatePath))
        {
            return true;
        }

        // Case-only rename of the exact same file is not a collision.
        if (PathComparer.Equals(candidatePath, ownSourcePath))
        {
            return false;
        }

        if (File.Exists(candidatePath) && !vacatedSources.Contains(candidatePath))
        {
            return true;
        }

        return false;
    }

    private static string FindUniquePath(
        string desiredPath,
        string ownSourcePath,
        HashSet<string> claimedDestinations,
        HashSet<string> vacatedSources)
    {
        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        var counter = 1;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{baseName}_{counter}{extension}");
            if (!IsPathOccupied(candidate, ownSourcePath, claimedDestinations, vacatedSources))
            {
                return candidate;
            }

            counter++;
        }
    }
}
