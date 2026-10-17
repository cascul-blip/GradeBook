using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public interface IStudentRepository
{
    Task<List<Student>> GetAllAsync(bool includeInactive = false);

    /// <summary>Students actively enrolled in the class, including deactivated students who are still enrolled.</summary>
    Task<List<Student>> GetActiveInClassAsync(int classId);

    Task<Student?> GetByIdAsync(int id);
    Task<int> AddAsync(string name);
    Task RenameAsync(int id, string name);
    Task SetActiveAsync(int id, bool isActive);

    /// <summary>
    /// Permanently deletes the student. Throws <see cref="InvalidOperationException"/> if they have any
    /// grade history or non-zero attendance — deactivate instead of deleting a student with real data.
    /// </summary>
    Task DeleteAsync(int id);
}
