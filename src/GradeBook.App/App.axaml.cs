using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GradeBook.App.Services;
using GradeBook.App.ViewModels;
using GradeBook.App.Views;
using GradeBook.Core.Data;
using GradeBook.Core.Data.Repositories;
using GradeBook.Core.Reporting;
using Microsoft.Data.Sqlite;
using QuestPDF.Infrastructure;

namespace GradeBook.App;

public partial class App : Application
{
    private const int MaxListedItems = 15;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            var errorReporter = new ErrorReporter(() => mainWindow, ErrorReporter.DefaultLogPath);
            InstallGlobalExceptionHandlers(errorReporter);

            // Opening the database happens once the window is up, so any problem (damaged file, open on
            // another computer, ...) can be explained in a dialog instead of the app just failing to start.
            var started = false;
            mainWindow.Opened += async (_, _) =>
            {
                if (started)
                {
                    return;
                }

                started = true;
                await StartAsync(desktop, mainWindow, errorReporter);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InstallGlobalExceptionHandlers(ErrorReporter errorReporter)
    {
        // Covers exceptions from button commands and other UI-thread work: report them instead of
        // letting the whole app close without explanation.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            _ = errorReporter.ReportAsync(
                "Something went wrong. Anything already saved is safe; if the screen looks out of date, switch tabs or restart GradeBook.",
                e.Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            errorReporter.Log("Unobserved background task exception", e.Exception);
            e.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                errorReporter.Log("Fatal unhandled exception", ex);
            }
        };
    }

    private static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow, ErrorReporter errorReporter)
    {
        IConfirmationDialogService dialogs = new ConfirmationDialogService(() => mainWindow);
        DatabaseLockFile? lockFile = null;
        var databasePath = "(unknown)";

        void Quit()
        {
            lockFile?.Release();
            desktop.Shutdown();
        }

        try
        {
            // 1. Settings.
            var settingsStore = new AppSettingsStore();
            AppSettings settings;
            try
            {
                settings = settingsStore.Load();
            }
            catch (InvalidDataException ex)
            {
                errorReporter.Log("Loading settings", ex);
                var reset = await dialogs.ConfirmAsync(
                    "Settings Damaged",
                    $"GradeBook's settings file is damaged and can't be read:\n{settingsStore.SettingsFilePath}\n\n" +
                    "Reset the settings? Your grades aren't affected, but if you'd chosen a database folder (e.g. Nextcloud) " +
                    "you'll need to choose it again in Settings. The damaged file is kept as settings.json.damaged.",
                    confirmText: "Reset Settings",
                    cancelText: "Quit");
                if (!reset)
                {
                    Quit();
                    return;
                }

                File.Move(settingsStore.SettingsFilePath, settingsStore.SettingsFilePath + ".damaged", overwrite: true);
                settings = new AppSettings();
            }

            databasePath = settings.DatabasePath ?? SqliteConnectionFactory.ResolveDefaultDatabasePath();
            var connectionFactory = new SqliteConnectionFactory(databasePath);

            // 2. Is the gradebook already open on another computer (or in another window)?
            lockFile = new DatabaseLockFile(databasePath);
            if (!await ConfirmLockAsync(lockFile, dialogs))
            {
                lockFile = null; // not ours — never delete someone else's lock
                desktop.Shutdown();
                return;
            }

            lockFile.Acquire();
            desktop.Exit += (_, _) => lockFile.Release();

            // 3. Sync-conflict copies that may hold grades missing from the main file.
            var conflicts = SyncConflictDetector.FindConflictCopies(databasePath);
            if (conflicts.Count > 0)
            {
                var carryOn = await dialogs.ConfirmAsync(
                    "Sync Conflict Copies Found",
                    $"These files are in the gradebook folder ({Path.GetDirectoryName(databasePath)}):\n\n" +
                    ListItems(conflicts) + "\n\n" +
                    "A sync conflict copy appears when the gradebook was changed on two computers before syncing. " +
                    "It may contain grades that aren't in the file GradeBook is about to open. Check it before deleting it.",
                    confirmText: "Continue",
                    cancelText: "Quit");
                if (!carryOn)
                {
                    Quit();
                    return;
                }
            }

            // 4. Integrity check before anything writes to the file.
            if (File.Exists(databasePath))
            {
                string? problems;
                try
                {
                    problems = DatabaseInitializer.CheckIntegrity(connectionFactory);
                }
                catch (SqliteException ex)
                {
                    problems = ex.Message;
                }

                if (problems is not null)
                {
                    errorReporter.Log($"Integrity check failed for {databasePath}", new InvalidDataException(problems));
                    var newestBackup = DatabaseBackupService.FindNewestBackup(databasePath);
                    await dialogs.ShowMessageAsync(
                        "Gradebook Database Damaged",
                        $"The gradebook database appears to be damaged:\n{databasePath}\n\n{FirstLines(problems, 5)}\n\n" +
                        "GradeBook will close without changing it. " +
                        (newestBackup is null
                            ? "No backups were found next to it."
                            : $"The most recent backup is:\n{newestBackup}\n\nTo restore it, rename the damaged file, then copy the backup to gradebook.db."));
                    Quit();
                    return;
                }
            }

            DatabaseInitializer.Initialize(connectionFactory);

            // 5. Daily backup.
            try
            {
                DatabaseBackupService.CreateDailyBackupIfNeeded(connectionFactory.DatabasePath);
            }
            catch (Exception ex)
            {
                errorReporter.Log("Creating the daily backup", ex);
                await dialogs.ShowMessageAsync("Backup Failed", $"Could not create today's database backup: {ex.Message}");
            }

            IStudentRepository studentRepository = new StudentRepository(connectionFactory);
            IClassRepository classRepository = new ClassRepository(connectionFactory);
            IEnrollmentRepository enrollmentRepository = new EnrollmentRepository(connectionFactory);
            IAssignmentRepository assignmentRepository = new AssignmentRepository(connectionFactory);
            IGradeRepository gradeRepository = new GradeRepository(connectionFactory);

            // 6. One-time cleanup of grades older versions created for lessons from before a student enrolled.
            if (!settings.PreEnrollmentReviewDone)
            {
                await ReviewPreEnrollmentGradesAsync(gradeRepository, dialogs);
                try
                {
                    settingsStore.Update(s => s.PreEnrollmentReviewDone = true);
                }
                catch (Exception ex)
                {
                    errorReporter.Log("Saving the pre-enrollment review flag", ex);
                }
            }

            // 7. Build the UI.
            var classReportService = new ClassReportService(classRepository, studentRepository, enrollmentRepository, gradeRepository);
            var studentReportService = new StudentReportService(studentRepository, classRepository, enrollmentRepository, gradeRepository);
            var summaryReportService = new SummaryReportService(studentRepository, studentReportService);

            ISaveFileDialogService saveFileDialogService = new SaveFileDialogService(() => mainWindow);
            IFolderPickerService folderPickerService = new FolderPickerService(() => mainWindow);

            var classesAndStudentsViewModel = new ClassesAndStudentsViewModel(studentRepository, classRepository, enrollmentRepository, dialogs, errorReporter);
            var gradebookViewModel = new GradebookViewModel(
                classRepository, studentRepository, enrollmentRepository, assignmentRepository, gradeRepository, dialogs, errorReporter, settingsStore);
            var reportsViewModel = new ReportsViewModel(classRepository, studentRepository, classReportService, studentReportService, summaryReportService, saveFileDialogService);
            var settingsViewModel = new SettingsViewModel(settingsStore, connectionFactory.DatabasePath, Quit, folderPickerService, dialogs);
            var mainViewModel = new MainViewModel(classesAndStudentsViewModel, gradebookViewModel, reportsViewModel, settingsViewModel);

            mainWindow.DataContext = mainViewModel;
            await mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            errorReporter.Log($"Starting up with database {databasePath}", ex);
            await dialogs.ShowMessageAsync(
                "Couldn't Open the Gradebook",
                $"GradeBook couldn't open the gradebook at:\n{databasePath}\n\n{ex.Message}\n\nDetails were written to {errorReporter.LogPath}.");
            Quit();
        }
    }

    /// <summary>Returns true if it's OK to open the database (no conflicting lock, or the user chose to open anyway).</summary>
    private static async Task<bool> ConfirmLockAsync(DatabaseLockFile lockFile, IConfirmationDialogService dialogs)
    {
        var check = lockFile.Check();
        switch (check.State)
        {
            case DatabaseLockState.HeldByOtherMachine:
                var where = check.Holder is { } holder
                    ? $"on {holder.MachineName} (opened {holder.OpenedAtUtc.ToLocalTime():g})"
                    : "on another computer";
                return await dialogs.ConfirmAsync(
                    "Gradebook Open Elsewhere",
                    $"GradeBook appears to be open {where}.\n\n" +
                    "If you make changes here while it's open there, one computer's changes will be lost when the files sync. " +
                    "Close GradeBook on the other computer and let it sync first.\n\n" +
                    "Open anyway only if you're sure it isn't running there (for example, it crashed or the computer was turned off).",
                    confirmText: "Open Anyway",
                    cancelText: "Quit");

            case DatabaseLockState.HeldByThisMachineRunning:
                return await dialogs.ConfirmAsync(
                    "Gradebook Already Open",
                    "GradeBook is already open in another window on this computer. Using two windows at once can overwrite changes made in the other one.",
                    confirmText: "Open Anyway",
                    cancelText: "Quit");

            default:
                return true;
        }
    }

    private static async Task ReviewPreEnrollmentGradesAsync(IGradeRepository gradeRepository, IConfirmationDialogService dialogs)
    {
        var grades = await gradeRepository.FindPreEnrollmentUncompletedAsync();
        if (grades.Count == 0)
        {
            return;
        }

        var lines = grades.Select(g => $"{g.ClassName}: {g.StudentName} — {g.AssignmentName} ({g.AssignmentDate:MMM d})").ToList();
        var excuse = await dialogs.ConfirmAsync(
            "Lessons From Before Enrollment",
            $"{grades.Count} lesson{(grades.Count == 1 ? " is" : "s are")} marked Uncompleted (0 points) for students who weren't enrolled " +
            "in the class when the lesson was assigned. They count against those students' grades and show up as missing.\n\n" +
            ListItems(lines) + "\n\n" +
            "Mark them Excused so they don't count? Anything you've already entered a score for isn't in this list and won't change. " +
            "You won't be asked again.",
            confirmText: "Mark Excused",
            cancelText: "Leave As Is");

        if (excuse)
        {
            await gradeRepository.ExcuseGradesAsync(grades.Select(g => g.GradeId).ToList());
        }
    }

    private static string ListItems(IReadOnlyList<string> items)
    {
        var shown = items.Take(MaxListedItems).Select(i => $"• {i}");
        var more = items.Count > MaxListedItems ? $"\n…and {items.Count - MaxListedItems} more" : string.Empty;
        return string.Join("\n", shown) + more;
    }

    private static string FirstLines(string text, int count) =>
        string.Join("\n", text.Split('\n').Take(count));
}
