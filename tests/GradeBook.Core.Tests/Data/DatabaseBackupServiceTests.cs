using GradeBook.Core.Data;
using GradeBook.Core.Data.Repositories;
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

    private async Task<SqliteConnectionFactory> CreateDatabaseWithStudentAsync(string name)
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        DatabaseInitializer.Initialize(factory);
        await new StudentRepository(factory).AddAsync(name);
        return factory;
    }

    [Fact]
    public async Task CreateDailyBackupIfNeeded_CreatesDatedCopy_ThatIsAHealthyDatabaseWithTheSameData()
    {
        await CreateDatabaseWithStudentAsync("Micah");

        DatabaseBackupService.CreateDailyBackupIfNeeded(_databasePath);

        Assert.True(File.Exists(TodaysBackupPath));
        var backup = new SqliteConnectionFactory(TodaysBackupPath);
        Assert.Null(DatabaseInitializer.CheckIntegrity(backup));
        var students = await new StudentRepository(backup).GetAllAsync();
        Assert.Equal("Micah", Assert.Single(students).Name);
        Assert.False(File.Exists(TodaysBackupPath + ".tmp"));
    }

    [Fact]
    public async Task CreateDailyBackupIfNeeded_DoesNothing_WhenTodaysBackupAlreadyExists()
    {
        await CreateDatabaseWithStudentAsync("Micah");
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

    [Fact]
    public async Task CopyDatabase_RefusesToOverwriteAnExistingFile()
    {
        await CreateDatabaseWithStudentAsync("Micah");
        var destination = Path.Combine(_directory, "copy.db");
        File.WriteAllText(destination, "keep me");

        Assert.Throws<IOException>(() => DatabaseBackupService.CopyDatabase(_databasePath, destination));
        Assert.Equal("keep me", File.ReadAllText(destination));
    }

    [Fact]
    public void FindNewestBackup_ReturnsMostRecentDatedBackup_AndIgnoresOtherFiles()
    {
        var older = Path.Combine(_directory, "090126-gradebook.db");
        var newer = Path.Combine(_directory, "092326-gradebook.db");
        File.WriteAllText(older, "");
        File.WriteAllText(newer, "");
        File.WriteAllText(Path.Combine(_directory, "gradebook.db"), "");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-20));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow.AddDays(-1));

        Assert.Equal(newer, DatabaseBackupService.FindNewestBackup(_databasePath));
    }
}
