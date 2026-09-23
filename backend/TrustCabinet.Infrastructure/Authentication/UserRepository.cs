using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// UC-01's <c>:User Repository</c> — the reads the authentication service needs, plus the one
/// write UC-02 makes: linking a Google identity at first sign-in.
/// </summary>
public sealed class UserRepository(TrustCabinetDbContext db) : IUserRepository
{
    /// <summary>Every account this repository returns carries its role, for the role's name.</summary>
    private IQueryable<User> Users => db.Users.Include(u => u.Role);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        // No ToLower() on either side: the column is citext, so PostgreSQL compares
        // case-insensitively and can still use the unique index. Lowercasing here would defeat it.
        => Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByGoogleSubjectIdAsync(string? googleSubjectId, CancellationToken ct = default)
        // A blank subject matches nothing, and never reaches the query. EF would translate
        // `== null` into `IS NULL`, which every password-only account satisfies — so a token with
        // no subject claim would sign the caller in as whichever unlinked row PostgreSQL happened
        // to return first. GoogleTokenVerifier already refuses such a token; this is the second
        // lock, because this method is not private to that one caller.
        => string.IsNullOrWhiteSpace(googleSubjectId)
            ? Task.FromResult<User?>(null)
            : Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId, ct);

    public async Task<User?> LinkGoogleSubjectByEmailAsync(
        string email, string googleSubjectId, CancellationToken ct = default)
    {
        // Same blank guard as above, for the same reason: a blank value here would be written
        // into the column and then matched by the next lookup.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(googleSubjectId))
        {
            return null;
        }

        // One conditional UPDATE rather than read-then-save: the `google_subject_id IS NULL`
        // test and the write happen together, so two concurrent first sign-ins cannot both link.
        var linked = await db.Users
            .Where(u => u.Email == email && u.GoogleSubjectId == null && u.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.GoogleSubjectId, googleSubjectId), ct);

        // AsNoTracking: ExecuteUpdate bypasses the change tracker, so an instance this context
        // already tracks would come back with the old, null subject.
        return linked == 0
            ? null
            : await Users.AsNoTracking().FirstAsync(u => u.GoogleSubjectId == googleSubjectId, ct);
    }

    public async Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        Guid roleId, CancellationToken ct = default)
        => await db.RoleCapabilities
            .Where(rc => rc.RoleId == roleId)
            .Select(rc => rc.Capability)
            .ToArrayAsync(ct);
}
