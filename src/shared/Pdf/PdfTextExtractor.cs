using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ContractClause.Shared.Pdf;

/// <summary>Extracts page-tagged text from contract PDFs (input to <see cref="ClauseChunker"/>).</summary>
public static class PdfTextExtractor
{
    public static List<PageText> Extract(Stream pdf)
    {
        using var document = PdfDocument.Open(pdf);
        var pages = new List<PageText>();
        foreach (var page in document.GetPages())
            pages.Add(new PageText(page.Number, ContentOrderTextExtractor.GetText(page)));
        return pages;
    }
}
