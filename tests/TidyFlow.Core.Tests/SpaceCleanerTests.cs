using TidyFlow.Core.Cleaning;

namespace TidyFlow.Core.Tests;

public sealed class SpaceCleanerTests
{
    [Fact]
    public void ScanAndDeleteEmptyFolders_RanksLargestFilesAndRemovesEmptyFoldersOnly()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("small.txt", "abc");
        ws.CreateFile(Path.Combine("Videos", "big.bin"), new string('X', 2048));

        var emptyLeaf = Path.Combine(ws.RootPath, "EmptyParent", "EmptyChild");
        Directory.CreateDirectory(emptyLeaf);

        var cleaner = new SpaceCleaner();
        var result = cleaner.Scan(ws.RootPath);

        Assert.Equal(2, result.TotalFileCount);
        Assert.Equal("big.bin", result.LargestFiles[0].Name);
        Assert.Equal("Videos", result.TopSubfolders[0].Name);
        Assert.Contains(result.EmptyFolders, e => e.FullPath == emptyLeaf);

        var deleted = SpaceCleaner.DeleteEmptyFolders(result.EmptyFolders.Select(e => e.FullPath));
        Assert.True(deleted >= 1);
        Assert.False(Directory.Exists(emptyLeaf));
        Assert.True(Directory.Exists(Path.Combine(ws.RootPath, "Videos")));
    }
}
