using CommunityToolkit.Mvvm.ComponentModel;

namespace GradeBook.App.ViewModels;

public partial class MainViewModel(
    ClassesAndStudentsViewModel classesAndStudents,
    GradebookViewModel gradebook,
    ReportsViewModel reports,
    SettingsViewModel settings) : ViewModelBase
{
    public ClassesAndStudentsViewModel ClassesAndStudents { get; } = classesAndStudents;
    public GradebookViewModel Gradebook { get; } = gradebook;
    public ReportsViewModel Reports { get; } = reports;
    public SettingsViewModel Settings { get; } = settings;

    [ObservableProperty]
    private int _selectedTabIndex;

    public async Task InitializeAsync()
    {
        await ClassesAndStudents.InitializeAsync();
        await Gradebook.InitializeAsync();
        await Reports.InitializeAsync();
        Settings.Initialize();
    }

    // Classes/students are added on tab 0; the Gradebook (1) and Reports (2) tabs cache their own
    // class/student dropdown lists, so refresh those whenever the user switches into that tab.
    partial void OnSelectedTabIndexChanged(int value)
    {
        _ = value switch
        {
            1 => Gradebook.RefreshClassesAsync(),
            2 => Reports.RefreshFilterListsAsync(),
            _ => Task.CompletedTask
        };
    }
}
