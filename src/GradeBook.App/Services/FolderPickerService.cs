using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace GradeBook.App.Services;

public sealed class FolderPickerService(Func<Window?> getWindow) : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var window = getWindow() ?? throw new InvalidOperationException("No active window to show the folder picker on.");

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
