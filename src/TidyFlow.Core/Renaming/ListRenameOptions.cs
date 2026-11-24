using TidyFlow.Core.Safety;

namespace TidyFlow.Core.Renaming;

public enum ListMismatchBehavior
{
    SkipExtraFiles,
    PadWithOriginalNameAndIndex,
    PadWithPrefixAndIndex
}

public sealed record ListRenameOptions
{
    public IReadOnlyList<string> TargetNames { get; init; } = Array.Empty<string>();

    public bool PreserveOriginalExtension { get; init; } = true;

    public bool CleanDuplicateMarkers { get; init; } = false;

    public string Prefix { get; init; } = string.Empty;

    public string Suffix { get; init; } = string.Empty;

    public int StartIndex { get; init; } = 1;

    public int IndexPadding { get; init; } = 2;

    public string InvalidCharReplacement { get; init; } = "_";

    public ListMismatchBehavior MismatchBehavior { get; init; } = ListMismatchBehavior.SkipExtraFiles;

    public DuplicateNameBehavior DuplicateBehavior { get; init; } = DuplicateNameBehavior.AutoNumberSuffix;

    public static IReadOnlyList<string> ParseRawNameList(string? rawText, bool ignoreBlankLines = true)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return Array.Empty<string>();
        }

        var lines = rawText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        var result = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            var trimmed = line.Trim().Trim('"');
            if (ignoreBlankLines && string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            result.Add(trimmed);
        }

        return result;
    }
}

public sealed record TemplateRenameOptions
{
    public string Pattern { get; init; } = "{index}_{name}";

    public string? FindText { get; init; }

    public string ReplaceText { get; init; } = string.Empty;

    public bool CleanDuplicateMarkers { get; init; } = true;

    public int StartIndex { get; init; } = 1;

    public int IndexPadding { get; init; } = 3;

    public string DateFormat { get; init; } = "yyyy-MM-dd";

    public string InvalidCharReplacement { get; init; } = "_";

    public DuplicateNameBehavior DuplicateBehavior { get; init; } = DuplicateNameBehavior.AutoNumberSuffix;
}
