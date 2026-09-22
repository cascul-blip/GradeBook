using GradeBook.Core.Data;
using Xunit;

namespace GradeBook.Core.Tests.Data;

public class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _databasePath;

    public DatabaseBackupServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"gradebook-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "gradebook.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string TodaysBackupPath => Path.Combine(_directory, $"{DateTime.Now:MMddyy}-gradebook.db");

    [Fact]
    public void CreateDailyBackupIfNeeded_CreatesDatedCopy_WhenNoBackupExistsYet()
    {
        File.WriteAllText(_databasePath, "db contents");

        DatabaseBackupService.CreateDailyBackupIfNeeded(_databasePath);

        Assert.True(File.Exists(TodaysBackupPath));
        Assert.Equal("db contents", File.ReadAllText(TodaysBackupPath));
    }

    [Fact]
    public void CreateDailyBackupIfNeeded_DoesNothing_WhenTodaysBackupAlreadyExists()
    {
        File.WriteAllText(_databasePath, "current contents");
        File.WriteAllText(TodaysBackupPath, "existing backup contents");

        DatabaseBackupService.CreateDailyBackupIfNeeded(_databasePath);

        Assert.Equal("existing backup contents", File.ReadAllText(TodaysBackupPath));
    }

    [Fact]
    public void CreateDailyBackupIfNeeded_DoesNothing_WhenDatabaseFileDoesNotExist()
    {
        DatabaseBackupService.CreateDailyBackupIfNeeded(_databasePath);

        Assert.False(File.Exists(TodaysBackupPath));
    }
}
