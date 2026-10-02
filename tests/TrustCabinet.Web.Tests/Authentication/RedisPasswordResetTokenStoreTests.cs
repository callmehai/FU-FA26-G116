using StackExchange.Redis;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// UC-08's reset tokens against a real Redis — what the fake cannot prove: that GETDEL makes a
/// link single-use, that a newer link really supersedes an older one, that the raw token is not
/// what gets stored, and that the keys expire.
///
/// Needs `docker compose up -d redis`, and fails rather than skips without it, for the reason
/// <see cref="RedisRefreshTokenStoreTests"/> gives.
/// </summary>
public class RedisPasswordResetTokenStoreTests : IAsyncLifetime
{
    private const string ConnectionString = "localhost:6379";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    private IConnectionMultiplexer? _redis;

    public async Task InitializeAsync()
    {
        var options = ConfigurationOptions.Parse(ConnectionString);
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

    private RedisPasswordResetTokenStore NewStore() => new(_redis!);

    [Fact]
    public async Task A_created_token_can_be_peeked_and_then_spent_exactly_once()
    {
        var store = NewStore();
        var userId = Guid.CreateVersion7();
        var token = await store.CreateAsync(userId, Lifetime);

        Assert.Equal(userId, await store.PeekAsync(token));
        Assert.Equal(userId, await store.PeekAsync(token));
        Assert.Equal(userId, await store.ConsumeAsync(token));
        Assert.Null(await store.ConsumeAsync(token));
        Assert.Null(await store.PeekAsync(token));
    }

    [Fact]
    public async Task Of_two_simultaneous_submissions_only_one_spends_the_token()
    {
        var store = NewStore();
        var token = await store.CreateAsync(Guid.CreateVersion7(), Lifetime);

        var results = await Task.WhenAll(store.ConsumeAsync(token), store.ConsumeAsync(token));

        Assert.Single(results, owner => owner is not null);
    }

    [Fact]
    public async Task A_newer_token_supersedes_the_older_one()
    {
        var store = NewStore();
        var userId = Guid.CreateVersion7();
        var older = await store.CreateAsync(userId, Lifetime);
        var newer = await store.CreateAsync(userId, Lifetime);

        Assert.Null(await store.PeekAsync(older));
        Assert.Null(await store.ConsumeAsync(older));
        Assert.Equal(userId, await store.ConsumeAsync(newer));
    }

    [Fact]
    public async Task Tokens_of_different_accounts_do_not_supersede_each_other()
    {
        var store = NewStore();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var firstToken = await store.CreateAsync(first, Lifetime);
        await store.CreateAsync(second, Lifetime);

        Assert.Equal(first, await store.ConsumeAsync(firstToken));
    }

    [Fact]
    public async Task Revoking_kills_the_outstanding_token()
    {
        var store = NewStore();
        var userId = Guid.CreateVersion7();
        var token = await store.CreateAsync(userId, Lifetime);

        await store.RevokeAsync(userId);

        Assert.Null(await store.PeekAsync(token));
        Assert.Null(await store.ConsumeAsync(token));
    }

    [Fact]
    public async Task An_unknown_token_belongs_to_nobody()
    {
        var store = NewStore();

        Assert.Null(await store.PeekAsync("khong-phai-token-nao-ca"));
        Assert.Null(await store.ConsumeAsync("khong-phai-token-nao-ca"));
    }

    [Fact]
    public async Task The_raw_token_is_never_stored_and_both_keys_expire()
    {
        var store = NewStore();
        var userId = Guid.CreateVersion7();
        var token = await store.CreateAsync(userId, Lifetime);
        var db = _redis!.GetDatabase();

        Assert.False(await db.KeyExistsAsync("prt:" + token));

        var userKey = (RedisKey)("prt-user:" + userId);
        var storedHash = (string?)await db.StringGetAsync(userKey);
        Assert.NotNull(storedHash);
        Assert.NotEqual(token, storedHash);

        Assert.InRange((await db.KeyTimeToLiveAsync(userKey))!.Value, TimeSpan.FromMinutes(59), Lifetime);
        Assert.InRange(
            (await db.KeyTimeToLiveAsync("prt:" + storedHash))!.Value, TimeSpan.FromMinutes(59), Lifetime);
    }

    [Fact]
    public async Task Tokens_are_url_safe_and_unique()
    {
        var store = NewStore();
        var userId = Guid.CreateVersion7();

        var first = await store.CreateAsync(userId, Lifetime);
        var second = await store.CreateAsync(userId, Lifetime);

        Assert.NotEqual(first, second);
        // 32 random bytes in base64url: 43 characters, nothing a URL would need to escape.
        Assert.Matches("^[A-Za-z0-9_-]{43}$", first);
    }
}
