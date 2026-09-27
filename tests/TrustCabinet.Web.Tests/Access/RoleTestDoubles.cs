using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// Hand-written stand-in for the role repository, in the style of the other test doubles: no
/// mocking library, and enough state for an assertion to be about what ended up stored.
/// </summary>
internal sealed class FakeRoleRepository : IRoleRepository
{
    /// <summary>Starts with the four built-in roles and the seeded matrix, as a real database does.</summary>
    private readonly List<Role> _roles = [.. ReferenceData.Roles()];

    private readonly Dictionary<Guid, List<Capability>> _capabilities =
        ReferenceData.RoleCapabilities
            .GroupBy(rc => rc.RoleId)
            .ToDictionary(group => group.Key, group => group.Select(rc => rc.Capability).ToList());

    /// <summary>How many accounts hold each role — deactivated ones included, like the real count.</summary>
    private readonly Dictionary<Guid, int> _userCounts = [];

    private readonly List<(Role Role, List<Capability> Capabilities)> _pendingAdds = [];
    private readonly List<Role> _pendingRemovals = [];

    public IReadOnlyList<Role> Roles => _roles;

    public int SaveCount { get; private set; }

    /// <summary>Makes the next save throw, standing in for the database refusing the write.</summary>
    public Exception? NextSaveThrows { get; set; }

    /// <summary>
    /// Runs at the start of the next save — how a test makes something happen "in the meantime",
    /// between the service's checks and its write.
    /// </summary>
    public Action? OnSave { get; set; }

    public FakeRoleRepository WithRole(Role role, params Capability[] capabilities)
    {
        _roles.Add(role);
        _capabilities[role.Id] = [.. capabilities];
        return this;
    }

    public FakeRoleRepository WithUsers(Guid roleId, int count)
    {
        _userCounts[roleId] = count;
        return this;
    }

    public IReadOnlyCollection<Capability> CapabilitiesOf(Guid roleId)
        => _capabilities.TryGetValue(roleId, out var caps) ? caps : [];

    public Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<RoleSummary>>(
            [.. _roles
                .OrderBy(r => BuiltInRoles.SortKey(r.Id))
                .ThenBy(r => r.CreatedAt)
                .Select(r => new RoleSummary(
                    r.Id, r.Name, r.Description, r.IsBuiltIn, BuiltInRoles.IsDefault(r.Id),
                    _userCounts.GetValueOrDefault(r.Id)))]);

    public Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_roles.FirstOrDefault(r => r.Id == id));

    public Task<bool> NameExistsAsync(
        string name, Guid? exceptId = null, CancellationToken ct = default)
        // citext in PostgreSQL, so the fake ignores case too — otherwise a test would pass here
        // while the real index refused the row.
        => Task.FromResult(_roles.Any(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || r.Id != exceptId)));

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult(_userCounts.GetValueOrDefault(roleId));

    public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Capability>>([.. CapabilitiesOf(roleId).Order()]);

    public Task AddAsync(
        Role role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
    {
        _pendingAdds.Add((role, [.. capabilities]));
        return Task.CompletedTask;
    }

    public void Remove(Role role) => _pendingRemovals.Add(role);

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        OnSave?.Invoke();
        OnSave = null;

        if (NextSaveThrows is { } exception)
        {
            // Nothing staged survives a refused save, as with a rolled-back savepoint.
            NextSaveThrows = null;
            _pendingAdds.Clear();
            _pendingRemovals.Clear();
            throw exception;
        }

        foreach (var (role, capabilities) in _pendingAdds)
        {
            _roles.Add(role);
            _capabilities[role.Id] = capabilities;
        }

        foreach (var role in _pendingRemovals)
        {
            // The cascade: the role's capabilities go with it.
            _roles.Remove(role);
            _capabilities.Remove(role.Id);
        }

        _pendingAdds.Clear();
        _pendingRemovals.Clear();

        return Task.CompletedTask;
    }

    /// <summary>What EF throws when PostgreSQL refuses a save with <paramref name="sqlState"/>.</summary>
    public static DbUpdateException Refused(string sqlState)
        => new("refused", new PostgresException("refused", "ERROR", "ERROR", sqlState));
}
