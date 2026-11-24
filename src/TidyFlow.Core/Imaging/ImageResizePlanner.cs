using TidyFlow.Core.Models;
using TidyFlow.Core.Safety;

namespace TidyFlow.Core.Imaging;

public enum ImageResizeMode
{
    MaxWidth,
    MaxHeight,
    FitWithinBox,
    Percentage,
    CropToExact,
    ExactStretch
}

public enum ImageOutputFormat
{
    KeepOriginal,
    Jpg,
    Png,
    Bmp,
    Tiff
}

public sealed record ImageResizeOptions
{
    public ImageResizeMode Mode { get; init; } = ImageResizeMode.MaxWidth;

    public int TargetWidth { get; init; } = 1920;

    public int TargetHeight { get; init; } = 1080;

    public int Percentage { get; init; } = 50;

    public bool DoNotUpscale { get; init; } = true;

    public ImageOutputFormat OutputFormat { get; init; } = ImageOutputFormat.KeepOriginal;

    public int JpegQuality { get; init; } = 90;

    public string Suffix { get; init; } = "_resized";

    public string? OutputDirectory { get; init; }
}

public sealed record PlannedImageOperation(
    string SourcePath,
    string DestinationPath,
    ImageOutputFormat TargetFormat,
    OperationStatus Status,
    string? Note = null);

/// <summary>
/// Plans safe, conflict-free batch image resize and format conversion operations.
/// </summary>
public sealed class ImageResizePlanner
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"
    };

    private readonly ConflictResolver _conflictResolver = new();

    public static bool IsSupportedImage(string filePath)
        => SupportedImageExtensions.Contains(Path.GetExtension(filePath));

    public static (int Width, int Height) ComputeTargetDimensions(
        int sourceWidth,
        int sourceHeight,
        ImageResizeOptions options)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return (sourceWidth, sourceHeight);
        }

        if (options.Mode is ImageResizeMode.CropToExact or ImageResizeMode.ExactStretch)
        {
            var exactW = options.TargetWidth > 0 ? options.TargetWidth : sourceWidth;
            var exactH = options.TargetHeight > 0 ? options.TargetHeight : sourceHeight;
            return (Math.Max(1, exactW), Math.Max(1, exactH));
        }

        double scale = options.Mode switch
        {
            ImageResizeMode.MaxWidth => options.TargetWidth > 0 ? (double)options.TargetWidth / sourceWidth : 1.0,
            ImageResizeMode.MaxHeight => options.TargetHeight > 0 ? (double)options.TargetHeight / sourceHeight : 1.0,
            ImageResizeMode.FitWithinBox => Math.Min(
                options.TargetWidth > 0 ? (double)options.TargetWidth / sourceWidth : 1.0,
                options.TargetHeight > 0 ? (double)options.TargetHeight / sourceHeight : 1.0),
            ImageResizeMode.Percentage => Math.Max(1, options.Percentage) / 100.0,
            _ => 1.0
        };

        if (options.DoNotUpscale && options.Mode != ImageResizeMode.Percentage && scale > 1.0)
        {
            scale = 1.0;
        }

        var w = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var h = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return (w, h);
    }

    public static (int X, int Y, int Width, int Height) ComputeCenterCropRect(
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            return (0, 0, sourceWidth, sourceHeight);
        }

        var sourceRatio = (double)sourceWidth / sourceHeight;
        var targetRatio = (double)targetWidth / targetHeight;

        if (sourceRatio > targetRatio)
        {
            var cropW = Math.Clamp((int)Math.Round(sourceHeight * targetRatio), 1, sourceWidth);
            var x = (sourceWidth - cropW) / 2;
            return (x, 0, cropW, sourceHeight);
        }
        else
        {
            var cropH = Math.Clamp((int)Math.Round(sourceWidth / targetRatio), 1, sourceHeight);
            var y = (sourceHeight - cropH) / 2;
            return (0, y, sourceWidth, cropH);
        }
    }

    public IReadOnlyList<PlannedImageOperation> BuildPlan(
        IReadOnlyList<FileEntry> files,
        ImageResizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(options);

        var proposals = new List<ProposedOperation>(files.Count);
        var formats = new List<ImageOutputFormat>(files.Count);

        foreach (var file in files)
        {
            if (!IsSupportedImage(file.FullPath))
            {
                proposals.Add(new ProposedOperation(
                    file.FullPath,
                    file.FullPath,
                    OperationKind.MoveAndRename,
                    InitialNote: "Unsupported image format.",
                    ForceSkip: true));
                formats.Add(ImageOutputFormat.KeepOriginal);
                continue;
            }

            var targetDir = string.IsNullOrWhiteSpace(options.OutputDirectory)
                ? file.DirectoryPath
                : options.OutputDirectory.Trim();

            var targetFormat = ResolveTargetFormat(file.Extension, options.OutputFormat);
            var targetExt = FormatToExtension(targetFormat, file.Extension);
            var stem = $"{file.NameWithoutExtension}{options.Suffix}";
            var destPath = Path.Combine(targetDir, $"{stem}{targetExt}");

            proposals.Add(new ProposedOperation(
                file.FullPath,
                destPath,
                OperationKind.MoveAndRename,
                InitialNote: DescribeResizeNote(options, targetFormat)));
            formats.Add(targetFormat);
        }

        var plan = _conflictResolver.Resolve(proposals, $"Image resize/convert ({files.Count} file(s))");
        var results = new List<PlannedImageOperation>(plan.Operations.Count);

        for (var i = 0; i < plan.Operations.Count; i++)
        {
            var op = plan.Operations[i];
            results.Add(new PlannedImageOperation(
                op.SourcePath,
                op.DestinationPath,
                formats[i],
                op.Status,
                op.Note));
        }

        return results;
    }

    private static ImageOutputFormat ResolveTargetFormat(string sourceExtension, ImageOutputFormat requested)
    {
        if (requested != ImageOutputFormat.KeepOriginal)
        {
            return requested;
        }

        return sourceExtension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageOutputFormat.Jpg,
            ".png" => ImageOutputFormat.Png,
            ".bmp" => ImageOutputFormat.Bmp,
            ".tif" or ".tiff" => ImageOutputFormat.Tiff,
            _ => ImageOutputFormat.Png
        };
    }

    private static string FormatToExtension(ImageOutputFormat format, string fallbackExtension)
        => format switch
        {
            ImageOutputFormat.Jpg => ".jpg",
            ImageOutputFormat.Png => ".png",
            ImageOutputFormat.Bmp => ".bmp",
            ImageOutputFormat.Tiff => ".tiff",
            _ => fallbackExtension
        };

    private static string DescribeResizeNote(ImageResizeOptions options, ImageOutputFormat format)
    {
        var sizePart = options.Mode switch
        {
            ImageResizeMode.MaxWidth => $"MaxW {options.TargetWidth}px",
            ImageResizeMode.MaxHeight => $"MaxH {options.TargetHeight}px",
            ImageResizeMode.FitWithinBox => $"Fit {options.TargetWidth}x{options.TargetHeight}",
            ImageResizeMode.Percentage => $"{options.Percentage}%",
            ImageResizeMode.CropToExact => $"Crop {options.TargetWidth}x{options.TargetHeight}",
            ImageResizeMode.ExactStretch => $"Stretch {options.TargetWidth}x{options.TargetHeight}",
            _ => "Original"
        };

        return $"{sizePart} -> {format.ToString().ToUpperInvariant()}";
    }
}
