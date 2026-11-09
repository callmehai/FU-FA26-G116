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
/// SCRUM-32 — the Secretary's draft of the minutes: built from the meeting's records and its AI draft, then edited and
/// saved as often as needed. SCRUM-33 — one approver, and approving issues: the Secretary sends the draft to the Chair
/// (UC-61), who issues it (UC-64) or returns it with a reason (UC-65); issued text is never changed, a correction is a new
/// revision (UC-83). Until a revision is issued it is the Chair's and the Secretary's alone (BR-386, BR-412).
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
            // The meeting row is locked as a note save locks it (SCRUM-30): a note saved before is in these minutes, one
            // saved after is refused (BR-396).
            return await meetings.InTransactionAsync<MinutesOutcome>(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, token, forUpdate: true);
                if (meeting is null) return new(failure);
                if (!SummaryService.Manages(meeting, actorId)) return new(MinutesFailure.Forbidden);
                if (meeting.Status is not (MeetingStatus.InProgress or MeetingStatus.Ended)) return new(MinutesFailure.WrongMeetingStatus);
                var current = await db.MeetingMinutes.Where(m => m.MeetingId == meetingId)
                    .OrderByDescending(m => m.RevisionNumber).FirstOrDefaultAsync(token);
                if (current is not null && !Editable(current.Status)) return new(MinutesFailure.Locked);
                if (current is not null && !rebuild) return new(MinutesFailure.Exists);
                // A correction starts from the issued text; the records behind it were sealed with it (BR-413).
                if (current?.SupersedesId is not null) return new(MinutesFailure.CorrectionRebuild);

                var (draft, draftAt) = await DraftAsync(meetingId, token);
                var content = MinutesHtml.FromLines(MinutesTemplate.Build(await RecordsAsync(meeting, token), draft, Hanoi));
                var now = clock.GetUtcNow();
                if (current is null)
                {
                    current = new MeetingMinutes { MeetingId = meetingId, CreatedById = actorId, CreatedAt = now };
                    db.MeetingMinutes.Add(current);
                }
                current.Content = content;
                current.BuiltFromDraftAt = draftAt;
                current.UpdatedAt = now;
                await db.SaveChangesAsync(token);
                return new(Data: await StateAsync(meeting, actorId, token));
            }, ct);
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
            if (!SummaryService.Manages(meeting, actorId)) return new(Hidden(row.Status));
            // A returned revision is corrected again before it is sent back (BR-407).
            if (!Editable(row.Status)) return new(MinutesFailure.Locked);
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
                .Where(m => m.Id == minutesId && (m.Status == MinutesStatus.Draft || m.Status == MinutesStatus.Rejected)
                    && m.UpdatedAt >= from && m.UpdatedAt <= to)
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
            // Issued minutes, and the revisions they replaced, download for everyone who may read them (PO, Q7 settled
            // 08/11/2026); a draft or a revision waiting for approval is the two officers' alone (BR-412).
            if (!SummaryService.Manages(meeting, actorId) && !ReaderVisible(row.Status)) return new(MinutesFailure.NotFound);
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

    public async Task<MinutesOutcome> GetRevisionAsync(Guid minutesId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var row = await db.MeetingMinutes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == minutesId, ct);
            if (row is null) return new(MinutesFailure.NotFound);
            var (meeting, failure) = await ReadableAsync(row.MeetingId, actorId, ct);
            if (meeting is null) return new(failure);
            // A revision only the two officers may read is not there for anyone else, not even forbidden.
            if (!SummaryService.Manages(meeting, actorId) && !ReaderVisible(row.Status)) return new(MinutesFailure.NotFound);
            return new(Data: await StateAsync(meeting, actorId, ct, minutesId));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not read minutes {MinutesId}", minutesId);
            return new(MinutesFailure.ReadFailed);
        }
    }

    public Task<MinutesOutcome> SubmitAsync(Guid minutesId, Guid actorId, DateTimeOffset? loadedAt, CancellationToken ct = default)
        => TransitionAsync(minutesId, actorId, "submit", (meeting, row, now, _) =>
        {
            // Sequence UC-61: someone who is neither officer learns nothing (EX-6); the Chair is told it is not theirs (EX-1).
            if (!SummaryService.Manages(meeting, actorId)) return Task.FromResult(MinutesFailure.NotFound);
            if (!Holds(meeting, actorId, MeetingRole.Secretary)) return Task.FromResult(MinutesFailure.NotSecretary);
            if (meeting.Status != MeetingStatus.Ended) return Task.FromResult(MinutesFailure.MeetingNotEnded);
            if (!Editable(row.Status)) return Task.FromResult(MinutesFailure.Locked);
            if (!Fresh(row, loadedAt)) return Task.FromResult(MinutesFailure.Stale);
            row.Status = MinutesStatus.WaitingApproval;
            row.SubmittedById = actorId;
            row.SubmittedAt = now;
            row.UpdatedAt = now;
            Notify(Holders(meeting, MeetingRole.Chair), actorId, meeting, row, now, NotificationType.MinutesWaitingApproval,
                "minutes-submitted", "Biên bản chờ duyệt",
                $"Thư ký đã gửi biên bản cuộc họp “{meeting.Title}” để Chủ toạ duyệt.");
            return Task.FromResult(MinutesFailure.None);
        }, ct);

    public Task<MinutesOutcome> ApproveAsync(Guid minutesId, Guid actorId, DateTimeOffset? loadedAt, CancellationToken ct = default)
        => TransitionAsync(minutesId, actorId, "approve", async (meeting, row, now, token) =>
        {
            if (!SummaryService.Manages(meeting, actorId)) return MinutesFailure.NotFound;
            if (!Holds(meeting, actorId, MeetingRole.Chair)) return MinutesFailure.NotChair;
            if (row.Status != MinutesStatus.WaitingApproval) return MinutesFailure.NotWaiting;
            if (!Fresh(row, loadedAt)) return MinutesFailure.Stale;
            // Approving issues at once, and the meeting keeps its Ended status: being issued is the minutes' state (BR-408).
            row.Status = MinutesStatus.Issued;
            row.ApprovedById = actorId;
            row.ApprovedAt = now;
            row.IssuedAt = now;
            row.UpdatedAt = now;
            // A correction replaces the revision it corrects, which stays readable as superseded (BR-409).
            if (row.SupersedesId is { } earlierId)
            {
                var earlier = await db.MeetingMinutes.FirstAsync(m => m.Id == earlierId, token);
                earlier.Status = MinutesStatus.Superseded;
                earlier.UpdatedAt = now;
            }
            Notify(await ReadersAsync(meeting, token), actorId, meeting, row, now, NotificationType.MinutesIssued,
                "minutes-issued", "Biên bản đã ban hành",
                row.SupersedesId is null
                    ? $"Biên bản cuộc họp “{meeting.Title}” đã được ban hành."
                    : $"Bản đính chính biên bản cuộc họp “{meeting.Title}” đã được ban hành.");
            return MinutesFailure.None;
        }, ct);

    public Task<MinutesOutcome> ReturnAsync(Guid minutesId, Guid actorId, string? reason, DateTimeOffset? loadedAt,
        CancellationToken ct = default)
        => TransitionAsync(minutesId, actorId, "return", (meeting, row, now, _) =>
        {
            if (!SummaryService.Manages(meeting, actorId)) return Task.FromResult(MinutesFailure.NotFound);
            if (!Holds(meeting, actorId, MeetingRole.Chair)) return Task.FromResult(MinutesFailure.NotChair);
            if (row.Status != MinutesStatus.WaitingApproval) return Task.FromResult(MinutesFailure.NotWaiting);
            if (!Fresh(row, loadedAt)) return Task.FromResult(MinutesFailure.Stale);
            var text = reason?.Trim() ?? "";
            if (text.Length == 0 || text.Length > MinutesLimits.ReturnReason) return Task.FromResult(MinutesFailure.ReasonInvalid);
            row.Status = MinutesStatus.Rejected;
            row.RejectionReason = text;
            row.ReturnedById = actorId;
            row.ReturnedAt = now;
            row.UpdatedAt = now;
            Notify(Holders(meeting, MeetingRole.Secretary), actorId, meeting, row, now, NotificationType.MinutesReturned,
                "minutes-returned", "Biên bản bị trả lại",
                $"Chủ toạ đã trả lại biên bản cuộc họp “{meeting.Title}”. Xem lý do trên tab Biên bản.");
            return Task.FromResult(MinutesFailure.None);
        }, ct);

    public Task<MinutesOutcome> CorrectAsync(Guid minutesId, Guid actorId, CancellationToken ct = default)
        => TransitionAsync(minutesId, actorId, "correct", async (meeting, row, now, token) =>
        {
            // Sequence UC-83: a reader of the issued text is told it is not theirs to write (EX-1); anything they may not
            // read is not there (EX-5).
            if (!SummaryService.Manages(meeting, actorId)) return Hidden(row.Status);
            if (row.Status != MinutesStatus.Issued) return MinutesFailure.NotIssued;
            // At most one revision is not issued yet (BR-410): an issued revision with a later one has its correction.
            if (await db.MeetingMinutes.AnyAsync(m => m.MeetingId == row.MeetingId && m.RevisionNumber > row.RevisionNumber, token))
                return MinutesFailure.CorrectionInProgress;
            db.MeetingMinutes.Add(new MeetingMinutes
            {
                MeetingId = row.MeetingId, RevisionNumber = row.RevisionNumber + 1, SupersedesId = row.Id,
                Content = row.Content, BuiltFromDraftAt = row.BuiltFromDraftAt, Status = MinutesStatus.Draft,
                CreatedById = actorId, CreatedAt = now, UpdatedAt = now,
            });
            // Nobody is told until the correction is issued (UC-83 POST-3).
            return MinutesFailure.None;
        }, ct);

    /// <summary>
    /// One state change of one revision. The meeting row is locked as building the minutes locks it, and the revision row
    /// with it, so two officers acting at once take turns and a save racing a submission finds the revision locked. The
    /// state and its notifications are one save: when it fails, nothing changed and nobody was told.
    /// </summary>
    private async Task<MinutesOutcome> TransitionAsync(Guid minutesId, Guid actorId, string action,
        Func<Meeting, MeetingMinutes, DateTimeOffset, CancellationToken, Task<MinutesFailure>> apply, CancellationToken ct)
    {
        try
        {
            var meetingId = await db.MeetingMinutes.AsNoTracking().Where(m => m.Id == minutesId)
                .Select(m => (Guid?)m.MeetingId).FirstOrDefaultAsync(ct);
            if (meetingId is null) return new(MinutesFailure.NotFound);
            return await meetings.InTransactionAsync<MinutesOutcome>(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId.Value, actorId, token, forUpdate: true);
                if (meeting is null) return new(failure);
                var row = (await db.MeetingMinutes
                    .FromSqlInterpolated($"SELECT * FROM meeting_minutes WHERE id = {minutesId} FOR UPDATE")
                    .ToListAsync(token)).SingleOrDefault();
                if (row is null) return new(MinutesFailure.NotFound);
                var result = await apply(meeting, row, clock.GetUtcNow(), token);
                if (result != MinutesFailure.None) return new(result);
                await db.SaveChangesAsync(token);
                return new(Data: await StateAsync(meeting, actorId, token));
            }, ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two corrections of the same revision at once: the revision-number index lets one through (BR-410).
            logger.LogWarning(error, "Concurrent minutes {Action} on {MinutesId}", action, minutesId);
            return new(MinutesFailure.CorrectionInProgress);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not {Action} minutes {MinutesId}", action, minutesId);
            return new(MinutesFailure.SaveFailed);
        }
    }

    /// <summary>A revision is edited, rebuilt and submitted while it is a draft or was returned (BR-388, BR-407).</summary>
    private static bool Editable(MinutesStatus status) => status is MinutesStatus.Draft or MinutesStatus.Rejected;

    /// <summary>What someone who is neither officer may read: the issued text and the revisions it replaced (BR-412).</summary>
    private static bool ReaderVisible(MinutesStatus status) => status is MinutesStatus.Issued or MinutesStatus.Superseded;

    /// <summary>Refusal for someone who is neither officer: forbidden on what they may read, not there otherwise.</summary>
    private static MinutesFailure Hidden(MinutesStatus status)
        => ReaderVisible(status) ? MinutesFailure.Forbidden : MinutesFailure.NotFound;

    /// <summary>The copy the caller read is still the stored one (BR-389), within the precision PostgreSQL keeps.</summary>
    private static bool Fresh(MeetingMinutes row, DateTimeOffset? loadedAt)
        => loadedAt is { } at && row.UpdatedAt >= at.AddMilliseconds(-1) && row.UpdatedAt <= at.AddMilliseconds(1);

    private static bool Holds(Meeting meeting, Guid actorId, MeetingRole role) => Holders(meeting, role).Contains(actorId);

    private static IEnumerable<Guid> Holders(Meeting meeting, MeetingRole role) => meeting.Participants
        .Where(p => p.Role == role && ParticipantRules.Active(p)).Select(p => p.UserId);

    /// <summary>Everyone who may read the issued minutes (BR-412): the current participants, the organizer and the
    /// approver of the meeting. Never a Guest, whom the meeting's read policy does not count as a participant.</summary>
    private async Task<IReadOnlyCollection<Guid>> ReadersAsync(Meeting meeting, CancellationToken ct)
    {
        var readers = meeting.Participants.Where(ParticipantRules.Active).Select(p => p.UserId).ToHashSet();
        var others = new[] { meeting.CreatedById, meeting.AssignedApproverId ?? Guid.Empty }
            .Where(id => id != Guid.Empty && !readers.Contains(id)).ToList();
        if (others.Count > 0)
            readers.UnionWith(await db.Users.AsNoTracking().Where(u => others.Contains(u.Id) && u.IsActive)
                .Select(u => u.Id).ToListAsync(ct));
        return readers;
    }

    /// <summary>In-app notifications saved with the state change (BR-411); the realtime dispatcher pushes them.</summary>
    private void Notify(IEnumerable<Guid> recipients, Guid actorId, Meeting meeting, MeetingMinutes row, DateTimeOffset now,
        NotificationType type, string key, string title, string body)
    {
        foreach (var userId in recipients.Where(id => id != actorId).Distinct())
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId, Type = type, Title = title, Body = body, MeetingId = meeting.Id,
                TargetType = "Meeting", TargetId = meeting.Id,
                DeduplicationKey = $"{key}:{row.Id:D}:{now.UtcTicks}:{userId:D}",
                CreatedAt = now, UpdatedAt = now,
            });
        }
    }

    /// <summary>
    /// The revision the caller reads: <paramref name="selectedId"/> when given, otherwise the latest for the two officers
    /// and the latest issued for everyone else (BR-386, BR-412). The history is filtered on the server the same way, so a
    /// reader never learns that a correction is being written or waits for approval.
    /// </summary>
    private async Task<MinutesState> StateAsync(Meeting meeting, Guid actorId, CancellationToken ct, Guid? selectedId = null)
    {
        var manages = SummaryService.Manages(meeting, actorId);
        var revisions = await db.MeetingMinutes.AsNoTracking()
            .Where(m => m.MeetingId == meeting.Id)
            .Where(m => manages || m.Status == MinutesStatus.Issued || m.Status == MinutesStatus.Superseded)
            .OrderByDescending(m => m.RevisionNumber)
            .Select(m => new { m.Id, m.RevisionNumber, m.Status, m.IssuedAt })
            .ToListAsync(ct);
        var chosen = selectedId ?? (manages ? revisions.FirstOrDefault()?.Id
            : revisions.FirstOrDefault(r => r.Status == MinutesStatus.Issued)?.Id);
        var row = chosen is { } id
            ? await db.MeetingMinutes.AsNoTracking().Include(m => m.CreatedBy).Include(m => m.SubmittedBy)
                .Include(m => m.ApprovedBy).Include(m => m.ReturnedBy).FirstOrDefaultAsync(m => m.Id == id, ct)
            : null;
        var draftChangedAt = manages ? (await DraftAsync(meeting.Id, ct)).ChangedAt : null;
        return new(row is null ? null : View(row, manages), manages, draftChangedAt,
            revisions.Select(r => new MinutesRevision(r.Id, r.RevisionNumber, r.Status.ToString(), r.IssuedAt)).ToList(),
            Holds(meeting, actorId, MeetingRole.Secretary), Holds(meeting, actorId, MeetingRole.Chair));
    }

    private static MinutesView View(MeetingMinutes row, bool manages) => new(row.Id, row.RevisionNumber, row.Status.ToString(),
        MinutesHtml.Normalize(row.Content), row.CreatedAt, row.UpdatedAt, Person(row.CreatedBy),
        manages ? row.BuiltFromDraftAt : null, row.SupersedesId,
        manages ? Person(row.SubmittedBy) : null, manages ? row.SubmittedAt : null,
        Person(row.ApprovedBy), row.ApprovedAt, row.IssuedAt,
        manages ? Person(row.ReturnedBy) : null, manages ? row.ReturnedAt : null,
        manages && row.RejectionReason.Length > 0 ? row.RejectionReason : null);

    private static MinutesPerson? Person(User? user) => user is null ? null : new(user.Id, user.FullName);

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

    private async Task<(Meeting? Meeting, MinutesFailure Failure)> ReadableAsync(Guid meetingId, Guid actorId,
        CancellationToken ct, bool forUpdate = false)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, MinutesFailure.Inactive);
        var meeting = await meetings.FindWorkflowAsync(meetingId, forUpdate, ct);
        if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return (null, MinutesFailure.NotFound);
        return (meeting, MinutesFailure.None);
    }
}
