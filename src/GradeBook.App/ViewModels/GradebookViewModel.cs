using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GradeBook.App.Services;
using GradeBook.Core.Data;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.App.ViewModels;

public partial class GradebookViewModel(
    IClassRepository classRepository,
    IStudentRepository studentRepository,
    IEnrollmentRepository enrollmentRepository,
    IAssignmentRepository assignmentRepository,
    IGradeRepository gradeRepository,
    IConfirmationDialogService confirmationDialogService,
    IErrorReporter errorReporter,
    AppSettingsStore settingsStore) : ViewModelBase
{
    public static IReadOnlyList<Quarter> Quarters { get; } = [Quarter.Q1, Quarter.Q2, Quarter.Q3, Quarter.Q4];

    public ObservableCollection<SchoolClass> AvailableClasses { get; } = [];
    public ObservableCollection<Assignment> Assignments { get; } = [];
    public ObservableCollection<GradebookRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private SchoolClass? _selectedClass;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddAssignmentButtonText))]
    private Quarter _selectedQuarter = Quarter.Q1;

    [ObservableProperty]
    private string _newAssignmentName = string.Empty;

    [ObservableProperty]
    private string _newAssignmentPoints = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Names the quarter on the button itself so a lesson can't be added to the wrong quarter unnoticed.</summary>
    public string AddAssignmentButtonText => $"Add to {SelectedQuarter}";

    public Task InitializeAsync()
    {
        // Restore the last-used quarter (rather than always starting on Q1) so new lessons land where expected.
        try
        {
            if (settingsStore.Load().LastQuarter is { } lastQuarter && Enum.IsDefined(lastQuarter))
            {
                SelectedQuarter = lastQuarter;
            }
        }
        catch (Exception ex)
        {
            errorReporter.Log("Restoring the last-used quarter", ex);
        }

        return RefreshClassesAsync();
    }

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

    partial void OnSelectedClassChanged(SchoolClass? value) => _ = LoadGradebookSafelyAsync();

    partial void OnSelectedQuarterChanged(Quarter value)
    {
        try
        {
            settingsStore.Update(s => s.LastQuarter = value);
        }
        catch (Exception ex)
        {
            errorReporter.Log("Saving the last-used quarter", ex);
        }

        _ = LoadGradebookSafelyAsync();
    }

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

        if (!decimal.TryParse(NewAssignmentPoints, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture, out var points) || points <= 0)
        {
            StatusMessage = "Enter a positive point value.";
            return;
        }

        var name = NewAssignmentName.Trim();
        if (Assignments.Any(a => string.Equals(a.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            var addAnyway = await confirmationDialogService.ConfirmAsync(
                "Duplicate Lesson",
                $"\"{name}\" already exists in {SelectedClass.Name} for {SelectedQuarter}. Add a second copy anyway? " +
                "Every student would get another lesson to complete.",
                confirmText: "Add Anyway");
            if (!addAnyway)
            {
                return;
            }
        }

        await assignmentRepository.CreateAssignmentWithGradesAsync(SelectedClass.Id, SelectedQuarter, name, points);
        NewAssignmentName = string.Empty;
        NewAssignmentPoints = string.Empty;
        await LoadGradebookAsync();
    }

    public async Task UpdateAssignmentAsync(Assignment original, string name, decimal pointsPossible, Quarter quarter)
    {
        StatusMessage = null;

        if (pointsPossible < original.PointsPossible)
        {
            var scoresAbove = await gradeRepository.CountScoresAboveAsync(original.Id, pointsPossible);
            if (scoresAbove > 0)
            {
                var saveAnyway = await confirmationDialogService.ConfirmAsync(
                    "Scores Above New Point Value",
                    $"{scoresAbove} student{(scoresAbove == 1 ? " has a score" : "s have scores")} above {pointsPossible:0.##} on \"{original.Name}\". " +
                    "Their scores won't change, so they'd count as extra credit (over 100%). Save the new point value anyway?",
                    confirmText: "Save Anyway");
                if (!saveAnyway)
                {
                    StatusMessage = "Nothing was changed.";
                    return;
                }
            }
        }

        await assignmentRepository.UpdateAsync(original.Id, name, pointsPossible, quarter);
        if (quarter != original.Quarter)
        {
            StatusMessage = $"Moved \"{name}\" to {quarter}.";
        }

        await LoadGradebookAsync();
    }

    public async Task DeleteAssignmentAsync(int assignmentId)
    {
        await assignmentRepository.DeleteAsync(assignmentId);
        await LoadGradebookAsync();
    }

    private async Task LoadGradebookSafelyAsync()
    {
        try
        {
            await LoadGradebookAsync();
        }
        catch (Exception ex)
        {
            await errorReporter.ReportAsync("Couldn't load the gradebook.", ex);
        }
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
        // row for those yet — create the missing rows (Excused for lessons from before they enrolled).
        await gradeRepository.EnsureGradeRecordsForClassAndQuarterAsync(classId, quarter);
        var records = (await gradeRepository.GetRecordsForClassAndQuarterAsync(classId, quarter))
            .ToDictionary(r => (r.StudentId, r.AssignmentId));

        foreach (var assignment in assignments)
        {
            Assignments.Add(assignment);
        }

        foreach (var student in activeStudents)
        {
            var row = new GradebookRowViewModel(student.Name);
            foreach (var assignment in assignments)
            {
                // A missing row must not become an editable cell: saves to it would update nothing.
                row.Cells.Add(records.TryGetValue((student.Id, assignment.Id), out var record)
                    ? new GradeCellViewModel(
                        gradeRepository, errorReporter, LoadGradebookAsync,
                        assignment.Id, assignment.Name, assignment.PointsPossible,
                        student.Id, student.Name, record.Score, record.Status)
                    : null);
            }

            Rows.Add(row);
        }
    }
}
