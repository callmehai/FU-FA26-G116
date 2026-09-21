using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Authentication;

/// <summary>Issues the access token (AT) and the refresh token (RT) for a signed-in account.</summary>
public interface ITokenIssuer
{
    /// <summary>
    /// A signed JWT carrying the user's id, email, role and capabilities, valid for
    /// <see cref="AccessTokenLifetime"/>. Capabilities travel inside the token so an endpoint can
    /// authorize without a database round trip — the cost is that a capability change only takes
    /// effect at the next refresh, which is the reason the lifetime is minutes rather than days.
    /// </summary>
    string CreateAccessToken(User user, IReadOnlyCollection<Capability> capabilities);

    /// <summary>
    /// A fresh opaque refresh token — cryptographically random, carrying no information. It means
    /// something only because <see cref="IRefreshTokenStore"/> remembers it.
    /// </summary>
    string CreateRefreshToken();

    TimeSpan AccessTokenLifetime { get; }

    TimeSpan RefreshTokenLifetime { get; }
}
