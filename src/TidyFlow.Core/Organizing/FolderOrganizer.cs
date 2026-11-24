using TidyFlow.Core.Metadata;
using TidyFlow.Core.Models;
using TidyFlow.Core.Renaming;
using TidyFlow.Core.Safety;

namespace TidyFlow.Core.Organizing;

/// <summary>
/// Builds a dry-run preview plan to reorder files into structured subfolders by category, date, or custom rules.
/// </summary>
public sealed class FolderOrganizer
{
    private readonly ConflictResolver _conflictResolver;

    public FolderOrganizer(ConflictResolver? conflictResolver = null)
    {
        _conflictResolver = conflictResolver ?? new ConflictResolver();
    }

    public OperationPlan BuildPlan(
        string sourceRootDirectory,
        IReadOnlyList<FileEntry> files,
        OrganizeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(sourceRootDirectory))
        {
            throw new ArgumentException("Source directory must not be empty.", nameof(sourceRootDirectory));
        }

        ArgumentNullException.ThrowIfNull(files);
        options ??= new OrganizeOptions();

        var targetRoot = string.IsNullOrWhiteSpace(options.TargetRootDirectory)
            ? sourceRootDirectory
            : options.TargetRootDirectory;

        var now = DateTimeOffset.Now;
        var proposals = new List<ProposedOperation>(files.Count);

        foreach (var file in files)
        {
            if (options.MinimumAgeDays is > 0 && !MeetsMinimumAge(file, options.MinimumAgeDays.Value, now))
            {
                proposals.Add(new ProposedOperation(
                    file.FullPath,
                    file.FullPath,
                    OperationKind.Move,
                    InitialNote: $"Newer than {options.MinimumAgeDays.Value} day(s).",
                    ForceSkip: true));
                continue;
            }

            var relativeSubfolder = ResolveSubfolder(file, options, now);
            if (relativeSubfolder is null)
            {
                proposals.Add(new ProposedOperation(
                    file.FullPath,
                    file.FullPath,
                    OperationKind.Move,
                    InitialNote: "No matching rule.",
                    ForceSkip: true));
                continue;
            }

            var targetDirectory = Path.Combine(targetRoot, relativeSubfolder);
            var destinationPath = Path.Combine(targetDirectory, file.Name);

            proposals.Add(new ProposedOperation(
                file.FullPath,
                destinationPath,
                OperationKind.Move,
                InitialNote: $"-> {relativeSubfolder}"));
        }

        return _conflictResolver.Resolve(
            proposals,
            $"Organize {files.Count} file(s) ({options.Mode})",
            options.DuplicateBehavior);
    }

    private static string? ResolveSubfolder(FileEntry file, OrganizeOptions options, DateTimeOffset now)
    {
        return options.Mode switch
        {
            OrganizeMode.ByCategory => FileCategoryRegistry.GetCategory(file.Extension, options.DefaultOtherFolder),
            OrganizeMode.ByDate => FormatDateFolder(file, options),
            OrganizeMode.ByCustomRulesFirstThenCategory =>
                MatchCustomRule(file, options.CustomRules, now)
                ?? FileCategoryRegistry.GetCategory(file.Extension, options.DefaultOtherFolder),
            OrganizeMode.ByCustomRulesOnly => MatchCustomRule(file, options.CustomRules, now),
            _ => FileCategoryRegistry.GetCategory(file.Extension, options.DefaultOtherFolder)
        };
    }

    private static string FormatDateFolder(FileEntry file, OrganizeOptions options)
    {
        var exifDate = options.UseExifDateWhenAvailable
            ? ExifDateReader.TryReadDateTaken(file.FullPath)
            : null;
        var timestamp = exifDate ?? (options.UseCreationTimeForDate ? file.CreationTime : file.LastWriteTime);
        return options.DateFormat switch
        {
            DateGroupingFormat.YearMonth => timestamp.ToString("yyyy-MM"),
            DateGroupingFormat.YearSlashMonth => Path.Combine(timestamp.ToString("yyyy"), timestamp.ToString("MM")),
            DateGroupingFormat.YearOnly => timestamp.ToString("yyyy"),
            _ => timestamp.ToString("yyyy-MM")
        };
    }

    private static string? MatchCustomRule(
        FileEntry file,
        IReadOnlyList<KeywordOrganizeRule> rules,
        DateTimeOffset now)
    {
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.TargetFolderName))
            {
                continue;
            }

            if (rule.MinimumAgeDays is > 0 && !MeetsMinimumAge(file, rule.MinimumAgeDays.Value, now))
            {
                continue;
            }

            var hasExtensions = rule.Extensions.Any(e => !string.IsNullOrWhiteSpace(e));
            var hasKeywords = rule.Keywords.Any(k => !string.IsNullOrWhiteSpace(k));

            var extensionMatch = hasExtensions && rule.Extensions.Any(ext =>
            {
                if (string.IsNullOrWhiteSpace(ext))
                {
                    return false;
                }

                var normalized = ext.Trim().StartsWith('.') ? ext.Trim() : $".{ext.Trim()}";
                return string.Equals(file.Extension, normalized, StringComparison.OrdinalIgnoreCase);
            });

            var keywordMatch = hasKeywords && rule.Keywords.Any(kw =>
                !string.IsNullOrWhiteSpace(kw) &&
                file.NameWithoutExtension.Contains(kw.Trim(), StringComparison.OrdinalIgnoreCase));

            bool isMatch;
            if (!hasExtensions && !hasKeywords)
            {
                isMatch = rule.MinimumAgeDays is > 0;
            }
            else if (rule.MatchLogic == RuleMatchLogic.MatchAll)
            {
                isMatch = (!hasExtensions || extensionMatch) && (!hasKeywords || keywordMatch);
            }
            else
            {
                isMatch = extensionMatch || keywordMatch;
            }

            if (isMatch)
            {
                return SanitizeRelativeSubpath(rule.TargetFolderName);
            }
        }

        return null;
    }

    private static bool MeetsMinimumAge(FileEntry file, int minimumAgeDays, DateTimeOffset now)
        => (now - file.LastWriteTime).TotalDays >= minimumAgeDays;

    private static string SanitizeRelativeSubpath(string rawFolderPath)
    {
        var segments = rawFolderPath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s is not "." and not "..")
            .Select(s => FilenameSanitizer.SanitizeName(s, "_"))
            .Where(s => !string.IsNullOrWhiteSpace(s));

        return Path.Combine(segments.ToArray());
    }
}
