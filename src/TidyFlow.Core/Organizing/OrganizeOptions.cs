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

public enum RuleMatchLogic
{
    MatchAny,
    MatchAll
}

public sealed record KeywordOrganizeRule(
    string TargetFolderName,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Extensions,
    RuleMatchLogic MatchLogic = RuleMatchLogic.MatchAny,
    int? MinimumAgeDays = null);

public sealed record OrganizeOptions
{
    public OrganizeMode Mode { get; init; } = OrganizeMode.ByCategory;

    public DateGroupingFormat DateFormat { get; init; } = DateGroupingFormat.YearMonth;

    public bool UseCreationTimeForDate { get; init; } = false;

    public bool UseExifDateWhenAvailable { get; init; } = true;

    public IReadOnlyList<KeywordOrganizeRule> CustomRules { get; init; } = Array.Empty<KeywordOrganizeRule>();

    public string DefaultOtherFolder { get; init; } = "Others";

    public string? TargetRootDirectory { get; init; }

    public int? MinimumAgeDays { get; init; }

    public DuplicateNameBehavior DuplicateBehavior { get; init; } = DuplicateNameBehavior.AutoNumberSuffix;
}
