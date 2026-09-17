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

public sealed record MissingAssignmentEntry(string AssignmentName, DateTime AssignmentDate);

public sealed record StudentClassResult(
    string ClassName,
    decimal? Percentage,
    IReadOnlyList<MissingAssignmentEntry> MissingAssignments,
    IReadOnlyList<QuarterBreakdownEntry> QuarterBreakdown);

public sealed record StudentReportData(
    string StudentName,
    ReportPeriod Period,
    IReadOnlyList<StudentClassResult> ClassResults);

public sealed record SummaryClassGrade(string ClassName, decimal? Percentage, int MissingCount);

public sealed record SummaryStudentEntry(string StudentName, IReadOnlyList<SummaryClassGrade> Classes);

public sealed record SummaryReportData(ReportPeriod Period, IReadOnlyList<SummaryStudentEntry> Students);
