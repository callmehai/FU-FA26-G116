using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Participants;

public enum AssignmentStatus { Current, Removed }
public enum ParticipantOperation { Context, Roster, Options, OwnResponse, AddInternal, AddGuest, ChangeAssignments, RemoveInternal, RevokeGuest, Respond }
public enum ParticipantFailure { None, NotFound, Forbidden, Inactive, Locked, Invalid, InvalidRole, ChairHandoverDenied, InvalidChairRecipient, RequiredRoles, Stale, InvalidRequest, UnsupportedMedia, TooLarge, ReadFailed, SaveFailed, CannotRespond }
public enum ParticipantSuccess { Saved, Created, RoleChanged, ChairHandedOver, Removed, Responded, NoChanges }

public sealed record ParticipantActor(Guid? UserId = null, ParticipantGuestSession? Guest = null);
public sealed record ParticipantGuestSession(Guid MeetingId, Guid InvitationId, Guid SessionId)
{
    public bool IsValid => MeetingId != Guid.Empty && InvitationId != Guid.Empty && SessionId != Guid.Empty;
}
public sealed record GuestDisplayProfile(string? DisplayName, string? JobTitle);
public interface IParticipantGuestAccess
{
    Task<IReadOnlyList<ParticipantGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ParticipantGuestSession>>([]);
    Task<bool> CanAccessAsync(ParticipantGuestSession session, CancellationToken ct);
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
    long? Revision = null, ParticipantFailure ParseFailure = ParticipantFailure.None);
public sealed record ParticipantQuery(int Page = 1, int PageSize = 20, string? Search = null, bool Invalid = false);
public sealed record ParticipantResult(ParticipantFailure Failure = ParticipantFailure.None, object? Data = null,
    long? Revision = null, ParticipantSuccess Success = ParticipantSuccess.Saved,
    IReadOnlyDictionary<string, string[]>? Errors = null);
public sealed record ParticipantPermissions(bool CanManageParticipants, bool CanReplaceChair, bool CanRespond);
public sealed record ParticipantContext(Guid MeetingId, string Status, string Revision, string? MyRole, ParticipantPermissions Permissions);
public sealed record ParticipantRow(Guid Id, string Kind, string DisplayName, string Email, string? Role,
    Guid? UserId, bool? IsActive, string? InvitedName, string? SelfDeclaredDisplayName, string? SelfDeclaredJobTitle,
    string Response, DateTimeOffset? RespondedAt);
public sealed record ParticipantPage(IReadOnlyList<ParticipantRow> Items, int Total, int Page, int PageSize, string Revision, ResponseTotals Totals);
public sealed record ParticipantOption(Guid Id, string FullName, string Email);
public sealed record ParticipantOptions(IReadOnlyList<ParticipantOption> Items, int Total, int Page, int PageSize,
    string[] Roles, string Revision);
public sealed record InvitationResponseView(Guid Id, string Kind, string Response, DateTimeOffset? RespondedAt, bool CanRespond);
public sealed record ResponseTotals(int Pending, int Accepted, int Declined, int Total);
public interface IParticipantService
{
    Task<ParticipantResult> ReadAsync(Guid meetingId, ParticipantActor actor, ParticipantOperation operation, ParticipantQuery query, CancellationToken ct);
    Task<ParticipantResult> MutateAsync(Guid meetingId, Guid? targetId, ParticipantActor actor, ParticipantOperation operation, ParticipantInput input, CancellationToken ct);
}

public sealed record ParticipantSnapshot(Meeting Meeting, IReadOnlyList<MeetingGuestInvitation> Guests);
public interface IParticipantRepository
{
    Task<ParticipantSnapshot?> LoadAsync(Guid meetingId, bool forUpdate, CancellationToken ct);
}

public sealed record ParticipantsChange(Guid EventId, Guid MeetingId, string Revision, DateTimeOffset OccurredAt);
public interface IParticipantsChangePublisher
{
    Task PublishAsync(ParticipantsChange change, IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<ParticipantGuestSession> guests, CancellationToken ct);
}
