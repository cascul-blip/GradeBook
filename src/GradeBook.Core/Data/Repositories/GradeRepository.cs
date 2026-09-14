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
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        // Auto-flip Uncompleted (0) -> Completed (1); leave any other status (Late/Excused/Completed) untouched.
        command.CommandText = """
            UPDATE Grades
            SET Score = $score,
                Status = CASE WHEN Status = 0 THEN 1 ELSE Status END
            WHERE AssignmentId = $assignmentId AND StudentId = $studentId;
            """;
        command.Parameters.AddWithValue("$score", score);
        command.Parameters.AddWithValue("$assignmentId", assignmentId);
        command.Parameters.AddWithValue("$studentId", studentId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetStatusAsync(int assignmentId, int studentId, GradeStatus status)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Grades SET Status = $status WHERE AssignmentId = $assignmentId AND StudentId = $studentId;";
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$assignmentId", assignmentId);
        command.Parameters.AddWithValue("$studentId", studentId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task EnsureGradeRecordAsync(int assignmentId, int studentId)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO Grades (AssignmentId, StudentId, Score, Status)
            VALUES ($assignmentId, $studentId, 0, 0);
            """;
        command.Parameters.AddWithValue("$assignmentId", assignmentId);
        command.Parameters.AddWithValue("$studentId", studentId);
        await command.ExecuteNonQueryAsync();
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
                AssignmentDate: DateTime.Parse(reader.GetString(4)),
                Quarter: (Quarter)reader.GetInt32(5),
                Score: reader.GetDecimal(6),
                PointsPossible: reader.GetDecimal(7),
                Status: (GradeStatus)reader.GetInt32(8)));
        }

        return records;
    }
}
