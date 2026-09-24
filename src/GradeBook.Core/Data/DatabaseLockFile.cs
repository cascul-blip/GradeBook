using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GradeBook.Core.Data;

public enum DatabaseLockState
{
    /// <summary>No lock file, or it's this process's own lock.</summary>
    Free,

    /// <summary>Left behind by a GradeBook on this computer that is no longer running (e.g. after a crash) — safe to take over.</summary>
    HeldByThisMachineStale,

    /// <summary>Another GradeBook window on this computer has the database open.</summary>
    HeldByThisMachineRunning,

    /// <summary>Another computer has (or had, and didn't close cleanly) the database open. Holder is null if the lock file couldn't be read.</summary>
    HeldByOtherMachine
}

public sealed record DatabaseLockHolder(string MachineName, string UserName, int ProcessId, DateTime OpenedAtUtc);

public sealed record DatabaseLockCheck(DatabaseLockState State, DatabaseLockHolder? Holder);

/// <summary>
/// A small "gradebook.db.lock" file written next to the database while GradeBook has it open. Because
/// it lives in the same (possibly synced) folder, a second computer can see that the gradebook is
/// already open elsewhere and warn before two sets of edits collide into a sync conflict. It's
/// advisory only: sync delays mean it can't guarantee exclusivity, it just catches the common
/// "left it open at school" case.
/// </summary>
public sealed class DatabaseLockFile(string databasePath)
{
    public string LockPath { get; } = databasePath + ".lock";

    public DatabaseLockCheck Check()
    {
        if (!File.Exists(LockPath))
        {
            return new DatabaseLockCheck(DatabaseLockState.Free, null);
        }

        DatabaseLockHolder? holder;
        try
        {
            holder = JsonSerializer.Deserialize(File.ReadAllText(LockPath), DatabaseLockJsonContext.Default.DatabaseLockHolder);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            holder = null;
        }

        if (holder is null)
        {
            return new DatabaseLockCheck(DatabaseLockState.HeldByOtherMachine, null);
        }

        if (!string.Equals(holder.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseLockCheck(DatabaseLockState.HeldByOtherMachine, holder);
        }

        if (holder.ProcessId == Environment.ProcessId)
        {
            return new DatabaseLockCheck(DatabaseLockState.Free, holder);
        }

        return new DatabaseLockCheck(
            IsProcessStillRunning(holder) ? DatabaseLockState.HeldByThisMachineRunning : DatabaseLockState.HeldByThisMachineStale,
            holder);
    }

    /// <summary>Writes this process's lock, replacing any existing one.</summary>
    public void Acquire()
    {
        var holder = new DatabaseLockHolder(Environment.MachineName, Environment.UserName, Environment.ProcessId, DateTime.UtcNow);
        var tempPath = LockPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(holder, DatabaseLockJsonContext.Default.DatabaseLockHolder));
        File.Move(tempPath, LockPath, overwrite: true);
    }

    /// <summary>Deletes the lock, but only if it's still this process's — never another computer's.</summary>
    public void Release()
    {
        try
        {
            var check = Check();
            if (check.State == DatabaseLockState.Free && check.Holder is not null)
            {
                File.Delete(LockPath);
            }
        }
        catch (IOException)
        {
            // Best effort on shutdown; a leftover lock from this machine is detected as stale next launch.
        }
    }

    private static bool IsProcessStillRunning(DatabaseLockHolder holder)
    {
        try
        {
            using var process = Process.GetProcessById(holder.ProcessId);
            // Guard against the PID having been reused by an unrelated, newer process.
            return !process.HasExited && process.StartTime.ToUniversalTime() <= holder.OpenedAtUtc.AddMinutes(1);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

[JsonSerializable(typeof(DatabaseLockHolder))]
internal sealed partial class DatabaseLockJsonContext : JsonSerializerContext
{
}
