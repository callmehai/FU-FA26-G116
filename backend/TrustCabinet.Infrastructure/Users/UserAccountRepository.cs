using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Infrastructure.Users;

/// <summary>
/// UC-04 … UC-07's <c>:User Repository</c>. Queries only — every rule lives in
/// <see cref="UserService"/>.
/// </summary>
public sealed class UserAccountRepository(TrustCabinetDbContext db) : IUserAccountRepository
{
    public async Task<IReadOnlyCollection<User>> ListAsync(CancellationToken ct = default)
        // Sorted in the database, so PostgreSQL's collation orders Vietnamese names — .NET's
        // default ordinal comparison would put "Đ" after "Z".
        //
        // No AsNoTracking: this is a read, but the DbContext is per-request and nothing writes
        // during a list, so tracking costs nothing worth the risk of someone later mutating one of
        // these entities and finding the change silently dropped.
        => await db.Users
            .Include(u => u.Role)
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Email)
            .ToArrayAsync(ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Role?> FindRoleAsync(Guid roleId, CancellationToken ct = default)
        => db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);

    public Task<bool> EmailExistsAsync(
        string email, Guid? exceptId = null, CancellationToken ct = default)
        // No ToLower(): the column is citext, so PostgreSQL compares case-insensitively and can
        // still use the unique index. Lowercasing either side would defeat it — the same reason
        // Authentication.UserRepository.FindByEmailAsync does not either.
        => db.Users.AnyAsync(u => u.Email == email && (exceptId == null || u.Id != exceptId), ct);

    public async Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        Guid roleId, CancellationToken ct = default)
        => await db.RoleCapabilities
            .Where(rc => rc.RoleId == roleId)
            .Select(rc => rc.Capability)
            .ToArrayAsync(ct);

    public Task<int> CountActiveWithCapabilityAsync(
        Capability capability, Guid exceptId, CancellationToken ct = default)
        // Joined against role_capabilities rather than filtering on Role == Admin: that table is
        // what decides who may administer, and it can be re-pointed to another role without a
        // deployment. Counting the role name would leave this guard silently wrong afterwards.
        => db.Users
            .Where(u => u.IsActive
                && u.Id != exceptId
                && db.RoleCapabilities.Any(rc => rc.RoleId == u.RoleId && rc.Capability == capability))
            .CountAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task DiscardChangesAsync(User user, CancellationToken ct = default)
        // Reload rather than Detach: the caller still holds the reference and reads from it after
        // a failure, so it has to carry the database's values rather than become a detached
        // entity whose state is whatever was last assigned to it.
        => db.Entry(user).ReloadAsync(ct);
}
