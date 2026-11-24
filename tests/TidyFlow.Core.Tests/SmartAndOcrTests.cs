using TidyFlow.Core.Models;
using TidyFlow.Core.Renaming;
using TidyFlow.Core.Scanning;
using TidyFlow.Core.Smart;

namespace TidyFlow.Core.Tests;

public sealed class SmartAndOcrTests
{
    [Fact]
    public void OcrNameSuggester_ExtractsCleanStemAndSkipsNoiseLines()
    {
        var rawOcr = """
            ---
            12
            Invoice #2026-04: Cloud Hosting / AWS
            Total: $49.00
            """;

        var stem = OcrNameSuggester.SuggestStem(rawOcr, "IMG_001");
        Assert.Equal("Invoice #2026-04_ Cloud Hosting _ AWS", stem);

        var fallback = OcrNameSuggester.SuggestStem("   \n ! ", "IMG_002");
        Assert.Equal("IMG_002", fallback);
    }

    [Fact]
    public void AiOrganizePlanner_ParsesFencedJsonAndMaterializesSafePlan()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("doc1.pdf");
        ws.CreateFile("doc2.pdf");

        var rawResponse = """
            ```json
            [
              { "index": 0, "folder": "Finance/Tax", "newName": "Tax_2026:Q1", "reason": "Tax document" },
              { "index": 1, "folder": "../../Illegal", "newName": null, "reason": "Attempted traversal" }
            ]
            ```
            """;

        var suggestions = AiOrganizePlanner.ParseSuggestions(rawResponse);
        Assert.Equal(2, suggestions.Count);

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new AiOrganizePlanner().MaterializePlan(ws.RootPath, files, suggestions, "AI Test");

        Assert.Equal(OperationStatus.Ready, plan.Operations[0].Status);
        Assert.Equal(OperationKind.MoveAndRename, plan.Operations[0].Kind);
        Assert.Equal(Path.Combine(ws.RootPath, "Finance", "Tax", "Tax_2026_Q1.pdf"), plan.Operations[0].DestinationPath);

        // Traversal segments ".." are stripped safely, keeping destination inside sourceRootDirectory.
        Assert.Equal(Path.Combine(ws.RootPath, "Illegal", "doc2.pdf"), plan.Operations[1].DestinationPath);
    }

    [Fact]
    public void AiOrganizePlanner_OfflineSmartPlan_MatchesFileContentSnippets()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("notes_a.txt", "Meeting agenda for Dincox launch and video edits.");
        ws.CreateFile("random.zip", "binary");

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new AiOrganizePlanner().BuildOfflineSmartPlan(
            ws.RootPath,
            files,
            "Organize files mentioning dincox into their own folder");

        Assert.Equal(
            Path.Combine(ws.RootPath, "Dincox", "notes_a.txt"),
            plan.Operations.Single(o => o.SourceName == "notes_a.txt").DestinationPath);
        Assert.Equal(
            Path.Combine(ws.RootPath, "Archives", "random.zip"),
            plan.Operations.Single(o => o.SourceName == "random.zip").DestinationPath);
    }
}
