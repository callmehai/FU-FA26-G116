using System.ComponentModel.DataAnnotations;

namespace TrustCabinet.Infrastructure.GuestAccess;

/// <summary>UC-04 limits, bound from <c>GuestAccess</c>. Values settled 30/10/2026 (SCRUM-117).</summary>
public sealed class GuestAccessOptions
{
    public const string SectionName = "GuestAccess";

    [Range(1, 60)]
    public int CodeLifetimeMinutes { get; set; } = 15;

    /// <summary>The wrong guess that reaches this count retires the code; the guest must request a new one.</summary>
    [Range(1, 20)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 3600)]
    public int ResendIntervalSeconds { get; set; } = 60;

    /// <summary>BR-155. The guest token expires with the session; there is no refresh.</summary>
    [Range(1, 72)]
    public int SessionLifetimeHours { get; set; } = 12;
}
