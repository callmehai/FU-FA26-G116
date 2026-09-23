using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// Hand-written stand-ins for the role permission service's collaborators, in the same style as
/// <c>UserTestDoubles</c>: no mocking library, and each fake records enough for an assertion to be
/// about real behaviour rather than about a call having been made.
/// </summary>
internal sealed class FakeRolePermissionRepository : IRolePermissionRepository
{
    /// <summary>
    /// Starts from the seeded defaults, so a test that does not care gets the real matrix rather
    /// than an empty one — an empty matrix would make the lock-out rule believe no role
    /// administers anything, which is the opposite of the usual starting state.
    /// </summary>
    private readonly Dictionary<Guid, List<Capability>> _matrix =
        ReferenceData.RoleCapabilities
            .GroupBy(rc => rc.RoleId)
            .ToDictionary(group => group.Key, group => group.Select(rc => rc.Capability).ToList());

    /// <summary>
    /// Who holds each role. Starts with one active administrator, because that is the state every
    /// real system is in — a database with no administrator at all cannot be reached through the
    /// application. A test that cares about the lock-out rule overrides this with
    /// <see cref="WithNoUsers"/>.
    /// </summary>
    private readonly Dictionary<Guid, List<Guid>> _usersByRole = new()
    {
        [BuiltInRoles.Admin] = [Guid.CreateVersion7()],
    };

    /// <summary>The four built-in roles, plus any a test adds with <see cref="WithRole"/>.</summary>
    private readonly List<Role> _roles = [.. ReferenceData.Roles()];

    /// <summary>A role an administrator created, with nothing granted and nobody holding it.</summary>
    public FakeRolePermissionRepository WithRole(Role role)
    {
        _roles.Add(role);
        return this;
    }

    public Task<IReadOnlyCollection<Role>> ListRolesAsync(CancellationToken ct = default)
        // Same order as the real query: built-in first, then by creation time.
        => Task.FromResult<IReadOnlyCollection<Role>>(
            [.. _roles.OrderBy(r => BuiltInRoles.SortKey(r.Id)).ThenBy(r => r.CreatedAt)]);

    /// <summary>Empties the organisation, for the tests about nobody being left to administer.</summary>
    public FakeRolePermissionRepository WithNoUsers()
    {
        _usersByRole.Clear();
        return this;
    }

    /// <summary>How many times the service asked for changes to be persisted.</summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// Set to make the next save throw, standing in for the database refusing the write —
    /// UC-10's MSG18 branch.
    /// </summary>
    public Exception? NextSaveThrows { get; set; }

    public FakeRolePermissionRepository WithCapabilities(Guid role, params Capability[] caps)
    {
        _matrix[role] = [.. caps];
        return this;
    }

    public FakeRolePermissionRepository WithActiveUser(Guid role, Guid userId)
    {
        if (!_usersByRole.TryGetValue(role, out var ids))
        {
            _usersByRole[role] = ids = [];
        }

        ids.Add(userId);
        return this;
    }

    public IReadOnlyCollection<Capability> CapabilitiesOf(Guid role)
        => _matrix.TryGetValue(role, out var caps) ? caps : [];

    public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>>> GetMatrixAsync(
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>>>(
            _matrix.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyCollection<Capability>)[.. entry.Value]));

    public Task ReplaceCapabilitiesAsync(
        Guid role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
    {
        // Staged rather than applied, mirroring the real repository: nothing is visible until
        // SaveChangesAsync, so a test can prove a failed save leaves the matrix as it was.
        _pending = (role, [.. capabilities]);
        return Task.CompletedTask;
    }

    private (Guid RoleId, List<Capability> Capabilities)? _pending;

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;

        if (NextSaveThrows is { } exception)
        {
            NextSaveThrows = null;
            _pending = null;
            throw exception;
        }

        if (_pending is { } pending)
        {
            _matrix[pending.RoleId] = pending.Capabilities;
            _pending = null;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Guid>> FindActiveUserIdsInRoleAsync(
        Guid role, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<Guid>>(
            _usersByRole.TryGetValue(role, out var ids) ? [.. ids] : []);

    public Task<int> CountActiveUsersInRolesAsync(
        IReadOnlyCollection<Guid> rolesWithCapability, CancellationToken ct = default)
        // Only accounts added through WithActiveUser count, mirroring the real query's
        // `IsActive` filter — a test that grants a capability to a role nobody holds must see
        // zero here, because that is precisely the lock-out this rule exists to catch.
        => Task.FromResult(rolesWithCapability.Sum(
            role => _usersByRole.TryGetValue(role, out var ids) ? ids.Count : 0));
}
