using GradeBook.Core.GradeCalculation;
using GradeBook.Core.Models;
using Xunit;

namespace GradeBook.Core.Tests;

public class GradeCalculatorTests
{
    [Theory]
    [InlineData(25, 3)]   // ceil(2.5) = 3
    [InlineData(1, 1)]    // ceil(0.1) = 1
    [InlineData(30, 3)]   // ceil(3.0) = 3
    [InlineData(33, 4)]   // ceil(3.3) = 4, no floating-point drift
    public void CalculateLatePenalty_RoundsUp(decimal pointsPossible, decimal expectedPenalty)
    {
        Assert.Equal(expectedPenalty, GradeCalculator.CalculateLatePenalty(pointsPossible));
    }

    [Fact]
    public void EffectiveScore_Late_SubtractsPenalty()
    {
        var line = new GradeLine(Score: 20, PointsPossible: 25, Status: GradeStatus.Late);
        Assert.Equal(17, GradeCalculator.CalculateEffectiveScore(line));
    }

    [Fact]
    public void EffectiveScore_Late_ClampsAtZero_NeverNegative()
    {
        var line = new GradeLine(Score: 1, PointsPossible: 50, Status: GradeStatus.Late);
        Assert.Equal(0, GradeCalculator.CalculateEffectiveScore(line));
    }

    [Fact]
    public void EffectiveScore_Late_PenalizesEvenAPerfectScore()
    {
        var line = new GradeLine(Score: 30, PointsPossible: 30, Status: GradeStatus.Late);
        Assert.Equal(27, GradeCalculator.CalculateEffectiveScore(line));
    }

    [Fact]
    public void EffectiveScore_Uncompleted_IsAlwaysZero_RegardlessOfStoredScore()
    {
        var line = new GradeLine(Score: 25, PointsPossible: 30, Status: GradeStatus.Uncompleted);
        Assert.Equal(0, GradeCalculator.CalculateEffectiveScore(line));
    }

    [Fact]
    public void EffectiveScore_Completed_IsRawScore()
    {
        var line = new GradeLine(Score: 27, PointsPossible: 30, Status: GradeStatus.Completed);
        Assert.Equal(27, GradeCalculator.CalculateEffectiveScore(line));
    }

    [Fact]
    public void SumNonExcused_ExcludesExcusedFromBothNumeratorAndDenominator()
    {
        var lines = new[]
        {
            new GradeLine(0, 30, GradeStatus.Uncompleted),  // Lesson 3: 0/30
            new GradeLine(0, 20, GradeStatus.Excused)       // Lesson 4: excused entirely
        };

        var (earned, possible, count) = GradeCalculator.SumNonExcused(lines);

        Assert.Equal(0, earned);
        Assert.Equal(30, possible); // the excused assignment's 20 points must NOT appear here
        Assert.Equal(1, count);
    }

    [Fact]
    public void QuarterGrade_AllExcused_IsNullNotDivideByZero()
    {
        var lines = new[] { new GradeLine(0, 30, GradeStatus.Excused) };
        var result = GradeCalculator.CalculateQuarterGrade(Quarter.Q1, lines);
        Assert.Null(result.Percentage);
    }

    [Fact]
    public void QuarterGrade_NoAssignments_IsNullNotDivideByZero()
    {
        var result = GradeCalculator.CalculateQuarterGrade(Quarter.Q1, []);
        Assert.Null(result.Percentage);
        Assert.Equal(0, result.UncompletedCount);
    }

    [Fact]
    public void QuarterGrade_MultipleAssignments_AggregatesCorrectly()
    {
        var lines = new[]
        {
            new GradeLine(30, 30, GradeStatus.Completed),
            new GradeLine(10, 20, GradeStatus.Completed)
        };

        var result = GradeCalculator.CalculateQuarterGrade(Quarter.Q1, lines);

        Assert.Equal(80m, result.Percentage); // 40/50 = 80%
    }

    [Fact]
    public void QuarterGrade_CountsUncompletedAssignments()
    {
        var lines = new[]
        {
            new GradeLine(30, 30, GradeStatus.Completed),
            new GradeLine(0, 20, GradeStatus.Uncompleted),
            new GradeLine(0, 15, GradeStatus.Uncompleted)
        };

        var result = GradeCalculator.CalculateQuarterGrade(Quarter.Q1, lines);

        Assert.Equal(2, result.UncompletedCount);
    }

    [Fact]
    public void SemesterGrade_AveragesBothQuarters_WhenBothPresent()
    {
        var q1 = new QuarterGradeResult(Quarter.Q1, 90m, 0);
        var q2 = new QuarterGradeResult(Quarter.Q2, 70m, 0);

        var semester = GradeCalculator.CalculateSemesterGrade(q1, q2);

        Assert.Equal(80m, semester.Percentage);
    }

    [Fact]
    public void SemesterGrade_DropsMissingQuarter_RatherThanTreatingItAsZero()
    {
        var q1 = new QuarterGradeResult(Quarter.Q1, 90m, 0);
        var q2 = new QuarterGradeResult(Quarter.Q2, null, 0); // e.g. Q2 hasn't started yet

        var semester = GradeCalculator.CalculateSemesterGrade(q1, q2);

        Assert.Equal(90m, semester.Percentage); // not 45m
    }

    [Fact]
    public void SemesterGrade_BothQuartersMissing_IsNull()
    {
        var q1 = new QuarterGradeResult(Quarter.Q1, null, 0);
        var q2 = new QuarterGradeResult(Quarter.Q2, null, 0);

        var semester = GradeCalculator.CalculateSemesterGrade(q1, q2);

        Assert.Null(semester.Percentage);
    }

    [Fact]
    public void FinalGrade_IsFlatAverageOfPresentQuarters_NotNestedSemesterAverage()
    {
        // Q2 missing: nested (semester-of-semester) averaging would give avg(90, avg(80,90)) = 87.5,
        // but the spec calls for a flat average of whichever of the 4 quarters have data: (90+80+90)/3.
        var quarters = new List<QuarterGradeResult>
        {
            new(Quarter.Q1, 90m, 0),
            new(Quarter.Q2, null, 0),
            new(Quarter.Q3, 80m, 0),
            new(Quarter.Q4, 90m, 0)
        };

        var final = GradeCalculator.CalculateFinalGrade(quarters);

        Assert.Equal(86.67m, final.Percentage);
    }

    [Fact]
    public void FinalGrade_AllFourPresent_MatchesEqualWeightedAverage()
    {
        var quarters = new List<QuarterGradeResult>
        {
            new(Quarter.Q1, 100m, 0),
            new(Quarter.Q2, 90m, 0),
            new(Quarter.Q3, 80m, 0),
            new(Quarter.Q4, 70m, 0)
        };

        var final = GradeCalculator.CalculateFinalGrade(quarters);

        Assert.Equal(85m, final.Percentage);
    }

    [Fact]
    public void FinalGrade_AllFourMissing_IsNull()
    {
        var quarters = new List<QuarterGradeResult>
        {
            new(Quarter.Q1, null, 0),
            new(Quarter.Q2, null, 0),
            new(Quarter.Q3, null, 0),
            new(Quarter.Q4, null, 0)
        };

        Assert.Null(GradeCalculator.CalculateFinalGrade(quarters).Percentage);
    }

    [Fact]
    public void CalculatePeriodGrade_Semester_PopulatesQuarterBreakdown()
    {
        var gradesByQuarter = new Dictionary<Quarter, IReadOnlyList<GradeLine>>
        {
            [Quarter.Q1] = [new GradeLine(30, 30, GradeStatus.Completed)],
            [Quarter.Q2] = [new GradeLine(15, 30, GradeStatus.Completed)]
        };

        var result = GradeCalculator.CalculatePeriodGrade(ReportPeriod.Semester1, gradesByQuarter);

        Assert.Equal(2, result.QuarterBreakdown.Count);
        Assert.Equal(75m, result.Percentage); // average of 100% and 50%
    }

    [Fact]
    public void CalculatePeriodGrade_SingleQuarter_HasEmptyBreakdown()
    {
        var gradesByQuarter = new Dictionary<Quarter, IReadOnlyList<GradeLine>>
        {
            [Quarter.Q1] = [new GradeLine(30, 30, GradeStatus.Completed)]
        };

        var result = GradeCalculator.CalculatePeriodGrade(ReportPeriod.Quarter1, gradesByQuarter);

        Assert.Empty(result.QuarterBreakdown);
        Assert.Equal(100m, result.Percentage);
    }

    [Fact]
    public void StoryTest_Lesson3InMath87_MicahTurnsItInPrentissDoesNot()
    {
        // Reproduces the project's own worked example: a 30-point assignment where one student
        // turns it in and scores full marks, and another simply never turns it in.
        var micahLine = new GradeLine(30, 30, GradeStatus.Completed);
        var prentissLine = new GradeLine(0, 30, GradeStatus.Uncompleted);

        var micahResult = GradeCalculator.CalculateQuarterGrade(Quarter.Q1, [micahLine]);
        var prentissResult = GradeCalculator.CalculateQuarterGrade(Quarter.Q1, [prentissLine]);

        Assert.Equal(100m, micahResult.Percentage);
        Assert.Equal(0, prentissResult.Percentage);
        Assert.Equal(0, micahResult.UncompletedCount);
        Assert.Equal(1, prentissResult.UncompletedCount);
    }
}
