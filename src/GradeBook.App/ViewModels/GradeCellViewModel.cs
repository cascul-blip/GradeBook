using CommunityToolkit.Mvvm.ComponentModel;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.App.ViewModels;

public partial class GradeCellViewModel : ObservableObject
{
    private readonly IGradeRepository _gradeRepository;
    private bool _suppressPersist;

    public GradeCellViewModel(IGradeRepository gradeRepository, int assignmentId, int studentId, string assignmentName, decimal pointsPossible, decimal score, GradeStatus status)
    {
        _gradeRepository = gradeRepository;
        AssignmentId = assignmentId;
        StudentId = studentId;
        AssignmentName = assignmentName;
        PointsPossible = pointsPossible;

        _suppressPersist = true;
        Score = score;
        Status = status;
        _suppressPersist = false;
    }

    public int AssignmentId { get; }
    public int StudentId { get; }
    public string AssignmentName { get; }
    public decimal PointsPossible { get; }

    public static IReadOnlyList<GradeStatus> StatusOptions { get; } =
        [GradeStatus.Uncompleted, GradeStatus.Completed, GradeStatus.Late, GradeStatus.Excused];

    [ObservableProperty]
    private decimal _score;

    [ObservableProperty]
    private GradeStatus _status;

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

        _ = _gradeRepository.SetStatusAsync(AssignmentId, StudentId, value);
    }

    private async Task PersistScoreAsync(decimal value)
    {
        await _gradeRepository.SetScoreAsync(AssignmentId, StudentId, value);

        // The repository auto-flips Uncompleted -> Completed on a score write; mirror that locally
        // so the status dropdown reflects it without a full grid reload.
        if (Status == GradeStatus.Uncompleted)
        {
            _suppressPersist = true;
            Status = GradeStatus.Completed;
            _suppressPersist = false;
        }
    }
}
