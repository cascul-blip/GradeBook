namespace GradeBook.App.Services;

public interface IConfirmationDialogService
{
    /// <summary>Shows a Cancel/Confirm dialog; returns true only if the user confirmed.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete");
}
