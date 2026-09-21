using System.Security.Cryptography;
using System.Text;
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
/// Two kinds of key, both expiring by themselves:
///
///   rt:{sha256(token)}  → the user id.        Answers "is this token still valid, and whose?"
///   rt-user:{userId}    → set of token hashes. Answers "which tokens does this account hold?",
///                         which is what makes revoking every session possible at all.
/// </summary>
public sealed class RedisRefreshTokenStore(IConnectionMultiplexer redis) : IRefreshTokenStore
{
    private const string TokenKeyPrefix = "rt:";
    private const string UserKeyPrefix = "rt-user:";

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

    public async Task<Guid?> ConsumeAsync(string refreshToken, CancellationToken ct = default)
    {
        // GETDEL, not GET-then-DEL: reading and deleting in one command is what makes the token
        // single-use under concurrency. With two commands, two simultaneous refreshes with the
        // same stolen token would both read it as valid before either deleted it.
        var stored = await redis.GetDatabase().StringGetDeleteAsync(TokenKey(Hash(refreshToken)));

        // Cast is required: RedisValue converts implicitly to both string and ReadOnlySpan<byte>,
        // so Guid.TryParse cannot pick an overload on its own.
        return Guid.TryParse((string?)stored, out var userId) ? userId : null;
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

    private static RedisKey UserKey(Guid userId) => UserKeyPrefix + userId;
}
