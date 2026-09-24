using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TrustCabinet.Domain.Authentication;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// Refresh tokens in Redis.
///
/// Redis rather than a table because every property this needs is something Redis does natively:
/// the rows are short-lived and expire on their own (no cleanup job for stale tokens), every
/// operation is a single key lookup, and consuming a token has to be atomic — see
/// <see cref="ConsumeAsync"/>. A PostgreSQL table would need a sweeper, an index, and a
/// transaction to get the same guarantees.
///
/// Three kinds of key, all expiring by themselves:
///
///   rt:{sha256(token)}      → the user id.        Answers "is this token still valid, and whose?"
///   rt-used:{sha256(token)} → the user id.        Answers "was this token already spent?" — see
///                                                 <see cref="ConsumeAsync"/>.
///   rt-user:{userId}        → set of token hashes. Answers "which tokens does this account hold?",
///                                                 which is what makes revoking every session
///                                                 possible at all.
/// </summary>
public sealed class RedisRefreshTokenStore(
    IConnectionMultiplexer redis,
    IOptions<AuthenticationOptions> options,
    ILogger<RedisRefreshTokenStore> logger) : IRefreshTokenStore
{
    private const string TokenKeyPrefix = "rt:";
    private const string UsedKeyPrefix = "rt-used:";
    private const string UserKeyPrefix = "rt-user:";

    /// <summary>
    /// Consume and mark as used in one step. KEYS[1] is the token key, KEYS[2] its used-marker,
    /// ARGV[1] how long the marker lives in seconds. Returns {owner, 0} for a live token,
    /// {owner, 1} for one already spent, nil for one never seen or long gone.
    ///
    /// A script rather than GETDEL followed by SET: between those two commands a replay would find
    /// neither key, and be waved through as merely unknown instead of caught.
    /// </summary>
    private const string ConsumeScript = """
        local owner = redis.call('GETDEL', KEYS[1])
        if owner then
          redis.call('SET', KEYS[2], owner, 'EX', ARGV[1])
          return {owner, 0}
        end
        local spent = redis.call('GET', KEYS[2])
        if spent then
          return {spent, 1}
        end
        return false
        """;

    public async Task StoreAsync(
        string refreshToken, Guid userId, TimeSpan lifetime, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var db = redis.GetDatabase();

        var batch = db.CreateBatch();
        var store = batch.StringSetAsync(TokenKey(hash), userId.ToString(), lifetime);
        var index = batch.SetAddAsync(UserKey(userId), hash);
        // The index outlives the tokens inside it by a margin: if it expired first, the tokens it
        // points at would survive with no way to revoke them in bulk. Stale members are harmless —
        // ConsumeAsync validates against the token key, not this set.
        var expire = batch.KeyExpireAsync(UserKey(userId), lifetime.Add(TimeSpan.FromDays(1)));
        batch.Execute();

        await Task.WhenAll(store, index, expire);
    }

    /// <summary>
    /// Single-use via GETDEL inside <see cref="ConsumeScript"/>: reading and deleting in one step
    /// is what stops two simultaneous refreshes with the same stolen token from both reading it as
    /// valid before either deleted it.
    ///
    /// Reuse detection (OAuth 2.1 refresh token rotation): a spent token leaves an
    /// <c>rt-used</c> marker for a full refresh token lifetime. Presenting it again means two
    /// parties held the same token — the thief and the real user, in whichever order — and there is
    /// no telling which is which, so every session of that account ends. Without this, whoever
    /// refreshed first kept the session and the other was merely turned away; when the thief wins
    /// that race, the real user is the one locked out.
    /// </summary>
    public async Task<Guid?> ConsumeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            ConsumeScript,
            [TokenKey(hash), UsedKey(hash)],
            [(long)TimeSpan.FromDays(options.Value.RefreshTokenDays).TotalSeconds]);

        if (result.IsNull)
        {
            return null;
        }

        var parts = (RedisResult[])result!;

        // Cast is required: RedisResult converts explicitly to string, and Guid.TryParse needs one.
        if (!Guid.TryParse((string?)parts[0], out var userId))
        {
            return null;
        }

        if ((long)parts[1] == 1)
        {
            logger.LogWarning(
                "Refresh token reuse detected for account {UserId}; revoking all of its sessions.",
                userId);
            await RevokeAllForUserAsync(userId, ct);
            return null;
        }

        return userId;
    }

    public Task RevokeAsync(string refreshToken, CancellationToken ct = default)
        // The user-index entry is left behind deliberately: removing it would need a read to learn
        // whose token this was, and a dangling hash in the set costs nothing.
        => redis.GetDatabase().KeyDeleteAsync(TokenKey(Hash(refreshToken)));

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var hashes = await db.SetMembersAsync(UserKey(userId));

        if (hashes.Length > 0)
        {
            await db.KeyDeleteAsync([.. hashes.Select(h => TokenKey(h!))]);
        }

        await db.KeyDeleteAsync(UserKey(userId));
    }

    /// <summary>
    /// What gets stored, so that a dump of Redis is not a pile of usable session tokens.
    ///
    /// A plain SHA-256 with no salt or stretching, unlike a password: the input is already 256
    /// bits of cryptographic randomness, so there is nothing to brute-force or look up in a
    /// rainbow table, and the lookup has to stay a single fast keyed read.
    /// </summary>
    private static string Hash(string refreshToken)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static RedisKey TokenKey(string hash) => TokenKeyPrefix + hash;

    private static RedisKey UsedKey(string hash) => UsedKeyPrefix + hash;

    private static RedisKey UserKey(Guid userId) => UserKeyPrefix + userId;
}
