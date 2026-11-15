using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Access;

/// <summary>Over the real <see cref="AdministratorInvariantGuard"/>, so the lock and the transaction are seen too.</summary>
public sealed class RolePermissionServiceTests
{
    private readonly FakeRolePermissionRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeCapabilitiesChangePublisher _publisher = new();
    private readonly FakeCalendarShareStopper _calendarShares = new();
    private readonly Guid _actorId = Guid.CreateVersion7();
    private readonly Guid _admin = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    public RolePermissionServiceTests()
    {
        _roles.ActiveUsers[BuiltInRoles.Admin] = [_admin];
        _roles.ActiveUsers[BuiltInRoles.Member] = [_member];
    }

    private RolePermissionService Service() => new(
        _roles, _unitOfWork, _audit, _publisher, new AdministratorInvariantGuard(_unitOfWork, _roles), _calendarShares,
        NullLogger<RolePermissionService>.Instance);

    [Fact]
    public async Task Update_saves_under_the_lock_commits_then_tells_the_role_holders()
    {
        var result = await Service().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(["lock", "replace"], _roles.Calls);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal([AuditActions.RolePermissionsUpdated], _audit.Actions);
        Assert.Equal([_member], _publisher.Told);
    }

    [Fact]
    public async Task Update_refuses_to_leave_no_active_administrator()
    {
        var result = await Service().UpdateAsync(BuiltInRoles.Admin, [], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome);
        Assert.True(result.Errors!.ContainsKey("adminUsers"));
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Told);
    }

    [Fact]
    public async Task A_storage_failure_on_save_answers_save_failed_and_tells_nobody()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await Service().UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator], _actorId);

        Assert.Equal(RolePermissionOutcome.SaveFailed, result.Outcome);
        Assert.Empty(_audit.Actions);
        Assert.Empty(_publisher.Told);
    }

    [Fact]
    public async Task Taking_meeting_creator_away_stops_the_holders_calendar_shares()
    {
        _roles.Matrix[BuiltInRoles.Member] = [Capability.MeetingCreator];

        await Service().UpdateAsync(BuiltInRoles.Member, [], _actorId);

        Assert.Equal([(_member, CalendarShareStopReason.ViewerLostMeetingCreator)], _calendarShares.Stopped);
    }

    [Fact]
    public async Task The_matrix_is_read_without_a_transaction()
    {
        var matrix = await Service().GetMatrixAsync();

        Assert.Equal(2, matrix.Roles.Count);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }
}
