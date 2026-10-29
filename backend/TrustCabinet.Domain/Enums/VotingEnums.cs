namespace TrustCabinet.Domain.Enums;

/// <summary>
/// When the vote is held. Both kinds share one set of tables because they produce the same
/// evidence and both land in the minutes — see SCRUM-24 and SCRUM-28.
/// </summary>
public enum VotingKind
{
    /// <summary>Opened and closed during the sitting.</summary>
    Live,
    /// <summary>Opinion collected before the meeting, open until a deadline.</summary>
    Asynchronous,
}

public enum VotingStatus
{
    Draft,
    Open,
    Closed,
}
