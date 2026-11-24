using System.IO;
using TidyFlow.Core.Models;

namespace TidyFlow.App.Models;

public sealed record PreviewOperationRow(
    int Index,
    string StatusText,
    OperationStatus Status,
    string SourceName,
    string TargetDisplay,
    string Note)
{
    public static PreviewOperationRow FromPlanned(
        int index,
        PlannedFileOperation op,
        string? baseDirectoryForRelativeTarget = null)
    {
        var targetDisplay = string.IsNullOrWhiteSpace(baseDirectoryForRelativeTarget)
            ? op.DestinationName
            : Path.GetRelativePath(baseDirectoryForRelativeTarget, op.DestinationPath);

        var statusText = op.Status switch
        {
            OperationStatus.Ready => "Ready",
            OperationStatus.ConflictResolved => "Auto-Numbered",
            OperationStatus.Skipped => "Skipped",
            OperationStatus.Warning => "Warning",
            OperationStatus.Error => "Error",
            _ => op.Status.ToString()
        };

        return new PreviewOperationRow(
            index,
            statusText,
            op.Status,
            op.SourceName,
            targetDisplay,
            op.Note ?? string.Empty);
    }
}

public sealed record OrderedRenameFileItem(
    int Index,
    FileEntry Entry)
{
    public string Name => Entry.Name;

    public string Extension => Entry.Extension;

    public string FolderName => Path.GetFileName(Entry.DirectoryPath);
}
