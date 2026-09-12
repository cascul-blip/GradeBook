using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data;

/// <summary>
/// Resolves the gradebook database file to an OS-appropriate application-data directory
/// (not next to the executable) and opens connections against it with foreign keys enabled.
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
            ForeignKeys = true
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
