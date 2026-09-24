namespace GradeBook.Core.Data;

/// <summary>
/// Finds sync-client conflict copies of the database (e.g. Nextcloud's
/// "gradebook (conflicted copy 2026-09-24 083012).db"). Those appear when the gradebook was edited on
/// two computers between syncs — one computer's edits end up only in the conflict copy, so they need
/// to be surfaced rather than left to sit unnoticed. Only reports; never touches the files.
/// </summary>
public static class SyncConflictDetector
{
    public static IReadOnlyList<string> FindConflictCopies(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (directory is null || !Directory.Exists(directory))
        {
            return [];
        }

        var baseName = Path.GetFileNameWithoutExtension(databasePath);
        return Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
                           && name.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                           && !name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
                           && !name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
