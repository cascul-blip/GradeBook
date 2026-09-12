namespace GradeBook.Core.Models;

public sealed class SchoolClass
{
    public int Id { get; init; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}
