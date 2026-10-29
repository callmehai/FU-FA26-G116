namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Links a meeting document to a vote of the same meeting (UC-76, BR-287). Eligible voters confirm
/// reading each one before casting a ballot (UC-74). Attached or detached only while the vote is
/// Draft, so the list is fixed once it opens (BR-288); detaching keeps the document in the meeting
/// (BR-305).
/// </summary>
public class VoteDocument
{
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    public Guid MeetingDocumentId { get; set; }
    public MeetingDocument MeetingDocument { get; set; } = null!;
}
