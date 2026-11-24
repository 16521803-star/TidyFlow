using TidyFlow.Core.Organizing;
using TidyFlow.Core.Presets;
using TidyFlow.Core.Renaming;
using TidyFlow.Core.Safety;
using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Tests;

public sealed class BatchExecutorTests
{
    [Fact]
    public void ExecuteAndUndoLatest_OrganizesFilesThenRestoresAndCleansEmptyFolders()
    {
        using var ws = new TempTestWorkspace();
        var pdfPath = ws.CreateFile("doc.pdf", "pdf-bytes");
        var imgPath = ws.CreateFile("pic.jpg", "jpg-bytes");

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new FolderOrganizer().BuildPlan(ws.RootPath, files);
        var executor = new BatchExecutor(ws.HistoryPath);

        var execResult = executor.Execute(plan);

        Assert.Equal(2, execResult.AppliedCount);
        Assert.Equal(0, execResult.FailedCount);
        Assert.NotNull(execResult.UndoLogPath);
        Assert.True(File.Exists(Path.Combine(ws.RootPath, "Documents", "doc.pdf")));
        Assert.True(File.Exists(Path.Combine(ws.RootPath, "Images", "pic.jpg")));

        var undoResult = executor.UndoLatest();

        Assert.Equal(2, undoResult.RestoredCount);
        Assert.Equal(0, undoResult.FailedCount);
        Assert.True(File.Exists(pdfPath));
        Assert.True(File.Exists(imgPath));
        Assert.False(Directory.Exists(Path.Combine(ws.RootPath, "Documents")));
        Assert.False(Directory.Exists(Path.Combine(ws.RootPath, "Images")));
    }

    [Fact]
    public void Execute_HandlesCyclicSwapAndCaseOnlyRenameSafely()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("a.txt", "content-A");
        ws.CreateFile("b.txt", "content-B");
        ws.CreateFile("report.pdf", "content-R");

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new BatchRenamer().BuildListRenamePlan(files, new ListRenameOptions
        {
            TargetNames = new[] { "b", "a", "Report" }
        });

        var executor = new BatchExecutor(ws.HistoryPath);
        var execResult = executor.Execute(plan);

        Assert.Equal(3, execResult.AppliedCount);
        Assert.Equal(0, execResult.FailedCount);
        Assert.Equal("content-A", File.ReadAllText(Path.Combine(ws.RootPath, "b.txt")));
        Assert.Equal("content-B", File.ReadAllText(Path.Combine(ws.RootPath, "a.txt")));
        Assert.Equal("content-R", File.ReadAllText(Path.Combine(ws.RootPath, "Report.pdf")));

        var undoResult = executor.UndoLatest();

        Assert.Equal(3, undoResult.RestoredCount);
        Assert.Equal("content-A", File.ReadAllText(Path.Combine(ws.RootPath, "a.txt")));
        Assert.Equal("content-B", File.ReadAllText(Path.Combine(ws.RootPath, "b.txt")));
    }

    [Fact]
    public void PresetStore_SavesAndLoadsCustomRulesAndNameLists()
    {
        using var ws = new TempTestWorkspace();
        var presetPath = Path.Combine(ws.RootPath, "presets.json");
        var store = new PresetStore(presetPath);

        var data = new TidyFlowPresetsData
        {
            CustomOrganizeRules = new[]
            {
                new KeywordOrganizeRule("Invoices", new[] { "invoice", "bill" }, new[] { ".pdf" })
            },
            SavedNameLists = new[]
            {
                new SavedNameList("Monthly Reports", new[] { "Jan", "Feb", "Mar" })
            }
        };

        store.Save(data);
        var loaded = store.Load();

        Assert.Single(loaded.CustomOrganizeRules);
        Assert.Equal("Invoices", loaded.CustomOrganizeRules[0].TargetFolderName);
        Assert.Single(loaded.SavedNameLists);
        Assert.Equal(new[] { "Jan", "Feb", "Mar" }, loaded.SavedNameLists[0].Items);
    }
}
