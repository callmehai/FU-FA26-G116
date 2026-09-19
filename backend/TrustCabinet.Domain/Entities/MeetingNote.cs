using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// The secretary's running notes, typed during the sitting and flowing into the minutes draft
/// (SCRUM-30). One note per author per agenda item, so two people taking notes do not overwrite
/// each other.
/// </summary>
public class MeetingNote : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>Null for notes about the meeting as a whole rather than one item.</summary>
    public Guid? AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
}
