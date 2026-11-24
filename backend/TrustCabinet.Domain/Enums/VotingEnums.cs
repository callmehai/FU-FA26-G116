namespace TrustCabinet.Domain.Enums;

public enum VotingStatus
{
    Draft,
    Open,
    Closed,
}

/// <summary>The business outcome the Chair records for a closed vote (UC-67, BR-099).</summary>
public enum VoteConclusion
{
    Passed,
    NotPassed,
    Deferred,
}
