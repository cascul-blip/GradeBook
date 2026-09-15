using GradeBook.Core.Data;
using GradeBook.Core.GradeCalculation;
using GradeBook.Core.Models;

namespace GradeBook.Core.Reporting;

internal static class GradeLineGrouping
{
    public static Dictionary<Quarter, IReadOnlyList<GradeLine>> ByQuarter(
        IReadOnlyList<Quarter> quarters,
        IEnumerable<AssignmentGradeRecord> records) =>
        quarters.ToDictionary(
            q => q,
            q => (IReadOnlyList<GradeLine>)records
                .Where(r => r.Quarter == q)
                .Select(r => new GradeLine(r.Score, r.PointsPossible, r.Status))
                .ToList());
}
