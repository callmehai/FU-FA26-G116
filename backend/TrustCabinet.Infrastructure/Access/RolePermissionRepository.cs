using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Access;

/// <summary>
/// UC-09 and UC-10's <c>:Role Permission Repository</c>. Queries and writes only — every rule
/// lives in <see cref="RolePermissionService"/>.
/// </summary>
public sealed class RolePermissionRepository(TrustCabinetDbContext db) : IRolePermissionRepository
{
    public async Task<IReadOnlyDictionary<SystemRole, IReadOnlyCollection<Capability>>> GetMatrixAsync(
        CancellationToken ct = default)
    {
        var rows = await db.RoleCapabilities.ToArrayAsync(ct);

        return rows
            .GroupBy(rc => rc.Role)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Capability>)[.. group.Select(rc => rc.Capability)]);
    }

    public async Task ReplaceCapabilitiesAsync(
        SystemRole role,
        IReadOnlyCollection<Capability> capabilities,
        CancellationToken ct = default)
    {
        var existing = await db.RoleCapabilities.Where(rc => rc.Role == role).ToArrayAsync(ct);

        // Only the difference is touched. Deleting the row and re-inserting it would produce
        // churn EF has to order carefully against the composite primary key, and it would make
        // every save look like a change even when nothing moved.
        var removed = existing.Where(rc => !capabilities.Contains(rc.Capability));
        db.RoleCapabilities.RemoveRange(removed);

        var added = capabilities
            .Where(capability => !existing.Any(rc => rc.Capability == capability))
            .Select(capability => new RoleCapability { Role = role, Capability = capability });
        db.RoleCapabilities.AddRange(added);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyCollection<Guid>> FindActiveUserIdsInRoleAsync(
        SystemRole role, CancellationToken ct = default)
        // Inactive accounts are skipped: they hold no usable session to end, since a refresh
        // already refuses them and they cannot sign in to get a new token.
        => await db.Users
            .Where(u => u.Role == role && u.IsActive)
            .Select(u => u.Id)
            .ToArrayAsync(ct);

    public Task<int> CountActiveUsersInRolesAsync(
        IReadOnlyCollection<SystemRole> rolesWithCapability, CancellationToken ct = default)
    {
        // No roles would hold it, so no account could: skip the round trip and answer the
        // question the caller is really asking.
        if (rolesWithCapability.Count == 0)
        {
            return Task.FromResult(0);
        }

        // Matches UserAccountRepository.CountActiveWithCapabilityAsync — active accounts whose
        // role carries the capability — except the roles come from the caller, because the set
        // being tested is the one that does not exist in the table yet.
        var roles = rolesWithCapability.ToArray();

        return db.Users.CountAsync(u => u.IsActive && roles.Contains(u.Role), ct);
    }

    public async Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> update, CancellationToken ct = default)
    {
        // Serializable so the read the validation depends on cannot change under it. PostgreSQL
        // would then abort one of two conflicting transactions with a serialization failure
        // rather than let both commit — but the explicit lock below means the second caller waits
        // instead, so that retry path is never reached for this table.
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        // Held until commit. EXCLUSIVE rather than ACCESS EXCLUSIVE: concurrent readers of the
        // matrix (every UC-09 request) keep working, while any second writer waits here.
        await db.Database.ExecuteSqlRawAsync(
            "LOCK TABLE role_capabilities IN EXCLUSIVE MODE", ct);

        var result = await update(ct);

        await transaction.CommitAsync(ct);

        return result;
    }
}
