using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Web.Tests.Authentication;
using TrustCabinet.Web.Tests.Users;
using TrustCabinet.Web.Tests.Persistence;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The rules of UC-09, UC-10 (SCRUM-60), one test per branch of each sequence diagram, plus the
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
    private readonly FakeCapabilitiesChangePublisher _publisher = new();
    private readonly FakeAdministratorInvariantGuard _invariant = new();
    private readonly Guid _actorId = Guid.CreateVersion7();
    private readonly FakeCalendarShareStopper _calendarShares;

    public RolePermissionServiceTests() => _calendarShares = new FakeCalendarShareStopper(_invariant);

    private RolePermissionService NewService() => new(
        _roles, new TranslatingUnitOfWork(_roles.SaveChangesAsync), _audit, _publisher, _invariant, _calendarShares,
        NullLogger<RolePermissionService>.Instance);

    // ------------------------------------------------------ UC-09 · View role & permission matrix

    [Fact]
    public async Task Matrix_lists_every_role_even_one_with_no_capabilities()
    {
        // A role holding nothing still has to appear: the screen draws a grid, and a missing role would be a missing
        // row rather than an empty one. Since ADR-0075 every built-in role holds something, so this one is custom.
        var empty = new Role { Name = "Khách mời thường xuyên" };
        _roles.WithRole(empty);

        var matrix = await NewService().GetMatrixAsync();

        Assert.Equal(BuiltInRoles.All.Count + 1, matrix.Roles.Count);
        Assert.Empty(matrix.Roles.Single(r => r.Id == empty.Id).Capabilities);
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

        Assert.All(matrix.Roles.Take(BuiltInRoles.All.Count), r => Assert.True(r.IsBuiltIn));
    }

    [Fact]
    public async Task Matrix_returns_the_seeded_defaults()
    {
        var matrix = await NewService().GetMatrixAsync();

        // ADR-0075: the three built-in roles and what each carries.
        Assert.Equal([Capability.MeetingCreator],
            matrix.Roles.Single(r => r.Id == BuiltInRoles.Member).Capabilities.Order());
        Assert.Equal([Capability.MeetingCreator, Capability.MeetingApprover],
            matrix.Roles.Single(r => r.Id == BuiltInRoles.Approver).Capabilities.Order());
        Assert.Equal([Capability.MeetingCreator, Capability.AdminUsers, Capability.VoiceCollector],
            matrix.Roles.Single(r => r.Id == BuiltInRoles.Admin).Capabilities.Order());
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
            BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingApprover], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal([Capability.MeetingCreator, Capability.MeetingApprover], _roles.CapabilitiesOf(BuiltInRoles.Member).Order());

        var member = result.Matrix!.Roles.Single(r => r.Id == BuiltInRoles.Member);
        Assert.Equal([Capability.MeetingCreator, Capability.MeetingApprover], member.Capabilities.Order());
    }

    [Fact]
    public async Task Update_can_remove_every_capability_from_a_role()
    {
        // Legitimate: an approver who may no longer create or approve meetings. Only AdminUsers is protected,
        // and only as the last holder of it.
        var result = await NewService().UpdateAsync(BuiltInRoles.Approver, [], _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_roles.CapabilitiesOf(BuiltInRoles.Approver));
    }

    [Fact]
    public async Task Update_records_the_change_with_before_and_after()
    {
        await NewService().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingApprover], _actorId);

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

        // Before holds the default (ADR-0075), after the default and the granted capability.
        using var parsed = JsonDocument.Parse(details);
        Assert.Equal("Thành viên", parsed.RootElement.GetProperty("Name").GetString());
        Assert.Equal(["MeetingCreator"], parsed.RootElement.GetProperty("Before").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["MeetingApprover", "MeetingCreator"],
            parsed.RootElement.GetProperty("After").EnumerateArray().Select(e => e.GetString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Update_tells_everyone_holding_the_role_and_nobody_else()
    {
        var affected = Guid.CreateVersion7();
        var untouched = Guid.CreateVersion7();
        _roles.WithActiveUser(BuiltInRoles.Member, affected);
        _roles.WithActiveUser(BuiltInRoles.Approver, untouched);

        await NewService().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingApprover], _actorId);

        // Capabilities travel inside the access token: told, the browser refreshes now instead of
        // carrying the old set until the token is close to expiring.
        Assert.Contains(affected, _publisher.Notified);
        Assert.DoesNotContain(untouched, _publisher.Notified);
    }

    [Fact]
    public async Task Update_tells_the_role_when_a_capability_is_taken_away_too()
    {
        var approver = Guid.CreateVersion7();
        _roles.WithActiveUser(BuiltInRoles.Approver, approver);

        await NewService().UpdateAsync(BuiltInRoles.Approver, [], _actorId);

        // The server already refuses the lost capability; the push lets the menu drop it too.
        Assert.Equal([approver], _publisher.Notified);
    }

    [Fact]
    public async Task Accounts_are_told_after_the_transaction_commits_not_inside_it()
    {
        // Told before the commit, a browser could refresh, read the old matrix, and wait for a
        // push that has already been and gone.
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());

        _publisher.OnPublish = () =>
            Assert.False(_invariant.IsInsideWrite, "Accounts were told while the lock was held.");

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingApprover], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(2, _publisher.Notified.Count);
    }

    [Fact]
    public async Task The_matrix_change_survives_the_push_failing()
    {
        // The configuration is saved and correct. Telling the administrator it failed would have
        // them repeat a change that did happen; what is lost is only an early token refresh.
        _roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());
        _publisher.OnPublish = () => throw new InvalidOperationException("Hub khong phan hoi");

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingApprover], _actorId);

        Assert.True(result.Succeeded);
        Assert.Single(_audit.Records);
    }

    [Fact]
    public async Task The_update_runs_under_the_shared_invariant_lock()
    {
        // The lock-out hole the review found: this service and UserService both decide "is anyone
        // still able to administer?", and they used to take different locks — so a matrix edit and
        // a deactivation could each remove the last administrator at the same time, neither
        // seeing the other. They now queue behind one name.
        await NewService().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingApprover], _actorId);

        Assert.Equal(1, _invariant.WriteCount);
    }

    [Fact]
    public async Task Update_that_changes_nothing_writes_nothing()
    {
        var member = Guid.CreateVersion7();
        _roles.WithActiveUser(BuiltInRoles.Admin, member);

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Admin, [Capability.AdminUsers, Capability.MeetingCreator, Capability.VoiceCollector], _actorId);

        // Answered as success — the system is in the state asked for — but nothing happened, so
        // there is no audit row for a change that was not made and nobody told their capabilities
        // changed.
        Assert.True(result.Succeeded);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
        Assert.Empty(_publisher.Notified);
    }

    [Fact]
    public async Task Update_ignores_duplicates_in_the_request()
    {
        var result = await NewService().UpdateAsync(
            BuiltInRoles.Member,
            [Capability.MeetingApprover, Capability.MeetingApprover],
            _actorId);

        Assert.True(result.Succeeded);

        // Stored once, because the service de-duplicates before writing. That it *must* is
        // enforced by the composite primary key in PostgreSQL, which this fake does not have —
        // RolePermissionRepositoryTests covers the real table.
        Assert.Equal([Capability.MeetingApprover], _roles.CapabilitiesOf(BuiltInRoles.Member));
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
        Assert.Equal([Capability.MeetingCreator, Capability.AdminUsers, Capability.VoiceCollector],
            _roles.CapabilitiesOf(BuiltInRoles.Admin).Order());
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_allows_moving_admin_users_to_a_role_that_has_active_users()
    {
        // The point of the table is that the capability can be re-pointed without a deployment.
        // Legitimate — as long as somebody actually holds the destination role.
        _roles.WithCapabilities(BuiltInRoles.Approver, Capability.MeetingCreator, Capability.AdminUsers);
        _roles.WithActiveUser(BuiltInRoles.Approver, Guid.CreateVersion7());

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
            BuiltInRoles.Approver, [Capability.MeetingCreator], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.Contains("adminUsers", result.Errors!.Keys);
    }

    [Fact]
    public async Task Granting_admin_users_to_a_role_with_users_is_allowed()
    {
        // The safe direction of the same rule: adding a second administering role is fine, and
        // must not be mistaken for the lock-out case by a rule that only counts.
        _roles.WithActiveUser(BuiltInRoles.Approver, Guid.CreateVersion7());

        var result = await NewService().UpdateAsync(
            BuiltInRoles.Approver, [Capability.AdminUsers], _actorId);

        Assert.True(result.Succeeded);
        Assert.Contains(Capability.AdminUsers, _roles.CapabilitiesOf(BuiltInRoles.Approver));
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
            deputy.Id, [Capability.VoiceCollector], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal([Capability.VoiceCollector], _roles.CapabilitiesOf(deputy.Id));
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
            BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingApprover], _actorId);

        Assert.Equal(RolePermissionOutcome.SaveFailed, result.Outcome);
        Assert.Null(result.Matrix);

        // Nothing changed, nothing recorded, nobody told — the diagram's failure branch stops
        // before all three.
        Assert.Equal([Capability.MeetingCreator], _roles.CapabilitiesOf(BuiltInRoles.Member));
        Assert.Empty(_audit.Records);
        Assert.Empty(_publisher.Notified);
    }
}
