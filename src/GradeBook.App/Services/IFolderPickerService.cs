namespace GradeBook.App.Services;

public interface IFolderPickerService
{
    /// <summary>Shows a folder picker; returns the chosen local folder path, or null if the user cancelled.</summary>
    Task<string?> PickFolderAsync(string title);
}
