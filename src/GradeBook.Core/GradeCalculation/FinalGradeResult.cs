namespace GradeBook.Core.GradeCalculation;

/// <summary>Percentage is a flat average of whichever of the four quarters have a grade; null only if all four are null.</summary>
public sealed record FinalGradeResult(IReadOnlyList<QuarterGradeResult> Quarters, decimal? Percentage);
