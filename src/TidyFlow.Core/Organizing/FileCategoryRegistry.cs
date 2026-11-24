namespace TidyFlow.Core.Organizing;

/// <summary>
/// Maps common file extensions to clean user-facing folder categories.
/// </summary>
public static class FileCategoryRegistry
{
    private static readonly Dictionary<string, string> ExtensionToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        [".pdf"] = "Documents",
        [".doc"] = "Documents",
        [".docx"] = "Documents",
        [".xls"] = "Documents",
        [".xlsx"] = "Documents",
        [".csv"] = "Documents",
        [".ppt"] = "Documents",
        [".pptx"] = "Documents",
        [".txt"] = "Documents",
        [".md"] = "Documents",
        [".rtf"] = "Documents",
        [".odt"] = "Documents",
        [".epub"] = "Documents",

        // Images
        [".jpg"] = "Images",
        [".jpeg"] = "Images",
        [".png"] = "Images",
        [".gif"] = "Images",
        [".webp"] = "Images",
        [".bmp"] = "Images",
        [".svg"] = "Images",
        [".ico"] = "Images",
        [".heic"] = "Images",
        [".tif"] = "Images",
        [".tiff"] = "Images",
        [".raw"] = "Images",

        // Videos
        [".mp4"] = "Videos",
        [".mkv"] = "Videos",
        [".avi"] = "Videos",
        [".mov"] = "Videos",
        [".wmv"] = "Videos",
        [".webm"] = "Videos",
        [".flv"] = "Videos",
        [".m4v"] = "Videos",

        // Audio
        [".mp3"] = "Audio",
        [".wav"] = "Audio",
        [".flac"] = "Audio",
        [".aac"] = "Audio",
        [".ogg"] = "Audio",
        [".m4a"] = "Audio",
        [".wma"] = "Audio",

        // Installers
        [".exe"] = "Installers",
        [".msi"] = "Installers",
        [".msix"] = "Installers",
        [".appx"] = "Installers",
        [".bat"] = "Installers",

        // Archives
        [".zip"] = "Archives",
        [".rar"] = "Archives",
        [".7z"] = "Archives",
        [".tar"] = "Archives",
        [".gz"] = "Archives",
        [".bz2"] = "Archives",
        [".xz"] = "Archives",
        [".iso"] = "Archives",

        // Code
        [".cs"] = "Code",
        [".py"] = "Code",
        [".js"] = "Code",
        [".ts"] = "Code",
        [".jsx"] = "Code",
        [".tsx"] = "Code",
        [".json"] = "Code",
        [".xml"] = "Code",
        [".yaml"] = "Code",
        [".yml"] = "Code",
        [".html"] = "Code",
        [".css"] = "Code",
        [".sql"] = "Code",
        [".ps1"] = "Code",
        [".sh"] = "Code"
    };

    public static string GetCategory(string extension, string fallbackCategory = "Others")
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return fallbackCategory;
        }

        var normalized = extension.StartsWith('.') ? extension : $".{extension}";
        return ExtensionToCategory.TryGetValue(normalized, out var category)
            ? category
            : fallbackCategory;
    }
}
