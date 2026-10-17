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

    public async Task<List<Student>> GetActiveInClassAsync(int classId)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.Id, s.Name, s.IsActive
            FROM Students s
            JOIN Enrollments e ON e.StudentId = s.Id AND e.IsActive = 1
            WHERE e.ClassId = $classId
            ORDER BY s.Name;
            """;
        command.Parameters.AddWithValue("$classId", classId);

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

    public async Task DeleteAsync(int id)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        // Only actual grade rows count as history worth protecting. A bare enrollment (active or
        // since-unenrolled) in a class that never had an assignment carries no grade data, so it's
        // safe to clean up as part of the delete rather than blocking it.
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = "SELECT COUNT(*) FROM Grades WHERE StudentId = $id;";
            checkCommand.Parameters.AddWithValue("$id", id);
            var gradeCount = (long)(await checkCommand.ExecuteScalarAsync() ?? 0L);
            if (gradeCount > 0)
            {
                throw new InvalidOperationException(
                    "This student has grade history and can't be deleted. Deactivate them instead.");
            }
        }

        // Recorded absences/tardies are history too; all-zero rows (counts entered then cleared) are not.
        using (var attendanceCheck = connection.CreateCommand())
        {
            attendanceCheck.Transaction = transaction;
            attendanceCheck.CommandText = "SELECT COUNT(*) FROM Attendance WHERE StudentId = $id AND (Absences > 0 OR Tardies > 0);";
            attendanceCheck.Parameters.AddWithValue("$id", id);
            var attendanceCount = (long)(await attendanceCheck.ExecuteScalarAsync() ?? 0L);
            if (attendanceCount > 0)
            {
                throw new InvalidOperationException(
                    "This student has attendance history and can't be deleted. Deactivate them instead.");
            }
        }

        using (var deleteAttendance = connection.CreateCommand())
        {
            deleteAttendance.Transaction = transaction;
            deleteAttendance.CommandText = "DELETE FROM Attendance WHERE StudentId = $id;";
            deleteAttendance.Parameters.AddWithValue("$id", id);
            await deleteAttendance.ExecuteNonQueryAsync();
        }

        using (var deleteEnrollments = connection.CreateCommand())
        {
            deleteEnrollments.Transaction = transaction;
            deleteEnrollments.CommandText = "DELETE FROM Enrollments WHERE StudentId = $id;";
            deleteEnrollments.Parameters.AddWithValue("$id", id);
            await deleteEnrollments.ExecuteNonQueryAsync();
        }

        using (var deleteStudent = connection.CreateCommand())
        {
            deleteStudent.Transaction = transaction;
            deleteStudent.CommandText = "DELETE FROM Students WHERE Id = $id;";
            deleteStudent.Parameters.AddWithValue("$id", id);
            await deleteStudent.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    private static Student ReadStudent(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        IsActive = reader.GetInt32(2) == 1
    };
}
