using GradeBook.Core.Data.Repositories;
using GradeBook.Core.GradeCalculation;
using GradeBook.Core.Models;

namespace GradeBook.Core.Reporting;

/// <summary>
/// Builds a school-wide summary: every student with grades or a current class in the period (including
/// students who have since left, flagged inactive), the classes they're in, and their grade
/// percentage in each for the selected period, plus their absences and tardies summed over the period's
/// quarters. Reuses StudentReportService's per-class grade calculation rather than recomputing it.
/// </summary>
public sealed class SummaryReportService(
    IStudentRepository studentRepository,
    StudentReportService studentReportService,
    IAttendanceRepository attendanceRepository)
{
    public async Task<SummaryReportData> BuildReportAsync(ReportPeriod period)
    {
        var students = (await studentRepository.GetAllAsync(includeInactive: true))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var attendance = await attendanceRepository.GetTotalsAsync(ReportPeriodQuarters.For(period));

        var entries = new List<SummaryStudentEntry>();
        foreach (var student in students)
        {
            var data = await studentReportService.BuildReportAsync(student.Id, period);
            if (data.ClassResults.Count == 0)
            {
                continue;
            }

            var classGrades = data.ClassResults
                .Select(r => new SummaryClassGrade(r.ClassName, r.Percentage, r.MissingAssignments))
                .ToList();
            var (absences, tardies) = attendance.GetValueOrDefault(student.Id);
            entries.Add(new SummaryStudentEntry(student.Name, classGrades, absences, tardies, student.IsActive));
        }

        return new SummaryReportData(period, entries);
    }
}
