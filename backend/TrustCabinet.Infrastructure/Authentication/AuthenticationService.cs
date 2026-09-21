using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// UC-01's <c>:Authentication Service</c>. Decides which of the four branches a sign-in attempt
/// takes, and issues the token pair for the one that succeeds.
///
/// The order of the checks is part of the design: credentials first, then whether the account is
/// usable. Checking "is this account active?" before the password would let anyone learn which
/// emails exist by watching which ones answer differently.
/// </summary>
public sealed class AuthenticationService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IRefreshTokenStore refreshTokens,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    /// <summary>
    /// Verifying a password is deliberately slow, so skipping it when the email is unknown would
    /// make "no such account" measurably faster to answer than "wrong password" — the timing alone
    /// would reveal which emails are registered. Hashing against this instead keeps both paths
    /// equally slow. It is a real hash of a random value, so nothing can match it.
    /// </summary>
    private static readonly string DummyHash = new PasswordHasher().Hash(Guid.NewGuid().ToString());

    public async Task<SignInResult> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        // The endpoint rejects blank input before this point; this is the service's own guard, so
        // the rule holds no matter who calls it.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        var user = await users.FindByEmailAsync(email.Trim(), ct);

        // Branch 1 — MSG01. One outcome for "no such email" and for "wrong password": the caller
        // must not be able to tell the two apart (SCRUM-11 acceptance criteria).
        if (!passwordHasher.Verify(user?.PasswordHash ?? DummyHash, password) || user is null)
        {
            logger.LogInformation("Sign-in rejected: invalid credentials.");
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        // Branch 2 — MSG03. Deactivated, not deleted (SCRUM-13): the row still exists because
        // their past approvals stay attributable, but it is no longer a way in.
        if (!user.IsActive)
        {
            logger.LogInformation("Sign-in rejected: account {UserId} is deactivated.", user.Id);
            return SignInResult.Failure(SignInOutcome.AccountDeactivated);
        }

        var capabilities = await users.GetCapabilitiesAsync(user.Role, ct);

        // Branch 3 — MSG04. A role that grants nothing and is not Member means someone has
        // emptied role_capabilities for it, so there is nothing to sign in to. Member is excluded
        // because it carries no capability by design and still signs in — attending, reading and
        // voting need none (see ReferenceData.RoleCapabilities).
        if (capabilities.Count == 0 && user.Role != SystemRole.Member)
        {
            logger.LogWarning(
                "Sign-in rejected: role {Role} grants no capability, so account {UserId} has no way in.",
                user.Role, user.Id);
            return SignInResult.Failure(SignInOutcome.NoSystemAccess);
        }

        // Branch 4 — success.
        logger.LogInformation("Sign-in succeeded for account {UserId}.", user.Id);
        return await IssueAsync(user, capabilities, ct);
    }

    public async Task<SignInResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        // Single-use: consuming it here means a replay of the same token finds nothing.
        var userId = await refreshTokens.ConsumeAsync(refreshToken, ct);

        if (userId is null)
        {
            logger.LogInformation("Refresh rejected: token unknown, expired or already used.");
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        var user = await users.FindByIdAsync(userId.Value, ct);

        if (user is null)
        {
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        // Re-checked on every refresh, not just at sign-in: deactivating someone has to end their
        // session, and it would not if refreshing only looked at the token. Every token they hold
        // goes too, so a second refresh token cannot carry the session on.
        if (!user.IsActive)
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, ct);
            logger.LogInformation("Refresh rejected: account {UserId} is deactivated.", user.Id);
            return SignInResult.Failure(SignInOutcome.AccountDeactivated);
        }

        var capabilities = await users.GetCapabilitiesAsync(user.Role, ct);

        if (capabilities.Count == 0 && user.Role != SystemRole.Member)
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, ct);
            return SignInResult.Failure(SignInOutcome.NoSystemAccess);
        }

        return await IssueAsync(user, capabilities, ct);
    }

    public Task SignOutAsync(string refreshToken, CancellationToken ct = default)
        // Blank token: nothing to revoke. Still a successful sign-out — the client is discarding
        // its access token either way, and a 400 here would only tell a caller that its token was
        // already invalid.
        => string.IsNullOrWhiteSpace(refreshToken)
            ? Task.CompletedTask
            : refreshTokens.RevokeAsync(refreshToken, ct);

    private async Task<SignInResult> IssueAsync(
        User user, IReadOnlyCollection<Capability> capabilities, CancellationToken ct)
    {
        var accessToken = tokenIssuer.CreateAccessToken(user, capabilities);
        var refreshToken = tokenIssuer.CreateRefreshToken();

        await refreshTokens.StoreAsync(refreshToken, user.Id, tokenIssuer.RefreshTokenLifetime, ct);

        return new SignInResult(
            SignInOutcome.Success,
            user,
            capabilities,
            accessToken,
            refreshToken,
            DateTimeOffset.UtcNow.Add(tokenIssuer.AccessTokenLifetime));
    }
}
