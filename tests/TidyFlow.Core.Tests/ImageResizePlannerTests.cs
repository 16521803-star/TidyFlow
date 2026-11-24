using TidyFlow.Core.Imaging;
using TidyFlow.Core.Models;
using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Tests;

public sealed class ImageResizePlannerTests
{
    [Fact]
    public void ComputeTargetDimensions_PreservesAspectRatioAndRespectsDoNotUpscale()
    {
        var (w1, h1) = ImageResizePlanner.ComputeTargetDimensions(4000, 2000, new ImageResizeOptions
        {
            Mode = ImageResizeMode.MaxWidth,
            TargetWidth = 1000
        });
        Assert.Equal((1000, 500), (w1, h1));

        var (w2, h2) = ImageResizePlanner.ComputeTargetDimensions(800, 600, new ImageResizeOptions
        {
            Mode = ImageResizeMode.MaxWidth,
            TargetWidth = 1920,
            DoNotUpscale = true
        });
        Assert.Equal((800, 600), (w2, h2));

        var (w3, h3) = ImageResizePlanner.ComputeTargetDimensions(2000, 1000, new ImageResizeOptions
        {
            Mode = ImageResizeMode.Percentage,
            Percentage = 25
        });
        Assert.Equal((500, 250), (w3, h3));

        var (w4, h4) = ImageResizePlanner.ComputeTargetDimensions(1080, 1080, new ImageResizeOptions
        {
            Mode = ImageResizeMode.CropToExact,
            TargetWidth = 1080,
            TargetHeight = 1920
        });
        Assert.Equal((1080, 1920), (w4, h4));

        var crop = ImageResizePlanner.ComputeCenterCropRect(1080, 1080, 1080, 1920);
        Assert.Equal((236, 0, 608, 1080), crop);
    }

    [Fact]
    public void BuildPlan_ConvertsFormatAndSkipsNonImages()
    {
        using var ws = new TempTestWorkspace();
        ws.CreateFile("photo.png");
        ws.CreateFile("notes.txt");
        var outDir = Path.Combine(ws.RootPath, "Converted");

        var files = new FolderScanner().Scan(ws.RootPath);
        var plan = new ImageResizePlanner().BuildPlan(files, new ImageResizeOptions
        {
            Mode = ImageResizeMode.MaxWidth,
            TargetWidth = 1280,
            OutputFormat = ImageOutputFormat.Jpg,
            Suffix = "_web",
            OutputDirectory = outDir
        });

        Assert.Equal(2, plan.Count);

        var imgOp = plan.Single(p => Path.GetFileName(p.SourcePath) == "photo.png");
        Assert.Equal(OperationStatus.Ready, imgOp.Status);
        Assert.Equal(ImageOutputFormat.Jpg, imgOp.TargetFormat);
        Assert.Equal(Path.Combine(outDir, "photo_web.jpg"), imgOp.DestinationPath);

        var txtOp = plan.Single(p => Path.GetFileName(p.SourcePath) == "notes.txt");
        Assert.Equal(OperationStatus.Skipped, txtOp.Status);
    }
}
