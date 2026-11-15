using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Users;

/// <summary>Over the real <see cref="AdministratorInvariantGuard"/>, so the lock and the transaction are seen too.</summary>
public sealed class UserServiceTests
{
    private const string ValidPassword = "mot-mat-khau-du-dai";

    private readonly FakeAccounts _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRolePermissionRepository _lock = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakePasswordResetTokenStore _resetTokens = new();
    private readonly FakePasswordResetEmailSender _resetEmail = new();
    private readonly FakeCapabilitiesChangePublisher _capabilitiesChanged = new();
    private readonly FakeVoiceIdErasure _voiceId = new();
    private readonly FakeCalendarShareStopper _calendarShares = new();
    private readonly Guid _actorId = Guid.CreateVersion7();

    private UserService Service() => new(
        _users, _unitOfWork, new FakePasswordHasher(), _audit, _refreshTokens,
        new AdministratorInvariantGuard(_unitOfWork, _lock), _resetTokens, _resetEmail, _capabilitiesChanged,
        _voiceId, _calendarShares, NullLogger<UserService>.Instance);

    private static NewUser NewMember(string email = "thanhvien@trustcabinet.local")
        => new("Nguyễn Thành Viên", email, ValidPassword, BuiltInRoles.Member);

    [Fact]
    public async Task Create_saves_once_without_a_transaction_then_audits()
    {
        var result = await Service().CreateAsync(NewMember(), _actorId);

        Assert.True(result.Succeeded);
        Assert.Single(_users.Added);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal(TransactionMode.None, _unitOfWork.Modes[0]);
        Assert.Equal([AuditActions.UserCreated], _audit.Actions);
    }

    [Fact]
    public async Task Create_refuses_an_email_already_taken()
    {
        _users.Add("thanhvien@trustcabinet.local", BuiltInRoles.Member);

        var result = await Service().CreateAsync(NewMember(), _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Create_answers_a_unique_conflict_on_save_as_a_duplicate_email()
    {
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var result = await Service().CreateAsync(NewMember(), _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Create_answers_a_reference_conflict_as_the_role_being_gone()
    {
        _unitOfWork.FailNextSave = new ReferenceConflictException();

        var result = await Service().CreateAsync(NewMember(), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.True(result.Errors!.ContainsKey("roleId"));
    }

    [Fact]
    public async Task Create_lets_any_other_storage_failure_reach_the_caller()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Service().CreateAsync(NewMember(), _actorId));
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Update_answers_a_unique_conflict_by_reloading_the_account_and_committing_nothing_else()
    {
        var user = _users.Add("thanhvien@trustcabinet.local", BuiltInRoles.Member);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var result = await Service().UpdateAsync(
            user.Id, new UserEdit("Tên Mới", "khac@trustcabinet.local", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Equal([user.Id], _users.Discarded);
        Assert.Empty(_audit.Actions);
        Assert.Empty(_refreshTokens.RevokedAll);
    }

    [Fact]
    public async Task Deactivating_runs_under_the_lock_then_ends_the_sessions_after_the_commit()
    {
        _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);
        var user = _users.Add("thanhvien@trustcabinet.local", BuiltInRoles.Member);

        var result = await Service().ChangeStatusAsync(user.Id, isActive: false, _actorId);

        Assert.True(result.Succeeded);
        Assert.False(user.IsActive);
        Assert.Equal(["lock"], _lock.Calls);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal([user.Id], _voiceId.Ended);
        Assert.Equal([user.Id], _refreshTokens.RevokedAll);
        Assert.Equal([user.Id], _resetTokens.Revoked);
    }

    [Fact]
    public async Task Deactivating_the_last_administrator_is_refused()
    {
        var admin = _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);

        var result = await Service().ChangeStatusAsync(admin.Id, isActive: false, _actorId);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.True(admin.IsActive);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_storage_failure_while_deactivating_rolls_back_and_ends_no_session()
    {
        _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);
        var user = _users.Add("thanhvien@trustcabinet.local", BuiltInRoles.Member);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(
            () => Service().ChangeStatusAsync(user.Id, isActive: false, _actorId));

        Assert.Equal(1, _unitOfWork.Rollbacks);
        Assert.Empty(_refreshTokens.RevokedAll);
    }

    [Fact]
    public async Task A_reset_link_is_mailed_and_activated()
    {
        var user = _users.Add("thanhvien@trustcabinet.local", BuiltInRoles.Member);

        var result = await Service().RequestPasswordResetAsync(user.Id, _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(["thanhvien@trustcabinet.local"], _resetEmail.SentTo);
        Assert.Equal(user.Id, Assert.Single(_resetTokens.Live).Value);
        Assert.Equal([AuditActions.UserPasswordResetRequested], _audit.Actions);
        // The account read before the send and again after it, each on its own.
        Assert.Equal([TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Resetting_through_the_link_saves_the_new_password_and_ends_every_session()
    {
        var user = _users.Add("thanhvien@trustcabinet.local", BuiltInRoles.Member);
        await Service().RequestPasswordResetAsync(user.Id, _actorId);
        var token = Assert.Single(_resetTokens.Live).Key;

        var result = await Service().ResetPasswordAsync(token, "mat-khau-moi-du-dai");

        Assert.True(result.Succeeded);
        Assert.Equal("hash:mat-khau-moi-du-dai", user.PasswordHash);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal([user.Id, user.Id], _refreshTokens.RevokedAll);
    }
}
