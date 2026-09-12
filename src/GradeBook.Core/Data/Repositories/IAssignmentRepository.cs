using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public interface IAssignmentRepository
{
    Task<List<Assignment>> GetForClassAndQuarterAsync(int classId, Quarter quarter);
    Task<Assignment?> GetByIdAsync(int id);

    /// <summary>
    /// Inserts the Assignment row and, in the same transaction, a Score=0/Uncompleted Grade row for
    /// every currently actively-enrolled student in the class. Returns the new assignment's id.
    /// </summary>
    Task<int> CreateAssignmentWithGradesAsync(int classId, Quarter quarter, string name, decimal pointsPossible);

    Task DeleteAsync(int id);
}
