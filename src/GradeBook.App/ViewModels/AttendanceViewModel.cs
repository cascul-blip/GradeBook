using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GradeBook.App.Services;
using GradeBook.Core.Data;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.App.ViewModels;

public partial class AttendanceViewModel(
    IStudentRepository studentRepository,
    IAttendanceRepository attendanceRepository,
    IErrorReporter errorReporter,
    AppSettingsStore settingsStore) : ViewModelBase
{
    public static IReadOnlyList<Quarter> Quarters { get; } = [Quarter.Q1, Quarter.Q2, Quarter.Q3, Quarter.Q4];

    public ObservableCollection<AttendanceRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private Quarter _selectedQuarter = Quarter.Q1;

    public Task InitializeAsync()
    {
        // Start on the quarter the Gradebook was last used for, which is normally the current one.
        try
        {
            if (settingsStore.Load().LastQuarter is { } lastQuarter && Enum.IsDefined(lastQuarter))
            {
                SelectedQuarter = lastQuarter;
            }
        }
        catch (Exception ex)
        {
            errorReporter.Log("Restoring the last-used quarter for attendance", ex);
        }

        return RefreshSafelyAsync();
    }

    partial void OnSelectedQuarterChanged(Quarter value) => _ = RefreshSafelyAsync();

    /// <summary>
    /// Re-reads students and their counts. Students are added/deactivated on the Classes &amp; Students tab,
    /// so this runs again whenever the Attendance tab is shown.
    /// </summary>
    public async Task RefreshSafelyAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await errorReporter.ReportAsync("Couldn't load attendance.", ex);
        }
    }

    private async Task LoadAsync()
    {
        var quarter = SelectedQuarter;
        var students = await studentRepository.GetAllAsync();
        var records = (await attendanceRepository.GetForQuarterAsync(quarter)).ToDictionary(r => r.StudentId);

        // The quarter was switched again while this load was running; let the newer load fill the list.
        if (quarter != SelectedQuarter)
        {
            return;
        }

        Rows.Clear();
        foreach (var student in students.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            records.TryGetValue(student.Id, out var record);
            Rows.Add(new AttendanceRowViewModel(
                attendanceRepository, errorReporter, LoadAsync,
                student.Id, student.Name, quarter, record?.Absences ?? 0, record?.Tardies ?? 0));
        }
    }
}
