using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Access;

/// <summary>
/// UC-12a … UC-12c's <c>:Role Repository</c>. Queries and writes only — every rule lives in
/// <see cref="RoleService"/>.
/// </summary>
public sealed class RoleRepository(TrustCabinetDbContext db) : IRoleRepository
{
    public async Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default)
    {
        // One query with a correlated count, rather than a count per role.
        var rows = await db.Roles
            .AsNoTracking()
            .Select(r => new { Role = r, UserCount = db.Users.Count(u => u.RoleId == r.Id) })
            .ToArrayAsync(ct);

        // Ordered in memory: the built-in order is a list in code, not a column, and there are a
        // handful of roles.
        return [.. rows
            .OrderBy(row => BuiltInRoles.SortKey(row.Role.Id))
            .ThenBy(row => row.Role.CreatedAt)
            .Select(row => new RoleSummary(
                row.Role.Id,
                row.Role.Name,
                row.Role.Description,
                row.Role.IsBuiltIn,
                BuiltInRoles.IsDefault(row.Role.Id),
                row.UserCount))];
    }

    public Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> NameExistsAsync(
        string name, Guid? exceptId = null, CancellationToken ct = default)
        // No ToLower(): the column is citext, so PostgreSQL compares case-insensitively and can
        // still use the unique index — the same reason UserAccountRepository.EmailExistsAsync
        // does not lowercase either.
        => db.Roles.AnyAsync(r => r.Name == name && (exceptId == null || r.Id != exceptId), ct);

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default)
        // Deactivated accounts count: they still point at the role, and the foreign key would
        // refuse the delete for them just the same.
        => db.Users.CountAsync(u => u.RoleId == roleId, ct);

    public async Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        Guid roleId, CancellationToken ct = default)
        => await db.RoleCapabilities
            .Where(rc => rc.RoleId == roleId)
            .OrderBy(rc => rc.Capability)
            .Select(rc => rc.Capability)
            .ToArrayAsync(ct);

    public Task AddAsync(
        Role role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
    {
        db.Roles.Add(role);
        db.RoleCapabilities.AddRange(
            capabilities.Select(c => new RoleCapability { RoleId = role.Id, Capability = c }));

        return Task.CompletedTask;
    }

    public void Remove(Role role) => db.Roles.Remove(role);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
