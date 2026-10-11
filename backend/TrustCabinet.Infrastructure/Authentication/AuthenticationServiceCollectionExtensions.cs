using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using TrustCabinet.Domain.Authentication;

namespace TrustCabinet.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything UC-01 needs: the service, the repository, the hasher, the JWT issuer
    /// and the Redis-backed refresh token store.
    ///
    /// Like the connection string, the Redis address and the signing key are not in the repo. The
    /// app refuses to start without them rather than quietly falling back to an in-memory store,
    /// because that fallback would look like working authentication while dropping every session
    /// on restart and sharing nothing between instances.
    /// </summary>
    public static IServiceCollection AddTrustCabinetAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            // At startup, not at the first sign-in attempt: a missing signing key should stop a
            // deployment, not surface as a 500 when someone tries to log in.
            .ValidateOnStart();

        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException(
                "No connection string 'Redis' is configured, so refresh tokens have nowhere to "
                + "live and the application cannot start. Run `docker compose up -d` and set it "
                + "with: dotnet user-secrets set \"ConnectionStrings:Redis\" \"localhost:6379\" "
                + "--project backend/TrustCabinet.Web. See docs/03-api/01-xac-thuc.md.");
        }

        // One multiplexer for the whole application — it is expensive to build, thread-safe, and
        // multiplexes concurrent commands over a single connection. Creating one per request is
        // the classic way to exhaust the connection pool.
        //
        // Resolved lazily, so the web app still starts when Redis is briefly unreachable: sign-in
        // then fails while everything else keeps serving, instead of the whole process refusing to
        // boot. AbortOnConnectFail=false lets the multiplexer reconnect by itself afterwards.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        // Google sign-in is optional, so its options are validated only when switched on — an
        // application that does not offer it must still start without a ClientId.
        services
            .AddOptions<GoogleAuthenticationOptions>()
            .Bind(configuration.GetSection(GoogleAuthenticationOptions.SectionName))
            .Validate(
                google => !google.Enabled || !string.IsNullOrWhiteSpace(google.ClientId),
                "Authentication:Google:ClientId is required when Authentication:Google:Enabled "
                + "is true. See docs/03-api/01-xac-thuc.md.")
            .ValidateOnStart();

        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        // The store reads the clock for its reuse grace period; TryAdd so a test can substitute it.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();
        // UC-07's reset links, in the same Redis.
        services.AddSingleton<IPasswordResetTokenStore, RedisPasswordResetTokenStore>();

        // Scoped: it depends on the repository, which holds the request's DbContext.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        return services;
    }
}
