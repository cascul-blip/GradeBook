using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.Core.Reporting;

/// <summary>
/// Builds a school-wide summary: every active student, the classes they're in, and their grade
/// percentage in each for the selected period. Reuses StudentReportService's per-class grade
/// calculation rather than recomputing it.
/// </summary>
public sealed class SummaryReportService(IStudentRepository studentRepository, StudentReportService studentReportService)
{
    public async Task<SummaryReportData> BuildReportAsync(ReportPeriod period)
    {
        var students = (await studentRepository.GetAllAsync())
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = new List<SummaryStudentEntry>();
        foreach (var student in students)
        {
            var data = await studentReportService.BuildReportAsync(student.Id, period);
            if (data.ClassResults.Count == 0)
            {
                continue;
            }

            var classGrades = data.ClassResults
                .Select(r => new SummaryClassGrade(r.ClassName, r.Percentage, r.MissingAssignments.Count))
                .ToList();
            entries.Add(new SummaryStudentEntry(student.Name, classGrades));
        }

        return new SummaryReportData(period, entries);
    }
}
