namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Links a meeting document to a vote of the same meeting (UC-68, UC-68a). Eligible voters confirm
/// reading each one before casting a ballot (UC-42). Attached or detached only while the vote is
/// Draft, so the list is fixed once it opens (BR-094, BR-095); detaching keeps the document in the meeting
/// (UC-68b).
/// </summary>
public class VoteDocument
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    public Guid MeetingDocumentId { get; set; }
    public MeetingDocument MeetingDocument { get; set; } = null!;
}
