namespace TrustCabinet.Domain.Summaries;

public static class MinutesLimits
{
    /// <summary>Characters of HTML, as sent and as stored: HTML is wordier than the text it carries.</summary>
    public const int Content = 300_000;

    /// <summary>Characters of the reason the Chair gives for returning the minutes (BR-407).</summary>
    public const int ReturnReason = 2_000;
}

public static class SummaryLimits
{
    public const int Objectives = 2000;
    public const int Discussion = 12000;
    public const int Conclusions = 8000;
}
