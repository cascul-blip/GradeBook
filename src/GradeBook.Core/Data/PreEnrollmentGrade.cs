namespace GradeBook.Core.Data;

/// <summary>An Uncompleted, zero-score grade for a lesson that was assigned while the student wasn't enrolled in the class.</summary>
public sealed record PreEnrollmentGrade(int GradeId, string StudentName, string ClassName, string AssignmentName, DateTime AssignmentDate);
