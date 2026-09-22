namespace GradeBook.Core.Data;

public static class DatabaseBackupService
{
    /// <summary>Copies the database to a same-folder, date-stamped backup (MMddyy-gradebook.db) if one doesn't already exist for today. No-op if the database file doesn't exist yet.</summary>
    public static void CreateDailyBackupIfNeeded(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(databasePath)!;
        var backupPath = Path.Combine(directory, $"{DateTime.Now:MMddyy}-gradebook.db");

        if (File.Exists(backupPath))
        {
            return;
        }

        File.Copy(databasePath, backupPath);
    }
}
