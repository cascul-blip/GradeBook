using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public interface IGradeRepository
{
    /// <summary>All grade records for a class across the given quarters — feeds the class report.</summary>
    Task<List<AssignmentGradeRecord>> GetRecordsForClassAsync(int classId, IReadOnlyCollection<Quarter> quarters);

    /// <summary>All grade records for a student (across every class they're in) for the given quarters — feeds the student report.</summary>
    Task<List<AssignmentGradeRecord>> GetRecordsForStudentAsync(int studentId, IReadOnlyCollection<Quarter> quarters);

    /// <summary>All grade records for one class+quarter — feeds the gradebook grid.</summary>
    Task<List<AssignmentGradeRecord>> GetRecordsForClassAndQuarterAsync(int classId, Quarter quarter);

    /// <summary>
    /// Updates the raw score. If the grade's current status is Uncompleted it auto-flips to Completed;
    /// Late/Excused/Completed statuses are left as-is so correcting a score doesn't silently clear a manual override.
    /// </summary>
    Task SetScoreAsync(int assignmentId, int studentId, decimal score);

    /// <summary>Manual status override (Completed/Late/Excused/Uncompleted) — never touches the stored score.</summary>
    Task SetStatusAsync(int assignmentId, int studentId, GradeStatus status);

    /// <summary>
    /// Inserts a Score=0/Uncompleted row for (assignmentId, studentId) if one doesn't already exist.
    /// Self-heals the case where a student enrolls after an assignment was already created.
    /// </summary>
    Task EnsureGradeRecordAsync(int assignmentId, int studentId);
}
