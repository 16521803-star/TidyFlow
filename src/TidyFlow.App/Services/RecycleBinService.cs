using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace TidyFlow.App.Services;

public static class RecycleBinService
{
    public static (int DeletedCount, List<string> Errors) MoveFilesToRecycleBin(IEnumerable<string> filePaths)
    {
        var deleted = 0;
        var errors = new List<string>();

        foreach (var path in filePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                FileSystem.DeleteFile(
                    path,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
                deleted++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        return (deleted, errors);
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{bytes} B" : $"{value:0.##} {units[unitIndex]}";
    }
}
