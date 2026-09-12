using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GradeBook.App.Services;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Models;
using GradeBook.Core.Reporting;
using GradeBook.Reports.Pdf;

namespace GradeBook.App.ViewModels;

public partial class ReportsViewModel(
    IClassRepository classRepository,
    IStudentRepository studentRepository,
    ClassReportService classReportService,
    StudentReportService studentReportService,
    ISaveFileDialogService saveFileDialogService) : ViewModelBase
{
    public static IReadOnlyList<ReportPeriod> Periods { get; } =
    [
        ReportPeriod.Quarter1, ReportPeriod.Quarter2, ReportPeriod.Quarter3, ReportPeriod.Quarter4,
        ReportPeriod.Semester1, ReportPeriod.Semester2, ReportPeriod.Final
    ];

    public static IReadOnlyList<ReportTargetType> ReportTypes { get; } =
        [ReportTargetType.ClassReport, ReportTargetType.StudentReport];

    public ObservableCollection<SchoolClass> Classes { get; } = [];
    public ObservableCollection<Student> Students { get; } = [];

    [ObservableProperty]
    private ReportTargetType _selectedReportType = ReportTargetType.ClassReport;

    [ObservableProperty]
    private SchoolClass? _selectedClass;

    [ObservableProperty]
    private Student? _selectedStudent;

    [ObservableProperty]
    private ReportPeriod _selectedPeriod = ReportPeriod.Quarter1;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public Task InitializeAsync() => RefreshFilterListsAsync();

    /// <summary>
    /// Re-reads the class/student lists from the database. They're added/renamed on the Classes &amp;
    /// Students tab, so this needs to run again whenever the Reports tab is shown, not just once at startup.
    /// </summary>
    public async Task RefreshFilterListsAsync()
    {
        var previousClassId = SelectedClass?.Id;
        var previousStudentId = SelectedStudent?.Id;

        var classes = await classRepository.GetAllAsync(includeInactive: true);
        Classes.Clear();
        foreach (var schoolClass in classes)
        {
            Classes.Add(schoolClass);
        }

        var students = await studentRepository.GetAllAsync(includeInactive: true);
        Students.Clear();
        foreach (var student in students)
        {
            Students.Add(student);
        }

        SelectedClass = Classes.FirstOrDefault(c => c.Id == previousClassId) ?? Classes.FirstOrDefault();
        SelectedStudent = Students.FirstOrDefault(s => s.Id == previousStudentId) ?? Students.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        StatusMessage = null;

        if (SelectedReportType == ReportTargetType.ClassReport && SelectedClass is null)
        {
            StatusMessage = "Select a class first.";
            return;
        }

        if (SelectedReportType == ReportTargetType.StudentReport && SelectedStudent is null)
        {
            StatusMessage = "Select a student first.";
            return;
        }

        IsBusy = true;
        try
        {
            QuestPDF.Infrastructure.IDocument document;
            string suggestedFileName;

            if (SelectedReportType == ReportTargetType.ClassReport)
            {
                var data = await classReportService.BuildReportAsync(SelectedClass!.Id, SelectedPeriod);
                document = new ClassReportPdfDocument(data);
                suggestedFileName = $"{SanitizeFileName(data.ClassName)}-{SelectedPeriod}.pdf";
            }
            else
            {
                var data = await studentReportService.BuildReportAsync(SelectedStudent!.Id, SelectedPeriod);
                document = new StudentReportPdfDocument(data);
                suggestedFileName = $"{SanitizeFileName(data.StudentName)}-{SelectedPeriod}.pdf";
            }

            var path = await saveFileDialogService.PickSaveFileAsync(suggestedFileName, "Export Report");
            if (path is null)
            {
                return;
            }

            await using var stream = File.Create(path);
            await ReportPdfExporter.ExportAsync(document, stream);
            StatusMessage = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
