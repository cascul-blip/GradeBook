using Avalonia.Controls;
using GradeBook.App.Views;

namespace GradeBook.App.Services;

/// <summary>
/// Writes errors to error.log and shows them to the user, so a failed save can never pass unnoticed.
/// Only one error dialog is shown at a time; errors raised while one is open are just logged.
/// </summary>
public sealed class ErrorReporter(Func<Window?> getWindow, string logPath) : IErrorReporter
{
    private bool _isShowingDialog;

    public string LogPath { get; } = logPath;

    public static string DefaultLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GradeBook", "error.log");

    public void Log(string context, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never itself become the thing that crashes the app.
        }
    }

    public async Task ReportAsync(string message, Exception exception)
    {
        Log(message, exception);

        if (_isShowingDialog || getWindow() is not { IsVisible: true } window)
        {
            return;
        }

        _isShowingDialog = true;
        try
        {
            await ConfirmationDialog.ShowMessageAsync(
                window, "Something Went Wrong", $"{message}{Environment.NewLine}{Environment.NewLine}{exception.Message}{Environment.NewLine}{Environment.NewLine}Details were written to {LogPath}.");
        }
        finally
        {
            _isShowingDialog = false;
        }
    }
}
