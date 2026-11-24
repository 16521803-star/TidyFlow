using System.Text;
using System.Text.RegularExpressions;

namespace TidyFlow.Core.Renaming;

/// <summary>
/// Sanitizes filenames for Windows (removes forbidden characters, trailing dots/spaces, and reserved device names)
/// and optionally strips common duplicate download markers such as "(1)" or " - Copy".
/// </summary>
public static partial class FilenameSanitizer
{
    private static readonly HashSet<char> InvalidChars =
        Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToHashSet();

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string SanitizeName(string rawName, string replacement = "_")
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        var safeReplacement = string.IsNullOrEmpty(replacement)
            ? string.Empty
            : string.Concat(replacement.Where(c => !InvalidChars.Contains(c)));

        var sb = new StringBuilder(rawName.Length);
        foreach (var ch in rawName.Trim())
        {
            if (InvalidChars.Contains(ch) || char.IsControl(ch))
            {
                sb.Append(safeReplacement);
            }
            else
            {
                sb.Append(ch);
            }
        }

        var cleaned = sb.ToString().Trim().TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        var stem = Path.GetFileNameWithoutExtension(cleaned);
        if (ReservedWindowsNames.Contains(stem))
        {
            cleaned = $"_{cleaned}";
        }

        return cleaned;
    }

    public static string CleanDuplicateMarkers(string nameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
        {
            return nameWithoutExtension;
        }

        var result = CopyPrefixRegex().Replace(nameWithoutExtension.Trim(), string.Empty);
        result = CopySuffixRegex().Replace(result, string.Empty);
        result = TrailingNumberParenRegex().Replace(result, string.Empty);
        return result.Trim();
    }

    [GeneratedRegex(@"^Copy\s+of\s+", RegexOptions.IgnoreCase)]
    private static partial Regex CopyPrefixRegex();

    [GeneratedRegex(@"\s*-\s*Copy(\s*\(\d+\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex CopySuffixRegex();

    [GeneratedRegex(@"\s*\(\d+\)$")]
    private static partial Regex TrailingNumberParenRegex();
}
