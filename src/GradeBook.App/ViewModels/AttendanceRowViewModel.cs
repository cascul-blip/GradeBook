using CommunityToolkit.Mvvm.ComponentModel;
using GradeBook.App.Services;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;

namespace GradeBook.App.ViewModels;

public partial class AttendanceRowViewModel : ObservableObject
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IErrorReporter _errorReporter;
    private readonly Func<Task> _reload;
    private readonly int _studentId;
    private readonly Quarter _quarter;

    // Saves are serialized and each one writes the row's values as they are when it runs, so a burst of
    // +/− clicks can't finish out of order and leave an older count in the database.
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _suppressPersist;

    public AttendanceRowViewModel(
        IAttendanceRepository attendanceRepository, IErrorReporter errorReporter, Func<Task> reload,
        int studentId, string studentName, Quarter quarter, int absences, int tardies)
    {
        _attendanceRepository = attendanceRepository;
        _errorReporter = errorReporter;
        _reload = reload;
        _studentId = studentId;
        _quarter = quarter;
        StudentName = studentName;

        _suppressPersist = true;
        Absences = absences;
        Tardies = tardies;
        _suppressPersist = false;
    }

    public string StudentName { get; }

    // decimal? because that's what NumericUpDown binds to; an emptied box is saved as 0.
    [ObservableProperty]
    private decimal? _absences;

    [ObservableProperty]
    private decimal? _tardies;

    partial void OnAbsencesChanged(decimal? value) => PersistIfAllowed();
    partial void OnTardiesChanged(decimal? value) => PersistIfAllowed();

    private void PersistIfAllowed()
    {
        if (!_suppressPersist)
        {
            _ = PersistAsync();
        }
    }

    private async Task PersistAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            await _attendanceRepository.SetAsync(_studentId, _quarter, ToCount(Absences), ToCount(Tardies));
        }
        catch (Exception ex)
        {
            await _errorReporter.ReportAsync(
                $"Couldn't save {StudentName}'s attendance for {_quarter}. The list will reload to show what is actually saved.", ex);

            try
            {
                await _reload();
            }
            catch (Exception reloadEx)
            {
                _errorReporter.Log("Reloading attendance after a failed save", reloadEx);
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static int ToCount(decimal? value) => value is { } v && v > 0 ? (int)Math.Round(v) : 0;
}
