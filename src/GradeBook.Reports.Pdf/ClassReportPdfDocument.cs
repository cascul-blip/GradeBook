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
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(column =>
            {
                column.Item().Text($"Class Report — {data.ClassName} — {ReportLabels.PeriodLabel(data.Period)}")
                    .FontSize(16).Bold();
                column.Item().Text($"Generated {DateTime.Now:MMMM d, yyyy}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });

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
                    table.Cell().Element(BodyCell).Text(row.StudentName);
                    foreach (var entry in row.QuarterBreakdown)
                    {
                        table.Cell().Element(BodyCell).Text(ReportLabels.PercentLabel(entry.Percentage));
                    }
                    table.Cell().Element(BodyCell).Text(ReportLabels.PercentLabel(row.Percentage));
                    table.Cell().Element(BodyCell).Text(row.UncompletedCount.ToString());
                }
            });
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.Bold()).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);

    private static IContainer BodyCell(IContainer container) =>
        container.PaddingVertical(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
}
