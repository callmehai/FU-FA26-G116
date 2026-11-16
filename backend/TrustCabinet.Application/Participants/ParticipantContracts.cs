using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Participants;

public enum ParticipantFailure { None, NotFound, Forbidden, Inactive, Locked, Invalid, InvalidRole, ChairHandoverDenied, InvalidChairRecipient, RequiredRoles, Stale, InvalidRequest, UnsupportedMedia, TooLarge, ReadFailed, SaveFailed, CannotRespond, CannotHandOverChair, ResponseFinal, ChairCannotRespond }
public enum ParticipantSuccess { Saved, Created, RoleChanged, ChairHandedOver, Removed, Responded, NoChanges }

public sealed record ParticipantActor(Guid? UserId = null, MeetingGuestSession? Guest = null);
public sealed record GuestDisplayProfile(string? DisplayName, string? JobTitle);
public interface IParticipantGuestAccess
{
    Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MeetingGuestSession>>([]);
    Task<bool> CanAccessAsync(MeetingGuestSession session, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId, IReadOnlyCollection<Guid> invitationIds, CancellationToken ct);
}
public enum GuestLinkStatus { Ready, Unavailable, Invalid }
public sealed record GuestInvitationLink(GuestLinkStatus Status, string? Url = null);
public interface IGuestInvitationLinkProvider
{
    /// <summary>Read-only readiness probe. False defers dispatch without locking the meeting or preparing credentials.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct);
    /// <summary>Prepare a verification link in the current database unit of work; never send email or issue an authenticated session.</summary>
    Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct);
}
public sealed record MeetingAccessChange(Guid MeetingId, Guid SubjectId, bool IsGuest, Guid EventId);
public interface IMeetingParticipantAccessPublisher
{
    Task PublishAsync(MeetingAccessChange change, CancellationToken ct);
}

public sealed record ParticipantChange(Guid ParticipantId, string? Role);
public sealed record ChairReplacement(Guid OutgoingParticipantId, Guid IncomingParticipantId, bool Confirmed);
public sealed record ParticipantInput(Guid[]? UserIds = null, string? Role = null,
    string? InvitedName = null, string? Email = null,
    ParticipantChange[]? Changes = null, ChairReplacement? ChairReplacement = null, string? Response = null,
    long? Revision = null, ParticipantFailure ParseFailure = ParticipantFailure.None,
    Guid? NewChairParticipantId = null, bool Confirmed = false);
public sealed record ParticipantQuery(int Page = 1, int PageSize = 20, string? Search = null, bool Invalid = false);
public sealed record ParticipantResult(ParticipantFailure Failure = ParticipantFailure.None, object? Data = null,
    long? Revision = null, ParticipantSuccess Success = ParticipantSuccess.Saved,
    IReadOnlyDictionary<string, string[]>? Errors = null);
public sealed record ParticipantPermissions(bool CanManageParticipants, bool CanReplaceChair, bool CanRespond);
public sealed record ParticipantContext(Guid MeetingId, string Status, string Revision, string? MyRole, ParticipantPermissions Permissions);
// Email is null when a Guest reads the roster, except on the Guest's own row (PO 01/11/2026, SCRUM-116).
public sealed record ParticipantRow(Guid Id, string Kind, string DisplayName, string? Email, string? Role,
    Guid? UserId, bool? IsActive, string? InvitedName, string? SelfDeclaredDisplayName, string? SelfDeclaredJobTitle,
    string Response, DateTimeOffset? RespondedAt);
public sealed record ParticipantPage(IReadOnlyList<ParticipantRow> Items, int Total, int Page, int PageSize, string Revision);
public sealed record ParticipantOption(Guid Id, string FullName, string Email);
public sealed record ChairHandoverCandidate(Guid ParticipantId, Guid UserId, string FullName, string Email);
public sealed record ChairHandoverCandidatePage(IReadOnlyList<ChairHandoverCandidate> Items, int Total,
    int EligibleTotal, int Page, int PageSize, string Revision);
public sealed record ParticipantCreationOptions(IReadOnlyList<ParticipantOption> Items, int Total, int Page, int PageSize, string[] Roles);
public sealed record ParticipantOptions(IReadOnlyList<ParticipantOption> Items, int Total, int Page, int PageSize,
    string[] Roles, string Revision);
public sealed record InvitationResponseView(Guid Id, string Kind, string Response, DateTimeOffset? RespondedAt, bool CanRespond);
public interface IParticipantService
{
    Task<ParticipantResult> ReadCreationOptionsAsync(ParticipantQuery query, CancellationToken ct);
    Task<ParticipantResult> ReadAsync(Guid meetingId, ParticipantActor actor, ParticipantOperation operation, ParticipantQuery query, CancellationToken ct);
    Task<ParticipantResult> MutateAsync(Guid meetingId, Guid? targetId, ParticipantActor actor, ParticipantOperation operation, ParticipantInput input, CancellationToken ct);
}

public sealed record ParticipantSnapshot(Meeting Meeting, IReadOnlyList<MeetingGuestInvitation> Guests);
public interface IParticipantRepository
{
    Task<ParticipantSnapshot?> LoadAsync(Guid meetingId, bool forUpdate, CancellationToken ct);

    /// <summary>
    /// Takes a shared lock on the one account row until the current transaction ends, so it cannot change
    /// e-mail or active state between validation and save. Runs immediately, not staged.
    /// </summary>
    Task LockUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Takes a shared lock on the account rows, in stable id order, until the current transaction ends, and
    /// returns them untracked: fresh rows are authoritative for account state and never overwrite tracked
    /// users. Runs immediately, not staged. One query.
    /// </summary>
    Task<IReadOnlyList<User>> LockUsersAsync(Guid[] userIds, CancellationToken ct);

    /// <summary>
    /// Active accounts not yet in the meeting: not currently assigned, and (when <paramref name="snapshot"/>
    /// is set) with no active guest invitation on the same e-mail. Two queries: a count, then one page,
    /// both untracked and ordered by name.
    /// </summary>
    Task<(int Total, IReadOnlyList<ParticipantOption> Items)> ListInvitationOptionsAsync(
        ParticipantSnapshot? snapshot, ParticipantQuery query, CancellationToken ct);

    /// <summary>Ids of the candidate readers whose accounts are still active. One untracked query.</summary>
    Task<IReadOnlyList<Guid>> ListActiveReaderIdsAsync(Guid[] candidateUserIds, CancellationToken ct);

    /// <summary>
    /// Agenda items still presented by the removed member (by account) or the revoked guest (by invitation),
    /// tracked for clearing. One query.
    /// </summary>
    Task<IReadOnlyList<AgendaItem>> ListPresentedItemsAsync(
        Guid meetingId, Guid? presenterUserId, Guid? presenterGuestInvitationId, CancellationToken ct);

    /// <summary>Whether an active guest invitation of the meeting already uses one of the e-mails. One query.</summary>
    Task<bool> ExistsActiveGuestWithEmailsAsync(Guid meetingId, string[] emails, CancellationToken ct);

    /// <summary>Whether an active guest invitation of the meeting already uses the e-mail. One query.</summary>
    Task<bool> ExistsActiveGuestWithEmailAsync(Guid meetingId, string email, CancellationToken ct);

    /// <summary>Whether a current participant of the meeting is registered under the e-mail. One query.</summary>
    Task<bool> ExistsCurrentParticipantWithEmailAsync(Guid meetingId, string email, CancellationToken ct);

    /// <summary>
    /// Stages the participant, pointing it at its account: the already-tracked instance when the unit of work
    /// holds it, else the locked row attached without inserting it. Written by the next
    /// <c>IUnitOfWork.SaveChangesAsync</c>.
    /// </summary>
    void Add(MeetingParticipant participant, User account);

    /// <summary>Stages the row; it is written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(MeetingGuestInvitation guest);

    /// <summary>
    /// Guest invitations of the meeting held by this unit of work, including rows added but not yet saved,
    /// for the audit evidence. No query.
    /// </summary>
    Task<IReadOnlyList<MeetingGuestInvitation>> ListStagedGuestsAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The meeting's open vote, if any, tracked. One query.</summary>
    Task<VotingSession?> FindOpenVoteAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Whether the vote already holds a receipt from the e-mail, through its invitation. One query.</summary>
    Task<bool> ExistsVoteReceiptWithEmailAsync(Guid votingSessionId, string email, CancellationToken ct);

    /// <summary>
    /// The vote's eligible voter row matching the e-mail, through its invitation, tracked for moving to the
    /// new invitation; null when the e-mail was never eligible. One query.
    /// </summary>
    Task<VoteEligibleVoter?> FindEligibleVoterByEmailAsync(Guid votingSessionId, string email, CancellationToken ct);
}

public sealed record ParticipantsChange(Guid EventId, Guid MeetingId, string Revision, DateTimeOffset OccurredAt);
public interface IParticipantsChangePublisher
{
    Task PublishAsync(ParticipantsChange change, IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<MeetingGuestSession> guests, CancellationToken ct);
}
