namespace GradeBook.Core.Models;

public sealed class Enrollment
{
    public int Id { get; init; }
    public required int StudentId { get; init; }
    public required int ClassId { get; init; }
    public bool IsActive { get; set; } = true;
    public DateTime EnrolledDate { get; init; }
    public DateTime? UnenrolledDate { get; set; }
}
