using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
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
    private readonly FakeAdministratorInvariantGuard _invariant = new();
    private readonly Guid _actorId = Guid.CreateVersion7();

    private RolePermissionService NewService() => new(
        _roles, _audit, _refreshTokens, _invariant,
        NullLogger<RolePermissionService>.Instance);

    // ------------------------------------------------------ UC-09 · View role & permission matrix

    [Fact]
    public async Task Matrix_lists_every_role_even_one_with_no_capabilities()
    {
        var matrix = await NewService().GetMatrixAsync();

        // Member holds nothing, and still has to appear: the screen draws a grid, and a missing
        // role would be a missing row rather than an empty one.
        Assert.Equal(BuiltInRoles.All.Count, matrix.Roles.Count);

        var member = matrix.Roles.Single(r => r.Id == BuiltInRoles.Member);
        Assert.Empty(member.Capabilities);
    }

    [Fact]
    public async Task Matrix_lists_built_in_roles_first_then_custom_roles_by_creation()
    {
        // The same order GET /api/roles uses, so the two screens never disagree.
        var older = new Role { Name = "Uỷ viên thường trực", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var newer = new Role { Name = "Thư ký phó", Description = "Hỗ trợ thư ký." };
        _roles.WithRole(newer).WithRole(older);

        var matrix = await NewService().GetMatrixAsync();

        Assert.Equal(
            [.. BuiltInRoles.All, older.Id, newer.Id],
            matrix.Roles.Select(r => r.Id));

        var deputy = matrix.Roles.Single(r => r.Id == newer.Id);
        Assert.Equal("Thư ký phó", deputy.Name);
        Assert.Equal("Hỗ trợ thư ký.", deputy.Description);
        Assert.False(deputy.IsBuiltIn);
        Assert.Empty(deputy.Capabilities);

        Assert.All(matrix.Roles.Take(4), r => Assert.True(r.IsBuiltIn));
    }

    [Fact]
    public async Task Matrix_returns_the_seeded_defaults()
    {
        var matrix = await NewService().GetMatrixAsync();

        var admin = matrix.Roles.Single(r => r.Id == BuiltInRoles.Admin);
        Assert.Equal([Capability.AdminUsers], admin.Capabilities);

        var secretary = matrix.Roles.Single(r => r.Id == BuiltInRoles.Secretary);
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
            BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal([Capability.MeetingCreator], _roles.CapabilitiesOf(BuiltInRoles.Member));

        var member = result.Matrix!.Roles.Single(r => r.Id == BuiltInRoles.Member);
        Assert.Equal([Capability.MeetingCreator], member.Capabilities);
    }

    [Fact]
    public async Task Update_can_remove_every_capability_from_a_role()
    {
        // Legitimate: a secretary who may no longer create meetings. Only AdminUsers is protected,
        // and only as the last holder of it.
        var result = await NewService().UpdateAsync(BuiltInRoles.Secretary, [], _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_roles.CapabilitiesOf(BuiltInRoles.Secretary));
    }

    [Fact]
    public async Task Update_records_the_change_with_before_and_after()
    {
        await NewService().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

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

        // Roles are rows now: the row's id points at the role, and its name as it was at the time
        // travels in the details, since a role can be renamed later.
        Assert.Equal(BuiltInRoles.Member, record.ObjectId);

        // Before was empty and after holds exactly the granted capability.
        using var parsed = JsonDocument.Parse(details);
        Assert.Equal("Thành viên", parsed.RootElement.GetProperty("Name").GetString());
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
        _roles.WithActiveUser(BuiltInRoles.Member, affected);
        _roles.WithActiveUser(BuiltInRoles.Chair, untouched);

        await NewService().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        // Capabilities travel inside the access token, so without this the people holding this
        // role keep the old set until their token expires — SCRUM-60's "có hiệu lực ngay".
        Assert.Contains(affected, _refreshTokens.RevokedUsers);
        Assert.DoesNotContain(untouched, _refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Update_ends_sessions_when_a_capability_is_granted_too()
    {
        var member = Guid.CreateVersion7();
        _roles.WithActiveUser(BuiltInRoles.Member, member);

        await NewService().UpdateAsync(BuiltInRoles.Member, [Capability.MinutesEditor], _actorId);

        // Not only on a removal: someone whose role just gained a capability holds a token that
        // does not carry it, so their new rights would not work until it lapsed either.
        Assert.Contains(member, _refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Sessions_are_ended_after_the_transaction_commits_not_inside_it()
    {
        // Revoking runs one Redis round trip per affected account, in sequence. Inside the
        // invariant lock that would block every other administrative write for the whole run, and
        // a Redis outage would roll back a configuration change the database had accepted.
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());

        _refreshTokens.OnRevokeAll = () =>
            Assert.False(_invariant.IsInsideWrite, "Sessions were revoked while the lock was held.");

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(2, _refreshTokens.RevokedUsers.Count);
    }

    [Fact]
    public async Task The_matrix_change_survives_the_session_store_being_down()
    {
        // The configuration is saved and correct. Telling the administrator it failed would have
        // them repeat a change that did happen; what is actually lost is the early end of some
        // sessions, which lapse by themselves when their access tokens expire.
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());
        _refreshTokens.OnRevokeAll = () => throw new InvalidOperationException("Redis khong phan hoi");

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.True(result.Succeeded);
        Assert.Single(_audit.Records);
    }

    [Fact]
    public async Task One_unreachable_session_does_not_stop_the_others_being_ended()
    {
        // The loop goes on after a failure: one account whose revoke threw must not leave everyone
        // else on the role still holding a usable refresh token.
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());

        var attempts = 0;
        _refreshTokens.OnRevokeAll = () =>
        {
            if (++attempts == 1)
            {
                throw new InvalidOperationException("Redis tu choi mot lan");
            }
        };

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task The_update_runs_under_the_shared_invariant_lock()
    {
        // The lock-out hole the review found: this service and UserService both decide "is anyone
        // still able to administer?", and they used to take different locks — so a matrix edit and
        // a deactivation could each remove the last administrator at the same time, neither
        // seeing the other. They now queue behind one name.
        await NewService().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.Equal(1, _invariant.WriteCount);
    }

    [Fact]
    public async Task Update_that_changes_nothing_writes_nothing()
    {
        var member = Guid.CreateVersion7();
        _roles.WithActiveUser(BuiltInRoles.Admin, member);

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Admin, [Capability.AdminUsers], _actorId);

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
            BuiltInRoles.Member,
            [Capability.MeetingCreator, Capability.MeetingCreator],
            _actorId);

        Assert.True(result.Succeeded);

        // Stored once, because the service de-duplicates before writing. That it *must* is
        // enforced by the composite primary key in PostgreSQL, which this fake does not have —
        // RolePermissionRepositoryTests covers the real table.
        Assert.Equal([Capability.MeetingCreator], _roles.CapabilitiesOf(BuiltInRoles.Member));
    }

    // ------------------------------------------- UC-10 · MSG17, invalid permission configuration

    [Fact]
    public async Task Update_refuses_to_remove_admin_users_from_the_last_role_holding_it()
    {
        // The lock-out rule, and the reason this screen is dangerous: AdminUsers gates this very
        // endpoint, so a system where nobody holds it cannot be repaired through the application.
        var result = await NewService().UpdateAsync(BuiltInRoles.Admin, [], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);

        // Its own key, not the generic "capabilities" one: "you would lock everyone out" is the
        // message an administrator most needs to read correctly, and the screen cannot mark it
        // apart from "you sent an unknown capability" if both arrive under the same name.
        Assert.Contains("adminUsers", result.Errors!.Keys);

        // And nothing was written.
        Assert.Equal(0, _roles.SaveCount);
        Assert.Equal([Capability.AdminUsers], _roles.CapabilitiesOf(BuiltInRoles.Admin));
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_allows_moving_admin_users_to_a_role_that_has_active_users()
    {
        // The point of the table is that the capability can be re-pointed without a deployment.
        // Legitimate — as long as somebody actually holds the destination role.
        _roles.WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor, Capability.AdminUsers);
        _roles.WithActiveUser(BuiltInRoles.Secretary, Guid.CreateVersion7());

        var result = await NewService().UpdateAsync(BuiltInRoles.Admin, [], _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_roles.CapabilitiesOf(BuiltInRoles.Admin));
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
        _roles.WithCapabilities(BuiltInRoles.Member, Capability.AdminUsers);

        var result = await NewService().UpdateAsync(BuiltInRoles.Admin, [], _actorId);

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
            BuiltInRoles.Secretary, [Capability.MinutesEditor], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains("adminUsers", result.Errors!.Keys);
    }

    [Fact]
    public async Task Granting_admin_users_to_a_role_with_users_is_allowed()
    {
        // The safe direction of the same rule: adding a second administering role is fine, and
        // must not be mistaken for the lock-out case by a rule that only counts.
        _roles.WithActiveUser(BuiltInRoles.Secretary, Guid.CreateVersion7());

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Secretary, [Capability.AdminUsers], _actorId);

        Assert.True(result.Succeeded);
        Assert.Contains(Capability.AdminUsers, _roles.CapabilitiesOf(BuiltInRoles.Secretary));
    }

    [Fact]
    public async Task Update_refuses_an_unknown_role()
    {
        // A stale screen after another administrator deleted the role, or a made-up id. Written
        // unchecked, the foreign key would turn it into a 500.
        var result = await NewService().UpdateAsync(
            Guid.CreateVersion7(), [Capability.MeetingCreator], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains("roleId", result.Errors!.Keys);
        Assert.Equal(0, _roles.SaveCount);
    }

    [Fact]
    public async Task Update_configures_a_custom_role_like_any_other()
    {
        var deputy = new Role { Name = "Thư ký phó" };
        _roles.WithRole(deputy);

        var result = await NewService().UpdateAsync(
            deputy.Id, [Capability.MinutesEditor], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal([Capability.MinutesEditor], _roles.CapabilitiesOf(deputy.Id));
        Assert.Equal(deputy.Id, Assert.Single(_audit.Records).ObjectId);
    }

    [Fact]
    public async Task A_custom_role_can_take_over_administration()
    {
        // ADR-0007's invariant counts accounts, not role names: once somebody active holds a
        // custom role carrying AdminUsers, the built-in Admin role may give it up.
        var board = new Role { Name = "Ban quản trị" };
        _roles.WithNoUsers().WithRole(board).WithActiveUser(board.Id, Guid.CreateVersion7());

        Assert.True((await NewService().UpdateAsync(
            board.Id, [Capability.AdminUsers], _actorId)).Succeeded);

        var result = await NewService().UpdateAsync(BuiltInRoles.Admin, [], _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_roles.CapabilitiesOf(BuiltInRoles.Admin));
    }

    [Fact]
    public async Task Update_refuses_an_unknown_capability()
    {
        var result = await NewService().UpdateAsync(
            BuiltInRoles.Member, [(Capability)(-1)], _actorId);

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
            BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.Equal(RolePermissionOutcome.SaveFailed, result.Outcome);
        Assert.Null(result.Matrix);

        // Nothing changed, nothing recorded, nobody signed out — the diagram's failure branch
        // stops before all three.
        Assert.Empty(_roles.CapabilitiesOf(BuiltInRoles.Member));
        Assert.Empty(_audit.Records);
        Assert.Empty(_refreshTokens.RevokedUsers);
    }
}
