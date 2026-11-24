using TidyFlow.Core.Models;
using TidyFlow.Core.Safety;

namespace TidyFlow.Core.Renaming;

/// <summary>
/// Builds dry-run rename plans from an ordered list of user-chosen names or a naming template.
/// </summary>
public sealed class BatchRenamer
{
    private readonly ConflictResolver _conflictResolver;

    public BatchRenamer(ConflictResolver? conflictResolver = null)
    {
        _conflictResolver = conflictResolver ?? new ConflictResolver();
    }

    public OperationPlan BuildListRenamePlan(
        IReadOnlyList<FileEntry> orderedFiles,
        ListRenameOptions options)
    {
        ArgumentNullException.ThrowIfNull(orderedFiles);
        ArgumentNullException.ThrowIfNull(options);

        var proposals = new List<ProposedOperation>(orderedFiles.Count);

        for (var i = 0; i < orderedFiles.Count; i++)
        {
            var file = orderedFiles[i];
            var indexNumber = options.StartIndex + i;
            var paddedIndex = indexNumber.ToString().PadLeft(Math.Max(1, options.IndexPadding), '0');

            string? rawTargetName;
            string? fallbackNote = null;

            if (i < options.TargetNames.Count)
            {
                rawTargetName = options.TargetNames[i];
                if (string.IsNullOrWhiteSpace(rawTargetName))
                {
                    proposals.Add(new ProposedOperation(
                        file.FullPath,
                        file.FullPath,
                        OperationKind.Rename,
                        InitialNote: $"List entry #{i + 1} is empty.",
                        ForceSkip: true));
                    continue;
                }
            }
            else
            {
                switch (options.MismatchBehavior)
                {
                    case ListMismatchBehavior.SkipExtraFiles:
                        proposals.Add(new ProposedOperation(
                            file.FullPath,
                            file.FullPath,
                            OperationKind.Rename,
                            InitialNote: "No corresponding item in name list.",
                            ForceSkip: true));
                        continue;

                    case ListMismatchBehavior.PadWithOriginalNameAndIndex:
                        var baseStem = options.CleanDuplicateMarkers
                            ? FilenameSanitizer.CleanDuplicateMarkers(file.NameWithoutExtension)
                            : file.NameWithoutExtension;
                        rawTargetName = $"{paddedIndex}_{baseStem}";
                        fallbackNote = "Padded from original name (beyond list length).";
                        break;

                    case ListMismatchBehavior.PadWithPrefixAndIndex:
                        var stemPrefix = string.IsNullOrWhiteSpace(options.Prefix) ? "File_" : options.Prefix;
                        rawTargetName = $"{stemPrefix}{paddedIndex}";
                        fallbackNote = "Padded with index (beyond list length).";
                        break;

                    default:
                        continue;
                }
            }

            var finalFileName = BuildTargetFileName(
                rawTargetName,
                file.Extension,
                options.PreserveOriginalExtension,
                options.CleanDuplicateMarkers,
                i < options.TargetNames.Count ? options.Prefix : string.Empty,
                options.Suffix,
                options.InvalidCharReplacement);

            if (string.IsNullOrWhiteSpace(finalFileName))
            {
                proposals.Add(new ProposedOperation(
                    file.FullPath,
                    file.FullPath,
                    OperationKind.Rename,
                    InitialNote: $"Target name '{rawTargetName}' became empty after sanitization.",
                    ForceError: true));
                continue;
            }

            var destinationPath = Path.Combine(file.DirectoryPath, finalFileName);
            proposals.Add(new ProposedOperation(
                file.FullPath,
                destinationPath,
                OperationKind.Rename,
                InitialNote: fallbackNote));
        }

        return _conflictResolver.Resolve(
            proposals,
            $"List rename for {orderedFiles.Count} file(s)",
            options.DuplicateBehavior);
    }

    public OperationPlan BuildTemplateRenamePlan(
        IReadOnlyList<FileEntry> orderedFiles,
        TemplateRenameOptions options)
    {
        ArgumentNullException.ThrowIfNull(orderedFiles);
        ArgumentNullException.ThrowIfNull(options);

        var proposals = new List<ProposedOperation>(orderedFiles.Count);

        for (var i = 0; i < orderedFiles.Count; i++)
        {
            var file = orderedFiles[i];
            var baseName = file.NameWithoutExtension;

            if (options.CleanDuplicateMarkers)
            {
                baseName = FilenameSanitizer.CleanDuplicateMarkers(baseName);
            }

            if (!string.IsNullOrEmpty(options.FindText))
            {
                baseName = baseName.Replace(options.FindText, options.ReplaceText, StringComparison.OrdinalIgnoreCase);
            }

            var indexValue = (options.StartIndex + i).ToString().PadLeft(Math.Max(1, options.IndexPadding), '0');
            var dateValue = file.LastWriteTime.ToString(options.DateFormat);

            var formattedStem = (string.IsNullOrWhiteSpace(options.Pattern) ? "{name}" : options.Pattern)
                .Replace("{name}", baseName, StringComparison.OrdinalIgnoreCase)
                .Replace("{index}", indexValue, StringComparison.OrdinalIgnoreCase)
                .Replace("{date}", dateValue, StringComparison.OrdinalIgnoreCase)
                .Replace("{ext}", file.Extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase);

            var sanitizedStem = FilenameSanitizer.SanitizeName(formattedStem, options.InvalidCharReplacement);
            if (string.IsNullOrWhiteSpace(sanitizedStem))
            {
                proposals.Add(new ProposedOperation(
                    file.FullPath,
                    file.FullPath,
                    OperationKind.Rename,
                    InitialNote: "Generated filename is empty.",
                    ForceError: true));
                continue;
            }

            var destinationPath = Path.Combine(file.DirectoryPath, $"{sanitizedStem}{file.Extension}");
            proposals.Add(new ProposedOperation(file.FullPath, destinationPath, OperationKind.Rename));
        }

        return _conflictResolver.Resolve(
            proposals,
            $"Template rename for {orderedFiles.Count} file(s)",
            options.DuplicateBehavior);
    }

    private static string BuildTargetFileName(
        string rawTargetName,
        string originalExtension,
        bool preserveOriginalExtension,
        bool cleanDuplicateMarkers,
        string prefix,
        string suffix,
        string invalidCharReplacement)
    {
        string stem;
        string extension;

        if (preserveOriginalExtension)
        {
            // Keep the file's real extension even if the user included a dot in the name, unless it matches the original extension.
            stem = !string.IsNullOrEmpty(originalExtension) &&
                   rawTargetName.EndsWith(originalExtension, StringComparison.OrdinalIgnoreCase)
                ? rawTargetName[..^originalExtension.Length]
                : rawTargetName;
            extension = originalExtension;
        }
        else
        {
            var parsedExt = Path.GetExtension(rawTargetName);
            stem = string.IsNullOrEmpty(parsedExt) ? rawTargetName : Path.GetFileNameWithoutExtension(rawTargetName);
            extension = string.IsNullOrEmpty(parsedExt) ? originalExtension : parsedExt;
        }

        if (cleanDuplicateMarkers)
        {
            stem = FilenameSanitizer.CleanDuplicateMarkers(stem);
        }

        var combinedStem = $"{prefix}{stem}{suffix}";
        var sanitizedStem = FilenameSanitizer.SanitizeName(combinedStem, invalidCharReplacement);

        if (string.IsNullOrWhiteSpace(sanitizedStem))
        {
            return string.Empty;
        }

        return $"{sanitizedStem}{extension}";
    }
}
