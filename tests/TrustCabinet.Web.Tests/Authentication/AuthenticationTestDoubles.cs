using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// Hand-written stand-ins for the authentication service's three collaborators.
///
/// Hand-written rather than a mocking library: the project has no mocking dependency, these are a
/// few lines each, and a fake that records what it was asked reads better in an assertion than a
/// verify-call. They also let a test assert on real behaviour — that a token really was stored,
/// not merely that StoreAsync was invoked.
/// </summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];
    private readonly Dictionary<SystemRole, Capability[]> _capabilities = [];

    /// <summary>How many times the service went looking for a user. Used to prove a short-circuit.</summary>
    public int LookupCount { get; private set; }

    public FakeUserRepository WithUser(User user)
    {
        _users.Add(user);
        return this;
    }

    public FakeUserRepository WithCapabilities(SystemRole role, params Capability[] capabilities)
    {
        _capabilities[role] = capabilities;
        return this;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        LookupCount++;
        // citext in PostgreSQL, so the fake has to compare case-insensitively too — otherwise a
        // test would pass here and fail against the real database.
        return Task.FromResult(_users.FirstOrDefault(
            u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> FindByGoogleSubjectIdAsync(string? googleSubjectId, CancellationToken ct = default)
    {
        // Mirrors UserRepository exactly, including the blank-subject guard. An earlier version of
        // this fake guarded against a null subject while production did not, so the tests passed
        // while a token with no subject claim signed the caller in as an arbitrary unlinked
        // account. A fake that is safer than the real thing hides the bug it should expose.
        if (string.IsNullOrWhiteSpace(googleSubjectId))
        {
            return Task.FromResult<User?>(null);
        }

        // Case-sensitive and exact, unlike the email: Google's subject is an opaque identifier,
        // not something a person types.
        return Task.FromResult(_users.FirstOrDefault(u => u.GoogleSubjectId == googleSubjectId));
    }

    public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        SystemRole role, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Capability>>(
            _capabilities.TryGetValue(role, out var caps) ? caps : []);
}

/// <summary>
/// Reversible stand-in for the password hasher: the real one is deliberately slow, and every test
/// in this file would pay for it.
/// </summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    /// <summary>
    /// How many times verification was attempted. This is what makes the timing defence testable:
    /// the property is "an unknown email still costs one verification", and counting the calls
    /// pins it exactly, where measuring elapsed time would be flaky on a loaded machine.
    /// </summary>
    public int VerifyCount { get; private set; }

    /// <summary>
    /// How many times a hash was produced through this instance. The service's dummy hash has to
    /// come from the hasher it was given, so that hash and verify are the same implementation;
    /// this is what shows it did.
    /// </summary>
    public int HashCount { get; private set; }

    /// <summary>Every stored hash <see cref="Verify"/> was asked about, in order.</summary>
    public List<string> VerifiedHashes { get; } = [];

    public string Hash(string password)
    {
        HashCount++;
        return $"hashed:{password}";
    }

    public bool Verify(string passwordHash, string providedPassword)
    {
        VerifyCount++;
        VerifiedHashes.Add(passwordHash);
        // Hash() would inflate HashCount with verifications, so the shape is spelled out here.
        return passwordHash == $"hashed:{providedPassword}";
    }
}

/// <summary>
/// Stand-in for Google's token verification. Accepts only tokens registered with
/// <see cref="WithValidToken"/>, so a test can exercise the "token did not verify" branch without
/// a network call or a genuinely signed token.
/// </summary>
internal sealed class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly Dictionary<string, GoogleIdentity> _valid = [];

    public FakeGoogleTokenVerifier WithValidToken(string idToken, string subject, string email)
    {
        _valid[idToken] = new GoogleIdentity(subject, email);
        return this;
    }

    public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default)
        => Task.FromResult(_valid.GetValueOrDefault(idToken));
}

/// <summary>Issues predictable tokens so a test can assert on an exact value.</summary>
internal sealed class FakeTokenIssuer : ITokenIssuer
{
    private int _issued;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(7);

    public string CreateAccessToken(User user, IReadOnlyCollection<Capability> capabilities)
        => $"at:{user.Id}:{string.Join(',', capabilities)}";

    // Numbered, so a test can tell a rotated token from the one it replaced.
    public string CreateRefreshToken() => $"rt-{++_issued}";
}

/// <summary>
/// In-memory refresh token store with the same contract as the Redis one — single-use consume,
/// expiry, and revoke-all-for-user.
/// </summary>
internal sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    private readonly Dictionary<string, (Guid UserId, DateTimeOffset ExpiresAt)> _tokens = [];

    /// <summary>Overridable clock, so an expiry test does not have to wait seven days.</summary>
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<string> ActiveTokens => _tokens.Keys.ToArray();

    public Task StoreAsync(string refreshToken, Guid userId, TimeSpan lifetime, CancellationToken ct = default)
    {
        _tokens[refreshToken] = (userId, Now.Add(lifetime));
        return Task.CompletedTask;
    }

    public Task<Guid?> ConsumeAsync(string refreshToken, CancellationToken ct = default)
    {
        if (!_tokens.TryGetValue(refreshToken, out var entry))
        {
            return Task.FromResult<Guid?>(null);
        }

        // Single-use: gone whether or not it turned out to be expired.
        _tokens.Remove(refreshToken);

        return Task.FromResult<Guid?>(entry.ExpiresAt <= Now ? null : entry.UserId);
    }

    public Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        _tokens.Remove(refreshToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs at the start of <see cref="RevokeAllForUserAsync"/>, before anything is removed.
    ///
    /// Two things need it: asserting *where* a revoke happens (it must be outside the invariant
    /// lock, not inside), and making the store fail on demand, since a real Redis being down must
    /// not undo a database write that already committed.
    /// </summary>
    public Action? OnRevokeAll { get; set; }

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        OnRevokeAll?.Invoke();

        foreach (var token in _tokens.Where(t => t.Value.UserId == userId).Select(t => t.Key).ToArray())
        {
            _tokens.Remove(token);
        }

        return Task.CompletedTask;
    }
}
