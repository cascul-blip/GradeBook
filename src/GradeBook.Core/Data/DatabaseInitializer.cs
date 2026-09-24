using System.Reflection;
using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data;

public static class DatabaseInitializer
{
    private const string SchemaResourceName = "GradeBook.Core.Data.Sql.schema.sql";

    public static void Initialize(SqliteConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateOpenConnection();

        // Rollback-journal mode keeps the database to a single file between writes. WAL would leave
        // -wal/-shm side files that a sync client (e.g. Nextcloud) can copy out of step with the main file.
        using (var journalCommand = connection.CreateCommand())
        {
            journalCommand.CommandText = "PRAGMA journal_mode = DELETE;";
            journalCommand.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText = ReadEmbeddedSchema();
        command.ExecuteNonQuery();
    }

    /// <summary>Runs SQLite's quick integrity check. Returns null when the database is healthy, otherwise the problems it reported.</summary>
    public static string? CheckIntegrity(SqliteConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";

        var problems = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            problems.Add(reader.GetString(0));
        }

        return problems is ["ok"] ? null : string.Join(Environment.NewLine, problems);
    }

    private static string ReadEmbeddedSchema()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{SchemaResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
