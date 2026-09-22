using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// The outcome of a Google sign-in, plus the tokens when it succeeded.
///
/// Mirrors <see cref="SignInResult"/>: on failure every field but <see cref="Outcome"/> is null,
/// so no code path can leak an account object for an attempt that was refused.
/// </summary>
public sealed record GoogleSignInResult(
    GoogleSignInOutcome Outcome,
    User? User = null,
    IReadOnlyCollection<Capability>? Capabilities = null,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTimeOffset? AccessTokenExpiresAt = null)
{
    public bool Succeeded => Outcome == GoogleSignInOutcome.Success;

    public static GoogleSignInResult Failure(GoogleSignInOutcome outcome) => new(outcome);
}
