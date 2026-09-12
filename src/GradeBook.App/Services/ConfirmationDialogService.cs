using Avalonia.Controls;
using GradeBook.App.Views;

namespace GradeBook.App.Services;

public sealed class ConfirmationDialogService(Func<Window?> getWindow) : IConfirmationDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete")
    {
        var window = getWindow() ?? throw new InvalidOperationException("No active window to show the confirmation dialog on.");
        return ConfirmationDialog.ShowAsync(window, title, message, confirmText);
    }
}
