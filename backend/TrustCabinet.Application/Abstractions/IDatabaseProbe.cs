namespace TrustCabinet.Application.Abstractions;

/// <summary>
/// Whether the database answers, for <c>/api/health</c>: the deploy's health check rolls back a version that started
/// but cannot reach it (ADR-0047).
/// </summary>
public interface IDatabaseProbe
{
    /// <summary>False when no connection to the database can be opened.</summary>
    Task<bool> CanConnectAsync(CancellationToken ct);
}
