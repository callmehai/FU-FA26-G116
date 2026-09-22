using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Web.Tests.Authentication;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The rules of UC-09 and UC-10 (SCRUM-60), one test per branch of each sequence diagram, plus the
/// cases the diagrams leave implicit — an unknown role, a no-op save, and the lock-out rule the
/// acceptance criteria add.
///
/// No database and no HTTP: the branches are the part worth pinning down, and they are decided
/// entirely inside <see cref="RolePermissionService"/>.
/// </summary>
public class RolePermissionServiceTests
{
    private readonly FakeRolePermissionRepository _roles = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly Guid _actorId = Guid.CreateVersion7();

    private RolePermissionService NewService() => new(
        _roles, _audit, _refreshTokens, NullLogger<RolePermissionService>.Instance);

    // ------------------------------------------------------ UC-09 · View role & permission matrix

    [Fact]
    public async Task Matrix_lists_every_role_even_one_with_no_capabilities()
    {
        var matrix = await NewService().GetMatrixAsync();

        // Member holds nothing, and still has to appear: the screen draws a grid, and a missing
        // role would be a missing row rather than an empty one.
        Assert.Equal(Enum.GetValues<SystemRole>().Length, matrix.Roles.Count);

        var member = matrix.Roles.Single(r => r.Role == SystemRole.Member);
        Assert.Empty(member.Capabilities);
    }

    [Fact]
    public async Task Matrix_returns_the_seeded_defaults()
    {
        var matrix = await NewService().GetMatrixAsync();

        var admin = matrix.Roles.Single(r => r.Role == SystemRole.Admin);
        Assert.Equal([Capability.AdminUsers], admin.Capabilities);

        var secretary = matrix.Roles.Single(r => r.Role == SystemRole.Secretary);
        Assert.Contains(Capability.MinutesEditor, secretary.Capabilities);
        Assert.Contains(Capability.MeetingCreator, secretary.Capabilities);
    }

    [Fact]
    public async Task Matrix_carries_the_whole_capability_catalogue()
    {
        var matrix = await NewService().GetMatrixAsync();

        // Every capability the system enforces, so the grid's columns come from the backend
        // rather than from a hard-coded list in the browser.
        Assert.Equal(Enum.GetValues<Capability>().Length, matrix.Capabilities.Count);
        Assert.All(matrix.Capabilities, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
    }

    // ------------------------------------------------------- UC-10 · Configure role permissions

    [Fact]
    public async Task Update_saves_the_requested_capabilities()
    {
        var result = await NewService().UpdateAsync(
            SystemRole.Member, [Capability.MeetingCreator], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal([Capability.MeetingCreator], _roles.CapabilitiesOf(SystemRole.Member));

        var member = result.Matrix!.Roles.Single(r => r.Role == SystemRole.Member);
        Assert.Equal([Capability.MeetingCreator], member.Capabilities);
    }

    [Fact]
    public async Task Update_can_remove_every_capability_from_a_role()
    {
        // Legitimate: a secretary who may no longer create meetings. Only AdminUsers is protected,
        // and only as the last holder of it.
        var result = await NewService().UpdateAsync(SystemRole.Secretary, [], _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_roles.CapabilitiesOf(SystemRole.Secretary));
    }

    [Fact]
    public async Task Update_records_the_change_with_before_and_after()
    {
        await NewService().UpdateAsync(SystemRole.Member, [Capability.MeetingCreator], _actorId);

        var record = Assert.Single(_audit.Records);
        Assert.Equal(AuditActions.RolePermissionsUpdated, record.Action);
        Assert.Equal(AuditActions.RoleObjectType, record.ObjectType);
        Assert.Equal(_actorId, record.ActorId);

        // The acceptance criteria ask for the values before and after, which is most of what makes
        // the record worth keeping.
        //
        // Serialised rather than ToString()'d: the details are an anonymous object whose ToString
        // prints "System.String[]" for the two arrays, so asserting on it would pass while the
        // audit row said nothing. This is also what AuditService actually stores.
        var details = JsonSerializer.Serialize(record.Details);

        Assert.Contains("Member", details);
        Assert.Contains("MeetingCreator", details);

        // Before was empty and after holds exactly the granted capability.
        using var parsed = JsonDocument.Parse(details);
        Assert.Empty(parsed.RootElement.GetProperty("Before").EnumerateArray());
        Assert.Equal(
            "MeetingCreator",
            parsed.RootElement.GetProperty("After").EnumerateArray().Single().GetString());
    }

    [Fact]
    public async Task Update_ends_the_sessions_of_everyone_holding_the_role()
    {
        var affected = Guid.CreateVersion7();
        var untouched = Guid.CreateVersion7();
        _roles.WithActiveUser(SystemRole.Member, affected);
        _roles.WithActiveUser(SystemRole.Chair, untouched);

        await NewService().UpdateAsync(SystemRole.Member, [Capability.MeetingCreator], _actorId);

        // Capabilities travel inside the access token, so without this the people holding this
        // role keep the old set until their token expires — SCRUM-60's "có hiệu lực ngay".
        Assert.Contains(affected, _refreshTokens.RevokedUsers);
        Assert.DoesNotContain(untouched, _refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Update_ends_sessions_when_a_capability_is_granted_too()
    {
        var member = Guid.CreateVersion7();
        _roles.WithActiveUser(SystemRole.Member, member);

        await NewService().UpdateAsync(SystemRole.Member, [Capability.MinutesEditor], _actorId);

        // Not only on a removal: someone whose role just gained a capability holds a token that
        // does not carry it, so their new rights would not work until it lapsed either.
        Assert.Contains(member, _refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Update_that_changes_nothing_writes_nothing()
    {
        var member = Guid.CreateVersion7();
        _roles.WithActiveUser(SystemRole.Admin, member);

        var result = await NewService().UpdateAsync(
            SystemRole.Admin, [Capability.AdminUsers], _actorId);

        // Answered as success — the system is in the state asked for — but nothing happened, so
        // there is no audit row for a change that was not made and no session ended for
        // capabilities nobody lost.
        Assert.True(result.Succeeded);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
        Assert.Empty(_refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Update_ignores_duplicates_in_the_request()
    {
        var result = await NewService().UpdateAsync(
            SystemRole.Member,
            [Capability.MeetingCreator, Capability.MeetingCreator],
            _actorId);

        Assert.True(result.Succeeded);

        // Stored once, because the service de-duplicates before writing. That it *must* is
        // enforced by the composite primary key in PostgreSQL, which this fake does not have —
        // RolePermissionRepositoryTests covers the real table.
        Assert.Equal([Capability.MeetingCreator], _roles.CapabilitiesOf(SystemRole.Member));
    }

    // ------------------------------------------- UC-10 · MSG17, invalid permission configuration

    [Fact]
    public async Task Update_refuses_to_remove_admin_users_from_the_last_role_holding_it()
    {
        // The lock-out rule, and the reason this screen is dangerous: AdminUsers gates this very
        // endpoint, so a system where nobody holds it cannot be repaired through the application.
        var result = await NewService().UpdateAsync(SystemRole.Admin, [], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);

        // Its own key, not the generic "capabilities" one: "you would lock everyone out" is the
        // message an administrator most needs to read correctly, and the screen cannot mark it
        // apart from "you sent an unknown capability" if both arrive under the same name.
        Assert.Contains("adminUsers", result.Errors!.Keys);

        // And nothing was written.
        Assert.Equal(0, _roles.SaveCount);
        Assert.Equal([Capability.AdminUsers], _roles.CapabilitiesOf(SystemRole.Admin));
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_allows_moving_admin_users_to_a_role_that_has_active_users()
    {
        // The point of the table is that the capability can be re-pointed without a deployment.
        // Legitimate — as long as somebody actually holds the destination role.
        _roles.WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor, Capability.AdminUsers);
        _roles.WithActiveUser(SystemRole.Secretary, Guid.CreateVersion7());

        var result = await NewService().UpdateAsync(SystemRole.Admin, [], _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_roles.CapabilitiesOf(SystemRole.Admin));
    }

    [Fact]
    public async Task Update_refuses_to_move_admin_users_to_a_role_nobody_holds()
    {
        // The two-request lock-out. Granting AdminUsers to an empty role and then stripping it
        // from Admin leaves the capability assigned to a role no living account has — so no
        // person can administer, and the screen that could undo it is the one being closed.
        //
        // A check of "does some role still hold it?" says yes here, which is why the rule counts
        // active accounts instead.
        _roles.WithCapabilities(SystemRole.Member, Capability.AdminUsers);

        var result = await NewService().UpdateAsync(SystemRole.Admin, [], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains("adminUsers", result.Errors!.Keys);
        Assert.Equal(0, _roles.SaveCount);
    }

    [Fact]
    public async Task Update_refuses_when_the_only_administrator_account_is_inactive()
    {
        // Deactivating the last administrator is already refused by UserService, but an account
        // can also be inactive for reasons that predate this matrix. Either way an inactive
        // account cannot sign in, so it is not somebody who can still administer.
        _roles.WithNoUsers();

        var result = await NewService().UpdateAsync(
            SystemRole.Secretary, [Capability.MinutesEditor], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains("adminUsers", result.Errors!.Keys);
    }

    [Fact]
    public async Task Granting_admin_users_to_a_role_with_users_is_allowed()
    {
        // The safe direction of the same rule: adding a second administering role is fine, and
        // must not be mistaken for the lock-out case by a rule that only counts.
        _roles.WithActiveUser(SystemRole.Secretary, Guid.CreateVersion7());

        var result = await NewService().UpdateAsync(
            SystemRole.Secretary, [Capability.AdminUsers], _actorId);

        Assert.True(result.Succeeded);
        Assert.Contains(Capability.AdminUsers, _roles.CapabilitiesOf(SystemRole.Secretary));
    }

    [Fact]
    public async Task Update_refuses_an_unknown_role()
    {
        // Arrives when a caller posts a role name the enum has no value for. Stored unchecked it
        // would become a grant nothing matches.
        var result = await NewService().UpdateAsync(
            (SystemRole)(-1), [Capability.MeetingCreator], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains("role", result.Errors!.Keys);
        Assert.Equal(0, _roles.SaveCount);
    }

    [Fact]
    public async Task Update_refuses_an_unknown_capability()
    {
        var result = await NewService().UpdateAsync(
            SystemRole.Member, [(Capability)(-1)], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains("capabilities", result.Errors!.Keys);
        Assert.Equal(0, _roles.SaveCount);
    }

    // ------------------------------------------------ UC-10 · MSG18, configuration cannot be saved

    [Fact]
    public async Task Update_answers_save_failed_when_the_database_refuses()
    {
        _roles.NextSaveThrows = new DbUpdateException("the database said no");

        var result = await NewService().UpdateAsync(
            SystemRole.Member, [Capability.MeetingCreator], _actorId);

        Assert.Equal(RolePermissionOutcome.SaveFailed, result.Outcome);
        Assert.Null(result.Matrix);

        // Nothing changed, nothing recorded, nobody signed out — the diagram's failure branch
        // stops before all three.
        Assert.Empty(_roles.CapabilitiesOf(SystemRole.Member));
        Assert.Empty(_audit.Records);
        Assert.Empty(_refreshTokens.RevokedUsers);
    }
}
