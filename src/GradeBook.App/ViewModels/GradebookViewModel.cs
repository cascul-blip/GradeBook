using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
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
    IAssignmentRepository assignmentRepository,
    IGradeRepository gradeRepository,
    IConfirmationDialogService confirmationDialogService,
    IErrorReporter errorReporter,
    AppSettingsStore settingsStore) : ViewModelBase
{
    public static IReadOnlyList<Quarter> Quarters { get; } = [Quarter.Q1, Quarter.Q2, Quarter.Q3, Quarter.Q4];

    public ObservableCollection<SchoolClass> AvailableClasses { get; } = [];
    // Replaced in one step per load (see LoadGradebookAsync): the view rebuilds every grid column on each
    // change to Assignments, so adding lessons one at a time would rebuild the columns once per lesson.
    public RangeObservableCollection<Assignment> Assignments { get; } = [];
    public RangeObservableCollection<GradebookRowViewModel> Rows { get; } = [];

    // What the grid currently shows, so re-selecting the same class/quarter (e.g. after the class list is
    // re-read on a tab switch, which creates new SchoolClass instances) doesn't rebuild the grid.
    private (int ClassId, Quarter Quarter)? _loadedKey;

    // Set when another tab may have changed students or enrollments, so the next visit reloads even if
    // the class and quarter are the same.
    private bool _isStale = true;

    private bool _restoringQuarter;
    private bool _refreshingClasses;

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
                _restoringQuarter = true;
                SelectedQuarter = lastQuarter;
            }
        }
        catch (Exception ex)
        {
            errorReporter.Log("Restoring the last-used quarter", ex);
        }
        finally
        {
            _restoringQuarter = false;
        }

        return RefreshClassesAsync();
    }

    /// <summary>Students or enrollments may have changed elsewhere; reload the grid on the next refresh.</summary>
    public void MarkStale() => _isStale = true;

    /// <summary>
    /// Re-reads the class list from the database. Classes are added/renamed on the Classes &amp; Students
    /// tab, so this needs to run again whenever the Gradebook tab is shown, not just once at startup.
    /// The grid itself only reloads if the selection changed or <see cref="MarkStale"/> was called.
    /// </summary>
    public async Task RefreshClassesAsync()
    {
        var previouslySelectedId = SelectedClass?.Id;
        var classes = await classRepository.GetAllAsync();

        // Clearing the list can null the ComboBox selection, and restoring it assigns a new instance, so
        // hold off on loading until the selection is settled, then load once if anything changed.
        _refreshingClasses = true;
        try
        {
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
        finally
        {
            _refreshingClasses = false;
        }

        await LoadIfNeededSafelyAsync();
    }

    partial void OnSelectedClassChanged(SchoolClass? value)
    {
        if (!_refreshingClasses)
        {
            _ = LoadIfNeededSafelyAsync();
        }
    }

    partial void OnSelectedQuarterChanged(Quarter value)
    {
        // Restoring the saved quarter at startup shouldn't write it straight back.
        if (!_restoringQuarter)
        {
            try
            {
                settingsStore.Update(s => s.LastQuarter = value);
            }
            catch (Exception ex)
            {
                errorReporter.Log("Saving the last-used quarter", ex);
            }
        }

        _ = LoadIfNeededSafelyAsync();
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

    private Task LoadIfNeededSafelyAsync()
    {
        (int, Quarter)? wanted = SelectedClass is null ? null : (SelectedClass.Id, SelectedQuarter);
        if (!_isStale && wanted == _loadedKey)
        {
            return Task.CompletedTask;
        }

        return LoadGradebookSafelyAsync();
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
        if (SelectedClass is null)
        {
            ShowGrid([], []);
            _loadedKey = null;
            _isStale = false;
            return;
        }

        try
        {
            await LoadSelectedClassAsync(SelectedClass.Id, SelectedQuarter);
        }
        catch
        {
            // Don't leave the previous grid on screen looking current after a failed load (e.g. the
            // reload that follows a failed save, which would otherwise keep showing the unsaved value).
            ShowGrid([], []);
            _loadedKey = null;
            throw;
        }
    }

    private async Task LoadSelectedClassAsync(int classId, Quarter quarter)
    {
        var stopwatch = Stopwatch.StartNew();

        var activeStudents = (await studentRepository.GetActiveInClassAsync(classId))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assignments = await assignmentRepository.GetForClassAndQuarterAsync(classId, quarter);

        // Self-heal: a student who enrolled after some assignments already existed won't have a Grade
        // row for those yet — create the missing rows (Excused for lessons from before they enrolled).
        await gradeRepository.EnsureGradeRecordsForClassAndQuarterAsync(classId, quarter);
        var records = (await gradeRepository.GetRecordsForClassAndQuarterAsync(classId, quarter))
            .ToDictionary(r => (r.StudentId, r.AssignmentId));

        var databaseTime = stopwatch.Elapsed;

        // Build everything before touching the bound collections, so the current grid stays on screen
        // during the database reads instead of going blank.
        var rows = new List<GradebookRowViewModel>(activeStudents.Count);
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

            rows.Add(row);
        }

        var buildTime = stopwatch.Elapsed;
        ShowGrid(assignments, rows);
        _loadedKey = (classId, quarter);
        _isStale = false;
        LogLoadTiming(rows.Count, assignments.Count, stopwatch, databaseTime, buildTime);
    }

    /// <summary>
    /// Debug builds (e.g. run.sh) print how long each part of a load took, to tell a slow database read
    /// from a slow grid. Background priority runs after the layout and render pass for the new grid.
    /// </summary>
    [Conditional("DEBUG")]
    private static void LogLoadTiming(int students, int assignments, Stopwatch stopwatch, TimeSpan databaseTime, TimeSpan buildTime)
    {
        var showTime = stopwatch.Elapsed;
        Dispatcher.UIThread.Post(() => Console.WriteLine(
            $"Gradebook load ({students} students x {assignments} lessons): " +
            $"database {databaseTime.TotalMilliseconds:0} ms, " +
            $"view models {(buildTime - databaseTime).TotalMilliseconds:0} ms, " +
            $"columns + rows {(showTime - buildTime).TotalMilliseconds:0} ms, " +
            $"layout + render {(stopwatch.Elapsed - showTime).TotalMilliseconds:0} ms, " +
            $"total {stopwatch.Elapsed.TotalMilliseconds:0} ms"), DispatcherPriority.Background);
    }

    /// <summary>
    /// Swaps in a new grid in three single-step changes: clear the rows first so the column rebuild doesn't
    /// realize cells for rows that are about to go, then rebuild the columns once, then add all rows at once.
    /// </summary>
    private void ShowGrid(IReadOnlyList<Assignment> assignments, IReadOnlyList<GradebookRowViewModel> rows)
    {
        Rows.ReplaceAll([]);
        Assignments.ReplaceAll(assignments);
        Rows.ReplaceAll(rows);
    }
}
