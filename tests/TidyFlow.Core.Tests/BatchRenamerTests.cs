using TidyFlow.Core.Models;
using TidyFlow.Core.Renaming;
using TidyFlow.Core.Safety;
using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Tests;

public sealed class BatchRenamerTests
{
    [Fact]
    public void BuildListRenamePlan_MapsOrderedListAndSanitizesInvalidCharacters()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("a.pdf");
        ws.CreateFile("b.png");
        ws.CreateFile("c.txt");

        var files = new FolderScanner().Scan(ws.RootPath);
        var targetNames = ListRenameOptions.ParseRawNameList("""
            "Contract: Q1/2026"

            CON
            """);

        var plan = new BatchRenamer().BuildListRenamePlan(files, new ListRenameOptions
        {
            TargetNames = targetNames,
            PreserveOriginalExtension = true,
            MismatchBehavior = ListMismatchBehavior.SkipExtraFiles
        });

        Assert.Equal(2, plan.ReadyCount);
        Assert.Equal(1, plan.SkippedCount);

        Assert.Equal("Contract_ Q1_2026.pdf", plan.Operations[0].DestinationName);
        Assert.Equal("_CON.png", plan.Operations[1].DestinationName);
        Assert.Equal(OperationStatus.Skipped, plan.Operations[2].Status);
    }

    [Fact]
    public void BuildListRenamePlan_ResolvesDuplicateNamesInListOrMarksError()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("1.txt");
        ws.CreateFile("2.txt");

        var files = new FolderScanner().Scan(ws.RootPath);
        var renamer = new BatchRenamer();

        var autoResolved = renamer.BuildListRenamePlan(files, new ListRenameOptions
        {
            TargetNames = new[] { "Summary", "Summary" },
            DuplicateBehavior = DuplicateNameBehavior.AutoNumberSuffix
        });

        Assert.Equal("Summary.txt", autoResolved.Operations[0].DestinationName);
        Assert.Equal("Summary_1.txt", autoResolved.Operations[1].DestinationName);
        Assert.Equal(OperationStatus.ConflictResolved, autoResolved.Operations[1].Status);

        var markedError = renamer.BuildListRenamePlan(files, new ListRenameOptions
        {
            TargetNames = new[] { "Summary", "Summary" },
            DuplicateBehavior = DuplicateNameBehavior.MarkError
        });

        Assert.True(markedError.HasErrors);
        Assert.Equal(OperationStatus.Error, markedError.Operations[1].Status);
    }

    [Fact]
    public void BuildTemplateRenamePlan_CleansDuplicateMarkersAndFormatsIndex()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("Copy of Invoice (2).pdf");
        ws.CreateFile("Receipt - Copy.pdf");

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new BatchRenamer().BuildTemplateRenamePlan(files, new TemplateRenameOptions
        {
            Pattern = "{index}_{name}",
            CleanDuplicateMarkers = true,
            StartIndex = 1,
            IndexPadding = 2
        });

        Assert.Equal("01_Invoice.pdf", plan.Operations[0].DestinationName);
        Assert.Equal("02_Receipt.pdf", plan.Operations[1].DestinationName);
    }

    [Fact]
    public void BuildListRenamePlan_WithOutputDirectory_PlansMoveAndRenameToTargetFolder()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("raw1.mp4");
        var outputDir = Path.Combine(ws.RootPath, "RenamedVideos");

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new BatchRenamer().BuildListRenamePlan(files, new ListRenameOptions
        {
            TargetNames = new[] { "Intro Clip" },
            OutputDirectory = outputDir
        });

        var op = Assert.Single(plan.Operations);
        Assert.Equal(OperationKind.MoveAndRename, op.Kind);
        Assert.Equal(Path.Combine(outputDir, "Intro Clip.mp4"), op.DestinationPath);
    }
}
