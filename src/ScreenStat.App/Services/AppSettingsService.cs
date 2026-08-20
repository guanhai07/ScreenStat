using System.IO;
using System.Text.Json;

namespace ScreenStat.App.Services;

/// <summary>
/// The little bit of state that has to outlive a run: right now only whether
/// capture collection is on. Kept in the user profile rather than next to the
/// exe so a portable copy stays writable.
/// </summary>
public sealed class AppSettingsService
{
    private const string ForceEnableVariableName = "SCREENSTAT_DATASET";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly bool _forcedOn;
    private AppSettings _settings;

    public AppSettingsService()
        : this(DefaultPath())
    {
    }

    /// <summary>Overload for tests, so they never touch the real user profile.</summary>
    public AppSettingsService(string settingsPath)
    {
        _path = settingsPath;
        _forcedOn = string.Equals(
            Environment.GetEnvironmentVariable(ForceEnableVariableName)?.Trim(),
            "1",
            StringComparison.Ordinal);
        _settings = Load(_path);
    }

    private static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenStat",
        "settings.json");

    /// <summary>True when the environment pins collection on and the UI must not turn it off.</summary>
    public bool IsDatasetCaptureForced => _forcedOn;

    public bool IsDatasetCaptureEnabled
    {
        get => _forcedOn || _settings.DatasetCaptureEnabled;
        set
        {
            if (_settings.DatasetCaptureEnabled == value)
            {
                return;
            }

            _settings.DatasetCaptureEnabled = value;
            Save();
        }
    }

    /// <summary>
    /// The combination the user chose. Falls back to Ctrl+Shift+X when the
    /// stored value is missing or unusable.
    /// </summary>
    public HotkeyDefinition Hotkey
    {
        get => HotkeyDefinition.Parse(_settings.Hotkey);
        set
        {
            var serialized = value.Serialize();
            if (string.Equals(_settings.Hotkey, serialized, StringComparison.Ordinal))
            {
                return;
            }

            _settings.Hotkey = serialized;
            Save();
        }
    }

    public AppLanguage Language
    {
        get => Enum.TryParse<AppLanguage>(_settings.Language, ignoreCase: true, out var language)
            ? language
            : AppLanguage.System;
        set
        {
            var serialized = value.ToString();
            if (string.Equals(_settings.Language, serialized, StringComparison.Ordinal))
            {
                return;
            }

            _settings.Language = serialized;
            Save();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(_settings, SerializerOptions));
        }
        catch
        {
            // A settings write failure must not take the app down; the toggle
            // simply reverts to its default on the next run.
        }
    }

    private static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), SerializerOptions)
                       ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt settings fall back to defaults rather than blocking startup.
        }

        return new AppSettings();
    }

    private sealed class AppSettings
    {
        public bool DatasetCaptureEnabled { get; set; }

        /// <summary>Serialized <see cref="HotkeyDefinition"/>; null means the default.</summary>
        public string? Hotkey { get; set; }

        /// <summary>Serialized <see cref="AppLanguage"/>; null means follow the system.</summary>
        public string? Language { get; set; }
    }
}
