using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// UC-01's <c>:Authentication Service</c>. Decides which branch a sign-in attempt takes, and
/// issues the token pair for the one that succeeds.
///
/// There is no "no system access" branch any more (it never had a code in the catalogue). It refused a
/// role with no capabilities unless that role was Member; since roles became data (ADR-0008) a role
/// without capabilities is a legitimate configuration that works exactly like Member, and deciding
/// otherwise would mean comparing role ids in business code. Pending BA confirmation — see
/// docs/03-api/04-quan-ly-vai-tro.md.
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
    IGoogleTokenVerifier googleTokenVerifier,
    IAuditService audit,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    /// <summary>
    /// Verifying a password is deliberately slow, so skipping it when the email is unknown would
    /// make "no such account" measurably faster to answer than "wrong password" — the timing alone
    /// would reveal which emails are registered. Hashing against this instead keeps both paths
    /// equally slow. It is a real hash of a random value, so nothing can match it.
    ///
    /// Produced by the injected hasher, not by <c>new PasswordHasher()</c>: it has to be a hash the
    /// same implementation's <c>Verify</c> will actually chew on. Building it from a hard-coded
    /// implementation would make the unknown-email path cheap — and differently shaped — the moment
    /// a test substitutes a hasher, which is exactly the path the timing test is there to check.
    ///
    /// Lazy because the hasher arrives by constructor injection, and per-instance because the
    /// service is registered scoped; one hash per request is a rounding error next to the verify
    /// it exists to pay for.
    /// </summary>
    private readonly Lazy<string> _dummyHash =
        new(() => passwordHasher.Hash(Guid.NewGuid().ToString()));

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
        if (!passwordHasher.Verify(user?.PasswordHash ?? _dummyHash.Value, password) || user is null)
        {
            logger.LogInformation("Sign-in rejected: invalid credentials.");
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        // Branch 2 — MSG02. Deactivated, not deleted (SCRUM-13): the row still exists because
        // their past approvals stay attributable, but it is no longer a way in.
        if (!user.IsActive)
        {
            logger.LogInformation("Sign-in rejected: account {UserId} is deactivated.", user.Id);
            return SignInResult.Failure(SignInOutcome.AccountDeactivated);
        }

        // Empty is fine: attending, reading and voting need no capability.
        var capabilities = await users.GetCapabilitiesAsync(user.RoleId, ct);

        // Branch 3 — success.
        logger.LogInformation("Sign-in succeeded for account {UserId}.", user.Id);
        return await IssueAsync(user, capabilities, ct);
    }

    public async Task<GoogleSignInResult> SignInWithGoogleAsync(
        string idToken, CancellationToken ct = default)
    {
        // Branch 1 — MSG03. Everything the verifier rejects lands here:
        // absent, malformed, expired, wrong audience, bad signature, unverified email.
        var identity = await googleTokenVerifier.VerifyAsync(idToken, ct);

        if (identity is null)
        {
            return GoogleSignInResult.Failure(GoogleSignInOutcome.InvalidGoogleToken);
        }

        var user = await users.FindByGoogleSubjectIdAsync(identity.Subject, ct);

        // Not linked yet: the first Google sign-in of an account an administrator created with
        // this (Google-verified) email links it (ADR-0009). Otherwise nothing ever writes the
        // subject, and every Google sign-in answers MSG05.
        if (user is null)
        {
            user = await users.LinkGoogleSubjectByEmailAsync(identity.Email, identity.Subject, ct);

            if (user is not null)
            {
                logger.LogInformation("Google identity linked to account {UserId}.", user.Id);
                // No details: the email is already on the user row, and the subject is a
                // credential-adjacent identifier the audit log has no use for.
                await audit.RecordAsync(
                    AuditActions.UserGoogleLinked, AuditActions.UserObjectType, user.Id, user.Id, ct: ct);
            }
            // Created for this Gmail but deactivated before its first Google sign-in: not linked,
            // but answered MSG02 like a linked deactivated account, since "not authorised" would
            // send them asking for access they already have. Telling the verified owner of the
            // address that an account under it is deactivated leaks nothing. An account already
            // linked to a *different* subject stays MSG05 — this caller is not its owner.
            else if (await users.FindByEmailAsync(identity.Email, ct)
                     is { IsActive: false, GoogleSubjectId: null } deactivated)
            {
                logger.LogInformation(
                    "Google sign-in rejected: account {UserId} is deactivated and was never linked.",
                    deactivated.Id);
                return GoogleSignInResult.Failure(GoogleSignInOutcome.AccountDeactivated);
            }
        }

        // Branch 2 — MSG05. Google proved who they are; that is not the same as being one of ours.
        // No account is created here: provisioning is an administrator's job, and self-registration
        // would let anybody holding a Google account in.
        if (user is null)
        {
            logger.LogInformation(
                "Google sign-in rejected: no internal account is linked to that Google identity.");
            return GoogleSignInResult.Failure(GoogleSignInOutcome.NotLinked);
        }

        // Branch 3 — MSG02, the same code UC-01 answers for a deactivated account.
        if (!user.IsActive)
        {
            logger.LogInformation("Google sign-in rejected: account {UserId} is deactivated.", user.Id);
            return GoogleSignInResult.Failure(GoogleSignInOutcome.AccountDeactivated);
        }

        var capabilities = await users.GetCapabilitiesAsync(user.RoleId, ct);

        // Branch 4 — success. The same token pair as a password sign-in: how someone proved who
        // they are does not change what their session is.
        logger.LogInformation("Google sign-in succeeded for account {UserId}.", user.Id);
        var issued = await IssueAsync(user, capabilities, ct);

        return new GoogleSignInResult(
            GoogleSignInOutcome.Success,
            issued.User,
            issued.Capabilities,
            issued.AccessToken,
            issued.RefreshToken,
            issued.AccessTokenExpiresAt);
    }

    public async Task<SignInResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        // Read the account before spending the token. Consumed first, a database error here would
        // leave the browser holding a spent token: its retry would be refused, and after the reuse
        // grace period read as theft, ending every session the account has. Peeked first, a failure
        // leaves the token as it was and the retry simply works.
        var owner = await refreshTokens.PeekAsync(refreshToken, ct);

        if (owner is null)
        {
            // Consumed anyway, for its verdict: a spent token presented again is how reuse is
            // detected, and that detection lives in ConsumeAsync.
            await refreshTokens.ConsumeAsync(refreshToken, ct);
            logger.LogInformation("Refresh rejected: token unknown, expired or already used.");
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        var user = await users.FindByIdAsync(owner.Value, ct);

        if (user is null)
        {
            await refreshTokens.ConsumeAsync(refreshToken, ct);
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

        // Re-read, so a capability change reaches the next access token.
        var capabilities = await users.GetCapabilitiesAsync(user.RoleId, ct);

        // Single-use: consuming it here means a replay of the same token finds nothing. Another
        // request may have spent it since the peek — then this one lost the race and is refused.
        if (await refreshTokens.ConsumeAsync(refreshToken, ct) != user.Id)
        {
            logger.LogInformation("Refresh rejected: token of account {UserId} was spent concurrently.", user.Id);
            return SignInResult.Failure(SignInOutcome.InvalidCredentials);
        }

        return await IssueAsync(user, capabilities, ct);
    }

    public async Task SignOutAsync(string refreshToken, CancellationToken ct = default)
    {
        // Blank or already-dead token: nothing to revoke. Still a successful sign-out — the client
        // is discarding its access token either way, and a 400 here would only tell a caller that
        // its token was already invalid.
        var userId = string.IsNullOrWhiteSpace(refreshToken)
            ? null
            : await refreshTokens.RevokeAsync(refreshToken, ct);

        if (userId is null)
        {
            logger.LogInformation("Sign-out with no live refresh token; nothing to revoke.");
            return;
        }

        logger.LogInformation("Sign-out: session of account {UserId} ended.", userId);
    }

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
