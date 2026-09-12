using GradeBook.Core.Models;

namespace GradeBook.Core.GradeCalculation;

/// <summary>
/// Single result shape the Reports layer consumes for any period. QuarterBreakdown is empty for a
/// single-quarter period, and holds the 2 (Semester) or 4 (Final) contributing quarters otherwise.
/// </summary>
public sealed record PeriodGradeResult(
    ReportPeriod Period,
    decimal? Percentage,
    int UncompletedCount,
    IReadOnlyList<QuarterGradeResult> QuarterBreakdown);
