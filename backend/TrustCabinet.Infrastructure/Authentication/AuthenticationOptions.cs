using System.ComponentModel.DataAnnotations;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// Token settings, bound from the <c>Authentication</c> configuration section.
///
/// <see cref="SigningKey"/> is a credential: it belongs in user-secrets locally and an
/// environment variable in deployment, never in appsettings.json, which is committed.
/// </summary>
public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// HMAC-SHA256 signing key for the access token. Anyone holding it can mint a token for any
    /// account, so it is treated exactly like a database password.
    ///
    /// The 32-byte floor is not a style preference: HMAC-SHA256 keys shorter than the hash output
    /// weaken the signature, and the JWT library refuses them outright.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "Authentication:SigningKey must be at least 32 characters.")]
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "TrustCabinet";

    public string Audience { get; set; } = "TrustCabinet";

    /// <summary>
    /// Kept short because a JWT cannot be revoked before it expires — a deactivated account keeps
    /// working for at most this long. Lengthening it widens that window.
    /// </summary>
    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// How long a session survives without signing in again. Revocable at any moment, since the
    /// refresh token is only valid while the store holds it.
    /// </summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}
