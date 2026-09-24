using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GradeBook.App.Services;
using GradeBook.Core.Data;

namespace GradeBook.App.ViewModels;

public partial class SettingsViewModel(
    AppSettingsStore settingsStore,
    string activeDatabasePath,
    Action requestShutdown,
    IFolderPickerService folderPickerService,
    IConfirmationDialogService confirmationDialogService) : ViewModelBase
{
    [ObservableProperty]
    private string _currentDatabasePath = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Shows the database actually open right now (not just what settings.json says).</summary>
    public void Initialize() => CurrentDatabasePath = activeDatabasePath;

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        StatusMessage = null;

        var folder = await folderPickerService.PickFolderAsync("Choose a Folder for the Grade Data (e.g. a synced Nextcloud folder)");
        if (folder is null)
        {
            return;
        }

        var newDatabasePath = Path.Combine(folder, "gradebook.db");
        if (string.Equals(Path.GetFullPath(newDatabasePath), Path.GetFullPath(CurrentDatabasePath), StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "That folder already holds the gradebook in use.";
            return;
        }

        if (File.Exists(newDatabasePath))
        {
            // Switching to a different existing file isn't data loss, but it looks like it if it happens silently.
            var switchToExisting = await confirmationDialogService.ConfirmAsync(
                "Use Existing Gradebook?",
                $"That folder already has a gradebook.db (last changed {File.GetLastWriteTime(newDatabasePath):g}). " +
                $"GradeBook will switch to that file. Your current data stays where it is, at {CurrentDatabasePath}.",
                confirmText: "Switch");
            if (!switchToExisting)
            {
                return;
            }
        }
        else if (File.Exists(CurrentDatabasePath))
        {
            var copyExisting = await confirmationDialogService.ConfirmAsync(
                "Copy Existing Data?",
                "No gradebook.db was found in that folder. Copy your current data there now, so it isn't left behind?",
                confirmText: "Copy",
                cancelText: "Start Empty");
            if (copyExisting)
            {
                try
                {
                    DatabaseBackupService.CopyDatabase(CurrentDatabasePath, newDatabasePath);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Couldn't copy the data, so nothing was changed: {ex.Message}";
                    return;
                }
            }
        }

        try
        {
            settingsStore.Update(s => s.DatabasePath = newDatabasePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't save the setting, so nothing was changed: {ex.Message}";
            return;
        }

        // Close straight away: if GradeBook kept running, further edits would keep going to the old file.
        await confirmationDialogService.ShowMessageAsync(
            "Restart Needed",
            $"GradeBook will now close. Open it again to use the gradebook at {newDatabasePath}.");
        requestShutdown();
    }
}
