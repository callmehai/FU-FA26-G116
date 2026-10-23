using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Web.Tests.Documents;

public sealed class DocumentFileRulesTests
{
    private static Task<string?> Detect(byte[] bytes) => DocumentFileRules.DetectContentTypeAsync(new MemoryStream(bytes), default);

    [Fact]
    public async Task Pdf_and_docx_are_recognised_by_their_bytes()
    {
        Assert.Equal(DocumentFileRules.PdfContentType, await Detect(SampleFiles.Pdf()));
        Assert.Equal(DocumentFileRules.DocxContentType, await Detect(SampleFiles.Docx()));
    }

    [Fact]
    public async Task Everything_else_is_rejected()
    {
        Assert.Null(await Detect(SampleFiles.Png()));
        // The header must open the file: otherwise an HTML page with "%PDF-" in a comment passes.
        Assert.Null(await Detect([.. new byte[1], .. SampleFiles.Pdf()]));
        Assert.Null(await Detect("<html><script>alert(1)</script><!-- %PDF-1.4 -->"u8.ToArray()));
        // A DOCM that merely mentions the DOCX type somewhere else in [Content_Types].xml.
        Assert.Null(await Detect(SampleFiles.Docx("application/vnd.ms-word.document.macroEnabled.main+xml",
            extraTypes: $"<!-- {SampleFiles.DocxMain} --><Override PartName=\"/x.xml\" ContentType=\"{SampleFiles.DocxMain}\"/>")));
        // ZipArchive loads the whole central directory: one too large to inspect cheaply is refused.
        Assert.Null(await Detect(SampleFiles.Docx(padding: 5001)));
        Assert.Equal(DocumentFileRules.DocxContentType, await Detect(SampleFiles.Docx(padding: 100)));
        Assert.Null(await Detect(SampleFiles.Docx("application/vnd.ms-word.document.macroEnabled.main+xml")));
        Assert.Null(await Detect(SampleFiles.Docx("application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml")));
        Assert.Null(await Detect(SampleFiles.Docx(withDocument: false)));
        Assert.Null(await Detect([0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5]));   // zip header, no zip
        Assert.Null(await Detect([]));
    }

    [Theory]
    [InlineData("C:\\Users\\me\\Báo cáo.pdf", "Báo cáo.pdf")]
    [InlineData("  thư mục/tờ trình.docx ", "tờ trình.docx")]
    [InlineData("", null)]
    [InlineData("a/", null)]
    [InlineData("tab\there.pdf", null)]
    public void File_names_keep_only_the_last_segment(string input, string? expected)
        => Assert.Equal(expected, DocumentFileRules.NormalizeFileName(input));

    [Fact]
    public void A_256_character_file_name_is_too_long()
    {
        Assert.NotNull(DocumentFileRules.NormalizeFileName(new string('a', 251) + ".pdf"));
        Assert.Null(DocumentFileRules.NormalizeFileName(new string('a', 252) + ".pdf"));
    }

    [Fact]
    public void Folder_date_is_the_planned_start_in_vietnam_time()
    {
        var meeting = new Meeting { Title = "Họp", PlannedStartTime = new DateTimeOffset(2026, 10, 23, 17, 0, 0, TimeSpan.Zero) };
        Assert.Equal("Họp – 24/10/2026", DocumentFileRules.UploadFolderName(meeting));
        meeting.PlannedStartTime = new DateTimeOffset(2026, 10, 23, 16, 59, 0, TimeSpan.Zero);
        Assert.Equal("Họp – 23/10/2026", DocumentFileRules.UploadFolderName(meeting));
    }

    [Theory]
    [InlineData("poly.html", true)]     // real PDF bytes under a name that would download as HTML
    [InlineData("x.pdf.exe", true)]
    [InlineData("..", true)]
    [InlineData("du-thao.pdf", false)]  // DOCX bytes named .pdf
    [InlineData("BAO-CAO.PDF", true)]   // the extension check ignores case: this one passes
    public async Task The_name_must_be_pdf_or_docx_and_agree_with_the_bytes(string name, bool pdfBytes)
    {
        var bytes = pdfBytes ? SampleFiles.Pdf() : SampleFiles.Docx();
        var result = await DocumentFileRules.ValidateAsync([new(name, bytes.Length, () => new MemoryStream(bytes), null)], default);
        Assert.Equal(name == "BAO-CAO.PDF" ? DocumentUploadFailure.None : DocumentUploadFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task Total_size_over_200_MB_is_checked_before_any_file_is_read()
    {
        Stream Unread() => throw new InvalidOperationException("Must not be read.");
        var five = Enumerable.Range(0, 5).Select(i => new DocumentUploadCandidate($"{i}.pdf", 41L * 1024 * 1024, Unread, null)).ToArray();
        Assert.Equal(DocumentUploadFailure.TooMany, (await DocumentFileRules.ValidateAsync(five, default)).Failure);

        var exactly = Enumerable.Range(0, 4).Select(i => new DocumentUploadCandidate($"{i}.pdf", 50L * 1024 * 1024, () => new MemoryStream(SampleFiles.Pdf()), null)).ToArray();
        Assert.NotEqual(DocumentUploadFailure.TooMany, (await DocumentFileRules.ValidateAsync(exactly, default)).Failure);
    }

    [Fact]
    public async Task Every_bad_file_is_reported_and_page_counts_are_kept_for_pdf_only()
    {
        var bad = await DocumentFileRules.ValidateAsync([
            new("ok.pdf", 10, () => new MemoryStream(SampleFiles.Pdf()), "4"),
            new("rong.pdf", 0, () => new MemoryStream(), null),
            new("anh.pdf", 16, () => new MemoryStream(SampleFiles.Png()), null),
        ], default);
        Assert.Equal(DocumentUploadFailure.Invalid, bad.Failure);
        Assert.Equal(["files[1]", "files[2]"], bad.Errors!.Keys.Order());

        var good = await DocumentFileRules.ValidateAsync([
            new("a.pdf", 10, () => new MemoryStream(SampleFiles.Pdf()), "4"),
            new("b.pdf", 10, () => new MemoryStream(SampleFiles.Pdf()), "-1"),
            new("c.docx", 10, () => new MemoryStream(SampleFiles.Docx()), "9"),
        ], default);
        Assert.Equal([4, null, null], good.Files!.Select(f => f.PageCount));
    }
}
