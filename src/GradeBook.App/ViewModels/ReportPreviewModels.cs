namespace GradeBook.App.ViewModels;

/// <summary>Flattened, display-ready row for the on-screen class report preview.</summary>
public sealed record ClassReportRowDisplay(string StudentName, string GradeDisplay, int MissingCount, string QuarterBreakdownDisplay);

/// <summary>Flattened, display-ready row for the on-screen student report preview.</summary>
public sealed record StudentReportRowDisplay(string ClassName, string GradeDisplay, string MissingDisplay, string QuarterBreakdownDisplay);

/// <summary>One class/grade line under a student in the on-screen summary report preview.</summary>
public sealed record SummaryClassGradeDisplay(string ClassName, string GradeDisplay);

/// <summary>One student's block (name + their classes) in the on-screen summary report preview.</summary>
public sealed record SummaryStudentDisplay(string StudentName, IReadOnlyList<SummaryClassGradeDisplay> Classes);
