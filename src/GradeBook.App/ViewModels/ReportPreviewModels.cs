namespace GradeBook.App.ViewModels;

/// <summary>Flattened, display-ready row for the on-screen class report preview.</summary>
public sealed record ClassReportRowDisplay(string StudentName, string GradeDisplay, int MissingCount, string QuarterBreakdownDisplay);

/// <summary>Flattened, display-ready row for the on-screen student report preview.</summary>
public sealed record StudentReportRowDisplay(string ClassName, string GradeDisplay, string MissingDisplay, string QuarterBreakdownDisplay);
