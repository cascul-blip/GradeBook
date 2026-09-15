using GradeBook.Core.Data.Repositories;
using GradeBook.Core.GradeCalculation;
using GradeBook.Core.Models;

namespace GradeBook.Core.Reporting;

/// <summary>
/// Builds a student report: their grade percentage in every class they were ever enrolled in or have
/// grades within the selected period, plus the names of their missing (Uncompleted) assignments.
/// </summary>
public sealed class StudentReportService(
    IStudentRepository studentRepository,
    IClassRepository classRepository,
    IEnrollmentRepository enrollmentRepository,
    IGradeRepository gradeRepository)
{
    public async Task<StudentReportData> BuildReportAsync(int studentId, ReportPeriod period)
    {
        var student = await studentRepository.GetByIdAsync(studentId)
            ?? throw new InvalidOperationException($"Student {studentId} not found.");

        var quarters = ReportPeriodQuarters.For(period);
        var records = await gradeRepository.GetRecordsForStudentAsync(studentId, quarters);
        var activeClassIds = await enrollmentRepository.GetActiveClassIdsForStudentAsync(studentId);

        var allClassIds = activeClassIds.Union(records.Select(r => r.ClassId)).ToList();
        var classesById = (await classRepository.GetAllAsync(includeInactive: true)).ToDictionary(c => c.Id);

        var results = new List<StudentClassResult>();
        foreach (var classId in allClassIds)
        {
            if (!classesById.TryGetValue(classId, out var schoolClass))
            {
                continue;
            }

            var classRecords = records.Where(r => r.ClassId == classId).ToList();
            var periodResult = GradeCalculator.CalculatePeriodGrade(period, GradeLineGrouping.ByQuarter(quarters, classRecords));
            var breakdown = periodResult.QuarterBreakdown
                .Select(q => new QuarterBreakdownEntry(ReportPeriodQuarters.Label(q.Quarter), q.Percentage))
                .ToList();
            var missingAssignments = classRecords
                .Where(r => r.Status == GradeStatus.Uncompleted)
                .OrderBy(r => r.AssignmentDate)
                .Select(r => new MissingAssignmentEntry(r.AssignmentName, r.AssignmentDate))
                .ToList();

            results.Add(new StudentClassResult(schoolClass.Name, periodResult.Percentage, missingAssignments, breakdown));
        }

        results = results.OrderBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase).ToList();
        return new StudentReportData(student.Name, period, results);
    }
}
