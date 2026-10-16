using GradeBook.Core.Models;

namespace GradeBook.Core.Data.Repositories;

public sealed class AttendanceRepository(SqliteConnectionFactory connectionFactory) : IAttendanceRepository
{
    public async Task<List<AttendanceRecord>> GetForQuarterAsync(Quarter quarter)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StudentId, Quarter, Absences, Tardies FROM Attendance WHERE Quarter = $quarter;";
        command.Parameters.AddWithValue("$quarter", (int)quarter);

        var records = new List<AttendanceRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new AttendanceRecord(reader.GetInt32(0), (Quarter)reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
        }

        return records;
    }

    public async Task<Dictionary<int, (int Absences, int Tardies)>> GetTotalsAsync(IReadOnlyCollection<Quarter> quarters)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        var parameterNames = new List<string>();
        foreach (var quarter in quarters)
        {
            var paramName = $"$q{parameterNames.Count}";
            command.Parameters.AddWithValue(paramName, (int)quarter);
            parameterNames.Add(paramName);
        }

        var quarterList = parameterNames.Count > 0 ? string.Join(",", parameterNames) : "-1";
        command.CommandText = $"""
            SELECT StudentId, SUM(Absences), SUM(Tardies)
            FROM Attendance
            WHERE Quarter IN ({quarterList})
            GROUP BY StudentId;
            """;

        var totals = new Dictionary<int, (int Absences, int Tardies)>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            totals[reader.GetInt32(0)] = (reader.GetInt32(1), reader.GetInt32(2));
        }

        return totals;
    }

    public async Task SetAsync(int studentId, Quarter quarter, int absences, int tardies)
    {
        if (absences < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(absences), absences, "Absences can't be negative.");
        }

        if (tardies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tardies), tardies, "Tardies can't be negative.");
        }

        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Attendance (StudentId, Quarter, Absences, Tardies)
            VALUES ($studentId, $quarter, $absences, $tardies)
            ON CONFLICT (StudentId, Quarter) DO UPDATE SET Absences = excluded.Absences, Tardies = excluded.Tardies;
            """;
        command.Parameters.AddWithValue("$studentId", studentId);
        command.Parameters.AddWithValue("$quarter", (int)quarter);
        command.Parameters.AddWithValue("$absences", absences);
        command.Parameters.AddWithValue("$tardies", tardies);
        await command.ExecuteNonQueryAsync();
    }
}
