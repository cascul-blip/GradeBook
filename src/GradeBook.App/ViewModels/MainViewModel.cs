using CommunityToolkit.Mvvm.ComponentModel;

namespace GradeBook.App.ViewModels;

public partial class MainViewModel(
    ClassesAndStudentsViewModel classesAndStudents,
    GradebookViewModel gradebook,
    AttendanceViewModel attendance,
    ReportsViewModel reports,
    SettingsViewModel settings) : ViewModelBase
{
    public ClassesAndStudentsViewModel ClassesAndStudents { get; } = classesAndStudents;
    public GradebookViewModel Gradebook { get; } = gradebook;
    public AttendanceViewModel Attendance { get; } = attendance;
    public ReportsViewModel Reports { get; } = reports;
    public SettingsViewModel Settings { get; } = settings;

    [ObservableProperty]
    private int _selectedTabIndex;

    private int _previousTabIndex;

    public async Task InitializeAsync()
    {
        await ClassesAndStudents.InitializeAsync();
        await Gradebook.InitializeAsync();
        await Attendance.InitializeAsync();
        await Reports.InitializeAsync();
        Settings.Initialize();
    }

    // Classes/students are added on tab 0; the Gradebook (1), Attendance (2) and Reports (3) tabs cache
    // their own class/student lists, so refresh those whenever the user switches into that tab. The
    // Gradebook grid is expensive to rebuild, so it only reloads after a visit to tab 0 (the only place
    // students and enrollments change), not after Attendance or Reports.
    partial void OnSelectedTabIndexChanged(int value)
    {
        if (_previousTabIndex == 0)
        {
            Gradebook.MarkStale();
        }

        _previousTabIndex = value;

        _ = value switch
        {
            1 => Gradebook.RefreshClassesAsync(),
            2 => Attendance.RefreshSafelyAsync(),
            3 => Reports.RefreshFilterListsAsync(),
            _ => Task.CompletedTask
        };
    }
}
