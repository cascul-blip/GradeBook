using GradeBook.Core.Models;
using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data.Repositories;

public sealed class GradeRepository(SqliteConnectionFactory connectionFactory) : IGradeRepository
{
    private const string SelectRecordsSql = """
        SELECT g.StudentId, a.ClassId, g.AssignmentId, a.Name, a.DateCreated, a.Quarter, g.Score, a.PointsPossible, g.Status
        FROM Grades g
        JOIN Assignments a ON a.Id = g.AssignmentId
        """;

    public async Task<List<AssignmentGradeRecord>> GetRecordsForClassAsync(int classId, IReadOnlyCollection<Quarter> quarters)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        var quarterList = BuildQuarterInClause(command, quarters);
        command.CommandText = $"{SelectRecordsSql} WHERE a.ClassId = $classId AND a.Quarter IN ({quarterList});";
        command.Parameters.AddWithValue("$classId", classId);

        return await ReadRecordsAsync(command);
    }

    public async Task<List<AssignmentGradeRecord>> GetRecordsForStudentAsync(int studentId, IReadOnlyCollection<Quarter> quarters)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        var quarterList = BuildQuarterInClause(command, quarters);
        command.CommandText = $"{SelectRecordsSql} WHERE g.StudentId = $studentId AND a.Quarter IN ({quarterList});";
        command.Parameters.AddWithValue("$studentId", studentId);

        return await ReadRecordsAsync(command);
    }

    public async Task<List<AssignmentGradeRecord>> GetRecordsForClassAndQuarterAsync(int classId, Quarter quarter)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"{SelectRecordsSql} WHERE a.ClassId = $classId AND a.Quarter = $quarter;";
        command.Parameters.AddWithValue("$classId", classId);
        command.Parameters.AddWithValue("$quarter", (int)quarter);

        return await ReadRecordsAsync(command);
    }

    public async Task SetScoreAsync(int assignmentId, int studentId, decimal score)
    {
        if (score < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "A score can't be negative.");
        }

        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        // Auto-flip Uncompleted (0) -> Completed (1) only for a real (non-zero) score, so typing a 0 or
        // clearing the box doesn't quietly drop the lesson off the missing list. Any other status
        // (Late/Excused/Completed) is left untouched. The flip decision is made here rather than by
        // comparing $score in SQL: decimals bind as TEXT, and TEXT always compares greater than a number.
        command.CommandText = """
            UPDATE Grades
            SET Score = $score,
                Status = CASE WHEN Status = 0 AND $autoComplete = 1 THEN 1 ELSE Status END
            WHERE AssignmentId = $assignmentId AND StudentId = $studentId;
            """;
        command.Parameters.AddWithValue("$score", score);
        command.Parameters.AddWithValue("$autoComplete", score > 0 ? 1 : 0);
        command.Parameters.AddWithValue("$assignmentId", assignmentId);
        command.Parameters.AddWithValue("$studentId", studentId);
        EnsureExactlyOneRow(await command.ExecuteNonQueryAsync(), assignmentId, studentId);
    }

    public async Task SetStatusAsync(int assignmentId, int studentId, GradeStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown grade status.");
        }

        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Grades SET Status = $status WHERE AssignmentId = $assignmentId AND StudentId = $studentId;";
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$assignmentId", assignmentId);
        command.Parameters.AddWithValue("$studentId", studentId);
        EnsureExactlyOneRow(await command.ExecuteNonQueryAsync(), assignmentId, studentId);
    }

    public async Task EnsureGradeRecordsForClassAndQuarterAsync(int classId, Quarter quarter)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        // Both dates are datetime('now') UTC text in the same format, so a text comparison orders them correctly.
        command.CommandText = """
            INSERT OR IGNORE INTO Grades (AssignmentId, StudentId, Score, Status)
            SELECT a.Id, e.StudentId, 0, CASE WHEN a.DateCreated < e.EnrolledDate THEN 3 ELSE 0 END
            FROM Assignments a
            JOIN Enrollments e ON e.ClassId = a.ClassId AND e.IsActive = 1
            WHERE a.ClassId = $classId AND a.Quarter = $quarter;
            """;
        command.Parameters.AddWithValue("$classId", classId);
        command.Parameters.AddWithValue("$quarter", (int)quarter);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CountScoresAboveAsync(int assignmentId, decimal points)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Grades WHERE AssignmentId = $assignmentId AND Score > CAST($points AS REAL);";
        command.Parameters.AddWithValue("$assignmentId", assignmentId);
        command.Parameters.AddWithValue("$points", points);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<List<PreEnrollmentGrade>> FindPreEnrollmentUncompletedAsync()
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.Id, s.Name, c.Name, a.Name, a.DateCreated
            FROM Grades g
            JOIN Assignments a ON a.Id = g.AssignmentId
            JOIN Students s ON s.Id = g.StudentId
            JOIN Classes c ON c.Id = a.ClassId
            WHERE g.Status = 0 AND g.Score = 0
              AND NOT EXISTS (
                  SELECT 1 FROM Enrollments e
                  WHERE e.StudentId = g.StudentId AND e.ClassId = a.ClassId
                    AND e.EnrolledDate <= a.DateCreated
                    AND (e.UnenrolledDate IS NULL OR e.UnenrolledDate > a.DateCreated))
            ORDER BY c.Name, s.Name, a.DateCreated;
            """;

        var grades = new List<PreEnrollmentGrade>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            grades.Add(new PreEnrollmentGrade(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                SqliteDates.ParseUtcToLocal(reader.GetString(4))));
        }

        return grades;
    }

    public async Task ExcuseGradesAsync(IReadOnlyCollection<int> gradeIds)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var gradeId in gradeIds)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            // Re-check the row is still an untouched Uncompleted/0 so a grade entered in the meantime is never overwritten.
            command.CommandText = "UPDATE Grades SET Status = 3 WHERE Id = $id AND Status = 0 AND Score = 0;";
            command.Parameters.AddWithValue("$id", gradeId);
            await command.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    private static void EnsureExactlyOneRow(int rowsAffected, int assignmentId, int studentId)
    {
        if (rowsAffected != 1)
        {
            throw new InvalidOperationException(
                $"No grade record exists for assignment {assignmentId} / student {studentId}, so nothing was saved.");
        }
    }

    private static string BuildQuarterInClause(SqliteCommand command, IReadOnlyCollection<Quarter> quarters)
    {
        var parameterNames = new List<string>();
        var i = 0;
        foreach (var quarter in quarters)
        {
            var paramName = $"$q{i}";
            command.Parameters.AddWithValue(paramName, (int)quarter);
            parameterNames.Add(paramName);
            i++;
        }

        return parameterNames.Count > 0 ? string.Join(",", parameterNames) : "-1";
    }

    private static async Task<List<AssignmentGradeRecord>> ReadRecordsAsync(SqliteCommand command)
    {
        var records = new List<AssignmentGradeRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new AssignmentGradeRecord(
                StudentId: reader.GetInt32(0),
                ClassId: reader.GetInt32(1),
                AssignmentId: reader.GetInt32(2),
                AssignmentName: reader.GetString(3),
                AssignmentDate: SqliteDates.ParseUtcToLocal(reader.GetString(4)),
                Quarter: (Quarter)reader.GetInt32(5),
                Score: reader.GetDecimal(6),
                PointsPossible: reader.GetDecimal(7),
                Status: (GradeStatus)reader.GetInt32(8)));
        }

        return records;
    }
}
