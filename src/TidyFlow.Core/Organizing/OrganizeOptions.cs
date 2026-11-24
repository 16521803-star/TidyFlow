using TidyFlow.Core.Safety;

namespace TidyFlow.Core.Organizing;

public enum OrganizeMode
{
    ByCategory,
    ByDate,
    ByCustomRulesFirstThenCategory,
    ByCustomRulesOnly
}

public enum DateGroupingFormat
{
    YearMonth,
    YearSlashMonth,
    YearOnly
}

public sealed record KeywordOrganizeRule(
    string TargetFolderName,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Extensions);

public sealed record OrganizeOptions
{
    public OrganizeMode Mode { get; init; } = OrganizeMode.ByCategory;

    public DateGroupingFormat DateFormat { get; init; } = DateGroupingFormat.YearMonth;

    public bool UseCreationTimeForDate { get; init; } = false;

    public IReadOnlyList<KeywordOrganizeRule> CustomRules { get; init; } = Array.Empty<KeywordOrganizeRule>();

    public string DefaultOtherFolder { get; init; } = "Others";

    public string? TargetRootDirectory { get; init; }

    public DuplicateNameBehavior DuplicateBehavior { get; init; } = DuplicateNameBehavior.AutoNumberSuffix;
}
