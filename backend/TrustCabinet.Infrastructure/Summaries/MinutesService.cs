using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// SCRUM-32 — the Secretary's draft of the minutes: built from the meeting's records and its AI draft, then
/// edited and saved as often as needed. Sending it for approval and issuing it are SCRUM-33; until then a draft is the
/// Chair's and the Secretary's alone.
/// </summary>
public sealed class MinutesService(TrustCabinetDbContext db, IMeetingRepository meetings, TimeProvider clock,
    ILogger<MinutesService> logger) : IMinutesService
{
    private static readonly TimeZoneInfo Hanoi = SummaryWriter.FindHanoi();

    public async Task<MinutesOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            return new(Data: await StateAsync(meeting, actorId, ct));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not read the minutes of meeting {MeetingId}", meetingId);
            return new(MinutesFailure.ReadFailed);
        }
    }

    public async Task<MinutesOutcome> CreateAsync(Guid meetingId, Guid actorId, bool rebuild, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            if (!SummaryService.Manages(meeting, actorId)) return new(MinutesFailure.Forbidden);
            if (meeting.Status is not (MeetingStatus.InProgress or MeetingStatus.Ended)) return new(MinutesFailure.WrongMeetingStatus);
            var current = await db.MeetingMinutes.Where(m => m.MeetingId == meetingId)
                .OrderByDescending(m => m.RevisionNumber).FirstOrDefaultAsync(ct);
            if (current is not null && current.Status != MinutesStatus.Draft) return new(MinutesFailure.Locked);
            if (current is not null && !rebuild) return new(MinutesFailure.Exists);

            var (draft, draftAt) = await DraftAsync(meetingId, ct);
            var content = MinutesHtml.FromLines(MinutesTemplate.Build(await RecordsAsync(meeting, ct), draft, Hanoi));
            var now = clock.GetUtcNow();
            if (current is null)
            {
                current = new MeetingMinutes { MeetingId = meetingId, CreatedById = actorId, CreatedAt = now };
                db.MeetingMinutes.Add(current);
            }
            current.Content = content;
            current.BuiltFromDraftAt = draftAt;
            current.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return new(Data: await StateAsync(meeting, actorId, ct));
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two first builds at once: the unique meeting index lets one through. Any other failure is a failed save.
            logger.LogWarning(error, "Concurrent minutes creation for meeting {MeetingId}", meetingId);
            return new(MinutesFailure.Exists);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not build the minutes of meeting {MeetingId}", meetingId);
            return new(MinutesFailure.SaveFailed);
        }
    }

    public async Task<MinutesOutcome> SaveAsync(Guid minutesId, Guid actorId, string? content, DateTimeOffset? loadedAt,
        CancellationToken ct = default)
    {
        try
        {
            var row = await db.MeetingMinutes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == minutesId, ct);
            if (row is null) return new(MinutesFailure.NotFound);
            var (meeting, failure) = await ReadableAsync(row.MeetingId, actorId, ct);
            if (meeting is null) return new(failure);
            // Someone who may not edit learns nothing about a draft they may not read.
            if (!SummaryService.Manages(meeting, actorId)) return new(row.Status == MinutesStatus.Draft ? MinutesFailure.NotFound : MinutesFailure.Forbidden);
            if (row.Status != MinutesStatus.Draft) return new(MinutesFailure.Locked);
            // Only the allow-list is stored (ADR-0055): whatever else the request carries never reaches a reader. A
            // document of empty paragraphs says nothing, so it is refused like an empty one.
            // Nesting deeper than any document the editor writes is refused before it is walked (MinutesHtml.MaxDepth).
            var html = content is null || content.Length > MinutesLimits.Content || MinutesHtml.TooDeep(content)
                ? null : MinutesHtml.Sanitize(content);
            if (html is null || html.Length > MinutesLimits.Content || !MinutesHtml.HasText(html))
                return new(MinutesFailure.Invalid, Errors: new Dictionary<string, string[]>
                {
                    ["content"] = [$"Nhập nội dung biên bản, tối đa {MinutesLimits.Content:N0} ký tự."],
                });
            // Last writer loses: two people editing the same draft must not silently erase each other. The check and the
            // write are one UPDATE, so a save that lands between this read and this write makes this one stale. The 1 ms
            // window absorbs the precision PostgreSQL drops from the time the client was given.
            if (loadedAt is null) return new(MinutesFailure.Stale);
            var from = loadedAt.Value.AddMilliseconds(-1);
            var to = loadedAt.Value.AddMilliseconds(1);
            var now = clock.GetUtcNow();
            var saved = await db.MeetingMinutes
                .Where(m => m.Id == minutesId && m.Status == MinutesStatus.Draft && m.UpdatedAt >= from && m.UpdatedAt <= to)
                .ExecuteUpdateAsync(m => m.SetProperty(x => x.Content, html).SetProperty(x => x.UpdatedAt, now), ct);
            if (saved == 0) return new(MinutesFailure.Stale);
            return new(Data: await StateAsync(meeting, actorId, ct));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not save minutes {MinutesId}", minutesId);
            return new(MinutesFailure.SaveFailed);
        }
    }

    public async Task<MinutesOutcome> ExportAsync(Guid minutesId, Guid actorId, MinutesExportFormat format,
        CancellationToken ct = default)
    {
        try
        {
            var row = await db.MeetingMinutes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == minutesId, ct);
            if (row is null) return new(MinutesFailure.NotFound);
            var (meeting, failure) = await ReadableAsync(row.MeetingId, actorId, ct);
            if (meeting is null) return new(failure);
            if (!SummaryService.Manages(meeting, actorId)) return new(row.Status == MinutesStatus.Draft ? MinutesFailure.NotFound : MinutesFailure.Forbidden);
            if (meeting.Sensitivity == Sensitivity.Secret) return new(MinutesFailure.SecretMeeting);
            var name = string.Concat($"Biên bản - {meeting.Title}".Split(Path.GetInvalidFileNameChars()));
            var stem = name.Length > 120 ? name[..120] : name;
            return new(Data: format == MinutesExportFormat.Pdf
                ? new MinutesFile($"{stem}.pdf", MinutesPdf.Build(row.Content), "application/pdf")
                : new MinutesFile($"{stem}.docx", MinutesDocx.Build(row.Content),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not export minutes {MinutesId}", minutesId);
            return new(MinutesFailure.ReadFailed);
        }
    }

    private async Task<MinutesState> StateAsync(Meeting meeting, Guid actorId, CancellationToken ct)
    {
        var manages = SummaryService.Manages(meeting, actorId);
        var row = await db.MeetingMinutes.AsNoTracking().Include(m => m.CreatedBy)
            .Where(m => m.MeetingId == meeting.Id)
            .OrderByDescending(m => m.RevisionNumber)
            .FirstOrDefaultAsync(ct);
        var visible = row is not null && (manages || row.Status == MinutesStatus.Issued);
        var draftChangedAt = manages ? (await DraftAsync(meeting.Id, ct)).ChangedAt : null;
        return new(visible ? new MinutesView(row!.Id, row.RevisionNumber, row.Status.ToString(),
            MinutesHtml.Normalize(row.Content), row.CreatedAt,
            row.UpdatedAt, new(row.CreatedBy.Id, row.CreatedBy.FullName), manages ? row.BuiltFromDraftAt : null) : null,
            manages, draftChangedAt);
    }

    /// <summary>
    /// The Dự thảo as it stands, reviewed or not: eCab's Compose takes the meeting's outline whatever its state. Both are
    /// the Chair's and the Secretary's alone until they move on (the draft is reviewed, the minutes issued), so nobody
    /// reads unreviewed AI text through the minutes. A rerun in progress keeps the last finished draft. ChangedAt is when
    /// it was last written or edited, the stamp the minutes keep to tell when the draft has moved on.
    /// </summary>
    private async Task<(SummaryDraft? Draft, DateTimeOffset? ChangedAt)> DraftAsync(Guid meetingId, CancellationToken ct)
    {
        var row = await db.MeetingSummaries.AsNoTracking().FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct);
        var draft = row is null ? null : SummaryService.Read(row);
        if (row is null || draft is null) return (null, null);
        return (draft, row.EditedAt is { } edited && edited > row.GeneratedAt ? edited : row.GeneratedAt);
    }

    private async Task<MinutesRecords> RecordsAsync(Meeting meeting, CancellationToken ct)
    {
        var people = await db.MeetingParticipants.AsNoTracking()
            .Where(p => p.MeetingId == meeting.Id)
            .Where(ParticipantRules.ActiveExpression)
            .OrderBy(p => p.Role).ThenBy(p => p.User.FullName)
            .Select(p => new { p.Role, p.Attendance, p.User.FullName })
            .ToListAsync(ct);
        var guests = await db.MeetingGuestInvitations.AsNoTracking()
            .Where(g => g.MeetingId == meeting.Id && g.RevokedAt == null)
            .Select(g => new { Name = g.SelfDeclaredDisplayName ?? g.InvitedName, g.Attendance })
            .ToListAsync(ct);
        var lines = people.Select(p => new MinutesPersonLine(p.FullName, RoleName(p.Role),
                p.Attendance is AttendanceStatus.Absent or AttendanceStatus.Excused))
            .Concat(guests.Select(g => new MinutesPersonLine(g.Name, "Khách mời",
                g.Attendance is AttendanceStatus.Absent or AttendanceStatus.Excused)))
            .ToList();
        var agenda = await db.AgendaItems.AsNoTracking()
            .Where(a => a.MeetingId == meeting.Id)
            .OrderBy(a => a.Position)
            .Select(a => new AgendaFacts(a.Id, a.Title,
                a.Presenter != null ? a.Presenter.FullName
                    : a.PresenterGuestInvitation != null ? a.PresenterGuestInvitation.SelfDeclaredDisplayName ?? a.PresenterGuestInvitation.InvitedName
                    : a.LegacyPresenterName != "" ? a.LegacyPresenterName : null,
                a.Conclusion, a.ActualStartedAt, a.ActualEndedAt))
            .ToListAsync(ct);
        var place = meeting.Mode switch
        {
            MeetingMode.Online => "Phòng họp trực tuyến TrustCabinet",
            MeetingMode.Hybrid => $"{meeting.Location.Trim()} và phòng họp trực tuyến TrustCabinet".Trim(),
            _ => meeting.Location,
        };
        return new(meeting.Title, meeting.ActualStartedAt ?? meeting.PlannedStartTime, meeting.ActualEndedAt, place,
            people.FirstOrDefault(p => p.Role == MeetingRole.Chair)?.FullName,
            people.FirstOrDefault(p => p.Role == MeetingRole.Secretary)?.FullName,
            lines, agenda, await SummaryService.VoteLinesAsync(db, meeting.Id, ct), await SecretaryNotesAsync(meeting, ct));
    }

    /// <summary>The current Secretary's notes (SCRUM-30, BR-402); the Chair reads them but writes none.</summary>
    private async Task<IReadOnlyList<MinutesNote>> SecretaryNotesAsync(Meeting meeting, CancellationToken ct)
    {
        var secretaries = meeting.Participants
            .Where(p => p.Role == MeetingRole.Secretary && ParticipantRules.Active(p)).Select(p => p.UserId).ToList();
        return await db.MeetingNotes.AsNoTracking()
            .Where(n => n.MeetingId == meeting.Id && secretaries.Contains(n.AuthorId) && n.Body != "")
            .OrderBy(n => n.CreatedAt)
            .Select(n => new MinutesNote(n.AgendaItemId, n.Body))
            .ToListAsync(ct);
    }

    private static string RoleName(MeetingRole role) => role switch
    {
        MeetingRole.Chair => "Chủ trì",
        MeetingRole.Secretary => "Thư ký",
        MeetingRole.Guest => "Khách mời",
        _ => "Thành viên",
    };

    private async Task<(Meeting? Meeting, MinutesFailure Failure)> ReadableAsync(Guid meetingId, Guid actorId, CancellationToken ct)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, MinutesFailure.Inactive);
        var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
        if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return (null, MinutesFailure.NotFound);
        return (meeting, MinutesFailure.None);
    }
}
