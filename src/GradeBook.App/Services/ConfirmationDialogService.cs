using Avalonia.Controls;
using GradeBook.App.Views;

namespace GradeBook.App.Services;

public sealed class ConfirmationDialogService(Func<Window?> getWindow) : IConfirmationDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete", string cancelText = "Cancel") =>
        ConfirmationDialog.ShowAsync(GetWindow(), title, message, confirmText, cancelText);

    public Task ShowMessageAsync(string title, string message) =>
        ConfirmationDialog.ShowMessageAsync(GetWindow(), title, message);

    private Window GetWindow() =>
        getWindow() ?? throw new InvalidOperationException("No active window to show the dialog on.");
}
