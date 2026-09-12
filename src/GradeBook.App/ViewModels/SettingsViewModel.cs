using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GradeBook.App.Services;
using GradeBook.Core.Data;

namespace GradeBook.App.ViewModels;

public partial class SettingsViewModel(
    AppSettingsStore settingsStore,
    IFolderPickerService folderPickerService,
    IConfirmationDialogService confirmationDialogService) : ViewModelBase
{
    [ObservableProperty]
    private string _currentDatabasePath = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public void Initialize()
    {
        var settings = settingsStore.Load();
        CurrentDatabasePath = settings.DatabasePath ?? SqliteConnectionFactory.ResolveDefaultDatabasePath();
    }

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

        if (!File.Exists(newDatabasePath) && File.Exists(CurrentDatabasePath))
        {
            var copyExisting = await confirmationDialogService.ConfirmAsync(
                "Copy Existing Data?",
                "No gradebook.db was found in that folder. Copy your current data there now, so it isn't left behind?",
                confirmText: "Copy");
            if (copyExisting)
            {
                File.Copy(CurrentDatabasePath, newDatabasePath);
            }
        }

        settingsStore.Save(new AppSettings { DatabasePath = newDatabasePath });
        CurrentDatabasePath = newDatabasePath;
        StatusMessage = "Saved. Restart GradeBook for this change to take effect.";
    }
}
