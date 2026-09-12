using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public interface IStudentRepository
{
    Task<List<Student>> GetAllAsync(bool includeInactive = false);
    Task<Student?> GetByIdAsync(int id);
    Task<int> AddAsync(string name);
    Task RenameAsync(int id, string name);
    Task SetActiveAsync(int id, bool isActive);

    /// <summary>
    /// Permanently deletes the student. Throws <see cref="InvalidOperationException"/> if they have any
    /// enrollment or grade history — deactivate instead of deleting a student with real data.
    /// </summary>
    Task DeleteAsync(int id);
}
