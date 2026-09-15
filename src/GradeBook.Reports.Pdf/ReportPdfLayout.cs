using GradeBook.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GradeBook.Reports.Pdf;

/// <summary>Page/cell styling and per-student rendering shared across the report PDF documents.</summary>
internal static class ReportPdfLayout
{
    public static void ConfigurePage(PageDescriptor page, string title)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(x => x.FontSize(10));

        page.Header().Column(column =>
        {
            column.Item().Text(title).FontSize(16).Bold();
            column.Item().Text($"Generated {DateTime.Now:MMMM d, yyyy}")
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    public static IContainer BodyCell(IContainer container) =>
        container.PaddingVertical(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

    /// <summary>Bold name + bordered Class/Grade table per student, used by Summary and All Student reports.</summary>
    public static void ComposeSummaryStudents(ColumnDescriptor column, IReadOnlyList<SummaryStudentEntry> students, bool pageBreakBetweenStudents)
    {
        if (students.Count == 0)
        {
            column.Item().Text("No students found.");
            return;
        }

        if (!pageBreakBetweenStudents)
        {
            column.Spacing(16);
        }

        for (var i = 0; i < students.Count; i++)
        {
            var student = students[i];

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

            if (pageBreakBetweenStudents && i < students.Count - 1)
            {
                column.Item().PageBreak();
            }
        }
    }
}
