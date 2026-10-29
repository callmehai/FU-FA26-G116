using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One numbered item on a meeting's programme. The chair walks the meeting down this list, and
/// almost everything produced during the sitting — notes, votes, speeches — hangs off an item.
/// </summary>
public class AgendaItem : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>Order on the programme, starting at 1. Reordering rewrites these.</summary>
    public int Position { get; set; }

    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>The presenter when they are a user of the system.</summary>
    public Guid? PresenterId { get; set; }
    public User? Presenter { get; set; }

    /// <summary>Historical free text; never an active presenter assignment.</summary>
    public string LegacyPresenterName { get; set; } = string.Empty;

    public Guid? PresenterGuestInvitationId { get; set; }
    public MeetingGuestInvitation? PresenterGuestInvitation { get; set; }

    public string? Conclusion { get; set; }
    public Guid? ConclusionUpdatedById { get; set; }
    public User? ConclusionUpdatedBy { get; set; }
    public DateTimeOffset? ConclusionUpdatedAt { get; set; }

    public AgendaItemStatus Status { get; set; } = AgendaItemStatus.Pending;

    /// <summary>Server timestamp recorded when the chair starts the item.</summary>
    public DateTimeOffset? ActualStartedAt { get; set; }

    public DateTimeOffset? ActualEndedAt { get; set; }

    public ICollection<MeetingDocumentAgendaItem> DocumentLinks { get; set; } = [];
    public ICollection<VotingSession> VotingSessions { get; set; } = [];
}
