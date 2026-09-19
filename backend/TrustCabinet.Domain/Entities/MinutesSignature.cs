using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A signature on issued minutes (SCRUM-37).
///
/// **Simulated, not real PKI.** Keys are generated inside the application; there is no USB token,
/// no HSM and no certificate authority, because real signing hardware is out of scope. The
/// cryptography does prove the content has not changed since signing, but it proves nothing about
/// who holds the key, so a signature here has no legal weight. Every screen showing one must say
/// so — see SCRUM-37.
/// </summary>
public class MinutesSignature : Entity
{
    public Guid MinutesId { get; set; }
    public MeetingMinutes Minutes { get; set; } = null!;

    public Guid SignerId { get; set; }
    public User Signer { get; set; } = null!;

    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>SHA-256 of the content at signing time, hex-encoded. A later edit breaks the match.</summary>
    public required string ContentSha256 { get; set; }

    /// <summary>The signature bytes, base64-encoded.</summary>
    public required string Signature { get; set; }

    /// <summary>The signer's public key, so the signature can be checked later.</summary>
    public required string PublicKey { get; set; }
}
