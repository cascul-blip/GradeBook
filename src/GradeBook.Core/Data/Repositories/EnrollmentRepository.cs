namespace GradeBook.Core.Data.Repositories;

public sealed class EnrollmentRepository(SqliteConnectionFactory connectionFactory) : IEnrollmentRepository
{
    public async Task<List<int>> GetActiveStudentIdsForClassAsync(int classId)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StudentId FROM Enrollments WHERE ClassId = $classId AND IsActive = 1;";
        command.Parameters.AddWithValue("$classId", classId);

        var studentIds = new List<int>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            studentIds.Add(reader.GetInt32(0));
        }

        return studentIds;
    }

    public async Task<List<int>> GetActiveClassIdsForStudentAsync(int studentId)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ClassId FROM Enrollments WHERE StudentId = $studentId AND IsActive = 1;";
        command.Parameters.AddWithValue("$studentId", studentId);

        var classIds = new List<int>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            classIds.Add(reader.GetInt32(0));
        }

        return classIds;
    }

    public async Task<bool> IsActivelyEnrolledAsync(int studentId, int classId)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Enrollments WHERE StudentId = $studentId AND ClassId = $classId AND IsActive = 1;";
        command.Parameters.AddWithValue("$studentId", studentId);
        command.Parameters.AddWithValue("$classId", classId);
        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    public async Task EnrollAsync(int studentId, int classId)
    {
        if (await IsActivelyEnrolledAsync(studentId, classId))
        {
            return;
        }

        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Enrollments (StudentId, ClassId) VALUES ($studentId, $classId);";
        command.Parameters.AddWithValue("$studentId", studentId);
        command.Parameters.AddWithValue("$classId", classId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UnenrollAsync(int studentId, int classId)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Enrollments
            SET IsActive = 0, UnenrolledDate = datetime('now')
            WHERE StudentId = $studentId AND ClassId = $classId AND IsActive = 1;
            """;
        command.Parameters.AddWithValue("$studentId", studentId);
        command.Parameters.AddWithValue("$classId", classId);
        await command.ExecuteNonQueryAsync();
    }
}
