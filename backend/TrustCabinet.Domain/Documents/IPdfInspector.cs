namespace TrustCabinet.Domain.Documents;

/// <summary>
/// Opens a PDF and counts its pages, so the file rules in <see cref="DocumentFileRules"/> need no PDF library of their
/// own (SCRUM-144). Throws for a file that does not open; the rules read any exception as "not a PDF we can show".
/// The parser behind it can be ended by a hostile file (ADR-0044), so it runs only in the web app's child process or a test.
/// </summary>
public interface IPdfInspector
{
    int CountPages(Stream pdf);
}
