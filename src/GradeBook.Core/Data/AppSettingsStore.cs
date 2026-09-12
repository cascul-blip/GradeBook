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

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        File.WriteAllText(_settingsFilePath, json);
    }
}
