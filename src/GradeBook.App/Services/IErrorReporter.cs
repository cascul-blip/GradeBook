namespace GradeBook.App.Services;

public interface IErrorReporter
{
    /// <summary>Appends the error to the log file only.</summary>
    void Log(string context, Exception exception);

    /// <summary>Logs the error and tells the user about it. Must be called on the UI thread.</summary>
    Task ReportAsync(string message, Exception exception);
}
