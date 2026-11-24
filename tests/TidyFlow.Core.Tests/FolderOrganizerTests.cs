using TidyFlow.Core.Models;
using TidyFlow.Core.Organizing;
using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Tests;

public sealed class FolderOrganizerTests
{
    [Fact]
    public void BuildPlan_GroupsFilesByCategoryAndResolvesExistingCollision()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("report.pdf");
        ws.CreateFile("avatar.png");
        ws.CreateFile("setup.exe");
        ws.CreateFile("bundle.zip");
        ws.CreateFile("unknown.xyz");
        ws.CreateFile(Path.Combine("Documents", "report.pdf"), "existing");

        var scanner = new FolderScanner();
        var files = scanner.Scan(ws.RootPath);
        var organizer = new FolderOrganizer();

        var plan = organizer.BuildPlan(ws.RootPath, files, new OrganizeOptions
        {
            Mode = OrganizeMode.ByCategory
        });

        Assert.Equal(5, plan.ReadyCount);
        Assert.Equal(1, plan.ConflictResolvedCount);

        var pdfOp = plan.Operations.Single(o => o.SourceName == "report.pdf");
        Assert.Equal(OperationStatus.ConflictResolved, pdfOp.Status);
        Assert.Equal(Path.Combine(ws.RootPath, "Documents", "report_1.pdf"), pdfOp.DestinationPath);

        Assert.Equal(
            Path.Combine(ws.RootPath, "Images", "avatar.png"),
            plan.Operations.Single(o => o.SourceName == "avatar.png").DestinationPath);
        Assert.Equal(
            Path.Combine(ws.RootPath, "Installers", "setup.exe"),
            plan.Operations.Single(o => o.SourceName == "setup.exe").DestinationPath);
        Assert.Equal(
            Path.Combine(ws.RootPath, "Archives", "bundle.zip"),
            plan.Operations.Single(o => o.SourceName == "bundle.zip").DestinationPath);
        Assert.Equal(
            Path.Combine(ws.RootPath, "Others", "unknown.xyz"),
            plan.Operations.Single(o => o.SourceName == "unknown.xyz").DestinationPath);
    }

    [Fact]
    public void BuildPlan_CustomRulesTakePriorityOverCategory()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("March_Invoice_2026.pdf");
        ws.CreateFile("readme.pdf");

        var files = new FolderScanner().Scan(ws.RootPath);
        var organizer = new FolderOrganizer();

        var plan = organizer.BuildPlan(ws.RootPath, files, new OrganizeOptions
        {
            Mode = OrganizeMode.ByCustomRulesFirstThenCategory,
            CustomRules = new[]
            {
                new KeywordOrganizeRule("Finance/Invoices", new[] { "invoice" }, Array.Empty<string>())
            }
        });

        Assert.Equal(
            Path.Combine(ws.RootPath, "Finance", "Invoices", "March_Invoice_2026.pdf"),
            plan.Operations.Single(o => o.SourceName == "March_Invoice_2026.pdf").DestinationPath);
        Assert.Equal(
            Path.Combine(ws.RootPath, "Documents", "readme.pdf"),
            plan.Operations.Single(o => o.SourceName == "readme.pdf").DestinationPath);
    }

    [Fact]
    public void BuildPlan_ByDate_UsesYearMonthGrouping()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("note.txt", lastWriteTime: new DateTime(2026, 3, 15, 10, 0, 0));

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new FolderOrganizer().BuildPlan(ws.RootPath, files, new OrganizeOptions
        {
            Mode = OrganizeMode.ByDate,
            DateFormat = DateGroupingFormat.YearMonth
        });

        Assert.Equal(
            Path.Combine(ws.RootPath, "2026-03", "note.txt"),
            plan.Operations.Single().DestinationPath);
    }

    [Fact]
    public void BuildPlan_MatchAllLogicAndMinimumAge_OnlyMovesMatchingOlderFiles()
    {
        using var ws = new TempTestWorkspace();
        var oldMatch = ws.CreateFile("dincox_clip.mp4", lastWriteTime: DateTime.Now.AddDays(-45));
        ws.CreateFile("dincox_script.json", lastWriteTime: DateTime.Now.AddDays(-45));
        ws.CreateFile("other_clip.mp4", lastWriteTime: DateTime.Now.AddDays(-45));
        ws.CreateFile("dincox_fresh.mp4", lastWriteTime: DateTime.Now.AddDays(-2));

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new FolderOrganizer().BuildPlan(ws.RootPath, files, new OrganizeOptions
        {
            Mode = OrganizeMode.ByCustomRulesOnly,
            CustomRules = new[]
            {
                new KeywordOrganizeRule(
                    "DincoxVideo",
                    new[] { "dincox" },
                    new[] { ".mp4", ".jpg" },
                    RuleMatchLogic.MatchAll,
                    MinimumAgeDays: 30)
            }
        });

        Assert.Equal(1, plan.ReadyCount);
        Assert.Equal(3, plan.SkippedCount);

        var moved = plan.Operations.Single(o => o.WillModify);
        Assert.Equal(oldMatch, moved.SourcePath);
        Assert.Equal(Path.Combine(ws.RootPath, "DincoxVideo", "dincox_clip.mp4"), moved.DestinationPath);
    }
}
