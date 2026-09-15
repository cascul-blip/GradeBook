using GradeBook.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace GradeBook.Reports.Pdf;

public sealed class StudentReportPdfDocument(StudentReportData data) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            ReportPdfLayout.ConfigurePage(page, $"Student Report — {data.StudentName} — {ReportLabels.PeriodLabel(data.Period)}");

            page.Content().PaddingTop(10).Column(column =>
            {
                column.Spacing(14);

                if (data.ClassResults.Count == 0)
                {
                    column.Item().Text("Not enrolled in any classes.");
                    return;
                }

                foreach (var classResult in data.ClassResults)
                {
                    column.Item().Column(inner =>
                    {
                        inner.Item().Text(text =>
                        {
                            text.Span(classResult.ClassName).Bold().FontSize(12);
                            text.Span("   ");
                            text.Span(ReportLabels.PercentLabel(classResult.Percentage)).FontSize(12);
                        });

                        if (classResult.QuarterBreakdown.Count > 0)
                        {
                            var breakdownText = string.Join("    ",
                                classResult.QuarterBreakdown.Select(q => $"{q.QuarterLabel}: {ReportLabels.PercentLabel(q.Percentage)}"));
                            inner.Item().PaddingTop(2).Text(breakdownText).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        }

                        var missingText = classResult.MissingAssignments.Count > 0
                            ? $"Missing: {string.Join(", ", classResult.MissingAssignments.Select(MissingAssignmentLabel))}"
                            : "Missing: None";
                        inner.Item().PaddingTop(2).Text(missingText).FontSize(9.5f);
                    });
                }
            });
        });
    }

    private static string MissingAssignmentLabel(MissingAssignmentEntry entry) =>
        $"{entry.AssignmentName} ({ReportLabels.AssignmentDateLabel(entry.AssignmentDate)})";
}
