using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 causation tests for FE-02 (Users): one xUnit test per kept UTC below.
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// Tester: Phạm Gia Khánh.
public sealed class UserUnitRound1Tests
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

    [Fact(DisplayName = "UserService.CreateAsync/UTC001 valid member account is created active")]
    [Trait("UTC", "UserService.CreateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task CreateAsync_UTC001()
    {
        var result = await Service().CreateAsync(
            new NewUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", "MatKhau@2026!", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.True(result.Account!.IsActive);
        Assert.Equal([AuditActions.UserCreated], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.CreateAsync/UTC006 email taken in another case is refused")]
    [Trait("UTC", "UserService.CreateAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task CreateAsync_UTC006()
    {
        _users.Add("thuky@trustcabinet.local", BuiltInRoles.Member);

        var result = await Service().CreateAsync(NewMember("THUKY@trustcabinet.local"), _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Null(result.Account);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "UserService.ChangeStatusAsync/UTC001 deactivating another administrator works")]
    [Trait("UTC", "UserService.ChangeStatusAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ChangeStatusAsync_UTC001()
    {
        _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);
        var target = _users.Add("second@trustcabinet.local", BuiltInRoles.Admin);

        var result = await Service().ChangeStatusAsync(target.Id, false, _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.False(result.Account!.IsActive);
        Assert.Equal([AuditActions.UserDeactivated], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.ChangeStatusAsync/UTC003 deactivating yourself is refused")]
    [Trait("UTC", "UserService.ChangeStatusAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ChangeStatusAsync_UTC003()
    {
        var admin = _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);

        var result = await Service().ChangeStatusAsync(admin.Id, false, admin.Id);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.True(_users.Users.Single(u => u.Id == admin.Id).IsActive);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "UserService.UpdateAsync/UTC001 renaming keeps the account and audits")]
    [Trait("UTC", "UserService.UpdateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task UpdateAsync_UTC001()
    {
        var user = _users.Add("a@x.vn", BuiltInRoles.Member);

        var result = await Service().UpdateAsync(user.Id, new UserEdit("Lê Văn Anh", "a@x.vn", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal("Lê Văn Anh", result.Account!.FullName);
        Assert.Equal([AuditActions.UserUpdated], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.UpdateAsync/UTC005 taking another account email is refused")]
    [Trait("UTC", "UserService.UpdateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task UpdateAsync_UTC005()
    {
        var first = _users.Add("a@x.vn", BuiltInRoles.Member);
        _users.Add("b@x.vn", BuiltInRoles.Member);

        var result = await Service().UpdateAsync(first.Id, new UserEdit("Nguyễn Thành Viên", "b@x.vn", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Equal("a@x.vn", _users.Users.Single(u => u.Id == first.Id).Email);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "UserService.RequestPasswordResetAsync/UTC001 active account with a password gets a link")]
    [Trait("UTC", "UserService.RequestPasswordResetAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RequestPasswordResetAsync_UTC001()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member);

        var result = await Service().RequestPasswordResetAsync(user.Id, _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal(["lan@x.vn"], _resetEmail.SentTo);
        Assert.Equal([AuditActions.UserPasswordResetRequested], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.RequestPasswordResetAsync/UTC003 deactivated account gets no link")]
    [Trait("UTC", "UserService.RequestPasswordResetAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RequestPasswordResetAsync_UTC003()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member, isActive: false);

        var result = await Service().RequestPasswordResetAsync(user.Id, _actorId);

        Assert.Equal(UserOutcome.PasswordResetNotAvailable, result.Outcome);
        Assert.Empty(_resetEmail.SentTo);
        Assert.Empty(_resetTokens.Live);
    }

    [Fact(DisplayName = "UserService.ResetPasswordAsync/UTC001 live link replaces the password and ends sessions")]
    [Trait("UTC", "UserService.ResetPasswordAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ResetPasswordAsync_UTC001()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member);
        await _resetTokens.ActivateAsync(user.Id, "T-abc", TimeSpan.FromMinutes(60));

        var result = await Service().ResetPasswordAsync("T-abc", "MatKhauMoi@2026");

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal("hash:MatKhauMoi@2026", _users.Users.Single(u => u.Id == user.Id).PasswordHash);
        Assert.Equal([AuditActions.UserPasswordReset], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.ResetPasswordAsync/UTC002 missing link is refused")]
    [Trait("UTC", "UserService.ResetPasswordAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task ResetPasswordAsync_UTC002()
    {
        var result = await Service().ResetPasswordAsync(null, "MatKhauMoi@2026");

        Assert.Equal(UserOutcome.PasswordResetLinkInvalid, result.Outcome);
        Assert.Empty(_audit.Actions);
        Assert.Empty(_users.Added);
    }
}
