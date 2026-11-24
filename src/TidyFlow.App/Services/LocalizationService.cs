using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace TidyFlow.App.Services;

public sealed record LanguageOption(string Code, string DisplayName);

public sealed record LocalizedOption<T>(T Value, string Display);

public sealed class LocalizationService
{
    private const string DictionaryPrefix = "Resources/Strings.";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static LocalizationService Instance { get; } = new();

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = new[]
    {
        new LanguageOption("en", "English"),
        new LanguageOption("vi", "Tiếng Việt"),
        new LanguageOption("ja", "日本語")
    };

    private readonly string _settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TidyFlow",
        "app_settings.json");

    private ResourceDictionary? _activeDictionary;

    public string CurrentLanguageCode { get; private set; } = "en";

    public event EventHandler? LanguageChanged;

    public void Initialize()
    {
        var saved = LoadSavedLanguageCode();
        var initial = saved ?? DetectDefaultLanguageCode();
        SetLanguage(initial, persist: saved is null);
    }

    public void SetLanguage(string languageCode, bool persist = true)
    {
        var normalized = AvailableLanguages.Any(l => string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase))
            ? languageCode.ToLowerInvariant()
            : "en";

        var dictUri = new Uri($"{DictionaryPrefix}{normalized}.xaml", UriKind.Relative);
        var newDict = new ResourceDictionary { Source = dictUri };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_activeDictionary is not null)
        {
            merged.Remove(_activeDictionary);
        }
        else
        {
            var existing = merged.FirstOrDefault(d =>
                d.Source is not null &&
                d.Source.OriginalString.Contains(DictionaryPrefix, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                merged.Remove(existing);
            }
        }

        merged.Add(newDict);
        _activeDictionary = newDict;
        CurrentLanguageCode = normalized;

        if (persist)
        {
            SaveLanguageCode(normalized);
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key, string? fallback = null)
    {
        if (_activeDictionary is not null && _activeDictionary[key] is string value)
        {
            return value;
        }

        if (Application.Current?.TryFindResource(key) is string appValue)
        {
            return appValue;
        }

        return fallback ?? key;
    }

    public string Format(string key, params object[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch
        {
            return template;
        }
    }

    private static string DetectDefaultLanguageCode()
    {
        var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return twoLetter is "vi" or "ja" ? twoLetter : "en";
    }

    private string? LoadSavedLanguageCode()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettingsDto>(json, JsonOptions);
            return settings?.LanguageCode;
        }
        catch
        {
            return null;
        }
    }

    private void SaveLanguageCode(string code)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(new AppSettingsDto { LanguageCode = code }, JsonOptions));
        }
        catch
        {
            // Ignore settings write failures.
        }
    }

    private sealed class AppSettingsDto
    {
        public string? LanguageCode { get; set; }
    }
}
