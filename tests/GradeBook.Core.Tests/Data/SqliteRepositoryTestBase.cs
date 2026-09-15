using GradeBook.Core.Data;

namespace GradeBook.Core.Tests.Data;

/// <summary>
/// Uses a real temp-file SQLite database (not :memory:) because each repository call opens its own
/// connection, and a pure :memory: database is a fresh empty instance per connection.
/// </summary>
public abstract class SqliteRepositoryTestBase : IDisposable
{
    private readonly string _dbPath;

    protected readonly SqliteConnectionFactory ConnectionFactory;

    protected SqliteRepositoryTestBase()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"gradebook-test-{Guid.NewGuid():N}.db");
        ConnectionFactory = new SqliteConnectionFactory(_dbPath);
        DatabaseInitializer.Initialize(ConnectionFactory);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
