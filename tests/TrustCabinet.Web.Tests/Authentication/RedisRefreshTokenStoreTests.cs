using StackExchange.Redis;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// The Redis store against a real Redis — the parts a fake cannot prove: that GETDEL really makes
/// a token single-use, that the raw token is not what gets stored, and that the user index finds
/// every session to revoke.
///
/// These need a real Redis: `docker compose up -d`. If it is unreachable they FAIL rather than
/// skip — an earlier version returned early instead, and the whole file reported green while
/// asserting nothing, which is worse than a red build telling you to start Docker.
/// </summary>
public class RedisRefreshTokenStoreTests : IAsyncLifetime
{
    private const string ConnectionString = "localhost:6379";

    private IConnectionMultiplexer? _redis;

    public async Task InitializeAsync()
    {
        var options = ConfigurationOptions.Parse(ConnectionString);
        // Fail fast rather than retry for half a minute: the message below is more useful than a
        // long hang.
        options.AbortOnConnectFail = true;
        options.ConnectTimeout = 2000;
        options.ConnectRetry = 1;

        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync(options);
        }
        catch (RedisConnectionException e)
        {
            throw new InvalidOperationException(
                $"These tests need a Redis at {ConnectionString}. Start one with "
                + "`docker compose up -d redis`.", e);
        }
    }

    public async Task DisposeAsync()
    {
        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }
    }

    /// <summary>
    /// A store, plus a token and user id unique to this test — the keys are shared with whatever
    /// else is using this Redis, so nothing here may collide with another test or a dev session.
    /// </summary>
    private (RedisRefreshTokenStore Store, string Token, Guid UserId) Arrange()
        => (new RedisRefreshTokenStore(_redis!), $"rt-test-{Guid.NewGuid()}", Guid.CreateVersion7());

    [Fact]
    public async Task A_stored_token_can_be_consumed_once()
    {
        var (store, token, userId) = Arrange();

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));

        Assert.Equal(userId, await store.ConsumeAsync(token));
        // Second attempt finds nothing: GETDEL removed it as it read it, which is what stops two
        // concurrent refreshes from both accepting the same stolen token.
        Assert.Null(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task An_unknown_token_consumes_to_nothing()
    {
        var (store, token, _) = Arrange();

        Assert.Null(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task The_raw_token_is_never_what_is_stored()
    {
        var (store, token, userId) = Arrange();

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));

        // A dump of Redis must not hand over usable session tokens, so the key is a hash of the
        // token and the plaintext appears nowhere.
        Assert.False(await _redis!.GetDatabase().KeyExistsAsync($"rt:{token}"));

        await store.ConsumeAsync(token);
    }

    [Fact]
    public async Task A_revoked_token_stops_working()
    {
        var (store, token, userId) = Arrange();

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));
        await store.RevokeAsync(token);

        Assert.Null(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task Revoking_a_token_that_is_already_gone_is_harmless()
    {
        var (store, token, _) = Arrange();

        await store.RevokeAsync(token);
    }

    [Fact]
    public async Task Revoking_everything_for_a_user_ends_every_session()
    {
        var (store, token, userId) = Arrange();

        var second = token + "-phone";
        var third = token + "-tablet";

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));
        await store.StoreAsync(second, userId, TimeSpan.FromMinutes(5));
        await store.StoreAsync(third, userId, TimeSpan.FromMinutes(5));

        await store.RevokeAllForUserAsync(userId);

        // What a deactivation has to do: every device, not just the one that asked.
        Assert.Null(await store.ConsumeAsync(token));
        Assert.Null(await store.ConsumeAsync(second));
        Assert.Null(await store.ConsumeAsync(third));
    }

    [Fact]
    public async Task Revoking_one_user_leaves_another_users_sessions_alone()
    {
        var (store, token, userId) = Arrange();

        var otherUserId = Guid.CreateVersion7();
        var otherToken = token + "-nguoi-khac";

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));
        await store.StoreAsync(otherToken, otherUserId, TimeSpan.FromMinutes(5));

        await store.RevokeAllForUserAsync(userId);

        Assert.Equal(otherUserId, await store.ConsumeAsync(otherToken));
    }

    [Fact]
    public async Task A_token_expires_on_its_own()
    {
        var (store, token, userId) = Arrange();

        await store.StoreAsync(token, userId, TimeSpan.FromSeconds(1));

        // Redis expires the key by itself — no cleanup job for stale tokens, which is a large part
        // of why this is not a table.
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.Null(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task The_stored_token_carries_a_time_to_live()
    {
        var (store, token, userId) = Arrange();

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(30));

        // This test's own key, derived the same way the store derives it. Scanning "rt:*" instead
        // would pick up keys from tests running in parallel and from a local dev session.
        var key = "rt:" + Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

        var ttl = await _redis!.GetDatabase().KeyTimeToLiveAsync(key);

        // A key stored without a TTL would live forever and quietly turn a seven-day session into
        // a permanent one.
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(30));

        await store.ConsumeAsync(token);
    }
}
