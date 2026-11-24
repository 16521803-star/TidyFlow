namespace TidyFlow.Core.Safety;

public sealed record AppliedFileChange(
    string SourcePath,
    string DestinationPath,
    bool CreatedDestinationDirectory);

public sealed record UndoTransaction(
    Guid BatchId,
    DateTimeOffset ExecutedAt,
    string Description,
    IReadOnlyList<AppliedFileChange> Changes);

public sealed record BatchExecutionResult(
    Guid BatchId,
    int AppliedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<string> Errors,
    string? UndoLogPath);

public sealed record UndoResult(
    Guid BatchId,
    int RestoredCount,
    int FailedCount,
    IReadOnlyList<string> Errors);
