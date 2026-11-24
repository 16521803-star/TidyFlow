using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Tests;

public sealed class FolderScannerTests
{
    [Fact]
    public void Scan_IgnoresActiveDownloadsAndOfficeLockFilesByDefault()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("invoice.pdf");
        ws.CreateFile("photo.png");
        ws.CreateFile("movie.mp4.crdownload");
        ws.CreateFile("installer.exe.part");
        ws.CreateFile("draft.tmp");
        ws.CreateFile("~$report.docx");

        var scanner = new FolderScanner();
        var results = scanner.Scan(ws.RootPath);

        Assert.Equal(2, results.Count);
        Assert.Equal(new[] { "invoice.pdf", "photo.png" }, results.Select(r => r.Name));
    }

    [Fact]
    public void Scan_FiltersByExtensionAndSortsByNameDescending()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("alpha.pdf");
        ws.CreateFile("zeta.pdf");
        ws.CreateFile("notes.txt");

        var scanner = new FolderScanner();
        var results = scanner.Scan(ws.RootPath, new FolderScanOptions
        {
            ExtensionFilter = new[] { "pdf" },
            SortOrder = FileSortOrder.NameDesc
        });

        Assert.Equal(new[] { "zeta.pdf", "alpha.pdf" }, results.Select(r => r.Name));
    }
}
