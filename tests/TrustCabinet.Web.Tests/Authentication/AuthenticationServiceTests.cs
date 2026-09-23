using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Authentication;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// The four branches of UC-01's alt block, and the rules that are easy to break without noticing.
/// No database and no Redis: the decision logic is what these pin down.
/// </summary>
public class AuthenticationServiceTests
{
    private const string Password = "MatKhau@2026";

    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeTokenIssuer _tokenIssuer = new();
    private readonly FakeGoogleTokenVerifier _google = new();
    private readonly FakePasswordHasher _passwordHasher = new();

    private AuthenticationService NewService() => new(
        _users, _passwordHasher, _tokenIssuer, _refreshTokens, _google, new FakeAuditService(),
        NullLogger<AuthenticationService>.Instance);

    private static User NewUser(
        string email = "thuky@trustcabinet.local",
        Guid? role = null,
        bool isActive = true,
        string password = Password) => new()
        {
            FullName = "Nguyễn Thư Ký",
            Email = email,
            PasswordHash = new FakePasswordHasher().Hash(password),
            RoleId = role ?? BuiltInRoles.Secretary,
            IsActive = isActive,
        };

    // ---------------------------------------------------------------- branch 4: authorized

    [Fact]
    public async Task An_active_user_with_the_right_password_signs_in()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary,
            Capability.MeetingCreator, Capability.MinutesEditor);

        var result = await NewService().SignInAsync(user.Email, Password);

        Assert.Equal(SignInOutcome.Success, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal(user.Id, result.User!.Id);
        Assert.Equal([Capability.MeetingCreator, Capability.MinutesEditor], result.Capabilities);
    }

    [Fact]
    public async Task Signing_in_issues_an_access_token_and_a_stored_refresh_token()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);

        var result = await NewService().SignInAsync(user.Email, Password);

        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);

        // The refresh token is only worth anything because the server remembers it — that is what
        // makes server-side sign-out possible (SCRUM-11).
        Assert.Contains(result.RefreshToken!, _refreshTokens.ActiveTokens);

        // Expiry is reported so the client can refresh before a call fails, and it has to be in
        // the future or the token is useless on arrival.
        Assert.True(result.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Capabilities_come_from_the_database_not_from_the_role_name()
    {
        // Deliberately not the seeded mapping: if the service derived capabilities from the role
        // name instead of reading role_capabilities, this would come back with the real Chair set
        // and the administrator screen in SCRUM-60 could never change anything.
        var user = NewUser(role: BuiltInRoles.Chair);
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Chair, Capability.AdminUsers);

        var result = await NewService().SignInAsync(user.Email, Password);

        Assert.Equal([Capability.AdminUsers], result.Capabilities);
    }

    [Fact]
    public async Task A_member_signs_in_although_they_have_no_capability()
    {
        // Member carries no capability by design — attending, reading and voting need none.
        var user = NewUser(role: BuiltInRoles.Member);
        _users.WithUser(user);

        var result = await NewService().SignInAsync(user.Email, Password);

        Assert.Equal(SignInOutcome.Success, result.Outcome);
        Assert.Empty(result.Capabilities!);
    }

    [Fact]
    public async Task Email_case_and_surrounding_spaces_do_not_prevent_sign_in()
    {
        var user = NewUser(email: "thuky@trustcabinet.local");
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);

        var result = await NewService().SignInAsync("  ThuKy@TrustCabinet.Local  ", Password);

        // The email column is citext and a typed address often arrives with a stray space or a
        // capitalised first letter from a phone keyboard; neither is a different account.
        Assert.Equal(SignInOutcome.Success, result.Outcome);
    }

    // ------------------------------------------------- branch 1: invalid username or password

    [Fact]
    public async Task An_unknown_email_is_rejected()
    {
        var result = await NewService().SignInAsync("khong-ton-tai@trustcabinet.local", Password);

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
    }

    [Fact]
    public async Task A_wrong_password_is_rejected()
    {
        var user = NewUser();
        _users.WithUser(user);

        var result = await NewService().SignInAsync(user.Email, "SaiMatKhau@2026");

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
    }

    [Fact]
    public async Task An_unknown_email_and_a_wrong_password_are_indistinguishable()
    {
        _users.WithUser(NewUser());

        var unknownEmail = await NewService().SignInAsync("nobody@trustcabinet.local", Password);
        var wrongPassword = await NewService().SignInAsync("thuky@trustcabinet.local", "SaiMatKhau@2026");

        // The acceptance criterion in SCRUM-11: a failed sign-in must not reveal whether the email
        // is registered. Same outcome, and no user object on either.
        Assert.Equal(unknownEmail.Outcome, wrongPassword.Outcome);
        Assert.Null(unknownEmail.User);
        Assert.Null(wrongPassword.User);
    }

    [Fact]
    public async Task An_unknown_email_still_costs_one_password_verification()
    {
        // The observable part of the anti-enumeration defence, and the part the assertions above
        // do NOT cover: both branches return InvalidCredentials by construction, so that test
        // keeps passing even with the defence deleted. What actually hides the difference is that
        // the unknown-email path pays for a hash too, against the dummy hash.
        //
        // Counted rather than timed: the property is "verification happened", and a wall-clock
        // assertion would be flaky on a busy machine while proving no more.
        await NewService().SignInAsync("nobody@trustcabinet.local", Password);

        Assert.Equal(1, _passwordHasher.VerifyCount);
    }

    [Fact]
    public async Task The_dummy_hash_comes_from_the_injected_hasher()
    {
        // Counting verifications is not enough on its own: the dummy hash also has to be something
        // this hasher can actually verify against. It used to be built from `new PasswordHasher()`
        // regardless of what was injected, so under a test double the two disagreed — the fake was
        // handed a hash of a shape it never produces, and the unknown-email branch was checking a
        // pairing that does not occur in production.
        //
        // Hashing through the injected hasher is what this pins: one Hash call, on the path where
        // no user was found, before the verify.
        await NewService().SignInAsync("nobody@trustcabinet.local", Password);

        Assert.Equal(1, _passwordHasher.HashCount);
        Assert.All(_passwordHasher.VerifiedHashes, hash => Assert.StartsWith("hashed:", hash));
    }

    [Fact]
    public async Task The_dummy_hash_never_matches_a_password()
    {
        // It is a hash of a fresh random value, so nothing a caller can send can match it. If it
        // ever did, an unknown email would sign in.
        var service = NewService();

        var first = await service.SignInAsync("nobody@trustcabinet.local", Password);
        var second = await service.SignInAsync("nobody@trustcabinet.local", "");

        Assert.Equal(SignInOutcome.InvalidCredentials, first.Outcome);
        Assert.Equal(SignInOutcome.InvalidCredentials, second.Outcome);
    }

    [Fact]
    public async Task A_known_email_and_an_unknown_one_cost_the_same()
    {
        _users.WithUser(NewUser());
        var service = NewService();

        await service.SignInAsync("thuky@trustcabinet.local", "SaiMatKhau@2026");
        var afterKnownEmail = _passwordHasher.VerifyCount;

        await service.SignInAsync("nobody@trustcabinet.local", "SaiMatKhau@2026");
        var afterUnknownEmail = _passwordHasher.VerifyCount - afterKnownEmail;

        // Hashing is deliberately slow, so skipping it when no user is found would make "no such
        // account" measurably faster to answer than "wrong password" — enough of a difference to
        // enumerate which emails are registered.
        Assert.Equal(afterKnownEmail, afterUnknownEmail);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("   ", Password)]
    [InlineData("thuky@trustcabinet.local", "")]
    [InlineData("thuky@trustcabinet.local", "   ")]
    public async Task Blank_input_is_rejected_without_touching_the_database(string email, string password)
    {
        _users.WithUser(NewUser());

        var result = await NewService().SignInAsync(email, password);

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        // An empty password must never be able to match a stored hash, so the guard comes before
        // the lookup rather than relying on the hasher to say no.
        Assert.Equal(0, _users.LookupCount);
    }

    [Fact]
    public async Task A_failed_attempt_issues_nothing()
    {
        _users.WithUser(NewUser());

        var result = await NewService().SignInAsync("thuky@trustcabinet.local", "SaiMatKhau@2026");

        Assert.Null(result.AccessToken);
        Assert.Null(result.RefreshToken);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task A_placeholder_password_hash_cannot_be_signed_into()
    {
        // Until SCRUM-11 the seeder wrote "SET-BY-SCRUM-11" into password_hash. A hash the hasher
        // cannot parse has to fail verification rather than throw — and rather than match.
        var user = NewUser();
        user.PasswordHash = "SET-BY-SCRUM-11";
        _users.WithUser(user);

        var service = new AuthenticationService(
            _users, new PasswordHasher(), _tokenIssuer, _refreshTokens, _google, new FakeAuditService(),
            NullLogger<AuthenticationService>.Instance);

        var result = await service.SignInAsync(user.Email, "SET-BY-SCRUM-11");

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
    }

    // ------------------------------------------------------- branch 2: account is deactivated

    [Fact]
    public async Task A_deactivated_account_is_told_so_only_after_the_password_matched()
    {
        var user = NewUser(isActive: false);
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);

        var result = await NewService().SignInAsync(user.Email, Password);

        Assert.Equal(SignInOutcome.AccountDeactivated, result.Outcome);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task A_deactivated_account_with_a_wrong_password_reveals_nothing()
    {
        var user = NewUser(isActive: false);
        _users.WithUser(user);

        var result = await NewService().SignInAsync(user.Email, "SaiMatKhau@2026");

        // Credentials are checked first on purpose: answering MSG03 here would confirm the address
        // belongs to a real, if disabled, account to someone who does not know the password.
        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
    }

    // ------------------------------------------- no "no system access" branch any more (SCRUM-63)

    [Fact]
    public async Task A_role_that_grants_nothing_still_signs_in()
    {
        // role_capabilities emptied for Secretary. MSG04 used to refuse this unless the role was
        // Member; since roles are data (ADR-0008) a role with nothing granted is a legitimate
        // configuration that works like Member, and telling them apart would mean comparing role
        // ids in business code.
        var user = NewUser(role: BuiltInRoles.Secretary);
        _users.WithUser(user);

        var result = await NewService().SignInAsync(user.Email, Password);

        Assert.Equal(SignInOutcome.Success, result.Outcome);
        Assert.Empty(result.Capabilities!);
    }

    [Fact]
    public async Task A_custom_role_signs_in_with_its_name_and_capabilities()
    {
        var deputy = new Role { Name = "Thư ký phó" };
        _users.WithRole(deputy, Capability.MinutesEditor);

        var user = NewUser(role: deputy.Id);
        _users.WithUser(user);

        var result = await NewService().SignInAsync(user.Email, Password);

        Assert.Equal(SignInOutcome.Success, result.Outcome);
        Assert.Equal([Capability.MinutesEditor], result.Capabilities);
        Assert.Equal("Thư ký phó", result.User!.Role!.Name);
    }

    // ------------------------------------------------------------------------------ refresh

    [Fact]
    public async Task A_refresh_token_can_be_exchanged_for_a_new_pair()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);
        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        Assert.Equal(SignInOutcome.Success, refreshed.Outcome);
        Assert.NotNull(refreshed.AccessToken);
        Assert.NotEqual(signIn.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task A_refresh_token_works_only_once()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);
        await service.RefreshAsync(signIn.RefreshToken!);

        var replayed = await service.RefreshAsync(signIn.RefreshToken!);

        // Rotation: a stolen refresh token stops working the moment the real client refreshes.
        Assert.Equal(SignInOutcome.InvalidCredentials, replayed.Outcome);
    }

    [Fact]
    public async Task An_expired_refresh_token_is_rejected()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);
        _refreshTokens.Now = DateTimeOffset.UtcNow.Add(_tokenIssuer.RefreshTokenLifetime).AddSeconds(1);

        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        // "The session expires after a configurable time" (SCRUM-11) — the refresh token is what
        // bounds it, so it has to actually stop working.
        Assert.Equal(SignInOutcome.InvalidCredentials, refreshed.Outcome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("khong-phai-token")]
    public async Task A_missing_or_unknown_refresh_token_is_rejected(string token)
    {
        var result = await NewService().RefreshAsync(token);

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
    }

    [Fact]
    public async Task Deactivating_an_account_ends_its_session_at_the_next_refresh()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);

        // An administrator deactivates them while they are signed in (SCRUM-13).
        user.IsActive = false;

        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        Assert.Equal(SignInOutcome.AccountDeactivated, refreshed.Outcome);
        // Every token they held, not just the one presented — otherwise a second refresh token
        // would keep the session alive indefinitely.
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task Losing_every_capability_reaches_the_next_access_token()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);

        // role_capabilities emptied for their role while they were signed in.
        _users.WithCapabilities(BuiltInRoles.Secretary);

        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        // Still a session — a role with nothing granted works like Member — but the new token
        // carries none of the capabilities that were taken away.
        Assert.Equal(SignInOutcome.Success, refreshed.Outcome);
        Assert.Empty(refreshed.Capabilities!);
    }

    [Fact]
    public async Task A_capability_granted_meanwhile_arrives_with_the_next_access_token()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);
        _users.WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator, Capability.MinutesEditor);

        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        // Capabilities live in the token, so a change only lands at the next refresh. That delay
        // is the trade-off for not querying the database on every request.
        Assert.Equal(
            [Capability.MeetingCreator, Capability.MinutesEditor],
            refreshed.Capabilities);
    }

    // ----------------------------------------------------------------------------- sign-out

    [Fact]
    public async Task Signing_out_destroys_the_session_on_the_server()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);
        await service.SignOutAsync(signIn.RefreshToken!);

        Assert.Empty(_refreshTokens.ActiveTokens);

        // "Sign-out destroys the session server-side, not just in the browser" (SCRUM-11): the
        // token has to be dead even for a client that kept a copy of it.
        var afterSignOut = await service.RefreshAsync(signIn.RefreshToken!);
        Assert.Equal(SignInOutcome.InvalidCredentials, afterSignOut.Outcome);
    }

    [Fact]
    public async Task Signing_out_twice_is_not_an_error()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var signIn = await service.SignInAsync(user.Email, Password);

        await service.SignOutAsync(signIn.RefreshToken!);
        await service.SignOutAsync(signIn.RefreshToken!);
        await service.SignOutAsync("");
    }

    [Fact]
    public async Task Signing_out_one_session_leaves_the_others_alone()
    {
        var user = NewUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MeetingCreator);
        var service = NewService();

        var laptop = await service.SignInAsync(user.Email, Password);
        var phone = await service.SignInAsync(user.Email, Password);

        await service.SignOutAsync(laptop.RefreshToken!);

        // Signing out of one device must not sign you out of the others.
        Assert.Equal([phone.RefreshToken!], _refreshTokens.ActiveTokens);
    }
}
