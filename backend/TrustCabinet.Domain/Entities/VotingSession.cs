using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One question put to a vote. The same shape covers the opinion collected before the meeting
/// (SCRUM-24) and the vote held during it (SCRUM-28); <see cref="Kind"/> says which.
/// </summary>
public class VotingSession : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    /// <summary>The agenda item this decides, when there is one.</summary>
    public Guid? AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    public VotingKind Kind { get; set; } = VotingKind.Live;

    /// <summary>
    /// When true, no row ever links a voter to what they chose — see <see cref="VoteChoice"/>.
    /// This is enforced in the data, not by hiding names in the UI (SCRUM-28).
    /// </summary>
    public bool IsAnonymous { get; set; }

    public VotingStatus Status { get; set; } = VotingStatus.Draft;

    public DateTimeOffset? OpensAt { get; set; }

    /// <summary>The deadline for an asynchronous vote; null for a live one the chair closes by hand.</summary>
    public DateTimeOffset? ClosesAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>
    /// How many people were entitled to vote, frozen when the session opened. Stored because the
    /// participant list can change afterwards and the recorded result must stay reproducible.
    /// </summary>
    public int EligibleCount { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    /// <summary>What the meeting concluded from the result, written by the chair or secretary.</summary>
    public string Conclusion { get; set; } = string.Empty;

    public ICollection<VotingOption> Options { get; set; } = [];
    public ICollection<VoteReceipt> Receipts { get; set; } = [];
    public ICollection<VoteChoice> Choices { get; set; } = [];
}
