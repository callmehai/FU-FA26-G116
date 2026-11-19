using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Application.Summaries;

/// <summary>
/// SCRUM-32 — the Secretary's draft of the minutes: built from the meeting's records and its AI draft, then edited and
/// saved as often as needed. SCRUM-33 — one approver, and approving issues: the Secretary sends the draft to the Chair
/// (UC-61), who issues it (UC-64) or returns it with a reason (UC-65); issued text is never changed, a correction is a new
/// revision (UC-83). Until a revision is issued it is the Chair's and the Secretary's alone (BR-386, BR-412).
/// </summary>
public sealed class MinutesService(IMeetingMinutesRepository minutes, IMinutesSourceQuery sources,
    ISummarySourceQuery summarySources, IMeetingSummaryRepository summaries, IMinutesRenderer renderer,
    INotificationRepository notifications, IAuditLogRepository auditLog, IUnitOfWork unitOfWork, IMeetingRepository meetings,
    TimeProvider clock, ILogger<MinutesService> logger, IEmailMessageRepository emails, IOptions<EmailDeliveryOptions> email)
    : IMinutesService
{
    private static readonly TimeZoneInfo Hanoi = SummaryWriter.FindHanoi();

    public async Task<MinutesOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, token);
                if (meeting is null) return new MinutesOutcome(failure);
                return new(Data: await StateAsync(meeting, actorId, token));
            }, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(Original(error), "Could not read the minutes of meeting {MeetingId}", meetingId);
            return new(MinutesFailure.ReadFailed);
        }
    }

    public async Task<MinutesOutcome> CreateAsync(Guid meetingId, Guid actorId, bool rebuild, CancellationToken ct = default)
    {
        try
        {
            // The meeting row is locked as a note save locks it (SCRUM-30): a note saved before is in these minutes, one
            // saved after is refused (BR-396).
            return await unitOfWork.ExecuteAsync<MinutesOutcome>(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, token, forUpdate: true);
                if (meeting is null) return new(failure);
                if (!SummaryService.Manages(meeting, actorId)) return new(MinutesFailure.Forbidden);
                // Only once the meeting has ended (Khánh 19/11/2026): built while it runs, the minutes would miss the rest
                // of it and read as if complete.
                if (meeting.Status != MeetingStatus.Ended) return new(MinutesFailure.WrongMeetingStatus);
                var current = await minutes.FindLatestAsync(meetingId, token);
                if (current is not null && !Editable(current.Status)) return new(MinutesFailure.Locked);
                if (current is not null && !rebuild) return new(MinutesFailure.Exists);
                // A correction starts from the issued text; the records behind it were sealed with it (BR-413).
                if (current?.SupersedesId is not null) return new(MinutesFailure.CorrectionRebuild);

                var (draft, draftAt) = await DraftAsync(meetingId, token);
                var content = renderer.FromLines(MinutesTemplate.Build(await RecordsAsync(meeting, token), draft, Hanoi));
                var now = clock.GetUtcNow();
                if (current is null)
                {
                    current = new MeetingMinutes { MeetingId = meetingId, CreatedById = actorId, CreatedAt = now };
                    minutes.Add(current);
                }
                current.Content = content;
                current.BuiltFromDraftAt = draftAt;
                current.UpdatedAt = now;
                await unitOfWork.SaveChangesAsync(token);
                return new(Data: await StateAsync(meeting, actorId, token));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (UniqueConflictException error)
        {
            // Two first builds at once: the unique meeting index lets one through. Any other failure is a failed save.
            logger.LogWarning(error.InnerException ?? error, "Concurrent minutes creation for meeting {MeetingId}", meetingId);
            return new(MinutesFailure.Exists);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(Original(error), "Could not build the minutes of meeting {MeetingId}", meetingId);
            return new(MinutesFailure.SaveFailed);
        }
    }

    public async Task<MinutesOutcome> SaveAsync(Guid minutesId, Guid actorId, string? content, DateTimeOffset? loadedAt,
        CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var row = await minutes.FindAsync(minutesId, token);
                if (row is null) return new MinutesOutcome(MinutesFailure.NotFound);
                var (meeting, failure) = await ReadableAsync(row.MeetingId, actorId, token);
                if (meeting is null) return new(failure);
                // Someone who may not edit learns nothing about a draft they may not read.
                if (!SummaryService.Manages(meeting, actorId)) return new(Hidden(row.Status));
                // A returned revision is corrected again before it is sent back (BR-407).
                if (!Editable(row.Status)) return new(MinutesFailure.Locked);
                // Only the allow-list is stored (ADR-0055): whatever else the request carries never reaches a reader. A
                // document of empty paragraphs says nothing, so it is refused like an empty one.
                // Nesting deeper than any document the editor writes is refused before it is walked (MinutesHtml.MaxDepth).
                var html = content is null || content.Length > MinutesLimits.Content || renderer.TooDeep(content)
                    ? null : renderer.Sanitize(content);
                if (html is null || html.Length > MinutesLimits.Content || !renderer.HasText(html))
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
                var saved = await minutes.SaveContentAsync(minutesId, html, from, to, now, token);
                if (saved == 0) return new(MinutesFailure.Stale);
                return new(Data: await StateAsync(meeting, actorId, token));
            }, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(Original(error), "Could not save minutes {MinutesId}", minutesId);
            return new(MinutesFailure.SaveFailed);
        }
    }

    public async Task<MinutesOutcome> ExportAsync(Guid minutesId, Guid actorId, MinutesExportFormat format,
        CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var row = await minutes.FindAsync(minutesId, token);
                if (row is null) return new MinutesOutcome(MinutesFailure.NotFound);
                var (meeting, failure) = await ReadableAsync(row.MeetingId, actorId, token);
                if (meeting is null) return new(failure);
                // Issued minutes, and the revisions they replaced, download for everyone who may read them (PO, Q7 settled
                // 08/11/2026); a draft or a revision waiting for approval is the two officers' alone (BR-412).
                if (!SummaryService.Manages(meeting, actorId) && !ReaderVisible(row.Status)) return new(MinutesFailure.NotFound);
                if (meeting.Sensitivity == Sensitivity.Secret) return new(MinutesFailure.SecretMeeting);
                var name = string.Concat($"Biên bản - {meeting.Title}".Split(Path.GetInvalidFileNameChars()));
                var stem = name.Length > 120 ? name[..120] : name;
                return new(Data: format == MinutesExportFormat.Pdf
                    ? new MinutesFile($"{stem}.pdf", renderer.BuildPdf(row.Content), "application/pdf")
                    : new MinutesFile($"{stem}.docx", renderer.BuildDocx(row.Content),
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
            }, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(Original(error), "Could not export minutes {MinutesId}", minutesId);
            return new(MinutesFailure.ReadFailed);
        }
    }

    public async Task<MinutesOutcome> GetRevisionAsync(Guid minutesId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var row = await minutes.FindAsync(minutesId, token);
                if (row is null) return new MinutesOutcome(MinutesFailure.NotFound);
                var (meeting, failure) = await ReadableAsync(row.MeetingId, actorId, token);
                if (meeting is null) return new(failure);
                // A revision only the two officers may read is not there for anyone else, not even forbidden.
                if (!SummaryService.Manages(meeting, actorId) && !ReaderVisible(row.Status)) return new(MinutesFailure.NotFound);
                return new(Data: await StateAsync(meeting, actorId, token, minutesId));
            }, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(Original(error), "Could not read minutes {MinutesId}", minutesId);
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
                var earlier = await minutes.GetAsync(earlierId, token);
                earlier.Status = MinutesStatus.Superseded;
                earlier.UpdatedAt = now;
            }
            // BR-446 — saved with the issue itself, so minutes are never issued without the evidence of who issued them.
            auditLog.Add(new AuditLogEntry
            {
                ActorId = actorId, Action = AuditActions.MinutesIssued, ObjectType = AuditActions.MinutesObjectType,
                ObjectId = row.Id, OccurredAt = now, CreatedAt = now, UpdatedAt = now,
                Details = JsonSerializer.Serialize(new
                {
                    meetingId = meeting.Id, revisionNumber = row.RevisionNumber, supersedesId = row.SupersedesId,
                }),
            });
            var readers = await ReadersAsync(meeting, token);
            Notify(readers, actorId, meeting, row, now, NotificationType.MinutesIssued,
                "minutes-issued", "Biên bản đã ban hành",
                row.SupersedesId is null
                    ? $"Biên bản cuộc họp “{meeting.Title}” đã được ban hành."
                    : $"Bản đính chính biên bản cuộc họp “{meeting.Title}” đã được ban hành.");
            await QueueIssuedEmailsAsync(readers, actorId, meeting, row, now, token);
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
            if (await minutes.HasLaterRevisionAsync(row.MeetingId, row.RevisionNumber, token))
                return MinutesFailure.CorrectionInProgress;
            minutes.Add(new MeetingMinutes
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
            var meetingId = await ReadAsync(token => minutes.FindMeetingIdAsync(minutesId, token), ct);
            if (meetingId is null) return new(MinutesFailure.NotFound);
            return await unitOfWork.ExecuteAsync<MinutesOutcome>(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId.Value, actorId, token, forUpdate: true);
                if (meeting is null) return new(failure);
                var row = await minutes.LockAsync(minutesId, token);
                if (row is null) return new(MinutesFailure.NotFound);
                var result = await apply(meeting, row, clock.GetUtcNow(), token);
                if (result != MinutesFailure.None) return new(result);
                await unitOfWork.SaveChangesAsync(token);
                return new(Data: await StateAsync(meeting, actorId, token));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (UniqueConflictException error)
        {
            // Two corrections of the same revision at once: the revision-number index lets one through (BR-410).
            logger.LogWarning(error.InnerException ?? error, "Concurrent minutes {Action} on {MinutesId}", action, minutesId);
            return new(MinutesFailure.CorrectionInProgress);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(Original(error), "Could not {Action} minutes {MinutesId}", action, minutesId);
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
            readers.UnionWith(await sources.ListActiveUsersAsync(others, ct));
        return readers;
    }

    /// <summary>
    /// The same readers as the bell, by email (PO 15/11/2026), queued in the issuing transaction so an issue never loses
    /// its emails. The Chair who issued gets none, as for the bell.
    /// </summary>
    private async Task QueueIssuedEmailsAsync(IEnumerable<Guid> readers, Guid actorId, Meeting meeting, MeetingMinutes row,
        DateTimeOffset now, CancellationToken ct)
    {
        var ids = readers.Where(id => id != actorId).Distinct().ToArray();
        var people = await sources.ListEmailRecipientsAsync(ids, ct);
        if (people.Count == 0) return;
        var names = await sources.GetIssuedEmailNamesAsync(actorId, meeting.CreatedById, ct);
        var chair = names.Chair;
        var contact = new MeetingPeople(chair, names.Organizer, names.OrganizerEmail);
        var link = $"{email.Value.ApplicationBaseUrl.TrimEnd('/')}/mgmt/meetings/{meeting.Id:D}?tab=minutes";
        // RevisionNumber 1 is the first issue; each correction counts up from there.
        int? correction = row.SupersedesId is null ? null : row.RevisionNumber - 1;
        var kind = correction is null ? "Biên bản cuộc họp đã ban hành" : "Bản đính chính biên bản đã ban hành";
        var subject = $"{kind}: {meeting.Title}".Replace('\r', ' ').Replace('\n', ' ');
        if (subject.Length > 300) subject = subject[..(char.IsHighSurrogate(subject[299]) ? 299 : 300)];
        foreach (var person in people)
        {
            emails.Add(new EmailMessage
            {
                DeduplicationKey = $"email-minutes-issued:{row.Id:D}:{person.Id:D}",
                ToAddress = person.Email, Subject = subject, MeetingId = meeting.Id, CreatedAt = now, UpdatedAt = now,
                Body = $"Xin chào {person.FullName},\n\n{kind}: \"{meeting.Title}\".\n"
                    + $"Ban hành lúc: {EmailLayout.At(now)}, Chủ toạ duyệt: {chair}\n\nXem biên bản: {link}\n"
                    + $"\nNgười tổ chức: {contact.Contact}\n",
                HtmlBody = EmailTemplates.MinutesIssued(person.FullName, meeting.Title, meeting.PlannedStartTime,
                    meeting.PlannedEndTime, now, chair, correction, link, contact,
                    downloadable: meeting.Sensitivity != Sensitivity.Secret),
            });
        }
    }

    /// <summary>In-app notifications saved with the state change (BR-411); the realtime dispatcher pushes them.</summary>
    private void Notify(IEnumerable<Guid> recipients, Guid actorId, Meeting meeting, MeetingMinutes row, DateTimeOffset now,
        NotificationType type, string key, string title, string body)
    {
        foreach (var userId in recipients.Where(id => id != actorId).Distinct())
        {
            notifications.Add(new Notification
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
        var revisions = await minutes.ListRevisionsAsync(meeting.Id, manages, ct);
        var chosen = selectedId ?? (manages ? revisions.FirstOrDefault()?.Id
            : revisions.FirstOrDefault(r => r.Status == MinutesStatus.Issued)?.Id);
        var row = chosen is { } id
            ? await minutes.FindWithPeopleAsync(id, ct)
            : null;
        var draftChangedAt = manages ? (await DraftAsync(meeting.Id, ct)).ChangedAt : null;
        return new(row is null ? null : View(row, manages), manages, draftChangedAt,
            revisions.Select(r => new MinutesRevision(r.Id, r.RevisionNumber, r.Status.ToString(), r.IssuedAt)).ToList(),
            Holds(meeting, actorId, MeetingRole.Secretary), Holds(meeting, actorId, MeetingRole.Chair));
    }

    private MinutesView View(MeetingMinutes row, bool manages) => new(row.Id, row.RevisionNumber, row.Status.ToString(),
        renderer.Normalize(row.Content), row.CreatedAt, row.UpdatedAt, Person(row.CreatedBy),
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
        var row = await summaries.FindByMeetingAsync(meetingId, false, ct);
        var draft = row is null ? null : SummaryService.Read(row);
        if (row is null || draft is null) return (null, null);
        return (draft, row.EditedAt is { } edited && edited > row.GeneratedAt ? edited : row.GeneratedAt);
    }

    private async Task<MinutesRecords> RecordsAsync(Meeting meeting, CancellationToken ct)
    {
        var people = await sources.ListAttendeesAsync(meeting.Id, ct);
        var guests = await sources.ListGuestsAsync(meeting.Id, ct);
        var lines = people.Select(p => new MinutesPersonLine(p.FullName, RoleName(p.Role),
                p.Attendance is AttendanceStatus.Absent or AttendanceStatus.Excused))
            .Concat(guests.Select(g => new MinutesPersonLine(g.Name, "Khách mời",
                g.Attendance is AttendanceStatus.Absent or AttendanceStatus.Excused)))
            .ToList();
        var agenda = await summarySources.ListAgendaAsync(meeting.Id, ct);
        var place = meeting.Mode switch
        {
            MeetingMode.Online => "Phòng họp trực tuyến TrustCabinet",
            MeetingMode.Hybrid => $"{meeting.Location.Trim()} và phòng họp trực tuyến TrustCabinet".Trim(),
            _ => meeting.Location,
        };
        return new(meeting.Title, meeting.ActualStartedAt ?? meeting.PlannedStartTime, meeting.ActualEndedAt, place,
            people.FirstOrDefault(p => p.Role == MeetingRole.Chair)?.FullName,
            people.FirstOrDefault(p => p.Role == MeetingRole.Secretary)?.FullName,
            lines, agenda, await SummaryService.VoteLinesAsync(summarySources, meeting.Id, ct), await SecretaryNotesAsync(meeting, ct));
    }

    /// <summary>The current Secretary's notes (SCRUM-30, BR-402); the Chair reads them but writes none.</summary>
    private async Task<IReadOnlyList<MinutesNote>> SecretaryNotesAsync(Meeting meeting, CancellationToken ct)
    {
        var secretaries = meeting.Participants
            .Where(p => p.Role == MeetingRole.Secretary && ParticipantRules.Active(p)).Select(p => p.UserId).ToList();
        return await sources.ListNotesAsync(meeting.Id, secretaries, ct);
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

    /// <summary>Repository calls with no transaction of their own, the conditional content save among them, as before.</summary>
    private Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        => unitOfWork.ExecuteAsync(work, TransactionMode.None, ct);

    /// <summary>What the log keeps of a failure: the storage error itself, not its translation (R8).</summary>
    private static Exception Original(Exception error) => (error as PersistenceException)?.InnerException ?? error;
}
