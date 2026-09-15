namespace GradeBook.Core.GradeCalculation;

/// <summary>Percentage is the average of whichever of the two quarters have a grade; null only if both are null.</summary>
public sealed record SemesterGradeResult(IReadOnlyList<QuarterGradeResult> Quarters, decimal? Percentage);
