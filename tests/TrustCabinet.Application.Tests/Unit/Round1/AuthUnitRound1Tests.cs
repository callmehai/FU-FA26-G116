using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 causation tests for FE-01 (Authentication): one xUnit test per kept UTC below.
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// Tester: Phạm Gia Khánh.
public sealed class AuthUnitRound1Tests
{
    private readonly FakeAccounts _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeGoogleTokenVerifier _google = new();
    private readonly FakeAuditService _audit = new();

    private AuthenticationService AuthService() => new(
        _users, _unitOfWork, new FakePasswordHasher(), new FakeTokenIssuer(), _refreshTokens, _google, _audit,
        NullLogger<AuthenticationService>.Instance);

    [Fact(DisplayName = "AuthenticationService.SignInAsync/UTC001 active user with the right password signs in")]
    [Trait("UTC", "AuthenticationService.SignInAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task SignInAsync_UTC001()
    {
        var user = _users.Add("organizer@example.com", BuiltInRoles.Member);

        var result = await AuthService().SignInAsync("organizer@example.com", "secret");

        Assert.Equal(SignInOutcome.Success, result.Outcome);
        Assert.Equal(user.Id, _refreshTokens.Live[result.RefreshToken!]);
        Assert.Equal([AuditActions.SignedIn], _audit.Actions);
    }

    [Fact(DisplayName = "AuthenticationService.SignInAsync/UTC002 unknown email is refused like a wrong password")]
    [Trait("UTC", "AuthenticationService.SignInAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task SignInAsync_UTC002()
    {
        var result = await AuthService().SignInAsync("nobody@example.com", "anything");

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        Assert.Empty(_audit.Actions);
        Assert.Empty(_refreshTokens.Live);
    }

    [Fact(DisplayName = "AuthenticationService.SignInAsync/UTC003 deactivated account with the right password is told so")]
    [Trait("UTC", "AuthenticationService.SignInAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task SignInAsync_UTC003()
    {
        _users.Add("former@example.com", BuiltInRoles.Member, isActive: false);

        var result = await AuthService().SignInAsync("former@example.com", "secret");

        Assert.Equal(SignInOutcome.AccountDeactivated, result.Outcome);
        Assert.Null(result.RefreshToken);
        Assert.Equal([AuditActions.SignInFailed], _audit.Actions);
    }

    [Fact(DisplayName = "AuthenticationService.RefreshAsync/UTC001 live refresh token rotates the token pair")]
    [Trait("UTC", "AuthenticationService.RefreshAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RefreshAsync_UTC001()
    {
        // Deviation kept in the UTC note: executed with the role's real capabilities,
        // and the re-read behaviour held.
        var user = _users.Add("organizer@example.com", BuiltInRoles.Member);
        var signIn = await AuthService().SignInAsync(user.Email, "secret");

        var result = await AuthService().RefreshAsync(signIn.RefreshToken!);

        Assert.Equal(SignInOutcome.Success, result.Outcome);
        Assert.NotEqual(signIn.RefreshToken, result.RefreshToken);
        Assert.DoesNotContain(signIn.RefreshToken!, _refreshTokens.Live.Keys);
    }

    [Fact(DisplayName = "AuthenticationService.RefreshAsync/UTC003 deactivated account loses every refresh token")]
    [Trait("UTC", "AuthenticationService.RefreshAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RefreshAsync_UTC003()
    {
        var user = _users.Add("organizer@example.com", BuiltInRoles.Member);
        var signIn = await AuthService().SignInAsync(user.Email, "secret");
        user.IsActive = false;

        var result = await AuthService().RefreshAsync(signIn.RefreshToken!);

        Assert.Equal(SignInOutcome.AccountDeactivated, result.Outcome);
        Assert.DoesNotContain(signIn.RefreshToken!, _refreshTokens.Live.Keys);
        Assert.Empty(_refreshTokens.Live);
    }

    [Fact(DisplayName = "AuthenticationService.SignInWithGoogleAsync/UTC001 linked active account signs in")]
    [Trait("UTC", "AuthenticationService.SignInWithGoogleAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task SignInWithGoogleAsync_UTC001()
    {
        var user = _users.Add("chutoa@gmail.com", BuiltInRoles.Member, passwordHash: null);
        user.GoogleSubjectId = "google-subject";
        _google.Identity = new GoogleIdentity("google-subject", "chutoa@gmail.com");

        var result = await AuthService().SignInWithGoogleAsync("id-token");

        Assert.Equal(GoogleSignInOutcome.Success, result.Outcome);
        Assert.Equal(user.Id, _refreshTokens.Live[result.RefreshToken!]);
        Assert.Equal([AuditActions.SignedIn], _audit.Actions);
    }

    [Fact(DisplayName = "AuthenticationService.SignInWithGoogleAsync/UTC003 unknown identity is not provisioned")]
    [Trait("UTC", "AuthenticationService.SignInWithGoogleAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task SignInWithGoogleAsync_UTC003()
    {
        _users.Add("chutoa@gmail.com", BuiltInRoles.Member, passwordHash: null);
        _google.Identity = new GoogleIdentity("stranger-subject", "stranger@gmail.com");

        var result = await AuthService().SignInWithGoogleAsync("id-token");

        Assert.Equal(GoogleSignInOutcome.NotLinked, result.Outcome);
        Assert.Empty(_audit.Actions);
        Assert.DoesNotContain(_users.Users, u => u.GoogleSubjectId == "stranger-subject");
    }

    [Fact(DisplayName = "AuthenticationService.SignOutAsync/UTC001 live refresh token is revoked")]
    [Trait("UTC", "AuthenticationService.SignOutAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task SignOutAsync_UTC001()
    {
        var user = _users.Add("organizer@example.com", BuiltInRoles.Member);
        var signIn = await AuthService().SignInAsync(user.Email, "secret");

        await AuthService().SignOutAsync(signIn.RefreshToken!);

        Assert.DoesNotContain(signIn.RefreshToken!, _refreshTokens.Live.Keys);
        Assert.Empty(_refreshTokens.Live);
    }

    [Fact(DisplayName = "AuthenticationService.SignOutAsync/UTC003 blank token touches no store")]
    [Trait("UTC", "AuthenticationService.SignOutAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task SignOutAsync_UTC003()
    {
        await AuthService().SignOutAsync("");

        Assert.Empty(_refreshTokens.Live);
        Assert.Empty(_refreshTokens.RevokedAll);
    }

    private static readonly DateTimeOffset GuestNow = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeGuestSessionRepository _sessions = new();
    private readonly FakeGuestCredentials _credentials = new();
    private readonly FakeGuestCodeSender _sender = new();
    private readonly FakeUnitOfWork _guestWork = new();
    private readonly FakeAuditLog _guestAudit = new();
    private static readonly GuestAccessOptions Limits = new();

    private GuestAccessService GuestService() => new(_sessions, _credentials, Options.Create(Limits), _sender,
        _guestWork, _guestAudit, new FixedClock(GuestNow), NullLogger<GuestAccessService>.Instance);

    private MeetingGuestInvitation GivenInvitation(string email = "nguoimoi@example.com")
    {
        _sessions.Now = GuestNow;
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = MeetingStatus.Published,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = GuestNow.AddHours(-1),
            PlannedEndTime = GuestNow.AddHours(1),
        };
        var invitation = new MeetingGuestInvitation
        {
            MeetingId = meeting.Id, Meeting = meeting, InvitedName = "Khách mời",
            Email = email, InvitedById = Guid.CreateVersion7(),
        };
        _sessions.Invitations.Add(invitation);
        _credentials.ValidLinks.Add(invitation.Id);
        return invitation;
    }

    [Fact(DisplayName = "GuestAccessService.OpenAsync/UTC001 valid link opens the invitation view")]
    [Trait("UTC", "GuestAccessService.OpenAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task OpenAsync_UTC001()
    {
        var invitation = GivenInvitation();

        var result = await GuestService().OpenAsync(invitation.Id, "link", default);

        var view = Assert.IsType<GuestInvitationView>(result.Data);
        Assert.Equal("ng***@example.com", view.MaskedEmail);
        Assert.Equal("Họp giao ban", view.Meeting.Title);
        Assert.Empty(_guestAudit.Rows);
    }

    [Fact(DisplayName = "GuestAccessService.OpenAsync/UTC002 wrong link token reads nothing")]
    [Trait("UTC", "GuestAccessService.OpenAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task OpenAsync_UTC002()
    {
        var invitation = GivenInvitation();

        var result = await GuestService().OpenAsync(invitation.Id, "wrong-token", default);

        Assert.Equal(GuestAccessFailure.InvalidInvitation, result.Failure);
        Assert.Null(result.Data);
        Assert.Empty(_sessions.Locks);
    }

    [Fact(DisplayName = "GuestAccessService.SendCodeAsync/UTC001 first code is stored hashed and mailed")]
    [Trait("UTC", "GuestAccessService.SendCodeAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task SendCodeAsync_UTC001()
    {
        var invitation = GivenInvitation();

        var result = await GuestService().SendCodeAsync(invitation.Id, "link", default);

        var sent = Assert.IsType<GuestCodeSent>(result.Data);
        Assert.Equal((15 * 60, 60), (sent.ExpiresInSeconds, sent.ResendAfterSeconds));
        Assert.Equal(6, _sender.Calls[0].Code.Length);
        Assert.Equal((_sender.Calls[0].Code), _credentials.Hashes[^1].Code);
    }

    [Fact(DisplayName = "GuestAccessService.SendCodeAsync/UTC003 resend during the wait is refused")]
    [Trait("UTC", "GuestAccessService.SendCodeAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task SendCodeAsync_UTC003()
    {
        var invitation = GivenInvitation();
        var challenge = new GuestVerificationChallenge
        {
            InvitationId = invitation.Id, CreatedAt = GuestNow.AddSeconds(-20), UpdatedAt = GuestNow.AddSeconds(-20),
            ExpiresAt = GuestNow.AddMinutes(15), CodeHash = "",
        };
        challenge.CodeHash = _credentials.CodeHash(challenge.Id, "000000");
        _sessions.Challenges.Add(challenge);

        var result = await GuestService().SendCodeAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.TooSoon, result.Failure);
        Assert.Equal(40, result.RetryAfterSeconds);
        Assert.Empty(_sender.Calls);
    }

    [Fact(DisplayName = "GuestAccessService.VerifyAsync/UTC001 correct live code opens a session")]
    [Trait("UTC", "GuestAccessService.VerifyAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task VerifyAsync_UTC001()
    {
        var invitation = GivenInvitation();
        var challenge = new GuestVerificationChallenge
        {
            InvitationId = invitation.Id, CreatedAt = GuestNow, UpdatedAt = GuestNow,
            ExpiresAt = GuestNow.AddMinutes(15), CodeHash = "",
        };
        challenge.CodeHash = _credentials.CodeHash(challenge.Id, "123456");
        _sessions.Challenges.Add(challenge);

        var result = await GuestService().VerifyAsync(invitation.Id, "link", "123456", default);

        var verified = Assert.IsType<GuestVerified>(result.Data);
        Assert.Equal(GuestNow.AddHours(12), verified.ExpiresAt);
        Assert.Single(_sessions.Sessions);
        Assert.Equal([AuditActions.GuestVerified], _guestAudit.Rows.Select(r => r.Action));
    }

    [Fact(DisplayName = "GuestAccessService.VerifyAsync/UTC003 code with no live challenge is refused")]
    [Trait("UTC", "GuestAccessService.VerifyAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task VerifyAsync_UTC003()
    {
        var invitation = GivenInvitation();

        var result = await GuestService().VerifyAsync(invitation.Id, "link", "123456", default);

        Assert.Equal(GuestAccessFailure.InvalidCode, result.Failure);
        Assert.Null(result.RemainingAttempts);
        Assert.Empty(_sessions.Sessions);
    }
}
