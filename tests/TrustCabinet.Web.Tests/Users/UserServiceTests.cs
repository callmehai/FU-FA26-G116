using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly Guid _actorId = Guid.CreateVersion7();

    private UserService NewService() => new(
        _users,
        new FakePasswordHasher(),
        _audit,
        _refreshTokens,
        NullLogger<UserService>.Instance);

    private User GivenUser(
        string fullName = "Lê Thành Viên",
        string email = "thanhvien@trustcabinet.local",
        SystemRole role = SystemRole.Member,
        bool isActive = true)
    {
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = new FakePasswordHasher().Hash(ValidPassword),
            Role = role,
            IsActive = isActive,
        };

        _users.WithUser(user);
        return user;
    }

    // ---------------------------------------------------------------- UC-04 · View user accounts

    [Fact]
    public async Task List_returns_every_account_sorted_by_name()
    {
        GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", SystemRole.Admin);
        GivenUser("Lê Thành Viên", "thanhvien@trustcabinet.local");
        GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", SystemRole.Secretary);

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
            new NewUser("Trần Chủ Toạ", "chutoa@trustcabinet.local", ValidPassword, SystemRole.Chair),
            _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal("chutoa@trustcabinet.local", result.Account!.Email);
        Assert.Equal(SystemRole.Chair, result.Account.Role);
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
            _users, hasher, _audit, _refreshTokens, NullLogger<UserService>.Instance);

        await service.CreateAsync(
            new NewUser("Trần Chủ Toạ", "chutoa@trustcabinet.local", ValidPassword, SystemRole.Chair),
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
            new NewUser("Trần Chủ Toạ", "chutoa@trustcabinet.local", ValidPassword, SystemRole.Chair),
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
            new NewUser(fullName, email, ValidPassword, SystemRole.Member), _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        // The field is named, because the diagram's "Display invalid fields" needs to know which.
        Assert.Contains(field, result.Errors!.Keys);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task Create_rejects_a_password_shorter_than_the_minimum()
    {
        var result = await NewService().CreateAsync(
            new NewUser("Tên hợp lệ", "hople@trustcabinet.local", "ngan", SystemRole.Member),
            _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("password", result.Errors!.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_role_outside_the_enum()
    {
        // A caller posting a number the enum has no name for. Without the check it would be stored
        // and read back as a role nothing matches.
        var result = await NewService().CreateAsync(
            new NewUser("Tên hợp lệ", "hople@trustcabinet.local", ValidPassword, (SystemRole)99),
            _actorId);

        Assert.Equal(UserOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("role", result.Errors!.Keys);
    }

    [Fact]
    public async Task Create_reports_every_invalid_field_at_once()
    {
        // One round trip per mistake would be a miserable form.
        var result = await NewService().CreateAsync(
            new NewUser("", "khong-phai-email", "ngan", SystemRole.Member), _actorId);

        Assert.Equal(
            ["email", "fullName", "password"],
            result.Errors!.Keys.OrderBy(k => k));
    }

    [Fact]
    public async Task Create_rejects_an_email_that_already_exists()
    {
        GivenUser(email: "trungnhau@trustcabinet.local");

        var result = await NewService().CreateAsync(
            new NewUser("Tên khác", "trungnhau@trustcabinet.local", ValidPassword, SystemRole.Member),
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
            new NewUser("Tên khác", "TrungNhau@TrustCabinet.local", ValidPassword, SystemRole.Member),
            _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
    }

    [Fact]
    public async Task Create_trims_surrounding_whitespace()
    {
        var result = await NewService().CreateAsync(
            new NewUser("  Trần Chủ Toạ  ", "  chutoa@trustcabinet.local  ", ValidPassword,
                SystemRole.Chair),
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
            new NewUser("Tên hợp lệ", "hople@trustcabinet.local", ValidPassword, SystemRole.Member),
            _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
        Assert.Empty(_audit.Records);
    }

    // -------------------------------------------------------------- UC-06 · Update user account

    [Fact]
    public async Task Get_returns_the_current_information_for_the_edit_form()
    {
        var user = GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", SystemRole.Secretary);

        var result = await NewService().GetAsync(user.Id);

        Assert.True(result.Succeeded);
        Assert.Equal("Nguyễn Thư Ký", result.Account!.FullName);
        Assert.Equal(SystemRole.Secretary, result.Account.Role);
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
        var user = GivenUser("Tên Cũ", "cu@trustcabinet.local", SystemRole.Member);

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit("Tên Mới", "moi@trustcabinet.local", SystemRole.Secretary),
            _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal("Tên Mới", result.Account!.FullName);
        Assert.Equal("moi@trustcabinet.local", result.Account.Email);
        Assert.Equal(SystemRole.Secretary, result.Account.Role);

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
            user.Id, new UserEdit("Tên Mới", "giunguyen@trustcabinet.local", user.Role), _actorId);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Update_rejects_an_email_another_account_already_uses()
    {
        var user = GivenUser(email: "nguoi-mot@trustcabinet.local");
        GivenUser("Người Hai", "nguoi-hai@trustcabinet.local");

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, "nguoi-hai@trustcabinet.local", user.Role),
            _actorId);

        Assert.Equal(UserOutcome.DuplicateEmail, result.Outcome);
    }

    [Fact]
    public async Task Update_rejects_invalid_information_without_touching_the_account()
    {
        var user = GivenUser("Tên Cũ", "cu@trustcabinet.local");

        var result = await NewService().UpdateAsync(
            user.Id, new UserEdit("", "khong-phai-email", SystemRole.Member), _actorId);

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
                SystemRole.Member),
            _actorId);

        Assert.Equal(UserOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_ends_the_sessions_when_the_role_changes()
    {
        // Capabilities travel inside the access token, so without this the person keeps the rights
        // they had until it expires.
        var user = GivenUser(role: SystemRole.Member);
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        await NewService().UpdateAsync(
            user.Id, new UserEdit(user.FullName, user.Email, SystemRole.Secretary), _actorId);

        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task Update_keeps_the_sessions_when_the_role_is_unchanged()
    {
        // Correcting a typo in someone's name should not sign them out.
        var user = GivenUser(role: SystemRole.Secretary);
        await _refreshTokens.StoreAsync("rt-cua-ho", user.Id, TimeSpan.FromDays(7));

        await NewService().UpdateAsync(
            user.Id, new UserEdit("Tên Đã Sửa", user.Email, SystemRole.Secretary), _actorId);

        Assert.Single(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task Update_refuses_to_let_an_administrator_drop_their_own_admin_role()
    {
        // The same lockout as deactivating yourself: with one organisation there may be no second
        // administrator to undo it.
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", SystemRole.Admin);

        var result = await NewService().UpdateAsync(
            admin.Id, new UserEdit(admin.FullName, admin.Email, SystemRole.Member), admin.Id);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        Assert.Equal(SystemRole.Admin, admin.Role);
    }

    [Fact]
    public async Task Update_lets_an_administrator_edit_their_own_name()
    {
        // Only the role is protected — an administrator is not locked out of their own profile.
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", SystemRole.Admin);

        var result = await NewService().UpdateAsync(
            admin.Id, new UserEdit("Phạm Quản Trị (mới)", admin.Email, SystemRole.Admin), admin.Id);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Update_lets_one_administrator_demote_another()
    {
        var other = GivenUser("Quản Trị Hai", "quantri2@trustcabinet.local", SystemRole.Admin);

        var result = await NewService().UpdateAsync(
            other.Id, new UserEdit(other.FullName, other.Email, SystemRole.Member), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(SystemRole.Member, other.Role);
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
        // The <c>:Session Service</c> step: a row marked inactive whose tokens still work is not
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
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", SystemRole.Admin);

        var result = await NewService().ChangeStatusAsync(admin.Id, isActive: false, admin.Id);

        Assert.Equal(UserOutcome.StatusChangeNotAllowed, result.Outcome);
        // Still usable: recovering from this would otherwise need a database console.
        Assert.True(admin.IsActive);
        Assert.Empty(_audit.Records);
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
