using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GradeBook.App.Services;
using GradeBook.App.ViewModels;
using GradeBook.App.Views;
using GradeBook.Core.Data;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Reporting;
using QuestPDF.Infrastructure;

namespace GradeBook.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var settingsStore = new AppSettingsStore();
            var databasePath = settingsStore.Load().DatabasePath ?? SqliteConnectionFactory.ResolveDefaultDatabasePath();

            var connectionFactory = new SqliteConnectionFactory(databasePath);
            DatabaseInitializer.Initialize(connectionFactory);

            IStudentRepository studentRepository = new StudentRepository(connectionFactory);
            IClassRepository classRepository = new ClassRepository(connectionFactory);
            IEnrollmentRepository enrollmentRepository = new EnrollmentRepository(connectionFactory);
            IAssignmentRepository assignmentRepository = new AssignmentRepository(connectionFactory);
            IGradeRepository gradeRepository = new GradeRepository(connectionFactory);

            var classReportService = new ClassReportService(classRepository, studentRepository, enrollmentRepository, gradeRepository);
            var studentReportService = new StudentReportService(studentRepository, classRepository, enrollmentRepository, gradeRepository);

            var mainWindow = new MainWindow();
            ISaveFileDialogService saveFileDialogService = new SaveFileDialogService(() => mainWindow);
            IConfirmationDialogService confirmationDialogService = new ConfirmationDialogService(() => mainWindow);
            IFolderPickerService folderPickerService = new FolderPickerService(() => mainWindow);

            var classesAndStudentsViewModel = new ClassesAndStudentsViewModel(studentRepository, classRepository, enrollmentRepository, confirmationDialogService);
            var gradebookViewModel = new GradebookViewModel(classRepository, studentRepository, enrollmentRepository, assignmentRepository, gradeRepository);
            var reportsViewModel = new ReportsViewModel(classRepository, studentRepository, classReportService, studentReportService, saveFileDialogService);
            var settingsViewModel = new SettingsViewModel(settingsStore, folderPickerService, confirmationDialogService);
            var mainViewModel = new MainViewModel(classesAndStudentsViewModel, gradebookViewModel, reportsViewModel, settingsViewModel);

            mainWindow.DataContext = mainViewModel;
            desktop.MainWindow = mainWindow;

            _ = mainViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
