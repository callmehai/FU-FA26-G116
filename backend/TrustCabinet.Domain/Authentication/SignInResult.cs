using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// What <c>IAuthenticationService.SignInAsync</c> hands back to the endpoint.
///
/// On failure everything but <see cref="Outcome"/> is null: a caller that leaks a user object on
/// a failed attempt cannot happen by accident. Use <see cref="Succeeded"/> rather than checking
/// the token for null.
/// </summary>
public sealed record SignInResult(
    SignInOutcome Outcome,
    User? User = null,
    IReadOnlyCollection<Capability>? Capabilities = null,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTimeOffset? AccessTokenExpiresAt = null)
{
    public bool Succeeded => Outcome == SignInOutcome.Success;

    public static SignInResult Failure(SignInOutcome outcome) => new(outcome);
}
