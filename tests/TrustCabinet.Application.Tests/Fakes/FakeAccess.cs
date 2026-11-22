using TrustCabinet.Application.Access;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>The four built-in roles, Admin holding AdminUsers, as the seed has them.</summary>
public static class Roles
{
    public static Role Admin() => new() { Id = BuiltInRoles.Admin, Name = "Quản trị viên", IsBuiltIn = true };
    public static Role Member() => new() { Id = BuiltInRoles.Member, Name = "Thành viên", IsBuiltIn = true };
    public static Role Custom(string name = "Thư ký phó") => new() { Name = name };
}

/// <summary>Roles in a list. <see cref="Add"/> and <see cref="Remove"/> only stage, like the real one.</summary>
public sealed class FakeRoleRepository : IRoleRepository
{
    public List<Role> Roles { get; } = [Fakes.Roles.Admin(), Fakes.Roles.Member()];
    public Dictionary<Guid, int> UserCounts { get; } = [];
    public List<Role> Added { get; } = [];
    public List<Role> Removed { get; } = [];

    public Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<RoleSummary>>([.. Roles.Select(r => new RoleSummary(
            r.Id, r.Name, r.Description, r.IsBuiltIn, BuiltInRoles.IsDefault(r.Id), UserCounts.GetValueOrDefault(r.Id)))]);

    public Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Roles.FirstOrDefault(r => r.Id == id));

    public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken ct = default)
        => Task.FromResult(Roles.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase) && r.Id != exceptId));

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult(UserCounts.GetValueOrDefault(roleId));

    public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Capability>>([]);

    public Task AddAsync(Role role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
    {
        Added.Add(role);
        return Task.CompletedTask;
    }

    public void Remove(Role role) => Removed.Add(role);
}

/// <summary>The role ↔ capability matrix in memory; active accounts are given per role.</summary>
public sealed class FakeRolePermissionRepository : IRolePermissionRepository
{
    public List<Role> Roles { get; } = [Fakes.Roles.Admin(), Fakes.Roles.Member()];

    public Dictionary<Guid, IReadOnlyCollection<Capability>> Matrix { get; } = new()
    {
        [BuiltInRoles.Admin] = [Capability.AdminUsers],
        [BuiltInRoles.Member] = [],
    };

    public Dictionary<Guid, Guid[]> ActiveUsers { get; } = [];

    /// <summary>Every call, in order: <c>lock</c>, <c>replace</c>.</summary>
    public List<string> Calls { get; } = [];

    public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>>> GetMatrixAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>>>(Matrix.ToDictionary());

    public Task<IReadOnlyCollection<Role>> ListRolesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Role>>(Roles);

    public Task ReplaceCapabilitiesAsync(Guid roleId, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
    {
        Calls.Add("replace");
        Matrix[roleId] = capabilities;
        return Task.CompletedTask;
    }

    public Task LockAdministratorInvariantAsync(CancellationToken ct = default)
    {
        Calls.Add("lock");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Guid>> FindActiveUserIdsInRoleAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Guid>>(ActiveUsers.GetValueOrDefault(roleId, []));

    public Task<int> CountActiveUsersInRolesAsync(IReadOnlyCollection<Guid> rolesWithCapability, CancellationToken ct = default)
        => Task.FromResult(rolesWithCapability.Sum(r => ActiveUsers.GetValueOrDefault(r, []).Length));
}

/// <summary>Runs the write at once and counts it; the real lock is <c>AdministratorInvariantGuard</c>.</summary>
public sealed class FakeAdministratorInvariantGuard : IAdministratorInvariantGuard
{
    public int Writes { get; private set; }

    public Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken ct = default)
    {
        Writes++;
        return write(ct);
    }
}

public sealed class FakeCapabilitiesChangePublisher : ICapabilitiesChangePublisher
{
    public List<Guid> Told { get; } = [];
    public Exception? Fail { get; set; }

    /// <summary>Runs first on every publish, so a test can see what had happened by then.</summary>
    public Action? OnPublish { get; set; }

    public Task PublishAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        OnPublish?.Invoke();
        if (Fail is { } failure) return Task.FromException(failure);
        Told.AddRange(userIds);
        return Task.CompletedTask;
    }
}

public sealed class FakeCalendarShareStopper : ICalendarShareStopper
{
    public List<(Guid UserId, CalendarShareStopReason Reason)> Stopped { get; } = [];

    public Task<int> StopDeadSharesAsync(IReadOnlyCollection<Guid> userIds, Guid? actorId, CalendarShareStopReason reason,
        CancellationToken ct = default)
    {
        Stopped.AddRange(userIds.Select(id => (id, reason)));
        return Task.FromResult(0);
    }
}

/// <summary>The audit service itself, faked: what was recorded, by action.</summary>
public sealed class FakeAuditService : IAuditService
{
    public List<string> Actions { get; } = [];

    /// <summary>The details each recorded action carried, in the same order as <see cref="Actions"/>.</summary>
    public List<object?> Details { get; } = [];

    /// <summary>
    /// Thrown by every record, in place of saving. The real one saves whatever the use case staged, so this is where
    /// a refused insert of that staged row surfaces.
    /// </summary>
    public Exception? Fail { get; set; }

    public Task RecordAsync(string action, string objectType, Guid? objectId, Guid? actorId, object? details = null,
        CancellationToken ct = default)
    {
        if (Fail is { } failure) return Task.FromException(failure);
        Actions.Add(action);
        Details.Add(details);
        return Task.CompletedTask;
    }
}
