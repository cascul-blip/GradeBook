using System.Reflection;
using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data;

public static class DatabaseInitializer
{
    private const string SchemaResourceName = "GradeBook.Core.Data.Sql.schema.sql";

    public static void Initialize(SqliteConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = ReadEmbeddedSchema();
        command.ExecuteNonQuery();
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
