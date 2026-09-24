namespace GradeBook.App.Services;

public interface IConfirmationDialogService
{
    /// <summary>Shows a Cancel/Confirm dialog; returns true only if the user confirmed.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete", string cancelText = "Cancel");

    /// <summary>Shows a message with a single OK button.</summary>
    Task ShowMessageAsync(string title, string message);
}
