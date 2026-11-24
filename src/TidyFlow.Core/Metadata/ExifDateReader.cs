using System.Globalization;
using System.Text;

namespace TidyFlow.Core.Metadata;

/// <summary>
/// Lightweight, dependency-free JPEG EXIF DateTaken reader (tags 0x9003 DateTimeOriginal, 0x9004 DateTimeDigitized, 0x0132 DateTime).
/// </summary>
public static class ExifDateReader
{
    private const int MaxHeaderBytesToRead = 131_072; // 128 KB is plenty for JPEG APP1 EXIF

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg"
    };

    public static DateTimeOffset? TryReadDateTaken(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        var ext = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(ext))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var len = (int)Math.Min(stream.Length, MaxHeaderBytesToRead);
            if (len < 12)
            {
                return null;
            }

            var buffer = new byte[len];
            var bytesRead = ReadFully(stream, buffer, len);
            if (bytesRead < 12)
            {
                return null;
            }

            // JPEG SOI marker: FF D8
            if (buffer[0] != 0xFF || buffer[1] != 0xD8)
            {
                return null;
            }

            var pos = 2;
            while (pos + 4 <= bytesRead)
            {
                if (buffer[pos] != 0xFF)
                {
                    break;
                }

                var marker = buffer[pos + 1];
                pos += 2;

                // Start of Scan (FF DA) or EOI (FF D9) -> stop searching headers
                if (marker == 0xDA || marker == 0xD9)
                {
                    break;
                }

                // Standalone markers without length
                if (marker is 0x00 or 0x01 or >= 0xD0 and <= 0xD7)
                {
                    continue;
                }

                if (pos + 2 > bytesRead)
                {
                    break;
                }

                var segmentLength = (buffer[pos] << 8) | buffer[pos + 1];
                if (segmentLength < 2 || pos + segmentLength > bytesRead)
                {
                    break;
                }

                // APP1 (FF E1) containing "Exif\0\0"
                if (marker == 0xE1 && segmentLength >= 8)
                {
                    var dataStart = pos + 2;
                    if (buffer[dataStart] == 'E' &&
                        buffer[dataStart + 1] == 'x' &&
                        buffer[dataStart + 2] == 'i' &&
                        buffer[dataStart + 3] == 'f' &&
                        buffer[dataStart + 4] == 0x00 &&
                        buffer[dataStart + 5] == 0x00)
                    {
                        var tiffStart = dataStart + 6;
                        var tiffLength = (pos + segmentLength) - tiffStart;
                        return ParseTiffForDate(buffer.AsSpan(tiffStart, tiffLength));
                    }
                }

                pos += segmentLength;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static DateTimeOffset? ParseTiffForDate(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return null;
        }

        bool isLittleEndian;
        if (tiff[0] == 'I' && tiff[1] == 'I')
        {
            isLittleEndian = true;
        }
        else if (tiff[0] == 'M' && tiff[1] == 'M')
        {
            isLittleEndian = false;
        }
        else
        {
            return null;
        }

        var magic = ReadUInt16(tiff, 2, isLittleEndian);
        if (magic != 42)
        {
            return null;
        }

        var ifd0Offset = (int)ReadUInt32(tiff, 4, isLittleEndian);
        if (ifd0Offset < 8 || ifd0Offset + 2 > tiff.Length)
        {
            return null;
        }

        // First check ExifIFD (pointer tag 0x8769) for DateTimeOriginal (0x9003) / DateTimeDigitized (0x9004)
        var exifIfdOffset = FindPointerTag(tiff, ifd0Offset, 0x8769, isLittleEndian);
        if (exifIfdOffset.HasValue)
        {
            var dateOriginal = ReadAsciiDateTag(tiff, exifIfdOffset.Value, 0x9003, isLittleEndian)
                               ?? ReadAsciiDateTag(tiff, exifIfdOffset.Value, 0x9004, isLittleEndian);
            if (dateOriginal.HasValue)
            {
                return dateOriginal;
            }
        }

        // Fallback to IFD0 DateTime (0x0132)
        return ReadAsciiDateTag(tiff, ifd0Offset, 0x0132, isLittleEndian);
    }

    private static int? FindPointerTag(ReadOnlySpan<byte> tiff, int ifdOffset, ushort targetTag, bool littleEndian)
    {
        if (ifdOffset + 2 > tiff.Length)
        {
            return null;
        }

        var entryCount = ReadUInt16(tiff, ifdOffset, littleEndian);
        for (var i = 0; i < entryCount; i++)
        {
            var entryOffset = ifdOffset + 2 + (i * 12);
            if (entryOffset + 12 > tiff.Length)
            {
                break;
            }

            var tag = ReadUInt16(tiff, entryOffset, littleEndian);
            if (tag == targetTag)
            {
                var ptr = (int)ReadUInt32(tiff, entryOffset + 8, littleEndian);
                if (ptr > 0 && ptr < tiff.Length)
                {
                    return ptr;
                }
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadAsciiDateTag(ReadOnlySpan<byte> tiff, int ifdOffset, ushort targetTag, bool littleEndian)
    {
        if (ifdOffset + 2 > tiff.Length)
        {
            return null;
        }

        var entryCount = ReadUInt16(tiff, ifdOffset, littleEndian);
        for (var i = 0; i < entryCount; i++)
        {
            var entryOffset = ifdOffset + 2 + (i * 12);
            if (entryOffset + 12 > tiff.Length)
            {
                break;
            }

            var tag = ReadUInt16(tiff, entryOffset, littleEndian);
            if (tag != targetTag)
            {
                continue;
            }

            var type = ReadUInt16(tiff, entryOffset + 2, littleEndian);
            var count = (int)ReadUInt32(tiff, entryOffset + 4, littleEndian);
            if (type != 2 || count < 10 || count > 64)
            {
                return null;
            }

            var valueOffset = count <= 4
                ? entryOffset + 8
                : (int)ReadUInt32(tiff, entryOffset + 8, littleEndian);

            if (valueOffset < 0 || valueOffset + count > tiff.Length)
            {
                return null;
            }

            var raw = Encoding.ASCII.GetString(tiff.Slice(valueOffset, count)).TrimEnd('\0', ' ');
            if (DateTime.TryParseExact(
                    raw,
                    new[] { "yyyy:MM:dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy:MM:dd" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsed))
            {
                return new DateTimeOffset(parsed);
            }
        }

        return null;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian)
        => littleEndian
            ? (ushort)(data[offset] | (data[offset + 1] << 8))
            : (ushort)((data[offset] << 8) | data[offset + 1]);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian)
        => littleEndian
            ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24))
            : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    private static int ReadFully(Stream stream, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer, total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
