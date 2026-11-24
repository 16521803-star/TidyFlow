using System.Text;
using TidyFlow.Core.Metadata;
using TidyFlow.Core.Organizing;
using TidyFlow.Core.Renaming;
using TidyFlow.Core.Scanning;

namespace TidyFlow.Core.Tests;

public sealed class ExifDateReaderTests
{
    [Fact]
    public void TryReadDateTaken_ParsesValidJpegExifHeaderAndSupportsTemplateAndDateOrganize()
    {
        using var ws = new TempTestWorkspace();
        var jpgPath = Path.Combine(ws.RootPath, "vacation.jpg");
        File.WriteAllBytes(jpgPath, BuildMinimalJpegWithExif("2024:08:19 14:32:10\0"));
        File.SetLastWriteTime(jpgPath, new DateTime(2026, 11, 24));

        var parsed = ExifDateReader.TryReadDateTaken(jpgPath);
        Assert.NotNull(parsed);
        Assert.Equal(2024, parsed!.Value.Year);
        Assert.Equal(8, parsed.Value.Month);
        Assert.Equal(19, parsed.Value.Day);

        var files = new FolderScanner().Scan(ws.RootPath);

        var renamePlan = new BatchRenamer().BuildTemplateRenamePlan(files, new TemplateRenameOptions
        {
            Pattern = "{exif_date}_{name}",
            DateFormat = "yyyy-MM-dd"
        });
        Assert.Equal("2024-08-19_vacation.jpg", renamePlan.Operations.Single().DestinationName);

        var organizePlan = new FolderOrganizer().BuildPlan(ws.RootPath, files, new OrganizeOptions
        {
            Mode = OrganizeMode.ByDate,
            DateFormat = DateGroupingFormat.YearMonth,
            UseExifDateWhenAvailable = true
        });
        Assert.Equal(Path.Combine(ws.RootPath, "2024-08", "vacation.jpg"), organizePlan.Operations.Single().DestinationPath);
    }

    [Fact]
    public void TryReadDateTaken_ReturnsNullForNonJpegOrCorruptFile()
    {
        using var ws = new TempTestWorkspace();
        var txtPath = ws.CreateFile("plain.jpg", "not-a-jpeg");
        Assert.Null(ExifDateReader.TryReadDateTaken(txtPath));
    }

    private static byte[] BuildMinimalJpegWithExif(string dateAsciiNullTerminated)
    {
        // Little-endian TIFF inside JPEG APP1:
        // Header (8): "II" + 42 + IFD0 offset (8)
        // IFD0 at 8 (2 + 12 + 4 = 18): 1 entry -> ExifIFDPointer (0x8769) at offset 26
        // ExifIFD at 26 (2 + 12 + 4 = 18): 1 entry -> DateTimeOriginal (0x9003) -> offset 44
        // Date string at 44 (20 bytes)
        var dateBytes = Encoding.ASCII.GetBytes(dateAsciiNullTerminated);
        var tiff = new List<byte>();
        // TIFF header
        tiff.AddRange(Encoding.ASCII.GetBytes("II"));
        tiff.AddRange(BitConverter.GetBytes((ushort)42));
        tiff.AddRange(BitConverter.GetBytes((uint)8));

        // IFD0 at 8
        tiff.AddRange(BitConverter.GetBytes((ushort)1)); // count
        tiff.AddRange(BitConverter.GetBytes((ushort)0x8769)); // ExifIFDPointer
        tiff.AddRange(BitConverter.GetBytes((ushort)4)); // LONG
        tiff.AddRange(BitConverter.GetBytes((uint)1)); // count
        tiff.AddRange(BitConverter.GetBytes((uint)26)); // ExifIFD offset
        tiff.AddRange(BitConverter.GetBytes((uint)0)); // next IFD

        // ExifIFD at 26
        tiff.AddRange(BitConverter.GetBytes((ushort)1)); // count
        tiff.AddRange(BitConverter.GetBytes((ushort)0x9003)); // DateTimeOriginal
        tiff.AddRange(BitConverter.GetBytes((ushort)2)); // ASCII
        tiff.AddRange(BitConverter.GetBytes((uint)dateBytes.Length));
        tiff.AddRange(BitConverter.GetBytes((uint)44)); // value offset
        tiff.AddRange(BitConverter.GetBytes((uint)0)); // next IFD

        // Value at 44
        tiff.AddRange(dateBytes);

        var app1PayloadLength = 2 + 6 + tiff.Count; // length field + "Exif\0\0" + tiff
        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1 };
        jpeg.Add((byte)((app1PayloadLength >> 8) & 0xFF));
        jpeg.Add((byte)(app1PayloadLength & 0xFF));
        jpeg.AddRange(Encoding.ASCII.GetBytes("Exif\0\0"));
        jpeg.AddRange(tiff);
        jpeg.AddRange(new byte[] { 0xFF, 0xD9 });
        return jpeg.ToArray();
    }
}
