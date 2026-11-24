using System.Text.RegularExpressions;

namespace TidyFlow.Core.Renaming;

/// <summary>
/// Converts raw OCR text extracted from screenshots or scanned documents into clean, safe filename stems.
/// </summary>
public static partial class OcrNameSuggester
{
    public static string SuggestStem(
        string? ocrText,
        string fallbackStem,
        int maxLength = 48)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return fallbackStem;
        }

        var lines = ocrText
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            // Skip tiny noise lines (like single symbols or page numbers)
            var alphanumericCount = line.Count(char.IsLetterOrDigit);
            if (alphanumericCount < 3)
            {
                continue;
            }

            var collapsed = WhitespaceRegex().Replace(line, " ").Trim();
            var sanitized = FilenameSanitizer.SanitizeName(collapsed, "_");
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                continue;
            }

            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized[..maxLength].TrimEnd(' ', '_', '.', '-');
            }

            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                return sanitized;
            }
        }

        return fallbackStem;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
