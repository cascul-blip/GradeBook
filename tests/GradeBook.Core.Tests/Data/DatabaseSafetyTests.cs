using System.Diagnostics;
using GradeBook.Core.Data;
using Xunit;

namespace GradeBook.Core.Tests.Data;

public class DatabaseSafetyTests : IDisposable
{
    private readonly string _directory;
    private readonly string _databasePath;

    public DatabaseSafetyTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"gradebook-safety-test-{Guid.NewGuid():N}");
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

    private void WriteLock(string machine, int processId, DateTime openedAtUtc) =>
        File.WriteAllText(_databasePath + ".lock",
            $$"""{"MachineName":"{{machine}}","UserName":"someone","ProcessId":{{processId}},"OpenedAtUtc":"{{openedAtUtc:O}}"}""");

    [Fact]
    public void Lock_IsFree_WhenNoLockFileExists()
    {
        Assert.Equal(DatabaseLockState.Free, new DatabaseLockFile(_databasePath).Check().State);
    }

    [Fact]
    public void Lock_AcquireThenRelease_RemovesTheFile()
    {
        var lockFile = new DatabaseLockFile(_databasePath);

        lockFile.Acquire();
        Assert.True(File.Exists(lockFile.LockPath));
        Assert.Equal(DatabaseLockState.Free, lockFile.Check().State); // our own lock doesn't block us

        lockFile.Release();
        Assert.False(File.Exists(lockFile.LockPath));
    }

    [Fact]
    public void Lock_HeldByOtherMachine_IsReported_AndReleaseNeverDeletesIt()
    {
        WriteLock("SCHOOL-PC", 1234, DateTime.UtcNow);
        var lockFile = new DatabaseLockFile(_databasePath);

        var check = lockFile.Check();

        Assert.Equal(DatabaseLockState.HeldByOtherMachine, check.State);
        Assert.Equal("SCHOOL-PC", check.Holder!.MachineName);
        lockFile.Release();
        Assert.True(File.Exists(lockFile.LockPath));
    }

    [Fact]
    public void Lock_Unreadable_IsTreatedAsHeldElsewhere()
    {
        File.WriteAllText(_databasePath + ".lock", "not json");

        var check = new DatabaseLockFile(_databasePath).Check();

        Assert.Equal(DatabaseLockState.HeldByOtherMachine, check.State);
        Assert.Null(check.Holder);
    }

    [Fact]
    public void Lock_FromThisMachine_WithDeadProcess_IsStale()
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", "--version") { RedirectStandardOutput = true })!;
        process.WaitForExit();
        WriteLock(Environment.MachineName, process.Id, DateTime.UtcNow);

        Assert.Equal(DatabaseLockState.HeldByThisMachineStale, new DatabaseLockFile(_databasePath).Check().State);
    }

    [Fact]
    public void Lock_FromThisMachine_WithLiveProcess_IsRunning()
    {
        // Stands in for another GradeBook window on this computer: a different, still-running process.
        using var child = Process.Start(new ProcessStartInfo("sleep", "5"))!;
        try
        {
            WriteLock(Environment.MachineName, child.Id, DateTime.UtcNow);

            Assert.Equal(DatabaseLockState.HeldByThisMachineRunning, new DatabaseLockFile(_databasePath).Check().State);
        }
        finally
        {
            child.Kill();
        }
    }

    [Fact]
    public void ConflictDetector_FindsSyncConflictCopies_ButNotBackupsOrLocks()
    {
        var conflict = "gradebook (conflicted copy 2026-09-24 083012).db";
        File.WriteAllText(Path.Combine(_directory, conflict), "");
        File.WriteAllText(Path.Combine(_directory, "092426-gradebook.db"), "");
        File.WriteAllText(Path.Combine(_directory, "gradebook.db"), "");
        File.WriteAllText(Path.Combine(_directory, "gradebook.db.lock"), "");
        File.WriteAllText(Path.Combine(_directory, "gradebook.db (conflicted copy 2026-09-24).lock"), "");

        var found = SyncConflictDetector.FindConflictCopies(_databasePath);

        Assert.Equal([conflict], found);
    }

    [Fact]
    public void CheckIntegrity_ReturnsNull_ForAHealthyDatabase()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        DatabaseInitializer.Initialize(factory);

        Assert.Null(DatabaseInitializer.CheckIntegrity(factory));
    }

    [Fact]
    public void CheckIntegrity_ReportsDamage_ForATruncatedDatabase()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        DatabaseInitializer.Initialize(factory);
        using (var connection = factory.CreateOpenConnection())
        using (var command = connection.CreateCommand())
        {
            // Enough rows to span several pages, so truncating the file cuts real data off.
            command.CommandText = "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 2000) " +
                                  "INSERT INTO Students (Name) SELECT 'Student ' || i FROM n;";
            command.ExecuteNonQuery();
        }

        using (var stream = new FileStream(_databasePath, FileMode.Open))
        {
            stream.SetLength(stream.Length / 2);
        }

        // Startup treats either outcome (problems reported, or SQLite refusing the file) as "damaged".
        try
        {
            Assert.NotNull(DatabaseInitializer.CheckIntegrity(factory));
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
        }
    }

    [Fact]
    public void CheckIntegrity_Throws_ForAFileThatIsNotADatabase()
    {
        File.WriteAllText(_databasePath, new string('x', 4096));

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => DatabaseInitializer.CheckIntegrity(new SqliteConnectionFactory(_databasePath)));
    }

    [Fact]
    public void Initialize_UsesRollbackJournal_NotWal()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        DatabaseInitializer.Initialize(factory);

        using var connection = factory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("delete", (string)command.ExecuteScalar()!);
    }
}
