using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;
using GradeBook.Core.Reporting;
using Xunit;

namespace GradeBook.Core.Tests.Data;

public class StudentRepositoryTests : SqliteRepositoryTestBase
{
    private readonly StudentRepository _students;
    private readonly ClassRepository _classes;
    private readonly EnrollmentRepository _enrollments;

    public StudentRepositoryTests()
    {
        _students = new StudentRepository(ConnectionFactory);
        _classes = new ClassRepository(ConnectionFactory);
        _enrollments = new EnrollmentRepository(ConnectionFactory);
        _assignments = new AssignmentRepository(ConnectionFactory);
        _grades = new GradeRepository(ConnectionFactory);
    }

    private readonly AssignmentRepository _assignments;
    private readonly GradeRepository _grades;

    [Fact]
    public async Task GetActiveInClassAsync_KeepsDeactivatedEnrolledStudents_AndSkipsUnenrolledAndOtherClasses()
    {
        var classId = await _classes.AddAsync("Math 87");
        var otherClassId = await _classes.AddAsync("Math 76");
        var enrolled = await _students.AddAsync("Enrolled");
        var deactivated = await _students.AddAsync("Deactivated");
        var unenrolled = await _students.AddAsync("Unenrolled");
        var elsewhere = await _students.AddAsync("Elsewhere");
        await _enrollments.EnrollAsync(enrolled, classId);
        await _enrollments.EnrollAsync(deactivated, classId);
        await _students.SetActiveAsync(deactivated, false);
        await _enrollments.EnrollAsync(unenrolled, classId);
        await _enrollments.UnenrollAsync(unenrolled, classId);
        await _enrollments.EnrollAsync(elsewhere, otherClassId);

        var students = await _students.GetActiveInClassAsync(classId);

        Assert.Equal(new[] { enrolled, deactivated }.Order(), students.Select(s => s.Id).Order());
    }

    [Fact]
    public async Task GetActiveInClassAsync_ReenrolledStudent_AppearsOnce()
    {
        var classId = await _classes.AddAsync("Math 87");
        var studentId = await _students.AddAsync("Cohen");
        await _enrollments.EnrollAsync(studentId, classId);
        await _enrollments.UnenrollAsync(studentId, classId);
        await _enrollments.EnrollAsync(studentId, classId);

        var students = await _students.GetActiveInClassAsync(classId);

        Assert.Equal(studentId, Assert.Single(students).Id);
    }

    [Fact]
    public async Task DeactivateAsync_UnenrollsFromEveryClass_AndKeepsGrades()
    {
        var math = await _classes.AddAsync("Math 87");
        var science = await _classes.AddAsync("Science");
        var studentId = await _students.AddAsync("Micah");
        await _enrollments.EnrollAsync(studentId, math);
        await _enrollments.EnrollAsync(studentId, science);
        var lesson = await _assignments.CreateAssignmentWithGradesAsync(math, Quarter.Q1, "Lesson 1", 30);
        await _grades.SetScoreAsync(lesson, studentId, 25);

        await _students.DeactivateAsync(studentId);

        Assert.False((await _students.GetByIdAsync(studentId))!.IsActive);
        Assert.Empty(await _enrollments.GetActiveClassIdsForStudentAsync(studentId));
        var record = Assert.Single(await _grades.GetRecordsForStudentAsync(studentId, [Quarter.Q1]));
        Assert.Equal(25, record.Score);
        Assert.Equal(GradeStatus.Completed, record.Status);
    }

    [Fact]
    public async Task DeactivateAsync_StudentGetsNoNewLessons()
    {
        var classId = await _classes.AddAsync("Math 87");
        var leaver = await _students.AddAsync("Leaver");
        var stayer = await _students.AddAsync("Stayer");
        await _enrollments.EnrollAsync(leaver, classId);
        await _enrollments.EnrollAsync(stayer, classId);

        await _students.DeactivateAsync(leaver);
        await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 1", 30);
        await _grades.EnsureGradeRecordsForClassAndQuarterAsync(classId, Quarter.Q1);

        var records = await _grades.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1);
        Assert.Equal(stayer, Assert.Single(records).StudentId);
    }

    [Fact]
    public async Task SummaryReport_IncludesDeactivatedStudentForPeriodsWithGrades_FlaggedInactive()
    {
        var attendance = new AttendanceRepository(ConnectionFactory);
        var studentReports = new StudentReportService(_students, _classes, _enrollments, _grades);
        var summary = new SummaryReportService(_students, studentReports, attendance);

        var classId = await _classes.AddAsync("Math 87");
        var leaver = await _students.AddAsync("Leaver");
        var stayer = await _students.AddAsync("Stayer");
        await _enrollments.EnrollAsync(leaver, classId);
        await _enrollments.EnrollAsync(stayer, classId);
        var lesson = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 1", 30);
        await _grades.SetScoreAsync(lesson, leaver, 27);
        await attendance.SetAsync(leaver, Quarter.Q1, 2, 1);

        await _students.DeactivateAsync(leaver);

        var q1 = await summary.BuildReportAsync(ReportPeriod.Quarter1);
        var leaverQ1 = q1.Students.Single(s => s.StudentName == "Leaver");
        Assert.False(leaverQ1.IsActive);
        Assert.Equal(90m, Assert.Single(leaverQ1.Classes).Percentage);
        Assert.Equal((2, 1), (leaverQ1.Absences, leaverQ1.Tardies));
        Assert.True(q1.Students.Single(s => s.StudentName == "Stayer").IsActive);

        // No grades after they left, so they aren't listed for a later quarter.
        var q2 = await summary.BuildReportAsync(ReportPeriod.Quarter2);
        Assert.DoesNotContain(q2.Students, s => s.StudentName == "Leaver");
    }
}
