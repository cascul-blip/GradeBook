using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.App.ViewModels;

public partial class ClassesAndStudentsViewModel(
    IStudentRepository studentRepository,
    IClassRepository classRepository,
    IEnrollmentRepository enrollmentRepository) : ViewModelBase
{
    public ObservableCollection<Student> Students { get; } = [];
    public ObservableCollection<SchoolClass> Classes { get; } = [];
    public ObservableCollection<EnrollmentRowViewModel> EnrollmentsForSelectedClass { get; } = [];

    [ObservableProperty]
    private Student? _selectedStudent;

    [ObservableProperty]
    private SchoolClass? _selectedClass;

    [ObservableProperty]
    private string _newStudentName = string.Empty;

    [ObservableProperty]
    private string _newClassName = string.Empty;

    [ObservableProperty]
    private string _editStudentName = string.Empty;

    [ObservableProperty]
    private string _editClassName = string.Empty;

    public async Task InitializeAsync()
    {
        await ReloadStudentsAsync();
        await ReloadClassesAsync();
    }

    partial void OnSelectedStudentChanged(Student? value) => EditStudentName = value?.Name ?? string.Empty;

    partial void OnSelectedClassChanged(SchoolClass? value)
    {
        EditClassName = value?.Name ?? string.Empty;
        _ = ReloadEnrollmentsAsync();
    }

    [RelayCommand]
    private async Task AddStudentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewStudentName))
        {
            return;
        }

        await studentRepository.AddAsync(NewStudentName.Trim());
        NewStudentName = string.Empty;
        await ReloadStudentsAsync();
    }

    [RelayCommand]
    private async Task RenameStudentAsync()
    {
        if (SelectedStudent is null || string.IsNullOrWhiteSpace(EditStudentName))
        {
            return;
        }

        await studentRepository.RenameAsync(SelectedStudent.Id, EditStudentName.Trim());
        await ReloadStudentsAsync();
    }

    [RelayCommand]
    private async Task ToggleStudentActiveAsync()
    {
        if (SelectedStudent is null)
        {
            return;
        }

        await studentRepository.SetActiveAsync(SelectedStudent.Id, !SelectedStudent.IsActive);
        await ReloadStudentsAsync();
    }

    [RelayCommand]
    private async Task AddClassAsync()
    {
        if (string.IsNullOrWhiteSpace(NewClassName))
        {
            return;
        }

        await classRepository.AddAsync(NewClassName.Trim());
        NewClassName = string.Empty;
        await ReloadClassesAsync();
    }

    [RelayCommand]
    private async Task RenameClassAsync()
    {
        if (SelectedClass is null || string.IsNullOrWhiteSpace(EditClassName))
        {
            return;
        }

        await classRepository.RenameAsync(SelectedClass.Id, EditClassName.Trim());
        await ReloadClassesAsync();
    }

    [RelayCommand]
    private async Task ToggleClassActiveAsync()
    {
        if (SelectedClass is null)
        {
            return;
        }

        await classRepository.SetActiveAsync(SelectedClass.Id, !SelectedClass.IsActive);
        await ReloadClassesAsync();
    }

    private async Task ReloadStudentsAsync()
    {
        var selectedId = SelectedStudent?.Id;
        var students = await studentRepository.GetAllAsync(includeInactive: true);

        Students.Clear();
        foreach (var student in students)
        {
            Students.Add(student);
        }

        SelectedStudent = Students.FirstOrDefault(s => s.Id == selectedId);
    }

    private async Task ReloadClassesAsync()
    {
        var selectedId = SelectedClass?.Id;
        var classes = await classRepository.GetAllAsync(includeInactive: true);

        Classes.Clear();
        foreach (var schoolClass in classes)
        {
            Classes.Add(schoolClass);
        }

        SelectedClass = Classes.FirstOrDefault(c => c.Id == selectedId);
        await ReloadEnrollmentsAsync();
    }

    private async Task ReloadEnrollmentsAsync()
    {
        EnrollmentsForSelectedClass.Clear();

        if (SelectedClass is null)
        {
            return;
        }

        var activeStudentIds = (await enrollmentRepository.GetActiveStudentIdsForClassAsync(SelectedClass.Id)).ToHashSet();

        foreach (var student in Students.Where(s => s.IsActive))
        {
            EnrollmentsForSelectedClass.Add(new EnrollmentRowViewModel(
                enrollmentRepository, student.Id, student.Name, SelectedClass.Id, activeStudentIds.Contains(student.Id)));
        }
    }
}
