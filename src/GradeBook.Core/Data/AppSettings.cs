using GradeBook.Core.Models;

namespace GradeBook.Core.Data;

public sealed class AppSettings
{
    /// <summary>Null means "use the default location" (SqliteConnectionFactory.ResolveDefaultDatabasePath()).</summary>
    public string? DatabasePath { get; set; }

    /// <summary>The quarter last selected on the Gradebook tab, restored at startup so new lessons don't silently land in Q1.</summary>
    public Quarter? LastQuarter { get; set; }

    /// <summary>True once the one-time check for old pre-enrollment "Uncompleted" grades has run on this computer.</summary>
    public bool PreEnrollmentReviewDone { get; set; }
}
