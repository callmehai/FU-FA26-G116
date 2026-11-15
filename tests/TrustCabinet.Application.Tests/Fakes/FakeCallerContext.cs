using TrustCabinet.Application.Audit;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>A request from <paramref name="ipAddress"/>; empty is a background job with no request.</summary>
public sealed class FakeCallerContext(string ipAddress = "") : ICallerContext
{
    public string IpAddress { get; } = ipAddress;
}
