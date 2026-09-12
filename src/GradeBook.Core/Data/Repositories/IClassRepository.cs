using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public interface IClassRepository
{
    Task<List<SchoolClass>> GetAllAsync(bool includeInactive = false);
    Task<SchoolClass?> GetByIdAsync(int id);
    Task<int> AddAsync(string name);
    Task RenameAsync(int id, string name);
    Task SetActiveAsync(int id, bool isActive);

    /// <summary>
    /// Permanently deletes the class. Throws <see cref="InvalidOperationException"/> if it has any
    /// enrollments or assignments — deactivate instead of deleting a class with real data.
    /// </summary>
    Task DeleteAsync(int id);
}
