using GradeBook.Core.Models;

namespace GradeBook.Reports.Pdf;

public static class ReportLabels
{
    public static string PeriodLabel(ReportPeriod period) => period switch
    {
        ReportPeriod.Quarter1 => "Quarter 1",
        ReportPeriod.Quarter2 => "Quarter 2",
        ReportPeriod.Quarter3 => "Quarter 3",
        ReportPeriod.Quarter4 => "Quarter 4",
        ReportPeriod.Semester1 => "Semester 1",
        ReportPeriod.Semester2 => "Semester 2",
        ReportPeriod.Final => "Final",
        _ => period.ToString()
    };

    public static string PercentLabel(decimal? percentage) => percentage.HasValue ? $"{percentage.Value:0.##}%" : "N/A";

    public static string MissingCountLabel(int count) => $"Missing {count} lesson{(count == 1 ? "" : "s")}";

    public static string AssignmentDateLabel(DateTime date) => date.ToString("MMM d");
}
