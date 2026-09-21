using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// Hand-written stand-ins for the user service's collaborators, in the same style as
/// <c>AuthenticationTestDoubles</c>: no mocking library, and each fake records enough for an
/// assertion to be about real behaviour rather than about a call having been made.
/// </summary>
internal sealed class FakeUserAccountRepository : IUserAccountRepository
{
    private readonly List<User> _users = [];

    /// <summary>How many times the service asked for changes to be persisted.</summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// Set to make the next save throw, standing in for PostgreSQL's unique index rejecting the
    /// row. Cleared once thrown, so a test can check the retry path.
    /// </summary>
    public Exception? NextSaveThrows { get; set; }

    public IReadOnlyList<User> Users => _users;

    public FakeUserAccountRepository WithUser(User user)
    {
        _users.Add(user);
        return this;
    }

    public Task<IReadOnlyCollection<User>> ListAsync(CancellationToken ct = default)
        // Sorted like the real query, so a test asserting on order is testing the same contract.
        => Task.FromResult<IReadOnlyCollection<User>>(
            [.. _users.OrderBy(u => u.FullName).ThenBy(u => u.Email)]);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<bool> EmailExistsAsync(
        string email, Guid? exceptId = null, CancellationToken ct = default)
        // citext in PostgreSQL, so the fake compares case-insensitively too — otherwise a test
        // would pass here and the real database would still reject the row.
        => Task.FromResult(_users.Any(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || u.Id != exceptId)));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;

        if (NextSaveThrows is { } exception)
        {
            NextSaveThrows = null;
            throw exception;
        }

        return Task.CompletedTask;
    }
}

/// <summary>Records what was audited, so a test can assert the evidence actually got written.</summary>
internal sealed class FakeAuditService : IAuditService
{
    public List<AuditRecord> Records { get; } = [];

    public Task RecordAsync(
        string action,
        string objectType,
        Guid? objectId,
        Guid? actorId,
        object? details = null,
        CancellationToken ct = default)
    {
        Records.Add(new AuditRecord(action, objectType, objectId, actorId, details));
        return Task.CompletedTask;
    }

    internal sealed record AuditRecord(
        string Action, string ObjectType, Guid? ObjectId, Guid? ActorId, object? Details);
}

/// <summary>A caller context with a fixed address, for the audit service's own tests.</summary>
internal sealed class FakeCallerContext(string ipAddress = "203.0.113.7") : ICallerContext
{
    public string IpAddress { get; } = ipAddress;
}
