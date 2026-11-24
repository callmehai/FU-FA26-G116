using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Application.Documents;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Votes;
using TrustCabinet.Domain.GuestAccess;

namespace TrustCabinet.Application.Votes;

/// <summary>
/// Votes taken during a meeting (FE-11, SCRUM-28): drafts, opening, ballots, closing and the Chair's conclusion; and
/// the documents voters must read first (SCRUM-96): attaching and detaching them, and confirming they were read.
/// Contract in docs/03-api/15-bieu-quyet.md.
/// </summary>
public sealed class VoteService(IVoteRepository votes, IParticipantRepository participants,
    IParticipantGuestAccess guests, IMeetingDocumentAttacher attacher,
    StoredFileWriter writer, IVoteChangePublisher publisher, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<VoteService> logger)
    : IVoteService
{
    private DateTimeOffset Now => AgendaExecution.UtcMicroseconds(clock.GetUtcNow());

    /// <summary>
    /// Who the caller is in this meeting, re-read on every request: losing access stops new ballots at once (BR-092, BR-093).
    /// A Guest has a member's rights here (BR-055) but must still hold a live verified session.
    /// </summary>
    private async Task<(VoteFailure Failure, VoteViewer? Viewer)> AccessAsync(ParticipantSnapshot? s, ParticipantActor actor,
        CancellationToken ct)
    {
        if (actor.Guest is { } g)
            return actor.UserId is null && g.IsValid && s is not null && g.MeetingId == s.Meeting.Id
                && GuestAccessRules.MeetingOpen(s.Meeting.Status) && s.Guests.Any(x => x.Id == g.InvitationId)
                && await guests.CanAccessAsync(g, ct)
                ? (VoteFailure.None, new(null, g.InvitationId)) : (VoteFailure.NotFound, null);
        if (actor.UserId is not { } id || !await votes.IsActiveUserAsync(id, ct))
            return (VoteFailure.Inactive, null);
        return s is not null && MeetingReadPolicy.CanRead(s.Meeting, id)
            ? (VoteFailure.None, new(id, null)) : (VoteFailure.NotFound, null);
    }

    public async Task<VoteOutcome> ListAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var s = await participants.LoadAsync(meetingId, false, token);
                var (failure, viewer) = await AccessAsync(s, actor, token);
                if (failure != VoteFailure.None) return new VoteOutcome(failure);
                var meeting = s!.Meeting;
                var rows = await votes.ListAsync(meetingId, VoteRules.IsManager(meeting, viewer!), viewer!, token);
                var anyOpen = rows.Any(r => r.Status == VotingStatus.Open);
                return new VoteOutcome(Data: new VoteListSnapshot(meetingId, meeting.Status.ToString(), clock.GetUtcNow(),
                    VoteRules.Permissions(meeting, viewer!), rows.Select(r => new VoteSummary(r.Id, r.Title, r.Status.ToString(),
                        r.IsAnonymous, Roles(r), r.OpenedAt, r.ClosedAt, r.EligibleCount, r.ReceiptCount, r.HasVoted,
                        Conclusion(r), r.DocumentCount, Actions(meeting, viewer!, r, anyOpen))).ToArray()));
            }, TransactionMode.RepeatableRead, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read votes of meeting {MeetingId}", meetingId);
            return new(VoteFailure.ListReadFailed);
        }
    }

    public async Task<VoteOutcome> GetAsync(Guid meetingId, Guid voteId, ParticipantActor actor, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var s = await participants.LoadAsync(meetingId, false, token);
                var (failure, viewer) = await AccessAsync(s, actor, token);
                if (failure != VoteFailure.None) return new VoteOutcome(failure);
                var meeting = s!.Meeting;
                var r = await votes.FindAsync(meetingId, voteId, viewer!, token);
                // A draft the caller may not see answers exactly like a vote that does not exist (BR-086, BR-087).
                if (r is null || (r.Status == VotingStatus.Draft && !VoteRules.IsManager(meeting, viewer!)))
                    return new VoteOutcome(VoteFailure.NotFound);
                var options = await votes.OptionsAsync(voteId, token);
                var anotherOpen = r.Status == VotingStatus.Draft && await votes.AnotherOpenAsync(meetingId, voteId, token);
                // While open, only turnout is shown — never results per option (UC-62b).
                var result = r.Status == VotingStatus.Closed ? await votes.ResultAsync(meetingId, voteId, r.IsAnonymous, token) : null;
                var attached = await votes.DocumentsAsync(voteId, viewer!, token);
                return new VoteOutcome(Data: new VoteDetail(meetingId, meeting.Status.ToString(), clock.GetUtcNow(), r.Id, r.Title,
                    r.Description, r.Status.ToString(), r.IsAnonymous, Roles(r),
                    options.Select(o => new VoteOptionView(o.Id, o.Position, o.Label)).ToArray(), r.OpenedAt, r.ClosedAt,
                    r.EligibleCount, r.ReceiptCount, r.IsEligible, r.HasVoted, Conclusion(r), r.DocumentCount, attached, result,
                    Actions(meeting, viewer!, r, anotherOpen)));
            }, TransactionMode.RepeatableRead, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read vote {VoteId} of meeting {MeetingId}", voteId, meetingId);
            return new(VoteFailure.DetailReadFailed);
        }
    }

    public async Task<VoteOutcome> MutateAsync(Guid meetingId, Guid? voteId, ParticipantActor actor, VoteOperation operation,
        VoteInput input, CancellationToken ct = default)
    {
        var cast = operation == VoteOperation.Cast;
        VoteChange change;
        var managersOnly = false;
        VoteOutcome outcome;
        VotingSession? vote = null;
        DateTimeOffset now = default;
        try
        {
            // A failure returns before SaveChanges and is rolled back, as the old block left its transaction
            // uncommitted; only a success is saved and committed.
            outcome = await unitOfWork.ExecuteAsync(async token =>
            {
                var (failure, s, viewer, locked) = await LockAsync(meetingId, voteId, actor, cast, token);
                if (failure != VoteFailure.None) return new VoteOutcome(failure);
                vote = locked;
                var meeting = s!.Meeting;
                now = Now;
                VoteOutcome result;
                if (operation == VoteOperation.Create) (result, vote) = await CreateAsync(s, viewer!, input, now, token);
                else result = operation switch
                {
                    VoteOperation.Update => await UpdateAsync(s, viewer!, vote!, input, now, token),
                    VoteOperation.Delete => Delete(meeting, viewer!, vote!),
                    VoteOperation.Open => await OpenAsync(s, viewer!, vote!, now, token),
                    VoteOperation.Cast => await CastAsync(meeting, viewer!, vote!, input, now, token),
                    VoteOperation.Close => await CloseAsync(meeting, viewer!, vote!, now, token),
                    VoteOperation.AttachDocuments => await AttachAsync(meeting, viewer!, vote!, input, token),
                    VoteOperation.AttachFromLibrary => await AttachFromLibraryAsync(meeting, viewer!, vote!, input, now, token),
                    VoteOperation.DetachDocument => await DetachAsync(meeting, viewer!, vote!, input, token),
                    _ => Conclude(meeting, viewer!, vote!, input, now)
                };
                if (result.Failure != VoteFailure.None) return result;

                await unitOfWork.SaveChangesAsync(token);
                return result;
            }, result => result.Failure == VoteFailure.None, TransactionMode.ReadCommitted, ct);
            if (outcome.Failure != VoteFailure.None) return outcome;
            var opened = operation == VoteOperation.Open
                || (operation is VoteOperation.Create or VoteOperation.Update && input.OpenImmediately);
            // Drafts and their documents are seen by the Chair and Secretary only (BR-086, BR-087).
            managersOnly = operation is VoteOperation.Create or VoteOperation.Update or VoteOperation.Delete
                or VoteOperation.AttachDocuments or VoteOperation.AttachFromLibrary or VoteOperation.DetachDocument && !opened;
            var changeType = opened ? "Opened" : operation switch
            {
                VoteOperation.Create => "DraftCreated", VoteOperation.Update => "DraftUpdated", VoteOperation.Delete => "DraftDeleted",
                VoteOperation.Cast => "BallotCast", VoteOperation.Close => "Closed",
                VoteOperation.AttachDocuments or VoteOperation.AttachFromLibrary or VoteOperation.DetachDocument => "DocumentsChanged",
                _ => "ConclusionRecorded"
            };
            change = new(Guid.CreateVersion7(), meetingId, vote!.Id, changeType, opened ? vote.Title : null, now);
        }
        catch (UniqueConflictException e) when (e.Conflict == UniqueConflict.OpenVoteInMeeting)
        {
            unitOfWork.DiscardChanges();
            return new(VoteFailure.AnotherOpen);
        }
        catch (UniqueConflictException e) when (e.Conflict == UniqueConflict.BallotAlreadyCast)
        {
            // Two simultaneous ballots by the same person: the database kept the first (BR-092, BR-093).
            unitOfWork.DiscardChanges();
            return new(VoteFailure.AlreadyCast);
        }
        catch (PersistenceException ex)
        {
            unitOfWork.DiscardChanges();
            logger.LogError(ex.InnerException ?? ex, "Failed vote {Operation} for meeting {MeetingId}", operation, meetingId);
            return new(VoteFailure.SaveFailed);
        }
        await PublishAsync(change, managersOnly);
        return outcome;
    }

    /// <summary>A committed write stays successful even when the client disconnects or push fails.</summary>
    private async Task PublishAsync(VoteChange change, bool managersOnly)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await publisher.PublishAsync(change, managersOnly, timeout.Token); }
        catch (Exception ex) { logger.LogWarning(ex, "Vote committed; realtime delivery failed for {EventId}", change.EventId); }
    }

    /// <summary>
    /// Takes the locks of a vote write, in the order used everywhere: the meeting, the caller, the vote. Then checks
    /// the caller may see the meeting and, when <paramref name="voteId"/> is given, the vote: a draft the caller may
    /// not see answers like a vote that does not exist (BR-086, BR-087).
    /// </summary>
    private async Task<(VoteFailure, ParticipantSnapshot?, VoteViewer?, VotingSession?)> LockAsync(Guid meetingId,
        Guid? voteId, ParticipantActor actor, bool shared, CancellationToken ct)
    {
        await votes.LockMeetingAsync(meetingId, shared, ct);
        if (actor.UserId is { } userId) await votes.ShareUserAsync(userId, ct);
        var s = await participants.LoadAsync(meetingId, false, ct);
        var (failure, viewer) = await AccessAsync(s, actor, ct);
        if (failure != VoteFailure.None) return (failure, null, null, null);
        if (voteId is not { } id) return (VoteFailure.None, s, viewer, null);
        var vote = await votes.LockAsync(meetingId, id, shared, ct);
        return vote is null || (vote.Status == VotingStatus.Draft && !VoteRules.IsManager(s!.Meeting, viewer!))
            ? (VoteFailure.NotFound, null, null, null) : (VoteFailure.None, s, viewer, vote);
    }

    public async Task<VoteOutcome> CheckAttachUploadAsync(Guid meetingId, Guid voteId, ParticipantActor actor,
        CancellationToken ct = default)
    {
        try
        {
            var denied = await unitOfWork.ExecuteAsync(async token =>
            {
                var s = await participants.LoadAsync(meetingId, false, token);
                var (failure, viewer) = await AccessAsync(s, actor, token);
                if (failure != VoteFailure.None) return new VoteOutcome(failure);
                var r = await votes.FindAsync(meetingId, voteId, viewer!, token);
                if (r is null || (r.Status == VotingStatus.Draft && !VoteRules.IsManager(s!.Meeting, viewer!)))
                    return new VoteOutcome(VoteFailure.NotFound);
                if (!VoteRules.CanManageDraft(s!.Meeting, viewer!) || r.Status != VotingStatus.Draft)
                    return new VoteOutcome(VoteFailure.DraftDenied);
                return new VoteOutcome();
            }, TransactionMode.RepeatableRead, ct);
            if (denied.Failure != VoteFailure.None) return denied;
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to check document upload to vote {VoteId} of meeting {MeetingId}", voteId, meetingId);
            return new(VoteFailure.SaveFailed);
        }
        if (writer.IsConfigured) return new();
        logger.LogError("Vote document upload refused: FileStorage is not configured.");
        return new(VoteFailure.SaveFailed);
    }

    public async Task<VoteOutcome> AttachUploadAsync(Guid meetingId, Guid voteId, ParticipantActor actor,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default)
    {
        // A Guest never uploads: the check answers for them as it does before the body is read.
        if (actor.UserId is not { } uploader) return await CheckAttachUploadAsync(meetingId, voteId, actor, ct);
        var outcome = await writer.WriteAsync(uploader, files, async (stored, now, token) =>
        {
            // Re-checked under the locks: the vote may have opened, or the actor lost the role, while the files were
            // uploading (UC-68a EX-1).
            var (failure, s, viewer, vote) = await LockAsync(meetingId, voteId, actor, false, token);
            if (failure != VoteFailure.None) return new VoteOutcome(failure);
            if (!VoteRules.CanManageDraft(s!.Meeting, viewer!) || vote!.Status != VotingStatus.Draft)
                return new VoteOutcome(VoteFailure.DraftDenied);
            var added = await attacher.AddUploadsAsync(s.Meeting, uploader, stored, now,
                null, voteId, token);
            await unitOfWork.SaveChangesAsync(token);
            return new VoteOutcome(Data: added, VoteId: voteId);
        }, o => o.Failure == VoteFailure.None, new(VoteFailure.SaveFailed), new(VoteFailure.StorageFull), ct);
        if (outcome.Failure == VoteFailure.None)
            await PublishAsync(new(Guid.CreateVersion7(), meetingId, voteId, "DocumentsChanged", null, Now), true);
        return outcome;
    }

    public async Task<VoteOutcome> ConfirmReadAsync(Guid meetingId, Guid meetingDocumentId, ParticipantActor actor,
        VoteInput input, CancellationToken ct = default)
    {
        VoteViewer? viewer = null;
        try
        {
            // Nothing is staged before the early returns, so committing them writes nothing.
            return await unitOfWork.ExecuteAsync(async token =>
            {
                // Shares the meeting lock as a ballot does: closing the vote or removing the document waits for this.
                await votes.LockMeetingAsync(meetingId, true, token);
                if (actor.UserId is { } userId) await votes.ShareUserAsync(userId, token);
                var s = await participants.LoadAsync(meetingId, false, token);
                (var failure, viewer) = await AccessAsync(s, actor, token);
                if (failure == VoteFailure.Inactive) return new VoteOutcome(failure);
                // A meeting the caller cannot see, a removed document and one no visible vote links all answer alike.
                if (failure != VoteFailure.None
                    || !await votes.IsVisibleVoteDocumentAsync(meetingId, meetingDocumentId, VoteRules.IsManager(s!.Meeting, viewer!), token))
                    return new VoteOutcome(VoteFailure.DocumentUnavailable);
                // Only a voter of an Open vote that links the document confirms it, in UC-65's order: not on the fixed list,
                // then no longer accepting ballots. A confirmation nobody's ballot waits for is not recorded.
                var (eligible, open) = await votes.ReadRequirementAsync(meetingDocumentId, viewer!, token);
                if (!eligible) return new VoteOutcome(VoteFailure.NotEligible);
                if (!open || s.Meeting.Status != MeetingStatus.InProgress) return new VoteOutcome(VoteFailure.NotAccepting);
                // Confirmed once, for every vote that links the document, and never withdrawn (UC-42 AF-1, UC-42).
                if (await votes.FindReadConfirmationAsync(meetingDocumentId, viewer!, token) is { } existing)
                    return new VoteOutcome(Data: new DocumentReadView(meetingDocumentId, existing.ConfirmedAt, true));
                if (input.ParseFailure is VoteFailure.UnsupportedMedia or VoteFailure.TooLarge) return new VoteOutcome(input.ParseFailure);
                // The server records the reader's word, nothing more: only the viewer knows the last page was reached, and
                // nothing here can prove the document was read (UC-42). It is not a security check.
                if (input.LastPageReached != true) return new VoteOutcome(VoteFailure.LastPageNotReached);
                var now = Now;
                // Nothing here points at a ballot, so it cannot reveal a secret choice (BR-096, BR-097).
                votes.Add(new DocumentReadConfirmation
                {
                    MeetingDocumentId = meetingDocumentId, UserId = viewer!.UserId, GuestInvitationId = viewer.GuestInvitationId,
                    ConfirmedAt = now, CreatedAt = now, UpdatedAt = now
                });
                await unitOfWork.SaveChangesAsync(token);
                return new VoteOutcome(Data: new DocumentReadView(meetingDocumentId, now, false));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (UniqueConflictException e) when (e.Conflict == UniqueConflict.DocumentAlreadyConfirmed)
        {
            // The same person ticked twice at once: the first confirmation stands.
            unitOfWork.DiscardChanges();
            var existing = await unitOfWork.ExecuteAsync(
                token => votes.FindReadConfirmationAsync(meetingDocumentId, viewer!, token), TransactionMode.None, ct);
            return new(Data: new DocumentReadView(meetingDocumentId, existing!.ConfirmedAt, true));
        }
        catch (PersistenceException ex)
        {
            unitOfWork.DiscardChanges();
            logger.LogError(ex.InnerException ?? ex, "Failed to confirm reading document {DocumentId} of meeting {MeetingId}", meetingDocumentId, meetingId);
            return new(VoteFailure.SaveFailed);
        }
    }

    /// <summary>
    /// UC-68a main flow: meeting documents not yet attached become documents of the draft, all or none (UC-68a,
    /// UC-36b). Only while the vote is Draft, so the list is fixed once it opens (BR-094, BR-095).
    /// </summary>
    private async Task<VoteOutcome> AttachAsync(Meeting meeting, VoteViewer viewer, VotingSession vote, VoteInput input,
        CancellationToken ct)
    {
        if (!VoteRules.CanManageDraft(meeting, viewer) || vote.Status != VotingStatus.Draft) return new(VoteFailure.DraftDenied);
        if (input.ParseFailure != VoteFailure.None) return new(input.ParseFailure);
        var ids = (input.DocumentIds ?? []).Distinct().ToArray();
        var found = await votes.CountMeetingDocumentsAsync(meeting.Id, ids, ct);
        // None selected, or one removed or of another meeting: the same answer (BR-074–BR-076).
        if (ids.Length == 0 || found != ids.Length) return new(VoteFailure.DocumentNotInMeeting);
        var attached = await votes.AttachedDocumentIdsAsync(vote.Id, ct);
        if (ids.Any(attached.Contains)) return new(VoteFailure.DocumentAlreadyAttached);
        votes.AddDocumentLinks(ids.Select(id => new VoteDocument { VotingSessionId = vote.Id, MeetingDocumentId = id }));
        return new(Data: Array.Empty<UploadedMeetingDocument>(), VoteId: vote.Id);
    }

    /// <summary>
    /// UC-68a AF-2: the actor's library documents are added to the meeting and attached to the draft in the same save
    /// (UC-36b), with UC-36a's checks (BR-072, BR-073); one already in the meeting is attached as it is.
    /// </summary>
    private async Task<VoteOutcome> AttachFromLibraryAsync(Meeting meeting, VoteViewer viewer, VotingSession vote,
        VoteInput input, DateTimeOffset now, CancellationToken ct)
    {
        if (!VoteRules.CanManageDraft(meeting, viewer) || vote.Status != VotingStatus.Draft) return new(VoteFailure.DraftDenied);
        if (input.ParseFailure != VoteFailure.None) return new(input.ParseFailure);
        var (failure, added) = await attacher.AddLibraryDocumentsAsync(meeting, viewer.UserId!.Value,
            input.DocumentIds ?? [], now, null, vote.Id, ct);
        if (failure != DocumentUploadFailure.None)
            return new(failure switch
            {
                DocumentUploadFailure.LibraryUnavailable => VoteFailure.LibraryUnavailable,
                DocumentUploadFailure.AlreadyLinked => VoteFailure.DocumentAlreadyAttached,
                _ => VoteFailure.SaveFailed
            });
        return new(Data: added, VoteId: vote.Id);
    }

    /// <summary>UC-68b: only this link goes; the document stays in the meeting with its read confirmations (UC-68b).</summary>
    private async Task<VoteOutcome> DetachAsync(Meeting meeting, VoteViewer viewer, VotingSession vote, VoteInput input,
        CancellationToken ct)
    {
        if (!VoteRules.CanManageDraft(meeting, viewer) || vote.Status != VotingStatus.Draft) return new(VoteFailure.DraftDenied);
        var link = await votes.FindDocumentLinkAsync(vote.Id, input.MeetingDocumentId, ct);
        if (link is null) return new(VoteFailure.DocumentNotAttached);
        votes.Remove(link);
        return new(VoteId: vote.Id);
    }

    private async Task<(VoteOutcome, VotingSession?)> CreateAsync(ParticipantSnapshot s, VoteViewer viewer, VoteInput input,
        DateTimeOffset now, CancellationToken ct)
    {
        var meeting = s.Meeting;
        if (!VoteRules.CanManageDraft(meeting, viewer)) return (new(VoteFailure.DraftDenied), null);
        if (input.ParseFailure != VoteFailure.None) return (new(input.ParseFailure), null);
        if (await OpenFromFormDeniedAsync(meeting, viewer, null, input, ct) is var denied and not VoteFailure.None)
            return (new(denied), null);
        input = VoteRules.Normalize(input);
        var errors = VoteRules.Validate(input, out var roles);
        if (errors.Count != 0) return (new(VoteFailure.Invalid, Errors: errors), null);
        var vote = new VotingSession
        {
            MeetingId = meeting.Id, Title = input.Title!, Description = input.Description!, IsAnonymous = input.IsAnonymous!.Value,
            EligibleRoles = roles, CreatedById = viewer.UserId!.Value, CreatedAt = now, UpdatedAt = now
        };
        foreach (var (label, index) in input.Options!.Select((label, index) => (label!, index)))
            vote.Options.Add(new VotingOption { VotingSessionId = vote.Id, Position = index + 1, Label = label, CreatedAt = now, UpdatedAt = now });
        votes.Add(vote);
        // Opening from the form saves nothing at all when it fails — no draft is left behind (UC-63 AF-3).
        if (input.OpenImmediately && await OpenNowAsync(s, vote, now, ct) is var opened and not VoteFailure.None)
            return (new(opened), null);
        return (new(VoteId: vote.Id), vote);
    }

    private async Task<VoteOutcome> UpdateAsync(ParticipantSnapshot s, VoteViewer viewer, VotingSession vote, VoteInput input,
        DateTimeOffset now, CancellationToken ct)
    {
        var meeting = s.Meeting;
        // Open and Closed votes keep their configuration (BR-088).
        if (!VoteRules.CanManageDraft(meeting, viewer) || vote.Status != VotingStatus.Draft) return new(VoteFailure.DraftDenied);
        if (input.ParseFailure != VoteFailure.None) return new(input.ParseFailure);
        if (await OpenFromFormDeniedAsync(meeting, viewer, vote.Id, input, ct) is var denied and not VoteFailure.None)
            return new(denied);
        input = VoteRules.Normalize(input);
        var errors = VoteRules.Validate(input, out var roles);
        if (errors.Count != 0) return new(VoteFailure.Invalid, Errors: errors);
        vote.Title = input.Title!;
        vote.Description = input.Description!;
        vote.IsAnonymous = input.IsAnonymous!.Value;
        vote.EligibleRoles = roles;
        vote.UpdatedAt = now;
        // Rewritten in place by position, so the unique (vote, position) index never sees two rows for one slot.
        var existing = vote.Options.OrderBy(o => o.Position).ToList();
        var labels = input.Options!.Select(o => o!).ToArray();
        for (var index = 0; index < labels.Length; index++)
        {
            if (index >= existing.Count)
                votes.Add(new VotingOption { VotingSessionId = vote.Id, Position = index + 1, Label = labels[index], CreatedAt = now, UpdatedAt = now });
            else if (existing[index].Label != labels[index])
            {
                existing[index].Label = labels[index];
                existing[index].UpdatedAt = now;
            }
        }
        votes.RemoveOptions(existing.Skip(labels.Length));
        // Opening from the edit form keeps the edits only if the vote opens; otherwise the saved draft stays as it
        // was (UC-64 AF-1), because nothing is saved when this returns a failure.
        if (input.OpenImmediately && await OpenNowAsync(s, vote, now, ct) is var opened and not VoteFailure.None)
            return new(opened);
        return new(VoteId: vote.Id);
    }

    /// <summary>
    /// Opening straight from the create or edit form (UC-63 AF-3): only the current Chair, while the meeting is
    /// InProgress, and checked for another Open vote before the form is validated, in UC-64's order.
    /// </summary>
    private async Task<VoteFailure> OpenFromFormDeniedAsync(Meeting meeting, VoteViewer viewer, Guid? voteId, VoteInput input,
        CancellationToken ct)
    {
        if (!input.OpenImmediately) return VoteFailure.None;
        if (!VoteRules.IsChair(meeting, viewer) || meeting.Status != MeetingStatus.InProgress) return VoteFailure.OpenDenied;
        return await votes.AnotherOpenAsync(meeting.Id, voteId, ct) ? VoteFailure.AnotherOpen : VoteFailure.None;
    }

    private VoteOutcome Delete(Meeting meeting, VoteViewer viewer, VotingSession vote)
    {
        // Only a draft: an Open or Closed vote is evidence and is never deleted (BR-086, BR-087).
        if (!VoteRules.CanManageDraft(meeting, viewer) || vote.Status != VotingStatus.Draft) return new(VoteFailure.DraftDenied);
        votes.Remove(vote);
        return new(VoteId: vote.Id);
    }

    private async Task<VoteOutcome> OpenAsync(ParticipantSnapshot s, VoteViewer viewer, VotingSession vote, DateTimeOffset now,
        CancellationToken ct)
    {
        if (!VoteRules.IsChair(s.Meeting, viewer) || s.Meeting.Status != MeetingStatus.InProgress || vote.Status != VotingStatus.Draft)
            return new(VoteFailure.OpenDenied);
        var opened = await OpenNowAsync(s, vote, now, ct);
        return opened == VoteFailure.None ? new(VoteId: vote.Id) : new(opened);
    }

    /// <summary>
    /// Fixes who may vote, and how many, at this moment (BR-091): later role or roster changes do not move either
    /// (BR-091). Another Open vote is checked here under the meeting lock, and the database index backs it (BR-090).
    /// </summary>
    private async Task<VoteFailure> OpenNowAsync(ParticipantSnapshot s, VotingSession vote, DateTimeOffset now, CancellationToken ct)
    {
        if (await votes.AnotherOpenAsync(s.Meeting.Id, vote.Id, ct)) return VoteFailure.AnotherOpen;
        var users = VoteRules.EligibleUsers(s.Meeting, vote.EligibleRoles);
        // A Guest votes by invitation (ADR-0016); one revoked before opening is no longer invited.
        Guid[] invitations = vote.EligibleRoles.Contains(MeetingRole.Guest) ? s.Guests.Select(g => g.Id).Order().ToArray() : [];
        if (users.Count + invitations.Length == 0) return VoteFailure.NoEligibleVoters;
        vote.Status = VotingStatus.Open;
        vote.OpenedAt = now;
        vote.EligibleCount = users.Count + invitations.Length;
        vote.UpdatedAt = now;
        votes.AddEligibleVoters(users.Select(id => new VoteEligibleVoter { VotingSessionId = vote.Id, UserId = id, CreatedAt = now, UpdatedAt = now })
            .Concat(invitations.Select(id => new VoteEligibleVoter { VotingSessionId = vote.Id, GuestInvitationId = id, CreatedAt = now, UpdatedAt = now })));
        return VoteFailure.None;
    }

    private async Task<VoteOutcome> CastAsync(Meeting meeting, VoteViewer viewer, VotingSession vote, VoteInput input,
        DateTimeOffset now, CancellationToken ct)
    {
        if (!await votes.IsEligibleAsync(vote.Id, viewer, ct)) return new(VoteFailure.NotEligible);
        if (vote.Status != VotingStatus.Open || meeting.Status != MeetingStatus.InProgress) return new(VoteFailure.NotAccepting);
        if (await votes.HasVotedAsync(vote.Id, viewer, ct)) return new(VoteFailure.AlreadyCast);
        // Every vote document still in the meeting must be confirmed read first; one removed since no longer counts
        // (BR-094, BR-095, UC-65). Removal takes the meeting lock this ballot shares, so the list cannot change under it.
        var unread = (await votes.DocumentsAsync(vote.Id, viewer, ct)).Where(d => d.ReadConfirmedAt is null).ToArray();
        if (unread.Length != 0) return new(VoteFailure.DocumentsUnread, Data: unread);
        if (input.ParseFailure != VoteFailure.None) return new(input.ParseFailure);
        if (input.OptionId is not { } optionId || optionId == Guid.Empty) return new(VoteFailure.OptionMissing);
        if (vote.Options.All(o => o.Id != optionId)) return new(VoteFailure.OptionUnavailable);
        votes.Add(new VoteReceipt
        {
            VotingSessionId = vote.Id, VoterId = viewer.UserId, GuestInvitationId = viewer.GuestInvitationId,
            CastAt = now, CreatedAt = now, UpdatedAt = now
        });
        // A secret ballot is only counted; nothing is stored that says what this voter chose (BR-096, BR-097).
        if (!vote.IsAnonymous)
            votes.Add(new VoteChoice
            {
                VotingSessionId = vote.Id, VotingOptionId = optionId, VoterId = viewer.UserId,
                GuestInvitationId = viewer.GuestInvitationId, CreatedAt = now, UpdatedAt = now
            });
        // Runs at once inside this transaction; a duplicate receipt at SaveChanges rolls it back with the rest.
        await votes.CountBallotAsync(vote.Id, optionId, ct);
        return new(VoteId: vote.Id);
    }

    private async Task<VoteOutcome> CloseAsync(Meeting meeting, VoteViewer viewer, VotingSession vote, DateTimeOffset now,
        CancellationToken ct)
    {
        if (!VoteRules.IsChair(meeting, viewer) || !VoteRules.Closable(meeting.Status) || vote.Status != VotingStatus.Open)
            return new(VoteFailure.CloseDenied);
        vote.Status = VotingStatus.Closed;
        vote.ClosedAt = now;
        vote.UpdatedAt = now;
        // Counted before commit, while the vote lock keeps ballots out: a read failing after the commit would
        // otherwise report a close that did happen as failed.
        return new(Data: await votes.ResultAsync(meeting.Id, vote.Id, vote.IsAnonymous, ct), VoteId: vote.Id);
    }

    private VoteOutcome Conclude(Meeting meeting, VoteViewer viewer, VotingSession vote, VoteInput input, DateTimeOffset now)
    {
        if (!VoteRules.IsChair(meeting, viewer) || !VoteRules.Closable(meeting.Status) || vote.Status != VotingStatus.Closed)
            return new(VoteFailure.ConclusionDenied);
        // Recorded once and never replaced (BR-099).
        if (vote.Conclusion is not null) return new(VoteFailure.ConclusionRecorded);
        if (input.ParseFailure != VoteFailure.None) return new(input.ParseFailure);
        var note = input.Note?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        // The Chair decides; nothing here is inferred from the tally (BR-099).
        if (!Enum.TryParse<VoteConclusion>(input.Conclusion, false, out var conclusion) || input.Conclusion != conclusion.ToString())
            errors["conclusion"] = ["Chọn Thông qua, Không thông qua hoặc Hoãn."];
        if (note.Length > VoteRules.NoteMax) errors["note"] = [$"Ghi chú không quá {VoteRules.NoteMax} ký tự."];
        if (errors.Count != 0) return new(VoteFailure.ConclusionInvalid, Errors: errors);
        vote.Conclusion = conclusion;
        vote.ConclusionNote = note;
        vote.ConcludedById = viewer.UserId;
        vote.ConcludedAt = now;
        vote.UpdatedAt = now;
        return new(VoteId: vote.Id);
    }

    private static VoteActions Actions(Meeting meeting, VoteViewer viewer, VoteRow r, bool anotherOpen)
        => VoteRules.Actions(meeting, viewer, r.Status, r.Conclusion is not null, r.IsEligible, r.HasVoted, anotherOpen);

    private static string[] Roles(VoteRow r) => r.EligibleRoles.Select(role => role.ToString()).ToArray();

    private static VoteConclusionView? Conclusion(VoteRow r) => r.Conclusion is { } value
        ? new(value.ToString(), r.ConclusionNote, r.ConcludedById, r.ConcludedByName, r.ConcludedAt) : null;
}
