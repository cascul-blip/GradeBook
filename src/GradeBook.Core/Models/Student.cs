namespace GradeBook.Core.Models;

public sealed class Student
{
    public int Id { get; init; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}
