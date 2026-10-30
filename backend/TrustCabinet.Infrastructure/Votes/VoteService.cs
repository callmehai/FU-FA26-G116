using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Infrastructure.Votes;

/// <summary>
/// Votes taken during a meeting (FE-11, SCRUM-28): drafts, opening, ballots, closing and the Chair's conclusion.
/// Contract in docs/03-api/15-bieu-quyet.md.
/// </summary>
public sealed class VoteService(TrustCabinetDbContext db, VoteRepository votes, IParticipantRepository participants,
    IParticipantGuestAccess guests, IVoteChangePublisher publisher, TimeProvider clock, ILogger<VoteService> logger) : IVoteService
{
    private const string OpenVoteIndex = "ix_voting_sessions_meeting_id";
    private static readonly string[] ReceiptIndexes =
        ["ix_vote_receipts_voting_session_id_voter_id", "ix_vote_receipts_voting_session_id_guest_invitation_id"];

    // The execution strategy wraps a transient failure in InvalidOperationException, as ParticipantService handles.
    private static bool Persistence(Exception ex) =>
        ex is DbUpdateException or NpgsqlException or TimeoutException
            or InvalidOperationException { InnerException: NpgsqlException or TimeoutException };
    private DateTimeOffset Now => AgendaExecution.UtcMicroseconds(clock.GetUtcNow());

    /// <summary>
    /// Who the caller is in this meeting, re-read on every request: losing access stops new ballots at once (BR-269).
    /// A Guest has a member's rights here (BR-181) but must still hold a live verified session.
    /// </summary>
    private async Task<(VoteFailure Failure, VoteViewer? Viewer)> AccessAsync(ParticipantSnapshot? s, ParticipantActor actor,
        CancellationToken ct)
    {
        if (actor.Guest is { } g)
            return actor.UserId is null && g.IsValid && s is not null && g.MeetingId == s.Meeting.Id
                && ParticipantRules.Published(s.Meeting.Status) && s.Guests.Any(x => x.Id == g.InvitationId)
                && await guests.CanAccessAsync(g, ct)
                ? (VoteFailure.None, new(null, g.InvitationId)) : (VoteFailure.NotFound, null);
        if (actor.UserId is not { } id || !await db.Users.AnyAsync(u => u.Id == id && u.IsActive, ct))
            return (VoteFailure.Inactive, null);
        return s is not null && MeetingReadPolicy.CanRead(s.Meeting, id)
            ? (VoteFailure.None, new(id, null)) : (VoteFailure.NotFound, null);
    }

    public async Task<VoteOutcome> ListAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct = default)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var s = await participants.LoadAsync(meetingId, false, ct);
            var (failure, viewer) = await AccessAsync(s, actor, ct);
            if (failure != VoteFailure.None) return new(failure);
            var meeting = s!.Meeting;
            var rows = await votes.ListAsync(meetingId, VoteRules.IsManager(meeting, viewer!), viewer!, ct);
            var anyOpen = rows.Any(r => r.Status == VotingStatus.Open);
            return new(Data: new VoteListSnapshot(meetingId, meeting.Status.ToString(), clock.GetUtcNow(),
                VoteRules.Permissions(meeting, viewer!), rows.Select(r => new VoteSummary(r.Id, r.Title, r.Status.ToString(),
                    r.IsAnonymous, Roles(r), r.OpenedAt, r.ClosedAt, r.EligibleCount, r.ReceiptCount, r.HasVoted,
                    Conclusion(r), r.DocumentCount, Actions(meeting, viewer!, r, anyOpen))).ToArray()));
        }
        catch (Exception ex) when (Persistence(ex))
        {
            logger.LogError(ex, "Failed to read votes of meeting {MeetingId}", meetingId);
            return new(VoteFailure.ListReadFailed);
        }
    }

    public async Task<VoteOutcome> GetAsync(Guid meetingId, Guid voteId, ParticipantActor actor, CancellationToken ct = default)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var s = await participants.LoadAsync(meetingId, false, ct);
            var (failure, viewer) = await AccessAsync(s, actor, ct);
            if (failure != VoteFailure.None) return new(failure);
            var meeting = s!.Meeting;
            var r = await votes.FindAsync(meetingId, voteId, viewer!, ct);
            // A draft the caller may not see answers exactly like a vote that does not exist (BR-253).
            if (r is null || (r.Status == VotingStatus.Draft && !VoteRules.IsManager(meeting, viewer!)))
                return new(VoteFailure.NotFound);
            var options = await votes.OptionsAsync(voteId, ct);
            var anotherOpen = r.Status == VotingStatus.Draft && await votes.AnotherOpenAsync(meetingId, voteId, ct);
            // While open, only turnout is shown — never results per option (BR-260).
            var result = r.Status == VotingStatus.Closed ? await votes.ResultAsync(meetingId, voteId, r.IsAnonymous, ct) : null;
            return new(Data: new VoteDetail(meetingId, meeting.Status.ToString(), clock.GetUtcNow(), r.Id, r.Title,
                r.Description, r.Status.ToString(), r.IsAnonymous, Roles(r),
                options.Select(o => new VoteOptionView(o.Id, o.Position, o.Label)).ToArray(), r.OpenedAt, r.ClosedAt,
                r.EligibleCount, r.ReceiptCount, r.IsEligible, r.HasVoted, Conclusion(r), r.DocumentCount, result,
                Actions(meeting, viewer!, r, anotherOpen)));
        }
        catch (Exception ex) when (Persistence(ex))
        {
            logger.LogError(ex, "Failed to read vote {VoteId} of meeting {MeetingId}", voteId, meetingId);
            return new(VoteFailure.DetailReadFailed);
        }
    }

    public async Task<VoteOutcome> MutateAsync(Guid meetingId, Guid? voteId, ParticipantActor actor, VoteOperation operation,
        VoteInput input, CancellationToken ct = default)
    {
        var cast = operation == VoteOperation.Cast;
        VoteChange? change = null;
        var managersOnly = false;
        VoteOutcome outcome;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            // Lock order everywhere: meeting, caller, vote.
            await votes.LockMeetingAsync(meetingId, cast, ct);
            if (actor.UserId is { } userId) await votes.ShareUserAsync(userId, ct);
            var s = await participants.LoadAsync(meetingId, false, ct);
            var (failure, viewer) = await AccessAsync(s, actor, ct);
            if (failure != VoteFailure.None) return new(failure);
            var meeting = s!.Meeting;
            VotingSession? vote = null;
            if (voteId is { } id)
            {
                vote = await votes.LockAsync(meetingId, id, cast, ct);
                if (vote is null || (vote.Status == VotingStatus.Draft && !VoteRules.IsManager(meeting, viewer!)))
                    return new(VoteFailure.NotFound);
            }
            var now = Now;
            if (operation == VoteOperation.Create) (outcome, vote) = await CreateAsync(s, viewer!, input, now, ct);
            else outcome = operation switch
            {
                VoteOperation.Update => await UpdateAsync(s, viewer!, vote!, input, now, ct),
                VoteOperation.Delete => Delete(meeting, viewer!, vote!),
                VoteOperation.Open => await OpenAsync(s, viewer!, vote!, now, ct),
                VoteOperation.Cast => await CastAsync(meeting, viewer!, vote!, input, now, ct),
                VoteOperation.Close => await CloseAsync(meeting, viewer!, vote!, now, ct),
                _ => Conclude(meeting, viewer!, vote!, input, now)
            };
            if (outcome.Failure != VoteFailure.None) return outcome;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var opened = operation == VoteOperation.Open
                || (operation is VoteOperation.Create or VoteOperation.Update && input.OpenImmediately);
            managersOnly = operation is VoteOperation.Create or VoteOperation.Update or VoteOperation.Delete && !opened;
            var changeType = opened ? "Opened" : operation switch
            {
                VoteOperation.Create => "DraftCreated", VoteOperation.Update => "DraftUpdated", VoteOperation.Delete => "DraftDeleted",
                VoteOperation.Cast => "BallotCast", VoteOperation.Close => "Closed", _ => "ConclusionRecorded"
            };
            change = new(Guid.CreateVersion7(), meetingId, vote!.Id, changeType, opened ? vote.Title : null, now);
        }
        catch (DbUpdateException ex) when (Violates(ex, OpenVoteIndex))
        {
            db.ChangeTracker.Clear();
            return new(VoteFailure.AnotherOpen);
        }
        catch (DbUpdateException ex) when (ReceiptIndexes.Any(index => Violates(ex, index)))
        {
            // Two simultaneous ballots by the same person: the database kept the first (BR-258).
            db.ChangeTracker.Clear();
            return new(VoteFailure.AlreadyCast);
        }
        catch (Exception ex) when (Persistence(ex))
        {
            db.ChangeTracker.Clear();
            logger.LogError(ex, "Failed vote {Operation} for meeting {MeetingId}", operation, meetingId);
            return new(VoteFailure.SaveFailed);
        }
        // A committed write stays successful even when the client disconnects or push fails.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await publisher.PublishAsync(change, managersOnly, timeout.Token); }
        catch (Exception ex) { logger.LogWarning(ex, "Vote committed; realtime delivery failed for {EventId}", change.EventId); }
        return outcome;
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
        db.VotingSessions.Add(vote);
        // Opening from the form saves nothing at all when it fails — no draft is left behind (UC-53 AF-3).
        if (input.OpenImmediately && await OpenNowAsync(s, vote, now, ct) is var opened and not VoteFailure.None)
            return (new(opened), null);
        return (new(VoteId: vote.Id), vote);
    }

    private async Task<VoteOutcome> UpdateAsync(ParticipantSnapshot s, VoteViewer viewer, VotingSession vote, VoteInput input,
        DateTimeOffset now, CancellationToken ct)
    {
        var meeting = s.Meeting;
        // Open and Closed votes keep their configuration (BR-254).
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
                db.VotingOptions.Add(new VotingOption { VotingSessionId = vote.Id, Position = index + 1, Label = labels[index], CreatedAt = now, UpdatedAt = now });
            else if (existing[index].Label != labels[index])
            {
                existing[index].Label = labels[index];
                existing[index].UpdatedAt = now;
            }
        }
        db.VotingOptions.RemoveRange(existing.Skip(labels.Length));
        // Opening from the edit form keeps the edits only if the vote opens; otherwise the saved draft stays as it
        // was (UC-54 AF-1), because nothing is saved when this returns a failure.
        if (input.OpenImmediately && await OpenNowAsync(s, vote, now, ct) is var opened and not VoteFailure.None)
            return new(opened);
        return new(VoteId: vote.Id);
    }

    /// <summary>
    /// Opening straight from the create or edit form (UC-53 AF-3): only the current Chair, while the meeting is
    /// InProgress, and checked for another Open vote before the form is validated, in UC-54's order.
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
        // Only a draft: an Open or Closed vote is evidence and is never deleted (BR-189).
        if (!VoteRules.CanManageDraft(meeting, viewer) || vote.Status != VotingStatus.Draft) return new(VoteFailure.DraftDenied);
        db.VotingSessions.Remove(vote);
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
    /// Fixes who may vote, and how many, at this moment (BR-190): later role or roster changes do not move either
    /// (BR-268). Another Open vote is checked here under the meeting lock, and the database index backs it (BR-266).
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
        db.VoteEligibleVoters.AddRange(users.Select(id => new VoteEligibleVoter { VotingSessionId = vote.Id, UserId = id, CreatedAt = now, UpdatedAt = now })
            .Concat(invitations.Select(id => new VoteEligibleVoter { VotingSessionId = vote.Id, GuestInvitationId = id, CreatedAt = now, UpdatedAt = now })));
        return VoteFailure.None;
    }

    private async Task<VoteOutcome> CastAsync(Meeting meeting, VoteViewer viewer, VotingSession vote, VoteInput input,
        DateTimeOffset now, CancellationToken ct)
    {
        if (!await votes.IsEligibleAsync(vote.Id, viewer, ct)) return new(VoteFailure.NotEligible);
        if (vote.Status != VotingStatus.Open || meeting.Status != MeetingStatus.InProgress) return new(VoteFailure.NotAccepting);
        if (await votes.HasVotedAsync(vote.Id, viewer, ct)) return new(VoteFailure.AlreadyCast);
        if (input.ParseFailure != VoteFailure.None) return new(input.ParseFailure);
        if (input.OptionId is not { } optionId || optionId == Guid.Empty) return new(VoteFailure.OptionMissing);
        if (vote.Options.All(o => o.Id != optionId)) return new(VoteFailure.OptionUnavailable);
        db.VoteReceipts.Add(new VoteReceipt
        {
            VotingSessionId = vote.Id, VoterId = viewer.UserId, GuestInvitationId = viewer.GuestInvitationId,
            CastAt = now, CreatedAt = now, UpdatedAt = now
        });
        db.VoteChoices.Add(VoteRules.Choice(vote, optionId, viewer));
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
        // Recorded once and never replaced (BR-262).
        if (vote.Conclusion is not null) return new(VoteFailure.ConclusionRecorded);
        if (input.ParseFailure != VoteFailure.None) return new(input.ParseFailure);
        var note = input.Note?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        // The Chair decides; nothing here is inferred from the tally (BR-263).
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

    private static bool Violates(DbUpdateException ex, string index)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            && postgres.ConstraintName == index;
}
