using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public interface IAttendanceRepository
{
    /// <summary>Every saved attendance row for the quarter. Students with no row have 0 absences and 0 tardies.</summary>
    Task<List<AttendanceRecord>> GetForQuarterAsync(Quarter quarter);

    /// <summary>Absences and tardies summed across the given quarters, keyed by student id. Students with no rows are omitted.</summary>
    Task<Dictionary<int, (int Absences, int Tardies)>> GetTotalsAsync(IReadOnlyCollection<Quarter> quarters);

    /// <summary>Saves a student's absence and tardy totals for the quarter, replacing any previous values.</summary>
    Task SetAsync(int studentId, Quarter quarter, int absences, int tardies);
}
