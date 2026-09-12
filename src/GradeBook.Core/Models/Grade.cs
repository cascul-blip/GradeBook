namespace GradeBook.Core.Models;

public sealed class Grade
{
    public int Id { get; init; }
    public required int AssignmentId { get; init; }
    public required int StudentId { get; init; }
    public decimal Score { get; set; }
    public GradeStatus Status { get; set; } = GradeStatus.Uncompleted;
}
