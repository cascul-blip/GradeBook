using System.Globalization;

namespace GradeBook.Core.Data;

internal static class SqliteDates
{
    /// <summary>
    /// SQLite's datetime('now') defaults are UTC text; convert to local time for display so a lesson
    /// created in the evening isn't labelled with tomorrow's date.
    /// </summary>
    public static DateTime ParseUtcToLocal(string value) =>
        DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture), DateTimeKind.Utc).ToLocalTime();
}
