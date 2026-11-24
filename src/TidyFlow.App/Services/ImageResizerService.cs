using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TidyFlow.Core.Imaging;

namespace TidyFlow.App.Services;

public sealed record ImageResizeExecutionResult(
    int ProcessedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<string> Errors);

public static class ImageResizerService
{
    public static ImageResizeExecutionResult Execute(
        IReadOnlyList<PlannedImageOperation> operations,
        ImageResizeOptions options)
    {
        var processed = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var op in operations)
        {
            if (op.Status is not (Core.Models.OperationStatus.Ready or Core.Models.OperationStatus.ConflictResolved))
            {
                skipped++;
                continue;
            }

            try
            {
                ResizeSingleImage(op.SourcePath, op.DestinationPath, op.TargetFormat, options);
                processed++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(op.SourcePath)}: {ex.Message}");
            }
        }

        return new ImageResizeExecutionResult(processed, skipped, errors.Count, errors);
    }

    private static void ResizeSingleImage(
        string sourcePath,
        string destinationPath,
        ImageOutputFormat format,
        ImageResizeOptions options)
    {
        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        BitmapFrame sourceFrame;
        using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            sourceFrame = decoder.Frames[0];
        }

        var (targetW, targetH) = ImageResizePlanner.ComputeTargetDimensions(
            sourceFrame.PixelWidth,
            sourceFrame.PixelHeight,
            options);

        BitmapSource workingBitmap = sourceFrame;

        if (options.Mode == ImageResizeMode.CropToExact)
        {
            var (cx, cy, cw, ch) = ImageResizePlanner.ComputeCenterCropRect(
                sourceFrame.PixelWidth,
                sourceFrame.PixelHeight,
                targetW,
                targetH);

            if (cw != sourceFrame.PixelWidth || ch != sourceFrame.PixelHeight)
            {
                var cropped = new CroppedBitmap(sourceFrame, new Int32Rect(cx, cy, cw, ch));
                cropped.Freeze();
                workingBitmap = cropped;
            }
        }

        BitmapSource outputBitmap = workingBitmap;
        if (targetW != workingBitmap.PixelWidth || targetH != workingBitmap.PixelHeight)
        {
            var scaleX = (double)targetW / workingBitmap.PixelWidth;
            var scaleY = (double)targetH / workingBitmap.PixelHeight;
            var transformed = new TransformedBitmap(workingBitmap, new ScaleTransform(scaleX, scaleY));
            transformed.Freeze();
            outputBitmap = transformed;
        }

        BitmapEncoder encoder = format switch
        {
            ImageOutputFormat.Jpg => new JpegBitmapEncoder { QualityLevel = Math.Clamp(options.JpegQuality, 10, 100) },
            ImageOutputFormat.Png => new PngBitmapEncoder(),
            ImageOutputFormat.Bmp => new BmpBitmapEncoder(),
            ImageOutputFormat.Tiff => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };

        encoder.Frames.Add(BitmapFrame.Create(outputBitmap));
        using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(output);
    }
}
