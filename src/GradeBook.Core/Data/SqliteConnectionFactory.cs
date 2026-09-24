using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data;

/// <summary>
/// Resolves the gradebook database file to an OS-appropriate application-data directory
/// (not next to the executable) and opens connections against it with foreign keys enabled.
/// Pooling is off so the file is only held open for the duration of each operation — the database
/// may live in a synced folder, and a long-held handle either blocks the sync client (Windows) or
/// keeps writing to a file the sync client has already replaced (Linux).
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string? databasePathOverride = null)
    {
        DatabasePath = databasePathOverride ?? ResolveDefaultDatabasePath();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 10
        }.ToString();
    }

    public string DatabasePath { get; }

    public SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>The path used when no explicit location has been configured via Settings.</summary>
    public static string ResolveDefaultDatabasePath()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var gradeBookDir = Path.Combine(appDataDir, "GradeBook");
        Directory.CreateDirectory(gradeBookDir);
        return Path.Combine(gradeBookDir, "gradebook.db");
    }
}
