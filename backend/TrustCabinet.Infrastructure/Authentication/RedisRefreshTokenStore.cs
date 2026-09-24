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
    TimeProvider time,
    ILogger<RedisRefreshTokenStore> logger) : IRefreshTokenStore
{
    private const string TokenKeyPrefix = "rt:";
    private const string UsedKeyPrefix = "rt-used:";
    private const string UserKeyPrefix = "rt-user:";

    /// <summary>
    /// How long after a token is spent a second use of it is only refused, not treated as theft.
    ///
    /// Spending a token and the client receiving its replacement are not one step. A refresh the
    /// browser aborts (a reload mid-request) or one that fails after the token was consumed (a
    /// database blip) leaves the browser holding the spent token, and it presents it again at the
    /// next page load. Without this window that ordinary accident would end every session on every
    /// device. A thief replaying a stolen token minutes or days later is still caught.
    /// ponytail: fixed window; a refresh that fails and is retried only after the window has
    /// passed (an idle tab after an outage) still reads as theft. Upgrade path: un-consume the
    /// token when the refresh fails after consuming it.
    /// </summary>
    internal static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(60);

    private const int Live = 0;
    private const int SpentWithinGrace = 1;
    private const int Reused = 2;

    /// <summary>
    /// Consume and mark as used in one step. KEYS[1] is the token key, KEYS[2] its used-marker;
    /// ARGV[1] is how long the marker lives, ARGV[2] the current time and ARGV[3] the grace period,
    /// all in seconds. The marker holds "owner|spentAt".
    ///
    /// Returns {owner, 0} for a live token, {owner, 1} for one spent within the grace period,
    /// {owner, 2} for a reuse, and nil for a token never seen or long gone. A reuse deletes the
    /// marker, so one stolen token ends the account's sessions once rather than every time it is
    /// replayed — including sessions the user signed in to after the first detection.
    ///
    /// A script rather than separate commands: between a GETDEL and a SET a replay would find
    /// neither key and be waved through as merely unknown instead of caught. <c>return nil</c>
    /// rather than <c>false</c>: under RESP3 a Lua false comes back as a boolean, not a null.
    /// </summary>
    private const string ConsumeScript = """
        local owner = redis.call('GETDEL', KEYS[1])
        if owner then
          redis.call('SET', KEYS[2], owner .. '|' .. ARGV[2], 'EX', ARGV[1])
          return {owner, 0}
        end
        local spent = redis.call('GET', KEYS[2])
        if not spent then
          return nil
        end
        local separator = string.find(spent, '|', 1, true)
        local spentOwner = string.sub(spent, 1, separator - 1)
        local spentAt = tonumber(string.sub(spent, separator + 1))
        if tonumber(ARGV[2]) - spentAt < tonumber(ARGV[3]) then
          return {spentOwner, 1}
        end
        redis.call('DEL', KEYS[2])
        return {spentOwner, 2}
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
    /// <c>rt-used</c> marker for a full refresh token lifetime. Presenting it again, after
    /// <see cref="ReuseGracePeriod"/>, means two parties held the same token — the thief and the
    /// real user, in whichever order — and there is no telling which is which, so every session of
    /// that account ends. Without this, whoever refreshed first kept the session and the other was
    /// merely turned away; when the thief wins that race, the real user is the one locked out.
    /// </summary>
    public async Task<Guid?> ConsumeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            ConsumeScript,
            [TokenKey(hash), UsedKey(hash)],
            [
                (long)TimeSpan.FromDays(options.Value.RefreshTokenDays).TotalSeconds,
                time.GetUtcNow().ToUnixTimeSeconds(),
                (long)ReuseGracePeriod.TotalSeconds,
            ]);

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

        switch ((int)parts[1])
        {
            case Live:
                return userId;

            case SpentWithinGrace:
                logger.LogInformation(
                    "Refresh rejected: token of account {UserId} was spent moments ago.", userId);
                return null;

            case Reused:
                logger.LogWarning(
                    "Refresh token reuse detected for account {UserId}; revoking all of its sessions.",
                    userId);
                await RevokeAllForUserAsync(userId, ct);
                return null;

            default:
                return null;
        }
    }

    public async Task<Guid?> RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        // GETDEL rather than DEL: same effect, and it says whose session just ended, for the log.
        // The user-index entry is left behind deliberately: a dangling hash in the set costs
        // nothing, since ConsumeAsync validates against the token key.
        var stored = await redis.GetDatabase().StringGetDeleteAsync(TokenKey(Hash(refreshToken)));

        return Guid.TryParse((string?)stored, out var userId) ? userId : null;
    }

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
