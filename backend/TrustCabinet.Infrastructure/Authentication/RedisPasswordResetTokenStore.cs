using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using TrustCabinet.Domain.Authentication;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// UC-08's reset tokens in Redis, beside the refresh tokens and for the same reasons: they are
/// short-lived and expire on their own, and spending one has to be atomic.
///
/// Two kinds of key, both expiring with the token:
///
///   prt:{sha256(token)} → the user id.          Answers "whose token is this?"
///   prt-user:{userId}   → the newest token hash. Answers "is it still the current one?" — what
///                                                makes a newer link supersede an older one.
/// </summary>
public sealed class RedisPasswordResetTokenStore(IConnectionMultiplexer redis)
    : IPasswordResetTokenStore
{
    private const string TokenKeyPrefix = "prt:";
    private const string UserKeyPrefix = "prt-user:";

    public async Task<string> CreateAsync(
        Guid userId, TimeSpan lifetime, CancellationToken ct = default)
    {
        // 256 bits from the CSPRNG, URL-safe so it survives being put in a link unescaped.
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(token);
        var db = redis.GetDatabase();

        // The user key is overwritten, not added to: from now on only this hash is current, and
        // any earlier token fails the check in IsCurrentAsync without having to be found and
        // deleted.
        await Task.WhenAll(
            db.StringSetAsync(TokenKey(hash), userId.ToString(), lifetime),
            db.StringSetAsync(UserKey(userId), hash, lifetime));

        return token;
    }

    public async Task<Guid?> PeekAsync(string token, CancellationToken ct = default)
    {
        var hash = Hash(token);
        var owner = await redis.GetDatabase().StringGetAsync(TokenKey(hash));

        return await IsCurrentAsync(owner, hash);
    }

    public async Task<Guid?> ConsumeAsync(string token, CancellationToken ct = default)
    {
        var hash = Hash(token);

        // GETDEL: reading and deleting in one step is what lets only one of two simultaneous
        // submissions find the token.
        var owner = await redis.GetDatabase().StringGetDeleteAsync(TokenKey(hash));

        return await IsCurrentAsync(owner, hash);
    }

    /// <summary>The owner, if the token had one and is still that account's newest.</summary>
    private async Task<Guid?> IsCurrentAsync(RedisValue owner, string hash)
    {
        if (!Guid.TryParse((string?)owner, out var userId))
        {
            return null;
        }

        var current = await redis.GetDatabase().StringGetAsync(UserKey(userId));

        return current == hash ? userId : null;
    }

    /// <summary>
    /// Plain SHA-256, as for refresh tokens: the input is already 256 random bits, so there is
    /// nothing to brute-force, and a dump of Redis must not hand out working links.
    /// </summary>
    private static string Hash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static RedisKey TokenKey(string hash) => TokenKeyPrefix + hash;

    private static RedisKey UserKey(Guid userId) => UserKeyPrefix + userId;
}
