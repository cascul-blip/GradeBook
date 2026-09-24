using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GradeBook.Core.Tests.Data;

public class GradeRepositoryTests : SqliteRepositoryTestBase
{
    private readonly StudentRepository _students;
    private readonly ClassRepository _classes;
    private readonly EnrollmentRepository _enrollments;
    private readonly AssignmentRepository _assignments;
    private readonly GradeRepository _grades;

    public GradeRepositoryTests()
    {
        _students = new StudentRepository(ConnectionFactory);
        _classes = new ClassRepository(ConnectionFactory);
        _enrollments = new EnrollmentRepository(ConnectionFactory);
        _assignments = new AssignmentRepository(ConnectionFactory);
        _grades = new GradeRepository(ConnectionFactory);
    }

    private void Execute(string sql)
    {
        using var connection = ConnectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private async Task<(int ClassId, int StudentId, int AssignmentId)> ClassWithOneGradeAsync()
    {
        var classId = await _classes.AddAsync("Math 87");
        var studentId = await _students.AddAsync("Micah");
        await _enrollments.EnrollAsync(studentId, classId);
        var assignmentId = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);
        return (classId, studentId, assignmentId);
    }

    private async Task<GradeStatus> StatusOfAsync(int classId, int studentId, int assignmentId) =>
        (await _grades.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1))
        .Single(r => r.StudentId == studentId && r.AssignmentId == assignmentId).Status;

    [Fact]
    public async Task SetScoreAsync_ZeroScore_DoesNotFlipUncompletedToCompleted()
    {
        var (classId, studentId, assignmentId) = await ClassWithOneGradeAsync();

        await _grades.SetScoreAsync(assignmentId, studentId, 0);

        Assert.Equal(GradeStatus.Uncompleted, await StatusOfAsync(classId, studentId, assignmentId));
    }

    [Fact]
    public async Task SetScoreAsync_NonZeroScore_FlipsUncompletedToCompleted()
    {
        var (classId, studentId, assignmentId) = await ClassWithOneGradeAsync();

        await _grades.SetScoreAsync(assignmentId, studentId, 1);

        Assert.Equal(GradeStatus.Completed, await StatusOfAsync(classId, studentId, assignmentId));
    }

    [Fact]
    public async Task SetScoreAsync_AboveMax_IsAllowedAsExtraCredit()
    {
        var (classId, studentId, assignmentId) = await ClassWithOneGradeAsync();

        await _grades.SetScoreAsync(assignmentId, studentId, 35);

        Assert.Equal(35, (await _grades.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1)).Single().Score);
    }

    [Fact]
    public async Task SetScoreAsync_Negative_Throws_AndLeavesScoreUnchanged()
    {
        var (classId, studentId, assignmentId) = await ClassWithOneGradeAsync();
        await _grades.SetScoreAsync(assignmentId, studentId, 20);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _grades.SetScoreAsync(assignmentId, studentId, -5));

        Assert.Equal(20, (await _grades.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1)).Single().Score);
    }

    [Fact]
    public async Task SetScoreAsync_Throws_WhenGradeRowDoesNotExist()
    {
        var (_, _, assignmentId) = await ClassWithOneGradeAsync();
        var outsiderId = await _students.AddAsync("Not Enrolled");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _grades.SetScoreAsync(assignmentId, outsiderId, 10));
    }

    [Fact]
    public async Task SetStatusAsync_Throws_WhenGradeRowDoesNotExist_OrStatusIsUnknown()
    {
        var (_, studentId, assignmentId) = await ClassWithOneGradeAsync();
        var outsiderId = await _students.AddAsync("Not Enrolled");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _grades.SetStatusAsync(assignmentId, outsiderId, GradeStatus.Late));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _grades.SetStatusAsync(assignmentId, studentId, (GradeStatus)7));
    }

    [Theory]
    [InlineData("UPDATE Grades SET Score = -1;")]
    [InlineData("UPDATE Grades SET Score = '-1';")]
    [InlineData("UPDATE Grades SET Status = 4;")]
    [InlineData("UPDATE Grades SET Status = -1;")]
    public async Task Triggers_RejectInvalidGradesWrittenDirectly(string sql)
    {
        await ClassWithOneGradeAsync();

        Assert.Throws<SqliteException>(() => Execute(sql));
    }

    [Fact]
    public async Task Triggers_RejectInvalidGradeOnInsert()
    {
        var (_, _, assignmentId) = await ClassWithOneGradeAsync();
        var otherId = await _students.AddAsync("Prentiss");

        Assert.Throws<SqliteException>(() =>
            Execute($"INSERT INTO Grades (AssignmentId, StudentId, Score, Status) VALUES ({assignmentId}, {otherId}, 0, 9);"));
    }

    [Fact]
    public async Task EnsureGradeRecords_ExcusesLessonsAssignedBeforeEnrollment_AndLeavesLaterOnesUncompleted()
    {
        var classId = await _classes.AddAsync("Math 87");
        var before = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 1", 30);
        var after = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 2", 30);
        var lateJoinerId = await _students.AddAsync("LateJoiner");
        await _enrollments.EnrollAsync(lateJoinerId, classId);
        Execute($"UPDATE Assignments SET DateCreated = '2026-09-01 08:00:00' WHERE Id = {before};");
        Execute("UPDATE Enrollments SET EnrolledDate = '2026-09-10 08:00:00';");
        Execute($"UPDATE Assignments SET DateCreated = '2026-09-15 08:00:00' WHERE Id = {after};");

        await _grades.EnsureGradeRecordsForClassAndQuarterAsync(classId, Quarter.Q1);

        var records = await _grades.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1);
        Assert.Equal(GradeStatus.Excused, records.Single(r => r.AssignmentId == before).Status);
        Assert.Equal(GradeStatus.Uncompleted, records.Single(r => r.AssignmentId == after).Status);
    }

    [Fact]
    public async Task EnsureGradeRecords_ExcusesLessonsFromAnUnenrolledGap_AndNeverTouchesExistingGrades()
    {
        var classId = await _classes.AddAsync("Math 87");
        var studentId = await _students.AddAsync("Cohen");
        await _enrollments.EnrollAsync(studentId, classId);
        var early = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 1", 30);
        await _grades.SetScoreAsync(early, studentId, 25);
        await _enrollments.UnenrollAsync(studentId, classId);
        var gap = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 2", 30);
        await _enrollments.EnrollAsync(studentId, classId);
        Execute($"UPDATE Assignments SET DateCreated = '2026-09-01 08:00:00' WHERE Id = {early};");
        Execute($"UPDATE Assignments SET DateCreated = '2026-09-10 08:00:00' WHERE Id = {gap};");
        Execute("UPDATE Enrollments SET EnrolledDate = '2026-09-20 08:00:00' WHERE IsActive = 1;");

        await _grades.EnsureGradeRecordsForClassAndQuarterAsync(classId, Quarter.Q1);

        var records = await _grades.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1);
        var earlyRecord = records.Single(r => r.AssignmentId == early);
        Assert.Equal(25, earlyRecord.Score);
        Assert.Equal(GradeStatus.Completed, earlyRecord.Status);
        Assert.Equal(GradeStatus.Excused, records.Single(r => r.AssignmentId == gap).Status);
    }

    [Fact]
    public async Task CountScoresAboveAsync_CountsOnlyScoresAboveTheGivenPoints()
    {
        var classId = await _classes.AddAsync("Math 87");
        var a = await _students.AddAsync("A");
        var b = await _students.AddAsync("B");
        var c = await _students.AddAsync("C");
        foreach (var id in new[] { a, b, c })
        {
            await _enrollments.EnrollAsync(id, classId);
        }

        var assignmentId = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 3", 30);
        await _grades.SetScoreAsync(assignmentId, a, 30);
        await _grades.SetScoreAsync(assignmentId, b, 20);
        await _grades.SetScoreAsync(assignmentId, c, 21);

        Assert.Equal(2, await _grades.CountScoresAboveAsync(assignmentId, 20));
        Assert.Equal(0, await _grades.CountScoresAboveAsync(assignmentId, 30));
    }

    [Fact]
    public async Task FindPreEnrollmentUncompleted_FindsOnlyUntouchedGradesFromBeforeEnrollment_AndExcuseFixesThem()
    {
        var classId = await _classes.AddAsync("Math 87");
        var onTimeId = await _students.AddAsync("OnTime");
        await _enrollments.EnrollAsync(onTimeId, classId);
        var lesson1 = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 1", 30);
        var lesson2 = await _assignments.CreateAssignmentWithGradesAsync(classId, Quarter.Q1, "Lesson 2", 30);
        var lateId = await _students.AddAsync("Late");
        await _enrollments.EnrollAsync(lateId, classId);
        // Simulate what older versions did: plain Uncompleted rows for the late joiner.
        Execute($"INSERT INTO Grades (AssignmentId, StudentId) VALUES ({lesson1}, {lateId}), ({lesson2}, {lateId});");
        Execute("UPDATE Assignments SET DateCreated = '2026-09-02 08:00:00';");
        Execute($"UPDATE Enrollments SET EnrolledDate = '2026-09-01 08:00:00' WHERE StudentId = {onTimeId};");
        Execute($"UPDATE Enrollments SET EnrolledDate = '2026-09-10 08:00:00' WHERE StudentId = {lateId};");
        await _grades.SetScoreAsync(lesson2, lateId, 12); // made up — must not be touched

        var found = await _grades.FindPreEnrollmentUncompletedAsync();

        var only = Assert.Single(found);
        Assert.Equal("Late", only.StudentName);
        Assert.Equal("Lesson 1", only.AssignmentName);

        await _grades.ExcuseGradesAsync(found.Select(f => f.GradeId).ToList());

        var records = await _grades.GetRecordsForClassAndQuarterAsync(classId, Quarter.Q1);
        Assert.Equal(GradeStatus.Excused, records.Single(r => r.StudentId == lateId && r.AssignmentId == lesson1).Status);
        Assert.Equal(12, records.Single(r => r.StudentId == lateId && r.AssignmentId == lesson2).Score);
        Assert.All(records.Where(r => r.StudentId == onTimeId), r => Assert.Equal(GradeStatus.Uncompleted, r.Status));
        Assert.Empty(await _grades.FindPreEnrollmentUncompletedAsync());
    }
}
