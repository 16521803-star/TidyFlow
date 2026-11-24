using System.IO;
using TidyFlow.App.Services;
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
    public static string LocalizeStatus(OperationStatus status)
    {
        var loc = LocalizationService.Instance;
        return status switch
        {
            OperationStatus.Ready => loc.Get("Str_StatusReady", "Ready"),
            OperationStatus.ConflictResolved => loc.Get("Str_StatusAutoNumbered", "Auto-Numbered"),
            OperationStatus.Skipped => loc.Get("Str_StatusSkipped", "Skipped"),
            OperationStatus.Warning => loc.Get("Str_StatusWarning", "Warning"),
            OperationStatus.Error => loc.Get("Str_StatusErr", "Error"),
            _ => status.ToString()
        };
    }

    public static PreviewOperationRow FromPlanned(
        int index,
        PlannedFileOperation op,
        string? baseDirectoryForRelativeTarget = null)
    {
        var targetDisplay = op.Kind == OperationKind.MoveAndRename && op.WillModify
            ? $"{Path.GetFileName(Path.GetDirectoryName(op.DestinationPath))}\\{op.DestinationName}"
            : string.IsNullOrWhiteSpace(baseDirectoryForRelativeTarget)
                ? op.DestinationName
                : Path.GetRelativePath(baseDirectoryForRelativeTarget, op.DestinationPath);

        return new PreviewOperationRow(
            index,
            LocalizeStatus(op.Status),
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
