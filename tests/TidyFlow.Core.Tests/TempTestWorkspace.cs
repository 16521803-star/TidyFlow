namespace TidyFlow.Core.Tests;

internal sealed class TempTestWorkspace : IDisposable
{
    public string RootPath { get; }

    public string HistoryPath { get; }

    public TempTestWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "TidyFlowTests", Guid.NewGuid().ToString("N"));
        HistoryPath = Path.Combine(RootPath, "_history");
        Directory.CreateDirectory(RootPath);
    }

    public string CreateFile(string relativePath, string content = "sample", DateTime? lastWriteTime = null)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content);
        if (lastWriteTime.HasValue)
        {
            File.SetLastWriteTime(fullPath, lastWriteTime.Value);
        }

        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup lock in test teardown.
        }
    }
}
