namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>A clock stopped at one instant, so a test can assert the exact time a use case stamped.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
