using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

    /// <summary>The store's clock, so a test can step past the reuse grace period without waiting.</summary>
    private readonly TestClock _clock = new();

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private void PastTheGracePeriod()
        => _clock.Now += RedisRefreshTokenStore.ReuseGracePeriod + TimeSpan.FromSeconds(1);

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
        => (new RedisRefreshTokenStore(
                _redis!,
                Options.Create(new AuthenticationOptions { RefreshTokenDays = 7 }),
                _clock,
                NullLogger<RedisRefreshTokenStore>.Instance),
            $"rt-test-{Guid.NewGuid()}",
            Guid.CreateVersion7());

    private static string KeyFor(string prefix, string token) => prefix + Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

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

        // It names whose session ended — what the sign-out log records.
        Assert.Equal(userId, await store.RevokeAsync(token));
        Assert.Null(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task Revoking_a_token_that_is_already_gone_is_harmless()
    {
        var (store, token, _) = Arrange();

        Assert.Null(await store.RevokeAsync(token));
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

    [Fact]
    public async Task Replaying_a_used_token_ends_every_session_of_its_account()
    {
        var (store, stolen, userId) = Arrange();
        var otherDevice = stolen + "-phone";

        await store.StoreAsync(stolen, userId, TimeSpan.FromMinutes(5));
        await store.StoreAsync(otherDevice, userId, TimeSpan.FromMinutes(5));

        // The thief refreshes first and receives a rotated token of their own.
        Assert.Equal(userId, await store.ConsumeAsync(stolen));
        var thiefsRotated = stolen + "-rotated";
        await store.StoreAsync(thiefsRotated, userId, TimeSpan.FromMinutes(5));

        // The real user then presents the same token: two holders of one token, no telling which
        // is honest, so nobody keeps a session — the thief included.
        PastTheGracePeriod();
        Assert.Null(await store.ConsumeAsync(stolen));
        Assert.Null(await store.ConsumeAsync(thiefsRotated));
        Assert.Null(await store.ConsumeAsync(otherDevice));
    }

    [Fact]
    public async Task A_token_presented_again_moments_after_it_was_spent_is_only_refused()
    {
        var (store, token, userId) = Arrange();
        var otherDevice = token + "-phone";

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));
        await store.StoreAsync(otherDevice, userId, TimeSpan.FromMinutes(5));
        await store.ConsumeAsync(token);

        // A refresh the browser aborted mid-flight leaves it holding the spent token, and the next
        // page load sends it again seconds later. Refused — but not mistaken for a theft.
        _clock.Now += TimeSpan.FromSeconds(5);
        Assert.Null(await store.ConsumeAsync(token));
        Assert.Equal(userId, await store.ConsumeAsync(otherDevice));
    }

    [Fact]
    public async Task A_replay_within_the_grace_period_does_not_hide_a_later_one()
    {
        var (store, token, userId) = Arrange();
        var otherDevice = token + "-phone";

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));
        await store.StoreAsync(otherDevice, userId, TimeSpan.FromMinutes(5));
        await store.ConsumeAsync(token);

        await store.ConsumeAsync(token); // within the grace period: refused, marker kept

        PastTheGracePeriod();
        Assert.Null(await store.ConsumeAsync(token));
        Assert.Null(await store.ConsumeAsync(otherDevice));
    }

    [Fact]
    public async Task One_stolen_token_ends_the_sessions_once_not_every_time_it_is_replayed()
    {
        var (store, stolen, userId) = Arrange();

        await store.StoreAsync(stolen, userId, TimeSpan.FromMinutes(5));
        await store.ConsumeAsync(stolen);
        PastTheGracePeriod();
        await store.ConsumeAsync(stolen); // detected: every session ends

        // The user signs in again. A thief replaying the same old token must not be able to
        // throw them out again and again for as long as the marker would have lived.
        var freshSession = stolen + "-sau-khi-dang-nhap-lai";
        await store.StoreAsync(freshSession, userId, TimeSpan.FromMinutes(5));

        Assert.Null(await store.ConsumeAsync(stolen));
        Assert.Equal(userId, await store.ConsumeAsync(freshSession));
    }

    [Fact]
    public async Task A_used_token_is_remembered_for_a_full_refresh_token_lifetime()
    {
        var (store, token, userId) = Arrange();

        await store.StoreAsync(token, userId, TimeSpan.FromMinutes(5));
        await store.ConsumeAsync(token);

        var ttl = await _redis!.GetDatabase().KeyTimeToLiveAsync(KeyFor("rt-used:", token));

        // Seven days, not the five minutes the token itself had left: the marker has to last as
        // long as the token issued in exchange for it, and it must still expire on its own.
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1), TimeSpan.FromDays(7));
        // Hashed like the token key: the marker is no more a usable token than the key was.
        Assert.False(await _redis.GetDatabase().KeyExistsAsync($"rt-used:{token}"));
    }
}
