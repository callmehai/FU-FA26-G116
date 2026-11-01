using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A one-time code sent to a guest invitation's email (UC-04, BR-153). Only an HMAC of the code is
/// stored. The newest challenge of an invitation is the only one that can verify, so sending a new
/// code retires the previous one without updating it.
/// </summary>
public class GuestVerificationChallenge : Entity
{
    public Guid InvitationId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
