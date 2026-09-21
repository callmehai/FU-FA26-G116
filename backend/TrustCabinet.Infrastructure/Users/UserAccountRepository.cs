using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
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
        => await db.Users.OrderBy(u => u.FullName).ThenBy(u => u.Email).ToArrayAsync(ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExistsAsync(
        string email, Guid? exceptId = null, CancellationToken ct = default)
        // No ToLower(): the column is citext, so PostgreSQL compares case-insensitively and can
        // still use the unique index. Lowercasing either side would defeat it — the same reason
        // Authentication.UserRepository.FindByEmailAsync does not either.
        => db.Users.AnyAsync(u => u.Email == email && (exceptId == null || u.Id != exceptId), ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
