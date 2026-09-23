using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Users;
using TrustCabinet.Web.Tests.Authentication;

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// The rules of UC-04 … UC-07, one test per branch of each sequence diagram, plus the cases the
/// diagrams leave implicit (a missing account, an administrator aiming at themselves).
///
/// No database and no HTTP: the branches are the part worth pinning down, and they are decided
/// entirely inside <see cref="UserService"/>.
/// </summary>
public class UserServiceTests
{
    private const string ValidPassword = "MatKhauDaiVaAnToan@2026";

    private readonly FakeUserAccountRepository _users = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeAdministratorInvariantGuard _invariant = new();
    private readonly Guid _actorId = Guid.CreateVersion7();

    private UserService NewService() => new(
        _users,
        new FakePasswordHasher(),
        _audit,
        _refreshTokens,
        _invariant,
        NullLogger<UserService>.Instance);

    private User GivenUser(
        string fullName = "Lê Thành Viên",
        string email = "thanhvien@trustcabinet.local",
        Guid? role = null,
        bool isActive = true)
    {
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = new FakePasswordHasher().Hash(ValidPassword),
            RoleId = role ?? BuiltInRoles.Member,
            IsActive = isActive,
        };

        _users.WithUser(user);
        return user;
    }

    // ---------------------------------------------------------------- UC-04 · View user accounts

    [Fact]
    public async Task List_returns_every_account_sorted_by_name()
    {
        GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);
        GivenUser("Lê Thành Viên", "thanhvien@trustcabinet.local");
        GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", BuiltInRoles.Secretary);

        var accounts = await NewService().ListAsync();

        Assert.Equal(
            ["Lê Thành Viên", "Nguyễn Thư Ký", "Phạm Quản Trị"],
            accounts.Select(a => a.FullName));
    }

    [Fact]
    public async Task List_includes_deactivated_accounts()
    {
        // Deactivated is a state the administrator manages, not a reason to hide the row —
        // UC-07 reactivates from this same list, so a hidden account could never come back.
        GivenUser(isActive: false);

        var accounts = await NewService().ListAsync();

        Assert.False(Assert.Single(accounts).IsActive);
    }

    [Fact]
    public async Task List_never_exposes_the_password_hash()
    {
        GivenUser();

        var account = Assert.Single(await NewService().ListAsync());

        // UserAccount has no hash property at all; what it reports is only that one exists. This
        // test fails the moment somebody adds the hash to the projection "for convenience".
        Assert.True(account.HasPassword);
        Assert.DoesNotContain(
            nameof(User.PasswordHash),
            typeof(UserAccount).GetProperties().Select(p => p.Name));
    }

    // -------------------------------------------------------------- UC-05 · Create user account

    [Fact]
    public async Task Create_stores_an_active_account_and_records_the_audit_entry()
    {
        var result = await NewService().CreateAsync(
            new NewUser("Trần Chủ Toạ", "chutoa@trustcabinet.local", ValidPassword, BuiltInRoles.Chair),
            _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal("chutoa@trustcabinet.local", result.Account!.Email);
        Assert.Equal(BuiltInRoles.Chair, result.Account.RoleId);
        Assert.Equal("Chủ toạ", result.Account.RoleName);
        // "Create active user account" in the diagram — an administrator filling this form wants
        // a working account.
        Assert.True(result.Account.IsActive);

        var recorded = Assert.Single(_audit.Records);
        Assert.Equal(AuditActions.UserCreated, recorded.Action);
        Assert.Equal(AuditActions.UserObjectType, recorded.ObjectType);
        Assert.Equal(result.Account.Id, recorded.ObjectId);
        Assert.Equal(_actorId, recorded.ActorId);
    }

    [Fact]
    public async Task Create_hashes_the_password_instead_of_storing_it()
    {
        // The real hasher, not the reversible fake: what this pins down is that the stored value
        // is the hasher's output rather than the password, and a fake whose "hash" contains the
        // password would make the assertion meaningless.
        var hasher = new TrustCabinet.Infrastructure.Authentication.PasswordHasher();
        var service = new UserService(
            _users, hasher, _audit, _refreshTokens, _invariant,
            NullLogger<UserService>.Instance);

        await service.CreateAsync(
            new NewUser("Trần Chủ Toạ", "chutoa@trustcabinet.local", ValidPassword, BuiltInRoles.Chair),
            _actorId);

        var stored = Assert.Single(_users.Users).PasswordHash;

        Assert.NotNull(stored);
        Assert.DoesNotContain(ValidPassword, stored);
        Assert.True(hasher.Verify(stored, ValidPassword));
    }

    [Fact]
    public async Task Create_never_puts_the_password_in_the_audit_details()
    {
        await NewService().CreateAsync(
            new NewUser("Trần Chủ Toạ", "chutoa@trustcabinet.local", ValidPassword, BuiltInRoles.Chair),
            _actorId);

        // The audit log is read by more people than the users table and is never deleted, so a
        // credential landing in it is permanent.
        var details = Serialize(Assert.Single(_audit.Records).Details);

        Assert.DoesNotContain(ValidPassword, details);
        Assert.DoesNotContain("hashed:", details);
    }

    [Theory]
    // Diagram branch "Invalid user information" — one case per field the form can get wrong.
    [InlineData("", "hople@trustcabinet.local", "fullName")]
    [InlineData("   ", "hople@trustcabinet.local", "fullName")]
    [InlineData("Tên hợp lệ", "", "email")]
    [InlineData("Tên hợp lệ", "khong-phai-email", "email")]
    [InlineData("Tên hợp lệ", "thieu@ten-mien", "email")]
    [InlineData("Tên hợp lệ", "co khoang trang@x.vn", "email")]
    public async Task Create_rejects_invalid_information(string fullName, string email, string field)
    {
        var result = await NewService().CreateAsync(
            new NewUser(fullName, email, ValidPassword, BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        // The field is named, because the diagram's "Display invalid fields" needs to know which.
        Assert.Contains(field, result.Errors!.Keys);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task Create_rejects_a_password_shorter_than_the_minimum()
    {
        var result = await NewService().CreateAsync(
            new NewUser("Tên hợp lệ", "hople@trustcabinet.local", "ngan", BuiltInRoles.Member),
            _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("password", result.Errors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_role_that_does_not_exist()
    {
        // A role is a row an administrator can delete (ADR-0008): a form opened before the delete
        // posts an id that is gone. MSG09 on the field, rather than the foreign key's 500.
        var result = await NewService().CreateAsync(
            new NewUser("Tên hợp lệ", "hople@trustcabinet.local", ValidPassword, Guid.CreateVersion7()),
            _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Vai trò không tồn tại.", result.Errors!["roleId"]);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Create_accepts_a_role_an_administrator_created()
    {
        var deputy = new Role { Name = "Thư ký phó" };
        _users.WithRole(deputy, Capability.MinutesEditor);

        var result = await NewService().CreateAsync(
            new NewUser("Phó Thư Ký", "phothuky@trustcabinet.local", ValidPassword, deputy.Id),
            _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(deputy.Id, result.Account!.RoleId);
        Assert.Equal("Thư ký phó", result.Account.RoleName);
    }

    [Fact]
    public async Task Create_reports_every_invalid_field_at_once()
    {
        // One round trip per mistake would be a miserable form.
        var result = await NewService().CreateAsync(
            new NewUser("", "khong-phai-email", "ngan", BuiltInRoles.Member), _actorId);

        Assert.Equal(
            ["email", "fullName", "password"],
            result.Errors!.Keys.OrderBy(k => k));
    }

    [Fact]
    public async Task Create_rejects_an_email_that_already_exists()
    {
        GivenUser(email: "trungnhau@trustcabinet.local");

        var result = await NewService().CreateAsync(
            new NewUser("Tên khác", "trungnhau@trustcabinet.local", ValidPassword, BuiltInRoles.Member),
            _actorId);

        // MSG08 at the endpoint.
        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Create_rejects_an_email_that_differs_only_in_case()
    {
        // The column is citext, so these are the same account as far as the unique index is
        // concerned. Comparing case-sensitively here would produce a 500 instead of MSG08.
        GivenUser(email: "trungnhau@trustcabinet.local");

        var result = await NewService().CreateAsync(
            new NewUser("Tên khác", "TrungNhau@TrustCabinet.local", ValidPassword, BuiltInRoles.Member),
            _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
    }

    [Fact]
    public async Task Create_trims_surrounding_whitespace()
    {
        var result = await NewService().CreateAsync(
            new NewUser("  Trần Chủ Toạ  ", "  chutoa@trustcabinet.local  ", ValidPassword,
                BuiltInRoles.Chair),
            _actorId);

        Assert.True(result.Succeeded);
        // A pasted address with a trailing space would otherwise become an account nobody can
        // find, and would not collide with the untrimmed one either.
        Assert.Equal("chutoa@trustcabinet.local", result.Account!.Email);
        Assert.Equal("Trần Chủ Toạ", result.Account.FullName);
    }

    [Fact]
    public async Task Create_does_not_audit_when_the_account_was_not_stored()
    {
        // Evidence of a creation that did not happen is worse than no evidence.
        _users.NextSaveThrows = DuplicateEmailException();

        var result = await NewService().CreateAsync(
            new NewUser("Tên hợp lệ", "hople@trustcabinet.local", ValidPassword, BuiltInRoles.Member),
            _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Empty(_audit.Records);
    }

    // -------------------------------------------------------------- UC-06 · Update user account

    [Fact]
    public async Task Get_returns_the_current_information_for_the_edit_form()
    {
        var user = GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", BuiltInRoles.Secretary);

        var result = await NewService().GetAsync(user.Id);

        Assert.True(result.Succeeded);
        Assert.Equal("Nguyễn Thư Ký", result.Account!.FullName);
        Assert.Equal(BuiltInRoles.Secretary, result.Account.RoleId);
    }

    [Fact]
    public async Task Get_reports_not_found_for_an_unknown_account()
    {
        var result = await NewService().GetAsync(Guid.CreateVersion7());

        Assert.Equal(UserOutcome.NotFound, result.Outcome);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task Update_changes_the_account_and_records_what_it_was_before()
    {
        var user = GivenUser("Tên Cũ", "cu@trustcabinet.local", BuiltInRoles.Member);

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit("Tên Mới", "moi@trustcabinet.local", BuiltInRoles.Secretary),
            _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal("Tên Mới", result.Account!.FullName);
        Assert.Equal("moi@trustcabinet.local", result.Account.Email);
        Assert.Equal(BuiltInRoles.Secretary, result.Account.RoleId);

        var recorded = Assert.Single(_audit.Records);
        Assert.Equal(AuditActions.UserUpdated, recorded.Action);
        // "What did it use to be" is most of the value of an update record.
        var details = Serialize(recorded.Details);
        Assert.Contains("Tên Cũ", details);
        Assert.Contains("Tên Mới", details);
    }

    [Fact]
    public async Task Update_lets_an_account_keep_its_own_email()
    {
        // Excluding the account itself from the duplicate check: renaming someone without touching
        // their address must not read as a collision with themselves.
        var user = GivenUser(email: "giunguyen@trustcabinet.local");

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit("Tên Mới", "giunguyen@trustcabinet.local", user.RoleId), _actorId);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Update_rejects_an_email_another_account_already_uses()
    {
        var user = GivenUser(email: "nguoi-mot@trustcabinet.local");
        GivenUser("Người Hai", "nguoi-hai@trustcabinet.local");

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, "nguoi-hai@trustcabinet.local", user.RoleId),
            _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
    }

    [Fact]
    public async Task Update_rejects_invalid_information_without_touching_the_account()
    {
        var user = GivenUser("Tên Cũ", "cu@trustcabinet.local");

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit("", "khong-phai-email", BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Tên Cũ", user.FullName);
        Assert.Equal(0, _users.SaveCount);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_reports_not_found_for_an_unknown_account()
    {
        var result = await NewService().UpdateAsync(
            Guid.CreateVersion7(), new UserEdit("Tên hợp lệ", "hople@trustcabinet.local",
                BuiltInRoles.Member),
            _actorId);

        Assert.Equal(UserOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_discards_the_rejected_values_when_the_save_is_refused()
    {
        // The unique index rejects the row, but the entity is still tracked and still holds what
        // was assigned to it. The audit write runs on the same DbContext, so its SaveChanges
        // would flush those values — a refused update that lands anyway, one call later.
        var user = GivenUser("Tên Cũ", "cu@trustcabinet.local", BuiltInRoles.Member);
        _users.NextSaveThrows = DuplicateEmailException();

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit("Tên Mới", "moi@trustcabinet.local", BuiltInRoles.Secretary),
            _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Contains(user.Id, _users.Discarded);
        // Back to what the database holds, not what the caller asked for.
        Assert.Equal("Tên Cũ", user.FullName);
        Assert.Equal("cu@trustcabinet.local", user.Email);
        Assert.Equal(BuiltInRoles.Member, user.RoleId);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_ends_the_sessions_when_the_role_changes()
    {
        // Capabilities travel inside the access token, so without this the person keeps the rights
        // they had until it expires.
        var user = GivenUser(role: BuiltInRoles.Member);
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, user.Email, BuiltInRoles.Secretary), _actorId);

        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task Update_keeps_the_sessions_when_the_role_is_unchanged()
    {
        // Correcting a typo in someone's name should not sign them out.
        var user = GivenUser(role: BuiltInRoles.Secretary);
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        await NewService().UpdateAsync(
            user.Id, new UserEdit("Tên Đã Sửa", user.Email, BuiltInRoles.Secretary), _actorId);

        Assert.Single(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task Update_refuses_to_let_an_administrator_drop_their_own_admin_role()
    {
        // The same lockout as deactivating yourself: with one organisation there may be no second
        // administrator to undo it.
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);

        var result = await NewService().UpdateAsync(
            admin.Id, new UserEdit(admin.FullName, admin.Email, BuiltInRoles.Member), admin.Id);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.Equal(BuiltInRoles.Admin, admin.RoleId);
    }

    [Fact]
    public async Task Update_lets_an_administrator_edit_their_own_name()
    {
        // Only the role is protected — an administrator is not locked out of their own profile.
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);

        var result = await NewService().UpdateAsync(
            admin.Id, new UserEdit("Phạm Quản Trị (mới)", admin.Email, BuiltInRoles.Admin), admin.Id);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Update_lets_one_administrator_demote_another_while_one_remains()
    {
        // Two administrators, so demoting one still leaves somebody able to administer.
        GivenUser("Quản Trị Một", "quantri1@trustcabinet.local", BuiltInRoles.Admin);
        var other = GivenUser("Quản Trị Hai", "quantri2@trustcabinet.local", BuiltInRoles.Admin);

        var result = await NewService().UpdateAsync(
            other.Id, new UserEdit(other.FullName, other.Email, BuiltInRoles.Member), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(BuiltInRoles.Member, other.RoleId);
    }

    [Fact]
    public async Task Update_refuses_to_demote_the_last_administrator()
    {
        // Nobody would be left able to administer, and recovering needs a database console.
        // Not the same rule as "you cannot demote yourself": this fires whoever is asking.
        var only = GivenUser("Quản Trị Duy Nhất", "quantri@trustcabinet.local", BuiltInRoles.Admin);

        var result = await NewService().UpdateAsync(
            only.Id, new UserEdit(only.FullName, only.Email, BuiltInRoles.Member), _actorId);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.Equal(BuiltInRoles.Admin, only.RoleId);
    }

    [Fact]
    public async Task Update_lets_the_last_administrator_keep_administering_under_another_role()
    {
        // The rule is about the capability, not the role name: moving to a role that also carries
        // AdminUsers takes nothing away, and must not be refused.
        var only = GivenUser("Quản Trị Duy Nhất", "quantri@trustcabinet.local", BuiltInRoles.Admin);
        _users.WithCapabilities(BuiltInRoles.Chair, Capability.AdminUsers);

        var result = await NewService().UpdateAsync(
            only.Id, new UserEdit(only.FullName, only.Email, BuiltInRoles.Chair), _actorId);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task An_inactive_account_can_still_be_moved_to_another_role()
    {
        // An inactive account administers nothing: it cannot sign in, and the "is anyone left?"
        // count only counts active accounts. So no change to it can remove the last administrator.
        //
        // The rule used to answer "that would leave no active administrator" here — true, but
        // already true before the change, and the change did not cause it. The sole administrator,
        // once deactivated, was stuck in the Admin role permanently: a dead end rather than a
        // safeguard.
        var retired = GivenUser(
            "Quản Trị Đã Nghỉ", "danghi@trustcabinet.local", BuiltInRoles.Admin, isActive: false);

        var result = await NewService().UpdateAsync(
            retired.Id, new UserEdit(retired.FullName, retired.Email, BuiltInRoles.Member), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(BuiltInRoles.Member, retired.RoleId);
    }

    [Fact]
    public async Task Changing_an_email_ends_the_sessions_too()
    {
        // The access token carries an email claim, so /api/auth/me would keep answering with the
        // old address for as long as the token lived. Only a role change used to revoke.
        var user = GivenUser("Lê Thành Viên", "cu@trustcabinet.local");
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, "moi@trustcabinet.local", user.RoleId), _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task Renaming_somebody_leaves_their_session_alone()
    {
        // The other half of the rule: a display name is not in the token and changes nothing about
        // what the account may do, so signing them out would be gratuitous.
        var user = GivenUser("Lê Thành Viên", "thanhvien@trustcabinet.local");
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit("Lê Thành Viên B", user.Email, user.RoleId), _actorId);

        Assert.True(result.Succeeded);
        Assert.Contains("rt-cua-ho", _refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task Both_writes_that_touch_the_invariant_run_under_the_shared_lock()
    {
        // The lock-out hole the review found: UserService and RolePermissionService both decide
        // "is anyone still able to administer?", and before this they took different locks — so
        // deactivating the last administrator and removing AdminUsers from the last role holding
        // it could commit at the same time, each seeing the other's administrator still there.
        //
        // What the real lock does is proved in AdministratorInvariantGuardTests, against a real
        // PostgreSQL. What this pins is that these two paths go through it at all.
        var target = GivenUser("Lê Thành Viên", "thanhvien@trustcabinet.local");
        var service = NewService();

        await service.UpdateAsync(
            target.Id, new UserEdit(target.FullName, target.Email, BuiltInRoles.Chair), _actorId);
        await service.ChangeStatusAsync(target.Id, false, _actorId);

        Assert.Equal(2, _invariant.WriteCount);
    }

    [Fact]
    public async Task Sessions_are_ended_after_the_transaction_commits_not_inside_it()
    {
        // Redis is a separate system. Revoking while the invariant lock is held would block every
        // other administrative write for a network round trip, and an outage there would roll back
        // an edit the database had already accepted.
        var user = GivenUser("Lê Thành Viên", "thanhvien@trustcabinet.local");
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        _refreshTokens.OnRevokeAll = () =>
            Assert.False(_invariant.IsInsideWrite, "Sessions were revoked while the lock was held.");

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, user.Email, BuiltInRoles.Chair), _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task A_change_survives_the_session_store_being_down()
    {
        // The write is committed and correct. Failing the request now would tell the administrator
        // their change did not happen and they would repeat it; what is actually lost is the early
        // end of one session, which expires by itself within the access token's lifetime.
        var user = GivenUser("Lê Thành Viên", "thanhvien@trustcabinet.local");
        _refreshTokens.OnRevokeAll = () => throw new InvalidOperationException("Redis khong phan hoi");

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, user.Email, BuiltInRoles.Chair), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(BuiltInRoles.Chair, user.RoleId);
    }

    // ------------------------------------------- UC-07 · Deactivate / reactivate user account

    [Fact]
    public async Task Deactivate_sets_the_status_ends_the_sessions_and_audits()
    {
        var user = GivenUser(isActive: true);
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        var result = await NewService().ChangeStatusAsync(user.Id, isActive: false, _actorId);

        Assert.True(result.Succeeded);
        Assert.False(result.Account!.IsActive);
        // The <c>:Token Service</c> step: a row marked inactive whose tokens still work is not
        // deactivated in any sense that matters.
        Assert.Empty(_refreshTokens.ActiveTokens);
        Assert.Equal(AuditActions.UserDeactivated, Assert.Single(_audit.Records).Action);
    }

    [Fact]
    public async Task Deactivate_never_deletes_the_row()
    {
        // SCRUM-13: someone who left still chairs last quarter's meeting, and their approvals stay
        // attributable.
        var user = GivenUser();

        await NewService().ChangeStatusAsync(user.Id, isActive: false, _actorId);

        Assert.Contains(_users.Users, u => u.Id == user.Id);
    }

    [Fact]
    public async Task Reactivate_sets_the_status_and_audits_without_touching_sessions()
    {
        var user = GivenUser(isActive: false);
        // Somebody else's live session, to prove reactivating does not sweep tokens.
        await _refreshTokens.StoreAsync("rt-nguoi-khac", Guid.CreateVersion7(), TimeSpan.FromDays(7));

        var result = await NewService().ChangeStatusAsync(user.Id, isActive: true, _actorId);

        Assert.True(result.Succeeded);
        Assert.True(result.Account!.IsActive);
        Assert.Equal(AuditActions.UserReactivated, Assert.Single(_audit.Records).Action);
        Assert.Single(_refreshTokens.ActiveTokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Status_change_is_refused_when_the_account_is_already_in_that_state(bool isActive)
    {
        // Two administrators on the same list, or a double-submitted form. Refused rather than
        // treated as success, so the audit log does not record one deactivation twice.
        var user = GivenUser(isActive: isActive);

        var result = await NewService().ChangeStatusAsync(user.Id, isActive, _actorId);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.Empty(_audit.Records);
        Assert.Equal(0, _users.SaveCount);
    }

    [Fact]
    public async Task Status_change_is_refused_when_an_administrator_aims_at_their_own_account()
    {
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);

        var result = await NewService().ChangeStatusAsync(admin.Id, isActive: false, admin.Id);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        // Still usable: recovering from this would otherwise need a database console.
        Assert.True(admin.IsActive);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Two_administrators_cannot_deactivate_each_other_into_a_locked_out_system()
    {
        // The lockout this rule exists for, and the one the self-check alone does not stop.
        // B deactivates A; A's access token still carries AdminUsers until it expires, so A's
        // next request is authorized and would have deactivated B — leaving nobody able to sign
        // in and administer, recoverable only from a database console.
        var a = GivenUser("Quản Trị A", "a@trustcabinet.local", BuiltInRoles.Admin);
        var b = GivenUser("Quản Trị B", "b@trustcabinet.local", BuiltInRoles.Admin);
        var service = NewService();

        var first = await service.ChangeStatusAsync(a.Id, isActive: false, b.Id);
        var second = await service.ChangeStatusAsync(b.Id, isActive: false, a.Id);

        Assert.True(first.Succeeded);
        // The second one is refused: A is now inactive, so B is the last one left.
        Assert.Equal(UserOutcome.StatusChangeNotAllowed, second.Outcome);
        Assert.True(b.IsActive);
        Assert.Equal(1, _users.Users.Count(u => u.RoleId == BuiltInRoles.Admin && u.IsActive));
    }

    [Fact]
    public async Task Deactivating_the_last_administrator_is_refused()
    {
        var only = GivenUser("Quản Trị Duy Nhất", "quantri@trustcabinet.local", BuiltInRoles.Admin);

        var result = await NewService().ChangeStatusAsync(only.Id, isActive: false, _actorId);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.True(only.IsActive);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task A_custom_role_holding_admin_users_counts_toward_the_last_administrator_rule()
    {
        // SCRUM-63's regression case: the invariant counts accounts whose role carries AdminUsers,
        // whichever role that is. Somebody active in a role an administrator created, with
        // AdminUsers granted, is an administrator — so the built-in Admin may be deactivated.
        var board = new Role { Name = "Ban quản trị" };
        _users.WithRole(board, Capability.AdminUsers);

        var oldAdmin = GivenUser("Quản Trị Cũ", "quantricu@trustcabinet.local", BuiltInRoles.Admin);
        var newAdmin = GivenUser("Quản Trị Mới", "quantrimoi@trustcabinet.local", board.Id);

        var result = await NewService().ChangeStatusAsync(oldAdmin.Id, isActive: false, _actorId);

        Assert.True(result.Succeeded);

        // And now the member of the custom role is the last one standing.
        var last = await NewService().ChangeStatusAsync(newAdmin.Id, isActive: false, _actorId);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, last.Outcome);
        Assert.True(newAdmin.IsActive);
    }

    [Fact]
    public async Task Moving_the_last_administrator_into_a_custom_role_without_admin_users_is_refused()
    {
        var plain = new Role { Name = "Thư ký phó" };
        _users.WithRole(plain, Capability.MinutesEditor);
        var only = GivenUser("Quản Trị Duy Nhất", "quantri@trustcabinet.local", BuiltInRoles.Admin);

        var result = await NewService().UpdateAsync(
            only.Id, new UserEdit(only.FullName, only.Email, plain.Id), _actorId);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.Equal(BuiltInRoles.Admin, only.RoleId);
    }

    [Fact]
    public async Task Update_answers_with_the_new_role_name_and_records_it()
    {
        var user = GivenUser(role: BuiltInRoles.Member);

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, user.Email, BuiltInRoles.Secretary), _actorId);

        Assert.Equal("Thư ký", result.Account!.RoleName);

        // The audit keeps both the id and the name: a role can be renamed or deleted later.
        var details = System.Text.Json.JsonSerializer.Serialize(Assert.Single(_audit.Records).Details);
        using var parsed = System.Text.Json.JsonDocument.Parse(details);
        Assert.Equal(
            "Thành viên", parsed.RootElement.GetProperty("Before").GetProperty("Role").GetString());
        Assert.Equal(
            "Thư ký", parsed.RootElement.GetProperty("After").GetProperty("Role").GetString());
        Assert.Equal(
            BuiltInRoles.Secretary,
            parsed.RootElement.GetProperty("After").GetProperty("RoleId").GetGuid());
    }

    [Fact]
    public async Task Update_answers_MSG09_for_a_role_that_has_been_deleted()
    {
        var user = GivenUser();

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, user.Email, Guid.CreateVersion7()), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("roleId", result.Errors!.Keys);
        Assert.Equal(BuiltInRoles.Member, user.RoleId);
    }

    [Fact]
    public async Task Deactivating_a_non_administrator_is_allowed_even_when_one_admin_exists()
    {
        // The rule guards the capability, not every account: an ordinary member carries no
        // AdminUsers, so deactivating them can never remove the last administrator.
        var member = GivenUser(role: BuiltInRoles.Member);

        var result = await NewService().ChangeStatusAsync(member.Id, isActive: false, _actorId);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Status_change_reports_not_found_for_an_unknown_account()
    {
        var result = await NewService().ChangeStatusAsync(
            Guid.CreateVersion7(), isActive: false, _actorId);

        Assert.Equal(UserOutcome.NotFound, result.Outcome);
    }

    /// <summary>
    /// The audit details as <c>AuditService</c> would write them — same encoder, so a test
    /// asserting on Vietnamese text matches what actually lands in the column rather than
    /// \u escapes.
    /// </summary>
    private static string Serialize(object? details)
        => System.Text.Json.JsonSerializer.Serialize(
            details,
            new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                    System.Text.Unicode.UnicodeRanges.All),
            });

    /// <summary>
    /// What EF Core throws when PostgreSQL's unique index rejects a row: a DbUpdateException
    /// wrapping a PostgresException with SQLSTATE 23505. Built here so the duplicate-email retry
    /// path can be tested without a database.
    /// </summary>
    private static Exception DuplicateEmailException()
        => new Microsoft.EntityFrameworkCore.DbUpdateException(
            "duplicate key value violates unique constraint",
            new Npgsql.PostgresException(
                messageText: "duplicate key value violates unique constraint \"ix_users_email\"",
                severity: "ERROR",
                invariantSeverity: "ERROR",
                sqlState: "23505"));
}
