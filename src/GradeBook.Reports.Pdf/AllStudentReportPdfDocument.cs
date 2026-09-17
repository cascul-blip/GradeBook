using GradeBook.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace GradeBook.Reports.Pdf;

/// <summary>Same content as SummaryReportPdfDocument, but each student starts on its own PDF page.</summary>
public sealed class AllStudentReportPdfDocument(SummaryReportData data) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            ReportPdfLayout.ConfigurePage(page, $"All Student Report — {ReportLabels.PeriodLabel(data.Period)}");

            page.Content().PaddingTop(10).Column(column =>
                ReportPdfLayout.ComposeSummaryStudents(column, data.Students, pageBreakBetweenStudents: true, showMissingCount: true));
        });
    }
}
