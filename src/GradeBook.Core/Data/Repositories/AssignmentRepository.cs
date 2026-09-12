using GradeBook.Core.Models;
using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data.Repositories;

public sealed class AssignmentRepository(SqliteConnectionFactory connectionFactory) : IAssignmentRepository
{
    public async Task<List<Assignment>> GetForClassAndQuarterAsync(int classId, Quarter quarter)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ClassId, Quarter, Name, PointsPossible, DateCreated
            FROM Assignments
            WHERE ClassId = $classId AND Quarter = $quarter
            ORDER BY DateCreated, Id;
            """;
        command.Parameters.AddWithValue("$classId", classId);
        command.Parameters.AddWithValue("$quarter", (int)quarter);

        var assignments = new List<Assignment>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            assignments.Add(ReadAssignment(reader));
        }

        return assignments;
    }

    public async Task<Assignment?> GetByIdAsync(int id)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ClassId, Quarter, Name, PointsPossible, DateCreated FROM Assignments WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadAssignment(reader) : null;
    }

    public async Task<int> CreateAssignmentWithGradesAsync(int classId, Quarter quarter, string name, decimal pointsPossible)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        int assignmentId;
        using (var insertAssignment = connection.CreateCommand())
        {
            insertAssignment.Transaction = transaction;
            insertAssignment.CommandText = """
                INSERT INTO Assignments (ClassId, Quarter, Name, PointsPossible)
                VALUES ($classId, $quarter, $name, $pointsPossible);
                SELECT last_insert_rowid();
                """;
            insertAssignment.Parameters.AddWithValue("$classId", classId);
            insertAssignment.Parameters.AddWithValue("$quarter", (int)quarter);
            insertAssignment.Parameters.AddWithValue("$name", name);
            insertAssignment.Parameters.AddWithValue("$pointsPossible", pointsPossible);
            assignmentId = Convert.ToInt32(await insertAssignment.ExecuteScalarAsync());
        }

        using (var selectStudents = connection.CreateCommand())
        {
            selectStudents.Transaction = transaction;
            selectStudents.CommandText = "SELECT StudentId FROM Enrollments WHERE ClassId = $classId AND IsActive = 1;";
            selectStudents.Parameters.AddWithValue("$classId", classId);

            var studentIds = new List<int>();
            using var reader = await selectStudents.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                studentIds.Add(reader.GetInt32(0));
            }

            foreach (var studentId in studentIds)
            {
                using var insertGrade = connection.CreateCommand();
                insertGrade.Transaction = transaction;
                insertGrade.CommandText = """
                    INSERT INTO Grades (AssignmentId, StudentId, Score, Status)
                    VALUES ($assignmentId, $studentId, 0, 0);
                    """;
                insertGrade.Parameters.AddWithValue("$assignmentId", assignmentId);
                insertGrade.Parameters.AddWithValue("$studentId", studentId);
                await insertGrade.ExecuteNonQueryAsync();
            }
        }

        transaction.Commit();
        return assignmentId;
    }

    public async Task UpdateAsync(int id, string name, decimal pointsPossible)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Assignments SET Name = $name, PointsPossible = $points WHERE Id = $id;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$points", pointsPossible);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Assignments WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static Assignment ReadAssignment(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ClassId = reader.GetInt32(1),
        Quarter = (Quarter)reader.GetInt32(2),
        Name = reader.GetString(3),
        PointsPossible = reader.GetDecimal(4),
        DateCreated = DateTime.Parse(reader.GetString(5))
    };
}
