using GradeBook.Core.Models;

namespace GradeBook.Core.GradeCalculation;

/// <summary>Percentage is null ("N/A") when there were no non-Excused assignments to grade.</summary>
public sealed record QuarterGradeResult(Quarter Quarter, decimal? Percentage, int UncompletedCount);
