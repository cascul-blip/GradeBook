using GradeBook.Core.Models;

namespace GradeBook.Core.Reporting;

public sealed record QuarterBreakdownEntry(string QuarterLabel, decimal? Percentage);

public sealed record ClassReportRow(
    string StudentName,
    decimal? Percentage,
    int UncompletedCount,
    IReadOnlyList<QuarterBreakdownEntry> QuarterBreakdown);

public sealed record ClassReportData(
    string ClassName,
    ReportPeriod Period,
    IReadOnlyList<ClassReportRow> Rows);

public sealed record StudentClassResult(
    string ClassName,
    decimal? Percentage,
    IReadOnlyList<string> UncompletedAssignmentNames,
    IReadOnlyList<QuarterBreakdownEntry> QuarterBreakdown);

public sealed record StudentReportData(
    string StudentName,
    ReportPeriod Period,
    IReadOnlyList<StudentClassResult> ClassResults);
