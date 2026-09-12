using GradeBook.Core.Models;

namespace GradeBook.Core.Data;

/// <summary>Flat join of Grades+Assignments, the shape both report services and the gradebook grid consume.</summary>
public readonly record struct AssignmentGradeRecord(
    int StudentId,
    int ClassId,
    int AssignmentId,
    string AssignmentName,
    Quarter Quarter,
    decimal Score,
    decimal PointsPossible,
    GradeStatus Status);
