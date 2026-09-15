using GradeBook.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GradeBook.Reports.Pdf;

public sealed class ClassReportPdfDocument(ClassReportData data) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            ReportPdfLayout.ConfigurePage(page, $"Class Report — {data.ClassName} — {ReportLabels.PeriodLabel(data.Period)}");

            page.Content().PaddingTop(10).Table(table =>
            {
                var breakdownCount = data.Rows.Count > 0 ? data.Rows[0].QuarterBreakdown.Count : 0;

                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    for (var i = 0; i < breakdownCount; i++)
                    {
                        columns.RelativeColumn(1.2f);
                    }
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.2f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Student");
                    for (var i = 0; i < breakdownCount; i++)
                    {
                        header.Cell().Element(HeaderCell).Text($"{data.Rows[0].QuarterBreakdown[i].QuarterLabel} %");
                    }
                    header.Cell().Element(HeaderCell).Text(breakdownCount > 0 ? "Average %" : "Grade %");
                    header.Cell().Element(HeaderCell).Text("Missing");
                });

                foreach (var row in data.Rows)
                {
                    table.Cell().Element(ReportPdfLayout.BodyCell).Text(row.StudentName);
                    foreach (var entry in row.QuarterBreakdown)
                    {
                        table.Cell().Element(ReportPdfLayout.BodyCell).Text(ReportLabels.PercentLabel(entry.Percentage));
                    }
                    table.Cell().Element(ReportPdfLayout.BodyCell).Text(ReportLabels.PercentLabel(row.Percentage));
                    table.Cell().Element(ReportPdfLayout.BodyCell).Text(row.UncompletedCount.ToString());
                }
            });
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.Bold()).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
}
