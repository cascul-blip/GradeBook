using GradeBook.Core.Models;
using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data.Repositories;

public sealed class StudentRepository(SqliteConnectionFactory connectionFactory) : IStudentRepository
{
    public async Task<List<Student>> GetAllAsync(bool includeInactive = false)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = includeInactive
            ? "SELECT Id, Name, IsActive FROM Students ORDER BY Name;"
            : "SELECT Id, Name, IsActive FROM Students WHERE IsActive = 1 ORDER BY Name;";

        var students = new List<Student>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            students.Add(ReadStudent(reader));
        }

        return students;
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, IsActive FROM Students WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadStudent(reader) : null;
    }

    public async Task<int> AddAsync(string name)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Students (Name) VALUES ($name); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name);
        var id = await command.ExecuteScalarAsync();
        return Convert.ToInt32(id);
    }

    public async Task RenameAsync(int id, string name)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Students SET Name = $name WHERE Id = $id;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Students SET IsActive = $isActive WHERE Id = $id;";
        command.Parameters.AddWithValue("$isActive", isActive ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static Student ReadStudent(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        IsActive = reader.GetInt32(2) == 1
    };
}
