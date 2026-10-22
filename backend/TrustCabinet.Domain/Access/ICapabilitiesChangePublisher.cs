namespace TrustCabinet.Domain.Access;

/// <summary>
/// Tells signed-in accounts that their role's capabilities changed (SCRUM-105), so the browser
/// refreshes its access token and picks up the new set without signing in again.
///
/// A hint, never a security boundary: the event carries no data, and a client that ignores it
/// gains nothing — every gated endpoint re-reads the caller's capabilities from the database.
/// </summary>
public interface ICapabilitiesChangePublisher
{
    Task PublishAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
