using TrustCabinet.Domain.Documents;
using UglyToad.PdfPig;

namespace TrustCabinet.Infrastructure.Documents;

/// <summary><see cref="IPdfInspector"/> on PdfPig (ADR-0044).</summary>
public sealed class PdfPigInspector : IPdfInspector
{
    public int CountPages(Stream pdf)
    {
        // Strict: a file the lenient parser would patch up is one pdf.js may still refuse to draw.
        using var document = PdfDocument.Open(pdf, new ParsingOptions { UseLenientParsing = false, SkipMissingFonts = true });
        return document.NumberOfPages;
    }
}
