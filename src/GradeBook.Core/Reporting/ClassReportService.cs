using GradeBook.Core.Data.Repositories;
using GradeBook.Core.GradeCalculation;
using GradeBook.Core.Models;

namespace GradeBook.Core.Reporting;

/// <summary>
/// Builds a class report: every student who was ever enrolled in the class or has grades within the
/// selected period, their grade percentage, and their missing-assignment count for that period.
/// </summary>
public sealed class ClassReportService(
    IClassRepository classRepository,
    IStudentRepository studentRepository,
    IEnrollmentRepository enrollmentRepository,
    IGradeRepository gradeRepository)
{
    public async Task<ClassReportData> BuildReportAsync(int classId, ReportPeriod period)
    {
        var schoolClass = await classRepository.GetByIdAsync(classId)
            ?? throw new InvalidOperationException($"Class {classId} not found.");

        var quarters = ReportPeriodQuarters.For(period);
        var records = await gradeRepository.GetRecordsForClassAsync(classId, quarters);
        var activeStudentIds = await enrollmentRepository.GetActiveStudentIdsForClassAsync(classId);

        // Include anyone currently enrolled (even with no grades yet) and anyone with historical
        // grades in this period (even if since unenrolled) — history is never hidden by unenrollment.
        var allStudentIds = activeStudentIds.Union(records.Select(r => r.StudentId)).ToList();
        var studentsById = (await studentRepository.GetAllAsync(includeInactive: true)).ToDictionary(s => s.Id);

        var rows = new List<ClassReportRow>();
        foreach (var studentId in allStudentIds)
        {
            if (!studentsById.TryGetValue(studentId, out var student))
            {
                continue;
            }

            var studentRecords = records.Where(r => r.StudentId == studentId).ToList();
            var periodResult = GradeCalculator.CalculatePeriodGrade(period, BuildGradesByQuarter(quarters, studentRecords));
            var breakdown = periodResult.QuarterBreakdown
                .Select(q => new QuarterBreakdownEntry(ReportPeriodQuarters.Label(q.Quarter), q.Percentage))
                .ToList();

            rows.Add(new ClassReportRow(student.Name, periodResult.Percentage, periodResult.UncompletedCount, breakdown));
        }

        rows = rows.OrderBy(r => r.StudentName, StringComparer.OrdinalIgnoreCase).ToList();
        return new ClassReportData(schoolClass.Name, period, rows);
    }

    private static Dictionary<Quarter, IReadOnlyList<GradeLine>> BuildGradesByQuarter(
        IReadOnlyList<Quarter> quarters,
        List<Data.AssignmentGradeRecord> records) =>
        quarters.ToDictionary(
            q => q,
            q => (IReadOnlyList<GradeLine>)records
                .Where(r => r.Quarter == q)
                .Select(r => new GradeLine(r.Score, r.PointsPossible, r.Status))
                .ToList());
}
