using GradeBook.Core.Data;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;
using GradeBook.Core.Reporting;

namespace GradeBook.Core.Tests.Data;

public class AttendanceRepositoryTests : SqliteRepositoryTestBase
{
    private readonly StudentRepository _studentRepo;
    private readonly AttendanceRepository _attendanceRepo;

    public AttendanceRepositoryTests()
    {
        _studentRepo = new StudentRepository(ConnectionFactory);
        _attendanceRepo = new AttendanceRepository(ConnectionFactory);
    }

    [Fact]
    public async Task SetAsync_ThenGetForQuarter_ReturnsSavedCounts()
    {
        var studentId = await _studentRepo.AddAsync("Micah");

        await _attendanceRepo.SetAsync(studentId, Quarter.Q1, 3, 2);

        var record = Assert.Single(await _attendanceRepo.GetForQuarterAsync(Quarter.Q1));
        Assert.Equal(new AttendanceRecord(studentId, Quarter.Q1, 3, 2), record);
        Assert.Empty(await _attendanceRepo.GetForQuarterAsync(Quarter.Q2));
    }

    [Fact]
    public async Task SetAsync_Again_OverwritesPreviousCounts()
    {
        var studentId = await _studentRepo.AddAsync("Micah");

        await _attendanceRepo.SetAsync(studentId, Quarter.Q1, 3, 2);
        await _attendanceRepo.SetAsync(studentId, Quarter.Q1, 4, 0);

        var record = Assert.Single(await _attendanceRepo.GetForQuarterAsync(Quarter.Q1));
        Assert.Equal(4, record.Absences);
        Assert.Equal(0, record.Tardies);
    }

    [Fact]
    public async Task SetAsync_RejectsNegativeCounts()
    {
        var studentId = await _studentRepo.AddAsync("Micah");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _attendanceRepo.SetAsync(studentId, Quarter.Q1, -1, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _attendanceRepo.SetAsync(studentId, Quarter.Q1, 0, -1));
        Assert.Empty(await _attendanceRepo.GetForQuarterAsync(Quarter.Q1));
    }

    [Fact]
    public async Task GetTotalsAsync_SumsOnlyRequestedQuarters()
    {
        var micah = await _studentRepo.AddAsync("Micah");
        var cohen = await _studentRepo.AddAsync("Cohen");
        await _attendanceRepo.SetAsync(micah, Quarter.Q1, 1, 2);
        await _attendanceRepo.SetAsync(micah, Quarter.Q2, 3, 4);
        await _attendanceRepo.SetAsync(micah, Quarter.Q3, 50, 50);
        await _attendanceRepo.SetAsync(cohen, Quarter.Q3, 1, 1);

        var totals = await _attendanceRepo.GetTotalsAsync([Quarter.Q1, Quarter.Q2]);

        Assert.Equal((4, 6), totals[micah]);
        Assert.False(totals.ContainsKey(cohen));
    }

    [Fact]
    public async Task SummaryReport_ShowsAttendanceSummedOverPeriod()
    {
        var classRepo = new ClassRepository(ConnectionFactory);
        var enrollmentRepo = new EnrollmentRepository(ConnectionFactory);
        var gradeRepo = new GradeRepository(ConnectionFactory);
        var studentReportService = new StudentReportService(_studentRepo, classRepo, enrollmentRepo, gradeRepo);
        var summaryService = new SummaryReportService(_studentRepo, studentReportService, _attendanceRepo);

        var classId = await classRepo.AddAsync("Math 87");
        var micah = await _studentRepo.AddAsync("Micah");
        var cohen = await _studentRepo.AddAsync("Cohen");
        await enrollmentRepo.EnrollAsync(micah, classId);
        await enrollmentRepo.EnrollAsync(cohen, classId);
        await _attendanceRepo.SetAsync(micah, Quarter.Q1, 1, 2);
        await _attendanceRepo.SetAsync(micah, Quarter.Q2, 3, 0);
        await _attendanceRepo.SetAsync(micah, Quarter.Q3, 9, 9);

        var semester = await summaryService.BuildReportAsync(ReportPeriod.Semester1);
        var quarter2 = await summaryService.BuildReportAsync(ReportPeriod.Quarter2);

        var micahSemester = semester.Students.Single(s => s.StudentName == "Micah");
        Assert.Equal((4, 2), (micahSemester.Absences, micahSemester.Tardies));
        var cohenSemester = semester.Students.Single(s => s.StudentName == "Cohen");
        Assert.Equal((0, 0), (cohenSemester.Absences, cohenSemester.Tardies));
        var micahQ2 = quarter2.Students.Single(s => s.StudentName == "Micah");
        Assert.Equal((3, 0), (micahQ2.Absences, micahQ2.Tardies));
    }

    [Fact]
    public async Task Initialize_OnExistingDatabase_KeepsAttendance()
    {
        var studentId = await _studentRepo.AddAsync("Micah");
        await _attendanceRepo.SetAsync(studentId, Quarter.Q1, 3, 2);

        DatabaseInitializer.Initialize(ConnectionFactory);

        Assert.Single(await _attendanceRepo.GetForQuarterAsync(Quarter.Q1));
    }
}
