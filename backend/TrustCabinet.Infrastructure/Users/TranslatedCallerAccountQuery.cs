using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Persistence;

namespace TrustCabinet.Infrastructure.Users;

/// <summary>
/// Serves <see cref="ICallerAccountQuery"/> from the account store it wraps and translates a storage failure into a
/// <see cref="PersistenceException"/> on the way out (rule R8). The web layer calls this port directly rather than
/// through <see cref="IUnitOfWork"/>, so without it the capability gate would have to know the driver's exceptions.
///
/// The reads themselves, their tracking and the account store they go to are those of the wrapped instance.
/// </summary>
internal sealed class TranslatedCallerAccountQuery(ICallerAccountQuery inner) : ICallerAccountQuery
{
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await inner.FindByIdAsync(id, ct);
        }
        catch (Exception exception) when (PersistenceExceptionTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(Guid roleId, CancellationToken ct = default)
    {
        try
        {
            return await inner.GetCapabilitiesAsync(roleId, ct);
        }
        catch (Exception exception) when (PersistenceExceptionTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }
}
