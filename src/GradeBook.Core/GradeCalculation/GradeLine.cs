using GradeBook.Core.Models;

namespace GradeBook.Core.GradeCalculation;

public readonly record struct GradeLine(decimal Score, decimal PointsPossible, GradeStatus Status);
