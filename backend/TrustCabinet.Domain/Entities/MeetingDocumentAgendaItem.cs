namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Links a meeting document to an agenda item of the same meeting. A document may be linked to
/// several items (ADR-0020 §5); one with no link is a general meeting document, and one with links is
/// general too unless taken out of that group (ADR-0043).
/// </summary>
public class MeetingDocumentAgendaItem
{
    public Guid MeetingDocumentId { get; set; }
    public MeetingDocument MeetingDocument { get; set; } = null!;

    public Guid AgendaItemId { get; set; }
    public AgendaItem AgendaItem { get; set; } = null!;
}
