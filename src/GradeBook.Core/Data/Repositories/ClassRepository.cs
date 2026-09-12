using GradeBook.Core.Models;
using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data.Repositories;

public sealed class ClassRepository(SqliteConnectionFactory connectionFactory) : IClassRepository
{
    public async Task<List<SchoolClass>> GetAllAsync(bool includeInactive = false)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = includeInactive
            ? "SELECT Id, Name, IsActive FROM Classes ORDER BY Name;"
            : "SELECT Id, Name, IsActive FROM Classes WHERE IsActive = 1 ORDER BY Name;";

        var classes = new List<SchoolClass>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            classes.Add(ReadClass(reader));
        }

        return classes;
    }

    public async Task<SchoolClass?> GetByIdAsync(int id)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, IsActive FROM Classes WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadClass(reader) : null;
    }

    public async Task<int> AddAsync(string name)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Classes (Name) VALUES ($name); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name);
        var id = await command.ExecuteScalarAsync();
        return Convert.ToInt32(id);
    }

    public async Task RenameAsync(int id, string name)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Classes SET Name = $name WHERE Id = $id;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Classes SET IsActive = $isActive WHERE Id = $id;";
        command.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        // Only actual assignments (and the grades hanging off them) count as history worth protecting.
        // A bare enrollment row (active or since-unenrolled) with no assignments behind it carries no
        // grade data, so it's safe to clean up as part of the delete rather than blocking it.
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = "SELECT COUNT(*) FROM Assignments WHERE ClassId = $id;";
            checkCommand.Parameters.AddWithValue("$id", id);
            var assignmentCount = (long)(await checkCommand.ExecuteScalarAsync() ?? 0L);
            if (assignmentCount > 0)
            {
                throw new InvalidOperationException(
                    "This class has assignments and grade history and can't be deleted. Deactivate it instead.");
            }
        }

        using (var deleteEnrollments = connection.CreateCommand())
        {
            deleteEnrollments.Transaction = transaction;
            deleteEnrollments.CommandText = "DELETE FROM Enrollments WHERE ClassId = $id;";
            deleteEnrollments.Parameters.AddWithValue("$id", id);
            await deleteEnrollments.ExecuteNonQueryAsync();
        }

        using (var deleteClass = connection.CreateCommand())
        {
            deleteClass.Transaction = transaction;
            deleteClass.CommandText = "DELETE FROM Classes WHERE Id = $id;";
            deleteClass.Parameters.AddWithValue("$id", id);
            await deleteClass.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    private static SchoolClass ReadClass(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        IsActive = reader.GetInt32(2) == 1
    };
}
