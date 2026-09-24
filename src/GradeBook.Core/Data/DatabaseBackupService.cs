using Microsoft.Data.Sqlite;

namespace GradeBook.Core.Data;

public static class DatabaseBackupService
{
    private const string BackupSuffix = "-gradebook.db";

    /// <summary>Copies the database to a same-folder, date-stamped backup (MMddyy-gradebook.db) if one doesn't already exist for today. No-op if the database file doesn't exist yet.</summary>
    public static void CreateDailyBackupIfNeeded(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(databasePath)!;
        var backupPath = Path.Combine(directory, $"{DateTime.Now:MMddyy}{BackupSuffix}");

        if (File.Exists(backupPath))
        {
            return;
        }

        CopyDatabase(databasePath, backupPath);
    }

    /// <summary>
    /// Makes a consistent copy of a SQLite database using SQLite's own backup API (rather than a raw
    /// file copy, which can capture a half-written state). The copy is built in a temp file and only
    /// renamed into place once complete, so a failure never leaves a truncated file at the destination.
    /// Throws if the destination already exists.
    /// </summary>
    public static void CopyDatabase(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            throw new IOException($"'{destinationPath}' already exists.");
        }

        var tempPath = destinationPath + ".tmp";
        try
        {
            using (var source = new SqliteConnection(ConnectionString(sourcePath, SqliteOpenMode.ReadOnly)))
            using (var destination = new SqliteConnection(ConnectionString(tempPath, SqliteOpenMode.ReadWriteCreate)))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            File.Move(tempPath, destinationPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>The most recently written MMddyy-gradebook.db backup next to the database, or null if there are none.</summary>
    public static string? FindNewestBackup(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }

        return new DirectoryInfo(directory)
            .EnumerateFiles($"*{BackupSuffix}")
            .Where(f => f.Name.Length == 6 + BackupSuffix.Length && f.Name[..6].All(char.IsDigit))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    // Pooling off so the files are actually closed on Dispose — the temp file has to be renamed right after.
    private static string ConnectionString(string path, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString();
}
