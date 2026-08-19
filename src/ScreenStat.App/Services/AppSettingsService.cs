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
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenStat",
            "settings.json");
        _forcedOn = string.Equals(
            Environment.GetEnvironmentVariable(ForceEnableVariableName)?.Trim(),
            "1",
            StringComparison.Ordinal);
        _settings = Load(_path);
    }

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
    }
}
