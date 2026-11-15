using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Access;

public sealed class RoleServiceTests
{
    private readonly FakeRoleRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeAdministratorInvariantGuard _guard = new();
    private readonly Guid _actorId = Guid.CreateVersion7();

    private RoleService Service() => new(_roles, _unitOfWork, _audit, _guard, NullLogger<RoleService>.Instance);

    private Role GivenCustomRole()
    {
        var role = Roles.Custom();
        _roles.Roles.Add(role);
        return role;
    }

    [Fact]
    public async Task Create_saves_through_the_guard_then_audits()
    {
        var result = await Service().CreateAsync(new NewRole("Thư ký phó", null, ["AdminUsers"]), _actorId);

        Assert.True(result.Succeeded);
        Assert.Single(_roles.Added);
        Assert.Equal((1, 1), (_guard.Writes, _unitOfWork.Saves));
        Assert.Equal([AuditActions.RoleCreated], _audit.Actions);
    }

    [Fact]
    public async Task Create_refuses_a_name_already_taken_and_saves_nothing()
    {
        var result = await Service().CreateAsync(new NewRole("thành viên", null, []), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Create_answers_a_unique_conflict_on_save_as_a_duplicate_name()
    {
        // Two requests raced past the name check; the unique index refused the second.
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var result = await Service().CreateAsync(new NewRole("Thư ký phó", null, []), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Create_answers_any_other_storage_failure_as_save_failed()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await Service().CreateAsync(new NewRole("Thư ký phó", null, []), _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Update_answers_a_concurrency_conflict_as_not_found()
    {
        // The role was deleted between reading it and saving the rename.
        var role = GivenCustomRole();
        _unitOfWork.FailNextSave = new ConcurrencyConflictException();

        var result = await Service().UpdateAsync(role.Id, new RoleEdit("Phó thư ký", null), _actorId);

        Assert.Equal(RoleOutcome.NotFound, result.Outcome);
        Assert.Equal(0, _guard.Writes);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Delete_answers_a_reference_conflict_as_in_use_with_the_fresh_count()
    {
        // Somebody was given the role between the count and the delete.
        var role = GivenCustomRole();
        _unitOfWork.FailNextSave = new ReferenceConflictException();

        var result = await Service().DeleteAsync(role.Id, _actorId);

        Assert.Equal((RoleOutcome.InUse, 1), (result.Outcome, result.UserCount));
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Delete_answers_another_storage_failure_as_save_failed()
    {
        var role = GivenCustomRole();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await Service().DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome);
    }

    [Fact]
    public async Task List_reads_without_a_transaction()
    {
        var roles = await Service().ListAsync();

        Assert.Equal(2, roles.Count);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }
}
