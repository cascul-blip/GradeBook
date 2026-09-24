using CommunityToolkit.Mvvm.ComponentModel;
using GradeBook.App.Services;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.App.ViewModels;

public partial class GradeCellViewModel : ObservableObject
{
    private readonly IGradeRepository _gradeRepository;
    private readonly IErrorReporter _errorReporter;
    private readonly Func<Task> _reloadGrid;
    private readonly string _studentName;
    private readonly string _assignmentName;
    private bool _suppressPersist;

    // True while the current Completed status came from the automatic Uncompleted -> Completed flip on
    // typing a score (not from the user picking it). If the score is then cleared back to 0, the flip is
    // undone so a stray keystroke can't quietly drop the lesson off the student's missing list.
    private bool _autoCompleted;

    public GradeCellViewModel(
        IGradeRepository gradeRepository,
        IErrorReporter errorReporter,
        Func<Task> reloadGrid,
        int assignmentId,
        string assignmentName,
        decimal pointsPossible,
        int studentId,
        string studentName,
        decimal score,
        GradeStatus status)
    {
        _gradeRepository = gradeRepository;
        _errorReporter = errorReporter;
        _reloadGrid = reloadGrid;
        _assignmentName = assignmentName;
        _studentName = studentName;
        AssignmentId = assignmentId;
        StudentId = studentId;
        PointsPossible = pointsPossible;

        _suppressPersist = true;
        Score = score;
        Status = status;
        _suppressPersist = false;
    }

    public int AssignmentId { get; }
    public int StudentId { get; }
    public decimal PointsPossible { get; }

    public static IReadOnlyList<GradeStatus> StatusOptions { get; } =
        [GradeStatus.Uncompleted, GradeStatus.Completed, GradeStatus.Late, GradeStatus.Excused];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAboveMax))]
    private decimal _score;

    [ObservableProperty]
    private GradeStatus _status;

    /// <summary>Allowed (extra credit), but flagged in the grid so a typo like 300 for 30 stands out.</summary>
    public bool IsAboveMax => Score > PointsPossible;

    partial void OnScoreChanged(decimal value)
    {
        if (_suppressPersist)
        {
            return;
        }

        _ = PersistScoreAsync(value);
    }

    partial void OnStatusChanged(GradeStatus value)
    {
        if (_suppressPersist)
        {
            return;
        }

        _autoCompleted = false;
        _ = PersistStatusAsync(value);
    }

    private async Task PersistScoreAsync(decimal value)
    {
        try
        {
            await _gradeRepository.SetScoreAsync(AssignmentId, StudentId, value);

            // The repository auto-flips Uncompleted -> Completed on a non-zero score write; mirror that
            // locally so the status dropdown reflects it without a full grid reload.
            if (Status == GradeStatus.Uncompleted && value > 0)
            {
                SetStatusWithoutPersisting(GradeStatus.Completed);
                _autoCompleted = true;
            }
            else if (_autoCompleted && value == 0 && Status == GradeStatus.Completed)
            {
                await _gradeRepository.SetStatusAsync(AssignmentId, StudentId, GradeStatus.Uncompleted);
                SetStatusWithoutPersisting(GradeStatus.Uncompleted);
                _autoCompleted = false;
            }
        }
        catch (Exception ex)
        {
            await ReportSaveFailureAsync("score", ex);
        }
    }

    private async Task PersistStatusAsync(GradeStatus value)
    {
        try
        {
            await _gradeRepository.SetStatusAsync(AssignmentId, StudentId, value);
        }
        catch (Exception ex)
        {
            await ReportSaveFailureAsync("status", ex);
        }
    }

    private void SetStatusWithoutPersisting(GradeStatus status)
    {
        _suppressPersist = true;
        Status = status;
        _suppressPersist = false;
    }

    private async Task ReportSaveFailureAsync(string what, Exception ex)
    {
        await _errorReporter.ReportAsync(
            $"Couldn't save {_studentName}'s {what} for \"{_assignmentName}\". The gradebook will reload to show what is actually saved.", ex);

        try
        {
            await _reloadGrid();
        }
        catch (Exception reloadEx)
        {
            _errorReporter.Log("Reloading the gradebook after a failed save", reloadEx);
        }
    }
}
