using System.Text.Json;

namespace GradeBook.Core.Data;

/// <summary>
/// Loads/saves the small local settings file (currently just the configured database path) from the
/// same application-data directory the default database lives in. This file itself always stays local
/// to the machine — it just remembers where the *real* database file is, which may be a synced folder
/// (e.g. Nextcloud) shared across multiple computers.
/// </summary>
public sealed class AppSettingsStore
{
    private readonly string _settingsFilePath;

    public AppSettingsStore(string? settingsFilePathOverride = null)
    {
        if (settingsFilePathOverride is not null)
        {
            _settingsFilePath = settingsFilePathOverride;
            return;
        }

        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var gradeBookDir = Path.Combine(appDataDir, "GradeBook");
        Directory.CreateDirectory(gradeBookDir);
        _settingsFilePath = Path.Combine(gradeBookDir, "settings.json");
    }

    public string SettingsFilePath => _settingsFilePath;

    /// <summary>Throws InvalidDataException (rather than a raw JsonException) if the file exists but is damaged.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_settingsFilePath);
        try
        {
            return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The settings file '{_settingsFilePath}' is damaged: {ex.Message}", ex);
        }
    }

    /// <summary>Written to a temp file and renamed into place, so a crash mid-save can't leave a half-written settings file.</summary>
    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        var tempPath = _settingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _settingsFilePath, overwrite: true);
    }

    /// <summary>Loads, applies a change, and saves — so updating one setting never drops the others.</summary>
    public void Update(Action<AppSettings> change)
    {
        var settings = Load();
        change(settings);
        Save(settings);
    }
}
