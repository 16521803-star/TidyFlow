namespace TidyFlow.Core.Models;

/// <summary>
/// Represents a discovered file selected for organization or batch renaming.
/// </summary>
public sealed record FileEntry(
    string FullPath,
    string Name,
    string Extension,
    long SizeBytes,
    DateTimeOffset LastWriteTime,
    DateTimeOffset CreationTime)
{
    public string NameWithoutExtension => Path.GetFileNameWithoutExtension(Name);

    public string DirectoryPath => Path.GetDirectoryName(FullPath) ?? string.Empty;

    public static FileEntry FromFileInfo(FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new FileEntry(
            FullPath: info.FullName,
            Name: info.Name,
            Extension: info.Extension,
            SizeBytes: info.Exists ? info.Length : 0,
            LastWriteTime: new DateTimeOffset(info.LastWriteTime),
            CreationTime: new DateTimeOffset(info.CreationTime));
    }
}
