using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Documents;

public static class DocumentFileRules
{
    public const long MaxFileBytes = 50L * 1024 * 1024;   // BR-214
    public const int MaxFiles = 10;                       // BR-319
    public const long MaxTotalBytes = 200L * 1024 * 1024; // BR-319
    public const int MaxFileNameLength = 255;

    public const string PdfContentType = "application/pdf";
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const string DocxMainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private static readonly XNamespace ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

    private static readonly byte[] PdfHeader = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipHeader = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] EndOfCentralDirectory = [0x50, 0x4B, 0x05, 0x06];

    /// <summary>Plenty for any real [Content_Types].xml, small enough that a crafted one cannot balloon.</summary>
    private const int ContentTypesReadLimit = 1024 * 1024;

    /// <summary>
    /// A real DOCX has a few dozen entries. ZipArchive loads the whole central directory into
    /// memory, so a crafted 50 MB one with half a million entries would cost ~200 MB per file.
    /// </summary>
    private const int MaxZipEntries = 5000;
    private const uint MaxCentralDirectoryBytes = 1024 * 1024;

    public static string StorageKey(Guid storedFileId) => $"stored-files/{storedFileId:D}";

    /// <summary>
    /// <c>&lt;title&gt; – dd/mm/yyyy</c> from the planned start in Vietnam time (BR-192, ADR-0020 §7).
    /// Vietnam has no daylight saving, so a fixed +07:00 is exact.
    /// </summary>
    public static string UploadFolderName(Meeting meeting)
        => $"{meeting.Title} – {meeting.PlannedStartTime.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>Who may upload right now: the meeting's current Chair or Secretary, until it ends (BR-184, BR-195).</summary>
    public static DocumentUploadFailure UploadAccess(Meeting? meeting, Guid actorId, bool actorActive)
    {
        if (!actorActive) return DocumentUploadFailure.Inactive;
        if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return DocumentUploadFailure.NotFound;
        if (!ParticipantRules.IsManager(meeting, actorId)) return DocumentUploadFailure.Forbidden;
        return meeting.Status == MeetingStatus.Ended ? DocumentUploadFailure.Ended : DocumentUploadFailure.None;
    }

    /// <summary>
    /// Checks the submission limits, then every file by its actual bytes (BR-187, BR-214, BR-215,
    /// BR-319), and hashes what passes. Reports every bad file, not just the first. Streams must
    /// be seekable — ASP.NET Core buffers form files to disk, so they are.
    /// </summary>
    public static async Task<DocumentUploadOutcome> ValidateAsync(IReadOnlyList<DocumentUploadCandidate> candidates,
        CancellationToken ct)
    {
        if (candidates.Count == 0)
            return new(DocumentUploadFailure.Invalid, Errors: new Dictionary<string, string[]> { ["files"] = ["Chưa chọn tệp nào."] });
        if (candidates.Count > MaxFiles || candidates.Sum(c => c.Length) > MaxTotalBytes)
            return new(DocumentUploadFailure.TooMany);

        var errors = new Dictionary<string, string[]>();
        var files = new List<DocumentUploadFile>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var name = NormalizeFileName(candidate.FileName);
            var label = name ?? $"Tệp thứ {index + 1}";
            string? problem = null;
            string? contentType = null;
            string? sha256 = null;
            if (name is null) problem = "tên tệp không hợp lệ.";
            else if (candidate.Length == 0) problem = "tệp rỗng.";
            else if (candidate.Length > MaxFileBytes) problem = "tệp lớn hơn 50 MB.";
            else if (ExpectedContentType(name) is not { } expected) problem = "chỉ nhận tệp .pdf hoặc .docx.";
            else
            {
                await using var stream = candidate.OpenRead();
                contentType = await DetectContentTypeAsync(stream, ct);
                // The name must agree with the bytes: the file is downloaded under this name later,
                // and a PDF-looking HTML page must not come back as "x.html" from our origin.
                if (contentType != expected) problem = "không phải tệp PDF hoặc DOCX hợp lệ.";
                else
                {
                    stream.Position = 0;
                    sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
                }
            }

            if (problem is not null)
            {
                errors[$"files[{index}]"] = [$"{label}: {problem}"];
                continue;
            }

            files.Add(new(name!, contentType!, candidate.Length, sha256!,
                contentType == PdfContentType ? ParsePageCount(candidate.PageCount) : null, candidate.OpenRead));
        }

        return errors.Count > 0 ? new(DocumentUploadFailure.Invalid, Errors: errors) : new(Files: files);
    }

    /// <summary>PDF or DOCX from the bytes alone; null for anything else, including macro-enabled
    /// documents and templates that share DOCX's zip layout, and zips too large to inspect safely.</summary>
    public static async Task<string?> DetectContentTypeAsync(Stream stream, CancellationToken ct)
    {
        if (!stream.CanSeek) throw new ArgumentException("The file stream must be seekable.", nameof(stream));
        stream.Position = 0;
        var head = new byte[PdfHeader.Length];
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        // At offset 0 only. The spec tolerates junk before the header, but then any HTML page with
        // "%PDF-" in a comment would pass as a PDF.
        if (head.AsSpan(0, read).SequenceEqual(PdfHeader)) return PdfContentType;
        if (!head.AsSpan(0, read).StartsWith(ZipHeader) || !await CentralDirectoryIsSmallAsync(stream, ct)) return null;

        stream.Position = 0;
        try
        {
            // Opening a zip reads only its central directory; just [Content_Types].xml is inflated.
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.GetEntry("word/document.xml") is null) return null;
            var types = zip.GetEntry("[Content_Types].xml");
            if (types is null) return null;
            await using var entry = types.Open();
            var buffer = new byte[ContentTypesReadLimit];
            var length = await entry.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
            // The main part's own declaration, not any mention: a DOCM declares a macro-enabled type here.
            var main = XDocument.Parse(Encoding.UTF8.GetString(buffer, 0, length)).Root?
                .Elements(ContentTypesNamespace + "Override")
                .FirstOrDefault(e => string.Equals((string?)e.Attribute("PartName"), "/word/document.xml", StringComparison.OrdinalIgnoreCase));
            return (string?)main?.Attribute("ContentType") == DocxMainContentType ? DocxContentType : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the end-of-central-directory record (the last 22 bytes plus a comment of at most
    /// 64 KB) and refuses a directory too large to load. ZIP64 (0xFFFF entries) is refused too.
    /// </summary>
    private static async Task<bool> CentralDirectoryIsSmallAsync(Stream stream, CancellationToken ct)
    {
        const int recordLength = 22;
        var tail = (int)Math.Min(stream.Length, recordLength + ushort.MaxValue);
        if (tail < recordLength) return false;
        stream.Position = stream.Length - tail;
        var buffer = new byte[tail];
        await stream.ReadExactlyAsync(buffer, ct);
        var at = buffer.AsSpan(0, tail - recordLength + EndOfCentralDirectory.Length).LastIndexOf(EndOfCentralDirectory);
        if (at < 0) return false;
        var entries = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(at + 10));
        var size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(at + 12));
        return entries <= MaxZipEntries && size <= MaxCentralDirectoryBytes;
    }

    private static string? ExpectedContentType(string fileName)
        => fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? PdfContentType
            : fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ? DocxContentType : null;

    /// <summary>The last path segment, trimmed; null when nothing usable is left.</summary>
    public static string? NormalizeFileName(string? fileName)
    {
        var name = fileName?.Split('/', '\\')[^1].Trim();
        return string.IsNullOrEmpty(name) || name.Length > MaxFileNameLength || name.Any(char.IsControl) ? null : name;
    }

    /// <summary>The browser counts pages with pdf.js; a value it could not produce is stored as unknown.</summary>
    private static int? ParsePageCount(string? value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : null;
}
