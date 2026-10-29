using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One question put to a vote during a meeting (FE-11). A vote belongs to the meeting, never to an
/// agenda item (BR-285, ADR-0014): it is prepared as a draft before or during the sitting, opened
/// and cast only while the meeting is InProgress, and may still be closed and concluded after it
/// ends (ADR-0017, ADR-0019).
/// </summary>
public class VotingSession : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Secret ballot when true, public when false (BR-272). In a secret vote no row ever links a
    /// voter to what they chose — see <see cref="VoteChoice"/>. This is enforced in the data, not by
    /// hiding names in the UI (BR-259).
    /// </summary>
    public bool IsAnonymous { get; set; }

    public VotingStatus Status { get; set; } = VotingStatus.Draft;

    /// <summary>
    /// The meeting roles the Chair lets vote; all four by default (BR-257). Turned into the fixed
    /// <see cref="EligibleVoters"/> list when the vote opens.
    /// </summary>
    public List<MeetingRole> EligibleRoles { get; set; } =
        [MeetingRole.Chair, MeetingRole.Secretary, MeetingRole.Member, MeetingRole.Guest];

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>
    /// How many people were entitled to vote, frozen when the vote opened (BR-190). Stored because the
    /// participant list can change afterwards and the recorded result must stay reproducible.
    /// </summary>
    public int EligibleCount { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    /// <summary>
    /// What the Chair concluded from the closed tally; null until recorded, and never changed once it
    /// is (BR-262). The system never infers it from the counts (BR-263).
    /// </summary>
    public VoteConclusion? Conclusion { get; set; }

    public string ConclusionNote { get; set; } = string.Empty;

    public Guid? ConcludedById { get; set; }
    public User? ConcludedBy { get; set; }

    public DateTimeOffset? ConcludedAt { get; set; }

    public ICollection<VotingOption> Options { get; set; } = [];
    public ICollection<VoteEligibleVoter> EligibleVoters { get; set; } = [];
    public ICollection<VoteDocument> Documents { get; set; } = [];
    public ICollection<VoteReceipt> Receipts { get; set; } = [];
    public ICollection<VoteChoice> Choices { get; set; } = [];
}
