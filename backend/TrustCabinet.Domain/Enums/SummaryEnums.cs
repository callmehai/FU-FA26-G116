namespace TrustCabinet.Domain.Enums;

/// <summary>Where the AI draft of a meeting (SCRUM-36) stands. Stored as a string.</summary>
public enum SummaryStatus
{
    /// <summary>Asked for; waits for the AI worker, which drafts one meeting at a time.</summary>
    Queued,
    Running,
    Ready,
    Failed,
}
