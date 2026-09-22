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

    public Task<User?> FindByGoogleSubjectIdAsync(string? googleSubjectId, CancellationToken ct = default)
        // A blank subject matches nothing, and never reaches the query. EF would translate
        // `== null` into `IS NULL`, which every password-only account satisfies — so a token with
        // no subject claim would sign the caller in as whichever unlinked row PostgreSQL happened
        // to return first. GoogleTokenVerifier already refuses such a token; this is the second
        // lock, because this method is not private to that one caller.
        => string.IsNullOrWhiteSpace(googleSubjectId)
            ? Task.FromResult<User?>(null)
            : db.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId, ct);

    public async Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        SystemRole role, CancellationToken ct = default)
        => await db.RoleCapabilities
            .Where(rc => rc.Role == role)
            .Select(rc => rc.Capability)
            .ToArrayAsync(ct);
}
