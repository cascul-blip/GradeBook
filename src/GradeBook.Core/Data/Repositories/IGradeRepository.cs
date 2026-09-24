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
    /// Updates the raw score. If the grade's current status is Uncompleted and the score is above 0 it
    /// auto-flips to Completed; Late/Excused/Completed statuses are left as-is so correcting a score
    /// doesn't silently clear a manual override. Throws on a negative score or if the grade row doesn't exist.
    /// </summary>
    Task SetScoreAsync(int assignmentId, int studentId, decimal score);

    /// <summary>Manual status override (Completed/Late/Excused/Uncompleted) — never touches the stored score. Throws if the grade row doesn't exist.</summary>
    Task SetStatusAsync(int assignmentId, int studentId, GradeStatus status);

    /// <summary>
    /// Creates any missing grade rows for the class+quarter's currently enrolled students. Self-heals the
    /// case where a student enrolls after assignments already exist: lessons assigned before their
    /// (current) enrollment date are created Excused so they don't count against them; later ones are
    /// created Uncompleted. Existing rows are never touched.
    /// </summary>
    Task EnsureGradeRecordsForClassAndQuarterAsync(int classId, Quarter quarter);

    /// <summary>How many students scored above the given point value on this assignment.</summary>
    Task<int> CountScoresAboveAsync(int assignmentId, decimal points);

    /// <summary>
    /// Untouched Uncompleted/0 grades for lessons assigned while the student wasn't enrolled in the class —
    /// left behind by older versions, which filled these in as Uncompleted rather than Excused.
    /// </summary>
    Task<List<PreEnrollmentGrade>> FindPreEnrollmentUncompletedAsync();

    /// <summary>Marks the given grades Excused, skipping any that have since been given a score or status.</summary>
    Task ExcuseGradesAsync(IReadOnlyCollection<int> gradeIds);
}
