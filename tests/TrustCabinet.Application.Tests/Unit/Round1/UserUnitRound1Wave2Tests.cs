using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 causation tests for FE-02 (Users), wave 2: the UTC left over after wave 1.
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// Tester: Phạm Gia Khánh.
public sealed class UserUnitRound1Wave2Tests
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

    private UserService ServiceWithSender(IPasswordResetEmailSender sender) => new(
        _users, _unitOfWork, new FakePasswordHasher(), _audit, _refreshTokens,
        new AdministratorInvariantGuard(_unitOfWork, _lock), _resetTokens, sender, _capabilitiesChanged,
        _voiceId, _calendarShares, NullLogger<UserService>.Instance);

    private static NewUser NewMember(string email = "thanhvien@trustcabinet.local")
        => new("Nguyễn Thành Viên", email, ValidPassword, BuiltInRoles.Member);

    // The reset email goes out while another administrator changes the account: the mutation
    // runs inside the send, exactly where the service re-reads the account afterwards.
    private sealed class DuringSendResetEmail(Action onSend) : IPasswordResetEmailSender
    {
        public List<string> SentTo { get; } = [];
        public Task<bool> SendAsync(string toAddress, string fullName, string resetToken, TimeSpan validFor,
            CancellationToken ct = default)
        {
            SentTo.Add(toAddress);
            onSend();
            return Task.FromResult(true);
        }
    }

    [Fact(DisplayName = "UserService.ChangeStatusAsync/UTC003 mutual deactivation of the last two administrators is refused")]
    [Trait("UTC", "UserService.ChangeStatusAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ChangeStatusAsync_UTC003()
    {
        var first = _users.Add("a@x.vn", BuiltInRoles.Admin);
        var second = _users.Add("b@x.vn", BuiltInRoles.Admin);
        var deactivated = await Service().ChangeStatusAsync(first.Id, false, second.Id);

        var result = await Service().ChangeStatusAsync(second.Id, false, first.Id);

        Assert.Equal(UserOutcome.Success, deactivated.Outcome);
        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.True(_users.Users.Single(u => u.Id == second.Id).IsActive);
    }

    [Fact(DisplayName = "UserService.ChangeStatusAsync/UTC004 reactivating the inactive administrator is never blocked")]
    [Trait("UTC", "UserService.ChangeStatusAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ChangeStatusAsync_UTC004()
    {
        _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);
        var target = _users.Add("second@trustcabinet.local", BuiltInRoles.Admin, isActive: false);

        var result = await Service().ChangeStatusAsync(target.Id, true, _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.True(result.Account!.IsActive);
        Assert.Equal([AuditActions.UserReactivated], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.ChangeStatusAsync/UTC005 session ending failure after the commit is logged, not raised")]
    [Trait("UTC", "UserService.ChangeStatusAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task ChangeStatusAsync_UTC005()
    {
        _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);
        var target = _users.Add("thanhvien@trustcabinet.local", BuiltInRoles.Member);
        _refreshTokens.OnRevokeAll = () => throw new InvalidOperationException("Redis unavailable");

        var result = await Service().ChangeStatusAsync(target.Id, false, _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal([AuditActions.UserDeactivated], _audit.Actions);
        Assert.Equal([target.Id], _voiceId.Ended);
    }

    [Fact(DisplayName = "UserService.CreateAsync/UTC002 surrounding spaces are trimmed before saving")]
    [Trait("UTC", "UserService.CreateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task CreateAsync_UTC002()
    {
        var result = await Service().CreateAsync(
            new NewUser("  Nguyễn Thư Ký  ", "  thuky@trustcabinet.local  ", ValidPassword, BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal("thuky@trustcabinet.local", result.Account!.Email);
        Assert.Equal("Nguyễn Thư Ký", result.Account!.FullName);
    }

    [Fact(DisplayName = "UserService.CreateAsync/UTC003 empty full name names the fullName field")]
    [Trait("UTC", "UserService.CreateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task CreateAsync_UTC003()
    {
        var result = await Service().CreateAsync(
            new NewUser("", "thuky@trustcabinet.local", ValidPassword, BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Họ tên không được để trống.", result.Errors!["fullName"]);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "UserService.CreateAsync/UTC004 two-hundred-character name is within the limit")]
    [Trait("UTC", "UserService.CreateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task CreateAsync_UTC004()
    {
        var name = new string('N', 200);

        var result = await Service().CreateAsync(
            new NewUser(name, "thuky@trustcabinet.local", ValidPassword, BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal(name, result.Account!.FullName);
        Assert.Equal([AuditActions.UserCreated], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.CreateAsync/UTC005 two-hundred-and-one-character name names the fullName field")]
    [Trait("UTC", "UserService.CreateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task CreateAsync_UTC005()
    {
        var result = await Service().CreateAsync(
            new NewUser(new string('N', 201), "thuky@trustcabinet.local", ValidPassword, BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Họ tên không được vượt quá 200 ký tự.", result.Errors!["fullName"]);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "UserService.RequestPasswordResetAsync/UTC003 undeliverable email keeps the earlier link working")]
    [Trait("UTC", "UserService.RequestPasswordResetAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RequestPasswordResetAsync_UTC003()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member);
        await _resetTokens.ActivateAsync(user.Id, "T-old", TimeSpan.FromMinutes(60));
        _resetEmail.Delivers = false;

        var result = await Service().RequestPasswordResetAsync(user.Id, _actorId);

        Assert.Equal(UserOutcome.PasswordResetEmailFailed, result.Outcome);
        Assert.Equal(user.Id, _resetTokens.Live["T-old"]);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "UserService.RequestPasswordResetAsync/UTC004 email changed during the send revokes the new link")]
    [Trait("UTC", "UserService.RequestPasswordResetAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task RequestPasswordResetAsync_UTC004()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member);
        var sender = new DuringSendResetEmail(() => user.Email = "moi@x.vn");

        var result = await ServiceWithSender(sender).RequestPasswordResetAsync(user.Id, _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal([user.Id], _resetTokens.Revoked);
        Assert.Equal(["lan@x.vn"], sender.SentTo);
    }

    [Fact(DisplayName = "UserService.RequestPasswordResetAsync/UTC005 deactivation during the send revokes the new link")]
    [Trait("UTC", "UserService.RequestPasswordResetAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task RequestPasswordResetAsync_UTC005()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member);
        var sender = new DuringSendResetEmail(() => user.IsActive = false);

        var result = await ServiceWithSender(sender).RequestPasswordResetAsync(user.Id, _actorId);

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal([user.Id], _resetTokens.Revoked);
        Assert.Equal(["lan@x.vn"], sender.SentTo);
    }

    [Fact(DisplayName = "UserService.ResetPasswordAsync/UTC003 dead link is checked before the short password")]
    [Trait("UTC", "UserService.ResetPasswordAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ResetPasswordAsync_UTC003()
    {
        var result = await Service().ResetPasswordAsync("T-old", "ngan");

        Assert.Equal(UserOutcome.PasswordResetLinkInvalid, result.Outcome);
        Assert.Empty(_audit.Actions);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "UserService.ResetPasswordAsync/UTC004 eleven-character password names the newPassword field")]
    [Trait("UTC", "UserService.ResetPasswordAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ResetPasswordAsync_UTC004()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member);
        await _resetTokens.ActivateAsync(user.Id, "T-abc", TimeSpan.FromMinutes(60));

        var result = await Service().ResetPasswordAsync("T-abc", "MatKhau@12");

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Mật khẩu phải có ít nhất 12 ký tự.", result.Errors!["newPassword"]);
        Assert.True(_resetTokens.Live.ContainsKey("T-abc"));
    }

    [Fact(DisplayName = "UserService.ResetPasswordAsync/UTC005 twelve-character password is the minimum")]
    [Trait("UTC", "UserService.ResetPasswordAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task ResetPasswordAsync_UTC005()
    {
        var user = _users.Add("lan@x.vn", BuiltInRoles.Member);
        await _resetTokens.ActivateAsync(user.Id, "T-abc", TimeSpan.FromMinutes(60));

        var result = await Service().ResetPasswordAsync("T-abc", "MatKhau@123!");

        Assert.Equal(UserOutcome.Success, result.Outcome);
        Assert.Equal("hash:MatKhau@123!", _users.Users.Single(u => u.Id == user.Id).PasswordHash);
        Assert.Equal([AuditActions.UserPasswordReset], _audit.Actions);
    }

    [Fact(DisplayName = "UserService.UpdateAsync/UTC002 empty full name names the fullName field")]
    [Trait("UTC", "UserService.UpdateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task UpdateAsync_UTC002()
    {
        var user = _users.Add("a@x.vn", BuiltInRoles.Member);

        var result = await Service().UpdateAsync(
            user.Id, new UserEdit("", "a@x.vn", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Họ tên không được để trống.", result.Errors!["fullName"]);
        Assert.Equal("Người Dùng", _users.Users.Single(u => u.Id == user.Id).FullName);
    }

    [Fact(DisplayName = "UserService.UpdateAsync/UTC003 two-hundred-and-one-character name names the fullName field")]
    [Trait("UTC", "UserService.UpdateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task UpdateAsync_UTC003()
    {
        var user = _users.Add("a@x.vn", BuiltInRoles.Member);

        var result = await Service().UpdateAsync(
            user.Id, new UserEdit(new string('N', 201), "a@x.vn", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Họ tên không được vượt quá 200 ký tự.", result.Errors!["fullName"]);
        Assert.Equal("Người Dùng", _users.Users.Single(u => u.Id == user.Id).FullName);
    }

    [Fact(DisplayName = "UserService.UpdateAsync/UTC005 demoting the only administrator is refused")]
    [Trait("UTC", "UserService.UpdateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task UpdateAsync_UTC005()
    {
        var admin = _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);

        var result = await Service().UpdateAsync(
            admin.Id, new UserEdit("Quản Trị Viên", "admin@trustcabinet.local", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.Equal(BuiltInRoles.Admin, _users.Users.Single(u => u.Id == admin.Id).RoleId);
        Assert.Empty(_audit.Actions);
    }
}
