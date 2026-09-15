using GradeBook.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace GradeBook.Reports.Pdf;

public sealed class SummaryReportPdfDocument(SummaryReportData data) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            ReportPdfLayout.ConfigurePage(page, $"Summary Report — {ReportLabels.PeriodLabel(data.Period)}");

            page.Content().PaddingTop(10).Column(column =>
                ReportPdfLayout.ComposeSummaryStudents(column, data.Students, pageBreakBetweenStudents: false));
        });
    }
}
