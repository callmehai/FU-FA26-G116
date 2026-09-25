using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// Hand-written stand-ins for the user service's collaborators, in the same style as
/// <c>AuthenticationTestDoubles</c>: no mocking library, and each fake records enough for an
/// assertion to be about real behaviour rather than about a call having been made.
/// </summary>
internal sealed class FakeUserAccountRepository : IUserAccountRepository
{
    private readonly List<User> _users = [];

    /// <summary>
    /// What each account looked like when it was seeded — the fake's stand-in for the row the
    /// real repository would reload from.
    /// </summary>
    private readonly Dictionary<Guid, (string FullName, string Email, Guid RoleId)> _snapshots = [];

    /// <summary>
    /// The roles that exist — the four built-in ones unless a test adds more with
    /// <see cref="WithRole"/>.
    /// </summary>
    private readonly Dictionary<Guid, Role> _roles = ReferenceData.Roles().ToDictionary(r => r.Id);

    /// <summary>How many times the service asked for changes to be persisted.</summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// Set to make the next save throw, standing in for PostgreSQL's unique index rejecting the
    /// row. Cleared once thrown, so a test can check the retry path.
    /// </summary>
    public Exception? NextSaveThrows { get; set; }

    public Exception? NextReadThrows { get; set; }

    public IReadOnlyList<User> Users => _users;

    public FakeUserAccountRepository WithUser(User user)
    {
        _users.Add(user);
        _snapshots[user.Id] = (user.FullName, user.Email, user.RoleId);
        return WithRoleLoaded(user);
    }

    /// <summary>Adds a role an administrator created, with what it carries.</summary>
    public FakeUserAccountRepository WithRole(Role role, params Capability[] caps)
    {
        _roles[role.Id] = role;
        _capabilities[role.Id] = caps;
        return this;
    }

    /// <summary>
    /// Mirrors the real repository's <c>Include(u => u.Role)</c>: every account handed out carries
    /// its role, looked up by id — null when the role no longer exists.
    /// </summary>
    private FakeUserAccountRepository WithRoleLoaded(User user)
    {
        user.Role = _roles.GetValueOrDefault(user.RoleId);
        return this;
    }

    public Task<IReadOnlyCollection<User>> ListAsync(CancellationToken ct = default)
        // Sorted like the real query, so a test asserting on order is testing the same contract.
        => Task.FromResult<IReadOnlyCollection<User>>(
            [.. _users.OrderBy(u => u.FullName).ThenBy(u => u.Email).Select(Loaded)]);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (NextReadThrows is { } exception)
        {
            NextReadThrows = null;
            throw exception;
        }

        return Task.FromResult(_users.FirstOrDefault(u => u.Id == id) is { } user ? Loaded(user) : null);
    }

    /// <summary>The account with its role (re)attached, as the real query's Include returns it.</summary>
    private User Loaded(User user)
    {
        WithRoleLoaded(user);
        return user;
    }

    public Task<Role?> FindRoleAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult(_roles.GetValueOrDefault(roleId));

    public Task<bool> EmailExistsAsync(
        string email, Guid? exceptId = null, CancellationToken ct = default)
        // citext in PostgreSQL, so the fake compares case-insensitively too — otherwise a test
        // would pass here and the real database would still reject the row.
        => Task.FromResult(_users.Any(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || u.Id != exceptId)));

    /// <summary>
    /// Which capabilities each role carries. Defaults to the seeded reference data, so a test
    /// that does not care gets the real answer rather than an empty one — an empty map would
    /// make the last-administrator rule believe nobody administers anything.
    /// </summary>
    private readonly Dictionary<Guid, Capability[]> _capabilities = new()
    {
        [BuiltInRoles.Chair] = [Capability.MeetingCreator, Capability.MeetingApprover],
        [BuiltInRoles.Secretary] = [Capability.MeetingCreator, Capability.MinutesEditor],
        [BuiltInRoles.Member] = [],
        [BuiltInRoles.Admin] = [Capability.AdminUsers],
    };

    public FakeUserAccountRepository WithCapabilities(Guid role, params Capability[] caps)
    {
        _capabilities[role] = caps;
        return this;
    }

    public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        Guid role, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Capability>>(
            _capabilities.TryGetValue(role, out var caps) ? caps : []);

    public Task<int> CountActiveWithCapabilityAsync(
        Capability capability, Guid exceptId, CancellationToken ct = default)
        // Mirrors the real query: active accounts, excluding this one, whose role carries the
        // capability — resolved through the map above rather than by comparing role names.
        => Task.FromResult(_users.Count(u =>
            u.IsActive
            && u.Id != exceptId
            && _capabilities.TryGetValue(u.RoleId, out var caps)
            && caps.Contains(capability)));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users.Add(user);
        _snapshots[user.Id] = (user.FullName, user.Email, user.RoleId);
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

    /// <summary>
    /// Entities whose in-memory changes were discarded, so a test can prove a rejected save does
    /// not leave the refused values tracked.
    ///
    /// The real repository reloads from the database; there is nothing to reload from here, so
    /// this records the call and restores the snapshot taken when the entity was handed out.
    /// </summary>
    public List<Guid> Discarded { get; } = [];

    public Task DiscardChangesAsync(User user, CancellationToken ct = default)
    {
        Discarded.Add(user.Id);

        if (_snapshots.TryGetValue(user.Id, out var original))
        {
            user.FullName = original.FullName;
            user.Email = original.Email;
            user.RoleId = original.RoleId;
            WithRoleLoaded(user);
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

/// <summary>
/// Runs the guarded write straight through, and counts how often it was asked to.
///
/// There is no lock to take without a database, so what a unit test can check here is that the
/// write went through the guard at all — the real serialisation is exercised in
/// <c>AdministratorInvariantGuardTests</c>, against a real PostgreSQL.
/// </summary>
internal sealed class FakeAdministratorInvariantGuard : IAdministratorInvariantGuard
{
    /// <summary>How many writes ran under the guard.</summary>
    public int WriteCount { get; private set; }

    /// <summary>
    /// Whether the guard was entered before the write began. Records the nesting rather than only
    /// the count, so a test can tell "ran inside the guard" from "ran, and the guard was also
    /// called at some point".
    /// </summary>
    public bool WasInsideGuard { get; private set; }

    private bool _inside;

    public async Task<T> WriteAsync<T>(
        Func<CancellationToken, Task<T>> write, CancellationToken ct = default)
    {
        WriteCount++;
        _inside = true;

        try
        {
            WasInsideGuard = true;
            return await write(ct);
        }
        finally
        {
            _inside = false;
        }
    }

    /// <summary>True while a guarded write is running — what a collaborator checks to prove it
    /// was called inside the lock rather than after it.</summary>
    public bool IsInsideWrite => _inside;
}
