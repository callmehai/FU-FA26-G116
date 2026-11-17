using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Users;

/// <summary>
/// The caller's account as the database holds it now — whether it is still active and what its role may do — for the
/// web layer's capability gate, <c>/api/auth/me</c> and the notification hub and worker. SCRUM-12 makes the database,
/// not the access token, the authority on both.
///
/// The same two reads as <see cref="IUserAccountRepository"/>, served by the same class, so the gate and the account
/// screens always see one account. Storage errors come back untranslated: the gate maps the driver's own exceptions to
/// each endpoint's database-failure response.
/// </summary>
public interface ICallerAccountQuery
{
    /// <summary>The account with its <see cref="User.Role"/>, or null; as <see cref="IUserAccountRepository.FindByIdAsync"/>.</summary>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>What this role may do, read from <c>role_capabilities</c>.</summary>
    Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(Guid roleId, CancellationToken ct = default);
}
