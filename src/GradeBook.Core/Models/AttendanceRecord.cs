namespace GradeBook.Core.Models;

public sealed record AttendanceRecord(int StudentId, Quarter Quarter, int Absences, int Tardies);
