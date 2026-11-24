using System.Text.Json;
using TidyFlow.Core.Organizing;

namespace TidyFlow.Core.Presets;

public sealed record SavedNameList(
    string Name,
    IReadOnlyList<string> Items);

public sealed record TidyFlowPresetsData
{
    public IReadOnlyList<KeywordOrganizeRule> CustomOrganizeRules { get; init; } = Array.Empty<KeywordOrganizeRule>();

    public IReadOnlyList<SavedNameList> SavedNameLists { get; init; } = Array.Empty<SavedNameList>();
}

/// <summary>
/// Persists user-defined folder rules and reusable rename lists to JSON.
/// </summary>
public sealed class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _presetsFilePath;

    public PresetStore(string? presetsFilePath = null)
    {
        _presetsFilePath = string.IsNullOrWhiteSpace(presetsFilePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TidyFlow",
                "presets.json")
            : presetsFilePath;
    }

    public TidyFlowPresetsData Load()
    {
        if (!File.Exists(_presetsFilePath))
        {
            return new TidyFlowPresetsData();
        }

        var json = File.ReadAllText(_presetsFilePath);
        return JsonSerializer.Deserialize<TidyFlowPresetsData>(json, JsonOptions) ?? new TidyFlowPresetsData();
    }

    public void Save(TidyFlowPresetsData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var dir = Path.GetDirectoryName(_presetsFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_presetsFilePath, JsonSerializer.Serialize(data, JsonOptions));
    }
}
