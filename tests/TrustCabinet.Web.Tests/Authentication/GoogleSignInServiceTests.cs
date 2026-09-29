using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Infrastructure.Authentication;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// UC-02's inner alt block: the four ways a verified Google identity can still be refused, and the
/// one way it is accepted. No network and no real ID token — the verifier is faked, because what
/// is under test is what the system does with an identity Google has already vouched for.
/// </summary>
public class GoogleSignInServiceTests
{
    private const string IdToken = "id-token-hop-le";
    private const string GoogleSubject = "115742...-google-sub";
    private const string GoogleEmail = "thuky@trustcabinet.local";

    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeTokenIssuer _tokenIssuer = new();
    private readonly FakeGoogleTokenVerifier _google = new();
    private readonly FakeAuditService _audit = new();

    private AuthenticationService NewService() => new(
        _users, new FakePasswordHasher(), _tokenIssuer, _refreshTokens, _google, _audit,
        NullLogger<AuthenticationService>.Instance);

    /// <summary>
    /// A Google-only account: no password at all, which is exactly what the migration in this
    /// story made possible.
    /// </summary>
    private static User NewGoogleUser(
        string? googleSubjectId = GoogleSubject,
        Guid? role = null,
        bool isActive = true) => new()
        {
            FullName = "Nguyễn Thư Ký",
            Email = GoogleEmail,
            PasswordHash = null,
            GoogleSubjectId = googleSubjectId,
            RoleId = role ?? BuiltInRoles.Secretary,
            IsActive = isActive,
        };

    // ----------------------------------------------------------------------- branch: success

    [Fact]
    public async Task A_linked_active_account_signs_in()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(NewGoogleUser()).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        Assert.Equal(GoogleSignInOutcome.Success, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal([Capability.MinutesEditor], result.Capabilities);
    }

    [Fact]
    public async Task Google_sign_in_issues_the_same_token_pair_as_a_password_sign_in()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(NewGoogleUser()).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Contains(result.RefreshToken!, _refreshTokens.ActiveTokens);
        Assert.True(result.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task An_account_with_both_a_password_and_a_google_link_can_use_either()
    {
        var user = NewGoogleUser();
        user.PasswordHash = new FakePasswordHasher().Hash("MatKhau@2026");

        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);
        var service = NewService();

        Assert.Equal(
            GoogleSignInOutcome.Success,
            (await service.SignInWithGoogleAsync(IdToken)).Outcome);

        Assert.Equal(
            SignInOutcome.Success,
            (await service.SignInAsync(GoogleEmail, "MatKhau@2026")).Outcome);
    }

    [Fact]
    public async Task A_member_signs_in_with_google_although_they_have_no_capability()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(NewGoogleUser(role: BuiltInRoles.Member));

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        // Same exemption as password sign-in: a plain Member carries no capability by design.
        Assert.Equal(GoogleSignInOutcome.Success, result.Outcome);
        Assert.Empty(result.Capabilities!);
    }

    // ------------------------------------------------------------ branch: MSG03, bad ID token

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("khong-phai-id-token")]
    public async Task A_token_that_does_not_verify_is_rejected(string idToken)
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(NewGoogleUser()).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(idToken);

        Assert.Equal(GoogleSignInOutcome.InvalidGoogleToken, result.Outcome);
        Assert.Null(result.User);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task The_database_is_not_consulted_when_the_token_does_not_verify()
    {
        var result = await NewService().SignInWithGoogleAsync("khong-phai-id-token");

        Assert.Equal(GoogleSignInOutcome.InvalidGoogleToken, result.Outcome);
        // Verification comes first: an unverified token's `sub` is just a string the caller chose,
        // and looking an account up by it would be acting on unauthenticated input.
        Assert.Equal(0, _users.LookupCount);
    }

    // -------------------------------------------------------------- branch: MSG05, not linked

    [Fact]
    public async Task A_google_account_nobody_linked_is_refused()
    {
        _google.WithValidToken(IdToken, "mot-sub-chua-ai-lien-ket", "nguoi.la@gmail.com");

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        // No account is created. Provisioning is an administrator's job — self-registration here
        // would let anybody with a Google account in.
        Assert.Equal(GoogleSignInOutcome.NotLinked, result.Outcome);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task An_account_created_with_that_email_is_linked_at_its_first_google_sign_in()
    {
        // An administrator created the account with this Gmail; nobody has linked it (ADR-0009).
        var user = NewGoogleUser(googleSubjectId: null);
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail.ToUpperInvariant());
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        Assert.Equal(GoogleSignInOutcome.Success, result.Outcome);
        Assert.Equal(GoogleSubject, user.GoogleSubjectId);
        var record = Assert.Single(_audit.Records);
        Assert.Equal(AuditActions.UserGoogleLinked, record.Action);
        Assert.Equal(user.Id, record.ObjectId);
        Assert.Equal(user.Id, record.ActorId);
    }

    [Fact]
    public async Task An_account_already_linked_to_another_google_identity_is_not_relinked()
    {
        // Same email, different subject: the address was changed or reassigned after the account
        // was linked. The subject decides from the first sign-in on, so the new holder of the
        // address does not get the previous owner's account.
        _google.WithValidToken(IdToken, "sub-cua-nguoi-khac", GoogleEmail);
        var user = NewGoogleUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        Assert.Equal(GoogleSignInOutcome.NotLinked, result.Outcome);
        Assert.Equal(GoogleSubject, user.GoogleSubjectId);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task An_account_deactivated_before_its_first_google_sign_in_is_refused_as_deactivated()
    {
        var user = NewGoogleUser(googleSubjectId: null, isActive: false);
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        // MSG02, not MSG05: the account exists and was deactivated, so "not authorised" would be
        // the wrong thing to tell them. Still not linked.
        Assert.Equal(GoogleSignInOutcome.AccountDeactivated, result.Outcome);
        Assert.Null(user.GoogleSubjectId);
        Assert.Empty(_audit.Records);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    [Fact]
    public async Task A_deactivated_account_linked_to_another_google_identity_stays_not_linked()
    {
        // Same email, different subject: the caller is not that account's owner, so it learns
        // nothing about the account's state.
        _google.WithValidToken(IdToken, "sub-cua-nguoi-khac", GoogleEmail);
        _users.WithUser(NewGoogleUser(isActive: false))
            .WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        Assert.Equal(GoogleSignInOutcome.NotLinked, result.Outcome);
    }

    // ------------------------------------------------------------ branch: MSG02, deactivated

    [Fact]
    public async Task A_deactivated_linked_account_is_refused()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(NewGoogleUser(isActive: false))
            .WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        // MSG02, the same code UC-01 answers for a deactivated account.
        Assert.Equal(GoogleSignInOutcome.AccountDeactivated, result.Outcome);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }

    // ------------------------------------------- no "no system access" branch any more (SCRUM-63)

    [Fact]
    public async Task A_linked_account_whose_role_grants_nothing_signs_in()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        // role_capabilities emptied for Secretary — works like Member now, as in UC-01.
        _users.WithUser(NewGoogleUser());

        var result = await NewService().SignInWithGoogleAsync(IdToken);

        Assert.Equal(GoogleSignInOutcome.Success, result.Outcome);
        Assert.Empty(result.Capabilities!);
    }

    // ------------------------------------------------------------------------ cross-cutting

    [Fact]
    public async Task A_google_only_account_cannot_be_signed_into_with_a_password()
    {
        _users.WithUser(NewGoogleUser()).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);

        // PasswordHash is null. The password path must treat that as "no password matches",
        // not throw and not somehow succeed on an empty credential.
        foreach (var attempt in new[] { "", "   ", "MatKhau@2026", "null" })
        {
            var result = await NewService().SignInAsync(GoogleEmail, attempt);
            Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        }
    }

    [Fact]
    public async Task A_session_started_with_google_refreshes_like_any_other()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(NewGoogleUser()).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);
        var service = NewService();

        var signIn = await service.SignInWithGoogleAsync(IdToken);
        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        // How a session was established does not change what it is — a Google-only account has no
        // password, and refresh must not go looking for one.
        Assert.Equal(SignInOutcome.Success, refreshed.Outcome);
        Assert.NotEqual(signIn.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Signing_out_of_a_google_session_destroys_it_server_side()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        _users.WithUser(NewGoogleUser()).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);
        var service = NewService();

        var signIn = await service.SignInWithGoogleAsync(IdToken);
        await service.SignOutAsync(signIn.RefreshToken!);

        Assert.Empty(_refreshTokens.ActiveTokens);
        Assert.Equal(
            SignInOutcome.InvalidCredentials,
            (await service.RefreshAsync(signIn.RefreshToken!)).Outcome);
    }

    [Fact]
    public async Task Deactivating_a_google_account_ends_its_session_at_the_next_refresh()
    {
        _google.WithValidToken(IdToken, GoogleSubject, GoogleEmail);
        var user = NewGoogleUser();
        _users.WithUser(user).WithCapabilities(BuiltInRoles.Secretary, Capability.MinutesEditor);
        var service = NewService();

        var signIn = await service.SignInWithGoogleAsync(IdToken);
        user.IsActive = false;

        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        Assert.Equal(SignInOutcome.AccountDeactivated, refreshed.Outcome);
        Assert.Empty(_refreshTokens.ActiveTokens);
    }
}
