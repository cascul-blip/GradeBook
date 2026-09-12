namespace GradeBook.Core.Data.Repositories;

public interface IEnrollmentRepository
{
    Task<List<int>> GetActiveStudentIdsForClassAsync(int classId);
    Task<List<int>> GetActiveClassIdsForStudentAsync(int studentId);
    Task<bool> IsActivelyEnrolledAsync(int studentId, int classId);
    Task EnrollAsync(int studentId, int classId);
    Task UnenrollAsync(int studentId, int classId);
}
