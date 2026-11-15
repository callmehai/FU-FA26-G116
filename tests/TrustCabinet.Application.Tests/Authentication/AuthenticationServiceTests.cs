using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Authentication;

public sealed class AuthenticationServiceTests
{
    private readonly FakeAccounts _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeGoogleTokenVerifier _google = new();
    private readonly FakeAuditService _audit = new();

    private AuthenticationService Service() => new(
        _users, _unitOfWork, new FakePasswordHasher(), new FakeTokenIssuer(), _refreshTokens, _google, _audit,
        NullLogger<AuthenticationService>.Instance);

    [Fact]
    public async Task Sign_in_issues_a_token_pair_and_reads_the_account_without_a_transaction()
    {
        var user = _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);

        var result = await Service().SignInAsync(" admin@trustcabinet.local ", "secret");

        Assert.True(result.Succeeded);
        Assert.Equal([Capability.AdminUsers], result.Capabilities);
        Assert.Equal(user.Id, _refreshTokens.Live[result.RefreshToken!]);
        Assert.Equal([AuditActions.SignedIn], _audit.Actions);
        // The account and then its capabilities, each on its own.
        Assert.Equal([TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_wrong_password_is_refused_like_an_unknown_email()
    {
        _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);

        var wrongPassword = await Service().SignInAsync("admin@trustcabinet.local", "nope");
        var unknownEmail = await Service().SignInAsync("ai@trustcabinet.local", "secret");

        Assert.Equal(SignInOutcome.InvalidCredentials, wrongPassword.Outcome);
        Assert.Equal(SignInOutcome.InvalidCredentials, unknownEmail.Outcome);
        Assert.Empty(_refreshTokens.Live);
    }

    [Fact]
    public async Task A_storage_failure_during_refresh_reaches_the_caller_and_leaves_the_token_usable()
    {
        var user = _users.Add("admin@trustcabinet.local", BuiltInRoles.Admin);
        var signIn = await Service().SignInAsync(user.Email, "secret");
        _users.Fail = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Service().RefreshAsync(signIn.RefreshToken!));

        Assert.Contains(signIn.RefreshToken!, _refreshTokens.Live.Keys);
    }

    [Fact]
    public async Task The_first_google_sign_in_links_the_account_with_that_email()
    {
        var user = _users.Add("chutoa@gmail.com", BuiltInRoles.Member, passwordHash: null);
        _google.Identity = new GoogleIdentity("google-subject", "chutoa@gmail.com");

        var result = await Service().SignInWithGoogleAsync("id-token");

        Assert.True(result.Succeeded);
        Assert.Equal("google-subject", user.GoogleSubjectId);
        Assert.Equal([AuditActions.UserGoogleLinked, AuditActions.SignedIn], _audit.Actions);
    }
}
