using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;
using Xunit;

namespace GradeBook.Core.Tests.Data;

public class AssignmentRepositoryTests : SqliteRepositoryTestBase
{
    [Fact]
    public async Task CreateAssignmentWithGradesAsync_PopulatesGradeForEveryActivelyEnrolledStudent_ButNotUnenrolled()
    {
        var studentRepo = new StudentRepository(ConnectionFactory);
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);
        var gradeRepo = new GradeRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Math 87");
        var micahId = await studentRepo.AddAsync("Micah");
        var prentissId = await studentRepo.AddAsync("Prentiss");
        var cohenId = await studentRepo.AddAsync("Cohen");

        await enrollmentRepo.EnrollAsync(micahId, classId);
        await enrollmentRepo.EnrollAsync(prentissId, classId);
        await enrollmentRepo.EnrollAsync(cohenId, classId);
        await enrollmentRepo.UnenrollAsync(cohenId, classId); // Cohen leaves before the assignment is created

        var assignmentId = await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);

        var records = await gradeRepo.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1);

        Assert.Equal(2, records.Count); // only the two actively-enrolled students
        Assert.Contains(records, r => r.StudentId == micahId && r.AssignmentId == assignmentId && r.Score == 0 && r.Status == GradeStatus.Uncompleted);
        Assert.Contains(records, r => r.StudentId == prentissId);
        Assert.DoesNotContain(records, r => r.StudentId == cohenId);
    }

    [Fact]
    public async Task SetScoreAsync_AutoFlipsUncompletedToCompleted_ButLeavesManualLateOverrideAlone()
    {
        var studentRepo = new StudentRepository(ConnectionFactory);
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);
        var gradeRepo = new GradeRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Math 87");
        var studentId = await studentRepo.AddAsync("Baleigh");
        await enrollmentRepo.EnrollAsync(studentId, classId);
        var assignmentId = await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);

        await gradeRepo.SetScoreAsync(assignmentId, studentId, 27);
        var afterFirstScore = (await gradeRepo.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1)).Single();
        Assert.Equal(GradeStatus.Completed, afterFirstScore.Status);

        await gradeRepo.SetStatusAsync(assignmentId, studentId, GradeStatus.Late);
        await gradeRepo.SetScoreAsync(assignmentId, studentId, 29); // correcting a typo shouldn't clear the manual Late override
        var afterCorrection = (await gradeRepo.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1)).Single();

        Assert.Equal(GradeStatus.Late, afterCorrection.Status);
        Assert.Equal(29, afterCorrection.Score);
    }

    [Fact]
    public async Task UpdateAsync_RenamesAndRepointsAssignment_WithoutTouchingExistingGrades()
    {
        var studentRepo = new StudentRepository(ConnectionFactory);
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);
        var gradeRepo = new GradeRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Math 87");
        var studentId = await studentRepo.AddAsync("Micah");
        await enrollmentRepo.EnrollAsync(studentId, classId);
        var assignmentId = await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);
        await gradeRepo.SetScoreAsync(assignmentId, studentId, 27);

        await assignmentRepo.UpdateAsync(assignmentId, "Lesson 3 (Revised)", 25, Quarter.Q1);

        var updated = await assignmentRepo.GetByIdAsync(assignmentId);
        Assert.Equal("Lesson 3 (Revised)", updated!.Name);
        Assert.Equal(25, updated.PointsPossible);

        var record = (await gradeRepo.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1)).Single();
        Assert.Equal(27, record.Score); // the existing grade itself is untouched by an edit to the assignment
    }

    [Fact]
    public async Task UpdateAsync_MovesAssignmentAndItsGradesToAnotherQuarter()
    {
        var studentRepo = new StudentRepository(ConnectionFactory);
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);
        var gradeRepo = new GradeRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Math 87");
        var studentId = await studentRepo.AddAsync("Micah");
        await enrollmentRepo.EnrollAsync(studentId, classId);
        var assignmentId = await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);
        await gradeRepo.SetScoreAsync(assignmentId, studentId, 27);

        await assignmentRepo.UpdateAsync(assignmentId, "Lesson 3", 30, Quarter.Q2);

        Assert.Empty(await gradeRepo.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1));
        var moved = (await gradeRepo.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q2)).Single();
        Assert.Equal(27, moved.Score);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenAssignmentNoLongerExists()
    {
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => assignmentRepo.UpdateAsync(999, "Ghost", 10, Quarter.Q1));
    }

    [Fact]
    public async Task DeleteAsync_Assignment_CascadesToItsGrades()
    {
        var studentRepo = new StudentRepository(ConnectionFactory);
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);
        var gradeRepo = new GradeRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Math 87");
        var studentId = await studentRepo.AddAsync("Micah");
        await enrollmentRepo.EnrollAsync(studentId, classId);
        var assignmentId = await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);

        await assignmentRepo.DeleteAsync(assignmentId);

        Assert.Null(await assignmentRepo.GetByIdAsync(assignmentId));
        Assert.Empty(await gradeRepo.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1));
    }

    [Fact]
    public async Task GetForClassAndQuarterAsync_ReturnsNewestAssignmentFirst()
    {
        var classRepo = new ClassRepository(ConnectionFactory);
        var assignmentRepo = new AssignmentRepository(ConnectionFactory);

        var classId = await classRepo.AddAsync("Math 87");
        var firstId = await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);
        var secondId = await assignmentRepo.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 4", 20);

        var assignments = await assignmentRepo.GetForClassAndQuarterAsync(classId, Quarter.Q1);

        // Newest lesson (Lesson 4, created second) should come first so it renders as the leftmost column.
        Assert.Equal(secondId, assignments[0].Id);
        Assert.Equal(firstId, assignments[1].Id);
    }
}
