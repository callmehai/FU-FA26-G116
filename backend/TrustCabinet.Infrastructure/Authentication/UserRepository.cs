using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// UC-01's <c>:User Repository</c> — the two reads the authentication service needs, and nothing
/// more.
/// </summary>
public sealed class UserRepository(TrustCabinetDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        // No ToLower() on either side: the column is citext, so PostgreSQL compares
        // case-insensitively and can still use the unique index. Lowercasing here would defeat it.
        => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId, ct);

    public async Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        SystemRole role, CancellationToken ct = default)
        => await db.RoleCapabilities
            .Where(rc => rc.Role == role)
            .Select(rc => rc.Capability)
            .ToArrayAsync(ct);
}
