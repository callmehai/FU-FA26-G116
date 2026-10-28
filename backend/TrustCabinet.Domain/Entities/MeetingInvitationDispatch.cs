using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

public enum InvitationEventKind { Invitation, RoleChanged, Removed, Rescheduled, Cancelled }
public enum InvitationDispatchState { Pending, BlockedDependency, Materialized, Suppressed, Failed }

/// <summary>A durable recipient-specific business event, materialized into the existing SMTP queue.</summary>
public class MeetingInvitationDispatch : Entity
{
    public Guid EventId { get; set; }
    public Guid MeetingId { get; set; }
    public Guid? ParticipantId { get; set; }
    public Guid? GuestInvitationId { get; set; }
    public InvitationEventKind EventKind { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public int PayloadVersion { get; set; } = 1;
    public string PayloadJson { get; set; } = string.Empty;
    public InvitationDispatchState State { get; set; }
    public int DependencyAttemptCount { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string LastError { get; set; } = string.Empty;
    public Guid? EmailMessageId { get; set; }
}
