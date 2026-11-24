using TidyFlow.Core.Duplicates;

namespace TidyFlow.Core.Tests;

public sealed class DuplicateFinderTests
{
    [Fact]
    public void Scan_FindsIdenticalContentAcrossSubfoldersAndIgnoresDistinctSameSizeFiles()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("alpha.txt", "identical-payload-12345");
        ws.CreateFile(Path.Combine("Sub", "beta_copy.txt"), "identical-payload-12345");
        ws.CreateFile("different.txt", "different-payload-12345");
        ws.CreateFile("empty1.txt", string.Empty);
        ws.CreateFile("empty2.txt", string.Empty);

        var result = new DuplicateFinder().Scan(ws.RootPath, includeSubdirectories: true);

        Assert.Equal(5, result.TotalFilesScanned);
        var group = Assert.Single(result.Groups);
        Assert.Equal(2, group.Files.Count);
        Assert.Equal(23, group.TotalBytesCheck());
    }
}

internal static class DuplicateGroupTestExtensions
{
    public static long TotalBytesCheck(this DuplicateGroup group) => group.WastedBytes;
}
