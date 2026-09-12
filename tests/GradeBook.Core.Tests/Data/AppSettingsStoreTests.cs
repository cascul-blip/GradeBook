using GradeBook.Core.Data;
using Xunit;

namespace GradeBook.Core.Tests.Data;

public class AppSettingsStoreTests : IDisposable
{
    private readonly string _settingsPath;

    public AppSettingsStoreTests()
    {
        _settingsPath = Path.Combine(Path.GetTempPath(), $"gradebook-settings-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    [Fact]
    public void Load_ReturnsEmptySettings_WhenNoFileExistsYet()
    {
        var store = new AppSettingsStore(_settingsPath);

        var settings = store.Load();

        Assert.Null(settings.DatabasePath);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheDatabasePath()
    {
        var store = new AppSettingsStore(_settingsPath);
        const string path = "/synced/Nextcloud/GradeBook/gradebook.db";

        store.Save(new AppSettings { DatabasePath = path });
        var reloaded = store.Load();

        Assert.Equal(path, reloaded.DatabasePath);
    }
}
