using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.App.ViewModels;

public partial class GradebookViewModel(
    IClassRepository classRepository,
    IStudentRepository studentRepository,
    IEnrollmentRepository enrollmentRepository,
    IAssignmentRepository assignmentRepository,
    IGradeRepository gradeRepository) : ViewModelBase
{
    public static IReadOnlyList<Quarter> Quarters { get; } = [Quarter.Q1, Quarter.Q2, Quarter.Q3, Quarter.Q4];

    public ObservableCollection<SchoolClass> AvailableClasses { get; } = [];
    public ObservableCollection<Assignment> Assignments { get; } = [];
    public ObservableCollection<GradebookRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private SchoolClass? _selectedClass;

    [ObservableProperty]
    private Quarter _selectedQuarter = Quarter.Q1;

    [ObservableProperty]
    private string _newAssignmentName = string.Empty;

    [ObservableProperty]
    private string _newAssignmentPoints = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public Task InitializeAsync() => RefreshClassesAsync();

    /// <summary>
    /// Re-reads the class list from the database. Classes are added/renamed on the Classes &amp; Students
    /// tab, so this needs to run again whenever the Gradebook tab is shown, not just once at startup.
    /// </summary>
    public async Task RefreshClassesAsync()
    {
        var previouslySelectedId = SelectedClass?.Id;
        var classes = await classRepository.GetAllAsync();

        AvailableClasses.Clear();
        foreach (var schoolClass in classes)
        {
            AvailableClasses.Add(schoolClass);
        }

        var restored = AvailableClasses.FirstOrDefault(c => c.Id == previouslySelectedId);
        if (restored is not null)
        {
            SelectedClass = restored;
        }
        else
        {
            SelectedClass = AvailableClasses.FirstOrDefault();
        }
    }

    partial void OnSelectedClassChanged(SchoolClass? value) => _ = LoadGradebookAsync();

    partial void OnSelectedQuarterChanged(Quarter value) => _ = LoadGradebookAsync();

    [RelayCommand]
    private async Task AddAssignmentAsync()
    {
        StatusMessage = null;

        if (SelectedClass is null)
        {
            StatusMessage = "Select a class first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewAssignmentName))
        {
            StatusMessage = "Enter an assignment name.";
            return;
        }

        if (!decimal.TryParse(NewAssignmentPoints, NumberStyles.Number, CultureInfo.InvariantCulture, out var points) || points <= 0)
        {
            StatusMessage = "Enter a positive point value.";
            return;
        }

        await assignmentRepository.CreateAssignmentWithGradesAsync(SelectedClass.Id, SelectedQuarter, NewAssignmentName.Trim(), points);
        NewAssignmentName = string.Empty;
        NewAssignmentPoints = string.Empty;
        await LoadGradebookAsync();
    }

    private async Task LoadGradebookAsync()
    {
        Assignments.Clear();
        Rows.Clear();

        if (SelectedClass is null)
        {
            return;
        }

        var classId = SelectedClass.Id;
        var quarter = SelectedQuarter;

        var activeStudentIds = await enrollmentRepository.GetActiveStudentIdsForClassAsync(classId);
        var activeStudents = (await studentRepository.GetAllAsync(includeInactive: true))
            .Where(s => activeStudentIds.Contains(s.Id))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assignments = await assignmentRepository.GetForClassAndQuarterAsync(classId, quarter);

        // Self-heal: a student who enrolled after some assignments already existed won't have a Grade
        // row for those yet — create the missing default rows before reading the grid back.
        var records = await gradeRepository.GetRecordsForClassAndQuarterAsync(classId, quarter);
        var existingPairs = records.Select(r => (r.StudentId, r.AssignmentId)).ToHashSet();
        foreach (var student in activeStudents)
        {
            foreach (var assignment in assignments)
            {
                if (!existingPairs.Contains((student.Id, assignment.Id)))
                {
                    await gradeRepository.EnsureGradeRecordAsync(assignment.Id, student.Id);
                }
            }
        }

        if (assignments.Count > 0 && activeStudents.Count > 0)
        {
            records = await gradeRepository.GetRecordsForClassAndQuarterAsync(classId, quarter);
        }

        foreach (var assignment in assignments)
        {
            Assignments.Add(assignment);
        }

        foreach (var student in activeStudents)
        {
            var row = new GradebookRowViewModel(student.Id, student.Name);
            foreach (var assignment in assignments)
            {
                var record = records.FirstOrDefault(r => r.StudentId == student.Id && r.AssignmentId == assignment.Id);
                row.Cells.Add(new GradeCellViewModel(
                    gradeRepository, assignment.Id, student.Id, assignment.Name, assignment.PointsPossible,
                    record.Score, record.Status));
            }

            Rows.Add(row);
        }
    }
}
