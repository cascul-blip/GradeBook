using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace GradeBook.App.Services;

public sealed class SaveFileDialogService(Func<Window?> getWindow) : ISaveFileDialogService
{
    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string title)
    {
        var window = getWindow() ?? throw new InvalidOperationException("No active window to show the save dialog on.");

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "pdf",
            FileTypeChoices =
            [
                new FilePickerFileType("PDF Document") { Patterns = ["*.pdf"] }
            ]
        });

        return file?.TryGetLocalPath();
    }
}
