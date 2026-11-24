using System.IO;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace TidyFlow.App.Services;

/// <summary>
/// Extracts text from local images using the built-in Windows 10/11 WinRT OCR engine (no external model required).
/// </summary>
public static class WindowsOcrService
{
    public static async Task<string> ExtractTextAsync(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return string.Empty;
        }

        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"));
        if (engine is null)
        {
            return string.Empty;
        }

        using var fileStream = File.OpenRead(imagePath);
        using var randomAccessStream = fileStream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var result = await engine.RecognizeAsync(bitmap);
        if (result?.Lines is null || result.Lines.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, result.Lines.Select(l => l.Text));
    }
}
