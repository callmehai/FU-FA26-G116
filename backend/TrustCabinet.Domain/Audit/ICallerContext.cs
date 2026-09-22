namespace TrustCabinet.Domain.Audit;

/// <summary>
/// Where the current request came from, for the audit log's evidence columns.
///
/// An interface rather than <c>IHttpContextAccessor</c> because TrustCabinet.Infrastructure is a
/// plain class library with no ASP.NET Core reference, and adding the whole web framework to the
/// data layer to read one string is not a trade worth making. The web project supplies the
/// implementation; a test supplies a constant.
/// </summary>
public interface ICallerContext
{
    /// <summary>
    /// The caller's IP address, or empty when there is no request in flight — a background job, or
    /// a seed run.
    /// </summary>
    string IpAddress { get; }
}
