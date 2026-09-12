using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public interface IClassRepository
{
    Task<List<SchoolClass>> GetAllAsync(bool includeInactive = false);
    Task<SchoolClass?> GetByIdAsync(int id);
    Task<int> AddAsync(string name);
    Task RenameAsync(int id, string name);
    Task SetActiveAsync(int id, bool isActive);
}
