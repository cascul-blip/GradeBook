using CommunityToolkit.Mvvm.ComponentModel;
using GradeBook.App.Services;
using GradeBook.Core.Data.Repositories;

namespace GradeBook.App.ViewModels;

public partial class EnrollmentRowViewModel : ObservableObject
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IErrorReporter _errorReporter;
    private readonly int _studentId;
    private readonly int _classId;
    private bool _suppressPersist;

    public EnrollmentRowViewModel(
        IEnrollmentRepository enrollmentRepository, IErrorReporter errorReporter, int studentId, string studentName, int classId, bool isEnrolled)
    {
        _enrollmentRepository = enrollmentRepository;
        _errorReporter = errorReporter;
        _studentId = studentId;
        _classId = classId;
        StudentName = studentName;

        SetWithoutPersisting(isEnrolled);
    }

    public string StudentName { get; }

    [ObservableProperty]
    private bool _isEnrolled;

    partial void OnIsEnrolledChanged(bool value)
    {
        if (_suppressPersist)
        {
            return;
        }

        _ = PersistAsync(value);
    }

    private async Task PersistAsync(bool enroll)
    {
        try
        {
            if (enroll)
            {
                await _enrollmentRepository.EnrollAsync(_studentId, _classId);
            }
            else
            {
                await _enrollmentRepository.UnenrollAsync(_studentId, _classId);
            }
        }
        catch (Exception ex)
        {
            SetWithoutPersisting(!enroll);
            await _errorReporter.ReportAsync($"Couldn't {(enroll ? "enroll" : "unenroll")} {StudentName}.", ex);
        }
    }

    private void SetWithoutPersisting(bool isEnrolled)
    {
        _suppressPersist = true;
        IsEnrolled = isEnrolled;
        _suppressPersist = false;
    }
}
