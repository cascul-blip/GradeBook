using GradeBook.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(column =>
            {
                column.Item().Text($"All Student Report — {ReportLabels.PeriodLabel(data.Period)}")
                    .FontSize(16).Bold();
                column.Item().Text($"Generated {DateTime.Now:MMMM d, yyyy}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingTop(10).Column(column =>
            {
                if (data.Students.Count == 0)
                {
                    column.Item().Text("No students found.");
                    return;
                }

                for (var i = 0; i < data.Students.Count; i++)
                {
                    var student = data.Students[i];

                    column.Item().Column(inner =>
                    {
                        inner.Item().Text(student.StudentName).Bold().FontSize(12);

                        inner.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });

                            foreach (var classGrade in student.Classes)
                            {
                                table.Cell().Element(BodyCell).Text(classGrade.ClassName);
                                table.Cell().Element(BodyCell).Text(ReportLabels.PercentLabel(classGrade.Percentage));
                            }
                        });
                    });

                    if (i < data.Students.Count - 1)
                    {
                        column.Item().PageBreak();
                    }
                }
            });
        });
    }

    private static IContainer BodyCell(IContainer container) =>
        container.PaddingVertical(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
}
