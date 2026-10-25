using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One numbered item on a meeting's programme. The chair walks the meeting down this list, and
/// much of what is produced during the sitting — notes, speeches — hangs off an item. Votes do not:
/// they belong to the meeting itself (BR-285).
/// </summary>
public class AgendaItem : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>Order on the programme, starting at 1. Reordering rewrites these.</summary>
    public int Position { get; set; }

    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    public int PlannedDurationMinutes { get; set; }

    /// <summary>The presenter when they are a user of the system.</summary>
    public Guid? PresenterId { get; set; }
    public User? Presenter { get; set; }

    /// <summary>Used when the presenter is a guest who has no account.</summary>
    public string PresenterName { get; set; } = string.Empty;

    public AgendaItemStatus Status { get; set; } = AgendaItemStatus.Pending;

    /// <summary>Set when the chair opens the item; with <see cref="ActualEndedAt"/> it gives the
    /// real time spent, which the minutes report against the planned duration.</summary>
    public DateTimeOffset? ActualStartedAt { get; set; }

    public DateTimeOffset? ActualEndedAt { get; set; }

    public ICollection<MeetingDocumentAgendaItem> DocumentLinks { get; set; } = [];
}
