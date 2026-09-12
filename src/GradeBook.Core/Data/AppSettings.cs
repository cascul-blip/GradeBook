namespace GradeBook.Core.Data;

public sealed class AppSettings
{
    /// <summary>Null means "use the default location" (SqliteConnectionFactory.ResolveDefaultDatabasePath()).</summary>
    public string? DatabasePath { get; set; }
}
