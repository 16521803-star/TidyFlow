using System.Text.Json;

namespace TidyFlow.Core.Smart;

public sealed class AiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public AiSettingsStore(string? settingsFilePath = null)
    {
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TidyFlow",
                "ai_settings.json")
            : settingsFilePath;
    }

    public AiProviderConfig Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AiProviderConfig();
        }

        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize<AiProviderConfig>(json, JsonOptions) ?? new AiProviderConfig();
    }

    public void Save(AiProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var dir = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(config, JsonOptions));
    }
}
