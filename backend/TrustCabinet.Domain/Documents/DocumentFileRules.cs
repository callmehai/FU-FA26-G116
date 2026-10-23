using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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

    private static readonly byte[] PdfHeader = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipHeader = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// The PDF spec lets the header sit anywhere in the first 1024 bytes, and readers accept that.
    /// </summary>
    private const int PdfHeaderWindow = 1024;

    /// <summary>Plenty for any real [Content_Types].xml, small enough that a crafted one cannot balloon.</summary>
    private const int ContentTypesReadLimit = 1024 * 1024;

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
            else
            {
                await using var stream = candidate.OpenRead();
                contentType = await DetectContentTypeAsync(stream, ct);
                if (contentType is null) problem = "không phải tệp PDF hoặc DOCX hợp lệ.";
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
    /// documents and templates that share DOCX's zip layout.</summary>
    public static async Task<string?> DetectContentTypeAsync(Stream stream, CancellationToken ct)
    {
        if (!stream.CanSeek) throw new ArgumentException("The file stream must be seekable.", nameof(stream));
        stream.Position = 0;
        var head = new byte[PdfHeaderWindow];
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        var window = head.AsSpan(0, read);
        if (window.IndexOf(PdfHeader) >= 0) return PdfContentType;
        if (!window.StartsWith(ZipHeader)) return null;

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
            return Encoding.UTF8.GetString(buffer, 0, length)
                .Contains("wordprocessingml.document.main+xml", StringComparison.Ordinal) ? DocxContentType : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

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
