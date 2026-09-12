using CommunityToolkit.Mvvm.ComponentModel;
using GradeBook.Core.Data.Repositories;

namespace GradeBook.App.ViewModels;

public partial class EnrollmentRowViewModel : ObservableObject
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly int _studentId;
    private readonly int _classId;
    private bool _suppressPersist;

    public EnrollmentRowViewModel(IEnrollmentRepository enrollmentRepository, int studentId, string studentName, int classId, bool isEnrolled)
    {
        _enrollmentRepository = enrollmentRepository;
        _studentId = studentId;
        _classId = classId;
        StudentName = studentName;

        _suppressPersist = true;
        IsEnrolled = isEnrolled;
        _suppressPersist = false;
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

        _ = value
            ? _enrollmentRepository.EnrollAsync(_studentId, _classId)
            : _enrollmentRepository.UnenrollAsync(_studentId, _classId);
    }
}
