using GradeBook.Core.Models;

namespace GradeBook.Core.GradeCalculation;

/// <summary>Maps a selectable report period to the underlying quarters that need to be queried from the database.</summary>
public static class ReportPeriodQuarters
{
    public static IReadOnlyList<Quarter> For(ReportPeriod period) => period switch
    {
        ReportPeriod.Quarter1 => [Quarter.Q1],
        ReportPeriod.Quarter2 => [Quarter.Q2],
        ReportPeriod.Quarter3 => [Quarter.Q3],
        ReportPeriod.Quarter4 => [Quarter.Q4],
        ReportPeriod.Semester1 => [Quarter.Q1, Quarter.Q2],
        ReportPeriod.Semester2 => [Quarter.Q3, Quarter.Q4],
        ReportPeriod.Final => [Quarter.Q1, Quarter.Q2, Quarter.Q3, Quarter.Q4],
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown report period.")
    };

    public static string Label(Quarter quarter) => quarter switch
    {
        Quarter.Q1 => "Q1",
        Quarter.Q2 => "Q2",
        Quarter.Q3 => "Q3",
        Quarter.Q4 => "Q4",
        _ => quarter.ToString()
    };
}
