using GradeBook.Core.Data;
using GradeBook.Core.Models;
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

    [Fact]
    public void Update_ChangesOneSetting_WithoutDroppingTheOthers()
    {
        var store = new AppSettingsStore(_settingsPath);
        store.Save(new AppSettings { DatabasePath = "/synced/gradebook.db" });

        store.Update(s => s.LastQuarter = Quarter.Q3);
        var reloaded = store.Load();

        Assert.Equal("/synced/gradebook.db", reloaded.DatabasePath);
        Assert.Equal(Quarter.Q3, reloaded.LastQuarter);
    }

    [Fact]
    public void Load_ThrowsInvalidDataException_WhenFileIsDamaged()
    {
        File.WriteAllText(_settingsPath, "{ \"DatabasePath\": ");
        var store = new AppSettingsStore(_settingsPath);

        Assert.Throws<InvalidDataException>(() => store.Load());
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        var store = new AppSettingsStore(_settingsPath);

        store.Save(new AppSettings { LastQuarter = Quarter.Q2 });

        Assert.False(File.Exists(_settingsPath + ".tmp"));
    }
}
