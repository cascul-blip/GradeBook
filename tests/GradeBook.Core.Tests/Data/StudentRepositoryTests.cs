using GradeBook.Core.Data.Repositories;
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
    }

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
}
