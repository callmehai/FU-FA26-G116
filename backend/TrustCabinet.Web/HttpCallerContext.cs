using TrustCabinet.Domain.Audit;

namespace TrustCabinet.Web;

/// <summary>
/// Supplies the audit log with the caller's IP address. Lives in the web project because
/// TrustCabinet.Infrastructure has no ASP.NET Core reference — see <see cref="ICallerContext"/>.
/// </summary>
public sealed class HttpCallerContext(IHttpContextAccessor accessor) : ICallerContext
{
    /// <summary>
    /// Whatever the server saw as the remote address. Deliberately not read from
    /// <c>X-Forwarded-For</c>: that header is caller-supplied, so trusting it without a configured
    /// forwarded-headers middleware would let anyone write any IP they like into the evidence.
    /// ponytail: direct remote address only. When a reverse proxy lands, configure
    /// ForwardedHeaders with its known networks rather than parsing the header here.
    /// </summary>
    public string IpAddress
        => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
}
