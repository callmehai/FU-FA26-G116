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
///   prt-user:{userId}   → the activated hash.    Answers "is it the current one?" — what makes
///                                                a newer link supersede an older one, and what a
///                                                token created but never emailed never becomes.
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
        // Only the token key: without the user key pointing at it, IsCurrentAsync refuses it, and
        // the account's existing link is left exactly as it was.
        await redis.GetDatabase().StringSetAsync(TokenKey(Hash(token)), userId.ToString(), lifetime);

        return token;
    }

    /// <summary>
    /// The user key is overwritten, not added to: from now on only this hash is current, and any
    /// earlier token fails the check in IsCurrentAsync without having to be found and deleted.
    /// </summary>
    public Task ActivateAsync(
        Guid userId, string token, TimeSpan lifetime, CancellationToken ct = default)
        => redis.GetDatabase().StringSetAsync(UserKey(userId), Hash(token), lifetime);

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

    /// <summary>
    /// Dropping the user key is enough: every token of the account then fails
    /// <see cref="IsCurrentAsync"/>, and the token keys expire on their own.
    /// </summary>
    public Task RevokeAsync(Guid userId, CancellationToken ct = default)
        => redis.GetDatabase().KeyDeleteAsync(UserKey(userId));

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
