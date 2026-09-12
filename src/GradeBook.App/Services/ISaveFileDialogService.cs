namespace GradeBook.App.Services;

public interface ISaveFileDialogService
{
    /// <summary>Shows a save-file dialog; returns the chosen path, or null if the user cancelled.</summary>
    Task<string?> PickSaveFileAsync(string suggestedFileName, string title);
}
