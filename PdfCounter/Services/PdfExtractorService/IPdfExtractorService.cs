using System.Collections.Generic;
using System.Collections.ObjectModel;
using iText.Kernel.Pdf;
using PdfCounter.Models;

public interface IPdfExtractorService
{
    public (ObservableCollection<PdfProperty> results, int totalPages, int totalBlankPages, int totalDocuments, HashSet<string> allDocFonts) ProcessPdf(PdfDocument pdfDocument, ObservableCollection<ExtractField> fields);
}