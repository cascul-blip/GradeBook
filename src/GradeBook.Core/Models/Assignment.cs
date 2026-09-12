namespace GradeBook.Core.Models;

public sealed class Assignment
{
    public int Id { get; init; }
    public required int ClassId { get; init; }
    public required Quarter Quarter { get; init; }
    public required string Name { get; set; }
    public required decimal PointsPossible { get; set; }
    public DateTime DateCreated { get; init; }
}
