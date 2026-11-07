using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// SCRUM-132 — caps the anonymous auth endpoints so a password cannot be guessed at line speed:
/// per IP on <c>/login</c>, <c>/google</c> and <c>/password-reset</c> (the middleware policy), and
/// per email on <c>/login</c> (<see cref="SignInEmailLimiter"/>, checked in the handler because the
/// email is in the body, which a partitioner cannot read).
///
/// The IP is <c>Connection.RemoteIpAddress</c>: on the test server it is the user's real address,
/// because ASPNETCORE_FORWARDEDHEADERS_ENABLED applies Caddy's X-Forwarded-For and the app takes no
/// traffic except through Caddy (deploy/docker-compose.yml).
///
/// A per-email limit also lets anyone lock a known account out for one window. Accepted: the window
/// is a minute, and the alternative — letting guesses through for an account under attack — is worse.
/// </summary>
public static class AuthRateLimiting
{
    internal const string PerIpPolicy = "auth-per-ip";
    private const string Msg414TooManyAttempts = "MSG414";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:WindowSeconds", 60));
        var perIp = configuration.GetValue("RateLimiting:AuthPerIp", 60);
        var perEmail = configuration.GetValue("RateLimiting:SignInPerEmail", 5);

        // Built by a factory so the container disposes it (and its timer) with the app.
        services.AddSingleton(_ => new SignInEmailLimiter(PartitionedRateLimiter.Create<string, string>(
            email => RateLimitPartition.GetFixedWindowLimiter(email, _ => Window(perEmail, window)))));

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PerIpPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                IpPartition(http.Connection.RemoteIpAddress), _ => Window(perIp, window)));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
                new ValueTask(TooManyAttempts(context.Lease).ExecuteAsync(context.HttpContext));
        });
        return services;
    }

    /// <summary>
    /// The partition of a caller's address. An IPv6 client usually holds a whole /64 and can pick any address in
    /// it, so the /64 is one caller; IPv4 (also when mapped into IPv6) is the address itself.
    /// </summary>
    public static string IpPartition(IPAddress? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

    /// <summary>429 + MSG414, with <c>Retry-After</c> in seconds when the limiter knows it.</summary>
    internal static IResult TooManyAttempts(RateLimitLease lease)
        => new TooManyAttemptsResult(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : null);

    private static FixedWindowRateLimiterOptions Window(int permits, TimeSpan window) => new()
    {
        PermitLimit = permits,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    private sealed class TooManyAttemptsResult(TimeSpan? retryAfter) : IResult
    {
        public Task ExecuteAsync(HttpContext http)
        {
            if (retryAfter is { } wait)
            {
                http.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            return Results.Json(
                new AuthErrorResponse(Msg414TooManyAttempts, "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau ít phút."),
                statusCode: StatusCodes.Status429TooManyRequests).ExecuteAsync(http);
        }
    }
}

/// <summary>The per-email sign-in limiter, a singleton so the count survives across requests.</summary>
public sealed class SignInEmailLimiter(PartitionedRateLimiter<string> limiter) : IDisposable
{
    /// <summary>The longest an email address can be (RFC 5321). Anything longer is no account's email.</summary>
    private const int MaxEmailLength = 320;

    /// <summary>
    /// Case and surrounding spaces do not make a new email: sign-in ignores both too. Over-long input shares one
    /// bucket, so a multi-megabyte "email" is not kept as a key for the whole window.
    /// </summary>
    public RateLimitLease Acquire(string email)
    {
        var key = email.Trim();
        return limiter.AttemptAcquire(key.Length > MaxEmailLength ? string.Empty : key.ToUpperInvariant());
    }

    public void Dispose() => limiter.Dispose();
}
