using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;
using Xunit;

namespace GradeBook.Core.Tests.Data;

public class DeleteProtectionTests : SqliteRepositoryTestBase
{
    [Fact]
    public async Task DeleteAsync_Student_SucceedsWhenNoHistoryExists()
    {
        var studentRepo = new StudentRepository(ConnectionFactory);
        var studentId = await studentRepo.AddAsync("Test Student");

        await studentRepo.DeleteAsync(studentId);

        Assert.Null(await studentRepo.GetByIdAsync(studentId));
    }

    [Fact]
    public async Task DeleteAsync_Student_Succeeds_WhenOnlyEnrolledButNoGradesExist()
    {
        // Enrollment alone (in a class with zero assignments, so zero grades) is not "history" —
        // it should be cleaned up as part of the delete, not treated as a reason to block it.
        var studentRepo = new StudentRepository(ConnectionFactory);
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);

        var studentId = await studentRepo.AddAsync("Micah");
        var classId = await classRepo.AddAsync("Math 87");
        await enrollmentRepo.EnrollAsync(studentId, classId);

        await studentRepo.DeleteAsync(studentId);

        Assert.Null(await studentRepo.GetByIdAsync(studentId));
    }

    [Fact]
    public async Task DeleteAsync_Student_ThrowsFriendlyError_WhenGradeHistoryExists()
    {
        var studentRepo = new StudentRepository(ConnectionFactory);
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);

        var studentId = await studentRepo.AddAsync("Micah");
        var classId = await classRepo.AddAsync("Math 87");
        await enrollmentRepo.EnrollAsync(studentId, classId);
        await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => studentRepo.DeleteAsync(studentId));
        Assert.Contains("Deactivate", ex.Message);

        // The student must still exist — the delete should have been rejected, not partially applied.
        Assert.NotNull(await studentRepo.GetByIdAsync(studentId));
    }

    [Fact]
    public async Task DeleteAsync_Class_SucceedsWhenNoHistoryExists()
    {
        var classRepo = new ClassRepository(ConnectionFactory);
        var classId = await classRepo.AddAsync("Test Class");

        await classRepo.DeleteAsync(classId);

        Assert.Null(await classRepo.GetByIdAsync(classId));
    }

    [Fact]
    public async Task DeleteAsync_Class_Succeeds_WhenOnlyEnrollmentsExist_NoAssignments()
    {
        // A class with students enrolled but no assignments ever created has no grade data behind it —
        // deleting it should clean up those enrollments rather than being blocked.
        var classRepo = new ClassRepository(ConnectionFactory);
        var studentRepo = new StudentRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Test");
        var studentId = await studentRepo.AddAsync("Micah");
        await enrollmentRepo.EnrollAsync(studentId, classId);

        await classRepo.DeleteAsync(classId);

        Assert.Null(await classRepo.GetByIdAsync(classId));
    }

    [Fact]
    public async Task DeleteAsync_Class_ThrowsFriendlyError_WhenAssignmentsExist()
    {
        var classRepo = new ClassRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Math 87");
        await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => classRepo.DeleteAsync(classId));
        Assert.Contains("Deactivate", ex.Message);
        Assert.NotNull(await classRepo.GetByIdAsync(classId));
    }
}
