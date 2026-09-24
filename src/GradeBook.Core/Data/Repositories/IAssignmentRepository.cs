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

    /// <summary>Renames the assignment, changes its point value, and/or moves it to another quarter. Does not touch any Grade rows.</summary>
    Task UpdateAsync(int id, string name, decimal pointsPossible, Quarter quarter);

    /// <summary>Deletes the assignment and, via ON DELETE CASCADE, every grade recorded against it.</summary>
    Task DeleteAsync(int id);
}
