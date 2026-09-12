using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace GradeBook.Reports.Pdf;

public static class ReportPdfExporter
{
    public static Task ExportAsync(IDocument document, Stream output) =>
        Task.Run(() => document.GeneratePdf(output));
}
