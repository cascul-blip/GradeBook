using GradeBook.Core.Models;

namespace GradeBook.Core.GradeCalculation;

/// <summary>
/// Pure grade-math functions. No DB/UI dependency, uses decimal throughout so the
/// "round the late penalty up" rule is exact rather than subject to floating-point drift.
/// </summary>
public static class GradeCalculator
{
    private const decimal LatePenaltyRate = 0.10m;

    public static decimal CalculateLatePenalty(decimal pointsPossible) =>
        Math.Ceiling(pointsPossible * LatePenaltyRate);

    public static decimal CalculateEffectiveScore(GradeLine line) => line.Status switch
    {
        GradeStatus.Excused => 0m,
        GradeStatus.Uncompleted => 0m,
        GradeStatus.Completed => line.Score,
        GradeStatus.Late => Math.Max(0m, line.Score - CalculateLatePenalty(line.PointsPossible)),
        _ => throw new ArgumentOutOfRangeException(nameof(line), line.Status, "Unknown grade status.")
    };

    /// <summary>Sums effective score/points-possible across all non-Excused lines. Excused lines are skipped entirely.</summary>
    public static (decimal Earned, decimal Possible, int Count) SumNonExcused(IEnumerable<GradeLine> lines)
    {
        decimal earned = 0m, possible = 0m;
        int count = 0;
        foreach (var line in lines)
        {
            if (line.Status == GradeStatus.Excused)
            {
                continue;
            }

            earned += CalculateEffectiveScore(line);
            possible += line.PointsPossible;
            count++;
        }

        return (earned, possible, count);
    }

    /// <summary>Percentage is null ("N/A") rather than a divide-by-zero when there are no non-Excused assignments.</summary>
    public static QuarterGradeResult CalculateQuarterGrade(Quarter quarter, IReadOnlyList<GradeLine> gradesInQuarter)
    {
        var (earned, possible, _) = SumNonExcused(gradesInQuarter);
        decimal? percentage = possible > 0m ? Math.Round(earned / possible * 100m, 2) : null;
        int uncompletedCount = gradesInQuarter.Count(g => g.Status == GradeStatus.Uncompleted);
        return new QuarterGradeResult(quarter, percentage, uncompletedCount);
    }

    private static decimal? AveragePercentages(IEnumerable<decimal?> percentages)
    {
        var values = percentages.Where(p => p.HasValue).Select(p => p!.Value).ToList();
        return values.Count > 0 ? Math.Round(values.Average(), 2) : null;
    }

    /// <summary>Averages whichever of the two quarters have a grade; a quarter with no data is dropped, not treated as 0%.</summary>
    public static SemesterGradeResult CalculateSemesterGrade(int semesterNumber, QuarterGradeResult quarterA, QuarterGradeResult quarterB)
    {
        var quarters = new[] { quarterA, quarterB };
        decimal? percentage = AveragePercentages(quarters.Select(q => q.Percentage));
        return new SemesterGradeResult(semesterNumber, quarters, percentage);
    }

    /// <summary>Flat average of whichever of the four quarters have a grade — not a nested average of the two semesters.</summary>
    public static FinalGradeResult CalculateFinalGrade(IReadOnlyList<QuarterGradeResult> allFourQuarters)
    {
        decimal? percentage = AveragePercentages(allFourQuarters.Select(q => q.Percentage));
        return new FinalGradeResult(allFourQuarters, percentage);
    }

    /// <summary>Single entry point the Reports layer calls for any selectable report period.</summary>
    public static PeriodGradeResult CalculatePeriodGrade(
        ReportPeriod period,
        IReadOnlyDictionary<Quarter, IReadOnlyList<GradeLine>> gradesByQuarter)
    {
        IReadOnlyList<GradeLine> LinesFor(Quarter q) =>
            gradesByQuarter.TryGetValue(q, out var lines) ? lines : Array.Empty<GradeLine>();

        QuarterGradeResult SingleQuarter(Quarter q) => CalculateQuarterGrade(q, LinesFor(q));

        switch (period)
        {
            case ReportPeriod.Quarter1:
            case ReportPeriod.Quarter2:
            case ReportPeriod.Quarter3:
            case ReportPeriod.Quarter4:
            {
                var quarter = period switch
                {
                    ReportPeriod.Quarter1 => Quarter.Q1,
                    ReportPeriod.Quarter2 => Quarter.Q2,
                    ReportPeriod.Quarter3 => Quarter.Q3,
                    _ => Quarter.Q4
                };
                var result = SingleQuarter(quarter);
                return new PeriodGradeResult(period, result.Percentage, result.UncompletedCount, Array.Empty<QuarterGradeResult>());
            }

            case ReportPeriod.Semester1:
            {
                var q1 = SingleQuarter(Quarter.Q1);
                var q2 = SingleQuarter(Quarter.Q2);
                var semester = CalculateSemesterGrade(1, q1, q2);
                return new PeriodGradeResult(period, semester.Percentage, q1.UncompletedCount + q2.UncompletedCount, semester.Quarters);
            }

            case ReportPeriod.Semester2:
            {
                var q3 = SingleQuarter(Quarter.Q3);
                var q4 = SingleQuarter(Quarter.Q4);
                var semester = CalculateSemesterGrade(2, q3, q4);
                return new PeriodGradeResult(period, semester.Percentage, q3.UncompletedCount + q4.UncompletedCount, semester.Quarters);
            }

            case ReportPeriod.Final:
            {
                var allFour = new[]
                {
                    SingleQuarter(Quarter.Q1),
                    SingleQuarter(Quarter.Q2),
                    SingleQuarter(Quarter.Q3),
                    SingleQuarter(Quarter.Q4)
                };
                var final = CalculateFinalGrade(allFour);
                return new PeriodGradeResult(period, final.Percentage, allFour.Sum(q => q.UncompletedCount), final.Quarters);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown report period.");
        }
    }
}
